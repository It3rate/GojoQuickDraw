using System.Text.Json;
using Gojo.Core;
using Gojo.Experiments.QuickDraw;
using Gojo.Patterns;
using Gojo.Structures;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.PatchEvidence;

namespace GojoQuickDraw.Learning;

// A proposed missing passage closes an actual ordered route. It never claims
// acquired ink, fill, object membership, or a particular named shape.
public static class NeighborhoodClosure
{
    public static readonly string[] Names=["gap/terminal-reach","gap/path-reach","route-turn/full-turn","terminal-direction-change",
        "path-edge-count","branched-interior-sites","same-acquired-stroke","stroke-order-span/path-count","timestamps-available","endpoint-time-span/path-time-span"];
    public sealed record PathStep(Edge Edge,Relation From,Relation To);
    public sealed record Opportunity(Relation Witness,Graph Before,Relation A,Relation B,PathStep[] Path,double Gap,double Reach,double PathLength,Relation Order);
    public sealed record OrderRead(Relation Surface,int? Stroke,double? Time,Relation? StrokeSource=null,Carrier? StrokeAxis=null,Carrier? Clock=null);
    public sealed record Example(int[] Bands,int Label,string Group,string Basis,string Kind);

    private static bool Group(IElement value,int count,out IElement[] fields)
    {
        fields=[];return value is Relation r&&r.TryReadOrderedGroup(out fields)&&fields.Length==count;
    }
    public static bool IsInferred(IElement source)=>source is Relation w&&ReadWitness.TryRead(w,out var selected,out var basis,out var result,out _,out _)&&
        Group(selected,3,out var s)&&Group(basis,5,out _)&&s[0] is Relation a&&s[1] is Relation b&&
        result is Relation ports&&ReferenceEquals(ports.Left,a)&&ReferenceEquals(ports.Right,b);

