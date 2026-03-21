# Folder Diff Report
- App Version: FolderDiffIL4DotNet 1.0.0
- Computer: dev-machine
- Old: /Users/UserA/workspace/old
- New: /Users/UserA/workspace/new
- Ignored Extensions: .cache, .DS_Store, .db, .ilcache, .log, .pdb
- Text File Extensions: .asax, .ascx, .asmx, .aspx, .bat, .c, .cmd, .config, .cpp, .cs, .cshtml, .csproj, .csx, .css, .csv, .editorconfig, .env, .fs, .fsi, .fsproj, .fsx, .gitattributes, .gitignore, .gitmodules, .go, .gql, .graphql, .h, .hpp, .htm, .html, .http, .ini, .js, .json, .jsx, .less, .manifest, .md, .mod, .nlog, .nuspec, .plist, .props, .ps1, .psd1, .psm1, .py, .razor, .resx, .rst, .sass, .scss, .sh, .sln, .sql, .sqlproj, .sum, .svg, .targets, .toml, .ts, .tsv, .tsx, .txt, .vb, .vbproj, .vue, .xaml, .xml, .yaml, .yml
- IL Disassembler: dotnet-ildasm (version: 0.12.2)
- Elapsed Time: 0h 0m 1.2s
- Timestamps (timezone): +09:00
- Note: When diffing IL, lines starting with "// MVID:" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.
- Note: When diffing IL, lines containing any of the configured strings are ignored:

| Ignored String |
|----------------|
| "buildserver1_" |
| "buildserver2_" |
| "// Method begins at Relative Virtual Address (RVA) 0x" |
| ".publickeytoken = ( " |
| ".custom instance void class [System.Windows.Forms]System.Windows.Forms.AxHost/TypeLibraryTimeStampAttribute::.ctor(string) = ( " |
| "// Code size " |
- Legend:

| Label | Description |
|-------|-------------|
| `MD5Match` / `MD5Mismatch` | MD5 hash match / mismatch |
| `ILMatch` / `ILMismatch` | IL(Intermediate Language) match / mismatch |
| `TextMatch` / `TextMismatch` | Text match / mismatch |

## [ x ] Ignored Files (3)

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|--------|--------------|
| `[ x ]` | /Users/UserA/workspace/old/logs/debug.log (old) | 2026-03-15 08:50:00 | | |
| `[ x ]` | /Users/UserA/workspace/new/obj/build.cache (new) | 2026-03-15 09:05:00 | | |
| `[ x ]` | bin/App.pdb (old/new) | 2026-03-15 08:57:00 → 2026-03-15 09:03:00 | | |

## [ = ] Unchanged Files (5)

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|--------|--------------|
| `[ = ]` | data/schema.bin | 2026-03-15 08:30:00 → 2026-03-15 09:00:00 | `MD5Match` | |
| `[ = ]` | vendor/lib.dll | 2026-03-15 09:00:00 | `MD5Match` | |
| `[ = ]` | util/Helper.dll | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `ILMatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ = ]` | appsettings.json | 2026-03-15 09:00:00 | `TextMatch` | |
| `[ = ]` | docs/notes.md | 2026-03-15 08:00:00 → 2026-03-15 09:00:00 | `TextMatch` | |

## [ + ] Added Files (1)

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|--------|--------------|
| `[ + ]` | /Users/UserA/workspace/new/docs/guide.md | 2026-03-15 09:01:00 | | |

## [ - ] Removed Files (1)

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|--------|--------------|
| `[ - ]` | /Users/UserA/workspace/old/legacy/old-tool.txt | 2026-03-15 08:55:00 | | |

## [ * ] Modified Files (12)

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|--------|--------------|
| `[ * ]` | config/app.config | 2026-03-15 08:56:00 → 2026-03-15 09:01:00 | `TextMismatch` | |
| `[ * ]` | config/settings.ini | 2026-03-15 09:08:00 → 2026-03-15 09:01:00 | `TextMismatch` | |
| `[ * ]` | src/DataModel.edmx | 2026-03-15 08:55:00 → 2026-03-15 09:04:00 | `TextMismatch` | |
| `[ * ]` | src/LargeConfig.xml | 2026-03-15 08:54:00 → 2026-03-15 09:05:00 | `TextMismatch` | |
| `[ * ]` | src/Strings.resx | 2026-03-15 08:57:00 → 2026-03-15 09:03:00 | `TextMismatch` | |
| `[ * ]` | src/Web.config | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `TextMismatch` | |
| `[ * ]` | lib/Core.dll | 2026-03-15 09:12:00 → 2026-03-15 09:03:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/App.dll | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/BigModule.dll | 2026-03-15 09:10:00 → 2026-03-15 09:02:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/Service.dll | 2026-03-15 09:05:00 → 2026-03-15 09:00:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | util/Legacy.dll | 2026-03-15 08:50:00 → 2026-03-15 09:01:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `MD5Mismatch` | |

