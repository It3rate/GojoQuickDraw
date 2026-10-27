using Gojo.Core;
using Gojo.Structures;
using static GojoQuickDraw.Learning.PatchEvidence;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.Learning;

public static class NeighborhoodProposals
{
    public sealed record Focus(Relation Surface,double[] Center,double Reach,double Radius,Relation[] Sites,Edge[] Edges);
    public sealed record Candidate(Relation Surface,Graph Before,Graph After,Focus Focus,Relation[] Bindings,Edge[] Removed,Edge[] Changed,
        double Movement,bool Straightening,string Description,bool Admissible,string[] Issues,Relation? LineContext=null,Relation? Observation=null);
    public sealed record Encounter(double A,double B,double[] P,double[] Q,double Gap);

    public static Encounter Closest(Edge a,Edge b)
    {
        var pa=Sample(a.Points,12);var pb=Sample(b.Points,12);var best=new Encounter(0,0,pa[0],pb[0],double.PositiveInfinity);
        for(var i=1;i<pa.Length;i++)for(var j=1;j<pb.Length;j++)
        {
            var p=pa[i-1];var q=pb[j-1];double[] u=[pa[i][0]-p[0],pa[i][1]-p[1]],v=[pb[j][0]-q[0],pb[j][1]-q[1]];
            var cross=u[0]*v[1]-u[1]*v[0];var options=new List<(double A,double B)>();
            if(Math.Abs(cross)>1e-12)
            {
                var x=q[0]-p[0];var y=q[1]-p[1];var s=(x*v[1]-y*v[0])/cross;var t=(x*u[1]-y*u[0])/cross;
                if(s>=0&&s<=1&&t>=0&&t<=1)options.Add((s,t));
            }
            double Projection(double[] point,double[] start,double[] vector)=>Math.Clamp(((point[0]-start[0])*vector[0]+(point[1]-start[1])*vector[1])/Math.Max(1e-12,vector.Sum(x=>x*x)),0,1);
            options.Add((0,Projection(p,q,v)));options.Add((1,Projection(pa[i],q,v)));
            options.Add((Projection(q,p,u),0));options.Add((Projection(pb[j],p,u),1));
            foreach(var (s,t) in options)
            {
                double[] x=[p[0]+s*u[0],p[1]+s*u[1]],y=[q[0]+t*v[0],q[1]+t*v[1]];var gap=PatchGeometry.Distance(x,y);
                if(gap<best.Gap-1e-8)best=new((i-1+s)/(pa.Length-1),(j-1+t)/(pb.Length-1),x,y,gap);
            }
        }
        return best;
    }
    public static Focus Neighborhood(Graph graph,double[] center,IEnumerable<Edge> anchors,double ratio=.10)
    {
        var selected=anchors.DistinctBy(e=>e.Witness,ReferenceEqualityComparer.Instance).ToArray();var reach=selected.Select(e=>Length(e.Points)).DefaultIfEmpty(1).Max();
        var radius=Math.Max(.001,ratio*reach);
        var sites=graph.Sites.OrderBy(s=>PatchGeometry.Distance(XY(s),center)).Where(s=>PatchGeometry.Distance(XY(s),center)<=radius).Take(6).ToArray();
        var edges=graph.Edges.Where(e=>selected.Any(a=>ReferenceEquals(a.Witness,e.Witness))||sites.Any(s=>ReferenceEquals(e.A,s)||ReferenceEquals(e.B,s))).ToArray();
        var axis=new Carrier();var span=Relation.Segment(axis,0,checked((long)Math.Round(reach*Q)));
        var radiusSpan=Relation.Segment(axis,0,checked((long)Math.Round(radius*Q)));
        var focus=G(graph.Surface,G(sites),G(edges.Select(e=>(IElement)e.Witness).ToArray()),new Relation(span,radiusSpan),
            Site(graph.Frame,center,G(selected.Select(e=>(IElement)e.Witness).ToArray())));
        return new(focus,center,reach,radius,sites,edges);
    }
    private static Candidate Make(Graph before,IEnumerable<Edge> output,Focus focus,IEnumerable<Relation> bindings,IEnumerable<Edge> removed,
        IEnumerable<Edge> changed,double movement,bool straightening,string description,Relation? lineContext=null,Relation? observation=null)
    {
        var after=Create(before.Frame,output);var map=bindings.ToArray();var residue=removed.ToArray();var edited=changed.DistinctBy(e=>e.Witness,ReferenceEqualityComparer.Instance).ToArray();
        var issues=new List<string>();
        if(after.Edges.Any(e=>Length(e.Points)<1e-7))issues.Add("would collapse a surviving interval to zero geometry");
        if(after.Edges.Any(e=>{e.Span.TryReadSegment(out _,out var a,out var b);return a.Ticks==b.Ticks;}))issues.Add("would lose a source interval below the declared parameter resolution");
        if(movement>focus.Reach*.16)issues.Add("movement exceeds the declared local reach budget");
        if(Cycles(after)<Cycles(before))issues.Add("would destroy an existing enclosure");
        if(residue.Any(e=>Length(e.Points)>focus.Reach*.085))issues.Add("would remove a substantial source interval");
        if(after.Edges.Any(e=>!before.Edges.Any(p=>ReferenceEquals(p.Source,e.Source))))issues.Add("would invent an unsupported source branch");
        if(after.Sites.Any(s=>!before.Sites.Any(p=>ReferenceEquals(p,s))&&Kind(after,s)=="coincident rays"))issues.Add("would collapse distinct, same-direction branches");
        // Every active ray comes from an actual surviving source interval.
        // A two-arm corner therefore cannot turn into a three-arm T by relabeling.
        var instruction=G(focus.Surface,G(map),G(edited.Select(e=>(IElement)e.Witness).ToArray()),lineContext ?? (IElement)new Placeholder(),observation??(IElement)new Placeholder());
        var surface=ReadWitness.Create(new Carrier(),before.Surface,instruction,after.Surface,G(residue.Select(e=>(IElement)e.Witness).ToArray()),focus.Surface);
        return new(surface,before,after,focus,map,residue,edited,movement,straightening,description,issues.Count==0,issues.ToArray(),lineContext,observation);
    }
    public static Candidate Merge(Graph graph,Relation[] sites,Focus focus,Relation? observation=null)
    {
        var xy=new[]{sites.Average(s=>XY(s)[0]),sites.Average(s=>XY(s)[1])};var joined=Site(graph.Frame,xy,G(sites));
        var removed=new List<Edge>();var changed=new List<Edge>();var edges=new List<Edge>();
        bool Has(Relation site)=>sites.Any(s=>ReferenceEquals(s,site));
        foreach(var e in graph.Edges)
        {
            if(Has(e.A)&&Has(e.B)&&e.Handles.Length==0){removed.Add(e);continue;}
            if(!Has(e.A)&&!Has(e.B)){edges.Add(e);continue;}
            edges.Add(Move(graph,e,Has(e.A)?joined:e.A,Has(e.B)?joined:e.B));changed.Add(e);
        }
        return Make(graph,edges,focus,sites.Select(s=>new Relation(s,joined)),removed,changed,sites.Max(s=>PatchGeometry.Distance(XY(s),xy)),false,"merge nearby sites",observation:observation);
    }
    public static Candidate Attach(Graph graph,NeighborhoodContacts.Contact contact)
    {
        var c=NeighborhoodContacts.Read(contact.Witness,graph);var endpoint=c.Endpoint;var other=c.Other;
        var incident=graph.Edges.Where(e=>ReferenceEquals(e.A,endpoint)||ReferenceEquals(e.B,endpoint)).ToArray();
        var focus=Neighborhood(graph,XY(c.Point),incident.Append(other));
        if(c.T<=.005||c.T>=.995)return Merge(graph,[endpoint,c.T<=.005?other.A:other.B],focus,c.Witness);
        var pair=Cut(graph,other,c.T,c.Point,c.Parameter);
        var changed=incident.Append(other).ToArray();
        var edges=graph.Edges.Where(e=>!ReferenceEquals(e.Witness,other.Witness)).Select(e=>
            ReferenceEquals(e.A,endpoint)||ReferenceEquals(e.B,endpoint)?Move(graph,e,ReferenceEquals(e.A,endpoint)?c.Point:e.A,ReferenceEquals(e.B,endpoint)?c.Point:e.B):e);
        return Make(graph,edges.Concat(new[]{pair.Left,pair.Right}),focus,[new Relation(endpoint,c.Point),new Relation(other.Witness,G(pair.Left.Witness,pair.Right.Witness))],[],changed,c.Gap,false,
            "attach endpoint to nearest other segment",observation:c.Witness);
    }
    public static Candidate Join(Graph graph,Edge a,Edge b,Encounter at,Focus focus,bool trim)
    {
        var point=new[]{(at.P[0]+at.Q[0])/2,(at.P[1]+at.Q[1])/2};var center=Site(graph.Frame,point,G(a.Witness,b.Witness));
        var bindings=new List<Relation>();var replacements=new Dictionary<Relation,Relation>(ReferenceEqualityComparer.Instance);
        var edges=graph.Edges.Where(e=>!ReferenceEquals(e.Witness,a.Witness)&&!ReferenceEquals(e.Witness,b.Witness)).ToList();
        foreach(var (edge,t) in new[]{(a,at.A),(b,at.B)})
        {
            if(t>.005&&t<.995)
            {
                var pair=Cut(graph,edge,t,center);edges.Add(pair.Left);edges.Add(pair.Right);
                bindings.Add(new Relation(edge.Witness,G(pair.Left.Witness,pair.Right.Witness)));
            }
            else
            {
                var old=t<=.005?edge.A:edge.B;replacements[old]=center;bindings.Add(new Relation(old,center));
                edges.Add(Move(graph,edge,t<=.005?center:edge.A,t<=.005?edge.B:center));
            }
        }
        // All other edges at a moved joint follow its exposed shared site.
        edges=edges.Select(e=>replacements.ContainsKey(e.A)||replacements.ContainsKey(e.B)?Move(graph,e,replacements.GetValueOrDefault(e.A,e.A),replacements.GetValueOrDefault(e.B,e.B)):e).ToList();
        var joined=Create(graph.Frame,edges);var removed=new List<Edge>();
        if(trim)
        {
            var incident=joined.Edges.Where(e=>ReferenceEquals(e.A,center)||ReferenceEquals(e.B,center)).ToArray();
            var longest=incident.Select(e=>Length(e.Points)).DefaultIfEmpty(1).Max();
            foreach(var e in incident.OrderBy(e=>Length(e.Points)))
            {
                var far=ReferenceEquals(e.A,center)?e.B:e.A;
                if(incident.Length-removed.Count<=2||Degree(joined,far)!=1||Length(e.Points)>longest*.08)continue;
                removed.Add(e);
            }
            edges.RemoveAll(e=>removed.Any(r=>ReferenceEquals(r.Witness,e.Witness)));
        }
        return Make(graph,edges,focus,bindings,removed,[a,b],at.Gap/2,false,trim?"connect and trim short overshoots":"connect source branches");
    }
    public static Candidate Trim(Graph graph,Edge edge,Relation joint,Focus focus)
        =>Make(graph,graph.Edges.Where(e=>!ReferenceEquals(e.Witness,edge.Witness)),focus,[],[edge],[edge],0,false,"trim a short terminal branch");
    public static Candidate Straighten(Graph graph,Edge edge,Focus focus)
        =>Make(graph,graph.Edges.Select(e=>ReferenceEquals(e.Witness,edge.Witness)?Curve(graph.Frame,e.Source,e.Span,e.A,e.B,[],e.Witness):e),
            focus,[],[],[edge],0,true,"interpret a curve as a straight span");

