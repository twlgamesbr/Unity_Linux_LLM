#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


@dataclass
class PromptSpec:
    prompt_id: str
    category: str
    prompt: str
    preferred_record_types: list[str]
    preferred_regions: list[str]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Compare CodebaseEmbedder collections and recommend promotion candidates.")
    parser.add_argument("collections", nargs="+", help="One or more collection names to compare")
    parser.add_argument("--root", default="../..", help="Project root relative to Tools/CodebaseEmbedder")
    parser.add_argument("--config", default=None, help="Path to experiment matrix JSON")
    parser.add_argument("--limit", type=int, default=8, help="Query result limit per prompt")
    parser.add_argument("--output", default=None, help="Optional markdown report path")
    parser.add_argument("--local", action="store_true", help="Use local lexical artifacts instead of live Qdrant queries")
    parser.add_argument("--artifact-root", default=None, help="Override artifact root for --local comparisons. Defaults to the project root.")
    parser.add_argument("--scene", default="Assets/Scenes/NPCDialoguePrototype.unity", help="Unity scene path for audit context in the report")
    return parser.parse_args()


def repo_root_from_script() -> Path:
    return Path(__file__).resolve().parents[4]


def default_config_path() -> Path:
    return Path(__file__).resolve().parents[1] / "references" / "experiment-matrix.json"


def embedder_dir(repo_root: Path) -> Path:
    return repo_root / "Tools" / "CodebaseEmbedder"


def load_matrix(path: Path) -> list[PromptSpec]:
    data = json.loads(path.read_text(encoding="utf-8"))
    prompts = []
    for item in data.get("prompts", []):
        prompts.append(
            PromptSpec(
                prompt_id=item["id"],
                category=item["category"],
                prompt=item["prompt"],
                preferred_record_types=list(item.get("preferred_record_types", [])),
                preferred_regions=list(item.get("preferred_regions", [])),
            )
        )
    return prompts


def run_cli(embedder: Path, args: list[str]) -> subprocess.CompletedProcess[str]:
    env = os.environ.copy()
    env.setdefault("UV_CACHE_DIR", "/tmp/uv-cache")
    env.setdefault("UV_TOOL_DIR", "/tmp/uv-tools")
    return subprocess.run(
        ["uv", "run", "codebase-embedder", *args],
        cwd=embedder,
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )


def query_collection(embedder: Path, root: str, collection: str, prompt: str, limit: int, use_local: bool, artifact_root: str | None) -> list[dict[str, Any]]:
    query_root = artifact_root if use_local and artifact_root else root
    args = ["query", "--root", query_root, "--collection", collection, "--limit", str(limit), "--json"]
    if use_local:
        args.append("--local")
    args.append(prompt)
    result = run_cli(embedder, args)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or f"query failed for {collection}")
    return json.loads(result.stdout)


def score_prompt(results: list[dict[str, Any]], spec: PromptSpec) -> dict[str, Any]:
    total = 0.0
    top = results[: min(5, len(results))]
    winning_types: list[str] = []
    top_runtime_hits = 0

    for rank, item in enumerate(top, start=1):
        payload = item.get("payload", {})
        record_type = payload.get("record_type", "")
        region = payload.get("unity_region", "")
        score = 0.0
        if record_type in spec.preferred_record_types:
            score += max(0.0, 10.0 - (rank - 1) * 1.5)
        if region in spec.preferred_regions:
            score += 2.0
            top_runtime_hits += 1
        if record_type:
            winning_types.append(record_type)
        total += score

    distinct_types = len(set(winning_types))
    if distinct_types >= 3:
        total += 3.0
    elif distinct_types >= 2:
        total += 1.5

    return {
        "prompt_id": spec.prompt_id,
        "prompt": spec.prompt,
        "category": spec.category,
        "score": round(total, 3),
        "top_record_types": winning_types,
        "top_runtime_hits": top_runtime_hits,
        "results": top,
    }


def status_snapshot(embedder: Path, root: str, collection: str) -> dict[str, Any]:
    result = run_cli(embedder, ["status", "--root", root, "--collection", collection])
    return {
        "ok": result.returncode == 0,
        "stdout": result.stdout.strip(),
        "stderr": result.stderr.strip(),
    }


