#!/usr/bin/env python3
"""
Checks BenchmarkDotNet results against compatible hosted-runner history.
BenchmarkDotNet の結果を互換性のある hosted runner 履歴と比較します。
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import math
import os
import re
import statistics
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any


BENCHMARK_DATA_PREFIX = "window.BENCHMARK_DATA ="
COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")


class GateError(RuntimeError):
    """Raised when benchmark inputs or policy are unsafe to evaluate."""


@dataclass(frozen=True)
class ThresholdGroup:
    """Thresholds shared by benchmarks with comparable observed stability."""

    name: str
    patterns: tuple[str, ...]
    warning_percent: float
    failure_percent: float
    observed_max_slowdown_percent: float


@dataclass(frozen=True)
class Policy:
    """Validated benchmark regression policy."""

    benchmark_suite: str
    baseline_revision: int
    legacy_baseline_revision: int
    minimum_compatible_samples: int
    history_window: int
    definition_roots: tuple[str, ...]
    definition_files: tuple[str, ...]
    groups: tuple[ThresholdGroup, ...]


@dataclass(frozen=True)
class BenchmarkValue:
    """One benchmark measurement."""

    name: str
    value: float
    unit: str


@dataclass(frozen=True)
class HistoryEntry:
    """One trusted benchmark-history entry."""

    commit: str
    date: float
    benchmarks: tuple[BenchmarkValue, ...]


@dataclass(frozen=True)
class Outcome:
    """Evaluation result for one benchmark."""

    benchmark: str
    group: str
    sample_count: int
    baseline: float | None
    current: float
    unit: str
    change_percent: float | None
    warning_percent: float
    failure_percent: float
    status: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Compare BenchmarkDotNet means with compatible trusted history."
    )
    parser.add_argument("--current-report", required=True, help="Combined BenchmarkDotNet JSON report.")
    parser.add_argument("--history-data", required=True, help="github-action-benchmark data.js file.")
    parser.add_argument("--policy", required=True, help="Evidence-based threshold policy JSON.")
    parser.add_argument(
        "--baseline-ancestor",
        required=True,
        help="Trusted base commit; history entries must be ancestors of this commit.",
    )
    parser.add_argument(
        "--repository-root",
        default=".",
        help="Git repository used to validate benchmark-definition compatibility.",
    )
    parser.add_argument(
        "--summary",
        help="Markdown summary destination. Defaults to GITHUB_STEP_SUMMARY when available.",
    )
    parser.add_argument(
        "--allow-failure",
        choices=("true", "false"),
        default="false",
        help="Report failures without returning exit 1 during an intentional baseline publication.",
    )
    return parser.parse_args()


def read_json(path: Path, description: str) -> dict[str, Any]:
    try:
        contents = path.read_text(encoding="utf-8")
    except OSError as error:
        raise GateError(f"Unable to read {description} '{path}': {error}") from error

    try:
        value = json.loads(contents)
    except json.JSONDecodeError as error:
        raise GateError(f"{description} '{path}' is not valid JSON: {error}") from error

    if not isinstance(value, dict):
        raise GateError(f"{description} '{path}' must contain a JSON object.")
    return value


def safe_repository_path(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value:
        raise GateError(f"{field} must contain non-empty repository-relative paths.")

    path = PurePosixPath(value)
    if path.is_absolute() or ".." in path.parts or value.startswith("-"):
        raise GateError(f"{field} contains unsafe repository path '{value}'.")
    return path.as_posix()


def numeric_percent(value: Any, field: str) -> float:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        raise GateError(f"{field} must be numeric.")
    number = float(value)
    if not math.isfinite(number) or number < 0:
        raise GateError(f"{field} must be a finite non-negative number.")
    return number


def positive_integer(value: Any, field: str) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise GateError(f"{field} must be a positive integer.")
    return value


def load_policy(path: Path) -> Policy:
    payload = read_json(path, "benchmark policy")
    if payload.get("schema_version") != 1:
        raise GateError("benchmark policy schema_version must be 1.")

    suite = payload.get("benchmark_suite")
    if not isinstance(suite, str) or not suite:
        raise GateError("benchmark_suite must be a non-empty string.")

    baseline_revision = payload.get("baseline_revision")
    legacy_revision = payload.get("legacy_baseline_revision")
    if not isinstance(baseline_revision, int) or baseline_revision < 0:
        raise GateError("baseline_revision must be a non-negative integer.")
    if not isinstance(legacy_revision, int) or legacy_revision < 0:
        raise GateError("legacy_baseline_revision must be a non-negative integer.")

    definitions = payload.get("benchmark_definition")
    if not isinstance(definitions, dict):
        raise GateError("benchmark_definition must be an object.")

    roots_value = definitions.get("roots")
    files_value = definitions.get("files")
    if not isinstance(roots_value, list) or not roots_value:
        raise GateError("benchmark_definition.roots must be a non-empty array.")
    if not isinstance(files_value, list):
        raise GateError("benchmark_definition.files must be an array.")

    roots = tuple(safe_repository_path(value, "benchmark_definition.roots") for value in roots_value)
    files = tuple(safe_repository_path(value, "benchmark_definition.files") for value in files_value)

    groups_value = payload.get("groups")
    if not isinstance(groups_value, list) or not groups_value:
        raise GateError("groups must be a non-empty array.")

    groups: list[ThresholdGroup] = []
    group_names: set[str] = set()
    for index, group_value in enumerate(groups_value):
        field = f"groups[{index}]"
        if not isinstance(group_value, dict):
            raise GateError(f"{field} must be an object.")

        name = group_value.get("name")
        patterns_value = group_value.get("patterns")
        if not isinstance(name, str) or not name:
            raise GateError(f"{field}.name must be a non-empty string.")
        if name in group_names:
            raise GateError(f"Duplicate threshold group '{name}'.")
        if not isinstance(patterns_value, list) or not patterns_value:
            raise GateError(f"{field}.patterns must be a non-empty array.")
        if not all(isinstance(pattern, str) and pattern for pattern in patterns_value):
            raise GateError(f"{field}.patterns must contain non-empty strings.")

        warning = numeric_percent(group_value.get("warning_percent"), f"{field}.warning_percent")
        failure = numeric_percent(group_value.get("failure_percent"), f"{field}.failure_percent")
        observed = numeric_percent(
            group_value.get("observed_max_slowdown_percent"),
            f"{field}.observed_max_slowdown_percent",
        )
        if warning <= observed:
            raise GateError(
                f"{field}.warning_percent must exceed its observed normal slowdown ({observed:.1f}%)."
            )
        if failure <= warning:
            raise GateError(f"{field}.failure_percent must exceed warning_percent.")
        if failure >= 200:
            raise GateError(f"{field}.failure_percent must remain below the retired 200% ceiling.")

        groups.append(
            ThresholdGroup(
                name=name,
                patterns=tuple(patterns_value),
                warning_percent=warning,
                failure_percent=failure,
                observed_max_slowdown_percent=observed,
            )
        )
        group_names.add(name)

    return Policy(
        benchmark_suite=suite,
        baseline_revision=baseline_revision,
        legacy_baseline_revision=legacy_revision,
        minimum_compatible_samples=positive_integer(
            payload.get("minimum_compatible_samples"),
            "minimum_compatible_samples",
        ),
        history_window=positive_integer(payload.get("history_window"), "history_window"),
        definition_roots=roots,
        definition_files=files,
        groups=tuple(groups),
    )


def parse_positive_number(value: Any, field: str) -> float:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        raise GateError(f"{field} must be numeric.")
    number = float(value)
    if not math.isfinite(number) or number <= 0:
        raise GateError(f"{field} must be a finite positive number.")
    return number


def load_current_report(path: Path) -> dict[str, BenchmarkValue]:
    payload = read_json(path, "current benchmark report")
    benchmarks_value = payload.get("Benchmarks")
    if not isinstance(benchmarks_value, list) or not benchmarks_value:
        raise GateError("Current benchmark report must contain a non-empty Benchmarks array.")

    benchmarks: dict[str, BenchmarkValue] = {}
    for index, item in enumerate(benchmarks_value):
        if not isinstance(item, dict):
            raise GateError(f"Benchmarks[{index}] must be an object.")
        name = item.get("FullName")
        statistics_value = item.get("Statistics")
        if not isinstance(name, str) or not name:
            raise GateError(f"Benchmarks[{index}].FullName must be a non-empty string.")
        if not isinstance(statistics_value, dict):
            raise GateError(f"Benchmarks[{index}].Statistics must be an object.")
        if name in benchmarks:
            raise GateError(f"Current benchmark report contains duplicate benchmark '{name}'.")

        benchmarks[name] = BenchmarkValue(
            name=name,
            value=parse_positive_number(
                statistics_value.get("Mean"),
                f"Benchmarks[{index}].Statistics.Mean",
            ),
            unit="ns",
        )
    return benchmarks


def load_history(path: Path, suite: str) -> list[HistoryEntry]:
    try:
        contents = path.read_text(encoding="utf-8").strip()
    except OSError as error:
        raise GateError(f"Unable to read benchmark history '{path}': {error}") from error

    if not contents.startswith(BENCHMARK_DATA_PREFIX):
        raise GateError("Benchmark history must start with the expected BENCHMARK_DATA assignment.")
    json_contents = contents[len(BENCHMARK_DATA_PREFIX) :].strip()
    if json_contents.endswith(";"):
        json_contents = json_contents[:-1].rstrip()

    try:
        payload = json.loads(json_contents)
    except json.JSONDecodeError as error:
        raise GateError(f"Benchmark history is not valid JSON data: {error}") from error
    if not isinstance(payload, dict):
        raise GateError("Benchmark history payload must be an object.")

    entries_value = payload.get("entries")
    if not isinstance(entries_value, dict):
        raise GateError("Benchmark history must contain an entries object.")
    suite_entries = entries_value.get(suite)
    if not isinstance(suite_entries, list):
        raise GateError(f"Benchmark history does not contain suite '{suite}'.")

    entries: list[HistoryEntry] = []
    for index, entry_value in enumerate(suite_entries):
        if not isinstance(entry_value, dict):
            raise GateError(f"History entry {index} must be an object.")
        if entry_value.get("tool") != "benchmarkdotnet":
            raise GateError(f"History entry {index} is not a BenchmarkDotNet result.")
        commit_value = entry_value.get("commit")
        commit = commit_value.get("id") if isinstance(commit_value, dict) else None
        if not isinstance(commit, str) or not COMMIT_PATTERN.fullmatch(commit):
            raise GateError(f"History entry {index} has an invalid full commit SHA.")
        date = parse_positive_number(entry_value.get("date"), f"History entry {index}.date")
        benches_value = entry_value.get("benches")
        if not isinstance(benches_value, list) or not benches_value:
            raise GateError(f"History entry {index} must contain benchmark values.")

        benchmarks: list[BenchmarkValue] = []
        names: set[str] = set()
        for bench_index, benchmark_value in enumerate(benches_value):
            if not isinstance(benchmark_value, dict):
                raise GateError(f"History entry {index} benchmark {bench_index} must be an object.")
            name = benchmark_value.get("name")
            unit = benchmark_value.get("unit")
            if not isinstance(name, str) or not name:
                raise GateError(f"History entry {index} benchmark {bench_index} has no name.")
            if not isinstance(unit, str) or not unit:
                raise GateError(f"History entry {index} benchmark {bench_index} has no unit.")
            if name in names:
                raise GateError(f"History entry {index} contains duplicate benchmark '{name}'.")
            benchmarks.append(
                BenchmarkValue(
                    name=name,
                    value=parse_positive_number(
                        benchmark_value.get("value"),
                        f"History entry {index} benchmark {bench_index}.value",
                    ),
                    unit=unit,
                )
            )
            names.add(name)

        entries.append(HistoryEntry(commit=commit, date=date, benchmarks=tuple(benchmarks)))

    return sorted(entries, key=lambda entry: entry.date)


def run_git(repository_root: Path, arguments: list[str], *, allow_failure: bool = False) -> bytes | None:
    result = subprocess.run(
        ["git", *arguments],
        cwd=repository_root,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode == 0:
        return result.stdout
    if allow_failure:
        return None
    stderr = result.stderr.decode("utf-8", errors="replace").strip()
    raise GateError(f"git {' '.join(arguments)} failed: {stderr or 'unknown error'}")


def fingerprint_contents(contents: dict[str, bytes]) -> str:
    digest = hashlib.sha256()
    for path in sorted(contents):
        digest.update(path.encode("utf-8"))
        digest.update(b"\0")
        digest.update(contents[path])
        digest.update(b"\0")
    return digest.hexdigest()


def current_definition_fingerprint(repository_root: Path, policy: Policy) -> str:
    selectors = [*policy.definition_roots, *policy.definition_files]
    output = run_git(
        repository_root,
        ["ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", *selectors],
    )
    assert output is not None
    paths = sorted(
        path.decode("utf-8")
        for path in output.split(b"\0")
        if path
    )
    for required_file in policy.definition_files:
        if required_file not in paths:
            raise GateError(f"Benchmark definition file '{required_file}' is missing.")
    for root in policy.definition_roots:
        prefix = f"{root.rstrip('/')}/"
        if not any(path == root or path.startswith(prefix) for path in paths):
            raise GateError(f"Benchmark definition root '{root}' contains no files.")

    contents: dict[str, bytes] = {}
    for path in paths:
        resolved = (repository_root / path).resolve()
        try:
            resolved.relative_to(repository_root)
        except ValueError as error:
            raise GateError(f"Benchmark definition path '{path}' escapes the repository.") from error
        try:
            contents[path] = resolved.read_bytes()
        except OSError as error:
            raise GateError(f"Unable to read benchmark definition '{path}': {error}") from error
    return fingerprint_contents(contents)


def historical_definition_fingerprint(
    repository_root: Path,
    commit: str,
    policy: Policy,
) -> str | None:
    selectors = [*policy.definition_roots, *policy.definition_files]
    output = run_git(
        repository_root,
        ["ls-tree", "-r", "--name-only", "-z", commit, "--", *selectors],
        allow_failure=True,
    )
    if output is None:
        return None

    paths = sorted(
        path.decode("utf-8")
        for path in output.split(b"\0")
        if path
    )
    if any(required_file not in paths for required_file in policy.definition_files):
        return None
    for root in policy.definition_roots:
        prefix = f"{root.rstrip('/')}/"
        if not any(path == root or path.startswith(prefix) for path in paths):
            return None

    contents: dict[str, bytes] = {}
    for path in paths:
        content = run_git(
            repository_root,
            ["show", f"{commit}:{path}"],
            allow_failure=True,
        )
        if content is None:
            return None
        contents[path] = content
    return fingerprint_contents(contents)


def historical_baseline_revision(
    repository_root: Path,
    commit: str,
    policy_relative_path: str,
    legacy_revision: int,
) -> int | None:
    contents = run_git(
        repository_root,
        ["show", f"{commit}:{policy_relative_path}"],
        allow_failure=True,
    )
    if contents is None:
        return legacy_revision
    try:
        payload = json.loads(contents.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None
    revision = payload.get("baseline_revision") if isinstance(payload, dict) else None
    return revision if isinstance(revision, int) and revision >= 0 else None


def compatible_history(
    repository_root: Path,
    policy_path: Path,
    policy: Policy,
    entries: list[HistoryEntry],
    baseline_ancestor: str,
) -> tuple[list[HistoryEntry], dict[str, int], str]:
    if not COMMIT_PATTERN.fullmatch(baseline_ancestor):
        raise GateError("baseline ancestor must be a full 40-character lowercase commit SHA.")
    baseline_exists = run_git(
        repository_root,
        ["cat-file", "-e", f"{baseline_ancestor}^{{commit}}"],
        allow_failure=True,
    )
    if baseline_exists is None:
        raise GateError(f"baseline ancestor commit '{baseline_ancestor}' is unavailable.")

    try:
        policy_relative_path = policy_path.resolve().relative_to(repository_root).as_posix()
    except ValueError as error:
        raise GateError("Benchmark policy must be inside the repository root.") from error

    current_fingerprint = current_definition_fingerprint(repository_root, policy)
    compatible: list[HistoryEntry] = []
    exclusions = {
        "missing_commit": 0,
        "not_base_ancestor": 0,
        "definition_mismatch": 0,
        "revision_mismatch": 0,
    }
    fingerprint_cache: dict[str, str | None] = {}
    revision_cache: dict[str, int | None] = {}

    for entry in entries:
        commit_exists = run_git(
            repository_root,
            ["cat-file", "-e", f"{entry.commit}^{{commit}}"],
            allow_failure=True,
        )
        if commit_exists is None:
            exclusions["missing_commit"] += 1
            continue

        is_base_ancestor = run_git(
            repository_root,
            ["merge-base", "--is-ancestor", entry.commit, baseline_ancestor],
            allow_failure=True,
        )
        if is_base_ancestor is None:
            exclusions["not_base_ancestor"] += 1
            continue

        if entry.commit not in fingerprint_cache:
            fingerprint_cache[entry.commit] = historical_definition_fingerprint(
                repository_root,
                entry.commit,
                policy,
            )
        if fingerprint_cache[entry.commit] != current_fingerprint:
            exclusions["definition_mismatch"] += 1
            continue

        if entry.commit not in revision_cache:
            revision_cache[entry.commit] = historical_baseline_revision(
                repository_root,
                entry.commit,
                policy_relative_path,
                policy.legacy_baseline_revision,
            )
        if revision_cache[entry.commit] != policy.baseline_revision:
            exclusions["revision_mismatch"] += 1
            continue

        compatible.append(entry)

    return compatible[-policy.history_window :], exclusions, current_fingerprint


def resolve_group(name: str, groups: tuple[ThresholdGroup, ...]) -> ThresholdGroup:
    matches = [
        group
        for group in groups
        if any(fnmatch.fnmatchcase(name, pattern) for pattern in group.patterns)
    ]
    if len(matches) != 1:
        matched_names = ", ".join(group.name for group in matches) or "none"
        raise GateError(
            f"Benchmark '{name}' must match exactly one threshold group; matched: {matched_names}."
        )
    return matches[0]


def meets_threshold(change_percent: float, threshold_percent: float) -> bool:
    return change_percent > threshold_percent or math.isclose(
        change_percent,
        threshold_percent,
        rel_tol=1e-12,
        abs_tol=1e-9,
    )


def evaluate(
    current: dict[str, BenchmarkValue],
    entries: list[HistoryEntry],
    policy: Policy,
) -> list[Outcome]:
    outcomes: list[Outcome] = []
    matched_groups: set[str] = set()
    for name in sorted(current):
        measurement = current[name]
        group = resolve_group(name, policy.groups)
        matched_groups.add(group.name)
        samples = [
            benchmark.value
            for entry in entries
            for benchmark in entry.benchmarks
            if benchmark.name == name and benchmark.unit == measurement.unit
        ]

        if len(samples) < policy.minimum_compatible_samples:
            outcomes.append(
                Outcome(
                    benchmark=name,
                    group=group.name,
                    sample_count=len(samples),
                    baseline=None,
                    current=measurement.value,
                    unit=measurement.unit,
                    change_percent=None,
                    warning_percent=group.warning_percent,
                    failure_percent=group.failure_percent,
                    status="WARMUP",
                )
            )
            continue

        baseline = statistics.median(samples)
        change_percent = ((measurement.value / baseline) - 1.0) * 100.0
        if meets_threshold(change_percent, group.failure_percent):
            status = "FAIL"
        elif meets_threshold(change_percent, group.warning_percent):
            status = "WARNING"
        else:
            status = "PASS"

        outcomes.append(
            Outcome(
                benchmark=name,
                group=group.name,
                sample_count=len(samples),
                baseline=baseline,
                current=measurement.value,
                unit=measurement.unit,
                change_percent=change_percent,
                warning_percent=group.warning_percent,
                failure_percent=group.failure_percent,
                status=status,
            )
        )

    unmatched_groups = [group.name for group in policy.groups if group.name not in matched_groups]
    if unmatched_groups:
        raise GateError(
            "Threshold groups matched no current benchmarks: "
            + ", ".join(unmatched_groups)
            + "."
        )
    return outcomes


def markdown_cell(value: str) -> str:
    return value.replace("\\", "\\\\").replace("|", "\\|").replace("\n", " ")


def format_measurement(value: float | None, unit: str) -> str:
    if value is None:
        return "—"
    return f"{value:,.2f} {unit}"


def build_summary(
    outcomes: list[Outcome],
    compatible_count: int,
    exclusions: dict[str, int],
    fingerprint: str,
    policy: Policy,
    allow_failure: bool,
    baseline_ancestor: str,
) -> str:
    failures = [outcome for outcome in outcomes if outcome.status == "FAIL"]
    warnings = [outcome for outcome in outcomes if outcome.status == "WARNING"]
    warmups = [outcome for outcome in outcomes if outcome.status == "WARMUP"]

    if failures:
        overall = "FAIL"
        marker = "❌"
    elif warnings:
        overall = "WARNING"
        marker = "⚠️"
    elif warmups:
        overall = "WARMUP"
        marker = "⚠️"
    else:
        overall = "PASS"
        marker = "✅"

    lines = [
        "## Performance regression gate",
        "",
        f"{marker} **{overall}** — {compatible_count} compatible hosted-runner sample(s); "
        f"median baseline; revision `{policy.baseline_revision}`.",
        "",
        f"- Intended base commit: `{baseline_ancestor}`",
        f"- Definition fingerprint: `{fingerprint[:16]}`",
        f"- History window: newest {policy.history_window} compatible samples",
        f"- Excluded history: definition mismatch {exclusions['definition_mismatch']}, "
        f"revision mismatch {exclusions['revision_mismatch']}, "
        f"not an intended-base ancestor {exclusions['not_base_ancestor']}, "
        f"missing commit {exclusions['missing_commit']}",
    ]
    if allow_failure:
        lines.append(
            "- Intentional baseline publication mode is enabled: failures remain visible but do not block this run."
        )
    if warmups:
        lines.append(
            f"- {len(warmups)} benchmark(s) have fewer than "
            f"{policy.minimum_compatible_samples} compatible samples and are reported as WARMUP."
        )

    lines.extend(
        [
            "",
            "| Benchmark | Group | Samples | Baseline median | Current mean | Change | Warning | Failure | Result |",
            "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |",
        ]
    )
    for outcome in outcomes:
        change = "—" if outcome.change_percent is None else f"{outcome.change_percent:+.1f}%"
        lines.append(
            "| "
            + " | ".join(
                [
                    markdown_cell(outcome.benchmark),
                    markdown_cell(outcome.group),
                    str(outcome.sample_count),
                    format_measurement(outcome.baseline, outcome.unit),
                    format_measurement(outcome.current, outcome.unit),
                    change,
                    f"{outcome.warning_percent:.0f}%",
                    f"{outcome.failure_percent:.0f}%",
                    outcome.status,
                ]
            )
            + " |"
        )

    lines.extend(
        [
            "",
            f"Warnings: {len(warnings)}. Failures: {len(failures)}. Warmups: {len(warmups)}.",
        ]
    )
    return "\n".join(lines) + "\n"


def build_error_summary(error: GateError) -> str:
    return "\n".join(
        [
            "## Performance regression gate",
            "",
            f"❌ **CONFIGURATION ERROR:** {error}",
            "",
        ]
    )


def append_summary(path_value: str | None, summary: str) -> None:
    if not path_value:
        return
    path = Path(path_value)
    try:
        with path.open("a", encoding="utf-8") as stream:
            stream.write(summary)
    except OSError as error:
        raise GateError(f"Unable to append GitHub job summary '{path}': {error}") from error


def emit_annotations(outcomes: list[Outcome]) -> None:
    for outcome in outcomes:
        if outcome.status not in {"WARNING", "FAIL"}:
            continue
        command = "error" if outcome.status == "FAIL" else "warning"
        change = outcome.change_percent if outcome.change_percent is not None else 0.0
        print(
            f"::{command} title=Benchmark {outcome.status}::{outcome.benchmark} "
            f"slowed by {change:.1f}% "
            f"({outcome.warning_percent:.0f}% warning / {outcome.failure_percent:.0f}% failure)."
        )


def main() -> int:
    args = parse_args()
    summary_path = args.summary or os.environ.get("GITHUB_STEP_SUMMARY")
    allow_failure = args.allow_failure == "true"

    try:
        repository_root = Path(args.repository_root).resolve()
        policy_path = Path(args.policy).resolve()
        policy = load_policy(policy_path)
        current = load_current_report(Path(args.current_report))
        history = load_history(Path(args.history_data), policy.benchmark_suite)
        compatible, exclusions, fingerprint = compatible_history(
            repository_root,
            policy_path,
            policy,
            history,
            args.baseline_ancestor,
        )
        outcomes = evaluate(current, compatible, policy)
        summary = build_summary(
            outcomes,
            len(compatible),
            exclusions,
            fingerprint,
            policy,
            allow_failure,
            args.baseline_ancestor,
        )
        print(summary, end="")
        emit_annotations(outcomes)
        append_summary(summary_path, summary)
    except GateError as error:
        summary = build_error_summary(error)
        print(summary, file=sys.stderr, end="")
        try:
            append_summary(summary_path, summary)
        except GateError as summary_error:
            print(summary_error, file=sys.stderr)
        return 2

    has_failure = any(outcome.status == "FAIL" for outcome in outcomes)
    return 0 if allow_failure or not has_failure else 1


if __name__ == "__main__":
    raise SystemExit(main())
