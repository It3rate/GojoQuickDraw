# First local joint construction model

This is an implemented, trained pilot. It proposes five connected local
constructions: separate runs, continuation, corner, end-to-span T, and four-arm
crossing. It does not automatically modify drawings or replace the church
heuristics. Y/K junctions, closed-loop interpretation, larger neighborhoods,
rectangle/triangle feedback and general settlement remain future work.

## Run it

Open Studio's **Train model** tab. Select a converted drawings folder, set the
maximum number of original drawings, and press **Train joint model**. The
category and game-recognition filters selected in View drawings apply. The
training tab shows held-out results and opens the patch review or model folder.
**Open saved model run** reopens an earlier run without training again.

CLI equivalent:

```powershell
python -m gojo_quickdraw train --dataset D:/_quickDraw/data/church3000 --limit 300 --synthetic 2500 --epochs 35 --seed 31 --output D:/_quickDraw/data/church3000-models/new-run
```

Use a new output outside the drawings folder. The command snapshots completed
journal entries, deduplicates original acquisitions, and ignores a currently
uncommitted tail without repairing journals or writing an index cache. Existing
drawing files are read-only. There is no GPU, NumPy or ML package dependency:
the first model uses pure-Python stochastic gradient descent and a small C#
native observer linked to Gojo. The separate learning executable does not
replace a converter binary that may still be running.

## What the model sees

Each patch selects two existing fitted source profiles, including nearby
unjoined ends. Up to 24 patches per drawing are selected, prioritizing usable
weak correspondences. Degenerate profiles stay in the native unresolved scope;
they do not receive an invented ratio. The first run found 42 such profiles.

The published interface consists of sixteen finite band cells:

| Cells | Native observation |
| --- | --- |
| 0–3 | Ordered before/after fractions of each run at the selected encounter |
| 4–5 | Encounter gap relative to each run's length |
| 6 | Folded local tangent angle between the runs |
| 7–8 | Maximum chord deviation relative to run length |
| 9–10 | Accumulated absolute turning |
| 11–12 | Peak turn relative to total turn, with an explicit one-degree observer floor |
| 13–14 | Short terminal hook reach relative to run length |
| 15 | Relative run-length ratio |

These are coarse numeric/relational reads. The model receives no page XY,
source key, category, game-recognition outcome, correspondence IDs or exact
metric values. Type ranking factors out run reversal and role order; the
ordered source and orientation hints remain available for an application.
This does not discard acquisition direction from the archive.

Curves use 20 finite Bezier sampling intervals; controlled polylines retain
their supplied samples. Length fractions use sampled arc length, not Bezier
parameter fraction. Parameter locations remain separately recorded. The
result is a bounded observer approximation, not exact curve geometry.

A smooth arc can accumulate substantial turning without one dominant turn.
A sharp corner concentrates turning. A short hook retains its reach and
turning evidence rather than causing destructive segmentation. This prevents
an unconditional quarter-turn-from-start rule from turning every quarter-circle
into a sharp corner. Existing source fitting already splits many abrupt turns;
this training stage does not alter that fitting.

Band thresholds are authored calibration, not learned natural laws. A finite
first band is not proof of exact contact. Multiple intersections between two
complex curves and intentional small gaps remain ambiguous; one selected
encounter does not solve the entire curve arrangement.

## Native structure and learned opinions

Native feature meaning is retained separately from its exported band numbers:

```text
actual source profile witnesses
  -> ordered run whole and shared cut
  -> before/after spans sharing that cut
  -> finite measurement and calibration witness
  -> lower/upper band cell and retained finer read
  -> candidate input/hint surface
  -> connected target sites and ordered edges, with open placement roles
```

The candidate constructions have their own sites. A T shares one target site
among three edges; a crossing shares one among four. Separate runs have two
disconnected components. Coincident source coordinates are not merged. Tests
read the different constructions without descriptive names and verify that
ordered subdivisions still share their whole and cut after independent restore.

Position, orientation and scale roles remain open or hinted; this pilot ranks
their suitability rather than learning their complete geometric settlement.
The constructions are not yet a general responsive shape engine.

Model outputs become read witnesses selecting the actual patch cells and
candidate constructions, with a finite score basis and external model hash.
All five alternatives and unresolved roles remain. Scores are non-draining
observations; they do not establish contact, import physical change or trigger
whole-drawing mutation. The learned model is an external provider; its small
weight matrix is saved separately. General probability calibration on real
human intentions is not asserted.

## Targets and splits

Controlled data comes from known constructions with rotations, scale changes,
gaps, short overruns, curved continuation, small bends and endpoint hooks.
Four variations of each template remain in one train/validation/test group.

