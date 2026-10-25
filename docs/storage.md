# Storage and evidence contract

## Category collections and selection

The `sample` command writes a `gojo.quickdraw-collection.v1` root:

```text
collection.json
_sampling/active.json
_sampling/sample-<time>-<id>.json
angel/manifest.json
angel/index.ndjson
angel/native/...
church/manifest.json
church/index.ndjson
church/native/...
```

Only categories actually selected for records acquire dataset folders. Each child
is independently readable and uses the existing native evidence format below.
There is no duplicated root record index. Inspection discovers real child manifests
recursively, so it also works on a single category, an older flat dataset, or a
parent folder containing several generated datasets.

The requested count is spread over random category rounds. Within each category,
record selection is uniform without replacement. This intentionally balances
categories rather than weighting categories by their different file lengths.
An explicit seed is reproducible for unchanged source files. The plan records source
fingerprints, original byte offsets and line numbers, record hashes, selected
categories and the seed. Interrupted runs resume the saved plan. Completed unseeded
runs create a new selection; repeated records are reused instead of duplicated.

Source indexes are rebuildable caches under
`%LOCALAPPDATA%/GojoQuickDraw/source-index` (or the platform cache equivalent).
They store offsets and line numbers, with checksums and source size/modification
fingerprints. They contain no fitted drawings. Indexing is lazy: only needed
category files are indexed. The first pass scans them; subsequent uncompressed
samples seek straight to records. Gzip offsets refer to its uncompressed stream.

The filename supplies category context for this mode, while the original record's
`word` and text remain intact. Exact origin file and line remain in the index and
adapter provenance. Selecting a category does not certify an intended part or concept.

Browsing order is separate from acquisition order. Category order traverses generated
folders alphabetically and retains journal order within each. Most recent uses each
successful record's `convertedUtc`, with artifact modification time for older records.
Random browsing ranks records through a seeded hash, so a shorter review is a prefix
of the same longer catalog. Changing the browse order never changes strokes or archives.

## Dataset layout

```text
manifest.json                  extraction configuration and source fingerprints
index.ndjson                   append-only per-record success/error journal
records/<id>.evidence.json.gz   review and proposal cache
native/<id>.gojo.json.gz        independently restorable native Gojo archive
errors/<id>.source.json.gz      failed source text and diagnostic, when present
.index-cache.sqlite3           disposable disk-backed journal projection
.convert.lock                  operating-system writer lock
```

The manifest is `gojo.quickdraw-dataset.v1`. The record adapter is
`gojo.quickdraw-evidence-packet.v1`. Both are initial experimental contracts.
The index records source identity, source file and line, exact-record digest,
artifact paths and hashes, observed counts, and the native reader's summary.

An item ID is derived from extraction fingerprint, source-text digest, and effective
category. A source key alone is not an archive identity: the same key with different
ink or different extraction settings must not overwrite an earlier interpretation.
The category participates because filename context can change the proposals for a
record without changing its text.

Files publish before their journal row. An interrupted final journal row is preserved
as `index-interrupted-tail.bin` and removed only during an explicit conversion resume.
A malformed committed row is rejected. Successful archived records are checked before
reuse. SQLite is a rebuildable cache, not the portable source of record.

## Exact acquisition and fitted intervals

`sourceOriginalJson` retains the original nonempty record text, excluding the line
separator and file BOM. Original whitespace, numeric spelling, stroke order and
time values survive in the archive. An independent native read checks its parsed
coordinates/times against the connected acquisition. No inferred ground, snapped
corner, or part scaffold is added to that source.

`observation` contains the current algorithm's fitted spans and their source support.
A span is an interval of a source stroke; several spans can belong to one stroke.
`program` contains calibration guides and acquisition-ordered fitted construction,
not the later semantic interpretation ladder. `annotations` refers to those intervals
and distinguishes supported relations from geometric proximity.

The source remains exact while fitted/native observation uses its declared calibration
and coordinate quantum. Equality after quantization does not manufacture a shared
acquisition event or shared site. Empty acquisitions have no fitted program or annotation
packet and cannot acquire a church through context alone.

## Context is separate and open

`context` keeps `parts`, `relations`, `assumptions`, and `views` separate. Every proposed
part carries its source intervals, provisional geometry/bounds, expectations, reads,
qualification, and any unresolved evidence. Relations refer to previously introduced
parts. Missing and forward references are rejected by the native bridge.

The native context surface connects:

- The existing observer frame and actual profile witnesses.
- Selected earlier part witnesses.
- Fresh provisional sites, ordered edges, and bounds.
- Open slots rather than fabricated acquired samples.
- Pair-bound reads with four signed clearances in the same frame.
- The adapter text as provenance for verbal roles and expectations.

For an outer frame and an inner frame, the clearances read left, top, right and bottom
margin. They are recomputed on restore. Positive margins mean inside those finite
axis-aligned bounds; they do not establish physical containment or an arbitrary
curved enclosure. A candidate's geometric extent, support and unresolved structure
remain readable without interpreting its descriptive class name. Its full conceptual
role may still be open.

## Authority and weights

Native connected structure is authoritative for implemented structural reads. JSON
labels and source category are proposals/context, not the sole source of meaning.
Existing native annotation evidence settings are retained; the added contextual
heuristics are authored and explicitly unlearned. They are not posterior probabilities.

Do not add fit residuals, source confidence, part likelihoods, physical plausibility and
complexity into an unexplained global score. A future learned read needs a named basis,
support, resolution, provenance, and a way to expose what it does not account for.
Repeated annotations of the same interval are correlated support, not fresh evidence.

