# First local joint construction model

This is an implemented, trained pilot. It proposes five connected local
constructions: separate runs, continuation, corner, end-to-span T, and four-arm
crossing. The opt-in Encoding comparison replaces covered local joint opinions
with this model's opinions, preserving the default as a paired baseline. It
does not automatically modify drawings or replace the church shape heuristics.
Y/K junctions, closed-loop interpretation, larger neighborhoods,
rectangle/triangle feedback and general settlement remain future work.

## Encoding comparison: structural contract

The implemented application exposes two reads of the same acquisition, not two
independent observations. Its connected shape is:

```text
source annotation -> source profiles / measured joint witnesses
  -> selected pair -> finite cells -> five candidate constructions
  -> external model score witnesses -> candidate placement frames
  -> selected local opinion / open choice + uncovered source witnesses

baseline read(source annotation, measured witnesses)
selected read(same source, checked provider, candidates, uncovered remainder)
  -> paired comparison witness(baseline, selected)
```

Placement uses the existing source parameter/orientation hints. These are
algorithmic conditional estimates, not an orientation model or confirmed
source contact. The candidate sites remain separate from acquisition sites.
All five scores remain available, including second and third choices. A
selected provider replaces the local heuristic opinion in the comparison
surface; it is not added as an independent vote on the same evidence. The
source heuristic witnesses remain inspectable as the paired baseline.

This integration does not implement rectangle/church feedback, introduce
missing corners without a two-profile proposal, or settle the whole drawing.
Inference proposal selection must not consult final interpretations or labels.

Open **Encoding**, expand **Encoding settings**, choose a completed drawings
folder and a saved joint model, then **Create paired encodings**. The model
checkbox selects the provider. Unchecking it is a no-op control: both native
reads alias the same baseline joint surface. One joint provider is currently
available; competing versions of that provider are not combined as if they
were independent evidence.

Results appear in the app, twelve pairs per page, and reopen from **Open saved
encoding run** without Python or Gojo. Opening a paired run with the shared
folder picker also routes to this view. Click a drawing to select a patch;
its source runs and joint position are highlighted and the leading three
oriented constructions are shown. All five candidates, finite scores,
placement frames, replaced baseline witnesses and uncovered source witnesses
survive independent native capture/restore. Unresolved choices can be toggled
in the overview. Unchanged no-join opinions remain in the data/detail selector
without cluttering the overview.

```powershell
python -m gojo_quickdraw encode --dataset D:/_quickDraw/data/church3000 --limit 25 --model models/joint-pilot-2026-10-27.json --output D:/_quickDraw/data/church3000-encodings/new-run
```

Omit `--model` for the unchecked-provider control. `--recognition` and the
current category folder bound the source snapshot, and `--max-patches` bounds
local proposals per drawing (default 128, up to 512). Proposal-cap hits are
visible. No final correspondences enter proposal selection or features.
Archives under `baseline` and `selected` are the two native reads; `drawings`
contains derived viewer projections, `models` retains the exact selected
weight bytes, and `encoding.json` retains source fingerprints and run scope.
Every run uses a new folder, normally beside the source under `-encodings`.
The original drawings and their ten saved stages are read-only.

The first paired run used 25 drawings and 661 source-only local patches. It
produced 522 provisional proposals, retained 139 open choices and had 359
different/additional patch opinions, in 36.2 seconds. This is an inference
review, not a held-out accuracy experiment or a count of improved joints.

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

## Neighborhood repair extension

This extension constructs and ranks local graph changes rather than relabelling
independent line pairs. Its connected Gojo shape is:

```text
actual source profile / acquired endpoint references
  -> working graph(frame, shared sites, ordered curve edges)
  -> local focus(sites, incident edges, nearby joint/encounter reads, scale)
  -> candidate(before graph, replacement bindings, target graph, trimmed spans)
  -> native coarse feature reads -> external repair / line-shape opinions
  -> structural admissibility + selected local application
  -> after graph + source-interval correspondence + timeline + remainder
```

Shared target site identity establishes a connection. Coordinate equality alone
does not. Splitting a bar preserves ordered source parameter spans with a shared
cut; joining a stem exposes three edge endpoints and their far sites. Removed
overshoot spans remain in the application residue. Model scores observe rather
than mutate; applying a selected interpretation is a separate structural act.
Existing source acquisition and step-10 interpretations remain unchanged.

Neighborhood scale uses local incident reach, bounded nearest participants and
an explicit ratio. It is not the longest line on the whole page. Line-shape
and local-repair providers remain separate; the first integrator uses authored
compatibility and movement limits, not a trained third network or a general
convergence claim. Final-rendering targets are weak guidance only when all
required rays have mapped source support within the selected neighborhood.

