# Six to sixty-four passes on ten churches

The models were not exhausted after six passes. Nine of the ten bidirectional
runs continued changing through pass 64; one stopped quietly again at pass 6.
Extra time improved several authored compatibility measures, but it did not
introduce missing part types, object counts or relationships.

## Controlled comparison

Ten diagnostic acquisitions were taken from the 1,024-drawing study: the four on
its final review page, a known roof/wall diagonal failure, a complex drawing with
missed openings, a previously quiet control, two two-window cases and an earlier
strong mapping-strain improvement. This is an inspection sample, not a random
accuracy benchmark.

The exact cached source packets, fitted curves, part proposals, source/part
associations and provider bytes were copied to a new study. The inner-step count
remained one and the candidate budget remained 48. All response weights,
tolerances, clipping, capacities and resolution stayed the same. Only the outer
pass budget increased from six to 64. No model was trained.

All forty earlier version trajectories matched exactly, including source graph,
joint reads, parts, error reads, rewrites and transfer summaries. The original
study and provider/input snapshots were checked unchanged. A larger budget does
not require an otherwise quiet run to repeat identical observations to pass 64.

## Results

| Version | Quiet runs | Reached 64 | Actual pass range | Rewrites after pass 6 | Actuator moves after pass 6 |
| --- | ---: | ---: | --- | ---: | ---: |
| Local models | 10 | 0 | 4–43 | 137 | 0 |
| Shapes around fixed ink | 10 | 0 | 8–50 | 0 | 483 |
| Fixed scaffold | 2 | 8 | 10–64 | 820 | 1,865 |
| Bidirectional | 1 | 9 | 6–64 | 860 | 2,793 |

Bidirectional joint incidence changed after pass 6 in six drawings. Construction
bounds changed in nine, by as much as fourteen fitted drawing units. Across the
ten drawings, the mean per-drawing source displacement after pass 6 was 1.755
units at fixed original profile parameters. Twenty-four of 2,414 comparison
samples were missing at one or both stages and are reported separately; removed
participation is not assigned zero drift. These are drawing-frame coordinates,
not physical distances or accuracy measurements.

Mean bidirectional mapping strain decreased from 1.7509 at pass 6 to 1.4504 at
the final executed pass. Eight drawings had lower final mapping strain, one had
higher strain and the quiet control was unchanged. Mean attachment strain also
decreased in this selected group, from 0.9564 to 0.8177. These outcomes do not
reverse the earlier whole-cohort tradeoff or establish semantic correctness.
Active graph changes can change the measured term set.

The path was not monotonic. For source 4950483162628096, mapping strain was about
1.713 at pass 6, 1.554 at pass 32 and 1.605 at pass 64. Extra updates can improve
one expectation while worsening another. No repeated visible tail period of
2–8 passes was detected; that does not establish convergence or rule out slower
or internal cycles.

## Why a quiet result still has error

Source 4981869022019584 stopped at pass 6 even with the larger budget. It retained
mapping strain of about 0.3339 and a nonzero aggregate quantum remainder. The
current local claims and response gains could not produce another accepted
whole-tick move. Its geometry and proposals were unchanged, so repeating the
same deterministic reads would supply no new evidence.

The numerical actors use quarter-unit ticks. Fractional response remainders are
retained but are not accumulated as persistent fractional actuator state between
the refreshed outer worlds. Tolerances, damping, competing requests and the
available proposal family also limit motion. Changing resolution, response
memory or proposal families is a separate experiment; it was not mixed into
this budget comparison.

Nine bidirectional runs still reached the new limit with changes occurring, so
64 passes is not a demonstrated settlement point for this system either.

Visual inspection exposes the distinction between numerical improvement and a
better interpretation. Source 4821138091802624 retains its erroneous peak-to-base
diagonal at pass 64 despite mapping strain falling from 3.134 to 2.105. Moving
existing endpoints cannot supply its missing roof/wall corner. Source
6527982287978496 also shows a limit of the weak teacher: the earlier cleanup omits
its prominent left tower. A polished reference can still miss the concept.

## The earlier cleanup is not fully present in this loop

The new experiment reuses the area estimator and associations, not the final
cleanup procedure. Their current differences are material:

