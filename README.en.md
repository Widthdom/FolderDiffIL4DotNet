# FolderDiffIL4DotNet (English)

This repository hosts a .NET console application that compares two folders, classifies the differences, and writes a detailed Markdown report. When both inputs are .NET assemblies, the app ignores build-specific artifacts such as the `// MVID:` line, so assemblies that behave the same are treated as equal even if they were produced at different times.

> Looking for the Japanese version? See [README.md](README.md).

## Requirements

- .NET SDK 8.x
- macOS / Windows / Linux / Unix-like OS (e.g., FreeBSD)
- IL disassembler (the app automatically probes candidates in this order)
  - Preferred: `dotnet-ildasm` or `dotnet ildasm`
  - Fallback: `ilspycmd`
- The app tries disassembler candidates **per file** in order. Tools that fail repeatedly are temporarily blacklisted and skipped.
- IL cache keys are built from `file MD5 + disassembler identity` (normally tool/version). If version lookup fails, the app falls back to a tool-binary fingerprint so caches from old/new tool builds do not get mixed.

Installation example:

```bash
dotnet tool install --global dotnet-ildasm
# add $HOME/.dotnet/tools (Unix) or %USERPROFILE%\.dotnet\tools (Windows) to PATH if necessary
# after installation, both invocations run the same tool
dotnet-ildasm --version
dotnet ildasm --version
```

```bash
dotnet tool install -g ilspycmd
# add $HOME/.dotnet/tools (Unix) or %USERPROFILE%\.dotnet\tools (Windows) to PATH if necessary
```

## CI (GitHub Actions)

The repository includes `.github/workflows/dotnet.yml`. The workflow runs for pushes and pull requests targeting `main`, and it can also be triggered manually via `workflow_dispatch`.

- `actions/checkout` uses `fetch-depth: 0` so Nerdbank.GitVersioning can traverse the full commit history.
- `actions/setup-dotnet` honors `global.json`, installing the same SDK (e.g., 8.0.413) that you use locally before running `dotnet restore` and a Release build.
- `dotnet test` runs only when `FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj` exists, so repositories still succeed even before the test project is introduced.
- NuGet packages inside `~/.nuget/packages` are cached via `actions/cache` to accelerate subsequent builds.
- Release artifacts are produced with `dotnet publish FolderDiffIL4DotNet.csproj --output publish`, stripped of debug symbols (`*.pdb`), and uploaded as `FolderDiffIL4DotNet` via `actions/upload-artifact`.

How to use it:

1. Push this repo to GitHub—no additional configuration required.
2. If your default branch is not `main`, edit `on.push.branches` and `on.pull_request.branches` in the workflow.
3. If you rename or move the test project, update the `Test` step in `.github/workflows/dotnet.yml` (both the `if` expression and the `dotnet test` `csproj` path).
4. Download the Release build from "Artifacts > FolderDiffIL4DotNet" on the workflow run page.

## Running tests

To run only unit tests locally, use:

```bash
dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --nologo
```

Prerequisites:

- .NET SDK 8.x is installed.
- `dotnet-ildasm` / `ilspycmd` are not required for unit tests (they are only needed for IL comparison in the application runtime path).
- The command is intended to be run from the repository root.

Relation to CI:

- The GitHub Actions `Test` step runs `dotnet test FolderDiffIL4DotNet.Tests/FolderDiffIL4DotNet.Tests.csproj --configuration Release --no-build --nologo`.
- At the moment, `FolderDiffIL4DotNet.Tests` is the only test project in the solution, so the local `csproj` command above targets the same tests.
- If more test projects are added later, either switch CI back to solution-level testing or add the additional target `csproj` as needed.

## What the app does