This is now a separate opt-in `consolidate` pass, available in Studio's
**Encoding → Neighborhood repair** mode. It never replaces the source dataset
or the existing ten-stage church reconstruction. Both provider switches off
preserves the exact initial graph identity. The earlier pairwise experiment is
still selectable for comparison.

### Scope and local response

Each proposal starts with actual acquired endpoints/fitted curve profiles.
Endpoints share a source site only when they reference the same acquired sample;
equal coordinates alone remain separate. A local focus includes at most six
nearest sites inside 10% of the longest selected incident span. Independent
curve encounters also require their gap to fit 10% of the shorter participating
span, limiting the reach of a long building edge into a small window.

Candidates merge nearby sites, close endpoint/interior gaps, split crossing bars,
trim short terminal overshoots, straighten a curve, or align almost continuing
connected spans. Their output is a new graph of actual shared sites, ordered
edges and retained source parameter intervals. Every T exposes three outgoing
edges and their far endpoints. Active intervals cannot overlap on the same
source/basis. A corner cannot acquire an unsupported arm by selecting a label.

Two CPU categorical logistic providers inspect coarse native reads: 24 cells
for a proposed repair and eight for line/curve interpretation. Their inputs
include neighborhood endpoint/corner/T/X counts, branch degree, relative tail
and gap reach, turn concentration, chord deviation and local source support.
Coordinates, category names and final-rendering labels are not inference
features. Fine coordinates still place the proposal, and numeric thresholds
still define the coarse cells; this is **not yet a fully pre-numeric encoding**.
Each cell retains its measured read, five actual partition cut locations, the
coarse basis and finer remainder in native structure. Restore checks reject a
grade that does not follow those cuts, even when it uses the correct axis.

The authored integrator rejects unsupported branches, enclosure destruction,
coincident same-direction branch collapse, excessive movement and substantial
removal. It ranks admissible proposals with explicit movement/removal costs,
uses a support threshold of 0.68, and leaves unfamiliar cells unresolved.
Joint and line scores are dependent opinions, not multiplied probabilities.
Each selected local application carries unaffected edges by identity; a stale
overlapping proposal waits for the next pass. Four passes are the default, up to
eight are supported. This is bounded rewriting with a retained timeline, not
native tension settlement or a convergence proof.

All candidates and scores remain in the native archive. The portable and Studio
viewers show the source fit and every completed pass, leading alternatives,
blocked/stale/kept decisions, source profile references, removed intervals and
far endpoint references. Geometry previews are projected from an independently
restored native result. Native shared-site identity is stable across passes;
new interpreted sites receive new identities and retain their source bindings.

### Training and limits

Known controls include gapped corners, T gaps and overruns, crossing lines,
continuations, separate parallel strokes, smooth arches, clean corners, double
overruns, fragmented and jogged Ts, and tiny terminal hooks. Four distortions
share a template group; groups are split before training/validation/test.
Model and epoch selection use validation only. Whole graph evaluation uses one
case per held-out template, with targets consulted only after inference.

Final church graphs provide *weak* targets only where every required ray has
mapped source support in the neighborhood and an admissible candidate can
actually realize that incidence. Added parts, missing rays, incompatible
correspondences and ambiguous neighborhoods are skipped. Their accepted labels
have weight 0.15; a validation-selected controlled-only baseline is retained as
an alternative training trial. No inference request includes the final graph
or its correspondences. These restrictions deliberately discard much of the
available teacher signal rather than teach a two-branch patch an unsupported T.

The first checkpoint used 600 controlled neighborhoods and a read-only snapshot
of 60 church drawings, with 146 learned weights across both providers. It is a
local mechanism pilot. It has no learned church part hierarchy, rectangle/roof
feedback, global symmetry correction, learned object boundaries, or trained
third integrator. Curves can be preserved or straightened; the existing fitter
supplies initial segmentation, and this pass does not yet learn arbitrary new
bends or arcs. Radius choices, candidate families and compatibility limits remain
authored. Synthetic agreement must not be read as human recognition accuracy.

```powershell
python -m gojo_quickdraw train-neighborhoods --dataset D:/_quickDraw/data/church3000 --output D:/_quickDraw/data/new-neighborhood-model --limit 60 --synthetic 600 --epochs 35 --seed 43
python -m gojo_quickdraw consolidate --dataset D:/_quickDraw/data/church3000 --output D:/_quickDraw/data/new-neighborhood-encoding --model models/neighborhood-pilot-2026-10-27.json --limit 25 --seed 31
```

