using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using static GojoQuickDraw.Learning.PatchEvidence;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.NeighborhoodProposals;

namespace GojoQuickDraw.Learning;

public static class NeighborhoodRun
{
    public sealed record Opinion(Candidate Candidate,NeighborhoodFeatures.Cells Cells,Relation RepairScore,Relation LineScore,
        double RepairSupport,double LineSupport,double Utility,int Unseen,bool Enabled);
    public sealed record Result(Relation Surface,object Readback);

    public static object[] Stages(Relation root)
    {
        var f=Fields(root,5);var initial=Read((Relation)f[1]);var sources=initial.Edges.Select(e=>e.Source).ToArray();
        var ids=new Dictionary<IElement,string>(ReferenceEqualityComparer.Instance);var result=new List<object>{new{title="Source fitted graph",graph=Project(initial,sources,ids)}};
        var i=0;foreach(var round in Rows(f[2]))result.Add(new{title=$"Neighborhood pass {++i}",graph=Project(Read((Relation)Fields(round,4)[2]),sources,ids)});
        return result.ToArray();
    }

    public static Opinion Evaluate(Candidate candidate,JsonElement bundle,string hash,bool joints,bool lines)
    {
        var cells=NeighborhoodFeatures.Repair(candidate);var repair=(Probability:0.0,Unseen:0);
        if(joints&&!candidate.Straightening)repair=NeighborhoodFeatures.Predict(bundle.GetProperty("repair"),cells.Bands);
        var lineReads=new List<IElement>();var lineValues=new List<double>();var unseen=repair.Unseen;
        foreach(var edge in candidate.LineContext is null?candidate.Changed:[])
        {
            var features=NeighborhoodFeatures.Line(edge);var predicted=lines?NeighborhoodFeatures.Predict(bundle.GetProperty("line"),features.Bands):(Probability:0.0,Unseen:0);
            var score=NeighborhoodFeatures.Score(edge.Witness,features.Surface,predicted.Probability,lines?hash:"line-provider-unselected");
            lineReads.Add(G(edge.Witness,features.Surface,score));lineValues.Add(predicted.Probability);if(candidate.Straightening)unseen+=predicted.Unseen;
        }
        if(candidate.LineContext is { } route)
        {
            var features=NeighborhoodFeatures.Route(candidate.Before,route);var predicted=lines?NeighborhoodFeatures.Predict(bundle.GetProperty("line"),features.Bands):(Probability:0.0,Unseen:0);
            lineReads.Add(G(route,features.Surface,NeighborhoodFeatures.Score(route,features.Surface,predicted.Probability,lines?hash:"line-provider-unselected")));
            lineValues.Add(predicted.Probability);unseen+=predicted.Unseen;
        }
        var line=lineValues.DefaultIfEmpty(0).Average();var utility=candidate.Straightening?line:repair.Probability;
        // This is authored compatibility, not multiplication of dependent
        // probabilities. Model opinions and the response rule stay distinct.
        utility-=.12*candidate.Movement/Math.Max(.001,candidate.Focus.Reach)+.08*candidate.Removed.Sum(e=>Length(e.Points))/Math.Max(.001,candidate.Focus.Reach);
        if(!candidate.Straightening&&lines)
        {
            var distortion=candidate.Changed.Select(edge=>
            {
                var prior=PatchGeometry.Measure(Sample(edge.Points,20),true).Bend;
                var next=candidate.After.Edges.Where(e=>ReferenceEquals(e.Source,edge.Source)&&Length(e.Points)>1e-7).Select(e=>PatchGeometry.Measure(Sample(e.Points,20),true).Bend).DefaultIfEmpty(prior).Max();
                return Math.Max(0,next-prior);
            }).DefaultIfEmpty(0).Average();
            utility-=line*distortion;
        }
        if(candidate.Straightening)
        {
            var bend=candidate.Changed.Select(e=>PatchGeometry.Measure(Sample(e.Points,20),true).Bend).DefaultIfEmpty(0).Max();
            if(bend>.15)utility=Math.Min(utility,.5);
        }
        var enabled=candidate.Straightening?lines:joints;
        if(!candidate.Admissible||unseen>3||!enabled)utility=double.NegativeInfinity;
        return new(candidate,cells,NeighborhoodFeatures.Score(candidate.Surface,cells.Surface,repair.Probability,joints?hash:"repair-provider-unselected"),
            G(lineReads.ToArray()),repair.Probability,line,utility,unseen,enabled);
    }
    private static Relation OpinionSurface(Opinion opinion)
    {
        var c=opinion.Candidate;var grade=new Carrier();
        var count=new Carrier();var coverage=opinion.Enabled?(IElement)ReadWitness.Create(new Carrier(),G(opinion.Cells.Surface,opinion.LineScore),
            new Relation(Relation.Segment(count,0,256),Relation.Point(count,3)),new CarrierLocation(count,opinion.Unseen),G(),G(opinion.RepairScore,opinion.LineScore)):new Placeholder();
        var selectedProvider=opinion.Enabled?(IElement)G(opinion.RepairScore,opinion.LineScore):new Placeholder();
        var admissibility=ReadWitness.Create(new Carrier(),c.Surface,G(c.Focus.Surface,Relation.Segment(grade,0,Q),coverage,selectedProvider),
            c.Admissible?c.After.Surface:new Placeholder(),G(c.Removed.Select(e=>(IElement)e.Witness).ToArray()),c.Before.Surface);
        return G(c.Surface,opinion.Cells.Surface,opinion.RepairScore,opinion.LineScore,admissibility);
    }
    public static Result Execute(IElement observation,Graph initial,JsonElement bundle,string hash,bool joints,bool lines,int passes=2,int maximum=192)
    {
        if(passes is <1 or >4||maximum is <1 or >512)throw new InvalidDataException("Choose one to four bounded neighborhood passes and at most 512 candidates.");
        var state=initial;var clock=new Carrier();var history=new List<Relation>();var rounds=new List<IElement>();
        var previewStages=new List<object>{new{title="Source fitted graph",graph=Project(initial,initial.Edges.Select(e=>e.Source).ToArray())}};
        var decisions=new List<object>();var sourceOrder=initial.Edges.Select(e=>e.Source).ToArray();var step=0;var accepted=0;
        for(var pass=0;pass<passes;pass++)
        {
            var beforePass=state;var candidates=Generate(state,maximum);var opinions=candidates.Select(c=>Evaluate(c,bundle,hash,joints,lines)).ToArray();
            var localGroups=opinions.GroupBy(o=>string.Join(',',o.Candidate.Focus.Edges.Select(e=>Array.FindIndex(beforePass.Edges,p=>ReferenceEquals(p.Witness,e.Witness))).Order())+":"+(o.Candidate.Straightening?"line":"joint"));
            var witnesses=opinions.Select(OpinionSurface).ToArray();var decisionRows=new List<IElement>();
            foreach(var group in localGroups)
            {
                var ranked=group.OrderByDescending(o=>o.Utility).ToArray();var selected=ranked[0];var c=selected.Candidate;
                var fresh=c.Focus.Edges.All(e=>state.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness)));
                var apply=selected.Utility>=.68&&fresh;
                var opinion=witnesses[Array.FindIndex(opinions,o=>ReferenceEquals(o,selected))];
                var scoreAxis=new Carrier();var utility=ReadWitness.Create(new Carrier(),G(selected.RepairScore,selected.LineScore,selected.Cells.Surface),
                    Relation.Segment(scoreAxis,0,Q),double.IsFinite(selected.Utility)?new CarrierLocation(scoreAxis,checked((long)Math.Round(selected.Utility*Q))):new Placeholder(),G(),opinion);
                var decisionBasis=G(utility,Relation.Segment(scoreAxis,680000,Q),state.Surface);
                var stale=G(c.Focus.Edges.Where(e=>!state.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness))).Select(e=>(IElement)e.Witness).ToArray());
                decisions.Add(new{pass=pass+1,operation=c.Description,center=c.Focus.Center,reach=c.Focus.Reach,radius=c.Focus.Radius,
                    neighborhoodSites=c.Focus.Sites.Length,neighborhoodEdges=c.Focus.Edges.Length,
                    status=apply?"applied":!fresh?"stale; reevaluate next pass":!c.Admissible?"blocked":!selected.Enabled?"provider unselected":selected.Unseen>3?"unfamiliar feature cells; keep unresolved":"keep / unresolved",
                    repairSupport=selected.RepairSupport,lineSupport=selected.LineSupport,unseenFeatureCells=selected.Unseen,utility=double.IsFinite(selected.Utility)?selected.Utility:(double?)null,
                    alternatives=ranked.Take(3).Select(o=>new{operation=o.Candidate.Description,repair=o.RepairSupport,line=o.LineSupport,unseenFeatureCells=o.Unseen,admissible=o.Candidate.Admissible,issues=o.Candidate.Issues}).ToArray(),
                    issues=c.Issues.Concat(selected.Unseen>3?new[]{"Input includes more unfamiliar feature cells than the provider coverage limit permits."}:Array.Empty<string>()).ToArray(),sources=c.Focus.Edges.Select(e=>"p"+Array.FindIndex(sourceOrder,s=>ReferenceEquals(s,e.Source))).Distinct().ToArray(),
                    removedIntervals=c.Removed.Select(e=>{e.Span.TryReadSegment(out _,out var a,out var b);return new{source="p"+Array.FindIndex(sourceOrder,s=>ReferenceEquals(s,e.Source)),from=a.Ticks/(double)Q,to=b.Ticks/(double)Q};}).ToArray()});
                if(!apply)
                {
                    decisionRows.Add(ReadWitness.Create(new Carrier(),opinion,decisionBasis,new Placeholder(),stale,state.Surface));continue;
                }
                // Commit one local replacement to the current interpreted graph.
                // Outside participation is carried by identity, not re-solved.
                var old=c.Before.Edges.Select(e=>e.Witness).ToHashSet<Relation>(ReferenceEqualityComparer.Instance);
                var changed=c.Changed.Select(e=>e.Witness).Concat(c.Removed.Select(e=>e.Witness)).ToHashSet<Relation>(ReferenceEqualityComparer.Instance);
                var removedFromCandidate=c.Before.Edges.Where(e=>!c.After.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness))).Select(e=>e.Witness).ToHashSet<Relation>(ReferenceEqualityComparer.Instance);
                foreach(var witness in removedFromCandidate)changed.Add(witness);
                var additions=c.After.Edges.Where(e=>!old.Contains(e.Witness)).ToArray();
                var next=Create(state.Frame,state.Edges.Where(e=>!changed.Contains(e.Witness)).Concat(additions));
                var application=ReadWitness.Create(new Carrier(),state.Surface,opinion,next.Surface,G(c.Removed.Select(e=>(IElement)e.Witness).ToArray()),c.Surface);
                decisionRows.Add(ReadWitness.Create(new Carrier(),opinion,decisionBasis,application,stale,state.Surface));
                history.Add(TimeStepSequence.Step(new Carrier(),clock,step,step+1,state.Surface,application,next.Surface,G()));step++;state=next;accepted++;
            }
            rounds.Add(G(beforePass.Surface,G(witnesses),state.Surface,G(decisionRows.ToArray())));
            previewStages.Add(new{title=$"Neighborhood pass {pass+1}",graph=Project(state,sourceOrder)});
            if(ReferenceEquals(beforePass.Surface,state.Surface))break;
        }
        var timeline=TimeStepSequence.Create(new Carrier(),clock,initial.Surface,history.ToArray());
        var root=G(observation,initial.Surface,G(rounds.ToArray()),state.Surface,timeline);
        Validate(root);
        return new(root,new{initial=Project(initial,sourceOrder),final=Project(state,sourceOrder),stages=Stages(root),decisions,accepted,
            passes=previewStages.Count-1,qualification="Two source-local learned opinions plus authored structural compatibility. Actual target sites and source spans change; source acquisition is unchanged. This is bounded local rewriting, not general kernel convergence or learned church priors."});
    }
    public static void Validate(Relation root)
    {
        var f=Fields(root,5);var initial=Read((Relation)f[1]);var final=Read((Relation)f[3]);
        var sources=initial.Edges.Select(e=>e.Source).ToHashSet<IElement>(ReferenceEqualityComparer.Instance);
        if(final.Edges.Any(e=>!sources.Contains(e.Source)))throw new InvalidDataException("Final graph introduced an unsupported source branch.");
        IElement previousRound=initial.Surface;
        foreach(var round in Rows(f[2]))
        {
            var r=Fields(round,4);var before=Read((Relation)r[0]);_=Read((Relation)r[2]);
            if(!ReferenceEquals(previousRound,before.Surface))throw new InvalidDataException("A neighborhood pass is not based on the previous pass.");
            IElement roundState=before.Surface;
            foreach(var opinion in Rows(r[1]))
            {
                var o=Fields(opinion,5);
                if(!ReadWitness.TryRead((Relation)o[0],out var selected,out _,out var result,out _,out _)||!ReferenceEquals(selected,before.Surface))
                    throw new InvalidDataException("A candidate lost its selected source graph.");
                var after=Read((Relation)result);if(after.Edges.Any(e=>!sources.Contains(e.Source)))throw new InvalidDataException("A candidate invented a source branch.");
                _=NeighborhoodFeatures.ReadCells((Relation)o[1]);_=NeighborhoodFeatures.ReadScore((Relation)o[2],o[0],(Relation)o[1]);
                foreach(var row in Rows(o[3])){var l=Fields(row,3);_=NeighborhoodFeatures.ReadCells((Relation)l[1]);_=NeighborhoodFeatures.ReadScore((Relation)l[2],l[0],(Relation)l[1]);}
                if(!ReadWitness.TryRead((Relation)o[4],out var candidate,out _,out var allowed,out _,out _)||!ReferenceEquals(candidate,o[0]) ||
                    allowed is not Placeholder&&!ReferenceEquals(allowed,after.Surface))throw new InvalidDataException("Candidate admissibility lost its actual target.");
            }
            foreach(var element in Rows(r[3]))
            {
                if(!ReadWitness.TryRead((Relation)element,out var choice,out var basis,out var application,out _,out var current)||
                    !Rows(r[1]).Any(o=>ReferenceEquals(o,choice)))throw new InvalidDataException("A decision selected an unrelated local opinion.");
                var b=Fields(basis,3);
                if(!ReferenceEquals(current,roundState)||!ReferenceEquals(b[2],current)||!ReadWitness.TryRead((Relation)b[0],out _,out _,out var utility,out _,out var selected)||!ReferenceEquals(selected,choice)||
                    b[1] is not Relation cut||!cut.TryReadSegment(out var axis,out var lo,out var hi)||lo.Ticks!=680000||hi.Ticks!=Q)
                    throw new InvalidDataException("A decision lost its local response threshold.");
                if(application is not Placeholder)
                {
                    var op=Fields(choice,5);
                    if(utility is not CarrierLocation score||!ReferenceEquals(score.Carrier,axis)||score.Ticks<lo.Ticks||
                        !ReadWitness.TryRead((Relation)op[4],out _,out _,out var allowed,out _,out _)||allowed is Placeholder||
                        !ReadWitness.TryRead((Relation)application,out var source,out var cause,out var next,out _,out _)||!ReferenceEquals(source,current)||!ReferenceEquals(cause,choice))
                        throw new InvalidDataException("A committed application has no admissible local score/source route.");
                    roundState=next;
                }
            }
            if(!ReferenceEquals(roundState,r[2]))throw new InvalidDataException("A pass output is not its retained decision sequence.");
            previousRound=roundState;
        }
        if(!ReferenceEquals(previousRound,final.Surface))throw new InvalidDataException("Final graph is not the last neighborhood pass.");
        if(!TimeStepSequence.TryRead((Relation)f[4],out _,out var first,out var steps)||!ReferenceEquals(first,initial.Surface))throw new InvalidDataException("Repair timeline lost its initial graph.");
        IElement state=initial.Surface;
        foreach(var step in steps)
        {
            if(!TimeStepSequence.TryReadStep(step,out _,out _,out _,out var before,out var transition,out var after,out _)||!ReferenceEquals(before,state)||
                transition is not Relation application||!ReadWitness.TryRead(application,out var selected,out _,out var result,out _,out _)||!ReferenceEquals(selected,before)||!ReferenceEquals(result,after))
                throw new InvalidDataException("A local application lost its before/after correspondence.");
            _=Read((Relation)after);state=after;
        }
        if(!ReferenceEquals(state,final.Surface))throw new InvalidDataException("Final graph is not the result of the retained local applications.");
    }
}
