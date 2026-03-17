using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using FolderDiffIL4DotNet.Core.Common;

namespace FolderDiffIL4DotNet.Core.IO
{
    /// <summary>
    /// ファイルシステム操作（タイムスタンプ取得、読み取り専用設定、ファイル削除、ネットワークパス検出）を提供するクラス
    /// </summary>
    public static class FileSystemUtility
    {
        /// <summary>
        /// macOS でネットワークドライブとみなすファイルシステム種別。
        /// </summary>
        private static readonly HashSet<string> s_macNetworkFsTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "smbfs","afpfs","webdav","nfs","autofs","fusefs","osxfuse","sshfs"
        };

        /// <summary>
        /// Linux/Unix でネットワークドライブとみなすファイルシステム種別。
        /// </summary>
        private static readonly HashSet<string> s_unixNetworkFsTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "nfs","nfs4","cifs","smbfs","sshfs","fuse.sshfs","fuse.gvfsd-fuse","davfs","fuse.davfs","afpfs","fuse.afpfs","ceph","fuse.ceph","glusterfs","9p"
        };
        private const string WINDOWS_UNC_PREFIX = @"\\";
        private const string WINDOWS_UNC_DEVICE_PREFIX = @"\\?\UNC\";
        /// <summary>
        /// Windows でスラッシュ形式の UNC パスを示すプレフィックス（例: //server/share）。
        /// </summary>
        private const string WINDOWS_FORWARD_SLASH_UNC_PREFIX = "//";
        private const string PROC_MOUNTS_PATH = "/proc/mounts";
        private const string ETC_MTAB_PATH = "/etc/mtab";
        private const string ESCAPED_SPACE = "\\040";
        private const string SPACE = " ";
        private const int FILE_SYSTEM_NAME_FIELD_LENGTH = 16;
        private const int MACOS_PATH_LIMIT = 1024;
        private const string ERROR_FILE_PATH_NULL = "File path cannot be null or whitespace.";
        private const string ERROR_FILE_NOT_FOUND = "File not found.";
        /// <summary>
        /// macOS の <c><see cref="statfs"/></c> におけるフラグ。<c>MNT_LOCAL</c> が立っている場合はローカルファイルシステム。
        /// 本値が未セットの場合はネットワークファイルシステムの可能性が高いとみなします。
        /// </summary>
        private const uint MNT_LOCAL = 0x00001000;

        /// <summary>
        /// Darwin (macOS) の <c><see cref="fsid_t"/></c> 構造体。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct fsid_t
        {
            public int val1;
            public int val2;
        }

        /// <summary>
        /// Darwin (macOS) の <c>struct <see cref="statfs"/></c> 定義（必要フィールドのみを抜粋）。
        /// 参考: /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/mount.h
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct statfs_darwin
        {
            public uint f_bsize;
            public int f_iosize;
            public ulong f_blocks;
            public ulong f_bfree;
            public ulong f_bavail;
            public ulong f_files;
            public ulong f_ffree;
            public fsid_t f_fsid;
            public uint f_owner;
            public uint f_type;
            public uint f_flags;
            public uint f_fssubtype;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = FILE_SYSTEM_NAME_FIELD_LENGTH)]
            public string f_fstypename;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MACOS_PATH_LIMIT)]
            public string f_mntonname;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MACOS_PATH_LIMIT)]
            public string f_mntfromname;
            public uint f_reserved0;
            public uint f_reserved1;
            public uint f_reserved2;
            public uint f_reserved3;
            public uint f_reserved4;
            public uint f_reserved5;
            public uint f_reserved6;
            public uint f_reserved7;
        }

        [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern int statfs(string path, out statfs_darwin buf);

        /// <summary>
        /// macOS で指定パスのファイルシステム種別およびフラグを取得します。
        /// </summary>
        private static bool TryGetFileSystemInfoOnMac(string path, out string fsType, out uint flags)
        {
            fsType = null;
            flags = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }
                var rc = statfs(path, out var info);
                if (rc == 0)
                {
                    fsType = info.f_fstypename;
                    flags = info.f_flags;
                    return true;
                }
            }
            catch (Exception ex) when (IsRecoverableNetworkPathDetectionException(ex))
            {
                // ignore recoverable detection errors and fall back to non-network.
            }
            return false;
        }

        /// <summary>
        /// ネットワークパス検出時に発生し得る回復可能な例外かどうかを判定します。
        /// </summary>
        private static bool IsRecoverableNetworkPathDetectionException(Exception ex)
            => ex is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or SecurityException
                or InvalidOperationException;

        /// <summary>
        /// Unix の mounts 形式行から、指定パスに最も長く一致するマウントポイントの fs type を取得します。
        /// </summary>
        private static string GetBestMatchingMountFileSystemType(string fullPath, IEnumerable<string> mountLines)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || mountLines == null)
            {
                return null;
            }

            string bestMountPoint = null;
            string bestFsType = null;

            foreach (var line in mountLines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                var parts = line.Split(' ');
                if (parts.Length < 3)
                {
                    continue;
                }

                string mountPointRaw = parts[1];
                string fsType = parts[2];
                string mountPoint = mountPointRaw.Replace(ESCAPED_SPACE, SPACE);

                bool isMatch = fullPath.StartsWith(mountPoint.EndsWith("/") ? mountPoint : mountPoint + "/", StringComparison.Ordinal)
                    || string.Equals(fullPath, mountPoint, StringComparison.Ordinal);
                if (!isMatch)
                {
                    continue;
                }

                if (bestMountPoint == null || mountPoint.Length > bestMountPoint.Length)
                {
                    bestMountPoint = mountPoint;
                    bestFsType = fsType;
                }
            }

            return bestFsType;
        }
        /// <summary>
        /// 指定されたファイルの最終更新日時を取得し、
        /// 「yyyy-MM-dd HH:mm:ss」形式の文字列（秒精度）で返します。
        /// </summary>
        public static string GetTimestamp(string fileAbsolutepath)
        {
            return new DateTimeOffset(File.GetLastWriteTime(fileAbsolutepath))
                .ToString(CoreConstants.TIMESTAMP_WITH_TIME_ZONE_FORMAT, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 指定されたファイルに読み取り専用属性（<see cref="FileAttributes.ReadOnly"/>）を付与します。
        /// 既に読み取り専用の場合は何も行いません。
        /// </summary>
        public static void TrySetReadOnly(string fileAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(fileAbsolutePath))
            {
                throw new ArgumentException(ERROR_FILE_PATH_NULL, nameof(fileAbsolutePath));
            }
            if (!File.Exists(fileAbsolutePath))
            {
                throw new FileNotFoundException(ERROR_FILE_NOT_FOUND, fileAbsolutePath);
            }
            var fileAttributes = File.GetAttributes(fileAbsolutePath);
            if ((fileAttributes & FileAttributes.ReadOnly) == 0)
            {
                File.SetAttributes(fileAbsolutePath, fileAttributes | FileAttributes.ReadOnly);
            }
        }

        /// <summary>
        /// ファイルを例外を無視して削除します。
        /// </summary>
        public static void DeleteFileSilent(string fileAbsolutePath)
        {
            if (string.IsNullOrEmpty(fileAbsolutePath))
            {
                return;
            }

            try
            {
                File.Delete(fileAbsolutePath);
            }
            catch (IOException)
            {
                /* ignore */
            }
            catch (UnauthorizedAccessException)
            {
                /* ignore */
            }
            catch (NotSupportedException)
            {
                /* ignore */
            }
        }

        /// <summary>
        /// 指定パスがネットワーク共有（UNC/NFS/CIFS/SSHFS など）の可能性が高いかを判定します。
        /// </summary>
        public static bool IsLikelyNetworkPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return false;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return IsLikelyWindowsNetworkPath(absolutePath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return IsLikelyMacNetworkPath(absolutePath);
            }

            return IsLikelyUnixNetworkPath(absolutePath);
        }

        private static bool IsLikelyWindowsNetworkPath(string absolutePath)
        {
            if (absolutePath.StartsWith(WINDOWS_UNC_PREFIX, StringComparison.Ordinal)
                || absolutePath.StartsWith(WINDOWS_UNC_DEVICE_PREFIX, StringComparison.OrdinalIgnoreCase)
                || absolutePath.StartsWith(WINDOWS_FORWARD_SLASH_UNC_PREFIX, StringComparison.Ordinal))
            {
                return true;
            }

            string root = TryGetPathRoot(absolutePath);
            return !string.IsNullOrEmpty(root) && IsNetworkDrive(root);
        }

        private static string TryGetPathRoot(string absolutePath)
        {
            try
            {
                return Path.GetPathRoot(absolutePath);
            }
            catch (Exception ex) when (IsRecoverableNetworkPathDetectionException(ex))
            {
                return null;
            }
        }

        private static bool IsNetworkDrive(string root)
        {
            try
            {
                return new DriveInfo(root).DriveType == DriveType.Network;
            }
            catch (Exception ex) when (IsRecoverableNetworkPathDetectionException(ex))
            {
                return false;
            }
        }

        private static bool IsLikelyMacNetworkPath(string absolutePath)
        {
            if (!TryGetFileSystemInfoOnMac(absolutePath, out var fsTypeMac, out var flagsMac))
            {
                return false;
            }

            return (flagsMac & MNT_LOCAL) == 0
                || (!string.IsNullOrEmpty(fsTypeMac) && s_macNetworkFsTypes.Contains(fsTypeMac));
        }

        private static bool IsLikelyUnixNetworkPath(string absolutePath)
        {
            string mountsFile = GetUnixMountsFilePath();
            if (mountsFile == null)
            {
                return false;
            }

            string fullPath = TryGetFullPath(absolutePath);
            if (fullPath == null)
            {
                return false;
            }

            var mountLines = TryReadMountLines(mountsFile);
            if (mountLines == null)
            {
                return false;
            }

            string bestFsType = GetBestMatchingMountFileSystemType(fullPath, mountLines);
            return !string.IsNullOrEmpty(bestFsType) && s_unixNetworkFsTypes.Contains(bestFsType);
        }

        private static string GetUnixMountsFilePath()
        {
            if (File.Exists(PROC_MOUNTS_PATH))
            {
                return PROC_MOUNTS_PATH;
            }

            return File.Exists(ETC_MTAB_PATH) ? ETC_MTAB_PATH : null;
        }

        private static string TryGetFullPath(string absolutePath)
        {
            try
            {
                return Path.GetFullPath(absolutePath);
            }
            catch (Exception ex) when (IsRecoverableNetworkPathDetectionException(ex))
            {
                return null;
            }
        }

        private static IEnumerable<string> TryReadMountLines(string mountsFile)
        {
            try
            {
                return File.ReadLines(mountsFile);
            }
            catch (Exception ex) when (IsRecoverableNetworkPathDetectionException(ex))
            {
                return null;
            }
        }
    }
}
