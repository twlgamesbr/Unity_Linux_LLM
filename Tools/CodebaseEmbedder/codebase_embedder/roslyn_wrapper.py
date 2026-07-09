from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from typing import Any

from .records import IndexRecord, RelationRecord

PARSER_PATH = Path(__file__).parent.parent / "roslyn_parser"
PARSER_DLL = PARSER_PATH / "bin" / "Debug" / "net10.0" / "CodebaseRoslynParser.dll"


def _find_dotnet_executable() -> str:
    return shutil.which("dotnet") or "dotnet"


def _build_roslyn_parser() -> None:
    dotnet = _find_dotnet_executable()
    if not shutil.which(dotnet):
        raise FileNotFoundError("dotnet SDK not found on PATH")
    proc = subprocess.run([dotnet, "build", str(PARSER_PATH), "--configuration", "Debug"], capture_output=True, text=True, check=False)
    if proc.returncode != 0:
        raise RuntimeError(f"Roslyn parser build failed: {proc.stderr.strip()}\n{proc.stdout.strip()}")


def parse_csharp_files_with_roslyn(root: Path, csharp_paths: list[Path]) -> tuple[list[IndexRecord], list[RelationRecord]]:
    if not PARSER_PATH.exists():
        raise FileNotFoundError(f"Roslyn parser project not found at {PARSER_PATH}")

    dotnet = _find_dotnet_executable()
    if not shutil.which(dotnet):
        raise FileNotFoundError("dotnet SDK not found on PATH")

    if not PARSER_DLL.exists():
        _build_roslyn_parser()

    command = [dotnet, "exec", str(PARSER_DLL), str(root)] + [str(p) for p in csharp_paths]
    proc = subprocess.run(command, capture_output=True, text=True, check=False)
    if proc.returncode != 0:
        raise RuntimeError(f"Roslyn parser failed: {proc.stderr.strip()}\n{proc.stdout.strip()}")

    payload = json.loads(proc.stdout)
    records: list[IndexRecord] = []
    relations: list[RelationRecord] = []
    def field(obj: dict[str, Any], *names: str, default: Any = None) -> Any:
        for name in names:
            if name in obj and obj[name] is not None:
                return obj[name]
        return default

    for obj in payload:
        record_type = field(obj, "record_type", "recordType", default="")
        if record_type == "relation":
            relations.append(
                RelationRecord(
                    field(obj, "relation_kind", "relationKind", default=""),
                    field(obj, "source", default=""),
                    field(obj, "target", default=""),
                    field(obj, "path", default=""),
                    field(obj, "payload", default={}),
                )
            )
        else:
            records.append(
                IndexRecord(
                    str(record_type),
                    str(field(obj, "stable_key", "stableKey", default="")),
                    str(field(obj, "text", default="")),
                    field(obj, "payload", default={}),
                    field(obj, "point_id", "pointId", default=None),
                )
            )
    return records, relations
