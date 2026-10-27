# Church area estimates

This experiment places ideal regions over unchanged acquired ink. It asks where
the main building, roof and openings could be, rather than turning each acquired
line into a straightened object boundary. It is an authored placement proposal,
not a trained classifier or a calibrated posterior.

Run **Complete pipeline for existing drawings** into a new derived collection. In the desktop viewer,
choose **9. Shape areas and attachments**. In a portable inspector,
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
| Tower rectangle | A local cap and supporting uprights, or independent paired uprights with a top beam | Down to the main wall or ground; a roof and a lower drawn divider are optional |
| Triangular or domed roof | Paired local upper contours, excluding selected cross intervals | Base sites explicitly select the supporting tower or wall boundary where they meet |
| Christian cross | Independent upper crossings or partial upright markers | Each uses its nearby cap, flat-topped tower or main building; multiple markers are allowed |
| Cross level | The proposed cross's arm intersection | Horizontal reference through that actual proposed site |
| Rectangular or arched doors | Substantially traced head and sides near ground; rectangular/curved-head comparison | Bottom sites share ground; preserve supported size and location within the building |
| Rectangular, arched, circular or elliptical windows | Traced perimeter above ground, including gapped or multistroke outlines | Inside the building; a round window may be larger than a door |

There is still one main building envelope, with up to six local roofs, six towers,
six crosses, two doors and eight windows. These are search capacities, not expected
church counts. A tower may overlap the main building area. Distinct towers retain
their own identities; the same area can participate in both building and tower.
Crosses have a longer lower stem and a transverse arm at 32% of height.
An upright above a cap can propose a **partial-marker-prior** cross, with its missing
horizontal arm explicitly recorded. If no crossing or partial marker is found,
there is one **prior-only** cross proposal, not one invented for every roof.
Missing openings are left absent. Window size follows its own source extent;
there is no latent door-size ceiling. Empty or essentially one-dimensional input leaves the
layout open.

## Area search and weighting

The regional search uses arclength-weighted source samples, not raw sample density
as confidence. At most 2,048 regional samples and 16 lower uprights are used.
Sample aggregation retains total arclength and records when resolution was reduced.
The raw acquisition remains complete. Optional opening groups combine small strokes
with nearby endpoints and require enough travel to suggest an extended contour;
an individual straight line does not create an opening.

Policy v6 reads consecutive near-vertical intervals as a candidate wall extent,
so an upper wall interval is not lost merely because only the lower one reaches
ground. Horizontal support also accounts for width: a short door lintel should
not displace a broadly supported wall top. Two inward roof slopes with a bounded
gap can propose an extrapolated tip; the gap remains in the source and its
completion is recorded as `inferred-slope-intersection`.

Opening candidates retain selected acquired intervals, not every point from a
stroke shared with a wall. Length-sampled source-to-outline costs and perimeter
coverage compare a rectangle, arched head and ellipse. A nearly round ellipse
can propose a circle with 32 connected boundary segments. Rectangle/arch doors
can have open bases; multistroke and small-gap window boundaries are allowed.
Interior handles, dividers and spokes can remain unmatched detail. The selected
source spans, competing costs, coverage comparisons and unmapped intervals stay
inspectable. This recognizes a possible round window, not stained glass as a
material or decoration. These thresholds are authored, not calibrated confidence.

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

### Local roofs and towers (policy v2)

The cap search reads coarse monotone contour runs and fitted source intervals.
It pairs left and right sides using tip proximity, similar eave levels and enough
width/height to be a regional feature. It compares the observed coarse heights
with a triangle and half ellipse. Midpoints constrain straight sides too, so two
straight slopes do not become a dome merely because their endpoints agree.
Acquisition direction is preserved in the source; reversing a contour does not
change this shape read.

Cap costs expose tip separation, eave-level difference, profile discrepancy and
unexplained upper contour. The last term helps keep a sloping wall edge from
winning over the roof above it. Caps need some overlap with the proposed building
footprint; disconnected contours remain in `unattachedRegions` instead of becoming
attached roofs. Competing caps with substantially overlapping horizontal extents
are suppressed in this first local search. Capacity-withheld proposals remain
visible in `withheldRegions`.

