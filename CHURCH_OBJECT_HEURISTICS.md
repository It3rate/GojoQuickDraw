# Church objects: grouping, local context, and a small-data learning path

Status: exploratory authored evidence providers, 25 October 2026. This document
describes a church-only review pass and its next steps. It does not claim trained
semantic recognition, physical simulation, or general automatic settlement.

The immediate task is to propose which source intervals participate in which
parts, distinguish copies from alternative readings, and expose relationships
that can later guide interpretation. Changing the final synthesis is deferred
until these labels and scopes have been inspected.

## What the audit found

The previous converter already preserves source points, order, optional timing,
fitted intervals, joints, symmetry reads, contextual candidates, and native
opinion structures. The seven authored interpretation states include continuity,
primitive recognition, relations, joint convergence, regularization, and final
synthesis. There are multiple passes; their scope and information flow are the
more immediate limitation.

Before this object pass, context and synthesis were separate reads. Context could
suggest a body and door, while the synthesis code independently selected a main
contour and centerline. The synthesis did not consume those body/door candidates
or their opinion responses. Its later stages also pooled all `wall-region` edges
into one rectangle, treated all roof edges together, and usually centered doors
on one global axis. These shortcuts can erase the distinction between a body
and tower, or between repeated parts.

The earlier opening detector expected the entire opening to be one acquisition
stroke and considered only the first body candidate. That excludes many perfectly
reasonable windows drawn in separate strokes. Conversely, one stroke can trace
several conceptual parts. Acquisition is important evidence but is not the
definition of an object.

Late synthesis records before/after scores, but its authored acceptance policy
does not compare all competing interpretations using those scores. A finite
proposal can be accepted even when an incorrect scope gives it an inappropriate
centerline. Repeating that interpretation can amplify the error. This is a
reason to add locally connected hypotheses and inspect their disagreements
before increasing regularization pressure.

### Regression examples from the current collection

| QuickDraw key | Current analyzed item | Observed failure to preserve as a regression |
|---|---|---|
| `6039787817402368` | `9943e9fc2d0d3ac4eb3154e7a839f677e9853bc7` | The body has separately drawn walls. Context proposes a body around x=83–447, but synthesis falls back to the longest acquisition stroke: the almost-closed roof. Its start/end midpoint near the left eave gives an axis around x=57 instead of the body center around x=265. Roof, cross, door, and an attached wall then distort. |
| `5165838967504896` | `06d05fffff0a48f816429c3b8de159e53a826775` | Earlier stages already retain a large body rectangle and a smaller upper tower rectangle. Final synthesis pools their six edges into one taller box, collapsing tower edges and duplicating the top. |
| `6280306992611328` | `0b37810f5d70cc7a48242f611b5350189f3aa6d9` | Distinct upper/lower rectangular scopes become one facade; some source-supported edges collapse. |
| `6680427051876352` | `a792fdb5404b8c4ebfd1eb060a718212112b3c07` | Multiple rectangular groups and upper curves lose their local scopes during final box synthesis. |

Coordinates above refer to the normalized review frame and approximate the
audited cache. They are diagnostic examples, not ground-truth semantic labels.

## What this pass implements

`gojo_quickdraw.objects.enrich_context(observed, context, annotations)` returns a
new context and a `gojo.quickdraw-objects.v1` summary. It does not change source
geometry, timing, drawing order, original JSON, or authored synthesis geometry.

The pass:

- Retains legacy context candidates and their source support.
- Proposes bounded endpoint chains across acquisition strokes, using proximity
  without merging acquired site identities.
- Looks for bounded closed sub-contours within longer acquisition strokes.
- Retains several wall scopes, including supported upper rectangular scopes.
- Creates one local building focus per wall scope. Building and wall can use the
  same evidence without implying two independently observed objects. Alternative
  wall scopes remain alternative building focuses; candidate count is not a
  measured building count.
- Proposes windows or doors within each candidate wall frame from closed or
  supported open-bottom contours. Closure and containment remain separate.
- Retains legacy roofs, steeples, and crosses, and can propose an upper triangular
  contour even when an earlier two-line roof read missed curved fitted runs.
