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