- Recursively compares the old folder (CLI arg #1) and the new folder (CLI arg #2).
- Tracks each file as `MD5Match`, `MD5Mismatch`, `ILMatch`, `ILMismatch`, `TextMatch`, or `TextMismatch` (for IL results, the used disassembler/version is also recorded).
- Groups files into `Unchanged`, `Added`, `Removed`, and `Modified` buckets.
- Writes per-bucket listings to `Reports/<report label>/diff_report.md`; paths are relative for `Unchanged`/`Modified` and absolute for `Added`/`Removed`.
- For `ILMatch` / `ILMismatch`, the report also includes the disassembler tool and version used (including cache hits).
- Summarizes counts per bucket in the same report.
- The report header lists only the disassembler labels actually observed during IL comparison (or `N/A` when not used).
- Optionally writes ignored files, unchanged files, timestamps, and warnings when at least one `MD5Mismatch` exists.

## File comparison flow

1. **MD5 hash** – if hashes match, the file is `Unchanged (MD5Match)`.
2. **IL diff** – if the file is a .NET assembly (detected via PE/CLR headers, regardless of extension), the app disassembles both versions with the same disassembler/version identity, strips `// MVID:` lines, and compares them line by line. Matches become `Unchanged (ILMatch)`; mismatches become `Modified (ILMismatch)`.
3. **Text diff** – if the extension appears in `TextFileExtensions`, a line-based text diff runs. Matches are `Unchanged (TextMatch)`; mismatches are `Modified (TextMismatch)`.
4. **Fallback** – remaining files are treated as `Modified (MD5Mismatch)`.

## Configuration (`config.json`)

Place `config.json` next to the executable. Example:

```json
{
  "IgnoredExtensions": [".cache", ".DS_Store", ".db", ".ilcache", ".log", ".pdb"],
  "TextFileExtensions": [
    ".asax",
    ".ascx",
    ".asmx",
    ".aspx",
    ".bat",
    ".c",
    ".cmd",
    ".config",
    ".cpp",
    ".cs",
    ".cshtml",
    ".csproj",
    ".csx",
    ".css",
    ".csv",
    ".editorconfig",
    ".env",
    ".fs",
    ".fsi",
    ".fsproj",
    ".fsx",
    ".gitattributes",
    ".gitignore",
    ".gitmodules",
    ".go",
    ".gql",
    ".graphql",
    ".h",
    ".hpp",
    ".htm",
    ".html",
    ".http",
    ".ini",
    ".js",
    ".json",
    ".jsx",
    ".less",
    ".manifest",
    ".md",
    ".mod",
    ".nlog",
    ".nuspec",
    ".plist",
    ".props",
    ".ps1",
    ".psd1",
    ".psm1",
    ".py",
    ".razor",
    ".resx",
    ".rst",
    ".sass",
    ".scss",
    ".sh",
    ".sln",
    ".sql",
    ".sqlproj",
    ".sum",
    ".svg",
    ".targets",
    ".toml",
    ".ts",
    ".tsv",
    ".tsx",
    ".txt",
    ".vb",
    ".vbproj",
    ".vue",
    ".xaml",
    ".xml",
    ".yaml",
    ".yml"
  ],
  "MaxLogGenerations": 5,
  "ShouldIncludeUnchangedFiles": true,
  "ShouldIncludeIgnoredFiles": true,
  "ShouldOutputILText": true,
  "ShouldOutputFileTimestamps": true,
  "MaxParallelism": 0,
  "EnableILCache": true,
  "ILCacheDirectoryAbsolutePath": "",
  "ILCacheStatsLogIntervalSeconds": 60,
  "ILCacheMaxDiskFileCount": 1000,
  "ILCacheMaxDiskMegabytes": 512,
  "OptimizeForNetworkShares": false,
  "AutoDetectNetworkShares": true
}
```

| Key | Description |
| --- | --- |
| `IgnoredExtensions` | Excludes matching extensions from comparison (e.g., `.pdb`). |
| `TextFileExtensions` | Treats matching extensions as text, diffed line by line. Include the dot (e.g., `.cs`, `.json`). |
| `MaxLogGenerations` | Number of log files kept in rotation. |
| `ShouldIncludeUnchangedFiles` | Whether to list `Unchanged` files inside `Reports/<label>/diff_report.md`. |
| `ShouldIncludeIgnoredFiles` | Whether to output ignored files in the `## [ x ] Ignored Files` section (before `Unchanged`). |
| `ShouldOutputILText` | Writes IL dumps to `Reports/<label>/IL/old` and `.../IL/new`. |
| `ShouldOutputFileTimestamps` | Adds last modified timestamps to each file line inside the report. |
| `MaxParallelism` | Degree of parallelism for file comparisons. `0` or omitted uses the logical core count. |
| `EnableILCache` | Caches IL disassembly results (MD5 + tool/version) in memory and optionally on disk. |
| `ILCacheDirectoryAbsolutePath` | Custom cache folder. Blank defaults to `<exe>/ILCache` with LRU + TTL control. |
| `ILCacheStatsLogIntervalSeconds` | Interval (seconds) for logging IL cache statistics. `<= 0` falls back to 60 seconds. |
| `ILCacheMaxDiskFileCount` | Upper bound for disk cache files. Default is `1000`. `<= 0` disables trimming. Oldest entries are removed first. |
| `ILCacheMaxDiskMegabytes` | Disk cache size limit (MB). Default is `512`. `<= 0` disables trimming. Oldest entries are removed until under the limit. |
| `OptimizeForNetworkShares` | Optimizes comparisons on NAS/SMB shares by skipping MD5 pre-warming, reducing parallelism, and forcing sequential diffing of large text files. |
| `AutoDetectNetworkShares` | Detects network paths automatically (UNC on Windows, `statfs` on macOS, `/proc/mounts`/`/etc/mtab` on Linux/Unix) and enables the same optimizations automatically. |

Notes:

- Files without extensions are still compared. Add an empty string to `TextFileExtensions` if you want them treated as text.
- .NET "extensionless" executables (apphosts) may keep the same MD5 even after rebuilding.

## Usage

1. Review and adjust `config.json` next to the executable.
2. Run the app with the following arguments:
   1. Absolute path to the old (baseline) folder.
   2. Absolute path to the new folder.
   3. Report label (used as the subfolder name under `Reports`).
   4. Optional `--no-pause` to skip the "Press any key" prompt.
3. The prompt is automatically skipped when the process is non-interactive (I/O redirection).
4. On errors, `--no-pause` also skips the key prompt; the process exits with code `1` (`0` on success).

Build and run example:

```bash
dotnet build
dotnet run "/Users/UserA/workspace/old" "/Users/UserA/workspace/new" "YYYYMMDD" --no-pause
```

The console shows progress, and after completion the report is available at `Reports/<label>/diff_report.md`.
Console color emphasis is intentionally minimal: only the final success message (`[INFO] ... finished without errors`) is shown in green, and only the exception error message (`[ERROR] ...`) is shown in red (all other logs use the default color).

After writing the report, the following files are marked read-only (failures only generate warnings):

- `diff_report.md`
- `IL/old/*_IL.txt` (when `ShouldOutputILText` is true)
- `IL/new/*_IL.txt` (when `ShouldOutputILText` is true)

## Generated artifacts

- `Logs/log_YYYYMMDD.log` – application logs. Entries older than `MaxLogGenerations` are deleted.
- If `ShouldOutputILText` is true:
  - `Reports/<label>/IL/old/*.txt` – IL dumps (build-specific noise removed) for files from the old folder.
  - `Reports/<label>/IL/new/*.txt` – IL dumps for the new folder.
  - IL dumps exclude lines that start with `// MVID:`.

## Performance optimizations

| Feature | Summary | Notes |
| --- | --- | --- |
| Parallel diffing | Compares files in parallel up to `MaxParallelism`. | Balances CPU and I/O usage. |
| IL cache | Reuses IL text based on MD5 + tool label (command + version). | In-memory cache (LRU up to 2000 items, TTL 12h) with optional disk persistence. |
| MD5 pre-warming | Precomputes MD5 for all targets in parallel before diffing. | Evens out cache key generation time. |
| IL cache prefetch | Promotes disk cache entries into memory before diffing. | Further reduces disassembler launches. |
| Parallel text diff | Files ≥512 KiB are split into 64 KiB chunks compared in parallel. | Only determines equality (no diff output). |
| Tool failure blacklist | Skips IL tools that failed repeatedly (default: 3 times) for 10 minutes. | Reduces repeated launch overhead. |

### IL cache notes

- File names in the disk cache are sanitized (invalid chars/colon replaced with `_`, overly long names shortened) to avoid NTFS alternate data stream quirks.
- Disk cache trimming obeys both LRU and the `ILCacheMaxDiskFileCount` / `ILCacheMaxDiskMegabytes` settings.

## Versioning (Nerdbank.GitVersioning)

The project uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) to produce SemVer versions.

- `version.json` declares release channels such as `main` or tags like `v1.2.3`.
- The generated `AssemblyInformationalVersion` is recorded in `Reports/<label>/diff_report.md`.
- You can override the version manually via `dotnet build /p:Version=1.2.3` when needed.

Tagging example:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## License

This project is distributed under the [MIT License](LICENSE).
