# QuickDraw Studio

Double-click `OpenQuickDraw.cmd` on Windows. The launcher builds the desktop app,
then opens it. The initial build requires the .NET 10 SDK and package restore.
Python 3.10 or newer and the linked Gojo checkout remain the pipeline requirements.
The app can also run from Visual Studio through `GojoQuickDraw.slnx`; choose
`GojoQuickDraw.Desktop` as the startup project.

## Main workflow

1. Choose the **active dataset** at the top. **Choose dataset** opens an existing
   converter output. **New dataset** chooses a new timestamped sibling destination.
   If the existing 25-drawing church pilot is available, the app initially loads it.
2. In **Convert**, pick an input file or folder, category filter, and drawing limit.
   Defaults keep a run small. **Process every matching drawing** explicitly removes
   the conversion limit. Fitting capacities and retry settings are under the
   expandable options. Existing dataset fitting settings load with the dataset.
3. Click **Convert → verify → review** to convert or resume, independently verify
   native archives, and build the visual review. Individual conversion and review
   buttons are also available. Reviews open automatically unless disabled.

The **Evidence** tab lists up to 1,000 drawing identities and displays original
strokes beside fitted intervals for the selection. These previews read the saved
observation cache. **Verify archives** checks hashes and native restoration. The
full browser review provides the detailed line, joint, symmetry, and hypothesis
annotations. Verification covers the whole dataset by default; unchecking that
option uses the review/verification drawing limit.

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
church-pilot/
church-pilot-runs/
  <timestamp>/evidence.html
  <timestamp>/stages/...
  <timestamp>/machine-probes.json
```

## Progress and stopping

The activity panel reports the current step, completion counts, errors and warnings.
Expand **Run details** for worker output. Configuration is disabled during a run;
**Stop** terminates the worker and its children. Completed baseline records remain
available. Resume conversion using the same input and settings; the existing
converter validates artifacts and repairs an interrupted final journal entry.
Closing the app during a run first stops the worker.

**Setup** selects Python and the linked Gojo checkout. **Check setup** builds the
native bridge and reports whether its revision matches the tested dependency.
Workspace paths and filter/limit preferences are stored in
`%LOCALAPPDATA%/GojoQuickDraw/desktop-settings.json`.

## Developer verification

```powershell
dotnet test GojoQuickDraw.slnx
python -m unittest discover -s tests
```

The desktop tests check literal argument transport, bounded versus whole-corpus
selection, independent output locations, latest journal status, incomplete journal
refusal, retained-error results, and stopping a worker's child process.

An explicit integration harness drives the real window and real CLI, saves rendered
screenshots, and checks conversion, verification, reviews, stages, probes, cancellation,
and restarting. Supply three nonempty church records and a new output directory:

```powershell
dotnet run --project desktop --configuration Release -- --smoke --qa-input D:/path/three-churches.ndjson --qa-output D:/path/new-qa-run
```

The harness avoids saving workspace preferences. Its `summary.json` records results;
it creates all temporary baseline and derived outputs inside the requested QA area.

The [initial desktop checkpoint](../reports/desktop-2026-10-25.json) passed this full
workflow on three real churches, including seven stages for the selected drawing,
both probe modes, stopping verification without changing archives, and restarting
the worker. Screenshots were inspected at 1180×980 and 920×760. Five desktop tests,
nine native tests, 45 Python tests, and all 5,407 linked Gojo tests passed.
