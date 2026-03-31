  (function() {
    /** @type {number} Current focused row index (-1 = none) */
    var _kbIndex = -1;
    /** @type {HTMLTableRowElement[]} Cached list of visible (non-hidden) file rows */
    var _kbRows  = [];

    /** Refresh the cached list of visible (non-hidden) file rows from the DOM. */
    function refreshRows() {
      _kbRows = Array.prototype.slice.call(
        document.querySelectorAll('tbody > tr[data-section]:not(.filter-hidden)')
      );
    }

    /**
     * Find the first visible row by section priority (add > rem > mod > ign > unch).
     * @returns {number} Row index in _kbRows
     */
    function findFirstRowByPriority() {
      var priorities = ['add', 'rem', 'mod', 'ign', 'unch'];
      for (var p = 0; p < priorities.length; p++) {
        for (var r = 0; r < _kbRows.length; r++) {
          if (_kbRows[r].getAttribute('data-section') === priorities[p]) return r;
        }
      }
      return 0;
    }

    /**
     * Ensure the row is visible by opening any collapsed parent detail elements.
     * @param {HTMLTableRowElement} row
     */
    function ensureVisible(row) {
      var el = row.parentElement;
      while (el) {
        if (el.tagName === 'DETAILS' && !el.open) {
          el.open = true;
        }
        el = el.parentElement;
      }
    }

    /** Apply keyboard focus highlight to the row at _kbIndex and scroll into view. */
    function applyFocus() {
      document.querySelectorAll('tr.kb-focus').forEach(function(el) {
        el.classList.remove('kb-focus');
      });
      if (_kbIndex < 0 || _kbIndex >= _kbRows.length) return;
      var row = _kbRows[_kbIndex];
      ensureVisible(row);
      row.classList.add('kb-focus');
      row.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      // Move DOM focus to the summary (if exists) or the row itself for Enter support
      // Enter対応のため、summaryがあればそこに、なければ行自体にDOMフォーカスを移す
      var summary = null;
      var next = row.nextElementSibling;
      if (next && !next.hasAttribute('data-section')) {
        summary = next.querySelector('summary');
      }
      if (summary) {
        summary.focus({ preventScroll: true });
      } else {
        // Ensure the row can receive focus / 行がフォーカスを受けられるようにする
        if (!row.getAttribute('tabindex')) row.setAttribute('tabindex', '-1');
        row.focus({ preventScroll: true });
      }
    }

    /**
     * Move keyboard focus by delta (+1 or -1). Clamps at list boundaries.
     * @param {number} delta - Direction to move: +1 for next, -1 for previous
     */
    function moveBy(delta) {
      refreshRows();
      if (_kbRows.length === 0) return;
      if (_kbIndex < 0) {
        _kbIndex = delta > 0 ? findFirstRowByPriority() : _kbRows.length - 1;
      } else {
        _kbIndex = Math.max(0, Math.min(_kbRows.length - 1, _kbIndex + delta));
      }
      applyFocus();
    }

    /** Toggle the review checkbox of the currently focused row (no-op in read-only mode). */
    function toggleCheck() {
      if (__savedState__ !== null) return; // Read-only mode / 読み取り専用モード
      if (_kbIndex < 0 || _kbIndex >= _kbRows.length) return;
      var cb = _kbRows[_kbIndex].querySelector('input[type="checkbox"]');
      if (cb) {
        cb.checked = !cb.checked;
        autoSave();
      }
    }

    /** Show or hide the keyboard shortcut help overlay. */
    function toggleHelp() {
      var overlay = document.getElementById('kb-help');
      if (!overlay) return;
      var isVisible = overlay.classList.contains('kb-help-visible');
      if (isVisible) {
        overlay.classList.remove('kb-help-visible');
        overlay.classList.add('kb-help-hidden');
      } else {
        overlay.classList.remove('kb-help-hidden');
        overlay.classList.add('kb-help-visible');
      }
    }

    /**
     * Check if the active element is a text input, textarea, or contenteditable.
     * @returns {boolean}
     */
    function isTyping() {
      var el = document.activeElement;
      if (!el) return false;
      var tag = el.tagName;
      if (tag === 'TEXTAREA') return true;
      if (tag === 'INPUT' && (el.type === 'text' || el.type === 'search')) return true;
      if (el.isContentEditable) return true;
      return false;
    }

    document.addEventListener('keydown', function(e) {
      // Escape: blur text input → return focus to file row
      // Escape: テキスト入力を blur → ファイル行にフォーカスを戻す
      if (e.key === 'Escape' && !e.isComposing) {
        if (isTyping()) {
          document.activeElement.blur();
          applyFocus();
          e.preventDefault();
          window.__kbEscHandled__ = true; // Signal init.js to skip details close / init.jsにdetails閉じをスキップさせる
          return;
        }
        // Clear keyboard focus if active / キーボードフォーカスが有効なら解除
        if (_kbIndex >= 0) {
          document.querySelectorAll('tr.kb-focus').forEach(function(el) {
            el.classList.remove('kb-focus');
          });
          _kbIndex = -1;
          e.preventDefault();
          window.__kbEscHandled__ = true;
          return;
        }
      }

      // Skip shortcuts when typing in an input
      // テキスト入力中はショートカットをスキップする
      if (isTyping()) return;

      // Skip if modifier keys are held (allow browser shortcuts)
      // 修飾キーが押されていたらスキップ（ブラウザショートカットを許可）
      if (e.ctrlKey || e.metaKey || e.altKey) return;

      // Resolve the effective key: when IME is active (e.g. Japanese input on Windows),
      // browsers report e.key === 'Process' instead of the actual character.
      // Fall back to the physical key code so shortcuts work regardless of IME state.
      // 有効キーの解決: IME が有効な場合（例: Windows の日本語入力）、
      // ブラウザは e.key === 'Process' を返すため、物理キーコードにフォールバックする。
      var key = e.key;
      if (key === 'Process' || key === 'Unidentified') {
        var codeMap = { 'KeyJ': 'j', 'KeyK': 'k', 'KeyX': 'x', 'Slash': '?' };
        key = codeMap[e.code] || '';
        // '?' requires Shift on most layouts / '?' は多くの配列で Shift が必要
        if (e.code === 'Slash' && !e.shiftKey) key = '';
      }

      switch (key) {
        case 'j':
          moveBy(1);
          e.preventDefault();
          break;
        case 'k':
          moveBy(-1);
          e.preventDefault();
          break;
        case 'x':
          toggleCheck();
          e.preventDefault();
          break;
        case '?':
          toggleHelp();
          e.preventDefault();
          break;
      }
    });

    // Show help overlay briefly on first visit to make shortcuts discoverable.
    // 初回訪問時にヘルプオーバーレイを短時間表示してショートカットの存在を周知する。
    document.addEventListener('DOMContentLoaded', function() {
      if (__savedState__ !== null) return; // Skip in reviewed mode / レビュー済みモードではスキップ
      var helpKey = __storageKey__ + '_kb_help_shown';
      try { if (localStorage.getItem(helpKey)) return; } catch(e) { /* ignore */ }
      var overlay = document.getElementById('kb-help');
      if (!overlay) return;
      overlay.classList.add('kb-help-visible');
      overlay.classList.remove('kb-help-hidden');
      try { localStorage.setItem(helpKey, '1'); } catch(e) { /* ignore */ }
      setTimeout(function() {
        overlay.classList.remove('kb-help-visible');
        overlay.classList.add('kb-help-hidden');
      }, 4000);
    });
  })();
