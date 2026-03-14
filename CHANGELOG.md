# Changelog

All notable changes to this project will be documented in this file.

The English section comes first, followed by a Japanese translation.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## English

### [Unreleased]

### [1.2.2] - 2026-03-14

#### Added

- Added configurable warnings when a file in `new` has an older last-modified timestamp than the matching file in `old`, including console output before exit and a final `Warnings` section in `diff_report.md`.
- Added coverlet-based coverage collection in CI and expanded automated tests for `Program`, logging, progress reporting, file-system helpers, and text-diff fallback paths.

#### Changed

- Extended console color emphasis so warning messages are also highlighted in yellow for consistency with the final success/failure messages.
- Updated configuration samples, documentation, and automated tests for timestamp-regression warnings.
- Reorganized runtime composition around `ProgramRunner`, `DiffExecutionContext`, and interface-based services to improve diff-pipeline testability and reduce direct static-state coupling.
- Split developer and testing guidance into dedicated documents and expanded the README with clearer installation examples and comparison-flow documentation.
- Expanded the developer guide with execution lifecycle, DI boundaries, runtime-mode notes, Mermaid diagrams, and a clearer documentation map across README and testing guidance.

#### Fixed

- Limited exception handling during network-share detection and added warning logs when parallel text comparison falls back to sequential mode.

### [1.2.1] - 2026-03-09

#### Added

- Added configuration keys for text-diff parallel threshold and chunk size in KiB.
- Added focused tests for `FolderDiffService` and strengthened report-generation coverage.

#### Changed

- Standardized guard clauses on `ArgumentNullException.ThrowIfNull`.

#### Fixed

- Made `FileDiffResultLists` thread-safe and added `ResetAll` to reliably clear shared result state between runs.

### [1.2.0] - 2026-03-07

#### Added

- Added an optional IL-comparison filter that ignores lines containing configured substrings and reflects the behavior in report output.

#### Changed

- Changed the default IL disk-cache setting from unlimited (`0`) to 1000 files and 512 MB.

### [1.1.9] - 2026-03-07

#### Added

- Added a dedicated test project, CI test execution guidance, and broader automated coverage for cache, disassembler, and reporting behavior.
- Added an ASCII-art application banner after successful command-line validation.
- Added color emphasis only to the final success or failure console message.

#### Changed

- Unified `dotnet-ildasm` and `dotnet ildasm` handling, tightened disassembler identity consistency checks, and improved disassembler reporting details.
- Refactored utility helpers into single-responsibility classes and split large methods in `DotNetDisassembleService` and `FolderDiffService`.
- Applied smaller internal cleanups, including replacing `HashSet.Union` result creation with `UnionWith`.

#### Fixed

- Fixed report output so it records the actual disassembler used instead of listing every available tool.
- Prevented mixed disassembler usage and cache contamination during IL comparison.
- Removed redundant sequential re-comparison for small text files.
- Improved regression handling, report wording, and exit-code behavior around IL comparison.

### [1.1.8] - 2026-01-24

#### Added

- Added the actual reverse-engineering tool name and version to `diff_report.md`.

#### Changed

- Redesigned folder-diff progress display to show a label, spinner, and progress bar together, then refined the presentation.

### [1.1.7] - 2025-12-30

#### Added

- Added `README.en.md`, which was later consolidated back into `README.md` during the documentation restructure.
- Added license information to the documentation.

#### Changed

- Replaced spinner-only feedback with a coordinated progress-bar presentation for long-running work.

### [1.1.6] - 2025-12-11

#### Added

- Added the executing computer name to `diff_report.md`.

#### Changed

- Refactored constant definitions and cache internals for the disassembler and IL cache.

#### Fixed

- Removed redundant internal processing paths.

### [1.1.5] - 2025-12-08

#### Added

- Added spinner-based feedback for long-running operations.

#### Changed

- Performed broad internal refactoring and refreshed README wording.

### [1.1.4] - 2025-12-07

#### Added

- Added GitHub Actions automation for .NET builds.

### [1.1.3] - 2025-11-29

#### Added

- Added configuration and report support for listing ignored files.

### [1.1.2] - 2025-11-16

#### Added

- Added report warnings when any file is classified as `MD5Mismatch`.

#### Changed

- Documented .NET executable detection behavior in the README.

#### Fixed

