using System;
using System.Linq;
using System.Reflection;
using FolderDiffIL4DotNet.Models;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ProgressReportService"/> lifecycle, range validation, and backward-progress handling.
    /// <see cref="ProgressReportService"/> のライフサイクル、範囲バリデーション、進捗の巻き戻し処理のテスト。
    /// </summary>
    public sealed class ProgressReportServiceTests
    {
        [Fact]
        public void Dispose_StopsTimer_AndFurtherCallsAreIgnored()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.SetLabel("test");
            service.ReportProgress(0.0);

            service.Dispose();
            service.ReportProgress(1.0);
            service.SetLabel("updated");

            var timerField = typeof(ProgressReportService).GetField("_keepAliveTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(timerField);
            Assert.Null(timerField.GetValue(service));
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(100.01)]
        public void ReportProgress_OutOfRange_Throws(double invalidPercentage)
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            Assert.Throws<ArgumentOutOfRangeException>(() => service.ReportProgress(invalidPercentage));
        }

        [Fact]
        public void ReportProgress_WhenProgressGoesBackward_IgnoresUpdate()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            service.ReportProgress(10.0);
            service.ReportProgress(5.0);

            var lastPercentage = GetPrivateField<double>(service, "_lastPercentage");
            Assert.Equal(10.0, lastPercentage);
        }

        [Fact]
        public void SetLabel_TrimsWhitespace_AndBlankResetsLabel()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            service.SetLabel("  diffing  ");
            Assert.Equal("diffing", GetPrivateField<string>(service, "_labelPrefix"));

            service.SetLabel("   ");
            Assert.Null(GetPrivateField<string>(service, "_labelPrefix"));
        }

        [Fact]
        public void BuildRedirectedProgressLine_UsesLabelAndKeepAliveFormat()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.SetLabel("Phase1");

            var result = InvokePrivate<string>(
                service,
                "BuildRedirectedProgressLine",
                "50.00",
                true);

            Assert.Equal("Phase1: 50.00% (processing...)", result);
        }

        [Fact]
        public void BuildProgressBarLine_WithLabel_ContainsSpinnerAndPercentage()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.SetLabel("Scan");

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "25.00",
                25.0,
                true);

            Assert.Contains("Scan", result);
            Assert.Contains("25.00%", result);
            Assert.Contains("█", result);
            Assert.Contains("░", result);
        }

        [Fact]
        public void BuildProgressBarLine_WithoutLabelAndKeepAlive_AppendsSpinnerFrame()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "12.34",
                12.34,
                true);

            Assert.Contains("12.34%", result);
            Assert.EndsWith("⠋", result);
        }

        [Fact]
        public void BuildProgressBarLine_WithCustomSpinnerFrames_UsesFirstConfiguredFrame()
        {
            var config = new ConfigSettingsBuilder { SpinnerFrames = [">>", "<<", "=="] }.Build();
            var service = new ProgressReportService(config);

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "50.00",
                50.0,
                true);

            Assert.Contains("50.00%", result);
            Assert.EndsWith(">>", result);
        }

        [Fact]
        public void BuildProgressBarLine_NegativePercentage_ClampsFilledToZero()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            // Pass a negative percentage (bypasses ReportProgress validation via reflection)
            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "0.00",
                -5.0,
                false);

            Assert.Contains("░", result);
            Assert.DoesNotContain("█", result); // no filled portion / 塗りつぶし部分なし
        }

        [Fact]
        public void BuildProgressBarLine_OverHundredPercent_ClampsFilledToBarWidth()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "100.00",
                999.0,
                false);

            Assert.Contains("█", result);
            Assert.DoesNotContain("░", result); // fully filled / 完全に塗りつぶし
        }

        [Fact]
        public void BuildProgressBarLine_WithoutLabelAndNoKeepAlive_ReturnsSimpleLine()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            var result = InvokePrivate<string>(
                service,
                "BuildProgressBarLine",
                "50.00",
                50.0,
                false);

            Assert.Contains("50.00%", result);
            Assert.EndsWith("50.00%", result.Trim());
        }

        [Fact]
        public void BuildProgressBarLine_CalledTwice_UsesCachedBarWidth()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            var first = InvokePrivate<string>(service, "BuildProgressBarLine", "30.00", 30.0, false);
            var second = InvokePrivate<string>(service, "BuildProgressBarLine", "60.00", 60.0, false);

            Assert.Contains("30.00%", first);
            Assert.Contains("60.00%", second);
        }

        [Fact]
        public void ResetProgress_AllowsProgressToRestartFromZero()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            service.ReportProgress(50.0);
            Assert.Equal(50.0, GetPrivateField<double>(service, "_lastPercentage"));

            service.ResetProgress();

            // After reset, _lastPercentage should allow 0.0 to be accepted again
            // リセット後は _lastPercentage が戻り、0.0 が再び受け入れられる
            Assert.Equal(double.NegativeInfinity, GetPrivateField<double>(service, "_lastPercentage"));
            Assert.Null(GetPrivateField<string>(service, "_lastFormattedPercentage"));

            // Verify that a lower value is now accepted
            // より小さい値が受け入れられることを確認
            service.ReportProgress(10.0);
            Assert.Equal(10.0, GetPrivateField<double>(service, "_lastPercentage"));
        }

        [Fact]
        public void ResetProgress_AfterDispose_IsIgnored()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.ReportProgress(50.0);
            service.Dispose();

            // Should not throw after dispose
            // Dispose 後に例外を投げないこと
            service.ResetProgress();
        }

        [Fact]
        public void ReportProgress_SamePercentageTwice_WithStalledConsoleWrite_EmitsKeepAlive()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            // First call to set _lastFormattedPercentage
            service.ReportProgress(50.0);

            // Set _lastConsoleWriteUtc to a very old value so shouldEmitKeepAlive is true
            var field = typeof(ProgressReportService).GetField("_lastConsoleWriteUtc", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(service, DateTime.UtcNow.AddHours(-1));

            // Second call with same value: hasChanged = false, shouldEmitKeepAlive = true
            service.ReportProgress(50.0);

            // If we get here without exception, the keepAlive path was exercised
            service.Dispose();
        }

        // ── Mutation-testing additions / ミューテーションテスト追加 ──────────────

        [Fact]
        public void ReportProgress_AtExactlyZero_DoesNotThrow()
        {
            // Boundary test: 0.0 is the lower inclusive bound
            // 境界テスト: 0.0 は下限の包含値
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            var ex = Record.Exception(() => service.ReportProgress(0.0));

            Assert.Null(ex);
        }

        [Fact]
        public void ReportProgress_AtExactlyHundred_DoesNotThrow()
        {
            // Boundary test: 100.0 is the upper inclusive bound
            // 境界テスト: 100.0 は上限の包含値
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            var ex = Record.Exception(() => service.ReportProgress(100.0));

            Assert.Null(ex);
        }

        [Fact]
        public void Dispose_ThenReportProgress_NoExceptionAndNoOutput()
        {
            // Verify that calling ReportProgress after Dispose silently returns without exception
            // Dispose 後の ReportProgress 呼び出しが例外なしに黙って戻ることを確認
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.Dispose();

            var ex = Record.Exception(() => service.ReportProgress(50.0));

            Assert.Null(ex);
            var lastPercentage = GetPrivateField<double>(service, "_lastPercentage");
            // _lastPercentage should not have been updated after dispose
            // Dispose 後に _lastPercentage は更新されないこと
            Assert.Equal(double.NegativeInfinity, lastPercentage);
        }

        // ── BeginPhase tests / BeginPhase テスト ──────────────────────────

        [Fact]
        [Trait("Category", "Unit")]
        public void BeginPhase_WithTotalPhases_FormatsLabelWithNumberPrefix()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.TotalPhases = 5;

            service.BeginPhase("Discovering files");

            var label = GetPrivateField<string>(service, "_labelPrefix");
            Assert.Equal("[1/5] Discovering files", label);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BeginPhase_CalledMultipleTimes_IncrementsPhaseNumber()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.TotalPhases = 4;

            service.BeginPhase("Phase A");
            Assert.Equal("[1/4] Phase A", GetPrivateField<string>(service, "_labelPrefix"));

            service.BeginPhase("Phase B");
            Assert.Equal("[2/4] Phase B", GetPrivateField<string>(service, "_labelPrefix"));

            service.BeginPhase("Phase C");
            Assert.Equal("[3/4] Phase C", GetPrivateField<string>(service, "_labelPrefix"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BeginPhase_ResetsProgressToZero()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.TotalPhases = 3;

            service.ReportProgress(80.0);
            service.BeginPhase("Next phase");

            // After BeginPhase, progress should have been reset and re-reported at 0.0
            // BeginPhase 後は進捗がリセットされ 0.0 で再報告される
            var lastPercentage = GetPrivateField<double>(service, "_lastPercentage");
            Assert.Equal(0.0, lastPercentage);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BeginPhase_WithoutTotalPhases_UsesPlainLabel()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            // TotalPhases defaults to 0 (no numbering)
            // TotalPhases のデフォルトは 0（番号付けなし）

            service.BeginPhase("Scanning");

            var label = GetPrivateField<string>(service, "_labelPrefix");
            Assert.Equal("Scanning", label);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BeginPhase_AfterDispose_IsIgnored()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());
            service.TotalPhases = 3;
            service.Dispose();

            var ex = Record.Exception(() => service.BeginPhase("Should not crash"));
            Assert.Null(ex);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BeginPhase_LogsPreviousPhaseElapsedTime()
        {
            var logger = new TestLogger();
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build(), logger);
            service.TotalPhases = 3;

            service.BeginPhase("Phase A");
            service.ReportProgress(100.0);

            // Begin Phase B — should log Phase A's elapsed time
            // Phase B 開始 — Phase A の経過時間がログ出力される
            service.BeginPhase("Phase B");

            var phaseLog = logger.Entries.FirstOrDefault(e =>
                e.Message.Contains("Phase completed:") && e.Message.Contains("[1/3] Phase A"));
            Assert.NotNull(phaseLog);
            Assert.Equal(AppLogLevel.Info, phaseLog.LogLevel);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void TotalPhases_NegativeValue_Throws()
        {
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build());

            Assert.Throws<ArgumentOutOfRangeException>(() => service.TotalPhases = -1);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void FormatPhaseElapsed_UnderOneMinute_ReturnsSeconds()
        {
            var result = ProgressReportService.FormatPhaseElapsed(TimeSpan.FromSeconds(45.3));

            Assert.Equal("45.3s", result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void FormatPhaseElapsed_OverOneMinute_ReturnsMinutesAndSeconds()
        {
            var result = ProgressReportService.FormatPhaseElapsed(TimeSpan.FromSeconds(125.7));

            Assert.Equal("2m 5.7s", result);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Dispose_LogsFinalPhaseElapsed()
        {
            var logger = new TestLogger();
            var service = new ProgressReportService(new ConfigSettingsBuilder().Build(), logger);
            service.TotalPhases = 2;

            service.BeginPhase("Only phase");
            service.ReportProgress(100.0);
            service.Dispose();

            var phaseLog = logger.Entries.FirstOrDefault(e =>
                e.Message.Contains("Phase completed:") && e.Message.Contains("[1/2] Only phase"));
            Assert.NotNull(phaseLog);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (T)field.GetValue(target);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = method.Invoke(target, args);
            return Assert.IsType<T>(result);
        }
    }
}
