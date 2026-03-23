# Folder Diff Report

| Property | Value |
|----------|-------|
| App Version | FolderDiffIL4DotNet 1.0.0.0+98587820469656dc0794524de1e1bf03ae3f2b8a |
| Computer | dev-machine |
| Old | /Users/UserA/workspace/old |
| New | /Users/UserA/workspace/new |
| Timezone | +09:00 |
| Elapsed Time | 0h 0m 1.2s |

| Tool | Available | Version |
|------|:---------:|---------|
| dotnet-ildasm | Yes | 0.12.2 — **In Use** |
| ilspycmd | No | N/A |

### Configuration Details

| Setting | Value |
|---------|-------|
| Ignored Extensions | .cache, .DS_Store, .db, .ilcache, .log, .pdb |
| Text File Extensions | .asax, .ascx, .asmx, .aspx, .bat, .c, .cmd, .config, .cpp, .cs, .cshtml, .csproj, .csx, .css, .csv, .editorconfig, .env, .fs, .fsi, .fsproj, .fsx, .gitattributes, .gitignore, .gitmodules, .go, .gql, .graphql, .h, .hpp, .htm, .html, .http, .ini, .js, .json, .jsx, .less, .manifest, .md, .mod, .nlog, .nuspec, .plist, .props, .ps1, .psd1, .psm1, .py, .razor, .resx, .rst, .sass, .scss, .sh, .sln, .sql, .sqlproj, .sum, .svg, .targets, .toml, .ts, .tsv, .tsx, .txt, .vb, .vbproj, .vue, .xaml, .xml, .yaml, .yml |

**IL Ignored Strings** — When diffing IL, lines containing any of the configured strings are ignored:

| Ignored String |
|----------------|
| "buildserver1_" |
| "buildserver2_" |
| "// Method begins at Relative Virtual Address (RVA) 0x" |
| ".publickeytoken = ( " |
| ".custom instance void class [System.Windows.Forms]System.Windows.Forms.AxHost/TypeLibraryTimeStampAttribute::.ctor(string) = ( " |
| "// Code size " |

> Note: When diffing IL, lines starting with "// MVID:" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.

### Legend — Diff Detail

| Label | Description |
|-------|-------------|
| `SHA256Match` / `SHA256Mismatch` | SHA256 hash match / mismatch |
| `ILMatch` / `ILMismatch` | IL(Intermediate Language) match / mismatch |
| `TextMatch` / `TextMismatch` | Text match / mismatch |

### Legend — Change Importance

| Label | Description |
|-------|-------------|
| `High` | Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change |
| `Medium` | Notable change: public/protected member addition, modifier change, access widening, internal removal |
| `Low` | Low-impact change: body-only modification, internal/private member addition |

## [ x ] Ignored Files (3)

| Status | File Path | Timestamp | Legend |
|:------:|-----------|:---------:|:------:|
| `[ x ]` | /Users/UserA/workspace/old/logs/debug.log (old) | 2026-03-15 08:50:00 | |
| `[ x ]` | /Users/UserA/workspace/new/obj/build.cache (new) | 2026-03-15 09:05:00 | |
| `[ x ]` | bin/App.pdb (old/new) | 2026-03-15 08:57:00 → 2026-03-15 09:03:00 | |

## [ = ] Unchanged Files (5)

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|:------:|--------------|
| `[ = ]` | data/schema.bin | 2026-03-15 08:30:00 → 2026-03-15 09:00:00 | `SHA256Match` | |
| `[ = ]` | vendor/lib.dll | 2026-03-15 09:00:00 | `SHA256Match` | |
| `[ = ]` | util/Helper.dll | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `ILMatch` | `dotnet-ildasm (version: 0.12.2)` |
| `[ = ]` | appsettings.json | 2026-03-15 09:00:00 | `TextMatch` | |
| `[ = ]` | docs/notes.md | 2026-03-15 08:00:00 → 2026-03-15 09:00:00 | `TextMatch` | |

