namespace GojoQuickDraw.Reads;

// A calculated observer read. Site keys select existing identities; geometric
// encounter groups never retarget them or turn coincidence into shared identity.
public sealed record JointEdge(string Id, string Part, double[] A, double[] B, string SiteA, string SiteB,
    string[] Sources, bool Added = false);
public sealed record JointAt(string Edge, double T);
public sealed record JointNode(string Id, string Kind, double[] Point, double[] Directions, JointAt[] On,
    string[] Sources, bool Shared, double Gap, string Basis, bool Added = false, int CoincidentRays = 0);
public sealed record JointLink(string From, string To, string Edge, string Order);
public sealed record SpatialOrder(string From, string To, string Horizontal, string Vertical);
public sealed record JointGraph(JointNode[] Nodes, JointLink[] Links, SpatialOrder[] Orders, bool OrderLimitReached);
public sealed record JointMatch(string Target, string[] Candidates, string Status, double SupportOverlap, string? SourceKind, string TargetKind);
public sealed record JointOrderChange(string First, string Second, string SourceOrder, string FinalOrder, bool Same);
public sealed record JointLineMatch(string FinalEdge, string[] SourceProfiles, string Status);
public sealed record JointComparison(JointGraph Source, JointGraph Final, JointMatch[] Matches, JointOrderChange[] OrderChecks,
    string[] UnmatchedSource, JointLineMatch[] LineMatches, string Basis, string Qualification);

public static class JointGraphRead
{
    public const string Version = "joint-graph-read.v1";
    public static double Distance(double[] a, double[] b) => Math.Sqrt((a[0]-b[0])*(a[0]-b[0])+(a[1]-b[1])*(a[1]-b[1]));
    public static bool Significant(JointNode node) => node.Kind is not ("endpoint" or "I");
    private static double Angle(double x, double y) => (Math.Atan2(y,x)*180/Math.PI+360)%360;
    private static double Separation(double a, double b) => Math.Abs((a-b+540)%360-180);
    public static string Classify(double[] directions, double allowance = 14)
    {
        var opposite = new List<(int A,int B)>();
        for(var i=0;i<directions.Length;i++) for(var j=i+1;j<directions.Length;j++)
            if(180-Separation(directions[i],directions[j])<=allowance) opposite.Add((i,j));
        return directions.Length switch {
            0 => "unresolved", 1 => "endpoint", 2 => opposite.Count>0 ? "I" : "L",
            3 => opposite.Count>0 ? "T" : "Y",
            4 => opposite.Any(a => opposite.Any(b => a.A!=b.A && a.A!=b.B && a.B!=b.A && a.B!=b.B)) ? "X" : opposite.Count>0 ? "K" : "unresolved",
            _ => "unresolved"
        };
    }
    public static string Normalize(string kind) => kind switch { "O candidate" => "O", "four-way" or "complex" => "unresolved", _ => kind };
    public static string Heading(double angle) => new[]{"right","down-right","down","down-left","left","up-left","up","up-right"}[(int)Math.Round(angle/45)%8];

    public static JointGraph Graph(JointNode[] nodes)
    {
        var links = new List<JointLink>();
        foreach(var group in nodes.Where(n=>n.Kind!="O").SelectMany(n=>n.On.Select(p=>(Node:n,At:p))).GroupBy(p=>p.At.Edge))
        {
            var ordered=group.OrderBy(p=>p.At.T).ThenBy(p=>p.Node.Id,StringComparer.Ordinal).ToArray();
            for(var i=1;i<ordered.Length;i++)
            {
                var a=ordered[i-1];var b=ordered[i];
                if(a.Node.Id==b.Node.Id || Math.Abs(a.At.T-b.At.T)<.0001)continue;
                links.Add(new(a.Node.Id,b.Node.Id,group.Key,Heading(Angle(b.Node.Point[0]-a.Node.Point[0],b.Node.Point[1]-a.Node.Point[1]))));
            }
        }
        var selected=nodes.Where(Significant).ToArray();var orderNodes=selected.Take(64).ToArray();
        var width=Math.Max(1,nodes.Select(n=>n.Point[0]).DefaultIfEmpty().Max()-nodes.Select(n=>n.Point[0]).DefaultIfEmpty().Min());
        var height=Math.Max(1,nodes.Select(n=>n.Point[1]).DefaultIfEmpty().Max()-nodes.Select(n=>n.Point[1]).DefaultIfEmpty().Min());
        var orders=new List<SpatialOrder>();
        for(var i=0;i<orderNodes.Length;i++)for(var j=i+1;j<orderNodes.Length;j++)
        {
            var a=orderNodes[i];var b=orderNodes[j];var dx=b.Point[0]-a.Point[0];var dy=b.Point[1]-a.Point[1];
            orders.Add(new(a.Id,b.Id,Math.Abs(dx)<.04*width ? "aligned" : dx>0 ? "right" : "left",
                Math.Abs(dy)<.04*height ? "level" : dy>0 ? "below" : "above"));
        }
        return new(nodes,links.ToArray(),orders.ToArray(),selected.Length>64);
    }

