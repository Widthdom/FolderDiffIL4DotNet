using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Common;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Checks nuget.org for a newer stable nildiff release and prints a best-effort startup notice.
    /// nuget.org で nildiff の新しい安定版を確認し、ベストエフォートで起動通知を表示します。
    /// </summary>
    internal sealed class UpdateNotificationService
    {
        internal const string PACKAGE_REGISTRATION_URL =
            "https://api.nuget.org/v3/registration5-gz-semver2/nildiff/index.json";
        internal const string RELEASE_NOTES_URL =
            "https://github.com/Widthdom/FolderDiffIL4DotNet/releases/latest";
        internal const string UPDATE_COMMAND = "dotnet tool update --global nildiff";
        internal const int CACHE_VALID_HOURS = 20;
        internal const int FAILURE_RETRY_HOURS = 1;

        private const int HTTP_TIMEOUT_SECONDS = 2;
        private const string NUGET_API_HOST = "api.nuget.org";
        private static readonly HttpClient s_httpClient = CreateDefaultHttpClient();

        private readonly HttpClient _httpClient;
        private readonly Func<string> _cachePathResolver;
        private readonly Func<DateTimeOffset> _utcNowProvider;
        private readonly Func<bool> _shouldCheck;
        private readonly Func<CancellationToken, Task<int>> _runUpdateCommand;

        internal UpdateNotificationService()
            : this(
                s_httpClient,
                AppDataPaths.GetUpdateCheckCacheFileAbsolutePath,
                static () => DateTimeOffset.UtcNow,
                ShouldCheckInCurrentEnvironment,
                RunUpdateCommandAsync)
        {
        }

        internal UpdateNotificationService(
            HttpClient httpClient,
            Func<string> cachePathResolver,
            Func<DateTimeOffset> utcNowProvider,
            Func<bool> shouldCheck,
            Func<CancellationToken, Task<int>>? runUpdateCommand = null)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(cachePathResolver);
            ArgumentNullException.ThrowIfNull(utcNowProvider);
            ArgumentNullException.ThrowIfNull(shouldCheck);

            _httpClient = httpClient;
            _cachePathResolver = cachePathResolver;
            _utcNowProvider = utcNowProvider;
            _shouldCheck = shouldCheck;
            _runUpdateCommand = runUpdateCommand ?? RunUpdateCommandAsync;
        }

        /// <summary>
        /// Prompts to install a newer stable release when one is known.
        /// Returns <see langword="true"/> only when the update command succeeds and the caller should exit.
        /// 新しい安定版が確認できた場合に更新選択を表示します。
        /// 更新コマンドが成功し、呼び出し元が終了すべき場合だけ <see langword="true"/> を返します。
        /// </summary>
        internal async Task<bool> TryNotifyAsync(
            string currentVersion,
            TextWriter output,
            TextReader? input = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
            ArgumentNullException.ThrowIfNull(output);

#pragma warning disable CA1031 // Update checks must never prevent the CLI from starting.
            try
            {
                if (!_shouldCheck())
                {
                    return false;
                }

                UpdateCheckCache? cache;
                using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutSource.CancelAfter(TimeSpan.FromSeconds(HTTP_TIMEOUT_SECONDS));
                    cache = await GetUpdateCheckCacheAsync(timeoutSource.Token);
                }

                string? latestVersion = cache?.LatestVersion;
                if (!IsNewerStableVersion(latestVersion, currentVersion)
                    || string.Equals(
                        latestVersion,
                        cache?.DismissedVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                await output.WriteLineAsync();
                await output.WriteLineAsync($"✨ Update available! {currentVersion} -> {latestVersion}");
                await output.WriteLineAsync();
                await output.WriteLineAsync($"Run: {UPDATE_COMMAND}");
                await output.WriteLineAsync($"Release notes: {RELEASE_NOTES_URL}");
                await output.WriteLineAsync();
                await output.WriteLineAsync("  1. Update now");
                await output.WriteLineAsync("  2. Skip");
                await output.WriteLineAsync("  3. Skip until next version");
                await output.WriteLineAsync();
                await output.WriteAsync(
                    "Select [1-3] (default: 2; unrecognized input also skips): ");
                await output.FlushAsync(cancellationToken);

                string? selection = await (input ?? TextReader.Null).ReadLineAsync(cancellationToken);
                string trimmedSelection = selection?.Trim() ?? string.Empty;
                if (string.Equals(trimmedSelection, "3", StringComparison.Ordinal))
                {
                    await TryWriteCacheAsync(
                        cache! with { DismissedVersion = latestVersion },
                        CancellationToken.None);
                    await output.WriteLineAsync();
                    await output.WriteLineAsync(
                        $"Skipped {latestVersion}. You will be notified when a newer version is available.");
                    await output.WriteLineAsync();
                    await output.FlushAsync(cancellationToken);
                    return false;
                }

                if (!string.Equals(trimmedSelection, "1", StringComparison.Ordinal))
                {
                    await output.WriteLineAsync();
                    return false;
                }

                await output.WriteLineAsync();
                await output.WriteLineAsync($"Updating nildiff via `{UPDATE_COMMAND}`...");
                await output.FlushAsync(cancellationToken);

                try
                {
                    int exitCode = await _runUpdateCommand(cancellationToken);
                    if (exitCode == 0)
                    {
                        await output.WriteLineAsync();
                        await output.WriteLineAsync("Update command completed successfully. Please restart nildiff.");
                        await output.WriteLineAsync();
                        await output.FlushAsync(cancellationToken);
                        return true;
                    }

                    await WriteUpdateFailureAsync(output, $"The update command exited with code {exitCode}.");
                    return false;
                }
                catch (Exception)
                {
                    await WriteUpdateFailureAsync(output, "The update command could not be completed.");
                    return false;
                }
            }
            catch (Exception)
            {
                // Update notification is informational and must never alter CLI behavior.
                // 更新通知は情報提供のみであり、CLI 本体の挙動を変えてはいけません。
                return false;
            }
#pragma warning restore CA1031
        }

        internal static bool IsNewerStableVersion(string? candidateVersion, string currentVersion)
        {
            if (!TryParseStableVersion(candidateVersion, out var candidate)
                || !TryParseStableVersion(currentVersion, out var current))
            {
                return false;
            }

            return candidate > current;
        }

        private async Task<UpdateCheckCache?> GetUpdateCheckCacheAsync(
            CancellationToken cancellationToken)
        {
            DateTimeOffset now = _utcNowProvider();
            UpdateCheckCache? cached = await TryReadCacheAsync(cancellationToken);
            if (cached != null)
            {
                DateTimeOffset nextCheckAtUtc = cached.NextCheckAtUtc
                    ?? cached.CheckedAtUtc.AddHours(CACHE_VALID_HOURS);
                TimeSpan untilNextCheck = nextCheckAtUtc - now;
                if (untilNextCheck > TimeSpan.Zero
                    && untilNextCheck <= TimeSpan.FromHours(CACHE_VALID_HOURS))
                {
                    return cached;
                }
            }

            try
            {
                string? latestVersion = await FetchLatestStableVersionAsync(cancellationToken);
                if (latestVersion == null)
                {
                    return await WriteFailureBackoffCacheAsync(now, cached);
                }

                var refreshedCache = new UpdateCheckCache(
                    now,
                    latestVersion,
                    now.AddHours(CACHE_VALID_HOURS),
                    cached?.DismissedVersion);
                await TryWriteCacheAsync(refreshedCache, CancellationToken.None);
                return refreshedCache;
            }
            catch (HttpRequestException)
            {
                return await WriteFailureBackoffCacheAsync(now, cached);
            }
            catch (OperationCanceledException)
            {
                return await WriteFailureBackoffCacheAsync(now, cached);
            }
            catch (JsonException)
            {
                return await WriteFailureBackoffCacheAsync(now, cached);
            }
        }

        private async Task<UpdateCheckCache> WriteFailureBackoffCacheAsync(
            DateTimeOffset now,
            UpdateCheckCache? cached)
        {
            var failureBackoffCache = new UpdateCheckCache(
                now,
                cached?.LatestVersion,
                now.AddHours(FAILURE_RETRY_HOURS),
                cached?.DismissedVersion);
            await TryWriteCacheAsync(failureBackoffCache, CancellationToken.None);
            return failureBackoffCache;
        }

        private async Task<string?> FetchLatestStableVersionAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, PACKAGE_REGISTRATION_URL);
            request.Headers.TryAddWithoutValidation("User-Agent", "nildiff-update-check");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument registrationIndex = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: cancellationToken);

            if (!registrationIndex.RootElement.TryGetProperty("items", out var pages)
                || pages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            Version? latest = null;
            string? latestText = null;

            foreach (JsonElement page in pages.EnumerateArray())
            {
                if (page.TryGetProperty("items", out var inlineItems)
                    && inlineItems.ValueKind == JsonValueKind.Array)
                {
                    FindLatestStableVersion(inlineItems, ref latest, ref latestText);
                    continue;
                }

                if (!TryGetTrustedPageUri(page, out var pageUri))
                {
                    continue;
                }

                using var pageResponse = await _httpClient.GetAsync(
                    pageUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                pageResponse.EnsureSuccessStatusCode();

                await using var pageStream = await pageResponse.Content.ReadAsStreamAsync(cancellationToken);
                using JsonDocument pageDocument = await JsonDocument.ParseAsync(
                    pageStream,
                    cancellationToken: cancellationToken);
                if (pageDocument.RootElement.TryGetProperty("items", out var pageItems)
                    && pageItems.ValueKind == JsonValueKind.Array)
                {
                    FindLatestStableVersion(pageItems, ref latest, ref latestText);
                }
            }

            return latestText;
        }

        private static void FindLatestStableVersion(
            JsonElement leaves,
            ref Version? latest,
            ref string? latestText)
        {
            foreach (JsonElement leaf in leaves.EnumerateArray())
            {
                if (!leaf.TryGetProperty("catalogEntry", out var catalogEntry)
                    || catalogEntry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (catalogEntry.TryGetProperty("listed", out var listed)
                    && listed.ValueKind == JsonValueKind.False)
                {
                    continue;
                }

                if (!catalogEntry.TryGetProperty("version", out var versionElement)
                    || versionElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? versionText = versionElement.GetString();
                if (!TryParseStableVersion(versionText, out var parsedVersion)
                    || (latest != null && parsedVersion <= latest))
                {
                    continue;
                }

                latest = parsedVersion;
                latestText = versionText;
            }
        }

        private static bool TryParseStableVersion(string? versionText, out Version version)
        {
            version = new Version();
            if (string.IsNullOrWhiteSpace(versionText)
                || versionText.Contains('-', StringComparison.Ordinal)
                || !Version.TryParse(versionText, out var parsedVersion)
                || parsedVersion == null)
            {
                return false;
            }

            version = parsedVersion;
            return true;
        }

        private static bool TryGetTrustedPageUri(JsonElement page, out Uri? pageUri)
        {
            pageUri = null;
            if (!page.TryGetProperty("@id", out var id)
                || id.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(id.GetString(), UriKind.Absolute, out var parsedUri)
                || !string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parsedUri.Host, NUGET_API_HOST, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            pageUri = parsedUri;
            return true;
        }

        private async Task<UpdateCheckCache?> TryReadCacheAsync(CancellationToken cancellationToken)
        {
#pragma warning disable CA1031 // Cache failures must not prevent the network check or CLI startup.
            try
            {
                string cachePath = _cachePathResolver();
                if (!File.Exists(cachePath))
                {
                    return null;
                }

                await using var stream = File.OpenRead(cachePath);
                return await JsonSerializer.DeserializeAsync<UpdateCheckCache>(
                    stream,
                    cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                return null;
            }
#pragma warning restore CA1031
        }

        private async Task TryWriteCacheAsync(
            UpdateCheckCache cache,
            CancellationToken cancellationToken)
        {
            string? temporaryPath = null;
#pragma warning disable CA1031 // Cache writes are best effort and must not affect CLI startup.
            try
            {
                string cachePath = _cachePathResolver();
                string? cacheDirectory = Path.GetDirectoryName(cachePath);
                if (string.IsNullOrWhiteSpace(cacheDirectory))
                {
                    return;
                }

                Directory.CreateDirectory(cacheDirectory);
                temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        cache,
                        cancellationToken: cancellationToken);
                }

                File.Move(temporaryPath, cachePath, overwrite: true);
                temporaryPath = null;
            }
            catch (Exception)
            {
                // Ignore cache persistence failures.
                // キャッシュ永続化の失敗は無視します。
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception)
                    {
                        // Ignore temporary-file cleanup failures.
                        // 一時ファイルの削除失敗は無視します。
                    }
                }
            }
#pragma warning restore CA1031
        }

        private static bool ShouldCheckInCurrentEnvironment()
            => !Console.IsInputRedirected
                && !Console.IsOutputRedirected
                && !Console.IsErrorRedirected;

        private static async Task<int> RunUpdateCommandAsync(CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Constants.DOTNET_MUXER,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("tool");
            startInfo.ArgumentList.Add("update");
            startInfo.ArgumentList.Add("--global");
            startInfo.ArgumentList.Add("nildiff");

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return -1;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }

        private static async Task WriteUpdateFailureAsync(TextWriter output, string detail)
        {
            await output.WriteLineAsync();
            await output.WriteLineAsync($"Update failed. {detail}");
            await output.WriteLineAsync($"Run `{UPDATE_COMMAND}` manually to retry.");
            await output.FlushAsync();
        }

        private static HttpClient CreateDefaultHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression =
                    DecompressionMethods.GZip
                    | DecompressionMethods.Deflate
                    | DecompressionMethods.Brotli
            };
            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SECONDS)
            };
        }

        private sealed record UpdateCheckCache(
            [property: JsonPropertyName("checkedAtUtc")] DateTimeOffset CheckedAtUtc,
            [property: JsonPropertyName("latestVersion")] string? LatestVersion,
            [property: JsonPropertyName("nextCheckAtUtc")] DateTimeOffset? NextCheckAtUtc,
            [property: JsonPropertyName("dismissedVersion")] string? DismissedVersion);
    }
}
