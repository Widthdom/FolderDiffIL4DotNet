  const __storageKey__  = '{{STORAGE_KEY}}';
  const __reportDate__  = '{{REPORT_DATE}}';
  // NOTE: 'const __savedState__ = null;' is replaced by downloadReviewed(). Do not change whitespace.
  const __savedState__  = null;
  // NOTE: replaced with SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __reviewedSha256__  = null;
  // NOTE: replaced with final SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __finalSha256__     = null;
  // Total number of reviewable files (injected at generation time). Do not change whitespace.
  // レビュー対象ファイルの総数（生成時に注入）。空白を変更しないこと。
  const __totalFiles__      = {{TOTAL_FILES}};
  const __totalFilesDetail__ = '{{TOTAL_FILES_DETAIL}}';

  function formatTs(d) {
    return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')
      +' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0');
  }

  function readSavedStateFromStorage(fallbackJson) {
    try {
      var parsed = JSON.parse(localStorage.getItem(__storageKey__) || fallbackJson);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch(e) {
      return null;
    }
  }

  document.addEventListener('DOMContentLoaded', function() {
    var toRestore = __savedState__;
    if (!toRestore) {
      toRestore = readSavedStateFromStorage('null');
    }
    if (toRestore) {
      Object.entries(toRestore).forEach(function(entry) {
        var el = document.getElementById(entry[0]);
        if (!el) return;
        if (el.type === 'checkbox') el.checked = Boolean(entry[1]);
        else el.value = String(entry[1] || '');
      });
    }
    if (__savedState__ !== null) {
      document.querySelectorAll('input[type="checkbox"]').forEach(function(cb){
        if (__filterIds__.indexOf(cb.id) >= 0) return; // keep filter controls interactive / フィルタコントロールは操作可能に
        cb.style.pointerEvents='none'; cb.style.cursor='default';
      });
      document.querySelectorAll('input[type="text"]').forEach(function(inp){
        if (inp.id === 'filter-search') return; // keep search interactive / 検索は操作可能に
        inp.readOnly=true; inp.style.cursor='text'; inp.style.userSelect='text';
      });
    } else {
      document.querySelectorAll('input, textarea').forEach(function(el) {
        el.addEventListener('change', autoSave);
        el.addEventListener('input',  autoSave);
      });
    }
    initColResize();
    syncTableWidths();
    syncScTableWidths();
    syncFilterRowHeight();
    initClearButtons();
    setupLazyDiff();
    setupLazySection();
    updateProgress();
    // Pre-create hidden file input for Verify integrity so the accept
    // filter is ready before the first click (some browsers ignore accept
    // on dynamically created inputs that are clicked immediately)
    var vi = document.createElement('input');
    vi.type = 'file';
    vi.accept = '.sha256';
    vi.style.display = 'none';
    vi.id = '__verifyInput__';
    document.body.appendChild(vi);
  });

  var __filterIds__ = ['filter-diff-sha256match','filter-diff-sha256mismatch','filter-diff-ilmatch','filter-diff-ilmismatch','filter-diff-textmatch','filter-diff-textmismatch','filter-imp-high','filter-imp-medium','filter-imp-low','filter-unchecked','filter-search'];
  function collectState() {
    var s = {};
    document.querySelectorAll('input[id], textarea[id]').forEach(function(el) {
      if (__filterIds__.indexOf(el.id) >= 0) return;
      s[el.id] = (el.type === 'checkbox') ? el.checked : el.value;
    });
    return s;
  }

  function autoSave() {
    try {
      localStorage.setItem(__storageKey__, JSON.stringify(collectState()));
    } catch(e) {
      // Gracefully handle localStorage quota exceeded or other storage errors
      // localStorage 容量超過やその他のストレージエラーを安全に処理
    }
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'Auto-saved at ' + formatTs(new Date());
    updateProgress();
  }

  // Update the review progress bar / レビュー進捗バーを更新
  function updateProgress() {
    if (__totalFiles__ <= 0) return;
    // Merge saved state with current DOM to include lazy-loaded sections
    // 遅延ロードセクションも含めるため、保存済み状態と現在のDOMをマージ
    var saved = __savedState__ || readSavedStateFromStorage('{}') || {};
    document.querySelectorAll('input[type="checkbox"][id^="cb_"]').forEach(function(cb) {
      saved[cb.id] = cb.checked;
    });
    var checked = 0;
    Object.keys(saved).forEach(function(k) {
      if (k.indexOf('cb_') !== 0 || !saved[k]) return;
      // Exclude Unchanged/Ignored from progress count / Unchanged/Ignoredは進捗カウントから除外
      if (k.indexOf('cb_unch_') === 0 || k.indexOf('cb_ign_') === 0) return;
      checked++;
    });
    var pct = Math.min(100, checked / __totalFiles__ * 100);
    var bar = document.getElementById('progress-bar-fill');
    var txt = document.getElementById('progress-text');
    if (bar) {
      bar.style.width = pct + '%';
      if (checked >= __totalFiles__) bar.classList.add('complete');
      else bar.classList.remove('complete');
    }
    if (txt) txt.textContent = checked + ' / ' + __totalFiles__ + ' reviewed';
    var det = document.getElementById('progress-detail');
    if (det && __totalFilesDetail__) det.textContent = '(' + __totalFilesDetail__ + ')';
  }

  async function downloadReviewed() {
    // 0. Force-decode all lazy sections so their inputs are captured in state
    // 全lazyセクションを強制デコードし、inputが状態に含まれるようにする
    forceDecodeLazySections();
    var state   = collectState();
    var slug    = 'diff_report_' + __reportDate__;
    var root    = document.documentElement;
    // 1. Collapse all diff-detail elements so exported file starts with diffs closed
    var openDetails = Array.from(document.querySelectorAll('details[open]'));
    openDetails.forEach(function(d){ d.removeAttribute('open'); });
    // 2. Clear all filter-hidden state so reviewed HTML shows all rows
    document.querySelectorAll('tr.filter-hidden').forEach(function(tr){ tr.classList.remove('filter-hidden'); });
    document.querySelectorAll('tr.filter-hidden-parent').forEach(function(tr){ tr.classList.remove('filter-hidden-parent'); });
    // 2b. Clear inline table widths so syncTableWidths recalculates on reviewed load
    // テーブルの inline width をクリアし reviewed ロード時に再計算させる
    document.querySelectorAll('table[style]').forEach(function(t){ t.style.removeProperty('width'); });
    // 3. Capture current effective column widths to bake into reviewed HTML as defaults
    var colVarNames = ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w'];
    var cs = getComputedStyle(root);
    var curWidths = {};
    colVarNames.forEach(function(v){ curWidths[v] = (root.style.getPropertyValue(v) || cs.getPropertyValue(v)).trim(); });
    var html    = document.documentElement.outerHTML;
    // Restore live page state / ライブページの状態を復元
    openDetails.forEach(function(d){ d.setAttribute('open', ''); });
    applyFilters();
    syncTableWidths();
    // Embed state
    html = html.replace('const __savedState__  = null;',
      'const __savedState__  = ' + JSON.stringify(state) + ';');
    // Update title
    html = html.replace('<title>diff_report</title>',
      '<title>' + slug + '_reviewed</title>');
    // Bake current column widths as CSS defaults in the reviewed HTML
    html = html.replace(/:root \{ --col-reason-w:[^}]+\}/,
      ':root { --col-reason-w: '  + curWidths['--col-reason-w']
      + '; --col-notes-w: '   + curWidths['--col-notes-w']
      + '; --col-path-w: '    + curWidths['--col-path-w']
      + '; --col-diff-w: '    + curWidths['--col-diff-w']
      + '; --col-disasm-w: '  + curWidths['--col-disasm-w']
      + '; --sc-class-w: '    + curWidths['--sc-class-w']
      + '; --sc-basetype-w: ' + curWidths['--sc-basetype-w']
      + '; --sc-type-w: '     + curWidths['--sc-type-w']
      + '; --sc-name-w: '     + curWidths['--sc-name-w']
      + '; --sc-rettype-w: '  + curWidths['--sc-rettype-w']
      + '; --sc-params-w: '   + curWidths['--sc-params-w']
      + '; --sc-body-w: '     + curWidths['--sc-body-w'] + '; }');
    // Remove inline col-var overrides from <html> element (now baked into :root)
    html = html.replace(/(<html\b[^>]*?) style="[^"]*"/, '$1');
    // Replace button row with reviewed banner (filter zone is preserved outside CTRL markers)
    // ボタン行を reviewed バナーに置換（フィルターゾーンは CTRL マーカー外なので維持）
    html = html.replace(/<!--CTRL-->[\s\S]*?<!--\/CTRL-->/g,
      '<div class="reviewed-banner" role="banner"><span>Reviewed: ' + formatTs(new Date()) + ' &#x2014; read-only</span>'
      + '<div class="reviewed-banner-actions">'
      + '<button class="btn" onclick="verifyIntegrity()" style="font-size:12px">&#x2713; Verify integrity</button>'
      + '<button class="btn" onclick="downloadExcelCompatibleHtml()" style="font-size:12px"><svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><rect x="1" y="1" width="14" height="14" rx="1.5"/><line x1="1" y1="5" x2="15" y2="5"/><line x1="1" y1="9" x2="15" y2="9"/><line x1="6" y1="1" x2="6" y2="15"/><line x1="11" y1="1" x2="11" y2="15"/></svg> Download as Excel-compatible HTML</button>'
      + '<button class="btn" onclick="downloadAsPdf()" style="font-size:12px"><svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><path d="M4 1h6l4 4v10H2V1h2z"/><polyline points="10,1 10,5 14,5"/><line x1="5" y1="8" x2="11" y2="8"/><line x1="5" y1="10.5" x2="11" y2="10.5"/><line x1="5" y1="13" x2="9" y2="13"/></svg> Download as PDF</button>'
      + '<button class="btn btn-clear" onclick="collapseAll()" style="font-size:12px">Fold all details</button>'
      + '<button class="btn btn-clear" onclick="resetFilters()" style="font-size:12px"><svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><path d="M2 3h12l-4 5v3l-4 2V8z"/><line x1="10" y1="10" x2="15" y2="15"/><line x1="15" y1="10" x2="10" y2="15"/></svg> Reset filters</button>'
      + '</div></div>');
    // 3. Embed SHA256 integrity hash for self-verification (placeholder approach)
    var placeholder = '0000000000000000000000000000000000000000000000000000000000000000';
    html = html.replace('const __reviewedSha256__  = null;',
      "const __reviewedSha256__  = '" + placeholder + "';");
    // Compute SHA256 of the HTML with placeholder
    var reviewedFileName = slug + '_reviewed.html';
    var encoded = new TextEncoder().encode(html);
    var hashBuffer = await crypto.subtle.digest('SHA-256', encoded);
    var hashArray = Array.from(new Uint8Array(hashBuffer));
    var hashHex = hashArray.map(function(b) { return b.toString(16).padStart(2, '0'); }).join('');
    // Replace placeholder with actual hash (same length, so no byte offset change)
    html = html.replace(placeholder, hashHex);
    // 4. Embed final SHA256 hash for .sha256 file verification (second placeholder approach)
    var placeholder2 = 'ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff';
    html = html.replace("const __finalSha256__     = null;",
      "const __finalSha256__     = '" + placeholder2 + "';");
    var finalEncoded = new TextEncoder().encode(html);
    var finalHashBuffer = await crypto.subtle.digest('SHA-256', finalEncoded);
    var finalHashArray = Array.from(new Uint8Array(finalHashBuffer));
    var finalHashHex = finalHashArray.map(function(b) { return b.toString(16).padStart(2, '0'); }).join('');
    // Replace placeholder2 with actual final hash (same length, so no byte offset change)
    html = html.replace(placeholder2, finalHashHex);
    // Download reviewed HTML
    var blob = new Blob([html], { type: 'text/html;charset=utf-8' });
    var a    = document.createElement('a');
    a.href   = URL.createObjectURL(blob);
    a.download = reviewedFileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    // Download companion SHA256 verification file (brief delay avoids browser download blocking)
    setTimeout(function(){
      var sha256Text = finalHashHex + '  ' + reviewedFileName + '\n';
      var sha256Blob = new Blob([sha256Text], { type: 'text/plain;charset=utf-8' });
      var a2 = document.createElement('a');
      a2.href = URL.createObjectURL(sha256Blob);
      a2.download = reviewedFileName + '.sha256';
      document.body.appendChild(a2);
      a2.click();
      document.body.removeChild(a2);
      setTimeout(function(){ URL.revokeObjectURL(a.href); URL.revokeObjectURL(a2.href); }, 1000);
    }, 500);
    // Display hash in save-status for manual verification
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'SHA256: ' + hashHex;
  }

  function verifyIntegrity() {
    if (__finalSha256__ === null) {
      alert('This report has not been downloaded as reviewed yet.');
      return;
    }
    var input = document.getElementById('__verifyInput__');
    input.value = '';
    input.onchange = async function() {
      var file = input.files[0];
      if (!file) return;
      if (!file.name.endsWith('.sha256')) {
        alert('Please select a .sha256 file.');
        return;
      }
      var sha256Text = await file.text();
      var match = sha256Text.match(/^([0-9a-f]{64})\b/i);
      if (!match) {
        alert('Invalid .sha256 file format.\nExpected: <hash>  <filename>');
        return;
      }
      var fileHash = match[1].toLowerCase();
      if (fileHash === __finalSha256__) {
        alert('Integrity verification passed.\nThe .sha256 file matches this report.');
      } else {
        alert('Integrity verification FAILED.\nThe .sha256 file does not match this report.'
          + '\n\nEmbedded: ' + __finalSha256__
          + '\n.sha256:  ' + fileHash);
      }
    };
    input.click();
  }

  function collapseAll() {
    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });
    autoSave();
  }

  function clearAll() {
    if (!confirm('Clear all checkboxes and text inputs?')) return;
    document.querySelectorAll('input[type="checkbox"]').forEach(function(cb){
      // Filter checkboxes → checked (no filter); other checkboxes → unchecked
      // フィルターチェックボックス → checked（フィルターなし）、その他 → unchecked
      cb.checked = (__filterIds__.indexOf(cb.id) >= 0 && cb.id !== 'filter-unchecked');
    });
    document.querySelectorAll('input[type="text"], textarea').forEach(function(inp){ inp.value=''; });
    // Reset column widths to defaults
    var root = document.documentElement;
    ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w'].forEach(function(v){ root.style.removeProperty(v); });
    syncTableWidths();
    // Close all open diff/IL-diff details
    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });
    applyFilters();
    localStorage.removeItem(__storageKey__);
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'Cleared.';
  }

  function decodeDiffHtml(b64) {
    var binary = atob(b64);
    var bytes = new Uint8Array(binary.length);
    for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return new TextDecoder('utf-8').decode(bytes);
  }

  // Toggle between unified and side-by-side diff view / 統合ビューとサイドバイサイドビューを切り替え
  // 3-column layout: [LineNum] [Old/Red] [New/Green]
  // Red and green columns each get exactly (table_width - line_number_width) / 2
  // 3列レイアウト: [行番号] [旧/赤] [新/緑] — 赤と緑は (テーブル幅 - 行番号列幅) / 2 で等幅、隙間なし
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
  }

  // Lazy-render: diff table HTML is stored base64-encoded in data-diff-html.
  // On first open, decode and insert into DOM, then remove the attribute.
  function setupLazyDiff() {
    document.querySelectorAll('details[data-diff-html]').forEach(function(d) {
      d.addEventListener('toggle', function onToggle() {
        if (!d.open) return;
        var b64 = d.getAttribute('data-diff-html');
        if (!b64) return;
        d.removeAttribute('data-diff-html');
        d.removeEventListener('toggle', onToggle);
        try {
          d.insertAdjacentHTML('beforeend', decodeDiffHtml(b64));
          // Insert side-by-side toggle button before each diff-table / 各diff-tableの前にサイドバイサイド切り替えボタンを挿入
          d.querySelectorAll('.diff-table').forEach(function(tbl) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'sbs-toggle';
            btn.textContent = 'Side-by-side';
            btn.addEventListener('click', function() { toggleDiffView(d); });
            tbl.parentNode.insertBefore(btn, tbl);
          });
          // Re-init column resize handles on newly rendered tables
          d.querySelectorAll('th.th-resizable').forEach(function(th) {
            if (th.querySelector('.col-resize-handle')) return;
            initColResizeSingle(th);
          });
          syncScTableWidths();
          // Wire up save events on new checkboxes
          if (__savedState__ === null) {
            d.querySelectorAll('input').forEach(function(el) {
              el.addEventListener('change', autoSave);
            });
          }
          // Restore state for new inputs
          var toRestore = __savedState__ || readSavedStateFromStorage('null');
          if (toRestore) {
            d.querySelectorAll('input[id]').forEach(function(el) {
              if (el.id in toRestore) {
                if (el.type === 'checkbox') el.checked = Boolean(toRestore[el.id]);
                else el.value = String(toRestore[el.id] || '');
              }
            });
          }
          if (__savedState__ !== null) {
            d.querySelectorAll('input[type="checkbox"]').forEach(function(cb){ cb.style.pointerEvents='none'; cb.style.cursor='default'; });
          }
          updateProgress();
          // Apply current importance filters to newly rendered semantic change rows
          // 新規レンダリングされたセマンティック変更行に現在の重要度フィルターを適用
          applyFilters();
        } catch(e) {}
      });
    });
  }

  // Lazy-render: section tables (Ignored/Unchanged) stored Base64-encoded in data-lazy-section.
  // On first open, decode and insert into DOM. / 遅延レンダリング: セクションテーブルをBase64で格納し初回展開時にデコード挿入。
  function setupLazySection() {
    document.querySelectorAll('details[data-lazy-section]').forEach(function(d) {
      d.addEventListener('toggle', function onToggle() {
        if (!d.open) return;
        var b64 = d.getAttribute('data-lazy-section');
        if (!b64) return;
        d.removeAttribute('data-lazy-section');
        d.removeEventListener('toggle', onToggle);
        try {
          d.insertAdjacentHTML('beforeend', decodeDiffHtml(b64));
          // Init column resize handles and sync widths / 列リサイズハンドル初期化と幅同期
          d.querySelectorAll('th.th-resizable').forEach(function(th) {
            if (th.querySelector('.col-resize-handle')) return;
            initColResizeSingle(th);
          });
          syncTableWidths();
          // Wire up save events on new inputs / 新規inputにsaveイベントを接続
          if (__savedState__ === null) {
            d.querySelectorAll('input, textarea').forEach(function(el) {
              el.addEventListener('change', autoSave);
              el.addEventListener('input',  autoSave);
            });
          }
          // Restore state for new inputs / 新規inputの状態を復元
          var toRestore = __savedState__ || readSavedStateFromStorage('null');
          if (toRestore) {
            d.querySelectorAll('input[id], textarea[id]').forEach(function(el) {
              if (toRestore[el.id] === undefined) return;
              if (el.type === 'checkbox') el.checked = Boolean(toRestore[el.id]);
              else el.value = String(toRestore[el.id] || '');
            });
          }
          if (__savedState__ !== null) {
            d.querySelectorAll('input[type="checkbox"]').forEach(function(cb){ cb.style.pointerEvents='none'; cb.style.cursor='default'; });
            d.querySelectorAll('input[type="text"]').forEach(function(inp){ inp.readOnly=true; inp.style.cursor='text'; inp.style.userSelect='text'; });
          }
          updateProgress();
        } catch(e) {}
      });
    });
  }

  // Force-decode all lazy sections (used before downloadReviewed captures HTML)
  // 全lazyセクションを強制デコード（downloadReviewed前にHTML取得用）
  function forceDecodeLazySections() {
    document.querySelectorAll('details[data-lazy-section]').forEach(function(d) {
      var b64 = d.getAttribute('data-lazy-section');
      if (!b64) return;
      d.removeAttribute('data-lazy-section');
      try {
        d.insertAdjacentHTML('beforeend', decodeDiffHtml(b64));
        syncTableWidths();
      } catch(e) {}
    });
  }

  function syncTableWidths() {
    var root = document.documentElement;
    var emPx = parseFloat(getComputedStyle(root).fontSize) || 16;
    var px = function(v, fb) {
      var s = root.style.getPropertyValue(v) || getComputedStyle(root).getPropertyValue(v);
      return (parseFloat(s) || fb) * emPx;
    };
    var colW = {
      'col-no-g': 3.2 * emPx,
      'col-cb-g': 2.2 * emPx,
      'col-reason-g': px('--col-reason-w', 10),
      'col-notes-g': px('--col-notes-w', 10),
      'col-status-g': 4.3 * emPx,
      'col-path-g': px('--col-path-w', 22),
      'col-ts-g': 28 * emPx,
      'col-diff-g': px('--col-diff-w', 10.8),
      'col-disasm-g': px('--col-disasm-w', 28)
    };
    document.querySelectorAll('table:not(.stat-table):not(.diff-table):not(.semantic-changes-table):not(.legend-table):not(.il-ignore-table)').forEach(function(t) {
      var cg = t.querySelector('colgroup');
      if (!cg) return;
      var hideDisasm = t.classList.contains('hide-disasm');
      var hideCol6 = t.classList.contains('hide-col6');
      var w = 0;
      cg.querySelectorAll('col').forEach(function(col) {
        if (hideDisasm && col.classList.contains('col-disasm-g')) return;
        if (hideCol6 && col.classList.contains('col-diff-g')) return;
        if (colW[col.className] !== undefined) w += colW[col.className];
      });
      if (w > 0) t.style.width = w + 'px';
    });
  }

  function syncScTableWidths() {
    var scEmPx = 12;
    var root = document.documentElement;
    var px = function(v, fb) {
      var s = root.style.getPropertyValue(v) || getComputedStyle(root).getPropertyValue(v);
      return (parseFloat(s) || fb) * scEmPx;
    };
    var detW = 3.2 * scEmPx
            + px('--sc-class-w', 14) + px('--sc-basetype-w', 16)
            + 7 * scEmPx + 7 * scEmPx + 10 * scEmPx + 8 * scEmPx + 11 * scEmPx
            + px('--sc-type-w', 12) + px('--sc-name-w', 10)
            + px('--sc-rettype-w', 12) + px('--sc-params-w', 18)
            + px('--sc-body-w', 5);
    document.querySelectorAll('table.sc-detail').forEach(function(t) { t.style.width = detW + 'px'; });
    // dc-detail (dependency changes): cb(3.2) + package(24) + status(7) + importance(7) + oldVer(12) + newVer(12) = 65.2em
    // dc-detail のチェック列・Status列を sc-detail と同じ実幅にするため、列幅合計に一致する幅を設定
    var dcW = (3.2 + 24 + 7 + 7 + 12 + 12) * scEmPx;
    document.querySelectorAll('table.dc-detail').forEach(function(t) { t.style.width = dcW + 'px'; });
  }

  function initColResizeSingle(th) {
    var label = document.createElement('span');
    label.className = 'th-label';
    while (th.childNodes.length) label.appendChild(th.childNodes[0]);
    th.appendChild(label);
    var handle = document.createElement('div');
    handle.className = 'col-resize-handle';
    th.appendChild(handle);
    var varName = th.dataset.colVar;
    var isSc = !!th.closest('.semantic-changes-table');
    handle.addEventListener('mousedown', function(e) {
      e.preventDefault();
      var startX = e.clientX;
      var root   = document.documentElement;
      var emPx   = isSc ? 12 : (parseFloat(getComputedStyle(root).fontSize) || 16);
      var cur    = root.style.getPropertyValue(varName) || getComputedStyle(root).getPropertyValue(varName);
      var startPx = (parseFloat(cur) || 10) * emPx;
      function onMove(ev) {
        var newPx = Math.max(48, startPx + (ev.clientX - startX));
        root.style.setProperty(varName, (newPx / emPx).toFixed(2) + 'em');
        syncTableWidths();
        if (isSc) syncScTableWidths();
      }
      function onUp() {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup', onUp);
      }
      document.addEventListener('mousemove', onMove);
      document.addEventListener('mouseup', onUp);
    });
  }

  // Measure a Diff Detail body row and set --ft-row-h so filter-table-dbl rows are exactly 2× / Diff Detail 行高さを計測し --ft-row-h を設定
  function syncFilterRowHeight() {
    var base = document.querySelector('table.filter-table:not(.filter-table-dbl) tbody tr');
    if (!base) return;
    var h = base.getBoundingClientRect().height;
    if (h > 0) document.documentElement.style.setProperty('--ft-row-h', h + 'px');
  }

  // Wrap td text inputs with clear button / td テキスト入力にクリアボタンを付与
  function wrapInputWithClear(inp) {
    if (inp.parentElement.classList.contains('input-wrap') || inp.parentElement.classList.contains('filter-search-wrap')) return;
    var isSearch = inp.classList.contains('filter-search');
    var wrap = document.createElement('div');
    wrap.className = isSearch ? 'filter-search-wrap' : 'input-wrap';
    inp.parentNode.insertBefore(wrap, inp);
    wrap.appendChild(inp);
    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'btn-input-clear';
    btn.tabIndex = -1;
    btn.title = 'Clear';
    btn.setAttribute('aria-label', 'Clear');
    btn.innerHTML = '<svg aria-hidden="true" width="8" height="8" viewBox="0 0 8 8" stroke="currentColor" stroke-width="1.5" fill="none"><line x1="1" y1="1" x2="7" y2="7"/><line x1="7" y1="1" x2="1" y2="7"/></svg>';
    wrap.appendChild(btn);
    function sync() { wrap.classList.toggle('has-text', inp.value.length > 0); }
    inp.addEventListener('input', sync);
    btn.addEventListener('click', function() {
      inp.value = '';
      sync();
      inp.dispatchEvent(new Event('input', { bubbles: true }));
      inp.dispatchEvent(new Event('change', { bubbles: true }));
      inp.focus();
    });
    sync();
  }

  function initClearButtons() {
    document.querySelectorAll('input#filter-search').forEach(wrapInputWithClear);
  }

  function initColResize() {
    document.querySelectorAll('th.th-resizable').forEach(function(th) {
      initColResizeSingle(th);
    });
  }

  // ── Filtering ──────────────────────────────────────────────────────────
  function applyFilters() {
    var impHigh    = document.getElementById('filter-imp-high');
    // If any element is missing (e.g. reviewed mode), skip / 要素がない場合（レビュー済みモード等）スキップ
    if (!impHigh) return;

    // Helper: read checkbox state, default to true if element is missing / ヘルパー: チェックボックス状態取得、要素がなければ true
    function chk(id) { var el = document.getElementById(id); return el ? el.checked : true; }

    // Diff detail filter (6 individual values) / Diff Detail フィルター（6個別値）
    var diffFilter = {
      SHA256Match:    chk('filter-diff-sha256match'),
      SHA256Mismatch: chk('filter-diff-sha256mismatch'),
      ILMatch:        chk('filter-diff-ilmatch'),
      ILMismatch:     chk('filter-diff-ilmismatch'),
      TextMatch:      chk('filter-diff-textmatch'),
      TextMismatch:   chk('filter-diff-textmismatch')
    };
    var impFilter  = { High: impHigh.checked, Medium: chk('filter-imp-medium'), Low: chk('filter-imp-low') };
    var onlyUnchecked = document.getElementById('filter-unchecked') ? document.getElementById('filter-unchecked').checked : false;
    var searchEl   = document.getElementById('filter-search');
    var searchText = searchEl ? (searchEl.value || '').toLowerCase().trim() : '';

    // All checked = no filtering for that category / 全チェック時はフィルタリングなし
    var diffActive = !(diffFilter.SHA256Match && diffFilter.SHA256Mismatch && diffFilter.ILMatch && diffFilter.ILMismatch && diffFilter.TextMatch && diffFilter.TextMismatch);
    var impActive  = !(impFilter.High && impFilter.Medium && impFilter.Low);

    document.querySelectorAll('tbody > tr[data-section]').forEach(function(tr) {
      var show = true;
      // Diff detail filter (exact value match)
      if (diffActive) {
        var diff = tr.getAttribute('data-diff');
        if (diff) {
          if (!diffFilter[diff]) show = false;
        }
      }
      // Importance filter (only for rows with importance) / 重要度フィルター（importance属性ありの行のみ）
      if (show && impActive) {
        var imps = tr.getAttribute('data-importances');
        if (imps) {
          // Show if ANY importance level passes the filter / いずれかの重要度レベルがフィルターを通過すれば表示
          var levels = imps.split(',');
          var anyMatch = levels.some(function(l) { return impFilter[l]; });
          if (!anyMatch) show = false;
        } else {
          var imp = tr.getAttribute('data-importance');
          if (imp) {
            if (!impFilter[imp]) show = false;
          }
        }
      }
      // Unchecked only filter
      if (show && onlyUnchecked) {
        var cb = tr.querySelector('input[type="checkbox"]');
        if (cb && cb.checked) show = false;
      }
      // Search filter
      if (show && searchText) {
        var pathEl = tr.querySelector('.path-text');
        var pathText = pathEl ? pathEl.textContent.toLowerCase() : '';
        if (pathText.indexOf(searchText) < 0) show = false;
      }
      if (show) {
        tr.classList.remove('filter-hidden');
      } else {
        tr.classList.add('filter-hidden');
      }
      // Hide associated diff-row/semantic-changes rows (but keep details elements visible)
      var next = tr.nextElementSibling;
      while (next && !next.hasAttribute('data-section')) {
        if (show) {
          next.classList.remove('filter-hidden-parent');
        } else {
          next.classList.add('filter-hidden-parent');
        }
        next = next.nextElementSibling;
      }
    });

    // Filter semantic change rows by importance inside detail tables / detail テーブル内の semantic change 行を importance でフィルター
    if (impActive) {
      document.querySelectorAll('.semantic-changes-table tr[data-sc-importance]').forEach(function(tr) {
        var imp = tr.getAttribute('data-sc-importance');
        if (imp && !impFilter[imp]) {
          tr.classList.add('filter-hidden');
        } else {
          tr.classList.remove('filter-hidden');
        }
      });
    } else {
      document.querySelectorAll('.semantic-changes-table tr.filter-hidden').forEach(function(tr) {
        tr.classList.remove('filter-hidden');
      });
    }
    // Fix group-cont headers: when a group header row is hidden, promote the first visible
    // continuation row to show typename/basetype so it doesn't appear orphaned.
    // グループヘッダー行が非表示時、最初の可視 group-cont 行に typename/basetype を復元
    document.querySelectorAll('.semantic-changes-table.sc-detail tbody').forEach(function(tbody) {
      var rows = tbody.querySelectorAll('tr[data-sc-typename]');
      var prevType = '';
      for (var i = 0; i < rows.length; i++) {
        var r = rows[i];
        if (r.classList.contains('filter-hidden')) continue;
        var tn = r.getAttribute('data-sc-typename') || '';
        if (tn !== prevType) {
          // First visible row for this type — ensure it shows typename/basetype
          // この型の最初の可視行 — typename/basetype を表示
          r.classList.remove('group-cont');
          var cells = r.querySelectorAll('td');
          if (cells.length >= 3) {
            cells[1].textContent = tn;
            cells[2].textContent = r.getAttribute('data-sc-basetype') || '';
          }
        } else {
          // Continuation — restore group-cont styling and clear typename/basetype cells
          // 継続行 — group-cont スタイルを復元し typename/basetype セルをクリア
          r.classList.add('group-cont');
          var cells = r.querySelectorAll('td');
          if (cells.length >= 3) {
            cells[1].textContent = '';
            cells[2].textContent = '';
          }
        }
        prevType = tn;
      }
    });

    autoSave();
  }
  function resetFilters() {
    __filterIds__.forEach(function(id) {
      if (id === 'filter-unchecked' || id === 'filter-search') return;
      var el = document.getElementById(id);
      if (el) el.checked = true;
    });
    var uc = document.getElementById('filter-unchecked');
    if (uc) uc.checked = false;
    var se = document.getElementById('filter-search');
    if (se) se.value = '';
    applyFilters();
  }

  function copyPath(btn) {
    var td = btn.closest('td');
    if (!td) return;
    var span = td.querySelector('.path-text');
    var text = span ? span.textContent.trim() : td.textContent.trim();
    if (!text) return;
    navigator.clipboard.writeText(text).then(function() {
      var svg = btn.querySelector('svg');
      if (svg) { svg.style.display='none'; btn.textContent='\u2713'; }
      setTimeout(function() {
        if (svg) { btn.textContent=''; btn.appendChild(svg); svg.style.display=''; }
      }, 1200);
    });
  }

  // ── PDF export via browser print / ブラウザ印刷による PDF エクスポート ──
  // Injects fixed-position header/footer elements and triggers window.print().
  // 固定位置のヘッダー/フッター要素を注入し、window.print() を呼び出します。
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

  // ── Excel export ────────────────────────────────────────────────────
  // Generates an Excel-compatible HTML table file from the current report data.
  // 現在のレポートデータから Excel 互換 HTML テーブルファイルを生成します。
  function downloadExcelCompatibleHtml() {
    // Force-decode all lazy sections to capture all data / 全lazyセクションを強制デコードしデータを取得
    forceDecodeLazySections();

    var sectionNames = {
      'ign': '[ x ] Ignored Files', 'unch': '[ = ] Unchanged Files',
      'add': '[ + ] Added Files', 'rem': '[ - ] Removed Files', 'mod': '[ * ] Modified Files',
      'sha256w': '[ ! ] Modified Files \u2014 SHA256Mismatch: binary diff only \u2014 not a .NET assembly or disassembler unavailable',
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

    // Helper: 11-cell empty row / 11セルの空行
    var COLS = 11;
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
      var hdrs = ['#', '\u2713', 'Justification', 'Notes', 'Status', 'File Path', 'Timestamp', 'Diff Reason', 'Disassembler'];
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

  function buildExcelRow(tr) {
    var cells = tr.querySelectorAll('td');
    if (cells.length < 9) return '';
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
    // Disassembler
    var disasm = cells[8].textContent.trim();

    return '<tr><td></td><td></td>'
      + '<td class="bd">' + esc(no) + '</td>'
      + '<td class="bd">' + esc(checked) + '</td>'
      + '<td class="bd">' + esc(reason) + '</td>'
      + '<td class="bd">' + esc(notes) + '</td>'
      + '<td class="bd" style="text-align:center">' + esc(status) + '</td>'
      + '<td class="bd">' + esc(path) + '</td>'
      + '<td class="bd" style="text-align:center;mso-number-format:\'\\@\'">' + esc(ts) + '</td>'
      + '<td class="bd" style="text-align:center">' + esc(diff) + '</td>'
      + '<td class="bd">' + esc(disasm) + '</td>'
      + '</tr>';
  }

  function esc(s) {
    if (!s) return '';
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  // ── Keyboard navigation (WCAG 2.1 AA) ────────────────────────────────
  // Escape closes the nearest open detail element when focus is inside it.
  // Escapeキーでフォーカス中のdetail要素を閉じる。
  document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
      var details = document.activeElement ? document.activeElement.closest('details[open]') : null;
      if (details) {
        details.removeAttribute('open');
        var summary = details.querySelector('summary');
        if (summary) summary.focus();
        e.preventDefault();
      }
    }
  });
