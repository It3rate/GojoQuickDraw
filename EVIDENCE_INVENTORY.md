# QuickDraw evidence inventory

This is the working inventory from the evidence review, including geometry,
objects, parts, construction, scene context, and higher-level meaning. It is a
research backlog, not a closed taxonomy or a claim of reliable recognition.
Meaning belongs to connected Gojo structure; names describe inspection reads.

- **in**: a current converter, on-demand pass, or authored control represents or
  checks it. The scope matters: authored membership is not discovered membership.
- **plan**: previously discussed or documented, but not generally implemented.
- **new**: an additional proposal from the inventory review.

The initial inventory distinguishes fitted observations, seeded hypotheses,
defaults, and goals. Implementation notes added during subsequent work do not
retroactively turn an authored control into an automatic recognizer.

Current extension: [church area estimates](CHURCH_LAYOUT_ESTIMATES.md) adds ideal
ground/building/tower/roof/cross/opening placements (**in**, authored hypotheses),
including several triangular or domed caps and separate markers. Proposed
roof-tower-wall and ground-wall/door contacts share actual construction sites;
they do not establish contact between acquired points. Optional openings and
alternative building areas remain revisable. General physical support, reliable
part recognition and learned regional likelihoods remain **plan**.

[Boundary mapping](CHURCH_BOUNDARY_MAPPING.md) now connects proposed boundaries to
whole/partial fitted intervals, actual endpoints and existing construction parts.
It retains shared support and the complete unresolved source complement. These
are **in** as authored correspondence hypotheses, not confirmed semantic ownership.

## 1. Acquisition and source

| Evidence | Status | Scope |
| --- | --- | --- |
| Original sampled positions | in | Exact acquisition retained separately from fitted geometry. |
| Pen strokes and pen lifts | in | Acquisition boundaries, not object boundaries. |
| Sample order | in | Preserved within strokes. |
| Stroke order | in | Preserved. |
| Per-point time when supplied | in | Raw XYT records; simplified XY records have open timing. |
| Original identity and source location | in | Acquisition, file, and record correspondence. |
| Supplied category and recognition flag | in | Source context, not structural truth. |
| Bounds, aspect ratio, normalization, calibration | in | Source/page correspondence retained. |
| Fitted interval boundaries and sample support | in | Several intervals can belong to one stroke. |
| Fit discrepancy and retained detail | in | Original observation survives approximation. |
| Empty, repeated, point-like, degenerate samples | in | Preserved or explicitly qualified. |
| Speed, pauses, acceleration | plan | Derived where timing supports them. |
| Retracing, correction, hesitation, deliberate overdraw | plan | Competing process interpretations. |
| Temporal grouping of strokes into parts | new | Timing supports but does not prove membership. |
| Pressure, tilt, actual stroke width | new | Additional sources required when not acquired. |
| Contributor/device context | plan | Conditioning when available. |

## 2. Lines and curves

| Evidence | Status | Scope |
| --- | --- | --- |
| Ordered start and end | in | Explicit from/to sites and first/last controls, including curves. |
| Point-like versus extended trace | in | Degenerate direction remains unresolved. |
| Straight segment | in | Measured straightness with an allowance. |
| Quadratic curve | in | Current source fitter. |
| Cubic curve | in | Imports, construction, later interpretation. |
| Horizontal, vertical, rising diagonal, falling diagonal | in | Separate direction candidates in a selected frame. |
| Arbitrary heading and traversal direction | in | Retained independently of coarse labels. |
| Chord length versus sampled path length | in | Separate measurements. |
| Straightness versus aligned endpoints | in | A curve can have horizontally aligned ends. |
| Endpoint tangents | in | Finite profile derivatives. |
| Tangent agreement | in | Continuation candidate, not attachment. |
| Signed bend relative to ordered chord | in | Reversal changes the side read. |
| Excursion and fullness | in | Limited finite-profile and earlier curve reads. |
| Inward/outward curvature | plan | Needs an oriented interior or reference. |
| Convex/concave boundary portion | plan | Conditional on interior. |
| Inflection | plan | Upstream mechanisms; not general extraction. |
| Cusp/tangent reversal | plan | Upstream mechanisms; extraction incomplete. |
| Curvature continuity | plan | Stronger than tangent agreement. |
| Approximately constant curvature | new | Circular-arc evidence. |
| Straight/curve and curve/curve transitions | plan | Smooth versus distinct corner. |
| Retracing a line or curve | plan | Not an additional directional arm. |
| Jitter versus meaningful small bend | plan | Retain alternatives. |
| Overshoot and undershoot | plan | Relative to proposed meeting points. |
| Dashes implying a continuous line | plan | Actual gaps remain observed. |
| Thickness, taper, asymmetric width | plan | Earlier band mechanisms; generally absent in QuickDraw centreline data. |
| Silhouette, internal edge, crease, texture, construction-line role | new | Geometry alone does not establish role. |

