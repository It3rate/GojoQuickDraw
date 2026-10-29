# Capturing church contacts and repeated parts

This opt-in extension of `church_refine` tests a distinction that the previous
experiment lacked: attracting independent constructions toward a relationship,
then choosing a construction that maintains that relationship exactly.
Ordinary conversion and the earlier experiments retain their existing behavior.

## What was missing

The previous attachment intervals intentionally accepted a small gap. Outside
those intervals, damping, finite response capacity, opposing source opinions and
quarter-unit rounding could leave a nonzero compromise. More passes do not
necessarily remove that compromise. Wall partitioning excluded tower interiors
from the wall, but did not prevent neighboring towers from overlapping each
other. Opening pairing also rejected a circle/rectangle pair before considering
whether one provisional family choice should be revised.

There was no two-window limit behind the reported omissions. There is an older
eight-window proposal safety budget. In the reported four-window example, one
floating outline had instead become a floor-reaching door envelope, and its
incomplete counterpart had failed enclosure admission.

## Inference changes

`church_opening_context.revise` rereads the acquired intervals of openings,
independently of the earlier envelope. A floating secondary door can become a
window when a grounded main door and a reflected source-supported counterpart
provide context. An incomplete outline requires a supported counterpart; it is
not admitted just because an empty wall could contain a window. Multiple rows
are considered independently, without prescribing their number.

Each pair compares the separate source family costs with the cost of a common
rectangle, arch or ellipse family. Long, straight upper corner evidence supplies
an additional opinion; tiny hooks and polygonal sampling of curves do not count
as those corners. Strong evidence for different families can reject a shared
family. The earlier role/shape, supporting intervals, candidate costs and reason
for revision remain inspectable. This is authored inference, not a new trained
classifier or human annotation. Family/role inference occurs once at admission;
it does not run continuously during the eight settlement passes.
The experimental corner opinion subtracts .020 from rectangular cost and adds
.025 to ellipse cost per substantial upper corner. Unsupported partial outlines
carry one-fifth of the family-cost weight. These authored values and the .035
shared-family regret allowance are inspectable choices, not calibrated odds.

## Capture, hold and release

The adapter proposes affine binding recipes over exposed left, top, right and
bottom reads. Intrinsic extent inputs support centering without declaring a
semantic port type. Runtime settlement does not dispatch on names such as
`roof` or `window`; it reads the compiled equations and response routes.

* Existing broad error opinions still run first. Capture starts at pass three.
* A candidate must enter its authored distance band and pass construction
  admissibility: no cyclic bindings, duplicate bound roles, reversed extents,
  collapse or unsupported fractional resolution.
* Acceptance explicitly imports a new construction. It retains the old
  constructions, public-port correspondences, input rounding remainder, and the
  actual displacement introduced by the replacement. This is a structural
  hypothesis, distinct from material parameter settlement.
* A roof base can consume the same height expression as its support's top.
  An upper tower can likewise use the supporting wall's top at its base; a
  grounded tower uses the facade's floor instead.
  Reflected parts can consume one prototype and an axis construction. These are
  executable native equations, including the rendered boundary points.
* Once held, a relationship remains exact under parameter changes. The system
  measures the *uncoupled opposing requests* that the other errors would have
  made before passage clipping. Sufficient pressure releases the construction
  on the next outer pass, with a short cooldown before recapture.

The ordinary capture band is 12 drawing units; selected facade contacts and
reflections use 24. Front-facing cross centering uses a quarter of its support
width, bounded to 12–32 units. Release uses 24 units in a separately normalized
request read after accounting for the selected receiver damping. These are
authored experimental thresholds in different diagnostic bases, not measured
physical forces. Focused tests demonstrate both retention and release.

This implements hysteresis by changing construction, rather than by hiding a
rounding operation in the renderer. It is not a learned energy well, a global
minimum, or a proof that every captured interpretation is correct.

An exact bound relationship is also distinct from shared joint identity. These
bindings establish an executable coordinate relation between constructions;
they do not fuse acquired source sites or claim that projected equal endpoints
were originally one site. Source-graph joining still requires its own explicit
local replacement and correspondence witnesses.
Reflection currently binds the parts' bounding frames. A shared opening family
can align their outline construction too; interior pane details and strongly
different families retain their own selected structures.

## Native construction and communication

The accepted affine constructions currently share one compound machine with
private position and extent carriers. Exposed coordinate requests reach those
carriers through explicit calibrated response families. For a coordinate
`q = sum(c_i p_i)`, its local response map uses `c_i / sum(c_i²)`; the existing
one-quarter receiver response and finite capacity still apply. Requests reaching
the same carrier combine before settlement. This is observation/coupling, not
branching conserved material into several copies. Each material receiver retains
accepted, blocked, deferred, quantized and rejected participation.

Inputs use a half-unit lattice so the selected half coefficients remain readable;
rounding stays in the import witness. This changes the response affordance as
well as the relationship choices, so the experiment is not a controlled test of
one attraction coefficient. Explicit native part surfaces identify each part's
four bound ports inside the compound machine. The independently restored
geometry and bound reads must match the saved review.

Neighboring grounded facade columns receive a non-overlap inequality. For a
supported three-column facade, the center column can share its two side
boundaries with its neighbors and a common floor. This addresses adjacent
towers; it is not a universal overlap ban. Roofs, crosses and openings can
legitimately overlap their containers, and foreground/behind interpretations
remain distinct.

## Run and inspect

```powershell
python -m gojo_quickdraw.church_refine --source-study D:/_quickDraw/data/church-duration-2026-10-29-v2 --selection D:/_quickDraw/data/church-heuristics-2026-10-29-v3/selection.json --baseline-study D:/_quickDraw/data/church-heuristics-2026-10-29-v3 --output D:/_quickDraw/data/my-church-connections --connections --passes 8 --steps 2 --limit 16
```

The baseline supplies comparison views only. Exact acquisitions, initial source
scaffolds and frozen provider bytes come from the selection's original studies.
The runner checks acquisition identity, game recognition, provider hashes and
read-only source studies. A fresh output retains its selection, input packets,
proposal alternatives, native construction histories and every executed pass.

Open `review.html`. The additional disclosure shows open, captured, held,
blocked and released constructions, their entry discrepancy, opposing pressure,
and opening revisions. Faint source ink, green working ink and unresolved
intervals remain separate from synthesized shape boundaries.

The review derives each polygon fill from that saved stage's own directed
boundary. A later family can have a different point order or point count and
cannot lend its fill indices to an earlier version. `review-metadata.json`
records the renderer fingerprint, calculation fingerprint and saved view hashes.
It is valid to rebuild this observer without rerunning the native calculation;
the original calculation fingerprint remains unchanged.
