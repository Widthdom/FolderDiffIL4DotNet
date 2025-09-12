using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using System.Text;

namespace FolderDiffIL4DotNet.Utils
{
    /// <summary>
    /// 汎用ユーティリティメソッドを提供するクラス
    /// </summary>
    public static class Utility
    {
        #region private read only member variables
        /// <summary>
        /// Windows の禁止記号群（\\ / : * ? " < > |）。これを上限集合として全OSに適用。
        /// 制御文字(0x00-0x1F)のチェックは別途実装側で行います。
        /// </summary>
        private static readonly char[] s_windowsInvalidFileNameChars = ['"', '<', '>', '|', '\\', '/', ':', '*', '?'];

        /// <summary>
        /// Windows の予約名（拡張子の有無を問わずNG）。
        /// </summary>
        private static readonly string[] s_windowsReservedNames =
        [
            "CON","PRN","AUX","NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        ];
        #endregion

        #region private interop (macOS)
        /// <summary>
        /// macOS の <c>statfs</c> におけるフラグ。<c>MNT_LOCAL</c> が立っている場合はローカルファイルシステム。
        /// 本値が未セットの場合はネットワークファイルシステムの可能性が高いとみなします。
        /// </summary>
        private const uint MNT_LOCAL = 0x00001000; // ローカルファイルシステムならセット

        /// <summary>
        /// Darwin (macOS) の <c>fsid_t</c> 構造体。
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct fsid_t
        {
            public int val1;
            public int val2;
        }

        /// <summary>
        /// Darwin (macOS) の <c>struct statfs</c> 定義（必要フィールドのみを抜粋）。
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
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string f_fstypename; // e.g., "apfs", "smbfs", "nfs"
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string f_mntonname;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string f_mntfromname;
            // 予約領域（実レイアウトとサイズを揃えるため8要素）
            public uint f_reserved0;
            public uint f_reserved1;
            public uint f_reserved2;
            public uint f_reserved3;
            public uint f_reserved4;
            public uint f_reserved5;
            public uint f_reserved6;
            public uint f_reserved7;
        }

        /// <summary>
        /// macOS の <c>statfs</c> 関数。指定パスのファイルシステム情報を取得します。
        /// </summary>
        [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern int statfs(string path, out statfs_darwin buf);

        /// <summary>
        /// macOS で指定パスのファイルシステム種別およびフラグを取得します。
        /// </summary>
        /// <param name="path">判定対象の絶対パス。</param>
        /// <param name="fsType">取得されたファイルシステム種別（例: apfs, smbfs）。取得失敗時は null。</param>
        /// <param name="flags">取得されたフラグ（<c>MNT_LOCAL</c> など）。取得失敗時は 0。</param>
        /// <returns>取得できた場合 true。それ以外は false。</returns>
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
            catch
            {
                // ignore
            }
            return false;
        }
        #endregion

        #region public methods
        /// <summary>
        /// 指定パスがネットワーク共有（UNC/NFS/CIFS/SSHFS など）の可能性が高いかを判定します。
        /// - Windows: UNC (\\\\ / \\?\UNC\) とネットワークドライブを検出。
        /// - macOS: <c>statfs</c> の <c>f_flags</c>（<c>MNT_LOCAL</c>）および <c>f_fstypename</c>（smbfs/afpfs/webdav/nfs/sshfs 等）で判定。
        /// - Linux/Unix: /proc/mounts または /etc/mtab を解析し、nfs/cifs/smbfs/sshfs 等のFSタイプを検出。
        /// </summary>
        public static bool IsLikelyNetworkPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return false;
            }
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // UNC: \\server\share or \\?\UNC\server\share
                    if (absolutePath.StartsWith(@"\\\\", StringComparison.Ordinal) || absolutePath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    // ネットワークドライブ（マップドドライブ）
                    var root = Path.GetPathRoot(absolutePath);
                    if (!string.IsNullOrEmpty(root))
                    {
                        try
                        {
                            var di = new DriveInfo(root);
                            if (di.DriveType == DriveType.Network)
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // ignore and fallthrough
                        }
                    }
                    return false;
                }

