# Connected church study

This is an independent experiment over real QuickDraw acquisitions. It couples the
existing three small neighborhood providers to executable native constructions and
native error obligations. It does not change Studio's default converter or promote
the old final church cleanup to training truth.

See the [29 October results](../reports/church-settlement-2026-10-29.md) for the
1,024-drawing comparison and its limitations.

## Run and inspect

```powershell
python -m gojo_quickdraw.church_study run --input D:/_quickDraw/quickdraw/church.ndjson --output D:/_quickDraw/data/church-settlement-2026-10-29-v2 --limit 1024 --workers 8 --passes 6 --steps 1 --candidates 48
python -m gojo_quickdraw.church_study review --output D:/_quickDraw/data/church-settlement-2026-10-29-v2
dotnet run --project tools/church-audit/ChurchAudit.csproj -c Release -- seal D:/_quickDraw/data/church-settlement-2026-10-29-v2
```

Alternatively, `tools/RunChurchStudy.ps1 -Output <new-folder>` runs both computation
and the final native boundary import. `--limit` bounds the numerical sample; the
sealer operates only on completed drawings. Sealing is resumable and never reruns
or moves a numerical world.

For an idle machine, the sealer's optional final arguments select its drawing
bound and concurrency, for example `seal <folder> 10000 8`. Its default is four
workers. Run the heavy numerical and final encoding phases sequentially to keep
memory available for their native witness graphs.

Open the output's `review.html`. It has twelve input/result pairs per page and a
four-version detail view. Page changes preserve the selected comparison and pass.
Arrow keys navigate drawings; Shift with left/right navigates pages; up/down changes
the pass. The gallery is regenerated during processing and can also be regenerated
independently from retained results. Reload to read newly completed drawings.

Joint glyphs describe the interpreted source graph. The colored scaffold boundaries
are separate constructions connected through the native error relations; drawing
them at the same coordinate does not weld them into the source graph.

The sample count is a hard bound on selected acquisitions. Selection is a seeded
priority sample from records whose `recognized` is exactly true. This is the game's
recognition result, not human recognition. Records referenced by the selected
providers' `sourceSnapshot` are excluded by acquisition key. Invalid or oversized
drawings remain explicit preparation failures; the runner does not replace failures
until it obtains a more attractive cohort. Whole-acquisition split assignments are
retained for future learning, but this run does not fit a new model.

The output must be separate from the source. Repeating the same command resumes
completed drawings; source, implementation, model and budget changes require a new
study folder. Large runs retain substantial native evidence. Work batches, binaries
and logs go under `D:/_quickDraw/_work/<study-name>`, never loose in a root directory.

## Four comparisons

| Version | Local repair/line/closure models | Ink motion | Shape motion |
| --- | --- | --- | --- |
| `local-models` | Enabled; re-read each pass | Explicit topology/curve replacements only | None |
| `shapes-only` | No topology replacements | Fixed by zero passage capacity | Enabled |
| `fixed-scaffold` | Enabled | Native tension and local replacements | Fixed by zero passage capacity |
| `bidirectional` | Enabled | Native tension and local replacements | Enabled |

Each pass first proposes source-local topology changes. It then builds exposed
native machines and connected error structures, responds to those errors, and
publishes only accepted displacement back into a new interpreted graph. The next
pass re-reads that graph. A gap can therefore become a valid attachment candidate;
a moved or straightened line can change a later junction read. A pass is not another
independent acquisition. Two quiet coupled passes stop the run; otherwise the effort
budget ends it with remaining strain visible.

Passes retain separate finite native worlds. A refreshed scaffold explicitly imports the previous public bound publications through retained port correspondences and calibrations; matching coordinates alone do not establish continuity. The source-derived scaffold seed remains the original observation basis. This is a bounded coupled experiment, not a general persistent Gojo network.

The two systems remain distinct: graph rewrites explicitly merge/split sites and
source intervals; native carrier tension changes parameters. A nearest projection
is never automatically a shared site. Numeric coordinate equality never merges two
sites. New closure edges remain inferred and have no acquired stroke reference.

## Operative structure

The linked Gojo `NegotiatingMachine` and `EvidenceObligation` implementations own the
numeric response. A source endpoint has a native two-parameter point construction,
exposed coordinate ports, response calibration, capacity, publications and timeline.
Its original acquisition correspondence remains attached.

A provisional church part has a native offsets rectangle providing its four bound
ports. An executable affine construction creates its actual boundary from those
same parameters: rectangle, triangle, dome trace, cross outline, window frame or
another existing source-selected scaffold. The boundary consumes live parameters;
it is not a saved image. This experiment can move and scale the construction but
does not yet replace its type or discover an additional object.

The setup adapter uses the existing source-derived church area estimator and finite
boundary mapping. Role names select authored seed expectations in that adapter.
After setup, an error executor reads actual port references, affine calibrations,
allowed intervals, response equations and limits. It does not branch on `roof`,
`rectangle`, `window`, or a descriptive error name.

An obligation retains its input ports, projection into its own basis, permitted
interval, inverse response routes, mixture shares and influence. Its observation
contains the actual sum, interval violation, correction and an ordered interval
from the actual value to the permitted value. That interval is the error structure;
there is no free error parameter that can become zero while the geometry stays
unchanged. Its responses address the contributing ports, so either participant
can accommodate a relation.

