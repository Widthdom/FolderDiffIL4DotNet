  /**
   * Decode a base64-encoded UTF-8 HTML string.
   * @param {string} b64 - Base64-encoded string
   * @returns {string} Decoded HTML
   */
  function decodeDiffHtml(b64) {
    var binary = atob(b64);
    var bytes = new Uint8Array(binary.length);
    for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return new TextDecoder('utf-8').decode(bytes);
  }

  /**
   * Toggle between unified and side-by-side diff view.
   * Side-by-side uses 3-column layout: [LineNum] [Old/Red] [New/Green].
   * @param {HTMLDetailsElement} detailsEl - The details element containing the diff table
   */
  function toggleDiffView(detailsEl) {
    var table = detailsEl.querySelector('.diff-table');
    if (!table) return;
    var btn = detailsEl.querySelector('.sbs-toggle');
    if (table.classList.contains('sbs-mode')) {
      // Restore unified view / 統合ビューに復元
      table.classList.remove('sbs-mode');
      var cg = table.querySelector('.sbs-colgroup');
      if (cg) cg.remove();
      var tf = table.querySelector('tfoot.sbs-tfoot');
      if (tf) tf.remove();
      var tbody = table.querySelector('tbody');
      if (table._unifiedHtml) { tbody.innerHTML = table._unifiedHtml; }
      if (btn) btn.textContent = 'Side-by-side';
      return;
    }
    // Switch to side-by-side / サイドバイサイドに切り替え
    var tbody = table.querySelector('tbody');
    table._unifiedHtml = tbody.innerHTML;
    // Add colgroup for fixed 3-column widths / 固定3列幅の colgroup を追加
    var cg = document.createElement('colgroup');
    cg.className = 'sbs-colgroup';
    var col1 = document.createElement('col');
    col1.style.width = '7em';
    var col2 = document.createElement('col');
    col2.style.width = 'calc(50% - 3.5em)';
    var col3 = document.createElement('col');
    col3.style.width = 'calc(50% - 3.5em)';
    cg.appendChild(col1); cg.appendChild(col2); cg.appendChild(col3);
    table.insertBefore(cg, table.firstChild);
    // Helper: extract line number text from unified row / 統合行から行番号テキストを取得するヘルパー
    function getLinePair(row) {
      var lns = row.querySelectorAll('.diff-ln');
      var oldLn = lns.length > 0 ? lns[0].textContent.trim() : '';
      var newLn = lns.length > 1 ? lns[1].textContent.trim() : '';
      return { old: oldLn, new: newLn };
    }
    var rows = Array.prototype.slice.call(tbody.querySelectorAll('tr'));
    var newRows = [];
    var i = 0;
    while (i < rows.length) {
      var r = rows[i];
      if (r.classList.contains('diff-hunk-tr')) {
        // Hunk header: [LN] [hunk text spanning 2 cols]
        // ハンクヘッダー: [行番号] [ハンクテキスト 2列結合]
        var tr = document.createElement('tr');
        tr.className = 'diff-hunk-tr';
        var tdLn = document.createElement('td');
        tdLn.className = 'diff-ln';
        var td = document.createElement('td');
        td.className = 'diff-hunk-td';
        td.colSpan = 2;
        td.textContent = r.querySelector('.diff-hunk-td') ? r.querySelector('.diff-hunk-td').textContent : '';
        tr.appendChild(tdLn); tr.appendChild(td);
        newRows.push(tr);
        i++;
      } else if (r.classList.contains('diff-del-tr') && i + 1 < rows.length && rows[i + 1].classList.contains('diff-add-tr')) {
        // Consecutive del+add pair: [oldLn/newLn] [old text] [new text]
        // 連続するdel+addペア: [旧行番号/新行番号] [旧テキスト] [新テキスト]
        var delRow = r;
        var addRow = rows[i + 1];
        var tr = document.createElement('tr');
        var delP = getLinePair(delRow);
        var addP = getLinePair(addRow);
        var oldLnText = delP.old || delP.new;
        var newLnText = addP.new || addP.old;
        var tdLn = document.createElement('td');
        tdLn.className = 'diff-ln';
        tdLn.textContent = oldLnText && newLnText ? oldLnText + '/' + newLnText : (oldLnText || newLnText);
        var delTd = delRow.querySelector('.diff-del-td');
        var addTd = addRow.querySelector('.diff-add-td');
        var tdOldText = document.createElement('td');
        tdOldText.className = 'sbs-old';
        tdOldText.textContent = delTd ? delTd.textContent : '';
        tdOldText.style.whiteSpace = 'pre';
        var tdNewText = document.createElement('td');
        tdNewText.className = 'sbs-new';
        tdNewText.textContent = addTd ? addTd.textContent : '';
        tdNewText.style.whiteSpace = 'pre';
        tr.appendChild(tdLn); tr.appendChild(tdOldText); tr.appendChild(tdNewText);
        newRows.push(tr);
        i += 2;
      } else if (r.classList.contains('diff-del-tr')) {
        // Standalone deletion: [oldLn] [old text] [empty]
        // 単独の削除行: [旧行番号] [旧テキスト] [空]
        var tr = document.createElement('tr');
        var lnP = getLinePair(r);
        var tdLn = document.createElement('td');
        tdLn.className = 'diff-ln';
        tdLn.textContent = lnP.old || lnP.new;
        var delTd = r.querySelector('.diff-del-td');
        var tdOldText = document.createElement('td');
        tdOldText.className = 'sbs-old';
        tdOldText.textContent = delTd ? delTd.textContent : '';
        tdOldText.style.whiteSpace = 'pre';
        var tdNewText = document.createElement('td');
        tdNewText.className = 'sbs-empty';
        tr.appendChild(tdLn); tr.appendChild(tdOldText); tr.appendChild(tdNewText);
        newRows.push(tr);
        i++;
      } else if (r.classList.contains('diff-add-tr')) {
        // Standalone addition: [newLn] [empty] [new text]
        // 単独の追加行: [新行番号] [空] [新テキスト]
        var tr = document.createElement('tr');
        var lnP = getLinePair(r);
        var tdLn = document.createElement('td');
        tdLn.className = 'diff-ln';
        tdLn.textContent = lnP.new || lnP.old;
        var addTd = r.querySelector('.diff-add-td');
        var tdOldText = document.createElement('td');
        tdOldText.className = 'sbs-empty';
        var tdNewText = document.createElement('td');
        tdNewText.className = 'sbs-new';
        tdNewText.textContent = addTd ? addTd.textContent : '';
        tdNewText.style.whiteSpace = 'pre';
        tr.appendChild(tdLn); tr.appendChild(tdOldText); tr.appendChild(tdNewText);
        newRows.push(tr);
        i++;
      } else if (r.classList.contains('diff-trunc-tr')) {
        // Truncation row: [LN] [text spanning 2 cols]
        // 省略行: [行番号] [テキスト 2列結合]
        var tr = document.createElement('tr');
        tr.className = 'diff-trunc-tr';
        var tdLn = document.createElement('td');
        tdLn.className = 'diff-ln';
        var td = document.createElement('td');
        td.className = 'diff-trunc-td';
        td.colSpan = 2;
        td.textContent = r.querySelector('.diff-trunc-td') ? r.querySelector('.diff-trunc-td').textContent : '';
        tr.appendChild(tdLn); tr.appendChild(td);
        newRows.push(tr);
        i++;
      } else {
        // Context row: [LN] [text spanning 2 cols]
        // コンテキスト行: [行番号] [テキスト 2列結合]
        var tr = document.createElement('tr');
        tr.className = 'diff-ctx-tr';
        var lnTd = r.querySelector('.diff-ln');
        var ctxTd = r.querySelector('.diff-ctx-td');
        var tdLn = document.createElement('td');
        tdLn.className = 'diff-ln';
        tdLn.textContent = lnTd ? lnTd.textContent : '';
        var tdCtx = document.createElement('td');
        tdCtx.className = 'sbs-ctx';
        tdCtx.colSpan = 2;
        tdCtx.textContent = ctxTd ? ctxTd.textContent : '';
        tdCtx.style.whiteSpace = 'pre';
        tr.appendChild(tdLn); tr.appendChild(tdCtx);
        newRows.push(tr);
        i++;
      }
    }
    tbody.innerHTML = '';
    newRows.forEach(function(tr) { tbody.appendChild(tr); });
    // Wrap sbs-old/sbs-new content in inner divs for synchronized horizontal scrolling
    // sbs-old/sbs-new のコンテンツを水平スクロール同期用の inner div でラップ
    tbody.querySelectorAll('td.sbs-old, td.sbs-new').forEach(function(td) {
      var div = document.createElement('div');
      div.className = 'sbs-inner';
      while (td.firstChild) div.appendChild(td.firstChild);
      td.appendChild(div);
    });
    // Calculate max content width across all inner divs
    // 全 inner div の最大コンテンツ幅を計算
    var maxW = 0;
    tbody.querySelectorAll('.sbs-inner').forEach(function(div) {
      if (div.scrollWidth > maxW) maxW = div.scrollWidth;
    });
    // Force all inner divs to the same scrollable width so short lines scroll together.
    // Wrap content in a span with min-width so every row has the same scroll range.
    // 短い行も一緒にスクロールするよう全 inner div を同じスクロール可能幅に統一。
    // 各行のコンテンツを min-width 付き span でラップし全行同一スクロール範囲にする。
    tbody.querySelectorAll('.sbs-inner').forEach(function(div) {
      var span = document.createElement('span');
      span.style.display = 'inline-block';
      span.style.minWidth = maxW + 'px';
      while (div.firstChild) span.appendChild(div.firstChild);
      div.appendChild(span);
    });
    // Add sticky proxy scrollbar row (tfoot) for synchronized horizontal scrolling
    // 水平スクロール同期用のスティッキープロキシスクロールバー行（tfoot）を追加
    var tfoot = document.createElement('tfoot');
    tfoot.className = 'sbs-tfoot';
    var scrollTr = document.createElement('tr');
    scrollTr.className = 'sbs-scroll-tr';
    var tdLn = document.createElement('td');
    var tdOld = document.createElement('td');
    var proxyOld = document.createElement('div');
    proxyOld.className = 'sbs-scroll-proxy';
    var spacerOld = document.createElement('div');
    spacerOld.style.width = maxW + 'px';
    proxyOld.appendChild(spacerOld);
    tdOld.appendChild(proxyOld);
    var tdNew = document.createElement('td');
    var proxyNew = document.createElement('div');
    proxyNew.className = 'sbs-scroll-proxy';
    var spacerNew = document.createElement('div');
    spacerNew.style.width = maxW + 'px';
    proxyNew.appendChild(spacerNew);
    tdNew.appendChild(proxyNew);
    scrollTr.appendChild(tdLn); scrollTr.appendChild(tdOld); scrollTr.appendChild(tdNew);
    tfoot.appendChild(scrollTr);
    table.appendChild(tfoot);
    // Sync scroll between proxy bars and inner divs
    // プロキシバーと inner div 間のスクロール同期
    var syncing = false;
    function syncScroll(src, tgt) {
      if (syncing) return;
      syncing = true;
      tgt.scrollLeft = src.scrollLeft;
      var sl = src.scrollLeft;
      tbody.querySelectorAll('.sbs-inner').forEach(function(d) { d.scrollLeft = sl; });
      syncing = false;
    }
    proxyOld.addEventListener('scroll', function() { syncScroll(proxyOld, proxyNew); });
    proxyNew.addEventListener('scroll', function() { syncScroll(proxyNew, proxyOld); });
    table.classList.add('sbs-mode');
    if (btn) btn.textContent = 'Unified';
    // Re-apply IL syntax highlighting after SBS conversion
    // SBS 変換後に IL シンタックスハイライトを再適用
    highlightILDiff(table);
  }

  /* Export functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { decodeDiffHtml: decodeDiffHtml, toggleDiffView: toggleDiffView }; }