A narrow cap above the wall suggests a tower reaching down to that wall. Paired
uprights extending near ground instead suggest a full-height tower. Neither case
requires a stroke along the tower's proposed lower edge: `lowerBoundary` is
`inferred-level`. The source gap and any continuous outline stay unchanged.

Broad, nearly aligned roofs prefer the wall's width. Narrow or displaced roofs
keep their own width and position. A dome uses an ideal half ellipse sampled into
32 ordered boundary intervals, plus a closing base split at its middle contact
site. This is a finite imported construction, not a new native exact-circle law.
The alternate triangle/dome costs remain available for inspection.

Crosses are placed independently of roof count. A cross between two towers can
reference the main building rather than being moved onto one tower. Each gets
its own horizontal level. These are provisional marker interpretations: the
existing crossing detector can still mistake a grid intersection for a cross.

## Connected evidence and preservation

The `layout` packet is a display summary. Corresponding `context.parts` and
`context.relations` are imported into native Gojo witnesses. A part selects actual
source-profile witnesses and earlier construction parts. Names such as "roof"
remain provisional verbal meaning; ordered edges, proposed shared sites, selected
participants, numerical comparisons and unresolved slots are native structure.

`geometry.siteSources` explicitly selects a point of an earlier participating
part. Wall and tower boundaries are split at actual contact sites, so a roof can
share two corners and a base-middle site with its supporting tower or wall. A
tower's lower sites select the wall or ground; walls and doors select ground sites.
These identities survive capture and restore. Equal coordinates alone
do not create identity. These sites do not alias acquired samples, and source
gaps are not silently closed. Missing, forward, unselected or disagreeing site
references are rejected.

The named heuristic cost components, shape choice, role and partial-marker reason
remain adapter provenance. Native context preserves selected source witnesses,
ordered proposed geometry, explicit shared sites, open placeholders and signed
pair-bound comparisons. It does not yet execute the triangle/dome selection or
the placement costs as native learned laws. The display's category labels are
not a substitute for that future structural work.

The source JSON, point order/times, fitted intervals and baseline program remain
unchanged. The old seven synthesis states remain available for comparison. This
new layout is neither another pass that mutates those lines nor a native physical
settlement engine. It is an explicit imported construction hypothesis with visible
support and disagreement, ready for later changes to the proposing mechanism.

## What this pass does not establish

- One main rectangle still compresses side views and complex connected bodies.
  This experiment does not first select an appropriate view-specific machine.
- The local search handles separated caps; several roofs stacked or overlapping
  at the same horizontal position can suppress each other. Policy v3 independently
  proposes flat-topped towers and removes the compulsory broad-roof fallback.
- An outlined cross can escape the existing crossing detector. A prior-only cross
  may consequently appear above a real cross drawn elsewhere.
- Writing, scribbles and decoration can propose false openings. Parts embedded in
  a long mixed-purpose stroke can be missed by the small-region grouping.
- A plausible construction can disagree substantially with its source. The original
  remains visible so that disagreement can be judged, not hidden by a fit score.
- The 24 drawings used for review are an existing development set, not an unseen
  evaluation set. Geometry and archive checks do not measure semantic accuracy.

The next pass, [boundary mapping](CHURCH_BOUNDARY_MAPPING.md), adds whole and
partial line associations, shared support, indirect endpoint references and
unresolved ink. The original area and synthesis views remain available.

Useful feedback is whether the individual **tower/roof counts and areas** are
plausible, which markers or optional openings are missing or spurious, and which
overlapping regions need a different construction. Learned weighting can later
propose these same connected structures; no trained model is needed for this trial.

## Tilted windows and local cross context — policy v8

Opening reads try a bounded set of rotated source frames before rejecting a
tilted enclosure. The chosen read basis and fit costs remain recorded; ideal
area placement is still a separate authored proposal. Candidate windows may
straddle the uncertain upper wall estimate by a small amount, allowing the
later containment and symmetry pass to place them coherently. A frame with an
interior plus remains one window with separately mapped divider intervals.

Cross placement considers tower faces as well as caps. Long near-horizontal
and near-vertical arms can supply an additional contextual crossing candidate
when the tight acquisition detector missed it. A short endpoint overrun sharing
an arm with a more complete crossing is withheld as a second church symbol.
The raw joint stays in the measured evidence; the competing interpretations
and selected spans remain visible in a contextual relation.

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
