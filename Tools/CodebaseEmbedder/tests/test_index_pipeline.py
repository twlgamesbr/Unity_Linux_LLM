import json
from pathlib import Path

from codebase_embedder.cli import embed_records_with_cache, write_timing_report
from codebase_embedder.config import CodebaseEmbedderConfig
from codebase_embedder.indexer import build_index
from codebase_embedder.query import build_query_response, format_query_workflow, lexical_query
from codebase_embedder.records import IndexRecord


def _write_coverage_report(project_root: Path, class_name: str, relative_path: str) -> None:
    report_dir = project_root / "CodeCoverage/Report"
    report_dir.mkdir(parents=True, exist_ok=True)
    (report_dir / "Summary.json").write_text(
        json.dumps(
            {
                "summary": {
                    "generatedon": "2026-07-05T16:03:17Z",
                    "linecoverage": 33.8,
                    "methodcoverage": 41.2,
                }
            }
        )
    )
    html = (
        "<!DOCTYPE html>\n"
        "<html><body>\n"
        "<table>\n"
        f"<tr><th>Class:</th><td title=\"{class_name}\">{class_name}</td></tr>\n"
        "<tr><th>Assembly:</th><td title=\"NPCSystem.Runtime\">NPCSystem.Runtime</td></tr>\n"
        f"<tr><th>File(s):</th><td><a>{project_root / relative_path}</a></td></tr>\n"
        "</table>\n"
        "<table>\n"
        "<tr><th>Covered lines:</th><td title=\"8\">8</td></tr>\n"
        "<tr><th>Coverable lines:</th><td title=\"10\">10</td></tr>\n"
        "<tr><th>Total lines:</th><td title=\"14\">14</td></tr>\n"
        "<tr><th>Line coverage:</th><td title=\"8 of 10\">80.0%</td></tr>\n"
        "</table>\n"
        "<table class=\"overview\">\n"
        "<tbody>\n"
        "<tr><td title=\"Bar()\"><a href=\"#file0_line1\">Bar()</a></td><td>100%</td><td>1</td><td>1</td><td>100%</td></tr>\n"
        "<tr><td title=\"Risky()\"><a href=\"#file0_line2\">Risky()</a></td><td>100%</td><td>220</td><td>14</td><td>0%</td></tr>\n"
        "</tbody>\n"
        "</table>\n"
        "</body></html>\n"
    )
    (report_dir / "NPCSystem.Runtime_Foo.html").write_text(html)


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


