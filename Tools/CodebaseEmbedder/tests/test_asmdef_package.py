import json
from pathlib import Path

from codebase_embedder.asmdef_parser import parse_asmdefs, resolve_asmdef_for_file
from codebase_embedder.package_parser import parse_package_manifest


def test_parse_asmdefs_and_resolve_nearest_parent(tmp_path: Path):
    runtime = tmp_path / "Assets/Scripts/Runtime"
    runtime.mkdir(parents=True)
    asmdef = runtime / "NPCSystem.Runtime.asmdef"
    asmdef.write_text(json.dumps({"name":"NPCSystem.Runtime","rootNamespace":"NPCSystem","references":["LLMUnity"]}))
    source = runtime / "Nested/Foo.cs"
    source.parent.mkdir()
    source.write_text("namespace NPCSystem { class Foo {} }")

    assemblies, relations = parse_asmdefs(tmp_path, [asmdef])

    assert assemblies[0].name == "NPCSystem.Runtime"
    assert assemblies[0].root_namespace == "NPCSystem"
    assert relations[0].relation_kind == "asmdef-references"
    assert resolve_asmdef_for_file(source, assemblies).name == "NPCSystem.Runtime"


def test_parse_package_manifest(tmp_path: Path):
    pkg = tmp_path / "Packages"
    pkg.mkdir()
    (pkg / "manifest.json").write_text(json.dumps({"dependencies":{"com.unity.inputsystem":"1.19.0"}}))

    record = parse_package_manifest(tmp_path)

    assert record.payload["package_versions"]["com.unity.inputsystem"] == "1.19.0"
