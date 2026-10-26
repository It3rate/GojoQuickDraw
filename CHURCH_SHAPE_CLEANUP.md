# Church shape cleanup

Implemented exploratory pass, 26 October 2026. This is an automatic Python
heuristic pipeline with independently readable native Gojo evidence. No model
has been trained, no AI service is called, and these are review proposals.

## Running it

Choose a converted collection in QuickDraw Studio, keep **Analyze every drawing
in the selected generated folder and subfolders** selected, choose a fresh
analysis output, and run **Full analysis â†’ review**. Select **Church cleanup
(final shape stage)** in either review. The gallery defaults to this stage when
it is present. Page changes retain the selected stage.

An existing analysis needs a new output after this implementation update.
**Refresh both reviews** rebuilds presentation from saved evidence; it does not
apply new detection or cleanup rules. Original collections are never overwritten.

## Detection before refinement

- Outlined crosses can span several connected pen strokes and can have open
  ends. Inset upper/lower stems and a wider horizontal band support this read.
  The inferred medial axes are explicitly prior-only boundaries; only outline
  boundaries can claim outline ink. Ordinary crossing strokes remain supported.
- Broad upper roof bands with an eave or a sloping end can propose an oblique
  quadrilateral. It does not require a front triangular peak.
- A cap beside the main building can belong to a tower when local upright
  portions support it down to ground. Mere adjacency without those portions
  does not establish that interpretation.

## Refinement and expectations

The earlier area estimate stays intact. A separate `cleanup` surface proposes:

- symmetric front triangular caps and centered domes;
- roof placement relative to the selected wall or tower;
- equal dimensions and mirrored placement for plausible paired towers and caps;
- crosses that keep their approximate prominence, with distinct face-relative
  placement or attachment to the revised roof/tower;
- separate side towers, joined to the main wall, without forcing global symmetry;
- visible wall rectangles partitioned around intruding towers, retaining the
  earlier complete bounds as a construction frame;
- contained openings and ground-attached entrances;
- at most one prior entrance when no entrance was detected, and a pair of prior
  windows when none were detected and there is sufficient wall area.

Observed opening counts are retained. Optional additions use `prior-only` and
have no direct source intervals. Refined shapes refer to their earlier area
estimates; those retain their source-to-boundary mappings. A cleaned boundary
does not acquire ink simply because it resembles a nearby stroke.

The viewer uses faint original strokes, solid refined boundaries, light gray
dotted added doors/windows, and dashed other expectations/ground. Select a part
for its changes, provenance, construction references and source stroke order. The original ink is never
removed. Unresolved ink remains inspectable in the mapping view.

Cleanup policy v2 removes the narrow-tower and shallow-cap cross-size ceilings.
Cross height stays unchanged up to 60% of the larger main-wall dimension; above
that threshold it retains 55% of the excess. This response is increasing: making
an input cross larger cannot make its refined height smaller. Width retains 80%
of the bounded earlier aspect ratio, with 20% toward a conventional cross ratio.
These are inspectable authored choices, not estimates of real-world dimensions.
Face motifs retain their vertical placement, including motifs spanning a roof
and wall; large horizontal offsets are softened rather than removed outright.
Area policy v5 also accepts sloping outline arms while still requiring inset
upper/lower stems, preventing an open outlined cross from becoming a default
small marker merely because its arms were uneven.

Wall partitioning subtracts foreground tower rectangles from the proposed
wall frame. An upper tower supported by the wall top stays above that boundary;
an unbroken wall-top interval and short upper uprights can instead select the
lower wall as foreground and trim the tower. Remaining rectangles have disjoint
interiors and keep all remaining area, including pieces on both sides of a
central tower. Neighboring boundaries
share explicit sites. Openings select a visible enclosing section; optional
windows are omitted on very narrow residual sections. This does not recover
missing wings or guarantee that the earlier tower hypotheses were correct.

Pen order and construction dependency order are distinct. The pass retains the
selected source stroke indices, while its proposed hierarchy follows the
attachment dependencies. This does not claim that the person's mental hierarchy
has been recovered from pen order.

## Connected native evidence

Policy v3 keeps the selected roof peak as an attachment
reference, using the earlier cross foot and its selected source endpoints to
recognize that relationship. The revised foot shares the revised peak's actual
site. A detached or clearly offset motif remains a different proposal. Paired
supports also propose a mirror read of their crosses; signed bound differences
remain native observations, with an authored review allowance in the adapter.

