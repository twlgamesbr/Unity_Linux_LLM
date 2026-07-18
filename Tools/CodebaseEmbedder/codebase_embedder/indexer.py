from __future__ import annotations

from dataclasses import asdict, dataclass
import json
from pathlib import Path
from typing import Any

from .asmdef_parser import AssemblyRecord, parse_asmdefs
from .chunking import chunk_docs, records_to_embedding_chunks
from .config import CodebaseEmbedderConfig
from .coverage import load_coverage_report
from .coverage_records import apply_coverage_to_records, build_coverage_summary_records
from .csharp_analyzer import analyze_csharp_files, _convention_flags
from .discovery import discover_project_files
from .package_parser import parse_package_manifest
from .project_rules import build_project_rule_records
from .records import IndexRecord, RelationRecord, utc_now, write_json, write_jsonl
from .structure_graph import build_structure_graph


SYMBOL_RECORD_TYPES = {
    "type",
    "member",
    "serialized_field",
    "field",
    "namespace",
    "using_directive",
    "file_overview",
    "namespace_summary",
    "runtime_summary",
    "coverage_summary",
    "code_convention",
    "assembly",
    "project_rule",
    "type_summary",
    "asmdef_summary",
}

EMBEDDED_RELATION_KINDS = {
    "asmdef-references",
    "inherits",
    "implements",
    "namespace-contains-type",
    "namespace-uses-namespace",
    "type-uses-type",
}


@dataclass(slots=True)
class IndexResult:
    records: list[IndexRecord]
    relations: list[RelationRecord]
    counts: dict[str, int]
    artifact_dir: Path


def build_index(config: CodebaseEmbedderConfig, write_artifacts: bool = True) -> IndexResult:
    files = discover_project_files(config)
    coverage_report = load_coverage_report(config.project_root, config.coverage_report_dir_name)
    assemblies, asm_relations = parse_asmdefs(config.project_root, files.asmdefs)
    records: list[IndexRecord] = [parse_package_manifest(config.project_root, config.project_slug)]
    records.extend(asm.to_index_record(config.project_slug) for asm in assemblies)
    records.extend(build_project_rule_records(config.project_root, config.project_slug))
    symbol_records, symbol_relations = analyze_csharp_files(
        config.project_root, files.csharp, assemblies, config.project_slug
    )
    records.extend(symbol_records)
    records.extend(chunk_docs(config.project_slug, config.project_root, files.docs))
    if coverage_report is not None:
        apply_coverage_to_records(records, coverage_report)

    # Always build namespace rollups; runtime profile adds ownership summaries.
    records.extend(_build_namespace_summary_records(symbol_records, config.project_slug))
    if config.collection_profile == "runtime":
        records.extend(_build_runtime_summary_records(records, config.project_slug))

    relations = asm_relations + symbol_relations
    _resolve_type_uses(relations, records)
    records.extend(_build_type_summary_records(records, relations, config.project_slug))
    records.extend(_build_asmdef_summary_records(records, relations, assemblies, config.project_slug))

    # Always build code_convention records regardless of profile
    records.extend(_build_convention_records(records, files.csharp, config.project_root, config.project_slug))
    if coverage_report is not None:
        apply_coverage_to_records(records, coverage_report)
        records.extend(build_coverage_summary_records(records, coverage_report, config.project_slug))

    # Deduplicate noisy type-uses-type before embedding (same source/target/path).
    relations = _dedupe_relations(relations)

    records.extend(
        rel.to_index_record(config.project_slug)
        for rel in relations
        if rel.relation_kind in EMBEDDED_RELATION_KINDS
    )
    chunks = records_to_embedding_chunks(records)
    counts = {
        "csharp_files": len(files.csharp),
        "asmdef_files": len(files.asmdefs),
        "doc_files": len(files.docs),
        "records": len(records),
        "relations": len(relations),
        "chunks": len(chunks),
    }
    if coverage_report is not None:
        counts["coverage_classes"] = len(coverage_report.classes)

    structure = build_structure_graph(records, relations, assemblies)

    if write_artifacts:
        art = config.artifact_dir
        write_json(
            art / "manifest.json",
            {
                "project": config.project_slug,
                "collection": config.collection_name,
                "indexed_at": utc_now(),
                "counts": counts,
            },
        )
        write_json(art / "asmdefs.json", [asdict(asm) for asm in assemblies])
        write_json(art / "structure-graph.json", structure)
        write_jsonl(
            art / "symbols.jsonl",
            [r for r in records if r.record_type in SYMBOL_RECORD_TYPES],
        )
        write_jsonl(art / "relations.jsonl", [asdict(r) for r in relations])
        write_jsonl(art / "chunks.jsonl", chunks)
        _write_report(art / "index-report.md", counts, records)
    return IndexResult(records=records, relations=relations, counts=counts, artifact_dir=config.artifact_dir)


