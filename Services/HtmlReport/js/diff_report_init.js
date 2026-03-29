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
