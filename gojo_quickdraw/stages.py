"""Explicit, disposable interpretation runs over a cached evidence dataset.

Baseline evidence is never rewritten. Later stages are authored exploratory
church constructions, not learned truth or additional independent samples.
"""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import sys
import tempfile


def _script(script: Path, *arguments: str) -> None:
    result = subprocess.run([sys.executable, str(script), *map(str, arguments)],
                            capture_output=True, text=True, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(f"{script.name} failed:\n{result.stderr or result.stdout}")


def _curve_extent(controls: list) -> list:
    """Read the actual line/quadratic/cubic extent, not its control polygon."""
    if not 1 <= len(controls) <= 4:
        raise ValueError("Extent diagnostics need native linear, quadratic, or cubic profiles")
    parameters = {0.0, 1.0}
    for axis in (0, 1):
        values = [point[axis] for point in controls]
        roots = []
        if len(values) == 3:
            denominator = values[0] - 2 * values[1] + values[2]
            if abs(denominator) > 1e-12:
                roots = [(values[0] - values[1]) / denominator]
        elif len(values) == 4:
            a = -values[0] + 3 * values[1] - 3 * values[2] + values[3]
            b = 2 * (values[0] - 2 * values[1] + values[2])
            c = values[1] - values[0]
            if abs(a) <= 1e-12:
                if abs(b) > 1e-12:
                    roots = [-c / b]
            elif b*b - 4*a*c >= 0:
                discriminant = math.sqrt(b*b - 4*a*c)
                roots = [(-b + discriminant) / (2*a), (-b - discriminant) / (2*a)]
        parameters.update(root for root in roots if 0 < root < 1)
    samples = []
    for t in parameters:
        points = controls
        while len(points) > 1:
            points = [[(1-t)*a[j] + t*b[j] for j in (0, 1)] for a, b in zip(points, points[1:])]
        samples.append(points[0])
    return [min(p[0] for p in samples), min(p[1] for p in samples),
            max(p[0] for p in samples), max(p[1] for p in samples)]


def _diagnose_review(review: Path, destination: Path) -> dict:
    """Inspect captured native output and add honest run-level qualifications."""
    html = review.read_text(encoding="utf-8")
    match = re.search(r'(<script id="review-data" type="application/json">)(.*?)(</script>)', html, re.S)
    if match is None:
        raise ValueError("Generated native review has no captured data for diagnostics")
    data = json.loads(match.group(2))
    rows, warnings = [], []
    for source in data["sources"]:
        construction = source["construction"]
        final = construction["rungs"][-1]
        by_id = {edge["id"]: edge for edge in construction["analysis"]["edges"]}
        extents = [_curve_extent(by_id[key]["controls"]) for key in final["visibleEdgeIds"]]
        bounds = ([min(b[0] for b in extents), min(b[1] for b in extents),
                   max(b[2] for b in extents), max(b[3] for b in extents)] if extents else None)
        overflow = ({"left": max(0, -bounds[0]), "top": max(0, -bounds[1]),
                     "right": max(0, bounds[2]-512), "bottom": max(0, bounds[3]-512)}
                    if bounds else {side: 0 for side in ("left", "top", "right", "bottom")})
        outside = any(value > 1e-8 for value in overflow.values())
        metrics = source["passes"][-1].get("metrics", {})
        reported = {key: value for key, value in metrics.items()
                    if key in ("rms", "max", "sourceRms", "proposalRms", "sourceMax", "proposalMax",
                               "sourceFitRms", "sourceFitMax", "proposalFitRms", "previousPassRms")}
        rows.append({"sourceId": source["id"], "programId": construction["id"],
                     "finalRungId": final["id"], "observerViewBox": [0, 0, 512, 512],
                     "finalNativeExtent": bounds, "visibleEdgeCount": len(final["visibleEdgeIds"]),
                     "extentStatus": "review-needed" if outside else "within-observer-frame",
                     "outOfFrame": outside, "overflow": overflow,
                     "reportedFitMetrics": reported, "qualityStatus": "authored-requires-review"})
        if outside:
            warnings.append({"sourceId": source["id"], "kind": "outside-observer-frame",
                             "status": "review-needed", "message": "Final native geometry extends outside the viewing frame and may be clipped."})
    diagnostics = {"schema": "gojo.quickdraw-stage-diagnostics.v1", "sources": rows, "warnings": warnings,
                   "qualityQualification": "Authored exploratory priors require visual review. Valid native execution and a small measured error do not establish conceptual correctness; these outputs are not ground-truth training targets.",
                   "extentQualification": "Actual finite native profiles, including Bezier extrema, in the normalized observer frame. Stroke width and faint carried detail are excluded.",
                   "metricQualification": "Fit displacement is reported by the existing proposal author, not recomputed from native rounded geometry. Deliberate source drift is diagnostic and does not reject the run."}
    note = " Authored interpretations require review and are not ground-truth training targets."
    if warnings:
        note += " Review needed: final geometry for " + ", ".join(row["sourceId"] for row in warnings) + " extends outside the viewing frame and may be clipped."
    data["introduction"] = data.get("introduction", "") + note
    data["qualityDiagnostics"] = diagnostics
    encoded = json.dumps(data, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    encoded = encoded.replace("<", "\\u003c").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    html = html[:match.start(2)] + encoded + html[match.end(2):]
    html = html.replace("Five source drawings", "Source drawings")
    review.write_text(html, encoding="utf-8")
    destination.write_text(json.dumps(diagnostics, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return diagnostics


def generate(dataset: Path, output: Path, gojo_root: Path,
             key_id: str | None = None, limit: int | None = None) -> dict:
    """Generate and verify selected seven-stage church interpretations on demand.

    Require an explicit key or limit; never synthesize an entire corpus by
    accident. A completed run is moved into an empty destination only after
    both native execution and the portable inspector validation succeed.
    """
    from . import bridge, upstream
    from .dataset import load_records

    if key_id is None and limit is None:
        raise ValueError("Select a drawing with key_id or set an explicit limit")
    if limit is not None and (isinstance(limit, bool) or not isinstance(limit, int) or limit < 1):
        raise ValueError("limit must be a positive integer")
    dataset, output, gojo_root = (Path(p).resolve() for p in (dataset, output, gojo_root))
    if output == dataset or output in dataset.parents or dataset in output.parents:
        raise ValueError("Stage output must be separate from the baseline dataset")
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError("Stage output must be a new or empty directory")
    selected = list(load_records(dataset, key_id=key_id, limit=limit))
    if not selected:
        raise ValueError("No matching successfully encoded drawings")
    unsupported = sorted({str(row.get("category", packet.get("source", {}).get("word", "")))
                          for row, packet in selected
                          if str(row.get("category", packet.get("source", {}).get("word", ""))).lower() != "church"})
    if unsupported:
        raise ValueError("Later-stage priors currently support church only; selected: " + ", ".join(unsupported))
    empty = [str(row["keyId"]) for row, packet in selected
             if packet["observation"].get("emptyRead") or not packet["observation"].get("spans")]
    if empty:
        raise ValueError("Selected drawings have no fitted intervals to interpret: " + ", ".join(empty))
    ids = [str(packet["observation"]["sourceId"]) for _, packet in selected]
    if len(set(ids)) != len(ids):
        raise ValueError("A stage run needs unique source identities")
    tools = gojo_root / "experiments" / "quickDraw" / "tools"
    author, inspector = tools / "quickdraw_gestalt.py", tools / "inspect_quickdraw_gestalt.py"
    for script in (author, inspector):
        if not script.is_file():
            raise FileNotFoundError(f"Required linked Gojo tool is missing: {script}")
    implementation = upstream.fingerprint(gojo_root)
    output.parent.mkdir(parents=True, exist_ok=True)
    # The temporary parent is on the destination filesystem for atomic rename.
    with tempfile.TemporaryDirectory(prefix=".quickdraw-stages-", dir=output.parent) as temporary:
        scratch = Path(temporary)
        inputs, run = scratch / "input", scratch / "run"
        inputs.mkdir()
        observations, originals = inputs / "observations.ndjson", inputs / "selected.ndjson"
        observations.write_text("".join(json.dumps(packet["observation"], ensure_ascii=False,
                                                   separators=(",", ":")) + "\n" for _, packet in selected), encoding="utf-8")
        originals.write_text("".join(packet["sourceOriginalJson"].rstrip("\r\n") + "\n"
                                     for _, packet in selected), encoding="utf-8")
        _script(author, "--synthesize", "--observations", observations, "--source", originals, "--output", run)
        construction = bridge.run(gojo_root, "construct", run / "programs.ndjson", run / "native")
        evidence = bridge.run(gojo_root, "gestalt-evidence", run / "packets.ndjson", run / "native-evidence")
        _script(inspector, "--observations", run / "observations.ndjson", "--manifest", run / "manifest.json",
                "--native", run / "native", "--output", run / "review" / "index.html", "--portable")
        diagnostics = _diagnose_review(run / "review" / "index.html", run / "diagnostics.json")
        manifest = json.loads((run / "manifest.json").read_text(encoding="utf-8"))
        provenance = {
            "schema": "gojo.quickdraw-dataset-stage-run.v1",
            "implementation": implementation,
            "baselineManifestSha256": hashlib.sha256((dataset / "manifest.json").read_bytes()).hexdigest(),
            "selected": [{"itemId": row["itemId"], "keyId": row["keyId"],
                          "evidenceSha256": row.get("evidenceSha256"),
                          "sourceOriginalSha256": hashlib.sha256(packet["sourceOriginalJson"].encode("utf-8")).hexdigest()}
                         for row, packet in selected],
            "authorSha256": hashlib.sha256(author.read_bytes()).hexdigest(),
            "runnerSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            "qualification": "On-demand authored interpretations of cached observations; original records remain unchanged. These stages are correlated alternatives, not new independent training samples.",
            "nativeConstruction": {**construction, "output": "native",
                                   "message": construction.get("message", "").replace(str(run), ".")},
            "nativeEvidence": {**evidence, "output": "native-evidence",
                               "message": evidence.get("message", "").replace(str(run), ".")},
        }
        (run / "dataset-provenance.json").write_text(json.dumps(provenance, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        if output.exists():
            output.rmdir()  # Only the checked, empty destination is removed.
        run.rename(output)
    return {"drawings": len(selected), "passes": sum(len(row["passes"]) for row in manifest["sources"]),
            "output": str(output), "review": str(output / "review" / "index.html"),
            "warnings": diagnostics["warnings"],
            "qualification": "Authored church priors; no model has been trained"}
