using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using NativeProgram = Gojo.Experiments.ConstructionMachines.Program;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>Adapter from source-supported scaffolds to actual exposed machines.
/// Runtime laws are affine relations over ports; role names are review labels.</summary>
public sealed class ChurchScene
{
    public const long Scale = 4;
    public const long Offset = 512;
    public const long Capacity = 4096;
    public sealed record Part(string Id, string Role, bool Prior, NegotiatingMachine Machine,
        Relation Geometry, double[][] Normalized, int[][] Segments, double[] InitialBounds, JsonElement Seed,int OutputOffset=0);
    public sealed record Rule(string Name, string Layer, EvidenceObligation Error);
    public readonly ConstructionRecipes Space = new();
    public readonly List<Part> Parts = [];
    public readonly List<Rule> Rules = [];
    public readonly List<NegotiatingMachine> Machines = [];
    public readonly Dictionary<Relation, NegotiatingMachine> Sites = new(ReferenceEqualityComparer.Instance);
    public readonly Dictionary<Relation, Relation> Correspondence = new(ReferenceEqualityComparer.Instance);
    public readonly List<Relation> Imports=[];
    public object[] DerivedParts {get;private set;}=[];
    public object[]? CaptureViews {get;set;}
    private readonly Dictionary<Relation,double[]> initialSiteSamples=new(ReferenceEqualityComparer.Instance);
    public readonly Graph Source;
    private readonly bool moveInk;
    public Graph Graph { get; private set; }
    public Relation Root => G(Source.Surface, Graph.Surface, G(Machines.Select(m => (IElement)m.Surface).ToArray()),
        G(Parts.Select(p => (IElement)(p.Machine.Ports.Count>8?
            G(p.Machine.Surface,p.Geometry,G(Left(p),Top(p),Right(p),Bottom(p))):G(p.Machine.Surface, p.Geometry))).ToArray()),
        G(Rules.Select(r => (IElement)r.Error.Surface).ToArray()),
        G(Correspondence.Select(p => (IElement)new Relation(p.Key, p.Value)).ToArray()),
        G(G(ProfileIds.Values.ToArray()),G(Imports.Cast<IElement>().ToArray())));

