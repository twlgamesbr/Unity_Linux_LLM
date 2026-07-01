from __future__ import annotations

from dataclasses import dataclass, field
import json
from pathlib import Path
from typing import Any

from .discovery import classify_unity_region
from .records import IndexRecord, RelationRecord


@dataclass(slots=True)
class AssemblyRecord:
    name: str
    path: str
    root_namespace: str = ""
    references: list[str] = field(default_factory=list)
    include_platforms: list[str] = field(default_factory=list)
    exclude_platforms: list[str] = field(default_factory=list)
    auto_referenced: bool | None = None
    optional_unity_references: list[str] = field(default_factory=list)
    raw: dict[str, Any] = field(default_factory=dict)

    @property
    def directory(self) -> str:
        return str(Path(self.path).parent).replace("\\", "/")

    def to_index_record(self, project: str) -> IndexRecord:
        text = f"Assembly {self.name} root namespace {self.root_namespace} references {', '.join(self.references)}"
        payload = {
            "project": project, "path": self.path, "asmdef": self.name,
            "root_namespace": self.root_namespace, "references": self.references,
            "includePlatforms": self.include_platforms, "excludePlatforms": self.exclude_platforms,
            "autoReferenced": self.auto_referenced, "optionalUnityReferences": self.optional_unity_references,
            "unity_region": classify_unity_region(Path(self.path)),
        }
        return IndexRecord("assembly", f"assembly:{self.name}", text, payload)


def parse_asmdefs(root: Path, asmdef_paths: list[Path]) -> tuple[list[AssemblyRecord], list[RelationRecord]]:
    assemblies: list[AssemblyRecord] = []
    relations: list[RelationRecord] = []
    for path in asmdef_paths:
        data = json.loads(path.read_text(encoding="utf-8"))
        rel = path.relative_to(root).as_posix() if path.is_absolute() or str(path).startswith(str(root)) else path.as_posix()
        refs = list(data.get("references") or [])
        asm = AssemblyRecord(
            name=data.get("name", path.stem), path=rel, root_namespace=data.get("rootNamespace", ""), references=refs,
            include_platforms=list(data.get("includePlatforms") or []), exclude_platforms=list(data.get("excludePlatforms") or []),
            auto_referenced=data.get("autoReferenced"), optional_unity_references=list(data.get("optionalUnityReferences") or []), raw=data,
        )
        assemblies.append(asm)
        for ref in refs:
            relations.append(RelationRecord("asmdef-references", asm.name, ref, rel, {"asmdef": asm.name, "unity_region": classify_unity_region(Path(rel))}))
    assemblies.sort(key=lambda a: len(a.directory), reverse=True)
    return assemblies, relations


def resolve_asmdef_for_file(path: Path, assemblies: list[AssemblyRecord]) -> AssemblyRecord | None:
    rel = path.as_posix()
    # If absolute path was supplied, compare against the Assets/ suffix when possible.
    if "/Assets/" in rel:
        rel = rel.split("/Assets/", 1)[1]
        rel = "Assets/" + rel
    for asm in assemblies:
        prefix = asm.directory.rstrip("/") + "/"
        if rel.startswith(prefix):
            return asm
    return None
