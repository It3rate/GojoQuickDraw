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
        foreach (var action in new[] { PipelineAction.Review, PipelineAction.Gallery, PipelineAction.Stages, PipelineAction.ExampleProbe })
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
    public void GeneratedFolderDiscoveryUsesActualDatasetsAndCatalogPreservesTheirIdentity()
    {
        var child = Path.Combine(Dataset, "church"); Directory.CreateDirectory(child);
        File.Copy(Path.Combine(Dataset, "manifest.json"), Path.Combine(child, "manifest.json"));
        var ignored = Path.Combine(Dataset, "records"); Directory.CreateDirectory(ignored);
        File.Copy(Path.Combine(Dataset, "manifest.json"), Path.Combine(ignored, "manifest.json"));
        Assert.Equal(2, Workspace.GeneratedFolders(Dataset).Count);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            storedRecords = 1, storedErrors = 0,
            records = new[] { new { itemId = "a", keyId = "1", category = "church", counts = new { strokes = 2, samples = 8, spans = 3 },
                evidencePath = "records/a.gz", datasetPath = child } }
        }));
        var row = Assert.Single(Workspace.ReadCatalogData(document.RootElement).Drawings);
        Assert.Equal(3, row.Intervals); Assert.Equal(child, row.DatasetPath);
    }

    [Fact]
    public void StaticCategoryPickerCatalogIsUniqueAndSourceFilenameIsRecognized()
    {
        Assert.Equal(345, CategoryCatalog.Names.Count);
        Assert.Equal(345, CategoryCatalog.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("angel", CategoryCatalog.FromFile("Angel.ndjson"));
        Assert.Equal("circle", CategoryCatalog.FromFile("full_raw_circle.ndjson"));
        Assert.Null(CategoryCatalog.FromFile("custom-name.ndjson"));
        Assert.Throws<ArgumentException>(() => Pipeline.Plan(PipelineAction.Convert, Options with { Category = "" }));
    }

    [Fact]
    public void CollectionSamplingAndInspectionArgumentsKeepDifferentSeeds()
    {
        var convert = Pipeline.Plan(PipelineAction.Convert, Options with { Category = "angel,church", SamplingSeed = 42 });
        Assert.Contains("sample", convert.Arguments);
        Assert.Equal(2, convert.Arguments.Count(argument => argument == "--category"));
        var review = Pipeline.Plan(PipelineAction.Review, Options with { InspectionOrder = "random", InspectionSeed = 99 });
        Assert.Contains("random", review.Arguments); Assert.Contains("99", review.Arguments);
        Assert.DoesNotContain("42", review.Arguments);
    }

    [Fact]
    public void ComparisonGalleryCoversAllDrawingsWithTwelvePairsIndependentOfInspectorPageSize()
    {
        var gallery = Pipeline.Plan(PipelineAction.Gallery, Options with { Limit = 50, InspectionOrder = "random", InspectionSeed = 99 });
        Assert.Contains("gallery", gallery.Arguments);
        Assert.Contains("random", gallery.Arguments); Assert.Contains("99", gallery.Arguments);
        Assert.DoesNotContain("--limit", gallery.Arguments);
        var args = gallery.Arguments.ToList();
        Assert.Equal("12", args[args.IndexOf("--page-size") + 1]);
        Assert.EndsWith("comparison.html", gallery.Artifact!);
        Assert.Equal(Path.Combine(Options.RunFolder!, "evidence.html"), args[args.IndexOf("--inspector") + 1]);
    }

    [Fact]
    public void CatalogPagesAndFullAnalysisHaveSeparateLimitsAndOutputs()
    {
        var page = Pipeline.Plan(PipelineAction.Catalog, Options with { Limit = 50, Offset = 950 });
        Assert.Contains("950", page.Arguments); Assert.Contains("--offset", page.Arguments);
        var analysis = Pipeline.Plan(PipelineAction.Analyze, Options with { AllRecords = true, Workers = 2 });
        Assert.Contains("analyze", analysis.Arguments); Assert.DoesNotContain("--limit", analysis.Arguments);
        Assert.StartsWith(Path.GetFullPath(Dataset) + "-analysis", analysis.Artifact!);
        Assert.Contains("--limit", Pipeline.Plan(PipelineAction.Analyze, Options with { AllRecords = false }).Arguments);
        Assert.EndsWith(Path.Combine("dataset-runs", "inspector"), Workspace.InspectorFolder(Dataset));
        Assert.NotEqual(Workspace.RunFolder(Dataset), Workspace.InspectorFolder(Dataset));
    }

    [Fact]
    public void OpeningAnAnalysisCollectionCanResumeItsActualBaseline()
    {
        var analyzed = Workspace.AnalysisFolder(Dataset); Directory.CreateDirectory(analyzed);
        File.WriteAllText(Path.Combine(analyzed, "collection.json"), JsonSerializer.Serialize(new { kind = "derived-analysis", baseline = Dataset }));
        var plan = Pipeline.Plan(PipelineAction.Analyze, Options with { Dataset = analyzed, AllRecords = true });
        var args = plan.Arguments.ToList();
        Assert.Equal(Dataset, args[args.IndexOf("--dataset") + 1]);
        Assert.Equal(analyzed, args[args.IndexOf("--output") + 1]);
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
