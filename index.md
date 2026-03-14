# FolderDiffIL4DotNet Documentation

This site brings together the existing hand-written guides and an API reference generated from XML documentation comments.

Start here:

- [README](README.md): product overview, setup, usage, and configuration
- [Developer Guide](doc/DEVELOPER_GUIDE.md): architecture, execution flow, DI scopes, and implementation guardrails
- [Testing Guide](doc/TESTING_GUIDE.md): test strategy, local commands, and isolation rules
- [API Reference](api/index.md): generated type/member reference for the public surface

To refresh the generated API reference locally:

```bash
dotnet build FolderDiffIL4DotNet.sln --configuration Release
dotnet tool update --global docfx --version '2.*'
export PATH="$PATH:$HOME/.dotnet/tools"
docfx metadata docfx.json
docfx build docfx.json
```

The generated site is written to `_site/`.

---

# FolderDiffIL4DotNet ドキュメント

このサイトは、既存の手書きガイド群と、XML ドキュメントコメントから自動生成する API リファレンスをまとめたものです。

主な入口:

- [README](README.md): 製品概要、導入、使い方、設定
- [開発者ガイド](doc/DEVELOPER_GUIDE.md): アーキテクチャ、実行フロー、DI スコープ、実装上の注意点
- [テストガイド](doc/TESTING_GUIDE.md): テスト戦略、ローカル実行コマンド、分離ルール
- [API リファレンス](api/index.md): 公開 API の型・メンバー情報を自動生成した参照資料

ローカルで API リファレンスを更新する手順:

```bash
dotnet build FolderDiffIL4DotNet.sln --configuration Release
dotnet tool update --global docfx --version '2.*'
export PATH="$PATH:$HOME/.dotnet/tools"
docfx metadata docfx.json
docfx build docfx.json
```

生成されたサイトは `_site/` に出力されます。
