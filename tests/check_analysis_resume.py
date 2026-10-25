"""Explicit real-data interruption audit; not part of automatic unit discovery.

Run with a generated source collection and a new QA output. This deliberately
terminates only its own analysis worker tree after durable record checkpoints.
"""
import argparse
import hashlib
import json
import os
import signal
from pathlib import Path
import subprocess
import sys
import time


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def committed(output):
    rows = []
    for index in output.rglob("index.ndjson"):
        for line in index.read_bytes().splitlines(keepends=True):
            if line.endswith(b"\n") and line.strip():
                rows.append((index.parent, json.loads(line)))
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--limit", type=int, default=20)
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists():
        raise ValueError("Use a new interruption-audit output")
    repository = Path(__file__).resolve().parents[1]
    source = args.dataset.resolve()
    source_hashes = {str(p): digest(p) for p in source.rglob("*") if p.name in ("manifest.json", "index.ndjson")}
    command = [sys.executable, "-u", "-m", "gojo_quickdraw", "analyze", "--dataset", str(source), "--output", str(output), "--limit", str(args.limit), "--workers", "2"]
    output.parent.mkdir(parents=True, exist_ok=True)
    stdout = output.parent / (output.name + "-interrupted.stdout")
    stderr = output.parent / (output.name + "-interrupted.stderr")
    with stdout.open("wb") as out, stderr.open("wb") as err:
        child = subprocess.Popen(command, cwd=repository, stdout=out, stderr=err,
                                 creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
                                 start_new_session=os.name != "nt")
        deadline = time.monotonic() + 180
        saved = []
        while child.poll() is None and time.monotonic() < deadline:
            saved = [(folder, row) for folder, row in committed(output) if row["status"] == "ok"] if output.exists() else []
            if len(saved) >= 4:
                break
            time.sleep(.1)
        if child.poll() is not None or len(saved) < 4:
            if child.poll() is None:
                if os.name == "nt":
                    subprocess.run(["taskkill", "/PID", str(child.pid), "/T", "/F"], capture_output=True)
                else:
                    os.killpg(child.pid, signal.SIGTERM)
                child.wait(timeout=20)
            raise RuntimeError("Analysis did not reach an interruptible checkpoint: " + stderr.read_text(errors="replace"))
        if os.name == "nt":
            subprocess.run(["taskkill", "/PID", str(child.pid), "/T", "/F"], check=True, capture_output=True)
        else:
            os.killpg(child.pid, signal.SIGTERM)
        child.wait(timeout=20)
    checkpoints = {str(folder / row[field]): digest(folder / row[field]) for folder, row in saved for field in ("nativePath", "evidencePath")}
    completed = subprocess.run(command, cwd=repository, capture_output=True, text=True, encoding="utf-8", check=True)
    result = json.loads(completed.stdout)
    assert result["storedRecords"] == args.limit
    assert result["resumed"] >= len(saved)
    assert all(digest(path) == expected for path, expected in checkpoints.items())
    assert all(digest(path) == expected for path, expected in source_hashes.items())
    rows = committed(output)
    assert len({row["itemId"] for _, row in rows if row["status"] == "ok"}) == args.limit
    report = {"source": str(source), "output": str(output), "checkpointedBeforeStop": len(saved), "completedRecordsPreserved": True,
              "sourceJournalsUnchanged": True, "result": result}
    report_path = output.parent / (output.name + "-resume-report.json")
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
