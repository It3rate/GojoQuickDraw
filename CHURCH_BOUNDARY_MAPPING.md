# From church regions to supporting ink

Full church analysis adds **Source ↔ shape mapping** to the inspector and gallery.
It relates each proposed boundary to portions of the fitted source, while keeping
acquired strokes unchanged. These are revisable associations from authored
heuristics, not training labels or calibrated probabilities.

Select a dotted boundary to highlight its supporting intervals and indirect
endpoint references. Select a colored source interval to see every proposed
boundary that uses it. Part colors show provisional support, ochre shows shared
support, and rose shows unresolved ink. The **Unresolved ink** filter includes
unassigned profile portions and acquired samples without a profile. The chosen
view survives drawing and page navigation. Original ink remains visible.

## Optional construction

Layout policy v3 allows paired upper uprights and a top beam to suggest a tower
without a cap. The tower can end on the main wall's upper edge or reach ground;
its inferred bottom need not have its own drawn divider. Roof and tower are
optional. A cross can use a flat-topped tower or the building as its placement
reference. An absent roof no longer triggers a compulsory broad triangle.

Openings compare a rectangular U with several rounded U outlines. A rounded
head needs an interior crown as well as a lower curve cost, so a sloping beam
alone need not become an arch. Nearby opening strokes may join an interior
portion of a beam. A small closed or retraced mark does not by itself close a
large writing region. These checks remain limited; they are not text recognition.

## What is stored

`boundaryMapping` has schema `gojo.quickdraw-boundary-mapping.v1`:

| Field | Meaning |
| --- | --- |
| `targets` | A proposed part and an ordered selection of its actual scaffold segments |
| `targets[].ink` | Existing fitted profile IDs, source and target parameter intervals, direction, comparison features and acquisition order |
| `endpointSupport` | A selected profile's actual start or end; supports placement without claiming a connecting line |
| `relatedParts` | Existing construction participants, including the boundary owner |
| `targetRemainder` | Portions of the proposed boundary without assigned line support |
| `sourceCoverage` | Complete partition of each source profile into mapped, shared or unresolved ranges |
| `unprofiledSamples` | Acquired events outside all fitted source selections, including isolated dots |
| `policy`, `summary` | Finite resolution, unlearned status and descriptive counts |

For example, a source interval can support the building's top. Its middle portion
can also support the tower's bottom. The associations retain the same source
profile identity; they do not duplicate acquisition or create two independent
votes. Traversal can be forward along the source and backward along the target.

The parameter is a position along a fitted line or Bezier, **not an exact raw
sample index, time interval or arclength fraction**. The profile retains its actual
ordered source sample selection. The initial matcher samples 40 parameter bins
and publishes ranges on a 10,000-tick parameter basis. A curved target is the
existing finite ordered scaffold, not an exact circle introduced by a label.

## Direct, indirect and expected support

Boundary proposals compare distance to the target path and tangent alignment.
A small stroke-order separation cost contributes when candidate support comes
from other strokes. Openings and crosses initially use their selected regional
support; wall, roof and tower boundaries may share intervals from outside that
initial selection. The stored distances, allowances, alignment, order gaps and
heuristic costs make these choices inspectable and replaceable by later providers.

Ground and cross-reference levels are inferred construction, not detected ink.
Ground can refer to lower endpoints of upright profiles and its related building.
A prior-only cross has no direct source matches. If only a cross stem was found,
its horizontal arm remains an expectation. The outline thickness around a cross's
center axes is also an explicit construction assumption; it does not count the
same thin cross stroke as two observed outlines.

An unsupported boundary can therefore have endpoint support, related construction,
or only an open expectation. Proximity alone does not weld acquired sites.
Unresolved source ink remains available for later proposals such as lettering,
clocks or additional buildings; no such class is asserted by this pass.

## Native Gojo representation

Analyzed native records can have a fifth ordered field after opinions. Older
three- and four-field archives remain readable. Each boundary is a non-draining
read witness over the actual observer frame and existing source-parameter basis.
Its selected inputs include actual profile witnesses linked to ordered source
and target parameter segments, actual profile endpoint placeholders separately
from interval support, and actual contextual part witnesses.

Its result selects the owner's actual ordered scaffold edges, a separate target
parameter basis, uncovered target ranges and an open placeholder. The selected
source remains residue of observation; no transfer or source mutation occurs.
Unresolved source ranges and unprofiled acquired events are also connected.

The reader verifies actual reference identity, finite ranges, target complements
and the complete source complement after taking the **union** of shared support.
It rejects missing coverage, false unresolved ranges, foreign/equal-but-copied
edges or endpoints, and duplicate support within a boundary. Native tests remove
all descriptive adapter JSON before restoring these relationships. Named roles,
matching costs and verbal explanations remain adapter provenance; the native
reader verifies correspondence integrity, not semantic correctness or the
heuristic's numerical score.

## Training direction and limits

Future examples can contain source intervals plus candidate boundaries, their
relations, comparison features, alternatives and open remainder. Reviewer
corrections can supervise a small association provider without replacing the
acquisition or representation. Do not train as if today's chosen matches were
confirmed labels, or count several owners of one interval as separate drawings.

Current limits include hard regional proposals, a finite matching threshold,
weak order evidence rather than a temporal model, incomplete treatment of
backtracking curves, and a single main building envelope. Broad A-frame forms,
side views, lettering, outlined crosses and sparse silhouettes still expose
missed or incorrect parts. The window gate currently needs one sufficiently large
closed trace; multi-stroke or gapped windows may remain unresolved. Recognition
of meaning in unresolved ink remains open.
The 24 familiar development drawings are regression examples, not a held-out
measure of recognition quality.

Run **Full analysis → review** into a new output folder to recompute these reads
from existing fitted records. **Refresh both reviews** only rebuilds the display
from saved evidence. The saved policy and implementation fingerprint identify
which rules produced each collection; open browser tabs require a reload.
