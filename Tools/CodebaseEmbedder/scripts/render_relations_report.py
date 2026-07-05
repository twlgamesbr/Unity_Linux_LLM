#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import html
import json
import math
from pathlib import Path
import re


SVG_WIDTH = 1600
SVG_HEIGHT = 1200
SVG_CENTER_X = SVG_WIDTH / 2
SVG_CENTER_Y = SVG_HEIGHT / 2


def load_nodes(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def load_edges(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def load_stale_cognee_hits(root: Path) -> list[dict[str, str]]:
    patterns = [
        Path("Tools/CodebaseEmbedder"),
        Path("Documentation"),
        Path("README.md"),
        Path(".codebase-index"),
    ]
    hits: list[dict[str, str]] = []
    matcher = re.compile(r"Cognee|cognee")
    for relative in patterns:
        path = root / relative
        if not path.exists():
            continue
        files = [path] if path.is_file() else [candidate for candidate in path.rglob("*") if candidate.is_file()]
        for file_path in files:
            if file_path.suffix in {".png", ".jpg", ".jpeg", ".gif", ".pdf", ".meta"}:
                continue
            try:
                lines = file_path.read_text(encoding="utf-8").splitlines()
            except UnicodeDecodeError:
                continue
            for line_number, line in enumerate(lines, start=1):
                if matcher.search(line):
                    hits.append(
                        {
                            "path": str(file_path.relative_to(root)),
                            "line": str(line_number),
                            "text": line.strip(),
                        }
                    )
    return hits[:120]


def build_focus_graph(nodes: list[dict[str, str]], edges: list[dict[str, str]], focus_terms: list[str]) -> tuple[list[dict[str, str]], list[dict[str, str]]]:
    lowered_terms = [term.lower() for term in focus_terms]
    selected_ids = {
        node["id"]
        for node in nodes
        if any(term in (node["id"] + " " + node["label"]).lower() for term in lowered_terms)
    }
    expanded_ids = set(selected_ids)
    for edge in edges:
        source = edge["source"]
        target = edge["target"]
        if source in selected_ids or target in selected_ids:
            expanded_ids.add(source)
            expanded_ids.add(target)
    graph_nodes = [node for node in nodes if node["id"] in expanded_ids]
    graph_edges = [edge for edge in edges if edge["source"] in expanded_ids and edge["target"] in expanded_ids]
    return graph_nodes, graph_edges


def build_runtime_graph(nodes: list[dict[str, str]], edges: list[dict[str, str]], max_nodes: int) -> tuple[list[dict[str, str]], list[dict[str, str]]]:
    runtime_nodes = [node for node in nodes if node.get("unity_region") == "Runtime"]
    degree: dict[str, int] = {node["id"]: 0 for node in runtime_nodes}
    runtime_ids = set(degree)
    runtime_edges = [edge for edge in edges if edge["source"] in runtime_ids and edge["target"] in runtime_ids]
    adjacency: dict[str, set[str]] = {node_id: set() for node_id in runtime_ids}
    for edge in runtime_edges:
        degree[edge["source"]] = degree.get(edge["source"], 0) + 1
        degree[edge["target"]] = degree.get(edge["target"], 0) + 1
        adjacency[edge["source"]].add(edge["target"])
        adjacency[edge["target"]].add(edge["source"])
    ordered_ids = [node["id"] for node in sorted(runtime_nodes, key=lambda node: (-degree.get(node["id"], 0), node["id"]))]
    selected_ids: set[str] = set()
    for node_id in ordered_ids:
        if len(selected_ids) >= max_nodes:
            break
        selected_ids.add(node_id)
        for neighbor in sorted(adjacency.get(node_id, set()), key=lambda item: (-degree.get(item, 0), item)):
            if len(selected_ids) >= max_nodes:
                break
            selected_ids.add(neighbor)
    selected = [node for node in runtime_nodes if node["id"] in selected_ids]
    selected_ids = {node["id"] for node in selected}
    selected_edges = [edge for edge in runtime_edges if edge["source"] in selected_ids and edge["target"] in selected_ids]
    return selected, selected_edges


def circle_layout(nodes: list[dict[str, str]], radius: float) -> dict[str, tuple[float, float]]:
    if not nodes:
        return {}
    positions: dict[str, tuple[float, float]] = {}
    step = (2 * math.pi) / len(nodes)
    for index, node in enumerate(sorted(nodes, key=lambda item: item["id"])):
        angle = index * step
        x = SVG_CENTER_X + math.cos(angle) * radius
        y = SVG_CENTER_Y + math.sin(angle) * radius
        positions[node["id"]] = (x, y)
    return positions


def draw_graph(title: str, nodes: list[dict[str, str]], edges: list[dict[str, str]]) -> str:
    if not nodes:
        return f"<section><h2>{html.escape(title)}</h2><p>No nodes matched this graph.</p></section>"
    positions = circle_layout(nodes, 420 if len(nodes) < 40 else 500)
    node_index = {node["id"]: node for node in nodes}
    edge_lines = []
    for edge in edges:
        source_position = positions.get(edge["source"])
        target_position = positions.get(edge["target"])
        if source_position is None or target_position is None:
            continue
        color = edge_color(edge["relation_kind"])
        edge_lines.append(
            f"<line x1=\"{source_position[0]:.1f}\" y1=\"{source_position[1]:.1f}\" "
            f"x2=\"{target_position[0]:.1f}\" y2=\"{target_position[1]:.1f}\" "
            f"stroke=\"{color}\" stroke-width=\"1.4\" stroke-opacity=\"0.58\" />"
        )
    node_circles = []
    node_labels = []
    for node_id, (x, y) in positions.items():
        node = node_index[node_id]
        fill = node_color(node)
        label = html.escape(node["label"])
        title_text = html.escape(f"{node['id']} | {node.get('asmdef', '')} | {node.get('unity_region', '')}")
        node_circles.append(
            f"<circle cx=\"{x:.1f}\" cy=\"{y:.1f}\" r=\"7\" fill=\"{fill}\"><title>{title_text}</title></circle>"
        )
        node_labels.append(
            f"<text x=\"{x + 10:.1f}\" y=\"{y + 4:.1f}\" font-size=\"12\" fill=\"#d8e1ec\">{label}</text>"
        )
    return "\n".join(
        [
            f"<section><h2>{html.escape(title)}</h2>",
            f"<p>Nodes: {len(nodes)} | Edges: {len(edges)}</p>",
            f"<svg viewBox=\"0 0 {SVG_WIDTH} {SVG_HEIGHT}\" class=\"graph\">",
            *edge_lines,
            *node_circles,
            *node_labels,
            "</svg></section>",
        ]
    )


def edge_color(relation_kind: str) -> str:
    colors = {
        "asmdef-references": "#7bdff2",
        "namespace-contains-type": "#b2f7ef",
        "namespace-uses-namespace": "#eff7f6",
        "inherits": "#f7d6e0",
        "implements": "#f2b5d4",
        "calls": "#f6bd60",
        "type-contains-member": "#84a59d",
    }
    return colors.get(relation_kind, "#bfc9d4")


def node_color(node: dict[str, str]) -> str:
    asmdef = node.get("asmdef", "")
    if "Runtime" in asmdef:
        return "#7bd389"
    if "Editor" in asmdef:
        return "#f6bd60"
    if "Tests" in asmdef:
        return "#f28482"
    return "#84a59d"


def render_hits_table(hits: list[dict[str, str]]) -> str:
    rows = []
    for hit in hits:
        rows.append(
            "<tr>"
            f"<td>{html.escape(hit['path'])}</td>"
            f"<td>{html.escape(hit['line'])}</td>"
            f"<td>{html.escape(hit['text'])}</td>"
            "</tr>"
        )
    body = "\n".join(rows) if rows else "<tr><td colspan=\"3\">No stale Cognee references found.</td></tr>"
    return (
        "<section><h2>Stale Cognee References</h2>"
        "<p>This list excludes `Assets/Scripts` runtime code. It is intended to show docs, tests, tooling, and index text that still mention Cognee.</p>"
        "<table><thead><tr><th>Path</th><th>Line</th><th>Text</th></tr></thead><tbody>"
        f"{body}</tbody></table></section>"
    )


def render_report(runtime_nodes: list[dict[str, str]], runtime_edges: list[dict[str, str]], focus_nodes: list[dict[str, str]], focus_edges: list[dict[str, str]], stale_hits: list[dict[str, str]]) -> str:
    return (
        "<!DOCTYPE html>\n"
        "<html lang=\"en\">\n"
        "<head>\n"
        "  <meta charset=\"utf-8\" />\n"
        "  <title>Unity Codebase Graph Report</title>\n"
        "  <style>\n"
        "    body { background:#0f1720; color:#e2e8f0; font-family: system-ui, sans-serif; margin:0; padding:24px; }\n"
        "    h1, h2 { margin: 0 0 12px; }\n"
        "    p { color:#b9c7d6; }\n"
        "    section { margin: 0 0 40px; padding:20px; border:1px solid #223042; border-radius:16px; background:#121c28; }\n"
        "    .graph { width:100%; height:auto; background:#0b1118; border-radius:12px; }\n"
        "    table { width:100%; border-collapse:collapse; font-size:14px; }\n"
        "    th, td { text-align:left; padding:8px 10px; border-bottom:1px solid #223042; vertical-align:top; }\n"
        "    code { background:#0b1118; padding:2px 6px; border-radius:6px; }\n"
        "  </style>\n"
        "</head>\n"
        "<body>\n"
        "  <h1>Unity Codebase Graph Report</h1>\n"
        "  <p>Generated from <code>.codebase-index/graph</code>. Runtime graph is structural only and excludes dense call edges. Cognee focus is shown separately so stale references are easy to spot.</p>\n"
        f"  {draw_graph('Runtime Structural Graph', runtime_nodes, runtime_edges)}\n"
        f"  {draw_graph('Cognee / Qdrant / Dialogue Focus Graph', focus_nodes, focus_edges)}\n"
        f"  {render_hits_table(stale_hits)}\n"
        "</body>\n"
        "</html>\n"
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Render a browser-viewable HTML report from exported CodebaseEmbedder graph CSVs.")
    parser.add_argument("--nodes", type=Path, default=Path(".codebase-index/graph/nodes.csv"))
    parser.add_argument("--edges", type=Path, default=Path(".codebase-index/graph/edges.csv"))
    parser.add_argument("--out", type=Path, default=Path(".codebase-index/graph/report.html"))
    parser.add_argument("--root", type=Path, default=Path("."))
    parser.add_argument("--max-runtime-nodes", type=int, default=80)
    args = parser.parse_args(argv)

    nodes = load_nodes(args.nodes)
    edges = load_edges(args.edges)
    runtime_nodes, runtime_edges = build_runtime_graph(nodes, edges, args.max_runtime_nodes)
    focus_nodes, focus_edges = build_focus_graph(nodes, edges, ["Cognee", "Qdrant", "NPCDialogue", "LocalAI", "LLM"])
    stale_hits = load_stale_cognee_hits(args.root.resolve())
    html_text = render_report(runtime_nodes, runtime_edges, focus_nodes, focus_edges, stale_hits)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(html_text, encoding="utf-8")
    print(json.dumps({"out": str(args.out), "runtime_nodes": len(runtime_nodes), "runtime_edges": len(runtime_edges), "focus_nodes": len(focus_nodes), "focus_edges": len(focus_edges), "stale_cognee_hits": len(stale_hits)}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
