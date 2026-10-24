"""Resolve and fingerprint the linked implementation, without copying it."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
_loaded_root = None


def resolve(value=None):
    path = Path(value or os.environ.get("GOJO_ROOT") or ROOT.parent / "Gojo").resolve()
    required = path / "experiments/quickDraw/QuickDraw.csproj"
    if not required.is_file():
        raise ValueError(f"Gojo checkout not found at {path}; set --gojo-root or GOJO_ROOT.")
    return path


def activate(value=None):
    global _loaded_root
    path = resolve(value)
    if _loaded_root is not None and _loaded_root != path:
        raise ValueError("Use a fresh process when changing the linked Gojo checkout.")
    directory = str(path / "experiments/quickDraw/tools")
    if directory not in sys.path:
        sys.path.insert(0, directory)
    _loaded_root = path
    return path


def fingerprint(path):
    path = resolve(path)
    revision = subprocess.run(["git", "-C", str(path), "rev-parse", "HEAD"],
                              check=True, capture_output=True, text=True).stdout.strip()
    changes = subprocess.run(["git", "-C", str(path), "status", "--porcelain"],
                             check=True, capture_output=True, text=True).stdout.splitlines()
    digest = hashlib.sha256()
    files = []
    for directory, folders, names in os.walk(path):
        folders[:] = [name for name in folders if name not in {".git", "bin", "obj", "__pycache__", "node_modules"}]
        files.extend(Path(directory) / name for name in names
                     if Path(name).suffix in (".cs", ".csproj", ".py", ".props", ".targets"))
    files.sort()
    for file in files:
        digest.update(file.relative_to(path).as_posix().encode())
        digest.update(b"\0")
        digest.update(file.read_bytes())
    pinned = json.loads((ROOT / "gojo.lock.json").read_text(encoding="utf-8"))["commit"]
    return {"repository": "https://github.com/It3rate/Gojo.git", "revision": revision,
            "pinnedRevision": pinned, "matchesPinnedRevision": revision == pinned,
            "sourceSha256": digest.hexdigest(), "dirtyPaths": changes,
            "qualification": "Source-content fingerprint includes C#, project and Python sources; build artifacts are excluded."}
