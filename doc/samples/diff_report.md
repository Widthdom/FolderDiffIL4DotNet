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
- Note: When diffing IL, lines containing any of the configured strings are ignored: "buildserver1_", "buildserver2_", "// Method begins at Relative Virtual Address (RVA) 0x", ".publickeytoken = ( ", ".custom instance void class [System.Windows.Forms]System.Windows.Forms.AxHost/TypeLibraryTimeStampAttribute::.ctor(string) = ( ", "// Code size ".
- Legend:
  - `MD5Match` / `MD5Mismatch`: MD5 hash match / mismatch
  - `ILMatch` / `ILMismatch`: IL(Intermediate Language) match / mismatch
  - `TextMatch` / `TextMismatch`: Text match / mismatch

## [ x ] Ignored Files (3)
- [ x ] /Users/UserA/workspace/old/logs/debug.log (old) [2026-03-15 08:50:00]
- [ x ] /Users/UserA/workspace/new/obj/build.cache (new) [2026-03-15 09:05:00]
- [ x ] bin/App.pdb (old/new) [2026-03-15 08:57:00 → 2026-03-15 09:03:00]

## [ = ] Unchanged Files (5)
- [ = ] vendor/lib.dll [2026-03-15 09:00:00] `MD5Match`
- [ = ] appsettings.json [2026-03-15 09:00:00] `TextMatch`
- [ = ] data/schema.bin [2026-03-15 08:30:00 → 2026-03-15 09:00:00] `MD5Match`
- [ = ] docs/notes.md [2026-03-15 08:00:00 → 2026-03-15 09:00:00] `TextMatch`
- [ = ] util/Helper.dll [2026-03-15 08:58:00 → 2026-03-15 09:02:00] `ILMatch` `dotnet-ildasm (version: 0.12.2)`

## [ + ] Added Files (1)
- [ + ] /Users/UserA/workspace/new/docs/guide.md [2026-03-15 09:01:00]

## [ - ] Removed Files (1)
- [ - ] /Users/UserA/workspace/old/legacy/old-tool.txt [2026-03-15 08:55:00]

## [ * ] Modified Files (9)
- [ * ] config/app.config [2026-03-15 08:56:00 → 2026-03-15 09:01:00] `TextMismatch`
- [ * ] payload.bin [2026-03-15 08:59:00 → 2026-03-15 08:54:00] `MD5Mismatch`
- [ * ] src/App.dll [2026-03-15 08:58:00 → 2026-03-15 09:02:00] `ILMismatch` `dotnet-ildasm (version: 0.12.2)`
- [ * ] src/Main.cs [2026-03-15 08:58:00 → 2026-03-15 09:02:00] `TextMismatch`
- [ * ] src/Service.dll [2026-03-15 09:05:00 → 2026-03-15 09:00:00] `ILMismatch` `dotnet-ildasm (version: 0.12.2)`
- [ * ] src/Utils.cs [2026-03-15 08:57:00 → 2026-03-15 09:03:00] `TextMismatch`
- [ * ] src/BigSchema.cs [2026-03-15 08:55:00 → 2026-03-15 09:04:00] `TextMismatch`
- [ * ] src/LargeConfig.xml [2026-03-15 08:54:00 → 2026-03-15 09:05:00] `TextMismatch`
- [ * ] util/Legacy.dll [2026-03-15 08:50:00 → 2026-03-15 09:01:00] `ILMismatch` `dotnet-ildasm (version: 0.12.2)`

## Summary
- Ignored   : 3
- Unchanged : 5
- Added     : 1
- Removed   : 1
- Modified  : 9
- Compared  : 18 (Old) vs 18 (New)

## Assembly Semantic Changes

### src/App.dll

| Class | Change | Kind | Access | Modifiers | Type | Name | ReturnType | Parameters | Body |
|-------|--------|------|--------|-----------|------|------|------------|------------|------|
| MyApp.Controllers.ApiController | `Added` | `Method` | `public` |  |  | HealthCheck | string |  |  |
|  | `Added` | `Method` | `public` |  |  | GetUsers | System.Collections.Generic.IList\<MyApp.Models.User\> | int page, int pageSize = 20 |  |
|  | `Removed` | `Method` | `public` |  |  | GetUsers | System.Collections.Generic.IList\<MyApp.Models.User\> | int page |  |
|  | `Modified` | `Method` | `public` | `virtual` |  | Search | System.Collections.Generic.IList\<MyApp.Models.User\> | string query | `Changed` |
|  | `Modified` | `Method` | `protected` |  |  | OnAuthorize | bool | MyApp.Models.UserContext ctx | `Changed` |
| MyApp.Services.DataService | `Modified` | `Method` | `internal` |  |  | RefreshCache | void |  | `Changed` |
|  | `Modified` | `Method` | `private` |  |  | ValidateConnection | bool | string connStr | `Changed` |
|  | `Added` | `Property` | `public` |  | int | CacheTimeout |  |  |  |
|  | `Added` | `Property` | `public` |  | MyApp.Models.CachePolicy | Policy |  |  |  |
|  | `Added` | `Property` | `internal` |  | MyApp.Services.IConnectionPool | ConnectionPool |  |  |  |

