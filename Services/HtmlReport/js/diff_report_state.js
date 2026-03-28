  const __storageKey__  = '{{STORAGE_KEY}}';
  const __reportDate__  = '{{REPORT_DATE}}';
  // NOTE: 'const __savedState__ = null;' is replaced by downloadReviewed(). Do not change whitespace.
  const __savedState__  = null;
  // NOTE: replaced with SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __reviewedSha256__  = null;
  // NOTE: replaced with final SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __finalSha256__     = null;
  // Total number of reviewable files (injected at generation time). Do not change whitespace.
  // レビュー対象ファイルの総数（生成時に注入）。空白を変更しないこと。
  const __totalFiles__      = {{TOTAL_FILES}};
  const __totalFilesDetail__ = '{{TOTAL_FILES_DETAIL}}';

  function formatTs(d) {
    return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')
      +' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0');
  }

  function readSavedStateFromStorage(fallbackJson) {
    try {
      var parsed = JSON.parse(localStorage.getItem(__storageKey__) || fallbackJson);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch(e) {
      return null;
    }
  }

  var __filterIds__ = ['filter-diff-sha256match','filter-diff-sha256mismatch','filter-diff-ilmatch','filter-diff-ilmismatch','filter-diff-textmatch','filter-diff-textmismatch','filter-imp-high','filter-imp-medium','filter-imp-low','filter-unchecked','filter-search'];
  function collectState() {
    var s = {};
    document.querySelectorAll('input[id], textarea[id]').forEach(function(el) {
      if (__filterIds__.indexOf(el.id) >= 0) return;
      s[el.id] = (el.type === 'checkbox') ? el.checked : el.value;
    });
    return s;
  }

  function autoSave() {
    try {
      localStorage.setItem(__storageKey__, JSON.stringify(collectState()));
    } catch(e) {
      // Gracefully handle localStorage quota exceeded or other storage errors
      // localStorage 容量超過やその他のストレージエラーを安全に処理
    }
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'Auto-saved at ' + formatTs(new Date());
    updateProgress();
  }

  // Update the review progress bar / レビュー進捗バーを更新
  function updateProgress() {
    if (__totalFiles__ <= 0) return;
    // Merge saved state with current DOM to include lazy-loaded sections
    // 遅延ロードセクションも含めるため、保存済み状態と現在のDOMをマージ
    var saved = __savedState__ || readSavedStateFromStorage('{}') || {};
    document.querySelectorAll('input[type="checkbox"][id^="cb_"]').forEach(function(cb) {
      saved[cb.id] = cb.checked;
    });
    var checked = 0;
    Object.keys(saved).forEach(function(k) {
      if (k.indexOf('cb_') !== 0 || !saved[k]) return;
      // Exclude Unchanged/Ignored from progress count / Unchanged/Ignoredは進捗カウントから除外
      if (k.indexOf('cb_unch_') === 0 || k.indexOf('cb_ign_') === 0) return;
      checked++;
    });
    var pct = Math.min(100, checked / __totalFiles__ * 100);
    var bar = document.getElementById('progress-bar-fill');
    var txt = document.getElementById('progress-text');
    if (bar) {
      bar.style.width = pct + '%';
      if (checked >= __totalFiles__) bar.classList.add('complete');
      else bar.classList.remove('complete');
    }
    if (txt) txt.textContent = checked + ' / ' + __totalFiles__ + ' reviewed';
    var det = document.getElementById('progress-detail');
    if (det && __totalFilesDetail__) det.textContent = '(' + __totalFilesDetail__ + ')';
  }
