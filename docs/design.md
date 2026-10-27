# Design and training direction

## Why the converter is separate

The converter owns input iteration, fitting orchestration, contextual proposals,
dataset identity, and reviews. The linked Gojo project owns the existing numeric and
structural kernel, native evidence readers, and church construction experiments.
This avoids copying a second kernel into the dataset tool. The dependency is pinned
for reproduction while actual source fingerprints expose local changes.

The current division is deliberately inspectable: Python proposes finite structures;
the native bridge connects them to real source witnesses and validates their legal
structure. Rich roles such as a functional door or gravity-supported wall are still
proposals until their mechanisms exist structurally. No change to Gojo's foundational
rules is introduced by this project.

## Shared outcomes without observed motion

Machine interventions are useful even for static drawings. Change a parameter through
a proposed relation and inspect what remains consistent. That tests the proposal's
response; it does not prove how the human actually constructed the drawing.

For two windows about a door, use the same requested displacement of the right window:

1. An anchored translation leaves the left window fixed and can break a midpoint rule.
2. A mirror moves the left window oppositely and can preserve that rule.
3. A centered repetition can produce the same paired movement and tie with the mirror.

The third alternative matters. Choosing weaker controls for one machine would falsely
identify reflection. Asymmetric motifs, reversed local frames, attachments to other
parts, and additional interventions may distinguish the candidates. Until then keep
both with their evidence and unresolved differences. Tests also retain a pre-existing
asymmetry; the probe must not secretly repair it before comparing responses.

The implementation compares midpoint, height and bounding-frame containment reads.
Its sum includes only equal-unit local distance remainders and is not a probability.
The future Gojo form should run change through explicit ports, admissibility, transfer,
capacity and settlement, with a timeline and remaining tension. The current Python
counterfactuals do not implement that feedback law.

## Closure, containment, and bidirectional support

Closure asks whether a boundary closes. Containment asks how a selected participant
relates to a selected container, potentially under a hypothesized missing boundary.
They should be connected where useful without becoming the same claim.

A window beyond a wall can leave several live alternatives: move the window, move the
wall, enlarge the wall, reject the proposed container, or retain the conflict. A more
reliable window observation should eventually constrain its wall as well as the wall
constraining the window. The current probes expose the first geometric alternatives;
they do not yet learn reliability or settle coupled native worlds.

The same pattern applies to clustering and repetition. A certain member can influence
the group's proposed frame, while an uncertain group does not get to erase that member.
Repetition also asks how many instances participate and what is shared or transformed.
Proximity alone must not merge identities. Later work should test membership changes
and alternative groupings as well as moving already selected members.

## Parts, views, and missing boundaries

Church seeds look for plausible upright supports, body bounds, roofs/steeples, crosses,
openings, and paired placements. They retain several body/view candidates rather than
forcing every input into one front-facing church. Weak corners can remain expected
sites supported by nearby intervals. Ground can be a hidden provisional lower boundary;
it is not inserted as observed ink.

Front, left/right oblique, top, and unresolved views are represented as conditional
candidates. This is not a trained view recognizer or a complete camera model. An oblique
wall can justify a different geometric family from a frontal wall, but labels alone
must not authorize a rewrite. Roof/support/ground expectations need their own support.

World scale, gravity, support and material are open assumptions. A page's downward axis
does not identify gravity, and image size does not identify physical size. A shirt on
a table versus a hanger, or a cup handle versus a building-scale cantilever, needs those
additional conditions. Do not infer engineering plausibility solely from a 2D silhouette.
These are useful future conditioning variables, not physical simulation in this release.

## Learning after the encoding stabilizes

Start by learning the strength and useful range of interpretable relations, rather
than asking an opaque model to replace acquisition. The next practical experiments are:

1. Review a stratified church pilot and annotate a small set of candidate preferences,
   missed relations, false joins, and unresolved alternatives. Include both easy and
   awkward drawings and several views. Add synthetic controls for known machine behavior.
2. Fit a small conditional model for individual relation support: gap relative to scale,
   continuation, corner orientation, paired placement, repeated part shape, and containment
   margin. Keep observation reliability distinct from how common a structure is.
3. Compare candidate interpretations using held-out human preferences and structural
   consistency under interventions. Worse pixel/source fit is acceptable when the
   conceptual interpretation improves. Preserve rare but supported alternatives.
4. Add part and view conditioning with shared low-level mechanisms. Keep optional parts
   optional; a church prior must not invent a cross or windows in every record.
5. Project the same evidence to coarser resolution. Preserve count, order, relative
   placement, open sites and repeated/shared frames while allowing geometric variation.
   A finer read must refine a valid coarser read rather than contradict its structure.

A first review set might contain 100–300 varied churches, followed by a few thousand
unreviewed records once extraction failures are understood. Those are proposed planning
sizes, not measured learning requirements. No model is trained in this checkpoint.

Split by original acquisition and, where available, contributor or collection context.
Keep all fits, stages and augmentations of a drawing in one split. Synthetic examples
from the same recipe family should also be grouped when testing generalization.
QuickDraw recognition flags are weak metadata, not labels for correct structure.

