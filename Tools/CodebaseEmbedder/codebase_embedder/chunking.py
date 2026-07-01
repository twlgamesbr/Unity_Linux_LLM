from __future__ import annotations

from pathlib import Path

from .records import IndexRecord


def chunk_docs(project: str, root: Path, docs: list[Path], max_chars: int = 3000) -> list[IndexRecord]:
    records: list[IndexRecord] = []
    for path in docs:
        text = path.read_text(encoding="utf-8", errors="ignore")
        rel = path.relative_to(root).as_posix()
        start = 0
        part = 0
        while start < len(text):
            chunk = text[start:start + max_chars]
            heading = next((line.strip("# ").strip() for line in chunk.splitlines() if line.startswith("#")), Path(rel).name)
            records.append(IndexRecord("doc", f"doc:{rel}:{part}", chunk, {"project": project, "path": rel, "heading": heading, "chunk_index": part, "unity_region": "Docs"}))
            part += 1
            start += max_chars
    return records


def records_to_embedding_chunks(records: list[IndexRecord]) -> list[IndexRecord]:
    # v1 embeds each normalized record directly, then removes exact logical
    # duplicates so Qdrant point counts match artifact counts.
    unique: dict[str, IndexRecord] = {}
    for record in records:
        unique.setdefault(record.point_id or record.stable_key, record)
    return list(unique.values())
