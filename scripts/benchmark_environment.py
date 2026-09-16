#!/usr/bin/env python3
"""
Preserves benchmark host metadata and partitions history by execution environment.
ベンチマークのホスト情報を保持し、実行環境ごとに履歴を分離します。
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


TEXT_FIELDS = (
    "BenchmarkDotNetVersion", "OsVersion", "ProcessorName", "RuntimeVersion",
    "Architecture", "Configuration", "DotNetCliVersion",
)
COUNT_FIELDS = ("PhysicalProcessorCount", "PhysicalCoreCount", "LogicalCoreCount")


def environment_identity(report: dict[str, Any]) -> dict[str, Any]:
    """Validate the host fields used for comparisons.
    比較に使用するホスト情報を検証します。
    """
    host = report.get("HostEnvironmentInfo")
    if not isinstance(host, dict):
        raise ValueError("Benchmark report must contain HostEnvironmentInfo.")
    identity = {}
    for field in TEXT_FIELDS:
        value = host.get(field)
        if not isinstance(value, str) or not value.strip():
            raise ValueError(f"HostEnvironmentInfo.{field} must be a non-empty string.")
        identity[field] = value.strip()
    for field in COUNT_FIELDS:
        value = host.get(field)
        if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
            raise ValueError(f"HostEnvironmentInfo.{field} must be a positive integer.")
        identity[field] = value
    return identity


def environment_suite_name(suite: str, report: dict[str, Any]) -> str:
    """Use the same environment key when reading and publishing history.
    履歴の読込と公開に同じ環境キーを使用します。
    """
    identity = json.dumps(environment_identity(report), sort_keys=True, separators=(",", ":"))
    fingerprint = hashlib.sha256(identity.encode("utf-8")).hexdigest()[:16]
    return f"{suite} [environment:{fingerprint}]"


def combine_reports(directory: Path) -> dict[str, Any]:
    """Combine only reports from one validated environment.
    検証済みの同一環境からのレポートだけを結合します。
    """
    combined: dict[str, Any] = {"Benchmarks": []}
    identity = None
    for path in sorted(directory.glob("*-report-full-compressed.json")):
        report = json.loads(path.read_text(encoding="utf-8"))
        current_identity = environment_identity(report)
        if identity is not None and current_identity != identity:
            raise ValueError("Benchmark reports contain different execution environments.")
        identity = current_identity
        combined["HostEnvironmentInfo"] = report["HostEnvironmentInfo"]
        benchmarks = report.get("Benchmarks")
        if not isinstance(benchmarks, list) or not benchmarks:
            raise ValueError(f"Benchmark report '{path.name}' contains no benchmarks.")
        combined["Benchmarks"].extend(benchmarks)
    if not combined["Benchmarks"]:
        raise ValueError("No benchmark results found.")
    return combined


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-directory", required=True, type=Path)
    parser.add_argument("--policy", required=True, type=Path)
    parser.add_argument("--github-output", required=True, type=Path)
    args = parser.parse_args()
    report = combine_reports(args.results_directory)
    policy = json.loads(args.policy.read_text(encoding="utf-8"))
    suite = environment_suite_name(policy["benchmark_suite"], report)
    (args.results_directory / "combined-report.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8",
    )
    with args.github_output.open("a", encoding="utf-8") as stream:
        stream.write(f"suite={suite}\n")
    print(f"Combined {len(report['Benchmarks'])} benchmark(s) for {suite}.")


if __name__ == "__main__":
    main()
