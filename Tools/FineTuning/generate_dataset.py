import concurrent.futures
import json
import os
import sys
import time
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path
from typing import Any

QDRANT_URL = os.environ.get("QDRANT_URL", "http://localhost:6333")
LOCALAI_URL = os.environ.get("LOCALAI_URL", "http://localhost:8080/v1")
COLLECTION = os.environ.get("COLLECTION", "unity_linux_llm_codebase_v1")
GENERATION_MODEL = os.environ.get("GENERATION_MODEL", "llama-3.2-3b-instruct:q8_0")
OUTPUT_PATH = Path(os.environ.get("OUTPUT_PATH", "Tools/FineTuning/datasets/npc-codebase-qa.jsonl"))

QA_PER_RECORD = int(os.environ.get("QA_PER_RECORD", "2"))
MAX_RECORDS_PER_TYPE = int(os.environ.get("MAX_RECORDS_PER_TYPE", "500"))
MAX_WORKERS = int(os.environ.get("MAX_WORKERS", "4"))

# Skip low-value record types for QA generation
SKIP_TYPES = {"using_directive", "coverage_summary"}

QUESTION_TEMPLATES: dict[str, list[str]] = {
    "member": [
        "Where is the method {signature} defined?",
        "What does the {symbol_kind} {member_name} in {type_name} do?",
        "Which file contains {member_name} in {namespace}?",
        "Show me the implementation of {member_name} in {type_name}",
    ],
    "relation": [
        "What namespace dependencies does {source} have?",
        "Which namespaces does {source} in {path} use?",
    ],
    "type": [
        "Show me the definition of {type_name} in {namespace}",
        "What class is defined in {path}?",
        "Describe the type {type_name} in namespace {namespace}",
    ],
    "namespace": [
        "What namespaces are defined in this project?",
        "List all namespaces in {path}",
    ],
    "doc": [
        "What documentation exists for this codebase?",
        "Summarize the documentation in {path}",
    ],
    "serialized_field": [
        "What serialized fields does {type_name} have?",
        "Where is {member_name} in {type_name} declared?",
    ],
    "file_overview": [
        "What is the purpose of the file at {path}?",
        "Give me an overview of what {path} contains",
    ],
    "assembly": [
        "What assemblies are defined in this project?",
        "Show me all .asmdef assembly definitions",
    ],
    "runtime_summary": [
        "What runtime components are available?",
        "Summarize the runtime architecture",
    ],
}


def qdrant_request(path: str, body: dict[str, Any] | None = None, method: str = "POST") -> dict[str, Any]:
    url = f"{QDRANT_URL}{path}"
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read().decode("utf-8"))


def scroll_all_points(collection: str) -> list[dict[str, Any]]:
    count_result = qdrant_request(f"/collections/{collection}/points/count", {"exact": True})
    total = count_result.get("result", {}).get("count", 0)
    print(f"Collection has {total} points. Fetching all...")
    data = qdrant_request(
        f"/collections/{collection}/points/scroll",
        {"limit": total, "with_payload": True},
    )
    points = data.get("result", {}).get("points", [])
    if not points and total > 0:
        all_points = []
        offset: str | None = None
        while len(all_points) < total:
            body: dict[str, Any] = {"limit": 1000, "with_payload": True}
            if offset is not None:
                body["offset"] = offset
            batch = qdrant_request(f"/collections/{collection}/points/scroll", body).get("result", {}).get("points", [])
            if not batch:
                break
            all_points.extend(batch)
            offset = batch[-1].get("id")
            print(f"  scrolled {len(all_points)}...")
        points = all_points
    print(f"  received {len(points)} points")
    return points


