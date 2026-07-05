from __future__ import annotations

import importlib.util
import json
from pathlib import Path


SCRIPTS_DIR = Path(__file__).resolve().parents[1] / "scripts"


def _load_script(name: str):
    path = SCRIPTS_DIR / name
    spec = importlib.util.spec_from_file_location(path.stem, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_export_relations_graph_builds_nodes_and_edges(tmp_path: Path):
    exporter = _load_script("export_relations_graph.py")
    relations = [
        {
            "source": "NPCSystem.Runtime",
            "target": "Unity.Netcode.Runtime",
            "relation_kind": "asmdef-references",
            "path": "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef",
            "payload": {"asmdef": "NPCSystem.Runtime", "unity_region": "Runtime"},
        },
        {
            "source": "NPCSystem.QdrantRAGService",
            "target": "NPCSystem.NPCLocalAIEmbedder",
            "relation_kind": "calls",
            "path": "Assets/Scripts/Runtime/NPCDialogue/QdrantRAGService.cs",
            "payload": {"asmdef": "NPCSystem.Runtime", "unity_region": "Runtime"},
        },
    ]

    nodes, edges = exporter.build_graph_rows(relations, include_kinds=None, exclude_calls=True)

    assert len(nodes) == 2
    assert len(edges) == 1
    assert edges[0]["relation_kind"] == "asmdef-references"


def test_export_relations_graph_main_writes_csv_outputs(tmp_path: Path):
    exporter = _load_script("export_relations_graph.py")
    relations_path = tmp_path / "relations.jsonl"
    relations_path.write_text(
        "\n".join(
            [
                json.dumps(
                    {
                        "source": "NPCSystem.QdrantRAGService",
                        "target": "NPCSystem.NPCLocalAIEmbedder",
                        "relation_kind": "calls",
                        "path": "Assets/Scripts/Runtime/NPCDialogue/QdrantRAGService.cs",
                        "payload": {"asmdef": "NPCSystem.Runtime", "unity_region": "Runtime"},
                    }
                ),
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    out_dir = tmp_path / "graph"

    exit_code = exporter.main(["--relations", str(relations_path), "--out-dir", str(out_dir)])

    assert exit_code == 0
    assert (out_dir / "nodes.csv").exists()
    assert (out_dir / "edges.csv").exists()
    assert json.loads((out_dir / "summary.json").read_text())["edge_count"] == 1