## 3. Joints and encounters

Letters summarize directional arrangements, not physical attachment. O is a
closure read, not a degree-two joint. K is this experiment's display convention.

| Type | Status | Meaning |
| --- | --- | --- |
| Isolated site | in | Authored reader; no incident ray. |
| Endpoint | in | One outward ray. |
| I | in | Two approximately opposed rays. |
| L | in | Two turning rays, not necessarily perpendicular. |
| Right-angle L | in | Additional perpendicular agreement. |
| Angled L | in | Turn without an admitted right-angle claim. |
| T | in | Three rays with an opposed pair. |
| Y | in | Three rays without an admitted opposed pair. |
| X | in | Four rays covered by two disjoint opposed pairs. |
| K | in | Four rays with opposed support but not a second disjoint opposed pair. |
| General four-way | in | Four rays outside X/K arrangement. |
| Complex junction | in | Beyond the simple vocabulary. |
| Coincident/co-directed rays | in | Ambiguous; do not manufacture another arm. |
| Unresolved/degenerate joint | in | Insufficient directional support. |
| Smooth continuation | in | Opposed endpoint tangents within allowance. |
| O / closed cycle | in | Closure candidate, with observed gap retained. |
| Cusp | plan | Reversal at a joined path. |
| Convex corner | plan | Requires interior context. |
| Concave corner/notch | plan | Requires interior context. |
| Rounded corner | plan | Curved transition serving a corner role. |
| Tangential contact | plan | Touching without necessarily joining. |
| Endpoint/endpoint encounter | in | Shared identity and proximity distinguished. |
| Endpoint/interior encounter | in | T appearance or near miss. |
| Interior/interior crossing | in | Finite geometric candidate. |
| Actual shared-site join | in | Acquisition, authored, or constructed topology. |
| Coincident independent sites | in | Equal position without shared identity. |
| Near miss/open gap | in | Does not silently become contact. |
| Over/under crossing | in | Authored controls; inference planned. |
| Crossing continuation pairing | plan | Which strands continue together. |
| Split versus merge | plan | Directional branching interpretation. |
| H-like bridge | plan | Two branching sites connected by a span. |
| F-like ordered branches | plan | Attachments along a continuing member. |
| Theta-like routes | plan | Alternative paths enclosing regions. |
| Radial hub/multi-joint attachment region | new | Several organized arms. |
| Joint through an occluded interval | plan | Expected topology with missing support. |

## 4. Shape vocabulary

A shape should expose its sides, turns, curves, partitions, shared sites and
construction. Finite polynomial curves do not certify exact Euclidean circles.

