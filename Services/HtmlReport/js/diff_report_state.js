  /** @type {string} localStorage key for this report (injected at generation time) */
  const __storageKey__  = '{{STORAGE_KEY}}';
  /** @type {string} Report generation date YYYYMMDD (injected at generation time) */
  const __reportDate__  = '{{REPORT_DATE}}';
  /* NOTE: 'const __savedState__ = null;' is replaced by downloadReviewed(). Do not change whitespace. */
  /** @type {Record<string, boolean|string>|null} Embedded review state (null in live mode, object in reviewed mode) */
  const __savedState__  = null;
  /* NOTE: replaced with SHA256 hash by downloadReviewed(). Do not change whitespace. */
  /** @type {string|null} SHA256 hash of reviewed HTML (null until downloaded) */
  const __reviewedSha256__  = null;
  /* NOTE: replaced with final SHA256 hash by downloadReviewed(). Do not change whitespace. */
  /** @type {string|null} Final SHA256 hash for .sha256 file verification */
  const __finalSha256__     = null;
  /** @type {number} Total number of reviewable files (injected at generation time). Do not change whitespace. */
  const __totalFiles__      = {{TOTAL_FILES}};
  /** @type {string} Compact breakdown of reviewable file counts by section */
  const __totalFilesDetail__ = '{{TOTAL_FILES_DETAIL}}';

  /**
   * Format a Date as 'YYYY-MM-DD HH:MM:SS'.
   * @param {Date} d
   * @returns {string}
   */
  function formatTs(d) {
    return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')
      +' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0');
  }

  /**
   * Read saved review state from localStorage, falling back to parsing fallbackJson.
   * @param {string} fallbackJson - JSON string to parse if localStorage is empty
   * @returns {Record<string, boolean|string>|null}
   */
  function readSavedStateFromStorage(fallbackJson) {
    try {
      var parsed = JSON.parse(localStorage.getItem(__storageKey__) || fallbackJson);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch(e) {
      return null;
    }
  }

  /** @type {string[]} Element IDs excluded from saved state (filter controls) */
  var __filterIds__ = ['filter-diff-sha256match','filter-diff-sha256mismatch','filter-diff-ilmatch','filter-diff-ilmismatch','filter-diff-textmatch','filter-diff-textmismatch','filter-imp-high','filter-imp-medium','filter-imp-low','filter-unchecked','filter-search'];
  /**
   * Collect current review state from all input/textarea elements, excluding filter controls.
   * @returns {Record<string, boolean|string>}
   */
  function collectState() {
    var s = {};
    document.querySelectorAll('input[id], textarea[id]').forEach(function(el) {
      if (__filterIds__.indexOf(el.id) >= 0) return;
      s[el.id] = (el.type === 'checkbox') ? el.checked : el.value;
    });
    return s;
  }

  /** Save current state to localStorage and update progress bar. Shows warning on quota exceeded. */
  function autoSave() {
    try {
      localStorage.setItem(__storageKey__, JSON.stringify(collectState()));
    } catch(e) {
      var status = document.getElementById('save-status');
      if (status) {
        status.textContent = '\u26A0 Storage full \u2014 clear old reviews in the storage bar above';
        status.style.color = 'var(--color-removed)';
      }
      updateStorageUsage();
      return;
    }
    var status = document.getElementById('save-status');
    if (status) { status.textContent = 'Auto-saved at ' + formatTs(new Date()); status.style.color = ''; }
    updateProgress();
    updateStorageUsage();
  }

  /**
   * Calculate and display the current localStorage usage in the storage bar.
   * Updates the width of #storage-bar-fill and the text of #storage-text.
   */
  function updateStorageUsage() {
    var bar = document.getElementById('storage-bar-fill');
    var txt = document.getElementById('storage-text');
    if (!bar || !txt) return;
    try {
      var usedBytes = 0;
      for (var i = 0; i < localStorage.length; i++) {
        var key = localStorage.key(i);
        usedBytes += key.length + localStorage.getItem(key).length;
      }
      var usedMB = (usedBytes * 2 / 1024 / 1024);
      var estimatedMaxMB = 5;
      var pct = Math.min(100, usedMB / estimatedMaxMB * 100);
      bar.style.width = pct + '%';
      if (pct > 80) { bar.style.background = 'var(--color-removed)'; }
      else if (pct > 60) { bar.style.background = 'var(--color-importance-medium)'; }
      else { bar.style.background = 'var(--color-progress-fill)'; }
      txt.textContent = usedMB.toFixed(2) + ' MB / ~' + estimatedMaxMB + ' MB';
    } catch(e) {
      txt.textContent = 'unavailable';
    }
  }

  /**
   * Remove all folderdiff-* localStorage entries except the current report's key.
   * @returns {number} Number of entries removed
   */
  function clearOldReviewStates() {
    var currentKey = __storageKey__;
    var themeKey = __storageKey__ + '-theme';
    var removed = 0;
    try {
      for (var i = localStorage.length - 1; i >= 0; i--) {
        var key = localStorage.key(i);
        if (key === currentKey || key === themeKey) continue;
        if (key.indexOf('folderdiff-') === 0) {
          localStorage.removeItem(key);
          removed++;
          i = localStorage.length;
        }
      }
    } catch(e) { /* ignore */ }
    updateStorageUsage();
    var status = document.getElementById('save-status');
    if (status) {
      status.textContent = 'Freed ' + removed + ' old report state' + (removed !== 1 ? 's' : '') + ' from storage';
      status.style.color = '';
    }
    return removed;
  }

  /** Update the review progress bar and fire celebration on 100% completion. */
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
      if (checked >= __totalFiles__) {
        bar.classList.add('complete');
        celebrateCompletion();
      }
      else bar.classList.remove('complete');
    }
    if (txt) txt.textContent = checked + ' / ' + __totalFiles__ + ' reviewed';
    var det = document.getElementById('progress-detail');
    if (det && __totalFilesDetail__) det.textContent = '(' + __totalFilesDetail__ + ')';
  }
