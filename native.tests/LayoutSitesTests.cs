using System.Text.Json.Nodes;
using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void SeparatePartitionedCapsRetainTheirTowerAndGroundContactsAfterLabelsAreRemoved()
    {
        var packet = ObjectFixture();
        var rows = new List<object> { new {
            id = "ground", spans = Array.Empty<string>(),
            geometry = new { points = new double[][] { [0, 20], [10, 20], [20, 20], [30, 20] },
                             segments = new int[][] { [0, 1], [1, 2], [2, 3] } }
        }};
        object Site(string part, int index) => new { part, index };
        for (var i = 0; i < 2; i++)
        {
            var x = i * 20.0; var name = $"tower-{i}";
            rows.Add(new {
                id = name, spans = new[] { "s1" }, participants = new[] { "ground" },
                geometry = new {
                    points = new double[][] { [x, 0], [x+10, 0], [x+10, 20], [x, 20], [x+5, 0] },
                    segments = new int[][] { [0, 4], [4, 1], [1, 2], [2, 3], [3, 0] },
                    siteSources = new object?[] { null, null, Site("ground", i*2+1), Site("ground", i*2), null }
                }
            });
            var points = Enumerable.Range(0, 33).Select(j => new[] {
                Math.Round(x+5-5*Math.Cos(j*Math.PI/32), 3), Math.Round(-6*Math.Sin(j*Math.PI/32), 3)
            }).Append([x+5, 0]).ToArray();
            rows.Add(new {
                id = $"cap-{i}", spans = new[] { "s0" }, participants = new[] { name },
                geometry = new {
                    points,
                    segments = Enumerable.Range(0, 34).Select(j => new[] { j, (j+1)%34 }).ToArray(),
                    siteSources = Enumerable.Range(0, 34).Select(j => j == 0 ? Site(name, 0) :
                        j == 32 ? Site(name, 1) : j == 33 ? Site(name, 4) : null).ToArray()
                }
            });
        }
        packet["context"] = JsonSerializer.SerializeToNode(new { parts = rows });
        var fields = Entries(Import(packet)); var context = Entries(fields[2]);
        context[5] = EncodedTextSurface.Create(new Carrier(), "{}"); fields[2] = G(context);
        var restored = NativeRecord.Restore(NativeRecord.Capture("multiple-proposed-caps", G(fields)));
        var parts = restored.Context.Parts;
        Assert.Equal(5, parts.Length);
        for (var i = 0; i < 2; i++)
        {
            var tower = parts[i*2+1]; var cap = parts[i*2+2];
            Assert.Same(parts[0].Points[i*2], tower.Points[3]);
            Assert.Same(parts[0].Points[i*2+1], tower.Points[2]);
            Assert.Same(tower.Points[0], cap.Points[0]);
            Assert.Same(tower.Points[1], cap.Points[32]);
            Assert.Same(tower.Points[4], cap.Points[33]);
            Assert.Same(cap.Points[33], cap.Segments[32].Right);
            Assert.Same(cap.Points[0], cap.Segments[33].Right);
        }
        Assert.NotSame(parts[2].Points[16], parts[4].Points[16]);
        Assert.NotSame(parts[1].Witness, parts[3].Witness);
    }

    [Fact]
    public void ProposedRoofSharesActualWallSitesWithoutAliasingAcquisition()
    {
        var packet = ObjectFixture();
        packet["context"]!["parts"]![1]!["participants"] = new JsonArray("wall");
        packet["context"]!["parts"]![1]!["geometry"]!["siteSources"] = JsonNode.Parse("""
            [{"part":"wall","index":0},null,{"part":"wall","index":1}]
            """);
        var fields = Entries(Import(packet));
        var context = Entries(fields[2]); context[5] = EncodedTextSurface.Create(new Carrier(), "{}");
        fields[2] = G(context);
        var restored = NativeRecord.Restore(NativeRecord.Capture("shared-layout", G(fields)));
        var wall = restored.Context.Parts[0]; var roof = restored.Context.Parts[1];
        Assert.Same(wall.Points[0], roof.Points[0]);
        Assert.Same(wall.Points[1], roof.Points[2]);
        Assert.NotSame(wall.Points[0], roof.Points[1]);
        Assert.Same(roof.Points[0], roof.Segments[0].Left);
        Assert.Same(roof.Points[2], roof.Segments[2].Left);
        Assert.DoesNotContain(roof.Points[0].Left, restored.Acquisition.Samples.Select(p => (IElement)p.Event));
        var separate = NativeRecord.Restore(NativeRecord.Capture("equal-not-shared", Import(ObjectFixture())));
        Assert.NotSame(separate.Context.Parts[0].Points[0], separate.Context.Parts[1].Points[0]);
    }

    [Theory]
    [InlineData("missing", 0, true)]
    [InlineData("window-a", 0, true)]
    [InlineData("wall", 99, true)]
    [InlineData("wall", 1, true)]
    [InlineData("wall", 0, false)]
    public void LayoutSiteImportsRejectUnselectedForwardMissingOrUnequalSites(string part, int index, bool selected)
    {
        var packet = ObjectFixture(); var roof = packet["context"]!["parts"]![1]!;
        roof["participants"] = selected ? new JsonArray("wall") : new JsonArray();
        roof["geometry"]!["siteSources"] = new JsonArray(new JsonObject { ["part"] = part, ["index"] = index }, null, null);
        Assert.Throws<InvalidDataException>(() => Import(packet));
    }
}
