# Gojo QuickDraw agent instructions

Read the canonical prompt at `../Gojo/Gojo/docs/GojoSystemPrompt.md` completely
before planning, inspecting, or changing this project. `--gojo-root` can select
another checkout; use its canonical prompt instead. The upstream structure is
the source of meaning. Python packets, names, and labels are adapters.

Keep exact acquisition, measured reads, seeded hypotheses, interventions, and
learned support distinct. Nearby or equal coordinates do not establish identity.
Do not count several annotations or interpretation stages as independent data.
Do not claim physical simulation, learned priors, or semantic recognition where
only authored candidates or finite geometric tests are implemented.

Desktop conversion runs the bounded `process` pipeline, retaining acquisition,
fitted evidence, native construction rungs, area proposals and refinement.
The CLI `sample`/`convert` commands still create baseline evidence; `analyze`
and `stages` remain available independently. Perturbation probes are optional.
Pipeline datasets can grow: the count is a target across selected categories,
existing records count toward it, and lowering it never deletes data. Preserve
completed artifacts across additions and keep each processing version inspectable.
Never silently process the full QuickDraw corpus during development. Keep raw
data and generated datasets outside this repository. Put temporary runs, QA
folders, screenshots and logs under `D:/_quickDraw/_work`, never loose in
`D:/_quickDraw` or the repository root. Persistent user datasets normally live
under `D:/_quickDraw/data`. Do not move existing user data to tidy a workspace.

`train` is a separate bounded, read-only local construction experiment. Keep
controlled targets, weak authored correspondences and unlabelled inference
distinct. Split by original acquisition/template before variations; never use
held-out test labels to select weights. Native coarse reads and connected
candidates underlie exported feature cells. Learned opinions do not currently
alter conversion or establish source contact. See `docs/local-joint-model.md`.

Work on `master` and push completed verified checkpoints to `It3rate/GojoQuickDraw`.
Do not publish the website without the user's milestone authorization.

Run `python -m unittest discover -s tests`, the native test project, a small
real-data conversion/restore check, and `dotnet test Gojo.slnx` in the linked
Gojo checkout for substantive changes. Keep the dependency revision and source
fingerprints inspectable. Do not modify or reset unrelated upstream work.
