using System.Text.Json;
using Gojo.Core;
using Gojo.Experiments.QuickDraw;
using Gojo.Structures;
using GojoQuickDraw.Reads;
using static GojoQuickDraw.Learning.PatchEvidence;

namespace GojoQuickDraw.Learning;

/// <summary>Two non-draining reads of one acquisition. A provider replaces a
/// local opinion, never source identity or the independently retained baseline.</summary>
public static class EncodingEvidence
{
    private static IElement[] Fields(IElement value, int count)
    {
        if(value is not Relation r || !r.TryReadOrderedGroup(out var f) || f.Length!=count)
            throw new InvalidDataException("Incomplete paired encoding surface.");
        return f;
    }
    private static IElement[] Rows(IElement value)
    {
        if(value is not Relation r || !r.TryReadOrderedGroup(out var f))throw new InvalidDataException("Expected ordered rows.");
        return f;
    }
    private static double[] XY(IElement site)
    {
        var xy=(Relation)((Relation)site).Right;
        return [((CarrierLocation)xy.Left).Ticks/(double)Q,((CarrierLocation)xy.Right).Ticks/(double)Q];
    }
    private static QuickDrawAnnotationEvidence.Read? Annotation(IElement source)
        =>source is Relation r && QuickDrawAnnotationEvidence.TryRead(r,out var read) ? read : null;

    private static PatchGeometry.Read Geometry(Relation patch, QuickDrawAnnotationEvidence.Read? annotation)
    {
        var pair=(Relation)Fields(patch,5)[0];
        double[][] Run(IElement source)
        {
            if(annotation is not null)
                return annotation.Profiles.Single(p=>ReferenceEquals(p.Witness,source)).Curve.Controls
                    .Select(p=>new[]{p.X/(double)QuickDrawAnnotationEvidence.CoordinateQuantum,p.Y/(double)QuickDrawAnnotationEvidence.CoordinateQuantum}).ToArray();
            return Rows(Fields(source,3)[1]).Select(XY).ToArray();
        }
        return PatchGeometry.ReadPair(Run(pair.Left),Run(pair.Right),annotation is null);
    }

    private static Relation Placement(IElement candidate, IElement selected, IElement hint, PatchGeometry.Read geometry, int kind)
    {
        // A frame is a conditional placement estimate, distinct from topology.
        // T uses the through-run for its bar, and the surviving end for its stem.
        var a=geometry.Runs[0];var b=geometry.Runs[1];var fractions=geometry.Fractions;
        var through=kind==3 && Math.Min(fractions[1],1-fractions[1])>Math.Min(fractions[0],1-fractions[0]) ? 1 : 0;
        var first=through==0 ? a : b;var second=through==0 ? b : a;
        var fraction=fractions[through];var other=fractions[1-through];
        double[] Delta(double[][] run,bool forward)=>forward ? [run[^1][0]-run[0][0],run[^1][1]-run[0][1]] : [run[0][0]-run[^1][0],run[0][1]-run[^1][1]];
        var axis=Delta(first,kind!=2 || fraction>=.5); // canonical corner's first ray points -X
        var length=Math.Sqrt(axis.Sum(v=>v*v));
        if(length<1e-7)axis=geometry.Orientation.ToArray();else axis=axis.Select(v=>v/length).ToArray();
        var stem=Delta(second,other<.5);var perpendicular=new[]{-axis[1],axis[0]};
        if(perpendicular[0]*stem[0]+perpendicular[1]*stem[1]<0)perpendicular=perpendicular.Select(v=>-v).ToArray();
        // Glyph reach is a finite display/placement hint, not a learned extent.
        var reach=Math.Clamp(Math.Min(a.Zip(a.Skip(1),PatchGeometry.Distance).Sum(),b.Zip(b.Skip(1),PatchGeometry.Distance).Sum())*.22,3,20);
        var x=new Carrier();var y=new Carrier(x.UnitShape);
        Relation Site(double px,double py)=>new(new Placeholder(),new Relation(new CarrierLocation(x,checked((long)Math.Round(px*Q))),new CarrierLocation(y,checked((long)Math.Round(py*Q)))));
        var origin=Site(geometry.Encounter[0],geometry.Encounter[1]);
        var along=Site(geometry.Encounter[0]+axis[0]*reach,geometry.Encounter[1]+axis[1]*reach);
        var across=Site(geometry.Encounter[0]+perpendicular[0]*reach,geometry.Encounter[1]+perpendicular[1]*reach);
        var frame=G(origin,new Relation(origin,along),new Relation(origin,across));
        return ReadWitness.Create(new Carrier(),new Relation(candidate,selected),hint,frame,new Placeholder(),selected);
    }

