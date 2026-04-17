# Folder Diff Report

| Property | Value |
|----------|-------|
| App Version | nildiff 1.0.0.1+98587820469656dc0794524de1e1bf03ae3f2b8a |
| Computer | dev-machine |
| Timezone | +09:00 |
| Elapsed Time | 0h 0m 1.2s |
| Old Folder | /Users/UserA/workspace/old |
| New Folder | /Users/UserA/workspace/new |

### Disassembler Availability

| Tool | Available | Version | In Use |
|------|:---------:|---------|:------:|
| dotnet-ildasm | Yes | 0.12.2 | Yes |
| ilspycmd | No | N/A | No |

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
| "rva" |

> Note: When diffing IL, lines starting with "// MVID:" (if present) are ignored because they contain disassembler-emitted Module Version ID metadata that can change on rebuild without meaning the executable IL changed.

### Legend — Diff Detail

| Label | Description |
|-------|-------------|
| `SHA256Match` / `SHA256Mismatch` | Byte-for-byte match / mismatch (SHA256) |
| `ILMatch` / `ILMismatch` | IL(Intermediate Language) match / mismatch |
| `TextMatch` / `TextMismatch` | Text-based match / mismatch |

### Legend — Change Importance

| Label | Description |
|-------|-------------|
| `High` | Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change |
| `Medium` | Notable change: public/protected member addition, modifier change, access widening, internal removal |
| `Low` | Low-impact change: body-only modification, internal/private member addition |

### Legend — Estimated Change

| Label | Description |
|-------|-------------|
| `+Method` | New method added |
| `-Method` | Method removed |
| `+Type` | New type added |
| `-Type` | Type removed |
| `Possible Extract` | Possible method body extraction to a new private/internal method |
| `Possible Inline` | Possible private/internal method inlining into another method |
| `Possible Move` | Possible method move between types |
| `Possible Rename` | Possible method rename (same signature and IL body) |
| `Signature` | Method/property signature changed |
| `Access` | Access modifier changed |
| `BodyEdit` | Method body IL changed only |
| `DepUpdate` | Dependency package version changed only |

## [ x ] Ignored Files (3)

| Status | File Path | Timestamp |
|:------:|-----------|:---------:|
| `[ x ]` | /Users/UserA/workspace/old/logs/debug.log (old) | 2026-03-15 08:50:00 |
| `[ x ]` | /Users/UserA/workspace/new/obj/build.cache (new) | 2026-03-15 09:05:00 |
| `[ x ]` | bin/App.pdb (old/new) | 2026-03-15 08:57:00 → 2026-03-15 09:03:00 |

## [ = ] Unchanged Files (5)

