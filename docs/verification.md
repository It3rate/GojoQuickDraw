# Verification checkpoints

## Framed windows with crossing bars, 27 October 2026

Layout v7 proposes a window from a rectangular frame plus crossing interior runs,
including partly open or uneven frames. Frame and divider source intervals remain
separate. Accepted pane bars no longer compete as church-cross markers. Unplaced
upper frame candidates retain the competing cross and an explicit unresolved read.
Cleanup v5 retains each window and centers its bars. Nine shared sites and twelve
segments encode each window's perimeter, four T contacts and central crossing.

The reported drawing `5619141325094912` now has two source-supported paned windows
and its separate roof cross. Three real drawings were processed into
`D:/_quickDraw/data/church-paned-windows`, with zero errors, 21 native construction
rungs and all ten inspectable stages per drawing. Independent native restore and
boundary coverage checks pass; original source JSON, observation and baseline file
hashes are unchanged. The Release viewer opens the saved folder without Python or
Gojo tools, and its enlarged result was visually inspected.

161 Python, 48 native and 5,407 linked Gojo tests pass. Cases cover one through four windows, an
uneven gapped frame, bare crosses, a circular frame, external crossing strokes,
ambiguous upper frames, and divider-specific source mappings. The native test
removes adapter labels and still reads shared center/frame sites, source profiles,
container participation and separate instance identity. A read-only comparison on
the earlier 100 drawings preserves role/shape/status counts and adds divider
geometry to one previously supported window. These are proposal checks, not a
labeled accuracy evaluation. Fragmented divider runs and windows outside the
accepted facade can remain unresolved. QA is under
`D:/_quickDraw/_work/qa/paned-windows-2026-10-27`; see
[the checkpoint report](../reports/paned-windows-2026-10-27.json).

## Source-supported openings and viewer keys, 27 October 2026

Layout v6 joins consecutive upright evidence when estimating the wall, compares
rectangular, arched and round opening contours, and retains larger source-supported
openings. Cleanup v4 removes the automatic two-window fallback and only suggests a
rectangular entrance with supported front-facade context and no unresolved lower
interior ink. Added crosses, doors and windows are gray and dotted. A shared-corner
fix preserves existing frame/ground contacts through wall/tower partitions.

The 100 existing records in `D:/_quickDraw/data/church3000` were processed into
`D:/_quickDraw/data/church3000-openings`: 100 completed, zero errors, all ten stages
per drawing. All original source JSON and observed evidence match their baselines.
Native capture/restore and source partition checks pass for every result; four
selected archives also pass separate restore commands, including the reported
large door, a gapped circular window and the two shared-corner regression cases.
Counts are 29 source-supported door proposals, 13 source-supported window proposals
(one circular), 13 optional rectangular doors and no automatic window additions.
These are development proposals, not labeled accuracy measurements.

The desktop gallery and enlarged view accept Left/Right for drawings,
Shift+Left/Right for pages and Up/Down for stages. Integration checks cross page
boundaries, preserve the stage, clamp the short final page, update the open enlarged
view, and preserve text editing. A Release startup audit opens all 100 saved records
without Python/Gojo tools, checks nine pages and renders the three visual examples.
Screenshots were inspected. The bounded nine-record conversion/resume and two-record
analysis workflow also passes, including cancellation and optional HTML export.

155 Python, 28 desktop, 47 native and 5,407 linked Gojo tests pass. The native round
opening test removes labels and retains the closed perimeter, source selection,
container reference and distinct identity of an unsupported alternative. Release
builds have zero warnings/errors. QA files are grouped under
`D:/_quickDraw/_work/qa/openings-2026-10-27`. See
[the report](../reports/openings-2026-10-27.json). Difficult mixed strokes, text,
incorrect wall estimates and ambiguous viewpoints remain recognition limitations.

## Full pipeline and retained stages, 26 October 2026

The desktop's Convert action now runs the finite `process` workflow, retaining
source acquisition, fitted profiles, seven native construction rungs, area
proposals and final church refinement. The ten church views are selectable in
the folder viewer. Independent native restore checks the correspondence between
the record's original acquisition and the construction's acquired events, without
merging equal coordinates into shared identities. Per-drawing stage bundles are
included in artifact hash checks. Earlier baseline/native archives still open.