Real targets come only from unambiguous existing source/final joint
correspondences whose source support fits the two-profile patch. Three-profile
neighborhoods are not mislabeled as complete two-profile examples. Conflicting
targets stay unlabelled. These labels are **weak authored interpretations**,
not human truth; there are no reviewed real negative/no-join labels yet.

All patches from one original acquisition stay in one split. Training uses
70%, validation 15%, test 15% by stable group hashing. Validation chooses the
epoch, temperature and model trial. Held-out test labels do not influence that
choice. Automated tests change test labels and verify unchanged weights and
temperature.

Three feature ablations train on controls: topology/directions, plus coarse
partitions/gaps, plus curve/turn/hook evidence. A further trial adds real weak
training patches at weight 0.25. Its selection criterion is controlled
validation cross entropy plus 0.25 times weak-real validation cross entropy.
The source of each target and shared observation cause stays inspectable.

## First run: 27 October 2026

The snapshot used 299 distinct completed drawings from `church3000`, 2,500
controlled patches, and 5,571 real patches. Of the latter, 881 had usable weak
labels; other patches were inference-only. The selected model has **520
weights**.

| Training/input | Controlled held-out agreement | Real weak held-out agreement |
| --- | ---: | ---: |
| Controls, topology/directions | 72.4% | 40.5% |
| Controls, plus partitions/gaps | 95.9% | 47.1% |
| Controls, plus curves/turn/hooks | 96.2% | 45.5% |
| Selected curve model plus weak-real training | 95.7% | 69.4% |

The controlled test has 392 patches. The weak real test has 121 labelled
patches from 44 drawings, with corner/T/crossing targets only. Its majority target is T (67 of
121, or 55.4%). Patches from one drawing are dependent, so 121 patches are not
121 independent drawings. No real-world accuracy claim follows from these
figures. Curve evidence gives a small control improvement here, while coarse
partitions/gaps account for most of the gain over the first baseline.

This first type-ranking input folds the tangent angle, so it does not fully
distinguish opposed from coincident rays. The native source/order hints retain
that information. Publishing a richer ray-incidence read is a specific next
extension, alongside reviewed real non-matches and larger joint neighborhoods.

The selected model makes a provisional proposal when its top score is at least
0.60, its margin is at least 0.15, and few categorical features are unseen. It
proposes on 82.6% of the labelled real test, with 69.0% agreement there. This
threshold is a review convenience, **not** established real-data calibration.
Several visually ordinary corners still receive T support. More unreviewed
drawings alone cannot resolve noisy targets or missing context.

Eight additional authored checks ranked clean T, small-overrun T, small-gap T,
large-gap separation, genuine crossing, T with a tiny hook, and smooth curved
continuation as intended. The corner with two overruns stayed unresolved,
with T narrowly ranked above corner. These are small sanity checks, not another
independent accuracy estimate.

## Cost and artifacts

The first complete run took about 417 seconds: roughly 159 seconds exporting
and independently restoring native feature evidence, **5.4 seconds fitting all
model trials**, and 233 seconds exporting/restoring the learned opinion
archives. The two native archive folders total about 1 GB. The weight file is
small; retaining extensive independently restorable audit evidence dominates
this first workflow. Reusing feature reads and publishing narrower ports are
useful next optimizations, not implemented cache guarantees.

The separate conversion timing check found 80 MB and 156 MB of uncompressed
native history in two church examples. A redundant in-memory restore was
removed from conversion; the archive actually written is still independently
restored before publication. In the small warm encoder comparison, mean time
fell from 8.86 to 6.51 seconds (26.5%). Source summaries, every stage view and
joint readback were identical. This is a two-drawing encoder measurement, not
a guaranteed whole-dataset speedup. No door-knob detector or learned model is
in the conversion path.

Each model run retains `training.json`, `model.json`, `report.json`, a portable
`report.html`, compressed inputs/features/predictions, native patch archives,
and native learned opinion archives. The review shows controlled, weak-reference
and unlabelled held-out examples, with optional faint drawing context and a
local zoom. Model values and rendered coordinates serve different purposes.

The [small saved model](../models/joint-pilot-2026-10-27.json) and
[verification summary](../reports/joint-model-2026-10-27.json) are in Git.
Raw QuickDraw data and generated native archives remain outside the repository.

The next useful evidence is a reviewed set of real corners, T/cross ambiguities,
intentional gaps and non-matches, followed by a small neighboring-shape model.
Keep local geometric support and category-conditioned expectations separately
inspectable when combining them. Automatic shape feedback and guaranteed
convergence are not implemented by this first ranker.
