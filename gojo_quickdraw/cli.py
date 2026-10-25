"""Public batch and on-demand entry points."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

from . import dataset, upstream


def parser():
    p = argparse.ArgumentParser(description="Convert QuickDraw files into native Gojo evidence; derive interpretations on demand.")
    p.add_argument("--gojo-root", type=Path, help="Linked Gojo checkout (default sibling Gojo or GOJO_ROOT)")
    commands = p.add_subparsers(dest="command", required=True)
    convert = commands.add_parser("convert", help="Stream a file or folder into resumable fitted evidence")
    convert.add_argument("--input", type=Path, required=True)
    convert.add_argument("--output", type=Path, required=True)
    convert.add_argument("--category", action="append", help="Filter source prompt categories; repeat for several")
    convert.add_argument("--limit", type=int, help="Maximum selected source records this invocation; omitted processes all")
    convert.add_argument("--recursive", action="store_true")
    convert.add_argument("--fit-tolerance", type=float, default=6.0)
    convert.add_argument("--max-points", type=int, default=20000)
    convert.add_argument("--max-spans", type=int, default=256)
    convert.add_argument("--retry-errors", action="store_true")
    sample = commands.add_parser("sample", help="Randomly sample category files into category subfolders")
    sample.add_argument("--input", type=Path, required=True)
    sample.add_argument("--output", type=Path, required=True)
    sample.add_argument("--category", action="append")
    sample.add_argument("--limit", type=int, default=25, help="Total sample across selected categories")
    sample.add_argument("--all-records", action="store_true")
    sample.add_argument("--seed", type=int, help="Explicit seed; interrupted selections otherwise resume automatically")
    sample.add_argument("--index-cache", type=Path)
    sample.add_argument("--recursive", action="store_true")
    sample.add_argument("--fit-tolerance", type=float, default=6.0)
    sample.add_argument("--max-points", type=int, default=20000)
    sample.add_argument("--max-spans", type=int, default=256)
    sample.add_argument("--retry-errors", action="store_true")
    check = commands.add_parser("verify", help="Check content hashes and independently restore native evidence")
    check.add_argument("--dataset", type=Path, required=True)
    check.add_argument("--limit", type=int)
    review = commands.add_parser("review", help="Build an offline evidence inspector with pages for every drawing")
    review.add_argument("--dataset", type=Path, required=True)
    review.add_argument("--output", type=Path, required=True)
    review.add_argument("--limit", type=int, default=50, help="Drawings per page (all records remain accessible)")
    review.add_argument("--sample-only", action="store_true", help="Retain the older single-page limited sample")
    stages = commands.add_parser("stages", help="Generate church interpretation and synthesis stages on demand")
    stages.add_argument("--dataset", type=Path, required=True)
    stages.add_argument("--output", type=Path, required=True)
    selection = stages.add_mutually_exclusive_group(required=True)
    selection.add_argument("--key-id")
    selection.add_argument("--limit", type=int)
    catalog = commands.add_parser("catalog", help="Discover generated category folders and select lightweight drawing metadata")
    catalog.add_argument("--dataset", type=Path, required=True)
    catalog.add_argument("--limit", type=int, default=1000)
    catalog.add_argument("--offset", type=int, default=0)
    analysis = commands.add_parser("analyze", help="Run full authored analysis over an existing generated collection")
    analysis.add_argument("--dataset", type=Path, required=True)
    analysis.add_argument("--output", type=Path, required=True)
    analysis.add_argument("--limit", type=int, help="Omitted deliberately processes every converted record in scope")
    analysis.add_argument("--workers", type=int, default=4)
    analysis.add_argument("--retry-errors", action="store_true")
    for command in (check, review, stages, catalog):
        command.add_argument("--order", choices=("category", "recent", "random"), default="category")
        command.add_argument("--seed", type=int, default=0)
    probes = commands.add_parser("probe", help="Run declared construction perturbations without changing baseline evidence")
    probes.add_argument("--dataset", type=Path)
    probes.add_argument("--key-id")
    probes.add_argument("--example", action="store_true", help="Use an authored two-window/door intervention fixture")
    probes.add_argument("--output", type=Path, required=True)
    commands.add_parser("doctor", help="Check linked revision and build the native bridge")
    return p


def main(argv=None):
    args = parser().parse_args(argv)
    try:
        root = upstream.activate(args.gojo_root)
        if args.command == "convert":
            result = dataset.convert(args.input, args.output, root, limit=args.limit, categories=args.category,
                                     recursive=args.recursive, fit_config={"polylineTolerance": args.fit_tolerance},
                                     max_points=args.max_points, max_spans=args.max_spans, retry_errors=args.retry_errors)
        elif args.command == "sample":
            from .sampling import convert_collection
            result = convert_collection(args.input, args.output, root, categories=args.category, count=args.limit,
                                        seed=args.seed, all_records=args.all_records, recursive=args.recursive, cache=args.index_cache,
                                        fit_config={"polylineTolerance": args.fit_tolerance}, max_points=args.max_points,
                                        max_spans=args.max_spans, retry_errors=args.retry_errors)
        elif args.command == "verify":
            result = dataset.verify(args.dataset, root, args.limit, order=args.order, seed=args.seed)
        elif args.command == "catalog":
            from .selection import catalog
            result = catalog(args.dataset, args.limit, order=args.order, seed=args.seed, offset=args.offset)
        elif args.command == "review":
            from .review import build
            result = build(args.dataset, args.output, args.limit, order=args.order, seed=args.seed, paged=not args.sample_only)
        elif args.command == "analyze":
            from .analysis import run
            result = run(args.dataset, args.output, root, limit=args.limit, workers=args.workers, retry_errors=args.retry_errors)
        elif args.command == "stages":
            from .stages import generate
            result = generate(args.dataset, args.output, root, key_id=args.key_id, limit=args.limit, order=args.order, seed=args.seed)
        elif args.command == "probe":
            from .probes import run_probes, example_context
            if args.example:
                if args.dataset or args.key_id:
                    raise ValueError("Choose the example or a dataset source")
                result = run_probes(example_context())
            else:
                if not args.dataset or not args.key_id:
                    raise ValueError("Provide --dataset and --key-id, or --example")
                records = list(dataset.load_records(args.dataset, key_id=args.key_id))
                if len(records) != 1:
                    raise ValueError("Select one unambiguous source key for a probe")
                row, packet = records[0]
                result = run_probes(packet["context"])
                result["provenance"] = {"itemId": row["itemId"], "sourceSha256": row["sourceSha256"],
                                        "baselineNativeSha256": row["nativeSha256"]}
                if args.output.resolve().is_relative_to(args.dataset.resolve()):
                    raise ValueError("Probe output must be outside the baseline dataset")
            if args.output.exists():
                raise ValueError("Choose a new probe output to retain earlier results")
            args.output.parent.mkdir(parents=True, exist_ok=True)
            dataset._atomic_json(args.output, result)
        else:
            from .bridge import executable
            result = {"gojoRoot": str(root), "upstream": upstream.fingerprint(root), "native": executable(root)}
        print(json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False))
        return 1 if args.command in ("convert", "sample", "analyze") and result.get("errors") else 0
    except (ValueError, RuntimeError, OSError, KeyError) as error:
        print(f"Gojo QuickDraw: {error}", file=sys.stderr)
        return 2
