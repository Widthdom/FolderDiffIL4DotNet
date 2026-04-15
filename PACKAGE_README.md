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

# Or omit the label to auto-generate a high-resolution timestamp
nildiff "/path/to/old-folder" "/path/to/new-folder" --no-pause

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

## Configuration

The tool works out of the box with default settings. To customize behavior, either create a user-local `config.json` in the default app-data location below or pass it via `--config`:

```bash
nildiff "/old" "/new" "label" --config /path/to/config.json
```

When the report label is omitted, nildiff auto-generates a high-resolution timestamp label. Tokens beginning with `--` are treated as options, not report labels.

Individual settings can also be overridden via `FOLDERDIFF_*` environment variables (e.g. `FOLDERDIFF_MAXPARALLELISM=8`). For maintainer-only IL noise suppression, `--creator` applies the predefined `buildserver-winforms` `ILIgnoreLineContainingStrings` profile. See the [annotated sample config](https://github.com/Widthdom/FolderDiffIL4DotNet/blob/main/doc/config.sample.jsonc) for all available settings.

The default user-local `config.json` location varies by OS:

| OS | Path |
|---|---|
| Windows | `%LOCALAPPDATA%\FolderDiffIL4DotNet\config.json` |
| macOS | `~/Library/Application Support/FolderDiffIL4DotNet/config.json` |
| Linux | `~/.local/share/FolderDiffIL4DotNet/config.json` |

> **Note:** This user-local config survives global-tool updates. If it does not exist, the tool falls back to the bundled `config.json` next to the executable.

By default, reports and logs are also written under the same user-local app-data root resolved from `Environment.SpecialFolder.LocalApplicationData` (`Reports/` and `Logs/`).

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