Current obligations include source displacement allowances, source-supported
ink/boundary correspondences, near-axis continuity, declared construction
attachments, positive extent, bounding containment, supported towers' horizontal
support, and provisional reflection/level relations for a pair of observed windows.
They are soft local expectations. Remaining overhang or conflict is an unresolved
error, not proof that a physically valid arrangement was found. Bounding containment
does not prove filled-region containment, and page-down does not establish gravity.

Quarter-pixel actor ticks use an explicit shifted frame. The import retains
calibration and resolution remainder. Publishing adds only the actor's accepted
displacement to the original higher-resolution site. A lower-resolution observation
alone cannot snap or move that site. Source anchors remain referenced to the
original fitted source even after several interpretation passes; shape seed
allowances remain referenced to the original source-selected scaffold.

## What is learned, authored, and measured

The bundled repair, line and closure providers supply their existing relative
support. Their coarse input cells, coverage limits and authored admissibility gates
remain in effect. This study does not use the recent synthetic point-consensus
regression or neural weights as church policies: that experiment showed poor
connected-world transfer. Fitting response policies on the actual connected
counterfactual outcomes is a later step.

Shape discovery, part roles, source/boundary association, containment expectations,
response shares, clipping and capacities are authored. The native executor performs
projection, response, admission, carrier tension and publication. Model scores are
not calibrated human probabilities. Error reduction does not establish conceptual
correctness or learned understanding of a church.

Zero-length source profiles are retained in acquisition/annotation evidence but do not become active edges. Source IDs bind the actual imported native profile witnesses, including those omitted from the active graph; a filtered list index must never select a source. The initial v1 trial exposed this failure and is superseded.

No final cleanup rendering, final shape labels, or source-to-final weak target is
an input or evaluation target. Input packets are explicitly restricted to acquired
source, fitted program and annotations. The source-derived scaffold and its mapping
are supplied separately as qualified hypotheses. Unmapped ink is preserved.

## Retained artifacts

| Path | Meaning |
| --- | --- |
| `study.json` | Exact source/model/implementation fingerprints, count, budgets, status and failures |
| `cohort.json` | Selected acquisition identities, original-text hashes, future whole-drawing splits |
| `inputs/*.json.gz` | Exact source text, fitted source packet and source-derived hypotheses |
| `models/local-providers.json` | Exact model bytes used by the experiment |
| `drawings/<id>/<version>.gojo.bin.gz` | Complete native Gojo binary graph, compressed, independently restored and validated before acceptance |
| `drawings/<id>/view.json` | Derived review projection of every executed pass |
| `drawings/<id>/error.txt` | An explicit failed native run, where applicable |
| `preparation.json`, `native-results.json` | Per-acquisition preparation and native outcomes |
| `summary.json`, `pages/*.js`, `review.html` | Rebuildable descriptive summary and offline paged review |
| `boundary-import.json` | Lossless import receipts binding declared scaffold edges to the actual native generator output roles |
| `analysis.json`, `analysis-drawings.json` | Descriptive whole-cohort comparisons and per-drawing inspection priorities |
| `native-audit.json`, `native-audit-selection.json`, `verification.json` | Final verification facts and the independent native restoration sample, when audited |

The numerical run first retains the native point generators, error relations and
source graph. The separate final import adds the scaffold's declared directed
boundary edges between those generators' actual output roles. These are constructed
expectations, not additional acquired ink. A sealed archive is
`G(original run, import witness)`; the original run remains intact and shares its
actual producer roles with the new boundary relations. The import records the
previous compressed archive hash and validates every source/output binding.
It does not retroactively influence settlement or change any numerical result.
This separates an explicit structural import from a claim that the numerical
study was already negotiating every aspect of shape topology.

The importer compiles a small native overlay and binds its temporary references
to the existing transport references. Every original carrier, tick, location,
relation, unit and placeholder row must remain an unchanged prefix. The temporary
proxy slots are removed; decoded output contains the actual shared producer roles.
This avoids rebuilding all numerical proof objects just to add boundary edges.
The independent auditor still restores complete sample archives and checks their
geometry, error reads and exact signed receiver accounting.

Binary graphs use the linked kernel's `GojoNumericFormat`, preserving identity,
aliases, carriers, definitions, ports, native error witnesses, source intervals,
inferred additions, removed participation and histories. This is the same kernel
graph, rather than a new application JSON encoding of its meaning.

Each receiver's signed actuator participation is checked as requested equals actual
movement plus bounds-blocked, capacity-deferred, quantum and candidate-rejected
participation. Passage clipping and response damping retain their own witnesses.
The review's magnitude totals summarize different actuators; they are not one
conserved physical energy and should not be added as if they were.

Descriptive comparisons report local rewrites, later-pass rewrites, movement,
components, cycles, inferred edges, remaining source interval coverage and normalized
strain by layer. Mean strain can change when the active graph changes, so it is not
a fixed accuracy benchmark. Endpoint drift excludes removed intervals; coverage is
reported separately. A visually worse result can have lower authored strain.

## Next learning work

1. Review matched successes and regressions, including ambiguous views, missing
   objects, asymmetric drawings, disconnected ink and words or ornaments.
2. Keep source-derived association failures separate from response failures. A
   correct local response to an incorrect scaffold can worsen the interpretation.
3. Form local counterfactual training patches from actual retained port/error
   neighborhoods, including inactive interval claims and shared actuators. Split
   every patch and every pass by acquisition identity.
4. Compare fixed responses with a small regression and small neural predictor on
   actual connected outcomes, preserving the unchecked baseline and coverage gates.
5. Add alternative shape constructions and explicit object-count/view hypotheses.
   Those should compete through their connections and retained errors, rather than
   a category dispatcher selecting an opaque final answer.
