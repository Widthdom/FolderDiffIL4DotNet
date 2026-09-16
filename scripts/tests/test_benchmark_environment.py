#!/usr/bin/env python3
"""
Tests environment validation and report combination before baseline publication.
ベースライン公開前の環境検証とレポート結合をテストします。
"""

import json
import tempfile
import unittest
from pathlib import Path

from test_check_benchmark_regressions import HOST
from benchmark_environment import combine_reports, environment_suite_name


class BenchmarkEnvironmentTests(unittest.TestCase):
    def test_missing_or_invalid_host_fields_are_rejected(self) -> None:
        for field in HOST:
            for value in (None, "", True, 0):
                with self.subTest(field=field, value=value):
                    with self.assertRaises(ValueError):
                        environment_suite_name("Suite", {"HostEnvironmentInfo": {**HOST, field: value}})

    def test_unrelated_metadata_and_json_key_order_do_not_change_the_suite(self) -> None:
        first = environment_suite_name("Suite", {"HostEnvironmentInfo": HOST})
        reordered = dict(reversed(list(HOST.items())))
        reordered["ChronometerFrequency"] = {"Hertz": 1000000000}
        self.assertEqual(first, environment_suite_name("Suite", {"HostEnvironmentInfo": reordered}))

    def test_combination_retains_host_metadata_and_all_benchmarks(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ("First", "Second"):
                (root / f"{name}-report-full-compressed.json").write_text(json.dumps({
                    "HostEnvironmentInfo": HOST,
                    "Benchmarks": [{"FullName": name, "Statistics": {"Mean": 100}}],
                }))
            report = combine_reports(root)
            self.assertEqual(HOST, report["HostEnvironmentInfo"])
            self.assertEqual(["First", "Second"], [value["FullName"] for value in report["Benchmarks"]])
            second = root / "Second-report-full-compressed.json"
            payload = json.loads(second.read_text())
            payload["HostEnvironmentInfo"]["ProcessorName"] = "Different CPU"
            second.write_text(json.dumps(payload))
            with self.assertRaisesRegex(ValueError, "different execution environments"):
                combine_reports(root)

    def test_missing_and_empty_benchmark_results_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaisesRegex(ValueError, "No benchmark results"):
                combine_reports(root)
            (root / "sample-report-full-compressed.json").write_text(json.dumps({
                "HostEnvironmentInfo": HOST, "Benchmarks": [],
            }))
            with self.assertRaisesRegex(ValueError, "contains no benchmarks"):
                combine_reports(root)


if __name__ == "__main__":
    unittest.main()
