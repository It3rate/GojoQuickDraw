using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private readonly NumericUpDown _encodingCount=Number(25,1,5000),_encodingPatches=Number(128,1,512);
    private readonly CheckBox _encodingUseModel=new(){Content="Local joint model",IsChecked=true},
        _encodingWeak=new(){Content="Show unresolved model opinions",IsChecked=false};
    private readonly TextBox _encodingModel=new(),_encodingOutput=new(){Watermark="Automatic: a new comparison beside the dataset"};
    private readonly TextBlock _encodingSummary=Text("Choose the joint provider, then create paired encodings. Earlier drawing stages remain available in View drawings."),
        _encodingPageLabel=Text("");
    private readonly Grid _encodingGallery=new(){ColumnSpacing=12,RowSpacing=12};
    private Button _encodingPrevious=null!,_encodingNext=null!,_encodingReport=null!;
    private Expander _encodingSettings=null!;
    private EncodingRun? _encodingRun;
    private int _encodingOffset,_encodingRevision;

    private Control EncodingPage()
    {
        _encodingModel.Text=string.IsNullOrWhiteSpace(_settings.EncodingModel) ? Path.Combine(_repository,"models","joint-pilot-2026-10-27.json") : _settings.EncodingModel;
        _encodingUseModel.IsChecked=_settings.EncodingUseModel;
        var source=Text(_dataset.Text ?? "Choose a drawings folder",12);_dataset.TextChanged+=(_,_)=>source.Text=_dataset.Text;
        _encodingPrevious=SmallButton("← Page",async()=>await EncodingPageAt(_encodingOffset-12));
        _encodingNext=SmallButton("Page →",async()=>await EncodingPageAt(_encodingOffset+12));
        _encodingPrevious.IsEnabled=_encodingNext.IsEnabled=false;
        _encodingReport=SmallButton("Open portable review",()=>Open(_encodingRun is null ? null : Path.Combine(_encodingRun.Folder,"report.html")));_encodingReport.IsEnabled=false;
        _encodingWeak.IsCheckedChanged+=async(_,_)=>await EncodingPageAt(_encodingOffset);
        return Stack(10,Text("Compare evidence providers",21,true),
            _encodingSettings=new Expander{Header="Encoding settings — models and drawings",IsExpanded=true,HorizontalContentAlignment=HorizontalAlignment.Stretch,
                Content=Card(Stack(10,source,
                    Text("Compare local joint opinions over the same original. This pass does not rerun the church shape stages.",13),
                    Buttons(Button("Choose drawings folder…",async()=>{await PickDataset();_tabs.SelectedIndex=3;}),
                        Button("Open saved encoding run…",async()=>await PickEncodingRun())),
                    _encodingUseModel,Row(_encodingModel,Button("Choose model…",async()=>await PickEncodingModel())),
                    Text("Unchecked: both encodings use the default heuristic. Checked: use the saved model's opinion in its place, retaining all five alternatives. One provider is currently available.",12),
                    Buttons(Field("Maximum drawings",_encodingCount),JobButton("Create paired encodings",[PipelineAction.Encode],true)),
                    new Expander{Header="Encoding options",HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=Stack(10,
                        Field("Maximum local patches per drawing",_encodingPatches,"Proposals come from source runs and gaps, without consulting final interpretations. Uncovered joints remain."),
                        Field("New encoding run folder (optional)",_encodingOutput))}))},
            _encodingSummary,Buttons(_encodingPrevious,_encodingPageLabel,_encodingNext,_encodingWeak,_encodingReport),_encodingGallery);
    }
    private async Task PickEncodingModel()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Choose a saved joint model",AllowMultiple=false,
            FileTypeFilter=[new("Joint model"){Patterns=["*.json"]}]});
        if(files.Count>0)_encodingModel.Text=files[0].TryGetLocalPath();
    }
    private async Task PickEncodingRun()
    {
        var folders=await StorageProvider.OpenFolderPickerAsync(new(){Title="Choose a paired encoding run",AllowMultiple=false});
        if(folders.Count>0)await LoadEncodingRun(folders[0].TryGetLocalPath()!);
    }
    private async Task OpenRequestedEncodingRun()
    {
        var i=Array.IndexOf(Program.Arguments,"--encoding-run");
        if(i>=0&&i+1<Program.Arguments.Length)await LoadEncodingRun(Program.Arguments[i+1]);
    }
    private async Task LoadEncodingRun(string folder)
    {
        try
        {
            _encodingRun=await Task.Run(()=>EncodingWorkspace.Open(folder));_encodingOffset=0;
            _encodingSummary.Text=$"{_encodingRun.SourceDrawings:N0} drawings · {_encodingRun.LocalPatches:N0} local opinions · {_encodingRun.ChangedPatches:N0} different / additional patch opinions · {_encodingRun.Unresolved:N0} unresolved\n"
                +"Green agrees · amber differs / adds an opinion · gray is unresolved. Hover for alternatives. Directions are source-based hints; scores are relative support.";
            _encodingReport.IsEnabled=File.Exists(Path.Combine(folder,"report.html"));
            _encodingSettings.IsExpanded=false;
            await EncodingPageAt(0);_tabs.SelectedIndex=3;
        }
        catch(Exception error){SetStatus("Encoding run could not be opened",error.Message,true);}
    }
    private async Task EncodingPageAt(int offset)
    {
        if(_encodingRun is not { } run)return;
        offset=Math.Clamp(offset,0,Math.Max(0,(run.Drawings.Length-1)/12*12));var revision=++_encodingRevision;
        var data=await Task.Run(()=>Enumerable.Range(offset,Math.Min(12,run.Drawings.Length-offset)).Select(i=>EncodingWorkspace.Drawing(run,i)).ToArray());
        if(revision!=_encodingRevision)return;
        _encodingOffset=offset;_encodingPrevious.IsEnabled=offset>0;_encodingNext.IsEnabled=offset+12<run.Drawings.Length;
        _encodingPageLabel.Text=$"Page {offset/12+1} of {Math.Max(1,(run.Drawings.Length+11)/12)}";
        _encodingGallery.Children.Clear();_encodingGallery.ColumnDefinitions=new ColumnDefinitions("*,*,*");_encodingGallery.RowDefinitions.Clear();
        for(var i=0;i<(data.Length+2)/3;i++)_encodingGallery.RowDefinitions.Add(new(GridLength.Auto));
        for(var i=0;i<data.Length;i++)
        {
            var drawing=data[i];var card=new Button{HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,
                Padding=new Thickness(10),Background=Brushes.White,Content=Stack(5,Text(drawing.Category+" · "+drawing.SourceKey,12,true),EncodingPair(drawing,165),
                    Text($"{drawing.Patches.Length} local opinions · inspect alternatives"+(drawing.PatchLimitReached ? " · patch cap reached" : ""),11))};
            card.Click+=async(_,_)=>await ShowEncodingDrawing(drawing);
            Grid.SetColumn(card,i%3);Grid.SetRow(card,i/3);_encodingGallery.Children.Add(card);
        }
    }
    private static double[] EncodingFrame(EncodingDrawing drawing)
    {
        var p=drawing.Strokes.SelectMany(s=>s).ToArray();
        return p.Length==0 ? [-10,-10,530,530] : [p.Min(p=>p[0])-20,p.Min(p=>p[1])-20,p.Max(p=>p[0])+20,p.Max(p=>p[1])+20];
    }
    private Control EncodingPair(EncodingDrawing drawing,double height,int focus=-1)
    {
        var frame=EncodingFrame(drawing);var grid=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*"),ColumnSpacing=8};
        var patch=focus>=0 ? drawing.Patches[focus] : null;var key="model-p"+focus;
        var baseline=new DrawingCanvas{Underlay=drawing.Strokes,UnderlayAlpha=115,Profiles=patch?.Runs ?? [],Frame=frame,Height=height,Joints=drawing.Baseline,
            Highlighted=(patch?.Baseline ?? []).ToHashSet(),ShowJointDirections=true};
        bool Visible(GojoQuickDraw.Reads.JointNode node)
        {
            if(!node.Id.StartsWith("model-p",StringComparison.Ordinal))return true;
            if(node.Id==key)return true;
            var index=int.Parse(node.Id[7..]);
            if(drawing.Patches[index].Proposed is null&&_encodingWeak.IsChecked!=true)return false;
            return node.Kind!="separate"||drawing.Patches[index].Changed;
        }
        var selected=new DrawingCanvas{Underlay=drawing.Strokes,UnderlayAlpha=115,Profiles=patch?.Runs ?? [],Frame=frame,Height=height,
            Joints=drawing.Selected.Where(Visible).ToArray(),Highlighted=[key],ShowJointDirections=true,IncludeContinuations=true,
            JointStatuses=drawing.Patches.Select((p,i)=>new{Id="model-p"+i,Status=p.Proposed is null?"unresolved":p.Changed?"changed type":"same type"}).ToDictionary(p=>p.Id,p=>p.Status)};
        grid.Children.Add(Stack(3,Text("Default joints",11),baseline));var right=Stack(3,Text("Selected provider",11),selected);Grid.SetColumn(right,1);grid.Children.Add(right);
        return grid;
    }
    private async Task ShowEncodingDrawing(EncodingDrawing drawing)
    {
        var window=new Window{Title="Joint encoding · "+drawing.SourceKey,Width=1080,Height=850,Background=Background};
        var selector=new ComboBox{ItemsSource=drawing.Patches.Select((p,i)=>$"Patch {i+1} · {string.Join(" / ",p.Profiles)} · {p.Status}").ToArray(),SelectedIndex=drawing.Patches.Length>0?0:-1};
        var choices=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*,*"),ColumnSpacing=10};
        var comparison=new Border{Child=EncodingPair(drawing,385)};
        var basis=Text("",12);
        void RenderChoices()
        {
            choices.Children.Clear();if(selector.SelectedIndex<0)return;var patch=drawing.Patches[selector.SelectedIndex];
            comparison.Child=EncodingPair(drawing,385,selector.SelectedIndex);
            basis.Text="Default: "+(patch.BaselineKinds.Length==0?"no matching two-run joint":string.Join(", ",patch.BaselineKinds))+". "+patch.Status+
                ". These are source-oriented construction candidates; the position and orientation are conditional hints.";
            for(var i=0;i<3;i++)
            {
                var candidate=patch.Candidates[patch.Ranking[i]];var points=candidate.Points;
                var color=i==0?"#247D72":"#7963A4";
                var part=new PreviewPart("joint",candidate.Kind,"candidate",color,points,candidate.Segments,[]);
                var frame=new[]{points.Min(p=>p[0])-8,points.Min(p=>p[1])-8,points.Max(p=>p[0])+8,points.Max(p=>p[1])+8};
                var canvas=new DrawingCanvas{Parts=[part],Frame=frame,Height=155};
                var card=Card(Stack(5,Text($"{candidate.Kind} · {candidate.Score:P1}",14,true),canvas));Grid.SetColumn(card,i);choices.Children.Add(card);
            }
        }
        selector.SelectionChanged+=(_,_)=>RenderChoices();RenderChoices();
        window.Content=Scroll(Stack(10,Buttons(Text(drawing.Category+" · "+drawing.SourceKey,21,true),SmallButton("Close",window.Close)),
            comparison,Text("Three leading constructions — all five remain encoded",16,true),selector,basis,choices,
            Text("Scores are relative model support. Unresolved choices and source geometry remain. Larger shape / category feedback is not applied in this comparison.",12)),new Thickness(16));
        await window.ShowDialog(this);
    }
}
