# FolderDiffIL4DotNet.Plugin.Abstractions

Plugin contract interfaces for [FolderDiffIL4DotNet](https://github.com/Widthdom/FolderDiffIL4DotNet).

Reference this package to build plugins that extend FolderDiffIL4DotNet without modifying its source code.

## Available Extension Points

| Interface | Purpose |
|-----------|---------|
| `IPlugin` | Plugin entry point — registers services into the host DI container |
| `IReportSectionWriter` | Add custom sections to the Markdown diff report |
| `IReportFormatter` | Add custom output formats (PDF, JUnit XML, etc.) |
| `IFileComparisonHook` | Run logic before/after each file comparison |
| `IPostProcessAction` | Execute actions after all reports are generated |
| `IDisassemblerProvider` | Provide custom disassemblers for non-.NET file types |

## Quick Start

```csharp
using FolderDiffIL4DotNet.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;

public class MyPlugin : IPlugin
{
    public PluginMetadata Metadata => new()
    {
        Id = "com.example.my-plugin",
        DisplayName = "My Custom Plugin",
        Version = new Version(1, 0, 0),
        MinHostVersion = new Version(1, 14, 0)
    };

    public void ConfigureServices(
        IServiceCollection services,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> pluginConfig)
    {
        services.AddSingleton<IPostProcessAction, MyNotifier>();
    }
}
```

## License

MIT
