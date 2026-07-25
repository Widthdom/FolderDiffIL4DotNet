# CONTRIBUTING.md

## English

Thanks for helping improve `FolderDiffIL4DotNet`.

Before opening a pull request:

1. Read `AGENT_GUIDE.md`.
2. Use the `nildiff` command name in user-facing examples.
3. Run the relevant tests, and run the Release configuration test when feasible.
4. Update docs, samples, and `CHANGELOG.md` when behavior changes.
5. Do not create tags or publish packages.
6. Use explicit `git add <file>` paths only.

If you are reporting a bug, include the exact command, OS, .NET SDK version, and sanitized output.
Do not paste private paths, customer data, proprietary binaries, or exploit details into public issues.

### Formatting

[`global.json`](global.json) pins the .NET SDK used by developers and CI, and [`.editorconfig`](.editorconfig) is the formatting source of truth. Install the pinned SDK, then run:

```shell
dotnet restore FolderDiffIL4DotNet.sln
dotnet format FolderDiffIL4DotNet.sln --verify-no-changes --no-restore
```

To fix formatting drift locally, replace the verification command with:

```shell
dotnet format FolderDiffIL4DotNet.sln --no-restore
```

Tracked text files use LF through [`.gitattributes`](.gitattributes), except Windows command scripts (`*.bat` and `*.cmd`), which use CRLF. Binary assets are marked as binary. Build output under `bin/` and `obj/`, files recognized by the SDK as generated, and conventional generated C# filenames are not part of the hand-maintained formatting baseline.

## 日本語

`FolderDiffIL4DotNet` の改善に協力していただきありがとうございます。

プルリクエストを出す前に次を確認してください。

1. `AGENT_GUIDE.md` を読む。
2. ユーザー向けの例では `nildiff` を使う。
3. 関連テストを実行し、可能なら Release 構成のテストも走らせる。
4. 挙動が変わった場合はドキュメント、サンプル、`CHANGELOG.md` を更新する。
5. タグ作成やパッケージ公開はしない。
6. `git add <file>` で明示的に追加する。

不具合報告には、実行したコマンド、OS、.NET SDK バージョン、匿名化した出力を含めてください。
public issue に private なパス、顧客データ、プロプライエタリなバイナリ、攻撃詳細を貼らないでください。

### フォーマット

開発環境と CI で使う .NET SDK は [`global.json`](global.json) で固定し、フォーマット規則は [`.editorconfig`](.editorconfig) を正本とします。固定された SDK をインストールしてから、次を実行してください。

```shell
dotnet restore FolderDiffIL4DotNet.sln
dotnet format FolderDiffIL4DotNet.sln --verify-no-changes --no-restore
```

手元でフォーマットのずれを修正するには、検証コマンドの代わりに次を実行します。

```shell
dotnet format FolderDiffIL4DotNet.sln --no-restore
```

追跡対象のテキストファイルは [`.gitattributes`](.gitattributes) により LF に統一します。ただし、Windows コマンドスクリプト（`*.bat` と `*.cmd`）は CRLF とします。バイナリアセットは binary として指定します。`bin/` と `obj/` 配下のビルド出力、SDK が生成済みと認識するファイル、および一般的な生成 C# ファイル名は、手作業で保守するフォーマット基準の対象外です。
