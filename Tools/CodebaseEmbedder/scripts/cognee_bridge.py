#!/usr/bin/env python3
"""
Cognee Bridge — ingest a compact references-only view of the codebase into Cognee.

Why this version exists:
- The old bridge pushed the full `.codebase-index/chunks.jsonl` corpus through
  `/api/v1/remember`, which was slow on this stack.
- For codebase graph/memory experiments we do not need every code chunk; we only
  need the high-signal structural references already present in
  `.codebase-index/relations.jsonl`.
- On the current local Cognee server, `/api/v1/memify` is dramatically faster
  than `/api/v1/remember` and `/api/v1/search` returns the stored text reliably.

This script therefore:
1. Reads `.codebase-index/relations.jsonl`
2. Keeps only structural relation kinds by default
3. Aggregates them into one small text document per source file
4. Sends each document via `/api/v1/memify`

Default relation kinds:
- asmdef-references
- namespace-uses-namespace
- namespace-contains-type
- inherits
- implements

Typical usage:
  cd Tools/CodebaseEmbedder
  uv run python3 scripts/cognee_bridge.py --root ../.. --dataset unity_npc_llm_references --force

Smoke-test after ingest:
  curl -X POST http://localhost:8000/api/v1/search \
    -H "Content-Type: application/json" \
    -d '{"query":"NPCDialogueManager qdrant references","datasets":["unity_npc_llm_references"],"search_type":"CHUNKS","top_k":5}'
"""

from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path


DEFAULT_RELATION_KINDS = [
    "asmdef-references",
    "namespace-uses-namespace",
    "namespace-contains-type",
    "inherits",
    "implements",
]


