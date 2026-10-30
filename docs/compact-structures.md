# Compact primitives and cooperating structures

Discussion reference, 30 October 2026. This is a proposed direction for compact
Gojo execution, not a finalized packet format or a change to the kernel laws.

Gojo can represent useful distinctions before it has measurements. The strongest
compact design is to share a connected basis and reusable construction, then
store only each occurrence's state, alternatives and connections. A few bits can
address that shared meaning; they cannot supply it independently.

The church pipeline is not yet this compact system. Its adapters still measure
coordinates, lengths, directions and fitted curves, and propose category parts
with authored rules. Native constructions then preserve connections, expose
errors and settle selected responses. That is a useful testbed for replacing
individual mechanisms without claiming that the complete parser already emerges
from pre-numeric primitives.

## Two kinds of low resolution

Strictly pre-numeric structure can express an ordered chain of landmarks,
alternative directions, a shared endpoint or a closed route without assigning
coordinates or magnitudes. A relation/placeholder graph need not contain a
numeric carrier at all. A two-bit index into `low, medium, high` does not make
medium the arithmetic average of low and high.

Coarse numeric structure additionally selects calibration and a finite read.
For example, a declared span can be partitioned into four bins. Those bins can
retain boundaries, a representable nominal value, discarded-detail support and
the relation back to their basis. This is different from an ordinal vocabulary
whose spacing is unknown. Minimum and maximum refer to extrema or boundaries of
a declared comparison scope; small and large need not be those boundaries.

Both forms can be refined. Refinement adds distinctions or calibration through
an exposed correspondence. It must not pretend that a lost measurement was
secretly preserved in the coarse symbol.

## Level one inventory

**In** means a present primitive or bounded executable fixture with focused
tests. **Partial** means some selected contracts exist but the general mechanism
does not. **Explore** is a proposed extension. These statuses describe the Gojo
checkout, not availability of every mechanism in QuickDraw Studio.

| Mechanic | What must remain connected | Status and compact opportunity |
| --- | --- | --- |
| Identity | The actual participant or site, distinct from an equal value | In. Local references can be small indexes. |
| Ordered relation and group | Which participant precedes or points to which | In. Ordered reference pairs and shared subgraphs. |
| Open role and binding | The unbound slot and later supplied participant | In. An explicit open form, never an invented zero. |
| Direction and reversal | A selected ordered frame and its traversal | In. A bit can choose between a known pair in a shared frame. |
| Flow face | The forward/opposed carrier-face pair and its unit correspondence | In. Distinct from reverse traversal and signed position. |
| Signed location | Position relative to that carrier's origin | In. Do not borrow the flow-face bit to encode it. |
| Orientation | Relation of a local frame to another exposed frame | Partial. Observer-relative alternatives can be compact; general bearing needs more structure. |
| Above, below, left, right, between | A presentation/order basis and the participants compared | In selected qualitative and spatial reads. No universal screen or gravitational direction. |
| Equal, less, greater, near, far | Ordered domains or selected comparison/calibration | Partial. Near and far require a scale or relational neighborhood. |
| Zero, small, medium, large | Distinct native states under a local vocabulary | In finite domains; selecting useful boundaries remains a modeling choice. |
| Minimum, maximum, boundary, wrap | The whole, limits and accounting behavior | In selected scalar and domain laws. Not synonymous with small/large. |
| Alternative, contradiction, unknown | Closed supported members versus an open remainder | In. Support masks work for closed finite alternatives. |
| Unit, calibration and resolution | Tick promise, correspondence and distinctions exposed | In. Share these once across compatible occurrences. |
| Accounted and orthogonal participation | The current order and what it cannot account for | In footholds. Both can coexist; a universal aligned/orthogonal flag would lose this. |
| Point, endpoint, interval, common site | Grounded carrier/location relations and actual aliases | In. Shared reference, proximity and equal coordinate remain different. |
| Whole, seam, contour and radius | Common reach, attached radius, traversal and actual return | In a selected circle contract. A point outlet alone is insufficient. |
| Partition and turn | Equal subdivision of an actual whole, selected landmarks and traversal | In bounded contour/frame constructions. Degrees are a later calibrated read. |
| Interpolation and scaling | Compatible source/target spans, parameter and law | In affine/equation readers. A generic relation alone is not interpolation. |
| Accumulation, repetition and feedback | Receiver state, controller, transition and later input | In selected native mechanisms. Timing and stopping conditions remain explicit. |
| Admit, block, filter and capacity | Legal lanes, passage, occupancy and response route | In selected contracts; a general automatic filter network remains partial. |
| Tension, residue, loss and provenance | Pending change, read remainder, forgotten detail and source/history respectively | In distinct surfaces. Optional shared extensions can avoid charging every small state for a full history. |
| Dormancy and resumption | Withdrawn participation plus retained identity/state | In core connection mechanisms. General utility-directed scheduling is partial. |