Use `--no-repair` or `--no-line` for ablations, or both for the unchanged control.
Training and application require a new output folder. Saved reviews can be
opened without Python or a Gojo checkout. Raw data and generated native archives
remain outside Git; the small model and verification summary are versioned.

The next useful supervision is a small reviewed set of real local **edits**:
the intended branch bindings, tails to retain/remove, straight/curve decisions,
and explicit non-matches. Then add rectangle/part candidates with their expected
corner ports as another evidence provider. A learned third integrator should
follow once candidate interactions and counterexamples can be tested; adding it
now would mainly learn the current authored integrator's decisions.

### Endpoints, nearest points and iteration

Endpoints were already present in the working graph, but the original viewer
hid them. Both viewers now show **E** and expose a separate nearest-point
overlay. Its dashed connectors are observations, not contact or added strokes.

Each free endpoint queries two nearest **other** intervals, excluding intervals
already incident at that exact site. A bounded curve search retains the selected
interval, source parameter point, separate projected site, calibrated gap and
local reach in a native `ReadWitness`. Projection does not join the sites.
An accepted attachment reuses that parameter as the shared cut, splits the
target interval, and explicitly binds the incoming endpoint. A shared far end
no longer excludes the whole segment pair from consideration. Equal coordinates
and source identity remain distinct reads.

The controls now also cover an existing corner plus a free endpoint, a free
endpoint near a segment whose far end is already shared, straightening followed
by attachment, T-to-L trimming, X-to-T trimming, and preserving a genuinely short
T stem. Small does not by itself mean accidental. Missing candidate coverage,
structural rejection and a low or unfamiliar model opinion are separate reasons
for keeping a gap.

Each pass evaluates the same two providers on the **current** interpreted graph.
Local competition distinguishes selected site neighborhoods as well as incident
edges, so unrelated corners on the same long strokes do not automatically share
one decision. Line and repair providers receive bounded candidate opportunities.
After an application, stale proposals wait for a fresh read in the next pass.
The native repeat controller retains the requested pass span, actual completed
count and last before/after transition. A run stops when no edit is accepted or
the budget is exhausted. Neither state proves that the interpretation is correct
or globally optimal. Repeating a read on unchanged input adds no support.

This is useful iteration without a recurrent neural network: straightening a
bar can expose an endpoint-to-bar attachment on a later pass. The same models
then see genuinely different input. Each completed graph, projection observation,
application and removed source interval remains independently inspectable.

### Connection to the supplied multi-model discussion

The user-supplied `scriptMultiModel.md` is a useful exploratory discussion, not
an executable specification or evidence that its historical counts remain
current. Its strongest proposals are reusable construction programs, separating
observation error from intended construction, local compatibility, and learning
from persistent unexplained structure. The following design direction remains
**unimplemented** beyond the two-provider experiment:

```text
unchanged acquired evidence -> exposed observation ports
small reader/model -> witnessed candidate maker and parameter hints
maker -> actual points/segments/curve roles/open slots and exposed ports
connected local relations -> expectation and mismatch reads
explicit response route -> admitted signed change or structural retargeting
affected local world -> settlement and new exposed reads
changed reads -> re-evaluate affected candidates/providers
timeline -> accepted changes, blocked response and remaining uncertainty
```

The model proposes an L/T/curve/rectangle **construction**, not merely a class
word. Its geometric roles and open slots must be connected. Geometry can then
settle through exposed relations while categorical alternatives remain available.
Changing a hypothesis needs a witnessed application or withdrawal; it is not
the same operation as adjusting a parameter. Current local rewrites retain their
history but do not yet provide general reversible topology or live multi-maker
settlement.

The ten existing church stages are useful saved reads and teacher proposals.
They do not imply ten different neural networks. Share readers for continuity,
incidence, enclosure, rectangularity, containment and symmetry across parts;
later add conditional view and category expectations. Those relations can
revisit one another in both directions. The inspection stages would show slices
of that process rather than dictate its one-way execution.

Several cautions matter:

- A statistical compatibility cost or prediction error is an observation. It
  is not automatically Gojo tension or a conserved physical energy. Converting
  it into change requires a visible receiver basis, response route, bounds,
  accepted/blocked accounting and settlement. Observation fanout must not be
  mistaken for draining material transfer.
- Keep the originating acquisition and dependency route. A rectangle, its
  corners and a symmetry reader can all depend on the same strokes. Returning
  that evidence through several loops must not create independent observations
  or steadily increasing confidence. Replace a current opinion when its input
  changes; retain its earlier version as history.
