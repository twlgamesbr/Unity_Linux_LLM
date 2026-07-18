from __future__ import annotations

import json

from .config import CodebaseEmbedderConfig
from .embeddings import EmbeddingClient
from .indexer import load_chunks
from .query_support import (
    camel_words,
    coverage_confidence_boost,
    coverage_record_boost,
    format_coverage_suffix,
    is_coverage_query,
    path_term_boost,
    payload_aliases,
    select_results,
    symbol_name_boost,
)
from .qdrant_store import QdrantStore
from .sparse import compute_sparse_vector, SPARSE_VECTOR_NAME
from .workflow import (
    QUERY_CLASS_BEHAVIOR,
    QUERY_CLASS_OWNERSHIP,
    QUERY_CLASS_SCENE_INTEGRATION,
    QUERY_CLASS_STRUCTURAL,
    classify_prompt,
    query_workflow,
)

STRUCTURAL_TERMS = {
    "namespace", "namespaces", "using", "usings", "reference", "references",
    "asmdef", "dependency", "dependencies", "symbol", "symbols", "class",
    "classes", "method", "methods", "field", "fields", "interface", "interfaces",
    "enum", "enums", "struct", "structs", "list", "enumerate", "every", "all",
}

STRUCTURAL_RECORD_BOOSTS = {
    "namespace": 52.0,
    "namespace_summary": 48.0,
    "type_summary": 44.0,
    "asmdef_summary": 42.0,
    "project_rule": 40.0,
    "runtime_summary": 26.0,
    "coverage_summary": 24.0,
    "using_directive": 14.0,
    "file_overview": 34.0,
    "assembly": 28.0,
    "relation": 26.0,
    "type": 8.0,
    "member": 4.0,
    "serialized_field": 2.0,
    "code_convention": 34.0,
}

GENERIC_QUERY_STOPWORDS = {"script", "scripts", "handle", "handles", "handled", "manage", "manages", "managed"}


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
    query_class = classify_prompt(question)
    structural = query_class == QUERY_CLASS_STRUCTURAL
    owner_intent = query_class == QUERY_CLASS_OWNERSHIP
    behavior_intent = query_class == QUERY_CLASS_BEHAVIOR
    scene_intent = query_class == QUERY_CLASS_SCENE_INTEGRATION
    coverage_intent = is_coverage_query(question)
    results = []
    for rec in load_chunks(config.artifact_dir):
        raw_hay = rec.text + " " + json.dumps(rec.payload) + " " + payload_aliases(rec.payload)
        hay = (raw_hay + " " + camel_words(raw_hay)).lower()
        score = sum(min(hay.count(t), 3) for t in terms)
        if not structural and rec.record_type in {"type", "member", "serialized_field"}:
            score *= 3
        score += _record_type_boost(rec.payload, structural)
        score += _owner_record_boost(rec.payload, owner_intent)
        score += _behavior_record_boost(rec.payload, behavior_intent)
        score += _scene_record_boost(rec.payload, scene_intent)
        score += coverage_record_boost(rec.payload, coverage_intent)
        score += coverage_confidence_boost(rec.payload, behavior_intent, owner_intent, scene_intent)
        score += symbol_name_boost(rec.payload, terms)
        score += path_term_boost(rec.payload, terms)
        if terms and all(t in hay for t in terms):
            score += 25
        if score:
            results.append({"score": float(score), "payload": {**rec.payload, "text": rec.text}})
    # Primary sort: high score first; secondary: Runtime before Editor before other
    results.sort(key=lambda r: (-r["score"], _region_sort_key(r.get("payload", {}))))
    return select_results(results, limit, structural)


