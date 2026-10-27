# QuickDraw Studio

Double-click `OpenQuickDraw.cmd`. The same app creates datasets, opens existing
ones and trains the local pilot model. It remembers the last drawing folder
and count. You can also pass a folder directly:

```powershell
.\OpenQuickDraw.cmd D:\_quickDraw\data\church3000
```

The UI is C# with Avalonia and Skia rendering; it is not an HTML application.
Python orchestrates processing and the C# Gojo bridge builds native evidence.
The launcher builds the desktop application with the .NET 10 SDK. Conversion
and training require Python 3.10+ and the linked Gojo checkout. Viewing saved
drawings requires neither Python nor a generated HTML inspector.

## Train a local model

**Train model** is a separate workflow from conversion. Choose a converted
drawings folder with the shared Dataset controls or **Choose drawings folder**,
set the maximum source count, then **Train joint model**. It uses the current
category/recognition filters and a read-only snapshot of completed records.
Controlled examples and weak earlier source/final mappings supply the initial
targets. Results distinguish held-out controls from unreviewed drawing references.

**Open patch review** shows source runs against their highest-ranked connected
construction, including unresolved alternatives, faint drawing context, and
filters for controls/reference/unlabelled examples. **Open saved model run**
loads a previous result. Model folders retain the weights and native evidence.
This pilot does not automatically alter the conversion output. See
[model scope, measured results and limitations](local-joint-model.md).

## View an existing dataset

1. Expand the **Dataset** button in the top bar. Click **Open folder**, or paste
   a path into **Dataset folder** and click **Load**. Hide the panel when finished.
   Choose a collection root, a category folder, or an older single dataset.
2. **View drawings** shows original/result pairs, 12 per page by default. Choose
   a generated category or all categories; subfolders are discovered automatically.
3. Previous and Next stay beside the page count. **Filters & pages** expands
   category, order, page size, page jump and First/Last controls.
   Choose recorded order, most recent or a stable random order. **New random
   sample** changes the browsing order without converting anything.
4. Click a pair to enlarge it. **Drawing stage** selects any saved stage;
   **◀ Stage / Stage ▶** moves through them. The choice survives page changes
   and reopening the app. The enlarged view also offers saved stage details.

Keyboard shortcuts work in the gallery and enlarged view:

- **Left / Right:** previous / next drawing, crossing page boundaries.
- **Shift + Left / Right:** previous / next page, preserving the page position
  when possible and keeping the selected stage.
- **Up / Down:** previous / next stage.

The enlarged view also has Previous / Next drawing buttons. Text and number
fields, and open dropdowns, retain their normal editing keys.

**Latest available stage** uses saved final shapes, then saved area estimates,
then fitted lines. A specifically requested layer that is absent is labeled as
missing. Faint ink remains the original. Optional added doors, windows and crosses
are gray and dotted; drawing-supported parts retain their role colors. Drawn
openings take precedence, including large circular windows. Missing windows are
left absent; a suggested entrance is rectangular and needs strong facade support.
Box-shaped windows with crossing interior bars retain those bars as part of each
window. Several windows may be recognized independently; the refined view centers
their dividers while the area view keeps their approximate source placement.

### Joint overlay and comparison

**Joints** shows recorded source L, T, Y, X, K and closed-walk O candidates;
unresolved reads appear as `?`. Endpoints and straight-through I reads are retained
but omitted from the overlay to reduce clutter. O describes a closed walk, not
a point junction. Source labels come from fitted evidence. Final labels are
recalculated over the displayed final edges; intermediate church stages do not
borrow final labels. Hover a marker for its evidence and possible counterpart.

Enlarge a drawing and click **Compare joints** beside the stage controls. The
comparison lists same/changed types, ambiguous matches, unmapped source/final
reads, added construction and boundary-to-source line mappings. Relative-order
checks compare above/below, left/right and approximate alignment for unique
matches. Green means the same type, ochre changed, rose ambiguous, purple unmapped
or source evidence, and gray added construction. These are not accuracy scores.