def _dedupe_relations(relations: list[RelationRecord]) -> list[RelationRecord]:
    seen: set[tuple[str, str, str, str]] = set()
    out: list[RelationRecord] = []
    for rel in relations:
        key = (rel.relation_kind, rel.source, rel.target, rel.path)
        if key in seen:
            continue
        seen.add(key)
        out.append(rel)
    return out


def _resolve_type_uses(relations: list[RelationRecord], records: list[IndexRecord]) -> None:
    """Attach resolved_to fq_name when a type-uses-type target matches a project type."""
    by_simple: dict[str, list[str]] = {}
    by_fq: dict[str, str] = {}
    for rec in records:
        if rec.record_type != "type":
            continue
        fq = rec.payload.get("fq_name") or ""
        name = rec.payload.get("type_name") or ""
        if fq:
            by_fq[fq] = fq
            by_simple.setdefault(name, []).append(fq)
    for rel in relations:
        if rel.relation_kind != "type-uses-type":
            continue
        target = rel.target
        if target in by_fq:
            rel.payload["resolved_to"] = target
            rel.payload["resolved"] = True
            continue
        candidates = by_simple.get(target, [])
        if len(candidates) == 1:
            rel.payload["resolved_to"] = candidates[0]
            rel.payload["resolved"] = True
            rel.target = candidates[0]
        elif candidates:
            rel.payload["resolved_to"] = candidates[0]
            rel.payload["resolution_count"] = len(candidates)
            rel.payload["resolved"] = False
        else:
            rel.payload["resolved"] = False


def _build_type_summary_records(
    records: list[IndexRecord],
    relations: list[RelationRecord],
    project: str,
) -> list[IndexRecord]:
    uses_by_source: dict[str, set[str]] = {}
    for rel in relations:
        if rel.relation_kind == "type-uses-type":
            uses_by_source.setdefault(rel.source, set()).add(rel.target)

    fields_by_type: dict[str, list[str]] = {}
    serialized_by_type: dict[str, list[str]] = {}
    members_by_type: dict[str, list[str]] = {}
    for rec in records:
        ns = rec.payload.get("namespace", "")
        type_name = rec.payload.get("type_name", "")
        fq = f"{ns}.{type_name}".strip(".") if ns else type_name
        if not fq:
            continue
        if rec.record_type == "field":
            fields_by_type.setdefault(fq, []).append(rec.payload.get("member_name", ""))
        elif rec.record_type == "serialized_field":
            serialized_by_type.setdefault(fq, []).append(rec.payload.get("member_name", ""))
        elif rec.record_type == "member":
            members_by_type.setdefault(fq, []).append(rec.payload.get("member_name", ""))

    out: list[IndexRecord] = []
    for rec in records:
        if rec.record_type != "type":
            continue
        fq = rec.payload.get("fq_name") or (
            f"{rec.payload.get('namespace', '')}.{rec.payload.get('type_name', '')}".strip(".")
        )
        fields = sorted(f for f in fields_by_type.get(fq, []) if f)
        serialized = sorted(f for f in serialized_by_type.get(fq, []) if f)
        members = sorted(m for m in members_by_type.get(fq, []) if m)
        uses = sorted(uses_by_source.get(fq, set()))
        payload = {
            "project": project,
            "path": rec.payload.get("path", ""),
            "asmdef": rec.payload.get("asmdef", ""),
            "asmdef_path": rec.payload.get("asmdef_path", ""),
            "unity_region": rec.payload.get("unity_region", ""),
            "namespace": rec.payload.get("namespace", ""),
            "type_name": rec.payload.get("type_name", ""),
            "fq_name": fq,
            "base_types": list(rec.payload.get("base_types") or []),
            "interfaces": list(rec.payload.get("interfaces") or []),
            "field_names": fields,
            "serialized_field_names": serialized,
            "member_names": members,
            "uses_types": uses,
            "symbol_kind": "type_summary",
        }
        text = "\n".join(
            [
                f"Type summary {fq}",
                f"Assembly {payload['asmdef'] or '-'}",
                f"Namespace {payload['namespace'] or '-'}",
                f"Path {payload['path']}",
                f"Base types: {', '.join(payload['base_types']) or '-'}",
                f"Interfaces: {', '.join(payload['interfaces']) or '-'}",
                f"Fields: {', '.join(fields) or '-'}",
                f"Serialized fields: {', '.join(serialized) or '-'}",
                f"Members: {', '.join(members) or '-'}",
                f"Uses types: {', '.join(uses) or '-'}",
            ]
        )
        out.append(IndexRecord("type_summary", f"type_summary:{fq}:{payload['path']}", text, payload))
    return out


