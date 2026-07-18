"""Load .codebaserules.yaml into first-class project_rule index records.

project_rule = policy source of truth (what must hold).
code_convention = per-file compliance evidence (what a file currently does).
"""

from __future__ import annotations

from pathlib import Path

from .records import IndexRecord
from .rules_engine import RuleDef, load_rules


def build_project_rule_records(
    project_root: Path,
    project: str,
    rules_path: Path | None = None,
) -> list[IndexRecord]:
    path = rules_path or (project_root / ".codebaserules.yaml")
    if not path.exists():
        return []
    rules = load_rules(path)
    return [rule_to_index_record(rule, project, path.as_posix()) for rule in rules]


def rule_to_index_record(rule: RuleDef, project: str, rules_path: str) -> IndexRecord:
    description = rule.description or rule.message or rule.title
    text = "\n".join(
        [
            f"Project rule {rule.id}: {rule.title}",
            f"Severity {rule.severity}",
            f"Check type {rule.check}",
            f"AGENTS.md ref {rule.agents_md_ref or '-'}",
            f"Description: {description}",
            f"Pattern: {rule.pattern or '(roslyn/structural)'}",
            f"Source {rules_path}",
        ]
    )
    payload = {
        "project": project,
        "path": rules_path,
        "rule_id": rule.id,
        "title": rule.title,
        "severity": rule.severity,
        "agents_md_ref": rule.agents_md_ref,
        "check": rule.check,
        "pattern": rule.pattern,
        "description": description,
        "message": rule.message,
        "symbol_kind": "project_rule",
        "unity_region": "Docs",
    }
    return IndexRecord("project_rule", f"project_rule:{rule.id}", text, payload)
