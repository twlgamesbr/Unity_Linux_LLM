from __future__ import annotations

import json
from pathlib import Path

from .records import IndexRecord


def parse_package_manifest(root: Path, project: str = "Unity_Linux_LLM") -> IndexRecord:
    manifest = root / "Packages/manifest.json"
    deps = {}
    if manifest.exists():
        deps = json.loads(manifest.read_text(encoding="utf-8")).get("dependencies", {})
    lock_path = root / "Packages/packages-lock.json"
    lock = {}
    if lock_path.exists():
        try:
            lock = json.loads(lock_path.read_text(encoding="utf-8")).get("dependencies", {})
        except json.JSONDecodeError:
            lock = {}
    text = "Unity package manifest: " + ", ".join(f"{k} {v}" for k, v in sorted(deps.items()))
    return IndexRecord("project_manifest", "project_manifest:packages", text, {
        "project": project, "path": "Packages/manifest.json", "package_versions": deps,
        "package_lock_count": len(lock), "unity_region": "Project",
    })
