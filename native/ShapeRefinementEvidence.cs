using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using static GojoQuickDraw.Native.NativeRecord;

namespace GojoQuickDraw.Native;

/// <summary>Local before/after scaffold reads and explicitly shared attachments.
/// No selected source is mutated, drained, or promoted to new acquired ink.</summary>
public static class ShapeRefinementEvidence
{
    // root = group(frame, result parts, correspondences, contacts, mirrors, provenance)
    // correspondence = ReadWitness(before -> after, frame,
    //   group((actual before site -> actual after site) -> signed XY change), before, acquisition)
    // contact = (actual parent -> actual child) -> (shared site -> shared site)
    // mirror = ReadWitness(group(left,right,axis part), frame,
    //   group(left.L+right.R-2*axis, left.R+right.L-2*axis, top delta, bottom delta),
    //   selection, acquisition). Nonzero differences remain readable residue.
    public sealed record Correspondence(Relation Source, Relation Target, Relation[] SitePairs);
    public sealed record Contact(Relation Parent, Relation Child, Relation Site);
    public sealed record Read(Relation[] Parts, Correspondence[] Correspondences, Contact[] Contacts, int Mirrors, string AdapterJson);

    public static Relation Import(JsonElement packet, Relation evidence, ContextEvidence.Read context)
    {
        var input = packet.GetProperty("cleanup");
        if (input.GetProperty("sourceId").GetString() != packet.GetProperty("source").GetProperty("key_id").GetString())
            throw new InvalidDataException("Refinement has a different acquisition cause.");
        var parts = packet.GetProperty("context").GetProperty("parts").EnumerateArray()
            .Select((p, i) => (Id: p.GetProperty("id").GetString()!, Part: context.Parts[i])).ToDictionary(p => p.Id, p => p.Part);
        ContextEvidence.Candidate Part(string id) => parts.TryGetValue(id, out var p) ? p : throw new InvalidDataException("Missing refinement part.");
        Relation Site(ContextEvidence.Candidate part, int i) => i >= 0 && i < part.Points.Length ? part.Points[i] : throw new InvalidDataException("Missing refinement site.");
        var finals = input.GetProperty("parts").EnumerateArray().Select(p => (IElement)Part(p.GetProperty("id").GetString()!).Witness).ToArray();
        var correspondences = new List<IElement>();
        foreach (var row in input.GetProperty("correspondences").EnumerateArray())
        {
            var before = Part(row.GetProperty("source").GetString()!); var after = Part(row.GetProperty("target").GetString()!);
            var selected = new Relation(before.Witness, after.Witness);
            var pairs = row.GetProperty("points").EnumerateArray().Select(p => {
                var a = Site(before, p[0].GetInt32()); var b = Site(after, p[1].GetInt32());
                return (IElement)new Relation(new Relation(a, b), Delta(a, b, context));
            }).ToArray();
            correspondences.Add(ReadWitness.Create(new Carrier(), selected, context.Frame.Surface, G(pairs), before.Witness, evidence));
        }
        var contacts = input.GetProperty("attachments").EnumerateArray().Select(row => {
            var parent = Part(row.GetProperty("parent").GetString()!); var child = Part(row.GetProperty("child").GetString()!);
            return (IElement)new Relation(new Relation(parent.Witness, child.Witness),
                new Relation(Site(parent, row.GetProperty("parentPoint").GetInt32()), Site(child, row.GetProperty("childPoint").GetInt32())));
        }).ToArray();
        var mirrors = input.GetProperty("symmetries").EnumerateArray().Select(row => {
            var left = Part(row.GetProperty("left").GetString()!); var right = Part(row.GetProperty("right").GetString()!); var axis = Part(row.GetProperty("axisPart").GetString()!);
            var selected = G(left.Witness, right.Witness, axis.Witness);
            return (IElement)ReadWitness.Create(new Carrier(), selected, context.Frame.Surface, Mirror(left, right, axis, context), selected, evidence);
        }).ToArray();
        var root = G(context.Frame.Surface, G(finals), G(correspondences.ToArray()), G(contacts), G(mirrors), EncodedTextSurface.Create(new Carrier(), input.GetRawText()));
        if (!TryRead(root, evidence, context, out _)) throw new InvalidDataException("Refinement correspondence or attachment is inconsistent.");
        return root;
    }

