from __future__ import annotations

import hashlib
import re
from collections import Counter
from typing import Any

# Code-aware stop words — keep project-relevant short tokens, drop English noise
STOP_WORDS: frozenset[str] = frozenset({
    "the", "a", "an", "is", "are", "was", "were", "be", "been",
    "being", "have", "has", "had", "do", "does", "did", "will",
    "would", "could", "should", "may", "might", "shall", "can",
    "to", "of", "in", "for", "on", "with", "at", "by", "from",
    "as", "into", "through", "during", "before", "after", "above",
    "below", "between", "out", "off", "over", "under", "again",
    "further", "then", "once", "here", "there", "when", "where",
    "why", "how", "all", "each", "every", "both", "few", "more",
    "most", "other", "some", "such", "no", "nor", "not", "only",
    "own", "same", "so", "than", "too", "very", "just", "because",
    "but", "and", "or", "if", "while", "that", "this", "these",
    "those", "it", "its", "he", "she", "they", "them", "their",
    "get", "set", "has", "had", "got", "let", "put", "use", "using",
    "new", "old", "one", "two", "three", "also", "well", "back",
    "make", "made", "takes", "took", "taken", "called", "calls",
    "see", "seen", "saw", "look", "looks", "looking", "like",
    "want", "needs", "need", "tried", "try", "trying", "done",
    "going", "go", "goes", "went", "come", "came", "coming",
    "know", "knows", "known", "take", "takes", "took", "taken",
    "say", "says", "said", "think", "thinks", "thought",
    "public", "private", "protected", "internal", "static",
    "virtual", "override", "abstract", "sealed", "partial",
    "readonly", "async", "await", "void", "int", "string", "bool",
    "float", "double", "long", "char", "byte", "short", "var",
    "null", "true", "false", "this", "base", "return", "throw",
    "new", "typeof", "nameof", "sizeof", "class", "struct",
    "interface", "enum", "record", "delegate", "event", "namespace",
    "using", "import", "from", "where", "select",
    "get", "set", "value", "init", "add", "remove",
})

SPARSE_VECTOR_NAME = "code_keywords"


def _token_index(token: str, max_index: int = 2_000_000) -> int:
    """Deterministic token → integer index via SHA-256 prefix.

    Stable across interpreter runs and platforms, which Python's built-in
    ``hash()`` is not. The 2M-dimension space keeps collisions negligible
    for a codebase vocabulary of a few thousand unique terms.
    """
    h = hashlib.sha256(token.encode("utf-8")).hexdigest()[:8]
    return int(h, 16) % max_index


def compute_sparse_vector(text: str) -> dict[str, list[int] | list[float]]:
    """Compute a TF-normalised sparse vector for Qdrant's named-sparse format.

    Returns ``{"indices": [int, ...], "values": [float, ...]}`` where each
    index is a deterministic hash of a token and its value is the term
    frequency divided by the maximum frequency in the document (so the most
    frequent token always has value 1.0).  Tokens shorter than 2 characters
    and English-noise words are dropped.
    """
    tokens = re.findall(r"\b[a-zA-Z_][a-zA-Z0-9_]*\b", text)
    tokens = [t.lower() for t in tokens]
    tokens = [t for t in tokens if len(t) > 1 and t not in STOP_WORDS]

    if not tokens:
        return {"indices": [0], "values": [0.0]}

    freq = Counter(tokens)
    max_freq = max(freq.values())

    # Collisions: tokens mapping to the same index sum their TF values.
    kv: dict[int, float] = {}
    for token, count in freq.items():
        idx = _token_index(token)
        kv[idx] = kv.get(idx, 0.0) + count / max_freq

    indices = sorted(kv.keys())
    values = [kv[i] for i in indices]

    return {"indices": indices, "values": values}


def compute_sparse_vectors(texts: list[str]) -> list[dict[str, Any]]:
    """Batch version of :func:`compute_sparse_vector`."""
    return [compute_sparse_vector(t) for t in texts]
