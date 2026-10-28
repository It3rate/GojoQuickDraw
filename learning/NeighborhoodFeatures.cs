using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using static GojoQuickDraw.Learning.PatchEvidence;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.NeighborhoodProposals;

namespace GojoQuickDraw.Learning;

public static class NeighborhoodFeatures
{
    public sealed record Cells(Relation Surface,int[] Bands,double[] Values);
    public static readonly string[] LineNames=["chord-deviation/reach","turn/full-turn","peak/total-turn","hook/reach","chord-loss/reach","end-gap/reach","control-participation","tangent/chord-mismatch"];
    public static readonly string[] RepairNames=["movement/local-reach","removed/local-reach","local-site-count","target-joint-degree","bound-site-count","new-edge-count","removed-edge-count","joined-components", "cycle-change", "source-bend", "source-turn", "short-rays-before", "short-rays-after", "source-edge-count", "straightening", "target-opposed-pairs", "target-coincident-rays", "move/radius",
        "nearby-endpoints","nearby-continuations","nearby-corners","nearby-tees","nearby-crossings","nearby-forks"];
    private static readonly double[] RatioCuts=[.004,.02,.06,.15,.35];
    private static readonly double[] CountCuts=[.5,1.5,2.5,3.5,4.5];
    internal static Cells Publish(IElement selected,IElement definition,double[] values,double[][] cuts)
    {
        var metrics=new Carrier();var grade=new Carrier();var rows=new List<IElement>();var bands=new int[values.Length];
        for(var i=0;i<values.Length;i++)
        {
            var tick=checked((long)Math.Round(values[i]*Q));var boundaries=cuts[i].Select(c=>new CarrierLocation(metrics,checked((long)Math.Round(c*Q)))).ToArray();
            bands[i]=boundaries.Count(b=>tick>b.Ticks);
            var measured=ReadWitness.Create(new Carrier(),selected,G(definition,Relation.Segment(metrics,0,Q)),new CarrierLocation(metrics,tick),G(),definition);
            var partition=G(Relation.Segment(grade,0,5),G(boundaries.Cast<IElement>().ToArray()));
            var lower=bands[i]==0?0:boundaries[bands[i]-1].Ticks;
            rows.Add(ReadWitness.Create(new Carrier(),measured,partition,new CarrierLocation(grade,bands[i]),Relation.Segment(metrics,lower,tick),definition));
        }
        return new(G(rows.ToArray()),bands,values);
    }
    public static int[] ReadCells(Relation surface)
    {
        return Rows(surface).Select(row=>
        {
            if(row is not Relation r||!ReadWitness.TryRead(r,out var source,out var basis,out var result,out _,out var provenance)||
                source is not Relation metric||!ReadWitness.TryRead(metric,out _,out _,out var fine,out _,out _)||fine is not CarrierLocation measurement||
                basis is not Relation partition||
                Fields(partition,2)[0] is not Relation b||!b.TryReadSegment(out var axis,out var first,out var last)||first.Ticks!=0||last.Ticks!=5||
                result is not CarrierLocation value||!ReferenceEquals(value.Carrier,axis)||value.Ticks<0||value.Ticks>5||provenance is not Relation)
                throw new InvalidDataException("Coarse neighborhood cell lost its measured source or finite grade basis.");
            var cuts=Rows(Fields(partition,2)[1]);
            if(cuts.Length!=5||cuts.Any(c=>c is not CarrierLocation boundary||!ReferenceEquals(boundary.Carrier,measurement.Carrier)))throw new InvalidDataException("A coarse cell lost its measured partition cuts.");
            var ticks=cuts.Cast<CarrierLocation>().Select(c=>c.Ticks).ToArray();
            if(!ticks.SequenceEqual(ticks.Order())||ticks.Distinct().Count()!=5||value.Ticks!=ticks.Count(t=>measurement.Ticks>t))throw new InvalidDataException("Coarse grade does not read from its actual partition.");
            return (int)value.Ticks;
        }).ToArray();
    }
    public static Cells Line(Edge edge)
    {
        return LineRead(Sample(edge.Points,20),edge.Witness,G(edge.Witness,G(edge.Handles)),edge.Handles.Length);
    }
    public static double[][] RoutePoints(Edge first,Edge second,Relation at)
    {
        var a=Sample(first.Points,20);var b=Sample(second.Points,20);
        if(ReferenceEquals(first.A,at))a=a.Reverse().ToArray();if(ReferenceEquals(second.B,at))b=b.Reverse().ToArray();
        return [..a,..b.Skip(1)];
    }
    public static Cells Route(Graph graph,Relation context)
    {
        var f=Fields(context,3);var first=graph.Edges.Single(e=>ReferenceEquals(e.Witness,f[0]));var second=graph.Edges.Single(e=>ReferenceEquals(e.Witness,f[1]));
        return LineRead(RoutePoints(first,second,(Relation)f[2]),context,context,Math.Max(first.Handles.Length,second.Handles.Length));
    }
    private static Cells LineRead(double[][] p,IElement source,IElement geometry,int handles)
    {
        var m=PatchGeometry.Measure(p,true);var chord=PatchGeometry.Distance(p[0],p[^1]);
        var direct=new[]{p[^1][0]-p[0][0],p[^1][1]-p[0][1]};
        double Mismatch(double[] a,double[] b)
        {
            var d=new[]{b[0]-a[0],b[1]-a[1]};var length=Math.Sqrt(d.Sum(v=>v*v))*chord;
            return length<1e-10?1:(1-Math.Clamp((d[0]*direct[0]+d[1]*direct[1])/length,-1,1))/2;
        }
        double[] values=[m.Bend,m.Turn/360,m.Locality,m.Hook,1-chord/m.Length,chord/m.Length,(p.Length-2)/3.0,
            (Mismatch(p[0],p[1])+Mismatch(p[^2],p[^1]))/2];
        values[6]=handles/3.0;
        double[][] cuts=[RatioCuts,RatioCuts,[.1,.25,.45,.65,.85],RatioCuts,RatioCuts,[.05,.2,.5,.8,.95],CountCuts.Select(c=>c/3).ToArray(),RatioCuts];
        var pathAxis=new Carrier();var turnAxis=new Carrier();
        var definition=G(geometry,new Relation(Relation.Segment(pathAxis,0,checked((long)Math.Round(chord*Q))),Relation.Segment(pathAxis,0,checked((long)Math.Round(m.Length*Q)))),
            Relation.Segment(turnAxis,0,360*Q));
        return Publish(source,definition,values,cuts);
    }
    public static Cells Repair(Candidate c)
    {
        var before=c.Before;var after=c.After;var focus=c.Focus;var reach=Math.Max(.0001,focus.Reach);
        var newSites=after.Sites.Where(s=>!before.Sites.Any(p=>ReferenceEquals(p,s))).ToArray();
        var center=newSites.OrderByDescending(s=>Degree(after,s)).FirstOrDefault() ?? after.Sites.OrderBy(s=>PatchGeometry.Distance(XY(s),focus.Center)).FirstOrDefault();
        var rays=center is null?[]:Directions(after,center);var opposed=0;var coincident=0;
        for(var i=0;i<rays.Length;i++)for(var j=i+1;j<rays.Length;j++)
        {
            var separation=Math.Abs((rays[i]-rays[j]+540)%360-180);if(separation>166)opposed++;if(separation<12)coincident++;
        }
        int Short(Graph graph)=>graph.Edges.Count(e=>Length(e.Points)<reach*.08 &&
            ((Degree(graph,e.A)==1&&Degree(graph,e.B)>2)||(Degree(graph,e.B)==1&&Degree(graph,e.A)>2)));
        var lines=c.Changed.Where(e=>Length(e.Points)>1e-7).Select(e=>PatchGeometry.Measure(Sample(e.Points,20),true)).ToArray();
        double[] values=[c.Movement/reach,c.Removed.Sum(e=>Length(e.Points))/reach,focus.Sites.Length,
            center is null?0:Degree(after,center),c.Bindings.Count(b=>b.Left is Relation{Right:Relation{Left:CarrierLocation,Right:CarrierLocation}}),
            Math.Max(0,after.Edges.Length-before.Edges.Length),c.Removed.Length,Math.Max(0,Components(before)-Components(after)),
            Cycles(after)-Cycles(before)+1,lines.Select(l=>l.Bend).DefaultIfEmpty().Max(),lines.Select(l=>l.Turn/360).DefaultIfEmpty().Max(),
            Short(before),Short(after),focus.Edges.Length,c.Straightening?1:0,opposed,coincident,c.Movement/Math.Max(.0001,focus.Radius),
            ..new[]{"endpoint","I","L","T","X","Y"}.Select(k=>(double)focus.Sites.Count(s=>Kind(before,s)==k))];
        var cuts=values.Select((v,i)=>i is 0 or 1 or 9 or 10 or 17?RatioCuts:CountCuts).ToArray();
        return Publish(c.Surface,G(focus.Surface,G(c.Bindings),G(c.Removed.Select(e=>(IElement)e.Witness).ToArray()),after.Surface),values,cuts);
    }
    public static Relation Score(IElement selected,Relation cells,double value,string modelHash)
    {
        if(!double.IsFinite(value)||value<0||value>1)throw new InvalidDataException("A provider score must be a finite support read.");
        var axis=new Carrier();var contract=EncodedTextSurface.Create(new Carrier(),modelHash);
        return ReadWitness.Create(new Carrier(),new Relation(selected,cells),Relation.Segment(axis,0,Q),new CarrierLocation(axis,checked((long)Math.Round(value*Q))),G(),contract);
    }
    public static double ReadScore(Relation score,IElement selected,Relation cells)
    {
        if(!ReadWitness.TryRead(score,out var input,out var basis,out var result,out _,out _)||input is not Relation pair||
            !ReferenceEquals(pair.Left,selected)||!ReferenceEquals(pair.Right,cells)||basis is not Relation b||
            !b.TryReadSegment(out var axis,out var lo,out var hi)||lo.Ticks!=0||hi.Ticks!=Q||result is not CarrierLocation v||!ReferenceEquals(v.Carrier,axis)||v.Ticks<0||v.Ticks>Q)
            throw new InvalidDataException("A neighborhood opinion lost its actual source/cells or finite score basis.");
        return v.Ticks/(double)Q;
    }
    public static (double Probability,int Unseen) Predict(JsonElement model,int[] bands)
    {
        var columns=model.GetProperty("columns").EnumerateArray().Select(v=>v.GetString()!).ToArray();
        var tokens=Tokens(bands);var weights=model.GetProperty("weights").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
        if(columns.Length!=weights.Length||weights.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("Malformed local provider weights.");
        var index=columns.Select((t,i)=>(t,i)).ToDictionary(v=>v.t,v=>v.i);var score=0.0;var unseen=0;
        foreach(var token in tokens){if(index.TryGetValue(token,out var k))score+=weights[k];else unseen++;}
        return (1/(1+Math.Exp(-Math.Clamp(score,-40,40))),unseen);
    }
    public static void ValidateBundle(JsonElement bundle)
    {
        if(bundle.GetProperty("schema").GetString()!="gojo.neighborhood-models.v1")throw new InvalidDataException("Expected a neighborhood provider bundle.");
        foreach(var (name,names) in new[]{("line",LineNames),("repair",RepairNames),("closure",NeighborhoodClosure.Names)})
        {
            if(name=="closure"&&!bundle.TryGetProperty(name,out _))continue;
            var model=bundle.GetProperty(name);
            if(model.GetProperty("schema").GetString()!="gojo.neighborhood-provider.v1"||model.GetProperty("featureCount").GetInt32()!=names.Length||
                !model.GetProperty("featureNames").EnumerateArray().Select(e=>e.GetString()).SequenceEqual(names))throw new InvalidDataException("The saved provider has a different native feature interface.");
            var columns=model.GetProperty("columns").EnumerateArray().Select(e=>e.GetString()).ToArray();var weights=model.GetProperty("weights").EnumerateArray().ToArray();
            if(columns.Length==0||columns.Any(string.IsNullOrWhiteSpace)||columns.Distinct(StringComparer.Ordinal).Count()!=columns.Length||!columns.Contains("bias")||
                weights.Length!=columns.Length||weights.Any(e=>e.ValueKind!=JsonValueKind.Number||!e.TryGetDouble(out var v)||!double.IsFinite(v)))throw new InvalidDataException("The provider needs distinct columns and finite matching weights.");
        }
    }
    public static string[] Tokens(int[] bands)=>["bias",..bands.Select((b,i)=>$"{i}:{b}"),
        $"pair:0:{bands[0]}:3:{bands[Math.Min(3,bands.Length-1)]}", $"pair:1:{bands[1]}:5:{bands[Math.Min(5,bands.Length-1)]}"];
}
