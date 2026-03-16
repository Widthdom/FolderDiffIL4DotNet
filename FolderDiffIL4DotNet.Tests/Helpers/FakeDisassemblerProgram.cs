using System;
using System.IO;
using System.Linq;

// Determine tool name from argv[0]
var toolName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0])
    .ToUpperInvariant()
    .Replace("-", "_");

var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

// Special: "dotnet" muxer - route to DOTNET_ILDASM vars when first arg is "ildasm"
if (toolName == "DOTNET" && cmdArgs.Length > 0 && cmdArgs[0].Equals("ildasm", StringComparison.OrdinalIgnoreCase))
{
    toolName = "DOTNET_ILDASM";
    cmdArgs = cmdArgs.Skip(1).ToArray(); // remove "ildasm" from cmdArgs
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

// Handle version flags
if (cmdArgs.Any(a => a == "--version" || a == "-v" || a == "-h"))
{
    if (!string.IsNullOrEmpty(versionOutput))
        Console.WriteLine(versionOutput);
    return versionExit;
}

// Write counter if requested
if (!string.IsNullOrEmpty(counterPath))
    File.AppendAllText(counterPath, "x\n");

// Check fail pattern
if (!string.IsNullOrEmpty(failPattern) && cmdArgs.Any(a => a.EndsWith(failPattern, StringComparison.Ordinal)))
    return failExit;

Console.WriteLine(output);
return exit;
