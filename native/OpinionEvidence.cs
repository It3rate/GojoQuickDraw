using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QualitativeFrieze.Composition;
using Gojo.Experiments.QuickDraw;
using Gojo.Patterns;
using Gojo.Structures;
using static GojoQuickDraw.Native.NativeRecord;

namespace GojoQuickDraw.Native;

/// <summary>Source-linked opinion envelopes over the existing evidence ladder.
/// Expected geometry and earlier claims survive removal of provider/type names.
/// Grades are authored ordinal reads, not probabilities or carrier tension.</summary>
public static class OpinionEvidence
{
    // claim = ReadWitness(group(actual profiles, earlier claims, optional actual context parts), observer frame,
    //   group(expected sites, ordered edges, comparison frames, open role), selected, source evidence)
    // stream = ((source cause -> claim) -> (support rung -> opposition rung))
    //          -> (Closed(earlier claims) -> provenance)
    // root = group(frame, structural evidence bundle, Closed(claims), adapter provenance)
    public sealed record Claim(Relation Witness, Relation[] Profiles, Relation[] Dependencies,
        Relation[] Sites, Relation[] Edges, Relation[] Comparisons, Relation[] ContextParts);
    public sealed record Read(StructuralEvidenceStreams.Bundle Bundle, Claim[] Claims, string AdapterJson);

