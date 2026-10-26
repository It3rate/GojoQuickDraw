# Gojo QuickDraw

Turn QuickDraw files into source-linked Gojo evidence, then generate interpretations
on demand. This is a separate converter project linked to [Gojo](https://github.com/It3rate/Gojo).
It starts with church drawings while retaining a category-independent acquisition,
fitting, and annotation path.

The saved baseline is the drawing we received and the evidence we can read from it.
Seeded church parts and expectations are separate proposals. Later simplification,
joint merging, and synthesis are disposable experiments, not training labels.

## Run from a checkout

Requirements: Python 3.10 or newer, the .NET 10 SDK, and a sibling Gojo checkout.
The tested Gojo revision is recorded in `gojo.lock.json`. No Python runtime packages
are required. The first command builds the native bridge and restores its .NET
dependencies. This is a checkout-based tool; the Python wheel alone does not contain
the native project or the linked Gojo project.

```powershell
git clone https://github.com/It3rate/Gojo.git
git clone https://github.com/It3rate/GojoQuickDraw.git
Set-Location GojoQuickDraw
python -m gojo_quickdraw doctor
python -m gojo_quickdraw convert --input examples/church.ndjson --output D:/_quickDraw/my-controls
python -m gojo_quickdraw verify --dataset D:/_quickDraw/my-controls
python -m gojo_quickdraw review --dataset D:/_quickDraw/my-controls --output D:/_quickDraw/my-controls.html
```

Use `--gojo-root C:/path/to/Gojo` **before** the subcommand, or set `GOJO_ROOT`,
for a different checkout. `doctor` reports the actual revision, content fingerprint,
and whether the revision matches the lock. A mismatch is recorded, not automatically
fixed by resetting your checkout. For exact reproduction, prepare a separate Gojo
checkout at the commit in the lock.

## Desktop app

On Windows, double-click **`OpenQuickDraw.cmd`**. It builds and opens QuickDraw
Studio. Choose a source file or folder, choose categories from the toggle-button
dialog, create a collection, and use
**Convert → verify → review** to run the main workflow. The app remembers your
paths and provides drawing previews, archive verification, on-demand drawing
steps, machine probes, progress details, and a Stop button.

The sample count is a total spread across the selected categories. Generation saves
one dataset per category folder. Inspection discovers actual generated folders and
supports category/source order, most recent, and random selection. Existing single
datasets remain readable. The Evidence tab pages through every record and has
**Full analysis → review** for an existing collection. See the [desktop guide](docs/desktop.md).

After conversion or analysis completes, the app automatically rebuilds both the
inspector and the input/final gallery for that output collection. **Refresh both
reviews** rebuilds them for the selected generated folder without processing the
source again. The entry paths remain stable. Reload an already open browser tab
to see updated files; the pages do not watch the filesystem or reload themselves.

Full church analysis also creates **Area estimates over original**: an ideal
building rectangle, separate tower rectangles, triangular or domed roofs,
independent crosses and optional rectangular or arched openings. Several towers, roofs and
crosses can coexist. Tower dividers can be implied and a narrow roof can keep its
own position. Choose this view in the inspector's **Read** or the gallery's
**Result** selector. Pale fills and dotted outlines stay behind the original ink.
See [church area estimates](CHURCH_LAYOUT_ESTIMATES.md) for the placement rules,
explicit assumptions and current limitations.

Full analysis also adds **Source ↔ shape mapping** to both selectors. Boundaries
can share whole or partial source intervals; inferred edges keep endpoint or
construction references without claiming drawn ink. Rose marks unresolved source,
and ochre marks shared support. Select a boundary or source range to inspect the
associations. Roofs and towers are optional, including flat-topped towers with a
cross above them. See [boundary mapping](CHURCH_BOUNDARY_MAPPING.md).

**Church cleanup (final shape stage)** refines those areas into a more regular
construction: centered front peaks, prominent crosses, paired towers and contained
openings. Wall sections meet towers without overlapping their interiors; crosses
on a facade keep their placement instead of being moved onto a roof tip.
Outlined/open crosses and oblique roof bands have additional detection
reads. Crosses attached to peaks follow their revised peaks; the crossbar stays
above halfway. Continuous foreground walls can remain whole below an upper tower.
Doors and windows have a local clearance check; optional additions are light gray
and dotted, while drawing-supported openings keep their role colors. Unmet
clearance and mirror expectations are visible review checks. The original stays
faintly visible. The gallery defaults to this stage when available. Use a fresh
analysis output to apply the new pass to older collections. See
[shape cleanup and its connected evidence](CHURCH_SHAPE_CLEANUP.md).

Open `GojoQuickDraw.slnx` in Visual Studio and set `GojoQuickDraw.Desktop` as the
startup project to run the UI there. The solution includes the desktop app and
tests, native converter and tests, linked Gojo dependencies, Python source files,
documentation, and examples. It expects Gojo at `../Gojo`, matching the checkout
layout above. Python files appear as solution items.

## Generate category samples

```powershell
python -m gojo_quickdraw sample --input D:/_quickDraw/quickdraw --category angel --category church --category frog --limit 90 --seed 42 --output D:/_quickDraw/samples-v1
python -m gojo_quickdraw verify --dataset D:/_quickDraw/samples-v1
python -m gojo_quickdraw review --dataset D:/_quickDraw/samples-v1 --limit 50 --order random --seed 123 --output D:/_quickDraw/samples-review.html
```

Each filename supplies its category. A file input can also select sibling category
files. Alternate `full_raw_` copies are grouped with their category and a canonical
file is preferred. Categories are balanced in randomized rounds, then records are
sampled uniformly without replacement within each category. The count is a total,
not a per-category count. A saved byte-offset index avoids favoring long drawings
and speeds later samples. The first index pass reads the required category files;
it does not convert every drawing. Gzip inputs also work but need decompression.

Interrupted samples resume their saved selection. A completed run with no explicit
seed starts a new random selection next time. An explicit seed reproduces the same
sample and reuses verified records. `--all-records` explicitly processes every record
in the selected category files. See [storage](docs/storage.md) for collection layout.

## Legacy sequential conversion

```powershell
python -m gojo_quickdraw convert --input D:/_quickDraw/quickdraw --category church --limit 100 --output D:/_quickDraw/church-evidence-v1
python -m gojo_quickdraw verify --dataset D:/_quickDraw/church-evidence-v1
python -m gojo_quickdraw review --dataset D:/_quickDraw/church-evidence-v1 --output D:/_quickDraw/church-evidence-v1.html --limit 50
```

- Input can be one file or a folder of `.ndjson`, `.jsonl`, or their `.gz` forms.
  Add `--recursive` for subfolders. Raw XY/time and simplified XY strokes are accepted.
- `--category` filters the source prompt, falling back to the filename when needed.
  It can be repeated. It is weak context, not a recognized semantic truth.
- `--limit` counts selected input records, including records already present. Repeat
  the same command to resume. Raise the limit to grow a pilot. Omit it deliberately
  when ready to process all matching records.
- Records stream through one native worker. A disk-backed index keeps corpus-scale
  bookkeeping out of memory. Each drawing is still processed as a complete record.
- Invalid records are retained under `errors/`; subsequent records continue. The
  command exits with status 1 when selected errors remain. Use `--retry-errors` to
  retry them. Command/configuration failures return status 2.
- Successful records are reused only after artifact hashes pass. The same output
  accepts only the same converter content, linked Gojo source, and configuration.
  Use a new output after changes. A writer lock prevents simultaneous converters
  from writing the same dataset.
- Default capacities are 20,000 samples and 256 fitted intervals per drawing.
  Exceeding them is an explicit retained error, not silent truncation. Raise
  `--max-points` or `--max-spans` in a new dataset if needed.

Keep output outside the scanned input folder. Baseline evidence can be expensive:
the first five real drawings produced roughly 0.4–1.3 MB of compressed native
structure each. Benchmark a larger pilot before scheduling a full corpus. A compact
training projection is a later step; it must preserve correspondence to this archive.

## Inspect and derive

The offline HTML inspector shows original strokes, fitted intervals, stroke order,
line/joint/symmetry annotations, and optional proposed parts and ground. Select an
annotation to inspect its supporting intervals. Seeded proposals are visibly qualified;
they are never silently painted into the acquired drawing.

The `review --limit` value is now **drawings per page**, not a collection cutoff.
Every successful record is accessible through First/Previous/Next/Last and a page
selector. Only one page's records are embedded in each offline document. Use
`--sample-only` for the older single-page limited sample. The desktop reuses
`<selected-folder>-runs/inspector/evidence.html` instead of creating a new review
entry for every run. Copy its adjacent `evidence-pages` folder when sharing it.
The selected **Read** view is carried in the URL across page changes, drawing
changes, and reloads. Selecting step 7 once keeps the synthesized view while browsing.

**Input / final gallery** in the desktop Evidence tab builds a compact overview:
12 input/result pairs per page, three pairs across and four rows. Each pair opens
the corresponding inspector page at its final interpretation. It reads existing
analysis; it does not rerun synthesis. On the command line, `refresh` builds both
views in the correct order and updates the exact drawing links:

```powershell
python -m gojo_quickdraw refresh --dataset D:/_quickDraw/samples-analysis
python -m gojo_quickdraw refresh --dataset D:/_quickDraw/samples-analysis --output D:/_quickDraw/custom-review --page-size 50 --gallery-page-size 12 --order random --seed 123
```

The default directory is `<selected-folder>-runs/inspector`, with stable
`evidence.html` and `comparison.html` entries. `refresh.json` records the source
collection journals, page settings, and refresh time. Refresh reads current saved
results; it does not rerun fitting or interpretation. To apply changed analysis
heuristics, analyze into a new output collection and refresh that collection.
The individual `review` and `gallery` commands remain available.

Add `--refresh` to command-line `convert`, `sample`, or `analyze` to rebuild both
views after records are converted or reused. This is optional for CLI jobs and
automatic in the desktop workflow. An empty or entirely failed invocation skips
refresh; successful records in a partly failed batch can still be reviewed.

Copy `comparison.html` with its `comparison-pages` folder for offline viewing;
include the detailed inspector and its page folder to retain the inspection links.
Gallery thumbnails show acquired strokes and the last cached authored proposal,
with retained detail in faint gray. Each pair shares a frame expanded to include
both drawings. Records without an interpretation say so rather than substituting
their fitted source. A small `evidence.pages.json` map records actual inspector
membership; stale or absent maps do not produce potentially wrong drawing links.

## Analyze already converted drawings

```powershell
python -m gojo_quickdraw analyze --dataset D:/_quickDraw/gojo-quickdraw-evidence/church1000 --output D:/_quickDraw/gojo-quickdraw-evidence/church1000-objects --workers 4 --refresh
```

This reads every successful converted record, recursively, without reopening or
sampling the raw library. Add `--limit N` for a smaller pilot. Outputs retain
category subfolders and native source-linked opinion archives. Identical completed
items resume after hash checks; interruptions preserve completed records, and
`--retry-errors` retries retained failures. Work is bounded to 1–8 workers and a
small queue. Use a new output folder after changing the analysis implementation.

Use repeated `--key-id` arguments for a specific reviewed subset. Selected keys
must exist in the input collection; a limit applies after this filter. Replace
the example keys below with source keys shown in your inspector:

```powershell
python -m gojo_quickdraw analyze --dataset D:/_quickDraw/gojo-quickdraw-evidence/church1000 --output D:/_quickDraw/church-reviewed-pilot --key-id FIRST_SOURCE_KEY --key-id SECOND_SOURCE_KEY --refresh
```

The pass runs all three source-annotation tolerances and, for churches, the existing
seven authored interpretation proposals: continuity, primitive fitting, relational
regularization, joint convergence, later regularization, and view-aware synthesis.
Other categories get generic evidence without a borrowed church template. The
inspector's **Read** selector compares these proposals and the source annotations.
Proposal previews are explicitly separate from executed native construction rungs;
**Drawing steps** still performs that independent native execution on demand.

Church analysis also proposes local wall/building scopes, roofs, steeples, crosses,
doors, windows and unknown groups. It can group contours across several strokes,
retain separate copies, and keep overlapping role/parent alternatives. The
inspector's **Church object candidates** panel selects their source support and
relationships; **Object colors** in the gallery follows that support into the
cached final drawing. Gray means unknown or conflicting direct membership.
These are authored candidates, not a trained classifier, and do not yet constrain
the seven synthesis states. See [church object heuristics](CHURCH_OBJECT_HEURISTICS.md)
for the implemented scope, known failures and suggested review/training budgets.

Additional opinions include trace and traversal reads, contour/part grouping,
rectangle/triangle candidates, dependent corner/join expectations, separate part
instances, roof/wall attachment, and a revisable container-carry default. Algorithms,
heuristics, and later small models use one request/response contract. Model requests
receive finite profile controls, comparisons, expected geometry, earlier claims and
their frame; reference IDs are correspondence, not training features. These are
unlearned hypotheses with separate support/opposition, not a trained arbiter.

Ordered endpoints are explicit on every finite segment/curve. Enable **Start / end
and direction** for markers and arrows; selecting a fitted line exposes its source
sample interval and endpoint times when available. Raw XYT records retain point
time; simplified XY records do not acquire synthetic timing.

```powershell
python -m gojo_quickdraw stages --dataset D:/_quickDraw/church-evidence-v1 --key-id SOURCE_KEY --output D:/_quickDraw/church-stages-v1
python -m gojo_quickdraw probe --dataset D:/_quickDraw/church-evidence-v1 --key-id SOURCE_KEY --output D:/_quickDraw/church-probe-v1.json
python -m gojo_quickdraw probe --example --output D:/_quickDraw/window-machine-example.json
```

`stages` runs the existing seven-stage church interpretation/synthesis machinery,
creates a portable review at `review/index.html`, and records native output diagnostics.
It requires an explicit source key or `--limit N`. Its output must be separate and new
or empty. The saved baseline never changes. Regeneration currently uses the verified
adapter cache alongside its native archive; it is not yet an archive-only regeneration API.

`probe` compares conditional machine responses, including mirror versus translation
and moving a contained part versus moving or enlarging its proposed container. It
reports the individual remaining discrepancies and preserves ties. These are finite
adapter experiments, not native tension settlement or evidence of actual object motion.

## What is implemented

| Layer | Current behavior |
| --- | --- |
| Acquisition | Exact original record text, native sample/stroke order, coordinates and optional timestamps; empty drawings stay empty. |
| Fitted source | Source-supported line/curve intervals and acquisition-ordered native construction. |
| Annotations | Existing Gojo line, junction, continuity, repetition, and symmetry observations, with supporting profiles and finite evidence. |
| Context | Bounded church-body, roof/steeple, cross, opening, containment, and placement candidates; weak corners and inferred ground; competing view/support/scale assumptions. |
| Native storage | Independently restorable connected Gojo structure, exact acquisition, source witnesses, provisional scaffolds, open slots, and relative-bound observations. |
| Interpretation | Existing authored church stages, generated only on request, with source correspondence and diagnostics. |
| Machine interventions | Non-mutating, explicitly conditioned geometry probes with alternative responses and unresolved ties. |
| Full collection analysis | Resumable category outputs, all annotation modes, authored church proposal states, and source-linked native opinion bundles. |
| Dependent opinions | Earlier claims and actual expected-site references, ordinal support/opposition, provenance and bounded capacity. No learned arbitration. |

Some contextual meanings remain verbal proposals in provenance. A `window` label or
`gravity` description does not implement its law. Bounding-frame clearance does not
prove filled-area containment. Page-down is not an inferred world gravity vector.
Weights are authored evidence settings, not learned or calibrated probabilities.

Fresh-source testing found that late church synthesis can produce an implausible
rounded silhouette, including geometry outside the review frame. The runner reports
that condition instead of promoting a successful execution to conceptual correctness.
Review generated interpretations before using them as supervision.

See [storage and evidence](docs/storage.md), [design and training direction](docs/design.md),
the root [evidence inventory](EVIDENCE_INVENTORY.md), and [verification](docs/verification.md). The bundled examples are authored controls;
real QuickDraw data and large generated datasets stay outside this repository.

## Verify changes

```powershell
python -m unittest discover -s tests -v
dotnet test GojoQuickDraw.slnx --verbosity quiet
dotnet test ../Gojo/Gojo.slnx --verbosity quiet
```

Also convert and independently verify a small real-data pilot. Optional browser
verification is in `tests/check_review.cjs`; it uses a separately installed Playwright
and Chromium-compatible browser, not a converter dependency.
