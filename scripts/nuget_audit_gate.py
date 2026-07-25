#!/usr/bin/env python3
"""
Audits direct and transitive NuGet dependencies and blocks High/Critical findings.
NuGet の直接・推移的依存関係を監査し、High/Critical の検出をブロックします。
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


BLOCKING_SEVERITIES = {"high", "critical"}
KNOWN_SEVERITIES = {"low", "moderate", "high", "critical"}
NUGET_AUDIT_SOURCE = "https://api.nuget.org/v3/index.json"


class AuditError(RuntimeError):
    """
    Indicates that the audit could not produce a trustworthy result.
    信頼できる監査結果を生成できなかったことを示します。
    """


@dataclass(frozen=True)
class Finding:
    project: str
    framework: str
    dependency: str
    package: str
    version: str
    severity: str
    advisory_url: str

    @property
    def is_blocking(self) -> bool:
        """
        Returns whether this finding blocks CI.
        この検出が CI をブロックするかを返します。
        """
        return self.severity.lower() in BLOCKING_SEVERITIES


def parse_args() -> argparse.Namespace:
    """
    Parses CLI arguments.
    CLI 引数を解析します。
    """
    parser = argparse.ArgumentParser(
        description=(
            "Fail on High/Critical direct or transitive NuGet vulnerabilities.\n"
            "NuGet の直接・推移的な High/Critical 脆弱性を失敗させます。"
        )
    )
    parser.add_argument("--solution", default="FolderDiffIL4DotNet.sln")
    parser.add_argument(
        "--input",
        type=Path,
        help=(
            "Read an existing dotnet list JSON report instead of running dotnet.\n"
            "dotnet を実行せず既存 JSON レポートを読み込みます。"
        ),
    )
    return parser.parse_args()


def run_dotnet_audit(solution: str) -> dict[str, Any]:
    """
    Runs the canonical NuGet audit command.
    正規の NuGet 監査コマンドを実行します。
    """
    command = [
        "dotnet",
        "list",
        solution,
        "package",
        "--vulnerable",
        "--include-transitive",
        "--format",
        "json",
        "--output-version",
        "1",
        "--source",
        NUGET_AUDIT_SOURCE,
    ]
    completed = subprocess.run(command, capture_output=True, text=True, check=False)
    if completed.stderr:
        print(completed.stderr, file=sys.stderr, end="")
    if completed.returncode != 0:
        raise AuditError(
            f"dotnet list package failed with exit code {completed.returncode}."
        )

    return parse_report(completed.stdout, "dotnet list package output")


def load_report(path: Path) -> dict[str, Any]:
    """
    Loads an audit report fixture.
    監査レポートの fixture を読み込みます。
    """
    try:
        return parse_report(path.read_text(encoding="utf-8-sig"), str(path))
    except OSError as error:
        raise AuditError(f"Could not read audit report '{path}': {error}") from error


def parse_report(contents: str, source: str) -> dict[str, Any]:
    """
    Parses and validates the report's JSON root.
    レポートの JSON ルートを解析・検証します。
    """
    try:
        report = json.loads(contents.lstrip("\ufeff"))
    except json.JSONDecodeError as error:
        raise AuditError(f"{source} is not valid JSON: {error}") from error

    if not isinstance(report, dict):
        raise AuditError(f"{source} must contain a JSON object.")
    return report


def inspect_report(report: dict[str, Any]) -> tuple[int, list[Finding]]:
    """
    Validates the NuGet schema and extracts every finding.
    NuGet スキーマを検証し、全検出を抽出します。
    """
    if report.get("version") != 1:
        raise AuditError("NuGet audit report must use output version 1.")

    parameters = report.get("parameters")
    if not isinstance(parameters, str):
        raise AuditError("NuGet audit report is missing its parameters.")
    for required_parameter in ("--vulnerable", "--include-transitive"):
        if required_parameter not in parameters.split():
            raise AuditError(
                f"NuGet audit report was not generated with {required_parameter}."
            )

    sources = report.get("sources")
    if not isinstance(sources, list) or NUGET_AUDIT_SOURCE not in sources:
        raise AuditError(
            f"NuGet audit report did not query the required advisory source {NUGET_AUDIT_SOURCE}."
        )

    projects = report.get("projects")
    if not isinstance(projects, list) or not projects:
        raise AuditError("NuGet audit report did not contain any projects.")

    findings: list[Finding] = []
    for project in projects:
        if not isinstance(project, dict):
            raise AuditError("NuGet audit report contains an invalid project entry.")
        project_path = project.get("path")
        if not isinstance(project_path, str) or not project_path:
            raise AuditError("NuGet audit report contains a project without a path.")

        frameworks = project.get("frameworks", [])
        if not isinstance(frameworks, list):
            raise AuditError(f"Project '{project_path}' has an invalid frameworks list.")

        for framework in frameworks:
            if not isinstance(framework, dict):
                raise AuditError(f"Project '{project_path}' has an invalid framework entry.")
            framework_name = framework.get("framework")
            if not isinstance(framework_name, str) or not framework_name:
                raise AuditError(f"Project '{project_path}' has a framework without a name.")

            for package_key, dependency in (
                ("topLevelPackages", "Direct"),
                ("transitivePackages", "Transitive"),
            ):
                packages = framework.get(package_key, [])
                if not isinstance(packages, list):
                    raise AuditError(
                        f"Project '{project_path}' has an invalid {package_key} list."
                    )
                findings.extend(
                    inspect_packages(
                        packages,
                        project_path,
                        framework_name,
                        dependency,
                    )
                )

    return len(projects), findings


def inspect_packages(
    packages: list[Any],
    project: str,
    framework: str,
    dependency: str,
) -> list[Finding]:
    """
    Extracts findings from one package section.
    1つの package セクションから検出を抽出します。
    """
    findings: list[Finding] = []
    for package in packages:
        if not isinstance(package, dict):
            raise AuditError(f"Project '{project}' has an invalid package entry.")

        package_id = package.get("id")
        version = package.get("resolvedVersion")
        vulnerabilities = package.get("vulnerabilities")
        if not isinstance(package_id, str) or not package_id:
            raise AuditError(f"Project '{project}' has a package without an id.")
        if not isinstance(version, str) or not version:
            raise AuditError(f"Package '{package_id}' does not have a resolved version.")
        if not isinstance(vulnerabilities, list) or not vulnerabilities:
            raise AuditError(
                f"Package '{package_id}' does not contain any vulnerability records."
            )

        for vulnerability in vulnerabilities:
            if not isinstance(vulnerability, dict):
                raise AuditError(f"Package '{package_id}' has an invalid vulnerability entry.")
            severity = vulnerability.get("severity")
            advisory_url = vulnerability.get("advisoryurl")
            if not isinstance(severity, str) or severity.lower() not in KNOWN_SEVERITIES:
                raise AuditError(
                    f"Package '{package_id}' has an unknown vulnerability severity '{severity}'."
                )
            if not isinstance(advisory_url, str) or not advisory_url:
                raise AuditError(f"Package '{package_id}' has a vulnerability without an advisory URL.")

            findings.append(
                Finding(
                    project=display_project_path(project),
                    framework=framework,
                    dependency=dependency,
                    package=package_id,
                    version=version,
                    severity=severity,
                    advisory_url=advisory_url,
                )
            )

    return findings


def display_project_path(project: str) -> str:
    """
    Makes absolute project paths concise in local and CI output.
    ローカル・CI 出力の絶対 project path を簡潔にします。
    """
    path = Path(project)
    if not path.is_absolute():
        return path.as_posix()
    try:
        return Path(os.path.relpath(path, Path.cwd())).as_posix()
    except ValueError:
        return path.as_posix()


def escape_markdown_cell(value: str) -> str:
    """
    Escapes a markdown table cell.
    markdown テーブルセルをエスケープします。
    """
    return value.replace("\\", "\\\\").replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def build_summary(project_count: int, findings: list[Finding]) -> str:
    """
    Builds the log and GitHub job summary.
    ログと GitHub job summary を構築します。
    """
    blocking_count = sum(finding.is_blocking for finding in findings)
    lines = [
        "## NuGet vulnerability audit",
        "",
        (
            f"Audited {project_count} solution projects for direct and transitive "
            f"vulnerabilities; found {len(findings)} advisory entries."
        ),
        "",
    ]

    if findings:
        lines.extend(
            [
                "| Project | Framework | Dependency | Package | Version | Severity | Advisory |",
                "| --- | --- | --- | --- | --- | --- | --- |",
            ]
        )
        for finding in findings:
            values = (
                finding.project,
                finding.framework,
                finding.dependency,
                finding.package,
                finding.version,
                finding.severity,
                finding.advisory_url,
            )
            lines.append("| " + " | ".join(escape_markdown_cell(value) for value in values) + " |")
        lines.append("")
    else:
        lines.extend(["No vulnerable NuGet packages were reported.", ""])

    if blocking_count:
        lines.append(
            f"**FAIL:** {blocking_count} High/Critical finding(s) block this build."
        )
    else:
        lines.append("**PASS:** No High/Critical findings.")

    return "\n".join(lines) + "\n"


def build_error_summary(error: AuditError) -> str:
    """
    Builds a fail-closed error summary.
    fail-closed のエラーサマリーを構築します。
    """
    return (
        "## NuGet vulnerability audit\n\n"
        f"**ERROR:** The audit could not produce a trustworthy result: {error}\n"
    )


def append_job_summary(summary: str) -> None:
    """
    Appends output to the GitHub job summary when available.
    利用可能な場合 GitHub job summary へ追記します。
    """
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return
    try:
        with Path(summary_path).open("a", encoding="utf-8") as handle:
            handle.write(summary)
    except OSError as error:
        raise AuditError(f"Could not write GITHUB_STEP_SUMMARY: {error}") from error


def main() -> int:
    """
    Runs the audit gate.
    監査ゲートを実行します。
    """
    args = parse_args()
    try:
        report = load_report(args.input) if args.input else run_dotnet_audit(args.solution)
        project_count, findings = inspect_report(report)
        summary = build_summary(project_count, findings)
        print(summary, end="")
        append_job_summary(summary)
        return 1 if any(finding.is_blocking for finding in findings) else 0
    except AuditError as error:
        summary = build_error_summary(error)
        print(summary, file=sys.stderr, end="")
        try:
            append_job_summary(summary)
        except AuditError as summary_error:
            print(f"Additionally, {summary_error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
