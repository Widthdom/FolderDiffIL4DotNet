  // IL syntax highlighting for diff table cells.
  // Applies lightweight regex-based highlighting to IL disassembly output in diff views.
  // IL 逆アセンブリ出力の差分ビューに軽量な正規表現ベースのシンタックスハイライトを適用。

  // Regex patterns for IL syntax elements (compiled once).
  // IL 構文要素の正規表現パターン（1回だけコンパイル）。
  var __ilPatterns__ = [
    // String literals (double-quoted) / 文字列リテラル（二重引用符）
    { re: /(&quot;(?:[^&]|&(?!quot;))*&quot;)/g, cls: 'hl-string' },
    // Single-line comments / 単行コメント
    { re: /(\/\/.*)/g, cls: 'hl-comment' },
    // IL labels (e.g. IL_0000:) / IL ラベル
    { re: /\b(IL_[0-9a-fA-F]{4}:?)\b/g, cls: 'hl-label' },
    // Directives (e.g. .method, .class, .assembly) / ディレクティブ
    { re: /(\.[a-z][a-z0-9]*)\b/g, cls: 'hl-directive' },
    // Builtin types / 組み込み型
    { re: /\b(void|bool|char|int8|int16|int32|int64|uint8|uint16|uint32|uint64|float32|float64|string|object|native int|native uint|typedref)\b/g, cls: 'hl-type' },
    // Keywords / キーワード
    { re: /\b(class|valuetype|interface|enum|extends|implements|public|private|family|assembly|static|virtual|abstract|sealed|specialname|rtspecialname|hidebysig|newslot|final|instance|cil managed|cil|managed|native|forwardref|pinvokeimpl)\b/g, cls: 'hl-keyword' }
  ];

  // Apply IL syntax highlighting to a single cell's innerHTML.
  // 単一セルの innerHTML に IL シンタックスハイライトを適用。
  function highlightILCell(td) {
    var html = td.innerHTML;
    // Skip if already highlighted or empty / ハイライト済みまたは空の場合スキップ
    if (!html || html.indexOf('hl-') !== -1) return;
    // Preserve the leading +/- prefix character / 先頭の +/- プレフィックス文字を保持
    var prefix = '';
    if (html.charAt(0) === '+' || html.charAt(0) === '-' || html.charAt(0) === ' ') {
      prefix = html.charAt(0);
      html = html.substring(1);
    }
    // Apply patterns in order (later patterns don't match inside earlier spans)
    // パターンを順に適用（後のパターンは先の span 内部にマッチしない）
    for (var i = 0; i < __ilPatterns__.length; i++) {
      var p = __ilPatterns__[i];
      html = html.replace(p.re, function(match) {
        // Don't highlight inside already-highlighted spans / 既にハイライト済みのspan内はハイライトしない
        return '<span class="' + p.cls + '">' + match + '</span>';
      });
    }
    td.innerHTML = prefix + html;
  }

  // Apply IL syntax highlighting to all diff cells in a container.
  // Only applies to diffs with data-diff="ILMismatch" on the parent file row.
  // コンテナ内の全差分セルに IL シンタックスハイライトを適用。
  // 親ファイル行の data-diff="ILMismatch" の差分にのみ適用。
  function highlightILDiff(container) {
    if (!container) return;
    // Find the parent file row to check if this is an IL diff
    // 親ファイル行を見つけて IL 差分かどうか確認
    var detailsEl = container.closest ? container.closest('details') : null;
    var diffRow = detailsEl ? detailsEl.closest('tr.diff-row') : null;
    // Walk previous siblings to find the file data row with data-diff attribute
    // data-diff 属性を持つファイルデータ行を見つけるため前の兄弟要素を辿る
    var fileRow = diffRow ? diffRow.previousElementSibling : null;
    while (fileRow && !fileRow.getAttribute('data-diff')) {
      fileRow = fileRow.previousElementSibling;
    }
    var diffType = fileRow ? fileRow.getAttribute('data-diff') : null;
    if (diffType !== 'ILMismatch' && diffType !== 'ILMatch') return;
    // Highlight all content cells / 全コンテンツセルをハイライト
    container.querySelectorAll('td.diff-del-td, td.diff-add-td, td.diff-ctx-td').forEach(highlightILCell);
  }

  // Apply highlighting to all currently-rendered diff tables on the page.
  // ページ上の現在レンダリング済みの全差分テーブルにハイライトを適用。
  function highlightAllILDiffs() {
    document.querySelectorAll('table.diff-table').forEach(function(tbl) {
      highlightILDiff(tbl);
    });
  }