                // macOS: statfs による判定
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    if (TryGetFileSystemInfoOnMac(absolutePath, out var fsTypeMac, out var flagsMac))
                    {
                        // MNT_LOCAL が立っていなければネットワーク FS とみなす
                        if ((flagsMac & MNT_LOCAL) == 0)
                        {
                            return true;
                        }
                        // 代表的なネットワークFS名のヒューリスティクス
                        var macNetworkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "smbfs","afpfs","webdav","nfs","autofs","fusefs","osxfuse","sshfs"
                        };
                        if (!string.IsNullOrEmpty(fsTypeMac) && macNetworkTypes.Contains(fsTypeMac))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                // Linux/Unix: /proc/mounts または /etc/mtab を解析
                string mountsFile = null;
                if (File.Exists("/proc/mounts"))
                {
                    mountsFile = "/proc/mounts";
                }
                else if (File.Exists("/etc/mtab"))
                {
                    mountsFile = "/etc/mtab";
                }
                if (mountsFile == null)
                {
                    return false; // 判定材料がない
                }

                var networkFsTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "nfs","nfs4","cifs","smbfs","sshfs","fuse.sshfs","fuse.gvfsd-fuse","davfs","fuse.davfs","afpfs","fuse.afpfs","ceph","fuse.ceph","glusterfs","9p"
                };

                string fullPath = Path.GetFullPath(absolutePath);
                string bestMountPoint = null;
                string bestFsType = null;

                foreach (var line in File.ReadLines(mountsFile))
                {
                    // フォーマット: device mountpoint fstype options ...
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        continue;
                    }
                    // mtab/proc/mounts ではスペースが \040 としてエスケープされる
                    var parts = line.Split(' ');
                    if (parts.Length < 3)
                    {
                        continue;
                    }
                    string mountPointRaw = parts[1];
                    string fsType = parts[2];
                    string mountPoint = mountPointRaw.Replace("\\040", " ");

                    // 最長一致のマウントポイントを選ぶ
                    if (fullPath.StartsWith(mountPoint.EndsWith("/") ? mountPoint : mountPoint + "/", StringComparison.Ordinal) || string.Equals(fullPath, mountPoint, StringComparison.Ordinal))
                    {
                        if (bestMountPoint == null || mountPoint.Length > bestMountPoint.Length)
                        {
                            bestMountPoint = mountPoint;
                            bestFsType = fsType;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(bestFsType) && networkFsTypes.Contains(bestFsType))
                {
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }
        /// <summary>
        /// 実行アセンブリの「ユーザ向け表示用バージョン文字列」を取得します。
        /// 取得順序: <see cref="System.Reflection.AssemblyInformationalVersionAttribute.InformationalVersion"/> が非空ならそれを返し、
        /// 未定義または空/空白のみの場合は <see cref="FileVersionInfo.FileVersion"/> を利用します。
        /// いずれの情報も有効でない（null / 空白）場合は <see cref="InvalidOperationException"/> を送出します。
        /// 文字列の加工（SemVer 正規化 / プレリリース除去 等）は行わず、そのまま返します。
        /// </summary>
        /// <param name="programType">バージョンを取得したいアセンブリを含む型（通常は Program クラス型）。</param>
        /// <returns>表示用途に利用できるバージョン文字列。</returns>
        /// <exception cref="InvalidOperationException">どのバージョン情報も取得できなかった場合。</exception>
        public static string GetAppVersion(Type programType)
        {
            var assembly = programType.Assembly;
            var infoAttr = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly);
            var infoVer = infoAttr?.InformationalVersion;
            var fileVer = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            var verToShow = string.IsNullOrWhiteSpace(infoVer) ? fileVer : infoVer;
            if (string.IsNullOrWhiteSpace(verToShow))
            {
                throw new InvalidOperationException("Version string is empty.");
            }
            return verToShow;
        }
        /// <summary>
        /// フォルダ名として妥当か（macOS/Linux/Windowsの禁止事項を包括）検証し、問題があれば例外を投げます。
        /// - 空白や空、"."/".." は不可
        /// - 制御文字(0x00-0x1F)や \ / : * ? " < > | を含むと不可
        /// - 末尾のスペースやドット不可
        /// - Windows予約名（CON, PRN, AUX, NUL, COM1..9, LPT1..9）は拡張子の有無に関わらず不可
        /// </summary>
        /// <param name="folderName">検証するフォルダ名</param>
        /// <param name="paramName">例外に含めるパラメータ名（省略可）</param>
        /// <exception cref="ArgumentException">フォルダ名が不正な場合</exception>
        public static void ValidateFolderNameOrThrow(string folderName, string paramName = null)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentException("Folder name cannot be empty or whitespace.", paramName ?? nameof(folderName));
            }

            // "." と ".." は不可
            if (folderName == "." || folderName == "..")
            {
                throw new ArgumentException("Folder name cannot be '.' or '..'.", paramName ?? nameof(folderName));
            }

            // 制御文字 / 禁則文字のチェック
            foreach (var ch in folderName)
            {
                if (ch <= 0x1F || s_windowsInvalidFileNameChars.Contains(ch))
                {
                    throw new ArgumentException($"Folder name contains invalid character: '{ch}'.", paramName ?? nameof(folderName));
                }
            }

            // 末尾スペース/ドット不可（Windows仕様に合わせて厳格化）
            if (folderName.EndsWith(" ") || folderName.EndsWith("."))
            {
                throw new ArgumentException("Folder name cannot end with a space or a dot.", paramName ?? nameof(folderName));
            }

            // Windows予約名チェック（拡張子が付いていてもNG）
            var trimmed = folderName.TrimEnd(' ', '.');
            var basePart = trimmed.Split('.')[0];
            var upper = basePart.ToUpperInvariant();
            if (s_windowsReservedNames.Contains(upper))
            {
                throw new ArgumentException($"Folder name '{folderName}' is a reserved name on Windows.", paramName ?? nameof(folderName));
            }
        }

        /// <summary>
        /// 指定された2つのファイルのMD5ハッシュ値を比較します。
        /// </summary>
        /// <param name="file1AbsolutePath">ファイル1の絶対パス</param>
        /// <param name="file2AbsolutePath">ファイル2の絶対パス</param>
        /// <returns>ハッシュ値が等しい場合は true、それ以外の場合は false</returns>
        /// <exception cref="FileNotFoundException">指定されたファイルが見つからない場合にスローされます。</exception>
        /// <exception cref="UnauthorizedAccessException">ファイルへのアクセス権限がない場合にスローされます。</exception>
        /// <exception cref="IOException">ファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public static async Task<bool> DiffFilesByHashAsync(string file1AbsolutePath, string file2AbsolutePath)
        {
            try
            {
                // まずサイズが異なれば不一致（I/O を最小化）
                var file1Info = new FileInfo(file1AbsolutePath);
                var file2Info = new FileInfo(file2AbsolutePath);
                if (file1Info.Length != file2Info.Length)
                {
                    return false;
                }

                using var md5 = MD5.Create();
                // ネットワークI/O最適化: 逐次読みヒントを与える
                using var file1stream = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 64, options: FileOptions.SequentialScan);
                using var file2stream = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 64, options: FileOptions.SequentialScan);
                var hash1 = await md5.ComputeHashAsync(file1stream);
                var hash2 = await md5.ComputeHashAsync(file2stream);
                return hash1.SequenceEqual(hash2);
            }
            catch (FileNotFoundException ex)
            {
                throw new FileNotFoundException($"File not found during hash diff: {ex.FileName}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access denied during hash diff for file: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"I/O error during hash diff: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 指定ファイルの MD5 を計算し、32桁の16進小文字文字列として返します。
        /// </summary>
        /// <param name="fileAbsolutePath">対象ファイルの絶対パス。</param>
        /// <returns>MD5 の16進小文字文字列。</returns>
        /// <exception cref="FileNotFoundException">ファイルが存在しない場合。</exception>
        /// <exception cref="UnauthorizedAccessException">アクセス権が不足している場合。</exception>
        /// <exception cref="IOException">読み取り中に I/O エラーが発生した場合。</exception>
        public static string ComputeFileMd5Hex(string fileAbsolutePath)
        {
            using (var md5 = MD5.Create())
            using (var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hash = md5.ComputeHash(fileStream);
                // 例: BitConverter.ToString => "AA-BB-.." を "aabb.." へ
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// テキストファイルを行単位で逐次比較します。両ファイルの先頭から読み進め、
        /// いずれかの行が異なった時点で false を返します。全行が一致すれば true を返します。
        /// </summary>
        /// <param name="file1AbsolutePath">ファイル1の絶対パス</param>
        /// <param name="file2AbsolutePath">ファイル2の絶対パス</param>
        /// <returns>ファイルが等しい場合は true、それ以外の場合は false</returns>
        /// <exception cref="FileNotFoundException">指定されたファイルが見つからない場合にスローされます。</exception>
        /// <exception cref="UnauthorizedAccessException">ファイルへのアクセス権限がない場合にスローされます。</exception>
        /// <exception cref="IOException">ファイルの読み取り中にエラーが発生した場合にスローされます。</exception>
        public static async Task<bool> DiffTextFilesAsync(string file1AbsolutePath, string file2AbsolutePath)
        {
            // 逐次読みのヒントを与える（ネットワーク共有でも効率的）
            using var fs1 = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 64, options: FileOptions.SequentialScan);
            using var fs2 = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 64, options: FileOptions.SequentialScan);
            using var file1StreamReader = new StreamReader(fs1);
            using var file2StreamReader = new StreamReader(fs2);

            string file1Line;
            string file2Line;

            do
            {
                file1Line = await file1StreamReader.ReadLineAsync();
                file2Line = await file2StreamReader.ReadLineAsync();

                if (file1Line != file2Line)
                {
                    return false;
                }
            } while (file1Line != null && file2Line != null);

            return true;
        }

        /// <summary>
        /// サイズが閾値を超えるテキストファイルに対して高速化を目的に並列チャンク比較を行う実験的メソッド。
        /// 完全一致判定のみを行い、差分箇所の特定は行いません。
        /// なお、本メソッドはエラーや引数不正が発生した場合でも例外を呼出し側へ送出せず、false を返します。
        /// </summary>
        /// <param name="file1AbsolutePath">ファイル1の絶対パス</param>
        /// <param name="file2AbsolutePath">ファイル2の絶対パス</param>
        /// <param name="largeFileSizeThresholdBytes">並列化閾値（バイト）。これ未満は逐次比較。</param>
        /// <param name="maxParallel">最大並列度</param>
        /// <returns>一致すれば true。エラーや引数不正時は false。</returns>
        public static async Task<bool> DiffTextFilesParallelAsync(string file1AbsolutePath, string file2AbsolutePath, long largeFileSizeThresholdBytes = 512 * 1024, int maxParallel = 1)
        {
            try
            {
                var file1Info = new FileInfo(file1AbsolutePath);
                var file2Info = new FileInfo(file2AbsolutePath);
                if (!file1Info.Exists || !file2Info.Exists)
                {
                    return false;
                }
                if (file1Info.Length != file2Info.Length)
                {
                    return false;
                }
                if (file1Info.Length < largeFileSizeThresholdBytes)
                {
                    // 小さい場合は既存逐次ロジックを再利用
                    return await DiffTextFilesAsync(file1AbsolutePath, file2AbsolutePath);
                }
                if (maxParallel <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxParallel), maxParallel, "The maximum degree of parallelism must be 1 or greater.");
                }

                int chunkSize = 64 * 1024; // 64KB
                int chunkCount = (int)((file1Info.Length + chunkSize - 1) / chunkSize);
                var differences = 0;
                await Parallel.ForEachAsync(Enumerable.Range(start: 0, chunkCount), new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, async (index, cancellationToken) =>
                {
                    if (Volatile.Read(ref differences) != 0)
                    {
                        return; // 早期終了
                    }
                    var buffer1 = new byte[chunkSize];
                    var buffer2 = new byte[chunkSize];
                    int read1, read2;
                    using (var file1Stream = new FileStream(file1AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var file2Stream = new FileStream(file2AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        file1Stream.Seek((long)index * chunkSize, SeekOrigin.Begin);
                        file2Stream.Seek((long)index * chunkSize, SeekOrigin.Begin);
                        read1 = await file1Stream.ReadAsync(buffer1, offset: 0, chunkSize, cancellationToken);
                        read2 = await file2Stream.ReadAsync(buffer2, offset: 0, chunkSize, cancellationToken);
                    }
                    if (read1 != read2)
                    {
                        Interlocked.Exchange(location1: ref differences, value: 1);
                        return;
                    }
                    for (int i = 0; i < read1; i++)
                    {
                        if (buffer1[i] != buffer2[i])
                        {
                            Interlocked.Exchange(location1: ref differences, value: 1);
                            break;
                        }
                    }
                });
                return differences == 0;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 指定されたファイルが .NET 実行可能ファイル（PE かつ CLR ヘッダを持つ）かを判定します。
        /// エラーが発生した場合や判定不能な場合は false を返します（例外は送出しません）。
        /// </summary>
        /// <param name="fileAbsolutePath">判定するファイルの絶対パス</param>
        /// <returns>.NET 実行可能ファイルと判定できた場合は true、それ以外（非 PE/CLR、読み取り失敗含む）の場合は false。</returns>
        public static bool IsDotNetExecutable(string fileAbsolutePath)
        {
            try
            {
                using (var fileStream = new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read))
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    // PEファイルのマジックナンバーを確認 (0x4D, 0x5A は "MZ" ヘッダ)
                    if (binaryReader.ReadUInt16() != 0x5A4D) // "MZ" in little-endian
                    {
                        return false;
                    }

                    // PEヘッダの位置を取得
                    fileStream.Seek(offset: 0x3C, SeekOrigin.Begin);
                    int peHeaderOffset = binaryReader.ReadInt32();

                    // PEヘッダを確認
                    fileStream.Seek(offset: peHeaderOffset, SeekOrigin.Begin);
                    if (binaryReader.ReadUInt32() != 0x00004550) // "PE\0\0"
                    {
                        return false;
                    }

                    // CLRヘッダの存在を確認
                    fileStream.Seek(offset: peHeaderOffset + 0x18 + 0x70, SeekOrigin.Begin); // Optional Header の CLR Runtime Header の位置
                    int clrHeaderRva = binaryReader.ReadInt32();

                    // CLRヘッダが存在する場合は .NET 実行可能ファイルと判定
                    return clrHeaderRva != 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 絶対パスの長さがOSの上限を超えていないかを検証し、超過時は例外を投げます。
        /// 目安の上限値:
        /// - Windows: 260（拡張パスプレフィックス \\?\ 使用時は 32767）
        /// - macOS:   1024
        /// - Linux:   4096
        /// 注意: 実際の制限は環境やAPIによって差異があります。本メソッドは一般的な安全域で検証します。
        /// また、与えられた文字列が「絶対パスかどうか」の検証は行いません（長さのみを検査）。
        /// </summary>
        /// <param name="absolutePath">検証対象の絶対パス</param>
        /// <param name="paramName">例外に含めるパラメータ名（省略可）</param>
        /// <exception cref="ArgumentException">絶対パスが空、または上限超過の場合</exception>
        public static void ValidateAbsolutePathLengthOrThrow(string absolutePath, string paramName = null)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                throw new ArgumentException("Absolute path cannot be null or whitespace.", paramName ?? nameof(absolutePath));
            }

            int limit;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 拡張長パス（\\?\ または \\?\UNC\）は理論上 32,767 文字まで
                bool isExtended = absolutePath.StartsWith(@"\\?\", StringComparison.Ordinal);
                limit = isExtended ? 32767 : 260;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                limit = 1024;
            }
            else // Linux その他POSIX想定
            {
                limit = 4096;
            }

            if (absolutePath.Length > limit)
            {
                throw new ArgumentException($"Absolute path is too long for this OS (length {absolutePath.Length} > limit {limit}).", paramName ?? nameof(absolutePath));
            }
        }

        /// <summary>
        /// 指定されたファイルの最終更新日時を取得し、
        /// 「yyyy-MM-dd HH:mm:ss.fff zzz」形式の文字列（ミリ秒精度・タイムゾーン付き）で返します。
        /// <para>
        /// 内部的には <see cref="File.GetLastWriteTime(string)"/> を使用します。取得に失敗した場合は
        /// 元例外を <see cref="Exception.InnerException"/> に保持した <see cref="Exception"/> を送出します。
        /// </para>
        /// </summary>
        /// <param name="fileAbsolutepath">最終更新日時を取得するファイルの絶対パス。</param>
        /// <returns>ミリ秒精度・タイムゾーン付きの最終更新日時文字列。</returns>
        /// <exception cref="Exception">最終更新日時の取得に失敗した場合。元例外を <see cref="Exception.InnerException"/> に保持します。</exception>
        public static string GetTimestamp(string fileAbsolutepath)
        {
            try
            {
                return new DateTimeOffset(File.GetLastWriteTime(fileAbsolutepath)).ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve the last modified time of '{fileAbsolutepath}'.", ex);
            }
        }

        /// <summary>
        /// 指定されたファイルに読み取り専用属性（<see cref="FileAttributes.ReadOnly"/>）を付与します。
        /// 既に読み取り専用の場合は何も行いません。プラットフォームに依存せず、.NET の属性設定に従って適用されます。
        /// </summary>
        /// <param name="fileAbsolutePath">対象ファイルの絶対パス。存在しない場合は例外を送出します。</param>
        /// <exception cref="ArgumentException">パスが null または空白のみの場合。</exception>
        /// <exception cref="FileNotFoundException">指定ファイルが存在しない場合。</exception>
        /// <exception cref="Exception">属性設定に失敗した場合。元例外を <see cref="Exception.InnerException"/> に保持します。</exception>
        public static void TrySetReadOnly(string fileAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(fileAbsolutePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(fileAbsolutePath));
            }
            if (!File.Exists(fileAbsolutePath))
            {
                throw new FileNotFoundException("File not found.", fileAbsolutePath);
            }
            try
            {
                var fileAttributes = File.GetAttributes(fileAbsolutePath);
                if ((fileAttributes & FileAttributes.ReadOnly) == 0)
                {
                    File.SetAttributes(fileAbsolutePath, fileAttributes | FileAttributes.ReadOnly);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set read-only attribute for '{fileAbsolutePath}'.", ex);
            }
        }

        /// <summary>
        /// サニタイズ(\\, /, :, ..を.に置換)返します。
        /// </summary>
        public static string Sanitize(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }
            var s = str.Replace('\\', '.').Replace('/', '.').Replace(':', '.');
            while (s.Contains(".."))
            {
                s = s.Replace("..", ".");
            }
            return s.Trim('.');
        }

        /// <summary>
        /// 任意の文字列を「ファイル名として安全に使える文字列」へ変換します。
        /// 無効文字およびコロン(:)は '_' に置換し、長すぎる場合は
        /// 「先頭40 + "_.._" + 末尾8 + '_' + 短縮ハッシュ(SHA1の先頭6バイト=12hex)」で短縮します。
        /// </summary>
        /// <param name="fileNameExcludeExtention">変換対象の文字列（拡張子は含めない想定）。</param>
        /// <param name="maxLength">短縮判定の閾値（既定: 180）。これ以上なら短縮します。</param>
        /// <returns>ファイル名として使用可能な安全な文字列。</returns>
        public static string ToSafeFileName(string fileNameExcludeExtention, int maxLength = 180)
        {
            if (string.IsNullOrEmpty(fileNameExcludeExtention))
            {
                return fileNameExcludeExtention ?? string.Empty;
            }

            // 1) 不正文字（OS依存）や ':' を '_' に置換
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var stringBuilder = new StringBuilder(fileNameExcludeExtention.Length);
            foreach (var ch in fileNameExcludeExtention)
            {
                if (ch == ':' || Array.IndexOf(invalidFileNameChars, ch) >= 0)
                {
                    stringBuilder.Append('_');
                }
                else
                {
                    stringBuilder.Append(ch);
                }
            }
            var sanitizedFileNameExcludeExtention = stringBuilder.ToString();

            // 2) 長すぎる場合は短縮（先頭40 + '..' + 末尾8 + '_' + 短縮ハッシュ）
            if (sanitizedFileNameExcludeExtention.Length >= maxLength)
            {
                using var sha1 = SHA1.Create();
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sanitizedFileNameExcludeExtention));
                var hashHex = BitConverter.ToString(hash, 0, 6).Replace("-", "").ToLowerInvariant();
                var head = sanitizedFileNameExcludeExtention[..Math.Min(40, sanitizedFileNameExcludeExtention.Length)];
                var tail = sanitizedFileNameExcludeExtention[^Math.Min(8, sanitizedFileNameExcludeExtention.Length)..];
                sanitizedFileNameExcludeExtention = head + "_.._" + tail + "_" + hashHex;
            }
            return sanitizedFileNameExcludeExtention;
        }

        /// <summary>
        /// シェルコマンド文字列を簡易にトークン分割（空白区切り・クォート対応）。
        /// </summary>
        public static List<string> TokenizeCommand(string str)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(str))
            {
                return list;
            }

            bool inQuotes = false;
            char quoteChar = '\0';
            var current = new StringBuilder();

            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (inQuotes)
                {
                    if (c == quoteChar)
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"' || c == '\'')
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (current.Length > 0)
                        {
                            list.Add(current.ToString());
                            current.Clear();
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }
            if (current.Length > 0)
            {
                list.Add(current.ToString());
            }
            return list;
        }

        /// <summary>
        /// 与えられた文字列に非ASCII文字（0x80以上）が含まれているかどうかを判定します。
        /// null/空文字列は false を返します。
        /// </summary>
        public static bool ContainsNonAscii(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }
            foreach (var ch in str)
            {
                if (ch > 0x7F)
                {
                    return true;
                }
            }
            return false;
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
            catch
            {
                /* ignore */
            }
        }

        /// <summary>
        /// 指定した実行ファイルと引数でプロセスを起動し、終了コードが 0 の場合に
        /// 標準出力（空なら標準エラー）をトリムして返します。失敗時は null を返します。
        /// 例外は送出しません（起動失敗・非ゼロ終了コード・読み取り失敗などを含め null を返します）。
        /// </summary>
        /// <param name="exe">実行ファイルパス</param>
        /// <param name="args">引数列</param>
        /// <returns>成功時の出力文字列（Trim 済み）。それ以外は null。</returns>
        public static async Task<string> TryGetProcessOutputAsync(string exe, IEnumerable<string> args)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (args != null)
            {
                foreach (var arg in args)
                {
                    processStartInfo.ArgumentList.Add(arg);
                }
            }

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                var stdout = stdoutTask.Result;
                var stderr = stderrTask.Result;
                var outText = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
                return outText?.Trim();
            }
            return null;
        }

        /// <summary>
        /// コマンドと引数を連結してベースラベルを返却します。
        /// </summary>
        public static string BuildBaseLabel(string command, string[] args)
        {
            var usedArgs = GetUsedArgs(args);
            return string.IsNullOrEmpty(usedArgs) ? command : $"{command} {usedArgs}";
        }

        /// <summary>
        /// 引数の配列から、使用されている引数を取得します。
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string GetUsedArgs(string[] args) => string.Join(" ", args.Select(x => x.Contains(' ') ? $"\"{x}\"" : x));
        #endregion
    }
}
