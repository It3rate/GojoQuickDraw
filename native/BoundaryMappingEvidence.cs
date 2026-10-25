using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QuickDraw;
using Gojo.Structures;
using static GojoQuickDraw.Native.NativeRecord;

namespace GojoQuickDraw.Native;

/// <summary>Non-draining interval correspondences, distinct from acquired site
/// identity. An expected boundary has its own ordered parameter basis. Missing
/// ink remains open; endpoint support never becomes an observed line.</summary>
public static class BoundaryMappingEvidence
{
    private const long Q = QuickDrawAnnotationEvidence.ParameterQuantum;
    // root = group(basis, target witnesses, unresolved source ranges,
    //              unprofiled acquired events, adapter provenance)
    // target selected = group(profile -> (source range -> target range),
    //                         profile -> actual acquired endpoint, actual parts)
    // target result = group(part -> ordered scaffold edges, target basis,
    //                       uncovered target ranges, open slot)
    // Both parameter bases are finite reads; neither is acquisition time.
    public sealed record Interval(Relation Profile, long From, long To);
    public sealed record Match(Interval Source, long TargetFrom, long TargetTo);
    public sealed record Endpoint(Relation Profile, Placeholder Site);
    public sealed record Target(Relation Witness, Relation Part, Relation[] Edges,
        Match[] Matches, Endpoint[] Endpoints, Relation[] RelatedParts, (long From, long To)[] Remainder);
    public sealed record Read(Target[] Targets, Interval[] Unresolved, Placeholder[] Unprofiled, string AdapterJson);

    public static Relation Import(JsonElement packet, Relation evidence, QuickDrawAnnotationEvidence.Read? annotation, ContextEvidence.Read context)
    {
        if (annotation is null) throw new InvalidDataException("Boundary correspondence requires fitted source evidence.");
        var input = packet.GetProperty("boundaryMapping");
        if (input.GetProperty("sourceId").GetString() != packet.GetProperty("source").GetProperty("key_id").GetString())
            throw new InvalidDataException("Boundary correspondence has a different acquisition cause.");
        var profiles = packet.GetProperty("observation").GetProperty("spans").EnumerateArray()
            .Select((p, i) => (Id: p.GetProperty("id").GetString()!, Profile: annotation.Profiles[i])).ToDictionary(p => p.Id, p => p.Profile);
        var parts = packet.GetProperty("context").GetProperty("parts").EnumerateArray()
            .Select((p, i) => (Id: p.GetProperty("id").GetString()!, Part: context.Parts[i])).ToDictionary(p => p.Id, p => p.Part);
        QuickDrawAnnotationEvidence.Profile Profile(JsonElement row) => profiles.TryGetValue(row.GetProperty("span").GetString()!, out var p) ? p : throw new InvalidDataException("Missing mapping profile.");
        ContextEvidence.Candidate Part(string id) => parts.TryGetValue(id, out var p) ? p : throw new InvalidDataException("Missing mapping part.");
        var parameter = annotation.Policy.Parameter;
        var basis = G(annotation.Frame.Surface, Relation.Segment(parameter, 0, Q));
        var targets = new List<IElement>();
        foreach (var row in input.GetProperty("targets").EnumerateArray())
        {
            var part = Part(row.GetProperty("part").GetString()!); var targetParameter = new Carrier();
            var edges = row.GetProperty("segments").EnumerateArray().Select(n => {
                var i = n.GetInt32(); return i >= 0 && i < part.Segments.Length ? part.Segments[i] : throw new InvalidDataException("Missing boundary segment.");
            }).ToArray();
            var ink = row.GetProperty("ink").EnumerateArray().Select(m => (IElement)new Relation(Profile(m).Witness,
                new Relation(Relation.Segment(parameter, Tick(m.GetProperty("from")), Tick(m.GetProperty("to"))),
                    Relation.Segment(targetParameter, Tick(m.GetProperty("targetFrom")), Tick(m.GetProperty("targetTo")))))).ToArray();
            var endpoints = row.GetProperty("endpointSupport").EnumerateArray().Select(m => {
                var p = Profile(m); var at = m.GetProperty("at").GetInt32();
                if (at is not (0 or 1)) throw new InvalidDataException("Endpoint support must select an actual profile endpoint.");
                return (IElement)new Relation(p.Witness, at == 0 ? p.Curve.From : p.Curve.To);
            }).ToArray();
            var related = row.GetProperty("relatedParts").EnumerateArray().Select(p => (IElement)Part(p.GetString()!).Witness).ToArray();
            var remainder = row.GetProperty("targetRemainder").EnumerateArray().Select(p => (IElement)Relation.Segment(targetParameter, Tick(p[0]), Tick(p[1]))).ToArray();
            var selected = G(G(ink), G(endpoints), G(related));
            targets.Add(ReadWitness.Create(new Carrier(), selected, basis,
                G(new Relation(part.Witness, G(edges)), Relation.Segment(targetParameter, 0, Q), G(remainder), new Placeholder()), selected, evidence));
        }
        var unresolved = input.GetProperty("sourceCoverage").EnumerateArray().Where(r => r.GetProperty("status").GetString() == "unresolved")
            .Select(r => (IElement)new Relation(Profile(r).Witness, Relation.Segment(parameter, Tick(r.GetProperty("from")), Tick(r.GetProperty("to"))))).ToArray();
        var samples = annotation.Interpretation.Acquisition.Samples.ToDictionary(p => (p.StrokeIndex, p.SampleIndex));
        var missing = input.GetProperty("unprofiledSamples").EnumerateArray().Select(r => samples.TryGetValue((r.GetProperty("stroke").GetInt32(), r.GetProperty("sample").GetInt32()), out var s)
            ? (IElement)s.Event : throw new InvalidDataException("Unknown unprofiled acquired event.")).ToArray();
        var root = G(basis, G(targets.ToArray()), G(unresolved), G(missing), EncodedTextSurface.Create(new Carrier(), input.GetRawText()));
        if (!TryRead(root, evidence, annotation, context, out _)) throw new InvalidDataException("Incomplete or inconsistent boundary correspondence.");
        return root;
    }

