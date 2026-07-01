import json
from pathlib import Path

from codebase_embedder.config import CodebaseEmbedderConfig
from codebase_embedder.indexer import build_index
from codebase_embedder.query import lexical_query
from codebase_embedder.records import IndexRecord


def test_build_index_writes_artifacts(tmp_path: Path):
    (tmp_path / "Assets/Scripts/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef").write_text(json.dumps({"name":"NPCSystem.Runtime","rootNamespace":"NPCSystem","references":[]}))
    (tmp_path / "Assets/Scripts/Runtime/Foo.cs").write_text("namespace NPCSystem { public class Foo { public void Bar() {} } }")
    (tmp_path / "Packages").mkdir()
    (tmp_path / "Packages/manifest.json").write_text(json.dumps({"dependencies":{"com.unity.test-framework":"1.7.0"}}))

    result = build_index(CodebaseEmbedderConfig(project_root=tmp_path), write_artifacts=True)

    assert result.counts["csharp_files"] == 1
    assert result.counts["asmdef_files"] == 1
    assert (tmp_path / ".codebase-index/chunks.jsonl").exists()
    assert any(r.record_type == "type" and r.payload["type_name"] == "Foo" for r in result.records)
    assert any(r.record_type == "namespace" and r.payload["namespace"] == "NPCSystem" for r in result.records)


def test_structural_query_prefers_namespace_records(tmp_path: Path):
    (tmp_path / "Assets/Scripts/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef").write_text(json.dumps({"name": "NPCSystem.Runtime", "rootNamespace": "NPCSystem", "references": []}))
    (tmp_path / "Assets/Scripts/Runtime/Foo.cs").write_text(
        "using UnityEngine;\n"
        "using LLMUnity;\n"
        "namespace NPCSystem.Dialogue { public class Foo { public void Bar() {} } }\n"
    )
    (tmp_path / "Packages").mkdir()
    (tmp_path / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.test-framework": "1.7.0"}}))

    cfg = CodebaseEmbedderConfig(project_root=tmp_path)
    build_index(cfg, write_artifacts=True)
    results = lexical_query(cfg, "list all namespaces and references in the project", limit=5)

    assert results
    assert results[0]["payload"]["record_type"] in {"namespace", "using_directive", "file_overview", "assembly", "relation"}


def test_index_record_point_id_is_stable_across_content_changes():
    first = IndexRecord("type", "type:NPCSystem.Foo:Assets/Scripts/Foo.cs", "old text")
    second = IndexRecord("type", "type:NPCSystem.Foo:Assets/Scripts/Foo.cs", "new text")

    assert first.point_id == second.point_id
