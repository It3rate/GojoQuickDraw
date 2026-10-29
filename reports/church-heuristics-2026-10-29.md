# Church heuristics through native inference and settlement

The completed local review is
`D:/_quickDraw/data/church-heuristics-2026-10-29-v3/review.html`.
Its [implementation guide](../docs/church-heuristic-settlement.md) describes the
operative structures, retained artifacts and command line.

## Cohort and comparison

Sixteen distinct game-recognized church acquisitions reuse the existing frozen
local repair, line and closure providers. Ten are the exact cases from the
six-to-64-pass duration comparison. Six additional cases were selected from the
retained 1,024-case cohort to exercise paired-tower, several-window, outlined-cross,
side-roof, round-opening and paned-opening **proposals**. These selections are not
human-verified part labels; some proposed parts are wrong.

Every new run executes eight outer passes and two native settlement rounds per
pass, with a 48-candidate local proposal budget. All sixteen complete without
conversion errors. Each native archive is independently restored by the producer.
The exact source packets and frozen provider bytes are checked against their
retained inputs. No new model is fitted, and there is no human accuracy score.

The six-panel review shows acquisition, two earlier reference stages, initial
construction proposals, new pass six and a selectable new pass. It uses one
common frame, preserves the selected pass between drawings, and supports
left/right drawing and up/down pass navigation. Green interpreted ink is optional
and off initially. Faint acquisition remains visible. Gray dotted parts are prior
additions. Orange unresolved intervals and gray missing-boundary intervals can be
shown separately with hover descriptions.

## What changed

The existing cleanup chooses construction families, parents, foreground choices,
pairings and conservative prior additions. Observed parts still start at their
source-estimate bounds. Native affine obligations connect the exposed bounds for
roof support/alignment, cross attachment, upper-tower support, containment,
window/tower repetition, mirroring, opening clearance and circular aspect.

The new cohort contains 266 such relationship opinions, in addition to the
existing source, mapping and line opinions. Families and normalized internal
geometry can change at admission, with source/construction correspondences;
their parameters then move through actual carrier tension and receiver receipts.
The old cleanup's absolute final coordinates are not numerical targets.

Wall cells are projections of current public frame/tower bounds, with native
selection witnesses, executable coordinate constructions and boundary topology.
They have no independent cell actuators that could drift apart. This supports the
old foreground/partition proposals without copying final wall rectangles.

The source graph still remains a separate interpretation. Green curves can be
straightened, joined or trimmed by the local graph providers, while exact acquired
ink is retained independently. A colored shape explaining a curve does not erase
that curve from the source or automatically withdraw it from the working graph.

## A calibration problem exposed by the trial

The first full trial (`church-heuristics-2026-10-29-v2`) encoded affine coefficients
with 256 feature ticks per coarse unit but left response clipping intervals at
their old tick counts. Algebraically equivalent high-resolution opinions then
had much weaker motion, often falling below the actuator quantum. Cross
centering was a visible example.

The corrected v3 scales those feature clipping intervals too. Receiver passage,
capacity, damping and motion limits are unchanged. A regression test compares
coarse and fine versions of the same opinion over eight rounds and requires
identical accepted displacement, quantum remainder and live shape bounds.

`calibration-ablation.json` verifies that v2 and v3 use identical acquisitions,
scaffolds, mappings, authored opinions, providers and budgets. Their native
response calibration differs. The uncalibrated trial remains available for this
comparison; it is not described as a successful symmetry result.

## Descriptive results

These are mean normalized port demands in drawing units, not physical energy or
recognition accuracy. Means use only drawings containing that relationship layer.
They must not be added into a semantic success score.

| Relationship | Drawings | Before new settlement | Corrected final | Uncalibrated final |
|---|---:|---:|---:|---:|
| Attachment | 16 | 6.8603 | 5.0756 | 7.4884 |
| Mirroring | 4 | 14.1090 | 5.6833 | 14.1270 |
| Repetition / equal size and level | 4 | 3.1904 | 1.4164 | 2.7808 |
| Tower support | 4 | 1.1932 | 0.3713 | 0.7101 |
| Containment | 11 | 0.1754 | 0.1473 | 0.2493 |
| Ink/construction correspondence | 16 | 2.3069 | 1.7991 | 1.5237 |

