#!/usr/bin/env python3
"""
Tests the NuGet vulnerability audit gate.
NuGet 脆弱性監査ゲートをテストします。
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parent.parent / "nuget_audit_gate.py"
NUGET_AUDIT_SOURCE = "https://api.nuget.org/v3/index.json"


def package(package_id: str, severity: str, advisory: str) -> dict:
    return {
        "id": package_id,
        "resolvedVersion": "1.2.3",
        "vulnerabilities": [
            {
                "severity": severity,
                "advisoryurl": advisory,
            }
        ],
    }


def report(*projects: dict) -> dict:
    return {
        "version": 1,
        "parameters": "--vulnerable --include-transitive",
        "sources": [NUGET_AUDIT_SOURCE],
        "projects": list(projects),
    }


class NuGetAuditGateTests(unittest.TestCase):
    def run_gate(self, payload: dict, summary_path: Path | None = None) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as temp_directory:
            fixture_path = Path(temp_directory) / "audit.json"
            fixture_path.write_text(json.dumps(payload), encoding="utf-8")
            environment = os.environ.copy()
            if summary_path is None:
                environment.pop("GITHUB_STEP_SUMMARY", None)
            else:
                environment["GITHUB_STEP_SUMMARY"] = str(summary_path)
            return subprocess.run(
                [sys.executable, str(SCRIPT_PATH), "--input", str(fixture_path)],
                capture_output=True,
                text=True,
                check=False,
                env=environment,
            )

    def test_clean_report_passes_and_writes_job_summary(self) -> None:
        with tempfile.TemporaryDirectory() as temp_directory:
            summary_path = Path(temp_directory) / "summary.md"
            result = self.run_gate(
                report(
                    {"path": "Product.csproj"},
                    {"path": "Product.Tests.csproj"},
                ),
                summary_path,
            )

            self.assertEqual(0, result.returncode, result.stderr)
            self.assertIn("Audited 2 solution projects", result.stdout)
            self.assertIn("No vulnerable NuGet packages", result.stdout)
            self.assertIn("**PASS:**", summary_path.read_text(encoding="utf-8"))

    def test_moderate_finding_is_visible_but_does_not_block(self) -> None:
        result = self.run_gate(
            report(
                {
                    "path": "Product.csproj",
                    "frameworks": [
                        {
                            "framework": "net8.0",
                            "topLevelPackages": [
                                package(
                                    "Example.Direct",
                                    "Moderate",
                                    "https://github.com/advisories/GHSA-moderate",
                                )
                            ],
                        }
                    ],
                }
            )
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("Direct", result.stdout)
        self.assertIn("Moderate", result.stdout)
        self.assertIn("**PASS:**", result.stdout)

    def test_high_and_critical_findings_block_direct_and_transitive_packages(self) -> None:
        result = self.run_gate(
            report(
                {
                    "path": "Product.Tests.csproj",
                    "frameworks": [
                        {
                            "framework": "net8.0",
                            "topLevelPackages": [
                                package(
                                    "Example.Direct",
                                    "Critical",
                                    "https://github.com/advisories/GHSA-critical",
                                )
                            ],
                            "transitivePackages": [
                                package(
                                    "Example.Transitive",
                                    "High",
                                    "https://github.com/advisories/GHSA-high",
                                )
                            ],
                        }
                    ],
                }
            )
        )

        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn("Direct", result.stdout)
        self.assertIn("Transitive", result.stdout)
        self.assertIn("2 High/Critical finding(s)", result.stdout)

    def test_incomplete_report_fails_closed(self) -> None:
        result = self.run_gate(
            {
                "version": 1,
                "parameters": "--vulnerable",
                "sources": [NUGET_AUDIT_SOURCE],
                "projects": [{"path": "Product.csproj"}],
            }
        )

        self.assertEqual(2, result.returncode)
        self.assertIn("was not generated with --include-transitive", result.stderr)

    def test_report_without_advisory_capable_source_fails_closed(self) -> None:
        payload = report({"path": "Product.csproj"})
        payload["sources"] = ["/private/local-feed"]

        result = self.run_gate(payload)

        self.assertEqual(2, result.returncode)
        self.assertIn("did not query the required advisory source", result.stderr)

    def test_listed_package_without_vulnerability_records_fails_closed(self) -> None:
        result = self.run_gate(
            report(
                {
                    "path": "Product.csproj",
                    "frameworks": [
                        {
                            "framework": "net8.0",
                            "topLevelPackages": [
                                {
                                    "id": "Malformed.Package",
                                    "resolvedVersion": "1.2.3",
                                }
                            ],
                        }
                    ],
                }
            )
        )

        self.assertEqual(2, result.returncode)
        self.assertIn("does not contain any vulnerability records", result.stderr)


if __name__ == "__main__":
    unittest.main()