    public static bool TryRead(Relation root, Relation evidence, QuickDrawAnnotationEvidence.Read? annotation, ContextEvidence.Read context, out Read read)
    {
        read = null!;
        if (annotation is null || !Fields(root, 5, out var fields) || !Fields(fields[0], 2, out var basis) ||
            !ReferenceEquals(basis[0], annotation.Frame.Surface) || !Range(basis[1], annotation.Policy.Parameter, out var start, out var end) || start != 0 || end != Q ||
            !Elements(fields[1], out var rows) || !Elements(fields[2], out var unresolvedRows) || !Elements(fields[3], out var missingRows) ||
            !EncodedTextSurface.TryRead(fields[4], out var adapter)) return false;
        var profiles = new Dictionary<Relation, QuickDrawAnnotationEvidence.Profile>(ReferenceEqualityComparer.Instance);
        foreach (var profile in annotation.Profiles) profiles.Add(profile.Witness, profile);
        var parts = new Dictionary<Relation, ContextEvidence.Candidate>(ReferenceEqualityComparer.Instance);
        foreach (var part in context.Parts) parts.Add(part.Witness, part);
        var parameters = new HashSet<Carrier>(ReferenceEqualityComparer.Instance) { annotation.Policy.Parameter, annotation.Frame.X, annotation.Frame.Y };
        var slots = new HashSet<Placeholder>(ReferenceEqualityComparer.Instance) { annotation.Interpretation.Acquisition.Event };
        foreach (var sample in annotation.Interpretation.Acquisition.Samples) slots.Add(sample.Event);
        foreach (var p in context.All)
        {
            foreach (var slot in p.OpenSlots) slots.Add(slot);
            foreach (var point in p.Points) slots.Add((Placeholder)point.Left);
        }
        var targets = new List<Target>();
        foreach (var row in rows)
        {
            if (row is not Relation witness || !ReadWitness.TryRead(witness, out var selected, out var actualBasis, out var result, out var residue, out var provenance) ||
                !ReferenceEquals(actualBasis, fields[0]) || !ReferenceEquals(selected, residue) || !ReferenceEquals(evidence, provenance) ||
                !Fields(selected, 3, out var selection) || !Elements(selection[0], out var ink) || !Elements(selection[1], out var endpoints) || !Elements(selection[2], out var related) ||
                !Fields(result, 4, out var body) || body[0] is not Relation { Left: Relation owner, Right: var edgeGroup } || !parts.TryGetValue(owner, out var part) ||
                !Elements(edgeGroup, out var edges) || edges.Length == 0 || edges.Any(e => e is not Relation r || !part.Segments.Contains(r, ReferenceEqualityComparer.Instance)) || !Unique(edges) ||
                !Axis(body[1], out var targetParameter) || !parameters.Add(targetParameter) || !Elements(body[2], out var remaining) ||
                body[3] is not Placeholder open || open.IsInverse || !slots.Add(open) ||
                related.Any(p => p is not Relation r || !parts.ContainsKey(r)) || !Unique(related) || !related.Contains(owner, ReferenceEqualityComparer.Instance)) return false;
            var matches = new List<Match>(); var seen = new HashSet<(int, long, long)>();
            foreach (var item in ink)
            {
                if (item is not Relation { Left: Relation profile, Right: Relation pair } || !profiles.ContainsKey(profile) ||
                    !Range(pair.Left, annotation.Policy.Parameter, out var a, out var b) || a >= b || !seen.Add((profiles[profile].Index, a, b)) ||
                    !Range(pair.Right, targetParameter, out var c, out var d) || c == d) return false;
                matches.Add(new(new(profile, a, b), c, d));
            }
            var endpointReads = new List<Endpoint>();
            foreach (var item in endpoints)
            {
                if (item is not Relation { Left: Relation profile, Right: Placeholder site } || !profiles.TryGetValue(profile, out var p) ||
                    !ReferenceEquals(site, p.Curve.From) && !ReferenceEquals(site, p.Curve.To) || endpointReads.Any(e => e.Profile == profile && e.Site == site)) return false;
                endpointReads.Add(new(profile, site));
            }
            var remainder = new List<(long, long)>();
            foreach (var item in remaining)
            { if (!Range(item, targetParameter, out var a, out var b) || a >= b) return false; remainder.Add((a, b)); }
            if (!remainder.SequenceEqual(Complement(matches.Select(m => (m.TargetFrom, m.TargetTo))))) return false;
            targets.Add(new(witness, owner, edges.Cast<Relation>().ToArray(), matches.ToArray(), endpointReads.ToArray(), related.Cast<Relation>().ToArray(), remainder.ToArray()));
        }
        if (!Unique(rows)) return false;
        var unresolved = new List<Interval>();
        foreach (var row in unresolvedRows)
        {
            if (row is not Relation { Left: Relation profile, Right: var range } || !profiles.ContainsKey(profile) ||
                !Range(range, annotation.Policy.Parameter, out var a, out var b) || a >= b) return false;
            unresolved.Add(new(profile, a, b));
        }
        // Verify coverage by union, so shared references never multiply source ink.
        foreach (var profile in profiles.Keys)
        {
            var used = targets.SelectMany(t => t.Matches).Where(m => m.Source.Profile == profile).Select(m => (m.Source.From, m.Source.To));
            if (!unresolved.Where(r => r.Profile == profile).Select(r => (r.From, r.To)).OrderBy(r => r.From).SequenceEqual(Complement(used))) return false;
        }
        var expectedMissing = annotation.Interpretation.Acquisition.Samples.Where(s => !annotation.Profiles.Any(p =>
            p.Source.Stroke == s.StrokeIndex && p.Source.Start <= s.SampleIndex && p.Source.End >= s.SampleIndex)).Select(s => s.Event).ToArray();
        if (!Unique(missingRows) || missingRows.Length != expectedMissing.Length || missingRows.Any(e => e is not Placeholder p || !expectedMissing.Contains(p, ReferenceEqualityComparer.Instance))) return false;
        read = new(targets.ToArray(), unresolved.ToArray(), missingRows.Cast<Placeholder>().ToArray(), adapter); return true;
    }

