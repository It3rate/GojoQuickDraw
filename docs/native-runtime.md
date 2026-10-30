# What Gojo executes in QuickDraw

QuickDraw currently combines ordinary recognition code with native Gojo
construction and settlement. It is not yet an entirely native recognizer.
The connected refinement performs real computation through Gojo relations,
ports, equations and carrier tension. The older ten-stage cleanup makes many
of its decisions in Python before importing the resulting constructions.

## The calculation boundary

| Work | Present implementation |
| --- | --- |
| Read QuickDraw and fit its strokes | Python geometry over measured coordinates; source order, intervals and exact acquisition are imported into native evidence. |
| Propose walls, roofs, windows, crosses and their relationships | Mostly authored Python/C# algorithms. These choose candidate meanings and compile their constructions and error laws. |
| Score local joint and closure alternatives | Small conventional logistic models and authored selection code. Their opinions are connected to native evidence; the models themselves are not native Gojo networks. |
| Generate a shape from its current parameters | Native executable expressions. Points depend on actual parameter carriers and ordered expressions; changing a carrier changes the generated geometry. |
| Assess a connected error and respond | Native `EvidenceObligation` relations read exposed ports, calculate permitted participation and unmet demand, and propose responses. `NegotiatingMachine` admits those responses and applies carrier tension. |
| Decide when to merge, replace or capture a construction | An authored host controller. Once admitted, exact contact and symmetry use actual shared native dependencies. |
| Show a picture | A small JSON read of the native result. It is a view cache, not the executable structure. |

The native interpreter is C# code; that alone does not make its computation
external to Gojo. The distinction is whether connected structure supplies the
operation, participants and legal response, or whether a host branch simply
chooses and assigns an answer. Both kinds currently exist, in different places.

A roof alignment example follows this path:

1. The proposal algorithm selects a roof and supporting wall from the ink.
2. Their constructions expose actual native boundary ports.
3. An error structure connects those ports to a permitted alignment interval.
4. Native reads find the excess or missing participation.
5. Response passages address the machines' exposed ports. Bounds, capacity,
   finite resolution and candidate admissibility can block part of a request.
6. Accepted signed tension changes the live parameter carriers. The same
   construction expressions regenerate the roof and wall.
7. The remaining error is still readable. A later construction capture can
   replace independent coordinates with a shared dependency.

The original ten-stage cleanup often moves bounds directly in Python instead.
Restoring that final construction proves that its encoding works; it does not
prove Gojo inferred the correction. Connected refinement is the stronger test
of native calculation, although its proposal discovery remains a bootstrap.

## Why the files and runs became large

Two different storage problems developed:

* Ordinary Studio conversion saved a textual JSON serialization of the entire
  native relation graph, compressed afterward. In one retained complete
  example, this expanded to over 200 MB of JSON before compression.
* Connected refinement already saved binary Gojo, but saved almost every
  historical world, proposed alternative, response, private preview and read
  witness. Explicit imports connected later worlds to their ancestors.
  Independently restoring the archive allocated that history again.

The ordinary graph also contains duplicate acquired structures for stage
correspondence and encoded copies of some adapter JSON as provenance. Those
copies are not substitutes for native meaning, but they add overhead. The
binary codec change preserves them; removing them needs a separate structural
loss and compatibility contract.

## The new storage choices

Ordinary **Convert drawings** and **Complete pipeline** now write
`native/<id>.gojo.bin.gz`, using Gojo's existing numeric binary format. The
complete graph and all ten stages remain. The reader detects the actual
decompressed format and also opens older `.gojo.json.gz` archives. Existing
datasets are not rewritten; a dataset can contain both formats through its
recorded artifact paths.

**Connected church refinement** defaults to a current-state native checkpoint.
It retains:

* Executable machine definitions, live carriers and their remaining tension.
* Exposed ports, response routes and shared parameter identities.
* Current part geometry, derived wall constructions and error laws.
* Fitted source geometry, imported source profiles and current interval links.
* Explicit open origins for inferred closure segments.
* Small visual observations of every executed pass.

It omits the full proposal history, discarded intervals, old import routes and
individual response receipts. Missing history is represented by open structure
and documented by the checkpoint's loss contract. This is not a complete replay
or a resumption of the adaptive topology/capture controller. Restored machines
can execute and answer fresh local numeric requests; their current error laws
can be reevaluated.

The **Keep full proof history** checkbox retains the earlier audit behavior.
CLI callers can choose `--archive-mode current` or `--archive-mode history`.
Changing the storage mode requires a new result folder, just like changing the
processing version. Source datasets are unaffected.

## What is still expensive

The connected loop still creates a new native scene each outer pass and imports
previous published values. Its live ancestry is not yet bounded, even when the
saved checkpoint is small. The response machinery also creates native preview
graphs and witnesses. These are real native calculations with implementation
overhead; smaller archives do not eliminate them.

This change additionally compiles the frozen logistic provider into numeric
lookup tables once, preserving the exact accumulation order. It removes a
JSON serialization and parse that previously extracted a single accepted-count
integer each pass. Neither changes model behavior or native settlement laws.

Measured phases now separate source import, host local inference, scene import,
native response rounds, visual reads, checkpoint selection, encoding and restore.
The review's collapsed **Processing and saved native state** section exposes
these measurements and the actual native archive. Python run metadata records
proposal preparation, build, worker and review time separately.

## The next runtime step

The useful direction is a persistent native working world with compact,
indexed execution tables derived from its relations. Arithmetic and finite
resolution are already available; a new bit vocabulary is not required first.

1. Keep the same machines alive across numeric passes; replace only the
   constructions affected by accepted topology changes.
2. Cache immutable read plans and invalidate only dependencies touched by
   accepted carrier changes.
3. Make transient witnesses and optional audit retention explicit policies.
   Preserve blocked participation, residue and active uncertainty in the live
   world even when chronological explanation is omitted.
4. Compile repeated relation expressions into indexed operations while keeping
   a correspondence back to native carriers, ordered relations and ports.
5. Move proposal mechanisms into native structures incrementally, testing
   their behavior against the current authored reference.

This separates three independent goals: smaller serialization, faster native
execution, and more native conceptual inference. Improving one does not
automatically establish the others.

See the [measured verification report](../reports/native-runtime-2026-10-30.md)
for the controlled comparisons and their limits.
