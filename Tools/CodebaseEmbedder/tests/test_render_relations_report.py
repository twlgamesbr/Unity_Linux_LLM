from __future__ import annotations

import importlib.util
from pathlib import Path


SCRIPTS_DIR = Path(__file__).resolve().parents[1] / "scripts"


def _load_script(name: str):
    path = SCRIPTS_DIR / name
    spec = importlib.util.spec_from_file_location(path.stem, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_render_relations_report_main_writes_html(tmp_path: Path):
    render = _load_script("render_relations_report.py")
    graph_dir = tmp_path / ".codebase-index" / "graph"
    graph_dir.mkdir(parents=True)
    (graph_dir / "nodes.csv").write_text(
        "id,label,namespace,kind,asmdef,unity_region\n"
        "NPCSystem.QdrantRAGService,QdrantRAGService,NPCSystem,member_or_type,NPCSystem.Runtime,Runtime\n"
        "NPCSystem.NPCDialogueManager,NPCDialogueManager,NPCSystem,member_or_type,NPCSystem.Runtime,Runtime\n",
        encoding="utf-8",
    )
    (graph_dir / "edges.csv").write_text(
        "source,target,relation_kind,path,asmdef,unity_region,weight\n"
        "NPCSystem.NPCDialogueManager,NPCSystem.QdrantRAGService,namespace-contains-type,Assets/Scripts/Runtime/NPCDialogue/NPCDialogueManager.cs,NPCSystem.Runtime,Runtime,1\n",
        encoding="utf-8",
    )
    out_path = graph_dir / "report.html"

    exit_code = render.main(
        [
            "--nodes", str(graph_dir / "nodes.csv"),
            "--edges", str(graph_dir / "edges.csv"),
            "--out", str(out_path),
            "--root", str(tmp_path),
            "--max-runtime-nodes", "10",
        ]
    )

    assert exit_code == 0
    html = out_path.read_text(encoding="utf-8")
    assert "Unity Codebase Graph Report" in html
    assert "QdrantRAGService" in html
