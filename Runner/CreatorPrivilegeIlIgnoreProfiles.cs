using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace FolderDiffIL4DotNet.Runner
{
    /// <summary>
    /// Loads maintainer-managed IL ignore profiles from an embedded JSON catalog.
    /// メンテナー管理の IL 無視プロファイルを埋め込み JSON カタログから読み込みます。
    /// </summary>
    internal static class CreatorPrivilegeIlIgnoreProfiles
    {
        private const string RESOURCE_NAME = "FolderDiffIL4DotNet.Runner.creator_il_ignore_profiles.json";
        private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> _profiles = new(LoadProfiles);

        /// <summary>
        /// Returns whether the specified profile name exists in the embedded catalog.
        /// 指定したプロファイル名が埋め込みカタログに存在するかどうかを返します。
        /// </summary>
        internal static bool IsKnownProfile(string? profileName)
        {
            return !string.IsNullOrWhiteSpace(profileName)
                && _profiles.Value.ContainsKey(profileName);
        }

        /// <summary>
        /// Resolves a known profile to its IL ignore strings.
        /// 既知プロファイルを IL 無視文字列一覧へ解決します。
        /// </summary>
        internal static IReadOnlyList<string> GetStringsOrThrow(string profileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

            if (_profiles.Value.TryGetValue(profileName, out var strings))
            {
                return strings;
            }

            throw new InvalidOperationException(
                $"Unknown creator IL ignore profile '{profileName}'. Known profiles: {GetKnownProfilesDisplayText()}.");
        }

        /// <summary>
        /// Returns a human-readable, sorted list of known profile names.
        /// 既知プロファイル名のソート済み一覧を人間向け文字列で返します。
        /// </summary>
        internal static string GetKnownProfilesDisplayText()
        {
            var names = new string[_profiles.Value.Count];
            int index = 0;
            foreach (var name in _profiles.Value.Keys)
            {
                names[index++] = name;
            }
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return names.Length == 0 ? "(none)" : string.Join(", ", names);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadProfiles()
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCE_NAME)
                ?? throw new InvalidOperationException($"Embedded resource '{RESOURCE_NAME}' was not found.");
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();

            var catalog = JsonSerializer.Deserialize<CreatorIlIgnoreProfileCatalog>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Creator IL ignore profile catalog could not be deserialized.");

            if (catalog.Profiles == null || catalog.Profiles.Count == 0)
            {
                throw new InvalidOperationException("Creator IL ignore profile catalog does not contain any profiles.");
            }

            var profiles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in catalog.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name))
                {
                    throw new InvalidOperationException("Creator IL ignore profile catalog contains a profile with an empty name.");
                }

                if (profile.Strings == null || profile.Strings.Count == 0)
                {
                    throw new InvalidOperationException($"Creator IL ignore profile '{profile.Name}' does not contain any strings.");
                }

                if (profiles.ContainsKey(profile.Name))
                {
                    throw new InvalidOperationException($"Creator IL ignore profile '{profile.Name}' is defined more than once.");
                }

                profiles.Add(profile.Name, profile.Strings.AsReadOnly());
            }

            return profiles;
        }

        private sealed class CreatorIlIgnoreProfileCatalog
        {
            public List<CreatorIlIgnoreProfileDefinition> Profiles { get; set; } = new();
        }

        private sealed class CreatorIlIgnoreProfileDefinition
        {
            public string Name { get; set; } = string.Empty;
            public List<string> Strings { get; set; } = new();
        }
    }
}
