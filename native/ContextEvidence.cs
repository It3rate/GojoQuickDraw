using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QuickDraw;
using Gojo.Patterns;
using Gojo.Structures;
using static Gojo.Experiments.QuickDraw.SequentialEvidence;
using static GojoQuickDraw.Native.NativeRecord;

namespace GojoQuickDraw.Native;

/// <summary>Context is supported open construction, never silently promoted to
/// observed geometry. Native participants, provisional scaffold sites, ordered
/// edges, bounds and signed relative-bound observations survive removal of all
/// descriptive names. Rich physical/category expectations remain adapter
/// proposals until an executable structural law is supplied.</summary>
public static class ContextEvidence
{
    // context = group(frame, parts, relations, assumptions, views, adapter provenance)
    // candidate = ReadWitness(group(actual profile witnesses, actual part witnesses),
    //   frame, group(scaffold, open slots, pair-bound reads), selected, evidence)
    // scaffold = group(group(site -> calibrated XY position), group(actual site pairs),
    //                  group(optional lower position -> upper position))
    // A site is fresh or explicitly selected from an earlier candidate. Coordinate
    // equality alone cannot identify it, and it cannot become an acquired sample.
    // pair-bound read = ReadWitness(outer bounds -> inner bounds, frame,
    //   group(inner.left-outer.left, inner.top-outer.top,
    //         outer.right-inner.right, outer.bottom-inner.bottom), selected, selected)
    // Positive values read inside these finite axis-aligned envelopes. This is
    // not a filled-area enclosure law, physical containment or feedback solver.
    public sealed record Candidate(Relation Witness, Relation[] Profiles, Relation[] Parts,
        Relation[] Points, Relation[] Segments, Relation? Bounds, Placeholder[] OpenSlots, Relation[] Comparisons);
    public sealed record Read(Frame Frame, Candidate[] Parts, Candidate[] Relations, Candidate[] Assumptions,
        Candidate[] Views, string AdapterJson)
    { public IEnumerable<Candidate> All => Parts.Concat(Relations).Concat(Assumptions).Concat(Views); }