## Summary

| Category | Count |
|----------|------:|
| Ignored | 3 |
| Unchanged | 5 |
| Added | 1 |
| Removed | 1 |
| Modified | 12 |
| Compared | 21 (Old) vs 21 (New) |

## Assembly Semantic Changes

> **Note / 注:** The semantic summary is supplementary information. Always verify the final details in the inline IL diff. / セマンティックサマリーは補助情報です。最終確認は必ず IL インライン差分で行ってください。

### lib/Core.dll

| Class | BaseType | Change | Kind | Access | Modifiers | Type | Name | ReturnType | Parameters | Body |
|-------|----------|:------:|:----:|:------:|:---------:|------|------|------------|------------|------|
| MyApp.CoreEngine |  | `Added` | `Method` | `public` |  |  | Initialize | System.Void |  |  |
|  |  | `Removed` | `Method` | `public` | `virtual` |  | LegacyInit | System.Void | System.String\u00A0config |  |
|  |  | `Modified` | `Method` | `internal` |  |  | ProcessData | System.Boolean | System.Int32\u00A0id | `Changed` |
|  |  | `Modified` | `Property` | `public` |  | System.String | Status |  |  |  |

| Class | Change | Count |
|-------|:------:|------:|
| MyApp.CoreEngine | `Added` | 1 |
|  | `Removed` | 1 |
|  | `Modified` | 2 |

### src/App.dll

| Class | BaseType | Change | Kind | Access | Modifiers | Type | Name | ReturnType | Parameters | Body |
|-------|----------|:------:|:----:|:------:|:---------:|------|------|------------|------------|------|
| MyApp.UserService |  | `Modified` | `Method` | `internal` |  |  | HandleRequest | System.Void | System.String\u00A0request | `Changed` |

| Class | Change | Count |
|-------|:------:|------:|
| MyApp.UserService | `Modified` | 1 |

### src/BigModule.dll

- No structural changes detected. See IL diff for implementation-level differences.

### src/Service.dll

| Class | BaseType | Change | Kind | Access | Modifiers | Type | Name | ReturnType | Parameters | Body |
|-------|----------|:------:|:----:|:------:|:---------:|------|------|------------|------------|------|
| MyApp.Service |  | `Modified` | `Field` | `public` | `readonly` | System.Int32 | MaxRetries |  |  |  |

| Class | Change | Count |
|-------|:------:|------:|
| MyApp.Service | `Modified` | 1 |

### util/Legacy.dll

| Class | BaseType | Change | Kind | Access | Modifiers | Type | Name | ReturnType | Parameters | Body |
|-------|----------|:------:|:----:|:------:|:---------:|------|------|------------|------------|------|
| Legacy.Helper |  | `Modified` | `Method` | `public` |  |  | Convert | System.String | System.Object\u00A0input | `Changed` |

| Class | Change | Count |
|-------|:------:|------:|
| Legacy.Helper | `Modified` | 1 |

## IL Cache Stats

| Metric | Value |
|--------|------:|
| Hits | 42 |
| Misses | 8 |
| Hit Rate | 84.0% |
| Stores | 8 |
| Evicted | 0 |
| Expired | 0 |

## Warnings
- **WARNING:** One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.
- **WARNING:** One or more **modified** files in `new` have older last-modified timestamps than the corresponding files in `old`.

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|--------|--------------|
| `[ * ]` | config/settings.ini | 2026-03-15 09:08:00 → 2026-03-15 09:01:00 | `TextMismatch` | |
| `[ * ]` | lib/Core.dll | 2026-03-15 09:12:00 → 2026-03-15 09:03:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/BigModule.dll | 2026-03-15 09:10:00 → 2026-03-15 09:02:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/Service.dll | 2026-03-15 09:05:00 → 2026-03-15 09:00:00 | `ILMismatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `MD5Mismatch` | |