| Shape | Status | Scope |
| --- | --- | --- |
| Point/point-like mark | in | Filled-dot meaning is additional. |
| Open straight segment | in | Fit and construction. |
| Open polyline | in | Ordered connected fragments. |
| Smooth open curve | in | Finite quadratic/cubic. |
| Circular-looking arc | in | Finite profile/construction. |
| Elliptical-looking arc | in | Imported/authored finite profile. |
| Semicircle/half-oval profile | in | Controls and arch-like profiles. |
| Circle | in | Controls; source detector currently supplies weaker oval evidence. |
| Ellipse/oval | in | Near-closed source candidate and controls. |
| Generic closed loop | in | Near-closure or explicit closure, distinguished. |
| Rectangle | in | Seeded body frame and on-demand primitive. |
| Square | in | Controls/reference data; general equality-based discovery planned. |
| Generic quadrilateral | plan | Four runs without assuming rectangle. |
| Parallelogram | in | On-demand side-plane construction. |
| Rhombus/diamond | in | Authored windows and prior controls. |
| Trapezoid | new | Projected/tapered quadrilateral candidate. |
| Kite | new | Adjacent equal-side pairs and possible mirror axis. |
| Triangle | in | Controls and roof interpretation. |
| Open peak/chevron | in | Upper slopes without inventing a base. |
| Inverted triangle | in | Construction engine. |
| Isosceles triangle | in | Symmetric construction; general detection planned. |
| Equilateral triangle | plan | Equal sides and turns. |
| Right triangle | plan | Supported perpendicular corner. |
| Scalene/acute/obtuse/slender triangle | new | Alternatives and qualifiers. |
| Pentagon | new | Five sides; regularity separate. |
| Hexagon | plan | Broader interior-shape direction. |
| Heptagon | new | Seven sides. |
| Octagon | plan | Source category exists; structural detector needed. |
| General n-sided polygon | plan | Count, closure, turns and regularity separate. |
| Regular polygon | plan | Equal side/turn expectations. |
| Irregular/concave polygon | plan | Do not force regular convexity. |
| Rounded rectangle | new | Straight runs with rounded corners. |
| Capsule/pill | new | Rounded ends and connecting sides. |
| Rounded arch | in | Controls and whole-arch construction. |
| Pointed arch | in | Authored openings; general extraction planned. |
| Flat/asymmetric arch | new | Alternatives to symmetric arch. |
| Dome-like upper contour | in | Controls and limited synthesis. |
| Left-facing crescent | in | Shared tips and facing retained. |
| Right-facing crescent | in | Different ordered frame. |
| Arbitrarily oriented crescent/fullness | plan | General extraction. |
| Cross/plus | in | Candidate arms and construction. |
| Diagonal cross/saltire | new | Orientation and symbolic role separate. |
| Star | in | Circular partitions, insets/outsets, ordered joins. |
| Radial rosette | plan | Repeated parts around a centre. |
| Organic closed contour/blob | in | Controls; broad recognition planned. |
| S-curve | plan | Also a completion alternative. |
| Wave/scalloped contour | plan | Repeated bend and phase. |
| Zigzag | plan | Alternating turns. |
| Spiral | new | Winding with varying reach. |
| Ring/annulus | new | Outer/inner regional boundaries. |
| Petal/leaf/lens | new | Paired curves, tips and possible common construction. |
| Teardrop | new | Rounded region with a concentrated tip. |
| Heart-like contour | new | Paired lobes and lower tip. |
| Stepped/notched outline | plan | Retain meaningful recesses. |
| Branching/tree-like skeleton | plan | Topology separate from biological meaning. |
| Capitals A B C D E F G H I J K L M N O P Q R S T U V W X Y Z | in | Authored controls/ladders; general glyph recognition planned. |
| Cuboid/prism/cylinder/cone/sphere/hemisphere interpretation | new | Possible 3D explanation, not a new 2D primitive tag. |

Applicable qualifiers: open/closed; complete/partial; convex/concave;
symmetric/asymmetric; regular/irregular; repeated/unique; observed/inferred;
approximate/exact under an explicit premise.

## 5. Topology, enclosures and containment