    private static Relation[] MatchingJoints(QuickDrawAnnotationEvidence.Read? annotation,Relation pair,PatchGeometry.Read geometry)
    {
        if(annotation is null)return [];
        var radius=Math.Max(1,geometry.Runs.Select(run=>run.Zip(run.Skip(1),PatchGeometry.Distance).Sum()).Min()*.1);
        return annotation.Joints.Where(j=>j.Profiles.Length==2 &&
            j.Profiles.Any(p=>ReferenceEquals(p.Witness,pair.Left)) && j.Profiles.Any(p=>ReferenceEquals(p.Witness,pair.Right)) &&
            j.Participants.Length>0 && JointGraphRead.Distance(geometry.Encounter,
                [j.Participants.Average(p=>p.Position.X/(double)QuickDrawAnnotationEvidence.CoordinateQuantum),
                 j.Participants.Average(p=>p.Position.Y/(double)QuickDrawAnnotationEvidence.CoordinateQuantum)])<=radius)
            .Select(j=>j.Witness).ToArray();
    }

    public static Relation Build(Relation source,JsonElement request)
    {
        Validate(source);var sf=Fields(source,3);var patches=Rows(sf[1]);var annotation=Annotation(sf[0]);
        var baseline=G((annotation?.Joints ?? []).Select(j=>(IElement)j.Witness).ToArray());
        var baselineRead=ReadWitness.Create(new Carrier(),sf[0],sf[0],baseline,new Placeholder(),source);
        var enabled=request.GetProperty("enabled").GetBoolean();
        if(!enabled)
            return G(source,baselineRead,ReadWitness.Create(new Carrier(),sf[0],baselineRead,baseline,new Placeholder(),baselineRead),new Placeholder());
        var scored=Scores(source,request);var scoreFields=Fields(scored,3);var opinions=Rows(scoreFields[2]);
        var predictions=request.GetProperty("predictions").EnumerateArray().ToArray();var applications=new List<IElement>();
        var covered=new HashSet<Relation>(ReferenceEqualityComparer.Instance);
        foreach(var (element,i) in patches.Select((p,i)=>(p,i)))
        {
            var p=Fields(element,5);var candidates=Rows(p[2]);var geometry=Geometry((Relation)element,annotation);
            var placements=candidates.Select((c,k)=>(IElement)Placement(c,p[0],p[4],geometry,k)).ToArray();
            var selected=new Placeholder();IElement result=selected;
            if(predictions[i].TryGetProperty("proposed",out var proposed)&&proposed.ValueKind==JsonValueKind.Number)
            {
                var k=proposed.GetInt32();if(k<0||k>=Names.Length)throw new InvalidDataException("Unknown selected construction.");
                result=candidates[k];
            }
            var replaced=MatchingJoints(annotation,(Relation)p[0],geometry);foreach(var joint in replaced)covered.Add(joint);
            var opinion=ReadWitness.Create(new Carrier(),element,opinions[i],result,G(placements),G(baselineRead,G(replaced)));
            applications.Add(opinion);
        }
        var uncovered=G((annotation?.Joints ?? []).Select(j=>j.Witness).Where(j=>!covered.Contains(j)).Cast<IElement>().ToArray());
        var selectedRead=ReadWitness.Create(new Carrier(),sf[0],scored,G(applications.ToArray()),uncovered,baselineRead);
        return G(source,baselineRead,selectedRead,scored);
    }