`verify` checks both artifact hashes and independently restores the native archive,
matching its exact source digest, source ID, and acquisition counts to the index.
The native command also supports `inspect --input PATH --source` for recovering exact
source text without relying on the sidecar. The HTML inspector uses the verified cache;
it is an observation tool and does not mutate native carriers.

## Derived runs

Interpretation runs have a separate destination, source artifact hashes, linked-code
fingerprints, their construction/evidence outputs, and a portable review. Their native
execution and restore checks are necessary integrity tests, not a proof of correctness.
`diagnostics.json` reports actual final native curve extent and whether it is outside
the observer frame. Displacement metrics retain their author's rounding qualification.

Probe output records the intervention, participants, changed bounds, component reads,
and remaining discrepancies. It can be discarded and regenerated. Neither several
stages nor several perturbations count as independent human drawings.

The baseline stores no later synthesis passes. For corpus-scale training, a future
compact projection should select exposed evidence surfaces and point back to this
archive. The current rich capture is intended for inspection and schema development,
not yet a claim of economical full-corpus storage.

## Derived analysis collections and opinion structure

`analyze` consumes generated datasets rather than resampling raw QuickDraw. It
writes a separate collection with the same category/dataset/index layout. Each
record identifies the baseline item and its evidence/native hashes, an analysis
implementation fingerprint, and the unchanged original-source digest. A bounded
worker queue proposes and independently captures/restores native records; the
parent commits their journal entries in source-selection order. Completed records
resume after hash checks. An interrupted journal tail is retained/repaired by the
same explicit resume mechanism used for conversion.

Analysis packets add `analysis` and `opinions`, and the bounded church pass adds
`objects`. `analysis.annotationModes` contains
the three source-based allowances. `analysis.proposal` caches seven authored church
states, their operations, complement, correspondence and warnings. These previews
are **not executed native rungs**. The independent `stages` command still creates
and executes the complete construction program on demand. No synthesized state is
promoted to new human evidence or a ground-truth target.

Native record roots accept the earlier three-field layout and an optional fourth
opinion surface. That surface connects the actual observer frame, the existing
`StructuralEvidenceStreams.Bundle`, claim witnesses, and adapter provenance.
Each claim selects actual source-profile witnesses and earlier claims; an optional
third selection group holds actual context-part witnesses. Older two-group claims
remain readable. Part labels alone cannot satisfy these references. Its expected
sites and ordered edges are native structure. A dependent corner can consume the
actual earlier rectangle sites through explicit correspondence. Equal coordinates
alone do not create this sharing, and acquired identities cannot be stolen.

Separate observation/hypothesis/default/goal scopes retain support and opposition
on the connected ordinal ladder. Comparison inputs have a declared finite import
quantum and remain provider inputs, not independent native proofs of a detector's
correctness. Type names, verbal roles and provider versions remain provenance;
geometric participation, dependency, site sharing and evidence context remain
readable after those names are removed. Models can later answer the same exposed
requests without changing the source-selection and native evidence contract.

Container carrying is an explicit revisable default, separate from geometric
containment. Its counterfactual translates a parent and reachable contained parts
once each; a child placement can vary independently. Cycles, alternative parents,
anchored-child overrides and existing containment discrepancy remain visible.
This is not yet general native physical settlement.

## Provisional church objects

`objects.candidates` is a presentation summary of additional `context.parts` and
their connected relations. Each part selects actual source profiles and, where
applicable, earlier part witnesses. An assembly and a wall can select the same
profiles at different focuses; they do not duplicate acquisition. IDs derive from
source membership and role, while `instanceId` deliberately omits role. This is
stable for an unchanged fitted source; it is not identity tracking across a refit.

Source-supported groups may span pen lifts or select a sub-contour inside a longer
stroke. Copies remain distinct. `parentIds` is a display hierarchy; actual native
dependencies use ordered `participants`, and relation witnesses link other parent
alternatives. Bounds are provisional envelopes. They are not occupied regions or
proof of physical containment. Verbal roles remain authored hypotheses, not newly
implemented native roof/window semantics.

Unknown and conflicting memberships remain separate from mere candidate coverage.
The summary records grouping limits, withheld candidates and unread spans. Provider
requests receive immutable snapshots and select exact native object candidates;
ordinal support/opposition is not probability. Repeated passes over the same ink
are not independent training examples. The new groups do not yet change the seven
synthesis states. See [church heuristics](../CHURCH_OBJECT_HEURISTICS.md).

Review HTML is a regenerable projection. `refresh` rebuilds both presentations
from saved packets and records the collection journals in `refresh.json`. The
desktop does this after successful processing; an open browser needs reloading.
Changing analysis requires a new derived output, so source and earlier results
remain inspectable under their original fingerprints.

## Paged inspection

`catalog --offset N --limit K` selects a lightweight page; actual latest journal
states determine totals, not a stale manifest's last-run count. Stable category,
recent and seeded random orders have the same membership across catalog pages
and offline review pages.

The CLI review's limit is now page size. The entry HTML and its adjacent
`<entry-stem>-pages/<generation>/page-00002.html` documents expose every successful
record while embedding only one page's data per document. All page documents are
written before publishing the entry. Older page generations remain available for
already-open reviews; the desktop reuses one stable entry per selected collection.
The single-page limited API remains available as `review --sample-only`.

## Ordered endpoints and timing

Every finite source interval has `strokeIndex`, `startIndex`, `endIndex` and
ordered controls. Every native line/quadratic/cubic has explicit `From -> To`
sites: the parameter begins at the first control and ends at the last. An O read
does not weld separate first/last acquisition events. A constructed closing edge
can deliberately return to an actual earlier site.

Raw three-array XYT records preserve point times exactly in acquisition; simplified
two-array XY records leave timing open. Missing time is not zero, interpolation,
or an invented drawing duration. Time order, geometric order and later authored
interpretation history stay distinct.