| Capability | Coupled experiment | Earlier authored cleanup |
| --- | --- | --- |
| Two-window symmetry | Centers about the building axis and top alignment, only when exactly two observed windows were selected | Additional size/shape matching and clearance handling |
| Tower/roof/cross symmetry | No general pair-discovery or repetition law | Selected mirrored pairs, equalized towers/caps and related crosses |
| Roof/cross correction | Fixed internal construction shape; move/scale and declared attachments | Centered front peaks and specialized cross placement |
| Support and overlap | Soft bounding support/containment; no dedicated general roof-overhang law | Support interpretation and some foreground-aware wall splitting |
| Missing parts | Counts and construction types fixed | Conditional support/entrance additions |
| Unexplained ink | Retained but no active request for a new explanation | Some unresolved ink blocks inappropriate optional additions |

Appearance also differs: the coupled view displays interpreted ink as well as
construction shapes. The old cleanup mostly displays constructions with faint
acquired ink. The new comparison includes an interpreted-ink toggle so that
these layers can be compared explicitly.

## Learning conceptual corrections

The paired source and earlier cleanup can provide weak supervision for proposing
construction edits: introduce a mirror relation, share window size, attach a cap,
split a roof/wall contour, or consider a different part construction. Learning
the proposal and its approximate direction is more useful here than learning
only a polished final image.

The next extension should make those procedures propose connected expectations
and alternatives. For example, a matched-window hypothesis can introduce shared
size and reflection relations whose errors negotiate both windows and the wall.
The model can support the hypothesis and its participation strength; the cleanup
need not directly assign the final coordinates.

Unexplained source intervals should likewise remain connected to open explanation
claims. A cluster may support another window, writing, a clock or a different
building part. Coverage alone must not reward an arbitrary assignment or deletion
of the source. Candidate support, complexity and counter-evidence need to remain
visible, and shared strokes may support more than one construction.

The earlier cleanup is an authored teacher with errors and prior-only additions,
not ground truth. Review useful examples, keep observed and inferred parts
distinct, and split by acquisition before extracting patches or variations.

## Inspect and reproduce

Open `D:/_quickDraw/data/church-duration-2026-10-29-v2/comparison.html`. It shows the
original, passes 6/16/32/64 and a post-run cleanup reference in one common frame.
The unexplained-ink overlay identifies the original mapper's unresolved source
intervals on hover. Neither that overlay nor the reference affects inference.

`review.html` retains all four versions and every executed pass. Machine-readable
results are in `duration-analysis.json`, `duration-drawings.json` and
`prefix-verification.json`. See [the replay guide](../docs/church-duration.md) for
commands, controls and limitations.

The numerical run completed from 16:37:17 to 17:12:26 UTC, using one worker. An
earlier two-worker resource trial was interrupted before any drawing completed
and is superseded. The producer now releases a completed original proof graph
before independently restoring its archive. This changes object lifetime, not
the calculation; exact earlier-pass checks passed after the change.

## Verification

All forty numerical archives were independently restored before acceptance. The
final boundary import retained their original transport rows and finalized every
executed construction pass. A separate final audit restored all four versions of
two drawings, covering full 64-pass histories and the quiet control. It checked
8,159 generated points, 24,222 error reads and 11,316 receiver outcomes, with zero
coordinate difference and exact signed receiver accounting.

Browser checks covered all ten drawings, sixty common-frame milestone panels,
the interpreted-ink and unresolved-source toggles, and two hundred four-version
projection renders. They verified keyboard navigation, a default final-pass
view, deep links above sixteen passes, immutability and narrow-screen layout.
There were no script errors or external requests.

The Python suite passed 225 tests, GojoQuickDraw passed 135 tests and the linked
Gojo solution passed 5,545 tests across thirteen projects. Execution source
fingerprints, original study/input hashes and provider bytes were checked. No
incomplete temporary files remain in the completed dataset.

The dataset occupies approximately 7.07 GiB, of which 6.99 GiB is native archives.
The [machine-readable report](church-duration-2026-10-29.json) contains compact
per-drawing results and verification facts. Full per-pass diagnostics and
`verification.json` remain in the local dataset.
