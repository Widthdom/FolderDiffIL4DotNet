using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using FolderDiffIL4DotNet.Services;
using FolderDiffIL4DotNet.Tests.Helpers;
using Xunit;

namespace FolderDiffIL4DotNet.Tests
{
    [Trait("Category", "Unit")]
    public sealed class TestLoggerTests
    {
        [Fact]
        public async Task LogMessage_ConcurrentCalls_CapturesAllEntriesWithoutLosingMessages()
        {
            var callbackEntries = new ConcurrentQueue<TestLogEntry>();
            var logger = new TestLogger(onEntry: callbackEntries.Enqueue);

            await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (index, cancellationToken) =>
            {
                await Task.Yield();
                logger.LogMessage(AppLogLevel.Info, $"message-{index}", shouldOutputMessageToConsole: false);
            });

            Assert.Equal(200, logger.Entries.Count);
            Assert.Equal(200, callbackEntries.Count);
            Assert.Equal(200, logger.Messages.Distinct().Count());
        }

        [Fact]
        public void Messages_ReturnsCapturedMessagesInInsertionOrderForSequentialWrites()
        {
            var logger = new TestLogger();

            logger.LogMessage(AppLogLevel.Info, "first", shouldOutputMessageToConsole: false);
            logger.LogMessage(AppLogLevel.Warning, "second", shouldOutputMessageToConsole: false);

            Assert.Equal(new[] { "first", "second" }, logger.Messages.ToArray());
        }
    }
}