A nine-drawing Release audit across angel, church and frog retains all ten views
for the three churches and original/fitted views for the other six. A repeated
request reuses all nine unchanged archives. Two already converted churches also
complete and resume independently. All nine result archives pass a separate
restore/verification. The UI audit renders every stage, enlarges a drawing,
preserves stage choice while paging, stops and restarts a worker, and explicitly
exports a portable review. Both tabs' final controls are fully reachable at
920×760; scroll padding is part of the measured content, and status is above the
workspace. A separate read-only Release startup audit pages all 100 records
currently present in the user's selected folder with Python/Gojo unavailable.

146 Python, 18 desktop, 46 native and 5,407 linked Gojo tests pass. Release builds
have no warnings/errors. All QA output is under `D:/_quickDraw/_work/qa/full-pipeline`.
This checkpoint changes orchestration, persistence and inspection, not recognition
accuracy or learned priors. See [the report](../reports/studio-full-pipeline-2026-10-26.json).

## Boundary correspondence and optional construction, 25 October 2026

`church-mapping-pilot-v1` recomputes the same 24 development drawings with layout
policy v3 and explicit boundary mapping. It contains 372 proposed boundary reads,
348 source-range associations, 78 shared source ranges and 175 unresolved ranges.
These are finite correspondence counts, not independent drawings or accuracy
scores. Every profile has a complete source partition; missing target portions
stay explicit. No ground/cross-reference level, prior-only cross, missing crossbar
or assumed cross-outline thickness claims a physical source line.

Samples 1 and 6 gain independent upper towers; 6 shares part of the wall's top
with the tower's bottom. Sample 7 permits a cross in a building without a roof or
tower. Sample 17 separates the tall tower from its small cap. Samples 2 and 15
use arched doors. Sample 1's lettering is unresolved instead of a window. These
are authored development checks; side views and sparse/A-frame shapes remain
weak, and the conservative window gate can miss multi-stroke/gapped openings.

All 24 records capture and independently restore the new native field. Six
archives pass a separate restore command; all 24 resume without re-encoding.
Exact source JSON, source objects, observed intervals, baseline programs and
baseline journals are unchanged. A separate three-record raw QuickDraw conversion
also restores. Recomputing the mapping matches each saved adapter exactly.

118 Python, 38 native and 11 desktop tests pass. The unchanged linked Gojo suite
passes 5,407 tests. Native checks strip descriptive labels, preserve partial shared
support and ordered target reversal, distinguish endpoint-only inference, and
reject missing coverage and equal-but-copied or foreign references. Browser
checks inspect all 24 mapping/area pairs, boundary and source selection, unresolved
filters, both gallery pages, four inspector pages, mobile layout and retained view
choice. See [the report](../reports/mapping-2026-10-25.json) and
[the mapping contract](../CHURCH_BOUNDARY_MAPPING.md).

## Multiple towers, roofs and markers, 25 October 2026

Policy v2 is saved in `church-towers-pilot-v2`, using the same 24 development
drawings. It proposes eight tower rectangles, 26 roofs (including two domes),
27 cross markers and their levels. Counts describe hypotheses, not confirmed
objects. Sample 13 keeps a narrow offset roof; 14 gains a tall tower; 15 keeps
two towers and a central cross; 16 uses an inferred lower tower edge; 22 has two
domes and two markers. Its right marker explicitly assumes a missing crossbar.

All 24 source JSON strings, source objects, observations and baseline programs
match the original collection; its journals also remain unchanged. Native
captures independently restore, six archives pass a separate verification, and
all 24 resume without re-encoding. Three real raw QuickDraw records also convert
and restore. A new native test removes adapter labels and verifies two distinct
32-partition caps retain their tower and ground contact identities.

110 Python, 25 native and 11 desktop tests pass. The unchanged linked Gojo
solution passes 5,407 tests. Browser checks cover all 24 original/area pairs,
the five requested examples, explicit assumption text, both gallery pages,
four inspector pages, mobile layout and preserved view selection. There are no
script errors or external requests. The seven older synthesis states remain
available; this change adds area hypotheses, not a new learned synthesis pass.

The upper contour preference also fixes a lower eave winning over sample 18's
roof, and disconnected writing no longer creates an extra attached roof in 11.
Cross/grid ambiguity, flat-topped or overlapping towers, side views, writing
and sparse lower ink remain limitations. See [the v2 report](../reports/towers-2026-10-25.json)
for IDs, fingerprints and exact checks. This is development feedback, not unseen
semantic evaluation.

## Ideal church area estimates, 25 October 2026

