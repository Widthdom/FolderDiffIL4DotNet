#!/usr/bin/env python3
"""
Validates that all test classes in the test project are listed
in TESTING_GUIDE.md scope map tables.

テストプロジェクト内のすべてのテストクラスが
TESTING_GUIDE.md の範囲マップテーブルにリストされていることを検証します。

Exit codes:
  0 — all test classes are listed in the scope map
  1 — one or more test classes are missing from the scope map (warning)
"""

import glob
import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEST_DIR = os.path.join(REPO_ROOT, "FolderDiffIL4DotNet.Tests")
GUIDE_PATH = os.path.join(REPO_ROOT, "doc", "TESTING_GUIDE.md")

# Patterns to match test class file names / テストクラスファイル名のパターン
TEST_FILE_PATTERN = "**/*Tests.cs"

# Patterns and files to exclude from validation / 検証から除外するパターンとファイル
EXCLUDE_PATTERNS = {
    # Partial files — the base class is what should be listed, not each partial
    # パーシャルファイル — リストされるべきは基底クラスであり各パーシャルではない
    re.compile(r"\.\w+\.cs$"),  # e.g. Tests.MutationKilling.cs, Tests.Combined.cs
}

# Classes that are test infrastructure, not test classes / テストインフラであり、テストクラスではないもの
INFRASTRUCTURE_CLASSES = {
    "TestLogger",
}


def find_test_classes():
    """Finds all unique test class names in the test project."""
    classes = set()
    for path in glob.glob(os.path.join(TEST_DIR, TEST_FILE_PATTERN), recursive=True):
        filename = os.path.basename(path)
        # Skip partial files (e.g. FooTests.Bar.cs) — only count FooTests.cs
        # パーシャルファイル（例: FooTests.Bar.cs）をスキップ — FooTests.cs のみカウント
        name_without_ext = filename[:-3]  # remove .cs
        if "." in name_without_ext:
            continue
        if name_without_ext in INFRASTRUCTURE_CLASSES:
            continue
        classes.add(name_without_ext)
    return classes


def find_classes_in_guide():
    """Extracts test class names referenced in TESTING_GUIDE.md via Markdown links."""
    if not os.path.isfile(GUIDE_PATH):
        print(f"ERROR: {GUIDE_PATH} not found", file=sys.stderr)
        sys.exit(2)

    with open(GUIDE_PATH, "r", encoding="utf-8") as f:
        content = f.read()

    # Match patterns like [`FooTests`](../path/FooTests.cs)
    # [`FooTests`](../path/FooTests.cs) のようなパターンにマッチ
    link_pattern = re.compile(r"\[`(\w+Tests)`\]\([^)]+\)")
    return set(link_pattern.findall(content))


def main():
    test_classes = find_test_classes()
    guide_classes = find_classes_in_guide()

    missing = sorted(test_classes - guide_classes)
    extra = sorted(guide_classes - test_classes)

    print(f"Test classes in project: {len(test_classes)}")
    print(f"Test classes in TESTING_GUIDE.md: {len(guide_classes)}")

    if missing:
        print(f"\n--- Test classes NOT listed in TESTING_GUIDE.md ({len(missing)}) ---")
        for cls in missing:
            print(f"  - {cls}")

    if extra:
        print(f"\n--- Classes in TESTING_GUIDE.md but NOT found in test project ({len(extra)}) ---")
        for cls in extra:
            print(f"  - {cls}")

    if missing:
        print(
            f"\nWARNING: {len(missing)} test class(es) missing from TESTING_GUIDE.md scope map.",
            file=sys.stderr,
        )
        # Non-blocking warning — exits 1 but CI step uses continue-on-error
        # 非ブロッキング警告 — exit 1 だが CI ステップは continue-on-error を使用
        sys.exit(1)

    print("\nAll test classes are listed in TESTING_GUIDE.md scope map.")
    sys.exit(0)


if __name__ == "__main__":
    main()
