  /**
   * Generate and download a self-contained "reviewed" HTML copy with embedded state and SHA256 integrity.
   * @returns {Promise<void>}
   */
  async function downloadReviewed() {
    // 0. Force-decode all lazy sections and materialize virtual scroll tables
    // 全lazyセクションを強制デコードし、仮想スクロールテーブルをマテリアライズ
    forceDecodeLazySections();
    vsMaterializeAll();
    var state   = collectState();
    var slug    = 'diff_report_' + __reportDate__;
    var root    = document.documentElement;
    // 1. Collapse all diff-detail elements so exported file starts with diffs closed
    var openDetails = Array.from(document.querySelectorAll('details[open]'));
    openDetails.forEach(function(d){ d.removeAttribute('open'); });
    // 2. Reset filters to defaults and clear all filter-hidden state so reviewed HTML shows all rows
    // フィルターをデフォルトにリセットし、全行表示にする
    __filterIds__.forEach(function(id) {
      var el = document.getElementById(id);
      if (!el) return;
      if (id === 'filter-unchecked') el.checked = false;
      else if (id === 'filter-search') el.value = '';
      else el.checked = true;
    });
    document.querySelectorAll('tr.filter-hidden').forEach(function(tr){ tr.classList.remove('filter-hidden'); });
    document.querySelectorAll('tr.filter-hidden-parent').forEach(function(tr){ tr.classList.remove('filter-hidden-parent'); });
    // 2b. Clear inline table widths so syncTableWidths recalculates on reviewed load
    // テーブルの inline width をクリアし reviewed ロード時に再計算させる
    document.querySelectorAll('table[style]').forEach(function(t){ t.style.removeProperty('width'); });
    // 3. Capture current effective column widths to bake into reviewed HTML as defaults
    var colVarNames = ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-tag-w','--col-disasm-w','--col-sdk-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w','--dc-refs-w'];
    var cs = getComputedStyle(root);
    var curWidths = {};
    colVarNames.forEach(function(v){ curWidths[v] = (root.style.getPropertyValue(v) || cs.getPropertyValue(v)).trim(); });
    // Clear body/root inline theme styles before capture so reviewed HTML starts clean
    // キャプチャ前にbody/rootのインラインテーマスタイルをクリアし、reviewed HTMLを初期状態にする
    var savedBodyColor = document.body.style.color;
    var savedBodyBg = document.body.style.backgroundColor;
    var savedColorScheme = root.style.colorScheme;
    document.body.style.removeProperty('color');
    document.body.style.removeProperty('background-color');
    root.style.removeProperty('color-scheme');
    /* Remove keyboard focus highlight so reviewed HTML has no yellow highlight row */
    /* キーボードフォーカスのハイライトを除去し reviewed HTML に黄色ハイライト行を残さない */
    var kbFocused = document.querySelectorAll('tr.kb-focus');
    kbFocused.forEach(function(tr) { tr.classList.remove('kb-focus'); });
    var html    = document.documentElement.outerHTML;
    /* Restore keyboard focus on live page / ライブページのキーボードフォーカスを復元 */
    kbFocused.forEach(function(tr) { tr.classList.add('kb-focus'); });
    // Restore live page state / ライブページの状態を復元
    document.body.style.color = savedBodyColor;
    document.body.style.backgroundColor = savedBodyBg;
    if (savedColorScheme) root.style.colorScheme = savedColorScheme;
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
      + '; --col-tag-w: '     + curWidths['--col-tag-w']
      + '; --col-disasm-w: '  + curWidths['--col-disasm-w']
      + '; --col-sdk-w: '    + curWidths['--col-sdk-w']
      + '; --sc-class-w: '    + curWidths['--sc-class-w']
      + '; --sc-basetype-w: ' + curWidths['--sc-basetype-w']
      + '; --sc-type-w: '     + curWidths['--sc-type-w']
      + '; --sc-name-w: '     + curWidths['--sc-name-w']
      + '; --sc-rettype-w: '  + curWidths['--sc-rettype-w']
      + '; --sc-params-w: '   + curWidths['--sc-params-w']
      + '; --sc-body-w: '     + curWidths['--sc-body-w']
      + '; --dc-refs-w: '    + curWidths['--dc-refs-w'] + '; }');
    // Remove inline col-var overrides from <html> element (now baked into :root)
    html = html.replace(/(<html\b[^>]*?) style="[^"]*"/, '$1');
    // Remove data-theme attribute so reviewed HTML uses system default
    // data-theme 属性を除去し reviewed HTML はシステム設定をデフォルトにする
    html = html.replace(/(<html\b[^>]*?) data-theme="[^"]*"/, '$1');
    // Replace button row with reviewed banner (filter zone is preserved outside CTRL markers)
    // ボタン行を reviewed バナーに置換（フィルターゾーンは CTRL マーカー外なので維持）
    html = html.replace(/<!--CTRL-->[\s\S]*?<!--\/CTRL-->/g,
      '<div class="reviewed-banner" role="banner"><span>Reviewed: ' + formatTs(new Date()) + ' &#x2014; read-only</span>'
      + '<div class="reviewed-banner-actions">'
      + '<button class="btn" onclick="verifyIntegrity()" style="font-size:12px">&#x2713; Verify integrity</button>'
      + '<button class="btn" onclick="downloadExcelCompatibleHtml()" style="font-size:12px"><svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><rect x="1" y="1" width="14" height="14" rx="1.5"/><line x1="1" y1="5" x2="15" y2="5"/><line x1="1" y1="9" x2="15" y2="9"/><line x1="6" y1="1" x2="6" y2="15"/><line x1="11" y1="1" x2="11" y2="15"/></svg> Download as Excel-compatible HTML</button>'
      + '<button class="btn" onclick="downloadAsPdf()" style="font-size:12px"><svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><path d="M4 1h6l4 4v10H2V1h2z"/><polyline points="10,1 10,5 14,5"/><line x1="5" y1="8" x2="11" y2="8"/><line x1="5" y1="10.5" x2="11" y2="10.5"/><line x1="5" y1="13" x2="9" y2="13"/></svg> Download as PDF</button>'
      + '<button class="btn btn-clear" onclick="collapseAll()" style="font-size:12px"><svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><polyline points="4 7 8 3 12 7"/><polyline points="4 13 8 9 12 13"/></svg> Fold all details</button>'
      + '<button class="btn btn-clear" onclick="resetFilters()" style="font-size:12px"><svg aria-hidden="true" width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" style="vertical-align:-1px"><path d="M2 3h12l-4 5v3l-4 2V8z"/><line x1="10" y1="10" x2="15" y2="15"/><line x1="15" y1="10" x2="10" y2="15"/></svg> Reset filters</button>'
      + '<button id="theme-toggle" class="btn btn-clear theme-toggle" onclick="cycleTheme()" title="Toggle theme (Light / Dark / System)" style="font-size:12px">&#x2699; System</button>'
      + '</div></div>');
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

  /** Prompt user to select a .sha256 file and verify it matches the embedded hash. */
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

  /** Collapse all open detail elements and auto-save. */
  function collapseAll() {
    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });
    autoSave();
  }

  /** Uncheck all review checkboxes, clear text inputs, reset column widths, and remove localStorage state (restore to pre-review state). */
  function clearAll() {
    if (!confirm('Uncheck all review checkboxes and clear all notes/reasons to restore the pre-review state. Continue?')) return;
    document.querySelectorAll('input[type="checkbox"]').forEach(function(cb){
      // Filter checkboxes → checked (no filter); other checkboxes → unchecked
      // フィルターチェックボックス → checked（フィルターなし）、その他 → unchecked
      cb.checked = (__filterIds__.indexOf(cb.id) >= 0 && cb.id !== 'filter-unchecked');
    });
    document.querySelectorAll('input[type="text"], textarea').forEach(function(inp){ inp.value=''; });
    // Reset column widths to defaults
    var root = document.documentElement;
    ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-tag-w','--col-disasm-w','--col-sdk-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w','--dc-refs-w'].forEach(function(v){ root.style.removeProperty(v); });
    syncTableWidths();
    // Close all open diff/IL-diff details
    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });
    applyFilters();
    localStorage.removeItem(__storageKey__);
    clearFilterState();
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'Cleared.';
  }
