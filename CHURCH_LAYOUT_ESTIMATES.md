# Church area estimates

This experiment places ideal regions over unchanged acquired ink. It asks where
the main building, roof and openings could be, rather than turning each acquired
line into a straightened object boundary. It is an authored placement proposal,
not a trained classifier or a calibrated posterior.

Run **Full analysis → review** into a new derived collection. In the inspector,
choose **Read → Area estimates over original**. In the comparison gallery,
choose **Result → Area estimates over original**. Both views retain that choice
through paging. An original is shown beside the overlay and again, as solid ink,
on top of its pale fills. Dotted borders are proposed geometry. Hover or select
a region for its support, assumptions, placement costs and construction references.

## Current construction

| Element | Placement evidence | Authored relationship |
| --- | --- | --- |
| Ground level | Lower ink extent and bottoms of long lower uprights | One horizontal level, whether or not a baseline was drawn |
| Building rectangle | Broad lower extent, upright regions, upper horizontal levels | Bottom corners share the proposed ground sites |
| Roof triangle | Upper ink extent and approximate peak region, excluding detected cross strokes | Its base shares the building's two upper sites |
| Christian cross | An upper crossing candidate, if available | Place above the roof or inside its triangle; ideal proportions |
| Cross level | The proposed cross's arm intersection | Horizontal reference through that actual proposed site |
| Door rectangles | Small, substantially traced regions near ground | Bottom sites share ground; rectangles remain inside the building |
| Window rectangles | Smaller traced regions above ground | Inside the building and smaller than doors |

One building and one roof are deliberately used in this first pass. Up to two
doors and eight windows can be proposed. Crosses have a longer lower stem and
a transverse arm at 32% of height. An absent detected cross gets a **prior-only**
proposal, not a detection claim. Missing openings are left absent. If no door is
proposed, window sizing uses an explicit latent door-size default; it does not
create a visible door. Empty or essentially one-dimensional input leaves the
layout open.

## Area search and weighting

The regional search uses arclength-weighted source samples, not raw sample density
as confidence. At most 2,048 regional samples and 16 lower uprights are used.
Sample aggregation retains total arclength and records when resolution was reduced.
The raw acquisition remains complete. Optional opening groups combine small strokes
with nearby endpoints and require enough travel to suggest an extended contour;
an individual straight line does not create an opening.

The main rectangle is chosen from a finite set of regional widths and top levels.
Its cost has separately recorded terms:

- Lower region extent: departure from the lower ink's broad horizontal coverage.
- Body height prior: preference for a moderate wall height within the scene.
- Left and right boundary regions: proximity to long lower uprights and their tops.
- Upper level evidence: proximity to a sufficiently long horizontal interval.

Each term records its value, named basis and allowance. Lower cost chooses a
proposal within this limited search, not a probability that a church part exists.
The two next distinct rectangle candidates are retained, where available, with
their own geometry and costs. The roof, cross and openings then use explicit
relative placement rules. These placements do not count as new observed support
for the very premises that created them.

## Connected evidence and preservation

The `layout` packet is a display summary. Corresponding `context.parts` and
`context.relations` are imported into native Gojo witnesses. A part selects actual
source-profile witnesses and earlier construction parts. Names such as "roof"
remain provisional verbal meaning; ordered edges, proposed shared sites, selected
participants, numerical comparisons and unresolved slots are native structure.

`geometry.siteSources` explicitly selects a point of an earlier participating
part. Thus the roof really shares the proposed wall's top sites and the wall/doors
share proposed ground sites after capture and restore. Equal coordinates alone
do not create identity. These sites do not alias acquired samples, and source
gaps are not silently closed. Missing, forward, unselected or disagreeing site
references are rejected.

The source JSON, point order/times, fitted intervals and baseline program remain
unchanged. The old seven synthesis states remain available for comparison. This
new layout is neither another pass that mutates those lines nor a native physical
settlement engine. It is an explicit imported construction hypothesis with visible
support and disagreement, ready for later changes to the proposing mechanism.

## What this first pass does not establish

- A single rectangle and triangle compress side views, towers and several roofs.
  This experiment does not first select an appropriate view-specific machine.
- An outlined cross can escape the existing crossing detector. A prior-only cross
  may consequently appear above a real cross drawn elsewhere.
- Writing, scribbles and decoration can propose false openings. Parts embedded in
  a long mixed-purpose stroke can be missed by the small-region grouping.
- A plausible construction can disagree substantially with its source. The original
  remains visible so that disagreement can be judged, not hidden by a fit score.
- The 24 drawings used for review are an existing development set, not an unseen
  evaluation set. Geometry and archive checks do not measure semantic accuracy.

Next useful feedback is whether the main **areas and relationships** are plausible,
which optional openings are missing or spurious, and when this one-roof premise
should be replaced by several connected regions. Learned weighting can later
propose these same connected structures; no trained model is needed for this trial.

## Refresh and reproduce

```powershell
python -m gojo_quickdraw analyze --dataset D:/_quickDraw/gojo-quickdraw-evidence/church1000 --output D:/_quickDraw/church-layout-next --limit 24 --workers 3 --refresh
python -m gojo_quickdraw refresh --dataset D:/_quickDraw/church-layout-next
```

Omit `--limit` deliberately to analyze the whole selected collection. Use repeated
`--key-id` options for a reviewed subset. Processing refreshes both reviews when
requested by the CLI and automatically in the desktop workflow. A refresh only
rebuilds presentation from saved evidence; changing placement code requires
analysis into a new output with its own implementation fingerprint. Reload open
browser tabs to read the updated files.