    public static Candidate? Align(Graph graph,Relation site,Edge first,Edge second,Focus focus)
    {
        var a=XY(ReferenceEquals(first.A,site)?first.B:first.A);var b=XY(ReferenceEquals(second.A,site)?second.B:second.A);var p=XY(site);
        var dx=b[0]-a[0];var dy=b[1]-a[1];var t=((p[0]-a[0])*dx+(p[1]-a[1])*dy)/Math.Max(1e-10,dx*dx+dy*dy);
        if(t<=.03||t>=.97)return null;
        var target=new[]{a[0]+t*dx,a[1]+t*dy};var movement=PatchGeometry.Distance(p,target);
        if(movement<focus.Reach*.002)return null;
        var center=Site(graph.Frame,target,G(site,first.Witness,second.Witness));
        var changed=graph.Edges.Where(e=>ReferenceEquals(e.A,site)||ReferenceEquals(e.B,site)).ToArray();
        var edges=graph.Edges.Select(e=>changed.Any(c=>ReferenceEquals(c.Witness,e.Witness))?Move(graph,e,ReferenceEquals(e.A,site)?center:e.A,ReferenceEquals(e.B,site)?center:e.B):e);
        return Make(graph,edges,focus,[new Relation(site,center)],[],changed,movement,true,"align continuing spans",G(first.Witness,second.Witness,site));
    }

