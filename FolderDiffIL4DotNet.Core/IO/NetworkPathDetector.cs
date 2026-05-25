using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace FolderDiffIL4DotNet.Core.IO
{
    /// <summary>
    /// Detects whether a given path is likely a network share (UNC / NFS / CIFS / SSHFS etc.).
    /// 指定パスがネットワーク共有の可能性が高いかを判定するクラス。
    /// </summary>
    public static class NetworkPathDetector
    {
        /// <summary>
        /// File-system types considered network drives on macOS.
        /// macOS でネットワークドライブとみなすファイルシステム種別。
        /// </summary>
        private static readonly HashSet<string> s_macNetworkFsTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "smbfs","afpfs","webdav","nfs","autofs","fusefs","osxfuse","sshfs"
        };

        /// <summary>
        /// File-system types considered network drives on Linux/Unix.
        /// Linux/Unix でネットワークドライブとみなすファイルシステム種別。
        /// </summary>
        private static readonly HashSet<string> s_unixNetworkFsTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "nfs","nfs4","cifs","smbfs","sshfs","fuse.sshfs","fuse.gvfsd-fuse","davfs","fuse.davfs","afpfs","fuse.afpfs","ceph","fuse.ceph","glusterfs","9p"
        };
        private const string WINDOWS_UNC_PREFIX = @"\\";
        private const string WINDOWS_UNC_DEVICE_PREFIX = @"\\?\UNC\";
        /// <summary>
        /// Forward-slash UNC prefix on Windows (e.g. //server/share).
        /// Windows でスラッシュ形式の UNC パスを示すプレフィックス。
        /// </summary>
        private const string WINDOWS_FORWARD_SLASH_UNC_PREFIX = "//";
        private const string PROC_MOUNTS_PATH = "/proc/mounts";
        private const string ETC_MTAB_PATH = "/etc/mtab";
        private const string ESCAPED_SPACE = "\\040";
        private const string SPACE = " ";
        private const int FILE_SYSTEM_NAME_FIELD_LENGTH = 16;
        private const int MACOS_PATH_LIMIT = 1024;
        /// <summary>
        /// macOS <c>statfs</c> flag. When <c>MNT_LOCAL</c> is set the filesystem is local; when unset it is likely a network filesystem.
        /// macOS の <c>statfs</c> フラグ。<c>MNT_LOCAL</c> が立っていればローカル、未セットならネットワークファイルシステムの可能性が高い。
        /// </summary>
        private const uint MNT_LOCAL = 0x00001000;

        /// <summary>
        /// Darwin (macOS) <c>fsid_t</c> structure.
        /// Darwin (macOS) の <c>fsid_t</c> 構造体。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct fsid_t
        {
            public int val1;
            public int val2;
        }

        /// <summary>
        /// Darwin (macOS) <c>struct statfs</c> definition (relevant fields only).
        /// Ref: /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/mount.h
        /// Darwin (macOS) の <c>struct statfs</c> 定義（必要フィールドのみ抜粋）。
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
        /// Returns true if the path is likely a network share (UNC / NFS / CIFS / SSHFS etc.).
        /// 指定パスがネットワーク共有の可能性が高いかを判定します。
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

        /// <summary>
        /// Returns true if the exception is recoverable during network-path detection.
        /// ネットワークパス検出時に発生し得る回復可能な例外かどうかを判定します。
        /// </summary>
        internal static bool IsRecoverableException(Exception ex)
            => ex is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or SecurityException
                or InvalidOperationException;

        /// <summary>
        /// Finds the filesystem type of the longest-matching mount point for the given path from Unix mount-format lines.
        /// Unix の mounts 形式行から、指定パスに最も長く一致するマウントポイントの fs type を取得します。
        /// </summary>
        internal static string? GetBestMatchingMountFileSystemType(string fullPath, IEnumerable<string> mountLines)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || mountLines == null)
            {
                return null;
            }

            string? bestMountPoint = null;
            string? bestFsType = null;

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

        // ── Private platform-specific helpers ─────────────────────────────────

        /// <summary>
        /// Retrieves the filesystem type and flags for a path on macOS via <c>statfs</c>.
        /// macOS で指定パスのファイルシステム種別およびフラグを取得します。
        /// </summary>
        private static bool TryGetFileSystemInfoOnMac(string path, out string? fsType, out uint flags)
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
            catch (Exception ex) when (IsRecoverableException(ex))
            {
                // Ignore recoverable detection errors and fall back to non-network.
                // 回復可能な検出エラーは無視し、非ネットワークとみなす。
            }
            return false;
        }

        internal static bool IsLikelyWindowsNetworkPath(string absolutePath)
        {
            if (absolutePath.StartsWith(WINDOWS_UNC_PREFIX, StringComparison.Ordinal)
                || absolutePath.StartsWith(WINDOWS_UNC_DEVICE_PREFIX, StringComparison.OrdinalIgnoreCase)
                || absolutePath.StartsWith(WINDOWS_FORWARD_SLASH_UNC_PREFIX, StringComparison.Ordinal))
            {
                return true;
            }

            string? root = TryGetPathRoot(absolutePath);
            return !string.IsNullOrEmpty(root) && IsNetworkDrive(root);
        }

        private static string? TryGetPathRoot(string absolutePath)
        {
            try
            {
                return Path.GetPathRoot(absolutePath);
            }
            catch (Exception ex) when (IsRecoverableException(ex))
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
            catch (Exception ex) when (IsRecoverableException(ex))
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

        internal static bool IsLikelyUnixNetworkPath(string absolutePath)
        {
            string? mountsFile = GetUnixMountsFilePath();
            if (mountsFile == null)
            {
                return false;
            }

            string? fullPath = TryGetFullPath(absolutePath);
            if (fullPath == null)
            {
                return false;
            }

            var mountLines = TryReadMountLines(mountsFile);
            if (mountLines == null)
            {
                return false;
            }

            string? bestFsType = GetBestMatchingMountFileSystemType(fullPath, mountLines);
            return !string.IsNullOrEmpty(bestFsType) && s_unixNetworkFsTypes.Contains(bestFsType);
        }

        internal static string? GetUnixMountsFilePath()
        {
            if (File.Exists(PROC_MOUNTS_PATH))
            {
                return PROC_MOUNTS_PATH;
            }

            return File.Exists(ETC_MTAB_PATH) ? ETC_MTAB_PATH : null;
        }

        private static string? TryGetFullPath(string absolutePath)
        {
            try
            {
                return Path.GetFullPath(absolutePath);
            }
            catch (Exception ex) when (IsRecoverableException(ex))
            {
                return null;
            }
        }

        internal static IEnumerable<string>? TryReadMountLines(string mountsFile)
        {
            try
            {
                return File.ReadLines(mountsFile);
            }
            catch (Exception ex) when (IsRecoverableException(ex))
            {
                return null;
            }
        }
    }
}