| Class | Change | Count |
|-------|--------|-------|
| MyApp.Controllers.ApiController | `Added` | 2 |
|  | `Modified` | 2 |
|  | `Removed` | 1 |
| MyApp.Services.DataService | `Added` | 3 |
|  | `Modified` | 2 |

### src/Service.dll

| Class | Change | Kind | Access | Modifiers | Type | Name | ReturnType | Parameters | Body |
|-------|--------|------|--------|-----------|------|------|------------|------------|------|
| MyApp.Services.NewValidator | `Added` | `Class` | `public` |  |  |  |  |  |  |
|  | `Added` | `Constructor` | `public` |  |  | NewValidator | void |  |  |
|  | `Added` | `Method` | `public` |  |  | Validate | bool | string input |  |
|  | `Added` | `Method` | `public` |  |  | Validate | bool | string input, MyApp.Models.ValidationOptions options |  |
|  | `Added` | `Method` | `private` |  |  | ParseInput | string | string raw |  |
|  | `Added` | `Property` | `public` |  | MyApp.Models.ValidationResult | LastResult |  |  |  |
|  | `Added` | `Field` | `private` | `readonly` | string | _pattern |  |  |  |
| MyApp.Services.OrderService | `Added` | `Method` | `public` |  |  | ValidateWithNewValidator | bool | string data |  |
|  | `Removed` | `Method` | `public` | `virtual` |  | LegacyValidate | bool | string data |  |
|  | `Modified` | `Method` | `public` |  |  | ProcessOrder | void | int orderId | `Changed` |
|  | `Modified` | `Method` | `internal` | `static` |  | CalculateTotal | decimal | int qty, int price | `Changed` |
|  | `Added` | `Property` | `protected` |  | MyApp.Models.OrderContext | CurrentContext |  |  |  |
|  | `Added` | `Field` | `private` | `readonly` | MyApp.Models.UserRecord | _defaultUser |  |  |  |
| MyApp.Services.LegacyHelper | `Removed` | `Class` | `internal` |  |  |  |  |  |  |
|  | `Removed` | `Method` | `public` |  |  | Convert | string | object value |  |
|  | `Removed` | `Method` | `public` | `static` |  | Format | string | string template, object[] args |  |
| MyApp.Models.UserRecord | `Added` | `Record` | `public` |  |  |  |  |  |  |
|  | `Added` | `Constructor` | `public` |  |  | UserRecord | void | string Name, int Age |  |
|  | `Added` | `Property` | `public` |  | string | Name |  |  |  |
|  | `Added` | `Property` | `public` |  | int | Age |  |  |  |
|  | `Added` | `Method` | `public` | `override` |  | ToString | string |  |  |
|  | `Added` | `Method` | `public` | `virtual` |  | Equals | bool | object obj |  |
|  | `Added` | `Method` | `public` | `override` |  | GetHashCode | int |  |  |
| MyApp.Models.UserDto | `Removed` | `Class` | `public` |  |  |  |  |  |  |
|  | `Removed` | `Property` | `public` |  | string | Name |  |  |  |
|  | `Removed` | `Property` | `public` |  | int | Age |  |  |  |

| Class | Change | Count |
|-------|--------|-------|
| MyApp.Models.UserDto | `Removed` | 3 |
| MyApp.Models.UserRecord | `Added` | 7 |
| MyApp.Services.LegacyHelper | `Removed` | 3 |
| MyApp.Services.NewValidator | `Added` | 7 |
| MyApp.Services.OrderService | `Added` | 3 |
|  | `Modified` | 2 |
|  | `Removed` | 1 |

### util/Legacy.dll
- Other changes only. See IL diff for details.

| Class | Change | Count |
|-------|--------|-------|

## IL Cache Stats
- Hits    : 42
- Misses  : 8
- Hit Rate: 84.0%
- Stores  : 8
- Evicted : 0
- Expired : 0

## Warnings
- **WARNING:** One or more files were classified as `MD5Mismatch`. Manual review is recommended because only an MD5 hash comparison was possible.
- **WARNING:** One or more files in `new` have older last-modified timestamps than the corresponding files in `old`.
  - payload.bin [2026-03-15 08:59:00 → 2026-03-15 08:54:00]
  - src/Service.dll [2026-03-15 09:05:00 → 2026-03-15 09:00:00]
