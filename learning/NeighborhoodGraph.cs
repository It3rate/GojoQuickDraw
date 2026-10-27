using System.Text.Json;
using Gojo.Core;
using Gojo.Experiments.QuickDraw;
using Gojo.Structures;
using GojoQuickDraw.Reads;
using static GojoQuickDraw.Learning.PatchEvidence;

namespace GojoQuickDraw.Learning;

// A local reader/builder over ordinary Gojo sites, ordered edges, parameter
// spans and curve witnesses. These records are projections, not a second graph.
public static class NeighborhoodGraph
{
    public sealed record Edge(Relation Witness,IElement Source,Relation Span,Relation A,Relation B,Relation[] Handles,double[][] Points);
    public sealed record Graph(Relation Surface,Relation Frame,Relation[] Sites,Edge[] Edges);
    public sealed record SiteView(string Id,double[] Point,int Degree,string Kind,string[] Edges,string[] FarEndpoints);
    public sealed record EdgeView(string Id,string Start,string End,double[][] Controls,string Source,double[] Interval);
    public sealed record View(double[][][] Curves,JointNode[] Joints,SiteView[] Sites,EdgeView[] Edges,NeighborhoodContacts.View[] Projections);

    public static IElement[] Rows(IElement value)
    {
        if(value is not Relation r||!r.TryReadOrderedGroup(out var rows))throw new InvalidDataException("Expected an ordered native surface.");
        return rows;
    }
    public static IElement[] Fields(IElement value,int count)
    {
        var fields=Rows(value);if(fields.Length!=count)throw new InvalidDataException("Incomplete local graph surface.");return fields;
    }
    public static bool TryReadSegment(this Relation span,out Carrier axis,out CarrierLocation start,out CarrierLocation end)
    {
        axis=Carrier.Unknown;start=end=default;
        if(span is not {Left:Relation{Left:CarrierLocation a,Right:Carrier ca},Right:Relation{Left:CarrierLocation b,Right:Carrier cb}} ||
            !ReferenceEquals(ca,cb)||!ReferenceEquals(a.Carrier,ca)||!ReferenceEquals(b.Carrier,ca))return false;
        axis=ca;start=a;end=b;return true;
    }
    public static double[] XY(IElement site)
    {
        if(site is not Relation {Right:Relation{Left:CarrierLocation x,Right:CarrierLocation y}})throw new InvalidDataException("Site has no calibrated two-axis read.");
        return [x.Ticks/(double)Q,y.Ticks/(double)Q];
    }
    public static Relation Frame()
    {
        var x=new Carrier();var y=new Carrier(x.UnitShape);return G(Relation.Segment(x,0,Q),Relation.Segment(y,0,Q));
    }
    public static Relation Site(Relation frame,double[] xy,IElement origin)
    {
        var f=Fields(frame,2);var x=(Carrier)((Relation)((Relation)f[0]).Left).Right;var y=(Carrier)((Relation)((Relation)f[1]).Left).Right;
        return new Relation(origin,new Relation(new CarrierLocation(x,checked((long)Math.Round(xy[0]*Q))),new CarrierLocation(y,checked((long)Math.Round(xy[1]*Q)))));
    }
    public static Edge Curve(Relation frame,IElement source,Relation span,Relation a,Relation b,double[][] handles,IElement history)
    {
        var controls=handles.Select(p=>Site(frame,p,source)).ToArray();
        var witness=ReadWitness.Create(new Carrier(),new Relation(source,span),frame,G(new Relation(a,b),G(controls)),G(),history);
        return new(witness,source,span,a,b,controls,[XY(a),..controls.Select(XY),XY(b)]);
    }
    public static Graph Create(Relation frame,IEnumerable<Edge> input)
    {
        var edges=input.ToArray();var sites=edges.SelectMany(e=>new[]{e.A,e.B}).Distinct<Relation>(ReferenceEqualityComparer.Instance).ToArray();
        return Read(G(frame,G(sites),G(edges.Select(e=>(IElement)e.Witness).ToArray())));
    }
    public static Graph Read(Relation root)
    {
        var f=Fields(root,3);var frame=(Relation)f[0];var sites=Rows(f[1]).Cast<Relation>().ToArray();
        var axes=Fields(frame,2).Cast<Relation>().Select(s=>s.TryReadSegment(out var c,out _,out _)?c:throw new InvalidDataException("Graph has no calibrated axes.")).ToArray();
        foreach(var site in sites)
            if(site.Right is not Relation {Left:CarrierLocation x,Right:CarrierLocation y}||!ReferenceEquals(x.Carrier,axes[0])||!ReferenceEquals(y.Carrier,axes[1]))
                throw new InvalidDataException("Graph site belongs to a different basis.");
        var edges=Rows(f[2]).Select(element=>
        {
            if(element is not Relation w||!ReadWitness.TryRead(w,out var selected,out var basis,out var result,out _,out _)||
                selected is not Relation {Right:Relation span} input||!ReferenceEquals(basis,frame)||!span.TryReadSegment(out _,out var lo,out var hi)||lo.Ticks>hi.Ticks)
                throw new InvalidDataException("Curve lost its source, ordered interval or basis.");
            var r=Fields(result,2);
            if(r[0] is not Relation {Left:Relation a,Right:Relation b}||!sites.Any(s=>ReferenceEquals(s,a))||!sites.Any(s=>ReferenceEquals(s,b)))
                throw new InvalidDataException("An edge endpoint is not one of its graph's actual sites.");
            var handles=Rows(r[1]).Cast<Relation>().ToArray();
            if(handles.Any(h=>h.Right is not Relation {Left:CarrierLocation x,Right:CarrierLocation y}||!ReferenceEquals(x.Carrier,axes[0])||!ReferenceEquals(y.Carrier,axes[1])))
                throw new InvalidDataException("Curve handle belongs to a different basis.");
            return new Edge(w,input.Left,span,a,b,handles,[XY(a),..handles.Select(XY),XY(b)]);
        }).ToArray();
        foreach(var group in edges.GroupBy(e=>e.Source,ReferenceEqualityComparer.Instance))
        {
            Carrier? axis=null;long last=long.MinValue;
            foreach(var edge in group.OrderBy(e=>{e.Span.TryReadSegment(out _,out var start,out _);return start.Ticks;}))
            {
                edge.Span.TryReadSegment(out var carrier,out var start,out var end);
                if(axis is not null&&(!ReferenceEquals(axis,carrier)||start.Ticks<last))throw new InvalidDataException("Active source intervals overlap or have incompatible parameter bases.");
                axis=carrier;last=end.Ticks;
            }
        }
        return new(root,frame,sites,edges);
    }
    public static Graph FromAnnotation(QuickDrawAnnotationEvidence.Read annotation)
    {
        var frame=Frame();var bySample=new Dictionary<IElement,Relation>(ReferenceEqualityComparer.Instance);var edges=new List<Edge>();
        foreach(var profile in annotation.Profiles)
        {
            var controls=profile.Curve.Controls.Select(p=>new[]{p.X/(double)QuickDrawAnnotationEvidence.CoordinateQuantum,p.Y/(double)QuickDrawAnnotationEvidence.CoordinateQuantum}).ToArray();
            if(controls.Length<2||Length(controls)<1e-6)continue;
            var samples=Rows(profile.Source.Selection.Right);
            Relation End(IElement sample,double[] xy)
            {
                if(!bySample.TryGetValue(sample,out var site))bySample[sample]=site=Site(frame,xy,sample);
                return site;
            }
            edges.Add(Curve(frame,profile.Witness,Relation.Segment(new Carrier(),0,Q),End(samples[0],controls[0]),End(samples[^1],controls[^1]),controls[1..^1],profile.Witness));
        }
        return Create(frame,edges);
    }
    public static Graph FromJson(JsonElement data)
    {
        var frame=Frame();var sites=data.GetProperty("sites").EnumerateArray().Select(p=>Site(frame,p.EnumerateArray().Select(v=>v.GetDouble()).ToArray(),new Placeholder())).ToArray();
        var edges=new List<Edge>();
        foreach(var entry in data.GetProperty("edges").EnumerateArray())
        {
            var a=sites[entry.GetProperty("a").GetInt32()];var b=sites[entry.GetProperty("b").GetInt32()];
            var handles=entry.TryGetProperty("handles",out var h)?h.EnumerateArray().Select(p=>p.EnumerateArray().Select(v=>v.GetDouble()).ToArray()).ToArray():[];
            var source=new Relation(new Relation(a,b),G(handles.Select(p=>(IElement)Site(frame,p,new Placeholder())).ToArray()));
            edges.Add(Curve(frame,source,Relation.Segment(new Carrier(),0,Q),a,b,handles,source));
        }
        return Create(frame,edges);
    }
    public static double[][] Sample(double[][] controls,int steps=12)=>controls.Length==2?controls:Enumerable.Range(0,steps+1).Select(i=>PatchGeometry.Bezier(controls,i/(double)steps)).ToArray();
    public static double Length(double[][] controls){var p=Sample(controls);return p.Zip(p.Skip(1),PatchGeometry.Distance).Sum();}
    public static double[][][] Split(double[][] controls,double t)
    {
        var current=controls.Select(p=>p.ToArray()).ToArray();var left=new List<double[]>{current[0]};var right=new List<double[]>{current[^1]};
        while(current.Length>1){current=current.Zip(current.Skip(1),(a,b)=>new[]{a[0]*(1-t)+b[0]*t,a[1]*(1-t)+b[1]*t}).ToArray();left.Add(current[0]);right.Add(current[^1]);}
        right.Reverse();return [left.ToArray(),right.ToArray()];
    }
    public static (Edge Left,Edge Right) Cut(Graph graph,Edge edge,double t,Relation site,Relation? parameter=null)
    {
        var split=Split(edge.Points,t);edge.Span.TryReadSegment(out var axis,out var start,out var end);
        var cut=parameter??Relation.Point(axis,checked((long)Math.Round(start.Ticks+t*(end.Ticks-start.Ticks))));
        if(cut.Left is not CarrierLocation location||!ReferenceEquals(location.Carrier,axis)||location.Ticks<start.Ticks||location.Ticks>end.Ticks)
            throw new InvalidDataException("A supplied cut is outside its source parameter interval.");
        var left=new Relation(edge.Span.Left,cut);var right=new Relation(cut,edge.Span.Right);
        return (Curve(graph.Frame,edge.Source,left,edge.A,site,split[0][1..^1],edge.Witness),
            Curve(graph.Frame,edge.Source,right,site,edge.B,split[1][1..^1],edge.Witness));
    }
    public static Edge Move(Graph graph,Edge edge,Relation a,Relation b)
    {
        var pa=XY(a);var pb=XY(b);var oldA=XY(edge.A);var oldB=XY(edge.B);
        var handles=edge.Handles.Select((s,i)=>{var p=XY(s);var t=(i+1.0)/(edge.Handles.Length+1);return new[]{p[0]+(pa[0]-oldA[0])*(1-t)+(pb[0]-oldB[0])*t,p[1]+(pa[1]-oldA[1])*(1-t)+(pb[1]-oldB[1])*t};}).ToArray();
        return Curve(graph.Frame,edge.Source,edge.Span,a,b,handles,edge.Witness);
    }
    public static int Degree(Graph graph,Relation site)=>graph.Edges.Sum(e=>(ReferenceEquals(e.A,site)?1:0)+(ReferenceEquals(e.B,site)?1:0));
    public static double[] Directions(Graph graph,Relation site)
    {
        var result=new List<double>();
        foreach(var e in graph.Edges)
        {
            if(ReferenceEquals(e.A,site)){var p=e.Points.FirstOrDefault(p=>PatchGeometry.Distance(p,e.Points[0])>1e-7);if(p is not null)result.Add(Angle(e.Points[0],p));}
            if(ReferenceEquals(e.B,site)){var p=e.Points.Reverse().FirstOrDefault(p=>PatchGeometry.Distance(p,e.Points[^1])>1e-7);if(p is not null)result.Add(Angle(e.Points[^1],p));}
        }
        return result.ToArray();
    }
    public static double Angle(double[] a,double[] b)=>(Math.Atan2(b[1]-a[1],b[0]-a[0])*180/Math.PI+360)%360;
    public static string Kind(Graph graph,Relation site)
    {
        if(graph.Edges.Any(e=>ReferenceEquals(e.A,site)&&ReferenceEquals(e.B,site)))return "O";
        var rays=Directions(graph,site);
        for(var i=0;i<rays.Length;i++)for(var j=i+1;j<rays.Length;j++)
            if(Math.Abs((rays[i]-rays[j]+540)%360-180)<10)return "coincident rays";
        return JointGraphRead.Classify(rays);
    }
    public static int Components(Graph graph)
    {
        var unseen=graph.Sites.ToHashSet<Relation>(ReferenceEqualityComparer.Instance);var count=0;
        while(unseen.Count>0){count++;var stack=new Stack<Relation>();stack.Push(unseen.First());while(stack.TryPop(out var s)){if(!unseen.Remove(s))continue;foreach(var e in graph.Edges){if(ReferenceEquals(e.A,s))stack.Push(e.B);if(ReferenceEquals(e.B,s))stack.Push(e.A);}}}
        return count;
    }
    public static int Cycles(Graph graph)=>graph.Edges.Length-graph.Sites.Length+Components(graph);
    public static View Project(Graph graph,IElement[] sources,Dictionary<IElement,string>? identities=null,IEnumerable<NeighborhoodContacts.Contact>? contacts=null)
    {
        identities??=new(ReferenceEqualityComparer.Instance);
        string Id(IElement element,string prefix){if(!identities.TryGetValue(element,out var id))identities[element]=id=prefix+identities.Count;return id;}
        string SiteId(Relation s)=>Id(s,"v");
        string SourceId(IElement source)=>"p"+Array.FindIndex(sources,p=>ReferenceEquals(p,source));
        var sites=graph.Sites.Select(s=>
        {
            var incident=graph.Edges.Select((e,i)=>(e,i)).Where(p=>ReferenceEquals(p.e.A,s)||ReferenceEquals(p.e.B,s)).ToArray();
            return new SiteView(SiteId(s),XY(s),Degree(graph,s),Kind(graph,s),incident.Select(p=>Id(p.e.Witness,"e")).ToArray(),
                incident.Select(p=>SiteId(ReferenceEquals(p.e.A,s)?p.e.B:p.e.A)).ToArray());
        }).ToArray();
        var edges=graph.Edges.Select((e,i)=>{e.Span.TryReadSegment(out _,out var a,out var b);return new EdgeView(Id(e.Witness,"e"),SiteId(e.A),SiteId(e.B),e.Points,SourceId(e.Source),[a.Ticks/(double)Q,b.Ticks/(double)Q]);}).ToArray();
        var joints=sites.Select((s,i)=>new JointNode(s.Id,s.Kind,s.Point,Directions(graph,graph.Sites[i]),
            edges.Where(e=>e.Start==s.Id||e.End==s.Id).Select(e=>new JointAt(e.Id,e.Start==s.Id?0:1)).ToArray(),
            edges.Where(e=>e.Start==s.Id||e.End==s.Id).Select(e=>e.Source).Distinct().ToArray(),s.Degree>1,0,
            $"Native site {s.Id}; degree {s.Degree}. Edges: {string.Join(", ",s.Edges)}. Far endpoints: {string.Join(", ",s.FarEndpoints)}.")).ToArray();
        var projections=(contacts??[]).Select(c=>new NeighborhoodContacts.View("near-"+SiteId(c.Endpoint)+"-"+Id(c.Other.Witness,"e"),SiteId(c.Endpoint),Id(c.Other.Witness,"e"),XY(c.Endpoint),XY(c.Point),c.T,c.Gap/c.Reach,c.Gap<=c.Limit,
            "Finite nearest-point observation; separate sites until an explicit application binds them. Source parameter and interval are retained.")).ToArray();
        return new(graph.Edges.Select(e=>e.Points).ToArray(),joints,sites,edges,projections);
    }
}
