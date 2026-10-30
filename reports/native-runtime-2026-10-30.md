# Native runtime and storage verification

The connected church experiment now separates current executable state from
full historical proof retention. Ordinary Studio conversion uses Gojo binary
storage for its complete graph. Both changes preserve the checked visual
results; neither makes the authored church recognizer entirely native.

## Measured comparisons

Sizes below are decimal MB. Each timing includes a fresh native worker process,
native import, calculation, archive writing and independent restoration. It
excludes compilation, raw stroke fitting, Python proposal preparation and HTML
diagnostics. These are small controlled examples, not corpus throughput claims.

| Work | Earlier or full history | New storage | Equivalent output |
| --- | --- | --- | --- |
| One ordinary complete Studio record | JSON: 15.03 s, 12.07 MB | Binary: 4.00 s, 9.06 MB | Exact stage views and joint comparison; same native source summary |
| One connected eight-pass request, same runtime build and input | Full history: 32.22 s, 80.51 MB | Current state: 6.33 s, 1.18 MB | All nine stages exactly equal |

The connected same-build comparison reduced archive size by **98.5%** and worker
time by about **5.1 times** on this drawing. A separate Studio integration run
of the same acquisition took 8.69 seconds in its worker, illustrating normal
timing variation. Earlier initial checks took about 10 seconds.

The decisive difference is visible in the phase timings:

| Connected phase | Full history | Current state |
| --- | ---: | ---: |
| Native settlement rounds | 4.02 s | 3.61 s |
| Save native archive | 15.71 s | 0.15 s |
| Independently restore | 9.60 s | 0.27 s |

Most savings came from retaining and restoring less historical structure.
The native response calculation remains substantial; it has not become a
persistent compact execution engine in this change.

Ordinary binary storage retains the full graph rather than discarding history.
Its import, save and restore phases measured 0.77, 1.49 and 1.41 seconds. An
initial fast-compression option increased compressed file size, so the shipped
ordinary writer uses normal compression instead. The connected checkpoint uses
fast compression because its selected graph is already much smaller.

## Restored native computation

Three retained churches cover the recovered central tower/cross, several
windows, and partitioned overlapping building sections. All **27 visual stages**
match their previous full-history results exactly.

| QuickDraw key | Current archive | Native shape points checked | Native error reads checked |
| --- | ---: | ---: | ---: |
| 6226189452574720 | 1.18 MB | 41 | 161 |
| 6474467716890624 | 2.56 MB | 119 | 335 |
| 5117095295582208 | 2.55 MB | 84 | 385 |

Independent decoding and execution produced **zero difference** from final
view coordinates and error demands in all three. The audit also checks current
graph incidences, source intervals and explicitly unobserved closure origins.
It does not claim to audit historical receipts that were intentionally omitted.

Focused native tests additionally restore a checkpoint, send a fresh request
through an exposed port, and compare its accepted response and generated
geometry with the live machine. Shared bound constructions, distinct coincident
sites, original source aliases and open closure origins survive restoration.

## Integration verification

* 258 Python tests passed.
* 166 QuickDraw solution tests passed, including 31 church and 53 native tests.
* 5,548 tests passed in the linked Gojo solution.
* A newly sampled, game-recognized church completed the ordinary pipeline,
  retained all ten stages and independently verified its binary native record.
* The new reader independently opened a real old JSON archive and reproduced
  its source summary and joint read.
* Studio's isolated Release build opened the new dataset, showed native cards,
  preserved the stage while paging and worked without conversion tools loaded.
* The connected page showed all nine passes, had no script errors or network
  requests, and retained its native checkpoint link and phase measurements.
* Repeating the connected run reused the verified receipt with zero build,
  preparation or native worker time.

The running Studio executable was locked. A separate Release build was tested;
the existing user session was left open. Closing and reopening through
`OpenQuickDraw.cmd` rebuilds the normal application location.

## Reproduction and remaining limits

Raw measurements, hashes and audit results are in
[the machine-readable report](native-runtime-2026-10-30.json).
The paired benchmark commands are implemented by
`tools/benchmark_church_runtime.py` and `tools/benchmark_native_codec.py`.
The linked Gojo revision is `bcecf48d28739bb1877c4bd1ce2f1b31f8eda4b4`.
The old adapter comparison uses `3ed23b13d0e0ce4d65b324c13cd4296756e9c1d3`.

The review is at `D:/_quickDraw/data/native-runtime-2026-10-30/review.html`.
Temporary benchmark graphs, logs and build outputs are confined to
`D:/_quickDraw/_work/native-runtime-2026-10-30`.

Current checkpoints retain executable local numeric state, not the adaptive
controller's complete historical memory. Full-history mode remains available.
The runtime still rebuilds scenes between outer passes, constructs native
preview graphs and retains some redundant adapter evidence. Recognition and
topology proposal selection remain partly conventional code. The next useful
optimization is persistent native execution with reusable read plans and
explicit transient-witness policy; the current result does not establish that
work as complete.
