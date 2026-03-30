# FolderDiffIL4DotNet.Core

Core utility library extracted from [FolderDiffIL4DotNet](https://github.com/Widthdom/FolderDiffIL4DotNet) — a folder-diff tool for .NET release validation.

## Features

| Namespace | Key Types | Description |
|---|---|---|
| `Core.IO` | `FileComparer` | SHA256 hash comparison and line-by-line text diff |
| | `PathValidator` | Cross-platform path and folder name validation |
| | `FileSystemUtility` | Timestamp, read-only flag, and deletion helpers |
| | `NetworkPathDetector` | UNC/NFS/CIFS/SSHFS network path detection |
| `Core.Text` | `TextDiffer` | Myers diff algorithm for line-level text comparison |
| | `EncodingDetector` | BOM detection and UTF-8 validation with ANSI fallback |
| | `TextSanitizer` | String sanitization and safe filename conversion |
| `Core.Diagnostics` | `DotNetDetector` | PE/CLR header parser for .NET executable detection |
| | `ProcessHelper` | Process execution and command tokenization |
| | `SystemInfo` | System information and app metadata retrieval |
| `Core.Console` | `ConsoleSpinner` | Animated console spinner with dispose support |
| `Core.Common` | `CoreConstants` | Shared constants |

## Quick Start

```csharp
using FolderDiffIL4DotNet.Core.IO;
using FolderDiffIL4DotNet.Core.Text;
using FolderDiffIL4DotNet.Core.Diagnostics;

// Compare two files by SHA256 hash
bool areEqual = FileComparer.CompareByHash("file1.dll", "file2.dll");

// Line-by-line diff using Myers algorithm
var diffs = TextDiffer.ComputeDiff(oldLines, newLines);

// Detect if a file is a .NET assembly
var result = DotNetDetector.Detect("MyApp.dll");
if (result.Status == DotNetExecutableDetectionStatus.DotNetExecutable)
    Console.WriteLine(".NET assembly detected");

// Validate a file path
bool isValid = PathValidator.IsValidPath(@"C:\MyProject\bin\Release");

// Detect file encoding
var encoding = EncodingDetector.Detect("data.csv");
```

## Requirements

- .NET 8.0 or later

## License

[MIT](https://github.com/Widthdom/FolderDiffIL4DotNet/blob/main/LICENSE)
