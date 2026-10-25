using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GojoQuickDraw.Desktop;

public enum PipelineAction { Convert, Verify, Review, Stages, Probe, ExampleProbe, Setup, Catalog }
public sealed record PipelineOptions(string Repository, string Python, string Gojo, string Input, string Dataset,
    string Category = "church", int Limit = 25, bool AllRecords = false, bool Recursive = false, bool RetryErrors = false,
    double FitTolerance = 6, int MaxPoints = 20000, int MaxSpans = 256, string? KeyId = null, string? RunFolder = null,
    string InspectionOrder = "category", long InspectionSeed = 0, long? SamplingSeed = null);
public sealed record CommandPlan(PipelineAction Action, string Executable, string WorkingDirectory, IReadOnlyList<string> Arguments, string? Artifact);
public sealed record JobResult(int ExitCode, bool Cancelled, JsonElement? Data, string Error)
{
    public bool Successful => !Cancelled && ExitCode == 0;
    public bool RetainedErrors => !Cancelled && ExitCode == 1 && Data is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("storedErrors", out _);
}

public static class Pipeline
{
    public static CommandPlan Plan(PipelineAction action, PipelineOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Python)) throw new ArgumentException("Choose a Python executable in Setup.");
        if (!File.Exists(Path.Combine(options.Gojo, "experiments", "quickDraw", "QuickDraw.csproj")))
            throw new ArgumentException("Choose the linked Gojo checkout in Setup.");
        if (options.Limit < 1 || options.MaxPoints < 1 || options.MaxSpans < 1 || !double.IsFinite(options.FitTolerance) || options.FitTolerance <= 0)
            throw new ArgumentException("Counts and fitting tolerance must be positive.");
        var arguments = new List<string> { "-u", "-m", "gojo_quickdraw", "--gojo-root", Path.GetFullPath(options.Gojo) };
        var run = options.RunFolder ?? (action == PipelineAction.Setup ? Path.GetTempPath() : Workspace.RunFolder(options.Dataset));
        string? artifact = null;
        void Dataset()
        {
            if (!Directory.Exists(options.Dataset)) throw new ArgumentException("Choose an existing generated folder, or convert drawings first.");
            arguments.AddRange(["--dataset", Path.GetFullPath(options.Dataset)]);
        }
        void Ordering() => arguments.AddRange(["--order", options.InspectionOrder, "--seed", options.InspectionSeed.ToString(CultureInfo.InvariantCulture)]);
        switch (action)
        {
            case PipelineAction.Convert:
                if (!File.Exists(options.Input) && !Directory.Exists(options.Input)) throw new ArgumentException("Choose a QuickDraw input file or folder.");
                if (string.IsNullOrWhiteSpace(options.Category)) throw new ArgumentException("Choose at least one source category.");
                arguments.AddRange(["sample", "--input", Path.GetFullPath(options.Input), "--output", Path.GetFullPath(options.Dataset),
                    "--fit-tolerance", options.FitTolerance.ToString(CultureInfo.InvariantCulture), "--max-points", options.MaxPoints.ToString(),
                    "--max-spans", options.MaxSpans.ToString()]);
                foreach (var category in options.Category.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    arguments.AddRange(["--category", category]);
                if (!options.AllRecords) arguments.AddRange(["--limit", options.Limit.ToString()]);
                else arguments.Add("--all-records");
                if (options.SamplingSeed is { } samplingSeed) arguments.AddRange(["--seed", samplingSeed.ToString(CultureInfo.InvariantCulture)]);
                if (options.Recursive) arguments.Add("--recursive");
                if (options.RetryErrors) arguments.Add("--retry-errors");
                break;
            case PipelineAction.Verify:
                arguments.Add("verify"); Dataset(); Ordering();
                if (!options.AllRecords) arguments.AddRange(["--limit", options.Limit.ToString()]);
                break;
            case PipelineAction.Review:
                arguments.Add("review"); Dataset(); Ordering();
                artifact = Path.Combine(run, "evidence.html");
                arguments.AddRange(["--output", artifact, "--limit", options.Limit.ToString()]);
                break;
            case PipelineAction.Stages:
                arguments.Add("stages"); Dataset(); Ordering();
                var stages = Path.Combine(run, "stages");
                arguments.AddRange(["--output", stages]);
                if (!string.IsNullOrWhiteSpace(options.KeyId)) arguments.AddRange(["--key-id", options.KeyId]);
                else arguments.AddRange(["--limit", options.Limit.ToString()]);
                artifact = Path.Combine(stages, "review", "index.html");
                break;
            case PipelineAction.Probe:
            case PipelineAction.ExampleProbe:
                arguments.Add("probe");
                if (action == PipelineAction.ExampleProbe) arguments.Add("--example");
                else
                {
                    Dataset();
                    if (string.IsNullOrWhiteSpace(options.KeyId)) throw new ArgumentException("Select a drawing before running its machine probes.");
                    arguments.AddRange(["--key-id", options.KeyId]);
                }
                artifact = Path.Combine(run, "machine-probes.json");
                arguments.AddRange(["--output", artifact]);
                break;
            case PipelineAction.Setup: arguments.Add("doctor"); break;
            case PipelineAction.Catalog:
                arguments.Add("catalog"); Dataset(); Ordering();
                arguments.AddRange(["--limit", options.Limit.ToString()]);
                break;
        }
        return new(action, options.Python, options.Repository, arguments, artifact);
    }

    public static async Task<JobResult> Run(CommandPlan plan, Action<string> log, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(plan.Executable)
        {
            WorkingDirectory = plan.WorkingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.Environment["PYTHONUTF8"] = "1";
        foreach (var argument in plan.Arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        process.Start();
        using var stop = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        });
        var output = new StringBuilder();
        var errorOutput = new StringBuilder();
        async Task Read(StreamReader stream, StringBuilder target, bool progress)
        {
            while (await stream.ReadLineAsync() is { } line)
            {
                // CLI results are bounded by the selected drawing count. Retain a useful diagnostic tail.
                target.AppendLine(line);
                if (target.Length > 8_000_000) target.Remove(0, target.Length - 4_000_000);
                if (progress) log(line);
            }
        }
        await Task.WhenAll(Read(process.StandardOutput, output, false), Read(process.StandardError, errorOutput, true), process.WaitForExitAsync());
        JsonElement? data = null;
        try { using var document = JsonDocument.Parse(output.ToString()); data = document.RootElement.Clone(); }
        catch (JsonException) { }
        if (data is not null) log(output.ToString());
        var error = errorOutput.ToString();
        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(error)) error = output.ToString();
        return new(process.ExitCode, cancellationToken.IsCancellationRequested, data, error);
    }
}