def _query_terms(question: str) -> list[str]:
    stopwords = {
        "where", "what", "which", "implemented", "implementation", "with", "that", "this",
        "the", "and", "for", "is", "list", "all", "every", "project", "from", "into", "about",
    } | GENERIC_QUERY_STOPWORDS
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
    query_class = classify_prompt(question)
    structural = query_class == QUERY_CLASS_STRUCTURAL
    owner_intent = query_class == QUERY_CLASS_OWNERSHIP
    behavior_intent = query_class == QUERY_CLASS_BEHAVIOR
    scene_intent = query_class == QUERY_CLASS_SCENE_INTEGRATION
    coverage_intent = is_coverage_query(question)
    emb = EmbeddingClient(config.localai_base_url, config.embedding_model)
    vec = emb.embed([question])[0]
    sparse_vec = compute_sparse_vector(question)
    candidate_limit = max(80 if structural else 50, limit * (20 if structural else 10))
    structural_filter = None
    if structural:
        structural_filter = {
            "must": [
                {
                    "key": "record_type",
                    "match": {
                        "any": ["namespace", "namespace_summary", "runtime_summary", "using_directive", "file_overview", "assembly", "relation", "type", "member", "code_convention", "serialized_field"]
                    },
                }
            ]
        }
    store = QdrantStore(config.qdrant_url, config.collection_name)
    candidates = store.search_hybrid(vec, sparse_vec, limit=candidate_limit, query_filter=structural_filter)
    terms = _query_terms(question)
    for item in candidates:
        payload = item.get("payload", {})
        raw_hay = json.dumps(payload) + " " + payload_aliases(payload)
        hay = (raw_hay + " " + camel_words(raw_hay)).lower()
        lexical_boost = sum(0.03 * min(hay.count(t), 3) for t in terms)
        if terms and all(t in hay for t in terms):
            lexical_boost += 0.20
        lexical_boost += 0.02 * _record_type_boost(payload, structural)
        lexical_boost += 0.02 * _owner_record_boost(payload, owner_intent)
        lexical_boost += 0.02 * _behavior_record_boost(payload, behavior_intent)
        lexical_boost += 0.02 * _scene_record_boost(payload, scene_intent)
        lexical_boost += 0.02 * coverage_record_boost(payload, coverage_intent)
        lexical_boost += 0.02 * coverage_confidence_boost(payload, behavior_intent, owner_intent, scene_intent)
        lexical_boost += 0.02 * symbol_name_boost(payload, terms)
        lexical_boost += 0.02 * path_term_boost(payload, terms)
        item["score"] = float(item.get("score", 0.0)) + lexical_boost
    # Primary sort: high score first; secondary: Runtime before Editor before other
    candidates.sort(key=lambda r: (-r.get("score", 0.0), _region_sort_key(r.get("payload", {}))))
    return select_results(candidates, limit, structural)


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
        coverage = format_coverage_suffix(payload)
        lines.append(f"{i}. score={item.get('score', 0):.3f} {loc} {symbol} {asm} {record_type}{coverage}".strip())
    return "\n".join(lines)


def format_query_workflow(question: str, has_scene_context: bool = False) -> str:
    plan = query_workflow(question, has_scene_context)
    return (
        f"Workflow: {plan['query_class']}\n"
        f"Preferred sources: {', '.join(plan['preferred_sources'])}"
    )


def build_query_response(config: CodebaseEmbedderConfig, question: str, limit: int = 8, local: bool = False) -> dict:
    results = lexical_query(config, question, limit) if local else qdrant_query(config, question, limit)
    workflow = query_workflow(question, has_scene_context=False)
    return {
        "question": question,
        "workflow": workflow,
        "results": results,
    }


def _is_structural_query(question: str) -> bool:
    return classify_prompt(question) == QUERY_CLASS_STRUCTURAL


def _record_type_boost(payload: dict, structural: bool) -> float:
    if not structural:
        return 0.0
    return STRUCTURAL_RECORD_BOOSTS.get(payload.get("record_type", ""), 0.0)


def _is_owner_query(question: str) -> bool:
    return classify_prompt(question) == QUERY_CLASS_OWNERSHIP


def _owner_record_boost(payload: dict, owner_intent: bool) -> float:
    if not owner_intent:
        return 0.0
    boosts = {
        "type": 14.0,
        "file_overview": 12.0,
        "runtime_summary": 8.0,
        "code_convention": 8.0,
        "namespace": 4.0,
        "assembly": 3.0,
        "member": -8.0,
        "serialized_field": -4.0,
    }
    bonus = boosts.get(payload.get("record_type", ""), 0.0)
    region = str(payload.get("unity_region") or "")
    if region == "Runtime":
        bonus += 6.0
    elif region == "Editor":
        bonus -= 6.0
    elif region == "Tests":
        bonus -= 10.0
    type_name = str(payload.get("type_name") or "")
    if payload.get("record_type") == "type" and type_name.endswith(("Request", "Response", "Payload", "Point", "Metadata")):
        bonus -= 16.0
    if type_name.startswith("Test"):
        bonus -= 12.0
    if type_name.endswith("Sample"):
        bonus -= 10.0
    return bonus


def _behavior_record_boost(payload: dict, behavior_intent: bool) -> float:
    if not behavior_intent:
        return 0.0
    boosts = {
        "member": 14.0,
        "type": 10.0,
        "relation": 8.0,
        "runtime_summary": 8.0,
        "file_overview": 6.0,
        "assembly": -4.0,
    }
    bonus = boosts.get(payload.get("record_type", ""), 0.0)
    if str(payload.get("unity_region") or "") == "Runtime":
        bonus += 6.0
    return bonus


def _scene_record_boost(payload: dict, scene_intent: bool) -> float:
    if not scene_intent:
        return 0.0
    boosts = {
        "runtime_summary": 14.0,
        "file_overview": 12.0,
        "type": 10.0,
        "member": 2.0,
        "relation": 6.0,
        "assembly": -2.0,
        "using_directive": -8.0,
    }
    bonus = boosts.get(payload.get("record_type", ""), 0.0)
    region = str(payload.get("unity_region") or "")
    if region == "Runtime":
        bonus += 6.0
    elif region == "Editor":
        bonus -= 4.0
    elif region == "Scene":
        bonus += 8.0
    return bonus
