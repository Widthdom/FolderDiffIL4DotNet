using System;
using System.IO;

namespace FolderDiffIL4DotNet.Core.Common
{
    /// <summary>
    /// Reusable exception filter predicates for common <c>catch (Exception ex) when (...)</c> patterns.
    /// よく使われる <c>catch (Exception ex) when (...)</c> パターンを再利用可能なフィルタ述語として提供します。
    /// </summary>
    public static class ExceptionFilters
    {
        /// <summary>
        /// Returns true for file-I/O recoverable exceptions: <see cref="IOException"/>,
        /// <see cref="UnauthorizedAccessException"/>, <see cref="NotSupportedException"/>.
        /// ファイル I/O で発生し得る回復可能な例外に対して true を返します。
        /// </summary>
        public static bool IsFileIoRecoverable(Exception ex)
            => ex is IOException
                or UnauthorizedAccessException
                or NotSupportedException;

        /// <summary>
        /// Returns true for file-I/O + operation recoverable exceptions:
        /// <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>,
        /// <see cref="InvalidOperationException"/>, <see cref="NotSupportedException"/>.
        /// ファイル I/O + 操作系の回復可能な例外に対して true を返します。
        /// </summary>
        public static bool IsFileIoOrOperationRecoverable(Exception ex)
            => ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or NotSupportedException;

        /// <summary>
        /// Returns true for path-validation + file-I/O recoverable exceptions:
        /// <see cref="ArgumentException"/>, <see cref="IOException"/>,
        /// <see cref="UnauthorizedAccessException"/>, <see cref="NotSupportedException"/>.
        /// パス検証 + ファイル I/O の回復可能な例外に対して true を返します。
        /// </summary>
        public static bool IsPathOrFileIoRecoverable(Exception ex)
            => ex is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException;

        /// <summary>
        /// Returns true for process-execution recoverable exceptions:
        /// <see cref="System.ComponentModel.Win32Exception"/>, <see cref="InvalidOperationException"/>,
        /// <see cref="IOException"/>, <see cref="NotSupportedException"/>, <see cref="UnauthorizedAccessException"/>.
        /// プロセス実行時の回復可能な例外に対して true を返します。
        /// </summary>
        public static bool IsProcessExecutionRecoverable(Exception ex)
            => ex is System.ComponentModel.Win32Exception
                or InvalidOperationException
                or IOException
                or NotSupportedException
                or UnauthorizedAccessException;
    }
}
