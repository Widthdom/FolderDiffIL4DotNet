  // ── Theme toggle (Light / Dark / System) ──────────────────────────────
  // テーマ切替（ライト / ダーク / システム）
  var __themeStorageKey__ = __storageKey__ + '-theme';
  var __themeLabels__ = { light: '\u2600 Light', dark: '\u263E Dark', system: '\u2699 System' };

  function getStoredTheme() {
    try { return localStorage.getItem(__themeStorageKey__); } catch(e) { return null; }
  }

  function applyTheme(mode) {
    var root = document.documentElement;
    if (mode === 'light' || mode === 'dark') {
      root.setAttribute('data-theme', mode);
    } else {
      root.removeAttribute('data-theme');
    }
    var btn = document.getElementById('theme-toggle');
    if (btn) btn.textContent = __themeLabels__[mode] || __themeLabels__.system;
  }

  function initTheme() {
    var stored = getStoredTheme();
    var mode = (stored === 'light' || stored === 'dark') ? stored : 'system';
    applyTheme(mode);
  }

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