The existing [pre-numeric assessment](../../Gojo/Gojo/docs/GojoPreNumericAssessment.md)
demonstrates carrier-free local laws using relations and placeholders. The
[qualitative execution fixture](../../Gojo/Gojo/docs/GojoQualitativeExecutionExperiment.md)
derives compact masks from those native laws. `FiniteLawPlan` handles up to eight
states per domain; `FiniteLawBatchPlan` handles up to 64 with a captured shared
basis. These are useful precedents, not a persistent compact representation for
arbitrary Gojo worlds. Current native archives still preserve full integer and
reference tables.

## Shapes from these mechanics

A rectangle needs more than four sides. A pre-numeric rectangle can retain four
ordered sides, actual closure, opposite-side parallel relations and right-turn
relations without choosing its width or height. A square adds equal side reach.
A generic closed four-sided figure should remain less specific until those
relations are supplied.

Three interchangeable rectangle constructions illustrate why the procedure
belongs in the meaning:

- A turning path repeats `(long move, left turn, short move, left turn)` twice.
  It carries phase, side reach and seam closure. Local changes can accumulate
  mismatch at the seam.
- Two offset families start from two grounded directions and their offsets.
  They expose comparatively independent span/offset adjustments. Their relative
  orientation must still be connected; independence does not remove constraints.
- A centre construction uses opposite pairs with signed half-spans. It naturally
  preserves a centre when dimensions change. Four equally spaced points on a
  Euclidean circle would specifically produce a square; an arbitrary rectangle
  instead needs two opposite pairs with a suitable separation.

The existing [construction machines](../../Gojo/experiments/ConstructionMachines/README.md)
provide executable rectangle variants and common public interfaces. Open roles
and distinct responses are part of the experiment, even when the rendered
rectangle is initially identical.

For a circle, the implemented construction is point outlet, selected common
reach, radial attachment, nonzero whole traversal, and closure back to the same
reference site. Equal partitions then supply intrinsic landmarks. A bare outlet
does not establish a plane, an isotropic distance law, a radius or a quarter-turn.
The [circle fixture](../../Gojo/Gojo/docs/GojoCircleConstructionFixture.md) retains
these premises without Cartesian endpoints or trigonometry. General arc-length
calibration and arbitrary-angle embedding remain incomplete.

Connecting two points supplies topology. Reading that connection as a straight
segment, interpolating along it, or bending it through controls each adds a
selected construction. This matters when the same primitive relation connects
a roof to a wall: it must not implicitly interpolate every property they have.

Sine and cosine are plausible outcomes of small coupled machines, but motion
toward a target and accumulated speed alone do not establish them. A calibrated
example is coupled opposite feedback, `dx/dt = y`, `dy/dt = -x`; integration,
step size and radius behavior still need a selected law. Circular projection is
another construction. Their equivalence would be a useful experiment, rather
than a hidden assumption about every accumulator.

## Level two inventory

These are useful roles of connected machines, not proposed disjoint base types.
One machine may construct, compare and communicate simultaneously.

| Machine or role | Its useful contract | Present footing |
| --- | --- | --- |
| Path, boundary and shape maker | Expose sites, continuation, closure and adjustable construction | In rectangle, circle and affine/curve fixtures; not every geometric law is complete. |
| Joint maker and replacement | Keep incident ports while changing T, fan, split corners or continuation | In bounded joint experiments. General automatic topology discovery remains exploratory. |
| Attachment and correspondence | Carry obligations between public sites, including partial line membership | In native structures and church imports. |
| Symmetry, repetition and alignment | Relate instances through a common construction and basis | In selected shape/CAD experiments. Shared support must not be counted repeatedly. |
| Containment, clearance and support | State permitted arrangements and expose violations | In selected church and settlement experiments. Not universal physical simulation. |
| Error or counterexample maker | Describe the mismatch, its permission and route for possible response | In native error/claim fixtures. Reading an error alone does not move its participants. |
| Router or facilitator | Improve access to useful signals through exposed surfaces | Core/ConceptSurface footholds; general learned routing remains partial. |
| Gate, filter or inhibitor | Admit some participation and retain blocked or unaccounted participation | In selected lane/capacity laws; automatic specialization remains exploratory. |
| Predictor or comparator | Expose expected/actual correspondence and remaining disagreement | In equation and bounded learning fixtures. |
| Utility evaluator | Relate recipient benefit to costs and alternative uses | In bounded UtilityEcology experiments, not a universal credit law. |
| Memory and compression | Preserve a declared future read while sharing, abstracting or forgetting detail | Exact transport and derived execution exist; general adaptive compression is partial. |
| Dormant reusable structure | Withdraw participation without pretending prior deliveries never happened | In core detach/resume and lifecycle fixtures. |
| Creator, replacer and specialist | Propose variants, preserve port obligations, compare utility, withdraw/reuse | Bounded authored candidates and AdaptiveLifecycle exist; broad self-organization is exploratory. |

An overhang structure is a positive description of a potentially unwanted
relationship: roof extent, supporting extent, permitted overhang, excess and
response routes. It may correctly produce zero excess. If the permission is
unknown, it should remain unresolved. It is not simply a roof with its sign
flipped. The same distinction applies to absent contact, forbidden contact,
withdrawn connection, intentional clearance and an unexplained gap.

