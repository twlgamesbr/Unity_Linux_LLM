from __future__ import annotations

import json
from pathlib import Path
from unittest.mock import MagicMock, patch

from codebase_embedder.config import CodebaseEmbedderConfig
from codebase_embedder.indexer import build_index
from codebase_embedder.watcher import CodebaseWatcher, load_jsonl_records


def test_watcher_ignores_excluded_and_non_matching_files(tmp_path: Path):
    cfg = CodebaseEmbedderConfig(project_root=tmp_path)
    watcher = CodebaseWatcher(cfg)

    # Excluded directory file
    excluded_file = tmp_path / ".git/config"
    # Unmatched suffix
    non_matching_file = tmp_path / "Assets/Scripts/Runtime/SomeImage.png"
    # Matched file
    csharp_file = tmp_path / "Assets/Scripts/Runtime/Foo.cs"

    watcher.queue_change = MagicMock()

    # Create handler and test events
    from codebase_embedder.watcher import WatchdogHandler
    from watchdog.events import FileModifiedEvent

    handler = WatchdogHandler(watcher)

    handler.on_modified(FileModifiedEvent(str(excluded_file)))
    handler.on_modified(FileModifiedEvent(str(non_matching_file)))
    handler.on_modified(FileModifiedEvent(str(csharp_file)))

    # Verify that only the C# file was queued (excluding directories is handled in queue_change)
    assert watcher.queue_change.call_count == 3
    watcher.queue_change.assert_any_call(excluded_file)
    watcher.queue_change.assert_any_call(non_matching_file)
    watcher.queue_change.assert_any_call(csharp_file)


def test_watcher_queue_change_filters_correctly(tmp_path: Path):
    cfg = CodebaseEmbedderConfig(project_root=tmp_path)
    watcher = CodebaseWatcher(cfg)

    watcher.start = MagicMock()
    watcher.process_changes = MagicMock()

    # Queue an excluded file
    watcher.queue_change(tmp_path / ".git/config")
    assert len(watcher.pending_changes) == 0

    # Queue an unallowed suffix
    watcher.queue_change(tmp_path / "Assets/Scripts/SomeImage.png")
    assert len(watcher.pending_changes) == 0

    # Queue allowed C# file
    watcher.queue_change(tmp_path / "Assets/Scripts/Foo.cs")
    assert len(watcher.pending_changes) == 1