    public ChurchScene(Graph source, Graph current, JsonElement layout, bool feedback, bool shapes,
        IReadOnlyDictionary<string,Part>? previousParts = null, bool moveInk = true, JsonElement? hypotheses = null,
        ChurchCaptureController? capture=null,int pass=0)
    {
        this.moveInk=moveInk;
        Source = source; Graph = current;
        foreach (var site in current.Sites)
        {
            var xy = XY(site); var machine = Point(xy);
            Sites.Add(site, machine); Machines.Add(machine);
            var imported=NativeProgram.XY(machine.Point(0));initialSiteSamples.Add(site,imported);
            var maps=G(F(Graph.Frame).Select((span,k)=>(IElement)new Relation(span,Relation.Segment(k==0?Space.X:Space.Y,Offset,Offset+Scale))).ToArray());
            var import=ReadWitness.Create(new Carrier(),site,maps,
                machine.CurrentPublication,G(xy.Select((v,k)=>(IElement)ProjectionResidue.Create(k==0?Space.X:Space.Y,
                    checked((long)Math.Round(v*Scale*PatchEvidence.Q)) + Offset*PatchEvidence.Q - checked((long)imported[k])*PatchEvidence.Q,PatchEvidence.Q)).ToArray()),site);
            Correspondence.Add(site, G(site, machine.Port(0, 0), machine.Port(0, 1),import));
            // A source anchor is a tolerance interval around the current source
            // correspondence, not a demand to copy the hand-drawn coordinate.
            var anchors = current.Edges.SelectMany(e => new[]{(Site:e.A,T:0),(Site:e.B,T:1)}.Where(p=>ReferenceEquals(p.Site,site)).Select(p=>
            {
                var original=source.Edges.FirstOrDefault(s=>ReferenceEquals(s.Source,e.Source));
                if(original is null)return (double[]?)null;
                e.Span.TryReadSegment(out _,out var start,out var end);
                var t=(p.T==0?start:end).Ticks/(double)PatchEvidence.Q;
                var row=original.Points.Select(v=>v.ToArray()).ToArray();
                while(row.Length>1)row=row.Zip(row.Skip(1),(a,b)=>new[]{a[0]*(1-t)+b[0]*t,a[1]*(1-t)+b[1]*t}).ToArray();
                return row[0];
            })).Where(p=>p is not null).Cast<double[]>().ToArray();
            var anchor=anchors.Length==0?xy:new[]{anchors.Average(p=>p[0]),anchors.Average(p=>p[1])};
            for (var k = 0; k < 2; k++) Add("Ink displacement allowance", "source", Coordinate(anchor[k]) - 24, Coordinate(anchor[k]) + 24,
                2, site, (machine.Port(0, k), 1));
        }
        if (!shapes) return;
        foreach (var item in layout.GetProperty("parts").EnumerateArray())
        {
            var bounds = item.GetProperty("bounds").EnumerateArray().Select(x => x.GetDouble()).ToArray();
            if (bounds[2] - bounds[0] < 1 || bounds[3] - bounds[1] < 1) continue; // reference levels remain explicit open hypotheses in input
            var role = item.GetProperty("role").GetString()!;
            var geometry = item.GetProperty("geometry");
            var points = geometry.GetProperty("points").Deserialize<double[][]>()!;
            var lines = geometry.GetProperty("segments").Deserialize<int[][]>()!;
            var normalized = points.Select(p => new[] { (p[0]-bounds[0])/(bounds[2]-bounds[0]), (p[1]-bounds[1])/(bounds[3]-bounds[1]) }).ToArray();
            var previous=previousParts is not null&&previousParts.TryGetValue(item.GetProperty("id").GetString()!,out var retained)?retained:null;
            var live=previous is null?bounds:Bounds(previous);
            var x = NativeProgram.Parameter(Coordinate(live[0])); var y = NativeProgram.Parameter(Coordinate(live[1]));
            var width = NativeProgram.Parameter(Math.Max(4, Tick(live[2]-live[0])));
            var height = NativeProgram.Parameter(Math.Max(4, Tick(live[3]-live[1])));
            var origin = Space.Plus(Space.Times(Space.Point(1, 0), x.ValueSlot, x), Space.Times(Space.Point(0, 1), y.ValueSlot, y));
            var machine = new NegotiatingMachine(Space.Offsets(origin, width, height));
            machine.AddResponse(machine.Port(0, 0), x, responseD: 4, max: Capacity, maxStep: feedback ? 8 : 0);
            machine.AddResponse(machine.Port(0, 1), y, responseD: 4, max: Capacity, maxStep: feedback ? 8 : 0);
            machine.AddResponse(machine.Port(1, 0), width, responseD: 4, min: hypotheses.HasValue?4:Math.Max(4, (long)width.Value / 2), max: Capacity, maxStep: feedback ? 8 : 0);
            machine.AddResponse(machine.Port(3, 1), height, responseD: 4, min: hypotheses.HasValue?4:Math.Max(4, (long)height.Value / 2), max: Capacity, maxStep: feedback ? 8 : 0);
            // The rendered shape is an executable native affine construction.
            // All points consume the same four mutable parameters. It is not a
            // cached SVG or a semantic label which the executor must understand.
            var outputs = normalized.Select(p => (IElement)Space.Plus(origin, Space.Plus(
                Space.Times(Space.Times(Space.Point(1, 0), width.ValueSlot, width), Quant(p[0]), 1024),
                Space.Times(Space.Times(Space.Point(0, 1), height.ValueSlot, height), Quant(p[1]), 1024)))).ToArray();
            var construction = TransformSurface.Create(new Carrier(), G(origin, width, height), G(outputs),
                G(outputs.Select(_ => (IElement)new Placeholder()).ToArray()), G(), G(Space.Basis, machine.Surface));
            var prior = item.GetProperty("status").GetString() == "prior-only";
            var part = new Part(item.GetProperty("id").GetString()!, role, prior, machine, construction, normalized, lines, bounds, item.Clone());
            Parts.Add(part); Machines.Add(machine);
            if(hypotheses.HasValue)
            {
                // The proposed boundary is actual ordered native topology over
                // this construction's output roles, not only review indices.
                var boundary=G(lines.Select(e=>(IElement)new Relation(outputs[e[0]],outputs[e[1]])).ToArray());
                Imports.Add(ReadWitness.Create(new Carrier(),construction,G(outputs),boundary,G(),Source.Surface));
            }
            if(item.TryGetProperty("sourceGeometry",out var oldGeometry)&&oldGeometry.ValueKind==JsonValueKind.Object)
            {
                // Family replacement is an explicit construction hypothesis,
                // not a movement of acquired points or a new observation.
                var oldBounds=item.GetProperty("sourceBounds").Deserialize<double[]>()!;
                var oldPoints=oldGeometry.GetProperty("points").Deserialize<double[][]>()!;
                var oldOutputs=oldPoints.Select(p=>(IElement)Space.Plus(origin,Space.Plus(
                    Space.Times(Space.Times(Space.Point(1,0),width.ValueSlot,width),Quant((p[0]-oldBounds[0])/(oldBounds[2]-oldBounds[0])),1024),
                    Space.Times(Space.Times(Space.Point(0,1),height.ValueSlot,height),Quant((p[1]-oldBounds[1])/(oldBounds[3]-oldBounds[1])),1024)))).ToArray();
                var priorConstruction=TransformSurface.Create(new Carrier(),G(origin,width,height),G(oldOutputs),G(oldOutputs.Select(_=>(IElement)new Placeholder()).ToArray()),G(),G(Space.Basis));
                Imports.Add(ReadWitness.Create(new Carrier(),priorConstruction,G(Source.Surface,machine.Surface),construction,G(),G(priorConstruction,construction)));
            }
            if(previous is not null)
            {
                var oldPorts=new[]{Left(previous),Top(previous),Right(previous),Bottom(previous)};
                var newPorts=new[]{Left(part),Top(part),Right(part),Bottom(part)};
                var maps=oldPorts.Zip(newPorts,(a,b)=>
                {
                    ((Relation)F(a)[1]).TryReadSegment(out var sourceAxis,out _,out _);
                    ((Relation)F(b)[1]).TryReadSegment(out var targetAxis,out _,out _);
                    return (IElement)G(new Relation(a,b),new Relation(Relation.Segment(sourceAxis,0,1),Relation.Segment(targetAxis,0,1)));
                }).ToArray();
                // New finite worlds import prior PUBLIC bounds through retained
                // calibrations. Equal numbers alone are not a continuity claim.
                Imports.Add(ReadWitness.Create(new Carrier(),G(oldPorts.Select(p=>(IElement)previous.Machine.Publish(p)).ToArray()),G(maps),
                    G(newPorts.Select(p=>(IElement)machine.Publish(p)).ToArray()),G(),G(previous.Geometry,construction)));
            }
            var support = G(machine.Definition, Source.Surface);
            foreach (var (port, value) in new[] { (Left(part), bounds[0]), (Top(part), bounds[1]), (Right(part), bounds[2]), (Bottom(part), bounds[3]) })
                Add("Scaffold seed allowance", "shape", Coordinate(value)-48, Coordinate(value)+48, 1, support, (port, 1));
            Add("Positive horizontal extent", "shape", hypotheses.HasValue?4:Math.Max(4, Tick(bounds[2]-bounds[0])/2), Capacity, 2, support, (Right(part), 1), (Left(part), -1));
            Add("Positive vertical extent", "shape", hypotheses.HasValue?4:Math.Max(4, Tick(bounds[3]-bounds[1])/2), Capacity, 2, support, (Bottom(part), 1), (Top(part), -1));
        }
        if(hypotheses is {} plan)
        {
            if(capture is not null&&feedback)capture.Begin(this,plan,pass);
            ChurchHypotheses.Link(this,plan);
        }
        else LinkParts();
    }

