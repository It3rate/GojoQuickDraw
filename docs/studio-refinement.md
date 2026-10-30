# Connected refinement in QuickDraw Studio

Studio now offers both routes. **Convert drawings** / **Complete pipeline**
retain the existing ten saved stages. Their area proposal already includes
the second residual-ink pass for missed pointed towers and crosses. Existing
saved records are not silently changed when processing code changes.

To run the newer connected experiment on an existing folder:

1. Open the dataset and select its category folder and inspection order.
2. Under the gallery, expand **Connected church refinement — compare the newer method**.
3. Choose the maximum church count (default 12) and settlement passes (default 8).
4. Leave **Keep full proof history** unchecked for a current native checkpoint
   and all visual passes. Check it only when complete historical audit is useful.
5. Click **Run connected refinement**, then **Open comparison**.

The result folder is separate from the source. A blank result field creates a
new sibling folder under `<dataset>-refinements`; the selected path appears in
the field immediately, including if the run is stopped. Run again with that
path and the same settings to reuse verified completed results. Use a new
folder for changed processing code, inputs, model or pass budget.
**Open comparison** refreshes the page from already completed outputs without
running settlement, so it also works after Stop. Small runs refresh as drawings
finish; larger runs refresh at growing intervals to avoid repeatedly embedding
the whole cohort.

This route currently selects **game-recognized churches only**, explicitly
shown in the controls. Other categories and unrecognized drawings are skipped
before the requested count is applied. The selected category-folder scope and
inspection order are used. Equal acquisitions appearing in several retained
records count once. It does not use the current page number as an offset.

The runtime consumes saved fitted source intervals, so it does not reparse the
raw QuickDraw corpus. It rebuilds current source-based proposals, compiles the
same connected/CAD/recovery relations used in the latest church comparison,
and runs the frozen local neighborhood providers. No training occurs here.
The default is eight outer passes with two local response rounds per pass,
48 local candidates, and the retained neighborhood-closure provider bundle.

Saved Studio area and cleanup panels are **comparison only**. They never enter
the native source packet or supply final settlement target coordinates. If a
dataset does not contain those saved stages, the corresponding reference panel
is blank. All new motion, topology and error response use the existing native
connected runtime. An independently restored `.gojo.bin.gz` and its read view
are retained per drawing, alongside the source snapshot, proposals and model
bytes. Receipts hash the archive, view, source request, proposal, saved comparison
and provider before resume can reuse them.

The default checkpoint retains executable current machines, shared bindings,
source intervals and current error laws. It omits old proposal/response history
and does not resume the adaptive topology controller. Fresh local numeric
requests still execute on restored machines. Visual passes remain inspectable.
The optional full history is much larger. Separate timings and the native
archive are available under **Processing and saved native state** in the review.
See [the runtime explanation](native-runtime.md) for the native calculation boundary.

This remains an explicit experiment. It is not silently appended
to every conversion. The comparison is an offline browser page opened from
Studio; the original ten stages remain in Studio's normal viewer. No website
publication occurs.

CLI equivalent:

```powershell
python -m gojo_quickdraw.church_dataset --dataset D:/_quickDraw/data/church3000 --output D:/_quickDraw/data/church3000-connected --limit 12 --passes 8
```

The one-drawing integration pilot at
`D:/_quickDraw/data/studio-refinement-2026-10-30-v2` reused the existing saved
sample `6226189452574720`. All nine connected saved stages were exactly equal
to that acquisition's previous residual-recovery study stages. Source/model
hashes matched, the native archive independently restored, and resume reused
the receipt without rerunning native settlement. This checks integration, not
general recognition quality.
