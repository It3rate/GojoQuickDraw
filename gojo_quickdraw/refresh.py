"""Refresh both stable review entry points after a selected batch completes.

This is an explicit read of saved evidence, not a filesystem watcher or another
interpretation pass. Opening an existing browser tab still requires refresh.
"""
from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path

from . import gallery, review
from .dataset import _atomic_json


def run(dataset: Path, output: Path | None = None, *, review_page_size=50,
        gallery_page_size=12, order="category", seed=0) -> dict:
    """Rebuild the inspector before its linked gallery, for this folder only."""
    dataset = Path(dataset).resolve()
    output = Path(output).resolve() if output is not None else Path(str(dataset) + "-runs") / "inspector"
    if output == dataset or dataset in output.parents:
        raise ValueError("Review refresh output must be outside the selected dataset")
    for value in (review_page_size, gallery_page_size):
        if isinstance(value, bool) or not isinstance(value, int) or value < 1:
            raise ValueError("Review and gallery page sizes must be positive integers")
    stamps = review.collection_stamp(dataset)
    inspector = output / "evidence.html"
    comparison = output / "comparison.html"
    detailed = review.build(dataset, inspector, review_page_size, order=order, seed=seed, paged=True)
    if review.collection_stamp(dataset) != stamps:
        raise ValueError("The collection changed during review refresh; retry after processing finishes")
    compared = gallery.build(dataset, comparison, gallery_page_size, order=order, seed=seed, inspector=inspector)
    if review.collection_stamp(dataset) != stamps:
        raise ValueError("The collection changed during review refresh; retry after processing finishes")
    result = {"schema": "gojo.quickdraw-review-refresh.v1", "dataset": str(dataset),
              "output": str(output), "drawings": detailed["drawings"],
              "inspector": str(inspector), "comparison": str(comparison),
              "review": detailed, "gallery": compared, "journals": stamps,
              "updatedUtc": datetime.now(timezone.utc).isoformat(),
              "qualification": "Views rebuilt from saved evidence after processing. This does not rerun analysis or reload already open browser tabs."}
    _atomic_json(output / "refresh.json", result)
    return result