The new area-placement experiment was reviewed on the same 24 development
churches, saved separately in `church-layout-pilot-v3`. It proposes one main
rectangle, one attached triangle, ground/cross levels, an ideal Christian cross
and optional opening rectangles. This run contains ten proposed doors and two
proposed windows; these are proposal counts, not confirmed detections. There
are 21 prior-only part/level records. Every acquired point, time, source interval
and baseline program remains unchanged.

All 24 native captures independently restore. Six archives pass a second
verification command, and all 24 resume without re-encoding. Native tests verify
actual shared proposed sites after labels are removed, distinguish equal but
unshared coordinates, and reject forward, unselected, absent or disagreeing
references. Ground/wall/door contact and roof/wall attachment are construction
relationships; they do not merge acquired samples or assert observed contact.

Browser checks cover all 24 area overlays, unchanged point lists, pale fills,
matching comparison frames, gallery/detail links, mobile layout and persistent
view selection. The existing synthesis gallery remains available and all four
inspector pages pass their annotation/proposal checks (88 view checks). No script
errors or external requests were observed. Automatic review refresh also passes
the real desktop workflow with nine converted sources and three church analyses,
including its intentional cancellation and resume.

This checkpoint passes 102 Python, 24 native and 11 desktop tests, plus 5,407 tests
in the unchanged linked Gojo solution. Release builds have no warnings or errors.
The simple frontal example improves visually; side views, several towers,
outlined crosses, sparse lower ink and writing remain explicit counterexamples.
See [the placement rules](../CHURCH_LAYOUT_ESTIMATES.md) and
[the verification report](../reports/layout-2026-10-25.json).

## Church object candidates and automatic refresh, 25 October 2026

A bounded development pilot analyzes 24 selected churches into
`church-objects-pilot-v2`. All 24 capture and independently restore native evidence,
with 1,831 opinion claims and 1,223 actual context-part selections. Six additional
archives restore through separate verification processes. Original JSON, source
acquisition, fitted intervals and baseline programs match their source records
exactly; source journals and artifact hashes remain intact. All 24 selected keys
resume without re-encoding. This is an engineering review set, not an accuracy
benchmark or an unseen holdout.

Source-backed candidates preserve distinct wall/tower scopes, shared wall/building
focuses, multiple copies, nested/attached relationships, ambiguity and unknown
support. Native tests verify alias preservation with labels removed and reject
missing, forward, duplicate and foreign selections. Geometry tests protect open
U shapes against closed-triangle/outlined-cross false rectangle reads and keep
each roof-parent comparison local. The existing seven synthesis states are
unchanged; object-conditioned synthesis remains the next experiment.

The desktop refreshes both stable review entries after processing. Tests cover
new records appearing after refresh, preservation of saved archives, explicit
reload semantics and detection of collection changes during rendering. A real
nine-record desktop workflow covers conversion/restore, analysis, automatic
refresh, cancellation and resume. Debug tests and Release build succeed.

The checkpoint passes 96 Python, 18 native and 11 desktop tests, and all 5,407
tests in the unchanged linked Gojo solution. The inspector's detail pane now has
a stable height so hovering long object/claim records cannot move the evidence
list beneath the pointer. The 24-record review has four inspector pages and two
comparison pages; colors follow source correspondence rather than rewriting
geometry. Full results: [object report](../reports/objects-2026-10-25.json).

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

## Church shape cleanup — 26 October 2026

The 36-drawing development review includes the four supplied examples and the
earlier 24-drawing pilot. All 36 completed analysis and native capture/restore,
with no retained errors. The audit compares source text, source records,
observations and original programs with their baselines; source archive hashes
remain unchanged. A deterministic rerun reproduces the layout, mapping and
cleanup. All proposed openings remain within their selected wall/tower bounds.

The results contain 260 shape parts, 270 explicit attachment references, 32
mirror reads and 110 prior-only parts (including ground/markers when inferred).
Native tests erase descriptive JSON and still read before/after site links and
shared roof/cross contacts. False contact by equal coordinates, missing
correspondence and direct ink claimed by added shapes are rejected. A finite
rounding case verifies that an attachment uses its selected parent's actual site.

Checks passed: 124 Python tests, 43 native tests, 11 desktop tests and all 5,407
linked Gojo tests. Three fresh raw drawings converted and restored. Six generated
archives were independently rechecked, and a second analysis reused all 36
verified results. Browser checks cover every drawing, all three gallery pages,
view persistence, original-point preservation, mapping/unresolved inspection,
mobile layout and earlier final-stage gallery behavior. No script errors or
external requests occurred.

