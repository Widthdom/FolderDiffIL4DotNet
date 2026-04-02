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
   */
  function downloadExcelImmediate() {
    /* Force-decode all lazy sections to capture all data / 全 lazy セクションを強制デコード */
    forceDecodeLazySections();

    var sectionNames = {
      'ign': '[ x ] Ignored Files', 'unch': '[ = ] Unchanged Files',
      'add': '[ + ] Added Files', 'rem': '[ - ] Removed Files', 'mod': '[ * ] Modified Files',
      'sha256w': '[ ! ] Modified Files \u2014 SHA256Mismatch: binary diff only \u2014 not a .NET assembly and not a recognized text file',
      'tsw': '[ ! ] Modified Files \u2014 new file timestamps older than old'
    };
    // Section background colors for column header rows / 列ヘッダー行のセクション背景色
    var sectionColors = {
      'ign': '#f0f0f2', 'unch': '#f0f0f2',
      'add': '#e6ffed', 'rem': '#ffeef0', 'mod': '#e3f2fd',
      'sha256w': '#e3f2fd', 'tsw': '#e3f2fd'
    };
    // Section title text colors (match HTML report h2 styles) / セクションタイトルの文字色（HTMLレポートのh2スタイルと一致）
    var sectionTextColors = {
      'ign': '#000', 'unch': '#000',
      'add': '#22863a', 'rem': '#b31d28', 'mod': '#0051c3',
      'sha256w': '#0051c3', 'tsw': '#0051c3'
    };

    // Helper: 12-cell empty row / 12セルの空行
    var COLS = 13;
    function emptyRow() {
      var r = '<tr>';
      for (var i = 0; i < COLS; i++) r += '<td></td>';
      return r + '</tr>';
    }
    // Helper: section title row — col 1 (Excel B) / セクションタイトル行 — 列1（Excel B列）
    function bannerRow(text, color, style) {
      var s = style || '';
      var r = '<tr><td></td><td style="color:' + color + ';' + s + '">' + esc(text) + '</td>';
      for (var i = 2; i < COLS; i++) r += '<td></td>';
      return r + '</tr>';
    }
    // Helper: 7-cell empty pad (align with col 7 = Excel H) / 7セルの空パディング（列7 = Excel H列に揃える）
    var PAD7 = '<td></td><td></td><td></td><td></td><td></td><td></td><td></td>';
    // Helper: section title row shifted to col 7 (Excel H) / 列7（Excel H列）に揃えたセクションタイトル行
    function bannerRow7(text, color, style) {
      var s = style || '';
      var r = '<tr>' + PAD7 + '<td style="color:' + color + ';' + s + '">' + esc(text) + '</td>';
      for (var i = 8; i < COLS; i++) r += '<td></td>';
      return r + '</tr>';
    }
    // Helper: column header row — # at col 2 (Excel C) / 列ヘッダー行 — #は列2（Excel C列）
    function colHeaderRow(bg) {
      var hdrs = ['#', '\u2713', 'Justification', 'Notes', 'Status', 'File Path', 'Timestamp', 'Diff Reason', 'Estimated Change', 'Disassembler', '.NET SDK'];
      var r = '<tr><td></td><td></td>';
      hdrs.forEach(function(h) { r += '<td class="bd" style="background:' + bg + ';font-weight:bold">' + esc(h) + '</td>'; });
      return r + '</tr>';
    }

    // Collect header info from report / レポートからヘッダー情報を収集
    var headerCards = document.querySelectorAll('.header-card');
    var headerHtml = '';
    headerCards.forEach(function(card) {
      var label = card.querySelector('.header-card-label');
      var value = card.querySelector('.header-card-value');
      if (label && value) {
        headerHtml += '<tr>' + PAD7 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(label.textContent.trim()) + '</td>'
          + '<td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(value.textContent.trim()) + '</td>';
        for (var i = 9; i < COLS; i++) headerHtml += '<td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc"></td>';
        headerHtml += '</tr>';
      }
    });
    var headerPaths = document.querySelectorAll('.header-path');
    headerPaths.forEach(function(hp) {
      var label = hp.querySelector('.header-path-label');
      var value = hp.querySelector('.header-path-value');
      if (label && value) {
        headerHtml += '<tr>' + PAD7 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(label.textContent.trim()) + '</td>'
          + '<td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(value.textContent.trim()) + '</td>';
        for (var i = 9; i < COLS; i++) headerHtml += '<td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc"></td>';
        headerHtml += '</tr>';
      }
    });

    // Build section tables — always include all main sections even when empty
    // セクションテーブルを構築 — 0件でも全メインセクションを見出し付きで出力
    var sectionsHtml = '';
    // Canonical section order (ign only if present in report) / 正規セクション順序（ignはレポートに存在する場合のみ）
    var hasIgnSection = document.querySelectorAll('tbody > tr[data-section' + '="ign"]').length > 0
      || document.querySelector('h2') && Array.prototype.slice.call(document.querySelectorAll('h2')).some(function(h) { return h.textContent.indexOf('Ignored Files') >= 0; });
    var allSectionKeys = hasIgnSection ? ['ign', 'unch', 'add', 'rem', 'mod'] : ['unch', 'add', 'rem', 'mod'];
    // Separate warning sections (placed after Summary) / 警告セクションを分離（Summary の後に配置）
    var warningSections = ['sha256w', 'tsw'];
    // Discover any warning sections that exist in the DOM / DOM に存在する警告セクションを検出
    var seenWarnSections = [];
    document.querySelectorAll('tbody > tr[data-section]').forEach(function(tr) {
      var sec = tr.getAttribute('data-section');
      if (warningSections.indexOf(sec) >= 0 && seenWarnSections.indexOf(sec) < 0) seenWarnSections.push(sec);
    });

    function buildSectionHtml(sec) {
      var bgColor = sectionColors[sec] || '#f0f0f2';
      var txtColor = sectionTextColors[sec] || '#000';
      var name = sectionNames[sec] || sec;
      var sectionRows = document.querySelectorAll('tbody > tr[data-section="' + sec + '"]');
      var h = bannerRow(name + ' (' + sectionRows.length + ')', txtColor, 'font-weight:bold;padding:8px');
      h += colHeaderRow(bgColor);
      sectionRows.forEach(function(tr) { h += buildExcelRow(tr); });
      h += emptyRow();
      return h;
    }

    allSectionKeys.forEach(function(sec) { sectionsHtml += buildSectionHtml(sec); });

    // Build legend section / 凡例セクションを構築
    var legendHtml = '';
    // Legend — Diff Detail / 凡例 — 判定根拠
    legendHtml += bannerRow7('Legend \u2014 Diff Detail', '#000', 'font-weight:bold;padding:8px');
    var diffLegend = [
      ['SHA256Match / SHA256Mismatch', 'Byte-for-byte match / mismatch (SHA256)'],
      ['ILMatch / ILMismatch', 'IL (Intermediate Language) match / mismatch'],
      ['TextMatch / TextMismatch', 'Text-based match / mismatch']
    ];
    diffLegend.forEach(function(row) {
      legendHtml += '<tr>' + PAD7 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(row[0]) + '</td><td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(row[1]) + '</td>';
      for (var i = 9; i < COLS; i++) legendHtml += '<td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc"></td>';
      legendHtml += '</tr>';
    });
    legendHtml += emptyRow();
    // Legend — Change Importance / 凡例 — 変更重要度
    legendHtml += bannerRow7('Legend \u2014 Change Importance', '#000', 'font-weight:bold;padding:8px');
    var impLegend = [
      ['High', 'Breaking change candidate: public/protected API removal, access narrowing, return-type / parameter / member-type change'],
      ['Medium', 'Notable change: public/protected member addition, modifier change, access widening, internal removal'],
      ['Low', 'Low-impact change: body-only modification, internal/private member addition']
    ];
    impLegend.forEach(function(row) {
      legendHtml += '<tr>' + PAD7 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(row[0]) + '</td><td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(row[1]) + '</td>';
      for (var i = 9; i < COLS; i++) legendHtml += '<td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc"></td>';
      legendHtml += '</tr>';
    });
    legendHtml += emptyRow();
    // Legend — Estimated Change / 凡例 — 推定変更
    legendHtml += bannerRow7('Legend \u2014 Estimated Change', '#000', 'font-weight:bold;padding:8px');
    var tagLegend = [
      ['+Method', 'New method added'],
      ['-Method', 'Method removed'],
      ['+Type', 'New type added'],
      ['-Type', 'Type removed'],
      ['Extract', 'Method body extracted to new private/internal method'],
      ['Inline', 'Private/internal method inlined into another method'],
      ['Move', 'Method moved between types'],
      ['Rename', 'Method renamed (same signature and IL body)'],
      ['Signature', 'Method/property signature changed'],
      ['Access', 'Access modifier changed'],
      ['BodyEdit', 'Method body IL changed only'],
      ['DepUpdate', 'Dependency package version changed only']
    ];
    tagLegend.forEach(function(row) {
      legendHtml += '<tr>' + PAD7 + '<td class="bd" style="font-weight:bold;background:#f0f0f2">' + esc(row[0]) + '</td><td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc">' + esc(row[1]) + '</td>';
      for (var i = 9; i < COLS; i++) legendHtml += '<td style="border-top:1px solid #ccc;border-bottom:1px solid #ccc"></td>';
      legendHtml += '</tr>';
    });
    legendHtml += emptyRow();

    // Build summary section / サマリーセクションを構築
    var summaryHtml = '';
    var statTable = document.querySelector('.stat-table');
    if (statTable) {
      summaryHtml += bannerRow7('Summary', '#000', 'font-weight:bold;padding:8px');
      // Column header row for summary (same bg as Ignored Files header) / サマリーの列ヘッダー行（Ignored Files ヘッダーと同じ背景色）
      summaryHtml += '<tr>' + PAD7 + '<td class="bd" style="background:#f0f0f2;font-weight:bold">Category</td>'
        + '<td class="bd" style="background:#f0f0f2;font-weight:bold">Count</td>';
      for (var si = 9; si < COLS; si++) summaryHtml += '<td></td>';
      summaryHtml += '</tr>';
      // Row background colors matching HTML report summary table / HTMLレポートのサマリーテーブルに合わせた行背景色
      var summaryRowColors = { 'Added': '#e6ffed', 'Removed': '#ffeef0', 'Modified': '#e3f2fd' };
      statTable.querySelectorAll('tr').forEach(function(tr) {
        // Skip header rows containing th elements / th要素を含むヘッダー行をスキップ
        if (tr.querySelector('th')) return;
        var cells = tr.querySelectorAll('td');
        if (cells.length >= 2) {
          var label = cells[0].textContent.trim();
          var bgStyle = summaryRowColors[label] ? 'background:' + summaryRowColors[label] + ';' : '';
          summaryHtml += '<tr>' + PAD7 + '<td class="bd" style="' + bgStyle + '">' + esc(label) + '</td>'
            + '<td class="bd" style="' + bgStyle + 'text-align:right">' + esc(cells[1].textContent.trim()) + '</td>';
          for (var i = 9; i < COLS; i++) summaryHtml += '<td></td>';
          summaryHtml += '</tr>';
        }
      });
      summaryHtml += emptyRow();
    }

    // Build warning sections (placed after Summary) / 警告セクション（Summary の後に配置）
    var warningsHtml = '';
    if (seenWarnSections.length > 0) {
      warningsHtml += bannerRow('Warnings', '#000', 'font-weight:bold;padding:8px');
    }
    seenWarnSections.forEach(function(sec) { warningsHtml += buildSectionHtml(sec); });

    var slug = 'diff_report_' + __reportDate__;
    var excelFileName = slug + '_reviewed_Excel-compatible.html';
    var out = '<!DOCTYPE html>\n<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">\n'
      + '<head><meta charset="UTF-8">\n'
      + '<style>\n'
      + 'table { border-collapse: collapse; font-family: "Meiryo UI", sans-serif; font-size: 11px; }\n'
      + 'td, th { border: none; padding: 4px 8px; white-space: nowrap; vertical-align: top; }\n'
      + 'td.bd, th.bd { border: 1px solid #ccc; }\n'
      + '</style>\n'
      + '</head><body>\n'
      + '<table>\n'
      + emptyRow() + '\n'
      + headerHtml
      + emptyRow() + '\n'
      + legendHtml
      + sectionsHtml
      + summaryHtml
      + warningsHtml
      + '</table>\n'
      + '</body></html>';

    var blob = new Blob([out], { type: 'application/vnd.ms-excel;charset=utf-8' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = excelFileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function() { URL.revokeObjectURL(a.href); }, 1000);
  }

  /**
   * Build an Excel-compatible HTML table row from a DOM table row element.
   * @param {HTMLTableRowElement} tr - A data row with data-section attribute
   * @returns {string} HTML string for the Excel row, or empty string if insufficient cells
   */
  function buildExcelRow(tr) {
    var cells = tr.querySelectorAll('td');
    if (cells.length < 10) return '';
    // #
    var no = cells[0].textContent.trim();
    // Checkbox state / チェックボックス状態
    var cb = cells[1].querySelector('input[type="checkbox"]');
    var checked = cb && cb.checked ? '\u2713' : '';
    // Justification (text input value) / 理由（テキスト入力値）
    var reasonInp = cells[2].querySelector('input');
    var reason = reasonInp ? reasonInp.value : '';
    // Notes (text input value) / メモ（テキスト入力値）
    var notesInp = cells[3].querySelector('input');
    var notes = notesInp ? notesInp.value : '';
    // Status / ステータス
    var status = cells[4].textContent.trim();
    // File Path / ファイルパス
    var pathSpan = cells[5].querySelector('.path-text');
    var path = pathSpan ? pathSpan.textContent.trim() : cells[5].textContent.trim();
    // Timestamp / タイムスタンプ
    var ts = cells[6].textContent.trim();
    // Diff Detail
    var diff = cells[7].textContent.trim();
    // Estimated Change / 推定変更
    var tag = cells[8].textContent.trim();
    // Disassembler
    var disasm = cells[9].textContent.trim();
    // .NET SDK
    var sdk = cells.length > 10 ? cells[10].textContent.trim() : '';

    return '<tr><td></td><td></td>'
      + '<td class="bd">' + esc(no) + '</td>'
      + '<td class="bd">' + esc(checked) + '</td>'
      + '<td class="bd">' + esc(reason) + '</td>'
      + '<td class="bd">' + esc(notes) + '</td>'
      + '<td class="bd" style="text-align:center">' + esc(status) + '</td>'
      + '<td class="bd">' + esc(path) + '</td>'
      + '<td class="bd" style="text-align:center;mso-number-format:\'\\@\'">' + esc(ts) + '</td>'
      + '<td class="bd" style="text-align:center">' + esc(diff) + '</td>'
      + '<td class="bd">' + esc(tag) + '</td>'
      + '<td class="bd">' + esc(disasm) + '</td>'
      + '<td class="bd">' + esc(sdk) + '</td>'
      + '</tr>';
  }

  /**
   * Build the complete Excel HTML from pre-built section row arrays (used by chunked export).
   * @param {Object<string, string[]>} builtRows - Section key to array of HTML row strings
   * @returns {string} Complete Excel-compatible HTML document
   */
  function buildExcelFramework(builtRows) {
    var sectionNames = {
      'ign': '[ x ] Ignored Files', 'unch': '[ = ] Unchanged Files',
      'add': '[ + ] Added Files', 'rem': '[ - ] Removed Files', 'mod': '[ * ] Modified Files',
      'sha256w': '[ ! ] Modified Files \u2014 SHA256Mismatch: binary diff only \u2014 not a .NET assembly and not a recognized text file',
      'tsw': '[ ! ] Modified Files \u2014 new file timestamps older than old'
    };
    var sectionColors = { 'ign': '#f0f0f2', 'unch': '#f0f0f2', 'add': '#e6ffed', 'rem': '#ffeef0', 'mod': '#e3f2fd', 'sha256w': '#e3f2fd', 'tsw': '#e3f2fd' };
    var sectionTextColors = { 'ign': '#000', 'unch': '#000', 'add': '#22863a', 'rem': '#b31d28', 'mod': '#0051c3', 'sha256w': '#0051c3', 'tsw': '#0051c3' };
    var COLS = 13;
    function emptyRow() { var r = '<tr>'; for (var i = 0; i < COLS; i++) r += '<td></td>'; return r + '</tr>'; }
    function bannerRow(text, color, style) {
      var s = style || '';
      var r = '<tr><td></td><td style="color:' + color + ';' + s + '">' + esc(text) + '</td>';
      for (var i = 2; i < COLS; i++) r += '<td></td>'; return r + '</tr>';
    }
    function colHeaderRow(bg) {
      var hdrs = ['#', '\u2713', 'Justification', 'Notes', 'Status', 'File Path', 'Timestamp', 'Diff Reason', 'Estimated Change', 'Disassembler', '.NET SDK'];
      var r = '<tr><td></td><td></td>';
      hdrs.forEach(function(h) { r += '<td class="bd" style="background:' + bg + ';font-weight:bold">' + esc(h) + '</td>'; });
      return r + '</tr>';
    }
    var hasIgn = builtRows['ign'] && builtRows['ign'].length > 0;
    var mainKeys = hasIgn ? ['ign', 'unch', 'add', 'rem', 'mod'] : ['unch', 'add', 'rem', 'mod'];
    var sectionsHtml = '';
    mainKeys.forEach(function(sec) {
      var rows = builtRows[sec] || [];
      var txtColor = sectionTextColors[sec] || '#000';
      var name = sectionNames[sec] || sec;
      sectionsHtml += bannerRow(name + ' (' + rows.length + ')', txtColor, 'font-weight:bold;padding:8px');
      sectionsHtml += colHeaderRow(sectionColors[sec] || '#f0f0f2');
      sectionsHtml += rows.join('');
      sectionsHtml += emptyRow();
    });
    var warnKeys = ['sha256w', 'tsw'];
    var warningsHtml = '';
    warnKeys.forEach(function(sec) {
      var rows = builtRows[sec];
      if (!rows || rows.length === 0) return;
      warningsHtml += bannerRow((sectionNames[sec] || sec) + ' (' + rows.length + ')', sectionTextColors[sec] || '#000', 'font-weight:bold;padding:8px');
      warningsHtml += colHeaderRow(sectionColors[sec] || '#e3f2fd');
      warningsHtml += rows.join('');
      warningsHtml += emptyRow();
    });
    return '<!DOCTYPE html>\n<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">\n'
      + '<head><meta charset="UTF-8">\n<style>\ntable { border-collapse: collapse; font-family: "Meiryo UI", sans-serif; font-size: 11px; }\ntd, th { border: none; padding: 4px 8px; white-space: nowrap; vertical-align: top; }\ntd.bd, th.bd { border: 1px solid #ccc; }\n</style>\n</head><body>\n<table>\n'
      + emptyRow() + '\n' + sectionsHtml + warningsHtml + '</table>\n</body></html>';
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
