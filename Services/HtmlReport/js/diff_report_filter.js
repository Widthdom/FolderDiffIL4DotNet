  /**
   * Apply all active filters (diff detail, importance, unchecked-only, search) to file rows.
   * Hides non-matching rows and manages semantic change row visibility.
   */
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
  /** Reset all filter checkboxes and search input to defaults and re-apply. */
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

  /**
   * Copy the file path text from the row containing the clicked button to clipboard.
   * @param {HTMLButtonElement} btn - The copy button element
   */
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
