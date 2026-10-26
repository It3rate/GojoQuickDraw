using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QuickDraw;

namespace GojoQuickDraw.Native;

/// <summary>A restorable record, not a semantic DTO: ordered source text,
/// actual acquisition/finite observations, and separately open contextual reads.
/// The source text is provenance. Acquisition identity and annotation meaning
/// remain in their connected native structures and are independently checked.</summary>
public static class NativeRecord
{
    // record = group(exact source text, evidence, context, optional opinions,
    //                optional boundary correspondences, optional shape refinement)
    // evidence = native source annotations OR exact empty acquisition.
    public sealed record Read(string SourceOriginalJson, QuickDrawRecord Source,
        NativeAcquisition.Read Acquisition, QuickDrawAnnotationEvidence.Read? Annotation,
        ContextEvidence.Read Context, OpinionEvidence.Read? Opinions, BoundaryMappingEvidence.Read? BoundaryMapping, ShapeRefinementEvidence.Read? Refinement, Relation Surface);

    public static Relation Import(JsonElement packet)
    {
        var original = packet.TryGetProperty("sourceOriginalJson", out var raw) && raw.ValueKind == JsonValueKind.String
            ? raw.GetString()! : packet.GetProperty("source").GetRawText();
        var source = QuickDrawRecord.Parse(original);
        if (packet.TryGetProperty("context", out var contextInput) && contextInput.TryGetProperty("sourceId", out var contextSource) && contextSource.GetString() != source.KeyId)
            throw new InvalidDataException("Context source identity differs from acquisition.");
        Relation evidence;
        QuickDrawAnnotationEvidence.Read? annotation = null;
        if (source.Strokes.Sum(stroke => stroke.Samples.Count) == 0) evidence = new NativeAcquisition(source).Surface;
        else
        {
            evidence = QuickDrawAnnotationEvidence.Import(packet);
            if (!QuickDrawAnnotationEvidence.TryRead(evidence, out annotation)) throw new InvalidDataException("Source annotations are not readable.");
        }
        var context = ContextEvidence.Import(packet, evidence, annotation);
        if (!ContextEvidence.TryRead(context, evidence, annotation, out var contextual)) throw new InvalidDataException("Context is not independently readable.");
        var fields = new List<IElement> { EncodedTextSurface.Create(new Carrier(), original), evidence, context };
        if (packet.TryGetProperty("opinions", out _)) fields.Add(OpinionEvidence.Import(packet, evidence, annotation, contextual));
        if (packet.TryGetProperty("boundaryMapping", out _))
        {
            if (fields.Count != 4) throw new InvalidDataException("Boundary mapping requires the analysis opinion envelope.");
            fields.Add(BoundaryMappingEvidence.Import(packet, evidence, annotation, contextual));
        }
        if (packet.TryGetProperty("cleanup", out _))
        {
            if (fields.Count != 5) throw new InvalidDataException("Shape refinement requires the boundary correspondence envelope.");
            fields.Add(ShapeRefinementEvidence.Import(packet, evidence, contextual));
        }
        var result = G(fields.ToArray());
        if (!TryRead(result, out _)) throw new InvalidDataException("Complete native record does not preserve source and contextual references.");
        return result;
    }

    public static bool TryRead(Relation root, out Read read)
    {
        read = null!;
        if (!root.TryReadOrderedGroup(out var fields) || fields.Length is not (3 or 4 or 5 or 6) ||
            !EncodedTextSurface.TryRead(fields[0], out var text) || fields[1] is not Relation evidence || fields[2] is not Relation context) return false;
        QuickDrawRecord source;
        try { source = QuickDrawRecord.Parse(text); } catch (Exception e) when (e is JsonException or InvalidDataException or FormatException or OverflowException) { return false; }
        QuickDrawAnnotationEvidence.Read? annotation = null;
        NativeAcquisition.Read acquisition;
        if (QuickDrawAnnotationEvidence.TryRead(evidence, out var annotated)) { annotation = annotated; acquisition = annotated.Interpretation.Acquisition; }
        else if (!NativeAcquisition.TryRead(evidence, out acquisition) || acquisition.Samples.Count != 0) return false;
        if (source.Strokes.Count != acquisition.StrokeCount || source.Strokes.Sum(s => s.Samples.Count) != acquisition.Samples.Count) return false;
        foreach (var sample in acquisition.Samples)
        {
            var expected = source.Strokes[sample.StrokeIndex].Samples[sample.SampleIndex];
            if (expected.X.Numerator != sample.XNumerator || expected.X.Denominator != sample.XDenominator ||
                expected.Y.Numerator != sample.YNumerator || expected.Y.Denominator != sample.YDenominator ||
                expected.Time?.Numerator != sample.TimeNumerator || expected.Time?.Denominator != sample.TimeDenominator) return false;
        }
        if (!ContextEvidence.TryRead(context, evidence, annotation, out var contextual)) return false;
        OpinionEvidence.Read? opinions = null;
        if (fields.Length >= 4 && (fields[3] is not Relation opinionRoot || !OpinionEvidence.TryRead(opinionRoot, evidence, annotation, out opinions, contextual))) return false;
        BoundaryMappingEvidence.Read? mapping = null;
        if (fields.Length >= 5 && (fields[4] is not Relation mappingRoot || !BoundaryMappingEvidence.TryRead(mappingRoot, evidence, annotation, contextual, out mapping))) return false;
        ShapeRefinementEvidence.Read? refinement = null;
        if (fields.Length == 6 && (fields[5] is not Relation refinementRoot || !ShapeRefinementEvidence.TryRead(refinementRoot, evidence, contextual, out refinement))) return false;
        read = new(text, source, acquisition, annotation, contextual, opinions, mapping, refinement, root); return true;
    }