- Connects local roof/wall, upper-marker, containment, assembly-membership, and
  similar-part relations to the actual participating candidates.
- Distinguishes uncovered source intervals from intervals with competing
  interpretations. Both can remain visually unresolved.
- Publishes bounded numerical comparisons where available, with their basis,
  allowance, provider policy, and qualification. These are authored tests, not
  calibrated probabilities.

The first pass intentionally favors inspectable uncertainty over exhaustive
recovery. For example, branching components may remain unknown, a wall pair may
still include a door upright, and an irregular circular window may not yet get a
useful grouping. The proposed contour geometry is only a scaffold: a rectangular
bounding frame does not make a circle rectangular.

Visual review found an outlined plus/cross in `5650365196271616` proposed as a
window. The boundary is now correctly read as a nonrectangular closed contour,
but the proposed window role remains a known misclassification. Two crossing
centerlines and a cross-shaped perimeter need different geometric support. This
is a retained negative example for further grouping/role work, not an accepted
training label. The 24-record pilot produces only two window candidates overall;
that is a coverage warning to review, not a measured recall estimate.

### Current finite budgets

The provider currently considers at most 256 source intervals, 384 candidate
contour groups per search, 12 wall scopes, 96 new object candidates, and 96 new
relations. Source beyond a bounded read remains available and explicitly unread
or unknown. Capacity exhaustion is exposed separately for span, group, wall, candidate,
and relation work; it is not interpreted as absence of a part.

These limits are computation choices for the review experiment. They are not
learned priors about how many windows or towers a church can have.

## What constitutes an object hypothesis

The representation must distinguish the following:

| Read | Meaning and example |
|---|---|
| Source interval | An acquisition-supported ordered piece of ink. Its start/end, source range, and identity survive interpretation. |
| Candidate membership | These intervals might participate in one part. Membership can cross strokes or select only part of a stroke. |
| Instance identity | This window is distinct from that window, even when they share a template and look alike. |
| Geometric hypothesis | Rectangle, arch, circular contour, triangle, parallelogram, or an unresolved contour. Geometry and semantic role are separate questions. |
| Role hypothesis | Window, door, wall, roof, steeple, cross, building, or unknown. The role depends on connected context and can remain competing. |
| Assembly focus | Building versus wall can be different focuses over shared evidence. This does not duplicate the ink or prove multiple objects. |
| Containment | The child is expected within a selected parent region. A bounding-frame read is weaker than occupied area or physical containment. |
| Attachment | Roof and wall may share an expected boundary or connected site. Nearness alone does not establish actual contact. |
| Repetition | Separate instances can expose a common shape and placement controller. Their identities and individual edits remain distinct. |
| Alternative candidate | Two wall readings with overlapping support need not be two walls. Both may remain unresolved until wider evidence discriminates them. |
| Unknown | No retained role/group hypothesis explains this support at the current resolution, or a finite budget did not inspect it. |
| Ambiguous | More than one retained interpretation competes over support. Coverage by candidates is not enough to make it known. |

The adapter assigns stable candidate IDs from role and source membership, and an
`instanceId` from membership alone. The source record identifies the enclosing
world; matching candidate IDs in different drawings do not establish shared
identity. Refinement that changes membership needs correspondence rather than a
pretence that the old and new identities are numerically equal.

The summary exposes `id`, `instanceId`, `role`, `focusKind`, `spans`, `bounds`,
`parentIds`, `participants`, `status`, `reasons`, `conflicts`, and `comparisons`.
`unknownSpanIds` and `ambiguousSpanIds` are separate reads. Building assembly
overlap is not automatically a direct-role conflict. Roof versus steeple can
remain alternative role readings of the same upper support.

## Structural meaning and replaceable providers

The canonical connection remains in Gojo structure. A useful notation for the
current foothold is:

```text
candidate read
  selected source profile witnesses
  + selected earlier candidate witnesses
  -> observer frame
  -> provisional sites and ordered scaffold relations
  -> open slots and local comparison witnesses
  -> source provenance and unresolved support

ordered parent candidate -> child candidate
  -> relative-bound read -> signed margins / outside remainder

candidate interpretation A -> shared source support <- interpretation B
  -> exposed disagreement, without another copy of the observation
```