    private static JointNode[] SourceNodes(QuickDrawAnnotationEvidence.Read? annotation)
    {
        return (annotation?.Joints ?? []).Select((j,i)=>
        {
            var positions=j.Kind=="O candidate" ? j.Profiles.SelectMany(p=>p.Curve.Controls).ToArray() : j.Participants.Select(p=>p.Position).ToArray();
            return new JointNode("source-j"+i,JointGraphRead.Normalize(j.Kind),positions.Length>0 ?
                [positions.Average(p=>p.X/(double)QuickDrawAnnotationEvidence.CoordinateQuantum),positions.Average(p=>p.Y/(double)QuickDrawAnnotationEvidence.CoordinateQuantum)] : [0,0],
                j.Rays.Select(r=>(Math.Atan2(r.Direction.Y,r.Direction.X)*180/Math.PI+360)%360).ToArray(),
                j.Participants.Select(p=>new JointAt("p"+p.Profile.Index,p.At/(double)QuickDrawAnnotationEvidence.ParameterQuantum)).ToArray(),
                j.Profiles.Select(p=>"p"+p.Index).Distinct().Order().ToArray(),j.ActualSharedSite,j.Gap/(double)QuickDrawAnnotationEvidence.CoordinateQuantum,
                "Default source heuristic witness; acquisition unchanged.");
        }).ToArray();
    }
    public static object Read(Relation root)
    {
        var f=Fields(root,4);var source=(Relation)f[0];Validate(source);var sf=Fields(source,3);var annotation=Annotation(sf[0]);
        var baseline=SourceNodes(annotation);
        if(!ReadWitness.TryRead((Relation)f[1],out var original,out _,out var defaultResult,out _,out _) || !ReferenceEquals(original,sf[0]) ||
            !Rows(defaultResult).SequenceEqual((annotation?.Joints ?? []).Select(j=>j.Witness)))throw new InvalidDataException("Baseline lost its actual joint witnesses.");
        if(!ReadWitness.TryRead((Relation)f[2],out var sameSource,out var provider,out var applications,out var remainder,out var provenance) ||
            !ReferenceEquals(sameSource,sf[0]) || !ReferenceEquals(provenance,f[1]))throw new InvalidDataException("Selected provider lost its common source/baseline.");
        var enabled=f[3] is Relation;
        if(!enabled)
        {
            if(!ReferenceEquals(provider,f[1])||!ReferenceEquals(applications,defaultResult))throw new InvalidDataException("Unchecked provider changed the baseline read.");
            return new{enabled=false,baseline,selected=baseline,patches=Array.Empty<object>(),uncovered=baseline.Select(n=>n.Id).ToArray()};
        }
        if(!ReferenceEquals(provider,f[3]))throw new InvalidDataException("Provider alias was lost.");
        ValidateScores((Relation)f[3]);var scoreRoot=Fields(f[3],3);
        if(!ReferenceEquals(scoreRoot[0],source))throw new InvalidDataException("Scored provider observes a different source.");
        var patches=Rows(sf[1]);var opinions=Rows(scoreRoot[2]);var rows=Rows(applications);
        if(rows.Length!=patches.Length)throw new InvalidDataException("Selected provider lost patch coverage.");
        var output=new List<object>();var selectedNodes=new List<JointNode>();var covered=new HashSet<string>();
        for(var i=0;i<rows.Length;i++)
        {
            var patch=Fields(patches[i],5);var candidates=Rows(patch[2]);
            if(!ReadWitness.TryRead((Relation)rows[i],out var selected,out var opinion,out var chosen,out var placement,out var history) ||
                !ReferenceEquals(selected,patches[i])||!ReferenceEquals(opinion,opinions[i]))throw new InvalidDataException("Local opinion lost provider or source binding.");
            var correspondence=Fields(history,2);
            if(!ReferenceEquals(correspondence[0],f[1]))throw new InvalidDataException("Replacement lost its retained baseline.");
            ReadWitness.TryRead((Relation)opinions[i],out _,out _,out var scores,out _,out _);
            var values=Rows(scores).Select(s=>{ReadWitness.TryRead((Relation)s,out _,out _,out var v,out _,out _);return ((CarrierLocation)v).Ticks/(double)Q;}).ToArray();
            var ranking=Enumerable.Range(0,5).OrderByDescending(k=>values[k]).ToArray();
            int? proposed=chosen is Placeholder ? null : Array.FindIndex(candidates,c=>ReferenceEquals(c,chosen));
            if(proposed is <0 || proposed is not null && values[proposed.Value]<values[ranking[0]])throw new InvalidDataException("Selected result is not a supported candidate.");
            var geometry=Geometry((Relation)patches[i],annotation);var pair=(Relation)patch[0];
            var profileIds=annotation is null ? Array.Empty<string>() : new[]{pair.Left,pair.Right}.Select(p=>"p"+annotation.Profiles.Single(a=>ReferenceEquals(a.Witness,p)).Index).ToArray();
            // A two-run provider must not replace a complete Y/K/three-run joint.
            var replacements=Rows(correspondence[1]);
            if(!replacements.SequenceEqual(MatchingJoints(annotation,pair,geometry)))throw new InvalidDataException("Replacement no longer corresponds to its actual local source joints.");
            var matches=baseline.Where((n,k)=>annotation is not null&&replacements.Any(r=>ReferenceEquals(r,annotation.Joints[k].Witness))).ToArray();
            foreach(var node in matches)covered.Add(node.Id);
            var frames=Rows(placement);if(frames.Length!=5)throw new InvalidDataException("Missing candidate orientation frames.");
            var views=new List<object>();double[]? directions=null;double[]? center=null;
            for(var k=0;k<5;k++)
            {
                if(!ReadWitness.TryRead((Relation)frames[k],out var inputs,out var hint,out var result,out _,out var origin)||
                    inputs is not Relation links || !ReferenceEquals(links.Left,candidates[k])||!ReferenceEquals(links.Right,patch[0])||
                    !ReferenceEquals(hint,patch[4])||!ReferenceEquals(origin,patch[0]))throw new InvalidDataException("Placement lost actual candidate/source hints.");
                var frame=Fields(result,3);var along=(Relation)frame[1];var across=(Relation)frame[2];
                if(!ReferenceEquals(along.Left,frame[0])||!ReferenceEquals(across.Left,frame[0]))throw new InvalidDataException("Placement axes no longer share their origin.");
                var zero=XY(frame[0]);var x=XY(along.Right);var y=XY(across.Right);
                ReadWitness.TryRead((Relation)candidates[k],out _,out _,out var machine,out _,out _);
                var mf=Fields(machine,4);var sites=Rows(mf[1]);var edges=Rows(mf[2]);
                var points=sites.Select(s=>{var p=XY(s);return new[]{zero[0]+p[0]*(x[0]-zero[0])+p[1]*(y[0]-zero[0]),zero[1]+p[0]*(x[1]-zero[1])+p[1]*(y[1]-zero[1])};}).ToArray();
                var segments=edges.Select(e=>new[]{Array.IndexOf(sites,((Relation)e).Left),Array.IndexOf(sites,((Relation)e).Right)}).ToArray();
                var rays=segments.Where(e=>k!=0&&(e[0]==1||e[1]==1)).Select(e=>points[e[0]==1?e[1]:e[0]])
                    .Select(p=>(Math.Atan2(p[1]-zero[1],p[0]-zero[0])*180/Math.PI+360)%360).ToArray();
                views.Add(new{kind=Names[k],score=values[k],points,segments,directions=rays});
                if(k==ranking[0]){directions=rays;center=zero;}
            }
            var best=ranking[0];var kind=new[]{"separate","I","L","T","X"}[best];
            var status=proposed is null ? "unresolved" : "proposed";
            var alternatives=string.Join(" · ",ranking.Take(3).Select(k=>$"{Names[k]} {values[k]:P0}"));
            var orientation=string.Join(" / ",directions!.Select(JointGraphRead.Heading));
            selectedNodes.Add(new("model-p"+i,kind,center!,directions!,geometry.Parameters.Select((t,j)=>new JointAt(profileIds.ElementAtOrDefault(j)??"p"+j,t)).ToArray(),
                profileIds,false,geometry.Metrics[4]*geometry.Runs[0].Zip(geometry.Runs[0].Skip(1),PatchGeometry.Distance).Sum(),
                $"{status}; {alternatives}. Directions: {orientation}. Source-based orientation hint; no acquired sites merged."));
            output.Add(new{profiles=profileIds,baseline=matches.Select(n=>n.Id).ToArray(),baselineKinds=matches.Select(n=>n.Kind).Distinct().ToArray(),
                candidates=views,ranking,proposed,status,encounter=center,changed=matches.Length==0 ? best!=0 : matches.All(n=>n.Kind!=kind)});
        }
        var uncovered=baseline.Where(n=>!covered.Contains(n.Id)).ToArray();selectedNodes.AddRange(uncovered);
        var expected=(annotation?.Joints ?? []).Where((j,i)=>!covered.Contains("source-j"+i)).Select(j=>j.Witness);
        if(!Rows(remainder).SequenceEqual(expected))throw new InvalidDataException("Uncovered source joints were dropped or counted twice.");
        return new{enabled=true,baseline,selected=selectedNodes.ToArray(),patches=output,uncovered=uncovered.Select(n=>n.Id).ToArray()};
    }
}