| Evidence | Status | Scope |
| --- | --- | --- |
| Shared sites and incidence | in | Structural connectivity. |
| Connected source traversal | in | Does not prove one object. |
| Proposed connection between acquired events | in | Later target construction; source identities separate. |
| Connected components | plan | Beyond individual pen strokes. |
| Open boundary | in | Not automatically closed. |
| Near-closure and gap | in | Separate from completed interpretation. |
| Explicit closed boundary walk | in | Controls and construction. |
| Closure assembled across strokes | plan | General discovery incomplete. |
| Elementary versus subdivided enclosure | plan | Earlier interior mechanisms not generally integrated. |
| Interior region | in | Authored; discovery planned. |
| Exterior region | plan | Supported boundary and embedding required. |
| Hole/void | in | Authored; inference planned. |
| Nested regions/holes | plan | Qualified containment hierarchy. |
| Container candidate | in | Seeded wall/body bounds. |
| Containment relation | in | Mainly bounds and selected construction checks. |
| Inside/outside/touching/straddling | in | Finite clearances; arbitrary regions planned. |
| Partial overlap | plan | Neither containment nor contact. |
| Curved/nonconvex containment | plan | Beyond bounds. |
| Containment under a missing-boundary hypothesis | plan | Missing boundary stays assumed. |
| Open container/entrance/gap | plan | Enclosure need not be impermeable. |
| Boundary clearance and margin | in | Signed frame comparisons. |
| Border ownership | plan | Which adjacent region owns the boundary. |
| Figure/ground | in | Authored; inference planned. |
| Interior mark versus physical opening | new | An interior circle need not be a hole. |
| Filled area/outline/centreline | plan | Distinct readings. |
| Region adjacency/shared boundary | plan | Sharing does not merge objects. |
| Topology changed by repair | new | Loops created, gaps removed, components joined. |

## 6. Position, extent and alignment

| Evidence | Status | Scope |
| --- | --- | --- |
| Relative positions/bounds | in | Selected numeric page basis. |
| Left/right/above/below | in | Comparisons; qualitative publication planned. |
| Between | plan | Objects or boundaries. |
| Near/far relative to local scale | plan | Beyond fixed allowances. |
| Same horizontal level | in | Limited paired placement. |
| Shared baseline | plan | Ground, sill, text or other reference. |
| Shared axis/centreline | in | Symmetry and construction. |
| Centred within a part | in | Limited seeds and construction. |
| Centred crossbar join | in | On-demand regularization. |
| Parallel | in | Profile candidates. |
| Perpendicular | in | Page geometry, not Gojo's general orthogonal concept. |
| Collinearity/nearly collinear fragments | in | Continuation/simplification. |
| Equal length | plan | General equivalence grouping. |
| Equal angle | plan | Beyond selected joint comparisons. |
| Equal size/similar extent | in | Limited profile/opening comparisons. |
| Same aspect ratio | plan | Similarity under scale. |
| Smaller/larger/narrow/wide/short/tall | plan | Explicit comparison basis. |
| Relative spacing/spacing ratio | plan | Group organization. |
| Equal spacing | plan | Separate from repeated shape. |
| Object-relative alignment | plan | Internal frame of a tilted object. |
| Orientation relative to parent/path/surface | plan | Attachments and repeated parts. |
| Order surviving coarse resolution | in | Authored resolution controls. |

## 7. Objects, parts and separation

| Evidence | Status | Scope |
| --- | --- | --- |
| Explicit authored object membership | in | Connected member scopes. |
| Source lines supporting proposed church parts | in | Provisional parts. |
| General inferred object membership | plan | Not reliable general recognition. |
| General inferred part membership | plan | Parts retain independent participation. |
| Several strokes forming one object | in, bounded church pass | Endpoint-chain and local contour proposals can cross pen lifts; branching/ownership remain revisable. |
| One stroke contributing to several parts | in, boundary mapping | Whole or partial fitted intervals can support several boundaries; one acquisition cause. |
| Same object versus similar objects | in, bounded candidates | Source-membership identities, distinct copies, and repeated-part relations; general template inference remains planned. |
| Object separation | in, bounded candidates | Disjoint local part scopes and separation opinions; physical independence remains unproven. |
| Distinct touching objects | in | Controls; inference planned. |
| Distinct objects crossing in projection | in | Controls and crossing candidates. |
| Attached but distinct parts | plan | Attachment preserves part identity. |
| Contained but independently movable participant | plan | Containment versus coupling. |
| Parent/part hierarchy | plan | Potentially several levels. |
| Alternative/overlapping memberships | plan | Do not force a single partition. |
| Boundary shared by two parts | in, provisional | Actual scaffold contacts plus non-exclusive source interval associations; confirmed ownership remains open. |
| Inferred boundary without drawn ink | in | Separate endpoint/construction support and explicit uncovered target intervals. |
| Unresolved source portions | in | Complete profile complement and unprofiled acquired samples remain inspectable. |
| Common construction frame | in | Authored/generated. |
| Common template, separate instances | in | Authored repeat and limited reuse. |
| Common rigid movement | plan | A common object-frame displacement. |
| Common scale/affine deformation | plan | Not the same as rigid movement. |
| Articulated movement | new | Connected parts move around a joint. |
| Flexible coupling | new | Response without rigidity. |
| Anchored member constraining a group | plan | Bidirectional support. |
| Splitting/regrouping | plan | Test different membership. |
| Object identity across interpretations | plan | Source correspondence already exists. |
| Unassigned lines | in | Not automatically decoration. |
| Opposition to grouping | plan | Conflicting responses or better separate explanations. |

