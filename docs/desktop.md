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

Drawing identities and original/fitted previews are paged. First/Previous/Next/Last
and a page number reach every record. The page size also sets the offline review's
page size; it does not cut the collection down to its first 50 records.
The same scope/order applies to visual reviews and limited verification.
**Verify archives** checks hashes and native
restoration. **Verify the entire dataset** covers every record in the selected scope
regardless of browsing order. The full browser review includes detailed line,
joint, symmetry, and provisional-part annotations.

**Full analysis → review** reads every converted drawing in the selected folder and
subfolders. The options lower in Evidence can restrict its count, choose 1–8 parallel
workers, retry errors, or set an output folder. The default output is a separate
`<selected-folder>-analysis` collection. Completed records resume after hash checks.
The app selects that analysis collection when ready. Running analysis again from
its collection root resumes the original baseline rather than making another
generation of derived drawings. Use a new output after changing the implementation.

The latest full pass also supplies **Church cleanup (final shape stage)**. It
keeps front peaks symmetric, moves crosses with their support, preserves side
tower/roof arrangements, and labels optional added openings. Select it in the
inspector's Read list or the gallery's Result list. Faint ink remains the original;
dashed shapes/ground are expectation-based. Older analyses need a fresh output
and Full analysis to acquire this stage; refreshing reviews alone cannot add it.

In the browser, **Read** selects fitted source, looser/stronger source relations,
and the seven church proposal states. The latter are marked as authored previews;
Drawing steps independently executes native programs. The evidence list includes
dependent opinions and proposed object groups with support, opposition, providers,
and links to earlier claims. Direction markers and endpoint details expose start,
end, acquisition indices, and point times where supplied.
The selected Read mode stays selected through First/Previous/Next/Last, page
selection, drawing changes, and browser refresh. Links can select it directly
using `?view=step%3A6` for step 7. If a drawing has no such step, its fitted source
is shown and the requested step remains available for later drawings.

**Input / final gallery** builds the detailed review and a smaller comparison
gallery without running analysis again. Each page shows 12 pairs: three pairs
across and four rows on a desktop, with fewer columns on small screens. Click a
pair to open that exact drawing's final interpretation in the detailed inspector.
The retained-detail toggle hides or shows the faint carried lines. Its choice
also survives gallery page changes. Unanalyzed drawings explicitly have no final
interpretation; run Full analysis first when those are needed.

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

The evidence inspector has a stable entry beside the selected collection; stage
runs and probes keep separate timestamped folders:

```text
samples/
  angel/...
  church/...
samples-runs/
  inspector/evidence.html
  inspector/evidence-pages/<generation>/page-00002.html
  inspector/evidence.pages.json
  inspector/comparison.html
  inspector/comparison-pages/<generation>/page-00002.html
  <timestamp>/stages/...
  <timestamp>/machine-probes.json
samples-analysis/
  church/...
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

The executable accepts `--dataset PATH` to open an existing generated collection
directly. A read-only startup audit checks initial loading, paging and access to
an existing inspector without changing workspace preferences:

```powershell
dotnet run --project desktop -- --startup-smoke --dataset D:/_quickDraw/gojo-quickdraw-evidence/church1000-analysis --qa-output D:/path/new-startup-audit
```

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
