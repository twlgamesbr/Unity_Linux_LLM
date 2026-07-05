from __future__ import annotations

from dataclasses import dataclass
from html import unescape
import json
from pathlib import Path
import re

_METHOD_ROW_PATTERN = re.compile(
    r'<tr><td title="(?P<title>[^"]+)"><a href="#file0_line(?P<line>\d+)"[^>]*>.*?</a></td>'
    r"<td[^>]*>[^<]*</td><td[^>]*>(?P<crap>[^<]+)</td><td[^>]*>(?P<complexity>[^<]+)</td><td[^>]*>(?P<coverage>[^<]+)</td></tr>",
    re.DOTALL,
)
_CLASS_NAME_PATTERN = re.compile(r"<th>Class:</th>\s*<td[^>]*title=\"([^\"]+)\"", re.DOTALL)
_ASSEMBLY_NAME_PATTERN = re.compile(r"<th>Assembly:</th>\s*<td[^>]*title=\"([^\"]+)\"", re.DOTALL)
_FILE_PATH_PATTERN = re.compile(r"<th>File\(s\):</th>\s*<td[^>]*><a(?: [^>]*)?>([^<]+)</a>", re.DOTALL)
_VALUE_ROW_PATTERN = {
    "covered_lines": re.compile(r"<th>Covered lines:</th>\s*<td[^>]*title=\"(\d+)\"", re.DOTALL),
    "coverable_lines": re.compile(r"<th>Coverable lines:</th>\s*<td[^>]*title=\"(\d+)\"", re.DOTALL),
    "total_lines": re.compile(r"<th>Total lines:</th>\s*<td[^>]*title=\"(\d+)\"", re.DOTALL),
    "covered_methods": re.compile(r"coveredmethods['\"]?\s*[:=]\s*(\d+)", re.DOTALL),
}
_RATE_PATTERN = re.compile(r"<th>Line coverage:</th>\s*<td[^>]*title=\"[^\"]+\">([0-9.]+)%</td>", re.DOTALL)


@dataclass(frozen=True, slots=True)
class CoverageMethod:
    name: str
    line_start: int
    line_rate: float
    crap_score: float | None
    cyclomatic_complexity: int | None


@dataclass(frozen=True, slots=True)
class CoverageClass:
    full_name: str
    assembly_name: str
    relative_path: str
    covered_lines: int
    coverable_lines: int
    total_lines: int
    line_rate: float
    covered_methods: int
    total_methods: int
    method_rate: float
    methods: tuple[CoverageMethod, ...]


@dataclass(frozen=True, slots=True)
class CoverageReport:
    generated_on: str
    line_rate: float
    method_rate: float
    classes: tuple[CoverageClass, ...]

    def class_by_full_name(self, full_name: str) -> CoverageClass | None:
        for coverage_class in self.classes:
            if coverage_class.full_name == full_name:
                return coverage_class
        return None

    def classes_for_path(self, relative_path: str) -> tuple[CoverageClass, ...]:
        return tuple(coverage_class for coverage_class in self.classes if coverage_class.relative_path == relative_path)


def load_coverage_report(project_root: Path, report_dir_name: str) -> CoverageReport | None:
    report_dir = project_root / report_dir_name
    summary_path = report_dir / "Summary.json"
    if not summary_path.exists():
        return None
    raw_summary = json.loads(summary_path.read_text(encoding="utf-8"))
    generated_on = str(raw_summary.get("summary", {}).get("generatedon", ""))
    line_rate = float(raw_summary.get("summary", {}).get("linecoverage", 0.0))
    method_rate = float(raw_summary.get("summary", {}).get("methodcoverage", 0.0))
    classes = []
    for html_path in sorted(report_dir.glob("*.html")) + sorted(report_dir.glob("*.htm")):
        if html_path.name.startswith("index."):
            continue
        coverage_class = _parse_class_page(html_path, project_root)
        if coverage_class is not None:
            classes.append(coverage_class)
    return CoverageReport(
        generated_on=generated_on,
        line_rate=line_rate,
        method_rate=method_rate,
        classes=tuple(classes),
    )


def _parse_class_page(path: Path, project_root: Path) -> CoverageClass | None:
    html = path.read_text(encoding="utf-8")
    class_name_match = _CLASS_NAME_PATTERN.search(html)
    assembly_name_match = _ASSEMBLY_NAME_PATTERN.search(html)
    file_path_match = _FILE_PATH_PATTERN.search(html)
    if class_name_match is None or assembly_name_match is None or file_path_match is None:
        return None
    relative_path = _normalize_project_path(unescape(file_path_match.group(1)), project_root)
    methods = tuple(_parse_method_rows(html))
    covered_lines = _int_value(html, "covered_lines")
    coverable_lines = _int_value(html, "coverable_lines")
    total_lines = _int_value(html, "total_lines")
    covered_methods = sum(1 for method in methods if method.line_rate > 0.0)
    total_methods = len(methods)
    method_rate = round((covered_methods / total_methods) * 100, 1) if total_methods else 0.0
    return CoverageClass(
        full_name=unescape(class_name_match.group(1)),
        assembly_name=unescape(assembly_name_match.group(1)),
        relative_path=relative_path,
        covered_lines=covered_lines,
        coverable_lines=coverable_lines,
        total_lines=total_lines,
        line_rate=_float_rate(html),
        covered_methods=covered_methods,
        total_methods=total_methods,
        method_rate=method_rate,
        methods=methods,
    )


def _parse_method_rows(html: str) -> list[CoverageMethod]:
    methods = []
    for match in _METHOD_ROW_PATTERN.finditer(html):
        title = unescape(match.group("title"))
        methods.append(
            CoverageMethod(
                name=title.split("(", 1)[0],
                line_start=int(match.group("line")),
                line_rate=_parse_percent(match.group("coverage")),
                crap_score=_parse_float(match.group("crap")),
                cyclomatic_complexity=_parse_int(match.group("complexity")),
            )
        )
    return methods


def _normalize_project_path(path_text: str, project_root: Path) -> str:
    absolute_path = Path(path_text)
    try:
        return absolute_path.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return path_text


def _int_value(html: str, field_name: str) -> int:
    match = _VALUE_ROW_PATTERN[field_name].search(html)
    return int(match.group(1)) if match is not None else 0


def _float_rate(html: str) -> float:
    match = _RATE_PATTERN.search(html)
    return float(match.group(1)) if match is not None else 0.0


def _parse_percent(value: str) -> float:
    raw_value = value.strip().removesuffix("%")
    return float(raw_value) if raw_value and raw_value != "N/A" else 0.0


def _parse_float(value: str) -> float | None:
    raw_value = value.strip()
    return float(raw_value) if raw_value and raw_value != "-" else None


def _parse_int(value: str) -> int | None:
    raw_value = value.strip()
    return int(raw_value) if raw_value and raw_value != "-" else None
