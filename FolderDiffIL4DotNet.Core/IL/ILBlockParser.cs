using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FolderDiffIL4DotNet.Core.IL
{
    // Linked nodes keep deep class paths compact until the public display value is requested.
    // Each digest uses the complete class header while Signature preserves the first-line display format.
    // 連結nodeにより、公開表示値が要求されるまで深いclass pathをコンパクトに保持します。
    // 各digestには完全なclass headerを使い、Signatureは従来の先頭行表示形式を維持します。
    internal sealed class ILContainerPath
    {
        private readonly byte[] _comparisonDigest;
        private string? _comparisonKey;
        private string? _value;

        internal ILContainerPath(
            ILContainerPath? parent,
            string signature,
            string comparisonIdentity)
        {
            Parent = parent;
            Signature = signature;
            _comparisonDigest = CreateComparisonDigest(parent, comparisonIdentity);
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

        private static byte[] CreateComparisonDigest(ILContainerPath? parent, string comparisonIdentity)
        {
            byte[] signatureBytes = Encoding.UTF8.GetBytes(comparisonIdentity);
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
            int headerParenthesisDepth = 0;
            bool sawOpeningBrace = false;
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
                    headerParenthesisDepth = 0;
                    sawOpeningBrace = false;
                }

                currentBlock.Add(line);

                if (inBlock)
                {
                    // Only a top-level header brace starts the body. Header constructs such as
                    // marshal({ ... }) have braces nested inside parentheses and are not delimiters.
                    // headerの最上位波括弧だけを本体開始とします。marshal({ ... })のように
                    // 丸括弧内へnestedしたheader構文の波括弧は区切りではありません。
                    UpdateBlockBraceState(
                        trimmed,
                        ref sawOpeningBrace,
                        ref braceDepth,
                        ref headerParenthesisDepth);

                    if (sawOpeningBrace && braceDepth <= 0)
                    {
                        // Block ended — save it and start new inter-block collection
                        // ブロック終了 — 保存して新しいブロック間コレクションを開始
                        blocks.Add(currentBlock);
                        currentBlock = new List<string>();
                        inBlock = false;
                        braceDepth = 0;
                        headerParenthesisDepth = 0;
                        sawOpeningBrace = false;
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
        /// An ordinary class is represented by a shell block without its directly nested reorderable members;
        /// each member is emitted separately with the containing class signature path. Interface and imported-type
        /// members remain in declaration order in their class shell. Comparison identity includes every line of
        /// each class header, including multiline type names and base declarations.
        /// class/member階層を保持した比較用ブロックへILを解析します。
        /// 通常classは直接包含する並び替え可能memberを除いたshell blockとして表し、各memberは包含class
        /// signature path付きで個別に出力します。interfaceとimport typeのmemberは宣言順のままclass shellへ
        /// 保持します。比較identityには、複数行の型名やbase宣言を含む各class headerの全行を使用します。
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
            var frames = new Stack<ComparableBlockFrame>();
            frames.Push(ComparableBlockFrame.CreateClass(parentPath, canEndBeforeOpeningBrace: false));

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
                    frame.HeaderParenthesisDepth == 0 &&
                    (trimmed.StartsWith("}", StringComparison.Ordinal) || IsReorderableClassMemberStart(trimmed)))
                {
                    CompleteComparableBlock(frames.Pop(), result);
                    continue;
                }

                // Depth 1 identifies direct members; deeper directives belong to the current member body.
                // depth 1だけを直接memberとして扱い、それより深いdirectiveは現在のmember本体に残します。
                if (frame.IsClass &&
                    frame.BraceDepth == 1 &&
                    IsReorderableClassMemberStart(trimmed) &&
                    (!frame.PreserveDirectMemberOrder || trimmed.StartsWith(".class ", StringComparison.Ordinal)))
                {
                    ComparableBlockFrame childFrame;
                    if (trimmed.StartsWith(".class ", StringComparison.Ordinal))
                    {
                        childFrame = ComparableBlockFrame.CreateClass(
                            frame.ClassPath,
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
            private int _braceDepth;
            private int _headerParenthesisDepth;
            private bool _sawOpeningBrace;
            private List<string>? _classHeaderIdentityLines;
            private ILContainerPath? _classPath;

            private ComparableBlockFrame(
                bool isClass,
                bool canEndBeforeOpeningBrace,
                ILContainerPath? containerPath,
                ILContainerPath? classPath)
            {
                IsClass = isClass;
                CanEndBeforeOpeningBrace = canEndBeforeOpeningBrace;
                ContainerPath = containerPath;
                _classPath = classPath;
                _classHeaderIdentityLines = isClass ? new List<string>() : null;
                Lines = new List<string>();
            }

            internal bool IsClass { get; }

            internal bool CanEndBeforeOpeningBrace { get; }

            internal ILContainerPath? ContainerPath { get; }

            internal ILContainerPath ClassPath => _classPath ??
                throw new InvalidOperationException("The class path is unavailable before its header is complete.");

            internal List<string> Lines { get; }

            internal int BraceDepth => _braceDepth;

            internal bool SawOpeningBrace => _sawOpeningBrace;

            internal int HeaderParenthesisDepth => _headerParenthesisDepth;

            internal bool PreserveDirectMemberOrder { get; private set; }

            internal static ComparableBlockFrame CreateClass(
                ILContainerPath? containerPath,
                bool canEndBeforeOpeningBrace)
            {
                return new ComparableBlockFrame(
                    isClass: true,
                    canEndBeforeOpeningBrace,
                    containerPath,
                    classPath: null);
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
                string trimmed = line.TrimStart();
                int bodyOpeningBraceIndex = UpdateBlockBraceState(
                    trimmed,
                    ref _sawOpeningBrace,
                    ref _braceDepth,
                    ref _headerParenthesisDepth);

                if (IsClass && _classPath == null)
                {
                    string headerFragment = bodyOpeningBraceIndex >= 0
                        ? trimmed.Substring(0, bodyOpeningBraceIndex).Trim()
                        : trimmed.Trim();
                    if (headerFragment.Length > 0)
                    {
                        _classHeaderIdentityLines!.Add(headerFragment);
                    }

                    if (bodyOpeningBraceIndex >= 0)
                    {
                        string displaySignature = ExtractBlockSignature(Lines).Trim();
                        PreserveDirectMemberOrder =
                            ContainsClassHeaderToken(_classHeaderIdentityLines!, "interface") ||
                            ContainsClassHeaderToken(_classHeaderIdentityLines!, "import");
                        string comparisonIdentity = string.Join("\n", _classHeaderIdentityLines!);
                        _classPath = new ILContainerPath(
                            ContainerPath,
                            displaySignature,
                            comparisonIdentity);
                        _classHeaderIdentityLines = null;
                    }
                }
            }
        }

        private static bool ContainsClassHeaderToken(
            IReadOnlyList<string> headerLines,
            string expectedToken)
        {
            foreach (string line in headerLines)
            {
                int index = 0;
                while (index < line.Length)
                {
                    while (index < line.Length && char.IsWhiteSpace(line[index]))
                    {
                        index++;
                    }

                    if (index >= line.Length ||
                        (line[index] == '/' && index + 1 < line.Length && line[index + 1] == '/'))
                    {
                        break;
                    }

                    if (line[index] == '\'' || line[index] == '"')
                    {
                        char quote = line[index++];
                        while (index < line.Length)
                        {
                            if (line[index] == quote && !IsEscaped(line, index))
                            {
                                index++;
                                break;
                            }
                            index++;
                        }
                        continue;
                    }

                    int tokenStart = index;
                    while (index < line.Length && !char.IsWhiteSpace(line[index]))
                    {
                        index++;
                    }

                    int tokenLength = index - tokenStart;
                    if (tokenLength == expectedToken.Length &&
                        string.CompareOrdinal(line, tokenStart, expectedToken, 0, tokenLength) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
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
        /// Updates body-brace state while ignoring braces nested in a declaration header's parentheses.
        /// declaration headerの丸括弧内にnestedした波括弧を無視しながら、本体の波括弧状態を更新します。
        /// </summary>
        private static int UpdateBlockBraceState(
            string line,
            ref bool sawOpeningBrace,
            ref int braceDepth,
            ref int headerParenthesisDepth)
        {
            if (sawOpeningBrace)
            {
                braceDepth += CountBraces(line, 0);
                return -1;
            }

            int bodyOpeningBraceIndex = FindBodyOpeningBrace(line, ref headerParenthesisDepth);
            if (bodyOpeningBraceIndex < 0)
            {
                return -1;
            }

            sawOpeningBrace = true;
            braceDepth += CountBraces(line, bodyOpeningBraceIndex);
            return bodyOpeningBraceIndex;
        }

        private static int FindBodyOpeningBrace(string line, ref int parenthesisDepth)
        {
            char quote = '\0';

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quote == '\0' && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    break;
                }

                if (c == '\'' || c == '"')
                {
                    if (quote == '\0')
                    {
                        quote = c;
                    }
                    else if (quote == c && !IsEscaped(line, i))
                    {
                        quote = '\0';
                    }
                    continue;
                }

                if (quote != '\0')
                {
                    continue;
                }

                if (c == '(')
                {
                    parenthesisDepth++;
                }
                else if (c == ')' && parenthesisDepth > 0)
                {
                    parenthesisDepth--;
                }
                else if (c == '{' && parenthesisDepth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Counts the net brace change in a line (opening minus closing),
        /// skipping braces inside string literals ("..."), single-quoted identifiers, and after line comments (//).
        /// This prevents false block boundary detection from braces in IL string operands
        /// (e.g. <c>ldstr "JSON: {\"key\": \"value\"}"</c>) or comments.
        /// 行中の波括弧の差分（開き - 閉じ）を数えます。
        /// 文字列リテラル（"..."）内、single quote identifier内、およびラインコメント（//）以降の波括弧はスキップします。
        /// IL 文字列オペランド（例: <c>ldstr "JSON: {\"key\": \"value\"}"</c>）や
        /// コメント内の波括弧によるブロック境界の誤検知を防止します。
        /// </summary>
        private static int CountBraces(string line)
        {
            return CountBraces(line, 0);
        }

        private static int CountBraces(string line, int startIndex)
        {
            int count = 0;
            char quote = '\0';

            for (int i = startIndex; i < line.Length; i++)
            {
                char c = line[i];

                // Check for line comment start (outside of quoted values)
                // quoteされた値の外でのラインコメント開始をチェック
                if (quote == '\0' && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    // Rest of line is a comment — no more braces to count
                    // 行の残りはコメント — これ以上波括弧をカウントしない
                    break;
                }

                // Track double-quoted strings and single-quoted IL identifiers.
                // double quote文字列とsingle quote IL identifierを追跡します。
                if (c == '\'' || c == '"')
                {
                    if (quote == '\0')
                    {
                        quote = c;
                    }
                    else if (quote == c && !IsEscaped(line, i))
                    {
                        quote = '\0';
                    }
                    continue;
                }

                if (quote == '\0')
                {
                    if (c == '{') count++;
                    else if (c == '}') count--;
                }
            }
            return count;
        }

        private static bool IsEscaped(string line, int characterIndex)
        {
            int backslashCount = 0;
            for (int i = characterIndex - 1; i >= 0 && line[i] == '\\'; i--)
            {
                backslashCount++;
            }

            return backslashCount % 2 != 0;
        }
    }
}
