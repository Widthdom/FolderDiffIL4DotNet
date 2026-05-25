  /**
   * Export report as PDF via browser print dialog.
   * Injects fixed-position header/footer elements and triggers window.print().
   */
  function downloadAsPdf() {
    // Force-decode all lazy sections / 全 lazy セクションを強制デコード
    forceDecodeLazySections();

    // Extract report metadata from header DOM / ヘッダー DOM からレポートメタデータを抽出
    var headerCards = document.querySelectorAll('.header-card');
    var appVersion = '';
    var computerName = '';
    headerCards.forEach(function(card) {
      var label = (card.querySelector('.header-card-label') || {}).textContent || '';
      var value = (card.querySelector('.header-card-value') || {}).textContent || '';
      if (label.indexOf('App Version') >= 0) appVersion = value;
      if (label.indexOf('Computer') >= 0) computerName = value;
    });
    var pathEls = document.querySelectorAll('.header-path');
    var oldPath = '';
    var newPath = '';
    pathEls.forEach(function(el) {
      var label = (el.querySelector('.header-path-label') || {}).textContent || '';
      var value = (el.querySelector('.header-path-value') || {}).textContent || '';
      if (label === 'Old Folder') oldPath = value;
      if (label === 'New Folder') newPath = value;
    });

    // Get reviewed date from banner / バナーからレビュー日を取得
    var bannerSpan = document.querySelector('.reviewed-banner > span');
    var reviewedDate = bannerSpan ? bannerSpan.textContent : '';

    // Create header element / ヘッダー要素を作成
    var header = document.createElement('div');
    header.className = 'pdf-print-header';
    header.innerHTML = '<span>Folder Diff Report \u2014 ' + esc(appVersion) + '</span>'
      + '<span>' + esc(oldPath) + ' \u2192 ' + esc(newPath) + '</span>';

    // Create footer element / フッター要素を作成
    var footer = document.createElement('div');
    footer.className = 'pdf-print-footer';
    footer.innerHTML = '<span>' + esc(reviewedDate) + ' \u2014 ' + esc(computerName) + '</span>'
      + '<span>Page numbers: enable in browser print settings</span>';

    // Inject and activate PDF print mode / PDF 印刷モードを注入・有効化
    document.body.appendChild(header);
    document.body.appendChild(footer);
    document.body.classList.add('pdf-print-mode');

    // Clean up after print dialog closes / 印刷ダイアログ終了後にクリーンアップ
    function cleanup() {
      document.body.classList.remove('pdf-print-mode');
      if (header.parentNode) header.parentNode.removeChild(header);
      if (footer.parentNode) footer.parentNode.removeChild(footer);
      window.removeEventListener('afterprint', cleanup);
    }
    window.addEventListener('afterprint', cleanup);
    window.print();
  }

  /**
   * Generate and download an Excel-compatible HTML table file from the current report data.
   * Uses requestAnimationFrame chunking to avoid UI freezes on large reports.
   */
  function downloadExcelCompatibleHtml() {
    forceDecodeLazySections();
    var allRows = Array.prototype.slice.call(document.querySelectorAll('tbody > tr[data-section]'));
    if (allRows.length > 500) {
      downloadExcelChunked(allRows);
      return;
    }
    downloadExcelImmediate();
  }

  /**
   * Chunked Excel export: builds Excel rows in batches of 200 via requestAnimationFrame.
   * Shows progress in save-status while processing.
   * @param {HTMLTableRowElement[]} allRows - All data rows from the DOM
   */
  function downloadExcelChunked(allRows) {
    var status = document.getElementById('save-status');
    var builtRows = {};
    var idx = 0;
    var CHUNK = 200;
    allRows.forEach(function(tr) {
      var sec = tr.getAttribute('data-section');
      if (!builtRows[sec]) builtRows[sec] = [];
    });
    function processChunk() {
      var end = Math.min(idx + CHUNK, allRows.length);
      for (; idx < end; idx++) {
        var tr = allRows[idx];
        var sec = tr.getAttribute('data-section');
        if (!builtRows[sec]) builtRows[sec] = [];
        builtRows[sec].push(buildExcelRow(tr));
      }
      if (status) status.textContent = 'Building Excel... ' + Math.round(idx / allRows.length * 100) + '%';
      if (idx < allRows.length) {
        requestAnimationFrame(processChunk);
      } else {
        finalizeExcelDownload(builtRows);
        if (status) { status.textContent = 'Excel export complete.'; }
      }
    }
    if (status) status.textContent = 'Building Excel... 0%';
    requestAnimationFrame(processChunk);
  }

  /**
   * Finalize the Excel download from pre-built row data.
   * @param {Object<string, string[]>} builtRows - Section key to array of HTML row strings
   */
  function finalizeExcelDownload(builtRows) {
    var excelParts = buildExcelFramework(builtRows);
    var blob = new Blob([excelParts], { type: 'application/vnd.ms-excel;charset=utf-8' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'diff_report_' + __reportDate__ + '_reviewed_Excel-compatible.html';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function() { URL.revokeObjectURL(a.href); }, 1000);
  }

  /**
   * Immediate (non-chunked) Excel export for small reports.
   * Builds rows synchronously and delegates to buildExcelFramework for final assembly.
   * 小規模レポート用の即時（非チャンク）Excelエクスポート。
   * 行を同期的に構築し、最終組み立ては buildExcelFramework に委譲。
   */
  function downloadExcelImmediate() {
    forceDecodeLazySections();
    var allRows = document.querySelectorAll('tbody > tr[data-section]');
    var builtRows = {};
    allRows.forEach(function(tr) {
      var sec = tr.getAttribute('data-section');
      if (!builtRows[sec]) builtRows[sec] = [];
      builtRows[sec].push(buildExcelRow(tr));
    });
    var out = buildExcelFramework(builtRows);
    var slug = 'diff_report_' + __reportDate__;
    var blob = new Blob([out], { type: 'application/vnd.ms-excel;charset=utf-8' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = slug + '_reviewed_Excel-compatible.html';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function() { URL.revokeObjectURL(a.href); }, 1000);
  }

  /**
   * Build an Excel-compatible HTML table row from a DOM table row element.
   * For Ignored section rows, Estimated Change / Disassembler / .NET SDK columns are omitted.
   * @param {HTMLTableRowElement} tr - A data row with data-section attribute
   * @returns {string} HTML string for the Excel row, or empty string if insufficient cells
   */
  function buildExcelRow(tr) {
    var cells = tr.querySelectorAll('td');
    var sec = tr.getAttribute('data-section');
    if (sec === 'checklist') {
      if (cells.length < 3) return '';
      var checklistCb = cells[0].querySelector('input[type="checkbox"]');
      var checklistChecked = checklistCb && checklistCb.checked ? '\u2713' : '';
      var checklistItemEl = cells[1].querySelector('.checklist-item-text');
      var checklistItem = checklistItemEl ? checklistItemEl.textContent.trim() : cells[1].textContent.trim();
      var checklistNotesEl = cells[2].querySelector('textarea, input');
      var checklistNotes = checklistNotesEl ? checklistNotesEl.value : '';

      return '<tr><td></td><td></td><td></td><td></td><td></td><td></td><td></td>'
        + '<td></td>'
        + '<td class="bd">' + esc(checklistChecked) + '</td>'
        + '<td class="bd">' + esc(checklistItem).replace(/\r?\n/g, '<br>') + '</td>'
        + '<td class="bd">' + esc(checklistNotes).replace(/\r?\n/g, '<br>') + '</td>'
        + '<td></td><td></td></tr>';
    }

    if (cells.length < 8) return '';
    // Common columns (0-7): #, Checkbox, Justification, Notes, Status, File Path, Timestamp, Diff Detail/Location
    // 共通列 (0-7): #, チェック, 理由, メモ, Status, パス, タイムスタンプ, 判定根拠/Location
    var no = cells[0].textContent.trim();
    var cb = cells[1].querySelector('input[type="checkbox"]');
    var checked = cb && cb.checked ? '\u2713' : '';
    var reasonInp = cells[2].querySelector('input');
    var reason = reasonInp ? reasonInp.value : '';
    var notesInp = cells[3].querySelector('input');
    var notes = notesInp ? notesInp.value : '';
    var status = cells[4].textContent.trim();
    var pathSpan = cells[5].querySelector('.path-text');
    var path = pathSpan ? pathSpan.textContent.trim() : cells[5].textContent.trim();
    var ts = cells[6].textContent.trim();
    var diff = cells[7].textContent.trim();

    var r = '<tr><td></td><td></td>'
      + '<td class="bd">' + esc(no) + '</td>'
      + '<td class="bd">' + esc(checked) + '</td>'
      + '<td class="bd">' + esc(reason) + '</td>'
      + '<td class="bd">' + esc(notes) + '</td>'
      + '<td class="bd" style="text-align:center">' + esc(status) + '</td>'
      + '<td class="bd">' + esc(path) + '</td>'
      + '<td class="bd" style="text-align:center;max-width:280px;mso-number-format:\'\\@\'">' + esc(ts) + '</td>'
      + '<td class="bd" style="text-align:center">' + esc(diff) + '</td>';

    if (sec === 'ign') {
      // Ignored: no Estimated Change, Disassembler, .NET SDK — pad to COLS
      // Ignored: 推定変更、逆アセンブラ、.NET SDK なし — COLS までパディング
      r += '<td></td><td></td><td></td>';
    } else {
      // Standard: Estimated Change, Disassembler, .NET SDK
      var tag = cells.length > 8 ? cells[8].textContent.trim() : '';
      var disasm = cells.length > 9 ? cells[9].textContent.trim() : '';
      var sdk = cells.length > 10 ? cells[10].textContent.trim() : '';
      r += '<td class="bd">' + esc(tag) + '</td>'
        + '<td class="bd">' + esc(disasm) + '</td>'
        + '<td class="bd" style="text-align:center">' + esc(sdk) + '</td>';
    }
    return r + '</tr>';
  }

  /**
   * Build the complete Excel HTML document from pre-built section row arrays.
   * Includes header metadata, legend, sections, summary, and warnings — all from DOM.
   * 事前構築済みセクション行配列から完全な Excel HTML ドキュメントを構築。
   * ヘッダーメタデータ、凡例、セクション、サマリー、警告をすべて DOM から取得。
   * @param {Object<string, string[]>} builtRows - Section key to array of HTML row strings
   * @returns {string} Complete Excel-compatible HTML document
   */
  function buildExcelFramework(builtRows) {
    var sectionNames = {
      'ign': '[ x ] Ignored Files', 'unch': '[ = ] Unchanged Files',
      'add': '[ + ] Added Files', 'rem': '[ - ] Removed Files', 'mod': '[ * ] Modified Files',
      'sha256w': '[ ! ] Modified Files \u2014 SHA256Mismatch: binary diff only \u2014 not a .NET assembly and not a recognized text file',
      'tsw': '[ ! ] Modified Files \u2014 new file timestamps older than old',
      'checklist': 'Review Checklist'
    };
    var sectionColors = { 'ign': '#f0f0f2', 'unch': '#f0f0f2', 'add': '#e6ffed', 'rem': '#ffeef0', 'mod': '#e3f2fd', 'sha256w': '#e3f2fd', 'tsw': '#e3f2fd', 'checklist': '#f0f0f2' };
    var sectionTextColors = { 'ign': '#000', 'unch': '#000', 'add': '#22863a', 'rem': '#b31d28', 'mod': '#0051c3', 'sha256w': '#0051c3', 'tsw': '#0051c3', 'checklist': '#000' };

    // ── Helpers ───────────────────────────────────────────────────────────
    var COLS = 13;
    function padCells(count) {
      var r = '';
      for (var i = 0; i < count; i++) r += '<td></td>';
      return r;
    }
    function emptyRow() { var r = '<tr>'; for (var i = 0; i < COLS; i++) r += '<td></td>'; return r + '</tr>'; }
    function bannerRowAt(offset, text, color, style) {
      var s = style || '';
      var r = '<tr>' + padCells(offset) + '<td style="color:' + color + ';' + s + '">' + esc(text) + '</td>';
      for (var i = offset + 1; i < COLS; i++) r += '<td></td>';
      return r + '</tr>';
    }
    function bannerRow(text, color, style) { return bannerRowAt(1, text, color, style); }
    var PAD7 = padCells(7);
    var PAD8 = padCells(8);
    function bannerRow7(text, color, style) { return bannerRowAt(7, text, color, style); }
    function bannerRow8(text, color, style) { return bannerRowAt(8, text, color, style); }

    // Per-section column headers: Ignored uses "Location" and omits trailing columns
    // セクション別列ヘッダー: Ignored は "Location" を使用し末尾列を省略
    var COL_STYLES = { 6: 'width:280px' };
    var DEFAULT_HDRS = ['#', '\u2713', 'Justification', 'Notes', 'Status', 'File Path', 'Timestamp', 'Diff Reason', 'Estimated Change', 'Disassembler', '.NET SDK'];
    var IGN_HDRS = ['#', '\u2713', 'Justification', 'Notes', 'Status', 'File Path', 'Timestamp', 'Location'];
    var CHECKLIST_HDRS = ['', '\u2713', 'Checklist Item', 'Notes'];
    function colHeaderRow(bg, sec, offset) {
      var colOffset = typeof offset === 'number' ? offset : 2;
      var hdrs = sec === 'ign' ? IGN_HDRS : sec === 'checklist' ? CHECKLIST_HDRS : DEFAULT_HDRS;
      var r = '<tr>' + padCells(colOffset);
      hdrs.forEach(function(h, i) {
        var style = 'background:' + bg + ';font-weight:bold' + (COL_STYLES[i] ? ';' + COL_STYLES[i] : '');
        if (sec === 'checklist' && i === 0) {
          r += '<td></td>';
          return;
        }
        r += '<td class="bd" style="' + style + '">' + esc(h) + '</td>';
      });
      // Pad remaining cells to maintain column count / 列数を揃えるためにパディング
      for (var j = hdrs.length + colOffset; j < COLS; j++) r += '<td></td>';
      return r + '</tr>';
    }

    // ── Header info from DOM / DOM からのヘッダー情報 ─────────────────────
    // Value cell spans remaining columns (colspan) to avoid inflating Timestamp column width
    // 値セルは残りの列に colspan で広がり、Timestamp 列幅の肥大化を防止
    var HEADER_SPAN = COLS - 9; // columns remaining after PAD8 + label = 4
    var headerHtml = '';
    document.querySelectorAll('.header-card').forEach(function(card) {
      var label = card.querySelector('.header-card-label');
      var value = card.querySelector('.header-card-value');
      if (label && value) {
        headerHtml += '<tr>' + PAD8 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(label.textContent.trim()) + '</td>'
          + '<td colspan="' + HEADER_SPAN + '" style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(value.textContent.trim()) + '</td>'
          + '</tr>';
      }
    });
    document.querySelectorAll('.header-path').forEach(function(hp) {
      var label = hp.querySelector('.header-path-label');
      var value = hp.querySelector('.header-path-value');
      if (label && value) {
        headerHtml += '<tr>' + PAD8 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(label.textContent.trim()) + '</td>'
          + '<td colspan="' + HEADER_SPAN + '" style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(value.textContent.trim()) + '</td>'
          + '</tr>';
      }
    });

    // ── Legend / 凡例 ─────────────────────────────────────────────────────
    var legendHtml = '';
    function legendKeyValueRows(items) {
      items.forEach(function(row) {
        legendHtml += '<tr>' + PAD8 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(row[0]) + '</td>'
          + '<td colspan="' + HEADER_SPAN + '" style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(row[1]) + '</td></tr>';
      });
    }
    legendHtml += bannerRow8('Legend \u2014 Diff Detail', '#000', 'font-weight:bold;padding:8px');
    legendKeyValueRows([
      ['SHA256Match / SHA256Mismatch', 'Byte-for-byte match / mismatch (SHA256)'],
      ['ILMatch / ILMismatch', 'IL (Intermediate Language) match / mismatch'],
      ['TextMatch / TextMismatch', 'Text-based match / mismatch']
    ]);
    legendHtml += emptyRow();
    legendHtml += bannerRow8('Legend \u2014 Change Importance', '#000', 'font-weight:bold;padding:8px');
    legendKeyValueRows([
      ['High', 'Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change'],
      ['Medium', 'Notable change: public/protected member addition, modifier change, access widening, internal removal'],
      ['Low', 'Low-impact change: body-only modification, internal/private member addition']
    ]);
    legendHtml += emptyRow();
    legendHtml += bannerRow8('Legend \u2014 Estimated Change', '#000', 'font-weight:bold;padding:8px');
    legendKeyValueRows([
      ['+Method', 'New method added'], ['-Method', 'Method removed'],
      ['+Type', 'New type added'], ['-Type', 'Type removed'],
      ['Possible Extract', 'Possible method body extraction to a new private/internal method'],
      ['Possible Inline', 'Possible private/internal method inlining into another method'],
      ['Possible Move', 'Possible method move between types'],
      ['Possible Rename', 'Possible method rename (same signature and IL body)'],
      ['Signature', 'Method/property signature changed'],
      ['Access', 'Access modifier changed'],
      ['BodyEdit', 'Method body IL changed only'],
      ['DepUpdate', 'Dependency package version changed only']
    ]);
    legendHtml += emptyRow();

    // ── Sections / セクション ─────────────────────────────────────────────
    var hasIgn = builtRows['ign'] && builtRows['ign'].length > 0;
    var mainKeys = hasIgn ? ['ign', 'unch', 'add', 'rem', 'mod'] : ['unch', 'add', 'rem', 'mod'];
    var sectionsHtml = '';
    mainKeys.forEach(function(sec) {
      var rows = builtRows[sec] || [];
      var txtColor = sectionTextColors[sec] || '#000';
      var name = sectionNames[sec] || sec;
      sectionsHtml += bannerRow(name + ' (' + rows.length + ')', txtColor, 'font-weight:bold;padding:8px');
      sectionsHtml += colHeaderRow(sectionColors[sec] || '#f0f0f2', sec);
      sectionsHtml += rows.join('');
      sectionsHtml += emptyRow();
    });

    // ── Summary from DOM / DOM からのサマリー ─────────────────────────────
    var summaryHtml = '';
    var statTable = document.querySelector('.stat-table');
    if (statTable) {
      // Summary is indented 1 column further right than header/legend (PAD7 + 1 = 8 cells)
      // サマリーはヘッダー/凡例より1列右にインデント（PAD7 + 1 = 8セル）
      var PAD8 = PAD7 + '<td></td>';
      var SUM_FILL = COLS - 10; // remaining cols after PAD8 + Category + Count / PAD8 + Category + Count 後の残り列数
      summaryHtml += '<tr>' + PAD8 + '<td style="color:#000;font-weight:bold;padding:8px">' + esc('Summary') + '</td>';
      for (var si = 9; si < COLS; si++) summaryHtml += '<td></td>';
      summaryHtml += '</tr>';
      summaryHtml += '<tr>' + PAD8 + '<td class="bd" style="background:#f0f0f2;font-weight:bold">Category</td>'
        + '<td class="bd" style="background:#f0f0f2;font-weight:bold">Count</td>'
        + '<td colspan="' + SUM_FILL + '"></td></tr>';
      var summaryRowColors = { 'Added': '#e6ffed', 'Removed': '#ffeef0', 'Modified': '#e3f2fd' };
      statTable.querySelectorAll('tr').forEach(function(tr) {
        if (tr.querySelector('th')) return;
        var cells = tr.querySelectorAll('td');
        if (cells.length >= 2) {
          var label = cells[0].textContent.trim();
          var bgStyle = summaryRowColors[label] ? 'background:' + summaryRowColors[label] + ';' : '';
          summaryHtml += '<tr>' + PAD8 + '<td class="bd" style="' + bgStyle + '">' + esc(label) + '</td>'
            + '<td class="bd" style="' + bgStyle + 'text-align:right">' + esc(cells[1].textContent.trim()) + '</td>'
            + '<td colspan="' + SUM_FILL + '"></td></tr>';
        }
      });
      summaryHtml += emptyRow();
    }

    // ── Warnings / 警告 ──────────────────────────────────────────────────
    var warningsHtml = '';
    var warnKeys = ['sha256w', 'tsw'];
    var hasWarn = false;
    warnKeys.forEach(function(sec) {
      var rows = builtRows[sec];
      if (!rows || rows.length === 0) return;
      if (!hasWarn) { warningsHtml += bannerRow('Warnings', '#000', 'font-weight:bold;padding:8px'); hasWarn = true; }
      warningsHtml += bannerRow((sectionNames[sec] || sec) + ' (' + rows.length + ')', sectionTextColors[sec] || '#000', 'font-weight:bold;padding:8px');
      warningsHtml += colHeaderRow(sectionColors[sec] || '#e3f2fd', sec);
      warningsHtml += rows.join('');
      warningsHtml += emptyRow();
    });

    // ── Review Checklist / レビューチェックリスト ────────────────────────
    var checklistHtml = '';
    var checklistRows = builtRows['checklist'] || [];
    if (checklistRows.length > 0) {
      checklistHtml += bannerRow8((sectionNames.checklist || 'Review Checklist') + ' (' + checklistRows.length + ')', sectionTextColors.checklist || '#000', 'font-weight:bold;padding:8px');
      checklistHtml += colHeaderRow(sectionColors.checklist || '#f0f0f2', 'checklist', 7);
      checklistHtml += checklistRows.join('');
      checklistHtml += emptyRow();
    }

    // ── Assemble final HTML / 最終 HTML を組み立て ────────────────────────
    // Colgroup constrains Timestamp column (index 8) — Excel ignores td width with white-space:nowrap
    // colgroup で Timestamp 列（インデックス 8）を制約 — Excel は white-space:nowrap 時に td の width を無視する
    var colgroup = '<colgroup>';
    for (var ci = 0; ci < COLS; ci++) {
      colgroup += ci === 8 ? '<col style="width:280px">' : '<col>';
    }
    colgroup += '</colgroup>\n';
    return '<!DOCTYPE html>\n<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">\n'
      + '<head><meta charset="UTF-8">\n'
      + '<style>\n'
      + 'table { border-collapse: collapse; font-family: "Meiryo UI", sans-serif; font-size: 11px; }\n'
      + 'td, th { border: none; padding: 4px 8px; white-space: nowrap; vertical-align: top; }\n'
      + 'td.bd, th.bd { border: 1px solid #ccc; }\n'
      + '</style>\n'
      + '</head><body>\n'
      + '<table>\n'
      + colgroup
      + emptyRow() + '\n'
      + headerHtml
      + emptyRow() + '\n'
      + legendHtml
      + sectionsHtml
      + summaryHtml
      + warningsHtml
      + checklistHtml
      + '</table>\n'
      + '</body></html>';
  }

  /**
   * Escape HTML special characters for safe embedding in HTML output.
   * @param {string} s
   * @returns {string}
   */
  function esc(s) {
    if (!s) return '';
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  /* Export pure functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に純粋関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { esc: esc }; }
