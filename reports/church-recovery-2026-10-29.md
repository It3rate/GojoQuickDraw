# Recovering omitted church parts from unexplained ink

The completed [sixteen-drawing comparison](D:/_quickDraw/data/church-recovery-2026-10-29-v1/review.html)
uses the same recognized acquisitions and frozen local providers as the CAD
study, with eight outer passes and two inner rounds. No new model trains.
The earlier CAD result supplies comparison views only; it does not supply
target positions or new evidence to inference.

The new second proposal pass recovers a front steeple and lower cross in sample
4, and replaces a small assumed cross with the drawn crossing in sample 9.
The other fourteen drawings retain exactly the same saved stages. See the
[implementation and limitations](../docs/church-recovery.md) and the
[machine-readable results](church-recovery-2026-10-29.json).

The source-length diagnostic falls from 48.1% unexplained in the previous final
to 19.5% in sample 4, and from 24.0% to 8.9% in sample 9. These are mapping
coverage reads, not semantic accuracy. Sample 9 starts at 2.4% after recovery
and rises during settlement, illustrating that canonical/contextual adjustment
can trade source fit for another preferred interpretation.

## Why sample 4 was missed

Drawing `6226189452574720` begins with the central pointed outline, followed
by the surrounding building and then the two crossing arms. The first cap
detector treated the left contour as one slope continuing almost to the floor.
Its end was much lower than the right roof slope's end, so the cap was rejected.
The flat-tower fallback did not recover that pointed silhouette. It was not
selected and subsequently rejected as a door.

The curved crossbar had maximum chord error 9.47 against the raw detector's
8-unit allowance. The crossing also lay below the old upper-page restriction.
The fallback required straight arms, so it could not repair that omission.
The earlier small top cross was a separate prior-only addition.

## Second proposal read

The initial boundary mapping supplies unexplained fitted-profile ranges and
their approximate source-curve lengths. A broadly selected building span is
not automatically completely explained. Those residual ranges can motivate
another local construction read without adding another independent observation.

One near-closed stroke or two nearby acquisition strokes can supply a pointed
enclosure with continuing walls. The adapter infers a common eave, samples the
two lower paths for a coarse tower frame and proposes a rectangle plus cap.
Unequal original roof-arm endpoints no longer automatically eliminate the
whole structure. Stroke order, selected source intervals and pen-up gaps remain
inspectable; the inferred eave/base and closure are not claimed as acquired ink.

The mapping is reread after region recovery. If no full source-supported cross
exists, substantial residual arms can suggest a crossing anywhere on the page.
Straight and gently curved profiles are considered; tiny overruns, folded
curves and accepted window/door dividers are excluded. A recovered cross can
attach to a tower face below its cap. The active prior-only cross is withdrawn,
with the first proposal retained for comparison.

## Verified examples

| Sample | Result |
| --- | --- |
| 4 | A grounded tower, its pointed cap and the source-supported lower cross are present. The tower/cap contact and ground contact are held; the cross remains on the tower face. Wall cells are derived around the tower. |
| 9 | The substantial drawn crossing replaces the earlier prior-only top marker. Its source profiles remain connected to the reconstructed cross. |
| Other fourteen | Saved native stage projections remain identical to the preceding CAD study. |

The original shallow roof remains in sample 4. Recovery does not yet decide
that all earlier parts are correct, remove every contradictory roof line, or
solve foreground/occlusion. These are improved weak interpretation pairs, not
human-approved training labels or a semantic accuracy result.

## Integration and provenance

New ordinary Studio processing uses the recovery read automatically in the area
proposal stage. Existing saved records retain their earlier processing version.
The same cached study can opt in with `--recover-missing`, alongside
`--connections --cad` in this comparison.

The record retains `layout.recovery`, including first-pass parts/relations,
unexplained source intervals and selected decisions. The experimental native
study additionally retains a first shape world, actual native source-profile
parameter intervals, the live proposed targets and an unresolved completion
slot. This witness contributes provenance rather than another motion request.
The ordinary boundary mapping and port-error settlement still control motion.

The standalone native test proves source identity, interval basis/order,
separate first/current geometry identity, readable restoration, unchanged
source bytes and no position change from attaching the recovery witness.

## Scope and verification

A separate bounded proposal-only scan of 32 additional recognized cached
acquisitions made no additions. It supplies a conservative regression check,
not a measure of recall or precision. The current new tower read targets
near-closed, relatively narrow pointed routes extending into the body. More
fragmented/open outlines, routes across many strokes, upper-only missed towers
and additional domed interpretations remain work for later passes.

The sixteen-case replay completes with zero production errors. Source packets,
comparison studies and frozen provider bytes remain unchanged. The independent
native audit checks restored geometry, bound reads, receiver accounting and the
new recovery witnesses. Browser verification covers all sixteen drawings,
96 panels, all saved passes, recovery disclosures, overlays, keyboard navigation
and mobile layout, without script errors or external requests. Samples 4 and 9
were also inspected visually.

The audit matches 6,704 executable shape points, 21,569 error reads and 15,412
actuator outcomes with maximum numeric difference zero. It also verifies 24
restored recovery witnesses and 72 source-interval references across the saved
passes. Signed receiver accounting balances. The complete run took 586.15
seconds including archive production/restore and review generation; compressed
worlds total 1,273,349,163 bytes. Full histories remain a research cost, not a
production throughput measurement.

A one-drawing Studio pipeline pilot processes the same raw sample 4 through all
ten saved stages; its artifacts and native restore verify. Its ready-to-inspect
results are in `D:/_quickDraw/data/church-recovery-2026-10-29-v1/studio-pilot/results`.
All temporary builds, logs and browser captures stay under
`D:/_quickDraw/_work/church-recovery-2026-10-29-v1`.

Verification passes: 254 Python tests, 148 QuickDraw solution tests and 5,548
linked Gojo tests across thirteen projects. The focused recovery tests pass
again after the final test assertions. Gojo remains clean at
`bcecf48d28739bb1877c4bd1ce2f1b31f8eda4b4`, matching the dependency lock.
