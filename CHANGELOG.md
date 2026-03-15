# Changelog

All notable changes to this project will be documented in this file.

The English section comes first, followed by a Japanese translation.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## English

### [Unreleased]

#### Changed

- Refactored [`ILCache`](Services/Caching/ILCache.cs) into a thinner coordinator backed by [`ILMemoryCache`](Services/Caching/ILMemoryCache.cs) and [`ILDiskCache`](Services/Caching/ILDiskCache.cs), keeping the public API stable while separating in-memory retention from disk persistence/quota handling.
- Added regression coverage in [`ILCacheTests`](FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) for same-key updates at memory-capacity limits and for coordinated disk cleanup when LRU eviction removes an entry.
- Updated the [developer guide](doc/DEVELOPER_GUIDE.md) and [testing guide](doc/TESTING_GUIDE.md) to describe the split cache internals and reflect the latest passing test count (`223`).
- Replaced eager `Directory.GetFiles(...)` usage in [`FolderDiffService`](Services/FolderDiffService.cs) with lazy `Directory.EnumerateFiles(...)` behind [`IFileSystemService`](Services/IFileSystemService.cs), reducing discovery-side allocations for large trees and network shares while keeping folder-diff behavior unchanged.
- Added unit-test coverage for streaming file discovery in [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) and updated the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md) accordingly.
- Fixed missing link to [Developer Guide](doc/DEVELOPER_GUIDE.md).
- Clarified the `// MVID:` ignore rationale in the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and report note output, while tightening the English/Japanese wording so the high-level behavior stays aligned across both locales.
- Expanded documentation link coverage across the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md), and aligned locale-selectable external URLs to English/Japanese contexts.
- Added stable bilingual document anchors across the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), [testing guide](doc/TESTING_GUIDE.md), and [documentation index](index.md), and redirected config-related links to the README configuration-table sections so navigation lands reliably in common Markdown renderers.
- Added direct `.cs` source links for class references across the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md), excluding classes that would be split across partial definitions.
- Moved the report-level `MD5Mismatch` warning from `Summary` into the final `Warnings` section, ordered it before timestamp-regression warnings, and refreshed the related docs and regression tests.
- Introduced DocFX-based API documentation generation, added a documentation-site build path, and wired CI to publish the generated `DocumentationSite` artifact.
- Added [`IFileSystemService`](Services/IFileSystemService.cs) and [`IFileComparisonService`](Services/IFileComparisonService.cs) as low-level seams for folder discovery/output I/O and per-file comparison I/O, making permission and disk-failure paths unit-testable without changing production behavior.
- Split folder/file diff coverage more clearly into lightweight unit tests and temp-directory-backed integration tests, and expanded automated coverage for hash failures, IL-output failures, and large-text comparison paths.
- Updated the [README](README.md), [developer guide](doc/DEVELOPER_GUIDE.md), and [testing guide](doc/TESTING_GUIDE.md) in both English and Japanese to document the new service seams, test boundaries, and the latest passing test count (`219`).
- Moved aggregated `MD5Mismatch` console warnings into [`ProgramRunner`](ProgramRunner.cs), kept [`ReportGenerateService`](Services/ReportGenerateService.cs) report-only, and updated related docs and automated tests.
- Replaced one-off `string.Format(...)` usage with interpolated strings, removed broad `#region` usage, and deleted now-unused format/message constants.
- Updated the developer and testing guides to reflect the current source-style expectations and latest passing test count.
- Made `.NET` executable detection distinguish `NotDotNetExecutable` from detection failure, log a warning for non-fatal detection failures, and let chunk-parallel text-diff exceptions bubble to the existing sequential fallback path instead of silently returning `false`.
- Enabled the `CA1031` analyzer for production code so broad exception catches are surfaced during normal builds, while excluding test cleanup code from the warning.
- Removed generic `throw new Exception(..., ex)` wrapping from [`FileSystemUtility`](Utils/FileSystemUtility.cs), using [`Exception`](https://learn.microsoft.com/en-us/dotnet/api/system.exception?view=net-8.0) only as the referenced outer type name here, so original exception types and stack traces are preserved, and added regression coverage plus bilingual guide updates.
- Moved configuration defaults into [`ConfigSettings`](Models/ConfigSettings.cs), normalized missing or `null` config values back to code-defined defaults, simplified the shipped [`config.json`](config.json) to an override-only shape, and refreshed bilingual docs plus config-focused tests.

### [1.2.2] - 2026-03-14

#### Added

- Added configurable warnings when a file in `new` has an older last-modified timestamp than the matching file in `old`, including console output before exit and a final `Warnings` section in `diff_report.md`.
- Added coverlet-based coverage collection in CI and expanded automated tests for [`Program`](Program.cs), logging, progress reporting, file-system helpers, and text-diff fallback paths.

#### Changed

- Extended console color emphasis so warning messages are also highlighted in yellow for consistency with the final success/failure messages.
- Updated configuration samples, documentation, and automated tests for timestamp-regression warnings.
- Reorganized runtime composition around [`ProgramRunner`](ProgramRunner.cs), [`DiffExecutionContext`](Services/DiffExecutionContext.cs), and interface-based services to improve diff-pipeline testability and reduce direct static-state coupling.
- Split the [developer guide](doc/DEVELOPER_GUIDE.md) and [testing guide](doc/TESTING_GUIDE.md) into dedicated documents and expanded the [README](README.md) with clearer installation examples and comparison-flow documentation.
- Expanded the [developer guide](doc/DEVELOPER_GUIDE.md) with execution lifecycle, DI boundaries, runtime-mode notes, Mermaid diagrams, and a clearer documentation map across [README](README.md) and [testing guide](doc/TESTING_GUIDE.md).

#### Fixed

- Limited exception handling during network-share detection and added warning logs when parallel text comparison falls back to sequential mode.

### [1.2.1] - 2026-03-09

#### Added

- Added configuration keys for text-diff parallel threshold and chunk size in KiB.
- Added focused tests for [`FolderDiffService`](Services/FolderDiffService.cs) and strengthened report-generation coverage.

#### Changed

- Standardized guard clauses on [`ArgumentNullException.ThrowIfNull`](https://learn.microsoft.com/en-us/dotnet/api/system.argumentnullexception.throwifnull?view=net-8.0).

#### Fixed

- Made [`FileDiffResultLists`](Models/FileDiffResultLists.cs) thread-safe and added `ResetAll` to reliably clear shared result state between runs.

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

- Unified [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) and [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) handling, tightened disassembler identity consistency checks, and improved disassembler reporting details.
- Refactored utility helpers into single-responsibility classes and split large methods in [`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) and [`FolderDiffService`](Services/FolderDiffService.cs).
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

- Made [`IgnoredExtensions`](README.md#configuration-table-en) matching case-insensitive.

#### Removed

- Removed the unused `ShouldSkipPromptOnExit` configuration entry.

#### Fixed

- Corrected [`TextFileExtensions`](README.md#configuration-table-en) configuration values.
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

#### 変更

- [`ILCache`](Services/Caching/ILCache.cs) を、公開 API を維持したまま [`ILMemoryCache`](Services/Caching/ILMemoryCache.cs) と [`ILDiskCache`](Services/Caching/ILDiskCache.cs) を使う薄い調停役へ整理し、メモリ保持とディスク永続化/クォータ制御の責務を分離しました。
- [`ILCacheTests`](FolderDiffIL4DotNet.Tests/Services/Caching/ILCacheTests.cs) に、メモリ上限到達時の同一キー再保存と、LRU 退避時のディスクキャッシュ連動削除に対する回帰テストを追加しました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md) と [テストガイド](doc/TESTING_GUIDE.md) を更新し、キャッシュ内部の分離方針と最新の通過テスト件数（`223` 件）を反映しました。
- [`FolderDiffService`](Services/FolderDiffService.cs) 内で使っていた即時配列化の `Directory.GetFiles(...)` 相当を、[`IFileSystemService`](Services/IFileSystemService.cs) 越しの遅延列挙 `Directory.EnumerateFiles(...)` へ置き換えました。これにより、大量ファイルやネットワーク共有上の列挙で不要な配列確保を減らしつつ、フォルダ差分の振る舞いは維持しています。
- [`FolderDiffServiceUnitTests`](FolderDiffIL4DotNet.Tests/Services/FolderDiffServiceUnitTests.cs) にストリーミング列挙のテストを追加し、あわせて [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) を更新しました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md)のリンク付与漏れを修正しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、レポート注記にある `// MVID:` 無視理由の説明を整理し、あわせて日英の要約表現がずれないように調整しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) のリンク付与範囲を広げ、ロケール切替可能な外部 URL を英語文脈・日本語文脈に合わせて統一しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md)、[ドキュメント index](index.md) に日英対応の安定アンカーを追加し、設定値まわりのリンクは README の設定表セクションへ寄せるようにして、一般的な Markdown レンダラでも着地先が安定するようにしました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md) にあるクラス参照へ、`partial` 分割を前提としないものを中心に対応する `.cs` ソースリンクを追加しました。
- レポート上の `MD5Mismatch` 警告を `Summary` から末尾の `Warnings` セクションへ移し、更新日時逆転警告より先に出すように変更しました。あわせて関連ドキュメントと回帰テストを更新しました。
- DocFX ベースの API ドキュメント自動生成を導入し、ドキュメントサイトの生成経路と `DocumentationSite` artifact 公開を CI に追加しました。
- [`IFileSystemService`](Services/IFileSystemService.cs) と [`IFileComparisonService`](Services/IFileComparisonService.cs) を追加し、フォルダ列挙/出力系 I/O とファイル単位比較 I/O の差し替え口を明確にしました。これにより、本番挙動を変えずに権限エラーやディスク系失敗をユニットテストできるようにしました。
- `FolderDiffService` / `FileDiffService` まわりのテストを、軽量ユニットテストと temp ディレクトリ前提の統合テストにより明確に分離し、ハッシュ失敗、IL 出力失敗、大きいテキスト比較経路の自動テストを拡充しました。
- [README](README.md)、[開発者ガイド](doc/DEVELOPER_GUIDE.md)、[テストガイド](doc/TESTING_GUIDE.md)の日英両記述を更新し、新しいサービス境界、テスト境界、最新の通過テスト件数（`219` 件）を反映しました。
- 集約後の `MD5Mismatch` コンソール警告を [`ProgramRunner`](ProgramRunner.cs) に移し、[`ReportGenerateService`](Services/ReportGenerateService.cs) はレポート専用の責務に整理しました。あわせて関連ドキュメントと自動テストを更新しました。
- 単発利用の `string.Format(...)` を補間文字列へ置き換え、広範な `#region` 利用をやめ、不要になった書式・メッセージ定数を削除しました。
- 開発ガイドとテストガイドを更新し、現在のソースコード方針と最新の通過テスト件数を反映しました。
- `.NET` 実行可能判定で `NotDotNetExecutable` と判定失敗を区別するようにし、致命ではない判定失敗は warning を残して継続するようにしました。あわせて並列テキスト比較の例外は `false` に潰さず、既存の逐次比較フォールバック経路へ伝播させるようにしました。
- 本体コードで広すぎる例外捕捉を通常ビルド時に検出できるよう、`CA1031` アナライザーを有効化しました。テストの後片付け用 catch は warning 対象から外しています。
- [`FileSystemUtility`](Utils/FileSystemUtility.cs) での `throw new Exception(..., ex)` 形式の汎用ラップをやめ、ここで言う外側の型名 [`Exception`](https://learn.microsoft.com/ja-jp/dotnet/api/system.exception?view=net-8.0) への包み直しを避けることで、元の例外型とスタックトレースを維持するようにしました。あわせて回帰テストと日英ガイドを更新しました。
- 設定の既定値を [`ConfigSettings`](Models/ConfigSettings.cs) へ集約し、未指定や `null` の設定値をコード既定値へ正規化するようにしました。あわせて配布する [`config.json`](config.json) を override 専用の形に簡素化し、日英ドキュメントと設定まわりのテストを更新しました。

### [1.2.2] - 2026-03-14

#### 追加

- `new` 側ファイルの更新日時が対応する `old` 側より古い場合に、終了前のコンソール警告と `diff_report.md` 末尾の `Warnings` セクションを出す設定付き機能を追加しました。
- CI に coverlet ベースのカバレッジ計測を追加し、[`Program`](Program.cs)、ロギング、進捗表示、ファイルシステム補助、テキスト差分フォールバック経路の自動テストを拡充しました。

#### 変更

- コンソール出力の色強調を見直し、最終的な成功・失敗メッセージに加えて警告メッセージも黄色で強調表示するようにしました。
- 更新日時逆転警告に合わせて、設定例、各種ドキュメント、自動テストを更新しました。
- [`ProgramRunner`](ProgramRunner.cs)、[`DiffExecutionContext`](Services/DiffExecutionContext.cs)、インターフェイスベースのサービス構成へ整理し、差分パイプラインのテスタビリティを向上させるとともに、静的状態への直接依存を減らしました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md) と [テストガイド](doc/TESTING_GUIDE.md) を専用ドキュメントとして分離し、[README](README.md) のインストール手順と比較フロー説明を拡充しました。
- [開発者ガイド](doc/DEVELOPER_GUIDE.md) に実行ライフサイクル、DI 境界、実行モード、Mermaid 図を追加し、[README](README.md) と [テストガイド](doc/TESTING_GUIDE.md) のドキュメント導線も整理しました。

#### 修正

- ネットワーク共有判定時の例外捕捉範囲を限定し、並列テキスト比較が逐次比較へフォールバックした際に警告ログを出すようにしました。

### [1.2.1] - 2026-03-09

#### 追加

- テキスト差分の並列化しきい値とチャンクサイズを KiB 単位で設定できる構成項目を追加しました。
- [`FolderDiffService`](Services/FolderDiffService.cs) 向けの専用テストを追加し、レポート生成まわりのテストを強化しました。

#### 変更

- ガード節の null チェックを [`ArgumentNullException.ThrowIfNull`](https://learn.microsoft.com/ja-jp/dotnet/api/system.argumentnullexception.throwifnull?view=net-8.0) に統一しました。

#### 修正

- [`FileDiffResultLists`](Models/FileDiffResultLists.cs) をスレッドセーフ化し、実行間で共有状態を確実に初期化できる `ResetAll` を追加しました。

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

- [`dotnet-ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) と [`dotnet ildasm`](https://www.nuget.org/packages/dotnet-ildasm/) の扱いを統一し、逆アセンブラ識別の整合性チェックとレポート表記を改善しました。
- ユーティリティ群を単一責任のクラスへ分割し、[`DotNetDisassembleService`](Services/DotNetDisassembleService.cs) と [`FolderDiffService`](Services/FolderDiffService.cs) の長大メソッドを責務ごとに整理しました。
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

- [`IgnoredExtensions`](README.md#configuration-table-ja) を大文字小文字を無視して評価するようにしました。

#### 削除

- 未使用だった `ShouldSkipPromptOnExit` 設定を削除しました。

#### 修正

- [`TextFileExtensions`](README.md#configuration-table-ja) の設定値誤りを是正しました。
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
