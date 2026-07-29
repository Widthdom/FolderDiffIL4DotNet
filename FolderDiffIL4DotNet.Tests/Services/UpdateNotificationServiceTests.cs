using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Core.Diagnostics;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class UpdateNotificationServiceTests : IDisposable
    {
        private readonly string _tempDirectory =
            Path.Combine(Path.GetTempPath(), "nildiff-update-tests-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public async Task TryNotifyAsync_NewerStableVersion_PrintsUpdateCommandAndReleaseNotes()
        {
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.22.0", true), ("1.23.0", true))));
            var service = CreateService(handler);
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.22.0", output);

            string notice = output.ToString();
            string expectedLayout = string.Join(
                Environment.NewLine,
                "✨ Update available! 1.22.0 -> 1.23.0",
                string.Empty,
                "Run: dotnet tool update --global nildiff",
                $"Release notes: {UpdateNotificationService.RELEASE_NOTES_URL}",
                string.Empty,
                "  1. Update now",
                "  2. Skip",
                "  3. Skip until next version",
                string.Empty,
                "Select [1-3] (default: 2; unrecognized input also skips):");
            Assert.Contains(expectedLayout, notice, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_PrereleaseAndUnlistedVersions_UsesLatestListedStableVersion()
        {
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(
                    ("1.22.0", true),
                    ("1.23.0-beta.1", true),
                    ("2.0.0", false),
                    ("1.24.0", true))));
            var service = CreateService(handler);
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.22.0", output);

            Assert.Contains("1.22.0 -> 1.24.0", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("2.0.0", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task TryNotifyAsync_CurrentVersionIsLatest_DoesNotPrintNotice()
        {
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.22.0", true), ("1.23.0", true))));
            var service = CreateService(handler);
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.23.0", output);

            Assert.Equal(string.Empty, output.ToString());
        }

        [Fact]
        public async Task TryNotifyAsync_FreshCache_DoesNotIssueAnotherHttpRequest()
        {
            var now = new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);
            string cachePath = Path.Combine(_tempDirectory, "update-check.json");
            var initialHandler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.23.0", true))));
            var initialService = CreateService(initialHandler, cachePath, () => now);

            await initialService.TryNotifyAsync("1.22.0", TextWriter.Null);

            var cachedHandler = new RecordingHttpHandler(
                _ => throw new InvalidOperationException("HTTP must not be used for a fresh cache."));
            var cachedService = CreateService(
                cachedHandler,
                cachePath,
                () => now.AddHours(UpdateNotificationService.CACHE_VALID_HOURS - 1));
            using var output = new StringWriter();

            await cachedService.TryNotifyAsync("1.22.0", output);

            Assert.Contains("1.22.0 -> 1.23.0", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(0, cachedHandler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_StaleCache_RefreshesFromNuGet()
        {
            var now = new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);
            string cachePath = Path.Combine(_tempDirectory, "update-check.json");
            var initialService = CreateService(
                new RecordingHttpHandler(_ => JsonResponse(RegistrationIndex(("1.23.0", true)))),
                cachePath,
                () => now);
            await initialService.TryNotifyAsync("1.22.0", TextWriter.Null);

            var refreshHandler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.24.0", true))));
            var refreshService = CreateService(
                refreshHandler,
                cachePath,
                () => now.AddHours(UpdateNotificationService.CACHE_VALID_HOURS));
            using var output = new StringWriter();

            await refreshService.TryNotifyAsync("1.22.0", output);

            Assert.Contains("1.22.0 -> 1.24.0", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(1, refreshHandler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_NuGetFailure_IsSilent()
        {
            var now = new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);
            string cachePath = Path.Combine(_tempDirectory, "update-check.json");
            var handler = new RecordingHttpHandler(
                _ => throw new HttpRequestException("nuget unavailable"));
            var service = CreateService(handler, cachePath, () => now);
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.22.0", output);

            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(1, handler.RequestCount);

            var backoffHandler = new RecordingHttpHandler(
                _ => throw new InvalidOperationException("HTTP must not be retried during backoff."));
            var backoffService = CreateService(
                backoffHandler,
                cachePath,
                () => now.AddMinutes(30));

            await backoffService.TryNotifyAsync("1.22.0", output);

            Assert.Equal(0, backoffHandler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_CheckDisabled_DoesNotReadCacheOrUseNetwork()
        {
            var handler = new RecordingHttpHandler(
                _ => throw new InvalidOperationException("HTTP must not be used when disabled."));
            var service = new UpdateNotificationService(
                new HttpClient(handler),
                () => throw new InvalidOperationException("Cache must not be read when disabled."),
                static () => DateTimeOffset.UtcNow,
                static () => false);
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.22.0", output);

            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_EnvironmentDetectionFailure_IsSilent()
        {
            var handler = new RecordingHttpHandler(
                _ => throw new InvalidOperationException("HTTP must not be used."));
            var service = new UpdateNotificationService(
                new HttpClient(handler),
                () => throw new InvalidOperationException("Cache must not be read."),
                static () => DateTimeOffset.UtcNow,
                static () => throw new IOException("console state unavailable"));
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.22.0", output);

            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(0, handler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_ExternalRegistrationPage_FollowsTrustedNuGetUrl()
        {
            const string pageUrl =
                "https://api.nuget.org/v3/registration5-gz-semver2/nildiff/page/1.0.0/1.23.0.json";
            var handler = new RecordingHttpHandler(request =>
            {
                string json = string.Equals(request.RequestUri?.AbsoluteUri, pageUrl, StringComparison.Ordinal)
                    ? "{\"items\":[{\"catalogEntry\":{\"version\":\"1.23.0\",\"listed\":true}}]}"
                    : "{\"items\":[{\"@id\":\"" + pageUrl + "\"}]}";
                return JsonResponse(json);
            });
            var service = CreateService(handler);
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.22.0", output);

            Assert.Contains("1.22.0 -> 1.23.0", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(2, handler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_ExternalRegistrationPage_DoesNotFollowUntrustedHost()
        {
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                "{\"items\":[{\"@id\":\"https://example.test/forged-page.json\"}]}"));
            var service = CreateService(handler);
            using var output = new StringWriter();

            await service.TryNotifyAsync("1.22.0", output);

            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_UpdateSelected_RunsCommandAndRequestsCallerExit()
        {
            int updateCommandCalls = 0;
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.23.0", true))));
            var service = CreateService(
                handler,
                runUpdateCommand: _ =>
                {
                    updateCommandCalls++;
                    return Task.FromResult(0);
                });
            using var output = new StringWriter();

            bool shouldExit = await service.TryNotifyAsync(
                "1.22.0",
                output,
                new StringReader("1\n"));

            Assert.True(shouldExit);
            Assert.Equal(1, updateCommandCalls);
            Assert.Contains("Please restart nildiff", output.ToString(), StringComparison.Ordinal);
            Assert.EndsWith(
                "Update command completed successfully. Please restart nildiff."
                + Environment.NewLine
                + Environment.NewLine,
                output.ToString(),
                StringComparison.Ordinal);
        }

        [Fact]
        public async Task TryNotifyAsync_SkipSelected_DoesNotRunUpdateCommand()
        {
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.23.0", true))));
            var service = CreateService(
                handler,
                runUpdateCommand: _ => throw new InvalidOperationException("Update must not run."));
            using var output = new StringWriter();

            bool shouldExit = await service.TryNotifyAsync(
                "1.22.0",
                output,
                new StringReader("2\n"));

            Assert.False(shouldExit);
            Assert.DoesNotContain("Updating nildiff via", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task TryNotifyAsync_SkipUntilNextVersionSelected_DismissesCurrentLatest()
        {
            var now = new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);
            string cachePath = Path.Combine(_tempDirectory, "update-check.json");
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.23.0", true))));
            var service = CreateService(handler, cachePath, () => now);
            using var firstOutput = new StringWriter();

            bool shouldExit = await service.TryNotifyAsync(
                "1.22.0",
                firstOutput,
                new StringReader("3\n"));

            Assert.False(shouldExit);
            Assert.Contains(
                "Skipped 1.23.0. You will be notified when a newer version is available.",
                firstOutput.ToString(),
                StringComparison.Ordinal);
            Assert.EndsWith(
                "Skipped 1.23.0. You will be notified when a newer version is available."
                + Environment.NewLine
                + Environment.NewLine,
                firstOutput.ToString(),
                StringComparison.Ordinal);

            var cachedHandler = new RecordingHttpHandler(
                _ => throw new InvalidOperationException("HTTP must not be used for a fresh cache."));
            var cachedService = CreateService(
                cachedHandler,
                cachePath,
                () => now.AddHours(1));
            using var secondOutput = new StringWriter();

            await cachedService.TryNotifyAsync(
                "1.22.0",
                secondOutput,
                new StringReader("1\n"));

            Assert.Equal(string.Empty, secondOutput.ToString());
            Assert.Equal(0, cachedHandler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_SkipUntilNextVersionSelected_NewerVersionPromptsAgain()
        {
            var now = new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero);
            string cachePath = Path.Combine(_tempDirectory, "update-check.json");
            var initialService = CreateService(
                new RecordingHttpHandler(_ => JsonResponse(
                    RegistrationIndex(("1.23.0", true)))),
                cachePath,
                () => now);

            await initialService.TryNotifyAsync(
                "1.22.0",
                TextWriter.Null,
                new StringReader("3\n"));

            var refreshHandler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.24.0", true))));
            var refreshService = CreateService(
                refreshHandler,
                cachePath,
                () => now.AddHours(UpdateNotificationService.CACHE_VALID_HOURS));
            using var output = new StringWriter();

            await refreshService.TryNotifyAsync(
                "1.22.0",
                output,
                new StringReader("2\n"));

            Assert.Contains("1.22.0 -> 1.24.0", output.ToString(), StringComparison.Ordinal);
            Assert.Equal(1, refreshHandler.RequestCount);
        }

        [Fact]
        public async Task TryNotifyAsync_UpdateCommandFails_ReportsFailureAndContinues()
        {
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("1.23.0", true))));
            var service = CreateService(
                handler,
                runUpdateCommand: _ => Task.FromResult(17));
            using var output = new StringWriter();

            bool shouldExit = await service.TryNotifyAsync(
                "1.22.0",
                output,
                new StringReader("1\n"));

            Assert.False(shouldExit);
            Assert.Contains("Update failed", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("exited with code 17", output.ToString(), StringComparison.Ordinal);
            Assert.Contains(UpdateNotificationService.UPDATE_COMMAND, output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task RunAsync_VersionFlag_WithAvailableUpdate_PreservesStdoutAndWritesNoticeToStderr()
        {
            var handler = new RecordingHttpHandler(_ => JsonResponse(
                RegistrationIndex(("99.0.0", true))));
            var updateService = CreateService(handler);
            var runner = new ProgramRunner(
                new TestLogger(logFileAbsolutePath: "test.log"),
                new ConfigService(),
                static _ => { },
                updateService);
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            TextReader originalIn = Console.In;
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            Console.SetOut(stdout);
            Console.SetError(stderr);
            Console.SetIn(new StringReader("2\n"));

            try
            {
                int exitCode = await runner.RunAsync(["--version"]);

                Assert.Equal(0, exitCode);
                Assert.Equal(
                    SystemInfo.GetAppVersion(typeof(Program)),
                    stdout.ToString().Trim());
                Assert.Contains("Update available!", stderr.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                Console.SetIn(originalIn);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private UpdateNotificationService CreateService(
            RecordingHttpHandler handler,
            string? cachePath = null,
            Func<DateTimeOffset>? utcNowProvider = null,
            Func<CancellationToken, Task<int>>? runUpdateCommand = null)
        {
            cachePath ??= Path.Combine(_tempDirectory, "update-check.json");
            utcNowProvider ??= static () => DateTimeOffset.UtcNow;
            return new UpdateNotificationService(
                new HttpClient(handler),
                () => cachePath,
                utcNowProvider,
                static () => true,
                runUpdateCommand);
        }

        private static string RegistrationIndex(params (string Version, bool Listed)[] versions)
        {
            var entries = new StringBuilder();
            for (int i = 0; i < versions.Length; i++)
            {
                if (i > 0)
                {
                    entries.Append(',');
                }

                entries.Append("{\"catalogEntry\":{\"version\":\"");
                entries.Append(versions[i].Version);
                entries.Append("\",\"listed\":");
                entries.Append(versions[i].Listed ? "true" : "false");
                entries.Append("}}");
            }

            return "{\"items\":[{\"items\":[" + entries + "]}]}";
        }

        private static HttpResponseMessage JsonResponse(string json)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

        private sealed class RecordingHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

            internal RecordingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            internal int RequestCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestCount++;
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}
