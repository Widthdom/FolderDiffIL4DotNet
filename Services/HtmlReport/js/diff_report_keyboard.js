  // ── Keyboard shortcuts (j/k/x/?) ───────────────────────────────────────
  // キーボードショートカット（j/k/x/?）
  (function() {
    var _kbIndex = -1;        // Current focused row index / 現在フォーカス中の行インデックス
    var _kbRows  = [];        // Cached visible file rows / キャッシュ済み可視ファイル行

    // Refresh the list of visible (non-hidden) file rows.
    // 表示中（非表示でない）のファイル行リストを更新する。
    function refreshRows() {
      _kbRows = Array.prototype.slice.call(
        document.querySelectorAll('tbody > tr[data-section]:not(.filter-hidden)')
      );
    }

    // Apply keyboard focus highlight to the row at _kbIndex.
    // _kbIndex の行にキーボードフォーカスハイライトを適用する。
    function applyFocus() {
      document.querySelectorAll('tr.kb-focus').forEach(function(el) {
        el.classList.remove('kb-focus');
      });
      if (_kbIndex < 0 || _kbIndex >= _kbRows.length) return;
      var row = _kbRows[_kbIndex];
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

    // Move focus by delta (+1 or -1). Wraps at boundaries.
    // delta（+1 または -1）だけフォーカスを移動する。端では止まる。
    function moveBy(delta) {
      refreshRows();
      if (_kbRows.length === 0) return;
      if (_kbIndex < 0) {
        _kbIndex = delta > 0 ? 0 : _kbRows.length - 1;
      } else {
        _kbIndex = Math.max(0, Math.min(_kbRows.length - 1, _kbIndex + delta));
      }
      applyFocus();
    }

    // Toggle the review checkbox of the currently focused row.
    // 現在フォーカス中の行のレビューチェックボックスをトグルする。
    function toggleCheck() {
      if (__savedState__ !== null) return; // Read-only mode / 読み取り専用モード
      if (_kbIndex < 0 || _kbIndex >= _kbRows.length) return;
      var cb = _kbRows[_kbIndex].querySelector('input[type="checkbox"]');
      if (cb) {
        cb.checked = !cb.checked;
        autoSave();
      }
    }

    // Show/hide the keyboard shortcut help overlay.
    // キーボードショートカットヘルプオーバーレイの表示/非表示を切り替える。
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

    // Check if the active element is a text input.
    // アクティブ要素がテキスト入力かどうかを判定する。
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
  })();