def evaluate_collection(embedder: Path, root: str, collection: str, prompts: list[PromptSpec], limit: int, use_local: bool, artifact_root: str | None) -> dict[str, Any]:
    prompt_scores = []
    failures = []
    for spec in prompts:
        try:
            results = query_collection(embedder, root, collection, spec.prompt, limit, use_local, artifact_root)
            prompt_scores.append(score_prompt(results, spec))
        except Exception as exc:  # noqa: BLE001
            failures.append({"prompt_id": spec.prompt_id, "prompt": spec.prompt, "error": str(exc)})

    total = sum(item["score"] for item in prompt_scores)
    structural = sum(item["score"] for item in prompt_scores if item["category"] == "structural")
    runtime = sum(item["score"] for item in prompt_scores if item["category"] == "runtime")
    return {
        "collection": collection,
        "status": status_snapshot(embedder, root, collection),
        "total_score": round(total, 3),
        "structural_score": round(structural, 3),
        "runtime_score": round(runtime, 3),
        "prompt_scores": prompt_scores,
        "failures": failures,
    }


def recommend(results: list[dict[str, Any]]) -> dict[str, Any] | None:
    ranked = sorted(results, key=lambda item: (-item["total_score"], -item["structural_score"], -item["runtime_score"], item["collection"]))
    return ranked[0] if ranked else None


def report_markdown(repo_root: Path, scene: str, prompts: list[PromptSpec], results: list[dict[str, Any]]) -> str:
    timestamp = datetime.now(timezone.utc).isoformat()
    winner = recommend(results)
    lines = [
        "# Collection Comparison Report",
        "",
        f"- generated_at: `{timestamp}`",
        f"- scene_context: `{scene}`",
        f"- prompt_count: `{len(prompts)}`",
        "",
        "## Ranking",
        "",
    ]

    ranked = sorted(results, key=lambda item: (-item["total_score"], -item["structural_score"], -item["runtime_score"], item["collection"]))
    for index, item in enumerate(ranked, start=1):
        lines.append(
            f"{index}. `{item['collection']}` total=`{item['total_score']}` structural=`{item['structural_score']}` runtime=`{item['runtime_score']}` failures=`{len(item['failures'])}`"
        )

    if winner:
        lines += [
            "",
            "## Recommendation",
            "",
            f"Promote `{winner['collection']}` only if its smoke-query wins match the intended experiment scope and there are no unresolved runtime regressions.",
        ]

    for item in ranked:
        lines += [
            "",
            f"## {item['collection']}",
            "",
            "### Status",
            "",
            "```text",
            item["status"]["stdout"] or item["status"]["stderr"] or "(no output)",
            "```",
            "",
            "### Prompt Scores",
            "",
        ]
        for prompt_score in item["prompt_scores"]:
            lines.append(
                f"- `{prompt_score['prompt_id']}` score=`{prompt_score['score']}` top_types=`{', '.join(prompt_score['top_record_types']) or '-'}`"
            )
        if item["failures"]:
            lines += ["", "### Failures", ""]
            for failure in item["failures"]:
                lines.append(f"- `{failure['prompt_id']}` {failure['error']}")

    return "\n".join(lines) + "\n"


def default_output_path(repo_root: Path) -> Path:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    return repo_root / ".codebase-index" / f"collection-comparison-{stamp}.md"


def main() -> int:
    args = parse_args()
    repo_root = repo_root_from_script()
    embedder = embedder_dir(repo_root)
    config_path = Path(args.config).resolve() if args.config else default_config_path()
    prompts = load_matrix(config_path)

    results = [
        evaluate_collection(embedder, args.root, collection, prompts, args.limit, args.local, args.artifact_root)
        for collection in args.collections
    ]

    report = report_markdown(repo_root, args.scene, prompts, results)
    output_path = Path(args.output).resolve() if args.output else default_output_path(repo_root)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(report, encoding="utf-8")

    json.dump({"collections": results, "report_path": str(output_path)}, sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
