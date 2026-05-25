using System.Collections.Generic;

namespace FolderDiffIL4DotNet.Models
{
    /// <summary>
    /// CycloneDX SBOM record (spec version 1.5).
    /// CycloneDX SBOM レコード（仕様バージョン 1.5）。
    /// </summary>
    public sealed class CycloneDxBom
    {
        /// <summary>CycloneDX schema identifier. / CycloneDX スキーマ識別子。</summary>
        public string BomFormat { get; init; } = "CycloneDX";

        /// <summary>CycloneDX specification version. / CycloneDX 仕様バージョン。</summary>
        public string SpecVersion { get; init; } = "1.5";

        /// <summary>Unique serial number for this BOM. / この BOM の一意なシリアル番号。</summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>BOM version (incremented on updates). / BOM バージョン（更新ごとにインクリメント）。</summary>
        public int Version { get; init; } = 1;

        /// <summary>Metadata about the BOM generation. / BOM 生成に関するメタデータ。</summary>
        public CycloneDxMetadata Metadata { get; init; } = new();

        /// <summary>List of software components. / ソフトウェアコンポーネント一覧。</summary>
        public List<CycloneDxComponent> Components { get; init; } = new();
    }

    /// <summary>
    /// CycloneDX metadata section.
    /// CycloneDX メタデータセクション。
    /// </summary>
    public sealed class CycloneDxMetadata
    {
        /// <summary>ISO 8601 timestamp. / ISO 8601 タイムスタンプ。</summary>
        public string Timestamp { get; init; } = string.Empty;

        /// <summary>Tools used to generate the BOM. / BOM 生成に使用したツール。</summary>
        public List<CycloneDxTool> Tools { get; init; } = new();
    }

    /// <summary>
    /// CycloneDX tool entry.
    /// CycloneDX ツールエントリ。
    /// </summary>
    public sealed class CycloneDxTool
    {
        /// <summary>Vendor / ベンダー。</summary>
        public string Vendor { get; init; } = string.Empty;

        /// <summary>Tool name. / ツール名。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Tool version. / ツールバージョン。</summary>
        public string Version { get; init; } = string.Empty;
    }

    /// <summary>
    /// CycloneDX component entry representing a single file/assembly in the compared folders.
    /// CycloneDX コンポーネントエントリ。比較フォルダ内の単一ファイル/アセンブリを表します。
    /// </summary>
    public sealed class CycloneDxComponent
    {
        /// <summary>Component type (e.g. "library", "file"). / コンポーネント種別。</summary>
        public string Type { get; init; } = "library";

        /// <summary>Component name. / コンポーネント名。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Component version (assembly version or empty). / コンポーネントバージョン。</summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>Scope: "required" or "optional". / スコープ。</summary>
        public string Scope { get; init; } = "required";

        /// <summary>SHA256 hashes for integrity. / 整合性用 SHA256 ハッシュ。</summary>
        public List<CycloneDxHash> Hashes { get; init; } = new();

        /// <summary>
        /// Additional properties (status, diff detail, folder).
        /// 追加プロパティ（ステータス、差分詳細、フォルダ）。
        /// </summary>
        public List<CycloneDxProperty> Properties { get; init; } = new();
    }

    /// <summary>
    /// CycloneDX hash entry.
    /// CycloneDX ハッシュエントリ。
    /// </summary>
    public sealed class CycloneDxHash
    {
        /// <summary>Hash algorithm (e.g. "SHA-256"). / ハッシュアルゴリズム。</summary>
        public string Alg { get; init; } = string.Empty;

        /// <summary>Hash content. / ハッシュ値。</summary>
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// CycloneDX property entry (name-value pair).
    /// CycloneDX プロパティエントリ（名前-値ペア）。
    /// </summary>
    public sealed class CycloneDxProperty
    {
        /// <summary>Property name. / プロパティ名。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Property value. / プロパティ値。</summary>
        public string Value { get; init; } = string.Empty;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SPDX models (SPDX 2.3 JSON format)
    // SPDX モデル（SPDX 2.3 JSON 形式）
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SPDX 2.3 document record.
    /// SPDX 2.3 ドキュメントレコード。
    /// </summary>
    public sealed class SpdxDocument
    {
        /// <summary>SPDX version header. / SPDX バージョンヘッダー。</summary>
        public string SpdxVersion { get; init; } = "SPDX-2.3";