def test_build_index_enriches_records_with_coverage(tmp_path: Path):
    (tmp_path / "Assets/Scripts/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef").write_text(
        json.dumps({"name": "NPCSystem.Runtime", "rootNamespace": "NPCSystem", "references": []})
    )
    (tmp_path / "Assets/Scripts/Runtime/Foo.cs").write_text(
        "namespace NPCSystem { public class Foo { public void Bar() {} public void Risky() {} } }"
    )
    (tmp_path / "Packages").mkdir()
    (tmp_path / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.test-framework": "1.7.0"}}))
    _write_coverage_report(tmp_path, "NPCSystem.Foo", "Assets/Scripts/Runtime/Foo.cs")

    result = build_index(CodebaseEmbedderConfig(project_root=tmp_path), write_artifacts=True)

    type_record = next(record for record in result.records if record.record_type == "type" and record.payload["type_name"] == "Foo")
    coverage_record = next(record for record in result.records if record.record_type == "coverage_summary")

    assert type_record.payload["coverage_line_rate"] == 80.0
    assert type_record.payload["coverage_method_rate"] == 50.0
    assert type_record.payload["coverage_bucket"] == "high"
    assert coverage_record.payload["coverage_hotspot_methods"] == ["NPCSystem.Foo.Risky"]
    assert result.counts["coverage_classes"] == 1


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


def test_coverage_query_prefers_coverage_summary_records(tmp_path: Path):
    (tmp_path / "Assets/Scripts/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef").write_text(
        json.dumps({"name": "NPCSystem.Runtime", "rootNamespace": "NPCSystem", "references": []})
    )
    (tmp_path / "Assets/Scripts/Runtime/Foo.cs").write_text(
        "namespace NPCSystem { public class Foo { public void Bar() {} public void Risky() {} } }"
    )
    (tmp_path / "Packages").mkdir()
    (tmp_path / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.test-framework": "1.7.0"}}))
    _write_coverage_report(tmp_path, "NPCSystem.Foo", "Assets/Scripts/Runtime/Foo.cs")

    cfg = CodebaseEmbedderConfig(project_root=tmp_path)
    build_index(cfg, write_artifacts=True)
    results = lexical_query(cfg, "what coverage hotspots exist for foo", limit=3)

    assert results
    assert results[0]["payload"]["record_type"] == "coverage_summary"


def test_index_record_point_id_is_stable_across_content_changes():
    first = IndexRecord("type", "type:NPCSystem.Foo:Assets/Scripts/Foo.cs", "old text")
    second = IndexRecord("type", "type:NPCSystem.Foo:Assets/Scripts/Foo.cs", "new text")

    assert first.point_id == second.point_id



def test_owner_query_prefers_type_over_member_for_implemented_prompt(tmp_path: Path):
    (tmp_path / "Assets/LLMUnity/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/LLMUnity/Runtime/undream.llmunity.Runtime.asmdef").write_text(json.dumps({"name": "undream.llmunity.Runtime", "rootNamespace": "LLMUnity", "references": []}))
    (tmp_path / "Assets/LLMUnity/Runtime/LLMClient.cs").write_text(
        "namespace LLMUnity { public class LLMClient { public void Register() {} public void GetNumClients() {} } }"
    )

    cfg = CodebaseEmbedderConfig(project_root=tmp_path)
    build_index(cfg, write_artifacts=True)
    results = lexical_query(cfg, "where is llm client implemented", limit=5)

    assert results
    assert results[0]["payload"]["record_type"] == "type"
    assert results[0]["payload"]["type_name"] == "LLMClient"


def test_query_response_includes_workflow_metadata(tmp_path: Path):
    (tmp_path / "Assets/Scripts/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef").write_text(json.dumps({"name":"NPCSystem.Runtime","rootNamespace":"NPCSystem","references":[]}))
    (tmp_path / "Assets/Scripts/Runtime/Foo.cs").write_text("namespace NPCSystem { public class Foo { public void Bar() {} } }")
    (tmp_path / "Packages").mkdir()
    (tmp_path / "Packages/manifest.json").write_text(json.dumps({"dependencies":{"com.unity.test-framework":"1.7.0"}}))

    cfg = CodebaseEmbedderConfig(project_root=tmp_path)
    build_index(cfg, write_artifacts=True)
    response = build_query_response(cfg, "which scripts reference Foo", limit=3, local=True)

    assert response["workflow"]["query_class"] == "structural"
    assert response["workflow"]["preferred_sources"][0] == "structural_records"
    assert response["results"]


def test_format_query_workflow_mentions_mcp_for_scene_questions():
    text = format_query_workflow("which scene objects use Qdrant")

    assert "scene_integration" in text
    assert "gladekit_mcp_scene_hierarchy" in text


def test_write_timing_report_creates_machine_readable_json(tmp_path: Path):
    path = tmp_path / "timings" / "scan.json"
    cfg = CodebaseEmbedderConfig(project_root=tmp_path, collection_name="benchmark_collection", collection_profile="runtime")

    write_timing_report(
        path,
        command="scan",
        config=cfg,
        timings={"build_index": 0.125},
        counts={"records": 3, "chunks": 2},
        extra={"cache_hits": 1},
    )

    data = json.loads(path.read_text())
    assert data["command"] == "scan"
    assert data["collection"] == "benchmark_collection"
    assert data["profile"] == "runtime"
    assert data["counts"] == {"chunks": 2, "records": 3}
    assert data["timings_seconds"]["build_index"] == 0.125
    assert data["cache_hits"] == 1


def test_embed_records_with_cache_skips_unchanged_records(tmp_path: Path):
    records = [
        IndexRecord("type", "type:A", "same text"),
        IndexRecord("type", "type:B", "new text"),
    ]
    calls: list[list[str]] = []

    class FakeEmbeddingClient:
        model = "fake-embedder"

        def embed(self, texts: list[str]) -> list[list[float]]:
            calls.append(texts)
            return [[float(len(text)), 0.0] for text in texts]

    first_vectors, first_stats = embed_records_with_cache(records, FakeEmbeddingClient(), tmp_path, 2, batch_size=2, use_cache=True)
    second_vectors, second_stats = embed_records_with_cache(records, FakeEmbeddingClient(), tmp_path, 2, batch_size=2, use_cache=True)

    assert first_vectors == second_vectors
    assert first_stats["cache_misses"] == 2
    assert second_stats["cache_hits"] == 2
    assert second_stats["cache_misses"] == 0
    assert calls == [["same text", "new text"]]
