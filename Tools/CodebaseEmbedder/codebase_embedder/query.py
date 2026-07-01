from __future__ import annotations

import json
import re
from pathlib import Path

from .config import CodebaseEmbedderConfig
from .embeddings import EmbeddingClient
from .indexer import load_chunks
from .qdrant_store import QdrantStore

STRUCTURAL_TERMS = {
    "namespace", "namespaces", "using", "usings", "reference", "references",
    "asmdef", "dependency", "dependencies", "symbol", "symbols", "class",
    "classes", "method", "methods", "field", "fields", "interface", "interfaces",
    "enum", "enums", "struct", "structs", "list", "enumerate", "every", "all",
}

STRUCTURAL_RECORD_BOOSTS = {
    "namespace": 52.0,
    "namespace_summary": 48.0,
    "runtime_summary": 26.0,
    "using_directive": 14.0,
    "file_overview": 34.0,
    "assembly": 28.0,
    "relation": 26.0,
    "type": 8.0,
    "member": 4.0,
}


def _region_sort_key(payload: dict) -> int:
    """Return sort order for unity_region: Runtime=0, Scene=1, Editor=2, other=3."""
    region = payload.get("unity_region", "")
    if region == "Runtime":
        return 0
    if region == "Scene":
        return 1
    if region == "Editor":
        return 2
    return 3


def lexical_query(config: CodebaseEmbedderConfig, question: str, limit: int = 8) -> list[dict]:
    terms = _query_terms(question)
    structural = _is_structural_query(question)
    results = []
    for rec in load_chunks(config.artifact_dir):
        raw_hay = rec.text + " " + json.dumps(rec.payload) + " " + _payload_aliases(rec.payload)
        hay = (raw_hay + " " + _camel_words(raw_hay)).lower()
        score = sum(min(hay.count(t), 3) for t in terms)
        if not structural and rec.record_type in {"type", "member", "serialized_field"}:
            score *= 3
        score += _record_type_boost(rec.payload, structural)
        if terms and all(t in hay for t in terms):
            score += 25
        if score:
            results.append({"score": float(score), "payload": {**rec.payload, "text": rec.text}})
    # Primary sort: high score first; secondary: Runtime before Editor before other
    results.sort(key=lambda r: (-r["score"], _region_sort_key(r.get("payload", {}))))
    return _select_results(results, limit, structural)


def _camel_words(text: str) -> str:
    tokens = re.findall(r"[A-Z]?[a-z]+|[A-Z]+(?=[A-Z]|$)|\d+", text)
    return " ".join(tokens)


def _query_terms(question: str) -> list[str]:
    stopwords = {
        "where", "what", "which", "implemented", "implementation", "with", "that", "this",
        "the", "and", "for", "is", "list", "all", "every", "project", "from", "into", "about",
    }
    terms: list[str] = []
    for raw in question.replace("_", " ").split():
        token = raw.lower().strip(".,:;!?()[]{}\"'")
        if len(token) <= 2 or token in stopwords:
            continue
        if token.endswith("s") and token[:-1] in STRUCTURAL_TERMS:
            token = token[:-1]
        terms.append(token)
    return terms


def qdrant_query(config: CodebaseEmbedderConfig, question: str, limit: int = 8) -> list[dict]:
    structural = _is_structural_query(question)
    emb = EmbeddingClient(config.localai_base_url, config.embedding_model)
    vec = emb.embed([question])[0]
    candidate_limit = max(80 if structural else 50, limit * (20 if structural else 10))
    structural_filter = None
    if structural:
        structural_filter = {
            "must": [
                {
                    "key": "record_type",
                    "match": {
                        "any": ["namespace", "namespace_summary", "runtime_summary", "using_directive", "file_overview", "assembly", "relation", "type", "member"]
                    },
                }
            ]
        }
    candidates = QdrantStore(config.qdrant_url, config.collection_name).search(vec, limit=candidate_limit, query_filter=structural_filter)
    terms = _query_terms(question)
    for item in candidates:
        payload = item.get("payload", {})
        raw_hay = json.dumps(payload) + " " + _payload_aliases(payload)
        hay = (raw_hay + " " + _camel_words(raw_hay)).lower()
        lexical_boost = sum(0.03 * min(hay.count(t), 3) for t in terms)
        if terms and all(t in hay for t in terms):
            lexical_boost += 0.20
        lexical_boost += 0.02 * _record_type_boost(payload, structural)
        item["score"] = float(item.get("score", 0.0)) + lexical_boost
    # Primary sort: high score first; secondary: Runtime before Editor before other
    candidates.sort(key=lambda r: (-r.get("score", 0.0), _region_sort_key(r.get("payload", {}))))
    return _select_results(candidates, limit, structural)


def format_results(results: list[dict]) -> str:
    lines = []
    for i, item in enumerate(results, 1):
        payload = item.get("payload", {})
        path = payload.get("path", "")
        line = payload.get("line_start")
        symbol = payload.get("member_name") or payload.get("type_name") or payload.get("heading") or payload.get("record_type")
        asm = payload.get("asmdef", "")
        record_type = payload.get("record_type", "")
        loc = f"{path}:{line}" if line else path
        lines.append(f"{i}. score={item.get('score', 0):.3f} {loc} {symbol} {asm} {record_type}".strip())
    return "\n".join(lines)


def _is_structural_query(question: str) -> bool:
    q = question.lower()
    return any(term in q for term in STRUCTURAL_TERMS)


def _record_type_boost(payload: dict, structural: bool) -> float:
    if not structural:
        return 0.0
    return STRUCTURAL_RECORD_BOOSTS.get(payload.get("record_type", ""), 0.0)


def _payload_aliases(payload: dict) -> str:
    record_type = payload.get("record_type", "")
    aliases = {
        "namespace": "namespace namespaces declared declaration",
        "namespace_summary": "namespace namespaces hierarchy summary overview types members using directives",
        "runtime_summary": "runtime runtime ownership transport service bootstrapper logger history qdrant cognee scene dialogue",
        "using_directive": "using usings import imports reference references dependency dependencies",
        "file_overview": "file overview namespace namespaces using usings type types member members reference references",
        "assembly": "assembly asmdef reference references dependency dependencies",
        "relation": "relation relations reference references dependency dependencies graph edge",
        "type": "type types class classes interface interfaces enum enums struct structs",
        "member": "member members method methods field fields function functions",
        "serialized_field": "field fields serialized serializefield member members",
    }
    return aliases.get(record_type, "")


def _select_results(results: list[dict], limit: int, structural: bool) -> list[dict]:
    if not structural:
        return results[:limit]

    selected: list[dict] = []
    type_limits = {
        "namespace": 4,
        "namespace_summary": 4,
        "runtime_summary": 4,
        "file_overview": 3,
        "assembly": 3,
        "relation": 4,
        "using_directive": 2,
        "type": 3,
        "member": 2,
        "serialized_field": 1,
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
