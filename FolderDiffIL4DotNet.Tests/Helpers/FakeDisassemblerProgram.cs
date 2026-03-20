using System;
using System.IO;
using System.Linq;

// Determine tool name from the process executable path (AppHost name),
// falling back to argv[0] (managed DLL path) if ProcessPath is unavailable.
// プロセス実行パス（AppHost 名）からツール名を決定。ProcessPath が取得できない場合は argv[0] にフォールバック。
var execPath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
var toolName = Path.GetFileNameWithoutExtension(execPath)
    .ToUpperInvariant()
    .Replace("-", "_");

var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

// Special: "dotnet" muxer - route to DOTNET_ILDASM vars when first arg is "ildasm"
// 特殊: "dotnet" マルチプレクサ — 最初の引数が "ildasm" なら DOTNET_ILDASM 変数にルーティング
if (toolName == "DOTNET" && cmdArgs.Length > 0 && cmdArgs[0].Equals("ildasm", StringComparison.OrdinalIgnoreCase))
{
    toolName = "DOTNET_ILDASM";
    cmdArgs = cmdArgs.Skip(1).ToArray(); // remove "ildasm" from cmdArgs / cmdArgs から "ildasm" を除去
}

var prefix = $"FD_FAKE_{toolName}_";
string? GetEnv(string key) => Environment.GetEnvironmentVariable(prefix + key);

var versionExit = int.TryParse(GetEnv("VERSION_EXIT"), out var ve) ? ve : 0;
var versionOutput = GetEnv("VERSION_OUTPUT") ?? $"fake {toolName.ToLowerInvariant().Replace("_", "-")} 0.0.1";
var output = GetEnv("OUTPUT") ?? "FAKE_IL";
var exit = int.TryParse(GetEnv("EXIT"), out var e) ? e : 0;
var failPattern = GetEnv("FAIL_PATTERN");
var failExit = int.TryParse(GetEnv("FAIL_EXIT"), out var fe) ? fe : 90;
var counterPath = GetEnv("COUNTER_PATH");

// Handle version flags / バージョンフラグの処理
if (cmdArgs.Any(a => a == "--version" || a == "-v" || a == "-h"))
{
    if (!string.IsNullOrEmpty(versionOutput))
        Console.WriteLine(versionOutput);
    return versionExit;
}

// Write counter if requested / 要求があればカウンターを書き込む
if (!string.IsNullOrEmpty(counterPath))
    File.AppendAllText(counterPath, "x\n");

// Check fail pattern / 失敗パターンをチェック
if (!string.IsNullOrEmpty(failPattern) && cmdArgs.Any(a => a.EndsWith(failPattern, StringComparison.Ordinal)))
    return failExit;

Console.WriteLine(output);
return exit;
