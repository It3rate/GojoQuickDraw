# Connected church study — 29 October 2026

The coupled system is ready as an inspectable baseline. These results do not establish a learned church recognizer, semantic correctness, or reliable convergence.

## Cohort and method

The run selected 1,024 unique game-recognized churches from 142,273 recognized records in a 164,225-record file. Sixty acquisitions recorded in the local providers’ training snapshots were excluded. There is no overlap with those recorded sources. All selected records contain per-point timestamps and stroke order. Source-text hashes and the original corpus file hash were verified unchanged.

Four versions were run for each drawing: local repair/line/closure models, shapes around fixed ink, ink guided by a fixed scaffold, and bidirectional ink/scaffold responses. Each had a six-pass limit. Coupled passes perform one native numerical response before re-reading the graph. No final church cleanup rendering entered inference or evaluation. No new model was fitted.

The numerical phase completed all 4,096 versions with zero preparation or native errors, from 13:22:24 to 14:52:18 UTC. Future whole-acquisition splits are 716 training, 151 validation and 157 test drawings; they were not used to fit a model in this experiment.

## Descriptive results

| Version | Mean mapping strain | Fixed-parameter source drift | Active source interval coverage | Local rewrites | Quiet stops |
| --- | ---: | ---: | ---: | ---: | ---: |
| local-models | — | 3.233 | 97.28% | 11,898 | 386 |
| shapes-only | 1.976 | 0.000 | 100.00% | 0 | 83 |
| fixed-scaffold | 1.792 | 3.471 | 97.59% | 13,528 | 5 |
| bidirectional | 1.658 | 3.432 | 97.60% | 13,492 | 7 |

Strain is an authored compatibility measure normalized in each error’s own port basis. Active graph changes can change the measured term set. Source drift is expressed in the fitted packet’s drawing coordinates and uses the same seventeen parameter samples per initial nondegenerate profile; missing intervals are counted separately and never receive zero error. Interval coverage is parameter participation, not physical ink length. The exact acquisition remains retained even when interpreted intervals are trimmed.

Bidirectional feedback changed the joint-incidence signature relative to local models alone in 512/1,024 drawings and relative to a fixed scaffold in 118/1,024. A signature includes junction kinds and which directed source participations meet; equality does not establish geometric equality or correctness.

Its mapping strain was lower than fixed scaffolding in 857 drawings, higher in 133, and tied in 34. The paired mean difference was -0.1342, with a whole-drawing bootstrap percentile interval of [-0.1465, -0.1212]. This is evidence of a systematic change in our compatibility measure, not semantic accuracy.

There is an important tradeoff: mean attachment strain increased from 0.5998 with fixed scaffolding to 0.9077 with bidirectional responses. Containment strain decreased from 1.0973 to 0.9765. The system moves tension between expectations; reducing one error does not certify an improved whole drawing.

Only seven bidirectional runs stopped quietly; 1,017 reached their effort budget. Quiet means no selected whole-tick changes under the current laws, not zero error. Fractional response remainder occurred in 6,141 of 6,142 bidirectional numerical rounds. Remaining errors and remainders are retained rather than declaring convergence.

## What visual inspection found

In source 4821138091802624, a poor interpretation spanning a roof and a wall becomes a long diagonal. Bidirectional feedback can reinforce that interpretation. Its final mapping strain is about 3.134 versus 1.047 with fixed scaffolding. The next useful proposal is an interior corner or line split supported by the roof/wall hypothesis; moving endpoints alone cannot create the missing degree of freedom.

Source 6474467716890624 illustrates incomplete part detection: several openings remain visible in the source graph but are not all selected as window constructions. Existing part proposals and counts are held fixed during this study. Numerical settlement cannot discover a missing object by itself.

These are inspection examples, not human-labelled accuracy estimates.

## Verification

The Python suite passed 219 tests, the GojoQuickDraw solution passed 134 tests,
and the linked Gojo solution passed 5,545 tests across thirteen projects. A fresh
three-drawing conversion through the ordinary converter also restored its native
artifacts and verified their hashes.

