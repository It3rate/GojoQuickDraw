using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private readonly NumericUpDown _trainingCount=Number(60,1,5000),_trainingSynthetic=Number(600,100,20000),
        _trainingEpochs=Number(35,1,200),_trainingSeed=Number(43,0,long.MaxValue);
    private readonly TextBox _trainingOutput=new(){Watermark="Automatic: a new run beside the dataset, under its -models folder"};
    private readonly TextBlock _trainingSummary=Text("No model run selected. Training creates a small local joint ranker and a review of its proposals.");
    private string? _trainingRun;
    private readonly ComboBox _trainingMode=new(){ItemsSource=new[]{"Neighborhood repair + line / curve models","Pairwise joint ranker (earlier experiment)"},SelectedIndex=0};
    private Button _trainingReview=null!,_trainingFolder=null!;

    private Control TrainingPage()
    {
        var source=Text(_dataset.Text ?? "Choose a converted dataset folder.",13);
        _dataset.TextChanged+=(_,_)=>source.Text=_dataset.Text;
        _trainingReview=Button("Open patch review",()=>Open(_trainingRun is null?null:Path.Combine(_trainingRun,"report.html")));_trainingReview.IsEnabled=false;
        _trainingFolder=Button("Open model folder",()=>Open(_trainingRun));_trainingFolder.IsEnabled=false;
        return Stack(14,Text("Learn local constructions",21,true),
            Text("Neighborhood models score local connections, trims and straightening. The earlier pairwise model predicts joint opinions without changing the drawing."),_trainingMode,
            Card(Stack(10,Text("Drawings folder",13,true),source,Button("Choose drawings folder…",async()=>{await PickDataset();_tabs.SelectedIndex=2;}),
                Field("Maximum source drawings",_trainingCount,"Uses a read-only snapshot of completed records. The current category and game-recognition filters in View drawings apply."),
                JobButton("Train selected models",[PipelineAction.Train],true),
                Text("Known synthetic constructions supply controlled targets. Existing drawing/final correspondences supply weak suggestions. Held-out results are reported separately. Training keeps drawing files intact; conversion does not automatically use this pilot model.",12),
                new Expander{Header="Training options",HorizontalContentAlignment=HorizontalAlignment.Stretch,
                    Content=Stack(10,Field("Controlled patches",_trainingSynthetic),Field("Training epochs",_trainingEpochs),Field("Repeatable seed",_trainingSeed),
                        Field("New model run folder (optional)",_trainingOutput,"Choose a new folder outside the drawing dataset. Each run retains its weights, input snapshot, native candidates, learned opinions and test results."))})),
            Card(Stack(10,Text("Model results",16,true),_trainingSummary,Buttons(_trainingReview,_trainingFolder,
                Button("Open saved model run…",async()=>await OpenTrainingRun())))));
    }
    private async Task OpenTrainingRun()
    {
        var selected=await StorageProvider.OpenFolderPickerAsync(new(){Title="Choose a saved joint model run",AllowMultiple=false});
        if(selected.Count==0)return;
        var path=selected[0].TryGetLocalPath();
        try
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(path!,"report.json")));
            if(document.RootElement.GetProperty("schema").GetString() is not ("gojo.local-joint-training.v1" or "gojo.neighborhood-training.v1"))throw new InvalidDataException("This folder is not a local model run.");
            ShowTrainingMetrics(document.RootElement,path!);
        }
        catch(Exception error){SetStatus("Model run could not be opened",error.Message,true);}
    }
    private void OpenRequestedTrainingRun()
    {
        var index=Array.IndexOf(Program.Arguments,"--model-run");
        if(index<0||index+1>=Program.Arguments.Length)return;
        try
        {
            var path=Path.GetFullPath(Program.Arguments[index+1]);
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(path,"report.json")));
            ShowTrainingMetrics(document.RootElement,path);_tabs.SelectedIndex=2;
        }
        catch(Exception error){SetStatus("Model run could not be opened",error.Message,true);}
    }
    private void ShowTraining(JobResult result)
    {
        if(result.Data is { } data&&data.TryGetProperty("output",out var output))ShowTrainingMetrics(data,output.GetString()!);
    }
    private void ShowTrainingMetrics(JsonElement data,string path)
    {
        string Percent(JsonElement value,string field)=>value.TryGetProperty(field,out var number)&&number.ValueKind==JsonValueKind.Number?$"{number.GetDouble():P1}":"not available";
        if(data.TryGetProperty("schema",out var schema)&&schema.GetString()=="gojo.neighborhood-training.v1")
        {
            _trainingSummary.Text=$"{data.GetProperty("sourceDrawings").GetInt32():N0} source drawings · {data.GetProperty("controlledNeighborhoods").GetInt32():N0} controlled neighborhoods · {data.GetProperty("parameterCount").GetInt32():N0} weights\n"
                +$"Held-out candidate agreement: {Percent(data.GetProperty("repairTest"),"agreement")}. Line / curve agreement: {Percent(data.GetProperty("lineTest"),"agreement")}.\n"
                +(data.TryGetProperty("closureTest",out var closure)?$"Closure control agreement: {Percent(closure,"agreement")}. ":"")
                +"Local providers use authored compatibility gates. Open the review for actual graph changes and the limits of these controlled targets.";
            _trainingRun=path;_trainingReview.IsEnabled=File.Exists(Path.Combine(path,"report.html"));_trainingFolder.IsEnabled=Directory.Exists(path);
            if(File.Exists(Path.Combine(path,"model.json")))_encodingNeighborhoodModel.Text=Path.Combine(path,"model.json");
            return;
        }
        var controlled=data.GetProperty("controlledTest");var weak=data.GetProperty("realWeakTest");
        _trainingSummary.Text=$"{data.GetProperty("sourceDrawings").GetInt32():N0} source drawings · {data.GetProperty("controlledPatches").GetInt32():N0} controlled patches · {data.GetProperty("parameterCount").GetInt32():N0} learned weights\n"
            +$"Held-out controls: {Percent(controlled,"top1Agreement")} reference agreement. Real weak references: {Percent(weak,"top1Agreement")} ({weak.GetProperty("examples").GetInt32():N0} labelled patches).\n"
            +"Real references are generated interpretations, not human labels. Open the review to inspect individual patches and unresolved choices.";
        _trainingRun=path;_trainingReview.IsEnabled=File.Exists(Path.Combine(path,"report.html"));_trainingFolder.IsEnabled=Directory.Exists(path);
        if(File.Exists(Path.Combine(path,"model.json")))_encodingModel.Text=Path.Combine(path,"model.json");
    }
}
