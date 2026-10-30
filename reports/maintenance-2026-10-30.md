# Studio integration and workspace cleanup

The current parser is available through both Studio paths. Ordinary conversion
retains the ten-stage pipeline with residual tower/cross recovery. The new
optional connected refinement reads saved fitted drawings and runs the same
CAD/recovery settlement used in the latest church study, preserving the old
areas and cleanup as comparison only. See [the guide](../docs/studio-refinement.md).

## Integration verification

The one-drawing pilot is
`D:/_quickDraw/data/studio-refinement-2026-10-30-v2/review.html`.
It consumes the already-saved sample `6226189452574720` from the earlier Studio
pilot. All nine saved connected stages exactly match that acquisition's earlier
recovery-study stages. Acquisition and model hashes match, runtime fingerprint
is current, the 80,508,948-byte native archive independently restores, and the
receipt checks all six retained artifacts. Resume reuses the completed result;
review-only refresh does not run settlement. This is an integration control,
not an estimate of recognition quality.

The source remains read-only. Selection is bounded and applies to distinct,
completed, game-recognized churches in the selected folder and order. The
default twelve drawings and eight passes do not silently apply to an entire
dataset. Saved Studio final geometry does not enter source features or targets.

Verification: 257 Python tests and 149 QuickDraw .NET tests passed. The Release
desktop build, native startup smoke and offline browser review passed. Browser
checks exercised six comparison panels, nine stages and overlays with no script
errors or external requests. The linked Gojo solution passed all 5,548 tests
across thirteen projects. No upstream Gojo files changed.

## Cleanup completed

Removed logical file size: **68,385,647,978 bytes**, approximately **63.69 GiB**.

| Retired material | Files or folders | Bytes |
| --- | --- | ---: |
| Superseded native proof histories in the large settlement study | 2,655 files | 58,053,787,560 |
| Five superseded study folders plus thirteen completed scratch folders | 18 folders | 10,331,860,418 |

The five removed study folders were duration v1, heuristics v1/v2 and connections
v1/v2 from 29 October. They were interrupted pilots or superseded implementations,
not current source/baseline dependencies. Prior metadata is retained in the
cleanup receipt. Historical reports still describe those trials; their deleted
generated artifacts are no longer directly available.

The large settlement study retains all **1,024 input packets and 1,024 visual
results**, model bytes, pages and manifests. Its sixteen selected reference
drawings retain all four native variants, totaling **64 archives**. The previous
1,377 retired histories plus this pass's 2,655 account for 4,032 retired histories.
Retired worlds require regeneration for exact native restoration. The existing
retention receipt now records their availability, and the review directs retired
archive links to that receipt. Browser checks verified both retired and retained
links.

Deletion used exact allow-listed paths, containment and reparse-point checks.
Native history paths and sizes were compared with their prior import receipt.
Stored hashes describe that earlier import; this cleanup does not claim a fresh
hash audit of every retired file. No user acquisition, current model, current
CAD/recovery native archive or `church3000` dataset was selected for removal.

Preserved:

- Raw `quickdraw`, `older`, all `church3000*` collections and model folders.
- Current CAD and recovery studies with all sixteen native worlds each.
- Source-study inputs, visual baselines and metadata still used for replay.
- `_work/cache` and `_work/qa`, including nested browser tooling.
- The current Studio v2 integration pilot and current maintenance receipts.

Automatic approval review rejected deletion of the superseded Studio v1
integration pilot with the message **blocked by policy** and no further reason.
That folder remains; no alternate deletion or move was attempted.

Machine-readable receipts live under
`D:/_quickDraw/_work/maintenance-2026-10-30`, and the native retention receipt
remains inside `data/church-settlement-2026-10-29-v2/retention.json`.

## Compact structure discussion

[Compact primitives and cooperating structures](../docs/compact-structures.md)
lists two levels: primitive participation/reads and reusable cooperating
machines. It records existing carrier-free laws, finite compiled masks, contour
construction and bounded utility/lifecycle experiments, then proposes sharing
bases/templates with compact per-instance support and connections. It changes
neither kernel semantics nor the parser's representation.