- Preserve fixed acquisition evidence separately from the current interpretation.
  The present local descriptors reread interpreted geometry; they do not yet
  include a separately learned fixed-source observation likelihood that resists
  cumulative drift. A useful future loop needs both channels. Treating its own
  cleaned output as fresh observed ink would reinforce its own mistakes even
  without explicitly adding confidence scores.
- Sum-product has exact tree guarantees; loopy propagation generally gives an
  approximation. Excluding the immediate return message does not remove every
  cyclic dependency. Oscillation can reflect update dynamics, model error or
  competing interpretations; it does not alone prove genuine scene ambiguity.
  See [the original factor-graph paper](https://www.isiweb.ee.ethz.ch/papers/arch/aloe-2001-1.pdf)
  and [Ihler, Fisher and Willsky's convergence/error analysis](https://jmlr.org/papers/v6/ihler05a.html).
- A learned expert should usually provide revisable support/opposition rather
  than an absolute veto. High confidence alone does not create a shared site.
  Hard rejection belongs to the declared structural contract; uncertain shape
  and category priors remain inspectable opinions.
- The discussion's handedness, gap, omission, style and drawing-order claims
  are hypotheses to test. Optional acquired timestamps are not present in every
  QuickDraw variant. Neither round dimensions nor a repeatedly convenient
  interpretation establish a human intention by themselves.

The next composition experiment should couple just a line/continuation maker,
a joint maker and one enclosure/rectangle maker, with explicit upward and
downward expectation ports. Test whether one maker's change produces an
accountable response in the others, preserves alternative readings and stops
without recycling support. That would be a useful small Gojo-flow experiment;
wrapping the existing ten stages in a repeated loop would not establish it.

## Research reading

These are relevant precedents, not claims that Gojo implements their inference
laws. The first three are a useful starting order for this project. In an
eventual combination, a rectangle opinion, a corner opinion and a church prior
may depend on the same source evidence. Their scores cannot automatically be
multiplied as independent probabilities. Preserve the common acquisition,
conditional premises and response routes when adding feedback.

1. Rosenfeld, Hummel and Zucker (1976), [Scene Labeling by Relaxation Operations](https://doi.org/10.1109/TSMC.1976.4309519). Local ambiguities revised through compatibility with neighboring interpretations.
2. Kschischang, Frey and Loeliger (2001), [Factor Graphs and the Sum-Product Algorithm](https://www.isiweb.ee.ethz.ch/papers/arch/aloe-2001-1.pdf). Local factors and message passing over connected variables.
3. Zhu and Mumford (2006), [A Stochastic Grammar of Images](https://dash.harvard.edu/entities/publication/73120378-84d2-6bd4-e053-0100007fdf3b). Hierarchical parts, alternative configurations and contextual spatial relations.
4. Hinton (2002), [Training Products of Experts by Minimizing Contrastive Divergence](https://www.cs.toronto.edu/~hinton/absps/nccd.pdf). Combining specialized probabilistic constraints.
5. Tu, Chen, Yuille and Zhu (2005), [Image Parsing: Unifying Segmentation, Detection, and Recognition](https://people.eecs.berkeley.edu/~efros/courses/AP06/Papers/tu-ijcv-05.pdf). Competing and cooperating interpretations at several visual levels.
6. LeCun, Chopra, Hadsell, Ranzato and Huang (2006), [A Tutorial on Energy-Based Learning](https://cs.nyu.edu/~yann/research/ebm/). Scoring structured alternatives through compatibility, without requiring every score to be a normalized probability; the author's page links the tutorial chapter and slides.
7. Lafferty, McCallum and Pereira (2001), [Conditional Random Fields: Probabilistic Models for Segmenting and Labeling Sequence Data](https://web.cs.ucla.edu/~yzsun/classes/2014Spring_CS7280/Papers/Probabilistic_Models/crf_sequence.pdf). Learning contextual compatibility from overlapping observation features.
8. Lake, Salakhutdinov and Tenenbaum (2015), [Human-level concept learning through probabilistic program induction](https://www.cs.cmu.edu/~rsalakhu/papers/LakeEtAl2015Science.pdf). Visual concepts as generative programs, with compositional stroke structure.
9. Kulkarni, Kohli, Tenenbaum and Mansinghka (2015), [Picture: A Probabilistic Programming Language for Scene Perception](https://www.cv-foundation.org/openaccess/content_cvpr_2015/html/Kulkarni_Picture_A_Probabilistic_2015_CVPR_paper.html). Analysis by synthesis: propose generating structure and compare its output with observations.
10. Battaglia et al. (2018), [Relational inductive biases, deep learning, and graph networks](https://arxiv.org/abs/1806.01261). Learned local updates over entities, relations and larger connected structures.
