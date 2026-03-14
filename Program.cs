using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FolderDiffIL4DotNet
{
    /// <summary>
    /// アプリケーションのエントリーポイント。
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
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
