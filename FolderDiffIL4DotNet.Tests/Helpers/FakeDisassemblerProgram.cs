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
// Exit code used only when no "-o" is passed (simulates a stdout-mode failure).
// "-o" が無い場合にだけ使う終了コード（stdout モードの失敗を再現）。
var stdoutModeExit = int.TryParse(GetEnv("STDOUT_EXIT"), out var se) ? se : (int?)null;
// "1" writes no IL at all (an empty environment value cannot be set portably).
// "1" で IL を一切書かない（空文字の環境変数は移植性のある設定ができないため）。
var emptyOutput = GetEnv("EMPTY_OUTPUT") == "1";
var stderrOutput = GetEnv("STDERR");
var sleepMilliseconds = int.TryParse(GetEnv("SLEEP_MS"), out var sm) ? sm : 0;

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

if (sleepMilliseconds > 0)
    Thread.Sleep(sleepMilliseconds);

if (!string.IsNullOrEmpty(stderrOutput))
    Console.Error.WriteLine(stderrOutput);

// Emulate ilspycmd: "-o" names an output DIRECTORY and "<input name>.il" is written inside it; stdout stays empty.
// ilspycmd を模倣: "-o" は出力ディレクトリを指し、その中に "<入力名>.il" を書き出す。stdout には何も出さない。
var outputOptionIndex = Array.IndexOf(cmdArgs, "-o");
if (outputOptionIndex >= 0 && outputOptionIndex + 1 < cmdArgs.Length)
{
    var outputDirectory = cmdArgs[outputOptionIndex + 1];
    Directory.CreateDirectory(outputDirectory);
    if (!emptyOutput)
        File.WriteAllText(Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(cmdArgs[^1]) + ".il"), output + Environment.NewLine);
    return exit;
}

if (stdoutModeExit.HasValue)
    return stdoutModeExit.Value;

if (!emptyOutput)
    Console.WriteLine(output);
return exit;
