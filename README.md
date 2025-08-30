# FolderDiffIL4DotNet

2つのフォルダの差分をレポート出力するコンソールアプリケーションです。.NET アセンブリに関してはビルド固有情報（例: MVID）が存在する場合はこれを除外して IL 比較するため、ビルド日時が異なっていても実質同じ挙動であれば同一と判定します。

## 必要環境

- .NET SDK 8.x
- macOS/Windows/Linuxで動作
 - IL 逆アセンブラ（自動で候補順に試行します）
	- 優先: `dotnet-ildasm` または `dotnet ildasm`
	- 代替: `ilspycmd`

インストール例:
```bash
dotnet tool install --global dotnet-ildasm
# 必要に応じて PATH に追加
# macOS/Linux:  $HOME/.dotnet/tools を PATH に追加
# Windows:      %USERPROFILE%\.dotnet\tools を PATH に追加
```
```bash
dotnet tool install -g ilspycmd
# 必要に応じて PATH に追加
# macOS/Linux:  $HOME/.dotnet/tools を PATH に追加
# Windows:      %USERPROFILE%\.dotnet\tools を PATH に追加
```

## 処理概要

- 旧バージョン側（比較元）と新バージョン側（比較先）のフォルダ（コマンドライン第1引数と第2引数に指定）の内容を再帰的に比較
- ファイルごとに一致/不一致及びその判定根拠（以下）を記録
	- MD5Match: MD5ハッシュが一致
    - MD5Mismatch: MD5ハッシュが不一致
    - ILMatch: IL（中間言語）ベースで一致（ビルド固有情報の差異は無視）
    - ILMismatch: IL（中間言語）ベースで不一致（ビルド固有情報の差異は無視）
    - TextMatch: テキストベースで一致
    - TextMismatch: テキストベースで不一致
- 比較結果区分ごと（Unchanged/Added/Removed/Modified）にファイルを分類
- 比較結果区分ごとのファイル一覧を`Reports/<コマンドライン第3引数に指定したレポートのラベル>/diff_report.md`に出力（ファイルのパス、最終更新日時、判定根拠）
    - Unchanged/Modified は相対パスで記載されます。
    - Added/Removed は絶対パスで記載されます。
- 比較結果区分ごとのファイル数を集計し`Reports/<コマンドライン第3引数に指定したレポートのラベル>/diff_report.md`に出力
    - 比較結果区分Unchangedのファイル一覧は、`config.json`のShouldIncludeUnchangedFilesがtrueの場合のみ出力されます。

## ファイル比較フロー

1) バイナリデータをMD5ハッシュで比較します。
- 一致ならばUnchanged, MD5Matchと判定し次のファイル比較へ

2) .NET アセンブリであれば（拡張子に依存せず、PE/CLR ヘッダで判定）IL に逆アセンブルして比較します。
- 行単位の比較（IL出力中の「`// MVID:`」で始まる行があった場合はこれを無視します。）
	- ビルド日時などビルド固有情報の差異を無視して比較することで、実質同じ挙動のアセンブリはビルド日時が異なっていても同一と判定できます。
	- 逆アセンブルに`dotnet-ildasm`を使用した場合、IL 先頭付近に「// MVID: {GUID}」が出力されることが多い一方、`ilspycmd`を使用した場合は出力されません。
- 一致ならばUnchanged, ILMatch、不一致ならばModified, ILMismatchと判定し次のファイル比較へ

3) テキストベースのファイル（`config.json`のTextFileExtensionsに指定された拡張子か否かで判定）であれば行単位で比較します。
- 一致ならばUnchanged, TextMatch、不一致ならばModified, TextMismatchと判定し次のファイル比較へ

4) Modified, MD5Mismatchと判定し次のファイル比較へ

## アプリケーション設定（`config.json`）

実行ファイルと同じディレクトリに配置します。例:

```json
{
	"IgnoredExtensions": [".cache", ".DS_Store", ".db", ".ilcache", ".log", ".pdb"],
  	"TextFileExtensions": [".config", ".cs", ".go", ".htm", ".html", ".http", ".ini", ".js", ".json", ".manifest", ".md", ".mod", ".plist", ".ps1", ".py", ".sln", ".sql", ".sum", ".ts", ".txt", ".vb", "bat", "xaml", "xml", "yml"],
	"MaxLogGenerations": 5,
	"ShouldIncludeUnchangedFiles": true,
	"ShouldOutputILText": true,
	"MaxParallelism": 0,
	"EnableILCache": true,
	"ILCacheDirectoryAbsolutePath": "",
	"ILCacheStatsLogIntervalSeconds": 60,
	"ILCacheMaxDiskFileCount": 0,
	"ILCacheMaxDiskMegabytes": 0
}
```

- IgnoredExtensions
	- 指定拡張子は比較対象から除外します（例: .pdb）。
- TextFileExtensions
	- 指定拡張子のファイルはテキストとして行単位で比較します。
	- ピリオド（`.`）付きの拡張子で指定してください（例: `.cs`, `.json`, `.xml`）。
- MaxLogGenerations
	- アプリケーションログのローテーション世代数
- ShouldIncludeUnchangedFiles
	- `Reports/<コマンドライン第3引数に指定したレポートのラベル>/diff_report.md`にUnchangedのファイル一覧を含めるか否か
- ShouldOutputILText
	- `Reports/<コマンドライン第3引数に指定したレポートのラベル>/IL/old, new`にIL全文を出力するか否か
- MaxParallelism
	- ファイル比較の並列度。0 または未指定で論理コア数、自動判定。1 で逐次実行。
