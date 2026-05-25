using System;
using System.IO;
using System.Threading;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Tests.Helpers
{
    /// <summary>
    /// Temporarily redirects the application's LocalApplicationData root to a temp directory for tests.
    /// テスト中だけアプリケーションの LocalApplicationData ルートを一時ディレクトリへ切り替えます。
    /// </summary>
    internal sealed class AppDataOverrideScope : IDisposable
    {
        private static readonly SemaphoreSlim s_gate = new(1, 1);

        private readonly object? _originalOverride;
        private bool _disposed;

        internal AppDataOverrideScope(string rootAbsolutePath)
        {
            s_gate.Wait();
            try
            {
                RootAbsolutePath = Path.GetFullPath(rootAbsolutePath);
                Directory.CreateDirectory(RootAbsolutePath);
                _originalOverride = AppContext.GetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY);
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, RootAbsolutePath);
            }
            catch
            {
                s_gate.Release();
                throw;
            }
        }

        internal string RootAbsolutePath { get; }

        internal string ApplicationDataRootAbsolutePath
            => Path.Combine(RootAbsolutePath, Constants.APP_DATA_DIR_NAME);

        internal string ReportsRootAbsolutePath
            => Path.Combine(ApplicationDataRootAbsolutePath, "Reports");

        internal string LogsRootAbsolutePath
            => Path.Combine(ApplicationDataRootAbsolutePath, "Logs");

        internal string UserConfigFileAbsolutePath
            => Path.Combine(ApplicationDataRootAbsolutePath, "config.json");

        internal string ReviewChecklistFileAbsolutePath
            => Path.Combine(ApplicationDataRootAbsolutePath, "checklist.json");

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                AppContext.SetData(AppDataPaths.LOCAL_APP_DATA_OVERRIDE_KEY, _originalOverride);
                _disposed = true;
            }
            finally
            {
                s_gate.Release();
            }
        }
    }
}
