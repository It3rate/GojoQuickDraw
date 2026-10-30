using System.Diagnostics;
using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>One admission boundary, one mutable native world, transient rounds.
/// This path holds its selected topology while numeric relations negotiate.
/// Adaptive construction replacement remains an explicit audit experiment.</summary>
public static class ChurchLiveExperiment
{
    public static ChurchExperiment.Result Run(Relation observation,Graph original,JsonElement layout,JsonElement mapping,
        IReadOnlyDictionary<string,IElement> profiles,JsonElement model,string modelHash,string arm,
        int passes,int steps,int candidates,JsonElement? hypotheses)
    {
        var timing=new ChurchExperiment.Timings();var archive=new ChurchExperiment.ArchiveStats();
        var watch=Stopwatch.StartNew();var graph=original;var changes=0;
        if(arm!="shapes-only")
        {
            var admission=NeighborhoodRun.Execute(observation,graph,model,modelHash,true,true,1,candidates,true);
            var fields=NeighborhoodRun.RootFields(admission.Surface);graph=Read((Relation)fields[3]);
            if(!TimeStepSequence.TryRead((Relation)fields[4],out _,out _,out var transitions))
                throw new InvalidDataException("Local admission lost its actual transition count.");
            changes=transitions.Length;
        }
        timing.LocalInference=watch.Elapsed.TotalMilliseconds;watch.Restart();
        Relation[] knots=[];
        if(hypotheses is {} splitPlan)(graph,knots)=ChurchHypotheses.SplitKnots(graph,splitPlan,profiles);
        // Eligibility is assessed ONCE from current source-supported geometry.
        // This is not an assertion that later capture/release has taken place.
        var admissionPass=hypotheses is {} plan&&plan.TryGetProperty("connections",out var connections)
            ?connections.GetProperty("startPass").GetInt32():0;
        var capture=admissionPass>0?new ChurchCaptureController():null;
        var scene=new ChurchScene(original,graph,layout,arm is "shapes-only" or "bidirectional",true,
            moveInk:arm!="shapes-only",hypotheses:hypotheses,capture:capture,pass:admissionPass,live:true);
        scene.Imports.AddRange(knots);scene.BindProfiles(profiles);scene.AddMappings(mapping);
        if(hypotheses is {} knotPlan)ChurchHypotheses.LinkKnots(scene,knotPlan);
        if(arm!="shapes-only")scene.AddLineRules(model);
        if(hypotheses is {} initialPlan)scene.PrepareDerived(initialPlan);
        scene.SealLive();
        timing.SceneImport=watch.Elapsed.TotalMilliseconds;watch.Restart();
        var sourceOrder=original.Edges.Select(e=>e.Source).ToArray();
        var initial=ChurchExperiment.ProjectStage(0,scene.Graph,scene,ChurchSettlement.Errors(scene),changes,0,[],sourceOrder);
        timing.ViewRead=watch.Elapsed.TotalMilliseconds;
        var world=scene.Root;
        var machines=scene.Machines.ToArray();var sites=scene.Graph.Sites.ToArray();
        var memory=new Dictionary<Relation,IElement>(ReferenceEqualityComparer.Instance);
        var executed=0;var quiet=0;var totalMoved=0;var lastMoved=0;ChurchSettlement.Round? latest=null;
        watch.Restart();
        for(var pass=1;pass<=passes;pass++)
        {
            executed=pass;lastMoved=0;
            for(var inner=0;inner<steps;inner++)
            {
                latest=ChurchSettlement.Step(scene,memory);
                lastMoved+=latest.Moved;
                // The round's detailed negotiation is transient. Each machine
                // keeps its current response, never an ancestry of responses.
            }
            scene.PublishGraph(G());totalMoved+=lastMoved;
            if(!ReferenceEquals(world,scene.Root)||!machines.SequenceEqual(scene.Machines)||!sites.SequenceEqual(scene.Graph.Sites))
                throw new InvalidDataException("Live settlement replaced its native world, machines or sites.");
            quiet=lastMoved==0?quiet+1:0;
            if(quiet>=2)break;
        }
        timing.NativeSettlement=watch.Elapsed.TotalMilliseconds;watch.Restart();
        // Derived cell views are observations of final public bounds; replacing
        // these observations must not accumulate another run history.
        if(hypotheses is {} finalPlan)scene.PrepareDerived(finalPlan);
        var transfers=latest is null?Array.Empty<object>():new object[]{new{latest.Requested,latest.Accepted,latest.Blocked,latest.Deferred,latest.Quantum,latest.Rejected}};
        var final=ChurchExperiment.ProjectStage(executed,scene.Graph,scene,ChurchSettlement.Errors(scene),0,lastMoved,transfers,sourceOrder);
        timing.ViewRead+=watch.Elapsed.TotalMilliseconds;watch.Restart();
        var root=ChurchCheckpoint.Capture(scene,executed,retainCurrentResponses:true);
        timing.Capture=watch.Elapsed.TotalMilliseconds;
        return new(root,new{arm,stages=new[]{initial,final},accepted=changes,moved=totalMoved,passes=executed,passBudget=passes,
            archiveMode="current",executionMode="live",admissionPass,retainedVisualStages=2,
            live=new{workingSceneBuilds=1,stableIdentities=true,machines=machines.Length,sites=sites.Length,rules=scene.Rules.Count},
            lossContract=ChurchCheckpoint.LiveLossContract,
            stopReason=quiet>=2?"no accepted movement for two passes; unresolved strain remains":"budget reached; remaining strain is retained",
            qualification="One source-local repair/admission phase, then the SAME native carriers, machines, ports and relations settle in place. Topology and capture choices stay fixed during this run. No repeated classifier inference, structural recapture or per-pass history. Initial and final images are separate observations; current error, blocked transfer and remaining tension remain native.",
            timingMs=timing,archive},timing,archive);
    }
}