An LLM can help name a recurring unexplained residual and propose a new connected
mechanism, such as an optional seal on an envelope. Its explanation starts as a
hypothesis with examples, counterexamples and provenance. It becomes useful evidence
only as later tests establish its expectations and limits. Repeated unexplained marks
should not be quietly removed as noise to improve a category score.

## Known limits worth testing next

- Interior uprights can be mistaken for building walls. Single-stroke openings are
  easier to propose than multi-stroke ones, and door/window roles can stay ambiguous.
- A valid native archive can carry a bad interpretation. Fresh church synthesis already
  demonstrates this; later stages must not become automatic training truth.
- Dense native captures are expensive. Compact training reads need their own audited
  correspondence and capacity policy before large-scale conversion.
- Bounding-frame relations and adapter interventions are only an initial surface.
  Full curved enclosure, learned confidence, clustering changes, explicit transform
  machines, native bidirectional settlement, pre-numeric sampling, and physical priors
  remain future work.

## Opinion-provider warm-up

The first replaceable provider contract now exists in `gojo_quickdraw/opinions.py`.
A request exposes selected finite profile controls, its frame, measured/expected
comparisons, proposed geometry and earlier claim reads. A response supplies
support and opposition on the existing ordinal ladder, a provider version and
qualifications. A heuristic can be replaced by a small model or algorithm without
turning identifiers or verbal category names into the source of native meaning.

The derived analysis connects trace/traversal observations, contour/part groups,
rectangle/triangle hypotheses, dependent corner expectations, limited separation,
containment defaults and roof/wall attachment. Expected L corners consume the
rectangle's actual expected sites. The resulting native bundle preserves contrary
local evidence and common source causes. It does not yet train an arbiter, distribute
belief through a recurrent network, or execute general geometric settlement.

The seven existing church proposal states are also available over an entire converted
collection. They remain authored interpretations rather than learned labels. Source
acquisition and baseline archives do not change. The desktop's bounded `process`
workflow now executes and retains the native construction and independent output
readback as well as all other view stages. Standalone `analyze` still supports the
lighter proposal-cache path. Recorded source correspondence, dependency branches,
unmatched intervals and residue keep the evidence contract distinct from the
correctness of a particular synthesizer.

The user's container rule is now an explicit default to investigate: a child edit
can leave the parent fixed, while a parent placement carries contained participants.
The counterfactual keeps each descendant's displacement single even if several
containment routes reach it, and reports alternative parents and cyclical returns
to the selected root. General cycle diagnosis remains incomplete. Fitting the
container boundary to an explicitly anchored child is a different, qualified
response route. None of these provisional responses establishes universal physical
transport, object ownership, or a unique human construction process.

## First local training experiment (proposal, not implemented)

Start with a local decision: should the selected source participants meet, remain
separate, or remain uncertain, and which junction topology would that imply?
Train an opinion provider; do not let its output directly weld acquired sites.
An accepted interpretation can construct new sites with explicit correspondence.

Use one- or two-hop source-joint patches: ray-direction bins, ordered incident
edges, neighboring joint types, closed-walk membership and coarse relative order.
The coordinate-free export now makes these available. Avoid final-side features
as inputs to a source-side prediction. Also omit global category/key identifiers
from features; IDs only join records. Retain uncertain source reads rather than
labeling every unmapped node a negative. Closure is separate from containment;
this first joint graph does not supply a physical inside/outside law.

Compare a rule-only baseline, a small logistic model, and a two-layer MLP over
a bounded local neighborhood. The MLP is a modest CPU experiment with simple
inputs; it needs a stable neighborhood ordering and can miss variable graph
structure. If neighboring evidence clearly helps, a small message-passing graph
model is a later comparison: it handles variable neighborhoods but costs more
implementation and makes contribution auditing harder. Keep each provider's
input participants, alternatives and support/opposition inspectable.

As an initial budget estimate, review 200–500 local patches from 50–100 distinct
drawings, including gaps, T/X ambiguity, independent crossings, open frames and
paned windows. These counts are a starting experiment, not an empirical promise
of sufficient data. Training on fixed features should be seconds to minutes on
a normal CPU; label review is likely to dominate. Expand to 1,000–5,000 patches
if held-out performance is unstable. Split by original drawing before making
patches or variations; all its stages stay in the same split. Keep a separate
reviewed test set, and measure abstention, false joins, false separations and
calibration, rather than agreement with the synthesizer alone.

The current comparisons provide candidate correspondence and disagreements for
review. Synthesized finals are weak teacher proposals, not human intention or
independent ground truth. A later object-level experiment could distinguish a
framed crossing (window candidate) from a bare cross using the same graph, then
feed that opinion back alongside existing part hypotheses.

Method references: [scikit-learn MLP documentation](https://scikit-learn.org/stable/modules/neural_networks_supervised.html)
and [Gilmer et al., message passing neural networks](https://proceedings.mlr.press/v70/gilmer17a.html).
