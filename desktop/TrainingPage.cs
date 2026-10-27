using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private readonly NumericUpDown _trainingCount=Number(300,1,5000),_trainingSynthetic=Number(2500,100,20000),
        _trainingEpochs=Number(35,1,200),_trainingSeed=Number(31,0,long.MaxValue);
    private readonly TextBox _trainingOutput=new(){Watermark="Automatic: a new run beside the dataset, under its -models folder"};
    private readonly TextBlock _trainingSummary=Text("No model run selected. Training creates a small local joint ranker and a review of its proposals.");
    private string? _trainingRun;
    private Button _trainingReview=null!,_trainingFolder=null!;

    private Control TrainingPage()
    {
        var source=Text(_dataset.Text ?? "Choose a converted dataset folder.",13);
        _dataset.TextChanged+=(_,_)=>source.Text=_dataset.Text;
        _trainingReview=Button("Open patch review",()=>Open(_trainingRun is null?null:Path.Combine(_trainingRun,"report.html")));_trainingReview.IsEnabled=false;
        _trainingFolder=Button("Open model folder",()=>Open(_trainingRun));_trainingFolder.IsEnabled=false;
        return Stack(14,Text("Learn local joint constructions",21,true),
            Text("Train a small CPU model to rank separate lines, continuation, corners, T attachments and crossings. It reads coarse partitions, gaps and curve evidence from completed drawings."),
            Card(Stack(10,Text("Drawings folder",13,true),source,Button("Choose drawings folder…",async()=>{await PickDataset();_tabs.SelectedIndex=2;}),
                Field("Maximum source drawings",_trainingCount,"Uses a read-only snapshot of completed records. The current category and game-recognition filters in View drawings apply."),
                JobButton("Train joint model",[PipelineAction.Train],true),
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
            if(document.RootElement.GetProperty("schema").GetString()!="gojo.local-joint-training.v1")throw new InvalidDataException("This folder is not a local joint model run.");
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
        var controlled=data.GetProperty("controlledTest");var weak=data.GetProperty("realWeakTest");
        _trainingSummary.Text=$"{data.GetProperty("sourceDrawings").GetInt32():N0} source drawings · {data.GetProperty("controlledPatches").GetInt32():N0} controlled patches · {data.GetProperty("parameterCount").GetInt32():N0} learned weights\n"
            +$"Held-out controls: {Percent(controlled,"top1Agreement")} reference agreement. Real weak references: {Percent(weak,"top1Agreement")} ({weak.GetProperty("examples").GetInt32():N0} labelled patches).\n"
            +"Real references are generated interpretations, not human labels. Open the review to inspect individual patches and unresolved choices.";
        _trainingRun=path;_trainingReview.IsEnabled=File.Exists(Path.Combine(path,"report.html"));_trainingFolder.IsEnabled=Directory.Exists(path);
    }
}