The commonality you identify is useful: physical-looking parts and seemingly
invisible constraints can use the same relation/equation substrate. Their
difference is what they expose, what they respond to and how they help neighbors.
There is no need to create a separate fundamental species for every role.

## A compact encoding direction

Start with a shared native definition for the local basis, ordered domains,
allowed read/response laws and reusable machine templates. Store occurrence
identity, small support/value state, port bindings and changed internal state
separately. Attach unusual residue, evidence or detailed history only when the
chosen read/replay contract needs it. Immutable definitions can be shared;
mutable width, tension or connections must remain per-instance unless sharing
that state is itself intentional structure.

For a domain containing exactly `zero, low, medium, high`, two bits select one
known member. Four bits can instead retain any closed subset:

| Support | Read |
| --- | --- |
| `0001` | Zero only, if the first state is grounded as zero |
| `0110` | Low or medium |
| `1111` | Any of the four declared states |
| `0000` | No supported member under these premises |

An open domain, where other possibilities remain unaccounted for, needs an
additional open surface or escape representation. It is neither `0000` nor
necessarily `1111`. These masks express admissible alternatives, not
probabilities; support weights need their own calibrated representation.

One additional bit could select a flow face if that particular pair is already
shared. Another could select one of two known observer directions. Neither is a
universal orientation bit. A read can carry both aligned participation and an
orthogonal remainder, so one more Boolean cannot replace that composition.
Negative location, opposed face, reversed frame, inverse unit and inhibition
must not collapse into a common negative flag.

When direction is already expressed by the order of two stored references, a
second direction field is redundant unless it denotes a different read. Prefer
one source of truth and derive the other presentation. Likewise, independent
field masks cannot express every correlation: `left and small, or right and
large` needs coupled alternatives if the mixed combinations are not allowed.
Bit packing must preserve that dependency or explicitly declare its loss.

The major saving may come from connections and repeated definitions rather than
from shaving a few bits off each value. A thousand windows can reference one
window construction while retaining a thousand occurrence identities and local
bindings. Exact compression must preserve which endpoints really are shared.
Equal coordinates and identical template bytes do not establish that identity.

This suggests three compatible execution forms:

1. The full inspectable native graph establishes the contract.
2. A shared-template representation retains instance state and explicit bindings.
3. A derived compact execution plan handles a bounded local question directly.

Expanding every compact item into a large graph for every update could lose the
runtime benefit. The existing finite batch plan is a useful pattern: validate a
captured law and use compact state for the selected batch. Structural edits must
invalidate or recapture that plan. Do not invent full historical witnesses for
compute-only calls, or promise finer reconstruction after discarding detail.

## Learning and specialization

The network itself can carry learned competence. A separate neural net need
not be the permanent owner of every decision. The current
[UtilityEcology](../../Gojo/experiments/UtilityEcology/README.md) and
[AdaptiveLifecycle](../../Gojo/experiments/AdaptiveLifecycle/README.md) experiments
already explore bounded utility feedback, paid refinement, retirement and
replacement. They are not a demonstration that unrestricted cells will
spontaneously specialize into a good church parser.

Specialization still needs concrete local mechanisms: variation proposals,
recipient feedback, attribution of benefit, cost and capacity, protection of
useful minority alternatives, and conditions for dormancy/reactivation. For a
facilitator, recipient improvement compared with a delayed or withdrawn route
could supply utility evidence. That comparison itself has a cost and a selected
context; it is not an omniscient assessment of global benefit.

Neighbor inhibition can encourage different roles, but it can also silence a
correct minority hypothesis too early. Repeated agreement can be the same
observation traveling in a loop. Preserve shared causes and distinguish
corroboration from recirculation. Weak interpretations used as training targets
must not become more trustworthy merely because several downstream structures
repeat them.

A common transport grammar is plausible; a single universally interchangeable
quantity is not established. Resource budget, displacement, support and error
can all use carriers while retaining different units, legal lanes and
calibrations. Useful learning may select conversions between them. It must not
silently equate them.

## Recommended next experiment

Use one adjustable rectangle, a joint attached at its upper boundary and an
overhang/clearance relation. Include reversed observers, unknown reach, shared
sites, independent equal sites and an intentional clearance. Run exactly the
same selected questions through the full native graph, shared-template state
and compact finite plan.

Measure bytes per occurrence including amortized templates and references,
allocation per update, time per local response and invalidation/rebuild cost.
Require matching coarse answers, preserved alternatives and identical accepted,
blocked and remaining participation at the declared resolution. Change a joint
construction and check that external ports and dormant memory survive. Refine a
coarse read explicitly and verify that it does not invent discarded detail.

This tests the compact representation before asking it to discover its own
vocabulary and utility laws. After that comparison, allow one local structural
choice to adapt from recipient feedback and compare it with a fixed authored
choice and a small learned selector. That provides evidence for which mechanisms
can become native learned structure, without assuming a neural network is either
necessary or unnecessary.
