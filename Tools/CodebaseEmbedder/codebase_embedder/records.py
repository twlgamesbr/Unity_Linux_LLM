from __future__ import annotations

from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
from typing import Any
from uuid import NAMESPACE_URL, uuid5


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def content_hash(text: str) -> str:
    return "sha256:" + sha256(text.encode("utf-8", errors="ignore")).hexdigest()


def stable_point_id(*parts: object) -> str:
    return str(uuid5(NAMESPACE_URL, "|".join(str(p) for p in parts)))


@dataclass(slots=True)
class IndexRecord:
    record_type: str
    stable_key: str
    text: str
    payload: dict[str, Any] = field(default_factory=dict)
    point_id: str | None = None

    def __post_init__(self) -> None:
        h = content_hash(self.text)
        self.payload.setdefault("content_hash", h)
        self.payload.setdefault("record_type", self.record_type)
        self.payload.setdefault("stable_key", self.stable_key)
        if self.point_id is None:
            self.point_id = stable_point_id(self.stable_key)

    def to_json(self) -> dict[str, Any]:
        return asdict(self)


@dataclass(slots=True)
class RelationRecord:
    relation_kind: str
    source: str
    target: str
    path: str = ""
    payload: dict[str, Any] = field(default_factory=dict)

    def to_index_record(self, project: str) -> IndexRecord:
        stable = f"relation:{self.relation_kind}:{self.source}->{self.target}:{self.path}"
        text = f"{self.source} {self.relation_kind} {self.target} {self.path}"
        payload = {
            "project": project,
            "relation_kind": self.relation_kind,
            "source": self.source,
            "target": self.target,
            "path": self.path,
            **self.payload,
        }
        return IndexRecord("relation", stable, text, payload)


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True), encoding="utf-8")


def write_jsonl(path: Path, records: list[IndexRecord | RelationRecord | dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = []
    for rec in records:
        if hasattr(rec, "to_json"):
            obj = rec.to_json()
        elif isinstance(rec, RelationRecord):
            obj = asdict(rec)
        else:
            obj = rec
        lines.append(json.dumps(obj, sort_keys=True))
    path.write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")
