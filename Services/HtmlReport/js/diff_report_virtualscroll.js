  /**
   * Virtual scroll for semantic changes tables with many rows.
   * Wraps the table in a scrollable viewport and renders only visible rows + buffer.
   * Uses event delegation for checkbox change events and restores state after re-render.
   * 多数行を持つセマンティック変更テーブル用の仮想スクロール。
   * テーブルをスクロール可能なビューポートでラップし、表示行+バッファのみをレンダリングします。
   * チェックボックスのイベントデリゲーションを使用し、再レンダリング後に状態を復元します。
   */

  /** @type {number} Minimum row count to activate virtual scroll / 仮想スクロールを有効化する最小行数 */
  var VS_THRESHOLD = 100;
  /** @type {number} Estimated row height in pixels / 行の推定高さ（ピクセル） */
  var VS_ROW_HEIGHT = 22;
  /** @type {number} Extra rows rendered above and below the viewport / ビューポート上下に追加描画する行数 */
  var VS_BUFFER = 20;
  /** @type {number} Maximum viewport height in pixels / ビューポートの最大高さ（ピクセル） */
  var VS_MAX_HEIGHT = 600;

  /**
   * Initialize virtual scroll on a semantic changes table if it exceeds the row threshold.
   * Called after lazy decode inserts the table into the DOM.
   * セマンティック変更テーブルが行閾値を超える場合に仮想スクロールを初期化します。
   * 遅延デコードでテーブルがDOMに挿入された後に呼び出されます。
   * @param {HTMLTableElement} table - The semantic changes table element
   * @returns {boolean} true if virtual scroll was activated / 仮想スクロールが有効化された場合 true
   */
  function initVirtualScroll(table) {
    var tbody = table.querySelector('tbody');
    if (!tbody) return false;
    var rows = tbody.querySelectorAll('tr');
    if (rows.length <= VS_THRESHOLD) return false;

    // Extract all row data: outerHTML and key attributes / 全行データを抽出
    var rowData = [];
    for (var i = 0; i < rows.length; i++) {
      rowData.push({
        html: rows[i].outerHTML,
        hidden: rows[i].classList.contains('filter-hidden'),
        cbId: null,
        cbChecked: false
      });
      // Capture checkbox ID and state / チェックボックスIDと状態をキャプチャ
      var cb = rows[i].querySelector('input[type="checkbox"]');
      if (cb) {
        rowData[i].cbId = cb.id;
        rowData[i].cbChecked = cb.checked;
      }
    }

    // Create scrollable viewport wrapper / スクロール可能なビューポートラッパーを作成
    var wrapper = document.createElement('div');
    wrapper.className = 'vs-viewport';
    table.parentNode.insertBefore(wrapper, table);
    wrapper.appendChild(table);

    // Make thead sticky / thead をスティッキーに
    var thead = table.querySelector('thead');
    if (thead) thead.classList.add('vs-sticky-head');

    // Clear tbody / tbody をクリア
    tbody.innerHTML = '';

    // Store virtual scroll state on the table element / テーブル要素に仮想スクロール状態を保存
    table.__vs = {
      rowData: rowData,
      wrapper: wrapper,
      tbody: tbody,
      renderedStart: -1,
      renderedEnd: -1
    };

    // Event delegation for checkbox changes / チェックボックス変更のイベントデリゲーション
    tbody.addEventListener('change', function(ev) {
      var target = ev.target;
      if (target.type !== 'checkbox' || !target.id) return;
      // Update stored state / 保存された状態を更新
      for (var j = 0; j < table.__vs.rowData.length; j++) {
        if (table.__vs.rowData[j].cbId === target.id) {
          table.__vs.rowData[j].cbChecked = target.checked;
          break;
        }
      }
      if (__savedState__ === null) autoSave();
    });

    // Perform initial render / 初期レンダリングを実行
    vsRender(table);

    // Throttled scroll handler / スロットル付きスクロールハンドラ
    var ticking = false;
    wrapper.addEventListener('scroll', function() {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(function() {
        vsRender(table);
        ticking = false;
      });
    });

    // Show row count indicator / 行数インジケーターを表示
    var indicator = document.createElement('div');
    indicator.className = 'vs-indicator';
    var visibleCount = rowData.filter(function(r) { return !r.hidden; }).length;
    indicator.textContent = visibleCount + ' of ' + rowData.length + ' rows';
    wrapper.parentNode.insertBefore(indicator, wrapper.nextSibling);
    table.__vs.indicator = indicator;

    return true;
  }

  /**
   * Render the visible rows in the virtual scroll viewport.
   * Uses spacer rows to maintain correct scroll position.
   * 仮想スクロールビューポートに表示行をレンダリングします。
   * 正しいスクロール位置を維持するためにスペーサー行を使用します。
   * @param {HTMLTableElement} table - The table with __vs data attached
   */
  function vsRender(table) {
    var vs = table.__vs;
    if (!vs) return;

    // Build visible-only index / 表示行のみのインデックスを構築
    var visibleIndices = [];
    for (var i = 0; i < vs.rowData.length; i++) {
      if (!vs.rowData[i].hidden) visibleIndices.push(i);
    }
    var totalVisible = visibleIndices.length;
    var totalHeight = totalVisible * VS_ROW_HEIGHT;

    var scrollTop = vs.wrapper.scrollTop;
    var viewportH = vs.wrapper.clientHeight || VS_MAX_HEIGHT;

    var startVis = Math.max(0, Math.floor(scrollTop / VS_ROW_HEIGHT) - VS_BUFFER);
    var endVis = Math.min(totalVisible, Math.ceil((scrollTop + viewportH) / VS_ROW_HEIGHT) + VS_BUFFER);

    // Skip re-render if range unchanged / 範囲が変わっていなければ再レンダリングをスキップ
    if (startVis === vs.renderedStart && endVis === vs.renderedEnd) return;
    vs.renderedStart = startVis;
    vs.renderedEnd = endVis;

    var topPad = startVis * VS_ROW_HEIGHT;
    var bottomPad = Math.max(0, (totalVisible - endVis) * VS_ROW_HEIGHT);

    // Build new tbody content / 新しい tbody コンテンツを構築
    var html = '<tr class="vs-spacer" style="height:' + topPad + 'px"><td colspan="99"></td></tr>';
    for (var v = startVis; v < endVis; v++) {
      html += vs.rowData[visibleIndices[v]].html;
    }
    html += '<tr class="vs-spacer" style="height:' + bottomPad + 'px"><td colspan="99"></td></tr>';
    vs.tbody.innerHTML = html;

    // Restore checkbox states for rendered rows / レンダリング済み行のチェックボックス状態を復元
    var toRestore = __savedState__ || readSavedStateFromStorage('null');
    vs.tbody.querySelectorAll('input[type="checkbox"]').forEach(function(cb) {
      // First check stored rowData state / まず rowData の状態をチェック
      for (var j = 0; j < vs.rowData.length; j++) {
        if (vs.rowData[j].cbId === cb.id) {
          cb.checked = vs.rowData[j].cbChecked;
          return;
        }
      }
      // Fallback to saved state / 保存済み状態にフォールバック
      if (toRestore && cb.id in toRestore) {
        cb.checked = Boolean(toRestore[cb.id]);
      }
    });

    // Disable checkboxes in reviewed mode / レビュー済みモードではチェックボックスを無効化
    if (__savedState__ !== null) {
      vs.tbody.querySelectorAll('input[type="checkbox"]').forEach(function(cb) {
        cb.style.pointerEvents = 'none';
        cb.style.cursor = 'default';
      });
    }
  }

  /**
   * Update virtual scroll after filter changes. Recalculates visible rows and re-renders.
   * フィルター変更後に仮想スクロールを更新。表示行を再計算して再レンダリングします。
   */
  function vsUpdateFilters() {
    document.querySelectorAll('table.__vs-active').forEach(function(table) {
      vsRefreshVisibility(table);
    });
  }

  /**
   * Refresh visibility flags on virtual scroll row data based on current filter state.
   * 現在のフィルター状態に基づいて仮想スクロール行データの表示フラグを更新します。
   * @param {HTMLTableElement} table - The virtual-scrolled table
   */
  function vsRefreshVisibility(table) {
    var vs = table.__vs;
    if (!vs) return;

    // Re-evaluate filter state for each row by temporarily inserting and checking
    // 各行を一時的に挿入してフィルター状態を再評価
    var filterImpHigh = document.getElementById('filter-imp-high');
    var filterImpMedium = document.getElementById('filter-imp-medium');
    var filterImpLow = document.getElementById('filter-imp-low');
    var showHigh = !filterImpHigh || filterImpHigh.checked;
    var showMedium = !filterImpMedium || filterImpMedium.checked;
    var showLow = !filterImpLow || filterImpLow.checked;

    var visibleCount = 0;
    for (var i = 0; i < vs.rowData.length; i++) {
      var row = vs.rowData[i];
      // Parse importance from stored HTML / 保存されたHTMLからimportanceをパース
      var impMatch = row.html.match(/data-sc-importance="([^"]*)"/);
      var imp = impMatch ? impMatch[1] : '';
      var hidden = false;
      if (imp === 'High' && !showHigh) hidden = true;
      else if (imp === 'Medium' && !showMedium) hidden = true;
      else if (imp === 'Low' && !showLow) hidden = true;
      else if (imp !== 'High' && imp !== 'Medium' && imp !== 'Low' && !showLow) hidden = true;
      row.hidden = hidden;
      if (!hidden) visibleCount++;
    }

    // Reset scroll and re-render / スクロールをリセットして再レンダリング
    vs.renderedStart = -1;
    vs.renderedEnd = -1;
    vs.wrapper.scrollTop = 0;
    vsRender(table);

    // Update indicator / インジケーターを更新
    if (vs.indicator) {
      vs.indicator.textContent = visibleCount + ' of ' + vs.rowData.length + ' rows';
    }
  }

  /**
   * Force-materialize all virtual scroll tables back to full DOM (for downloadReviewed).
   * 全仮想スクロールテーブルを完全なDOMに復元します（downloadReviewed用）。
   */
  function vsMaterializeAll() {
    document.querySelectorAll('table.vs-active').forEach(function(table) {
      var vs = table.__vs;
      if (!vs) return;
      // Rebuild full tbody / 完全な tbody を再構築
      var html = '';
      for (var i = 0; i < vs.rowData.length; i++) {
        html += vs.rowData[i].html;
      }
      vs.tbody.innerHTML = html;
      // Restore all checkbox states / 全チェックボックス状態を復元
      vs.tbody.querySelectorAll('input[type="checkbox"]').forEach(function(cb) {
        for (var j = 0; j < vs.rowData.length; j++) {
          if (vs.rowData[j].cbId === cb.id) {
            cb.checked = vs.rowData[j].cbChecked;
            break;
          }
        }
      });
      // Unwrap from viewport / ビューポートからアンラップ
      var wrapper = vs.wrapper;
      wrapper.parentNode.insertBefore(table, wrapper);
      wrapper.parentNode.removeChild(wrapper);
      // Remove indicator / インジケーターを削除
      if (vs.indicator && vs.indicator.parentNode) {
        vs.indicator.parentNode.removeChild(vs.indicator);
      }
      // Clean up thead sticky class / thead sticky クラスをクリーンアップ
      var thead = table.querySelector('thead');
      if (thead) thead.classList.remove('vs-sticky-head');
      table.classList.remove('vs-active');
      delete table.__vs;
    });
  }

  /* Export functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { vsMaterializeAll: vsMaterializeAll }; }
