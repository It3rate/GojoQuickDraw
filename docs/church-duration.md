# Longer runs of the same church experiment

The six-pass study was a bounded inference experiment, not a six-step trained
network. The local providers can be queried again after geometry changes. Their
weights do not learn during a run, and querying the same acquisition again does
not provide another independent vote.

The outer controller now permits up to 64 passes; the ordinary study still
defaults to six. A coupled run can stop earlier after two passes with no accepted
local rewrite or whole-tick actuator movement. Local-model-only runs stop after
one quiet rewrite pass. These stops do not establish zero error.

## Exact replay

Use a separate output folder and an explicit selection of 1–32 acquisition IDs
from the existing study's `cohort.json`:

```json
[
  {"id":"ff9440ecc7712e8937b4b7931d7859b9e1ce8ef1", "reason":"Inspect a longer trajectory"}
]
```

```powershell
python -m gojo_quickdraw.church_replay --source-study D:/_quickDraw/data/church-settlement-2026-10-29-v2 --selection D:/_quickDraw/_work/church-duration-2026-10-29-v2/selection.json --output D:/_quickDraw/data/church-duration-2026-10-29-v2 --passes 64 --workers 1
python tools/analyze_church_duration.py D:/_quickDraw/data/church-duration-2026-10-29-v2
dotnet run --project tools/church-audit/ChurchAudit.csproj -c Release -- seal D:/_quickDraw/data/church-duration-2026-10-29-v2 10000 1
```

The replay copies the exact compressed input snapshots and provider bytes. It
does not refit strokes, rerun part discovery, change associations, modify response
weights or use the final cleanup as an input. It preserves the original inner-step
count and candidate budget; only the outer pass budget changes. All four versions
are retained. Source/model hashes are checked before and after processing.

`prefix-verification.json` checks every earlier stage of all four versions,
including geometry, joints, parts, errors, rewrites and transfer summaries. A
mismatch remains an explicit failed experiment. The new folder must not contain
the original study or sit inside it. Temporary builds and batches stay under
`D:/_quickDraw/_work`.

Long native histories consume substantial memory. Run one worker initially.
The producer releases the completed original proof graph before independently
restoring its archive; this changes object lifetime, not the numerical response.
The final boundary import remains a separate structural operation.

## Inspecting the result

`comparison.html` shows acquired ink, passes 6/16/32/final, and the earlier authored
cleanup in one common frame. A quiet run's later panels explicitly name its actual
stop pass. The interpreted-ink toggle allows comparison of construction shapes
while keeping acquired ink faintly visible. Joint glyphs describe the current
interpreted source graph.

The cleanup reference is generated only after inference has finished. It is a
visual comparison, not a ground-truth label or a scoring target. It can contain
unsupported additions and wrong interpretations.

The unexplained-ink toggle highlights source intervals not assigned by the initial
part mapper. Its hover describes the profile, source interval and pen-stroke
index. These correspondences remain fixed during this duration test. They do not
yet request a new part or impose an explanation-coverage error.

`review.html` retains the four-version inspector and every executed pass. It opens
at the full requested pass budget and supports deep links beyond sixteen passes.
`duration-analysis.json` and `duration-drawings.json` report late rewrites, actuator
motion, source-parameter displacement, construction-bound displacement, remaining
strain and repeated visible tail geometry. None is a semantic accuracy estimate.

## Why longer runs can plateau

The current response uses damping, permitted intervals and quarter-unit actuator
ticks. Requests smaller than one tick remain visible as quantum remainder but are
not accumulated into a persistent fractional actuator between refreshed outer
worlds. Competing requests may also cancel. The same unchanged conditions can
therefore remain quiet with nonzero strain.

Part types, counts, internal normalized construction geometry and source/part
associations are fixed. Extra passes cannot add a missed window, turn a triangle
into a dome, or invent a missing interior corner unless an existing local proposal
can supply it. Persistent small-step responses and richer proposal families are
separate future experiments, not changes hidden inside this duration comparison.

## The earlier cleanup has more conceptual machinery

The initial area estimator is reused, but the final cleanup is not run inside the
coupled loop. The latter currently has only two-observed-window centering and top
alignment as explicit symmetry expectations. It has no general repetition machine,
tower/cap pair matching, equal-sized window family or construction-type replacement.

The earlier cleanup also selects mirrored tower/cap/cross arrangements, centers
front peaks, follows cross attachments, separates openings, resolves some foreground
overlap by splitting walls, and conditionally proposes a missing support or entrance.
Its cleaner appearance partly reflects rendering constructions while the new
experiment also shows the interpreted and unresolved ink.

Those procedures can become candidate construction edits and connected relation
expectations. A learned opinion could favor reflection over translation, shared
window size over independent sizes, or a new enclosing construction over an
unexplained contour. The native ports, error responses and retained alternatives
would then negotiate the parameters instead of the cleanup directly choosing them.

Paired source/reference examples are useful weak supervision for those proposals.
Keep observed, inferred and prior-only parts separate; review targets, retain
unknowns, and split by acquisition before extracting patches. Low authored strain
or a polished reference must not automatically become a correctness label.