The browser check visited all 86 pages and all 1,024 unique drawings, inspecting
4,288 projections across the four versions and representative intermediate passes.
It found no script errors, external requests, nonfinite geometry, retained-data
mutation or narrow-screen overflow. Keyboard navigation and pass persistence
between pages passed.

All 4,096 numerical archives were independently restored before acceptance, then
finalized with native scaffold boundaries. A stratified final audit restored all
four versions of sixteen drawings, including complex, degenerate-profile and
regression cases. It checked 11,682 generated points, 38,719 error reads and 17,442
receiver outcomes. Coordinate difference was zero, and the exact signed receiver
accounting balanced.

The retained dataset occupies approximately 84.56 GiB, including 83.53 GiB of native
archives. The numerical phase took about ninety minutes; final native boundary
encoding took a further 49 minutes 44 seconds with eight workers. Complete witness
history is expensive; future training should extract smaller connected patches
while retaining correspondence to this evidence.

The [machine-readable report](church-settlement-2026-10-29.json) includes analysis,
implementation/model hashes, acquisition checks, audit selection and verification
receipts. The local dataset also contains `verification.json`, `native-audit.json`
and `native-audit-selection.json`. No incomplete temporary files remain in it.

## Native evidence and preservation

The numerical archives retain source annotations, ordered non-overlapping source intervals, local candidate witnesses, graph rewrites, native point/shape generators, exposed port relations, explicit inverse error responses, capacity admissions, signed receiver accounting and histories.

Review joint glyphs belong to the interpreted source graph. Scaffold boundaries
remain separate constructions connected through errors, rather than becoming
acquired lines or shared source sites merely because they are drawn together.

A subsequent structural import binds declared scaffold boundary edges to the actual public output roles of those native point generators. It adds authored expectations, not acquired lines, and does not retroactively influence settlement. The original numerical graph remains intact inside the wrapper. The packed importer requires every original carrier, tick, location, relation, unit and placeholder row to remain an unchanged prefix; temporary reference proxies are eliminated.

Zero-length profiles are retained in acquisition/annotation evidence but excluded from active curve edges: 115 such profiles occurred in 91 drawings. The first trial exposed an adapter bug that paired source IDs with filtered edge ordinals. That v1 trial is superseded; the current experiment binds actual native profile witnesses and has a regression test for an omitted tap between two valid profiles. Existing converted datasets were not changed.

The native encoder/auditor checks that boundaries refer to the actual producer roles, that archive restoration preserves graph and generated coordinates, that error reads match the review, and that signed participation balances at each receiver. Verification receipts accompany the generated dataset.

## Recommended next experiment

1. Add shape-supported interior anchors and bend/corner split proposals. Preserve competing line/curve/joint constructions and their source intervals.
2. Learn utility on actual connected receiver outcomes, with separate source, attachment, containment and complexity consequences. Use reviewed patches and acquisition holdouts; low strain must not automatically become a correct label. The earlier synthetic point-consensus regression/neural task is not adequate supervision for this system.
3. Improve source-to-part associations and object/view alternatives. Retain unresolved words, details and ambiguous strokes instead of forcing them into a building or opening.
4. Add adaptive effort, persistence and resolution policies; compare them with the fixed six-pass baseline. Do not equate a quiet whole-tick read with a zero-error interpretation.
5. Only then broaden learned category priors and structural shape replacement. Keep the current heuristic and model-only controls.

## Reproduce and inspect

Open `D:/_quickDraw/data/church-settlement-2026-10-29-v2/review.html` for all 1,024 drawings, twelve per page, with four-version details and every executed pass. Dataset `analysis.json`, `summary.json`, `cohort.json`, `acquisition-audit.json` and `source-verification.json` contain machine-readable results and scope checks.

See [the study guide](../docs/church-settlement.md) for commands, native storage, limitations and the full two-phase pipeline. Source code and tests are in GojoQuickDraw; the linked Gojo revision is 9b7af32075aaef435382ff0bc0ecc7db8e39ef3a.
