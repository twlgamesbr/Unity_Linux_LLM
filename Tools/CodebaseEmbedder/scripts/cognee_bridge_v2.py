#!/usr/bin/env python3
"""
Cognee Bridge v2 — ingest CodebaseEmbedder enriched chunks into Cognee.

Uses the /api/v1/memify endpoint with runInBackground=true so ingestion
returns immediately. Then polls dataset status until all processing completes.

Usage:
  cd Tools/CodebaseEmbedder
  uv run python3 scripts/cognee_bridge.py --root ../.. --dataset unity_npc_llm_codebase
"""

import argparse
import json
import sys
import time
import urllib.request
import urllib.error
from collections import defaultdict
from pathlib import Path


COGNEE_BASE = "http://localhost:8000/api/v1"


def read_chunks(root: str, max_chars: int = 2500) -> list[tuple[str, str]]:
    """Read chunks.jsonl, group by source file, split into sub-docs ≤ max_chars."""
    path = Path(root) / ".codebase-index" / "chunks.jsonl"
    if not path.exists():
        print(f"ERROR: {path} not found", file=sys.stderr)
        sys.exit(1)

    groups: dict[str, list[str]] = defaultdict(list)
    with open(path) as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            obj = json.loads(line)
            text = obj.get("text", "")
            if text:
                pkey = obj.get("payload", {}).get("path", "unknown")
                groups[pkey].append(text)

    total = sum(len(v) for v in groups.values())
    print(f"Read {total} chunks across {len(groups)} source files")

    docs: list[tuple[str, str]] = []
    for pkey, texts in groups.items():
        safe = pkey.replace("/", "__").replace("\\", "__")
        combined = "\n\n".join(texts)
        if len(combined) <= max_chars:
            docs.append((safe, combined))
        else:
            parts: list[str] = []
            size = 0
            idx = 0
            for t in texts:
                tsize = len(t) + 2
                if size + tsize > max_chars and parts:
                    fn = f"{safe}__p{idx}" if idx else safe
                    docs.append((fn, "\n\n".join(parts)))
                    parts = []
                    size = 0
                    idx += 1
                parts.append(t)
                size += tsize
            if parts:
                fn = f"{safe}__p{idx}" if idx else safe
                docs.append((fn, "\n\n".join(parts)))
    return docs


def memify(content: str, dataset: str) -> tuple[int, str]:
    """Send a single document to Cognee via /api/v1/memify (background)."""
    body = json.dumps({
        "data": content,
        "datasetName": dataset,
        "runInBackground": True,
    }).encode()
    req = urllib.request.Request(f"{COGNEE_BASE}/memify", data=body, method="POST")
    req.add_header("Content-Type", "application/json")
    try:
        resp = urllib.request.urlopen(req, timeout=60)
        result = json.loads(resp.read())
        dskey = list(result.keys())[0]
        status = result[dskey]["status"]
        did = result[dskey]["dataset_id"]
        return (200, f"{status} dataset={did[:16]}...")
    except urllib.error.HTTPError as e:
        return (e.code, e.read().decode()[:150])
    except Exception as e:
        return (500, str(e))


def poll_completion(dataset: str, timeout_secs: int = 1200) -> bool:
    """Poll dataset data count until stable (new docs stop growing)."""
    def get_doc_count():
        try:
            resp = urllib.request.urlopen(f"{COGNEE_BASE}/datasets", timeout=5)
            for d in json.loads(resp.read()):
                if d["name"] == dataset:
                    did = d["id"]
                    dr = urllib.request.urlopen(f"{COGNEE_BASE}/datasets/{did}/data", timeout=5)
                    return len(json.loads(dr.read()))
        except: pass
        return 0

    start = time.time()
    last_count = get_doc_count()
    stable_checks = 0
    while time.time() - start < timeout_secs:
        time.sleep(5)
        count = get_doc_count()
        if count != last_count:
            print(f"  Progress: {count} documents (change: {count - last_count})")
            last_count = count
            stable_checks = 0
        else:
            stable_checks += 1
            if stable_checks >= 3:  # stable for ~15s
                print(f"  Final: {count} documents")
                return True
    print(f"  Timed out ({timeout_secs}s), last count: {last_count}")
    return False


def delete_dataset(dataset: str):
    try:
        resp = urllib.request.urlopen(f"{COGNEE_BASE}/datasets", timeout=5)
        for d in json.loads(resp.read()):
            if d["name"] == dataset:
                req = urllib.request.Request(f"{COGNEE_BASE}/datasets/{d['id']}", method="DELETE")
                urllib.request.urlopen(req, timeout=5)
                print(f"Deleted '{dataset}'")
    except: pass


def main():
    parser = argparse.ArgumentParser(description="Ingest CodebaseEmbedder chunks into Cognee (v2)")
    parser.add_argument("--root", default="../..")
    parser.add_argument("--dataset", default="unity_npc_llm_codebase")
    parser.add_argument("--max-chars", type=int, default=2500)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--concurrency", type=int, default=5, help="Max concurrent memify calls")
    args = parser.parse_args()

    # Force delete
    if args.force:
        delete_dataset(args.dataset)

    # Read and chunk
    docs = read_chunks(args.root, args.max_chars)
    print(f"Built {len(docs)} sub-documents (max {args.max_chars} chars each)")

    if args.dry_run:
        for fn, c in docs[:5]:
            print(f"  {fn}: {len(c)} chars")
        if len(docs) > 5:
            print(f"  ... and {len(docs) - 5} more")
        print(f"\nTotal: {len(docs)} docs, {sum(len(c) for _,c in docs):,} chars")
        sys.exit(0)

    # Send in parallel batches
    print(f"\nSending {len(docs)} docs to Cognee (~{args.concurrency} concurrent)")
    failures = 0
    sent = 0
    total = len(docs)

    # import concurrent.futures
    from concurrent.futures import ThreadPoolExecutor, as_completed

    def send_one(item):
        fn, content = item
        code, msg = memify(content, args.dataset)
        if code == 200:
            return (fn, True, msg)
        return (fn, False, f"HTTP {code}: {msg}")

    with ThreadPoolExecutor(max_workers=args.concurrency) as ex:
        futures = [ex.submit(send_one, d) for d in docs]
        for i, f in enumerate(as_completed(futures)):
            fn, ok, detail = f.result()
            sent += 1
            if ok:
                print(f"  [{sent}/{total}] OK  {fn[:60]}")
            else:
                failures += 1
                print(f"  [{sent}/{total}] FAIL {fn[:60]}: {detail}")

    print(f"\nSent: {sent}/{total}, failures: {failures}")

    # Poll for completion
    if sent > 0:
        print("\nPolling for completion (may take several minutes)...")
        poll_completion(args.dataset, timeout_secs=1200)

    print(f"\nDone. Search with:")
    print(f'  curl -X POST {COGNEE_BASE}/recall -H "Content-Type: application/json" -d \'{{"query":"your query","datasets":["{args.dataset}"],"searchType":"CHUNKS","topK":5}}\'')


if __name__ == "__main__":
    main()
