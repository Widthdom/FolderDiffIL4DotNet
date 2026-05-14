  /** Synchronize main data table widths based on CSS variable column widths and font size. */
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
      'col-ts-g': px('--col-ts-w', 29),
      'col-diff-g': px('--col-diff-w', 10.8),
      'col-tag-g': px('--col-tag-w', 14),
      'col-disasm-g': px('--col-disasm-w', 28),
      'col-sdk-g': px('--col-sdk-w', 14),
      'col-checklist-cb-g': 2.2 * emPx,
      'col-checklist-item-g': px('--col-checklist-item-w', 30),
      'col-checklist-notes-g': px('--col-checklist-notes-w', 20)
    };
    document.querySelectorAll('table:not(.stat-table):not(.diff-table):not(.semantic-changes-table):not(.legend-table):not(.il-ignore-table)').forEach(function(t) {
      var cg = t.querySelector('colgroup');
      if (!cg) return;
      var hideDisasm = t.classList.contains('hide-disasm');
      var hideCol6 = t.classList.contains('hide-col6');
      var hideTag = t.classList.contains('hide-tag');
      var hideSdk = t.classList.contains('hide-sdk');
      var w = 0;
      cg.querySelectorAll('col').forEach(function(col) {
        if (hideDisasm && col.classList.contains('col-disasm-g')) return;
        if (hideCol6 && col.classList.contains('col-diff-g')) return;
        if (hideTag && col.classList.contains('col-tag-g')) return;
        if (hideSdk && col.classList.contains('col-sdk-g')) return;
        if (colW[col.className] !== undefined) w += colW[col.className];
      });
      if (w > 0) t.style.width = w + 'px';
    });
  }

  /** Synchronize semantic-changes and dependency-changes table widths. */
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
    // dc-detail (dependency changes): base = cb(3.2) + package(24) + status(7) + importance(7) + oldVer(12) + newVer(12) = 65.2em
    // Conditionally add vuln(18) and refs(var) columns if present in the table's colgroup
    // dc-detail の基本幅に、テーブルごとに vuln 列・refs 列が存在すれば加算
    document.querySelectorAll('table.dc-detail').forEach(function(t) {
      var dcW = (3.2 + 24 + 7 + 7 + 12 + 12) * scEmPx;
      if (t.querySelector('col.dc-col-vuln-g')) dcW += 18 * scEmPx;
      if (t.querySelector('col.dc-col-refs-g')) dcW += px('--dc-refs-w', 16);
      t.style.width = dcW + 'px';
    });
  }

  /**
   * Initialize column resize handle on a single resizable table header.
   * @param {HTMLTableCellElement} th - A th element with class 'th-resizable'
   */
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
        // Persist column widths to localStorage / カラム幅を localStorage に永続化
        saveColumnWidths();
      }
      document.addEventListener('mousemove', onMove);
      document.addEventListener('mouseup', onUp);
    });
  }

  /** CSS variable names for resizable columns. / リサイズ可能カラムの CSS 変数名。 */
  var __colVarNames__ = [
    '--col-reason-w','--col-notes-w','--col-path-w','--col-ts-w','--col-diff-w',
    '--col-tag-w','--col-disasm-w','--col-sdk-w',
    '--col-checklist-item-w','--col-checklist-notes-w',
    '--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w',
    '--sc-rettype-w','--sc-params-w','--sc-body-w','--dc-refs-w'
  ];

  /**
   * Save current column widths (CSS custom properties) to localStorage.
   * カラム幅（CSSカスタムプロパティ）を localStorage に保存する。
   */
  function saveColumnWidths() {
    if (__savedState__ !== null) return; // Do not persist in reviewed mode / レビュー済みモードでは保存しない
    try {
      var widths = {};
      var root = document.documentElement;
      __colVarNames__.forEach(function(v) {
        var val = root.style.getPropertyValue(v);
        if (val) widths[v] = val;
      });
      localStorage.setItem(__storageKey__ + '-colwidths', JSON.stringify(widths));
    } catch(e) { /* ignore quota errors */ }
  }

  /**
   * Restore column widths from localStorage and apply to CSS custom properties.
   * localStorage からカラム幅を復元し CSS カスタムプロパティに適用する。
   */
  function restoreColumnWidths() {
    try {
      var raw = localStorage.getItem(__storageKey__ + '-colwidths');
      if (!raw) return;
      var widths = JSON.parse(raw);
      if (!widths || typeof widths !== 'object') return;
      var root = document.documentElement;
      Object.keys(widths).forEach(function(v) {
        root.style.setProperty(v, widths[v]);
      });
    } catch(e) { /* ignore */ }
  }

  /** Measure a Diff Detail body row height and set --ft-row-h for double-height filter rows. */
  function syncFilterRowHeight() {
    var base = document.querySelector('table.filter-table:not(.filter-table-dbl) tbody tr');
    if (!base) return;
    var h = base.getBoundingClientRect().height;
    if (h > 0) document.documentElement.style.setProperty('--ft-row-h', h + 'px');
  }

  /**
   * Wrap a text input with a container div and append a clear (×) button.
   * @param {HTMLInputElement} inp
   */
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

  /** Initialize clear buttons for the search input field. */
  function initClearButtons() {
    document.querySelectorAll('input#filter-search').forEach(wrapInputWithClear);
  }

  /** Initialize column resize handles on all resizable table headers. */
  function initColResize() {
    document.querySelectorAll('th.th-resizable').forEach(function(th) {
      initColResizeSingle(th);
    });
  }

  /* Export functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { syncTableWidths: syncTableWidths, syncScTableWidths: syncScTableWidths, saveColumnWidths: saveColumnWidths, restoreColumnWidths: restoreColumnWidths }; }