    public static bool TryRead(Relation root, Relation evidence, ContextEvidence.Read context, out Read read)
    {
        read = null!;
        if (!Fields(root, 6, out var fields) || !ReferenceEquals(fields[0], context.Frame.Surface) ||
            !Elements(fields[1], out var finals) || !Elements(fields[2], out var rows) || !Elements(fields[3], out var contacts) ||
            !Elements(fields[4], out var mirrors) || !EncodedTextSurface.TryRead(fields[5], out var adapter)) return false;
        var parts = new Dictionary<Relation, ContextEvidence.Candidate>(ReferenceEqualityComparer.Instance);
        foreach (var p in context.Parts) parts.Add(p.Witness, p);
        var finalSet = new HashSet<IElement>(finals, ReferenceEqualityComparer.Instance);
        if (finalSet.Count != finals.Length || finals.Any(p => p is not Relation r || !parts.ContainsKey(r) || parts[r].Profiles.Length != 0)) return false;
        var correspondences = new List<Correspondence>(); var selectedTargets = new HashSet<IElement>(ReferenceEqualityComparer.Instance);
        foreach (var row in rows)
        {
            if (row is not Relation witness || !ReadWitness.TryRead(witness, out var selected, out var basis, out var result, out var residue, out var proof) ||
                !ReferenceEquals(basis, context.Frame.Surface) || !ReferenceEquals(proof, evidence) ||
                selected is not Relation { Left: Relation source, Right: Relation target } || !parts.TryGetValue(source, out var before) || !parts.TryGetValue(target, out var after) ||
                !ReferenceEquals(residue, source) || finalSet.Contains(source) || !finalSet.Contains(target) || !selectedTargets.Add(target) ||
                !after.Parts.Contains(source, ReferenceEqualityComparer.Instance) || !Elements(result, out var pairs) || pairs.Length != after.Points.Length) return false;
            var targetSites = new HashSet<IElement>(ReferenceEqualityComparer.Instance); var sitePairs = new List<Relation>();
            foreach (var item in pairs)
            {
                if (item is not Relation { Left: Relation pair, Right: var delta } || pair.Left is not Relation a || pair.Right is not Relation b ||
                    !before.Points.Contains(a, ReferenceEqualityComparer.Instance) || !after.Points.Contains(b, ReferenceEqualityComparer.Instance) || !targetSites.Add(b) ||
                    !SameNumbers(delta, Delta(a, b, context))) return false;
                sitePairs.Add(pair);
            }
            correspondences.Add(new(source, target, sitePairs.ToArray()));
        }
        // Parts without a correspondence must be explicit new open proposals:
        // no direct source profiles or earlier unrefined parts may be claimed.
        foreach (var target in finals.Cast<Relation>())
            if (!selectedTargets.Contains(target) && parts[target].Parts.Any(p => !finalSet.Contains(p))) return false;
        var contactReads = new List<Contact>(); var seenContacts = new HashSet<(int, int, int)>();
        foreach (var item in contacts)
        {
            if (item is not Relation { Left: Relation { Left: Relation parent, Right: Relation child }, Right: Relation { Left: Relation a, Right: Relation b } } ||
                !finalSet.Contains(parent) || !finalSet.Contains(child) || !ReferenceEquals(a, b) ||
                !parts[parent].Points.Contains(a, ReferenceEqualityComparer.Instance) || !parts[child].Points.Contains(b, ReferenceEqualityComparer.Instance) ||
                !parts[child].Parts.Contains(parent, ReferenceEqualityComparer.Instance)) return false;
            var pi = Array.FindIndex(finals, p => ReferenceEquals(p, parent)); var ci = Array.FindIndex(finals, p => ReferenceEquals(p, child));
            var si = Array.FindIndex(parts[parent].Points, p => ReferenceEquals(p, a));
            if (pi >= ci || !seenContacts.Add((pi, ci, si))) return false;
            contactReads.Add(new(parent, child, a));
        }
        foreach (var item in mirrors)
        {
            if (item is not Relation witness || !ReadWitness.TryRead(witness, out var selected, out var basis, out var result, out var residue, out var proof) ||
                !ReferenceEquals(basis, context.Frame.Surface) || !ReferenceEquals(proof, evidence) || !ReferenceEquals(residue, selected) ||
                !Fields(selected, 3, out var choices) || choices.Any(c => !finalSet.Contains(c)) ||
                choices.Any(c => parts[(Relation)c].Bounds is null) ||
                !SameNumbers(result, Mirror(parts[(Relation)choices[0]], parts[(Relation)choices[1]], parts[(Relation)choices[2]], context))) return false;
        }
        read = new(finals.Cast<Relation>().ToArray(), correspondences.ToArray(), contactReads.ToArray(), mirrors.Length, adapter); return true;
    }

    private static (long X, long Y) XY(IElement position)
    {
        var p = (Relation)position; return (((CarrierLocation)p.Left).Ticks, ((CarrierLocation)p.Right).Ticks);
    }
    private static Relation Delta(Relation a, Relation b, ContextEvidence.Read context)
    {
        var x = XY(a.Right); var y = XY(b.Right);
        return G(new CarrierLocation(context.Frame.X, checked(y.X-x.X)), new CarrierLocation(context.Frame.Y, checked(y.Y-x.Y)));
    }
    private static Relation Mirror(ContextEvidence.Candidate a, ContextEvidence.Candidate b, ContextEvidence.Candidate axis, ContextEvidence.Read context)
    {
        if (a.Bounds is null || b.Bounds is null || axis.Bounds is null) throw new InvalidDataException("Mirror read needs selected bounds.");
        var al=XY(a.Bounds.Left);var ar=XY(a.Bounds.Right);var bl=XY(b.Bounds.Left);var br=XY(b.Bounds.Right);
        var cl=XY(axis.Bounds.Left);var cr=XY(axis.Bounds.Right);
        return G(new CarrierLocation(context.Frame.X, checked(al.X+br.X-cl.X-cr.X)), new CarrierLocation(context.Frame.X, checked(ar.X+bl.X-cl.X-cr.X)),
            new CarrierLocation(context.Frame.Y, checked(al.Y-bl.Y)),new CarrierLocation(context.Frame.Y, checked(ar.Y-br.Y)));
    }
    private static bool SameNumbers(IElement a, IElement b) => Elements(a, out var aa) && Elements(b, out var bb) && aa.Length == bb.Length &&
        aa.Zip(bb).All(p => p.First is CarrierLocation x && p.Second is CarrierLocation y && ReferenceEquals(x.Carrier,y.Carrier) && x.Ticks==y.Ticks);
    private static bool Elements(IElement value, out IElement[] fields)
    { if (value is Relation group && group.TryReadOrderedGroup(out fields)) return true; fields=[]; return false; }
    private static bool Fields(IElement value, int count, out IElement[] fields) => Elements(value, out fields) && fields.Length==count;
}
