using Gojo.Core;
using Gojo.Structures;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.PatchEvidence;

namespace GojoQuickDraw.Learning;

// A nearest-point observation is not contact. Its site, selected other
// interval, parameter, separate projected site and finite gap stay connected.
public static class NeighborhoodContacts
{
    public sealed record Contact(Relation Witness,Relation Endpoint,Edge Other,Relation Parameter,Relation Point,double T,double Gap,double Reach,double Limit);
    public sealed record View(string Id,string Endpoint,string Edge,double[] From,double[] To,double Parameter,double GapRatio,bool WithinReach,string Basis);

    public static (double T,double[] Point) Nearest(double[] point,Edge edge)
    {
        double Distance(double t)=>PatchGeometry.Distance(point,PatchGeometry.Bezier(edge.Points,t));
        if(edge.Points.Length==2)
        {
            var a=edge.Points[0];var b=edge.Points[1];var dx=b[0]-a[0];var dy=b[1]-a[1];
            var t=Math.Clamp(((point[0]-a[0])*dx+(point[1]-a[1])*dy)/Math.Max(1e-20,dx*dx+dy*dy),0,1);
            return (t,PatchGeometry.Bezier(edge.Points,t));
        }
        // Bounded sampling plus bracket refinement; no claim of an exact
        // polynomial/global minimum for arbitrary self-intersecting curves.
        const int samples=32;var best=Enumerable.Range(0,samples+1).MinBy(i=>Distance(i/(double)samples));
        var lo=Math.Max(0,(best-1.0)/samples);var hi=Math.Min(1,(best+1.0)/samples);
        for(var i=0;i<24;i++)
        {
            var a=lo+(hi-lo)/3;var b=hi-(hi-lo)/3;
            if(Distance(a)<=Distance(b))hi=b;else lo=a;
        }
        var t0=new[]{0.0,1.0,best/(double)samples,(lo+hi)/2}.MinBy(Distance);
        return (t0,PatchGeometry.Bezier(edge.Points,t0));
    }
    public static Contact[] Discover(Graph graph,int neighbors=2)
    {
        var result=new List<Contact>();
        foreach(var endpoint in graph.Sites.Where(s=>Degree(graph,s)>0))
        {
            var incident=graph.Edges.Where(e=>ReferenceEquals(e.A,endpoint)||ReferenceEquals(e.B,endpoint)).ToArray();
            var reach=incident.Select(e=>Length(e.Points)).Max();
            var other=graph.Edges.Where(e=>!ReferenceEquals(e.A,endpoint)&&!ReferenceEquals(e.B,endpoint))
                .Select(e=>{var n=Nearest(XY(endpoint),e);return (Edge:e,Near:n,Gap:PatchGeometry.Distance(XY(endpoint),n.Point));})
                .OrderBy(v=>v.Gap).Take(neighbors);
            foreach(var row in other)
            {
                row.Edge.Span.TryReadSegment(out var parameterAxis,out var first,out var last);
                if(last.Ticks==first.Ticks)continue;
                var tick=checked((long)Math.Round(first.Ticks+row.Near.T*(last.Ticks-first.Ticks)));
                var parameter=Relation.Point(parameterAxis,tick);var t=(tick-first.Ticks)/(double)(last.Ticks-first.Ticks);
                var xy=PatchGeometry.Bezier(row.Edge.Points,t);var selected=new Relation(endpoint,row.Edge.Witness);
                var point=Site(graph.Frame,xy,new Relation(row.Edge.Witness,parameter));var connector=new Relation(endpoint,point);
                var gap=PatchGeometry.Distance(XY(endpoint),XY(point));var local=Math.Max(.001,Math.Min(reach,Length(row.Edge.Points)));var axis=new Carrier();
                var whole=Relation.Segment(axis,0,checked((long)Math.Round(local*Q)));var limit=Relation.Segment(axis,0,checked((long)Math.Round(.1*local*Q)));
                var gapRead=ReadWitness.Create(new Carrier(),connector,whole,new CarrierLocation(axis,checked((long)Math.Round(gap*Q))),G(),graph.Frame);
                var witness=ReadWitness.Create(new Carrier(),selected,G(graph.Frame,row.Edge.Span,whole,limit),G(parameter,point,connector,gapRead),G(),graph.Surface);
                result.Add(Read(witness,graph));
            }
        }
        return result.ToArray();
    }
    public static Contact Read(Relation witness,Graph graph)
    {
        if(!ReadWitness.TryRead(witness,out var selected,out var basis,out var result,out _,out var source)||!ReferenceEquals(source,graph.Surface)||
            selected is not Relation{Left:Relation endpoint,Right:Relation target}||!graph.Sites.Any(s=>ReferenceEquals(s,endpoint))||Degree(graph,endpoint)<1)
            throw new InvalidDataException("Nearest-point read lost its actual site/source graph.");
        var edge=graph.Edges.SingleOrDefault(e=>ReferenceEquals(e.Witness,target))??throw new InvalidDataException("Nearest-point read lost its other segment.");
        if(ReferenceEquals(edge.A,endpoint)||ReferenceEquals(edge.B,endpoint))throw new InvalidDataException("An endpoint cannot use its incident segment as other support.");
        var b=Fields(basis,4);var r=Fields(result,4);
        if(!ReferenceEquals(b[0],graph.Frame)||!ReferenceEquals(b[1],edge.Span)||r[0] is not Relation parameter||parameter.Left is not CarrierLocation at||
            parameter.Right is not Carrier parameterAxis||!ReferenceEquals(at.Carrier,parameterAxis)||!edge.Span.TryReadSegment(out var axis,out var first,out var last)||
            !ReferenceEquals(axis,parameterAxis)||at.Ticks<first.Ticks||at.Ticks>last.Ticks||last.Ticks==first.Ticks||r[1] is not Relation point||
            r[2] is not Relation connector||!ReferenceEquals(connector.Left,endpoint)||!ReferenceEquals(connector.Right,point)||
            b[2] is not Relation whole||!whole.TryReadSegment(out var scale,out var zero,out var end)||zero.Ticks!=0||end.Ticks<=0||
            b[3] is not Relation limit||!limit.TryReadSegment(out var limitAxis,out var lo,out var hi)||!ReferenceEquals(scale,limitAxis)||lo.Ticks!=0||
            !ReadWitness.TryRead((Relation)r[3],out var gapSource,out var gapBasis,out var value,out _,out _)||!ReferenceEquals(gapSource,connector)||!ReferenceEquals(gapBasis,whole)||
            value is not CarrierLocation gap||!ReferenceEquals(gap.Carrier,scale))throw new InvalidDataException("Nearest-point parameter, gap or selected basis is inconsistent.");
        var t=(at.Ticks-first.Ticks)/(double)(last.Ticks-first.Ticks);
        var frameAxes=Fields(graph.Frame,2).Cast<Relation>().Select(s=>{s.TryReadSegment(out var carrier,out _,out _);return carrier;}).ToArray();
        if(point.Right is not Relation{Left:CarrierLocation x,Right:CarrierLocation y}||!ReferenceEquals(x.Carrier,frameAxes[0])||!ReferenceEquals(y.Carrier,frameAxes[1]))
            throw new InvalidDataException("Projected point belongs to another frame.");
        if(PatchGeometry.Distance(XY(point),PatchGeometry.Bezier(edge.Points,t))>2e-6||Math.Abs(gap.Ticks/(double)Q-PatchGeometry.Distance(XY(endpoint),XY(point)))>2e-6)
            throw new InvalidDataException("Nearest-point result does not lie on its selected source interval.");
        var actualReach=Math.Max(.001,Math.Min(graph.Edges.Where(e=>ReferenceEquals(e.A,endpoint)||ReferenceEquals(e.B,endpoint)).Max(e=>Length(e.Points)),Length(edge.Points)));
        if(Math.Abs(end.Ticks/(double)Q-actualReach)>2e-6||Math.Abs(hi.Ticks/(double)Q-.1*actualReach)>2e-6)
            throw new InvalidDataException("Nearest-point reach is not calibrated to its actual incident intervals.");
        return new(witness,endpoint,edge,parameter,point,t,gap.Ticks/(double)Q,end.Ticks/(double)Q,hi.Ticks/(double)Q);
    }
}
