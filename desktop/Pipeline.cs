using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GojoQuickDraw.Desktop;

public enum PipelineAction { Convert, Verify, Review, Stages, Probe, ExampleProbe, Setup, Catalog, Analyze, Gallery, Refresh, Train, Encode, Refine, RefineReview }
public sealed record PipelineOptions(string Repository, string Python, string Gojo, string Input, string Dataset,
    string Category = "church", int Limit = 25, bool AllRecords = false, bool Recursive = false, bool RetryErrors = false,
    double FitTolerance = 6, int MaxPoints = 20000, int MaxSpans = 256, string? KeyId = null, string? RunFolder = null,
    string InspectionOrder = "category", long InspectionSeed = 0, long? SamplingSeed = null,
    int Offset = 0, int Workers = 4, string? AnalysisOutput = null, string Recognition = "all",
    int TrainingSynthetic = 2500, int TrainingEpochs = 35, long TrainingSeed = 31, string? TrainingOutput = null,
    IReadOnlyList<string>? EncodingModels = null, string? EncodingOutput = null, int EncodingPatches = 128,
    bool TrainingNeighborhoods = false, bool EncodingNeighborhoods = false, bool EncodingRepair = true, bool EncodingLine = true,int EncodingPasses=4,bool EncodingClosure=true,
    string? RefinementOutput = null, int RefinementPasses = 8);
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
        if (string.IsNullOrWhiteSpace(options.Python)) throw new ArgumentException("Choose a Python executable under Tool settings in Convert drawings.");
        if (!File.Exists(Path.Combine(options.Gojo, "experiments", "quickDraw", "QuickDraw.csproj")))
            throw new ArgumentException("Choose the linked Gojo checkout under Tool settings in Convert drawings.");
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
            case PipelineAction.RefineReview:
                if (string.IsNullOrWhiteSpace(options.RefinementOutput) || !File.Exists(Path.Combine(options.RefinementOutput,"study.json")))
                    throw new ArgumentException("Run connected refinement first, or enter its result folder.");
                arguments=["-u","-m","gojo_quickdraw.church_dataset","--output",Path.GetFullPath(options.RefinementOutput),"--review-only"];
                artifact=Path.Combine(Path.GetFullPath(options.RefinementOutput),"review.html");
                break;
            case PipelineAction.Refine:
                if (!Directory.Exists(options.Dataset)) throw new ArgumentException("Open a generated dataset first.");
                if (options.Limit > 10000 || options.RefinementPasses is < 1 or > 16)
                    throw new ArgumentException("Choose 1..10000 churches and 1..16 connected passes.");
                var refinementOutput = string.IsNullOrWhiteSpace(options.RefinementOutput)
                    ? Path.GetFullPath(options.Dataset).TrimEnd(Path.DirectorySeparatorChar) + "-refinements/connected-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                    : Path.GetFullPath(options.RefinementOutput);
                arguments = ["-u", "-m", "gojo_quickdraw.church_dataset", "--gojo-root", Path.GetFullPath(options.Gojo),
                    "--dataset", Path.GetFullPath(options.Dataset), "--output", refinementOutput,
                    "--limit", options.Limit.ToString(), "--passes", options.RefinementPasses.ToString(),
                    "--order", options.InspectionOrder, "--seed", options.InspectionSeed.ToString(CultureInfo.InvariantCulture)];
                artifact = Path.Combine(refinementOutput, "review.html");
                break;
            case PipelineAction.Encode:
                arguments.Add(options.EncodingNeighborhoods?"consolidate":"encode"); Dataset();
                var encodingOutput=string.IsNullOrWhiteSpace(options.EncodingOutput) ? Workspace.EncodingFolder(options.Dataset) : Path.GetFullPath(options.EncodingOutput);
                arguments.AddRange(["--output",encodingOutput,"--limit",options.Limit.ToString(),options.EncodingNeighborhoods?"--max-candidates":"--max-patches",options.EncodingPatches.ToString(),
                    "--seed",options.TrainingSeed.ToString(CultureInfo.InvariantCulture),"--recognition",options.Recognition]);
                foreach(var model in options.EncodingModels ?? [])arguments.AddRange(["--model",Path.GetFullPath(model)]);
                if(options.EncodingNeighborhoods)
                {
                    if(options.EncodingModels?.Count!=1)throw new ArgumentException("Choose the neighborhood repair / line model bundle.");
                    if(options.EncodingPasses is <1 or >8)throw new ArgumentException("Choose one to eight local passes.");
                    arguments.AddRange(["--passes",options.EncodingPasses.ToString(CultureInfo.InvariantCulture)]);
                    if(!options.EncodingRepair)arguments.Add("--no-repair");
                    if(!options.EncodingLine)arguments.Add("--no-line");
                    if(!options.EncodingClosure)arguments.Add("--no-closure");
                }
                artifact=Path.Combine(encodingOutput,"report.html");
                break;
            case PipelineAction.Train:
                arguments.Add(options.TrainingNeighborhoods?"train-neighborhoods":"train"); Dataset();
                var modelOutput=string.IsNullOrWhiteSpace(options.TrainingOutput) ? Workspace.ModelFolder(options.Dataset) : Path.GetFullPath(options.TrainingOutput);
                arguments.AddRange(["--output",modelOutput,"--limit",options.Limit.ToString(),"--synthetic",options.TrainingSynthetic.ToString(),
                    "--epochs",options.TrainingEpochs.ToString(),"--seed",options.TrainingSeed.ToString(CultureInfo.InvariantCulture),"--recognition",options.Recognition]);
                artifact=Path.Combine(modelOutput,"report.html");
                break;
            case PipelineAction.Convert:
                if (!File.Exists(options.Input) && !Directory.Exists(options.Input)) throw new ArgumentException("Choose a QuickDraw input file or folder.");
                if (string.IsNullOrWhiteSpace(options.Category)) throw new ArgumentException("Choose at least one source category.");
                arguments.AddRange(["process", "--input", Path.GetFullPath(options.Input), "--output", Path.GetFullPath(options.Dataset),
                    "--fit-tolerance", options.FitTolerance.ToString(CultureInfo.InvariantCulture), "--max-points", options.MaxPoints.ToString(),
                    "--max-spans", options.MaxSpans.ToString()]);
                foreach (var category in options.Category.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    arguments.AddRange(["--category", category]);
                // The desktop's count is always a hard selection limit. Whole
                // corpus conversion remains an explicit CLI-only operation.
                arguments.AddRange(["--limit", options.Limit.ToString(), "--workers", options.Workers.ToString()]);
                arguments.AddRange(["--recognition", options.Recognition]);
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
            case PipelineAction.Gallery:
                arguments.Add("gallery"); Dataset(); Ordering();
                artifact = Path.Combine(run, "comparison.html");
                arguments.AddRange(["--output", artifact, "--page-size", "12", "--inspector", Path.Combine(run, "evidence.html")]);
                break;
            case PipelineAction.Refresh:
                arguments.Add("refresh"); Dataset(); Ordering();
                artifact = Path.Combine(run, "evidence.html");
                arguments.AddRange(["--output", run, "--page-size", options.Limit.ToString(), "--gallery-page-size", "12"]);
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
                arguments.AddRange(["--limit", options.Limit.ToString(), "--offset", options.Offset.ToString(), "--recognition", options.Recognition]);
                break;
            case PipelineAction.Analyze:
                if (Workspace.PipelineManifest(options.Dataset) is { } workspace && workspace.GetProperty("request").GetProperty("mode").GetString() == "raw"
                    && (options.AnalysisOutput is null || Path.GetFullPath(options.AnalysisOutput).Equals(Path.GetFullPath(options.Dataset), StringComparison.OrdinalIgnoreCase)))
                {
                    // Resume the original selection, including a stop during
                    // acquisition; never sample this result as a new raw source.
                    var request = workspace.GetProperty("request");
                    var fit = request.GetProperty("fitConfig");
                    var original = options with { Input = request.GetProperty("source").GetString()!,
                        Category = string.Join(',', request.GetProperty("categories").EnumerateArray().Select(c => c.GetString())),
                        Recursive = request.GetProperty("recursive").GetBoolean(), SamplingSeed = workspace.GetProperty("seed").GetInt64(),
                        Recognition = request.TryGetProperty("recognition", out var recognition) ? recognition.GetString()! : "all",
                        FitTolerance = fit.ValueKind == JsonValueKind.Object && fit.TryGetProperty("polylineTolerance", out var tolerance) ? tolerance.GetDouble() : 6,
                        MaxPoints = request.GetProperty("maxPoints").GetInt32(), MaxSpans = request.GetProperty("maxSpans").GetInt32() };
                    return Plan(PipelineAction.Convert, original) with { Action = action, Artifact = Path.Combine(options.Dataset, "pipeline.json") };
                }
                var (analysisSource, analyzed) = Workspace.AnalysisPaths(options.Dataset, options.AnalysisOutput);
                arguments.AddRange(["process", "--dataset", analysisSource]);
                arguments.AddRange(["--output", Path.GetFullPath(analyzed), "--workers", options.Workers.ToString()]);
                arguments.AddRange(["--limit", options.Limit.ToString(), "--recognition", options.Recognition]);
                if (options.RetryErrors) arguments.Add("--retry-errors");
                artifact = Path.Combine(analyzed, "pipeline.json");
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