`native/ContextEvidence.cs` already connects actual profile witnesses and earlier
part witnesses, provisional sites, open slots, and signed relative-bound reads.
New context parts use its `participants` field; a text label such as
`expected.parent` alone would not establish nesting. Native construction order
allows references only to already introduced candidates. The review hierarchy
can point to a later assembly because hierarchy is a derived read, not a forward
construction dependency.

The opinion provider surface can evaluate the same local question using an
algorithm, an authored heuristic, a human hint, or eventually a trained model.
Inputs include selected source profiles, expected geometry, prior candidate
witnesses, and declared comparisons. Responses must retain support and opposition
with provenance. An unresolved role is not negative evidence that the object is
absent. A model's scalar output must not replace the connected evidence it read.

## Grouping signals and cautions

The table distinguishes what the current object pass uses from further useful
experiments. Existing line/joint annotations remain available independently.

| Signal | Intended contribution | Status and caution |
|---|---|---|
| Endpoint proximity | Candidate local chain across separate strokes | In: bounded endpoint clustering. It proposes association, not acquired identity. |
| Chain continuity | A connected nonbranching contour may be one part | In: finite endpoint graph. Full tangent agreement is a further read. |
| Near closure | Candidate enclosure or object outline | In: contour grouping. Closure is not the same as containment or semantic identity. |
| Open lower boundary | U-shaped or arched opening can omit a bottom line | In: explicit open-contour hypothesis. No acquired bottom is invented. |
| Long straight boundary runs | Prefer a simpler side over multiple short pieces | Existing earlier synthesis; this grouping pass does not replace geometry. |
| Opposed upright boundaries | Wall or rectangular opening candidate | In through legacy bodies and local rectangular groups. Scale/placement distinguishes alternatives only conditionally. |
| Curved versus straight support | Keep rectangle, arch, and rounded contour alternatives meaningful | Partial: contour shape read. Better curve/tangent grouping is planned. |
| Local turn pattern | Separate corners from smooth continuation | Existing annotations; more direct object-boundary use is planned. |
| T/Y/L/X/K/O-like junctions | Suggest attachment, crossing, enclosure, or depth | Existing joint evidence is available. A junction label alone is not an object boundary. |
| Similar scale and shape | Candidate repeated windows or paired openings | Partial: extent/placement comparison, separate instance identities. Stronger normalized-profile comparison is planned. |
| Local reflection | Candidate left/right correspondence inside one parent | Existing symmetry evidence; explicit object-pair refinement is planned. |
| Translation versus reflection | Competing placement controllers | Planned counterfactual consistency tests; neither gets independent support from its own generated examples. |
| Containment margins | Associate an opening with a local wall scope | In: envelope margins and explicit parent alternatives. Filled-region containment remains stronger. |
| Boundary sharing | A wall/roof or wall/wall edge can participate in multiple parts | Retained overlapping membership. Shared-site/contact proof remains separate. |
| Occlusion and interrupted contours | Parts may continue behind other parts | Planned; absent ink must remain hypothetical. |
| Stroke order and timing | Weak evidence of a common drawing action | Preserved source evidence; not yet a role classifier. |
| Repeated retracing | Detail, correction, emphasis, or an uncertain boundary | Earlier complement handling exists; distinguishing causes is planned. |
| Negative evidence | An expected support may be absent or contradictory | Partial through comparisons/conflicts. Missing a line is not proof that a part is absent. |
| Common response to a transform | Moving one proposed part should carry its members | Existing authored opinions; object-level intervention testing is planned. |
| Different response scopes | Moving one window differs from moving its parent wall | Existing default opinion; no general physical attachment/containment solver is claimed. |
| Front versus oblique frame | Symmetry and rectangles versus projected quadrilaterals | Existing view hypotheses; better per-part view conditioning is planned. |
| Scale, gravity, support material | Conditional expectations about plausible structures | Open assumptions. Page-up is not proven gravity, and normalized size is not physical scale. |
| Category context | Church parts can make weak geometric candidates useful | Explicit authored scope for this pilot. No unseen category generalization is claimed. |

