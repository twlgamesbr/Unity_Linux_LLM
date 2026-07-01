from __future__ import annotations

import json
import time
import urllib.request
from typing import Iterable


class EmbeddingError(RuntimeError):
    pass


class EmbeddingClient:
    def __init__(self, base_url: str, model: str, timeout: float = 30.0):
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.timeout = timeout

    def embed(self, texts: list[str]) -> list[list[float]]:
        payload = json.dumps({"model": self.model, "input": texts}).encode("utf-8")
        req = urllib.request.Request(f"{self.base_url}/embeddings", data=payload, headers={"Content-Type": "application/json"}, method="POST")
        last_error: Exception | None = None
        for attempt in range(3):
            try:
                with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                    data = json.loads(resp.read().decode("utf-8"))
                return [item["embedding"] for item in data.get("data", [])]
            except Exception as exc:  # noqa: BLE001
                last_error = exc
                time.sleep(0.5 * (attempt + 1))
        raise EmbeddingError(f"Embedding request failed at {self.base_url}: {last_error}")

    def dimension(self) -> int:
        vecs = self.embed(["dimension probe"])
        if not vecs or not vecs[0]:
            raise EmbeddingError("Embedding endpoint returned no vector for dimension probe")
        return len(vecs[0])
