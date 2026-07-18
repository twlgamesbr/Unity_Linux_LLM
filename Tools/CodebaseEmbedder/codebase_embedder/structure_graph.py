"""Build a deterministic structure-graph.json for agent placement decisions."""

from __future__ import annotations

from collections import defaultdict
from typing import Any

from .asmdef_parser import AssemblyRecord
from .records import IndexRecord, RelationRecord


def build_structure_graph(
    records: list[IndexRecord],
    relations: list[RelationRecord],
    assemblies: list[AssemblyRecord],
) -> dict[str, Any]:
    namespaces: dict[str, dict[str, Any]] = {}
    types: dict[str, dict[str, Any]] = {}
    asmdefs: dict[str, dict[str, Any]] = {}
    folders: dict[str, dict[str, Any]] = defaultdict(lambda: {"types": [], "namespaces": set(), "asmdef": ""})

    for asm in assemblies:
        asmdefs[asm.name] = {
            "name": asm.name,
            "path": asm.path,
            "root_namespace": asm.root_namespace,
            "references": list(asm.references),
            "directory": asm.directory,
            "owned_namespaces": set(),
            "owned_types": [],
            "type_count": 0,
        }

    for rec in records:
        payload = rec.payload
        if rec.record_type == "type":
            fq = payload.get("fq_name") or (
                f"{payload.get('namespace', '')}.{payload.get('type_name', '')}".strip(".")
            )
            if not fq:
                continue
            ns = payload.get("namespace", "") or ""
            path = payload.get("path", "")
            asm = payload.get("asmdef", "") or ""
            folder = "/".join(path.split("/")[:-1]) if path else ""
            entry = {
                "fq_name": fq,
                "type_name": payload.get("type_name", ""),
                "namespace": ns,
                "asmdef": asm,
                "path": path,
                "folder": folder,
                "base_types": list(payload.get("base_types") or []),
                "interfaces": list(payload.get("interfaces") or []),
                "fields": [],
                "serialized_fields": [],
                "members": [],
                "uses_types": [],
            }
            types[fq] = entry
            ns_entry = namespaces.setdefault(
                ns or "(global)",
                {"namespace": ns or "(global)", "asmdefs": set(), "types": [], "folders": set()},
            )
            ns_entry["types"].append(fq)
            if asm:
                ns_entry["asmdefs"].add(asm)
            if folder:
                ns_entry["folders"].add(folder)
                folders[folder]["types"].append(fq)
                if ns:
                    folders[folder]["namespaces"].add(ns)
                if asm:
                    folders[folder]["asmdef"] = asm
            if asm and asm in asmdefs:
                asmdefs[asm]["owned_types"].append(fq)
                asmdefs[asm]["type_count"] += 1
                if ns:
                    asmdefs[asm]["owned_namespaces"].add(ns)

        elif rec.record_type in {"field", "serialized_field"}:
            ns = payload.get("namespace", "")
            type_name = payload.get("type_name", "")
            fq = f"{ns}.{type_name}".strip(".") if ns else type_name
            if fq not in types:
                continue
            field_info = {
                "name": payload.get("member_name", ""),
                "field_type": payload.get("field_type") or payload.get("signature", ""),
                "is_serialized": bool(payload.get("is_serialized") or rec.record_type == "serialized_field"),
                "attributes": list(payload.get("attributes") or []),
            }
            if field_info["is_serialized"]:
                types[fq]["serialized_fields"].append(field_info)
            else:
                types[fq]["fields"].append(field_info)

        elif rec.record_type == "member":
            ns = payload.get("namespace", "")
            type_name = payload.get("type_name", "")
            fq = f"{ns}.{type_name}".strip(".") if ns else type_name
            if fq not in types:
                continue
            types[fq]["members"].append(
                {
                    "name": payload.get("member_name", ""),
                    "kind": payload.get("symbol_kind", "method"),
                    "signature": payload.get("signature", ""),
                    "return_type": payload.get("return_type", ""),
                    "parameter_types": list(payload.get("parameter_types") or []),
                }
            )

    type_uses: list[dict[str, str]] = []
    asmdef_refs: list[dict[str, str]] = []
    namespace_uses: list[dict[str, str]] = []
    for rel in relations:
        if rel.relation_kind == "type-uses-type":
            type_uses.append(
                {
                    "source": rel.source,
                    "target": rel.target,
                    "path": rel.path,
                    "via": str(rel.payload.get("via", "")),
                }
            )
            if rel.source in types and rel.target not in types[rel.source]["uses_types"]:
                types[rel.source]["uses_types"].append(rel.target)
        elif rel.relation_kind == "asmdef-references":
            asmdef_refs.append({"source": rel.source, "target": rel.target, "path": rel.path})
        elif rel.relation_kind == "namespace-uses-namespace":
            namespace_uses.append({"source": rel.source, "target": rel.target, "path": rel.path})

    # Serialize sets
    for ns_entry in namespaces.values():
        ns_entry["asmdefs"] = sorted(ns_entry["asmdefs"])
        ns_entry["folders"] = sorted(ns_entry["folders"])
        ns_entry["types"] = sorted(set(ns_entry["types"]))
    for asm_entry in asmdefs.values():
        asm_entry["owned_namespaces"] = sorted(asm_entry["owned_namespaces"])
        asm_entry["owned_types"] = sorted(set(asm_entry["owned_types"]))
    folder_out = {
        folder: {
            "folder": folder,
            "asmdef": data["asmdef"],
            "namespaces": sorted(data["namespaces"]),
            "types": sorted(set(data["types"])),
        }
        for folder, data in folders.items()
    }

    return {
        "namespaces": namespaces,
        "types": types,
        "asmdefs": asmdefs,
        "folders": folder_out,
        "relations": {
            "type_uses": type_uses,
            "asmdef_references": asmdef_refs,
            "namespace_uses": namespace_uses,
        },
    }