def _build_asmdef_summary_records(
    records: list[IndexRecord],
    relations: list[RelationRecord],
    assemblies: list[AssemblyRecord],
    project: str,
) -> list[IndexRecord]:
    owned_ns: dict[str, set[str]] = {asm.name: set() for asm in assemblies}
    owned_types: dict[str, set[str]] = {asm.name: set() for asm in assemblies}
    folders: dict[str, set[str]] = {asm.name: set() for asm in assemblies}
    unresolved_uses: dict[str, set[str]] = {asm.name: set() for asm in assemblies}

    type_fq_to_asm = {}
    for rec in records:
        if rec.record_type != "type":
            continue
        asm = rec.payload.get("asmdef") or ""
        fq = rec.payload.get("fq_name") or ""
        ns = rec.payload.get("namespace") or ""
        path = rec.payload.get("path") or ""
        if not asm:
            continue
        if fq:
            owned_types.setdefault(asm, set()).add(fq)
            type_fq_to_asm[fq] = asm
        if ns:
            owned_ns.setdefault(asm, set()).add(ns)
        if path:
            folders.setdefault(asm, set()).add("/".join(path.split("/")[:-1]))

    for rel in relations:
        if rel.relation_kind != "type-uses-type":
            continue
        src_asm = type_fq_to_asm.get(rel.source, "")
        if not src_asm:
            continue
        if not rel.payload.get("resolved"):
            unresolved_uses.setdefault(src_asm, set()).add(rel.target)

    refs_by_asm = {asm.name: list(asm.references) for asm in assemblies}
    out: list[IndexRecord] = []
    for asm in assemblies:
        ns_list = sorted(owned_ns.get(asm.name, set()))
        type_list = sorted(owned_types.get(asm.name, set()))
        folder_list = sorted(folders.get(asm.name, set()))
        unresolved = sorted(unresolved_uses.get(asm.name, set()))
        refs = refs_by_asm.get(asm.name, [])
        payload = {
            "project": project,
            "path": asm.path,
            "asmdef": asm.name,
            "root_namespace": asm.root_namespace,
            "references": refs,
            "owned_namespaces": ns_list,
            "owned_types": type_list,
            "folders": folder_list,
            "unresolved_type_uses": unresolved,
            "type_count": len(type_list),
            "symbol_kind": "asmdef_summary",
        }
        text = "\n".join(
            [
                f"Asmdef summary {asm.name}",
                f"Path {asm.path}",
                f"Root namespace {asm.root_namespace or '-'}",
                f"References: {', '.join(refs) or '-'}",
                f"Owned namespaces: {', '.join(ns_list) or '-'}",
                f"Folders: {', '.join(folder_list) or '-'}",
                f"Type count: {len(type_list)}",
                f"Unresolved type uses: {', '.join(unresolved[:40]) or '-'}",
            ]
        )
        out.append(IndexRecord("asmdef_summary", f"asmdef_summary:{asm.name}", text, payload))
    return out


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
            {
                "path": payload.get("path", ""),
                "asmdef": payload.get("asmdef", ""),
                "region": payload.get("unity_region", ""),
                "types": set(),
                "members": set(),
                "usings": set(),
            },
        )
        if not summary["asmdef"] and payload.get("asmdef"):
            summary["asmdef"] = payload["asmdef"]
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
        text = "\n".join(
            [
                f"Namespace summary {namespace}",
                f"Path {path}",
                f"Assembly {summary['asmdef'] or '-'}",
                f"Region {summary['region'] or '-'}",
                f"Types: {', '.join(types) or '-'}",
                f"Members: {', '.join(members) or '-'}",
                f"Using directives: {', '.join(usings) or '-'}",
            ]
        )
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
                text_parts.append(
                    f"Declared namespace: {ns} — declares types: {', '.join(sorted(type_names)) if type_names else '-'}"
                )
        else:
            text_parts.append("Declared namespace: (global)")
        for ns in namespace_names or ["(global)"]:
            for using_ns in usings or []:
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
    lines.extend(f"- {k}: {v}" for k, v in sorted(by_type.items(), key=lambda pair: str(pair[0])))
    lines += ["", "## Records by Assembly", ""]
    lines.extend(f"- {k}: {v}" for k, v in sorted(by_asm.items(), key=lambda pair: str(pair[0])))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _build_convention_records(
    records: list[IndexRecord],
    csharp_paths: list[Path],
    project_root: Path,
    project: str,
) -> list[IndexRecord]:
    """Build per-file code_convention records (compliance evidence)."""
    out: list[IndexRecord] = []
    path_to_overview = {}
    for rec in records:
        if rec.record_type == "file_overview" and rec.payload.get("path"):
            path_to_overview[rec.payload["path"]] = rec

    for path in csharp_paths:
        rel = path.relative_to(project_root).as_posix() if path.is_absolute() else path.as_posix()
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        flags = _convention_flags(text, "Runtime")
        overview = path_to_overview.get(rel)
        if overview:
            overview.payload.setdefault("has_xml_docs_on_public", flags["has_xml_docs_on_public"])
            overview.payload.setdefault("todo_count", flags["todo_count"])
            overview.payload.setdefault("has_hardcoded_localhost", flags["has_hardcoded_localhost"])
            overview.payload.setdefault("has_formeryserializedas", flags["has_formeryserializedas"])
            overview.payload.setdefault("public_methods_documented", flags["public_methods_documented"])
            overview.payload.setdefault("public_methods_total", flags["public_methods_total"])

        doc_rate = flags["public_methods_documented"] / max(flags["public_methods_total"], 1)
        phase4_count = flags.get("phase4_direct_count", 0)
        property_count = flags.get("property_wrapper_count", 0)

        text_parts = [
            f"Code convention {rel}",
            f"Assembly {overview.payload.get('asmdef', '') if overview else ''}",
            f"XML docs on public API: {'yes' if flags['has_xml_docs_on_public'] else 'no'} ({flags['public_methods_documented']}/{flags['public_methods_total']} methods documented)",
            f"TODO/FIXME/HACK count: {flags['todo_count']}",
            f"Hardcoded 'localhost' strings: {'yes' if flags['has_hardcoded_localhost'] else 'no'}",
            f"[FormerlySerializedAs] usage: {'yes' if flags['has_formeryserializedas'] else 'no'}",
            f"Phase 4 [SerializeField] private fields: {phase4_count} fields converted, {property_count} property wrappers",
            f"Async yield pattern: {'yes' if flags['has_async_pattern'] else 'no'}",
        ]
        payload = {
            "project": project,
            "path": rel,
            "asmdef": overview.payload.get("asmdef", "") if overview else "",
            "unity_region": overview.payload.get("unity_region", "") if overview else "",
            "symbol_kind": "code_convention",
            **flags,
            "phase4_converted_count": phase4_count,
            "property_wrapper_count": property_count,
            "xml_doc_rate": round(doc_rate, 3),
        }
        out.append(
            IndexRecord(
                "code_convention",
                f"code_convention:{rel}",
                "\n".join(text_parts),
                payload,
            )
        )
    return out


def load_chunks(artifact_dir: Path) -> list[IndexRecord]:
    path = artifact_dir / "chunks.jsonl"
    records = []
    if not path.exists():
        return records
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        obj = json.loads(line)
        records.append(
            IndexRecord(
                obj["record_type"],
                obj["stable_key"],
                obj["text"],
                obj.get("payload", {}),
                obj.get("point_id"),
            )
        )
    return records
