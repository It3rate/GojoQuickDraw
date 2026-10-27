using System.Text.Json;
using Gojo.Core;
using Gojo.Experiments.QuickDraw;
using GojoQuickDraw.Reads;

namespace GojoQuickDraw.Native;

/// <summary>Read existing native joint witnesses, selected final edges and
/// before/after site correspondences. No extra source or semantic graph is
/// imported. The JSON result is a reproducible observation cache.</summary>
public static class JointReadback
{
    private const double Q=QuickDrawAnnotationEvidence.CoordinateQuantum;
    private static double[] XY(Relation site)
    {
        var position=(Relation)site.Right;
        return [((CarrierLocation)position.Left).Ticks/Q,((CarrierLocation)position.Right).Ticks/Q];
    }
    public static JointComparison Read(NativeRecord.Read record)
    {
        var annotation=record.Annotation;
        var source=(annotation?.Joints ?? []).Select((j,i)=> {
            var positions=j.Kind=="O candidate" ? j.Profiles.SelectMany(p=>p.Curve.Controls).ToArray() : j.Participants.Select(p=>p.Position).ToArray();
            return new JointNode("source-j"+i,JointGraphRead.Normalize(j.Kind),
                positions.Length>0 ? [positions.Average(p=>p.X/Q),positions.Average(p=>p.Y/Q)] : [0,0],
                j.Rays.Select(r=>(Math.Atan2(r.Direction.Y,r.Direction.X)*180/Math.PI+360)%360).ToArray(),
                j.Participants.Select(p=>new JointAt("p"+p.Profile.Index,(double)p.At/QuickDrawAnnotationEvidence.ParameterQuantum)).ToArray(),
                j.Profiles.Select(p=>"p"+p.Index).Distinct().Order().ToArray(),j.ActualSharedSite,j.Gap/Q,
                j.ActualSharedSite ? "shared acquired sample" : "source proximity / crossing candidate; acquisition unchanged");
        }).ToArray();
        var edges=new List<JointEdge>();
        if(record.Refinement is { } refinement)
        {
            var parts=new Dictionary<Relation,ContextEvidence.Candidate>(ReferenceEqualityComparer.Instance);
            foreach(var p in record.Context.Parts)parts.Add(p.Witness,p);
            var siteIds=new Dictionary<IElement,string>(ReferenceEqualityComparer.Instance);
            string Site(IElement p){if(!siteIds.TryGetValue(p,out var id))siteIds[p]=id="site"+siteIds.Count;return id;}
            var profiles=new Dictionary<Relation,string>(ReferenceEqualityComparer.Instance);
            foreach(var p in annotation?.Profiles ?? [])profiles.Add(p.Witness,"p"+p.Index);
            using var adapter=JsonDocument.Parse(refinement.AdapterJson);
            var display=adapter.RootElement.TryGetProperty("parts",out var rows) ? rows.EnumerateArray().ToArray() : [];
            for(var pi=0;pi<refinement.Parts.Length;pi++)
            {
                var part=parts[refinement.Parts[pi]];var correspondence=refinement.Correspondences.FirstOrDefault(c=>ReferenceEquals(c.Target,part.Witness));
                // A correspondence can lead back to an entirely inferred area.
                // Selecting its wall as a parent does not supply its own ink.
                var added=correspondence is null || (parts[correspondence.Source].Profiles.Length==0 &&
                    !(record.BoundaryMapping?.Targets.Any(t=>ReferenceEquals(t.Part,correspondence.Source)&&t.Matches.Length>0) ?? false));
                var shown=Enumerable.Range(0,part.Segments.Length);
                if(pi<display.Length && display[pi].TryGetProperty("geometry",out var geometry) && geometry.TryGetProperty("displaySegments",out var selection))
                    shown=selection.EnumerateArray().Select(s=>s.GetInt32()).Where(i=>i>=0&&i<part.Segments.Length);
                foreach(var ei in shown)
                {
                    var edge=part.Segments[ei];var selectedProfiles=new HashSet<string>();
                    if(correspondence is not null && record.BoundaryMapping is { } mapping)
                    {
                        var a=correspondence.SitePairs.FirstOrDefault(p=>ReferenceEquals(p.Right,edge.Left))?.Left;
                        var b=correspondence.SitePairs.FirstOrDefault(p=>ReferenceEquals(p.Right,edge.Right))?.Left;
                        foreach(var target in mapping.Targets.Where(t=>ReferenceEquals(t.Part,correspondence.Source)))
                            if(target.Edges.Any(e=>ReferenceEquals(e.Left,a)&&ReferenceEquals(e.Right,b) || ReferenceEquals(e.Left,b)&&ReferenceEquals(e.Right,a)))
                                foreach(var match in target.Matches)selectedProfiles.Add(profiles[match.Source.Profile]);
                    }
                    edges.Add(new($"f{pi}/e{ei}","f"+pi,XY((Relation)edge.Left),XY((Relation)edge.Right),Site(edge.Left),Site(edge.Right),
                        selectedProfiles.Order().ToArray(),added));
                }
            }
        }
        return JointGraphRead.Compare(JointGraphRead.Graph(source),JointGraphRead.Final(edges.ToArray()),
            JointGraphRead.Version+"; independently restored native source / refinement surfaces; displayed-edge scope",edges.ToArray());
    }
    public static JsonElement Json(NativeRecord.Read record) => JsonSerializer.SerializeToElement(Read(record),Options);
    public static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
}