Proximity, similarity, closure, and symmetry should cooperate. None should be an
unconditional merge rule. For example, a window inside a wall shares containment
with it but remains a distinct editable object. Two adjacent windows share a
repeat controller but remain separate copies. A nearby roof peak is a useful
parent clue for a cross, but a window mullion can have the same crossing shape.

## Church role proposals

| Role | Useful authored expectations | Alternatives and unresolved cases |
|---|---|---|
| Wall | Opposed sides, supported boundary runs, locally coherent rectangular or projected quadrilateral frame | A door outline can look similar. Side planes, round towers, missing edges, and multiple facades remain possible. |
| Building | Assembly focus tied to a local wall scope, later connected to its attachments and contained parts | Multiple scope candidates do not prove multiple buildings. A wider assembly may need several walls and towers. |
| Roof | Converging upper slopes or a coherent cap attached near a wall top; local centering is an expectation | A roof need not be symmetric, triangular, closed, or centered globally. Chevrons and lettering can resemble peaks. |
| Steeple/tower | Narrow upper structural scope, often with its own wall and cap; local parent relationship | A peak-only steeple reading is weaker than a resolved tower assembly. Several towers are valid. |
| Cross | Two crossing arms, often upper and attached to a roof/tower axis | Incidental crossings, window mullions, or other symbols. Several crosses are valid. |
| Door | Lower interior opening, often near a local wall center; sides plus cap may omit a base | An off-center door, paired doors, open gateway, window, or decoration remains possible. |
| Window | Interior contour, often repeated or reflected with neighbors; size relative to its wall matters | Rounded, arched, rectangular, split, or decorative openings. There may be no windows. |
| Unknown | Preserve the selected support without committing to a role | Can later split, join, acquire a role, or remain unresolved. Unknown does not mean useless. |

The current provider does not invent absent crosses, doors, or windows. Stronger
completion may eventually propose missing parts, but it must expose those as
introduced expectations with their basis and cost.

## Three-stage contextual rescoring

The present pass records grouping, local role/parent proposals, and disagreement.
The following extension would turn those into a bounded contextual feedback loop
after inspection of the labels:

1. **Read source support.** Produce several local groups and geometric hypotheses
   from the preserved fitted intervals. Record missing boundaries, contrary
   evidence, and the precise source observations used. Keep unknown support.
2. **Read relational consequences.** For a proposed wall and roof, inspect their
   relative height, attachment, and local center. For a proposed opening, inspect
   its containment and correspondence with neighboring openings. A rectangle can
   expose expected corner sites, which become queries back into nearby source
   evidence. These are conditional consequences of the candidate, not new ink.
3. **Compare and revisit.** Compare alternative memberships, roles, parents, and
   views using the exposed source reads and residuals. Revisit only the affected
   local intervals, optionally retaining multiple outcomes. A later geometry
   transform must use the selected instance frame and preserve source
   correspondence and the carried complement.

A two- or three-round budget is a useful initial experiment, not an established
convergence law. Stop on stable candidate membership/support, an explicit budget,
or unresolved disagreement. Preserve the reason rather than declaring certainty.

Prevent self-amplifying confidence by retaining source-cause identity. A roof
prior predicts where to look; that prediction is not another independent vote
for the roof. If a rectangle supplies four corner expectations, those
expectations depend on the rectangle. They must not circle back as four
independent observations proving it. Repeated stages and several derived views
of one drawing remain one acquired sample. A human hint also remains the same
hint when revisited.

Do not gate this solely on distance from the original mouse drawing. Desired
conceptual improvement may worsen that distance. Evaluate instance count,
noncollapsed boundaries, parent consistency, open/closed structure, and source
correspondence separately. Improving those reads still does not prove that the
chosen interpretation was the person's intention.

## Manual hints and small models

Start with authored providers and human review. Useful future hint operations
are: group these intervals; separate these copies; this is one instance with two
possible roles; choose/reject a parent; identify a shared boundary; mark an
optional missing boundary; confirm a repeat pair; keep this unknown; and select
front/oblique/unknown view. A hint should cite the source record and intervals,
its human provenance, and the candidate it changes. It should not overwrite the
acquired drawing or silently become ground truth for unrelated cases.

