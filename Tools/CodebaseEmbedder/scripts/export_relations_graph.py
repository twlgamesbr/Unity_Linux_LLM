#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path


def load_relations(path: Path) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        rows.append(json.loads(line))
    return rows


def build_graph_rows(relations: list[dict[str, object]], include_kinds: set[str] | None, exclude_calls: bool) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    nodes: dict[str, dict[str, object]] = {}
    edges: list[dict[str, object]] = []
    for relation in relations:
        relation_kind = str(relation.get("relation_kind", ""))
        if exclude_calls and relation_kind == "calls":
            continue
        if include_kinds and relation_kind not in include_kinds:
            continue
        source = str(relation.get("source", ""))
        target = str(relation.get("target", ""))
        payload = relation.get("payload", {})
        path = str(relation.get("path", ""))
        source_node = nodes.setdefault(source, node_row(source))
        target_node = nodes.setdefault(target, node_row(target))
        if isinstance(payload, dict):
            source_node["asmdef"] = source_node["asmdef"] or str(payload.get("asmdef", ""))
            source_node["unity_region"] = source_node["unity_region"] or str(payload.get("unity_region", ""))
            target_node["asmdef"] = target_node["asmdef"] or str(payload.get("asmdef", ""))
            target_node["unity_region"] = target_node["unity_region"] or str(payload.get("unity_region", ""))
        edges.append(
            {
                "source": source,
                "target": target,
                "relation_kind": relation_kind,
                "path": path,
                "asmdef": str(payload.get("asmdef", "")) if isinstance(payload, dict) else "",
                "unity_region": str(payload.get("unity_region", "")) if isinstance(payload, dict) else "",
                "weight": 1,
            }
        )
    return sorted(nodes.values(), key=lambda row: str(row["id"])), edges


def node_row(symbol: str) -> dict[str, object]:
    namespace_name, _, local_name = symbol.rpartition(".")
    return {
        "id": symbol,
        "label": local_name or symbol,
        "namespace": namespace_name,
        "kind": infer_symbol_kind(symbol),
        "asmdef": "",
        "unity_region": "",
    }


def infer_symbol_kind(symbol: str) -> str:
    if symbol.startswith("Assets/"):
        return "asset"
    if "." not in symbol:
        return "assembly_or_namespace"
    tail = symbol.rsplit(".", 1)[-1]
    return "member_or_type" if tail and tail[0].isupper() else "symbol"


def write_csv(path: Path, rows: list[dict[str, object]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def write_summary(path: Path, nodes: list[dict[str, object]], edges: list[dict[str, object]]) -> None:
    relation_counts: dict[str, int] = {}
    for edge in edges:
        relation_kind = str(edge["relation_kind"])
        relation_counts[relation_kind] = relation_counts.get(relation_kind, 0) + 1
    summary = {
        "node_count": len(nodes),
        "edge_count": len(edges),
        "relation_counts": dict(sorted(relation_counts.items())),
    }
    path.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def parse_relation_kinds(raw_values: list[str] | None) -> set[str] | None:
    if not raw_values:
        return None
    kinds = {item.strip() for value in raw_values for item in value.split(",") if item.strip()}
    return kinds or None


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Export CodebaseEmbedder relations.jsonl to graph CSV files.")
    parser.add_argument("--relations", type=Path, default=Path(".codebase-index/relations.jsonl"))
    parser.add_argument("--out-dir", type=Path, default=Path(".codebase-index/graph"))
    parser.add_argument("--include-kind", action="append", default=None)
    parser.add_argument("--exclude-calls", action="store_true")
    args = parser.parse_args(argv)

    relations = load_relations(args.relations)
    include_kinds = parse_relation_kinds(args.include_kind)
    nodes, edges = build_graph_rows(relations, include_kinds, args.exclude_calls)
    write_csv(args.out_dir / "nodes.csv", nodes, ["id", "label", "namespace", "kind", "asmdef", "unity_region"])
    write_csv(args.out_dir / "edges.csv", edges, ["source", "target", "relation_kind", "path", "asmdef", "unity_region", "weight"])
    write_summary(args.out_dir / "summary.json", nodes, edges)
    print(json.dumps({"out_dir": str(args.out_dir), "nodes": len(nodes), "edges": len(edges)}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