## 8. Gestalt expectations

| Evidence | Status | Scope |
| --- | --- | --- |
| Proximity | in | Qualified neighbourhoods. |
| Profile similarity | in | Translation/reflection/extent. |
| Compound part similarity | plan | Beyond individual curves. |
| Good continuation | in | Tangent and merging candidates. |
| Closure | in | Observation and completion distinct. |
| Containment/common region | in | Limited relationships. |
| Connectedness | in | Actual versus proposed. |
| Reflection symmetry | in | Local/global scope and unmatched support. |
| Repetition | in | Profile pairs and controls. |
| Parallel organization | in | Candidates and preferences. |
| Fewer, longer coherent lines | in | Authored simplification. |
| Whole coherent curves | in | Authored simplification. |
| Simple supported shapes | in | Restricted primitive preferences. |
| Balance/centring | in | Limited construction. |
| Shared fate/response under variation | in | Restricted probes; generic coupling planned. |
| Independent fate/object separation | plan | Competing independence claim. |
| Clustering | plan | Frame, membership, density, outliers. |
| Within-cluster cohesion versus between-cluster separation | new | Internal proximity alone insufficient. |
| Group count | plan | Cardinality and membership. |
| Repeated-instance consistency | plan | Shared expectations with incomplete instances. |
| Whole constraining parts | plan | Top-down expectations. |
| Parts constraining whole | plan | Bottom-up anchors. |
| Convexity preference | plan | Conditional and defeasible. |
| Negative-space organization | plan | Shape of gaps between participants. |
| Local exception within regular pattern | plan | Keep pattern and exception. |
| Temporal synchrony/drawing rhythm | new | Where timing helps. |
| Salient anchor versus subordinate detail | plan | Learn importance. |
| Competing organizations of the same marks | plan | Preserve alternatives. |

## 9. Transform and construction machines

| Evidence | Status | Scope |
| --- | --- | --- |
| Local axis-aligned reflection | in | Axis and correspondence. |
| Whole/partial reflection | in | Coverage and unmatched support. |
| Arbitrary reflection axis | plan | Beyond current extraction. |
| Translation | in | Reversal and residual retained. |
| Uniform scaling | in | Limited on-demand reuse. |
| Independent X/Y scaling | in | Limited affine reuse. |
| Rotation/rotational symmetry | plan | Centre, turn, count, correspondence. |
| Glide reflection | plan | Earlier vocabulary. |
| Shear/general affine correspondence | plan | Beyond restricted reuse. |
| Projective correspondence | new | Perspective explanation. |
| Radial partition and alternating inset/outset | in | Star machine. |
| Linear repetition/shared displacement | in | Construction and profile pairs. |
| Repeat count/phase/spacing/extent | plan | Discover controller. |
| Repetition along a curve | new | Path-based attachment. |
| Nested repetition | plan | Several controller levels. |
| Alternation | new | A-B-A-B rather than copies. |
| Shared template versus independently similar instances | plan | Distinct explanations. |
| Fixed versus adjustable controls | in | Programs and probes. |
| Counterfactual variation/violations | in | Limited exposed constraints. |
| Equivalent machines under current tests | in | Mirror and centred repetition may tie. |
| Selecting a discriminating variation | plan | No premature causal identification. |
| Broken symmetry/imperfect repetition | in | Residual; richer exceptions planned. |

## 10. Church parts and generic relations

