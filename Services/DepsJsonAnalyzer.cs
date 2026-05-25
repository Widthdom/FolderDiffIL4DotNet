using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Compares two .deps.json files and produces a structured <see cref="DependencyChangeSummary"/>
    /// showing which NuGet packages were added, removed, or updated.
    /// Best-effort: returns <see langword="null"/> on parse failures.
    /// 2 つの .deps.json ファイルを比較し、NuGet パッケージの追加・削除・更新を示す
    /// 構造化された <see cref="DependencyChangeSummary"/> を生成します。
    /// ベストエフォート: パース失敗時は <see langword="null"/> を返します。
    /// </summary>
    internal static class DepsJsonAnalyzer
    {
        /// <summary>
        /// Analyses two .deps.json files and returns a summary of dependency changes.
        /// Returns <see langword="null"/> if analysis fails (best-effort).
        /// 2 つの .deps.json ファイルを解析し、依存関係変更の要約を返します。
        /// 解析に失敗した場合は <see langword="null"/> を返します（ベストエフォート）。
        /// </summary>
        public static DependencyChangeSummary? Analyze(string oldFilePath, string newFilePath, Action<Exception>? onError = null)
        {
            try
            {
                var oldDeps = ExtractLibraryVersions(oldFilePath);
                var newDeps = ExtractLibraryVersions(newFilePath);

                var entries = new List<DependencyChangeEntry>();

                // Removed: in old but not in new / 旧にあって新にないもの
                foreach (var (name, oldVersion) in oldDeps)
                {
                    if (!newDeps.ContainsKey(name))
                    {
                        entries.Add(new DependencyChangeEntry("Removed", name, oldVersion, ""));
                    }
                }

                // Added or Updated / 追加または更新
                foreach (var (name, newVersion) in newDeps)
                {
                    if (!oldDeps.TryGetValue(name, out var oldVersion))
                    {
                        entries.Add(new DependencyChangeEntry("Added", name, "", newVersion));
                    }
                    else if (!string.Equals(oldVersion, newVersion, StringComparison.Ordinal))
                    {
                        entries.Add(new DependencyChangeEntry("Updated", name, oldVersion, newVersion));
                    }
                }

                // Build reverse index: package → referencing assemblies / 逆引きインデックス構築: パッケージ → 参照アセンブリ
                var reverseIndex = BuildReferencingAssembliesIndex(newFilePath, oldFilePath, onError);

                // Classify importance and attach referencing assemblies for each entry
                // 各エントリの重要度分類と参照アセンブリの付与
                for (int i = 0; i < entries.Count; i++)
                {
                    entries[i] = ClassifyImportance(entries[i]);
                    if (reverseIndex.TryGetValue(entries[i].PackageName, out var refs))
                        entries[i] = entries[i] with { ReferencingAssemblies = refs };
                }

                // Sort: by Change order (Added → Removed → Updated), then by package name
                // ソート: Change 順（Added → Removed → Updated）、次にパッケージ名順
                entries.Sort((a, b) =>
                {
                    int cmp = ChangeOrder(a.Change).CompareTo(ChangeOrder(b.Change));
                    if (cmp != 0) return cmp;
                    return StringComparer.OrdinalIgnoreCase.Compare(a.PackageName, b.PackageName);
                });

                return new DependencyChangeSummary { Entries = entries };
            }
#pragma warning disable CA1031 // ベストエフォート解析のため全例外をキャッチ / Catch-all for best-effort analysis
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                return null;
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Extracts library name → version mappings from a .deps.json file.
        /// The "libraries" object in .deps.json has keys in "Name/Version" format.
        /// .deps.json ファイルからライブラリ名→バージョンのマッピングを抽出します。
        /// .deps.json の "libraries" オブジェクトのキーは "Name/Version" 形式です。
        /// </summary>
        internal static Dictionary<string, string> ExtractLibraryVersions(string filePath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var jsonBytes = File.ReadAllBytes(filePath);

            using var doc = JsonDocument.Parse(jsonBytes);
            var root = doc.RootElement;

            if (!root.TryGetProperty("libraries", out var libraries))
                return result;

            foreach (var lib in libraries.EnumerateObject())
            {
                // Key format: "PackageName/Version" (e.g. "Serilog/3.0.0")
                // キー形式: "PackageName/Version"（例: "Serilog/3.0.0"）
                var slashIndex = lib.Name.IndexOf('/', StringComparison.Ordinal);
                if (slashIndex <= 0 || slashIndex >= lib.Name.Length - 1)
                    continue;

                var name = lib.Name.Substring(0, slashIndex);
                var version = lib.Name.Substring(slashIndex + 1);
                result[name] = version;
            }

            return result;
        }

        /// <summary>
        /// Classifies the importance of a dependency change entry.
        /// Removed = High (potential breaking change), Added = Medium (new dependency),
        /// Updated with major version change = High, minor = Medium, patch = Low.
        /// 依存関係変更エントリの重要度を分類します。
        /// Removed = High（破壊的変更の可能性）、Added = Medium（新規依存）、
        /// Updated でメジャーバージョン変更 = High、マイナー = Medium、パッチ = Low。
        /// </summary>
        internal static DependencyChangeEntry ClassifyImportance(DependencyChangeEntry entry)
        {
            var importance = entry.Change switch
            {
                "Removed" => ChangeImportance.High,
                "Added" => ChangeImportance.Medium,
                "Updated" => ClassifyVersionChange(entry.OldVersion, entry.NewVersion),
                _ => ChangeImportance.Low
            };
            return entry with { Importance = importance };
        }

        /// <summary>
        /// Determines importance of a version update by comparing major.minor.patch components.
        /// メジャー.マイナー.パッチ比較によりバージョン更新の重要度を判定します。
        /// </summary>
        private static ChangeImportance ClassifyVersionChange(string oldVersion, string newVersion)
        {
            var oldParts = ParseVersionParts(oldVersion);
            var newParts = ParseVersionParts(newVersion);

            // Major version change = High (breaking) / メジャーバージョン変更 = High（破壊的）
            if (oldParts.Major != newParts.Major)
                return ChangeImportance.High;

            // Minor version change = Medium (new features) / マイナーバージョン変更 = Medium（新機能）
            if (oldParts.Minor != newParts.Minor)
                return ChangeImportance.Medium;

            // Patch or other = Low (bug fixes) / パッチ等 = Low（バグ修正）
            return ChangeImportance.Low;
        }

        /// <summary>
        /// Parses version string into major/minor/patch components.
        /// Tolerant of non-standard formats (e.g. "1.0.0-preview.1").
        /// バージョン文字列をメジャー/マイナー/パッチに分解します。
        /// 非標準形式にも寛容です（例: "1.0.0-preview.1"）。
        /// </summary>
        private static (int Major, int Minor, int Patch) ParseVersionParts(string version)
        {
            if (string.IsNullOrEmpty(version))
                return (0, 0, 0);

            // Strip pre-release suffix (e.g. "-preview.1") / プレリリースサフィックスを除去
            var hyphenIndex = version.IndexOf('-', StringComparison.Ordinal);
            var versionCore = hyphenIndex >= 0 ? version.Substring(0, hyphenIndex) : version;

            var parts = versionCore.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;

            return (major, minor, patch);
        }

        /// <summary>
        /// Builds a reverse index from package name to the list of assemblies that depend on it,
        /// by parsing the "targets" section of .deps.json files. Merges results from both old and new files.
        /// .deps.json の "targets" セクションを解析し、パッケージ名→依存アセンブリ一覧の逆引きインデックスを構築します。
        /// 旧ファイルと新ファイルの両方からマージします。
        /// </summary>
        internal static Dictionary<string, List<string>> BuildReferencingAssembliesIndex(
            string newFilePath, string oldFilePath, Action<Exception>? onError = null)
        {
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            CollectReferencesFromTargets(newFilePath, index, onError);
            CollectReferencesFromTargets(oldFilePath, index, onError);
            // Sort each list and deduplicate / 各リストをソートして重複排除
            foreach (var kv in index)
            {
                kv.Value.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = kv.Value.Count - 1; i > 0; i--)
                    if (string.Equals(kv.Value[i], kv.Value[i - 1], StringComparison.OrdinalIgnoreCase))
                        kv.Value.RemoveAt(i);
            }
            return index;
        }

        /// <summary>
        /// Parses a single .deps.json targets section and populates the reverse index.
        /// 単一の .deps.json の targets セクションを解析し逆引きインデックスに追加します。
        /// </summary>
        private static void CollectReferencesFromTargets(string filePath, Dictionary<string, List<string>> index, Action<Exception>? onError)
        {
            try
            {
                var jsonBytes = File.ReadAllBytes(filePath);
                using var doc = JsonDocument.Parse(jsonBytes);
                var root = doc.RootElement;

                if (!root.TryGetProperty("targets", out var targets))
                    return;

                // Iterate each target framework / 各ターゲットフレームワークを列挙
                foreach (var tfm in targets.EnumerateObject())
                {
                    foreach (var lib in tfm.Value.EnumerateObject())
                    {
                        // Extract library name from "Name/Version" key / "Name/Version" キーからライブラリ名を抽出
                        var slashIdx = lib.Name.IndexOf('/', StringComparison.Ordinal);
                        if (slashIdx <= 0) continue;
                        var libName = lib.Name.Substring(0, slashIdx);

                        if (!lib.Value.TryGetProperty("dependencies", out var deps))
                            continue;

                        foreach (var dep in deps.EnumerateObject())
                        {
                            if (!index.TryGetValue(dep.Name, out var list))
                            {
                                list = new List<string>();
                                index[dep.Name] = list;
                            }
                            list.Add(libName);
                        }
                    }
                }
            }
#pragma warning disable CA1031 // ベストエフォート / best-effort
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
#pragma warning restore CA1031
        }

        private static int ChangeOrder(string change)
            => change switch { "Added" => 0, "Removed" => 1, "Updated" => 2, _ => 3 };
    }
}