The first learning target should be a small local question, such as whether two
chains belong together or whether a supported opening is more door-like or
window-like in a given wall frame. A small interpretable classifier can use
normalized gaps, turns, closure, shape mismatch, relative extent/placement,
parent margins, and counterpart evidence. Keep explicit unknown/abstain behavior
and compare against the authored provider. A larger graph model becomes useful
only if these local providers cannot express the dependency patterns we need.

Suggested review/training budgets are estimates for planning, not promised
accuracy thresholds:

- Review **24–48 diverse churches** first, including difficult negative examples.
  This should expose missing grouping functions and annotation ambiguities.
- Collect approximately **200–500 reviewed part/group decisions** for an initial
  local classifier experiment. They can come from far fewer drawings than the
  raw corpus contains; one drawing may supply several related decisions.
- If that experiment is useful, expand to roughly **100–300 reviewed drawings**
  covering the difficult structures. Measure the learning curve before deciding
  whether more labels, different features, or a different model are needed.
- A small model should be inexpensive enough to retrain during iteration, but
  actual runtime and useful sample count depend on feature extraction, model,
  class imbalance, and annotation quality. Benchmark these rather than promising
  a training duration now.

Split by original drawing identity, never by derived step, part, or synthetic
variation from the same drawing. Otherwise closely related evidence leaks across
training and evaluation. Track grouping precision/recall, over-merge and
over-split errors, role confusion, parent accuracy, unknown/abstention coverage,
and user correction counts. A later generation evaluation should also ask whether
copy count, nesting, view, and local attachment survive synthesis.

## Initial 24-record review pilot

These keys are a bounded development/review selection from the existing 1,000
church collection. Selection mixed sparse and dense drawings, competing walls,
openings, upper parts, multiple crossings, and known regressions. Candidate
counts and source geometry were read during the audit; **this is not an unseen
accuracy holdout**. Ground-truth object labels still require review.

| QuickDraw key | Review emphasis |
|---|---|
| `6291594720313344` | Dense details and much unresolved support |
| `5320526270562304` | Body alternatives, upper part, cross, opening |
| `6292633682968576` | Sparse, relatively simple control |
| `5165838967504896` | Body plus upper rectangular tower regression |
| `5488048856891392` | Overlapping roof/steeple/body interpretations |
| `4835826410192896` | Several upper-part and wall alternatives |
| `5026742236348416` | Opening with competing body scopes |
| `6039787817402368` | Separate-stroke walls and closed-roof axis regression |
| `5389179863171072` | Unresolved interior/detail with multiple wall scopes |
| `6469580950077440` | Several possible upper structures |
| `5650365196271616` | Dense drawing, opening candidates, substantial unknown detail |
| `5231607982063616` | Roof/cross/opening with remaining disconnected evidence |
| `6082729202417664` | Alternative wall and upper-part assignments |
| `5650520033198080` | Nested upper/lower scopes and missing classifications |
| `4707059385761792` | Door/window alternatives within competing parents |
| `6280306992611328` | Upper/lower rectangle collapse regression |
| `5521537635450880` | Multiple candidate scopes with unresolved support |
| `5865058414362624` | Body, roof, open curved doorway control |
| `5161617551523840` | Crossing feature with uncertain body scopes |
| `6414840786583552` | Multiple cross candidates sharing some support |
| `6492920540561408` | Multiple opening hypotheses and possible over-grouping |
| `6680427051876352` | Multiple rectangular scopes and curved upper parts regression |
| `5901226988273664` | Multiple upper markers/roofs and local parent choice |
| `4836604000600064` | Upper curved interpretation versus lower rectangular scope |

After the first corrections, reserve an **additional 12 previously unreviewed
drawings** before tuning again. Later, expand that held-out set across front and
side views, repeated windows, multiple towers, missing bottoms, sparse outlines,
and decorative noise. The numbers are review budgets; no accuracy claim follows
from the existence of this pilot.

No source drawing data is stored in this document or the repository. The pilot
is reproducible by its QuickDraw record keys against the local collection.
