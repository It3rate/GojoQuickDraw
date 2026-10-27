# Local construction proposals from coarse drawing evidence

Status: **exploratory design** for a first learned patch experiment. Recognition
selection is implemented. Archives retain acquired stroke order, point times
when present, fitted profiles, native joint witnesses, parameter locations,
gaps, final edges and before/after correspondence. The coarse partition/gap
interface and learned construction selector below are not yet implemented.
No new model was trained in this checkpoint.

## Recognition is an external observation

QuickDraw's `recognized` means the **game's AI guessed the prompted word**.
It is not a human judgment, drawing quality score, or part annotation. See the
[official dataset documentation](https://github.com/googlecreativelab/quickdraw-dataset#the-raw-moderated-dataset).
Preserve its source and keep it out of local model features. A recognized
church does not certify that every apparent frame is a window.

Studio offers All, Recognized by game, Not recognized by game, and Outcome
unavailable. Conversion filters **before** balanced random selection. The target
counts eligible acquisitions in selected categories. Existing drawings outside
the filter remain saved and do not count toward that target. A shortfall is
reported if eligible data runs out. Browsing filters before ordering/paging,
including old records without a journal projection. Unknown is distinct from
false. The default remains All. The current 300-drawing collection contains
264 true and 36 false outcomes.

For local geometry, use both outcomes and report results separately. For an
initial church-layout model, a recognized subset is a reasonable curriculum,
with an unrecognized holdout to reveal selection bias. This is a proposal,
not a claim that recognized drawings are always better.

```powershell
python -m gojo_quickdraw process --input D:/_quickDraw/quickdraw --category church --recognition recognized --limit 300 --output D:/_quickDraw/data/church-recognized
python -m gojo_quickdraw catalog --dataset D:/_quickDraw/data/church3000 --recognition unrecognized --limit 12
```

## Predict applications of connected constructions

Propose a reusable construction with bindings and unresolved ports. T,
rectangle and window are UI reads over that structure. Removing the label must
leave its connectivity, frame, open roles and expected behavior discoverable.

The same short overrun can support an end-to-span attachment and a four-arm
crossing. These hypotheses share observations, not one physical output site.
The attachment candidate explains the short tail as residue; the crossing
candidate retains it as an arm. Wider rectangle/roof evidence can favor one.
Do not overwrite the observation to make the favored candidate look measured.

A model proposes bindings, split/merge participation and calibrated support.
The instantiated construction exposes constraints, local responses and
residuals without asking the model to choose every subsequent position.
General convergence, automatic connection growth/pruning, and a universal
bidirectional uncertainty-response law remain open Gojo mechanisms. Current
church passes are authored finite operations, not proof of those capabilities.

## Minimal source patch

Select one possible encounter and a bounded neighborhood: initially 2–6 local
line runs and one or two graph hops. Include endpoints and nearby unjoined
ends, not only detected junctions. Otherwise a gap with no measured joint
could never propose an attachment. Preserve acquired shared sites separately
from geometric encounter candidates, which make different claims.

| Participation | Coarse read | Retained source/basis |
| --- | --- | --- |
| Ordered local run | start/end; straight/curved/open alternatives | fitted profiles and acquisition correspondence |
| Incident run | ordered rays; opposite/quarter-turn/oblique alternatives | local finite geometric read, not universal orthogonality |
| Cuts on a run | near start/interior/near end; short/long pieces | ordered locations on a shared parameter/length basis |
| Overrun | tiny/substantial/ambiguous tail | tail span relative to the selected whole run |
| Gap | none/tiny/substantial/unknown, approach direction | distinct endpoints or end-to-interior projection and reference spans |
| Neighbor arrangement | above/below/left/right/between/near alternatives | selected page/local frame and scale |
| Temporal grouping | same stroke; before/after; near in draw sequence | acquired events, not assumed object membership |
| Support | observed/projected/proposed/unresolved | witness to acquisition or expectation |

Train with several resolutions. A ratio near a band boundary keeps neighboring
bands plausible. Initial boundaries are authored calibrations to compare, not
learned natural laws. Keep metric source outside the coarse model interface
for audit/refinement; a coarse consumer must not secretly recover it.

### Subdivisions and gaps

For cuts near .02 and .97 of a run, ordered pieces are .02, .95, .03: tiny,
long, tiny at a chosen resolution. Retain their order and common whole. Do not
feed three unrelated size labels. Reversing a run reverses its frame without
erasing acquisition direction.

The denominator should initially be a supported near-straight run, with its
link to the pen stroke. A pen stroke can contain an entire church. Bezier
parameter fraction is not generally arc-length fraction: use an explicit
length basis for curves, or publish parameter order without claiming length.

Read a gap relative to **both** participating runs. A distance small relative
to a wall may be large relative to a window. For end-to-interior attachments,
retain projection position, signed offset and approach direction separately
from contact. Parallel lines passing nearby do not automatically want a join.
Deliberately disconnected marks remain valid outcomes.

Pen-lift count and spatial gap count differ. A triangle in three strokes has
two intervening lifts but may have three imperfect spatial joins, including
final closure. Record those events and encounters independently. Timestamps
provide evidence, never proof of intention.

### Frames and anchors

Retain the acquired page frame and local candidate frames: a long straight
run, a supported right-angle pair or an enclosure axis. Longest, straightest
and most horizontal are preferences, not unique anchor laws. A large cross
should not silently become the frame for the building.

Publish correspondence between frames and keep uncertain choices. Page down
is an observer convention, not discovered gravity. An upright-building
hypothesis can suggest gravity alignment as a separate premise. Between needs
ordered references; near/far needs a scale. Neither is a free-standing word.

## Connected fixtures before selector helpers

Establish the intended Gojo shape before adding a constructor or classifier:

```text
source A: S(Ca,a0,a1), source B: S(Cb,b0,b1)
  -> exposed ordered endpoint/interior reads
  -> selected local frame and finite partition basis
  -> projection witnesses, including gap/overrun remainder
  -> candidate construction input ports
  -> target sites/edges, constraints and open roles
  -> admissibility and accepted/blocked participation
  -> explicit retarget/transfer response, if selected
  -> local settlement, timeline and remaining residue/tension
```

A T construction exposes three arms at one target site: two continue across
the bar and one ends there. A crossing exposes four. Source sites remain
distinct unless acquisition shares them; source-to-target correspondence
does not establish source contact. T-to-cross revision retains compatible
bindings and before/after correspondence, opens/rebinds the fourth role, and
exposes incompatible participation. It is not a label swap.

A rectangle connects four boundary runs, four corner sites, closure, paired
direction/extent expectations and a frame. A triangle connects three sides,
three corners and closure. A window binds a frame to an enclosure inside a
wall. Geometric containment and membership differ: a wall does not own every
element inside its screen bounds. Group movement needs explicit transform or
coupling relations. The earlier containment-motion preference is a candidate
object-ownership hypothesis, not a universal consequence of containment.

Numeric import uses tension settlement, or explicit site retargeting ports
where appropriate. Projection does not change geometry. Material flow obeys
incoming = accepted + blocked + explicit remainder. Several candidate reads
of one source neither duplicate conserved participation nor create independent
evidence. Retain basis, support, alternatives and unresolved roles.

## Model sequence and experiment

1. **Local constructions:** no join, smooth continuation, corner, end-to-span
   T and four-arm crossing. Begin with one or two encounters, then Y/K and
   local merges/splits. Hints bind through the selected input frame. First
   distinguish a supported T with a small overrun/gap from a true crossing
   and unrelated nearby lines.
2. **Connected shapes:** rectangle and triangle over the same edge/site ports.
   They provide expectations to local corner/closure candidates. Do not
   require every joint to be resolved before a shape model can participate.
3. **Dependent parts:** window frame/dividers and doorway/leaves/handles
   inside wall/building candidates. A small side mark in a supported door
   can suggest a knob. Two leaves expose a seam and one handle role each.
   Neither two handles nor a center seam is mandatory. Do not invent a door
   to explain any small circle. These detail recognizers are planned.

Start with heuristic candidates and a tiny supervised ranker. For fixed small
patches a linear/MLP scorer over explicit structural reads is a useful
baseline; varying connectivity warrants comparing a 2–3-layer message-passing
graph scorer. Predict bindings and an unresolved/non-match option.
[Graph Matching Networks](https://arxiv.org/abs/1904.12787) is a relevant graph
similarity precedent; [SuperGlue](https://arxiv.org/abs/1911.11763) illustrates
rejecting non-matchable elements. Their tasks/weights do not solve our sketch
problem. Strict one-to-one matching cannot express interval split/merge.

Generate 1,000–5,000 controlled patches with known construction bindings:
distorted T/cross/corners, gaps, tiny overruns, real fourth arms, unrelated
strokes, alternate order, rotations and scales. Hold out whole templates and
sources. Include cases indistinguishable after coarsening: the correct result
then retains alternatives. Review patches from 100–300 real drawings before
trusting transfer. These are experiment budgets, not predicted minimums or
accuracy guarantees. Measure training duration rather than promising it now.

Measure candidate recall first; a ranker cannot recover a missing hypothesis.
Then test T/cross/no-join confusion, unmatched rejection, split/merge mapping,
calibration, residuals and stability under small perturbations. Compare
connectivity alone, then partition bands, gap bands and frames/order. Compare
two-band and five-band reads as separate ablations.

Split by original before crops/augmentation. Keep global IDs, category, game
recognition and correspondence labels out of model inputs; IDs only wire a
local graph. Preserve audit provenance. All crops/rungs of one source stay in
one split. Heuristic finals are weak supervision, not automatic truth.

Combining local and shape opinions must retain shared evidence. A rectangle
opinion derived from four corners is correlated with those corner opinions;
it must not count as five independent observations. Begin with bounded rounds,
preserve alternatives, measure oscillation, and add damping/revision resistance
only through explicit response laws. Global convergence is an experiment.
