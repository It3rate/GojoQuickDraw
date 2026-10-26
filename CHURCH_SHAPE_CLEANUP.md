# Church shape cleanup

Implemented exploratory pass, 26 October 2026. This is an automatic Python
heuristic pipeline with independently readable native Gojo evidence. No model
has been trained, no AI service is called, and these are review proposals.

## Running it

Choose a converted collection in QuickDraw Studio, keep **Analyze every drawing
in the selected generated folder and subfolders** selected, choose a fresh
analysis output, and run **Full analysis → review**. Select **Church cleanup
(final shape stage)** in either review. The gallery defaults to this stage when
it is present. Page changes retain the selected stage.

An existing analysis needs a new output after this implementation update.
**Refresh both reviews** rebuilds presentation from saved evidence; it does not
apply new detection or cleanup rules. Original collections are never overwritten.

## Detection before refinement

- Outlined crosses can span several connected pen strokes and can have open
  ends. Inset upper/lower stems and a wider horizontal band support this read.
  The inferred medial axes are explicitly prior-only boundaries; only outline
  boundaries can claim outline ink. Ordinary crossing strokes remain supported.
- Broad upper roof bands with an eave or a sloping end can propose an oblique
  quadrilateral. It does not require a front triangular peak.
- A cap beside the main building can belong to a tower when local upright
  portions support it down to ground. Mere adjacency without those portions
  does not establish that interpretation.

## Refinement and expectations

The earlier area estimate stays intact. A separate `cleanup` surface proposes:

- symmetric front triangular caps and centered domes;
- roof placement relative to the selected wall or tower;
- equal dimensions and mirrored placement for plausible paired towers and caps;
- crosses centered within their cap or attached at its revised top;
- separate side towers, joined to the main wall, without forcing global symmetry;
- contained openings and ground-attached entrances;
- at most one prior entrance when no entrance was detected, and a pair of prior
  windows when none were detected and there is sufficient wall area.

Observed opening counts are retained. Optional additions use `prior-only` and
have no direct source intervals. Refined shapes refer to their earlier area
estimates; those retain their source-to-boundary mappings. A cleaned boundary
does not acquire ink simply because it resembles a nearby stroke.

The viewer uses faint original strokes, solid refined boundaries, and dashed
expectation additions/ground. Hover or select a part for its changes, provenance,
construction references and source stroke order. The original ink is never
removed. Unresolved ink remains inspectable in the mapping view.

Pen order and construction dependency order are distinct. The pass retains the
selected source stroke indices, while its proposed hierarchy follows the
attachment dependencies. This does not claim that the person's mental hierarchy
has been recovered from pen order.

## Connected native evidence

The native record accepts an optional sixth field after boundary mapping:

```text
group(frame, result parts, before/after correspondences,
      shared contacts, mirror reads, adapter provenance)

correspondence = ReadWitness(before part -> after part, frame,
  group((actual before site -> actual after site) -> signed XY difference),
  before part, acquisition)

contact = (actual parent -> actual child) -> (shared site -> shared site)

mirror = ReadWitness(group(left part, right part, axis part), frame,
  group(left.L + right.R - 2*axis, left.R + right.L - 2*axis,
        top difference, bottom difference), selection, acquisition)
```

Selected before/after sites are correspondences, not identity. Contacts select
the actual same proposed site. Equality of two independent coordinates cannot
substitute for a contact. Roof/eave, tower/ground, cross/peak, and door/ground
attachments remain readable after the descriptive JSON is removed. Mirror
residuals remain signed observations at the finite coordinate resolution.

Every refined site has a selected earlier-site correspondence when its part
has an earlier estimate. A genuinely added part instead references its refined
construction parents and retains open slots; it cannot claim direct ink or an
unrecorded earlier estimate. The existing native context also retains actual
ordered scaffold edges and signed pair-bound reads for participating parts.

This is non-draining observation and a separate authored proposal, not mutation
of acquired carriers. Python proposes the geometry. The native reader verifies
reference identity, arithmetic, correspondence coverage and shared contacts; it
does not certify church semantics or implement a general feedback solver.
Rerunning the pass recomputes dependent placements. The stored result is a
readable proposal snapshot, not an autonomous learned generative machine.

## Current limits

View selection is a coarse front-like versus side/oblique hypothesis. It does
not recover perspective or a camera. One main wall remains; some sparse or
curved buildings, tilted crosses, multiple crosses on one support, windows and
clock faces can still be missed or misinterpreted. Prior openings may disagree
with intended architecture. Earlier alternatives and unresolved ink remain
available for later training and feedback.

The development review contains 36 selected drawings, including the four user
examples and the earlier 24-drawing pilot. It is a regression and inspection
set, not an unseen accuracy benchmark.