| Status | File Path | Timestamp | Diff Reason | Disassembler | .NET SDK |
|:------:|-----------|:---------:|:-----------:|--------------|:--------:|
| `[ = ]` | data/schema.bin | 2026-03-15 08:30:00 → 2026-03-15 09:00:00 | `SHA256Match` | | |
| `[ = ]` | vendor/lib.dll | 2026-03-15 09:00:00 | `SHA256Match` | | `.NET 8.0` |
| `[ = ]` | util/Helper.dll | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `ILMatch` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ = ]` | appsettings.json | 2026-03-15 09:00:00 | `TextMatch` | | |
| `[ = ]` | docs/notes.md | 2026-03-15 08:00:00 → 2026-03-15 09:00:00 | `TextMatch` | | |

## [ + ] Added Files (1)

| Status | File Path | Timestamp |
|:------:|-----------|:---------:|
| `[ + ]` | /Users/UserA/workspace/new/docs/guide.md | 2026-03-15 09:01:00 |

## [ - ] Removed Files (1)

| Status | File Path | Timestamp |
|:------:|-----------|:---------:|
| `[ - ]` | /Users/UserA/workspace/old/legacy/old-tool.txt | 2026-03-15 08:55:00 |

## [ * ] Modified Files (15)

| Status | File Path | Timestamp | Diff Reason | Estimated Change | Disassembler | .NET SDK |
|:------:|-----------|:---------:|:-----------:|:----------------:|--------------|:--------:|
| `[ * ]` | bin/MyApp.deps.json | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `TextMismatch` `High` | `DepUpdate` | | |
| `[ * ]` | config/app.config | 2026-03-15 08:56:00 → 2026-03-15 09:01:00 | `TextMismatch` | | | |
| `[ * ]` | config/settings.ini | 2026-03-15 09:08:00 → 2026-03-15 09:01:00 | `TextMismatch` | | | |
| `[ * ]` | src/DataModel.edmx | 2026-03-15 08:55:00 → 2026-03-15 09:04:00 | `TextMismatch` | | | |
| `[ * ]` | src/LargeConfig.xml | 2026-03-15 08:54:00 → 2026-03-15 09:05:00 | `TextMismatch` | | | |
| `[ * ]` | src/Strings.resx | 2026-03-15 08:57:00 → 2026-03-15 09:03:00 | `TextMismatch` | | | |
| `[ * ]` | src/Web.config | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `TextMismatch` | | | |
| `[ * ]` | lib/Core.dll | 2026-03-15 09:12:00 → 2026-03-15 09:03:00 | `ILMismatch` `High` | `-Method`, `Signature` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | src/ApiClient.dll | 2026-03-15 08:55:00 → 2026-03-15 09:04:00 | `ILMismatch` `High` | `+Type`, `+Method` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | src/App.dll | 2026-03-15 08:58:00 → 2026-03-15 09:02:00 | `ILMismatch` `Medium` | `Possible Extract`, `+Method` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | src/BigModule.dll | 2026-03-15 09:10:00 → 2026-03-15 09:02:00 | `ILMismatch` `Medium` | `Possible Move`, `Possible Rename` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | lib/Logging.dll | 2026-03-15 08:52:00 → 2026-03-15 09:03:00 | `ILMismatch` `Medium` | `Access` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | src/Service.dll | 2026-03-15 09:05:00 → 2026-03-15 09:00:00 | `ILMismatch` `Low` | `BodyEdit` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | util/Legacy.dll | 2026-03-15 08:50:00 → 2026-03-15 09:01:00 | `ILMismatch` `Low` | `Possible Inline` | `dotnet-ildasm (version: 0.12.2)` | `.NET 6.0` → `.NET 8.0` |
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `SHA256Mismatch` | | | |

#### Dependency Changes: bin/MyApp.deps.json

| Package | Status | Importance | Old Version | New Version | Vulnerabilities | Referencing Assemblies |
|---------|:------:|:----------:|:-----------:|:-----------:|:---------------:|:-----------------------|
| System.Text.Json | `[ - ]` | `High` | 7.0.0 | — | — | MyApp, MyApp.Core |
| Newtonsoft.Json | `[ + ]` | `Medium` | — | 13.0.3 | ⚠ Moderate | MyApp.Core |
| Serilog | `[ * ]` | `High` | 3.0.0 | 4.1.0 | ~~High~~ | MyApp |
| Microsoft.Extensions.Logging | `[ * ]` | `Medium` | 8.0.0 | 9.0.0 | — | MyApp, MyApp.Core, MyApp.Data |
| Microsoft.Extensions.DependencyInjection | `[ * ]` | `Low` | 9.0.0 | 9.0.8 | — | MyApp |

## Summary

| Category | Count |
|----------|------:|
| Ignored | 3 |
| Unchanged | 5 |
| Added | 1 |
| Removed | 1 |
| Modified | 15 |
| Compared | 24 (Old) vs 24 (New) |

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

### [ ! ] IL filter validation warnings (1)

- ILIgnoreLineContainingStrings: "rva" is very short (3 chars) and may inadvertently exclude legitimate IL lines. Consider using a more specific pattern.

### [ ! ] Modified Files — SHA256Mismatch: binary diff only — not a .NET assembly and not a recognized text file (1)

| Status | File Path | Timestamp | Diff Reason | Estimated Change | Disassembler | .NET SDK |
|:------:|-----------|:---------:|:-----------:|:----------------:|--------------|:--------:|
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `SHA256Mismatch` | | | |

### [ ! ] Modified Files — new file timestamps older than old (5)

| Status | File Path | Timestamp | Diff Reason | Estimated Change | Disassembler | .NET SDK |
|:------:|-----------|:---------:|:-----------:|:----------------:|--------------|:--------:|
| `[ * ]` | config/settings.ini | 2026-03-15 09:08:00 → 2026-03-15 09:01:00 | `TextMismatch` | | | |
| `[ * ]` | lib/Core.dll | 2026-03-15 09:12:00 → 2026-03-15 09:03:00 | `ILMismatch` `High` | `-Method`, `Signature` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | src/BigModule.dll | 2026-03-15 09:10:00 → 2026-03-15 09:02:00 | `ILMismatch` `Medium` | `Possible Move`, `Possible Rename` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | src/Service.dll | 2026-03-15 09:05:00 → 2026-03-15 09:00:00 | `ILMismatch` `Low` | `BodyEdit` | `dotnet-ildasm (version: 0.12.2)` | `.NET 8.0` |
| `[ * ]` | payload.bin | 2026-03-15 08:59:00 → 2026-03-15 08:54:00 | `SHA256Mismatch` | | | |

## Review Checklist

| ✓ | Checklist Item | Notes |
|:-:|----------------|-------|
| ☐ | Confirm version.json and release notes are aligned. | |
| ☐ | Verify upgrade guide steps.<br>Include rollback notes if applicable. | |