def call_localai(prompt: str) -> str | None:
    body = {
        "model": GENERATION_MODEL,
        "messages": [{"role": "user", "content": prompt}],
        "temperature": 0.3,
        "max_tokens": 256,
        "top_p": 0.9,
    }
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        f"{LOCALAI_URL}/chat/completions",
        data=data,
        method="POST",
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            result = json.loads(resp.read().decode("utf-8"))
        choices = result.get("choices", [])
        if choices and choices[0].get("message", {}).get("content"):
            return choices[0]["message"]["content"].strip()
    except Exception as e:
        pass
    return None


def generate_one(record_type: str, payload: dict[str, Any], question_template: str) -> dict | None:
    context = payload.get("text", "")
    field_fmt = {
        "signature": payload.get("signature", ""),
        "symbol_kind": payload.get("symbol_kind", record_type),
        "member_name": payload.get("member_name", ""),
        "type_name": payload.get("type_name", ""),
        "namespace": payload.get("namespace", ""),
        "path": payload.get("path", ""),
        "source": payload.get("source", ""),
    }
    question = question_template.format(**field_fmt)
    prompt = (
        "You are a Unity C# codebase assistant. Answer concisely using ONLY the provided context.\n\n"
        f"Context:\n{context}\n\nQuestion: {question}\n\nAnswer:"
    )
    answer = call_localai(prompt)
    if not answer:
        return None
    return {
        "conversations": [
            {"from": "human", "value": question},
            {"from": "gpt", "value": answer},
        ],
        "source": record_type,
        "stable_key": payload.get("stable_key", ""),
        "path": payload.get("path", ""),
    }


def main():
    print(f"Qdrant: {QDRANT_URL}/{COLLECTION}")
    print(f"LocalAI: {LOCALAI_URL} model={GENERATION_MODEL}")
    print(f"Workers: {MAX_WORKERS}")
    print(f"Output: {OUTPUT_PATH}")

    all_points = scroll_all_points(COLLECTION)

    grouped: dict[str, list[dict]] = defaultdict(list)
    for p in all_points:
        payload = p.get("payload", {})
        rt = payload.get("record_type", "unknown")
        if rt not in SKIP_TYPES:
            grouped[rt].append(payload)

    print("\nRecord type breakdown (after filtering):")
    for rt, items in sorted(grouped.items(), key=lambda x: -len(x[1])):
        print(f"  {rt}: {len(items)}")

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    total_generated = 0
    total_skipped = 0
    tasks: list[tuple[str, dict, str]] = []

    for rt, items in sorted(grouped.items()):
        templates = QUESTION_TEMPLATES.get(rt, ["What can you tell me about this code?"])
        sampled = items[:MAX_RECORDS_PER_TYPE]
        for payload in sampled:
            for t_idx in range(min(QA_PER_RECORD, len(templates))):
                template = templates[t_idx % len(templates)]
                tasks.append((rt, payload, template))

    print(f"\nGenerating {len(tasks)} QA pairs with {MAX_WORKERS} workers...")

    def worker(task):
        rt, payload, template = task
        return generate_one(rt, payload, template)

    start = time.time()
    with open(OUTPUT_PATH, "w") as out:
        with concurrent.futures.ThreadPoolExecutor(max_workers=MAX_WORKERS) as pool:
            for i, result in enumerate(pool.map(worker, tasks, chunksize=1)):
                if result:
                    out.write(json.dumps(result, ensure_ascii=False) + "\n")
                    out.flush()
                    total_generated += 1
                else:
                    total_skipped += 1
                if (i + 1) % 100 == 0:
                    elapsed = time.time() - start
                    rate = (i + 1) / elapsed
                    print(f"  {i+1}/{len(tasks)} ({rate:.1f}/s, {total_generated} gen, {total_skipped} skip)")

    elapsed = time.time() - start
    print(f"\nDataset generation complete in {elapsed:.0f}s")
    print(f"  Generated: {total_generated}")
    print(f"  Skipped:   {total_skipped}")
    print(f"  Output:    {OUTPUT_PATH.resolve()}")


if __name__ == "__main__":
    main()