| Church description | Generic structure | Status |
| --- | --- | --- |
| Main body/facade | Large upright frame with boundary support | in, seeded |
| Wall | Boundary/surface potentially containing openings | in, provisional |
| Roof | Upper converging/enclosing/curved structure | in, limited |
| Roof connected to wall | Attachment at actual/proposed sites | in, provisional local-frame attachment opinions; observed contact remains separate |
| Roof overhang | Extension beyond lower container | in, offset/narrow area estimates; attachment reasoning limited |
| Steeple/peak | Slender upper extension relative to body | in, seeded |
| Tower | Upright substructure attached/beside body | in, local cap-supported area hypotheses; missing divider remains inferred |
| Multiple towers | Distinct upper parts with count/placement | in, separate regional proposals; reliable recognition planned |
| Rounded tower/dome | Curved upper closure | in, triangle/half-ellipse profile comparison and finite ideal scaffold |
| Cross | Crossing arms and candidate centre | in |
| Multiple/partial crosses | Independent markers; missing arm distinct from observed crossing | in, limited upper marker proposals and explicit partial assumptions |
| Cross on upper part | Attachment and above/near-top relation | in, limited construction |
| Door | Lower, often central opening | in, ambiguous seed |
| Window | Smaller interior opening | in, ambiguous seed |
| Door/window within wall | Containment and margins | in, mainly bounds |
| Window frame | Several lines belonging to one part | plan |
| Repeated windows | Shared template/placement expectations | in, controls/limited pairs |
| Circular/rose window | Round contour and possible radial subdivisions | in, controls; role discovery planned |
| Side wall | Connected separate projected plane | in, on-demand synthesis |
| Parallelogram side wall | Opposite directions and connected corners | in, affine hypothesis |
| Hidden ground | Possible support at lower extent | in, hidden seed, not ink |
| Building supported by ground | Support rather than proximity | plan |
| Ridge/eaves/corner returns | Upper boundaries and plane changes | new |
| Entrance steps | Repeated supports/access route | new |
| Bell opening/belfry/porch/annex/nave | Attached/contained substructures | new |
| Clock/cross/window alternatives | Competing small upper-part roles | plan |
| Ornament versus structural part | Role and importance | plan |
| Optional absent cross/door/windows | Defeasible category expectations | plan; some unsupported additions already avoided |

## 11. View, scene and physics

| Evidence | Status | Scope |
| --- | --- | --- |
| Front view | in | Heuristic candidate. |
| Left/right oblique view | in | Exterior slope/boundary-joint candidates. |
| Top view | plan | Stored open candidate, no discriminating inference. |
| Pure side/rear/bottom view | new | Additional observer relationships. |
| Elevated/lowered camera | plan | Currently open. |
| Object rotation versus camera change | plan | Different causes. |
| Visible faces and adjacency | plan | Partial 3D explanation. |
| Depth and occlusion | plan | Beyond authored declarations. |
| Perspective convergence/vanishing directions | new | Projective evidence. |
| Foreshortening | new | View-induced size change. |
| Page axes versus object axes | plan | Distinct frames. |
| Page-down versus gravity-down | plan | Do not equate them. |
| Ground/table/hanger/support context | plan | Changes expectations. |
| Physical scale | plan | Open slot; image size is not world size. |
| Rigid/flexible material | plan | Conditions plausible response. |
| Support/balance/load path | plan | Needs scale/material premises. |
| Tension versus compression support | new | Ropes, branches, structures. |
| Suspended versus resting | plan | Gravity/support context. |
| Support/gravity deformation | plan | Different from intended geometric construction. |
| Impossible versus unusual construction | new | Retain premises and uncertainty. |
| On/under/behind/supported by/attached to/contained in | plan | Scene relationships. |

## 12. Meaning, function and anomalies

