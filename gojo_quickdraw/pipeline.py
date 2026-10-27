"""Bounded end-to-end workspace: acquire once, retain every interpretation stage."""
from __future__ import annotations

from datetime import datetime, timezone
import json
from pathlib import Path
import secrets
import sys

from . import analysis, dataset, sampling
from .selection import selected_rows

SCHEMA = "gojo.quickdraw-pipeline.v1"


def run(output, gojo_root, *, input_path=None, source_dataset=None, count=25, categories=None,
        seed=None, workers=4, recursive=False, retry_errors=False, fit_config=None, max_points=20000, max_spans=256):
    if (input_path is None) == (source_dataset is None):
        raise ValueError("Choose raw source files or an existing dataset")
    if isinstance(count, bool) or not isinstance(count, int) or count < 1:
        raise ValueError("A positive drawing count is required; full pipeline runs never imply all records")
    if isinstance(workers, bool) or not isinstance(workers, int) or not 1 <= workers <= 8:
        raise ValueError("Choose 1..8 workers")
    output = Path(output).resolve()
    source = Path(input_path if input_path is not None else source_dataset).resolve()
    if not source.exists():
        raise ValueError("Source folder or file does not exist")
    source_scope = source.parent if source.is_file() else source
    if output == source_scope or output in source_scope.parents or source_scope in output.parents:
        raise ValueError("Choose a pipeline output separate from the source")
    raw = input_path is not None
    request = {"source": str(source), "mode": "raw" if raw else "existing", "count": count,
               "categories": sorted(categories or []), "recursive": recursive,
               "fitConfig": fit_config, "maxPoints": max_points, "maxSpans": max_spans}
    manifest_path = output / "pipeline.json"
    with dataset._writer_lock(output):
        if manifest_path.exists():
            state = json.loads(manifest_path.read_text(encoding="utf-8"))
            if state.get("schema") != SCHEMA:
                raise ValueError("Unsupported pipeline workspace format")
            if state.get('request', {}).get('mode') != request['mode']:
                raise ValueError('Use this folder with its original input mode, or choose New dataset to switch between raw input and an analyzed copy.')
            if state.get('request') != request or seed is not None and state.get('seed') != seed:
                state.setdefault('requestHistory', []).append({'request': state['request'], 'seed': state['seed'],
                    'status': state.get('status'), 'retainedUtc': datetime.now(timezone.utc).isoformat()})
                state['request'] = request
                if seed is not None:
                    state['seed'] = seed
        else:
            if any(p.name != ".convert.lock" for p in output.iterdir()):
                raise ValueError("Choose a new dataset folder. For existing converted drawings, use Complete pipeline in the viewer.")
            state = {"schema": SCHEMA, "request": request, "seed": seed if seed is not None else secrets.randbits(63),
                     "baseline": "acquired" if raw else str(source), "results": "results", "createdUtc": datetime.now(timezone.utc).isoformat()}
        state["status"] = "acquiring" if raw else "analyzing"
        dataset._atomic_json(manifest_path, state)
        baseline = output / "acquired" if raw else source
        acquisition = None
        keys = None
        if raw:
            print(f"Pipeline: grow selected categories to {count} drawings, keeping existing records", file=sys.stderr, flush=True)
            acquisition = sampling.convert_collection(source, baseline, gojo_root, categories=categories, count=count,
                seed=state["seed"], recursive=recursive, retry_errors=retry_errors, fit_config=fit_config,
                max_points=max_points, max_spans=max_spans, target_total=True, append=True)
            # Growing a collection preserves its exact earlier acquisitions and
            # completes pending interpretations before accepting another target.
            scope = {name.casefold() for name in acquisition['sourceCategories']}
            keys = {row['keyId'] for _, row in selected_rows(baseline) if row.get('category', '').casefold() in scope} if acquisition['storedRecords'] else set()
        state["status"] = "analyzing"
        dataset._atomic_json(manifest_path, state)
        print("Pipeline: saving source, fitted outlines, construction stages, shape areas and refinement", file=sys.stderr, flush=True)
        if raw and not keys:
            from .growth import existing
            saved = existing(output / 'results')
            result = {"converted": 0, "resumed": 0, "errors": 0,
                      "storedRecords": sum(row['status'] == 'ok' for row in saved.values()),
                      "storedErrors": sum(row['status'] == 'error' for row in saved.values())}
        else:
            result = analysis.run(baseline, output / "results", gojo_root, limit=count, workers=workers,
                                  retry_errors=retry_errors, key_ids=sorted(keys) if keys is not None else None, execute_stages=True,
                                  append=True, categories=acquisition['sourceCategories'] if raw else None)
        result["output"] = str(output)
        result["seed"] = state["seed"]
        result['targetTotal'] = count
        if acquisition:
            result["acquisition"] = acquisition
            result["errors"] = result.get("errors", 0) + acquisition["errors"]
            result["storedErrors"] += acquisition["storedErrors"]
        result["allStagesSaved"] = not result.get("errors")
        state["status"] = "retained-errors" if result.get("errors") else "completed"
        state["lastRun"] = result
        dataset._atomic_json(manifest_path, state)
        return result
