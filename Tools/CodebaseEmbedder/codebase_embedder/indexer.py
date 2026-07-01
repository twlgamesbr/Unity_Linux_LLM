from __future__ import annotations

from dataclasses import asdict, dataclass
import json
from pathlib import Path
from typing import Any

from .asmdef_parser import parse_asmdefs
from .chunking import chunk_docs, records_to_embedding_chunks
from .config import CodebaseEmbedderConfig
from .csharp_analyzer import analyze_csharp_files
from .discovery import discover_project_files
from .package_parser import parse_package_manifest
from .records import IndexRecord, RelationRecord, utc_now, write_json, write_jsonl


@dataclass(slots=True)
class IndexResult:
    records: list[IndexRecord]
    relations: list[RelationRecord]
    counts: dict[str, int]
    artifact_dir: Path


def build_index(config: CodebaseEmbedderConfig, write_artifacts: bool = True) -> IndexResult:
    files = discover_project_files(config)
    assemblies, asm_relations = parse_asmdefs(config.project_root, files.asmdefs)
    records: list[IndexRecord] = [parse_package_manifest(config.project_root, config.project_slug)]
    records.extend(asm.to_index_record(config.project_slug) for asm in assemblies)
    symbol_records, symbol_relations = analyze_csharp_files(config.project_root, files.csharp, assemblies, config.project_slug)
    records.extend(symbol_records)
    records.extend(chunk_docs(config.project_slug, config.project_root, files.docs))
    if config.collection_profile == "hierarchy":
        records.extend(_build_namespace_summary_records(symbol_records, config.project_slug))
    if config.collection_profile == "runtime":
        records.extend(_build_runtime_summary_records(records, config.project_slug))
    relations = asm_relations + symbol_relations
    # Keep the full relation graph in relations.jsonl, but only embed high-signal
    # structural edges into Qdrant. Per-invocation `calls` and containment edges
    # are useful as artifact data but too noisy/expensive as vector points.
    embedded_relation_kinds = {"asmdef-references", "inherits", "implements", "namespace-contains-type", "namespace-uses-namespace"}
    records.extend(rel.to_index_record(config.project_slug) for rel in relations if rel.relation_kind in embedded_relation_kinds)
    chunks = records_to_embedding_chunks(records)
    counts = {
        "csharp_files": len(files.csharp), "asmdef_files": len(files.asmdefs), "doc_files": len(files.docs),
        "records": len(records), "relations": len(relations), "chunks": len(chunks),
    }
    if write_artifacts:
        art = config.artifact_dir
        write_json(art / "manifest.json", {"project": config.project_slug, "collection": config.collection_name, "indexed_at": utc_now(), "counts": counts})
        write_json(art / "asmdefs.json", [asdict(asm) for asm in assemblies])
        write_jsonl(art / "symbols.jsonl", [r for r in symbol_records if r.record_type in {"type", "member", "serialized_field", "namespace", "using_directive", "file_overview", "namespace_summary", "runtime_summary"}])
        write_jsonl(art / "relations.jsonl", [asdict(r) for r in relations])
        write_jsonl(art / "chunks.jsonl", chunks)
        _write_report(art / "index-report.md", counts, records)
    return IndexResult(records=records, relations=relations, counts=counts, artifact_dir=config.artifact_dir)


def _build_namespace_summary_records(records: list[IndexRecord], project: str) -> list[IndexRecord]:
    summaries: dict[tuple[str, str], dict[str, set[str] | str]] = {}
    for rec in records:
        payload = rec.payload
        namespace = payload.get("namespace") or payload.get("root_namespace") or ""
        if not namespace:
            continue
        key = (payload.get("path", ""), namespace)
        summary = summaries.setdefault(
            key,
            {"path": payload.get("path", ""), "asmdef": payload.get("asmdef", ""), "region": payload.get("unity_region", ""), "types": set(), "members": set(), "usings": set()},
        )
        if rec.record_type == "type":
            summary["types"].add(payload.get("type_name", ""))  # type: ignore[union-attr]
        elif rec.record_type == "member":
            summary["members"].add(payload.get("member_name", ""))  # type: ignore[union-attr]
        elif rec.record_type == "using_directive":
            summary["usings"].add(payload.get("using_namespace", ""))  # type: ignore[union-attr]
        elif rec.record_type == "file_overview":
            for type_name in payload.get("type_names", []) or []:
                summary["types"].add(type_name)  # type: ignore[union-attr]
            for using_ns in payload.get("using_directives", []) or []:
                summary["usings"].add(using_ns)  # type: ignore[union-attr]

    out: list[IndexRecord] = []
    for (path, namespace), summary in summaries.items():
        types = sorted(t for t in summary["types"] if t)
        members = sorted(m for m in summary["members"] if m)
        usings = sorted(u for u in summary["usings"] if u)
        payload = {
            "project": project,
            "path": path,
            "asmdef": summary["asmdef"],
            "unity_region": summary["region"],
            "namespace": namespace,
            "type_names": types,
            "member_names": members,
            "using_directives": usings,
            "symbol_kind": "namespace_summary",
        }
        text = "\n".join([
            f"Namespace summary {namespace}",
            f"Path {path}",
            f"Assembly {summary['asmdef'] or '-'}",
            f"Region {summary['region'] or '-'}",
            f"Types: {', '.join(types) or '-'}",
            f"Members: {', '.join(members) or '-'}",
            f"Using directives: {', '.join(usings) or '-'}",
        ])
        stable = f"namespace_summary:{namespace}:{path}"
        out.append(IndexRecord("namespace_summary", stable, text, payload))
    return out