All four drawings with selected mirror/repetition relations reduce those demands.
Attachment demand decreases in 11/16. Mapping demand decreases in 12/16, but the
corrected final mean is worse than the overly weak v2. That is a real tradeoff:
making the structure more coherent can reduce fidelity to the hand-drawn ink.
Some initially satisfied roof, seed and containment opinions become strained.
Their remaining demands stay visible; nothing forces every premise to be true.

The mean unexplained source-length fraction changes from **19.64% to 18.60%**;
9/16 individual cases improve. This is a finite mapper diagnostic, not the fraction
of the concept understood. Missing boundary support is retained separately.

The conservative unused-ink enclosure retry admits **no additional windows in
these sixteen cases**, and the limited interior-bend rule admits **no new knots**.
Do not count their implementation as evidence of successful real-data discovery.
Controlled tests exercise recovery of an omitted window beside an existing one,
non-erasure of rejected/unknown ink, and a source-preserving interior split.

## Visual findings and remaining limits

- Case 8 retains two compatible window constructions, but still misses the
  prominent left tower and lower opening. Those omissions are already present in
  the part proposals; additional parameter settlement does not discover them.
- Case 11 exercises multiple towers/caps and a wall partition from live bounds.
  Its initial family/part choices remain debatable. Neighboring outlines can
  still have nonzero attachment strain even though the occupied wall cells do
  not overlap their cutters.
- Case 12 retains three window proposals rather than forcing a two-window count.
- Case 15 is an instructive failure: the expert calls a box-like opening round.
  Native settlement preserves that wrong family. The “round-opening” selection
  describes the expert's hypothesis, not a verified stained-glass window.
- Case 16 retains the paned-window construction and its internal bars. A second
  opening is not guaranteed merely by the presence of one such construction.

All sixteen use the full eight-pass budget. More rounds may reduce some remaining
demands, but are not expected to recover an absent tower or replace a mistaken
window family without another inference event. There is no general physical
simulation, full viewpoint inference, text recognition or semantic church model.

The next useful extension is an explicit second shape-inference epoch: let
persistent unexplained ink, mapping strain and blocked attachment requests
nominate a new part, different parent, family replacement or split. Preserve the
old candidate and its correspondences while testing the alternative through the
same ports. A reward for merely assigning every stroke would encourage invented
objects; source support, relation consistency and an unknown alternative must
remain distinct evidence.

## Verification and retained data

Python: 229 tests. QuickDraw solution: 140 tests. Linked Gojo solution: 5,545 tests
across thirteen test projects. The linked Gojo checkout remains unchanged at
`9b7af32075aaef435382ff0bc0ecc7db8e39ef3a`.

The browser check covers sixteen drawings, all saved new passes, 96 comparison
panels, layer toggles, common frames, keyboard navigation and a narrow viewport,
with no page errors or network requests. Native review fidelity and exact signed
receiver accounting are checked by the independent church auditor. Its machine
readable results are retained in `native-audit.json`.
The restored comparison covers 6,192 generated shape points, 20,794 error reads
and 16,624 actuator outcomes, with maximum review/native difference zero.

The corrected dataset occupies about **1.12 GiB**, including **1.07 GiB** of native
histories. The complete run, including review generation and producer restores,
takes about nine minutes on this machine; this is not a training-time estimate
or a performance benchmark.

The bounded cleanup retires **1,377** superseded native proof files from the older
1,024-drawing experiment, releasing **27.676 GiB**. Every retired file is checked
against the earlier import receipt's hash. Its `retention.json` lists the exact
retired paths, hashes, sizes and remaining archives. All inputs, models, cohort
identities, all 1,024 visual results and all four original native versions of the
sixteen selected acquisitions remain. Retired archive links point to the receipt;
retained links still open the actual archive.

Raw QuickDraw data and user datasets, including `church3000`, are not changed.
Two small superseded trial directories remain because automatic policy review
blocked their recursive deletion. Compact copies of their settings and available
views are retained under the corrected dataset's `retired-experiments` folder;
those copies do not mean the original directories were deleted.