    public static Relation Import(JsonElement packet, Relation evidence, QuickDrawAnnotationEvidence.Read? annotation, ContextEvidence.Read? context = null)
    {
        var input = packet.GetProperty("opinions");
        if (input.GetProperty("sourceId").GetString() != packet.GetProperty("source").GetProperty("key_id").GetString())
            throw new InvalidDataException("Opinion source differs from the acquisition.");
        var frame = annotation?.Frame ?? EmptyFrame();
        var byProfile = new Dictionary<string, Relation>(StringComparer.Ordinal);
        if (annotation is not null)
        {
            var sourceSpans = packet.GetProperty("observation").GetProperty("spans").EnumerateArray().ToArray();
            for (var i = 0; i < sourceSpans.Length; i++) byProfile.Add(sourceSpans[i].GetProperty("id").GetString()!, annotation.Profiles[i].Witness);
        }
        var byContextPart = new Dictionary<string, Relation>(StringComparer.Ordinal);
        if (context is not null && packet.TryGetProperty("context", out var suppliedContext) && suppliedContext.TryGetProperty("parts", out var suppliedParts))
        {
            var rows = suppliedParts.EnumerateArray().ToArray();
            if (rows.Length != context.Parts.Length) throw new InvalidDataException("Opinion context differs from the imported object candidates.");
            for (var i = 0; i < rows.Length; i++) byContextPart.Add(rows[i].GetProperty("id").GetString()!, context.Parts[i].Witness);
        }
        var ladder = new StructuralEvidenceStreams.Frame();
        IElement Grade(JsonElement value) => value.GetString() switch
        { "none" => ladder.None, "weak" => ladder.Weak, "medium" => ladder.Medium, "strong" => ladder.Strong, _ => throw new InvalidDataException("Unknown opinion grade.") };
        var claims = new Dictionary<string, Relation>(StringComparer.Ordinal);
        var claimSites = new Dictionary<string, Relation[]>(StringComparer.Ordinal);
        var scopes = new Dictionary<string, List<IElement>> { ["observations"] = [], ["hypotheses"] = [], ["defaults"] = [], ["goals"] = [] };
        foreach (var item in input.GetProperty("claims").EnumerateArray())
        {
            var key = item.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(key) || claims.ContainsKey(key)) throw new InvalidDataException("Opinion identity must be unique.");
            var profiles = item.GetProperty("spans").EnumerateArray().Select(id => byProfile.TryGetValue(id.GetString()!, out var profile) ? profile :
                throw new InvalidDataException("Opinion selects an absent source profile.")).ToArray();
            var dependencies = item.GetProperty("dependencies").EnumerateArray().Select(id => claims.TryGetValue(id.GetString()!, out var prior) ? prior :
                throw new InvalidDataException("Opinion dependency is absent or forward.")).ToArray();
            var contextParts = item.TryGetProperty("contextParts", out var partNames) ? partNames.EnumerateArray().Select(id => byContextPart.TryGetValue(id.GetString()!, out var part) ? part :
                throw new InvalidDataException("Opinion selects an absent context part.")).ToArray() : [];
            if (profiles.Distinct(ReferenceEqualityComparer.Instance).Count() != profiles.Length || dependencies.Distinct(ReferenceEqualityComparer.Instance).Count() != dependencies.Length ||
                contextParts.Distinct(ReferenceEqualityComparer.Instance).Count() != contextParts.Length)
                throw new InvalidDataException("An opinion duplicates support as independent participation.");
            var geometry = item.GetProperty("geometry");
            var sources = geometry.TryGetProperty("pointSources", out var sourcePoints) ? sourcePoints.EnumerateArray().ToArray() : [];
            var pointValues = geometry.GetProperty("points").EnumerateArray().ToArray();
            if (sources.Length != 0 && sources.Length != pointValues.Length) throw new InvalidDataException("Expected point correspondence is incomplete.");
            var dependencyNames = item.GetProperty("dependencies").EnumerateArray().Select(d => d.GetString()!).ToHashSet(StringComparer.Ordinal);
            var sites = pointValues.Select((p, index) =>
            {
                var xy = p.EnumerateArray().Select(Number).ToArray();
                if (xy.Length != 2) throw new InvalidDataException("An expected site needs two coordinates.");
                if (sources.Length > 0 && sources[index].ValueKind != JsonValueKind.Null)
                {
                    var source = sources[index]; var parent = source.GetProperty("claim").GetString()!; var siteIndex = source.GetProperty("index").GetInt32();
                    if (!dependencyNames.Contains(parent) || !claimSites.TryGetValue(parent, out var priorSites) || siteIndex < 0 || siteIndex >= priorSites.Length)
                        throw new InvalidDataException("Expected site must reference an actual earlier dependency output.");
                    var prior = priorSites[siteIndex];
                    if (prior.Right is not Relation { Left: CarrierLocation x, Right: CarrierLocation y } || x.Ticks != xy[0] || y.Ticks != xy[1])
                        throw new InvalidDataException("Expected site correspondence has incompatible coordinates.");
                    return prior;
                }
                return new Relation(new Placeholder(), new Relation(new CarrierLocation(frame.X, xy[0]), new CarrierLocation(frame.Y, xy[1])));
            }).ToArray();
            var edges = geometry.GetProperty("segments").EnumerateArray().Select(p =>
            {
                var ends = p.EnumerateArray().Select(v => v.GetInt32()).ToArray();
                if (ends.Length != 2 || ends.Any(i => i < 0 || i >= sites.Length)) throw new InvalidDataException("Expected edge has an absent endpoint.");
                return new Relation(sites[ends[0]], sites[ends[1]]);
            }).ToArray();
            var comparisons = item.GetProperty("comparisons").EnumerateArray().Select(c =>
            {
                var basis = new Carrier();
                return new Relation(new Relation(new CarrierLocation(basis, Number(c.GetProperty("actual"))), new CarrierLocation(basis, Number(c.GetProperty("expected")))),
                    new CarrierLocation(basis, Number(c.GetProperty("allowance"))));
            }).ToArray();
            var selected = contextParts.Length == 0 ? G(G(profiles), G(dependencies)) : G(G(profiles), G(dependencies), G(contextParts));
            var claim = ReadWitness.Create(new Carrier(), selected, frame.Surface, G(G(sites), G(edges), G(comparisons), new Placeholder()), selected, evidence);
            claims.Add(key, claim);
            claimSites.Add(key, sites);
            var stream = new StructuralEvidenceStreams.Stream(claim, Grade(item.GetProperty("support")), Grade(item.GetProperty("opposition")),
                evidence, StructuralConstraint.Closed(dependencies), evidence);
            if (!scopes.TryGetValue(item.GetProperty("context").GetString()!, out var scope)) throw new InvalidDataException("Unknown opinion context.");
            scope.Add(stream.Surface);
        }
        var bundle = new StructuralEvidenceStreams.Bundle(ladder, StructuralConstraint.Closed(scopes["observations"].ToArray()),
            StructuralConstraint.Closed(scopes["hypotheses"].ToArray()), StructuralConstraint.Closed(scopes["defaults"].ToArray()), StructuralConstraint.Closed(scopes["goals"].ToArray()));
        var root = G(frame.Surface, bundle.Surface, G(claims.Values.ToArray()), EncodedTextSurface.Create(new Carrier(), input.GetRawText()));
        if (!TryRead(root, evidence, annotation, out _, context)) throw new InvalidDataException("Opinion structure is inconsistent.");
        return root;
    }

    public static bool TryRead(Relation root, Relation evidence, QuickDrawAnnotationEvidence.Read? annotation, out Read read, ContextEvidence.Read? context = null)
    {
        read = null!;
        if (!Fields(root, 4, out var fields) || fields[0] is not Relation basis || !SequentialEvidence.TryReadFrame(basis, out var frame) ||
            annotation is not null && !ReferenceEquals(basis, annotation.Frame.Surface) || fields[1] is not Relation bundled ||
            !StructuralEvidenceStreams.Bundle.TryRead(bundled, out var bundle) || !Elements(fields[2], out var rows) ||
            !EncodedTextSurface.TryRead(fields[3], out var adapter)) return false;
        var profiles = (annotation?.Profiles.Select(p => p.Witness) ?? []).ToHashSet(ReferenceEqualityComparer.Instance);
        var contextParts = (context?.Parts.Select(p => p.Witness) ?? []).ToHashSet(ReferenceEqualityComparer.Instance);
        var seen = new HashSet<Relation>(ReferenceEqualityComparer.Instance);
        var allSites = new HashSet<Placeholder>(ReferenceEqualityComparer.Instance);
        if (frame.Quantum != QuickDrawAnnotationEvidence.CoordinateQuantum) return false;
        var acquired = annotation?.Interpretation.Acquisition;
        if (acquired is null && !NativeAcquisition.TryRead(evidence, out acquired)) return false;
        allSites.Add(acquired.Event);
        foreach (var sample in acquired.Samples) allSites.Add(sample.Event);
        var claims = new List<Claim>();
        foreach (var row in rows)
        {
            if (row is not Relation claim || !ReadWitness.TryRead(claim, out var selected, out var claimBasis, out var result, out var residue, out var provenance) ||
                !ReferenceEquals(claimBasis, basis) || !ReferenceEquals(residue, selected) || !ReferenceEquals(provenance, evidence) ||
                !Elements(selected, out var inputs) || inputs.Length is not (2 or 3) || !Elements(inputs[0], out var sources) || sources.Any(s => s is not Relation r || !profiles.Contains(r)) ||
                !Elements(inputs[1], out var dependencies) || dependencies.Any(d => d is not Relation r || !seen.Contains(r)) ||
                sources.Distinct(ReferenceEqualityComparer.Instance).Count() != sources.Length || dependencies.Distinct(ReferenceEqualityComparer.Instance).Count() != dependencies.Length ||
                !Fields(result, 4, out var expectation) || !Elements(expectation[0], out var sites) || !Elements(expectation[1], out var edges) ||
                !Elements(expectation[2], out var comparisons) || expectation[3] is not Placeholder open || open.IsInverse || !allSites.Add(open)) return false;
            IElement[] selectedContextParts = [];
            if (inputs.Length == 3 && (!Elements(inputs[2], out selectedContextParts) ||
                selectedContextParts.Any(p => p is not Relation r || !contextParts.Contains(r)) ||
                selectedContextParts.Distinct(ReferenceEqualityComparer.Instance).Count() != selectedContextParts.Length)) return false;
            var permittedSites = claims.Where(c => dependencies.Contains(c.Witness, ReferenceEqualityComparer.Instance)).SelectMany(c => c.Sites).ToHashSet(ReferenceEqualityComparer.Instance);
            foreach (var site in sites)
                if (site is not Relation { Left: Placeholder identity, Right: Relation { Left: CarrierLocation x, Right: CarrierLocation y } } ||
                    identity.IsInverse || !permittedSites.Contains(site) && !allSites.Add(identity) || !ReferenceEquals(x.Carrier, frame.X) || !ReferenceEquals(y.Carrier, frame.Y)) return false;
            foreach (var edge in edges)
                if (edge is not Relation pair || !sites.Contains(pair.Left, ReferenceEqualityComparer.Instance) || !sites.Contains(pair.Right, ReferenceEqualityComparer.Instance)) return false;
            foreach (var comparison in comparisons)
                if (comparison is not Relation { Left: Relation { Left: CarrierLocation actual, Right: CarrierLocation expected }, Right: CarrierLocation allowance } ||
                    !ReferenceEquals(actual.Carrier, expected.Carrier) || !ReferenceEquals(actual.Carrier, allowance.Carrier) || allowance.Ticks < 0) return false;
            if (!seen.Add(claim)) return false;
            claims.Add(new(claim, sources.Cast<Relation>().ToArray(), dependencies.Cast<Relation>().ToArray(), sites.Cast<Relation>().ToArray(), edges.Cast<Relation>().ToArray(), comparisons.Cast<Relation>().ToArray(), selectedContextParts.Cast<Relation>().ToArray()));
        }
        var streams = new[] { bundle.Observations, bundle.Hypotheses, bundle.Defaults, bundle.Goals }.SelectMany(bundle.Read).ToArray();
        if (streams.Length != claims.Count || streams.Select(s => s.Claim).Distinct(ReferenceEqualityComparer.Instance).Count() != claims.Count) return false;
        foreach (var stream in streams)
        {
            var claim = claims.SingleOrDefault(c => ReferenceEquals(c.Witness, stream.Claim));
            if (claim is null || !ReferenceEquals(stream.Cause, evidence) || !ReferenceEquals(stream.Provenance, evidence) ||
                !StructuralConstraint.TryReadScope(stream.Dependencies, out var dependencies, out var seam, out var tail) || !seam.Equals(tail) ||
                !dependencies.SequenceEqual(claim.Dependencies, ReferenceEqualityComparer.Instance)) return false;
        }
        read = new(bundle, claims.ToArray(), adapter); return true;
    }

    private static long Number(JsonElement value)
    {
        var number = value.GetDouble();
        if (!double.IsFinite(number) || Math.Abs(number) > 10000000) throw new InvalidDataException("Opinion number exceeds finite import capacity.");
        return checked((long)Math.Round(number * QuickDrawAnnotationEvidence.CoordinateQuantum, MidpointRounding.ToEven));
    }
    private static SequentialEvidence.Frame EmptyFrame()
    { SequentialEvidence.TryRead(SequentialEvidence.Create(QuickDrawAnnotationEvidence.CoordinateQuantum, []), out var read); return read.Frame; }
    private static bool Elements(IElement item, out IElement[] fields)
    { if (item is Relation row && row.TryReadOrderedGroup(out fields)) return true; fields = []; return false; }
    private static bool Fields(IElement item, int count, out IElement[] fields) => Elements(item, out fields) && fields.Length == count;
}
