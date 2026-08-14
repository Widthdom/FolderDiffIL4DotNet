# Real IL output corpus

## English

This directory contains golden IL emitted from the same small, deterministic test assembly by two supported disassemblers:

- `dotnet-ildasm 0.12.2.0`: `dotnet-ildasm-0.12.2.il`
- `ilspycmd 9.1.0.7988`: `ilspycmd-9.1.0.7988.il`

The sample covers ordinary nested classes and methods, instance and static constructors, async and iterator state machines, lambdas and compiler-generated types, generic types and methods, properties, events, fields, a strong-name-signed assembly, signed framework AssemblyRefs, and reproducible COM interop metadata.

The fixtures establish that `dotnet-ildasm` emits `// Method begins at Relative Virtual Address (RVA) 0x` and `// Code size `, while `ilspycmd` emits `// Method begins at RVA 0x` and `// Code size: `. They also establish that dotnet-ildasm custom-attribute signatures include `class ` before the assembly-qualified attribute type while ilspycmd signatures omit it. nildiff keeps separate built-in prefixes for these tool-specific forms and maps equivalent forms to the same normalization markers. All built-in rules are applied to every IL text; the disassembler name is provenance for the observed syntax, not an application condition.

`nildiff` itself targets `net8.0` and requires the .NET 8 runtime. `ilspycmd 9.1.0.7988` is pinned as the fallback baseline for inspecting assemblies that target .NET 8, .NET 9, or .NET 10; those target-framework versions do not describe the runtime used to execute `nildiff`.

The committed `ILCorpus.TestKey.snk` is a test-only strong-name key. It is public test material, not a credential, and must never be used to sign production assemblies.

ActiveX wrapper output is not included. Reproducing `AxHost.TypeLibraryTimeStampAttribute` requires Windows-specific `aximp` input and tooling that are not part of the .NET SDK, so it would not be a portable, reproducible fixture. The type-specific normalization tests use each disassembler's custom-attribute grammar established by the committed fixtures and model ilspycmd's multiline byte blob through its closing `)`. The source-level COM interface and coclass metadata are included instead.

From the repository root, regenerate both files with:

```powershell
pwsh -File FolderDiffIL4DotNet.Tests/Fixtures/ILCorpus/regenerate.ps1
```

Regeneration requires PowerShell 7 (`pwsh`), .NET SDK 8.0.423 from `global.json`, and both pinned disassemblers on `PATH`. The script uses cross-platform PowerShell APIs and the same command works on Windows, macOS, and Linux; fixture output should still be reviewed on the platform where it was generated.

Review fixture changes before committing them. Disassembler upgrades intentionally require updating the pinned filenames, version checks, tests, and this document together.

## 日本語

このディレクトリには、同じ小型の決定的テストAssemblyを、サポート対象の2つの逆アセンブラで処理したgolden ILを保存しています。

- `dotnet-ildasm 0.12.2.0`: `dotnet-ildasm-0.12.2.il`
- `ilspycmd 9.1.0.7988`: `ilspycmd-9.1.0.7988.il`

サンプルには、通常のclass内の複数method、instance/static constructor、async/iterator state machine、lambda/compiler-generated type、generic type/method、property/event/field、strong-name署名Assembly、署名されたframework AssemblyRef、再現可能なCOM interop metadataが含まれます。

fixtureにより、`dotnet-ildasm` は `// Method begins at Relative Virtual Address (RVA) 0x` と `// Code size `、`ilspycmd` は `// Method begins at RVA 0x` と `// Code size: ` を出力することを固定しています。また、dotnet-ildasmのcustom attributeシグネチャはAssembly修飾付きattribute型の前に `class ` を含み、ilspycmdは含まないことも固定しています。nildiffはツール固有の形式ごとに組み込み接頭辞を持ち、同等の形式を同じ正規化マーカーへ変換します。すべての組み込み規則は全IL textへ適用され、逆アセンブラ名は確認した構文の由来を示すだけで適用条件ではありません。

`nildiff` 本体のtarget frameworkは `net8.0` で、実行には.NET 8 runtimeが必要です。`ilspycmd 9.1.0.7988` は、.NET 8、.NET 9、.NET 10をtarget frameworkとするAssemblyを調査するためのfallback baselineとして固定しています。これらのtarget framework versionは、`nildiff` 自体を実行するruntimeを意味しません。

コミットされている `ILCorpus.TestKey.snk` はテスト専用strong-name鍵です。公開テスト資材でありcredentialではありません。production Assemblyの署名には使用しないでください。

ActiveX wrapper出力は含めていません。`AxHost.TypeLibraryTimeStampAttribute` の再現には、.NET SDKに含まれないWindows固有の `aximp` 入力とツールが必要で、portableかつ再現可能なfixtureにできないためです。型固有の正規化テストでは、コミット済みfixtureで確認した各逆アセンブラのcustom attribute文法を使い、ilspycmdの複数行byte blobを閉じ `)` まで再現します。代わりにソースから再現できるCOM interfaceとcoclass metadataを含めています。

リポジトリルートから、次のコマンドで両方のファイルを再生成します。

```powershell
pwsh -File FolderDiffIL4DotNet.Tests/Fixtures/ILCorpus/regenerate.ps1
```

再生成にはPowerShell 7（`pwsh`）、`global.json` で固定した.NET SDK 8.0.423、`PATH` 上の両固定バージョン逆アセンブラが必要です。このスクリプトはcross-platformなPowerShell APIを使用しており、Windows、macOS、Linuxで同じコマンドを使えますが、生成したplatform上でfixture差分を確認してください。

コミット前にfixture差分を確認してください。逆アセンブラを更新する場合は、固定バージョン入りファイル名、バージョン検証、テスト、この文書を同時に更新します。
