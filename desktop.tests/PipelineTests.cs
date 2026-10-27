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
    public void TrainingUsesItsOwnBoundedCommandAndNeverReentersConversion()
    {
        var plan=Pipeline.Plan(PipelineAction.Train,Options with{Limit=300,TrainingSynthetic=500,TrainingEpochs=12,Recognition="recognized"});
        var args=plan.Arguments.ToList();
        Assert.Contains("train",args);Assert.DoesNotContain("process",args);Assert.DoesNotContain("--input",args);
        Assert.Equal("300",args[args.IndexOf("--limit")+1]);Assert.Equal("500",args[args.IndexOf("--synthetic")+1]);
        Assert.Equal("recognized",args[args.IndexOf("--recognition")+1]);Assert.EndsWith("report.html",plan.Artifact);
        Assert.DoesNotContain(Path.GetFullPath(Dataset)+Path.DirectorySeparatorChar,plan.Artifact!);
    }

    [Fact]
    public void RecognitionSelectionIsForwardedWithTheBoundedCount()
    {
        var args = Pipeline.Plan(PipelineAction.Convert, Options with { Recognition = "recognized", Limit = 300 }).Arguments.ToList();
        Assert.Equal("recognized", args[args.IndexOf("--recognition") + 1]);
        Assert.Equal("300", args[args.IndexOf("--limit") + 1]);
        var analyze = Pipeline.Plan(PipelineAction.Analyze, Options with { Recognition = "unrecognized" }).Arguments.ToList();
        Assert.Equal("unrecognized", analyze[analyze.IndexOf("--recognition") + 1]);
    }

    [Fact]
    public void DerivedRunsStayOutsideBaselineAndFullCorpusIsExplicit()
    {
        var conversion = Pipeline.Plan(PipelineAction.Convert, Options);
        Assert.Contains("--limit", conversion.Arguments);
        var legacy = Pipeline.Plan(PipelineAction.Convert, Options with { AllRecords = true, Limit = 3000 }).Arguments.ToList();
        Assert.Equal("3000", legacy[legacy.IndexOf("--limit") + 1]);
        Assert.DoesNotContain("--all-records", legacy);
        foreach (var action in new[] { PipelineAction.Review, PipelineAction.Gallery, PipelineAction.Refresh, PipelineAction.Stages, PipelineAction.ExampleProbe })
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
        Assert.Contains("process", convert.Arguments);
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
    public void PortableReviewExportRemainsAnExplicitOperation()
    {
        Assert.Null(Pipeline.Plan(PipelineAction.Convert, Options).Artifact);
        var refresh = Pipeline.Plan(PipelineAction.Refresh, Options with { Limit = 50, InspectionOrder = "random", InspectionSeed = 99 });
        var args = refresh.Arguments.ToList();
        Assert.Contains("refresh", args);
        Assert.Equal(Path.GetFullPath(Dataset), args[args.IndexOf("--dataset") + 1]);
        Assert.Equal(Options.RunFolder, args[args.IndexOf("--output") + 1]);
        Assert.Equal("50", args[args.IndexOf("--page-size") + 1]);
        Assert.Equal("12", args[args.IndexOf("--gallery-page-size") + 1]);
        Assert.Contains("random", args); Assert.Contains("99", args);
    }

    [Fact]
    public void CatalogPagesAndFullAnalysisHaveSeparateLimitsAndOutputs()
    {
        var page = Pipeline.Plan(PipelineAction.Catalog, Options with { Limit = 50, Offset = 950 });
        Assert.Contains("950", page.Arguments); Assert.Contains("--offset", page.Arguments);
        var analysis = Pipeline.Plan(PipelineAction.Analyze, Options with { AllRecords = true, Workers = 2 });
        Assert.Contains("process", analysis.Arguments); Assert.Contains("--limit", analysis.Arguments);
        Assert.StartsWith(Path.GetFullPath(Dataset) + "-processed", analysis.Artifact!);
        Assert.Contains("--limit", Pipeline.Plan(PipelineAction.Analyze, Options with { AllRecords = false }).Arguments);
        Assert.EndsWith("inspector", Workspace.InspectorFolder(Dataset));
        Assert.DoesNotContain("dataset-runs", Workspace.InspectorFolder(Dataset));
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
        Assert.Equal(Workspace.AnalysisFolder(analyzed), args[args.IndexOf("--output") + 1]);
    }

    [Fact]
    public void CompletingAStoppedPipelineUsesItsOriginalSourceAndDestination()
    {
        var raw = new
        {
            schema = "gojo.quickdraw-pipeline.v1", seed = 42,
            request = new { mode = "raw", source = _root, categories = new[] { "church" }, count = 25, recursive = true,
                fitConfig = new { polylineTolerance = 3.0 }, maxPoints = 20000, maxSpans = 256 }
        };
        File.WriteAllText(Path.Combine(Dataset, "pipeline.json"), JsonSerializer.Serialize(raw));
        var plan = Pipeline.Plan(PipelineAction.Analyze, Options with { Input = "unrelated", Category = "frog", SamplingSeed = 9 });
        var args = plan.Arguments.ToList();
        Assert.Equal("42", args[args.IndexOf("--seed") + 1]);
        Assert.Equal(_root, args[args.IndexOf("--input") + 1]);
        Assert.Equal(Dataset, args[args.IndexOf("--output") + 1]);
        Assert.Equal("church", args[args.IndexOf("--category") + 1]);
        Assert.Equal("3", args[args.IndexOf("--fit-tolerance") + 1]);
        File.WriteAllText(Path.Combine(Dataset, "pipeline.json"), JsonSerializer.Serialize(new
            { schema = "gojo.quickdraw-pipeline.v1", request = new { mode = "existing", source = _root, count = 25 } }));
        args = Pipeline.Plan(PipelineAction.Analyze, Options).Arguments.ToList();
        Assert.Equal(_root, args[args.IndexOf("--dataset") + 1]);
        Assert.Equal(Dataset, args[args.IndexOf("--output") + 1]);
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
        File.WriteAllText(script, "import json,os,subprocess,sys,time\np=subprocess.Popen([sys.executable,'-c','import time;time.sleep(120)'])\nwith open(sys.argv[1]+'.tmp','w') as f: json.dump({'pid':p.pid},f)\nos.replace(sys.argv[1]+'.tmp',sys.argv[1])\ntime.sleep(120)\n");
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
