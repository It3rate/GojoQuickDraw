using System.Diagnostics;
using System.Text.Json;
using GojoQuickDraw.Desktop;

namespace GojoQuickDraw.Desktop.Tests;

public sealed class PipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickDraw desktop " + Guid.NewGuid().ToString("N"));
    private string Dataset => Path.Combine(_root, "dataset");
    public PipelineTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "gojo", "experiments", "quickDraw"));
        File.WriteAllText(Path.Combine(_root, "gojo", "experiments", "quickDraw", "QuickDraw.csproj"), "");
        Directory.CreateDirectory(Dataset);
        File.WriteAllText(Path.Combine(Dataset, "manifest.json"), """
            {"schema":"gojo.quickdraw-dataset.v1","lastRun":{"storedRecords":1,"storedErrors":0}}
            """);
    }
    public void Dispose() { Directory.Delete(_root, true); }
    private PipelineOptions Options => new(_root, new WorkspaceSettings().Python, Path.Combine(_root, "gojo"), _root, Dataset,
        RunFolder: Path.Combine(_root, "runs"));

    [Fact]
    public void DerivedRunsStayOutsideBaselineAndFullCorpusIsExplicit()
    {
        var conversion = Pipeline.Plan(PipelineAction.Convert, Options);
        Assert.Contains("--limit", conversion.Arguments);
        Assert.DoesNotContain("--limit", Pipeline.Plan(PipelineAction.Convert, Options with { AllRecords = true }).Arguments);
        foreach (var action in new[] { PipelineAction.Review, PipelineAction.Stages, PipelineAction.ExampleProbe })
        {
            var plan = Pipeline.Plan(action, Options);
            Assert.Equal("..", Path.GetRelativePath(Dataset, plan.Artifact!).Split(Path.DirectorySeparatorChar)[0]);
        }
    }

    [Fact]
    public void SelectedDrawingIsRequiredForItsProbeAndRemainsOneArgument()
    {
        Assert.Throws<ArgumentException>(() => Pipeline.Plan(PipelineAction.Probe, Options));
        const string unusual = "key with spaces & 'quotes'";
        var plan = Pipeline.Plan(PipelineAction.Probe, Options with { KeyId = unusual });
        var index = plan.Arguments.ToList().IndexOf("--key-id");
        Assert.Equal(unusual, plan.Arguments[index + 1]);
    }

    [Fact]
    public void CatalogUsesLatestStatusAndRefusesUncommittedTail()
    {
        const string error = """{"itemId":"a","keyId":"1","category":"church","status":"error"}""";
        const string ok = """{"itemId":"a","keyId":"1","category":"church","status":"ok","counts":{"strokes":2,"samples":8,"spans":3},"evidencePath":"records/a.gz"}""";
        var index = Path.Combine(Dataset, "index.ndjson");
        File.WriteAllText(index, error + "\n" + ok + "\n");
        var row = Assert.Single(Workspace.ReadCatalog(Dataset).Drawings);
        Assert.Equal(3, row.Intervals); Assert.Equal(2, row.Strokes);
        File.AppendAllText(index, error);
        Assert.Throws<InvalidDataException>(() => Workspace.ReadCatalog(Dataset));
    }

    [Fact]
    public async Task WorkerPreservesLiteralArgumentsAndRetainedErrorResult()
    {
        var script = Path.Combine(_root, "worker.py");
        File.WriteAllText(script, "import json,sys\nprint(json.dumps({'argument':sys.argv[1],'storedErrors':1}))\nsys.exit(1)\n");
        const string argument = "spaces & quotes ' \" $HOME $(echo ignored)";
        var result = await Pipeline.Run(new(PipelineAction.Convert, Options.Python, _root, [script, argument], null), _ => { }, CancellationToken.None);
        Assert.True(result.RetainedErrors); Assert.False(result.Successful);
        Assert.Equal(argument, result.Data!.Value.GetProperty("argument").GetString());
    }

    [Fact]
    public async Task StoppingWorkerTerminatesItsChildAndAllowsNextRun()
    {
        var script = Path.Combine(_root, "parent.py");
        var pidFile = Path.Combine(_root, "child.json");
        File.WriteAllText(script, "import json,subprocess,sys,time\np=subprocess.Popen([sys.executable,'-c','import time;time.sleep(120)'])\nwith open(sys.argv[1],'w') as f: json.dump({'pid':p.pid},f)\ntime.sleep(120)\n");
        using var cancel = new CancellationTokenSource();
        var running = Pipeline.Run(new(PipelineAction.Verify, Options.Python, _root, [script, pidFile], null), _ => { }, cancel.Token);
        var timeout = Stopwatch.StartNew();
        while (!File.Exists(pidFile) && timeout.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(25);
        if (!File.Exists(pidFile)) { cancel.Cancel(); await running; Assert.Fail("Worker child did not start."); }
        using var document = JsonDocument.Parse(File.ReadAllText(pidFile));
        var childId = document.RootElement.GetProperty("pid").GetInt32();
        cancel.Cancel(); var result = await running.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Cancelled);
        try { using var child = Process.GetProcessById(childId); Assert.True(child.HasExited); }
        catch (ArgumentException) { }
        var next = await Pipeline.Run(new(PipelineAction.Setup, Options.Python, _root, ["-c", "print('{}')"], null), _ => { }, CancellationToken.None);
        Assert.True(next.Successful);
    }
}