See [the recorded report](../reports/cleanup-2026-10-26.json) and
[the scope/limitations](../CHURCH_SHAPE_CLEANUP.md). These integrity checks do not
establish semantic recognition accuracy or learned priors.

## Cross prominence and wall partitions — 26 October 2026

The same 36 development drawings were reprocessed with area policy v5 and
cleanup policy v2. All 36 completed native capture/restore without errors;
source text, fitted observations, programs and baseline archive hashes stayed
unchanged. A deterministic rerun reproduces the proposals and mappings.

Cross heights in flagged samples 3, 5, 10, 15 and 21 changed approximately from
51→157, 22→77, 54→209, 28→111 and 64→186 normalized page units. Samples 3 and 10
now use their uneven outline evidence rather than prior-only default markers.
Sample 15 retains its face-relative placement. Tests also verify an increasing
size response and that a narrow tower cannot force a large cross to be tiny.

Across the pilot, wall interiors are disjoint from tower interiors, wall cells
do not overlap one another, partitioned frame area is retained, and windows do
not cross wall/tower seams. Samples 24 and 27 retain only the exposed wall area
beside the towers. A central-tower control splits one wall into two rectangles.
Native save/restore with descriptive JSON removed retains their common earlier
source, frame selection, separate edges and actual shared tower-boundary sites.
Duplicate attachment claims are explicitly tested.

Checks passed: 130 Python tests, 44 native tests and all 5,407 linked Gojo tests.
Three fresh raw drawings converted and independently restored; six additional
analysis archives were rechecked, and all 36 analyses resumed unchanged. Browser
checks visited every drawing and all three gallery pages, preserving original
points, mode selection and pagination; mapping inspection passed for all 36.
No browser script errors or external requests occurred.

See [the recorded report](../reports/cross-scale-partitions-2026-10-26.json).
These are bounded development checks; missing wings and incorrect earlier
part interpretations remain possible.

## Peak attachments, opening clearance and foreground — 26 October 2026

Cleanup policy v3 was checked on the same 36 development drawings. Samples 6
and 17 now follow the relocated triangle peak. In sample 34 the selected source
upright reaches the dome even though the earlier cross envelope stops short;
both revised crosses now share their respective cap peak sites. A paired-cross
mirror read detects the previous mismatch and falls within its authored
allowance after this correction. Cross heights stay unchanged across the pilot.

Samples 13–15 now satisfy the authored door/window clearance. Sample 21's
crossbar moves above halfway without reducing its scale. Sample 11 keeps the
continuous lower wall whole with the upper tower ending at its top. Sample 7's
long source upright still supports a tower reaching the ground. Drawing-supported
openings retain their colors; optional doors/windows are light gray and dotted.

All 36 complete native capture/restore with no retained errors. Source text,
fitted observations, programs, earlier area estimates and boundary mappings are
unchanged. Deterministic reruns reproduce the new cleanup. All occupied wall/tower
rectangles are disjoint and all openings remain contained. The results contain
265 proposed parts, 298 shared attachment references, 31 mirror reads and 104
prior-only parts. None of the finite clearance/mirror checks require review in
this pilot; that does not establish semantic recognition accuracy.

Checks passed: 141 Python tests, 44 native tests and all 5,407 linked Gojo tests.
Negative controls cover a genuinely offset cross, unrelated endpoints, a
grounded side tower, impossible opening placement, preserved observed openings
with warnings, and an asymmetric pair of crosses. Three fresh raw drawings
converted and restored; six derived archives were independently restored; all
36 analysis records resumed unchanged. Browser checks cover all drawings, both
views' opening styles, check listings, pagination and mapping inspection.

See [the recorded report](../reports/cleanup-details-2026-10-26.json).

## Joint comparison and compact desktop — 27 October 2026

Source joint evidence remains intact. The new final observer reads displayed
native edges, shared sites and existing correspondences; a label-removal test
retains source mappings and native shared junctions. Negative controls keep
independent crossing sites distinct and an earlier inferred area inferred after
refinement. Direction tests cover L/I/T/Y/X/K. A constructed paned frame retains
four T contacts and its X center, with closed walks recorded separately.

Three real churches completed all seven native rungs and independent restore,
with unchanged acquired source and fitted observations. Recalculated comparison
reads exactly equal the saved cache. The two-window example retains both windows
and all eight native T contacts. Its 13 same-type and four changed-type matches
are comparisons of authored reads, not accuracy claims; ten ambiguous and twelve
unmapped final reads remain visible.

