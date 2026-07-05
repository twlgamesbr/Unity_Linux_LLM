from __future__ import annotations

from .coverage import CoverageClass, CoverageMethod, CoverageReport
from .records import IndexRecord


def apply_coverage_to_records(records: list[IndexRecord], report: CoverageReport) -> None:
    for record in records:
        _apply_coverage_to_record(record, report)


def build_coverage_summary_records(records: list[IndexRecord], report: CoverageReport, project: str) -> list[IndexRecord]:
    file_overviews = [record for record in records if record.record_type == "file_overview"]
    summaries: list[IndexRecord] = []
    for record in file_overviews:
        payload = record.payload
        relative_path = str(payload.get("path") or "")
        class_entries = report.classes_for_path(relative_path)
        if not class_entries:
            continue
        line_rate = _weighted_line_rate(class_entries)
        method_rate = _weighted_method_rate(class_entries)
        class_names = [entry.full_name for entry in class_entries]
        hotspot_methods = _top_hotspot_methods(class_entries)
        coverage_payload = {
            "project": project,
            "path": relative_path,
            "asmdef": payload.get("asmdef", ""),
            "unity_region": payload.get("unity_region", ""),
            "record_type": "coverage_summary",
            "symbol_kind": "coverage_summary",
            "coverage_source": "unity_code_coverage",
            "coverage_generated_on": report.generated_on,
            "coverage_line_rate": line_rate,
            "coverage_method_rate": method_rate,
            "coverage_bucket": coverage_bucket(line_rate),
            "coverage_class_names": class_names,
            "coverage_hotspot_methods": hotspot_methods,
        }
        lines = [
            f"Coverage summary {relative_path}",
            f"Assembly {coverage_payload['asmdef'] or '-'}",
            f"Region {coverage_payload['unity_region'] or '-'}",
            f"Line coverage {line_rate:.1f}%",
            f"Method coverage {method_rate:.1f}%",
            f"Coverage bucket {coverage_payload['coverage_bucket']}",
            f"Classes: {', '.join(class_names)}",
            f"Hotspots: {', '.join(hotspot_methods) or '-'}",
        ]
        summaries.append(IndexRecord("coverage_summary", f"coverage_summary:{relative_path}", "\n".join(lines), coverage_payload))
    return summaries


def coverage_bucket(rate: float) -> str:
    if rate >= 70.0:
        return "high"
    if rate >= 35.0:
        return "medium"
    if rate > 0.0:
        return "low"
    return "none"


def coverage_rate_for_payload(payload: dict, key: str) -> float:
    value = payload.get(key)
    return float(value) if isinstance(value, int | float) else 0.0


def _apply_coverage_to_record(record: IndexRecord, report: CoverageReport) -> None:
    payload = record.payload
    relative_path = str(payload.get("path") or "")
    class_entries = report.classes_for_path(relative_path)
    if not class_entries:
        return
    payload["coverage_source"] = "unity_code_coverage"
    payload["coverage_generated_on"] = report.generated_on
    payload["coverage_project_line_rate"] = report.line_rate
    payload["coverage_project_method_rate"] = report.method_rate
    payload["coverage_file_line_rate"] = _weighted_line_rate(class_entries)
    payload["coverage_file_method_rate"] = _weighted_method_rate(class_entries)
    payload["coverage_bucket"] = coverage_bucket(float(payload["coverage_file_line_rate"]))
    match record.record_type:
        case "type":
            coverage_class = report.class_by_full_name(_full_type_name(payload))
            if coverage_class is not None:
                _apply_class_coverage(payload, coverage_class)
        case "member":
            coverage_class = report.class_by_full_name(_full_type_name(payload))
            if coverage_class is not None:
                _apply_class_coverage(payload, coverage_class)
                method = _find_method_coverage(coverage_class, str(payload.get("member_name") or ""), int(payload.get("line_start") or 0))
                if method is not None:
                    payload["coverage_method_line_rate"] = method.line_rate
                    payload["coverage_method_bucket"] = coverage_bucket(method.line_rate)
                    payload["coverage_method_line_start"] = method.line_start
                    payload["coverage_method_is_hotspot"] = _is_hotspot_method(method)
                    if method.crap_score is not None:
                        payload["coverage_method_crap_score"] = method.crap_score
                    if method.cyclomatic_complexity is not None:
                        payload["coverage_method_complexity"] = method.cyclomatic_complexity
        case "file_overview" | "runtime_summary":
            payload["coverage_class_names"] = [coverage_class.full_name for coverage_class in class_entries]
            payload["coverage_hotspot_methods"] = _top_hotspot_methods(class_entries)
        case _:
            return


def _apply_class_coverage(payload: dict[str, object], coverage_class: CoverageClass) -> None:
    payload["coverage_class_name"] = coverage_class.full_name
    payload["coverage_line_rate"] = coverage_class.line_rate
    payload["coverage_method_rate"] = coverage_class.method_rate
    payload["coverage_covered_lines"] = coverage_class.covered_lines
    payload["coverage_coverable_lines"] = coverage_class.coverable_lines
    payload["coverage_total_lines"] = coverage_class.total_lines
    payload["coverage_covered_methods"] = coverage_class.covered_methods
    payload["coverage_total_methods"] = coverage_class.total_methods
    payload["coverage_bucket"] = coverage_bucket(coverage_class.line_rate)


def _full_type_name(payload: dict[str, object]) -> str:
    namespace_name = str(payload.get("namespace") or "")
    type_name = str(payload.get("type_name") or "")
    return f"{namespace_name}.{type_name}" if namespace_name else type_name


def _find_method_coverage(coverage_class: CoverageClass, member_name: str, line_start: int) -> CoverageMethod | None:
    for method in coverage_class.methods:
        if method.name == member_name and method.line_start == line_start:
            return method
    for method in coverage_class.methods:
        if method.name == member_name:
            return method
    return None


def _top_hotspot_methods(class_entries: tuple[CoverageClass, ...]) -> list[str]:
    hotspots = []
    for coverage_class in class_entries:
        for method in coverage_class.methods:
            if _is_hotspot_method(method):
                hotspots.append((method.crap_score or 0.0, f"{coverage_class.full_name}.{method.name}"))
    hotspots.sort(reverse=True)
    return [label for _, label in hotspots[:4]]


def _is_hotspot_method(method: CoverageMethod) -> bool:
    return (method.crap_score or 0.0) >= 150.0 or (method.line_rate == 0.0 and (method.cyclomatic_complexity or 0) >= 12)


def _weighted_line_rate(class_entries: tuple[CoverageClass, ...]) -> float:
    coverable_lines = sum(entry.coverable_lines for entry in class_entries)
    covered_lines = sum(entry.covered_lines for entry in class_entries)
    return round((covered_lines / coverable_lines) * 100, 1) if coverable_lines else 0.0


def _weighted_method_rate(class_entries: tuple[CoverageClass, ...]) -> float:
    total_methods = sum(entry.total_methods for entry in class_entries)
    covered_methods = sum(entry.covered_methods for entry in class_entries)
    return round((covered_methods / total_methods) * 100, 1) if total_methods else 0.0
