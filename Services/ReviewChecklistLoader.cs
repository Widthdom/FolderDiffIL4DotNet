using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Loads the optional user-local review checklist snapshot for the current run.
    /// 現在の実行で使用する任意のユーザーローカルレビューチェックリストのスナップショットを読み込みます。
    /// </summary>
    internal static class ReviewChecklistLoader
    {
        /// <summary>
        /// Resolves and loads checklist items once for the current run.
        /// 現在の実行で使用するチェックリスト項目を 1 回だけ解決して読み込みます。
        /// </summary>
        internal static IReadOnlyList<string> Load(ILoggerService logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            string checklistFilePath;
            try
            {
                checklistFilePath = AppDataPaths.GetDefaultReviewChecklistFileAbsolutePath();
            }
            catch (InvalidOperationException ex)
            {
                logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Review checklist path could not be resolved and will be skipped ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                return Array.Empty<string>();
            }

            if (!File.Exists(checklistFilePath))
            {
                return Array.Empty<string>();
            }

            try
            {
                string json = File.ReadAllText(checklistFilePath);
                var items = JsonSerializer.Deserialize<List<string>>(json);
                if (items == null)
                {
                    return Array.Empty<string>();
                }

                return items
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(NormalizeItem)
                    .Where(item => item.Length > 0)
                    .ToList();
            }
            catch (JsonException ex)
            {
                logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Review checklist file '{checklistFilePath}' is invalid JSON and will be skipped ({ex.GetType().Name}): {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                return Array.Empty<string>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogMessage(
                    AppLogLevel.Warning,
                    $"Review checklist file '{checklistFilePath}' could not be read and will be skipped: {ex.GetType().Name}: {ex.Message}",
                    shouldOutputMessageToConsole: true,
                    ex);
                return Array.Empty<string>();
            }
        }

        private static string NormalizeItem(string item)
            => item.Replace("\r\n", "\n", StringComparison.Ordinal)
                   .Replace('\r', '\n')
                   .Trim();
    }
}
