#!/usr/bin/env python3
"""
Generates a compact markdown/json summary from a Stryker JSON report.
Stryker の JSON レポートから簡潔な markdown/json 要約を生成します。
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from typing import Any, Dict, Optional, Tuple, Union


DEFAULT_STRYKER_CONFIG_PATH = Path(__file__).resolve().parent.parent / "stryker-config.json"


def parse_args() -> argparse.Namespace:
    """Parses CLI arguments. / CLI 引数を解析します。"""
    parser = argparse.ArgumentParser(
        description="Generate mutation-testing visibility summary. / ミューテーションテスト可視化サマリーを生成します。"
    )
    parser.add_argument("--output-root", default="StrykerOutput")
    parser.add_argument("--stryker-config", default=str(DEFAULT_STRYKER_CONFIG_PATH))
    parser.add_argument("--run-url", default=build_default_run_url())
    parser.add_argument("--summary-artifact-name", default=build_default_artifact_name("StrykerSummary"))
    parser.add_argument("--report-artifact-name", default=build_default_artifact_name("StrykerReport"))
    return parser.parse_args()


def build_default_run_url() -> str:
    """Builds the GitHub Actions run URL from environment variables. / 環境変数から GitHub Actions run URL を構築します。"""
    server_url = os.environ.get("GITHUB_SERVER_URL")
    repository = os.environ.get("GITHUB_REPOSITORY")
    run_id = os.environ.get("GITHUB_RUN_ID")
    if server_url and repository and run_id:
        return f"{server_url}/{repository}/actions/runs/{run_id}"

    return "local-run"


def build_default_artifact_name(prefix: str) -> str:
    """Builds a per-run artifact name from environment variables. / 環境変数から run 単位の artifact 名を構築します。"""
    run_number = os.environ.get("GITHUB_RUN_NUMBER")
    run_attempt = os.environ.get("GITHUB_RUN_ATTEMPT")
    if run_number and run_attempt:
        return f"{prefix}-{run_number}-{run_attempt}"

    return f"{prefix}-local"


def iter_status_values(node):
    """Yields every nested mutant status string. / ネストされた mutant status 文字列をすべて列挙します。"""
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "status" and isinstance(value, str):
                yield value
            else:
                yield from iter_status_values(value)
    elif isinstance(node, list):
        for item in node:
            yield from iter_status_values(item)


def load_mutation_score(report_data):
    """Extracts the mutation score from known Stryker shapes. / 既知の Stryker 形式から mutation score を抽出します。"""
    mutation_score = report_data.get("mutationScore")
    if mutation_score is None and isinstance(report_data.get("files"), dict):
        mutation_score = report_data["files"].get("mutationScore")

    return mutation_score


def load_thresholds(config_path: Path) -> Dict[str, Union[int, float]]:
    """Loads Stryker thresholds from the canonical config file. / 正本の設定ファイルから Stryker 閾値を読み込みます。"""
    with config_path.open("r", encoding="utf-8") as handle:
        config = json.load(handle)

    thresholds = config["stryker-config"]["thresholds"]
    required_keys = ("high", "low", "break")
    normalized = {}
    for key in required_keys:
        value = thresholds[key]
        if not isinstance(value, (int, float)):
            raise ValueError(f"Threshold '{key}' must be numeric in {config_path}.")

        normalized[key] = value

    return normalized


def classify_score_band(mutation_score, thresholds):
    """Classifies the score against configured thresholds. / 設定済み閾値に対するスコア帯を判定します。"""
    if not isinstance(mutation_score, (int, float)):
        return "unknown", "N/A"

    if mutation_score >= thresholds["high"]:
        band = "high"
    elif mutation_score >= thresholds["low"]:
        band = "low"
    elif mutation_score >= thresholds["break"]:
        band = "warning"
    else:
        band = "break"

    return band, f"{mutation_score:.2f}%"


def select_report_path(output_root: Path) -> Optional[Path]:
    """Selects the newest report candidate by mtime. / mtime 基準で最新のレポート候補を選択します。"""
    report_candidates = [path for path in output_root.glob("**/reports/*.json") if path.is_file()]
    if not report_candidates:
        return None

    return max(report_candidates, key=lambda path: path.stat().st_mtime_ns)


def build_summary(
    output_root: Path,
    run_url: str,
    summary_artifact_name: str,
    report_artifact_name: str,
    thresholds: Dict[str, Union[int, float]],
) -> Tuple[Dict[str, Any], str]:
    """Builds the summary payload and markdown body. / サマリー payload と markdown 本文を構築します。"""
    report_path = select_report_path(output_root)

    summary = {
        "reportPath": str(report_path) if report_path else None,
        "runUrl": run_url,
        "thresholds": thresholds,
        "artifactNames": {
            "summary": summary_artifact_name,
            "report": report_artifact_name,
        },
    }

    lines = [
        "## Mutation Testing Results",
        "",
        f"- Run: [GitHub Actions run]({run_url})",
    ]

    load_error = None
    report_loaded = False
    if report_path and report_path.is_file():
        try:
            with report_path.open("r", encoding="utf-8") as handle:
                report_data = json.load(handle)
            report_loaded = True

            mutation_score = load_mutation_score(report_data)
            band, score_display = classify_score_band(mutation_score, thresholds)
            status_counts = {}
            for status in iter_status_values(report_data):
                status_counts[status] = status_counts.get(status, 0) + 1

            survivor_count = status_counts.get("Survived", 0)
            summary.update(
                {
                    "mutationScore": mutation_score,
                    "scoreBand": band,
                    "survivorCount": survivor_count,
                    "statusCounts": status_counts,
                }
            )

            lines.extend(
                [
                    f"- Mutation score: **{score_display}** ({band} band; thresholds high/low/break = {thresholds['high']}/{thresholds['low']}/{thresholds['break']})",
                    f"- Survived mutants: **{survivor_count}**",
                    f"- Report JSON: `{report_path}`",
                    "- Historical trail: `StrykerSummary-*` and `StrykerReport-*` artifacts on each Actions run.",
                ]
            )

            if status_counts:
                lines.extend(["", "| Mutant status | Count |", "| --- | ---: |"])
                for status in sorted(status_counts):
                    lines.append(f"| `{status}` | {status_counts[status]} |")
        except (OSError, json.JSONDecodeError) as ex:
            load_error = f"{type(ex).__name__}: {ex}"
            summary["reportPath"] = None

    if not report_loaded:
        summary.update(
            {
                "mutationScore": None,
                "scoreBand": "unavailable",
                "survivorCount": None,
                "statusCounts": {},
            }
        )
        lines.extend(
            [
                "- Mutation score: unavailable",
                "- Report JSON: not found under `StrykerOutput/**/reports/*.json`",
                "- Historical trail: `StrykerSummary-*` artifacts are still uploaded for this run.",
            ]
        )
        if load_error:
            summary["loadError"] = load_error
            lines.append(f"- Report load error: `{load_error}`")

    return summary, "\n".join(lines) + "\n"


def main() -> int:
    """Writes summary files into the output root. / output root 配下にサマリーファイルを書き込みます。"""
    args = parse_args()
    output_root = Path(args.output_root)
    thresholds = load_thresholds(Path(args.stryker_config))
    output_root.mkdir(parents=True, exist_ok=True)

    summary, markdown = build_summary(
        output_root,
        args.run_url,
        args.summary_artifact_name,
        args.report_artifact_name,
        thresholds,
    )

    (output_root / "mutation-summary.md").write_text(markdown, encoding="utf-8")
    (output_root / "mutation-summary.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True),
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