A tower ending at the wall top retains that support. A continuous wall-top
interval across the tower, together with upper uprights that stop near that
level, can select the lower wall as foreground and trim the upper rectangle.
The selection retains its source intervals and before/after correspondence;
it is an occlusion hypothesis, not a claim of measured physical depth.
Conversely, selected side-tower uprights that continue down through the wall
level retain a grounded tower, even if its earlier regional bounds stopped short.

Door/window clearance is a local read between two opening bounds in their
common wall. Candidate translations preserve size and containment; a mirrored
pair moves together where possible. Added windows may stay unplaced if no
clearance-preserving position exists. Drawing-supported openings are retained
with an unresolved review warning if the available wall cannot satisfy the
authored separation. Connected opening/wall relations retain the native bound
reads; thresholds and search choices are authored adapter policy.

Added doors and windows are light gray and dotted in both viewers. Other
opening colors retain the distinction between a refined drawing-supported
estimate and an optional addition. Crossbar correction changes only the final
proposal, keeping its bar above the midpoint and retaining the original ink.

The current minimum opening gap is 6% of the enclosing section's width,
measured between axis-aligned opening bounds. The search considers a finite set
of translations without shrinking the openings. Mirror checks use the existing
four signed bound differences; the review allowance is the larger of 4% of the
axis section's width and 8% of the smaller part height. These are explicit
starting assumptions, not learned architectural laws.

The refinement keeps a partition as connected construction, using the
existing context and correspondence grammar:

```text
earlier wall -> construction frame
construction frame -> group(wall rectangle A, wall rectangle B, tower)
earlier wall -> each refined wall rectangle
wall boundary site -> shared tower boundary site
```

The construction frame exposes bounds and sites but no occupied outline.
Visible wall rectangles cover its area outside the selected tower rectangles;
their actual ordered edges and shared contacts carry the partition. This is
non-draining observation of one estimate, not duplicated acquired material.
Cross refinement similarly retains its earlier scaffold and signed site changes.
An on-face cross need not be squeezed into its associated roof's height; an
above-roof attachment and a face-relative placement remain different reads.

The native record accepts an optional sixth field after boundary mapping:

```text
group(frame, result parts, before/after correspondences,
      shared contacts, mirror reads, adapter provenance)

correspondence = ReadWitness(before part -> after part, frame,
  group((actual before site -> actual after site) -> signed XY difference),
  before part, acquisition)

contact = (actual parent -> actual child) -> (shared site -> shared site)

mirror = ReadWitness(group(left part, right part, axis part), frame,
  group(left.L + right.R - 2*axis, left.R + right.L - 2*axis,
        top difference, bottom difference), selection, acquisition)
```

Selected before/after sites are correspondences, not identity. Contacts select
the actual same proposed site. Equality of two independent coordinates cannot
substitute for a contact. Roof/eave, tower/ground, cross/peak, and door/ground
attachments remain readable after the descriptive JSON is removed. Mirror
residuals remain signed observations at the finite coordinate resolution.

Every refined site has a selected earlier-site correspondence when its part
has an earlier estimate. A genuinely added part instead references its refined
construction parents and retains open slots; it cannot claim direct ink or an
unrecorded earlier estimate. The existing native context also retains actual
ordered scaffold edges and signed pair-bound reads for participating parts.

This is non-draining observation and a separate authored proposal, not mutation
of acquired carriers. Python proposes the geometry. The native reader verifies
reference identity, arithmetic, correspondence coverage and shared contacts; it
does not certify church semantics or implement a general feedback solver.
Rerunning the pass recomputes dependent placements. The stored result is a
readable proposal snapshot, not an autonomous learned generative machine.

## Current limits

View selection is a coarse front-like versus side/oblique hypothesis. It does
not recover perspective or a camera. The main frame can have several visible
wall rectangles; some sparse or curved buildings, missing wings, tilted
crosses, multiple crosses on one support, windows and
clock faces can still be missed or misinterpreted. Prior openings may disagree
with intended architecture. Earlier alternatives and unresolved ink remain
available for later training and feedback.

The development review contains 36 selected drawings, including the four user
examples and the earlier 24-drawing pilot. It is a regression and inspection
set, not an unseen accuracy benchmark.