- EnableILCache
	- IL 逆アセンブル結果（MD5 + ツール / バージョン単位）をメモリ & 任意ディスクにキャッシュし再実行時の逆アセンブルをスキップ。
- ILCacheDirectoryAbsolutePath
	- キャッシュ格納ディレクトリ。空 / 未指定で実行ディレクトリ配下 `ILCache`。容量制御 (LRU) と TTL（現在 12h）あり。
 - ILCacheStatsLogIntervalSeconds
	- IL キャッシュの内部統計（ヒット率など）をログへ出力する間隔（秒）。0 以下で 60 秒既定。
 - ILCacheMaxDiskFileCount
	- ディスク IL キャッシュの最大ファイル数。0 以下で無制限。超過時は最終アクセスの古い順に削除。
 - ILCacheMaxDiskMegabytes
	- ディスク IL キャッシュのサイズ上限（MB）。0 以下で無制限。超過時はサイズが下回るまで古い順に削除。

補足:
- 拡張子がないファイルも比較対象です。テキスト扱いにしたい場合はTextFileExtensionsに空文字（""）を含める運用を検討してください。
- .NET の「拡張子なし実行ファイル」（apphost）は、状況により再ビルドしてもMD5ハッシュが変わらないことがあります。

## アプリケーションの使用方法

1) `config.json`（実行ファイルと同じフォルダに配置されています）の内容を確認・修正します。
2) コマンドライン第1引数に「旧バージョン側（比較元）フォルダの絶対パス」、第2引数に「新バージョン側（比較先）フォルダの絶対パス」、第3引数に「レポートのラベル」を指定して実行します。
3) --no-pauseオプションをつけることで、終了時のキー入力待ちをスキップすることができます。
	- オプションをつけていなくても、非対話（リダイレクトされている）の場合はスキップされます。

ビルド・実行（例）:
```bash
dotnet build
dotnet run "/Users/UserA/workspace/old" "/Users/UserA/workspace/new" "YYYYMMDD" --no-pause
```

実行するとコンソールに進捗率が表示され、完了後`Reports/<コマンドライン第3引数に指定したレポートのラベル>/diff_report.md`にレポートが生成されます。

出力完了後、以下の生成物は読み取り専用（ReadOnly 属性）に変更されます（失敗時は警告を出し処理は継続）。
- `diff_report.md`
- `IL/old/*_IL.txt`（`config.json`のShouldOutputILText が true の場合）
- `IL/new/*_IL.txt`（`config.json`のShouldOutputILText が true の場合）

## 副生成物

- `Logs/log_YYYYMMDD.log` … アプリケーションログ（`config.json`のMaxLogGenerationsを超えるアプリケーションログがあった場合、古いものから順に削除されます。）
- 以下は`config.json`のShouldOutputILTextがtrueの場合のみ生成されます。
	- `Reports/<コマンドライン第3引数に指定したレポートのラベル>/IL/old/*.txt` … 旧バージョン側（比較元）ファイルのビルド固有情報を除く IL 全文を出力（ファイル名称は相対パスの区切り文字を.に置換したもの）
	- `Reports/<コマンドライン第3引数に指定したレポートのラベル>/IL/new/*.txt` … 新バージョン側（比較先）ファイルのビルド固有情報を除く IL 全文を出力（ファイル名称は相対パスの区切り文字を.に置換したもの）
		- 出力されるIL 全文は「`// MVID:`」で始まる行を除外しています。

## パフォーマンス最適化機能

| 機能 | 概要 | 備考 |
|------|------|------|
| 並列処理 | ファイル比較を最大 `MaxParallelism` 並列 | I/O/CPU バランス最適化 |
| IL キャッシュ | MD5 + ツールラベル (コマンド + バージョン) で IL テキストを再利用 | LRU (capacity=2000), TTL=12h, ディスク永続化可 |
| MD5 プリウォーム | 全対象ファイルの MD5 を先読み並列計算 | キャッシュキー生成の待ち時間平準化 |
| IL キャッシュ先読み | 既存ディスク IL キャッシュをメモリへ昇格 | 初回以降の逆アセンブル起動を更に削減 |
| 並列テキスト差分 | 512KB 以上のテキストを 64KB チャンクで並列バイト比較 | 完全一致判定のみ（差分位置抽出なし） |
| ツール失敗ブラックリスト | 同一ツール連続失敗 (既定 3 回) で 10 分間スキップ | 起動オーバーヘッド削減 |
| 統計出力 | 一定間隔でログにヒット率出力 | 解析・チューニング指標 |

### IL キャッシュ補足

- キャッシュファイル名にツールバージョンを含める際の `:` (version: x.y.z) による NTFS 代替データストリーム誤解釈を回避するため、
- ファイル名サニタイズ（不正文字/コロンを `_` へ置換 + 長大名短縮）を実施しています。
- ディスクキャッシュは LRU とは別に設定値 `ILCacheMaxDiskFileCount` と `ILCacheMaxDiskMegabytes` に基づいて、閾値超過時に古い順（ファイルの最終更新時刻ベース）でトリミングされます。

## バージョニング（Gitタグ連携）

このプロジェクトは [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) を用いてSemVerを自動付与します。
- `version.json`の設定に基づき、`main`ブランチや`v1.2.3`のようなタグでパブリックリリースとして扱われます。
- 生成される`AssemblyInformationalVersion`は`Reports/<コマンドライン第3引数に指定したレポートのラベル>/diff_report.md`に記録されます（コンソール出力は省略）。
- 手動で上書きしたい場合は`dotnet build /p:Version=1.2.3`のようにプロパティ指定も可能です。

タグ付け例:
```bash
git tag v1.0.0
git push origin v1.0.0
```