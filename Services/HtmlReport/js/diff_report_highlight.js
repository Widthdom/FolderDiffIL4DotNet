  /** @type {Array<{re: RegExp, cls: string}>} Regex patterns for IL syntax highlighting (compiled once) */
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

  /**
   * Apply IL syntax highlighting to a single table cell's innerHTML.
   * @param {HTMLTableCellElement} td
   */
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
      // Apply pattern only to text segments, skipping HTML tags
      // HTMLタグをスキップし、テキスト部分にのみパターンを適用
      html = html.replace(/(<[^>]+>)|([^<]+)/g, function(m, tag, text) {
        if (tag) return tag;
        return text.replace(p.re, function(match) {
          return '<span class="' + p.cls + '">' + match + '</span>';
        });
      });
    }
    td.innerHTML = prefix + html;
  }

  /**
   * Apply IL syntax highlighting to all diff cells in a container.
   * Only applies to diffs with data-diff="ILMismatch" or "ILMatch" on the parent file row.
   * @param {HTMLElement|null} container
   */
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
    container.querySelectorAll('td.diff-del-td, td.diff-add-td, td.diff-ctx-td, td.sbs-old, td.sbs-new, td.sbs-ctx').forEach(highlightILCell);
  }

  /** Apply IL syntax highlighting to all currently-rendered diff tables on the page. */
  function highlightAllILDiffs() {
    document.querySelectorAll('table.diff-table').forEach(function(tbl) {
      highlightILDiff(tbl);
    });
  }

  /* Export functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { highlightILCell: highlightILCell, __ilPatterns__: __ilPatterns__ }; }