| Evidence | Status | Scope |
| --- | --- | --- |
| Supplied category | in | Context without certifying parts. |
| Inferred category | plan | Separate hypothesis. |
| Part identity/function | plan | Names are currently seeds. |
| Common category arrangement | plan | Conditional part relationships. |
| Subtype/category variation | plan | Different structures, views, optional parts. |
| Natural/biological/man-made/abstract prior contexts | plan | Explicitly deferred. |
| Same category, different construction | plan | Avoid one mandatory template. |
| Similar construction, different category | plan | Geometry does not establish identity. |
| Required versus optional part | plan | Conditions and exceptions. |
| Decoration versus defining structure | plan | Retain both. |
| Recurring unexplained residue | plan | Potential missing concept. |
| Envelope flap | plan | Boundary and closure-related structure. |
| Optional wax seal | plan | Repeated anomaly motivating expectation. |
| Functional opening/passage | plan | Stronger than an interior contour. |
| Container and possible contents | plan | Including thin envelope contents. |
| Hinge/fold/closing/sealing | plan | Long-term structural function. |
| Tamper evidence versus decorative seal | plan | Context changes function. |
| Letter/symbol recognition | plan | Authored letters are not a recognizer. |
| Intentional versus accidental gap | plan | Competing explanations. |
| Intentional asymmetry versus error | plan | Competing explanations. |
| Conceptual simplification versus tracing | in | Separate source and interpretation. |
| Intended machine versus acquired procedure | plan | Do not equate them. |
| Skill/mouse difficulty/individual style | plan | Possible conditioning, not established fact. |
| LLM anomaly explanation | plan | Hypothesis with examples/counterexamples/tests. |
| Explanation failure | plan | Evidence for revising a concept. |
| Context-dependent purpose | new | Same shape, different use. |
| Symbolic versus literal depiction | new | Symbol versus depicted physical object. |

## 13. Strength, uncertainty and contradiction

| Evidence | Status | Scope |
| --- | --- | --- |
| Actual support participants | in | Source intervals/parts connected. |
| Reference basis and resolution | in | Required for measured meaning. |
| Explicit allowance | in | Authored, not learned probability. |
| Measured mismatch | in | Retained with admitted interpretation. |
| Support and opposition | in | Gojo mechanism; consistent contextual use planned. |
| Observation/hypothesis/default/goal | in | Structural vocabulary; not all proposals equally developed. |
| Competing candidates | in | Body/view/machine alternatives. |
| Unassigned/unexplained evidence | in | Visible complement. |
| Missing participation/open slots | in | Unresolved context. |
| Conditional claim | in | Premises retained. |
| Failed/rejected proposal | in | Interpretation history. |
| Source versus conceptual discrepancy | in | Separate diagnostics. |
| Prior frequency versus observation reliability | plan | Different quantities. |
| Learned expectation strength/range | plan | Relation/category priors currently authored. |
| Learned likelihood and calibration | plan | Explicit conditioning required. |
| Shared cause | in | Provenance; full dependency handling planned. |
| Correlated versus independent evidence | plan | Repeated reads are not independent votes. |
| Negative evidence | plan | Only when the missing feature should be observable. |
| Absent/unobserved/occluded/optional | plan | Four different states. |
| Not found versus ruled out | plan | Search limits remain visible. |
| Selection/search capacity reached | in | Some bounded reads expose it. |
| Stable anomaly versus isolated noise | plan | Missing expectations. |
| Confidence calibrated by feedback | plan | Not arbitrary cost/threshold. |
| Which participant should yield | plan | Reliability/resistance/context. |
| Unidentifiable alternatives | in | Ties retained. |
| Local exception/waiver | plan | Does not erase parent pattern. |

## 14. History, refinement and change

| Evidence | Status | Scope |
| --- | --- | --- |
| Earlier construction references | in | Actual dependencies. |
| Introduction before reference | in | Forward reference rejected. |
| Reusable instruction versus execution | in | Distinct structures. |
| Before/after interpretation | in | Rungs/operations. |
| Carried/replaced/introduced/suppressed detail | in | Separate roles. |
| Source/output correspondence | in | Can be many-to-many. |
| Shared constructed joints | in | Actual target identity, not coordinate equality. |
| Retained/faded guides | in | Visibility does not remove participation. |
| Reduced position resolution | in | Authored church/letter controls. |
| Reduced evidence count | in | Approximation/simplification. |
| Preserved count/order under geometric variation | in | Controls; general learning planned. |
| Preserved symmetry/repetition under variation | in | Selected authored correspondence. |
| Fully pre-numeric hints | plan | General reconstruction incomplete. |
| Task-selected variable-length publication | plan | Only needed exposed evidence. |
| Explicit loss from discarded detail | plan | Preserve coarse promise. |
| Bidirectional adjustment of related parts | plan | Probes expose choices, not general settlement. |
| Membership tested through variation | plan | Object ownership/separation extension. |
| Stale dependencies after upstream change | plan | Broader mechanisms; integration incomplete. |
| Several plausible constructions | plan | Beyond several fits of one topology. |
| Independent executed-output readback | in | Do not trust labels as execution. |
| Feedback bound to exact claim/occurrence | plan | No substitution of a similar instance. |

