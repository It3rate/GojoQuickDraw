# QuickDraw Studio

Double-click `OpenQuickDraw.cmd`. The same app creates datasets and opens existing
ones. It remembers the last folder and count. You can also pass a folder directly:

```powershell
.\OpenQuickDraw.cmd D:\_quickDraw\data\church3000
```

The launcher builds the desktop application with the .NET 10 SDK. Conversion and
analysis require Python 3.10+ and the linked Gojo checkout. The built-in viewer
reads saved datasets directly, without Python or a generated HTML inspector.

## View an existing dataset

1. Click **Open folder**, or paste a path into **Dataset folder** and click **Load**.
   Choose a collection root, a category folder, or an older single dataset.
2. **View drawings** shows original/result pairs, 12 per page by default. Choose
   a generated category or all categories; subfolders are discovered automatically.
3. Use First, Previous, Next, Last or the page number to reach every drawing.
   Choose recorded order, most recent or a stable random order. **New random
   sample** changes the browsing order without converting anything.
4. Click a pair to enlarge it. **Show beside original** selects fitted lines,
   area estimates or final church shapes. The choice survives page changes.

**Best available result** uses saved final shapes, then saved area estimates,
then fitted lines. A specifically requested layer that is absent is labeled as
missing. Faint ink remains the original. Optional added openings are gray and
dotted; drawing-supported openings retain their role colors.

Only the visible page's evidence files are loaded. A cached projection of the
index provides page counts; there is no need to load all drawings or generate
thousands of HTML pages. Refresh folder rereads changes. Committed records from
stopped runs remain available. An incomplete last index entry is skipped with
a visible notice, while the original journal is left intact. A malformed
committed entry remains an explicit read error.

## Convert drawings

1. Use **New dataset** to choose a parent folder for a fresh destination, or edit
   the shared Dataset folder path. Existing outputs can be reused with compatible
   settings; existing drawings are retained.
2. In **Convert drawings**, choose the QuickDraw source folder or category file.
3. Choose categories from the toggle-button dialog. Search, Select all and Clear
   all operate on the static 345-category catalog.
4. Set **Total drawing count** and click **Convert up to N drawings**.

The count always limits the selected records across all chosen categories; it is
not a count per category. The old “Process every matching drawing” control has
been removed. Even the desktop command builder's legacy all-records setting
cannot bypass the count. Whole-corpus conversion remains available explicitly
through the command line's `sample --all-records` option.

Drawings are randomly sampled across categories, then stored in category
subfolders. An optional fixed seed repeats a selection. Interrupted selections
resume with the same settings. Indexing the source file may read many records;
only the selected drawings are converted. The existing output can contain more
drawings than a new request; use a new dataset for a fresh bounded set.

Conversion saves original strokes and fitted evidence. Results open in **View
drawings** after completion or Stop. It does not build browser reviews or silently
start a second analysis pass.

## Create final shapes

In the viewer, expand **Create final shapes from converted drawings**. Set the
maximum number to analyze, then start the pass. It processes at most that many
saved drawings from the beginning of the chosen category scope. The count is
separate from the conversion count and is always enforced.

The current full shape rules are church-specific. This adds area proposals,
objects, line mappings, interpretation steps and the final cleanup to a separate
analysis dataset, then opens it in the same viewer. The baseline stays available
in its original folder. These are authored proposals, not learned labels.

Analysis options expose workers, retry and output location. The default output
is beside the source dataset. Use a fresh output path after changing analysis
code; interrupted compatible analyses can resume their saved output.

## Advanced tools and settings

The main workflows are View and Convert. Optional tools are grouped below them:

- **Verify saved archives** checks hashes and independently restores native Gojo
  evidence. It defaults to a limited check; checking the whole dataset is explicit.
- **Export browser inspector + gallery** builds the portable detailed inspector
  and comparison pages. This is an optional export, rather than a viewing prerequisite.
- **Drawing steps** executes the selected church's construction stages.
- **Machine probes** tests placement and containment assumptions; it does not
  convert drawings or train priors.
- **Convert drawings → Tool settings** selects Python and the linked Gojo checkout.

Run details contains worker output. Stop terminates the worker and its children.
Completed records remain available. Settings are saved under
`%LOCALAPPDATA%/GojoQuickDraw/desktop-settings.json`.

## Developer verification

```powershell
dotnet test GojoQuickDraw.slnx
python -m unittest discover -s tests
```

The read-only startup audit opens an existing folder, pages to its end and checks
the viewer while Python and Gojo tool paths are unavailable:

```powershell
dotnet run --project desktop -- --startup-smoke --dataset D:/_quickDraw/data/church3000 --qa-output D:/path/new-viewer-audit
```

The bounded integration audit converts nine real drawings across angel, church
and frog, analyzes two churches, checks native gallery rendering and enlargement,
stopping/restarting, unchanged source archives and explicit browser export:

```powershell
dotnet run --project desktop -- --smoke --qa-input D:/_quickDraw/quickdraw --qa-output D:/path/new-workflow-audit
```

Both audits write screenshots and a summary into a new requested QA directory;
neither saves the user's workspace preferences. Generated test data stays outside
the repository. The [26 October folder-viewer checkpoint](../reports/studio-folder-viewer-2026-10-26.json)
records the 45,208-drawing startup check and the bounded conversion/analysis run.
