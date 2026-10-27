using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QuickDraw;
using Gojo.Structures;
using static GojoQuickDraw.Learning.PatchGeometry;

namespace GojoQuickDraw.Learning;

/// <summary>Source-selected local read witnesses and competing connected target
/// constructions. The coarse model consumes only the published band cells.
/// Metric source and sampling residue remain available in the native archive.</summary>
public static class PatchEvidence
{
    public const long Q=1_000_000;
    public static readonly string[] Names=["separate","continuation","corner","tee","crossing"];
    public sealed record Machine(Relation Surface,Relation[] Sites,Relation[] Edges);
    public sealed record Export(string Id,int[] Bands,double[][][] Runs,double[] Encounter,double[] Parameters,double[] Orientation,
        int? Label,string LabelBasis,string Group,string SourceKey,int[] Profiles,string Archive,string[] Candidates);
    public static Relation G(params IElement[] elements)=>Relation.OrderedGroup(new Carrier(),elements);
    private static long Tick(double value)=>checked((long)Math.Round(value*Q));
    private static IElement Position(Carrier x,Carrier y,double a,double b)=>new Relation(new CarrierLocation(x,Tick(a)),new CarrierLocation(y,Tick(b)));
    public static Machine Construction(int index)
    {
        // All target sites are fresh. Only target edges explicitly share them.
        // Page coordinates, orientation and source bindings are separate ports.
        var x=new Carrier();var y=new Carrier(x.UnitShape);
        double[][] xy=index switch {
            0=>[[-1,0],[1,0],[0,-1],[0,-.2]],
            1=>[[-1,0],[0,0],[1,0]],
            2=>[[-1,0],[0,0],[0,1]],
            3=>[[-1,0],[0,0],[1,0],[0,1]],
            4=>[[-1,0],[0,0],[1,0],[0,-1],[0,1]],
            _=>throw new ArgumentOutOfRangeException(nameof(index))};
        int[][] links=index switch {0=>[[0,1],[2,3]],1 or 2=>[[0,1],[1,2]],3=>[[0,1],[1,2],[1,3]],_=>[[0,1],[1,2],[1,3],[1,4]]};
        var sites=xy.Select(p=>new Relation(new Placeholder(),Position(x,y,p[0],p[1]))).ToArray();
        var edges=links.Select(e=>new Relation(sites[e[0]],sites[e[1]])).ToArray();
        var ports=G(new Placeholder(),new Placeholder(),new Placeholder()); // placement, orientation, scale
        return new(G(G(Relation.Segment(x,-Q,Q),Relation.Segment(y,-Q,Q)),G(sites),G(edges),ports),sites,edges);
    }
    public static int Kind(Machine machine)
    {
        var degrees=machine.Sites.Select(site=>machine.Edges.Count(e=>ReferenceEquals(e.Left,site)||ReferenceEquals(e.Right,site))).ToArray();
        if(machine.Edges.Length==2&&machine.Sites.Length==4&&degrees.All(d=>d==1))return 0;
        var center=machine.Sites[Array.IndexOf(degrees,degrees.Max())];
        double[] XY(IElement site){var p=(Relation)((Relation)site).Right;return [((CarrierLocation)p.Left).Ticks,((CarrierLocation)p.Right).Ticks];}
        var point=XY(center);var rays=machine.Edges.Where(e=>ReferenceEquals(e.Left,center)||ReferenceEquals(e.Right,center))
            .Select(e=>XY(ReferenceEquals(e.Left,center)?e.Right:e.Left)).Select(p=>new[]{p[0]-point[0],p[1]-point[1]}).ToArray();
        var angles=rays.Select(r=>Math.Atan2(r[1],r[0])*180/Math.PI).ToArray();
        return GojoQuickDraw.Reads.JointGraphRead.Classify(angles,1) switch {
            "I"=>1,"L"=>2,"T"=>3,"X"=>4,_=>throw new InvalidDataException("The connected target is outside this five-construction catalog.")};
    }
    public static object[] Views()=>Enumerable.Range(0,Names.Length).Select(i=>
    {
        var m=Construction(i);
        return (object)new{kind=Names[Kind(m)],points=m.Sites.Select(s=>{var xy=(Relation)s.Right;return new[]{((CarrierLocation)xy.Left).Ticks/(double)Q,((CarrierLocation)xy.Right).Ticks/(double)Q};}).ToArray(),
            segments=m.Edges.Select(e=>new[]{Array.IndexOf(m.Sites,e.Left),Array.IndexOf(m.Sites,e.Right)}).ToArray()};
    }).ToArray();
    private static Machine ReadMachine(IElement element)
    {
        if(element is not Relation root || !root.TryReadOrderedGroup(out var fields)||fields.Length!=4||fields[1] is not Relation sites||fields[2] is not Relation edges||
            !sites.TryReadOrderedGroup(out var s)||!edges.TryReadOrderedGroup(out var e)||s.Any(p=>p is not Relation)||e.Any(p=>p is not Relation))throw new InvalidDataException("Incomplete target construction.");
        var result=new Machine(root,s.Cast<Relation>().ToArray(),e.Cast<Relation>().ToArray());
        if(result.Edges.Any(edge=>!s.Contains(edge.Left)||!s.Contains(edge.Right)))throw new InvalidDataException("Target edge lost its shared site.");
        return result;
    }
    private static Relation BandWitness(IElement source,int feature,double value,IElement definition)
    {
        var unit=new Carrier();var metric=ReadWitness.Create(new Carrier(),source,G(definition,Relation.Segment(unit,0,Q)),new CarrierLocation(unit,Tick(value)),new Placeholder(),source);
        var boundaries=Thresholds[feature].Select(Tick).ToArray();var band=boundaries.Count(b=>Tick(value)>b);
        var lower=band==0?0:boundaries[band-1];IElement upper=band<boundaries.Length ? new CarrierLocation(unit,boundaries[band]):new Placeholder();
        var basis=G(Relation.Segment(unit,0,Q),G(boundaries.Select(b=>(IElement)new CarrierLocation(unit,b)).ToArray()));
        // Only these ordered lower/upper cells are published to the model.
        return ReadWitness.Create(new Carrier(),metric,basis,new Relation(new CarrierLocation(unit,lower),upper),metric,source);
    }
    private static int ReadBand(IElement element)
    {
        if(element is not Relation w||!ReadWitness.TryRead(w,out _,out var frame,out var result,out _,out _)||frame is not Relation basis||
            !basis.TryReadOrderedGroup(out var f)||f.Length!=2||f[1] is not Relation cuts||!cuts.TryReadOrderedGroup(out var b)||result is not Relation {Left:CarrierLocation lower} span)
            throw new InvalidDataException("Coarse band lost its source/basis.");
        if(lower.Ticks==0)return 0;
        for(var i=0;i<b.Length;i++)if(b[i] is CarrierLocation boundary && ReferenceEquals(boundary.Carrier,lower.Carrier)&&boundary.Ticks==lower.Ticks)return i+1;
        throw new InvalidDataException("Band boundary not in its selected basis.");
    }
    private static Relation SyntheticRun(double[][] points)
    {
        var x=new Carrier();var y=new Carrier(x.UnitShape);
        var sites=points.Select(p=>new Relation(new Placeholder(),Position(x,y,p[0],p[1]))).ToArray();
        return G(G(Relation.Segment(x,0,Q),Relation.Segment(y,0,Q)),G(sites),G(sites.Zip(sites.Skip(1),(a,b)=>(IElement)new Relation(a,b)).ToArray()));
    }
    public static (Relation Root,List<Export> Rows) Build(JsonElement request,string archive)
    {
        var outputs=new List<Export>();var patches=new List<IElement>();IElement source;
        var real=request.TryGetProperty("packet",out var packet);
        QuickDrawAnnotationEvidence.Read? annotation=null;
        if(real)
        {
            source=QuickDrawAnnotationEvidence.Import(packet);
            if(!QuickDrawAnnotationEvidence.TryRead((Relation)source,out annotation))throw new InvalidDataException("Unreadable acquired profiles.");
        }
        else source=new Placeholder();
        foreach(var entry in request.GetProperty("patches").EnumerateArray())
        {
            double[][][] curves;IElement selected;int[] profiles;
            if(real)
            {
                profiles=entry.GetProperty("profiles").EnumerateArray().Select(p=>p.GetInt32()).ToArray();
                if(profiles.Length!=2||profiles[0]==profiles[1]||profiles.Any(p=>p<0||p>=annotation!.Profiles.Length))throw new InvalidDataException("Patch must expose two distinct existing profiles.");
                var p=profiles.Select(i=>annotation!.Profiles[i]).ToArray();selected=new Relation(p[0].Witness,p[1].Witness);
                curves=p.Select(a=>a.Curve.Controls.Select(c=>new[]{c.X/(double)QuickDrawAnnotationEvidence.CoordinateQuantum,c.Y/(double)QuickDrawAnnotationEvidence.CoordinateQuantum}).ToArray()).ToArray();
            }
            else
            {
                profiles=[0,1];curves=entry.GetProperty("runs").EnumerateArray().Select(run=>run.EnumerateArray().Select(p=>p.EnumerateArray().Select(v=>v.GetDouble()).ToArray()).ToArray()).ToArray();
                if(curves.Length!=2)throw new InvalidDataException("A synthetic patch requires two runs.");
                selected=new Relation(SyntheticRun(curves[0]),SyntheticRun(curves[1]));
            }
            var geometry=ReadPair(curves[0],curves[1],!real);
            // Each run exposes one shared whole and one shared cut. Before and
            // after are ordered spans sharing the cut point, not unrelated size
            // tags. Gap numerators share a source read across both denominators.
            var lengths=geometry.Runs.Select(run=>run.Zip(run.Skip(1),Distance).Sum()).ToArray();
            var axes=lengths.Select(length=>new Carrier()).ToArray();
            var start=axes.Select(c=>(IElement)Relation.Point(c,0)).ToArray();
            var end=axes.Select((c,i)=>(IElement)Relation.Point(c,Tick(lengths[i]))).ToArray();
            var cut=axes.Select((c,i)=>(IElement)Relation.Point(c,Tick(lengths[i]*geometry.Fractions[i]))).ToArray();
            var wholes=Enumerable.Range(0,2).Select(i=>new Relation(start[i],end[i])).ToArray();
            var gapAxis=new Carrier();var gap=new Relation(Relation.Point(gapAxis,0),Relation.Point(gapAxis,Tick(geometry.Metrics[4]*lengths[0])));
            var definitions=new List<IElement>{new Relation(wholes[0],new Relation(start[0],cut[0])),new Relation(wholes[0],new Relation(cut[0],end[0])),
                new Relation(wholes[1],new Relation(start[1],cut[1])),new Relation(wholes[1],new Relation(cut[1],end[1])),new Relation(gap,wholes[0]),new Relation(gap,wholes[1])};
            var angleAxis=new Carrier();var fullTurn=Relation.Segment(angleAxis,0,360*Q);
            definitions.Add(new Relation(selected,fullTurn));
            for(var f=7;f<FeatureNames.Length;f++)
            {
                var run=f is 7 or 9 or 11 or 13?0:1;
                var actualRun=run==0?((Relation)selected).Left:((Relation)selected).Right;
                IElement definition;
                if(f==15)definition=new Relation(wholes[lengths[0]<=lengths[1]?0:1],wholes[lengths[0]<=lengths[1]?1:0]);
                else if(f is 9 or 10)definition=G(actualRun,fullTurn,Relation.Segment(angleAxis,0,Tick(geometry.Metrics[f])));
                else if(f is 11 or 12)
                {
                    var turns=geometry.Metrics[9+run];
                    var denominator=Math.Max(1,turns); // declared finite observer floor, not an exact 0/0 quotient
                    definition=G(actualRun,Relation.Segment(angleAxis,0,Tick(turns)),Relation.Segment(angleAxis,0,Q),
                        new Relation(Relation.Segment(angleAxis,0,Tick(denominator*geometry.Metrics[f])),Relation.Segment(angleAxis,0,Tick(denominator))));
                }
                else definition=G(actualRun,new Relation(Relation.Segment(axes[run],0,Tick(lengths[run]*geometry.Metrics[f])),wholes[run]));
                definitions.Add(definition);
            }
            var reads=geometry.Metrics.Select((value,i)=>(IElement)BandWitness(selected,i,value,definitions[i])).ToArray();
            var machines=Enumerable.Range(0,Names.Length).Select(Construction).ToArray();
            // Expected topologies, orientation and source bindings remain connected
            // even when every diagnostic name and label is removed.
            var parameter=new Carrier();var hint=G(Relation.Segment(parameter,0,Q),
                new Relation(selected,G(new CarrierLocation(parameter,Tick(geometry.Parameters[0])),new CarrierLocation(parameter,Tick(geometry.Parameters[1])))),
                G(geometry.Orientation.Select(v=>(IElement)new CarrierLocation(parameter,Tick(v))).ToArray()));
            var candidates=machines.Select(m=>(IElement)ReadWitness.Create(new Carrier(),selected,hint,m.Surface,new Placeholder(),G(reads))).ToArray();
            IElement expected=new Placeholder();int? label=entry.TryGetProperty("label",out var l)&&l.ValueKind==JsonValueKind.Number?l.GetInt32():null;
            if(label is not null)expected=machines[label.Value].Surface;
            patches.Add(G(selected,G(reads),G(candidates),expected,hint));
            outputs.Add(new(entry.GetProperty("id").GetString()!,reads.Select(ReadBand).ToArray(),geometry.Runs,geometry.Encounter,geometry.Parameters,geometry.Orientation,
                label,entry.GetProperty("labelBasis").GetString()!,entry.GetProperty("group").GetString()!,request.GetProperty("sourceKey").GetString()!,profiles,archive,Names));
        }
        var unresolved=new List<IElement>();
        if(real&&request.TryGetProperty("unresolvedProfiles",out var unbound))
            foreach(var i in unbound.EnumerateArray().Select(v=>v.GetInt32()))
                unresolved.Add(ReadWitness.Create(new Carrier(),annotation!.Profiles[i].Witness,Relation.Segment(new Carrier(),0,0),new Placeholder(),annotation.Profiles[i].Witness,source));
        return (G(source,G(patches.ToArray()),G(unresolved.ToArray())),outputs);
    }
    public static int Validate(Relation root)
    {
        if(!root.TryReadOrderedGroup(out var fields)||fields.Length!=3||fields[1] is not Relation patches||!patches.TryReadOrderedGroup(out var rows)||fields[2] is not Relation remainder||!remainder.TryReadOrderedGroup(out _))throw new InvalidDataException("Incomplete patch root.");
        HashSet<Relation>? sourceProfiles=null;
        if(fields[0] is Relation actual)
        {
            if(!QuickDrawAnnotationEvidence.TryRead(actual,out var source))throw new InvalidDataException("Patch source annotation no longer reads.");
            sourceProfiles=source.Profiles.Select(p=>p.Witness).ToHashSet<Relation>(ReferenceEqualityComparer.Instance);
        }
        foreach(var row in rows)
        {
            if(row is not Relation r||!r.TryReadOrderedGroup(out var p)||p.Length!=5||p[1] is not Relation reads||!reads.TryReadOrderedGroup(out var fs)||fs.Length!=FeatureNames.Length||
                p[2] is not Relation choices||!choices.TryReadOrderedGroup(out var c)||c.Length!=Names.Length)throw new InvalidDataException("Incomplete patch source, features or candidates.");
            if(sourceProfiles is not null&&(p[0] is not Relation pair||pair.Left is not Relation a||pair.Right is not Relation b||!sourceProfiles.Contains(a)||!sourceProfiles.Contains(b)))
                throw new InvalidDataException("Patch input no longer selects its actual source profiles.");
            foreach(var f in fs)_=ReadBand(f);
            for(var i=0;i<c.Length;i++)
            {
                if(c[i] is not Relation w||!ReadWitness.TryRead(w,out var selected,out _,out var machine,out _,out _)||!ReferenceEquals(selected,p[0])||Kind(ReadMachine(machine))!=i)
                    throw new InvalidDataException("Candidate lost its selected source or connected topology.");
            }
        }
        return rows.Length;
    }
    public static int[][] Vectors(Relation root)
    {
        Validate(root);root.TryReadOrderedGroup(out var fields);((Relation)fields[1]).TryReadOrderedGroup(out var patches);
        return patches.Select(p=>{((Relation)p).TryReadOrderedGroup(out var f);((Relation)f[1]).TryReadOrderedGroup(out var values);return values.Select(ReadBand).ToArray();}).ToArray();
    }
    public static Relation Scores(Relation source,JsonElement request)
    {
        Validate(source);source.TryReadOrderedGroup(out var fields);((Relation)fields[1]).TryReadOrderedGroup(out var patches);
        var predictions=request.GetProperty("predictions").EnumerateArray().ToArray();
        if(predictions.Length!=patches.Length)throw new InvalidDataException("Model outputs do not cover the selected patches.");
        var contract=EncodedTextSurface.Create(new Carrier(),request.GetProperty("modelSha256").GetString()!);
        var opinions=new List<IElement>();
        for(var i=0;i<patches.Length;i++)
        {
            ((Relation)patches[i]).TryReadOrderedGroup(out var patch);((Relation)patch[2]).TryReadOrderedGroup(out var candidates);
            var values=predictions[i].GetProperty("scores").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
            if(values.Length!=candidates.Length||values.Any(v=>!double.IsFinite(v)||v<0||v>1)||Math.Abs(values.Sum()-1)>1e-6)throw new InvalidDataException("Invalid finite external model score read.");
            var score=new Carrier();var basis=Relation.Segment(score,0,Q);
            var reads=values.Select((v,k)=>(IElement)ReadWitness.Create(new Carrier(),new Relation(patch[1],candidates[k]),basis,
                new CarrierLocation(score,Tick(v)),new Placeholder(),contract)).ToArray();
            // All alternatives remain. An output distribution is observation,
            // not conserved transfer, shared source contact or a resolved choice.
            opinions.Add(ReadWitness.Create(new Carrier(),patches[i],contract,G(reads),new Placeholder(),patch[0]));
        }
        return G(source,contract,G(opinions.ToArray()));
    }
    public static int ValidateScores(Relation root)
    {
        if(!root.TryReadOrderedGroup(out var f)||f.Length!=3||f[0] is not Relation source||f[2] is not Relation reads||!reads.TryReadOrderedGroup(out var opinions))throw new InvalidDataException("Incomplete learned opinion graph.");
        if(Validate(source)!=opinions.Length)throw new InvalidDataException("Learned opinions lost source coverage.");
        source.TryReadOrderedGroup(out var sourceFields);((Relation)sourceFields[1]).TryReadOrderedGroup(out var patches);
        for(var i=0;i<opinions.Length;i++)
        {
            if(opinions[i] is not Relation o||!ReadWitness.TryRead(o,out var selected,out var model,out var scores,out _,out _)||!ReferenceEquals(selected,patches[i])||!ReferenceEquals(model,f[1])||scores is not Relation scoreRows||!scoreRows.TryReadOrderedGroup(out var s)||s.Length!=Names.Length)
                throw new InvalidDataException("Learned scores lost the external model contract.");
            ((Relation)patches[i]).TryReadOrderedGroup(out var patch);((Relation)patch[2]).TryReadOrderedGroup(out var candidates);
            for(var j=0;j<s.Length;j++)
                if(s[j] is not Relation score||!ReadWitness.TryRead(score,out var inputs,out var basis,out var result,out _,out var origin)||
                    inputs is not Relation pair||!ReferenceEquals(pair.Left,patch[1])||!ReferenceEquals(pair.Right,candidates[j])||!ReferenceEquals(origin,f[1])||
                    basis is not Relation {Left:Relation{Left:CarrierLocation from,Right:Carrier carrier},Right:Relation{Left:CarrierLocation to,Right:Carrier target}}||
                    !ReferenceEquals(carrier,target)||!ReferenceEquals(from.Carrier,carrier)||!ReferenceEquals(to.Carrier,carrier)||from.Ticks!=0||to.Ticks!=Q||
                    result is not CarrierLocation value||!ReferenceEquals(value.Carrier,carrier)||value.Ticks<0||value.Ticks>Q)
                    throw new InvalidDataException("Learned score lost its feature/candidate binding or finite score basis.");
        }
        return opinions.Length;
    }
    public static string Capture(string key,Relation root)=>GojoStructureCompactJson.Serialize(GojoStructureCompactJson.FromElement(key,root));
    public static Relation Restore(string json)
    {
        var document=GojoStructureCompactJson.Deserialize(json);
        if(!GojoStructureCompactJson.TryToElement(document,out var restored)||restored.Root is not Relation root)throw new InvalidDataException("Patch graph could not be independently restored.");
        Validate(root);return root;
    }
}
