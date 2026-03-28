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
