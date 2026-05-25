# precommit.md

## English

1. Check `git status --short`.
2. Use `cdidx` for final searches.
3. Run the relevant tests.
4. Run `dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --nologo` when possible.
5. Verify docs, changelog, and sample updates.
6. Do not use `git add .` or `git add -A`; add explicit files only.
7. Do not commit secrets.

## 日本語

1. `git status --short` を確認する。
2. 最終検索は `cdidx` を使う。
3. 関連するテストを実行する。
4. 可能なら `dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --nologo` を走らせる。
5. ドキュメント、CHANGELOG、サンプル更新を確認する。
6. `git add .` や `git add -A` は使わず、明示的にファイルを追加する。
7. シークレットをコミットしない。