    public static Opportunity? Propose(Graph graph,Relation a,Relation b)
    {
        if(ReferenceEquals(a,b)||Degree(graph,a)!=1||Degree(graph,b)!=1)return null;
        var queue=new Queue<(Relation Site,PathStep[] Path)>();queue.Enqueue((a,[]));
        var visited=new HashSet<Relation>(ReferenceEqualityComparer.Instance);PathStep[]? path=null;
        while(queue.TryDequeue(out var item))
        {
            if(!visited.Add(item.Site))continue;
            if(ReferenceEquals(item.Site,b)){path=item.Path;break;}
            if(item.Path.Length>=24)continue;
            foreach(var edge in graph.Edges.Where(e=>!IsInferred(e.Source)))
            {
                if(ReferenceEquals(edge.A,item.Site))queue.Enqueue((edge.B,[..item.Path,new(edge,item.Site,edge.B)]));
                else if(ReferenceEquals(edge.B,item.Site))queue.Enqueue((edge.A,[..item.Path,new(edge,item.Site,edge.A)]));
            }
        }
        if(path is null||path.Length==0)return null;
        var gap=PatchGeometry.Distance(XY(a),XY(b));var reach=Math.Max(.001,Math.Min(Length(path[0].Edge.Points),Length(path[^1].Edge.Points)));
        var total=path.Sum(p=>Length(p.Edge.Points));if(gap<1e-6||gap>.35*reach||gap>.12*total)return null;
        var axis=new Carrier();var length=Relation.Segment(axis,0,checked((long)Math.Round(gap*Q)));
        var route=G(path.Select(p=>(IElement)new Relation(p.From,p.Edge.Witness)).ToArray());
        var order=G(path.SelectMany(p=>new IElement[]{Order(p.Edge,p.From).Surface,Order(p.Edge,p.To).Surface}).ToArray());
        var witness=ReadWitness.Create(new Carrier(),G(a,b,route),G(graph.Frame,length,
            Relation.Segment(axis,0,checked((long)Math.Round(reach*Q))),Relation.Segment(axis,0,checked((long)Math.Round(total*Q))),order),
            new Relation(a,b),new Placeholder(),graph.Surface);
        return Read(witness);
    }
    public static Opportunity Read(Relation witness)
    {
        if(!IsInferred(witness)||!ReadWitness.TryRead(witness,out var selected,out var basis,out var result,out var unobserved,out var source)||
            source is not Relation graphSurface||unobserved is not Placeholder)throw new InvalidDataException("Closure has no explicit unobserved passage or source graph.");
        var graph=NeighborhoodGraph.Read(graphSurface);var s=Fields(selected,3);var b=Fields(basis,5);
        var a=(Relation)s[0];var end=(Relation)s[1];
        if(!ReferenceEquals(b[0],graph.Frame)||!graph.Sites.Any(p=>ReferenceEquals(p,a))||!graph.Sites.Any(p=>ReferenceEquals(p,end))||Degree(graph,a)!=1||Degree(graph,end)!=1)
            throw new InvalidDataException("Closure lost its actual terminal sites or frame.");
        var path=new List<PathStep>();var at=a;
        foreach(var row in Rows(s[2]))
        {
            if(row is not Relation{Left:Relation from,Right:Relation edgeRow}||!ReferenceEquals(from,at))throw new InvalidDataException("Closure route is not ordered by actual shared sites.");
            var edge=graph.Edges.SingleOrDefault(e=>ReferenceEquals(e.Witness,edgeRow))??throw new InvalidDataException("Closure route lost an actual interval.");
            if(IsInferred(edge.Source)||!ReferenceEquals(edge.A,at)&&!ReferenceEquals(edge.B,at))throw new InvalidDataException("Closure route is unsupported or already inferred.");
            var next=ReferenceEquals(edge.A,at)?edge.B:edge.A;path.Add(new(edge,at,next));at=next;
        }
        if(!ReferenceEquals(at,end)||path.Count is <1 or >24||path.Select(p=>p.Edge.Witness).Distinct(ReferenceEqualityComparer.Instance).Count()!=path.Count)
            throw new InvalidDataException("Closure does not connect the selected distinct terminals by a finite source route.");
        if(b[1] is not Relation gapSpan||!gapSpan.TryReadSegment(out var axis,out var zero,out var gap)||zero.Ticks!=0||gap.Ticks<=0||
            b[2] is not Relation reachSpan||!reachSpan.TryReadSegment(out var reachAxis,out var r0,out var reach)||!ReferenceEquals(axis,reachAxis)||r0.Ticks!=0||reach.Ticks<=0||
            b[3] is not Relation totalSpan||!totalSpan.TryReadSegment(out var totalAxis,out var t0,out var total)||!ReferenceEquals(axis,totalAxis)||t0.Ticks!=0||
            Math.Abs(gap.Ticks/(double)Q-PatchGeometry.Distance(XY(a),XY(end)))>2e-6||
            Math.Abs(reach.Ticks/(double)Q-Math.Max(.001,Math.Min(Length(path[0].Edge.Points),Length(path[^1].Edge.Points))))>2e-6||
            Math.Abs(total.Ticks/(double)Q-path.Sum(p=>Length(p.Edge.Points)))>2e-6||gap.Ticks>.350001*reach.Ticks||gap.Ticks>.120001*total.Ticks)
            throw new InvalidDataException("Closure lost its calibrated gap, local reach or route length.");
        var expected=path.SelectMany(p=>new[]{Order(p.Edge,p.From),Order(p.Edge,p.To)}).ToArray();var orders=Rows(b[4]);
        if(orders.Length!=expected.Length||orders.Where((o,i)=>!SameOrder(o,expected[i].Surface)).Any())throw new InvalidDataException("Closure chronology is not grounded in its source route.");
        return new(witness,graph,a,end,path.ToArray(),gap.Ticks/(double)Q,reach.Ticks/(double)Q,total.Ticks/(double)Q,(Relation)b[4]);
    }
    private static bool SameOrder(IElement actual,Relation expected)
    {
        var a=Rows(actual);var b=Rows(expected);return a.Length==b.Length&&a.Where((v,i)=>!ReferenceEquals(v,b[i])).Any()==false;
    }
    public static OrderRead Order(Edge edge,Relation at)
    {
        if(!ReferenceEquals(at,edge.A)&&!ReferenceEquals(at,edge.B))throw new InvalidDataException("Chronology requires an actual edge endpoint.");
        // Only exact acquired endpoint rows supply time. An interior fitted
        // parameter is not assigned an invented acquisition timestamp.
        if(edge.Source is not Relation source||!ReadWitness.TryRead(source,out var selected,out _,out _,out _,out _)||
            selected is not Relation{Left:Relation stroke,Right:Relation samples}||
            stroke.Right is not Relation{Left:CarrierLocation ordinal}||!samples.TryReadOrderedGroup(out var rows)||rows.Length==0)
            return new(G(),null,null);
        edge.Span.TryReadSegment(out _,out var lo,out var hi);var tick=ReferenceEquals(at,edge.A)?lo.Ticks:hi.Ticks;
        if(tick!=0&&tick!=Q)return new(G(stroke),checked((int)ordinal.Ticks),null,stroke,ordinal.Carrier);
        var sample=tick==0?rows[0]:rows[^1];
        if(sample is not Relation{Right:Relation{Right:Relation{Right:Relation times}}})return new(G(stroke),checked((int)ordinal.Ticks),null,stroke,ordinal.Carrier);
        double? time=null;Carrier? clock=null;
        if(StructuralConstraint.TryReadScope(times,out var entries,out var seam,out var tail)&&seam.Equals(tail)&&entries.Length==1&&
            entries[0] is Relation{Left:CarrierLocation value}&&NativeAcquisition.TryReadScalar(entries[0],value.Carrier,out var n,out var d))
        {time=(double)n/(double)d;clock=value.Carrier;}
        return new(G(stroke,sample,times),checked((int)ordinal.Ticks),time,stroke,ordinal.Carrier,clock);
    }
    public static Opportunity[] Discover(Graph graph,int maximum=48)
    {
        var endpoints=graph.Sites.Where(s=>Degree(graph,s)==1).ToArray();var result=new List<Opportunity>();
        for(var i=0;i<endpoints.Length;i++)for(var j=i+1;j<endpoints.Length;j++)
            if(Propose(graph,endpoints[i],endpoints[j]) is { } proposal)result.Add(proposal);
        return result.OrderBy(p=>p.Gap/p.Reach).Take(maximum).ToArray();
    }
    public static NeighborhoodFeatures.Cells Features(Opportunity opportunity)
    {
        var p=opportunity;var points=p.Path.SelectMany((v,i)=>
            (ReferenceEquals(v.Edge.A,v.From)?Sample(v.Edge.Points,20):Sample(v.Edge.Points,20).Reverse()).Skip(i==0?0:1)).ToArray();
        var turn=PatchGeometry.Measure(points,true).Turn/360;
        var exit=Angle(points[^2],points[^1]);var entry=Angle(points[0],points[1]);
        var change=Math.Abs((entry-exit+540)%360-180)/180;
        var orders=p.Path.SelectMany(v=>new[]{Order(v.Edge,v.From),Order(v.Edge,v.To)}).ToArray();
        var first=orders[0];var last=orders[^1];var strokes=orders.Where(v=>v.Stroke is not null&&ReferenceEquals(v.StrokeAxis,first.StrokeAxis)).Select(v=>v.Stroke!.Value).ToArray();
        var times=orders.Where(v=>v.Time is not null&&ReferenceEquals(v.Clock,first.Clock)).Select(v=>v.Time!.Value).ToArray();
        var hasTime=first.Time is not null&&last.Time is not null&&ReferenceEquals(first.Clock,last.Clock)&&times.Length>1&&times.Max()>times.Min();
        double[] values=[p.Gap/p.Reach,p.Gap/p.PathLength,turn,change,p.Path.Length,
            p.Path.SkipLast(1).Count(v=>Degree(p.Before,v.To)>2),first.StrokeSource is not null&&ReferenceEquals(first.StrokeSource,last.StrokeSource)?1:0,
            strokes.Length>0?(strokes.Max()-strokes.Min())/(double)p.Path.Length:0,hasTime?1:0,
            hasTime?Math.Abs(last.Time!.Value-first.Time!.Value)/(times.Max()-times.Min()):0];
        double[] ratios=[.004,.02,.06,.15,.35];double[] counts=[.5,1.5,2.5,3.5,4.5];
        double[][] cuts=[ratios,ratios,[.1,.3,.5,.75,.95],ratios,counts,counts,counts,ratios,counts,[.05,.25,.5,.75,.95]];
        return NeighborhoodFeatures.Publish(p.Witness,G(p.Before.Surface,p.Witness,p.Order),values,cuts);
    }
    public static NeighborhoodProposals.Candidate Candidate(Opportunity p)
    {
        var graph=p.Before;var edge=Curve(graph.Frame,p.Witness,Relation.Segment(new Carrier(),0,Q),p.A,p.B,[],p.Witness);
        var focus=NeighborhoodProposals.Neighborhood(graph,[(XY(p.A)[0]+XY(p.B)[0])/2,(XY(p.A)[1]+XY(p.B)[1])/2],p.Path.Select(v=>v.Edge));
        return NeighborhoodProposals.Make(graph,graph.Edges.Append(edge),focus,[],[],[],0,false,
            "add an inferred closure passage",completion:p.Witness);
    }
    public static (Relation Surface,Example[] Examples) Training(JsonElement cases)
    {
        var rows=new List<IElement>();var examples=new List<Example>();
        foreach(var c in cases.EnumerateArray())
        {
            var graph=FromJson(c.GetProperty("source"));var ends=c.GetProperty("ends").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            var p=Propose(graph,graph.Sites[ends[0]],graph.Sites[ends[1]]);if(p is null)continue;
            var cells=Features(p);var axis=new Carrier();var label=c.GetProperty("label").GetInt32();
            if(label is <0 or >1)throw new InvalidDataException("Closure controls need an explicit binary construction target.");
            rows.Add(ReadWitness.Create(new Carrier(),new Relation(p.Witness,cells.Surface),Relation.Segment(axis,0,1),new CarrierLocation(axis,label),G(),graph.Surface));
            examples.Add(new(cells.Bands,label,c.GetProperty("group").GetString()!,"controlled-closure",c.GetProperty("kind").GetString()!));
        }
        var surface=G(rows.ToArray());ValidateTraining(surface);return (surface,examples.ToArray());
    }
    public static void ValidateTraining(Relation surface)
    {
        foreach(var row in Rows(surface))
        {
            if(row is not Relation w||!ReadWitness.TryRead(w,out var selected,out var basis,out var result,out _,out var before)||
                selected is not Relation{Left:Relation proposal,Right:Relation cells}||basis is not Relation span||!span.TryReadSegment(out var axis,out var lo,out var hi)||lo.Ticks!=0||hi.Ticks!=1||
                result is not CarrierLocation label||!ReferenceEquals(label.Carrier,axis)||label.Ticks is <0 or >1||!ReferenceEquals(Read(proposal).Before.Surface,before)||
                NeighborhoodFeatures.ReadCells(cells).Length!=Names.Length)throw new InvalidDataException("Closure control lost its actual proposal, coarse evidence or target basis.");
        }
    }
}
