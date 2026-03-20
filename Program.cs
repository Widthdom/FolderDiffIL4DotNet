using System.Text;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // On Windows the console defaults to the OEM code page, causing Unicode
            // characters (box-drawing, block elements, etc.) to appear as '?'.
            // Switch to UTF-8 before any output.
            // Windows のコンソールは既定で OEM コードページを使用するため、
            // バナーや差分出力に含まれる Unicode 文字（罫線・ブロック文字等）が
            // 文字化けします。すべての出力に先立ち UTF-8 へ切り替えます。
            System.Console.OutputEncoding = Encoding.UTF8;
            using var serviceProvider = BuildServiceProvider();
            return await serviceProvider.GetRequiredService<ProgramRunner>().RunAsync(args);
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerService, LoggerService>();
            services.AddTransient<ConfigService>();
            services.AddTransient<ProgramRunner>();
            return services.BuildServiceProvider();
        }
    }
}
