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
    public static IElement[] RootFields(Relation root)
    {
        var f=Rows(root);if(f.Length is not (5 or 6 or 7))throw new InvalidDataException("Incomplete neighborhood run surface.");return f;
    }
    private static IElement[] RoundFields(IElement round)
    {
        var f=Rows(round);if(f.Length is not (4 or 5))throw new InvalidDataException("Incomplete neighborhood round surface.");return f;
    }
    private static NeighborhoodContacts.Contact[] Contacts(IElement[] fields,int index,Graph graph)
        =>fields.Length>index?Rows(fields[index]).Select(r=>NeighborhoodContacts.Read((Relation)r,graph)).ToArray():[];
    private static bool Fresh(Relation focus,Graph before,Graph current)
    {
        var f=Fields(focus,5);if(!ReferenceEquals(f[0],before.Surface))return false;
        var edges=Rows(f[2]).Select(w=>before.Edges.Single(e=>ReferenceEquals(e.Witness,w))).ToArray();
        if(edges.Any(e=>!current.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness))))return false;
        var sites=Rows(f[1]).Cast<Relation>().Concat(edges.SelectMany(e=>new[]{e.A,e.B})).Distinct<Relation>(ReferenceEqualityComparer.Instance);
        return sites.All(s=>before.Edges.Where(e=>ReferenceEquals(e.A,s)||ReferenceEquals(e.B,s)).Select(e=>e.Witness).ToHashSet(ReferenceEqualityComparer.Instance)
            .SetEquals(current.Edges.Where(e=>ReferenceEquals(e.A,s)||ReferenceEquals(e.B,s)).Select(e=>e.Witness)));
    }

    public static object[] Stages(Relation root)
    {
        var f=RootFields(root);var initial=Read((Relation)f[1]);var sources=initial.Edges.Select(e=>e.Source).ToArray();var rounds=Rows(f[2]).Select(RoundFields).ToArray();
        var ids=new Dictionary<IElement,string>(ReferenceEqualityComparer.Instance);var initialContacts=rounds.Length>0?Contacts(rounds[0],4,initial):[];
        var contexts=f.Length==7?Rows(f[6]).Cast<Relation>().ToArray():[];
        var result=new List<object>{new{title="Source fitted graph",graph=Project(initial,sources,ids,initialContacts,
            contexts.Length>0?NeighborhoodContinuations.ReadAll(contexts[0],initial):[])}};
        for(var i=0;i<rounds.Length;i++)
        {
            var graph=Read((Relation)rounds[i][2]);var contacts=i+1<rounds.Length?Contacts(rounds[i+1],4,graph):f.Length>=6?Contacts(Fields(f[5],4),3,graph):[];
            result.Add(new{title=$"Neighborhood pass {i+1}",graph=Project(graph,sources,ids,contacts,
                contexts.Length>i+1?NeighborhoodContinuations.ReadAll(contexts[i+1],graph):[])});
        }
        return result.ToArray();
    }

    public static Opinion Evaluate(Candidate candidate,JsonElement bundle,string hash,bool joints,bool lines,bool closures=true)
    {
        var closure=candidate.Completion is not null;var hasClosure=closures&&bundle.TryGetProperty("closure",out _);
        var cells=closure?NeighborhoodClosure.Features(NeighborhoodClosure.Read(candidate.Completion!)):NeighborhoodFeatures.Repair(candidate);var repair=(Probability:0.0,Unseen:0);
        if(closure&&hasClosure)repair=NeighborhoodFeatures.Predict(bundle.GetProperty("closure"),cells.Bands);
        else if(!closure&&joints&&!candidate.Straightening)repair=NeighborhoodFeatures.Predict(bundle.GetProperty("repair"),cells.Bands);
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
        if(!closure&&!candidate.Straightening&&lines)
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
        var enabled=closure?hasClosure:candidate.Straightening?lines:joints;
        if(!candidate.Admissible||unseen>3||!enabled)utility=double.NegativeInfinity;
        return new(candidate,cells,NeighborhoodFeatures.Score(candidate.Surface,cells.Surface,repair.Probability,enabled?hash:"provider-unselected"),
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
    public static Result Execute(IElement observation,Graph initial,JsonElement bundle,string hash,bool joints,bool lines,int passes=4,int maximum=192,bool closures=true)
    {
        if(passes is <1 or >8||maximum is <1 or >512)throw new InvalidDataException("Choose one to eight bounded neighborhood passes and at most 512 candidates.");
        var state=initial;var clock=new Carrier();var history=new List<Relation>();var rounds=new List<IElement>();
        var contexts=new List<IElement>{G(initial.Surface,G())};var hasProviders=joints||lines||closures&&bundle.TryGetProperty("closure",out _);
        var previewStages=new List<object>{new{title="Source fitted graph",graph=Project(initial,initial.Edges.Select(e=>e.Source).ToArray())}};
        var decisions=new List<object>();var sourceOrder=initial.Edges.Select(e=>e.Source).ToArray();var step=0;var accepted=0;var quiet=false;var passCounts=new List<int>();
        for(var pass=0;pass<passes;pass++)
        {
            var beforePass=state;var beforeAccepted=accepted;var contacts=NeighborhoodContacts.Discover(state);
            var closureCandidates=closures&&bundle.TryGetProperty("closure",out _)?NeighborhoodClosure.Discover(state,Math.Max(1,maximum/4)).Select(NeighborhoodClosure.Candidate).ToArray():[];
            var candidates=closureCandidates.Concat(Generate(state,Math.Max(1,maximum-closureCandidates.Length),contacts)).Take(maximum).ToArray();
            var opinions=candidates.Select(c=>Evaluate(c,bundle,hash,joints,lines,closures)).ToArray();
            var localGroups=opinions.GroupBy(o=>string.Join(',',o.Candidate.Focus.Edges.Select(e=>Array.FindIndex(beforePass.Edges,p=>ReferenceEquals(p.Witness,e.Witness))).Order())+"/"+
                string.Join(',',o.Candidate.Focus.Sites.Select(s=>Array.FindIndex(beforePass.Sites,p=>ReferenceEquals(p,s))).Order())+":"+(o.Candidate.Completion is not null?"closure":o.Candidate.Straightening?"line":"joint"));
            var witnesses=opinions.Select(OpinionSurface).ToArray();var decisionRows=new List<IElement>();
            // Schedule stronger current local proposals before weaker overlapping
            // fragments. This is an authored priority policy, not a global solve.
            foreach(var group in localGroups.OrderByDescending(g=>g.Max(o=>o.Utility)))
            {
                var ranked=group.OrderByDescending(o=>o.Utility).ToArray();var selected=ranked[0];var c=selected.Candidate;
                var fresh=Fresh(c.Focus.Surface,beforePass,state);
                var apply=selected.Utility>=.68&&fresh;
                var opinion=witnesses[Array.FindIndex(opinions,o=>ReferenceEquals(o,selected))];
                var scoreAxis=new Carrier();var utility=ReadWitness.Create(new Carrier(),G(selected.RepairScore,selected.LineScore,selected.Cells.Surface),
                    Relation.Segment(scoreAxis,0,Q),double.IsFinite(selected.Utility)?new CarrierLocation(scoreAxis,checked((long)Math.Round(selected.Utility*Q))):new Placeholder(),G(),opinion);
                var decisionBasis=G(utility,Relation.Segment(scoreAxis,680000,Q),state.Surface);
                var stale=G(c.Focus.Edges.Where(e=>!state.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness))).Select(e=>(IElement)e.Witness).ToArray());
                decisions.Add(new{pass=pass+1,operation=c.Description,provider=c.Completion is not null?"closure":c.Straightening?"line":"joint",center=c.Focus.Center,reach=c.Focus.Reach,radius=c.Focus.Radius,
                    neighborhoodSites=c.Focus.Sites.Length,neighborhoodEdges=c.Focus.Edges.Length,
                    status=apply?"applied":!fresh?"stale; reevaluate next pass":!c.Admissible?"blocked":!selected.Enabled?"provider unselected":selected.Unseen>3?"unfamiliar feature cells; keep unresolved":"keep / unresolved",
                    repairSupport=selected.RepairSupport,lineSupport=selected.LineSupport,unseenFeatureCells=selected.Unseen,utility=double.IsFinite(selected.Utility)?selected.Utility:(double?)null,
                    endpointObservation=c.Observation is null?null:ProjectionHint(c.Observation,beforePass,sourceOrder),
                    closureObservation=c.Completion is null?null:ClosureHint(c.Completion),
                    alternatives=ranked.Take(3).Select(o=>new{operation=o.Candidate.Description,repair=o.RepairSupport,line=o.LineSupport,unseenFeatureCells=o.Unseen,admissible=o.Candidate.Admissible,
                        targetKinds=o.Candidate.After.Sites.Where(s=>PatchGeometry.Distance(XY(s),o.Candidate.Focus.Center)<=o.Candidate.Focus.Radius).Select(s=>Kind(o.Candidate.After,s)).Distinct().ToArray(),issues=o.Candidate.Issues}).ToArray(),
                    issues=c.Issues.Concat(selected.Unseen>3?new[]{"Input includes more unfamiliar feature cells than the provider coverage limit permits."}:Array.Empty<string>()).ToArray(),sources=c.Focus.Edges.Select(e=>NeighborhoodClosure.IsInferred(e.Source)?"inferred closure":"p"+Array.FindIndex(sourceOrder,s=>ReferenceEquals(s,e.Source))).Distinct().ToArray(),
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
            rounds.Add(G(beforePass.Surface,G(witnesses),state.Surface,G(decisionRows.ToArray()),G(contacts.Select(c=>(IElement)c.Witness).ToArray())));
            passCounts.Add(accepted-beforeAccepted);
            contexts.Add(hasProviders?NeighborhoodContinuations.Build(state):G(state.Surface,G()));
            previewStages.Add(new{title=$"Neighborhood pass {pass+1}",graph=Project(state,sourceOrder)});
            if(ReferenceEquals(beforePass.Surface,state.Surface)){quiet=true;break;}
        }
        var timeline=TimeStepSequence.Create(new Carrier(),clock,initial.Surface,history.ToArray());
        var passAxis=new Carrier();var lastRound=RoundFields(rounds[^1]);var finalContacts=NeighborhoodContacts.Discover(state);
        var controller=G(Relation.Segment(passAxis,0,passes),new CarrierLocation(passAxis,rounds.Count),new Relation(lastRound[0],lastRound[2]),G(finalContacts.Select(c=>(IElement)c.Witness).ToArray()));
        var root=G(observation,initial.Surface,G(rounds.ToArray()),state.Surface,timeline,controller,G(contexts.ToArray()));
        Validate(root);
        return new(root,new{initial=Project(initial,sourceOrder),final=Project(state,sourceOrder),stages=Stages(root),decisions,accepted,
            passes=previewStages.Count-1,passBudget=passes,appliedByPass=passCounts,stopReason=quiet?"no accepted local changes":"pass budget reached; unresolved work may remain",
            qualification="Source-local providers reread each changed graph; repeated opinions are not independent evidence. Site projections observe distinct sites until an explicit binding. Closure passages are unobserved hypotheses; I continuation reads retain source splits. This is bounded local rewriting, not general Gojo tension flow or object segmentation."});
    }
    private static object ClosureHint(Relation observation)
    {
        var p=NeighborhoodClosure.Read(observation);return new{from=XY(p.A),to=XY(p.B),gapRatio=p.Gap/p.Reach,pathEdges=p.Path.Length,
            acquiredStroke=NeighborhoodClosure.Order(p.Path[0].Edge,p.A).Stroke,otherStroke=NeighborhoodClosure.Order(p.Path[^1].Edge,p.B).Stroke,
            timestampsAvailable=NeighborhoodClosure.Order(p.Path[0].Edge,p.A).Time is not null&&NeighborhoodClosure.Order(p.Path[^1].Edge,p.B).Time is not null,
            basis="Inferred connector supported by an existing ordered route; no acquired line reference. Closure alone does not establish fill or object membership."};
    }
    private static object ProjectionHint(Relation observation,Graph graph,IElement[] sources)
    {
        var c=NeighborhoodContacts.Read(observation,graph);return new{from=XY(c.Endpoint),to=XY(c.Point),parameter=c.T,gapRatio=c.Gap/c.Reach,
            targetSource="p"+Array.FindIndex(sources,s=>ReferenceEquals(s,c.Other.Source))};
    }
    public static void Validate(Relation root)
    {
        var f=RootFields(root);var initial=Read((Relation)f[1]);var final=Read((Relation)f[3]);
        var sources=initial.Edges.Select(e=>e.Source).ToHashSet<IElement>(ReferenceEqualityComparer.Instance);
        bool Supported(Edge edge)=>sources.Contains(edge.Source)||edge.Source is Relation closure&&NeighborhoodClosure.IsInferred(closure)&&NeighborhoodClosure.Read(closure) is not null;
        if(final.Edges.Any(e=>!Supported(e)))throw new InvalidDataException("Final graph introduced an unsupported source branch.");
        IElement previousRound=initial.Surface;
        foreach(var round in Rows(f[2]))
        {
            var r=RoundFields(round);var before=Read((Relation)r[0]);_=Read((Relation)r[2]);_=Contacts(r,4,before);
            if(!ReferenceEquals(previousRound,before.Surface))throw new InvalidDataException("A neighborhood pass is not based on the previous pass.");
            IElement roundState=before.Surface;
            foreach(var opinion in Rows(r[1]))
            {
                var o=Fields(opinion,5);
                if(!ReadWitness.TryRead((Relation)o[0],out var selected,out _,out var result,out _,out _)||!ReferenceEquals(selected,before.Surface))
                    throw new InvalidDataException("A candidate lost its selected source graph.");
                var after=Read((Relation)result);if(after.Edges.Any(e=>!Supported(e)))throw new InvalidDataException("A candidate invented a source branch.");
                foreach(var edge in after.Edges.Where(e=>!before.Edges.Any(p=>ReferenceEquals(p.Source,e.Source))))
                {
                    if(edge.Source is not Relation closure)throw new InvalidDataException("New participation has no closure witness.");
                    var p=NeighborhoodClosure.Read(closure);
                    if(!ReferenceEquals(p.Before.Surface,before.Surface)||!ReferenceEquals(edge.A,p.A)||!ReferenceEquals(edge.B,p.B)||edge.Handles.Length!=0)
                        throw new InvalidDataException("An inferred connector lost its actual opportunity or selected endpoint ports.");
                }
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
                    ReadWitness.TryRead((Relation)op[0],out _,out var instruction,out var proposed,out _,out _);
                    var proposedGraph=Read((Relation)proposed);var currentGraph=Read((Relation)current);var nextGraph=Read((Relation)next);
                    if(!Fresh((Relation)Rows(instruction)[0],before,currentGraph))throw new InvalidDataException("A stale incidence neighborhood was applied.");
                    var deltaRemoved=before.Edges.Where(e=>!proposedGraph.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness))).ToArray();
                    var deltaAdded=proposedGraph.Edges.Where(e=>!before.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness))).ToArray();
                    if(deltaRemoved.Any(e=>!currentGraph.Edges.Any(p=>ReferenceEquals(p.Witness,e.Witness))))throw new InvalidDataException("A stale local replacement was applied.");
                    var expected=currentGraph.Edges.Where(e=>!deltaRemoved.Any(p=>ReferenceEquals(p.Witness,e.Witness))).Concat(deltaAdded).Select(e=>e.Witness).ToHashSet(ReferenceEqualityComparer.Instance);
                    if(!expected.SetEquals(nextGraph.Edges.Select(e=>e.Witness)))throw new InvalidDataException("Application output is not its witnessed local replacement.");
                    roundState=next;
                }
            }
            if(!ReferenceEquals(roundState,r[2]))throw new InvalidDataException("A pass output is not its retained decision sequence.");
            previousRound=roundState;
        }
        if(!ReferenceEquals(previousRound,final.Surface))throw new InvalidDataException("Final graph is not the last neighborhood pass.");
        if(f.Length>=6)
        {
            var c=Fields(f[5],4);var last=RoundFields(Rows(f[2])[^1]);
            if(c[0] is not Relation budget||!budget.TryReadSegment(out var axis,out var zero,out var limit)||zero.Ticks!=0||limit.Ticks is <1 or >8||
                c[1] is not CarrierLocation count||!ReferenceEquals(count.Carrier,axis)||count.Ticks!=Rows(f[2]).Length||count.Ticks>limit.Ticks||
                c[2] is not Relation transition||!ReferenceEquals(transition.Left,last[0])||!ReferenceEquals(transition.Right,last[2])||
                count.Ticks<limit.Ticks&&!ReferenceEquals(transition.Left,transition.Right))throw new InvalidDataException("Iteration controller lost its budget, completed steps or quiet transition.");
            _=Contacts(c,3,final);
        }
        if(f.Length==7)
        {
            var contexts=Rows(f[6]);var states=new[]{initial}.Concat(Rows(f[2]).Select(r=>Read((Relation)RoundFields(r)[2]))).ToArray();
            if(contexts.Length!=states.Length)throw new InvalidDataException("Continuation history lost a pass.");
            for(var i=0;i<states.Length;i++)_=NeighborhoodContinuations.ReadAll((Relation)contexts[i],states[i]);
        }
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