**Export relative graph** saves junction types, coarse ray directions, ordered
edge incidence, relative orders and candidate correspondences without point
coordinates. It is an experimental training read, not a complete native archive.
Authored final shapes are weak targets that need review. Older datasets derive
the comparison read-only from their saved geometry; new conversions also retain
an independently restored native comparison cache. No HTML export is required.

Only the visible page's evidence files are loaded. A cached projection of the
index provides page counts; there is no need to load all drawings or generate
thousands of HTML pages. Refresh folder rereads changes. Committed records from
stopped runs remain available. An incomplete last index entry is skipped with
a visible notice, while the original journal is left intact. A malformed
committed entry remains an explicit read error.

**Filters & pages → Game recognition** selects the game's recognized,
unrecognized or unavailable outcomes. It filters before paging, and switching
back to All shows the complete dataset. Older folders lack an index projection
of this flag: the first filtered read checks the flag in each saved source
packet and caches that metadata for the browsing session. It does not rewrite
the records or run conversion. This is the game's AI judgment, not human review.

## Convert drawings

**Game recognition** also appears in the conversion form. Selection happens
before sampling, so a target of 300 with Recognized by game means up to 300
matching acquisitions across selected categories. Other saved outcomes remain
in the dataset and do not count toward that filtered target. A reported shortfall
means the eligible source pool ran out. All remains the default; unknown flags
are not silently treated as unrecognized. Completing an interrupted raw pipeline
uses its saved recognition selection, just as it uses the saved source and seed.

1. Open an existing pipeline dataset to add drawings, or use **New dataset**
   for a separate collection. Existing converted drawings are preserved.
2. In **Convert drawings**, choose the QuickDraw source folder or category file.
3. Choose categories from the toggle-button dialog. Search, Select all and Clear
   all operate on the static 345-category catalog.
4. Set **Target drawing total** and click **Create / extend to N drawings**.

The target includes existing drawings in the selected categories. For example,
100 saved churches and a target of 300 adds up to 200 new churches. Repeating
300 resumes unfinished work or reuses completed records. A lower target removes
nothing. If only angel is selected, a target of 300 applies to angels; saved
churches remain in the folder. New additions favor the least represented selected
categories. Duplicate source identities are skipped, and source exhaustion can
leave the collection below its target. Retained failed selections occupy their
slots; **Retry retained errors** retries their saved source without adding copies.

New source files, categories, seeds and processing versions can supply additions
to the same pipeline workspace. Existing evidence, stages and native archives
are not rewritten; each processing version remains recorded. A new seed changes
future additions, not already selected drawings. To reinterpret the same drawings
with changed algorithms, use a separate completed dataset.

The count always limits the selected records across all chosen categories; it is
not a count per category. The old “Process every matching drawing” control has
been removed. Even the desktop command builder's legacy all-records setting
cannot bypass the count. Whole-corpus conversion remains available explicitly
through the command line's `sample --all-records` option.

Drawings are randomly sampled across categories, then stored in category
subfolders. The selection and seed are saved before fitting. Indexing the source
file may read many records; only the selected drawings are converted.

**Convert drawings runs the full pipeline automatically.** It saves these ten
inspectable church views:

1. Original drawing, including order and any recorded times.
2. Close fitted outlines.
3. Long strokes and whole curves.
4. Supported primitive shapes.
5. Balanced related parts.
6. Merged nearby joints.
7. Regularized shapes and supported side frames.
8. Synthesized structure.
9. Shape areas and attachments.
10. Refined church shapes.

Stages 2–8 execute as seven native construction rungs with source correspondence.
Area estimates read the fitted source and contextual evidence; final cleanup
refines those areas. They are a separate branch, not a claim that stage 9 consumes
stage 8. The saved stage dependencies make this visible. Existing algorithms and
church expectations are retained; no model is trained. Non-church categories have
the original and fitted views, with unavailable church stages labeled explicitly.

The dataset contains `acquired/<category>` and `results/<category>` plus a
`pipeline.json` progress/selection manifest. The viewer opens the result branch
once analysis begins and the acquired branch while initial conversion is pending.
Results contain compressed evidence, native Gojo archives and a `stages` bundle
per drawing. The complete stage history and read caches are retained; no HTML is
required. Stop keeps committed drawings; rerunning the same request resumes.

