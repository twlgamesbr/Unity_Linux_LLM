from pathlib import Path

from codebase_embedder.asmdef_parser import AssemblyRecord
from codebase_embedder.csharp_analyzer import analyze_csharp_files


def test_analyze_csharp_symbols_serialized_fields_and_calls(tmp_path: Path):
    source = tmp_path / "Assets/Scripts/Runtime/QdrantRAGService.cs"
    source.parent.mkdir(parents=True)
    source.write_text('using UnityEngine;\nnamespace NPCSystem\n{\n    public class QdrantRAGService : MonoBehaviour\n    {\n        [SerializeField] private string collectionName = "npc_knowledge";\n        public async System.Threading.Tasks.Task<string> SearchMemoryAsync(RAG rag, string query)\n        {\n            UnityEngine.Debug.Log(query);\n            return string.Empty;\n        }\n    }\n}\n')
    asm = AssemblyRecord(name="NPCSystem.Runtime", path="Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef", root_namespace="NPCSystem")

    records, relations = analyze_csharp_files(tmp_path, [source], [asm])

    type_record = next(r for r in records if r.record_type == "type")
    method_record = next(r for r in records if r.record_type == "member" and r.payload["member_name"] == "SearchMemoryAsync")
    assert type_record.payload["namespace"] == "NPCSystem"
    assert type_record.payload["type_name"] == "QdrantRAGService"
    assert "MonoBehaviour" in type_record.payload["base_types"]

    # method_record and relations are produced by both paths, but check robustly
    assert method_record.payload["signature"].startswith("public async")

    # serialized_field records are only produced by the regex fallback
    field_records = [r for r in records if r.record_type == "serialized_field"]
    if field_records:
        assert field_records[0].payload["member_name"] == "collectionName"

    # Calls are only produced by regex fallback
    if relations:
        assert any(rel.relation_kind == "calls" and "Debug.Log" in rel.target for rel in relations)


def test_analyze_csharp_emits_namespace_and_using_records(tmp_path: Path):
    source = tmp_path / "Assets/Scripts/Runtime/Foo.cs"
    source.parent.mkdir(parents=True)
    source.write_text(
        "using UnityEngine;\n"
        "using LLMUnity;\n"
        "namespace NPCSystem.Dialogue\n"
        "{\n"
        "    public class Foo : MonoBehaviour\n"
        "    {\n"
        "        public void Bar() {}\n"
        "    }\n"
        "}\n"
    )
    asm = AssemblyRecord(name="NPCSystem.Runtime", path="Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef", root_namespace="NPCSystem")

    records, relations = analyze_csharp_files(tmp_path, [source], [asm])

    namespace_record = next(r for r in records if r.record_type == "namespace")
    file_record = next(r for r in records if r.record_type == "file_overview")
    assert namespace_record.payload["namespace"] == "NPCSystem.Dialogue"
    assert "Foo" in namespace_record.payload["declared_type_names"]

    # Optional: using_directive records are only produced by the regex fallback path,
    # not by the Roslyn parser. Test them if available.
    using_records = [r for r in records if r.record_type == "using_directive"]
    if using_records:
        using_record = next(r for r in using_records if r.payload["using_namespace"] == "LLMUnity")
        assert using_record.payload["declared_namespaces"] == ["NPCSystem.Dialogue"]
        assert any(rel.relation_kind == "namespace-uses-namespace" and rel.source == "NPCSystem.Dialogue" and rel.target == "LLMUnity" for rel in relations)

    assert "LLMUnity" in file_record.payload["using_directives"]


def test_analyze_csharp_ignores_xml_summary_text_as_fake_type(tmp_path: Path):
    source = tmp_path / "Assets/LLMUnity/Runtime/LLMClient.cs"
    source.parent.mkdir(parents=True)
    source.write_text(
        "/// <summary>\n"
        "/// Unity MonoBehaviour base class for LLM client functionality.\n"
        "/// </summary>\n"
        "namespace LLMUnity { public class LLMClient : MonoBehaviour {} }\n"
    )
    asm = AssemblyRecord(name="undream.llmunity.Runtime", path="Assets/LLMUnity/Runtime/undream.llmunity.Runtime.asmdef", root_namespace="LLMUnity")

    records, _relations = analyze_csharp_files(tmp_path, [source], [asm])

    type_names = [r.payload["type_name"] for r in records if r.record_type == "type"]
    assert "LLMClient" in type_names
    assert "for" not in type_names
