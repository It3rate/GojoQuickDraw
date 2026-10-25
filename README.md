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
datasets remain readable. See the [desktop guide](docs/desktop.md).

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

Some contextual meanings remain verbal proposals in provenance. A `window` label or
`gravity` description does not implement its law. Bounding-frame clearance does not
prove filled-area containment. Page-down is not an inferred world gravity vector.
Weights are authored evidence settings, not learned or calibrated probabilities.

Fresh-source testing found that late church synthesis can produce an implausible
rounded silhouette, including geometry outside the review frame. The runner reports
that condition instead of promoting a successful execution to conceptual correctness.
Review generated interpretations before using them as supervision.

See [storage and evidence](docs/storage.md), [design and training direction](docs/design.md),
and [verification](docs/verification.md). The bundled examples are authored controls;
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