## [ + ] Added Files (1)

| Status | File Path | Timestamp |
|:------:|-----------|:---------:|
| `[ + ]` | /Users/UserA/workspace/new/docs/guide.md | 2026-03-15 09:01:00 |

## [ - ] Removed Files (1)

| Status | File Path | Timestamp |
|:------:|-----------|:---------:|
| `[ - ]` | /Users/UserA/workspace/old/legacy/old-tool.txt | 2026-03-15 08:55:00 |

## [ * ] Modified Files (14)

| Status | File Path | Timestamp | Legend | Disassembler |
|:------:|-----------|:---------:|:------:|--------------|
| `[ * ]` | config/app.config | 2026-03-15 08:56:00 → 2026-03-15 09:01:00 | `TextMismatch` | |
| `[ * ]` | config/settings.ini | 2026-03-15 09:08:00 → 2026-03-15 09:01:00 | `TextMismatch` | |
| `[ * ]` | src/DataModel.edmx | 2026-03-15 08:55:00 → 2026-03-15 09:04:00 | `TextMismatch` | |
| `[ * ]` | src/LargeConfig.xml | 2026-03-15 08:54:00 → 2026-03-15 09:05:00 | `TextMismatch` | |
| `[ * ]` | src/Strings.resx | 2026-03-15 08:57:00 → 2026-03-15 09:03:00 | `TextMismatch` | |
| `[ * ]` | src/Web.config | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `TextMismatch` | |
| `[ * ]` | lib/Core.dll | 2026-03-15 09:12:00 → 2026-03-15 09:03:00 | `ILMismatch` `High` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/ApiClient.dll | 2026-03-15 08:55:00 → 2026-03-15 09:04:00 | `ILMismatch` `High` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/App.dll | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `ILMismatch` `Medium` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/BigModule.dll | 2026-03-15 09:10:00 → 2026-03-15 09:02:00 | `ILMismatch` `Medium` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | lib/Logging.dll | 2026-03-15 08:52:00 → 2026-03-15 09:03:00 | `ILMismatch` `Medium` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | src/Service.dll | 2026-03-15 09:05:00 → 2026-03-15 09:00:00 | `ILMismatch` `Low` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | util/Legacy.dll | 2026-03-15 08:50:00 → 2026-03-15 09:01:00 | `ILMismatch` `Low` | `dotnet-ildasm (version: 0.12.2)` |
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `SHA256Mismatch` | |

## Summary

| Category | Count |
|----------|------:|
| Ignored | 3 |
| Unchanged | 5 |
| Added | 1 |
| Removed | 1 |
| Modified | 14 |
| Compared | 23 (Old) vs 23 (New) |

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
- **WARNING:** One or more files were classified as `SHA256Mismatch`. Manual review is recommended because only a SHA256 hash comparison was possible.

### [ ! ] Modified Files — SHA256Mismatch (Manual Review Recommended) (1)

| Status | File Path | Timestamp | Legend |
|:------:|-----------|:---------:|:------:|
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `SHA256Mismatch` |

- **WARNING:** One or more **modified** files in `new` have older last-modified timestamps than the corresponding files in `old`.

### [ ! ] Modified Files — Timestamps Regressed (5)

| Status | File Path | Timestamp | Legend |
|:------:|-----------|:---------:|:------:|
| `[ * ]` | config/settings.ini | 2026-03-15 09:08:00 → 2026-03-15 09:01:00 | `TextMismatch` |
| `[ * ]` | lib/Core.dll | 2026-03-15 09:12:00 → 2026-03-15 09:03:00 | `ILMismatch` `High` |
| `[ * ]` | src/BigModule.dll | 2026-03-15 09:10:00 → 2026-03-15 09:02:00 | `ILMismatch` `Medium` |
| `[ * ]` | src/Service.dll | 2026-03-15 09:05:00 → 2026-03-15 09:00:00 | `ILMismatch` `Low` |
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `SHA256Mismatch` |