    public static JointGraph Final(JointEdge[] input)
    {
        var edges=input.Where(e=>Distance(e.A,e.B)>.001).ToArray();
        var groups=new List<List<(JointEdge Edge,double T,double[] Point,string? Site)>>();
        void Encounter(JointEdge edge,double t,double[] p,string? site)
        {
            var group=groups.FirstOrDefault(g=>g.All(v=>Distance(v.Point,p)<.01));
            if(group is null){group=[];groups.Add(group);}
            if(!group.Any(v=>v.Edge.Id==edge.Id && Math.Abs(v.T-t)<.00001))group.Add((edge,t,p,site));
        }
        foreach(var e in edges){Encounter(e,0,e.A,e.SiteA);Encounter(e,1,e.B,e.SiteB);}
        for(var i=0;i<edges.Length;i++)for(var j=i+1;j<edges.Length;j++)
        {
            var a=edges[i];var b=edges[j];var vx=a.B[0]-a.A[0];var vy=a.B[1]-a.A[1];var ux=b.B[0]-b.A[0];var uy=b.B[1]-b.A[1];
            var det=vx*uy-vy*ux;if(Math.Abs(det)<.000001)continue;
            var dx=b.A[0]-a.A[0];var dy=b.A[1]-a.A[1];var t=(dx*uy-dy*ux)/det;var s=(dx*vy-dy*vx)/det;
            if(t<-.000001 || t>1.000001 || s<-.000001 || s>1.000001)continue;
            t=Math.Clamp(t,0,1);s=Math.Clamp(s,0,1);double[] p=[a.A[0]+t*vx,a.A[1]+t*vy];
            Encounter(a,t,p,t<.000001 ? a.SiteA : t>.999999 ? a.SiteB : null);
            Encounter(b,s,p,s<.000001 ? b.SiteA : s>.999999 ? b.SiteB : null);
        }
        var nodes=new List<JointNode>();
        foreach(var group in groups)
        {
            var rays=new List<double>();
            foreach(var p in group)
            {
                var angle=Angle(p.Edge.B[0]-p.Edge.A[0],p.Edge.B[1]-p.Edge.A[1]);
                if(p.T>.000001)rays.Add((angle+180)%360);if(p.T<.999999)rays.Add(angle);
            }
            // Coincident rendered boundaries share a visible direction, but
            // their separate edge participation and identities remain in On.
            var unique=new List<double>();foreach(var ray in rays.Order())if(!unique.Any(r=>Separation(r,ray)<=1))unique.Add(ray);
            var shared=group.All(p=>p.Site is not null) && group.Select(p=>p.Site).Distinct().Count()==1;
            var point=new[]{group.Average(p=>p.Point[0]),group.Average(p=>p.Point[1])};
            nodes.Add(new("final-j"+nodes.Count,Classify(unique.ToArray()),point,unique.ToArray(),
                group.Select(p=>new JointAt(p.Edge.Id,p.T)).ToArray(),group.SelectMany(p=>p.Edge.Sources).Distinct().Order().ToArray(),shared,
                group.Max(p=>Distance(p.Point,point)),shared ? "shared constructed site" : "geometric encounter; sites remain distinct",
                group.All(p=>p.Edge.Added),rays.Count-unique.Count));
        }
        // A finite fundamental cycle basis per part. These are closed walks,
        // not an assertion about filled faces or the number of window panes.
        foreach(var part in edges.GroupBy(e=>e.Part))
        {
            var tree=new Dictionary<string,List<(string To,JointEdge Edge)>>();var seen=new HashSet<string>();
            foreach(var edge in part)
            {
                if(!tree.ContainsKey(edge.SiteA))tree[edge.SiteA]=[];if(!tree.ContainsKey(edge.SiteB))tree[edge.SiteB]=[];
                var queue=new Queue<(string Site,List<JointEdge> Path)>();queue.Enqueue((edge.SiteA,[]));var visited=new HashSet<string>();List<JointEdge>? path=null;
                while(queue.TryDequeue(out var next))
                {
                    if(!visited.Add(next.Site))continue;if(next.Site==edge.SiteB){path=next.Path;break;}
                    foreach(var link in tree[next.Site])queue.Enqueue((link.To,[..next.Path,link.Edge]));
                }
                if(path is null){tree[edge.SiteA].Add((edge.SiteB,edge));tree[edge.SiteB].Add((edge.SiteA,edge));continue;}
                var loop=path.Append(edge).ToArray();if(loop.Length<3)continue;
                var key=string.Join("|",loop.Select(e=>e.Id).Order());if(!seen.Add(key))continue;
                nodes.Add(new("final-cycle"+nodes.Count,"O",[loop.Average(e=>(e.A[0]+e.B[0])/2),loop.Average(e=>(e.A[1]+e.B[1])/2)],[],
                    loop.Select(e=>new JointAt(e.Id,.5)).ToArray(),loop.SelectMany(e=>e.Sources).Distinct().Order().ToArray(),true,0,
                    "closed constructed walk; cycle basis, not a point junction",loop.All(e=>e.Added)));
            }
        }
        return Graph(nodes.ToArray());
    }

