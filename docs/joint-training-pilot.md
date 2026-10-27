# Joint matching: a bounded first training experiment

This is an exploratory training proposal, with an implemented **untrained**
retrieval baseline. No model has been trained by this checkpoint.

This whole-drawing baseline remains historical. A later
[local patch experiment](local-joint-model.md) has now trained a small model;
its controlled and weak-reference results are separate from these retrieval
scores.

## What the current evidence says

The 27 October 2026 check selected the existing 300 church records in
`D:/_quickDraw/data/church3000`. Two hundred contained saved native joint
comparisons; the older hundred did not contain that cache and were skipped.
The experiment uses the saved earlier finals, before the new symmetry fixes.
It does not regenerate or overwrite either branch of the user's dataset.

Among 641 unambiguous source-to-final matches, 314 retained the same joint
type: **49.0%**. Among 1,084 relative-order comparisons for those matches,
**78.4%** retained their above/below/left/right/aligned/level relationship.
There were also 1,174 ambiguous, 674 unmapped and 140 added final joint reads.
These are counts of observer readings, not confirmed physical contacts or
independent training samples. Correspondence comes from existing boundary
support; it is not human ground truth. O denotes a closed-walk basis and is
not interchangeable with a point junction.

For retrieval, each source was compared against all 200 finals using joint
types, adjacent types with coarse headings, and relative-order type pairs.
The descriptor uses **no point coordinates, source-profile correspondence,
item identifiers, part names, category labels or timing**. Local node IDs only
connect a graph; tests verify that renaming them, reordering arrays, and changing
coordinate/provenance fields cannot identify a pair. Smooth degree-two I runs
are compressed in this derived read, leaving the saved graph unchanged.

| Read | Chance | Types only | Types, adjacency and relative order |
| --- | ---: | ---: | ---: |
| Own final ranked first | 0.5% | 0.25% | 1.0% |
| Own final in first 5 | 2.5% | 3.13% | 4.5% |
| Own final in first 10 | 5.0% | 6.13% | 9.5% |
| Own final beats a random other final | 50% | 51.74% | 51.98% |
| Own final beats another with similar final joint count | 50% | 53.56% | 54.14% |

Ties receive expected credit under random tie-breaking. The comparison pool
contains only churches, avoiding an easy category shortcut. Four drawings hit
the saved 64-node relative-order limit. The descriptor discards much of the
connected graph, and all weights were fixed before this run. These figures
are descriptive, not a held-out model accuracy estimate or evidence that the
current own-final interpretation is correct. The result is **not reliable
matching yet**; stronger-than-chance top-ten retrieval is a starting point,
not a solved task.

There is also a marked source/final vocabulary shift: these source reads
contain many more X/K candidates, while finals have more T/Y and closed walks.
Gaps, overruns, merges and added boundaries can change joint type legitimately.
A useful model needs to learn those transitions and which comparisons remain
unresolved. It should not demand identical type histograms.

Full results, the cohort fingerprint, per-drawing ranks and top candidates are
in [the report](../reports/joint-retrieval-2026-10-27.json). Reproduce the read:

```powershell
python -m gojo_quickdraw.joint_retrieval --dataset D:/_quickDraw/data/church3000 --limit 300 --output D:/_quickDraw/_work/qa/joint-retrieval-next.json
```

## Recommended first model

Start with a small **local graph-pair scorer**. Give it a source patch and one
candidate patch from the next stage, initially around a cross/roof attachment
or a window frame. It returns candidate support, expected type changes and an
explicit unmatched option. It supplies an opinion to connected Gojo evidence;
it does not replace the evidence or choose the whole church in one operation.

The input can be pre-numeric: joint type alternatives, ordered incident edges,
coarse directions, above/below relations, loop participation, shared-site versus
proximity evidence, and whether a boundary was observed or proposed. Raw signed
positions remain available in the archive but need not enter this model. A
low-resolution read must retain its ambiguity rather than imply exact geometry.

An initial implementation budget is 2–3 message-passing layers with 32–64
channels, followed by a pair scorer, roughly tens to a few hundred thousand
parameters depending on the inputs. This is an engineering proposal, not a
measured minimum or a training-time guarantee. Graph Matching Networks provide
a directly relevant precedent for learned similarity between connected
structures ([Li et al., 2019](https://arxiv.org/abs/1904.12787)).

Use two related objectives:

1. **Local correspondence:** rank a known supported partner above competing
   local patches. Preserve alternate candidates and an unmatched/open outcome.
   Support a group-to-group match where short intervals merge or split; a strict
   pointwise bijection would be an incorrect target here. Explicit rejection
   of unmatched points is a useful idea from
   [SuperGlue](https://arxiv.org/abs/1911.11763), although its image descriptors,
   pretrained weights and one-to-one assignment are not a drop-in solution.
2. **Drawing-level contrastive retrieval:** rank the drawing's plausible final
   graph above other plausible church finals. Train on similar-count negatives
   and controlled changes such as moving a cross to the wrong support, losing
   one window, or breaking containment. Use more than one acceptable positive
   when coarse evidence admits several interpretations. Otherwise the loss
   would falsely teach that structurally equivalent finals are unrelated.

Contrastive objectives can bring related examples closer while separating
negatives; supervised contrastive learning is one reference for supporting
multiple positives ([Khosla et al., 2020](https://papers.neurips.cc/paper_files/paper/2020/hash/d89a66c7c80a29b1bdbab0f2a1a94af8-Abstract.html)).
The definitions of positive and negative here must come from the particular
connected claim and its reviewed evidence, not merely a category label.

## A practical first dataset

- Review **100–300 original drawings**, selecting a few reliable local
  correspondences and explicit non-matches in each. This is a pilot budget to
  discover whether a signal exists, not an expected accuracy threshold.
- Use **1,000–5,000 controlled synthetic patches** to warm up gap, overrun,
  split/merge and distortion behavior with known correspondences. Preserve
  which variants share a generator and source; they are dependent examples.
- Expand to **1,000–3,000 distinct real drawings** only after the small pilot
  improves held-out local matching. Retain uncertain automated labels as weak
  suggestions, and inspect difficult disagreements rather than training every
  heuristic final as truth.

Split by original drawing **before** creating stages, crops or augmentations.
Keep all versions of one source in the same split. Hold out some synthetic
templates as well. Strip correspondence IDs, coordinates and source keys from
the input tensor; retain them in a separate audit/provenance surface.

Begin with adjacent rungs rather than the entire original-to-final jump. The
first useful outcome is a local choice such as “this X-like roof overrun is
better explained as one attachment” or “these two frames belong to a repeated
window pair.” Evaluate local correspondence precision/recall, unmatched
rejection, pair ranking against hard negatives, and residual constraint
violations. Track type transitions and disagreement with reviewed labels.
Whole-church retrieval can remain a diagnostic until these local reads work.

Gojo integration should keep the selected source, candidate target, basis,
model version, calibrated support, competing candidates and unresolved
participation connected through existing witness/port surfaces. A learned
score is evidence for a claim; it does not establish contact, shared identity,
physical support, or a new source observation by itself.
