# Initial verification checkpoint

This is an engineering pilot, not an accuracy benchmark or a trained model. Five
previously curated churches and the first twenty distinct fresh church records were
used to test acquisition, archive integrity, resume behavior, and inspection.

## Converter and native archive

All 25 drawings converted without retained errors and restored independently through
the native reader. Repeating the same conversion created no new records and preserved
all 50 native/adapter artifact hashes.

| Read | Pilot total |
| --- | ---: |
| Original strokes | 179 |
| Original samples | 5,383 |
| Fitted profiles | 398 |
| Joint reads | 697 |
| Proposed parts | 124 |
| Compressed native archives | 17,505,781 bytes |
| Compressed adapter caches | 562,117 bytes |

Joint reads include candidate observations; they are not 697 confirmed shared sites.
Parts are seeded candidates, not recognized ground truth.

On this Windows/Python 3.10/.NET 10 machine, the pilot conversion took 37.58 seconds,
independent verification 27.41 seconds, and resume 4.43 seconds. These are single-run
wall times, including command overhead, not a throughput guarantee. Native archive
sizes ranged from 375,292 to 1,287,537 bytes and averaged 700,231 bytes per drawing.

The committed [pilot report](../reports/pilot-2026-10-24.json) records source IDs,
digests, selection basis, extraction and dependency fingerprints, counts, and timings.
It contains no raw acquired strokes. The full local pilot lives at
`D:/_quickDraw/gojo-quickdraw-evidence/`.

## Focused tests

45 Python tests and 9 native tests pass. The linked Gojo solution also passes all
5,407 tests at the pinned revision; its working tree remains unchanged. The focused
converter tests cover source preservation, stroke/time
order, empty/degenerate input, invalid and duplicate JSON, capacity errors, continued
processing after failures, gzip/folder/category handling, resume identity, changed
artifacts, interrupted journal recovery, writer exclusion, and output isolation.

Native tests also check actual source aliases versus equal coordinates, provisional
sites versus acquired sites, invalid references, exact source mismatch, false bound
reads, independent restore, worker recovery, and archive path rejection.

Context and perturbation tests retain alternatives, distinguish closure from
containment, preserve an initial asymmetry, keep a centered translation tied with a
mirror when warranted, and expose unavailable container responses. Stage diagnostics
use actual Bezier extrema, so an outside control point alone does not produce a false
out-of-frame warning.

Additional real native smoke checks cover empty timed strokes, one point, coincident
samples, decimal coordinates, and irregular timing. A separate candidate-only smoke
run covered 60 churches and 9 non-church records with finite geometry and valid support
references; that is not an archive or semantic-accuracy claim.

## On-demand stages and visual review

The five familiar churches regenerate all 35 stages through the native machinery.
Three fresh drawings regenerate another 21 stages. Portable browser checks inspect
the original/fitted views, annotation support, exact-source downloads, drawing-order
control, native stage geometry, and absence of external requests or script errors.

The final baseline browser run passed all 25 drawings, 193 evidence-group selections,
and 25 byte-exact source downloads. The final familiar-source stage review passed
568 exact native paths, 113 operations (including 14 rejected proposals), and 35
downloads. Inspection left captured evidence unchanged; desktop/mobile review had
no horizontal overflow. Dense junction labels can overlap, but the evidence list
provides an alternative way to select them.

Fresh drawings expose limitations in the existing late synthesis priors. One rounds
the body into an arch, and another produces a flower-like rounded silhouette. Out-of-frame
native geometry is reported for fresh source `4890301275570176` and familiar source
`5437095780810752`. This is deliberately not repaired by changing the baseline or
hiding the resulting geometry. A within-frame result can still be conceptually wrong.

The inspector and diagnostics distinguish these review needs from execution failures.
Source reconstruction and artifact integrity are currently much better established
than semantic part recognition or the correctness of synthesized churches.

## Reproduce

Run the commands in the README using the linked revision in `gojo.lock.json`. The
selection report makes this pilot identifiable; use your own QuickDraw files or the
bundled synthetic controls as input. Keep generated datasets outside the checkout.
After code or dependency changes, use a new dataset destination and compare through
source correspondence instead of overwriting an earlier interpretation.