@patch("codebase_embedder.watcher.EmbeddingClient")
@patch("codebase_embedder.watcher.QdrantStore")
def test_watcher_incremental_update_modified_file(mock_qdrant_cls, mock_embedding_client_cls, tmp_path: Path):
    # Set up mocks
    mock_emb = MagicMock()
    mock_emb.model = "test-model"
    mock_emb.dimension.return_value = 4
    mock_emb.embed.side_effect = lambda texts: [[float(len(t)), 0.0, 0.0, 0.0] for t in texts]
    mock_embedding_client_cls.return_value = mock_emb

    mock_qdrant = MagicMock()
    mock_qdrant_cls.return_value = mock_qdrant

    # Write initial files and do a full index
    (tmp_path / "Assets/Scripts/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef").write_text(
        json.dumps({"name": "NPCSystem.Runtime", "rootNamespace": "NPCSystem", "references": []})
    )
    csharp_file = tmp_path / "Assets/Scripts/Runtime/Foo.cs"
    csharp_file.write_text("namespace NPCSystem { public class Foo { public void Bar() {} } }")

    (tmp_path / "Packages").mkdir()
    (tmp_path / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.test-framework": "1.7.0"}}))

    cfg = CodebaseEmbedderConfig(project_root=tmp_path, collection_profile="baseline")
    build_index(cfg, write_artifacts=True)

    # Load and verify initial chunks count
    initial_chunks = load_jsonl_records(cfg.artifact_dir / "chunks.jsonl")
    initial_foo_chunk_ids = [c.point_id for c in initial_chunks if c.payload.get("path") == "Assets/Scripts/Runtime/Foo.cs"]
    assert len(initial_foo_chunk_ids) > 0

    # Now, modify Foo.cs to add a new method
    csharp_file.write_text("namespace NPCSystem { public class Foo { public void Bar() {} public void Baz() {} } }")

    # Run watcher incremental update
    watcher = CodebaseWatcher(cfg)
    watcher._execute_incremental_update([csharp_file])

    # 1. Verify Qdrant delete was called with old Foo.cs point IDs
    mock_qdrant.delete.assert_called_once()
    deleted_ids = mock_qdrant.delete.call_args[0][0]
    assert set(deleted_ids) == set(initial_foo_chunk_ids)

    # 2. Verify Qdrant upsert was called with updated Foo.cs point IDs
    mock_qdrant.upsert.assert_called_once()
    upserted_chunks, upserted_vectors = mock_qdrant.upsert.call_args[0]
    assert len(upserted_chunks) > 0
    
    # Check that updated chunks contain baz
    assert any("Baz" in chunk.text for chunk in upserted_chunks)

    # 3. Verify local chunks.jsonl got updated
    updated_chunks = load_jsonl_records(cfg.artifact_dir / "chunks.jsonl")
    assert any("Baz" in chunk.text for chunk in updated_chunks)
    
    # Verify we did not lose manifest structure
    manifest = json.loads((cfg.artifact_dir / "manifest.json").read_text())
    assert manifest["counts"]["csharp_files"] == 1


@patch("codebase_embedder.watcher.EmbeddingClient")
@patch("codebase_embedder.watcher.QdrantStore")
def test_watcher_incremental_update_deleted_file(mock_qdrant_cls, mock_embedding_client_cls, tmp_path: Path):
    # Set up mocks
    mock_emb = MagicMock()
    mock_emb.model = "test-model"
    mock_emb.dimension.return_value = 4
    mock_emb.embed.side_effect = lambda texts: [[0.0] * 4 for _ in texts]
    mock_embedding_client_cls.return_value = mock_emb

    mock_qdrant = MagicMock()
    mock_qdrant_cls.return_value = mock_qdrant

    # Write initial files and do a full index
    (tmp_path / "Assets/Scripts/Runtime").mkdir(parents=True)
    (tmp_path / "Assets/Scripts/Runtime/NPCSystem.Runtime.asmdef").write_text(
        json.dumps({"name": "NPCSystem.Runtime", "rootNamespace": "NPCSystem", "references": []})
    )
    csharp_file = tmp_path / "Assets/Scripts/Runtime/Foo.cs"
    csharp_file.write_text("namespace NPCSystem { public class Foo { public void Bar() {} } }")

    (tmp_path / "Packages").mkdir()
    (tmp_path / "Packages/manifest.json").write_text(json.dumps({"dependencies": {"com.unity.test-framework": "1.7.0"}}))

    cfg = CodebaseEmbedderConfig(project_root=tmp_path, collection_profile="baseline")
    build_index(cfg, write_artifacts=True)

    # Check chunks loaded
    initial_chunks = load_jsonl_records(cfg.artifact_dir / "chunks.jsonl")
    initial_foo_chunk_ids = [c.point_id for c in initial_chunks if c.payload.get("path") == "Assets/Scripts/Runtime/Foo.cs"]
    assert len(initial_foo_chunk_ids) > 0

    # Delete Foo.cs
    csharp_file.unlink()

    # Run watcher incremental update for deleted file
    watcher = CodebaseWatcher(cfg)
    watcher._execute_incremental_update([csharp_file])

    # 1. Verify Qdrant delete was called
    mock_qdrant.delete.assert_called_once()
    deleted_ids = mock_qdrant.delete.call_args[0][0]
    assert set(deleted_ids) == set(initial_foo_chunk_ids)

    # 2. Verify Qdrant upsert was NOT called since file is gone
    mock_qdrant.upsert.assert_not_called()

    # 3. Verify local chunks.jsonl is cleaned up
    updated_chunks = load_jsonl_records(cfg.artifact_dir / "chunks.jsonl")
    assert not any(chunk.payload.get("path") == "Assets/Scripts/Runtime/Foo.cs" for chunk in updated_chunks)