    public static long Tick(double value) => checked((long)Math.Round(value * Scale));
    public static long Coordinate(double value) => checked(Tick(value)+Offset);
    private static long Quant(double value) => checked((long)Math.Round(value * 1024));
    public static Relation Left(Part p) => p.Machine.Port(p.OutputOffset, 0);
    public static Relation Right(Part p) => p.Machine.Port(p.OutputOffset+1, 0);
    public static Relation Top(Part p) => p.Machine.Port(p.OutputOffset, 1);
    public static Relation Bottom(Part p) => p.Machine.Port(p.OutputOffset+3, 1);
    public void ReplaceParts(IReadOnlyList<Part> next,NegotiatingMachine host,Dictionary<Relation,Relation> ports,Relation witness)
    {
        var retired=Parts.Select(p=>p.Machine).ToHashSet(ReferenceEqualityComparer.Instance);
        Machines.RemoveAll(m=>retired.Contains(m));Machines.Add(host);Parts.Clear();Parts.AddRange(next);Imports.Add(witness);
        for(var i=0;i<Rules.Count;i++)
        {
            var rule=Rules[i];var fields=F(rule.Error.Surface);
            var terms=rule.Error.Terms.Select(t=>{var f=F(t);return (IElement)G(ports.GetValueOrDefault((Relation)f[0],(Relation)f[0]),f[1],f[2]);}).ToArray();
            Rules[i]=rule with {Error=EvidenceObligation.Restore(G(G(fields[0],witness),G(terms),fields[2],fields[3],fields[4],fields[5]))};
        }
    }
    public static double Read(IElement sample)
    {
        if (!ProjectionResidue.TryRead(sample, out _, out var n, out var d)) throw new InvalidDataException("An unresolved coordinate remains.");
        return (double)n / (double)d;
    }
    public double[] Bounds(Part part) => new[] { Left(part), Top(part), Right(part), Bottom(part) }
        .Select(p => (Read(F(part.Machine.Publish(p))[2])-Offset) / Scale).ToArray();
    public double[][] Points(Part p)
    {
        var result = MachineRunner.Run(p.Geometry);
        return F(F(result)[2]).Select(v => NativeProgram.XY(v).Select(x => (x-Offset) / Scale).ToArray()).ToArray();
    }
    private NegotiatingMachine Point(double[] xy)
    {
        var x = NativeProgram.Parameter(Coordinate(xy[0]));
        var y = NativeProgram.Parameter(Coordinate(xy[1]));
        var output = Space.Plus(Space.Times(Space.Point(1, 0), x.ValueSlot, x), Space.Times(Space.Point(0, 1), y.ValueSlot, y));
        var definition = TransformSurface.Create(new Carrier(), G(x, y), G(output), G(new Placeholder()), G(), G(Space.Basis));
        var machine = new NegotiatingMachine(definition);
        machine.AddResponse(machine.Port(0, 0), x, responseD: 4, max: Capacity, maxStep: moveInk?8:0);
        machine.AddResponse(machine.Port(0, 1), y, responseD: 4, max: Capacity, maxStep: moveInk?8:0);
        return machine;
    }
    public void Add(string name, string layer, long lower, long upper, long influence, IElement support, params (Relation Port, long Coefficient)[] inputs)
    {
        // Pure authored setup. The retained native interval, affine terms,
        // response shares and influence own subsequent calculation.
        var combined = inputs.GroupBy(x => x.Port, (IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance)
            .Select(g => (g.Key, g.Sum(x => x.Coefficient))).Where(x => x.Item2 != 0).ToArray();
        if (combined.Length > 0) Rules.Add(new(name, layer, new EvidenceObligation(combined, lower, upper, [2, 2, 2], influence, 4, support)));
    }
    public void AddCalibrated(string name,string layer,long lower,long upper,long influence,IElement support,long featureScale,
        params (Relation Port,long Coefficient)[] inputs)
    {
        if(featureScale is <1 or >4096)throw new ArgumentOutOfRangeException(nameof(featureScale));
        var count=Rules.Count;Add(name,layer,lower,upper,influence,support,inputs);
        if(Rules.Count==count||featureScale==1)return;
        var rule=Rules[^1];var error=rule.Error;var fields=F(error.Surface);
        var calibrated=error.Rules.Select(r=>
        {
            var f=F(r);((Relation)f[1]).TryReadSegment(out var axis,out var lo,out var hi);
            // A finer feature unit must scale its response clipping intervals
            // as well as its coefficient maps. Passage/actuator limits remain
            // in their own bases and are not enlarged here.
            return (IElement)G(f[0],Relation.Segment(axis,checked(lo.Ticks*featureScale),checked(hi.Ticks*featureScale)),f[2]);
        }).ToArray();
        Rules[^1]=rule with {Error=EvidenceObligation.Restore(G(fields[0],fields[1],fields[2],fields[3],fields[4],G(calibrated)))};
    }
    private (Relation Port, long Coefficient)[] At(Part p, double[] uv, int axis, long coefficient = 1)
    {
        var u = Math.Clamp((long)Math.Round(uv[axis]*16), 0, 16);
        return axis == 0 ? [(Left(p), coefficient*(16-u)), (Right(p), coefficient*u)] : [(Top(p), coefficient*(16-u)), (Bottom(p), coefficient*u)];
    }
    private void LinkParts()
    {
        foreach (var p in Parts)
        {
            if (p.Seed.GetProperty("geometry").TryGetProperty("siteSources", out var sites))
                foreach (var (site, i) in sites.EnumerateArray().Select((s,i) => (s,i)))
                {
                    if (site.ValueKind != JsonValueKind.Object || !site.TryGetProperty("part", out var id)) continue;
                    var other = Parts.Find(q => q.Id == id.GetString()); var j = site.GetProperty("index").GetInt32();
                    if (other is null || j >= other.Normalized.Length || i >= p.Normalized.Length) continue;
                    for (var axis=0;axis<2;axis++) Add("Declared attachment", "attachment", -64, 64, 3,
                        G(p.Geometry, other.Geometry), At(p,p.Normalized[i],axis).Concat(At(other,other.Normalized[j],axis,-1)).ToArray());
                }
            if (p.Role is not ("window" or "door" or "tower")) continue;
            var wall = Parts.FirstOrDefault(q => q.Role == "building"); if (wall is null || ReferenceEquals(wall,p)) continue;
            var margin = p.Role == "window" ? 12 : 0;
            Add("Inside support on left", "containment", margin, Capacity, 3, G(p.Geometry,wall.Geometry), (Left(p),1),(Left(wall),-1));
            Add("Inside support on right", "containment", margin, Capacity, 3, G(p.Geometry,wall.Geometry), (Right(wall),1),(Right(p),-1));
            if (p.Role is "door" or "window")
            {
                Add("Opening above base", "containment", 0, Capacity, 3, G(p.Geometry,wall.Geometry), (Bottom(wall),1),(Bottom(p),-1));
                Add("Opening below upper boundary", "containment", margin, Capacity, 3, G(p.Geometry,wall.Geometry), (Top(p),1),(Top(wall),-1));
            }
        }
        // Source-selected similar windows can share a reflection expectation;
        // this is a provisional relation, not proof of object membership.
        var body=Parts.FirstOrDefault(p=>p.Role=="building");
        var windows=Parts.Where(p=>p.Role=="window"&&!p.Prior).OrderBy(p=>p.InitialBounds[0]).ToArray();
        if(body is not null && windows.Length==2)
        {
            var a=windows[0];var b=windows[1];
            Add("Window pair reflection", "repetition", -32,32,2,G(a.Geometry,b.Geometry,body.Geometry),
                (Left(a),1),(Right(a),1),(Left(b),1),(Right(b),1),(Left(body),-2),(Right(body),-2));
            Add("Window pair upper level", "repetition", -16,16,2,G(a.Geometry,b.Geometry),(Top(a),1),(Top(b),-1));
        }
    }
    private void MapInk(JsonElement mapping)
    {
        if (!mapping.TryGetProperty("targets", out var boundaries)) return;
        var used = new HashSet<string>();
        foreach (var boundary in boundaries.EnumerateArray())
        {
            var part=Parts.Find(p=>p.Id==boundary.GetProperty("part").GetString());
            if(part is null||part.Prior)continue;
            var indices=boundary.GetProperty("segments").Deserialize<int[]>()!;
            var segments=indices.Select(i=>part.Segments[i]).ToArray();
            var original=part.Seed.GetProperty("geometry").GetProperty("points").Deserialize<double[][]>()!;
            var lengths=segments.Select(e=>PatchGeometry.Distance(original[e[0]],original[e[1]])).ToArray();var total=lengths.Sum();
            if(total<=.001)continue;
            foreach(var ink in boundary.GetProperty("ink").EnumerateArray())
            {
                var span=ink.GetProperty("span").GetString();
                // Source profile IDs are bound explicitly by the caller; no
                // equality of coordinates is used to infer this correspondence.
                if(!ProfileIds.TryGetValue(span!,out var source))throw new InvalidDataException("A boundary refers to an unknown source profile.");
                var from=ink.GetProperty("from").GetDouble();var to=ink.GetProperty("to").GetDouble();
                var tf=ink.GetProperty("targetFrom").GetDouble();var tt=ink.GetProperty("targetTo").GetDouble();
                foreach(var e in Graph.Edges.Where(e=>ReferenceEquals(e.Source,source)))
                {
                    e.Span.TryReadSegment(out _,out var pa,out var pb);
                    foreach(var (site,t) in new[]{(e.A,pa.Ticks/(double)PatchEvidence.Q),(e.B,pb.Ticks/(double)PatchEvidence.Q)})
                    {
                        if(t<from-.03||t>to+.03||!used.Add($"{Graph.Sites.ToList().FindIndex(p=>ReferenceEquals(p,site))}/{boundary.GetProperty("id").GetString()}"))continue;
                        var u=Math.Clamp(tf+(t-from)/Math.Max(.001,to-from)*(tt-tf),0,1);var distance=u*total;var j=0;
                        while(j<lengths.Length-1&&distance>lengths[j])distance-=lengths[j++];
                        var q=Math.Clamp(distance/Math.Max(.001,lengths[j]),0,1);var edge=segments[j];
                        var uv=new[]{0,1}.Select(k=>part.Normalized[edge[0]][k]*(1-q)+part.Normalized[edge[1]][k]*q).ToArray();
                        for(var axis=0;axis<2;axis++)Add("Ink / scaffold correspondence", "mapping", -256,256,2,
                            G(e.Witness,part.Geometry,e.Span), new[]{(Sites[site].Port(0,axis),16L)}.Concat(At(part,uv,axis,-1)).ToArray());
                    }
                }
            }
        }
    }
    // Populated from exact ordered profile witnesses before MapInk runs.
    public Dictionary<string,IElement> ProfileIds { get; } = new();
    public void BindProfiles(IReadOnlyDictionary<string,IElement> bindings)
    {
        // Native annotation profiles can include degenerate taps which the
        // active graph omits. Binding through a filtered edge ordinal would
        // shift every later association. Only actual imported witnesses bind.
        ProfileIds.Clear();foreach(var (id,source) in bindings)ProfileIds.Add(id,source);
        if(Source.Edges.Any(e=>!ProfileIds.Values.Any(s=>ReferenceEquals(s,e.Source))))
            throw new InvalidDataException("An active source edge has no imported profile binding.");
    }
    public void AddMappings(JsonElement mapping) => MapInk(mapping);
    public void PrepareDerived(JsonElement plan)
    {
        var result=ChurchPartitions.Build(this,plan);DerivedParts=result.Views;Imports.Add(result.Surface);
    }
    public void AddLineRules(JsonElement bundle)
    {
        foreach(var edge in Graph.Edges)
        {
            if(edge.Points.Length>2)
            {
                var read=NeighborhoodFeatures.Line(edge);var opinion=NeighborhoodFeatures.Predict(bundle.GetProperty("line"),read.Bands);
                if(opinion.Probability<.68||opinion.Unseen>3)continue;
            }
            var a=XY(edge.A);var b=XY(edge.B);var dx=Math.Abs(b[0]-a[0]);var dy=Math.Abs(b[1]-a[1]);
            var axis=dy<.18*dx?1:dx<.18*dy?0:-1;if(axis<0)continue;
            Add("Near-axis continuity", "line", -4,4,1,edge.Witness,(Sites[edge.A].Port(0,axis),1),(Sites[edge.B].Port(0,axis),-1));
        }
    }
    public Graph PublishGraph(IElement occasion)
    {
        var replacements=new Dictionary<Relation,Relation>(ReferenceEqualityComparer.Instance);
        foreach(var (old,machine) in Sites)
        {
            var live=NativeProgram.XY(machine.Point(0));var original=XY(old);var start=initialSiteSamples[old];
            // Publication applies only accepted displacement to the original
            // site. Reading at quarter-pixel resolution must not itself move it.
            var xy=new[]{0,1}.Select(k=>original[k]+(live[k]-start[k])/Scale).ToArray();
            replacements[old]=PatchGeometry.Distance(xy,XY(old))<1e-7?old:Site(Graph.Frame,xy,G(old,machine.CurrentPublication,occasion));
        }
        var before=Graph;
        if(replacements.All(p=>ReferenceEquals(p.Key,p.Value)))return Graph;
        Graph=Create(before.Frame,before.Edges.Select(e=>ReferenceEquals(replacements[e.A],e.A)&&ReferenceEquals(replacements[e.B],e.B)?e:Move(before,e,replacements[e.A],replacements[e.B])));
        return Graph;
    }
}