    public static List<Candidate> Generate(Graph graph,int maximum=192,NeighborhoodContacts.Contact[]? contacts=null)
    {
        var result=new List<Candidate>();
        void Add(Candidate c)
        {
            // Similar coordinates do not identify candidates or their routes.
            result.Add(c);
        }
        foreach(var contact in contacts??NeighborhoodContacts.Discover(graph))
            if(contact.Gap<=contact.Limit)Add(Attach(graph,contact));
        foreach(var site in graph.Sites)
        {
            var incident=graph.Edges.Where(e=>ReferenceEquals(e.A,site)||ReferenceEquals(e.B,site)).ToArray();
            var focus=Neighborhood(graph,XY(site),incident);
            var nearby=focus.Sites.Where(s=>!ReferenceEquals(s,site)).ToArray();
            foreach(var other in nearby.Take(3))Add(Merge(graph,[site,other],focus));
            if(nearby.Length>1)Add(Merge(graph,[site,..nearby.Take(3)],focus));
            if(Degree(graph,site)>=2)
                foreach(var edge in incident.Where(e=>Length(e.Points)<focus.Reach*.08&&Degree(graph,ReferenceEquals(e.A,site)?e.B:e.A)==1))
                    Add(Trim(graph,edge,site,focus));
            if(incident.Length is >=2 and <=4)
                for(var i=0;i<incident.Length;i++)for(var j=i+1;j<incident.Length;j++)
                {
                    var first=incident[i];var second=incident[j];
                    var a=XY(ReferenceEquals(first.A,site)?first.B:first.A);var b=XY(ReferenceEquals(second.A,site)?second.B:second.A);
                    var angle=Math.Abs((Angle(XY(site),a)-Angle(XY(site),b)+540)%360-180);
                    if(angle>150&&Align(graph,site,first,second,focus) is { } aligned)Add(aligned);
                }
        }
        for(var i=0;i<graph.Edges.Length;i++)for(var j=i+1;j<graph.Edges.Length;j++)
        {
            var a=graph.Edges[i];var b=graph.Edges[j];
            if(ReferenceEquals(a.A,b.A)||ReferenceEquals(a.A,b.B)||ReferenceEquals(a.B,b.A)||ReferenceEquals(a.B,b.B))continue;
            var limit=.10*Math.Min(Length(a.Points),Length(b.Points));
            var x=a.Points;var y=b.Points;
            var box=Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(x.Min(p=>p[0])-y.Max(p=>p[0]),y.Min(p=>p[0])-x.Max(p=>p[0]))),2)+Math.Pow(Math.Max(0,Math.Max(x.Min(p=>p[1])-y.Max(p=>p[1]),y.Min(p=>p[1])-x.Max(p=>p[1]))),2));
            if(box>limit)continue;
            var near=Closest(a,b);if(near.Gap>limit)continue;
            var focus=Neighborhood(graph,[(near.P[0]+near.Q[0])/2,(near.P[1]+near.Q[1])/2],[a,b]);
            Add(Join(graph,a,b,near,focus,false));
            var trimmed=Join(graph,a,b,near,focus,true);if(trimmed.Removed.Length>0)Add(trimmed);
        }
        foreach(var edge in graph.Edges.Where(e=>e.Handles.Length>0))
        {
            var midpoint=PatchGeometry.Bezier(edge.Points,.5);var focus=Neighborhood(graph,midpoint,[edge]);
            if(PatchGeometry.Distance(edge.Points[0],edge.Points[^1])>.35*Length(edge.Points))Add(Straighten(graph,edge,focus));
        }
        var sorted=result.OrderBy(c=>c.Admissible?0:1).ThenBy(c=>c.Movement/Math.Max(.001,c.Focus.Reach)).ToArray();
        var repair=sorted.Where(c=>!c.Straightening).ToArray();var lines=sorted.Where(c=>c.Straightening).ToArray();var bounded=new List<Candidate>();
        for(var i=0;bounded.Count<maximum&&(i<repair.Length||i<lines.Length);i++)
        {
            if(i<repair.Length)bounded.Add(repair[i]);
            if(bounded.Count<maximum&&i<lines.Length)bounded.Add(lines[i]);
        }
        return bounded;
    }
}
