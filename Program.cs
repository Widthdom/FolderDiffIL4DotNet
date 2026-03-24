using System.Text;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Text;
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

            // Register legacy code pages (e.g. Shift_JIS / cp932) for encoding auto-detection
            // when reading non-UTF-8 text files for inline diff generation.
            // インライン差分生成時の非 UTF-8 テキストファイル読み込みに備え、
            // レガシーコードページ（Shift_JIS / cp932 等）を登録します。
            EncodingDetector.RegisterCodePages();
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