- Reduced the initial silent period before the first progress update.
- Updated `.NET` executable detection to support both PE32 and PE32+ binaries.
- Included additional minor corrections shipped between `v1.1.1` and `v1.1.2`.

### [1.1.1] - 2025-09-14

#### Added

- Added a configuration option to include or suppress file timestamps in `diff_report.md`.

### [1.1.0] - 2025-09-12

#### Added

- Added network-share optimization support.
- Added more file extensions that are treated as text during comparison.

#### Changed

- Made `IgnoredExtensions` matching case-insensitive.

#### Removed

- Removed the unused `ShouldSkipPromptOnExit` configuration entry.

#### Fixed

- Corrected `TextFileExtensions` configuration values.
- Corrected README mistakes.
- Reduced early runtime silence before progress output begins.

### [1.0.1] - 2025-08-30

#### Added

- Added more file extensions to the text-comparison list.

#### Removed

- Removed generation of `ILlog.md` and `ILlog.html`.

### [1.0.0] - 2025-08-17

#### Added

- Initial release of `FolderDiffIL4DotNet` with folder comparison, Markdown report generation, IL-based `.NET` assembly comparison, caching, configuration loading, progress reporting, and logging.

## 日本語

このファイルは主要な変更を記録するためのものです。

前半は英語、後半は日本語です。
形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/)、バージョン管理は [Semantic Versioning](https://semver.org/lang/ja/) に準拠します。

### [Unreleased]

### [1.2.2] - 2026-03-14

#### 追加

- `new` 側ファイルの更新日時が対応する `old` 側より古い場合に、終了前のコンソール警告と `diff_report.md` 末尾の `Warnings` セクションを出す設定付き機能を追加しました。
- CI に coverlet ベースのカバレッジ計測を追加し、`Program`、ロギング、進捗表示、ファイルシステム補助、テキスト差分フォールバック経路の自動テストを拡充しました。

#### 変更

- コンソール出力の色強調を見直し、最終的な成功・失敗メッセージに加えて警告メッセージも黄色で強調表示するようにしました。
- 更新日時逆転警告に合わせて、設定例、各種ドキュメント、自動テストを更新しました。
- `ProgramRunner`、`DiffExecutionContext`、インターフェイスベースのサービス構成へ整理し、差分パイプラインのテスタビリティを向上させるとともに、静的状態への直接依存を減らしました。
- 開発者向け・テスト向けドキュメントを分離し、README のインストール手順と比較フロー説明を拡充しました。
- 開発者ガイドに実行ライフサイクル、DI 境界、実行モード、Mermaid 図を追加し、README とテストガイドのドキュメント導線も整理しました。

#### 修正

- ネットワーク共有判定時の例外捕捉範囲を限定し、並列テキスト比較が逐次比較へフォールバックした際に警告ログを出すようにしました。

### [1.2.1] - 2026-03-09

#### 追加

- テキスト差分の並列化しきい値とチャンクサイズを KiB 単位で設定できる構成項目を追加しました。
- `FolderDiffService` 向けの専用テストを追加し、レポート生成まわりのテストを強化しました。

#### 変更

- ガード節の null チェックを `ArgumentNullException.ThrowIfNull` に統一しました。

#### 修正

- `FileDiffResultLists` をスレッドセーフ化し、実行間で共有状態を確実に初期化できる `ResetAll` を追加しました。

### [1.2.0] - 2026-03-07

#### 追加

- IL 比較時に設定した文字列を含む行を無視できるオプションを追加し、その挙動をレポート出力にも反映しました。

#### 変更

- IL ディスクキャッシュの既定値を、無制限 (`0`) ではなく 1000 件 / 512 MB に設定しました。

### [1.1.9] - 2026-03-07

#### 追加

- 専用のテストプロジェクトを追加し、CI でのテスト実行手順とあわせて、キャッシュ・逆アセンブラ・レポートまわりの自動テストを拡充しました。
- コマンドライン引数の検証完了後に、ASCII アートのアプリ名バナーを表示するようにしました。
- 最終的な成功・失敗メッセージだけを色強調するようにしました。

#### 変更

- `dotnet-ildasm` と `dotnet ildasm` の扱いを統一し、逆アセンブラ識別の整合性チェックとレポート表記を改善しました。
- ユーティリティ群を単一責任のクラスへ分割し、`DotNetDisassembleService` と `FolderDiffService` の長大メソッドを責務ごとに整理しました。
- `HashSet.Union` の結果生成を `UnionWith` に置き換えるなど、内部的な軽微改善を行いました。

#### 修正

- `diff_report.md` に利用可能な全逆アセンブラではなく、実際に使われた逆アセンブラだけを記録するよう修正しました。
- IL 比較時に複数の逆アセンブラが混在したり、キャッシュが混線したりする可能性を解消しました。
- 小さいテキストファイル比較で逐次比較が二重実行される冗長処理を解消しました。
- IL 比較まわりの回帰対応、レポート文言、終了コード挙動を改善しました。

### [1.1.8] - 2026-01-24

#### 追加

- `diff_report.md` に、実際に使われた逆アセンブルツール名とバージョンを出力するようにしました。

#### 変更

- フォルダ比較の進捗表示を「ラベル + スピナー + 進捗バー」の構成に刷新し、その後の軽微調整も取り込みました。

### [1.1.7] - 2025-12-30

#### 追加

- `README.en.md` を追加しました。その後、ドキュメント再編時に内容は `README.md` へ統合されました。
- ドキュメントにライセンス情報を追加しました。

#### 変更

- 長時間処理のフィードバックを、スピナー単体から進捗バーと協調する表示へ刷新しました。

### [1.1.6] - 2025-12-11

#### 追加

- `diff_report.md` に実行コンピュータ名を出力するようにしました。

#### 変更

- 定数定義、および逆アセンブラキャッシュ / IL キャッシュ内部をリファクタリングしました。

#### 修正

- 冗長な内部処理を整理しました。

### [1.1.5] - 2025-12-08

#### 追加

- 長時間処理向けのスピナー表示を追加しました。

#### 変更

- 全体的な内部リファクタリングを行い、README の記述も見直しました。

### [1.1.4] - 2025-12-07

#### 追加

- GitHub Actions による .NET ビルド自動化を追加しました。

### [1.1.3] - 2025-11-29

#### 追加

- Ignored ファイルをレポートに記載できる設定と出力対応を追加しました。

### [1.1.2] - 2025-11-16

#### 追加

- `MD5Mismatch` と判定されたファイルが存在した場合に、レポートで警告できるようにしました。

#### 変更

- `.NET` 実行ファイル判定の仕様を README に追記しました。

#### 修正

- 最初の進捗表示が出るまでの無音区間を短縮しました。
- `.NET` 実行ファイル判定を PE32 / PE32+ の両方に対応させました。
- `v1.1.1` 以降に入っていた軽微修正も本版に含めました。

### [1.1.1] - 2025-09-14

#### 追加

- `diff_report.md` に各ファイルの更新日時を出力するかどうかを切り替える設定を追加しました。

### [1.1.0] - 2025-09-12

#### 追加

- ネットワーク共有向けの最適化機能を追加しました。
- テキストとして扱う拡張子を追加しました。

#### 変更

- `IgnoredExtensions` を大文字小文字を無視して評価するようにしました。

#### 削除

- 未使用だった `ShouldSkipPromptOnExit` 設定を削除しました。

#### 修正

- `TextFileExtensions` の設定値誤りを是正しました。
- README の誤記を修正しました。
- 進捗出力開始前の無音区間を短縮しました。

### [1.0.1] - 2025-08-30

#### 追加

- テキスト比較対象とする拡張子を追加しました。

#### 削除

- `ILlog.md` / `ILlog.html` の生成を廃止しました。

### [1.0.0] - 2025-08-17

#### 追加

- `FolderDiffIL4DotNet` の初回リリース。フォルダ比較、Markdown レポート出力、`.NET` アセンブリの IL 比較、キャッシュ、設定読込、進捗表示、ログ出力を含みます。

[Unreleased]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.2.2...HEAD
[1.2.2]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.2.1...v1.2.2
[1.2.1]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.9...v1.2.0
[1.1.9]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.8...v1.1.9
[1.1.8]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.7...v1.1.8
[1.1.7]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.6...v1.1.7
[1.1.6]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.5...v1.1.6
[1.1.5]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.4...v1.1.5
[1.1.4]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.3...v1.1.4
[1.1.3]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Widthdom/FolderDiffIL4DotNet/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Widthdom/FolderDiffIL4DotNet/tree/v1.0.0
