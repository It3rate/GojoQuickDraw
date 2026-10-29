using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>Keep the earlier construction and selected unexplained intervals
/// connected to their actual proposed replacement. This import is observation,
/// not another acquisition, source-site merge, or additional force.</summary>
public static class ChurchRecovery
{
    public static void Attach(ChurchScene scene,JsonElement plan)
    {
        if(!plan.TryGetProperty("recovery",out var recovery))return;
        using var empty=JsonDocument.Parse("""{"rules":[]}""");
        var firstLayout=JsonSerializer.SerializeToElement(new{parts=recovery.GetProperty("initialParts").EnumerateArray().ToArray()});
        // The first shape world has no working-ink actors. Its original source
        // is still the same annotation, and its bound geometry stays readable.
        var first=new ChurchScene(scene.Source,Create(scene.Source.Frame,[]),firstLayout,false,true,moveInk:false,hypotheses:empty.RootElement);
        first.BindProfiles(scene.ProfileIds);
        foreach(var decision in recovery.GetProperty("decisions").EnumerateArray())
        {
            var intervals=new List<IElement>();
            foreach(var selection in decision.GetProperty("sourceIntervals").EnumerateArray())
            {
                var source=scene.ProfileIds[selection.GetProperty("span").GetString()!];
                var edge=scene.Source.Edges.First(e=>ReferenceEquals(e.Source,source));
                edge.Span.TryReadSegment(out var axis,out _,out _);
                foreach(var range in selection.GetProperty("ranges").EnumerateArray())
                    intervals.Add(G(source,Relation.Segment(axis,checked((long)Math.Round(range[0].GetDouble()*PatchEvidence.Q)),
                        checked((long)Math.Round(range[1].GetDouble()*PatchEvidence.Q)))));
            }
            var targets=decision.GetProperty("parts").EnumerateArray()
                .Select(id=>scene.Parts.Single(p=>p.Id==id.GetString()).Geometry).Cast<IElement>().ToArray();
            // An unobserved cap base or pen-up closure remains open evidence.
            // Actual uncovered target boundaries are separately read by mapping.
            scene.Imports.Add(ReadWitness.Create(new Carrier(),G(first.Root,G(intervals.ToArray())),
                G(scene.Source.Surface,G(intervals.ToArray())),G(targets),G(new Placeholder()),
                G(first.Root,G(targets))));
        }
    }
}
