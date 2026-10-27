using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void RoundOpeningKeepsItsClosedPerimeterSourceAndContainerWithoutLabels()
    {
        var packet = ObjectFixture();
        var points = Enumerable.Range(0,32).Select(i => new[] {
            Math.Round(5+3*Math.Cos(i*Math.Tau/32),3), Math.Round(8+3*Math.Sin(i*Math.Tau/32),3)
        }).ToArray();
        var perimeter = new { points, segments = Enumerable.Range(0,32).Select(i => new[] {i,(i+1)%32}).ToArray() };
        packet["context"] = JsonSerializer.SerializeToNode(new { parts = new object[] {
            new { id="wall", spans=new[]{"s0"}, bounds=new[]{0,0,10,20}, geometry=new {
                points=new int[][]{[0,0],[10,0],[10,20],[0,20]}, segments=new int[][]{[0,1],[1,2],[2,3],[3,0]} } },
            new { id="round-opening", spans=new[]{"s1"}, participants=new[]{"wall"}, geometry=perimeter },
            new { id="unsupported-alternative", spans=Array.Empty<string>(), participants=new[]{"wall"}, geometry=perimeter }
        }});
        var fields=Entries(Import(packet));var context=Entries(fields[2]);
        context[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[2]=G(context);
        var restored=NativeRecord.Restore(NativeRecord.Capture("round-opening",G(fields)));
        var opening=restored.Context.Parts[1];var alternative=restored.Context.Parts[2];
        Assert.Equal(32,opening.Segments.Length);
        for(var i=0;i<32;i++)
        {
            Assert.Same(opening.Points[i],opening.Segments[i].Left);
            Assert.Same(opening.Points[(i+1)%32],opening.Segments[i].Right);
        }
        Assert.Same(restored.Annotation!.Profiles[1].Witness,Assert.Single(opening.Profiles));
        Assert.Same(restored.Context.Parts[0].Witness,Assert.Single(opening.Parts));
        Assert.Empty(alternative.Profiles);
        Assert.Equal(PositionTicks(opening.Points[0]),PositionTicks(alternative.Points[0]));
        Assert.NotSame(opening.Points[0],alternative.Points[0]);
        Assert.Equal("{}",restored.Context.AdapterJson);
    }
}