    private static long Tick(JsonElement value)
    {
        var n = value.GetDouble();
        if (!double.IsFinite(n) || n < 0 || n > 1) throw new InvalidDataException("Parameter lies outside its finite basis.");
        return (long)Math.Round(n * Q, MidpointRounding.ToEven);
    }
    private static bool Elements(IElement e, out IElement[] rows) { rows = []; return e is Relation r && r.TryReadOrderedGroup(out rows); }
    private static bool Fields(IElement e, int n, out IElement[] rows) => Elements(e, out rows) && rows.Length == n;
    private static bool Unique(IElement[] rows) => rows.Distinct(ReferenceEqualityComparer.Instance).Count() == rows.Length;
    private static bool Range(IElement e, Carrier carrier, out long from, out long to)
    {
        from = to = 0;
        if (e is not Relation { Left: Relation { Left: CarrierLocation a, Right: Carrier c }, Right: Relation { Left: CarrierLocation b, Right: Carrier d } } ||
            !ReferenceEquals(c, carrier) || !ReferenceEquals(d, carrier) || !ReferenceEquals(a.Carrier, carrier) || !ReferenceEquals(b.Carrier, carrier) || a.Ticks < 0 || a.Ticks > Q || b.Ticks < 0 || b.Ticks > Q) return false;
        from = a.Ticks; to = b.Ticks; return true;
    }
    private static bool Axis(IElement e, out Carrier carrier)
    { carrier = null!; if (e is not Relation { Left: Relation { Right: Carrier c } } || !Range(e, c, out var a, out var b) || a != 0 || b != Q) return false; carrier = c; return true; }
    private static (long, long)[] Complement(IEnumerable<(long From, long To)> source)
    {
        var remaining = new List<(long, long)>(); long last = 0;
        foreach (var (a, b) in source.Select(r => (Math.Min(r.From, r.To), Math.Max(r.From, r.To))).OrderBy(r => r.Item1))
        { if (a > last) remaining.Add((last, a)); last = Math.Max(last, b); }
        if (last < Q) remaining.Add((last, Q)); return remaining.ToArray();
    }
}
