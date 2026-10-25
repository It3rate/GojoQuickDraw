# Verification checkpoints

## Comparison gallery and persistent views, 25 October 2026

The 1,000-church gallery has 84 pages of 12 input/result pairs, with four pairs
on the last page. Browser checks visit every page, verify unique complete
membership, source/final profile counts, three pairs across, actual unclipped
SVG bounds, mobile layout, retained-detail persistence and final-step links.
All 1,000 pairs have a final interpretation and a link to the correct inspector
page. The gallery is 9.2 MB across its pages; it does not embed full packets.

The detailed inspector preserves its selected Read view through every paging
control, drawing selection and reload. The 20-page audit again covers all 1,000
records and 400 views. A separate nine-record, three-category audit verifies that
a requested final step survives visiting drawings that have no interpretation;
those drawings show their fitted source without inventing a final result.
There are no browser script errors, external requests or evidence changes.

The real desktop workflow builds both reviews after analysis and verifies that
the gallery has nine source drawings, three church finals and nine inspection
links. This protects a deferred folder-selection fix: the first command after
switching to the analysis collection must not use the previous collection's
selected folder. The workflow also converts/restores real samples and checks
unchanged archives, cancellation and exact analysis resume.

The checkpoint passes 68 Python, 13 native and ten desktop tests. Debug and
Release desktop builds succeed. This change is limited to presentation and UI
selection; it does not alter extraction, proposals or native Gojo structure.
The linked Gojo checkout remains unchanged; its full suite was verified at the
preceding checkpoint. Results: [gallery report](../reports/gallery-2026-10-25.json).

## Collection analysis and paging, 25 October 2026

The checkpoint processes all 1,000 previously converted churches in `church1000`
into a separate `church1000-analysis` collection: no errors, 7,000 authored proposal
states, 50,202 native opinion claims and 12,863 dependency links. All 1,000 source
records have point timing. A complete audit checks source and derived artifact
hashes and exact equality of original text, acquisition, fitted observation and
baseline program. The source collection remains unchanged.

The native worker independently restores every captured record before publishing
it. Twenty additional seeded random archives restore in separate processes. One
selected analyzed church generates and independently executes all seven native
construction rungs. Cached proposals are not mislabeled as executed native rungs
or ground-truth supervision.

All 1,000 records resume without re-encoding. A separate interruption audit kills
its own worker tree after four durable records, resumes to 20, and verifies those
four records' exact hashes and the unchanged source journals.

The offline review exposes every drawing over 20 pages of 50. Browser checks visit
every page, verify unique complete membership, inspect 40 drawings and 400 modes,
and select 34 dependent claims. They report no script errors, external requests,
evidence mutation or mobile overflow. The nine-record pilot also exercises every
record and all 90 available modes.

The real desktop workflow covers sampling, folder/order selection, paging, archive
verification, drawing steps, probes, container carrying, cancellation, full analysis
and exact resume across three categories. A separate cold-start audit opens the
1,000-record collection without pressing Load, checks first/last pages, returns to
page one and confirms the existing inspector is available. It protects the deferred
text-change cancellation fix discovered during QA.

The checkpoint passes 64 Python, 13 native and nine desktop tests, plus all 5,407
tests in the unchanged linked Gojo solution. Release builds have no warnings.
Detailed results: [analysis report](../reports/analysis-2026-10-25.json).

## Initial verification checkpoint

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
