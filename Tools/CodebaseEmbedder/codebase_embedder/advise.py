"""Deterministic placement and asmdef advice over structure-graph artifacts."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any


STOPWORDS = {
    "a", "an", "the", "and", "or", "for", "to", "of", "in", "on", "with", "new",
    "feature", "add", "create", "implement", "support", "system", "service",
}


def _terms(text: str) -> list[str]:
    tokens = re.findall(r"[A-Za-z][A-Za-z0-9_]+", text.lower())
    out: list[str] = []
    for token in tokens:
        if len(token) <= 2 or token in STOPWORDS:
            continue
        out.append(token)
        # Split CamelCase leftovers already lowercased — keep stems
        if token.endswith("s") and len(token) > 3:
            out.append(token[:-1])
    return out


def load_structure_graph(artifact_dir: Path) -> dict[str, Any]:
    path = artifact_dir / "structure-graph.json"
    if not path.exists():
        raise FileNotFoundError(
            f"Missing {path}. Run `codebase-embedder scan` (or index) first."
        )
    return json.loads(path.read_text(encoding="utf-8"))


def advise_placement(artifact_dir: Path, feature: str, limit: int = 5) -> dict[str, Any]:
    graph = load_structure_graph(artifact_dir)
    terms = _terms(feature)
    folder_scores: dict[str, float] = {}
    namespace_scores: dict[str, float] = {}
    type_hits: list[tuple[float, str]] = []

    for fq, type_info in graph.get("types", {}).items():
        hay = " ".join(
            [
                fq,
                type_info.get("type_name", ""),
                type_info.get("namespace", ""),
                type_info.get("path", ""),
                type_info.get("folder", ""),
                " ".join(type_info.get("uses_types") or []),
            ]
        ).lower()
        score = float(sum(min(hay.count(t), 3) for t in terms))
        if not score:
            continue
        type_hits.append((score, fq))
        folder = type_info.get("folder") or ""
        ns = type_info.get("namespace") or ""
        if folder:
            folder_scores[folder] = folder_scores.get(folder, 0.0) + score
        if ns:
            namespace_scores[ns] = namespace_scores.get(ns, 0.0) + score

    # Folder-name boosts
    for folder, data in graph.get("folders", {}).items():
        folder_hay = folder.lower()
        boost = float(sum(1 for t in terms if t in folder_hay))
        if boost:
            folder_scores[folder] = folder_scores.get(folder, 0.0) + boost * 4

    if not folder_scores and not namespace_scores:
        # Default to main runtime dialogue area when nothing matches
        default_folder = "Assets/Scripts/Runtime/NPCDialogue"
        folders = graph.get("folders", {})
        if default_folder not in folders:
            runtime_folders = [f for f in folders if f.startswith("Assets/Scripts/Runtime/")]
            default_folder = sorted(runtime_folders)[0] if runtime_folders else "Assets/Scripts/Runtime"
        folder_info = folders.get(default_folder, {})
        return {
            "feature": feature,
            "suggested_namespace": (folder_info.get("namespaces") or ["NPCSystem"])[0],
            "suggested_folder": default_folder,
            "suggested_asmdef": folder_info.get("asmdef") or "NPCSystem.Runtime",
            "sibling_types": folder_info.get("types", [])[:limit],
            "required_usings": ["NPCSystem", "UnityEngine"],
            "rationale": [
                "No strong structural match; defaulting to NPCSystem runtime layout.",
            ],
            "candidates": [],
        }

    ranked_folders = sorted(folder_scores.items(), key=lambda kv: (-kv[1], kv[0]))
    best_folder, best_score = ranked_folders[0]
    folder_info = graph.get("folders", {}).get(best_folder, {})
    ranked_ns = sorted(namespace_scores.items(), key=lambda kv: (-kv[1], kv[0]))
    suggested_ns = ranked_ns[0][0] if ranked_ns else (folder_info.get("namespaces") or ["NPCSystem"])[0]
    suggested_asmdef = folder_info.get("asmdef") or _asmdef_for_folder(graph, best_folder)

    sibling_types = folder_info.get("types") or []
    # Prefer type hits in the winning folder
    ranked_hits = sorted(type_hits, key=lambda pair: (-pair[0], pair[1]))
    nearest = [fq for _, fq in ranked_hits if fq in sibling_types][:limit]
    if len(nearest) < limit:
        for fq in sibling_types:
            if fq not in nearest:
                nearest.append(fq)
            if len(nearest) >= limit:
                break

    usings = _usings_for_types(graph, nearest)
    candidates = []
    for folder, score in ranked_folders[:limit]:
        info = graph.get("folders", {}).get(folder, {})
        candidates.append(
            {
                "folder": folder,
                "score": score,
                "asmdef": info.get("asmdef") or _asmdef_for_folder(graph, folder),
                "namespaces": info.get("namespaces", []),
            }
        )

    rationale = [
        f"Matched feature terms {terms} against type/folder ownership graph.",
        f"Top folder {best_folder} scored {best_score:.1f}.",
        f"Owning assembly resolved to {suggested_asmdef}.",
    ]
    return {
        "feature": feature,
        "suggested_namespace": suggested_ns,
        "suggested_folder": best_folder,
        "suggested_asmdef": suggested_asmdef,
        "sibling_types": nearest,
        "required_usings": usings,
        "rationale": rationale,
        "candidates": candidates,
    }


def advise_asmdef(
    artifact_dir: Path,
    name: str,
    folder: str,
    usings: list[str] | None = None,
    root_namespace: str | None = None,
) -> dict[str, Any]:
    graph = load_structure_graph(artifact_dir)
    usings = usings or []
    root_ns = root_namespace or _infer_root_namespace(name, folder, graph)

    # Collect type uses from sibling folder / related namespaces
    related_types = []
    folder_info = graph.get("folders", {}).get(folder.rstrip("/"), {})
    related_types.extend(folder_info.get("types") or [])

    # Also pull types whose namespace prefix matches root_ns
    for fq, type_info in graph.get("types", {}).items():
        ns = type_info.get("namespace") or ""
        if root_ns and (ns == root_ns or ns.startswith(root_ns + ".")):
            related_types.append(fq)

    related_types = sorted(set(related_types))
    project_type_to_asm = {
        fq: info.get("asmdef", "")
        for fq, info in graph.get("types", {}).items()
        if info.get("asmdef")
    }

    needed_asmdefs: set[str] = set()
    unresolved: set[str] = set()
    rationale: list[str] = []

    # From usings → namespaces → types → asmdefs
    for using_ns in usings:
        matched = False
        for fq, info in graph.get("types", {}).items():
            ns = info.get("namespace") or ""
            if ns == using_ns or ns.startswith(using_ns + ".") or using_ns.startswith(ns + "."):
                asm = info.get("asmdef") or ""
                if asm and asm != name:
                    needed_asmdefs.add(asm)
                    matched = True
        if not matched and using_ns not in {"System", "System.Collections", "System.Collections.Generic", "System.Threading", "System.Threading.Tasks", "UnityEngine"}:
            unresolved.add(using_ns)

    # From type-uses edges of related types
    for edge in graph.get("relations", {}).get("type_uses", []):
        if edge.get("source") not in related_types:
            continue
        target = edge.get("target", "")
        # Resolve simple name to project fq
        resolved_asm = ""
        for fq, asm in project_type_to_asm.items():
            if fq == target or fq.endswith("." + target) or fq.split(".")[-1] == target:
                resolved_asm = asm
                break
        if resolved_asm and resolved_asm != name:
            needed_asmdefs.add(resolved_asm)
            rationale.append(f"type-uses-type {edge.get('source')} -> {target} implies {resolved_asm}")
        elif target and target not in project_type_to_asm:
            unresolved.add(target)

    # Sibling asmdef package refs commonly needed for Runtime folders
    sibling_asm = folder_info.get("asmdef") or _asmdef_for_folder(graph, folder)
    if sibling_asm and sibling_asm in graph.get("asmdefs", {}):
        sibling_refs = graph["asmdefs"][sibling_asm].get("references") or []
        # Prefer Unity/package refs that appear in unresolved or usings
        for ref in sibling_refs:
            ref_l = ref.lower()
            if any(u.lower() in ref_l or ref_l in u.lower() for u in usings):
                needed_asmdefs.add(ref)
                rationale.append(f"Sibling assembly {sibling_asm} already references {ref} for similar usings.")
            elif any(u.lower().replace(".", "") in ref_l.replace(".", "") for u in unresolved):
                needed_asmdefs.add(ref)
                rationale.append(f"Unresolved type/namespace mapped to sibling ref {ref}.")

    # Always include parent runtime asm when creating a nested feature asm under Runtime
    if folder.startswith("Assets/Scripts/Runtime/") and name != "NPCSystem.Runtime":
        if "NPCSystem.Runtime" in graph.get("asmdefs", {}):
            needed_asmdefs.add("NPCSystem.Runtime")
            rationale.append("New runtime assembly should reference NPCSystem.Runtime for shared NPCSystem types.")

    # Map unresolved Unity namespaces to common asmdef names already in the project
    package_map = _package_ref_hints(graph)
    for item in list(unresolved):
        for hint, ref in package_map.items():
            if hint.lower() in item.lower() or item.lower() in hint.lower():
                needed_asmdefs.add(ref)
                rationale.append(f"Unresolved '{item}' mapped to known package ref {ref}.")
                unresolved.discard(item)

    references = sorted(needed_asmdefs)
    sketch = {
        "name": name,
        "rootNamespace": root_ns,
        "references": references,
        "includePlatforms": [],
        "excludePlatforms": [],
        "allowUnsafeCode": False,
        "overrideReferences": False,
        "precompiledReferences": [],
        "autoReferenced": True,
        "defineConstraints": [],
        "versionDefines": [],
        "noEngineReferences": False,
    }
    if not rationale:
        rationale.append("Derived references from usings, type-uses-type edges, and sibling asmdef patterns.")

    return {
        "name": name,
        "folder": folder,
        "root_namespace": root_ns,
        "recommended_references": references,
        "unresolved": sorted(unresolved),
        "related_types": related_types[:20],
        "asmdef_sketch": sketch,
        "rationale": rationale[:30],
    }


def format_placement_advice(advice: dict[str, Any]) -> str:
    lines = [
        f"Feature: {advice['feature']}",
        f"Suggested namespace: {advice['suggested_namespace']}",
        f"Suggested folder: {advice['suggested_folder']}",
        f"Suggested asmdef: {advice['suggested_asmdef']}",
        f"Sibling types: {', '.join(advice.get('sibling_types') or []) or '-'}",
        f"Required usings: {', '.join(advice.get('required_usings') or []) or '-'}",
        "Rationale:",
    ]
    lines.extend(f"  - {r}" for r in advice.get("rationale") or [])
    if advice.get("candidates"):
        lines.append("Candidates:")
        for c in advice["candidates"]:
            lines.append(
                f"  - {c['folder']} (score={c['score']:.1f}, asmdef={c.get('asmdef')}, ns={', '.join(c.get('namespaces') or [])})"
            )
    return "\n".join(lines)


def format_asmdef_advice(advice: dict[str, Any]) -> str:
    lines = [
        f"Asmdef: {advice['name']}",
        f"Folder: {advice['folder']}",
        f"Root namespace: {advice['root_namespace']}",
        f"Recommended references: {', '.join(advice.get('recommended_references') or []) or '-'}",
        f"Unresolved: {', '.join(advice.get('unresolved') or []) or '-'}",
        "Sketch:",
        json.dumps(advice.get("asmdef_sketch") or {}, indent=2),
        "Rationale:",
    ]
    lines.extend(f"  - {r}" for r in advice.get("rationale") or [])
    return "\n".join(lines)


def _asmdef_for_folder(graph: dict[str, Any], folder: str) -> str:
    info = graph.get("folders", {}).get(folder, {})
    if info.get("asmdef"):
        return info["asmdef"]
    # Walk up
    parts = folder.rstrip("/").split("/")
    for i in range(len(parts), 0, -1):
        candidate = "/".join(parts[:i])
        for asm_name, asm in graph.get("asmdefs", {}).items():
            if asm.get("directory", "").rstrip("/") == candidate:
                return asm_name
    if folder.startswith("Assets/Scripts/Editor"):
        return "NPCSystem.Editor"
    if folder.startswith("Assets/Scripts/Tests"):
        return "NPCSystem.Tests"
    return "NPCSystem.Runtime"


def _usings_for_types(graph: dict[str, Any], type_fqs: list[str]) -> list[str]:
    usings: set[str] = set()
    for fq in type_fqs:
        info = graph.get("types", {}).get(fq) or {}
        ns = info.get("namespace")
        if ns:
            usings.add(ns)
        for used in info.get("uses_types") or []:
            # Prefer namespace of resolved project types
            for other_fq, other in graph.get("types", {}).items():
                if other_fq == used or other_fq.endswith("." + used) or other.get("type_name") == used:
                    if other.get("namespace"):
                        usings.add(other["namespace"])
                    break
            else:
                if "." in used:
                    usings.add(used.rsplit(".", 1)[0])
    preferred = ["UnityEngine", "NPCSystem"]
    ordered = [u for u in preferred if u in usings]
    ordered.extend(sorted(u for u in usings if u not in ordered))
    return ordered


def _infer_root_namespace(name: str, folder: str, graph: dict[str, Any]) -> str:
    if name.startswith("NPCSystem."):
        return "NPCSystem"
    folder_info = graph.get("folders", {}).get(folder.rstrip("/"), {})
    namespaces = folder_info.get("namespaces") or []
    if namespaces:
        return namespaces[0].split(".")[0] if namespaces[0] else "NPCSystem"
    return "NPCSystem"


def _package_ref_hints(graph: dict[str, Any]) -> dict[str, str]:
    """Map namespace/type fragments to asmdef reference names already used in the project."""
    hints: dict[str, str] = {
        "Unity.Netcode": "Unity.Netcode.Runtime",
        "Netcode": "Unity.Netcode.Runtime",
        "Unity.InputSystem": "Unity.InputSystem",
        "InputSystem": "Unity.InputSystem",
        "Unity.Collections": "Unity.Collections",
        "TextMeshPro": "Unity.TextMeshPro",
        "TMPro": "Unity.TextMeshPro",
        "EditorAttributes": "EditorAttributes",
        "DedicatedServer": "Unity.DedicatedServer.MultiplayerRoles",
    }
    for asm in graph.get("asmdefs", {}).values():
        for ref in asm.get("references") or []:
            hints[ref] = ref
            short = ref.split(".")[-1]
            hints.setdefault(short, ref)
    return hints