Passed: 161 Python, 50 native, 32 desktop and 5,407 linked Gojo tests. The bounded
desktop workflow converted nine real drawings across three categories, resumed
without changing archives, analyzed two churches, exercised keyboard navigation,
Stop/recovery and optional HTML export. Release startup audits opened the new
three-drawing set and an older 100-drawing folder without working Python/Gojo
paths, preserving paging and stage selection. Dataset/filter drawers start
collapsed, the gallery starts within 250 pixels of the top, and bottom controls
remain reachable at 920×760. Screenshots of both joint views were inspected.

See [the recorded report](../reports/joint-comparison-2026-10-27.json).

## Extend an existing dataset — 27 October 2026

Pipeline counts now describe a target across selected categories. Increasing
the count adds missing source identities, repeating it resumes saved work, and
lowering it leaves every existing drawing intact. Earlier processing versions
remain recorded; additions do not overwrite completed evidence or native stages.

A copy of an older nine-drawing dataset grew to twelve by adding exactly three.
Repeating twelve added nothing; lowering the target deleted nothing. A later
angel-only target added two more drawings without changing other categories.
All 45 original compressed artifacts remained byte-identical, and all fourteen
resulting native records passed independent restore and artifact-hash checks.

Passed: 167 Python, 50 native, 32 desktop and 5,407 linked Gojo tests. Controls
cover duplicate sources, exhaustion, category changes, interrupted additions,
changed processing versions and retrying retained failures after growth. The
desktop workflow also increased its count from nine to twelve through the
conversion button, added three, verified unchanged original archives, and
confirmed that a lower target preserves all twelve. A read-only startup audit
opened the user's existing 100-drawing folder and checked the updated controls.

See [the recorded report](../reports/dataset-growth-2026-10-27.json).

## Local facade symmetry and joint retrieval — 27 October 2026

Layout v8 reads tilted frames in a bounded local basis and admits windows near
an uncertain wall top. Cleanup v6 pairs locally supported openings, keeps
upper-tower footprints inside their supports, and follows tower/roof parents
when centering face symbols. A short roof-tip overrun sharing the stem of a
stronger cross is withheld as a second symbol while its raw joint remains.

All seven user examples were converted through the ten saved views and 49
native construction rungs. Acquired source text and fitted observations stayed
unchanged; all seven completed independent native restore and artifact hash
checks. Both source windows are retained in examples 1–4 and 7. Example 5 has
paired caps/supports, and example 6 has one cross. The enlarged drawing and
joint-comparison views were opened and inspected for all seven. The final
saved shape/joint caches equal those visually checked, including after the
round-window margin regression fix.

A read-only sweep over the 300 existing drawings found no invalid source
mutation, forward part reference, attachment coordinate mismatch, unsupported
upper-tower footprint, or missing horizontal window margin. It proposed 45
windows versus 28 in the saved earlier outputs; these counts do not measure
recognition accuracy. Three remaining clearance/mirror review warnings stay
visible. Tests preserve round windows under margin reduction and retain
genuinely different shape families and grounded side towers.

Passed: 178 Python tests, 50 native tests and all 5,407 linked Gojo tests.
See [the verification report](../reports/facade-symmetry-2026-10-27.json).

A separate untrained retrieval read used 200 saved earlier joint comparisons;
100 legacy records lacked that cache. Joint type, adjacency and relative order
descriptors ranked the own final first in 1.0% of cases versus 0.5% chance;
top-ten recall was 9.5% versus 5.0% chance. It used no point coordinates,
source correspondence or cross-drawing identity features. This is not reliable
matching, nor a trained-model or semantic-accuracy claim. The
[pilot note](joint-training-pilot.md) gives the full limitations and a local
correspondence-training proposal; [the retrieval report](../reports/joint-retrieval-2026-10-27.json)
retains the cohort, scores and per-drawing rankings.

## Reproduce the checks

Run the commands in the README using the linked revision in `gojo.lock.json`. The
selection report makes this pilot identifiable; use your own QuickDraw files or the
bundled synthetic controls as input. Keep generated datasets outside the checkout.
To reinterpret existing drawings after code or dependency changes, use a new
dataset destination and compare through source correspondence instead of
overwriting an earlier interpretation. A pipeline dataset can still accept new
drawings while preserving earlier artifacts and their processing versions.
