from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path


DEFAULT_EXCLUDE_DIRS = {
    ".git", ".venv", "Library", "Temp", "Logs", "UserSettings", "obj", "bin",
    "Build", "Builds", ".codebase-index", "node_modules", "__pycache__",
    # Exclude AI Agent memory, chat sessions, logs, and workspace snapshots
    # to avoid contaminating the main codebase RAG collection with conversational histories.
    ".agents", ".claude", ".gemini", ".gladekit", ".herdr", ".hermes", ".omo", ".opencode", ".vscode",
}


@dataclass(slots=True)
class CodebaseEmbedderConfig:
    project_root: Path | str = Path.cwd()
    project_slug: str = "Unity_Linux_LLM"
    qdrant_url: str = "http://localhost:6333"
    collection_name: str = "unity_linux_llm_codebase_v2"
    localai_base_url: str = "http://localhost:8080/v1"
    embedding_model: str = "nomic-embed-text-v1.5"
    artifact_dir_name: str = ".codebase-index"
    coverage_report_dir_name: str = "CodeCoverage/Report"
    collection_profile: str = "runtime"
    include_scenes: bool = False
    include_samples: bool = False
    include_package_cache: bool = False
    exclude_dirs: set[str] = field(default_factory=lambda: set(DEFAULT_EXCLUDE_DIRS))

    def __post_init__(self) -> None:
        self.project_root = Path(self.project_root).resolve()
        if self.include_package_cache:
            self.exclude_dirs.discard("Library")

    @property
    def artifact_dir(self) -> Path:
        return self.project_root / self.artifact_dir_name