    public static JointComparison Compare(JointGraph source,JointGraph final,string basis,JointEdge[]? finalEdges=null)
    {
        var matches=new List<JointMatch>();
        foreach(var node in final.Nodes.Where(Significant))
        {
            var scored=source.Nodes.Where(Significant).Where(s=>(s.Kind=="O")== (node.Kind=="O"))
                .Select(s=>(Node:s,Shared:s.Sources.Intersect(node.Sources).Count(),Score:(double)s.Sources.Intersect(node.Sources).Count()/Math.Max(1,s.Sources.Union(node.Sources).Count())))
                .Where(s=>s.Shared>0 && s.Score>=.25).OrderByDescending(s=>s.Score).ThenBy(s=>s.Node.Id,StringComparer.Ordinal).ToArray();
            var choices=scored.Where(s=>s.Score>=scored[0].Score-.10).Take(8).ToArray();
            var status=node.Added ? "added" : choices.Length==0 ? "unmapped" : choices.Length>1 ? "ambiguous" : choices[0].Node.Kind==node.Kind ? "same type" : "changed type";
            matches.Add(new(node.Id,choices.Select(s=>s.Node.Id).ToArray(),status,choices.FirstOrDefault().Score,choices.Length==1 ? choices[0].Node.Kind : null,node.Kind));
        }
        var reused=matches.Where(m=>m.Candidates.Length==1).GroupBy(m=>m.Candidates[0]).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet();
        matches=matches.Select(m=>m.Candidates.Length==1 && reused.Contains(m.Candidates[0]) ? m with {Status="ambiguous"} : m).ToList();
        var one=matches.Where(m=>m.Status is "same type" or "changed type").ToDictionary(m=>m.Target,m=>m.Candidates[0]);
        var orderChecks=new List<JointOrderChange>();
        foreach(var order in final.Orders)
        {
            if(!one.TryGetValue(order.From,out var a) || !one.TryGetValue(order.To,out var b))continue;
            var before=source.Orders.FirstOrDefault(o=>o.From==a&&o.To==b || o.From==b&&o.To==a);if(before is null)continue;
            string Reverse(string value)=>value switch {"right"=>"left","left"=>"right","above"=>"below","below"=>"above",_=>value};
            var x=before.From==a ? before.Horizontal : Reverse(before.Horizontal);var y=before.From==a ? before.Vertical : Reverse(before.Vertical);
            orderChecks.Add(new(order.From,order.To,x+" / "+y,order.Horizontal+" / "+order.Vertical,x==order.Horizontal&&y==order.Vertical));
        }
        return new(source,final,matches.ToArray(),orderChecks.ToArray(),source.Nodes.Where(Significant).Select(n=>n.Id).Except(matches.SelectMany(m=>m.Candidates)).ToArray(),
            (finalEdges ?? []).Select(e=>new JointLineMatch(e.Id,e.Sources,e.Added ? "added" : e.Sources.Length>0 ? "mapped boundary support" : "unmapped")).ToArray(),basis,
            "Derived reads over retained structure. Candidate matches use mapped source profiles, not absolute position or the expected joint label. Ambiguous and unmapped cases are not accuracy failures. Authored final shapes are weak targets, not human ground truth.");
    }

    public static object TrainingView(JointComparison read)
    {
        object View(JointGraph graph)=>new {nodes=graph.Nodes.Select(n=>new {id=n.Id,kind=n.Kind,directions=n.Directions.Select(Heading),
                on=n.On.Select(p=>p.Edge),sources=n.Sources,shared=n.Shared,added=n.Added,coincidentRays=n.CoincidentRays,basis=n.Basis}),
            edges=graph.Links,relativeOrder=graph.Orders,orderLimitReached=graph.OrderLimitReached};
        return new {schema=Version,source=View(read.Source),final=View(read.Final),matches=read.Matches,orderChecks=read.OrderChecks,
            unmatchedSource=read.UnmatchedSource,lineMatches=read.LineMatches,basis=read.Basis,qualification=read.Qualification};
    }
}
