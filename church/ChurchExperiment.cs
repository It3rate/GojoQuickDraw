using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.QuickDraw;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

public static class ChurchExperiment
{
    public const int MaximumPasses=64;
    public static readonly string[] Arms=["local-models","shapes-only","fixed-scaffold","bidirectional"];
    public sealed record Stage(int Pass,View Graph,object[] Parts,ChurchSettlement.ErrorView[] Errors,int Applied,int Moved,
        object[] Transfers,int SourceEdges,int InferredEdges,int Components,int Cycles,double SourceCoverage);
    public sealed record Result(Relation Surface,object View);

    public static Result Run(Relation observation,Graph original,JsonElement layout,JsonElement mapping,IReadOnlyDictionary<string,IElement> profiles,
        JsonElement model,string modelHash,string arm,int passes=6,int steps=2,int candidates=96,JsonElement? hypotheses=null)
    {
        if(!Arms.Contains(arm)||passes is <1 or >MaximumPasses||steps is <1 or >8||candidates is <1 or >256)
            throw new ArgumentException("Choose a declared arm and bounded church experiment budgets.");
        var graph=original;var sourceOrder=original.Edges.Select(e=>e.Source).ToArray();
        var shapes=arm!="local-models";var moveShapes=arm is "shapes-only" or "bidirectional";var moveInk=arm!="shapes-only";
        var topology=arm!="shapes-only";
        var native=new List<IElement>();var stages=new List<Stage>();var quiet=0;var applied=0;var totalMoved=0;
        Dictionary<string,ChurchScene.Part>? retainedParts=null;
        var initial=new ChurchScene(original,original,layout,false,true,moveInk:false,hypotheses:hypotheses);
        initial.BindProfiles(profiles);initial.AddMappings(mapping);initial.AddLineRules(model);
        if(hypotheses is {} initialPlan)initial.PrepareDerived(initialPlan);
        stages.Add(ProjectStage(0,original,initial,ChurchSettlement.Errors(initial),0,0,[],sourceOrder,shapes));
        for(var pass=1;pass<=passes;pass++)
        {
            Relation? local=null;var changes=0;
            if(topology)
            {
                var run=NeighborhoodRun.Execute(observation,graph,model,modelHash,true,true,1,candidates,true);
                local=run.Surface;graph=Read((Relation)NeighborhoodRun.RootFields(local)[3]);
                using var summary=JsonDocument.Parse(JsonSerializer.Serialize(run.Readback,Program.Json));
                changes=summary.RootElement.GetProperty("accepted").GetInt32();applied+=changes;
            }
            if(!shapes)
            {
                native.Add(G(local!,graph.Surface));
                stages.Add(ProjectStage(pass,graph,initial,[],changes,0,[],sourceOrder,false));
                if(changes==0)break;
                continue;
            }
            var before=graph;
            Relation[] knots=[];
            if(hypotheses is {} splitPlan&&pass==1)(graph,knots)=ChurchHypotheses.SplitKnots(graph,splitPlan,profiles);
            var scene=new ChurchScene(original,graph,layout,moveShapes,true,retainedParts,moveInk,hypotheses);
            scene.Imports.AddRange(knots);
            scene.BindProfiles(profiles);scene.AddMappings(mapping);
            if(hypotheses is {} knotPlan)ChurchHypotheses.LinkKnots(scene,knotPlan);
            if(moveInk)scene.AddLineRules(model);
            var beforeErrors=ChurchSettlement.Errors(scene);var memory=new Dictionary<Relation,IElement>(ReferenceEqualityComparer.Instance);
            var rounds=new List<ChurchSettlement.Round>();
            for(var inner=0;inner<steps;inner++)rounds.Add(ChurchSettlement.Step(scene,memory));
            var movement=rounds.Sum(r=>r.Moved);totalMoved+=movement;
            graph=scene.PublishGraph(G(rounds.Select(r=>(IElement)r.Surface).ToArray()));
            if(hypotheses is {} derivedPlan)scene.PrepareDerived(derivedPlan);
            var world=scene.Root;
            var publication=ReadWitness.Create(new Carrier(),before.Surface,world,graph.Surface,G(),G(rounds.Select(r=>(IElement)r.Surface).ToArray()));
            native.Add(G(local is null?new Placeholder():local,world,G(rounds.Select(r=>(IElement)r.Surface).ToArray()),publication,graph.Surface));
            retainedParts=scene.Parts.ToDictionary(p=>p.Id,p=>p);
            var transfers=rounds.Select(r=>(object)new{r.Requested,r.Accepted,r.Blocked,r.Deferred,r.Quantum,r.Rejected}).ToArray();
            stages.Add(ProjectStage(pass,graph,scene,rounds[^1].After,changes,movement,transfers,sourceOrder));
            quiet=changes==0&&movement==0?quiet+1:0;if(quiet>=2)break;
        }
        var axis=new Carrier();
        var root=G(observation,original.Surface,G(native.ToArray()),graph.Surface,G(Relation.Segment(axis,0,passes),new CarrierLocation(axis,native.Count)));
        Validate(root);
        return new(root,new{arm,stages,accepted=applied,moved=totalMoved,passes=native.Count,passBudget=passes,
            stopReason=quiet>=2||arm=="local-models"&&stages[^1].Applied==0?"quiet under selected laws":"budget reached; remaining strain is retained",
            qualification=hypotheses.HasValue?"Source-local learned repair/line/closure opinions plus existing authored church family, support, symmetry, repetition and clearance experts compiled into native port errors. Source-seeded parameters settle; the old final rendering is not a coordinate target. Repeated opinions are not independent observations.":"Source-local learned repair, line and closure opinions; authored source-supported shape constructions and affine error responses. No final church rendering or labels enter inference. This is bounded local topology rewriting coupled with native carrier tension, not a trained church recognizer or a general settlement law."});
    }
    private static Stage ProjectStage(int pass,Graph graph,ChurchScene scene,ChurchSettlement.ErrorView[] errors,int applied,int moved,object[] transfers,IElement[] sourceOrder,bool includeShapes=true)
        =>new(pass,Project(graph,sourceOrder),includeShapes?scene.Parts.Select(p=>(object)new{p.Id,p.Role,p.Prior,points=scene.Points(p),segments=p.Seed.TryGetProperty("shape",out var shape)&&shape.GetString()=="frame"?Array.Empty<int[]>():p.Segments,bounds=scene.Bounds(p)}).Concat(scene.DerivedParts).ToArray():[],
            includeShapes?errors:[],applied,moved,transfers,graph.Edges.Count(e=>!NeighborhoodClosure.IsInferred(e.Source)),graph.Edges.Count(e=>NeighborhoodClosure.IsInferred(e.Source)),Components(graph),Cycles(graph),
            graph.Edges.Where(e=>!NeighborhoodClosure.IsInferred(e.Source)).Sum(e=>{e.Span.TryReadSegment(out _,out var a,out var b);return Math.Abs(b.Ticks-a.Ticks)/(double)PatchEvidence.Q;})/Math.Max(1,sourceOrder.Length));