        /// <summary>Data license (always CC0-1.0 for SPDX). / データライセンス。</summary>
        public string DataLicense { get; init; } = "CC0-1.0";

        /// <summary>SPDX identifier for this document. / このドキュメントの SPDX 識別子。</summary>
        public string SPDXID { get; init; } = "SPDXRef-DOCUMENT";

        /// <summary>Document name. / ドキュメント名。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Document namespace (unique URI). / ドキュメント名前空間（一意 URI）。</summary>
        public string DocumentNamespace { get; init; } = string.Empty;

        /// <summary>Creation info. / 作成情報。</summary>
        public SpdxCreationInfo CreationInfo { get; init; } = new();

        /// <summary>Packages (components). / パッケージ（コンポーネント）。</summary>
        public List<SpdxPackage> Packages { get; init; } = new();
    }

    /// <summary>
    /// SPDX creation info.
    /// SPDX 作成情報。
    /// </summary>
    public sealed class SpdxCreationInfo
    {
        /// <summary>ISO 8601 creation timestamp. / ISO 8601 作成タイムスタンプ。</summary>
        public string Created { get; init; } = string.Empty;

        /// <summary>Creator entries (Tool, Organization, Person). / 作成者エントリ。</summary>
        public List<string> Creators { get; init; } = new();
    }

    /// <summary>
    /// SPDX package entry representing a component.
    /// SPDX パッケージエントリ。コンポーネントを表します。
    /// </summary>
    public sealed class SpdxPackage
    {
        /// <summary>SPDX identifier for this package. / このパッケージの SPDX 識別子。</summary>
        public string SPDXID { get; init; } = string.Empty;

        /// <summary>Package name. / パッケージ名。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Package version. / パッケージバージョン。</summary>
        public string VersionInfo { get; init; } = string.Empty;

        /// <summary>Download location (NOASSERTION for local files). / ダウンロード場所。</summary>
        public string DownloadLocation { get; init; } = "NOASSERTION";

        /// <summary>Files analyzed flag. / ファイル分析フラグ。</summary>
        public bool FilesAnalyzed { get; init; }

        /// <summary>Concluded license. / 結論付けられたライセンス。</summary>
        public string LicenseConcluded { get; init; } = "NOASSERTION";

        /// <summary>Declared license. / 宣言されたライセンス。</summary>
        public string LicenseDeclared { get; init; } = "NOASSERTION";

        /// <summary>Copyright text. / 著作権テキスト。</summary>
        public string CopyrightText { get; init; } = "NOASSERTION";

        /// <summary>Package checksums. / パッケージチェックサム。</summary>
        public List<SpdxChecksum> Checksums { get; init; } = new();

        /// <summary>Annotations (used for status/diff detail metadata). / アノテーション（ステータス/差分詳細メタデータに使用）。</summary>
        public string Comment { get; init; } = string.Empty;
    }

    /// <summary>
    /// SPDX checksum entry.
    /// SPDX チェックサムエントリ。
    /// </summary>
    public sealed class SpdxChecksum
    {
        /// <summary>Algorithm (e.g. "SHA256"). / アルゴリズム。</summary>
        public string Algorithm { get; init; } = string.Empty;

        /// <summary>Checksum value. / チェックサム値。</summary>
        public string ChecksumValue { get; init; } = string.Empty;
    }

    /// <summary>
    /// Supported SBOM output formats.
    /// サポートする SBOM 出力形式。
    /// </summary>
    public enum SbomFormat
    {
        /// <summary>CycloneDX 1.5 JSON format. / CycloneDX 1.5 JSON 形式。</summary>
        CycloneDX,

        /// <summary>SPDX 2.3 JSON format. / SPDX 2.3 JSON 形式。</summary>
        SPDX
    }
}