def load_relations(root: str, kinds: set[str]) -> dict[str, list[dict]]:
    relations_path = Path(root) / ".codebase-index" / "relations.jsonl"
    if not relations_path.exists():
        print(f"ERROR: relations.jsonl not found at {relations_path}", file=sys.stderr)
        sys.exit(1)

    grouped: dict[str, list[dict]] = defaultdict(list)
    total = 0
    kept = 0
    with relations_path.open(encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            total += 1
            obj = json.loads(line)
            kind = obj.get("relation_kind")
            if kind not in kinds:
                continue
            grouped[obj.get("path", "unknown")].append(obj)
            kept += 1

    print(f"Read {total} relations, kept {kept} high-signal references across {len(grouped)} files")
    return dict(grouped)


def build_reference_doc(path_key: str, relations: list[dict]) -> str:
    asmdef = next((r.get("payload", {}).get("asmdef") for r in relations if r.get("payload", {}).get("asmdef")), "")
    region = next((r.get("payload", {}).get("unity_region") for r in relations if r.get("payload", {}).get("unity_region")), "")

    by_kind: dict[str, list[str]] = defaultdict(list)
    for rel in relations:
        line = f"{rel.get('source', '')} {rel.get('relation_kind', '')} {rel.get('target', '')}".strip()
        by_kind[rel.get("relation_kind", "unknown")].append(line)

    parts = [f"Reference graph for {path_key}"]
    if asmdef:
        parts.append(f"Assembly: {asmdef}")
    if region:
        parts.append(f"Region: {region}")

    for kind in DEFAULT_RELATION_KINDS:
        items = sorted(set(by_kind.get(kind, [])))
        if not items:
            continue
        parts.append("")
        parts.append(f"{kind}:")
        parts.extend(f"- {item}" for item in items)

    return "\n".join(parts)


def split_doc(text: str, max_chars: int) -> list[str]:
    if len(text) <= max_chars:
        return [text]

    lines = text.splitlines()
    docs: list[str] = []
    current: list[str] = []
    current_len = 0
    for line in lines:
        add = len(line) + 1
        if current and current_len + add > max_chars:
            docs.append("\n".join(current))
            current = []
            current_len = 0
        current.append(line)
        current_len += add
    if current:
        docs.append("\n".join(current))
    return docs


def build_documents(root: str, kinds: set[str], max_chars: int, limit: int | None = None) -> list[tuple[str, str]]:
    grouped = load_relations(root, kinds)
    paths = sorted(grouped)
    if limit is not None:
        paths = paths[:limit]

    docs: list[tuple[str, str]] = []
    for path_key in paths:
        text = build_reference_doc(path_key, grouped[path_key])
        safe_name = path_key.replace("/", "__").replace("\\", "__")
        pieces = split_doc(text, max_chars)
        for idx, piece in enumerate(pieces, start=1):
            name = safe_name if len(pieces) == 1 else f"{safe_name}__part{idx}"
            docs.append((name, piece))
    return docs


def memify(cognee_url: str, dataset: str, filename: str, content: str, dry_run: bool = False) -> bool:
    payload = {
        "data": content,
        "datasetName": dataset,
        "runInBackground": False,
    }
    if dry_run:
        print(f"  [DRY-RUN] {filename}: {len(content)} chars")
        return True

    req = urllib.request.Request(
        f"{cognee_url.rstrip('/')}/memify",
        data=json.dumps(payload).encode("utf-8"),
        method="POST",
        headers={"Content-Type": "application/json"},
    )
    try:
        t0 = time.time()
        with urllib.request.urlopen(req, timeout=120) as resp:
            body = json.loads(resp.read())
        elapsed = time.time() - t0
        statuses = []
        if isinstance(body, dict):
            for _, value in body.items():
                if isinstance(value, dict) and value.get("status"):
                    statuses.append(value.get("status"))
        status = ",".join(sorted(set(statuses))) if statuses else "ok"
        print(f"  {filename}: {len(content)} chars → {status} ({elapsed:.2f}s)")
        return True
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")[:400]
        print(f"  {filename} HTTP {e.code}: {detail}", file=sys.stderr)
        return False
    except Exception as e:
        print(f"  {filename} ERROR: {e}", file=sys.stderr)
        return False


def delete_existing_dataset(cognee_url: str, dataset: str) -> bool:
    try:
        with urllib.request.urlopen(f"{cognee_url.rstrip('/')}/datasets", timeout=15) as resp:
            datasets = json.loads(resp.read())
        for d in datasets:
            if d.get("name") == dataset:
                req = urllib.request.Request(f"{cognee_url.rstrip('/')}/datasets/{d['id']}", method="DELETE")
                urllib.request.urlopen(req, timeout=15)
                print(f"Deleted existing dataset '{dataset}'")
                return True
        print(f"Dataset '{dataset}' not found, nothing to delete")
        return True
    except Exception as e:
        print(f"Could not delete dataset '{dataset}': {e}", file=sys.stderr)
        return False


def smoke_search(cognee_url: str, dataset: str, query: str) -> None:
    payload = {
        "query": query,
        "datasets": [dataset],
        "search_type": "CHUNKS",
        "top_k": 3,
    }
    req = urllib.request.Request(
        f"{cognee_url.rstrip('/')}/search",
        data=json.dumps(payload).encode("utf-8"),
        method="POST",
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=60) as resp:
        body = resp.read().decode("utf-8", "replace")
    print("\nSmoke search result:")
    print(body[:2000])


def main() -> None:
    parser = argparse.ArgumentParser(description="Ingest a references-only codebase view into Cognee via /memify")
    parser.add_argument("--root", default="../..", help="Project root (default: ../..)")
    parser.add_argument("--dataset", default="unity_npc_llm_references", help="Cognee dataset name")
    parser.add_argument("--cognee-url", default="http://localhost:8000/api/v1", help="Cognee API base URL")
    parser.add_argument("--max-chars", type=int, default=3500, help="Max chars per generated document")
    parser.add_argument("--limit", type=int, default=None, help="Only ingest the first N files (for quick testing)")
    parser.add_argument("--relation-kinds", nargs="*", default=DEFAULT_RELATION_KINDS, help="Relation kinds to keep")
    parser.add_argument("--force", action="store_true", help="Delete existing dataset before ingesting")
    parser.add_argument("--dry-run", action="store_true", help="Print docs without sending them")
    parser.add_argument("--smoke-query", default="NPCDialogueManager qdrant references", help="Query to run against /search after ingest")
    parser.add_argument("--skip-smoke", action="store_true", help="Skip the final /search smoke test")
    args = parser.parse_args()

    kinds = set(args.relation_kinds)
    if args.force and not args.dry_run:
        delete_existing_dataset(args.cognee_url, args.dataset)
        print()

    docs = build_documents(args.root, kinds, args.max_chars, args.limit)
    if not docs:
        print("No documents built; nothing to send.", file=sys.stderr)
        sys.exit(1)

    total_chars = sum(len(text) for _, text in docs)
    max_doc_chars = max(len(text) for _, text in docs)
    print(f"Built {len(docs)} reference documents")
    print(f"Total content: {total_chars:,} chars, max doc: {max_doc_chars:,} chars")

    failures = 0
    started = time.time()
    for filename, content in docs:
        ok = memify(args.cognee_url, args.dataset, filename, content, dry_run=args.dry_run)
        if not ok:
            failures += 1

    elapsed = time.time() - started
    print(f"\nDone in {elapsed:.2f}s — {len(docs) - failures}/{len(docs)} docs ingested")
    if failures:
        sys.exit(1)

    if not args.dry_run and not args.skip_smoke:
        smoke_search(args.cognee_url, args.dataset, args.smoke_query)


if __name__ == "__main__":
    main()