    public static void Validate(Relation root)
    {
        var f=F(root);if(f.Length!=5)throw new InvalidDataException("Incomplete church run.");
        var source=Read((Relation)f[1]);var final=Read((Relation)f[3]);
        var controller=F(f[4]);if(controller.Length!=2||controller[0] is not Relation budget||!budget.TryReadSegment(out var axis,out var zero,out var limit)||
            zero.Ticks!=0||limit.Ticks is <1 or >MaximumPasses||controller[1] is not CarrierLocation executed||!ReferenceEquals(executed.Carrier,axis)||executed.Ticks!=F(f[2]).Length||executed.Ticks>limit.Ticks)
            throw new InvalidDataException("The church controller lost its declared effort budget.");
        var current=source.Surface;
        foreach(var stage in F(f[2]))
        {
            var row=F(stage);if(row.Length is not (2 or 5))throw new InvalidDataException("Malformed church pass.");
            if(row[0] is Relation local)
            {
                NeighborhoodRun.Validate(local);
                if(!ReferenceEquals(NeighborhoodRun.RootFields(local)[1],current))throw new InvalidDataException("Local pass used a stale graph.");
                current=(Relation)NeighborhoodRun.RootFields(local)[3];
            }
            if(row.Length==2){if(!ReferenceEquals(row[1],current))throw new InvalidDataException("Local graph publication changed identity.");continue;}
            var world=F(row[1]);if(world.Length is not (6 or 7)||!ReferenceEquals(world[0],source.Surface)||!ReferenceEquals(world[1],row[4]))
                throw new InvalidDataException("The connected world lost its source or published graph.");
            if(world.Length==7&&source.Edges.Any(e=>!F(F(world[6])[0]).Any(p=>ReferenceEquals(p,e.Source))))
                throw new InvalidDataException("The world lost a source's actual imported profile identity.");
            var machines=F(world[2]).Cast<Relation>().Select(NegotiatingMachine.Restore).ToArray();
            var ports=machines.SelectMany(m=>m.Ports).ToHashSet<Relation>(ReferenceEqualityComparer.Instance);
            foreach(var error in F(world[4]).Cast<Relation>())
            {
                var obligation=EvidenceObligation.Restore(error);
                if(obligation.Terms.Any(t=>!ports.Contains((Relation)F(t)[0])))throw new InvalidDataException("An error bypassed the public port boundary.");
            }
            if(!ReadWitness.TryRead((Relation)row[3],out var before,out var basis,out var after,out _,out _)||!ReferenceEquals(before,current)||
                !ReferenceEquals(basis,row[1])||!ReferenceEquals(after,row[4]))throw new InvalidDataException("A numeric publication lost its transfer witness.");
            current=(Relation)row[4];_ = Read(current);
        }
        if(!ReferenceEquals(current,final.Surface))throw new InvalidDataException("Final graph is not the recorded pass output.");
    }
}
