# nildiff

**.NET Global Tool** for folder diff and release validation. Compares two folders and generates structured diff reports.

## Install

```bash
dotnet tool install -g nildiff
```

## Usage

```bash
# Compare two folders
nildiff "/path/to/old-folder" "/path/to/new-folder" "my-comparison" --no-pause

# Interactive wizard mode
nildiff --wizard

# Show help
nildiff --help
```

## Output

| File | Description |
|---|---|
| `diff_report.md` | Markdown report for archiving and text-based review |
| `diff_report.html` | Interactive single-file HTML report with sign-off workflow |
| `audit_log.json` | Structured audit log with SHA256 hashes |

## Key Feature: IL-Level Comparison

For .NET assemblies (`.dll`, `.exe`), nildiff compares at the **IL level** rather than binary level, filtering out build-specific noise (MVID, timestamps). Functionally identical assemblies are reported as "unchanged" even when binary hashes differ due to non-deterministic builds.

## Optional: IL Disassembler

For IL-level comparison, install an IL disassembler:

```bash
dotnet tool install -g dotnet-ildasm
```

Without an IL disassembler, .NET assemblies are compared by SHA256 hash only.

## Requirements

- [.NET SDK 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or later

## License

[MIT](https://github.com/Widthdom/FolderDiffIL4DotNet/blob/main/LICENSE)
