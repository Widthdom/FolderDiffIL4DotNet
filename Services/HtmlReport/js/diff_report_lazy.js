  /**
   * Set up lazy rendering for diff detail tables.
   * HTML is stored base64-encoded in data-diff-html; decoded and inserted on first open.
   */
  function setupLazyDiff() {
    document.querySelectorAll('details[data-diff-html]').forEach(function(d) {
      d.addEventListener('toggle', function onToggle() {
        if (!d.open) return;
        var b64 = d.getAttribute('data-diff-html');
        if (!b64) return;
        d.removeAttribute('data-diff-html');
        d.removeEventListener('toggle', onToggle);
        try {
          var decoded = decodeDiffHtml(b64);
          // Pre-initialize virtual scroll on a detached container to avoid inserting
          // thousands of rows into the live DOM only to immediately extract and remove them.
          // This eliminates expensive layout/reflow for assemblies with 10,000+ methods.
          // デタッチコンテナで仮想スクロールを事前初期化し、数千行のライブDOM挿入→
          // 即時抽出→削除の高コストなレイアウト/リフローを回避（1万メソッド超対応）
          var tmp = document.createElement('div');
          tmp.innerHTML = decoded;
          tmp.querySelectorAll('table.semantic-changes-table.sc-detail').forEach(function(tbl) {
            if (initVirtualScroll(tbl)) {
              tbl.classList.add('vs-active');
            }
          });
          // Move content to live DOM (virtual scroll tables already trimmed to viewport)
          // コンテンツをライブDOMに移動（仮想スクロールテーブルはビューポート分のみ）
          while (tmp.firstChild) {
            d.appendChild(tmp.firstChild);
          }
          // Insert side-by-side toggle button before each diff-table / 各diff-tableの前にサイドバイサイド切り替えボタンを挿入
          d.querySelectorAll('.diff-table').forEach(function(tbl) {
            highlightILDiff(tbl);
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
          // Wire up save events on new checkboxes (skip if virtual scroll handles them)
          // 新規チェックボックスにsaveイベントを接続（仮想スクロールが処理する場合はスキップ）
          if (__savedState__ === null) {
            d.querySelectorAll('input').forEach(function(el) {
              if (el.closest('table.vs-active')) return;
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
          syncHeaderCheckboxes();
          // Apply current importance filters to newly rendered semantic change rows
          // 新規レンダリングされたセマンティック変更行に現在の重要度フィルターを適用
          applyFilters();
        } catch(e) {}
      });
    });
  }

  /**
   * Set up lazy rendering for section tables (Ignored/Unchanged).
   * HTML is stored base64-encoded in data-lazy-section; decoded on first open.
   */
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
          syncHeaderCheckboxes();
        } catch(e) {}
      });
    });
  }

  /** Force-decode all lazy sections (used before downloadReviewed captures full HTML). */
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

  /**
   * Set up IntersectionObserver to auto-expand lazy sections when scrolled into view.
   * Uses a 200px rootMargin so sections start decoding slightly before they become visible.
   */
  function setupLazyIntersectionObserver() {
    if (typeof IntersectionObserver === 'undefined') return;
    var observer = new IntersectionObserver(function(entries) {
      entries.forEach(function(entry) {
        if (!entry.isIntersecting) return;
        var d = entry.target;
        if (d.hasAttribute('data-lazy-section') && !d.open) {
          d.open = true;
        }
        observer.unobserve(d);
      });
    }, { rootMargin: '200px 0px' });
    document.querySelectorAll('details[data-lazy-section]').forEach(function(d) {
      observer.observe(d);
    });
  }

  /* Export functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { forceDecodeLazySections: forceDecodeLazySections }; }