    public static Relation Import(JsonElement packet, Relation evidence, QuickDrawAnnotationEvidence.Read? annotation)
    {
        Frame frame;
        if (annotation is not null) frame = annotation.Frame;
        // Use the established sequential reader instead of inventing a basis.
        else { SequentialEvidence.TryRead(Create(QuickDrawAnnotationEvidence.CoordinateQuantum, []), out var empty); frame = empty.Frame; }
        var profiles = new Dictionary<string, Relation>(StringComparer.Ordinal);
        if (annotation is not null)
        {
            var spans = packet.GetProperty("observation").GetProperty("spans").EnumerateArray().ToArray();
            for (var i = 0; i < spans.Length; i++) profiles.Add(spans[i].GetProperty("id").GetString()!, annotation.Profiles[i].Witness);
        }
        var parts = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        using var defaultContext = JsonDocument.Parse("{}");
        var context = packet.TryGetProperty("context", out var supplied) ? supplied : defaultContext.RootElement;
        if (context.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Context must be an object.");
        if (context.TryGetProperty("sourceId", out var identity) && annotation is not null &&
            identity.GetString() != packet.GetProperty("observation").GetProperty("sourceId").GetString())
            throw new InvalidDataException("Context source identity differs from acquisition.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Candidate Build(JsonElement item)
        {
            var id = item.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidDataException("Context candidates need unique nonempty IDs.");
            var selectedProfiles = Array(item, "spans").Select(value => profiles.TryGetValue(value.GetString()!, out var profile) ? profile :
                throw new InvalidDataException("Context references a missing observed profile.")).ToArray();
            if (selectedProfiles.Distinct(ReferenceEqualityComparer.Instance).Count() != selectedProfiles.Length) throw new InvalidDataException("A candidate repeats a profile as independent support.");
            var selectedParts = Array(item, "participants").Select(value => parts.TryGetValue(value.GetString()!, out var part) ? part :
                throw new InvalidDataException("Context references a missing or forward part.")).ToArray();
            if (selectedParts.Distinct(ReferenceEqualityComparer.Instance).Count() != selectedParts.Length) throw new InvalidDataException("A candidate repeats a part as independent support.");
            var points = new List<Relation>(); var segments = new List<Relation>();
            if (item.TryGetProperty("geometry", out var geometry))
            {
                var values = Array(geometry, "points");
                var siteSources = Array(geometry, "siteSources");
                if (siteSources.Length != 0 && siteSources.Length != values.Length) throw new InvalidDataException("Scaffold site sources must match its points.");
                for (var i = 0; i < values.Length; i++)
                {
                    var position = PointOf(values[i], frame);
                    if (siteSources.Length == 0 || siteSources[i].ValueKind == JsonValueKind.Null)
                        points.Add(new Relation(new Placeholder(), position));
                    else
                    {
                        var source = siteSources[i];
                        if (!parts.TryGetValue(source.GetProperty("part").GetString()!, out var prior) || !selectedParts.Contains(prior))
                            throw new InvalidDataException("Scaffold site source must select an earlier participating part.");
                        var index = source.GetProperty("index").GetInt32();
                        if (index < 0 || index >= prior.Points.Length) throw new InvalidDataException("Scaffold site source is absent.");
                        var point = prior.Points[index];
                        if (!TryPosition(point.Right, frame, out var actual) || !TryPosition(position, frame, out var expected) || actual != expected)
                            throw new InvalidDataException("Scaffold site source disagrees with its imported position.");
                        points.Add(point);
                    }
                }
                if (points.Distinct(ReferenceEqualityComparer.Instance).Count() != points.Count)
                    throw new InvalidDataException("A scaffold repeats the same site in its point list; reuse its index in edges instead.");
                foreach (var value in Array(geometry, "segments"))
                {
                    var indices = value.EnumerateArray().Select(index => index.GetInt32()).ToArray();
                    if (indices.Length != 2 || indices.Any(index => index < 0 || index >= points.Count)) throw new InvalidDataException("Scaffold segment has an absent site.");
                    segments.Add(new Relation(points[indices[0]], points[indices[1]]));
                }
            }
            Relation? bounds = null;
            if (item.TryGetProperty("bounds", out var valueBounds) && valueBounds.ValueKind != JsonValueKind.Null)
            {
                var numbers = valueBounds.EnumerateArray().Select(Quantize).ToArray();
                if (numbers.Length != 4 || numbers[0] > numbers[2] || numbers[1] > numbers[3]) throw new InvalidDataException("Candidate bounds are not ordered.");
                bounds = new Relation(Position(frame.X, frame.Y, new(numbers[0], numbers[1])), Position(frame.X, frame.Y, new(numbers[2], numbers[3])));
            }
            var selected = G(G(selectedProfiles), G(selectedParts.Select(part => (IElement)part.Witness).ToArray()));
            var slots = Enumerable.Range(0, 1 + Array(item, "openSlots").Length).Select(_ => new Placeholder()).ToArray();
            var comparisons = new List<Relation>();
            for (var i = 0; i < selectedParts.Length; i++) for (var j = i + 1; j < selectedParts.Length; j++)
                if (selectedParts[i].Bounds is { } outer && selectedParts[j].Bounds is { } inner)
                {
                    var compared = new Relation(outer, inner);
                    comparisons.Add(ReadWitness.Create(new Carrier(), compared, frame.Surface, Differences(frame, outer, inner), compared, compared));
                }
            var scaffold = G(G(points.ToArray()), G(segments.ToArray()), G(bounds is null ? [] : [bounds]));
            var witness = ReadWitness.Create(new Carrier(), selected, frame.Surface, G(scaffold, G(slots), G(comparisons.ToArray())), selected, evidence);
            return new(witness, selectedProfiles, selectedParts.Select(part => part.Witness).ToArray(), points.ToArray(), segments.ToArray(), bounds, slots, comparisons.ToArray());
        }
        var partRows = new List<Relation>();
        foreach (var item in Array(context, "parts")) { var part = Build(item); parts.Add(item.GetProperty("id").GetString()!, part); partRows.Add(part.Witness); }
        Relation Rows(string field) => G(Array(context, field).Select(item => (IElement)Build(item).Witness).ToArray());
        var result = G(frame.Surface, G(partRows.ToArray()), Rows("relations"), Rows("assumptions"), Rows("views"), EncodedTextSurface.Create(new Carrier(), context.GetRawText()));
        if (!TryRead(result, evidence, annotation, out _)) throw new InvalidDataException("Context support or scaffold references are inconsistent.");
        return result;
    }

    public static bool TryRead(Relation surface, Relation evidence, QuickDrawAnnotationEvidence.Read? annotation, out Read read)
    {
        read = null!;
        if (!Fields(surface, 6, out var fields) || fields[0] is not Relation frameSurface || !TryReadFrame(frameSurface, out var frame) ||
            frame.Quantum != QuickDrawAnnotationEvidence.CoordinateQuantum || annotation is not null && !ReferenceEquals(frame.Surface, annotation.Frame.Surface) ||
            !EncodedTextSurface.TryRead(fields[5], out var adapter)) return false;
        var profileSet = (annotation?.Profiles.Select(profile => profile.Witness) ?? []).ToHashSet<Relation>(ReferenceEqualityComparer.Instance);
        var partMap = new Dictionary<Relation, Candidate>(ReferenceEqualityComparer.Instance);
        var slots = new HashSet<Placeholder>(ReferenceEqualityComparer.Instance);
        var acquisitionSurface = annotation?.Interpretation.Source ?? evidence;
        if (!NativeAcquisition.TryRead(acquisitionSurface, out var acquisition)) return false;
        slots.Add(acquisition.Event);
        foreach (var sample in acquisition.Samples) slots.Add(sample.Event);
        if (acquisitionSurface.Right is not Relation strokeScope || !StructuralConstraint.TryReadScope(strokeScope, out var strokeRows, out _, out _)) return false;
        foreach (var stroke in strokeRows)
            if (stroke is Relation { Left: Placeholder strokeEvent }) slots.Add(strokeEvent);
            else return false;
        bool CandidateOf(IElement row, out Candidate candidate)
        {
            candidate = null!;
            if (row is not Relation witness || !ReadWitness.TryRead(witness, out var selected, out var basis, out var result, out var residue, out var provenance) ||
                !ReferenceEquals(basis, frame.Surface) || !ReferenceEquals(residue, selected) || !ReferenceEquals(provenance, evidence) ||
                !Fields(selected, 2, out var selection) || !Elements(selection[0], out var sourceRows) || sourceRows.Any(p => p is not Relation r || !profileSet.Contains(r)) ||
                !Elements(selection[1], out var selectedParts) || selectedParts.Any(p => p is not Relation r || !partMap.ContainsKey(r)) ||
                sourceRows.Distinct(ReferenceEqualityComparer.Instance).Count() != sourceRows.Length || selectedParts.Distinct(ReferenceEqualityComparer.Instance).Count() != selectedParts.Length ||
                !Fields(result, 3, out var body) || !Fields(body[0], 3, out var scaffold) || !Elements(scaffold[0], out var points) ||
                !Elements(scaffold[1], out var segments) || !Elements(scaffold[2], out var boundsRows) || boundsRows.Length > 1 ||
                !Elements(body[1], out var openSlots) || openSlots.Length == 0 || !Elements(body[2], out var comparisons)) return false;
            var permitted = selectedParts.Cast<Relation>().SelectMany(p => partMap[p].Points).ToHashSet(ReferenceEqualityComparer.Instance);
            if (points.Distinct(ReferenceEqualityComparer.Instance).Count() != points.Length) return false;
            foreach (var point in points)
                if (point is not Relation { Left: Placeholder slot, Right: var position } || slot.IsInverse ||
                    (!permitted.Contains(point) && !slots.Add(slot)) || !TryPosition(position, frame, out _)) return false;
            foreach (var segment in segments)
                if (segment is not Relation pair || !points.Contains(pair.Left, ReferenceEqualityComparer.Instance) || !points.Contains(pair.Right, ReferenceEqualityComparer.Instance)) return false;
            foreach (var open in openSlots) if (open is not Placeholder slot || slot.IsInverse || !slots.Add(slot)) return false;
            Relation? bounds = null;
            if (boundsRows.Length == 1)
            {
                if (boundsRows[0] is not Relation rectangle || !TryPosition(rectangle.Left, frame, out var low) || !TryPosition(rectangle.Right, frame, out var high) || low.X > high.X || low.Y > high.Y) return false;
                bounds = rectangle;
            }
            var expectedPairs = new List<(Relation Outer, Relation Inner)>();
            for (var i = 0; i < selectedParts.Length; i++) for (var j = i + 1; j < selectedParts.Length; j++)
                if (partMap[(Relation)selectedParts[i]].Bounds is { } outer && partMap[(Relation)selectedParts[j]].Bounds is { } inner) expectedPairs.Add((outer, inner));
            if (comparisons.Length != expectedPairs.Count) return false;
            for (var i = 0; i < comparisons.Length; i++)
            {
                if (comparisons[i] is not Relation comparison || !ReadWitness.TryRead(comparison, out var compared, out var compareBasis, out var differences, out var remainder, out var proof) ||
                    compared is not Relation pair || !ReferenceEquals(pair.Left, expectedPairs[i].Outer) || !ReferenceEquals(pair.Right, expectedPairs[i].Inner) ||
                    !ReferenceEquals(compareBasis, frame.Surface) || !ReferenceEquals(remainder, compared) || !ReferenceEquals(proof, compared) ||
                    !Elements(differences, out var actual)) return false;
                var expected = Entries(Differences(frame, expectedPairs[i].Outer, expectedPairs[i].Inner));
                if (actual.Length != expected.Length || actual.Where((entry, j) => entry is not CarrierLocation a || expected[j] is not CarrierLocation b ||
                    !ReferenceEquals(a.Carrier, b.Carrier) || a.Ticks != b.Ticks).Any()) return false;
            }
            candidate = new(witness, sourceRows.Cast<Relation>().ToArray(), selectedParts.Cast<Relation>().ToArray(), points.Cast<Relation>().ToArray(),
                segments.Cast<Relation>().ToArray(), bounds, openSlots.Cast<Placeholder>().ToArray(), comparisons.Cast<Relation>().ToArray()); return true;
        }
        var groups = new List<Candidate[]>();
        for (var index = 1; index <= 4; index++)
        {
            if (!Elements(fields[index], out var rows)) return false;
            var candidates = new List<Candidate>();
            foreach (var row in rows)
            {
                if (!CandidateOf(row, out var candidate)) return false;
                candidates.Add(candidate); if (index == 1 && !partMap.TryAdd(candidate.Witness, candidate)) return false;
            }
            groups.Add(candidates.ToArray());
        }
        read = new(frame, groups[0], groups[1], groups[2], groups[3], adapter); return true;
    }

    private static Relation Differences(Frame frame, Relation outer, Relation inner)
    {
        TryPosition(outer.Left, frame, out var a); TryPosition(outer.Right, frame, out var b);
        TryPosition(inner.Left, frame, out var c); TryPosition(inner.Right, frame, out var d);
        return G(new CarrierLocation(frame.X, checked(c.X - a.X)), new CarrierLocation(frame.Y, checked(c.Y - a.Y)),
            new CarrierLocation(frame.X, checked(b.X - d.X)), new CarrierLocation(frame.Y, checked(b.Y - d.Y)));
    }
    private static JsonElement[] Array(JsonElement value, string property) => value.TryGetProperty(property, out var array) ? array.EnumerateArray().ToArray() : [];
    private static Relation PointOf(JsonElement value, Frame frame)
    { var numbers = value.EnumerateArray().Select(Quantize).ToArray(); return numbers.Length == 2 ? Position(frame.X, frame.Y, new(numbers[0], numbers[1])) : throw new InvalidDataException("A scaffold point needs two coordinates."); }
    private static long Quantize(JsonElement value)
    { var n = value.GetDouble(); if (!double.IsFinite(n) || Math.Abs(n) > 16777) throw new InvalidDataException("Context coordinate exceeds the finite annotation basis."); return checked((long)Math.Round(n * QuickDrawAnnotationEvidence.CoordinateQuantum, MidpointRounding.ToEven)); }
    private static Relation Position(Carrier x, Carrier y, Point p) => new(new CarrierLocation(x, p.X), new CarrierLocation(y, p.Y));
    private static bool TryPosition(IElement value, Frame frame, out Point point)
    {
        point = default;
        if (value is not Relation { Left: CarrierLocation x, Right: CarrierLocation y } || !ReferenceEquals(x.Carrier, frame.X) || !ReferenceEquals(y.Carrier, frame.Y) ||
            x.Ticks < -(1 << 24) || x.Ticks > (1 << 24) || y.Ticks < -(1 << 24) || y.Ticks > (1 << 24)) return false;
        point = new(x.Ticks, y.Ticks); return true;
    }
    private static bool Elements(IElement value, out IElement[] fields)
    { if (value is Relation group && group.TryReadOrderedGroup(out fields)) return true; fields = []; return false; }
    private static bool Fields(IElement value, int count, out IElement[] fields) => Elements(value, out fields) && fields.Length == count;
    private static IElement[] Entries(IElement value) => Elements(value, out var fields) ? fields : throw new InvalidDataException("Expected native ordered group.");
}
