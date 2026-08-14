using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FolderDiffIL4DotNet.Core.IL
{
    // Linked nodes keep deep class paths compact until the public string value is requested.
    // 連結nodeにより、公開文字列値が要求されるまで深いclass pathをコンパクトに保持します。
    internal sealed class ILContainerPath
    {
        private readonly byte[] _comparisonDigest;
        private string? _comparisonKey;
        private string? _value;

        internal ILContainerPath(ILContainerPath? parent, string signature)
        {
            Parent = parent;
            Signature = signature;
            _comparisonDigest = CreateComparisonDigest(parent, signature);
        }

        internal ILContainerPath? Parent { get; }

        internal string Signature { get; }

        internal string GetComparisonKey()
        {
            return _comparisonKey ??= Convert.ToHexString(_comparisonDigest);
        }

        internal string GetValue()
        {
            if (_value != null)
            {
                return _value;
            }

            var signatures = new Stack<string>();
            for (ILContainerPath? current = this; current != null; current = current.Parent)
            {
                signatures.Push(current.Signature);
            }

            _value = string.Join("\n", signatures);
            return _value;
        }

        private static byte[] CreateComparisonDigest(ILContainerPath? parent, string signature)
        {
            byte[] signatureBytes = Encoding.UTF8.GetBytes(signature);
            int parentDigestLength = parent?._comparisonDigest.Length ?? 0;
            var payload = new byte[1 + parentDigestLength + signatureBytes.Length];
            payload[0] = parent == null ? (byte)0 : (byte)1;
            if (parent != null)
            {
                parent._comparisonDigest.CopyTo(payload, 1);
            }
            signatureBytes.CopyTo(payload, 1 + parentDigestLength);
            return SHA256.HashData(payload);
        }
    }

    /// <summary>
    /// A logical IL block paired with the containing class path used for comparison.
    /// 比較に使用する包含class pathを伴う論理ILブロックです。
    /// </summary>
    public sealed class ILComparableBlock
    {
        private readonly ILContainerPath? _containerPath;

        internal ILComparableBlock(ILContainerPath? containerPath, List<string> lines)
        {
            _containerPath = containerPath;
            // Parser-owned lists are not mutated after this ownership handoff.
            // parser所有listは、この所有権移譲後には変更されません。
            Lines = lines.AsReadOnly();
        }

        /// <summary>
        /// Containing class signature path, or empty for top-level blocks.
        /// 包含class signature path。トップレベルブロックでは空です。
        /// </summary>
        public string ContainerPath => _containerPath?.GetValue() ?? string.Empty;

        /// <summary>
        /// Fixed-size hierarchy identity used internally for comparison without materializing every full path.
        /// 全pathを実体化せず比較するために内部で使用する固定長の階層identityです。
        /// </summary>
        internal string ContainerComparisonKey => _containerPath?.GetComparisonKey() ?? string.Empty;

        /// <summary>
        /// IL lines belonging to this block.
        /// このブロックに属するIL行です。
        /// </summary>
        public IReadOnlyList<string> Lines { get; }
    }

    /// <summary>
    /// Parses IL disassembly output into top-level blocks (methods, classes, properties, etc.)
    /// for order-independent comparison. IL lines that fall outside any block are grouped as
    /// a single "preamble" block preserving their original order.
    /// IL 逆アセンブリ出力をトップレベルブロック（メソッド、クラス、プロパティ等）に分割し、
    /// 順序非依存の比較を可能にします。ブロック外の行はプリアンブルとしてまとめられます。
    /// </summary>
    public static class ILBlockParser
    {
        // IL directives that start a nestable block (closed by a matching '}')
        // ネスト可能なブロックを開始する IL ディレクティブ（対応する '}' で閉じる）
        private static readonly string[] s_blockDirectives = new[]
        {
            ".method ",
            ".class ",
            ".property ",
            ".event ",
            ".field ",
        };

        /// <summary>
        /// Splits filtered IL lines into logical blocks. Each block is a list of lines
        /// representing a top-level IL construct (method, class, etc.).
        /// Lines before the first block or between blocks form the "preamble" block (index 0).
        /// フィルタ済み IL 行を論理ブロックに分割します。各ブロックはトップレベル IL 構造
        /// （メソッド、クラス等）を表す行のリスト。最初のブロック前やブロック間の行は
        /// プリアンブルブロック（インデックス 0）にまとめます。
        /// </summary>
        /// <param name="lines">Filtered IL lines (MVID / configured strings already excluded). / フィルタ済み IL 行（MVID / 設定文字列除外済み）。</param>
        /// <returns>List of blocks, where each block is a list of IL lines. / ブロックのリスト。各ブロックは IL 行のリスト。</returns>
        public static List<List<string>> ParseBlocks(IReadOnlyList<string> lines)
        {
            var blocks = new List<List<string>>();
            var currentBlock = new List<string>(); // preamble / プリアンブル
            int braceDepth = 0;
            bool inBlock = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (!inBlock && IsBlockStart(trimmed))
                {
                    // Save preamble or previous inter-block lines
                    // プリアンブルまたはブロック間の行を保存
                    if (currentBlock.Count > 0)
                    {
                        blocks.Add(currentBlock);
                        currentBlock = new List<string>();
                    }
                    inBlock = true;
                    braceDepth = 0;
                }

                currentBlock.Add(line);

                if (inBlock)
                {
                    // Count braces to detect block end
                    // 波括弧を数えてブロック終了を検出
                    braceDepth += CountBraces(trimmed);

                    if (braceDepth <= 0 && trimmed.StartsWith("}", StringComparison.Ordinal))
                    {
                        // Block ended — save it and start new inter-block collection
                        // ブロック終了 — 保存して新しいブロック間コレクションを開始
                        blocks.Add(currentBlock);
                        currentBlock = new List<string>();
                        inBlock = false;
                        braceDepth = 0;
                    }
                }
            }

            // Remaining lines (trailing preamble or unclosed block)
            // 残りの行（末尾のプリアンブルまたは閉じられていないブロック）
            if (currentBlock.Count > 0)
            {
                blocks.Add(currentBlock);
            }

            return blocks;
        }

        /// <summary>
        /// Parses IL into comparison blocks while preserving class/member hierarchy.
        /// A class is represented by a shell block without its directly nested reorderable members;
        /// each member is emitted separately with the containing class signature path.
        /// class/member階層を保持した比較用ブロックへILを解析します。
        /// classは直接包含する並び替え可能memberを除いたshell blockとして表し、
        /// 各memberは包含class signature path付きで個別に出力します。
        /// </summary>
        public static List<ILComparableBlock> ParseComparableBlocks(IReadOnlyList<string> lines)
        {
            var comparableBlocks = new List<ILComparableBlock>();
            foreach (var block in ParseBlocks(lines))
            {
                string signature = ExtractBlockSignature(block);
                if (signature.StartsWith(".class ", StringComparison.Ordinal))
                {
                    AddClassAndMemberBlocks(block, null, comparableBlocks);
                }
                else
                {
                    comparableBlocks.Add(new ILComparableBlock(null, block));
                }
            }

            return comparableBlocks;
        }

        private static void AddClassAndMemberBlocks(
            IReadOnlyList<string> classLines,
            ILContainerPath? parentPath,
            List<ILComparableBlock> result)
        {
            string classSignature = ExtractBlockSignature(classLines).Trim();
            var classPath = new ILContainerPath(parentPath, classSignature);
            var frames = new Stack<ComparableBlockFrame>();
            frames.Push(ComparableBlockFrame.CreateClass(parentPath, classPath, canEndBeforeOpeningBrace: false));

            // Each line is assigned exactly once to a class shell or direct member. Nested classes suspend
            // their parent frame on this explicit stack, avoiding subtree rescans, copies, and recursion.
            // 各行をclass shellまたは直接memberのどちらかへ一度だけ割り当てます。nested class解析中は
            // 親frameを明示stackで保留し、subtreeの再走査・コピー・再帰を回避します。
            int lineIndex = 0;
            while (lineIndex < classLines.Count && frames.Count > 0)
            {
                ComparableBlockFrame frame = frames.Peek();
                string line = classLines[lineIndex];
                string trimmed = line.TrimStart();

                // Multiline signatures without an opening brace end immediately before the next sibling
                // or the owning class close, matching the previous malformed-input behavior.
                // 開始波括弧のない複数行signatureは、次のsiblingまたは所有classの閉じ波括弧直前で終了し、
                // 従来の不正入力に対する挙動を維持します。
                if (frame.CanEndBeforeOpeningBrace &&
                    !frame.SawOpeningBrace &&
                    (trimmed.StartsWith("}", StringComparison.Ordinal) || IsReorderableClassMemberStart(trimmed)))
                {
                    CompleteComparableBlock(frames.Pop(), result);
                    continue;
                }

                // Depth 1 identifies direct members; deeper directives belong to the current member body.
                // depth 1だけを直接memberとして扱い、それより深いdirectiveは現在のmember本体に残します。
                if (frame.IsClass && frame.BraceDepth == 1 && IsReorderableClassMemberStart(trimmed))
                {
                    ComparableBlockFrame childFrame;
                    if (trimmed.StartsWith(".class ", StringComparison.Ordinal))
                    {
                        var nestedClassPath = new ILContainerPath(frame.ClassPath, trimmed.Trim());
                        childFrame = ComparableBlockFrame.CreateClass(
                            frame.ClassPath,
                            nestedClassPath,
                            canEndBeforeOpeningBrace: true);
                    }
                    else
                    {
                        childFrame = ComparableBlockFrame.CreateMember(frame.ClassPath);
                    }

                    childFrame.AddLine(line);
                    frames.Push(childFrame);
                    lineIndex++;
                    continue;
                }

                frame.AddLine(line);
                lineIndex++;

                // Once a body starts, its matching brace completes the frame, including nested scopes.
                // 本体開始後は、nested scopeを含めて対応する閉じ波括弧でframeを完了します。
                if (frame.CanEndBeforeOpeningBrace && frame.SawOpeningBrace && frame.BraceDepth <= 0)
                {
                    CompleteComparableBlock(frames.Pop(), result);
                }
            }

            // Preserve the previous behavior for an unclosed final member or class by consuming to EOF.
            // 閉じられていない末尾memberまたはclassはEOFまで取り込む従来挙動を維持します。
            while (frames.Count > 0)
            {
                CompleteComparableBlock(frames.Pop(), result);
            }
        }

        private static void CompleteComparableBlock(
            ComparableBlockFrame frame,
            List<ILComparableBlock> result)
        {
            result.Add(new ILComparableBlock(frame.ContainerPath, frame.Lines));
        }

        private sealed class ComparableBlockFrame
        {
            private ComparableBlockFrame(
                bool isClass,
                bool canEndBeforeOpeningBrace,
                ILContainerPath? containerPath,
                ILContainerPath classPath)
            {
                IsClass = isClass;
                CanEndBeforeOpeningBrace = canEndBeforeOpeningBrace;
                ContainerPath = containerPath;
                ClassPath = classPath;
                Lines = new List<string>();
            }

            internal bool IsClass { get; }

            internal bool CanEndBeforeOpeningBrace { get; }

            internal ILContainerPath? ContainerPath { get; }

            internal ILContainerPath ClassPath { get; }

            internal List<string> Lines { get; }

            internal int BraceDepth { get; private set; }

            internal bool SawOpeningBrace { get; private set; }

            internal static ComparableBlockFrame CreateClass(
                ILContainerPath? containerPath,
                ILContainerPath classPath,
                bool canEndBeforeOpeningBrace)
            {
                return new ComparableBlockFrame(
                    isClass: true,
                    canEndBeforeOpeningBrace,
                    containerPath,
                    classPath);
            }

            internal static ComparableBlockFrame CreateMember(ILContainerPath containerPath)
            {
                return new ComparableBlockFrame(
                    isClass: false,
                    canEndBeforeOpeningBrace: true,
                    containerPath,
                    containerPath);
            }

            internal void AddLine(string line)
            {
                Lines.Add(line);
                int braceChange = CountBraces(line.TrimStart());
                if (braceChange > 0)
                {
                    SawOpeningBrace = true;
                }

                BraceDepth += braceChange;
            }
        }

        private static bool IsReorderableClassMemberStart(string trimmedLine)
        {
            return trimmedLine.StartsWith(".method ", StringComparison.Ordinal) ||
                   trimmedLine.StartsWith(".class ", StringComparison.Ordinal) ||
                   trimmedLine.StartsWith(".property ", StringComparison.Ordinal) ||
                   trimmedLine.StartsWith(".event ", StringComparison.Ordinal);
        }

        /// <summary>
        /// Extracts the signature (first directive line) from a parsed block.
        /// Returns an empty string for preamble or inter-block lines that have no directive.
        /// パース済みブロックからシグネチャ（最初のディレクティブ行）を抽出します。
        /// ディレクティブを持たないプリアンブルやブロック間の行の場合は空文字列を返します。
        /// </summary>
        /// <param name="blockLines">A single block as returned by <see cref="ParseBlocks"/>. / <see cref="ParseBlocks"/> が返した単一ブロック。</param>
        /// <returns>The directive line (trimmed) if found, otherwise an empty string. / ディレクティブ行（トリム済み）が見つかれば返し、なければ空文字列。</returns>
        public static string ExtractBlockSignature(IReadOnlyList<string> blockLines)
        {
            if (blockLines == null || blockLines.Count == 0)
            {
                return string.Empty;
            }

            // The first line of a directive block is the directive itself.
            // For preamble blocks, no line starts with a block directive.
            // ディレクティブブロックの最初の行がディレクティブそのもの。
            // プリアンブルブロックではブロックディレクティブで始まる行はない。
            string firstTrimmed = blockLines[0].TrimStart();
            if (IsBlockStart(firstTrimmed))
            {
                return firstTrimmed;
            }
            return string.Empty;
        }

        /// <summary>
        /// Determines whether a trimmed line starts a new top-level IL block.
        /// トリム済みの行が新しいトップレベル IL ブロックの開始かどうかを判定します。
        /// </summary>
        private static bool IsBlockStart(string trimmedLine)
        {
            for (int i = 0; i < s_blockDirectives.Length; i++)
            {
                if (trimmedLine.StartsWith(s_blockDirectives[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Counts the net brace change in a line (opening minus closing),
        /// skipping braces inside string literals ("...") and after line comments (//).
        /// This prevents false block boundary detection from braces in IL string operands
        /// (e.g. <c>ldstr "JSON: {\"key\": \"value\"}"</c>) or comments.
        /// 行中の波括弧の差分（開き - 閉じ）を数えます。
        /// 文字列リテラル（"..."）内およびラインコメント（//）以降の波括弧はスキップします。
        /// IL 文字列オペランド（例: <c>ldstr "JSON: {\"key\": \"value\"}"</c>）や
        /// コメント内の波括弧によるブロック境界の誤検知を防止します。
        /// </summary>
        private static int CountBraces(string line)
        {
            int count = 0;
            bool inString = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Check for line comment start (outside of string literals)
                // 文字列リテラル外でのラインコメント開始をチェック
                if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    // Rest of line is a comment — no more braces to count
                    // 行の残りはコメント — これ以上波括弧をカウントしない
                    break;
                }

                // Track string literal boundaries (handle escaped quotes)
                // 文字列リテラルの境界を追跡（エスケープされた引用符を処理）
                if (c == '"')
                {
                    if (inString)
                    {
                        // Check if this quote is escaped by a backslash
                        // この引用符がバックスラッシュでエスケープされているかチェック
                        int backslashCount = 0;
                        for (int j = i - 1; j >= 0 && line[j] == '\\'; j--)
                            backslashCount++;
                        if (backslashCount % 2 == 0)
                            inString = false; // Unescaped quote — end of string / エスケープされていない引用符 — 文字列終了
                    }
                    else
                    {
                        inString = true;
                    }
                    continue;
                }

                if (!inString)
                {
                    if (c == '{') count++;
                    else if (c == '}') count--;
                }
            }
            return count;
        }
    }
}
