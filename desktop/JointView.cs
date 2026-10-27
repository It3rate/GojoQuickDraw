using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GojoQuickDraw.Reads;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private async Task ShowJointComparison(DrawingRow row,Preview preview)
    {
        if(preview.Joints is not { } read)return;
        var window=new Window {Title="Joint comparison · "+row.KeyId,Width=980,Height=740,Background=Background};
        var list=Stack(7);
        foreach(var match in read.Matches)
        {
            var description=match.Candidates.Length==0 ? "no source joint mapped" : string.Join(", ",match.Candidates.Select(id=>id+" "+read.Source.Nodes.First(n=>n.Id==id).Kind));
            list.Children.Add(Text($"{match.Target} {match.TargetKind} ← {description} · {match.Status}",13));
        }
        var same=read.Matches.Count(m=>m.Status=="same type");var changed=read.Matches.Count(m=>m.Status=="changed type");
        var uncertain=read.Matches.Count(m=>m.Status is "ambiguous" or "unmapped");
        string Counts(JointGraph graph)=>string.Join(" · ",graph.Nodes.GroupBy(n=>n.Kind).OrderBy(g=>g.Key).Select(g=>$"{g.Key}: {g.Count()}"));
        var orders=Stack(5,Text($"{read.OrderChecks.Count(o=>o.Same)} of {read.OrderChecks.Length} checked relative orders agree. Only unique source matches are compared."));
        foreach(var order in read.OrderChecks.Take(100))orders.Children.Add(Text($"{order.First} → {order.Second}: {order.SourceOrder} → {order.FinalOrder}"+(order.Same ? "" : " · changed"),12));
        var exports=SmallButton("Export relative graph…",async()=>
        {
            var file=await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {Title="Save joint training example",SuggestedFileName=row.KeyId+".joint-graph.json",DefaultExtension="json"});
            if(file is null)return;
            await using var stream=await file.OpenWriteAsync();stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream,new {drawingId=row.KeyId,category=row.Category,graph=JointGraphRead.TrainingView(read)},JointPreview.Options);
        });
        window.Content=Scroll(Stack(10,Text("Source ↔ refined shape",22,true),
            Text($"{same} same type · {changed} changed type · {uncertain} ambiguous / unmapped · {read.Matches.Count(m=>m.Status=="added")} added",15,true),
            Text("Matches follow boundary-to-source links. Position and the expected joint label do not choose the match. O marks a closed walk, not a point junction.",13),
            Text("Source: "+Counts(read.Source)),Text("Final: "+Counts(read.Final)),
            Buttons(exports,SmallButton("Close",window.Close)),
            new Expander {Header=$"Relative orders · {read.OrderChecks.Count(o=>o.Same)} / {read.OrderChecks.Length} agree",Content=orders,HorizontalContentAlignment=HorizontalAlignment.Stretch},
            Text($"{read.LineMatches.Count(l=>l.Status=="mapped boundary support")} of {read.LineMatches.Length} final edges have a traced boundary-to-source mapping. " +
                $"{read.UnmatchedSource.Length} source joint reads have no candidate match.",13),
            list,Text(read.Qualification,12),Text(read.Basis,11)),new Thickness(18));
        await window.ShowDialog(OwnedWindows.LastOrDefault() ?? this);
    }
}
