"""One long-lived native encoder, plus explicit on-demand native execution."""
from __future__ import annotations

import json
from pathlib import Path
import subprocess
import tempfile

from .upstream import ROOT, resolve

_built = {}


def executable(gojo_root):
    gojo_root = resolve(gojo_root)
    key = str(gojo_root)
    if key not in _built:
        project = ROOT / "native/GojoQuickDraw.Native.csproj"
        result = subprocess.run(["dotnet", "build", str(project), "-c", "Release", "--verbosity", "quiet",
                                 f"-p:GojoRoot={gojo_root}"], capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError("Native bridge build failed:\n" + result.stdout[-6000:] + result.stderr[-3000:])
        assembly = ROOT / "native/bin/Release/net10.0/GojoQuickDraw.Native.dll"
        if not assembly.is_file():
            raise RuntimeError(f"Native bridge assembly is missing: {assembly}")
        _built[key] = ["dotnet", str(assembly)]
    return _built[key]


class Encoder:
    def __init__(self, gojo_root, output):
        self.stderr = tempfile.TemporaryFile(mode="w+", encoding="utf-8")
        self.process = subprocess.Popen([*executable(gojo_root), "encode", "--output", str(output)],
                                        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=self.stderr,
                                        text=True, encoding="utf-8", bufsize=1)

    def encode(self, packet):
        self.process.stdin.write(json.dumps(packet, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n")
        self.process.stdin.flush()
        response = self.process.stdout.readline()
        if not response:
            self.stderr.seek(0)
            raise RuntimeError("Native encoder stopped unexpectedly: " + self.stderr.read()[-3000:])
        result = json.loads(response)
        if not result.get("ok"):
            raise ValueError(result.get("error", "Native evidence rejected"))
        if result.get("itemKey") != packet["itemKey"]:
            raise RuntimeError("Native response does not match the submitted item.")
        return result

    def close(self):
        if self.process.stdin:
            self.process.stdin.close()
        try:
            self.process.wait(timeout=30)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait()
        self.process.stdout.close()
        self.stderr.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()


def inspect(gojo_root, archive):
    result = subprocess.run([*executable(gojo_root), "inspect", "--input", str(archive)],
                            capture_output=True, text=True, encoding="utf-8")
    if result.returncode:
        raise ValueError("Native restore failed: " + result.stdout[-1500:] + result.stderr[-1500:])
    value = json.loads(result.stdout)
    if value.get("ok") is False:
        raise ValueError(value.get("error", "Native restore failed"))
    return value


def run(gojo_root: Path, command: str, input_path: Path, output: Path):
    if command not in ("construct", "gestalt-evidence"):
        raise ValueError("Unsupported on-demand native command")
    result = subprocess.run([*executable(gojo_root), command, "--input", str(input_path), "--output", str(output)],
                            capture_output=True, text=True, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(f"Native {command} failed:\n{result.stdout[-3000:]}\n{result.stderr[-3000:]}")
    return {"command": command, "output": str(output), "message": result.stdout.strip()}