    public static string Capture(string itemKey, Relation root) => GojoStructureCompactJson.Serialize(GojoStructureCompactJson.FromElement(itemKey, root));

    public static Read Restore(string json)
    {
        var document = GojoStructureCompactJson.Deserialize(json);
        if (document.Version != 3 || !GojoStructureCompactJson.TryToElement(document, out var restored) ||
            restored.Root is not Relation root || !TryRead(root, out var read))
            throw new InvalidDataException("Incomplete or inconsistent native record archive; no partial record was published.");
        return read;
    }

    public static object Summarize(Read read)
    {
        var annotation = read.Annotation;
        return new
        {
            schema = "gojo.quickdraw-native-record.v1", sourceId = read.Source.KeyId, category = read.Source.Word,
            status = annotation is null ? "acquisition-only" : "fitted-evidence",
            sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(read.SourceOriginalJson))),
            sourceStrokes = read.Acquisition.StrokeCount, sourceSamples = read.Acquisition.Samples.Count,
            hasTiming = read.Source.HasTiming, finiteProfiles = annotation?.Profiles.Length ?? 0,
            lineReads = annotation?.Lines.Length ?? 0, jointReads = annotation?.Joints.Length ?? 0,
            actualSharedJoints = annotation?.Joints.Count(j => j.ActualSharedSite && j.Participants.Length > 1) ?? 0,
            jointKinds = (annotation?.Joints ?? []).GroupBy(j => j.Kind).ToDictionary(group => group.Key, group => group.Count()),
            reflectionReads = annotation?.Symmetries.Length ?? 0, reflectionPairs = annotation?.Symmetries.Sum(s => s.Pairs) ?? 0,
            relationReads = annotation?.Relations ?? 0,
            acquisitionRungs = annotation?.Interpretation.Construction.Rungs.Length ?? 0,
            contextParts = read.Context.Parts.Length, contextRelations = read.Context.Relations.Length,
            contextAssumptions = read.Context.Assumptions.Length, contextViews = read.Context.Views.Length,
            contextOpenHypotheses = read.Context.All.Sum(candidate => candidate.OpenSlots.Length),
            candidateScaffoldPoints = read.Context.All.Sum(candidate => candidate.Points.Length),
            candidateScaffoldSegments = read.Context.All.Sum(candidate => candidate.Segments.Length),
            boundComparisonReads = read.Context.All.Sum(candidate => candidate.Comparisons.Length),
            opinionClaims = read.Opinions?.Claims.Length ?? 0,
            opinionDependencyLinks = read.Opinions?.Claims.Sum(c => c.Dependencies.Length) ?? 0,
            opinionContextPartLinks = read.Opinions?.Claims.Sum(c => c.ContextParts.Length) ?? 0,
            opinionExpectedSites = read.Opinions?.Claims.Sum(c => c.Sites.Length) ?? 0,
            boundaryTargets = read.BoundaryMapping?.Targets.Length ?? 0,
            boundarySourceIntervals = read.BoundaryMapping?.Targets.Sum(t => t.Matches.Length) ?? 0,
            boundaryEndpointLinks = read.BoundaryMapping?.Targets.Sum(t => t.Endpoints.Length) ?? 0,
            unresolvedSourceIntervals = read.BoundaryMapping?.Unresolved.Length ?? 0,
            unprofiledSourceSamples = read.BoundaryMapping?.Unprofiled.Length ?? 0,
            mappingCoverageVerified = read.BoundaryMapping is not null,
            refinedShapeParts = read.Refinement?.Parts.Length ?? 0,
            refinementCorrespondences = read.Refinement?.Correspondences.Length ?? 0,
            refinementAttachments = read.Refinement?.Contacts.Length ?? 0,
            refinementMirrorReads = read.Refinement?.Mirrors ?? 0,
            nativeRestoreVerified = true, actualSourceAliasesVerified = true,
            sourceOrderRetained = true, sourceNumericTokensRetained = true,
            contextualHypothesesAreObservedGeometry = false, synthesizedVariantsSaved = false,
            authoredShapeRefinementSaved = read.Refinement is not null,
            qualification = "Finite line/joint/reflection reads and exact acquisition are independently readable. Context contains supported open candidates and separate scaffold geometry; labels do not certify parts, enclosure, gravity or physical support. No learned priors or automatic feedback settlement are asserted."
        };
    }
    internal static Relation G(params IElement[] elements) => Relation.OrderedGroup(new Carrier(), elements);
}