def _build_runtime_summary_records(records: list[IndexRecord], project: str) -> list[IndexRecord]:
    out: list[IndexRecord] = []
    for rec in records:
        payload = rec.payload
        if rec.record_type != "file_overview":
            continue
        region = payload.get("unity_region", "")
        if region not in {"Runtime", "Scene"}:
            continue
        path = payload.get("path", "")
        asmdef = payload.get("asmdef", "")
        namespace_names = payload.get("declared_namespaces", []) or []
        type_names = payload.get("type_names", []) or []
        member_names = payload.get("member_names", []) or []
        usings = payload.get("using_directives", []) or []
        role = _runtime_role(path, type_names)
        summary_payload = {
            "project": project,
            "path": path,
            "asmdef": asmdef,
            "unity_region": region,
            "namespace": namespace_names[0] if namespace_names else "",
            "type_names": type_names,
            "member_names": member_names,
            "using_directives": usings,
            "runtime_role": role,
            "symbol_kind": "runtime_summary",
        }
        text_parts = [
            f"Runtime summary {path}",
            f"Assembly {asmdef or '-'} — OWNS this runtime file",
            f"Region {region}",
            f"Role: {role}",
        ]
        if namespace_names:
            for ns in namespace_names:
                text_parts.append(f"Declared namespace: {ns} — declares types: {', '.join(sorted(t for t in type_names if not t.startswith(ns))) if type_names else '-'}")
        else:
            text_parts.append(f"Declared namespace: (global)")
        for ns in (namespace_names or ["(global)"]):
            for using_ns in (usings or []):
                text_parts.append(f"Namespace-uses: {ns} imports {using_ns}")
        if type_names:
            text_parts.append(f"Exported types: {', '.join(sorted(type_names))}")
        if member_names:
            text_parts.append(f"Member methods: {', '.join(sorted(member_names))}")
        text = "\n".join(text_parts)
        out.append(IndexRecord("runtime_summary", f"runtime_summary:{path}", text, summary_payload))
    return out


def _runtime_role(path: str, type_names: list[str]) -> str:
    hay = f"{path} {' '.join(type_names)}".lower()
    if "qdrant" in hay:
        return "qdrant retrieval and semantic memory"
    if "cognee" in hay:
        return "cognee memory integration"
    if "npcdialoguemanager" in hay or "manager" in hay:
        return "dialogue orchestration and transport"
    if "bootstrapper" in hay:
        return "scene bootstrap and warmup"
    if "validator" in hay:
        return "startup smoke validation"
    if "history" in hay:
        return "conversation history storage"
    if "logger" in hay:
        return "structured runtime logging"
    if "actionplanner" in hay:
        return "player intent routing"
    if "llmagent" in hay or "llm" in hay:
        return "LLM transport and model control"
    return "runtime ownership"


def _write_report(path: Path, counts: dict[str, int], records: list[IndexRecord]) -> None:
    by_type: dict[str, int] = {}
    by_asm: dict[str, int] = {}
    for r in records:
        by_type[r.record_type] = by_type.get(r.record_type, 0) + 1
        asm = r.payload.get("asmdef")
        if asm:
            by_asm[asm] = by_asm.get(asm, 0) + 1
    lines = ["# Codebase Index Report", "", "## Counts", ""]
    lines.extend(f"- {k}: {v}" for k, v in counts.items())
    lines += ["", "## Records by Type", ""]
    lines.extend(f"- {k}: {v}" for k, v in sorted(by_type.items()))
    lines += ["", "## Records by Assembly", ""]
    lines.extend(f"- {k}: {v}" for k, v in sorted(by_asm.items()))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def load_chunks(artifact_dir: Path) -> list[IndexRecord]:
    path = artifact_dir / "chunks.jsonl"
    records = []
    if not path.exists():
        return records
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        obj = json.loads(line)
        records.append(IndexRecord(obj["record_type"], obj["stable_key"], obj["text"], obj.get("payload", {}), obj.get("point_id")))
    return records
