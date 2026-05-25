using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Extracts the target framework / SDK version from a .NET assembly's metadata.
    /// .NET アセンブリのメタデータからターゲットフレームワーク / SDK バージョンを抽出します。
    /// </summary>
    internal static class AssemblySdkVersionReader
    {
        private const string TARGET_FRAMEWORK_ATTRIBUTE_NAME = "TargetFrameworkAttribute";
        private const string TARGET_FRAMEWORK_ATTRIBUTE_NAMESPACE = "System.Runtime.Versioning";

        /// <summary>
        /// Reads the TargetFrameworkAttribute value from a .NET assembly (e.g. ".NETCoreApp,Version=v8.0").
        /// Returns a human-friendly display string (e.g. ".NET 8.0") or null on failure.
        /// .NET アセンブリから TargetFrameworkAttribute 値を読み取ります（例: ".NETCoreApp,Version=v8.0"）。
        /// 人間向け表示文字列（例: ".NET 8.0"）を返し、失敗時は null。
        /// </summary>
        internal static string? ReadTargetFramework(string assemblyPath)
        {
            try
            {
                using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var peReader = new PEReader(stream);
                if (!peReader.HasMetadata)
                    return null;

                var reader = peReader.GetMetadataReader();
                var assemblyDef = reader.GetAssemblyDefinition();

                foreach (var attrHandle in assemblyDef.GetCustomAttributes())
                {
                    var attr = reader.GetCustomAttribute(attrHandle);
                    if (!IsTargetFrameworkAttribute(reader, attr))
                        continue;

                    // Decode the attribute value blob / 属性値 blob をデコード
                    string? rawValue = DecodeStringAttributeValue(reader, attr);
                    if (rawValue != null)
                        return FormatTargetFramework(rawValue);
                }

                return null;
            }
#pragma warning disable CA1031 // Best-effort extraction / ベストエフォート抽出
            catch
            {
                return null;
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Reads SDK versions from both old and new assemblies and returns a display string.
        /// Returns null if neither has a readable version.
        /// If both are the same: "net8.0". If different: "net6.0 → net8.0".
        /// 旧新両アセンブリの SDK バージョンを読み取り、表示文字列を返します。
        /// 両方読めない場合は null。同一なら "net8.0"、異なれば "net6.0 → net8.0"。
        /// </summary>
        internal static string? ReadPairDisplayString(string oldAssemblyPath, string newAssemblyPath)
        {
            string? oldVersion = ReadTargetFramework(oldAssemblyPath);
            string? newVersion = ReadTargetFramework(newAssemblyPath);

            if (oldVersion == null && newVersion == null)
                return null;

            if (oldVersion == null)
                return newVersion;
            if (newVersion == null)
                return oldVersion;

            if (string.Equals(oldVersion, newVersion, StringComparison.Ordinal))
                return oldVersion;

            return $"{oldVersion} → {newVersion}";
        }

        /// <summary>
        /// Checks whether a custom attribute is System.Runtime.Versioning.TargetFrameworkAttribute.
        /// カスタム属性が TargetFrameworkAttribute かどうかを判定します。
        /// </summary>
        private static bool IsTargetFrameworkAttribute(MetadataReader reader, CustomAttribute attr)
        {
            if (attr.Constructor.Kind == HandleKind.MemberReference)
            {
                var memberRef = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                if (memberRef.Parent.Kind == HandleKind.TypeReference)
                {
                    var typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                    string name = reader.GetString(typeRef.Name);
                    string ns = reader.GetString(typeRef.Namespace);
                    return name == TARGET_FRAMEWORK_ATTRIBUTE_NAME &&
                           ns == TARGET_FRAMEWORK_ATTRIBUTE_NAMESPACE;
                }
            }
            return false;
        }

        /// <summary>
        /// Decodes the first string argument from a custom attribute's value blob.
        /// カスタム属性値 blob から最初の文字列引数をデコードします。
        /// </summary>
        private static string? DecodeStringAttributeValue(MetadataReader reader, CustomAttribute attr)
        {
            // Custom attribute value blob format:
            // [0..1] Prolog (0x0001)
            // [2..] Fixed args — first arg is a SerString (UTF-8 with length prefix)
            // カスタム属性値 blob 形式:
            // [0..1] プロログ (0x0001)
            // [2..] 固定引数 — 最初の引数は SerString（長さプレフィックス付き UTF-8）
            var blobReader = reader.GetBlobReader(attr.Value);
            if (blobReader.Length < 4) // prolog + at least 2 bytes for string
                return null;

            ushort prolog = blobReader.ReadUInt16();
            if (prolog != 0x0001)
                return null;

            return blobReader.ReadSerializedString();
        }

        /// <summary>
        /// Converts a raw TargetFramework string to a human-friendly display form.
        /// E.g. ".NETCoreApp,Version=v8.0" → ".NET 8.0", ".NETFramework,Version=v4.7.2" → ".NET Framework 4.7.2".
        /// TargetFramework 生文字列を人間向け表示形式に変換します。
        /// </summary>
        internal static string FormatTargetFramework(string rawValue)
        {
            // Common raw formats:
            // ".NETCoreApp,Version=v8.0" → ".NET 8.0"
            // ".NETFramework,Version=v4.7.2" → ".NET Framework 4.7.2"
            // ".NETStandard,Version=v2.1" → ".NET Standard 2.1"
            int commaIdx = rawValue.IndexOf(',');
            if (commaIdx < 0)
                return rawValue;

            string framework = rawValue.Substring(0, commaIdx);
            string versionPart = rawValue.Substring(commaIdx + 1);

            // Extract version number after "Version=v"
            // "Version=v" の後のバージョン番号を抽出
            string version = "";
            int vIdx = versionPart.IndexOf("Version=v", StringComparison.OrdinalIgnoreCase);
            if (vIdx >= 0)
                version = versionPart.Substring(vIdx + "Version=v".Length);
            else
            {
                int eqIdx = versionPart.IndexOf('=');
                if (eqIdx >= 0)
                    version = versionPart.Substring(eqIdx + 1);
            }

            return framework switch
            {
                ".NETCoreApp" => $".NET {version}",
                ".NETFramework" => $".NET Framework {version}",
                ".NETStandard" => $".NET Standard {version}",
                _ => $"{framework} {version}"
            };
        }
    }
}
