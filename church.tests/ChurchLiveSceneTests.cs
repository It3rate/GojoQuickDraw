using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.ChurchStudy;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchLiveSceneTests
{
    private static JsonElement Json(string value)=>JsonDocument.Parse(value).RootElement.Clone();
    private static Graph Ink()=>FromJson(Json("""{"sites":[[10.137,60.183],[180.167,60.229],[180.167,210.229]],"edges":[{"a":0,"b":1,"handles":[[45.125,61.175],[120.625,59.375]]},{"a":1,"b":2}]}"""));
    private static JsonElement Layout()=>Json("""{"parts":[{"id":"wall","role":"building","status":"proposed","bounds":[22,60,180,210],"geometry":{"points":[[22,60],[180,60],[180,210],[22,210]],"segments":[[0,1],[1,2],[2,3],[3,0]]}}]}""");
    private static void Bind(ChurchScene scene)=>scene.BindProfiles(scene.Source.Edges.Select((e,i)=>(Id:"p"+i,e.Source)).ToDictionary(v=>v.Id,v=>v.Source));
    private static double[] Values(ChurchScene scene)=>scene.Machines.SelectMany(m=>m.Points.SelectMany(Gojo.Experiments.ConstructionMachines.Program.XY)).ToArray();
    private static ChurchScene World(Graph ink,bool live)
    {
        var scene=new ChurchScene(ink,ink,Layout(),true,true,live:live);Bind(scene);
        scene.Add("Shared boundary","mapping",0,0,4,ink.Surface,
            (scene.Sites[scene.Graph.Sites[0]].Port(0,0),1),(ChurchScene.Left(scene.Parts[0]),-1));
        return scene;
    }

    [Fact] public void NumericPassesRetainTheWorkingRootActorsSitesAndSourceAcquisition()
    {
        var ink=Ink();var acquired=GojoNumericFormat.Encode(ink.Surface);var scene=World(ink,true);
        var root=scene.SealLive();var graphRoot=scene.Graph.Surface;var sites=scene.Graph.Sites.ToArray();
        var edges=scene.Graph.Edges.ToArray();var machines=scene.Machines.ToArray();var port=ChurchScene.Left(scene.Parts[0]);
        var before=XY(scene.Graph.Sites[0]);var moved=0;
        for(var i=0;i<12;i++)
        {
            var round=ChurchSettlement.Step(scene);moved+=round.Moved;scene.PublishGraph(round.Surface);
            Assert.Same(root,scene.Root);Assert.Same(graphRoot,scene.Graph.Surface);
            Assert.All(sites.Select((site,k)=>(site,k)),item=>Assert.Same(item.site,scene.Graph.Sites[item.k]));
            Assert.All(edges.Select((edge,k)=>(edge,k)),item=>Assert.Same(item.edge.Witness,scene.Graph.Edges[item.k].Witness));
            Assert.All(machines.Select((machine,k)=>(machine,k)),item=>Assert.Same(item.machine,scene.Machines[item.k]));
            Assert.Same(port,ChurchScene.Left(scene.Parts[0]));
        }
        Assert.True(moved>0);Assert.NotEqual(before[0],XY(scene.Graph.Sites[0])[0]);
        Assert.Equal(acquired,GojoNumericFormat.Encode(ink.Surface));
        Assert.NotSame(ink.Sites[0],scene.Graph.Sites[0]);
        Assert.Same(scene.Graph.Edges[0].B,scene.Graph.Edges[1].A);
        Assert.Throws<InvalidOperationException>(()=>scene.Add("Late mutation","test",0,1,1,root,(port,1)));
    }

    [Fact] public void LiveAndHistoryMachinesGiveTheSameSettlementForTheSameAdmittedLaws()
    {
        var ink=Ink();var audit=World(ink,false);var live=World(ink,true);live.SealLive();
        var aMemory=new Dictionary<Relation,IElement>(ReferenceEqualityComparer.Instance);
        var bMemory=new Dictionary<Relation,IElement>(ReferenceEqualityComparer.Instance);
        for(var i=0;i<12;i++)
        {
            var a=ChurchSettlement.Step(audit,aMemory);var b=ChurchSettlement.Step(live,bMemory);
            Assert.Equal(Values(audit),Values(live));Assert.Equal(a.Before,b.Before);Assert.Equal(a.After,b.After);
            Assert.Equal((a.Moved,a.Requested,a.Accepted,a.Blocked,a.Deferred,a.Quantum,a.Rejected),
                (b.Moved,b.Requested,b.Accepted,b.Blocked,b.Deferred,b.Quantum,b.Rejected));
            live.PublishGraph(b.Surface);
        }
        // Audit publication may be done once after its numeric passes; the
        // live path updates the same working sites after every pass.
        audit.PublishGraph(G());
        // The native affine read retains its fractional remainder separately;
        // the earlier host interpolation rounds once at the end instead.
        foreach(var pair in audit.Graph.Edges.SelectMany(e=>e.Points.SelectMany(p=>p)).Zip(live.Graph.Edges.SelectMany(e=>e.Points.SelectMany(p=>p))))
            Assert.InRange(Math.Abs(pair.First-pair.Second),0,2d/PatchEvidence.Q);
    }

    [Fact] public void RepeatedPublicationIsIdempotentAndInterpolatesCurveHandlesFromAcceptedMotion()
    {
        var ink=Ink();var scene=World(ink,true);scene.SealLive();
        var before=ink.Edges[0];var round=ChurchSettlement.Step(scene);scene.PublishGraph(round.Surface);
        var after=scene.Graph.Edges[0];
        for(var i=0;i<before.Handles.Length;i++)
            for(var k=0;k<2;k++)
            {
                var t=(i+1d)/(before.Handles.Length+1);
                var expected=before.Points[i+1][k]+(after.Points[0][k]-before.Points[0][k])*(1-t)+(after.Points[^1][k]-before.Points[^1][k])*t;
                Assert.Equal(expected,after.Points[i+1][k],5);
            }
        var values=after.Points.SelectMany(p=>p).ToArray();var current=scene.CurrentGraphPublication;
        scene.PublishGraph(G());Assert.NotSame(current,scene.CurrentGraphPublication);
        Assert.Equal(values,scene.Graph.Edges[0].Points.SelectMany(p=>p));
        Assert.Same(after.Handles[0],scene.Graph.Edges[0].Handles[0]);
    }

    [Fact] public void EqualIndependentSitesRemainDistinctAndFixedInkKeepsItsExactRead()
    {
        var ink=FromJson(Json("""{"sites":[[10.137,10.173],[40,10],[10.137,10.173],[10,40]],"edges":[{"a":0,"b":1},{"a":2,"b":3}]}"""));
        var scene=new ChurchScene(ink,ink,Layout(),false,false,moveInk:false,live:true);Bind(scene);scene.SealLive();
        var round=ChurchSettlement.Step(scene);scene.PublishGraph(round.Surface);
        Assert.Equal(2,Components(scene.Graph));Assert.NotSame(scene.Graph.Edges[0].A,scene.Graph.Edges[1].A);
        Assert.Equal(XY(ink.Sites[0]),XY(scene.Graph.Sites[0]));
    }

    [Fact] public void ReachableWorkingStructureDoesNotGrowDuringContinuingMovement()
    {
        var ink=Ink();var scene=new ChurchScene(ink,ink,Layout(),true,true,live:true);Bind(scene);
        var port=scene.Sites[scene.Graph.Sites[0]].Port(0,0);
        scene.Add("Distant request","mapping",ChurchScene.Coordinate(900),ChurchScene.Coordinate(900),20,ink.Surface,(port,1));
        scene.SealLive();
        var first=GojoEncodedSnapshot.CaptureGraph(scene.Root);var moved=0;int retainedAtTwenty=0;
        int Retained()=>GojoEncodedSnapshot.CaptureGraph(G(scene.Root,scene.CurrentGraphPublication!,G(scene.Machines.Select(m=>(IElement)m.Surface).ToArray()))).Relations.Length;
        for(var i=0;i<40;i++)
        {
            var round=ChurchSettlement.Step(scene);moved+=round.Moved;scene.PublishGraph(round.Surface);
            if(i==19)retainedAtTwenty=Retained();
        }
        var last=GojoEncodedSnapshot.CaptureGraph(scene.Root);
        Assert.True(moved>20);Assert.Equal(first.Relations.Length,last.Relations.Length);
        Assert.Equal(first.Carriers.Length,last.Carriers.Length);Assert.Equal(first.TickRefs.Length,last.TickRefs.Length);
        // Include current publications and receipts, not only the stable
        // computation root. This would grow quickly with ancestral receipts.
        Assert.InRange(Retained(),1,retainedAtTwenty*2);
        var checkpoint=ChurchCheckpoint.Capture(scene,40);
        var restored=ChurchCheckpoint.Restore((Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(checkpoint))[0]);
        Assert.Equal(scene.Graph.Edges.SelectMany(e=>e.Points.SelectMany(p=>p)),restored.Current.Edges.SelectMany(e=>e.Points.SelectMany(p=>p)));
    }
}
