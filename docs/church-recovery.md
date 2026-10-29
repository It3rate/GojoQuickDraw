# Reconsidering unexplained church ink

The proposed extension uses the existing native composition:

```text
G(first boundary read and its source intervals,
  unresolved interval participation,
  proposed connected tower/cap or crossing construction,
  source-to-boundary correspondences,
  admission/alternative witness)
```

The Python adapter selects candidates from retained acquisition and the first
mapping's unexplained complement. It never creates another acquisition, joins
source sites by coordinate equality, or reads an earlier final church as an
inference target. The selected geometry, attachments and mapping become actual
native constructions and exposed-port error relations through the existing
importer. Unselected source and the first proposal remain available.

This is authored second-pass inference, not a new learned model, a semantic
truth label, or an autonomous search during every settlement round. Recovery
must not count a first-pass mapping and its residual as independent evidence.

## Implemented reads

`layout.estimate` now defaults to a second read after the initial area proposal.
`estimate(observed, recover_missing=False)` exposes the original first pass for
controls. The adapter reads the existing boundary mapping's unexplained ranges
and estimates their source-curve length. Part membership is not the same as
explained ink: a span selected broadly by the building can still have an
unexplained complement that motivates recovery.

For a pointed enclosure, the adapter reads one near-closed acquisition path or
two nearby acquisition strokes whose ends approximately meet. It finds a
supported upper tip, two continuing sides and an implied shared eave. A long
roof-and-wall contour can be cut at that level even when the two original
sloping runs end at different heights. The two lower paths supply coarse frame
positions; a rectangle and triangular cap are then proposed. The selected source
intervals, stroke order and pen-up gaps remain recorded. The cap base and
unobserved closure are assumptions, not new fitted/acquired lines.

Recovery requires a relatively narrow upper structure extending into the body,
substantial unexplained length and a compatible footprint. An already explained
cap, a broad whole-building outline, incompatible routes or a tiny residual do
not request another tower. The bounded implementation considers at most 96
strokes, one- or two-stroke routes and four new pointed regions. General open
silhouettes, long routes across many strokes and domed recovery remain future
work; the earlier dome detector is unchanged.

After adding a region the mapping is read again. If there is no source-supported
full cross, the second read can examine remaining straight or gently curved
arms anywhere on the page. Sixteen subdivisions of each fitted profile supply
local crossing, direction and interior-arm reads. Those fractions belong to
the fitted profile, not acquisition time. A page-height prior does not veto this
search. Substantial arms, roughly transverse directions and interior crossing
are still required. Tiny overruns, strongly folded curves, accepted door/window
dividers and enclosed pane crossings are excluded. Existing full source crosses
suppress this fallback; outlined crosses count as existing source support too.
Up to six second-pass crossings can be retained.

A recovered cross uses actual source profiles and is attached to the smallest
compatible proposed container. The earlier prior-only cross is withdrawn from
the active proposal, while its first-pass construction remains in the retained
read. A recovered cross can be on the tower face, below its roof; it is not
automatically moved to the peak. Both recovery stages remain authored finite
geometric hypotheses rather than calibrated category likelihoods.

## Storage and native execution

The record's `layout.recovery` stores the earlier parts/relations, first-read
unexplained intervals, selected candidates and before/after residual-length
diagnostics. New geometry, source-profile references and attachments use the
same ordinary layout/context structures as other parts. Studio's native import
and saved stage views therefore consume recovered parts without a new semantic
node category. Existing completed dataset artifacts keep their processing
version; refreshing the viewer alone does not reprocess them.

The connected church study additionally imports a native recovery witness. It
retains a readable first shape world, actual native profile/parameter intervals,
the current proposed target constructions and an explicit unresolved completion
slot. That witness supplies provenance, not another force or independent vote.
The ordinary mapping, errors, capture/hold/release and parameter settlement then
operate on the admitted constructions. The separate native auditor checks both
the recovered targets and those restored source-interval references.

Cached study inputs can explicitly request the new pass:

```powershell
python -m gojo_quickdraw.church_refine --source-study D:/_quickDraw/data/church-duration-2026-10-29-v2 --selection D:/_quickDraw/data/church-heuristics-2026-10-29-v3/selection.json --baseline-study D:/_quickDraw/data/church-cad-2026-10-29-v1 --output D:/_quickDraw/data/my-church-recovery --connections --cad --recover-missing --passes 8 --steps 2 --limit 16
```

The earlier result is comparison material only. Raw acquisition and the earlier
source scaffold still determine the new proposals. The review compares the
earlier and new results, preserves original ink and exposes recovery decisions
under **Construction capture and proposal revisions**. The native worlds and
all executed passes remain independently inspectable.

## Example 4 and training qualification

In drawing `6226189452574720`, the initial roof detector followed the left
contour almost to the floor and rejected the unequal roof-arm endpoints. The
lower cross's bar also exceeded the raw straightness allowance and the old
upper-page restriction. Recovery admits the early pointed enclosure as a tower
plus cap and the later two arms as a source-supported cross on its face. It
does not classify that large contour as a door. The initial shallow roof is
still retained; recovering an omitted part does not prove that all earlier
parts are semantically correct or that occlusion is solved.

These are useful weak interpretation pairs for later learning. They are not
human-approved labels or evidence that all now-explained ink has the right
meaning. Inferred bases, closure, foreground choice and canonical shape remain
distinct from the original strokes. No model trains in this extension.
