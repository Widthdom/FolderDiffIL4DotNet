  const __storageKey__  = '{{STORAGE_KEY}}';
  const __reportDate__  = '{{REPORT_DATE}}';
  // NOTE: 'const __savedState__ = null;' is replaced by downloadReviewed(). Do not change whitespace.
  const __savedState__  = null;
  // NOTE: replaced with SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __reviewedSha256__  = null;
  // NOTE: replaced with final SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __finalSha256__     = null;

  function formatTs(d) {
    return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')
      +' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0');
  }

  document.addEventListener('DOMContentLoaded', function() {
    var toRestore = __savedState__ || JSON.parse(localStorage.getItem(__storageKey__) || 'null');
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
    localStorage.setItem(__storageKey__, JSON.stringify(collectState()));
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'Auto-saved at ' + formatTs(new Date());
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
      '<div class="reviewed-banner"><span>Reviewed: ' + formatTs(new Date()) + ' &#x2014; read-only</span>'
      + ' <button class="btn" onclick="verifyIntegrity()" style="font-size:12px">&#x2713; Verify integrity</button>'
      + ' <button class="btn btn-clear" onclick="collapseAll()" style="font-size:12px">Fold all details</button>'
      + ' <button class="btn btn-clear" onclick="resetFilters()" style="font-size:12px"><svg width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><path d="M2 3h12l-4 5v3l-4 2V8z"/><line x1="10" y1="10" x2="15" y2="15"/><line x1="15" y1="10" x2="10" y2="15"/></svg> Reset filters</button>'
      + '</div>');
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
          var toRestore = __savedState__ || JSON.parse(localStorage.getItem(__storageKey__) || 'null');
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
          var toRestore = __savedState__ || JSON.parse(localStorage.getItem(__storageKey__) || 'null');
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
      'col-path-g': px('--col-path-w', 22),
      'col-ts-g': 28 * emPx,
      'col-diff-g': px('--col-diff-w', 9),
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
    btn.innerHTML = '<svg width="8" height="8" viewBox="0 0 8 8" stroke="currentColor" stroke-width="1.5" fill="none"><line x1="1" y1="1" x2="7" y2="7"/><line x1="7" y1="1" x2="1" y2="7"/></svg>';
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
