using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void WindowFrameAndBarsKeepSharedContactsAndSourcesWithoutLabels()
    {
        var packet = ObjectFixture();
        var geometry = new {
            points = new int[][] { [2,8],[2,2],[8,2],[8,8],[2,5],[5,2],[8,5],[5,8],[5,5] },
            segments = new int[][] { [0,4],[4,1],[1,5],[5,2],[2,6],[6,3],[3,7],[7,0],
                                     [5,8],[8,7],[4,8],[8,6] }
        };
        packet["context"] = JsonSerializer.SerializeToNode(new { parts = new object[] {
            new { id="wall", spans=new[]{"s0"}, bounds=new[]{0,0,10,20}, geometry=new {
                points=new int[][]{[0,0],[10,0],[10,20],[0,20]}, segments=new int[][]{[0,1],[1,2],[2,3],[3,0]} } },
            new { id="window", spans=new[]{"s0","s1"}, participants=new[]{"wall"}, geometry },
            new { id="separate-copy", spans=Array.Empty<string>(), participants=new[]{"wall"}, geometry }
        }});
        var fields=Entries(Import(packet));var context=Entries(fields[2]);
        context[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[2]=G(context);
        var restored=NativeRecord.Restore(NativeRecord.Capture("paned-window",G(fields)));
        var window=restored.Context.Parts[1];
        Assert.Equal(9,window.Points.Length);Assert.Equal(12,window.Segments.Length);
        Assert.Equal(4,window.Segments.Count(s => ReferenceEquals(s.Left,window.Points[8]) || ReferenceEquals(s.Right,window.Points[8])));
        foreach(var point in window.Points.Skip(4).Take(4))
            Assert.Equal(3,window.Segments.Count(s => ReferenceEquals(s.Left,point) || ReferenceEquals(s.Right,point)));
        Assert.Same(restored.Context.Parts[0].Witness,Assert.Single(window.Parts));
        Assert.Equal(2,window.Profiles.Length);
        Assert.Same(restored.Annotation!.Profiles[0].Witness,window.Profiles[0]);
        Assert.Same(restored.Annotation.Profiles[1].Witness,window.Profiles[1]);
        Assert.NotSame(window.Points[8],restored.Context.Parts[2].Points[8]);
        Assert.Equal("{}",restored.Context.AdapterJson);
    }
}
