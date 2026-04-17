  document.addEventListener('DOMContentLoaded', function() {
    initTheme();
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
        if (el.classList.contains('cb-all') || el.classList.contains('cb-all-detail')) return;
        el.addEventListener('change', autoSave);
        el.addEventListener('input',  autoSave);
      });
    }
    // Restore persisted filter state (before first applyFilters) / 永続化されたフィルター状態を復元
    if (__savedState__ === null) {
      restoreFilterState();
    }
    initColResize();
    if (__savedState__ === null) {
      restoreColumnWidths();
    }
    syncTableWidths();
    syncScTableWidths();
    syncFilterRowHeight();
    initClearButtons();
    setupLazyDiff();
    setupLazySection();
    setupLazyIntersectionObserver();
    highlightAllILDiffs();
    applyFilters();
    updateProgress();
    syncHeaderCheckboxes();
    updateStorageUsage();
    // Custom tooltip hover/focus handling / カスタムツールチップのホバー・フォーカス処理
    document.querySelectorAll('.btn-tooltip-wrap').forEach(function(wrap) {
      var tip = wrap.querySelector('.btn-tooltip');
      var btn = wrap.querySelector('.btn');
      if (!tip || !btn) return;
      function show() { tip.classList.add('btn-tooltip-visible'); }
      function hide() { tip.classList.remove('btn-tooltip-visible'); }
      btn.addEventListener('mouseenter', show);
      btn.addEventListener('mouseleave', hide);
      btn.addEventListener('focus', show);
      btn.addEventListener('blur', hide);
    });
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

  // ── Keyboard navigation (WCAG 2.1 AA) ────────────────────────────────
  // Escape closes the nearest open detail element when focus is inside it.
  // Escapeキーでフォーカス中のdetail要素を閉じる。
  // Note: when focus is on a text input, diff_report_keyboard.js handles
  // Escape (blur) first and sets __kbEscHandled__ to skip this handler.
  // テキスト入力にフォーカスがある場合、diff_report_keyboard.js が先に
  // Escape（blur）を処理し __kbEscHandled__ でこのハンドラをスキップする。
  document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
      if (window.__kbEscHandled__) { window.__kbEscHandled__ = false; return; }
      var details = document.activeElement ? document.activeElement.closest('details[open]') : null;
      if (details) {
        details.removeAttribute('open');
        var summary = details.querySelector('summary');
        if (summary) summary.focus();
        e.preventDefault();
      }
    }
  });
