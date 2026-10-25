# QuickDraw Studio

Double-click `OpenQuickDraw.cmd` on Windows. The launcher builds the desktop app,
then opens it. The initial build requires the .NET 10 SDK and package restore.
Python 3.10 or newer and the linked Gojo checkout remain the pipeline requirements.
The app can also run from Visual Studio through `GojoQuickDraw.slnx`; choose
`GojoQuickDraw.Desktop` as the startup project.

## Main workflow

1. Choose the **active collection or dataset** at the top. **Choose generated folder**
   can open a collection root, one category folder, or an older single dataset.
   **New collection** chooses a new timestamped destination for category folders.
2. In **Convert**, choose the QuickDraw source library. A file input also supplies
   its parent folder for other selected categories. Click **Source categories** to
   open the static 345-category picker. Each category is a toggle button. Search
   filters the buttons; **Select all** and **Clear all** affect the entire catalog.
   Cancel preserves the previous selection. A cleared selection must be filled
   before running; it does not mean every category.
3. Set the **total sample size**. This is a total across selected categories, spread
   in randomized balanced rounds. Drawings within each category are uniformly
   sampled without replacement, including records far into the source file.
   **Process every matching drawing** explicitly removes the random sample limit.
4. Click **Convert → verify → review** to generate or resume, independently verify
   native archives, and build the visual review. Each generated category gets its
   own dataset subfolder. Reviews open automatically unless disabled.

The fitting options include an optional sampling seed. Leave it blank for a new
selection after a completed run. Interrupted runs recover their saved selection.
An explicit seed repeats the same selection, reusing verified records. The first
random sample indexes the required category files; those saved source indexes speed
later samples. Progress shows the category being indexed or converted. Gzip files
remain supported, with additional decompression work.

The **Evidence** tab discovers actual generated folders. Its **Generated folder**
selector offers one folder or **All categories**. Every scope includes subfolders.
The list shows only folders that contain generated datasets, rather than the static
source category catalog. Inspection supports:

- **Category / source order:** folders alphabetically, then saved order within each.
- **Most recent:** recently generated records first, with file-time fallback for
  older records that have no conversion timestamp.
- **Random:** a stable seeded sample/order across records in scope. **New random
  sample** changes that inspection seed, without generating or mutating drawings.

Up to 1,000 drawing identities and cached original/fitted previews are shown.
The same scope/order applies to visual reviews and limited verification; review
limits take a prefix of that selection. **Verify archives** checks hashes and native
restoration. **Verify the entire dataset** covers every record in the selected scope
regardless of browsing order. The full browser review includes detailed line,
joint, symmetry, and provisional-part annotations.

## Interpretations and probes

Choose a drawing in Evidence, then use **Drawing steps** to generate its seven
church interpretation passes. Alternatively choose the first N drawings. The
existing interpretation rules currently support churches. Generated reviews show
warnings and remain candidates for inspection.

**Machine probes** runs the selected drawing's exposed placement/containment
candidates, or the authored two-window example. The app displays each tested machine
and its remaining distance discrepancy. Equivalent responses can remain tied. A
drawing with no eligible candidates reports that explicitly. **Open last result**
opens the full JSON report for probes or the visual review for other operations.

Each review, stage run and probe goes into a new folder beside the baseline:

```text
samples/
  angel/...
  church/...
samples-runs/
  <timestamp>/evidence.html
  <timestamp>/stages/...
  <timestamp>/machine-probes.json
```

## Progress and stopping

The activity panel reports the current step, completion counts, errors and warnings.
Toggle **Run details** for worker output. Configuration is disabled during a run;
**Stop** terminates the worker and its children. Completed baseline records remain
available. Resume conversion using the same input and settings; the existing
converter validates artifacts and repairs an interrupted final journal entry.
Closing the app during a run first stops the worker.

**Setup** selects Python and the linked Gojo checkout. **Check setup** builds the
native bridge and reports whether its revision matches the tested dependency.
Workspace paths, selected categories, sampling seed, inspection order/seed, and
the sample limit are stored in
`%LOCALAPPDATA%/GojoQuickDraw/desktop-settings.json`.

## Developer verification

```powershell
dotnet test GojoQuickDraw.slnx
python -m unittest discover -s tests
```

The desktop tests check literal argument transport, explicit category selection,
separate sampling/inspection seeds, bounded versus whole-corpus selection, actual
folder discovery, record-to-dataset identity, independent output locations,
retained-error results, and stopping a worker's child process. Python tests cover
source indexing, uniform selection, saved-plan resume, hierarchical inspection,
stable random prefixes, latest journal status, and incomplete journal refusal.

An explicit integration harness drives the real window and real CLI, saves rendered
screenshots, and checks conversion, verification, reviews, stages, probes, cancellation,
and restarting. Supply a library containing angel, church and frog category files
and a new output directory; the harness samples nine real drawings:

```powershell
dotnet run --project desktop --configuration Release -- --smoke --qa-input D:/_quickDraw/quickdraw --qa-output D:/path/new-qa-run
```

The harness avoids saving workspace preferences. Its `summary.json` records results;
it creates all temporary baseline and derived outputs inside the requested QA area.
The persistent source index uses the normal local cache.

The [initial desktop checkpoint](../reports/desktop-2026-10-25.json) passed this full
workflow on three real churches, including seven stages for the selected drawing,
both probe modes, stopping verification without changing archives, and restarting
the worker. Screenshots were inspected at 1180×980 and 920×760. Five desktop tests,
nine native tests, 45 Python tests, and all 5,407 linked Gojo tests passed.

The [category-sampling checkpoint](../reports/category-sampling-2026-10-25.json)
extends that check to the picker, nine random real drawings in three category
folders, explicit-seed reuse, category filtering, most-recent ordering, random
multi-folder reviews, and selected-church stages while random browsing is active.
