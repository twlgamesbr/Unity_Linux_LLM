from __future__ import annotations

import re


COVERAGE_QUERY_TERMS = {"coverage", "covered", "untested", "tested", "hotspot", "hotspots", "risk", "risky", "regression", "crap"}


def symbol_name_boost(payload: dict, terms: list[str]) -> float:
    if not terms:
        return 0.0
    symbol = str(payload.get("type_name") or payload.get("member_name") or payload.get("using_namespace") or "")
    if not symbol:
        return 0.0
    symbol_words = camel_words(symbol).lower().split()
    if not symbol_words:
        return 0.0
    matched = sum(1 for term in terms if term in symbol_words)
    bonus = 6.0 * matched
    if matched == len(terms):
        bonus += 18.0
    return bonus


def path_term_boost(payload: dict, terms: list[str]) -> float:
    if not terms:
        return 0.0
    path_text = str(payload.get("path") or "") + " " + str(payload.get("relative_dir") or "")
    words = camel_words(path_text).lower().split()
    if not words:
        return 0.0
    matched = sum(1 for term in terms if term in words)
    bonus = 4.0 * matched
    if matched >= 2:
        bonus += 8.0
    return bonus


def payload_aliases(payload: dict) -> str:
    record_type = payload.get("record_type", "")
    aliases = {
        "namespace": "namespace namespaces declared declaration",
        "namespace_summary": "namespace namespaces hierarchy summary overview types members using directives",
        "runtime_summary": "runtime runtime ownership transport service bootstrapper logger history qdrant scene dialogue",
        "coverage_summary": "coverage tested untested hotspot regression risk crap complexity confidence quality reliability",
        "using_directive": "using usings import imports reference references dependency dependencies",
        "file_overview": "file overview namespace namespaces using usings type types member members reference references",
        "assembly": "assembly asmdef reference references dependency dependencies",
        "relation": "relation relations reference references dependency dependencies graph edge",
        "type": "type types class classes interface interfaces enum enums struct structs",
        "member": "member members method methods field fields function functions",
        "serialized_field": "field fields serialized serializefield member members",
        "code_convention": "code convention pattern style phase4 formeryserializedas todo fixme hack xml doc documentation anti-pattern quality compliance rules conventions best-practice editorconfig naming camelcase pascalcase",
    }
    return aliases.get(record_type, "")


def coverage_record_boost(payload: dict, coverage_intent: bool) -> float:
    if not coverage_intent:
        return 0.0
    boosts = {
        "coverage_summary": 70.0,
        "runtime_summary": 18.0,
        "file_overview": 12.0,
        "type": 2.0,
        "member": 0.0,
        "serialized_field": -10.0,
    }
    bonus = boosts.get(payload.get("record_type", ""), 0.0)
    if payload.get("coverage_method_is_hotspot"):
        bonus += 18.0
    line_rate = payload_rate(payload, "coverage_line_rate")
    if line_rate <= 0.0:
        bonus += 6.0
    elif line_rate < 35.0:
        bonus += 10.0
    return bonus


def coverage_confidence_boost(payload: dict, behavior_intent: bool, owner_intent: bool, scene_intent: bool) -> float:
    if not (behavior_intent or owner_intent or scene_intent):
        return 0.0
    record_type = str(payload.get("record_type", ""))
    if record_type not in {"coverage_summary", "runtime_summary", "file_overview", "type", "member"}:
        return 0.0
    line_rate = payload_rate(payload, "coverage_line_rate")
    if line_rate <= 0.0:
        line_rate = payload_rate(payload, "coverage_file_line_rate")
    if line_rate <= 0.0 and record_type == "member":
        line_rate = payload_rate(payload, "coverage_method_line_rate")
    if line_rate <= 0.0:
        return 0.0
    confidence = (line_rate / 100.0) * 12.0
    if scene_intent and str(payload.get("unity_region") or "") == "Runtime":
        confidence += 2.0
    return confidence


def payload_rate(payload: dict, key: str) -> float:
    value = payload.get(key)
    return float(value) if isinstance(value, int | float) else 0.0


def format_coverage_suffix(payload: dict) -> str:
    line_rate = payload_rate(payload, "coverage_line_rate")
    if line_rate <= 0.0:
        line_rate = payload_rate(payload, "coverage_file_line_rate")
    if line_rate <= 0.0 and payload.get("record_type") == "member":
        line_rate = payload_rate(payload, "coverage_method_line_rate")
    if line_rate <= 0.0:
        return ""
    return f" coverage={line_rate:.1f}%"


def is_coverage_query(question: str) -> bool:
    tokens = set(re.findall(r"[a-z0-9_]+", question.lower()))
    return any(term in tokens for term in COVERAGE_QUERY_TERMS)


def select_results(results: list[dict], limit: int, structural: bool) -> list[dict]:
    if not structural:
        return results[:limit]

    selected: list[dict] = []
    type_limits = {
        "namespace": 4,
        "namespace_summary": 4,
        "runtime_summary": 4,
        "coverage_summary": 4,
        "file_overview": 3,
        "assembly": 3,
        "relation": 4,
        "using_directive": 2,
        "type": 3,
        "member": 2,
        "serialized_field": 1,
        "code_convention": 4,
    }
    type_counts: dict[str, int] = {}
    using_paths: set[str] = set()
    seen_keys: set[tuple[str, str, str]] = set()
    for item in results:
        payload = item.get("payload", {})
        record_type = payload.get("record_type", "")
        path = payload.get("path", "")
        identity = payload.get("namespace") or payload.get("using_namespace") or payload.get("stable_key", "")
        seen_key = (record_type, path, identity)
        if seen_key in seen_keys:
            continue
        if type_counts.get(record_type, 0) >= type_limits.get(record_type, limit):
            continue
        if record_type == "using_directive" and path in using_paths:
            continue
        selected.append(item)
        seen_keys.add(seen_key)
        type_counts[record_type] = type_counts.get(record_type, 0) + 1
        if record_type == "using_directive":
            using_paths.add(path)
        if len(selected) >= limit:
            break
    return selected


def camel_words(text: str) -> str:
    tokens = re.findall(r"[A-Z]?[a-z]+|[A-Z]+(?=[A-Z]|$)|\d+", text)
    return " ".join(tokens)
