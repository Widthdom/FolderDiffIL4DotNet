#!/usr/bin/env python3
"""
Tests the evidence-based benchmark regression gate.
実測ベースのベンチマーク回帰ゲートをテストします。
"""

from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parent.parent / "check_benchmark_regressions.py"
sys.path.insert(0, str(SCRIPT_PATH.parent))
from benchmark_environment import environment_suite_name

BENCHMARK_NAME = "Example.Benchmarks.Sample"
HOST = {
    "BenchmarkDotNetVersion": "0.15.8",
    "OsVersion": "Linux Ubuntu 24.04.5 LTS",
    "ProcessorName": "AMD EPYC 9V74",
    "RuntimeVersion": ".NET 8.0.30",
    "Architecture": "X64",
    "Configuration": "RELEASE",
    "DotNetCliVersion": "8.0.423",
    "PhysicalProcessorCount": 1,
    "PhysicalCoreCount": 2,
    "LogicalCoreCount": 4,
}


class BenchmarkRegressionGateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        (self.root / "bench").mkdir()
        (self.root / "bench" / "Benchmark.cs").write_text(
            "public static class Benchmark {}\n",
            encoding="utf-8",
        )
        self.policy_path = self.root / "benchmark-regression-policy.json"
        self.current_host = dict(HOST)
        self.history_suite = environment_suite_name("Fixture Suite", {"HostEnvironmentInfo": HOST})
        self.write_policy()
        self.run_git("init")
        self.run_git("config", "user.name", "Benchmark Test")
        self.run_git("config", "user.email", "benchmark-test@example.invalid")
        self.run_git("config", "commit.gpgSign", "false")
        self.run_git("add", "bench/Benchmark.cs")
        self.run_git("commit", "-m", "Add benchmark definition")
        self.commit = self.run_git("rev-parse", "HEAD").stdout.strip()

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def run_git(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        result = subprocess.run(
            ["git", *arguments],
            cwd=self.root,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        return result

    def write_policy(self, *, revision: int = 0, include_pattern: bool = True) -> None:
        pattern = "*" if include_pattern else "Different.*"
        policy = {
            "schema_version": 1,
            "benchmark_suite": "Fixture Suite",
            "baseline_revision": revision,
            "legacy_baseline_revision": 0,
            "minimum_compatible_samples": 3,
            "history_window": 5,
            "benchmark_definition": {
                "roots": ["bench"],
                "files": [],
            },
            "groups": [
                {
                    "name": "fixture",
                    "patterns": [pattern],
                    "observed_max_slowdown_percent": 10,
                    "warning_percent": 20,
                    "failure_percent": 40,
                }
            ],
        }
        self.policy_path.write_text(json.dumps(policy), encoding="utf-8")

    def write_current_report(self, value: float) -> Path:
        path = self.root / "current.json"
        path.write_text(
            json.dumps(
                {
                    "HostEnvironmentInfo": self.current_host,
                    "Benchmarks": [
                        {
                            "FullName": BENCHMARK_NAME,
                            "Statistics": {"Mean": value},
                        }
                    ]
                }
            ),
            encoding="utf-8",
        )
        return path

    def write_history(self, values: list[float], commit: str | None = None) -> Path:
        path = self.root / "data.js"
        entries = []
        for index, value in enumerate(values):
            entries.append(
                {
                    "commit": {"id": commit or self.commit},
                    "date": 1_700_000_000_000 + index,
                    "tool": "benchmarkdotnet",
                    "benches": [
                        {
                            "name": BENCHMARK_NAME,
                            "value": value,
                            "unit": "ns",
                        }
                    ],
                }
            )
        payload = {
            "entries": {
                self.history_suite: entries,
            }
        }
        path.write_text(
            f"window.BENCHMARK_DATA = {json.dumps(payload)}",
            encoding="utf-8",
        )
        return path

    def run_gate(
        self,
        current_value: float,
        history_values: list[float],
        *,
        allow_failure: bool = False,
        history_commit: str | None = None,
        baseline_ancestor: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        current_path = self.write_current_report(current_value)
        history_path = self.write_history(history_values, history_commit)
        summary_path = self.root / "summary.md"
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT_PATH),
                "--current-report",
                str(current_path),
                "--history-data",
                str(history_path),
                "--policy",
                str(self.policy_path),
                "--baseline-ancestor",
                baseline_ancestor or self.commit,
                "--repository-root",
                str(self.root),
                "--summary",
                str(summary_path),
                "--allow-failure",
                str(allow_failure).lower(),
            ],
            cwd=self.root,
            capture_output=True,
            text=True,
            check=False,
        )

    def test_median_threshold_boundaries_produce_pass_warning_and_failure(self) -> None:
        history = [98, 100, 102]

        passing = self.run_gate(119.9, history)
        warning = self.run_gate(120, history)
        failure = self.run_gate(140, history)

        self.assertEqual(0, passing.returncode, passing.stderr)
        self.assertIn("| PASS |", passing.stdout)
        self.assertEqual(0, warning.returncode, warning.stderr)
        self.assertIn("| WARNING |", warning.stdout)
        self.assertIn("::warning", warning.stdout)
        self.assertEqual(1, failure.returncode, failure.stderr)
        self.assertIn("| FAIL |", failure.stdout)
        self.assertIn("::error", failure.stdout)

    def test_failure_can_be_reported_without_blocking_intentional_baseline_publication(self) -> None:
        result = self.run_gate(150, [98, 100, 102], allow_failure=True)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Intentional baseline publication mode is enabled", result.stdout)
        self.assertIn("| FAIL |", result.stdout)

    def test_different_environments_do_not_share_a_baseline(self) -> None:
        for field, value in (
            ("ProcessorName", "Intel Xeon 6973P-C"),
            ("OsVersion", "Linux Ubuntu 24.04.4 LTS"),
            ("RuntimeVersion", ".NET 8.0.29"),
            ("Architecture", "Arm64"),
            ("LogicalCoreCount", 8),
            ("DotNetCliVersion", "8.0.422"),
            ("BenchmarkDotNetVersion", "0.15.7"),
            ("Configuration", "DEBUG"),
        ):
            with self.subTest(field=field):
                self.current_host = {**HOST, field: value}
                result = self.run_gate(500, [98, 100, 102])
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertIn("0 compatible hosted-runner sample(s)", result.stdout)
                self.assertIn("| WARMUP |", result.stdout)

    def test_legacy_history_without_environment_enters_warmup(self) -> None:
        self.history_suite = "Fixture Suite"
        result = self.run_gate(500, [98, 100, 102])
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("| WARMUP |", result.stdout)
        self.assertIn("Environment suite:", result.stdout)

    def test_missing_current_host_metadata_fails_closed(self) -> None:
        self.current_host = None
        result = self.run_gate(100, [98, 100, 102])
        self.assertEqual(2, result.returncode)
        self.assertIn("must contain HostEnvironmentInfo", result.stderr)

    def test_same_host_regression_is_not_diluted_by_other_environments(self) -> None:
        self.write_current_report(150)
        history_path = self.write_history([98, 100, 102])
        payload = json.loads(history_path.read_text().split("=", 1)[1])
        other_suite = environment_suite_name(
            "Fixture Suite", {"HostEnvironmentInfo": {**HOST, "ProcessorName": "Other CPU"}},
        )
        other_entries = json.loads(json.dumps(payload["entries"][self.history_suite]))
        for entry in other_entries:
            entry["benches"][0]["value"] = 1000
        payload["entries"][other_suite] = other_entries
        history_path.write_text(f"window.BENCHMARK_DATA = {json.dumps(payload)}")
        result = subprocess.run(
            [sys.executable, str(SCRIPT_PATH), "--current-report", str(self.root / "current.json"),
             "--history-data", str(history_path), "--policy", str(self.policy_path),
             "--baseline-ancestor", self.commit, "--repository-root", str(self.root)],
            capture_output=True, text=True, check=False,
        )
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn("3 compatible hosted-runner sample(s)", result.stdout)
        self.assertIn("| FAIL |", result.stdout)

    def test_combiner_publishes_the_suite_read_by_the_gate(self) -> None:
        report_path = self.write_current_report(150)
        report_path.rename(self.root / "sample-report-full-compressed.json")
        output_path = self.root / "github-output"
        combined = subprocess.run(
            [sys.executable, str(SCRIPT_PATH.parent / "benchmark_environment.py"),
             "--results-directory", str(self.root), "--policy", str(self.policy_path),
             "--github-output", str(output_path)],
            capture_output=True, text=True, check=False,
        )
        self.assertEqual(0, combined.returncode, combined.stderr)
        self.history_suite = output_path.read_text().strip().removeprefix("suite=")
        self.assertEqual(HOST, json.loads((self.root / "combined-report.json").read_text())["HostEnvironmentInfo"])
        result = self.run_gate(150, [98, 100, 102])
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn("| FAIL |", result.stdout)

    def test_definition_mismatch_is_visible_and_enters_warmup(self) -> None:
        (self.root / "bench" / "Benchmark.cs").write_text(
            "public static class ChangedBenchmark {}\n",
            encoding="utf-8",
        )

        result = self.run_gate(500, [98, 100, 102])

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("definition mismatch 3", result.stdout)
        self.assertIn("| WARMUP |", result.stdout)

    def test_baseline_revision_change_excludes_old_history(self) -> None:
        self.write_policy(revision=1)

        result = self.run_gate(500, [98, 100, 102])

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("revision mismatch 3", result.stdout)
        self.assertIn("| WARMUP |", result.stdout)

    def test_history_newer_than_intended_base_is_excluded(self) -> None:
        (self.root / "after-base.txt").write_text("newer\n", encoding="utf-8")
        self.run_git("add", "after-base.txt")
        self.run_git("commit", "-m", "Create a commit after the intended base")
        newer_commit = self.run_git("rev-parse", "HEAD").stdout.strip()

        result = self.run_gate(
            500,
            [98, 100, 102],
            history_commit=newer_commit,
            baseline_ancestor=self.commit,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("not an intended-base ancestor 3", result.stdout)
        self.assertIn("| WARMUP |", result.stdout)

    def test_unassigned_benchmark_fails_closed(self) -> None:
        self.write_policy(include_pattern=False)

        result = self.run_gate(100, [98, 100, 102])

        self.assertEqual(2, result.returncode)
        self.assertIn("must match exactly one threshold group", result.stderr)

    def test_policy_rejects_retired_200_percent_failure_ceiling(self) -> None:
        policy = json.loads(self.policy_path.read_text(encoding="utf-8"))
        policy["groups"][0]["failure_percent"] = 200
        self.policy_path.write_text(json.dumps(policy), encoding="utf-8")

        result = self.run_gate(100, [98, 100, 102])

        self.assertEqual(2, result.returncode)
        self.assertIn("must remain below the retired 200% ceiling", result.stderr)


if __name__ == "__main__":
    unittest.main()