## Container movement default

The user proposes this logical/physical default: changing the placement of a
contained participant moves that participant; changing the placement of its
container also carries its contained participants. This has directional cause
and effect while remaining compatible with later bidirectional correction.

Represent the containment read separately from the proposed frame coupling.
For a selected container displacement `d`, a carried child's displacement
consumes that same actual `d` through an exposed relation. Nested descendants
must receive it once, not once per route. A child edit need not alter the parent.
External anchors, alternative parents, independent contents, clipping and failed
containment remain explicit qualifications or competing hypotheses. Merely
having equal coordinates never establishes this coupling.

The current intervention is an observational adapter experiment, not general
native physical settlement. A later material implementation must preserve the
port/projection/admissibility/transfer/capacity/settlement/timeline distinctions.

## Replaceable opinion providers

The intended progression is shared interfaces for algorithms, heuristics, and
small models, not one opaque category classifier. The proposed native shape is:

```text
actual source profiles -> selected input surface
earlier claim witnesses -> explicit dependencies
selected inputs -> expected sites/ordered edges/open roles
cause -> claim -> support/opposition on the connected evidence ladder
observation / hypothesis / default / goal scopes -> provenance
```

A rectangle hypothesis can expose four expected corner sites and incident sides.
That can support a separate L/join expectation over the relevant source profiles.
A roof hypothesis can consume the rectangle and upper slopes. These are
dependent opinions, not fresh independent observations. Contrary measured gap
evidence remains present. A model can supply one opinion through the same
input/output contract while retaining its version, training basis and limits.
There is no learned arbiter or general belief-propagation solver in this warm-up.

## Warm-up implementation following the inventory

The new **Full analysis** pass adds limited automatic candidates without claiming
the general recognizers in the tables are solved:

- connected acquisition traversals and possible near-closed contour groups;
- provisional part membership and separate opening instances;
- rectangle/triangle candidates and dependent corner/join expectations;
- actual sharing of earlier expected shape sites by later corner claims;
- source-linked support/opposition envelopes for replaceable opinion providers;
- a revisable parent-carries-contents default and nested movement probes;
- all three source annotation allowances and seven authored church proposal views;
- explicit endpoint/direction inspection and paged access to complete collections.

General object discovery, learned part/category priors, arbitration between models,
pre-numeric generation and native physical feedback remain **plan**. Full native
construction execution stays on demand; cached proposal views are labelled separately.

## Shape refinement extension — 26 October 2026

- **in:** provisional outlined-cross contour reads, including several strokes
  and open ends; inferred center lines stay separate from outline ink.
- **in:** oblique roof-band candidates and adjacent supported tower candidates.
- **in:** a separate authored shape cleanup, selected before/after site
  correspondences, actual shared parent/child attachment sites, and numeric
  mirror residuals. Original source and earlier estimates remain intact.
- **in:** explicitly prior-only optional entrance/window additions; acquisition
  order is retained separately from proposed construction dependency order.
- **in:** increasing cross-scale refinement with face-relative placement;
  wall rectangles partitioned around towers with connected construction frames,
  earlier-source correspondence and explicitly shared boundary sites.
- **plan:** learned placement/view priors, general perspective recovery,
  autonomous feedback arbitration and causal inference from pen order.

See [church shape cleanup](CHURCH_SHAPE_CLEANUP.md) for the executable scope.

## Sources

- [Source annotation implementation](../Gojo/experiments/quickDraw/tools/quickdraw_annotations.py)
- [Authored line-art contract](../Gojo/experiments/quickDraw/LineArt.md)
- [Context candidates](gojo_quickdraw/context.py)
- [Design and training direction](docs/design.md)
- [Canonical Gojo instructions](../Gojo/Gojo/docs/GojoSystemPrompt.md)
