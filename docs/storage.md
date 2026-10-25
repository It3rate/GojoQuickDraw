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
