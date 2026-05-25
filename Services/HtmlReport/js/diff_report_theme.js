  /** @type {string} localStorage key for theme preference */
  var __themeStorageKey__ = __storageKey__ + '-theme';
  /** @type {{light: string, dark: string, system: string}} Button labels for each theme mode */
  var __themeLabels__ = { light: '\u2600 Light', dark: '\u263E Dark', system: '\u2699 System' };

  /**
   * Read stored theme preference from localStorage.
   * @returns {string|null} 'light', 'dark', or null
   */
  function getStoredTheme() {
    try { return localStorage.getItem(__themeStorageKey__); } catch(e) { return null; }
  }

  /**
   * Apply a theme mode to the document root and body.
   * @param {'light'|'dark'|'system'} mode
   */
  function applyTheme(mode) {
    var root = document.documentElement;
    if (mode === 'light' || mode === 'dark') {
      root.setAttribute('data-theme', mode);
      // Override browser UA color-scheme to match manual toggle
      // ブラウザ UA の color-scheme を手動切替に合わせて上書き
      root.style.colorScheme = mode;
    } else {
      root.removeAttribute('data-theme');
      root.style.removeProperty('color-scheme');
    }
    // Belt-and-suspenders: force body color/background for reviewed HTML
    // reviewed HTML 用にbodyの色・背景を直接設定（フォールバック）
    if (mode === 'light') {
      document.body.style.color = '#1d1d1f';
      document.body.style.backgroundColor = '#fff';
    } else if (mode === 'dark') {
      document.body.style.color = '#e6edf3';
      document.body.style.backgroundColor = '#0d1117';
    } else {
      document.body.style.removeProperty('color');
      document.body.style.removeProperty('background-color');
    }
    var btn = document.getElementById('theme-toggle');
    if (btn) btn.textContent = __themeLabels__[mode] || __themeLabels__.system;
  }

  /** Initialize theme from stored preference or system default. */
  function initTheme() {
    var stored = getStoredTheme();
    var mode = (stored === 'light' || stored === 'dark') ? stored : 'system';
    applyTheme(mode);
  }

  /** Cycle theme: system -> light -> dark -> system. */
  function cycleTheme() {
    var stored = getStoredTheme();
    var current = (stored === 'light' || stored === 'dark') ? stored : 'system';
    var next = current === 'system' ? 'light' : current === 'light' ? 'dark' : 'system';
    try {
      if (next === 'system') localStorage.removeItem(__themeStorageKey__);
      else localStorage.setItem(__themeStorageKey__, next);
    } catch(e) { /* quota exceeded / 容量超過 */ }
    applyTheme(next);
  }

  /* Export functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { applyTheme: applyTheme, cycleTheme: cycleTheme }; }