## Complete older converted datasets

In the viewer, expand **Complete pipeline for existing drawings**. Set the
maximum number to complete, then start the pass. It processes at most that many
saved drawings from the beginning of the chosen category scope. The count is
separate from the conversion count and is always enforced.

The current full shape rules are church-specific. This adds area proposals,
objects, line mappings, interpretation steps and the final cleanup to a separate
completed dataset, then opens it in the same viewer. The baseline stays available
in its original folder. These are authored proposals, not learned labels.

Analysis options expose workers, retry and output location. The default output
is beside the source dataset, with `-processed` appended. Opening an interrupted
pipeline workspace restores its requested count; Complete pipeline follows its
original source and current target. Growing a workspace keeps existing results
across code changes; a fresh output path is still needed to reinterpret those
same drawings with the new algorithms.

## Advanced tools and settings

**Train model** defaults to neighborhood repair plus line/curve models, with a
bounded read-only source snapshot and grouped controlled examples. The earlier
pairwise ranker remains an option. **Encoding → Neighborhood repair** uses the
saved two-provider bundle. Independent switches enable repair and line
interpretation; both unchecked retain the source graph unchanged.

The Encoding gallery shows 12 source/result pairs per page. Select a pass above
the gallery or enlarge a drawing. Its pass selector and Up/Down keys show the
actual saved intermediate graphs. Left/Right moves between drawings;
Shift+Left/Right moves by a page while retaining the pass. Hover a joint for
its connected edges and far endpoints. Local decisions expose leading
alternatives, blocked/stale proposals, source intervals and trimmed residue.
Settings collapse after results load. A saved neighborhood run can also be
opened directly with the dataset folder picker, without Python or Gojo installed.
The portable review is an optional export of the same independently restored
native graph readbacks. This experimental pass does not replace conversion's
existing church stages or automatically learn church/category rules.

The main workflows are View and Convert. Optional tools are grouped below them:

- **Verify saved archives** checks hashes and independently restores native Gojo
  evidence. It defaults to a limited check; checking the whole dataset is explicit.
- **Export browser inspector + gallery** builds the portable detailed inspector
  and comparison pages. This is an optional export, rather than a viewing prerequisite.
- **Drawing steps** optionally exports the selected church's construction as a
  separate portable review; ordinary conversion already saves these stages.
- **Machine probes** tests placement and containment assumptions; it does not
  convert drawings or train priors.
- **Convert drawings → Tool settings** selects Python and the linked Gojo checkout.

Run details contains worker output. Stop terminates the worker and its children.
Completed records remain available. Settings are saved under
`%LOCALAPPDATA%/GojoQuickDraw/desktop-settings.json`.
Status and Stop are in a compact top bar. Dataset and run details are collapsible;
only stage, joints and ordinary paging remain above the pictures. The tabs scroll
within the remaining height, with no bottom status overlay. Temporary runs and exported
reviews are grouped under `D:/_quickDraw/_work`, not loose at the data-drive root.

## Developer verification

```powershell
dotnet test GojoQuickDraw.slnx
python -m unittest discover -s tests
```

The read-only startup audit opens an existing folder, pages to its end and checks
the viewer while Python and Gojo tool paths are unavailable:

```powershell
dotnet run --project desktop -- --startup-smoke --dataset D:/_quickDraw/data/church3000 --qa-output D:/_quickDraw/_work/qa/new-viewer-audit
```

The bounded integration audit converts nine real drawings across angel, church
and frog, analyzes two churches, checks native gallery rendering, enlargement,
keyboard drawing/page/stage navigation (including a short final page),
stopping/restarting, unchanged source archives and explicit browser export:

```powershell
dotnet run --project desktop -- --smoke --qa-input D:/_quickDraw/quickdraw --qa-output D:/_quickDraw/_work/qa/new-workflow-audit
```

Both audits write screenshots and a summary into a new requested QA directory;
neither saves the user's workspace preferences. Generated test data stays outside
the repository. The [26 October folder-viewer checkpoint](../reports/studio-folder-viewer-2026-10-26.json)
records the 45,208-drawing startup check and the bounded conversion/analysis run.
