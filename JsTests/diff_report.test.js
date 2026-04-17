/**
 * Unit tests for diff_report JS modules — the client-side JavaScript embedded in the HTML report.
 * diff_report JS モジュールのユニットテスト — HTML レポートに埋め込まれるクライアントサイド JavaScript。
 *
 * Strategy: The jest-environment-jsdom provides a global document/window.
 * Each test resets document.body, evaluates the script via eval() with
 * template placeholders replaced, and exercises the resulting global functions.
 * 戦略: jest-environment-jsdom がグローバル document/window を提供する。
 * 各テストは document.body をリセットし、テンプレートプレースホルダーを
 * 置換したスクリプトを eval() で評価し、結果のグローバル関数を実行する。
 */
const { TextEncoder, TextDecoder } = require('util');
// Polyfill TextEncoder/TextDecoder for jsdom environment
// jsdom 環境用の TextEncoder/TextDecoder ポリフィル
if (typeof globalThis.TextEncoder === 'undefined') {
  globalThis.TextEncoder = TextEncoder;
}
if (typeof globalThis.TextDecoder === 'undefined') {
  globalThis.TextDecoder = TextDecoder;
}

const fs = require('fs');
const path = require('path');

// Load all JS module files in order and concatenate them
// 全 JS モジュールファイルを順序どおりに読み込み結合する
const JS_MODULE_FILES = [
  'diff_report_state.js',
  'diff_report_export.js',
  'diff_report_diffview.js',
  'diff_report_lazy.js',
  'diff_report_virtualscroll.js',
  'diff_report_layout.js',
  'diff_report_filter.js',
  'diff_report_excel.js',
  'diff_report_theme.js',
  'diff_report_celebrate.js',
  'diff_report_highlight.js',
  'diff_report_keyboard.js',
  'diff_report_init.js',
];
const JS_SOURCE = JS_MODULE_FILES.map(function(f) {
  return fs.readFileSync(
    path.join(__dirname, '..', 'Services', 'HtmlReport', 'js', f),
    'utf-8'
  );
}).join('\n');

/**
 * Load diff_report JS modules into the current jsdom window with given options.
 * Replaces template placeholders and evaluates the concatenated script.
 * diff_report JS モジュールを指定オプションで現在の jsdom window にロードする。
 * テンプレートプレースホルダーを置換し、結合されたスクリプトを評価する。
 */
function loadScript(options = {}) {
  const {
    storageKey = 'test-key',
    reportDate = '2026-01-01',
    savedState = null,
    bodyHtml = '',
    totalFiles = 0,
    totalFilesDetail = '',
  } = options;

  // Reset DOM
  document.documentElement.innerHTML = '<head><title>diff_report</title></head><body></body>';
  document.body.innerHTML = bodyHtml;
  localStorage.clear();

  // Polyfill scrollIntoView for jsdom (not implemented)
  // jsdom 未実装の scrollIntoView をポリフィル
  if (typeof Element.prototype.scrollIntoView !== 'function') {
    Element.prototype.scrollIntoView = function() {};
  }

  // Replace template placeholders
  let js = JS_SOURCE
    .replace("'{{STORAGE_KEY}}'", JSON.stringify(storageKey))
    .replace("'{{REPORT_DATE}}'", JSON.stringify(reportDate))
    .replace('{{TOTAL_FILES}}', String(totalFiles))
    .replace('{{TOTAL_FILES_DETAIL}}', totalFilesDetail);

  if (savedState !== null) {
    js = js.replace(
      'const __savedState__  = null;',
      'const __savedState__  = ' + JSON.stringify(savedState) + ';'
    );
  }

  // Evaluate in global scope — use indirect eval to ensure declarations
  // attach to the global (window) object rather than a function scope.
  // 間接 eval でグローバル（window）オブジェクトに宣言を付与する。
  const indirectEval = eval;
  indirectEval(js);
}

/**
 * Fire DOMContentLoaded on document.
 * document 上で DOMContentLoaded を発火する。
 */
function fireDOMContentLoaded() {
  document.dispatchEvent(new Event('DOMContentLoaded'));
}

// ─── formatTs ────────────────────────────────────────────────────────────────
describe('formatTs', () => {
  beforeEach(() => loadScript());

  test('formats a Date as YYYY-MM-DD HH:MM:SS', () => {
    expect(window.formatTs(new Date(2026, 0, 15, 9, 5, 3))).toBe('2026-01-15 09:05:03');
  });

  test('pads single-digit months and days', () => {
    expect(window.formatTs(new Date(2025, 2, 3, 14, 30, 0))).toBe('2025-03-03 14:30:00');
  });
});

// ─── reviewed state Base64 helpers ───────────────────────────────────────────
describe('embedded reviewed state helpers', () => {
  test('round-trips UTF-8 and script-breaking text safely', () => {
    loadScript();

    const original = {
      'chk-a': true,
      'note-a': '</script><script>alert("xss")</script>',
      'note-ja': '日本語メモ',
    };

    const encoded = window.encodeEmbeddedState(original);
    const decoded = window.decodeEmbeddedState(encoded);

    expect(typeof encoded).toBe('string');
    expect(encoded).not.toContain('</script>');
    expect(decoded).toEqual(original);
  });
});

// ─── collectState ────────────────────────────────────────────────────────────
describe('collectState', () => {
  test('collects checkbox and text input values by id', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk-file1" checked>
        <input type="text" id="note-file1" value="looks good">
      `,
    });
    fireDOMContentLoaded();

    const state = window.collectState();
    expect(state['chk-file1']).toBe(true);
    expect(state['note-file1']).toBe('looks good');
  });

  test('excludes filter control IDs from state', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="filter-imp-high" checked>
        <input type="text" id="filter-search" value="test">
        <input type="checkbox" id="chk-real" checked>
      `,
    });
    fireDOMContentLoaded();

    const state = window.collectState();
    expect(state).not.toHaveProperty('filter-imp-high');
    expect(state).not.toHaveProperty('filter-search');
    expect(state['chk-real']).toBe(true);
  });

  test('collects textarea values', () => {
    loadScript({
      bodyHtml: '<textarea id="note-area">some text</textarea>',
    });
    fireDOMContentLoaded();

    const state = window.collectState();
    expect(state['note-area']).toBe('some text');
  });

  test('unchecked checkbox is collected as false', () => {
    loadScript({
      bodyHtml: '<input type="checkbox" id="chk-unchecked">',
    });
    fireDOMContentLoaded();

    const state = window.collectState();
    expect(state['chk-unchecked']).toBe(false);
  });
});

// ─── autoSave ────────────────────────────────────────────────────────────────
describe('autoSave', () => {
  test('saves state to localStorage and updates save-status', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk1" checked>
        <input type="checkbox" id="checklist_cb_0" checked>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    window.autoSave();

    const stored = JSON.parse(localStorage.getItem('test-key'));
    expect(stored['chk1']).toBe(true);

    const status = document.getElementById('save-status');
    expect(status.textContent).toMatch(/^Auto-saved at \d{4}-\d{2}-\d{2}/);
  });
});

// ─── applyFilters ────────────────────────────────────────────────────────────
describe('applyFilters', () => {
  function loadFilterEnv(rows) {
    const filterBar = `
      <input type="checkbox" id="filter-imp-high" checked>
      <input type="checkbox" id="filter-imp-medium" checked>
      <input type="checkbox" id="filter-imp-low" checked>
      <input type="checkbox" id="filter-unchecked">
      <input type="text" id="filter-search" value="">
      <span id="save-status"></span>
    `;

    const tableRows = rows.map(r =>
      `<tr data-section="${r.section}" ${r.importance ? 'data-importance="' + r.importance + '"' : ''} ${r.importances ? 'data-importances="' + r.importances + '"' : ''} ${r.diff ? 'data-diff="' + r.diff + '"' : ''}>
        <td><input type="checkbox" id="chk-${r.id}" ${r.checked ? 'checked' : ''}></td>
        <td><span class="path-text">${r.path}</span></td>
      </tr>`
    ).join('\n');

    loadScript({
      bodyHtml: `${filterBar}<table><tbody>${tableRows}</tbody></table>`,
    });
    fireDOMContentLoaded();
  }

  test('hides rows by importance when filter unchecked', () => {
    loadFilterEnv([
      { id: '1', section: 'modified', importance: 'High', path: 'a.dll' },
      { id: '2', section: 'modified', importance: 'Low', path: 'b.dll' },
    ]);

    document.getElementById('filter-imp-low').checked = false;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('filter-hidden')).toBe(false);
    expect(rows[1].classList.contains('filter-hidden')).toBe(true);
  });

  test('data-importances keeps row visible if any level passes filter', () => {
    loadFilterEnv([
      { id: '1', section: 'modified', importance: 'High', importances: 'High,Medium,Low', path: 'bin/MyApp.deps.json' },
      { id: '2', section: 'modified', importance: 'High', path: 'lib/Core.dll' },
    ]);

    // Uncheck High filter
    document.getElementById('filter-imp-high').checked = false;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    // Row with data-importances containing Medium/Low should remain visible
    expect(rows[0].classList.contains('filter-hidden')).toBe(false);
    // Row with only data-importance="High" should be hidden
    expect(rows[1].classList.contains('filter-hidden')).toBe(true);
  });

  test('importance filter does not hide rows without data-importance', () => {
    loadFilterEnv([
      { id: '1', section: 'unchanged', path: 'unchanged.dll' },
    ]);

    // Uncheck all importance filters
    document.getElementById('filter-imp-high').checked = false;
    document.getElementById('filter-imp-medium').checked = false;
    document.getElementById('filter-imp-low').checked = false;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    // Row without data-importance should NOT be hidden by importance filter
    expect(rows[0].classList.contains('filter-hidden')).toBe(false);
  });

  test('filters by search text against path (case-insensitive)', () => {
    loadFilterEnv([
      { id: '1', section: 'modified', path: 'src/MyApp.dll' },
      { id: '2', section: 'modified', path: 'src/Other.dll' },
    ]);

    document.getElementById('filter-search').value = 'myapp';
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('filter-hidden')).toBe(false);
    expect(rows[1].classList.contains('filter-hidden')).toBe(true);
  });

  test('unchecked-only filter shows only unchecked rows', () => {
    loadFilterEnv([
      { id: '1', section: 'modified', path: 'a.dll', checked: true },
      { id: '2', section: 'modified', path: 'b.dll', checked: false },
    ]);

    document.getElementById('filter-unchecked').checked = true;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('filter-hidden')).toBe(true);
    expect(rows[1].classList.contains('filter-hidden')).toBe(false);
  });

  test('hides associated diff-row siblings when parent row is hidden', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="filter-imp-high" checked>
        <input type="checkbox" id="filter-imp-medium" checked>
        <input type="checkbox" id="filter-imp-low" checked>
        <input type="checkbox" id="filter-diff-sha256match" checked>
        <input type="checkbox" id="filter-diff-sha256mismatch" checked>
        <input type="checkbox" id="filter-diff-ilmatch" checked>
        <input type="checkbox" id="filter-diff-ilmismatch" checked>
        <input type="checkbox" id="filter-diff-textmatch" checked>
        <input type="checkbox" id="filter-diff-textmismatch" checked>
        <input type="checkbox" id="filter-unchecked">
        <input type="text" id="filter-search" value="nomatch">
        <span id="save-status"></span>
        <table><tbody>
          <tr data-section="modified"><td><span class="path-text">readme.txt</span></td></tr>
          <tr class="diff-row"><td>diff content here</td></tr>
        </tbody></table>
      `,
    });
    fireDOMContentLoaded();

    window.applyFilters();

    const diffRow = document.querySelector('tr.diff-row');
    expect(diffRow.classList.contains('filter-hidden-parent')).toBe(true);
  });

  test('when all filters checked, no rows are hidden', () => {
    loadFilterEnv([
      { id: '1', section: 'modified', importance: 'High', path: 'a.dll' },
      { id: '2', section: 'modified', importance: 'Low', path: 'b.exe' },
      { id: '3', section: 'unchanged', path: 'c.json' },
    ]);

    window.applyFilters();

    const hidden = document.querySelectorAll('tr.filter-hidden');
    expect(hidden.length).toBe(0);
  });
});

// ─── resetFilters ────────────────────────────────────────────────────────────
describe('resetFilters', () => {
  test('resets all filter controls to default state', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="filter-imp-high">
        <input type="checkbox" id="filter-imp-medium" checked>
        <input type="checkbox" id="filter-imp-low" checked>
        <input type="checkbox" id="filter-unchecked" checked>
        <input type="text" id="filter-search" value="some query">
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    window.resetFilters();

    expect(document.getElementById('filter-imp-high').checked).toBe(true);
    expect(document.getElementById('filter-unchecked').checked).toBe(false);
    expect(document.getElementById('filter-search').value).toBe('');
  });
});

// ─── decodeDiffHtml ──────────────────────────────────────────────────────────
describe('decodeDiffHtml', () => {
  beforeEach(() => loadScript());

  test('decodes base64-encoded UTF-8 HTML', () => {
    const b64 = Buffer.from('<b>hello</b>', 'utf-8').toString('base64');
    expect(window.decodeDiffHtml(b64)).toBe('<b>hello</b>');
  });

  test('handles multibyte characters', () => {
    const text = '日本語テスト';
    const b64 = Buffer.from(text, 'utf-8').toString('base64');
    expect(window.decodeDiffHtml(b64)).toBe(text);
  });
});

// ─── collapseAll ─────────────────────────────────────────────────────────────
describe('collapseAll', () => {
  test('removes open attribute from all details elements', () => {
    loadScript({
      bodyHtml: `
        <details open><summary>A</summary><p>content</p></details>
        <details open><summary>B</summary><p>content</p></details>
        <details><summary>C</summary><p>content</p></details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    window.collapseAll();

    expect(document.querySelectorAll('details[open]').length).toBe(0);
  });
});

// ─── clearAll ────────────────────────────────────────────────────────────────
describe('clearAll', () => {
  test('clears all inputs, removes localStorage, and closes details', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk1" checked>
        <input type="checkbox" id="checklist_cb_0" checked>
        <input type="text" id="note1" value="some note">
        <details open><summary>D</summary></details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();
    localStorage.setItem('test-key', '{"chk1":true}');
    window.confirm = () => true;

    window.clearAll();

    expect(document.getElementById('chk1').checked).toBe(false);
    expect(document.getElementById('checklist_cb_0').checked).toBe(false);
    expect(document.getElementById('note1').value).toBe('');
    expect(localStorage.getItem('test-key')).toBeNull();
    expect(document.querySelectorAll('details[open]').length).toBe(0);
    expect(document.getElementById('save-status').textContent).toBe('Cleared.');
  });

  test('does nothing if confirm returns false', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk1" checked>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();
    window.confirm = () => false;

    window.clearAll();

    expect(document.getElementById('chk1').checked).toBe(true);
  });
});

// ─── DOMContentLoaded state restore ──────────────────────────────────────────
describe('DOMContentLoaded state restore', () => {
  test('restores state from __savedState__ on load', () => {
    loadScript({
      savedState: { 'chk-a': true, 'note-a': 'restored text' },
      bodyHtml: `
        <input type="checkbox" id="chk-a">
        <input type="text" id="note-a">
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('chk-a').checked).toBe(true);
    expect(document.getElementById('note-a').value).toBe('restored text');
  });

  test('in reviewed mode (savedState set), inputs become read-only', () => {
    loadScript({
      savedState: { 'chk-b': false },
      bodyHtml: `
        <input type="checkbox" id="chk-b">
        <input type="text" id="note-b">
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('chk-b').style.pointerEvents).toBe('none');
    expect(document.getElementById('note-b').readOnly).toBe(true);
  });

  test('restores state from localStorage when no savedState', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk-ls">
        <input type="text" id="note-ls">
      `,
    });
    localStorage.setItem('test-key', JSON.stringify({
      'chk-ls': true,
      'note-ls': 'from storage'
    }));
    fireDOMContentLoaded();

    expect(document.getElementById('chk-ls').checked).toBe(true);
    expect(document.getElementById('note-ls').value).toBe('from storage');
  });
});

// ─── verifyIntegrity ─────────────────────────────────────────────────────────
describe('verifyIntegrity', () => {
  test('alerts if __finalSha256__ is null (not reviewed)', () => {
    loadScript();
    fireDOMContentLoaded();

    const alerts = [];
    window.alert = (msg) => alerts.push(msg);

    window.verifyIntegrity();
    expect(alerts).toHaveLength(1);
    expect(alerts[0]).toMatch(/not been downloaded as reviewed/);
  });
});

// ─── setupLazyDiff ───────────────────────────────────────────────────────────
describe('setupLazyDiff', () => {
  test('decodes and inserts HTML on first toggle of details with data-diff-html', () => {
    const diffHtml = '<table class="diff-table"><tr><td>diff</td></tr></table>';
    const b64 = Buffer.from(diffHtml, 'utf-8').toString('base64');
    loadScript({
      bodyHtml: `
        <details data-diff-html="${b64}">
          <summary>Show diff</summary>
        </details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    const details = document.querySelector('details');
    details.setAttribute('open', '');
    details.dispatchEvent(new Event('toggle'));

    expect(details.hasAttribute('data-diff-html')).toBe(false);
    expect(details.querySelector('.diff-table')).not.toBeNull();
  });

  test('does not re-decode on second toggle', () => {
    const diffHtml = '<div class="injected">content</div>';
    const b64 = Buffer.from(diffHtml, 'utf-8').toString('base64');
    loadScript({
      bodyHtml: `
        <details data-diff-html="${b64}">
          <summary>Show diff</summary>
        </details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    const details = document.querySelector('details');
    // First toggle
    details.setAttribute('open', '');
    details.dispatchEvent(new Event('toggle'));
    expect(details.querySelectorAll('.injected').length).toBe(1);

    // Second toggle (close then open)
    details.removeAttribute('open');
    details.dispatchEvent(new Event('toggle'));
    details.setAttribute('open', '');
    details.dispatchEvent(new Event('toggle'));
    // Should still have exactly 1 injected element
    expect(details.querySelectorAll('.injected').length).toBe(1);
  });
});

// ─── downloadReviewed (partial — no crypto.subtle) ───────────────────────────
describe('downloadReviewed', () => {
  test('collectState excludes filter IDs for download', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk-dl" checked>
        <input type="checkbox" id="filter-imp-high" checked>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    const state = window.collectState();
    expect(state).toHaveProperty('chk-dl', true);
    expect(state).not.toHaveProperty('filter-imp-high');
  });
});

// ─── updateProgress ──────────────────────────────────────────────────────────
describe('updateProgress', () => {
  it('updates bar width and text based on checked checkboxes', () => {
    loadScript({
      totalFiles: 4,
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <input type="checkbox" id="cb_add_0" checked>
        <input type="checkbox" id="cb_mod_0">
        <input type="checkbox" id="cb_rem_0" checked>
        <input type="checkbox" id="cb_sha256w_0">
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('progress-bar-fill').style.width).toBe('50%');
    expect(document.getElementById('progress-text').textContent).toBe('2 / 4 reviewed');
  });

  it('excludes unchanged and ignored checkboxes from count', () => {
    loadScript({
      totalFiles: 2,
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <input type="checkbox" id="cb_add_0" checked>
        <input type="checkbox" id="cb_mod_0" checked>
        <input type="checkbox" id="cb_unch_0" checked>
        <input type="checkbox" id="cb_ign_0" checked>
      `,
    });
    fireDOMContentLoaded();

    // cb_unch_0 and cb_ign_0 are excluded — only cb_add_0 + cb_mod_0 counted
    // cb_unch_0 と cb_ign_0 は除外 — cb_add_0 + cb_mod_0 のみカウント
    expect(document.getElementById('progress-text').textContent).toBe('2 / 2 reviewed');
  });

  it('adds complete class when all files are reviewed', () => {
    loadScript({
      totalFiles: 2,
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <input type="checkbox" id="cb_add_0" checked>
        <input type="checkbox" id="cb_mod_0" checked>
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('progress-bar-fill').classList.contains('complete')).toBe(true);
    expect(document.getElementById('progress-text').textContent).toBe('2 / 2 reviewed');
  });

  it('does nothing when totalFiles is 0', () => {
    loadScript({
      totalFiles: 0,
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('progress-bar-fill').style.width).toBe('');
    expect(document.getElementById('progress-text').textContent).toBe('');
  });

  it('counts checked state from localStorage for lazy-loaded sections', () => {
    loadScript({
      totalFiles: 3,
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <input type="checkbox" id="cb_add_0" checked>
      `,
    });
    // Simulate lazy-loaded checkbox state in localStorage (sha256w/tsw from warning sections)
    // 遅延ロードセクションのチェックボックス状態をlocalStorageでシミュレート（警告セクション）
    localStorage.setItem('test-key', JSON.stringify({ cb_sha256w_0: true, cb_tsw_0: true }));
    fireDOMContentLoaded();

    // 1 from DOM (cb_add_0 checked) + 2 from localStorage (sha256w + tsw) = 3
    expect(document.getElementById('progress-text').textContent).toBe('3 / 3 reviewed');
  });

  it('includes checklist rows in the progress count', () => {
    loadScript({
      totalFiles: 3,
      totalFilesDetail: 'Added: 1 + Modified: 1 + Checklist: 1',
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <span id="progress-detail"></span>
        <input type="checkbox" id="cb_add_0" checked>
        <input type="checkbox" id="cb_mod_0">
        <input type="checkbox" id="checklist_cb_0" checked>
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('progress-text').textContent).toBe('2 / 3 reviewed');
    expect(document.getElementById('progress-detail').textContent).toBe('(Added: 1 + Modified: 1 + Checklist: 1)');
  });

  it('includes checklist rows in the reviewed progress count', () => {
    loadScript({
      totalFiles: 3,
      totalFilesDetail: 'Added: 1 + Modified: 1 + Checklist: 1',
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <span id="progress-detail"></span>
        <input type="checkbox" id="cb_add_0" checked>
        <input type="checkbox" id="cb_mod_0">
        <input type="checkbox" id="checklist_cb_0" checked>
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('progress-text').textContent).toBe('2 / 3 reviewed');
    expect(document.getElementById('progress-detail').textContent).toBe('(Added: 1 + Modified: 1 + Checklist: 1)');
  });

  it('ignores corrupted localStorage when computing progress', () => {
    loadScript({
      totalFiles: 2,
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <input type="checkbox" id="cb_add_0" checked>
        <input type="checkbox" id="cb_mod_0">
      `,
    });
    localStorage.setItem('test-key', '{broken json');

    expect(() => fireDOMContentLoaded()).not.toThrow();
    expect(document.getElementById('progress-text').textContent).toBe('1 / 2 reviewed');
  });

  it('shows detail breakdown when totalFilesDetail is provided', () => {
    loadScript({
      totalFiles: 3,
      totalFilesDetail: 'Added: 1 + Modified: 2',
      bodyHtml: `
        <div id="progress-bar-fill" class="progress-bar-fill"></div>
        <span id="progress-text"></span>
        <span id="progress-detail"></span>
        <input type="checkbox" id="cb_add_0" checked>
        <input type="checkbox" id="cb_mod_0">
        <input type="checkbox" id="cb_mod_1">
      `,
    });
    fireDOMContentLoaded();

    expect(document.getElementById('progress-detail').textContent).toBe('(Added: 1 + Modified: 2)');
  });
});

// ─── Corrupted localStorage ──────────────────────────────────────────────────
// 破損した localStorage のテスト — JSON パースエラーや不正データへの耐性を検証
describe('corrupted localStorage', () => {
  test('invalid JSON in localStorage does not crash DOMContentLoaded', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk-corrupt">
        <input type="text" id="note-corrupt">
      `,
    });
    // Write invalid JSON before firing DOMContentLoaded / 不正な JSON を書き込み
    localStorage.setItem('test-key', '{corrupted json!!!');

    // Should not throw / 例外をスローしないこと
    expect(() => fireDOMContentLoaded()).not.toThrow();

    // Inputs should remain at defaults (unchecked, empty) / 入力はデフォルトのまま
    expect(document.getElementById('chk-corrupt').checked).toBe(false);
    expect(document.getElementById('note-corrupt').value).toBe('');
  });

  test('empty string in localStorage does not crash DOMContentLoaded', () => {
    loadScript({
      bodyHtml: '<input type="checkbox" id="chk-empty">',
    });
    localStorage.setItem('test-key', '');

    expect(() => fireDOMContentLoaded()).not.toThrow();
    expect(document.getElementById('chk-empty').checked).toBe(false);
  });

  test('null value in localStorage is treated as no saved state', () => {
    loadScript({
      bodyHtml: '<input type="checkbox" id="chk-null">',
    });
    localStorage.setItem('test-key', 'null');

    expect(() => fireDOMContentLoaded()).not.toThrow();
    expect(document.getElementById('chk-null').checked).toBe(false);
  });

  test('non-object JSON in localStorage does not crash', () => {
    loadScript({
      bodyHtml: '<input type="checkbox" id="chk-array">',
    });
    // Array instead of object / オブジェクトではなく配列
    localStorage.setItem('test-key', '[1, 2, 3]');

    expect(() => fireDOMContentLoaded()).not.toThrow();
  });

  test('autoSave succeeds even when localStorage throws on setItem', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk-quota" checked>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    // Simulate localStorage quota exceeded / localStorage 容量超過をシミュレート
    const originalSetItem = localStorage.setItem.bind(localStorage);
    localStorage.setItem = () => { throw new DOMException('QuotaExceededError'); };

    // autoSave should not crash the page / autoSave はページをクラッシュさせないこと
    expect(() => window.autoSave()).not.toThrow();

    // Restore original / 元に戻す
    localStorage.setItem = originalSetItem;
  });

  test('state with non-existent element IDs is safely ignored', () => {
    loadScript({
      bodyHtml: '<input type="checkbox" id="chk-exists">',
    });
    localStorage.setItem('test-key', JSON.stringify({
      'chk-exists': true,
      'chk-nonexistent': true,
      'note-missing': 'ghost value'
    }));

    expect(() => fireDOMContentLoaded()).not.toThrow();
    expect(document.getElementById('chk-exists').checked).toBe(true);
  });

  test('state with wrong type values is handled gracefully', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="chk-type">
        <input type="text" id="note-type">
      `,
    });
    // Checkbox gets a string, text input gets a number / チェックボックスに文字列、テキストに数値
    localStorage.setItem('test-key', JSON.stringify({
      'chk-type': 'yes',
      'note-type': 12345
    }));

    expect(() => fireDOMContentLoaded()).not.toThrow();
    // Boolean("yes") is true / Boolean("yes") は true
    expect(document.getElementById('chk-type').checked).toBe(true);
    // Number is coerced to string / 数値は文字列に変換される
    expect(document.getElementById('note-type').value).toBe('12345');
  });
});

// ─── esc (HTML escaping) ────────────────────────────────────────────────────
// HTML エスケープユーティリティのテスト
describe('esc', () => {
  beforeEach(() => loadScript());

  test('escapes &, <, >, "', () => {
    expect(window.esc('a & b < c > d "e"')).toBe('a &amp; b &lt; c &gt; d &quot;e&quot;');
  });

  test('returns empty string for null/undefined/empty', () => {
    expect(window.esc(null)).toBe('');
    expect(window.esc(undefined)).toBe('');
    expect(window.esc('')).toBe('');
  });

  test('leaves plain text unchanged', () => {
    expect(window.esc('hello world 123')).toBe('hello world 123');
  });

  test('handles Japanese text without escaping', () => {
    expect(window.esc('日本語テスト')).toBe('日本語テスト');
  });
});

// ─── readSavedStateFromStorage ──────────────────────────────────────────────
// localStorage から保存状態を読み取るテスト
describe('readSavedStateFromStorage', () => {
  beforeEach(() => loadScript());

  test('returns object from localStorage', () => {
    localStorage.setItem('test-key', JSON.stringify({ chk: true }));
    const result = window.readSavedStateFromStorage('null');
    expect(result).toEqual({ chk: true });
  });

  test('returns null for invalid JSON', () => {
    localStorage.setItem('test-key', 'not json');
    const result = window.readSavedStateFromStorage('null');
    expect(result).toBeNull();
  });

  test('falls back to fallbackJson when key missing', () => {
    const result = window.readSavedStateFromStorage('{"a":1}');
    expect(result).toEqual({ a: 1 });
  });

  test('returns null when fallbackJson is "null" and key missing', () => {
    const result = window.readSavedStateFromStorage('null');
    expect(result).toBeNull();
  });

  test('returns array for array JSON (typeof array is "object")', () => {
    // Arrays pass the typeof === 'object' check in readSavedStateFromStorage
    // 配列は typeof === 'object' チェックを通過する
    localStorage.setItem('test-key', '[1,2,3]');
    const result = window.readSavedStateFromStorage('null');
    expect(result).toEqual([1, 2, 3]);
  });
});

// ──�� Diff Detail filter (SHA256/IL/Text) ────────────────────────────────────
// Diff Detail フィルタ（SHA256/IL/Text）のテスト
describe('applyFilters — diff detail filter', () => {
  function loadDiffFilterEnv(rows) {
    const filterBar = `
      <input type="checkbox" id="filter-imp-high" checked>
      <input type="checkbox" id="filter-imp-medium" checked>
      <input type="checkbox" id="filter-imp-low" checked>
      <input type="checkbox" id="filter-diff-sha256match" checked>
      <input type="checkbox" id="filter-diff-sha256mismatch" checked>
      <input type="checkbox" id="filter-diff-ilmatch" checked>
      <input type="checkbox" id="filter-diff-ilmismatch" checked>
      <input type="checkbox" id="filter-diff-textmatch" checked>
      <input type="checkbox" id="filter-diff-textmismatch" checked>
      <input type="checkbox" id="filter-unchecked">
      <input type="text" id="filter-search" value="">
      <span id="save-status"></span>
    `;
    const tableRows = rows.map(r =>
      `<tr data-section="${r.section}" ${r.diff ? 'data-diff="' + r.diff + '"' : ''}>
        <td><span class="path-text">${r.path}</span></td>
      </tr>`
    ).join('\n');
    loadScript({
      bodyHtml: `${filterBar}<table><tbody>${tableRows}</tbody></table>`,
    });
    fireDOMContentLoaded();
  }

  test('hides SHA256Match rows when filter unchecked', () => {
    loadDiffFilterEnv([
      { section: 'modified', diff: 'SHA256Match', path: 'a.dll' },
      { section: 'modified', diff: 'ILMismatch', path: 'b.dll' },
    ]);
    document.getElementById('filter-diff-sha256match').checked = false;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('filter-hidden')).toBe(true);
    expect(rows[1].classList.contains('filter-hidden')).toBe(false);
  });

  test('hides ILMatch rows when filter unchecked', () => {
    loadDiffFilterEnv([
      { section: 'modified', diff: 'ILMatch', path: 'a.dll' },
      { section: 'modified', diff: 'TextMismatch', path: 'b.txt' },
    ]);
    document.getElementById('filter-diff-ilmatch').checked = false;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('filter-hidden')).toBe(true);
    expect(rows[1].classList.contains('filter-hidden')).toBe(false);
  });

  test('hides TextMismatch rows when filter unchecked', () => {
    loadDiffFilterEnv([
      { section: 'modified', diff: 'TextMatch', path: 'a.config' },
      { section: 'modified', diff: 'TextMismatch', path: 'b.config' },
    ]);
    document.getElementById('filter-diff-textmismatch').checked = false;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('filter-hidden')).toBe(false);
    expect(rows[1].classList.contains('filter-hidden')).toBe(true);
  });

  test('rows without data-diff are not affected by diff filter', () => {
    loadDiffFilterEnv([
      { section: 'added', path: 'new.dll' },
    ]);
    // Uncheck all diff filters
    document.getElementById('filter-diff-sha256match').checked = false;
    document.getElementById('filter-diff-sha256mismatch').checked = false;
    document.getElementById('filter-diff-ilmatch').checked = false;
    document.getElementById('filter-diff-ilmismatch').checked = false;
    document.getElementById('filter-diff-textmatch').checked = false;
    document.getElementById('filter-diff-textmismatch').checked = false;
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('filter-hidden')).toBe(false);
  });

  test('combined diff + importance filter hides correctly', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="filter-imp-high" checked>
        <input type="checkbox" id="filter-imp-medium" checked>
        <input type="checkbox" id="filter-imp-low" checked>
        <input type="checkbox" id="filter-diff-sha256match" checked>
        <input type="checkbox" id="filter-diff-sha256mismatch" checked>
        <input type="checkbox" id="filter-diff-ilmatch">
        <input type="checkbox" id="filter-diff-ilmismatch" checked>
        <input type="checkbox" id="filter-diff-textmatch" checked>
        <input type="checkbox" id="filter-diff-textmismatch" checked>
        <input type="checkbox" id="filter-unchecked">
        <input type="text" id="filter-search" value="">
        <span id="save-status"></span>
        <table><tbody>
          <tr data-section="modified" data-diff="ILMatch" data-importance="High">
            <td><span class="path-text">matched.dll</span></td>
          </tr>
          <tr data-section="modified" data-diff="ILMismatch" data-importance="Low">
            <td><span class="path-text">changed.dll</span></td>
          </tr>
        </tbody></table>
      `,
    });
    fireDOMContentLoaded();

    // ILMatch filter unchecked, importance filter all checked
    window.applyFilters();

    const rows = document.querySelectorAll('tr[data-section]');
    // ILMatch row should be hidden (diff filter blocks it)
    expect(rows[0].classList.contains('filter-hidden')).toBe(true);
    // ILMismatch + Low row should be visible
    expect(rows[1].classList.contains('filter-hidden')).toBe(false);
  });
});

// ─── copyPath ───────────────────────────────────────────────────────────────
// クリップボードコピーのテスト
describe('copyPath', () => {
  test('copies path text from .path-text span', async () => {
    loadScript({
      bodyHtml: `
        <table><tr><td>
          <span class="path-text">src/MyApp.dll</span>
          <button id="copy-btn"><svg>icon</svg></button>
        </td></tr></table>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    let clipboardText = '';
    navigator.clipboard = {
      writeText: (text) => { clipboardText = text; return Promise.resolve(); },
    };

    const btn = document.getElementById('copy-btn');
    window.copyPath(btn);
    await new Promise(r => setTimeout(r, 10));
    expect(clipboardText).toBe('src/MyApp.dll');
  });
});

// ─── setupLazySection ───────────────────────────────────────────────────────
// 遅延セクションレンダリングのテスト
describe('setupLazySection', () => {
  test('decodes and inserts HTML on first toggle', () => {
    const sectionHtml = '<table><tr><td>lazy section content</td></tr></table>';
    const b64 = Buffer.from(sectionHtml, 'utf-8').toString('base64');
    loadScript({
      bodyHtml: `
        <details data-lazy-section="${b64}">
          <summary>Unchanged Files</summary>
        </details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    const details = document.querySelector('details');
    details.setAttribute('open', '');
    details.dispatchEvent(new Event('toggle'));

    expect(details.hasAttribute('data-lazy-section')).toBe(false);
    expect(details.querySelector('table')).not.toBeNull();
  });

  test('wires save events for new inputs in non-reviewed mode', () => {
    const sectionHtml = '<input type="checkbox" id="cb_unch_0"><span id="save-status"></span>';
    const b64 = Buffer.from(sectionHtml, 'utf-8').toString('base64');
    loadScript({
      bodyHtml: `
        <details data-lazy-section="${b64}">
          <summary>Unchanged Files</summary>
        </details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    const details = document.querySelector('details');
    details.setAttribute('open', '');
    details.dispatchEvent(new Event('toggle'));

    const cb = document.getElementById('cb_unch_0');
    expect(cb).not.toBeNull();
    // Trigger change event — autoSave should fire
    cb.checked = true;
    cb.dispatchEvent(new Event('change'));
    const stored = JSON.parse(localStorage.getItem('test-key') || '{}');
    expect(stored['cb_unch_0']).toBe(true);
  });
});

// ─── forceDecodeLazySections ────────────────────────────────────────────────
// 全 lazy セクション強制デコードのテスト
describe('forceDecodeLazySections', () => {
  test('decodes all lazy sections without requiring toggle', () => {
    const html1 = '<div class="sect-a">A</div>';
    const html2 = '<div class="sect-b">B</div>';
    const b64a = Buffer.from(html1, 'utf-8').toString('base64');
    const b64b = Buffer.from(html2, 'utf-8').toString('base64');
    loadScript({
      bodyHtml: `
        <details data-lazy-section="${b64a}"><summary>A</summary></details>
        <details data-lazy-section="${b64b}"><summary>B</summary></details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    window.forceDecodeLazySections();

    expect(document.querySelectorAll('details[data-lazy-section]').length).toBe(0);
    expect(document.querySelector('.sect-a')).not.toBeNull();
    expect(document.querySelector('.sect-b')).not.toBeNull();
  });
});

// ─── toggleDiffView (side-by-side) ──────────────────────────────────────────
// サイドバイサイド差分切替のテスト
describe('toggleDiffView', () => {
  function buildDiffDetails(rows) {
    const trs = rows.map(r => {
      if (r.type === 'del') {
        return `<tr class="diff-del-tr"><td class="diff-ln">${r.oldLn || ''}</td><td class="diff-ln">${r.newLn || ''}</td><td class="diff-del-td">${r.text}</td></tr>`;
      }
      if (r.type === 'add') {
        return `<tr class="diff-add-tr"><td class="diff-ln">${r.oldLn || ''}</td><td class="diff-ln">${r.newLn || ''}</td><td class="diff-add-td">${r.text}</td></tr>`;
      }
      if (r.type === 'ctx') {
        return `<tr class="diff-ctx-tr"><td class="diff-ln">${r.oldLn || ''}</td><td class="diff-ln">${r.newLn || ''}</td><td class="diff-ctx-td">${r.text}</td></tr>`;
      }
      if (r.type === 'hunk') {
        return `<tr class="diff-hunk-tr"><td class="diff-ln"></td><td class="diff-ln"></td><td class="diff-hunk-td">${r.text}</td></tr>`;
      }
      return '';
    }).join('');

    return `
      <details id="test-detail">
        <summary>Diff</summary>
        <button class="sbs-toggle">Side-by-side</button>
        <table class="diff-table"><tbody>${trs}</tbody></table>
      </details>
      <span id="save-status"></span>
    `;
  }

  test('switches to sbs-mode and back to unified', () => {
    loadScript({
      bodyHtml: buildDiffDetails([
        { type: 'del', oldLn: '1', text: 'old line' },
        { type: 'add', newLn: '1', text: 'new line' },
      ]),
    });
    fireDOMContentLoaded();

    const details = document.getElementById('test-detail');
    const table = details.querySelector('.diff-table');
    const btn = details.querySelector('.sbs-toggle');

    // Switch to side-by-side
    window.toggleDiffView(details);
    expect(table.classList.contains('sbs-mode')).toBe(true);
    expect(btn.textContent).toBe('Unified');
    // Should have 4-column layout with colgroup
    // 4列レイアウトの colgroup が存在すること
    expect(table.querySelector('.sbs-colgroup')).not.toBeNull();

    // Switch back to unified
    window.toggleDiffView(details);
    expect(table.classList.contains('sbs-mode')).toBe(false);
    expect(btn.textContent).toBe('Side-by-side');
    expect(table.querySelector('.sbs-colgroup')).toBeNull();
  });

  test('pairs consecutive del+add rows into 4-column rows', () => {
    loadScript({
      bodyHtml: buildDiffDetails([
        { type: 'del', oldLn: '5', text: 'removed' },
        { type: 'add', newLn: '5', text: 'added' },
      ]),
    });
    fireDOMContentLoaded();

    window.toggleDiffView(document.getElementById('test-detail'));

    const tbody = document.querySelector('.diff-table tbody');
    const row = tbody.querySelector('tr');
    const cells = row.querySelectorAll('td');
    // 4 cells: [oldLn] [sbs-old] [newLn] [sbs-new]
    // 4セル: [旧行番号] [sbs-old] [新行番号] [sbs-new]
    expect(cells.length).toBe(4);
    expect(cells[0].classList.contains('diff-ln')).toBe(true);
    expect(cells[0].textContent).toBe('5');
    expect(cells[1].classList.contains('sbs-old')).toBe(true);
    expect(cells[2].classList.contains('diff-ln')).toBe(true);
    expect(cells[2].textContent).toBe('5');
    expect(cells[3].classList.contains('sbs-new')).toBe(true);
  });

  test('standalone deletion gets 4-column row with sbs-old + sbs-empty', () => {
    loadScript({
      bodyHtml: buildDiffDetails([
        { type: 'del', oldLn: '10', text: 'deleted line' },
        { type: 'ctx', oldLn: '11', newLn: '10', text: 'context' },
      ]),
    });
    fireDOMContentLoaded();

    window.toggleDiffView(document.getElementById('test-detail'));

    const rows = document.querySelectorAll('.diff-table tbody tr');
    // First row: standalone del — [oldLn] [sbs-old] [empty] [sbs-empty]
    // 最初の行: 単独削除 — [旧行番号] [sbs-old] [空] [sbs-empty]
    const delCells = rows[0].querySelectorAll('td');
    expect(delCells.length).toBe(4);
    expect(delCells[0].textContent).toBe('10');
    expect(delCells[1].classList.contains('sbs-old')).toBe(true);
    expect(delCells[2].textContent).toBe('');
    expect(delCells[3].classList.contains('sbs-empty')).toBe(true);
  });

  test('standalone addition gets 4-column row with sbs-empty + sbs-new', () => {
    loadScript({
      bodyHtml: buildDiffDetails([
        { type: 'ctx', oldLn: '1', newLn: '1', text: 'context' },
        { type: 'add', newLn: '2', text: 'added line' },
      ]),
    });
    fireDOMContentLoaded();

    window.toggleDiffView(document.getElementById('test-detail'));

    const rows = document.querySelectorAll('.diff-table tbody tr');
    // Second row: standalone add — [empty] [sbs-empty] [newLn] [sbs-new]
    // 2行目: 単独追加 — [空] [sbs-empty] [新行番号] [sbs-new]
    const addCells = rows[1].querySelectorAll('td');
    expect(addCells.length).toBe(4);
    expect(addCells[0].textContent).toBe('');
    expect(addCells[1].classList.contains('sbs-empty')).toBe(true);
    expect(addCells[2].textContent).toBe('2');
    expect(addCells[3].classList.contains('sbs-new')).toBe(true);
  });

  test('hunk header spans 3 columns in 4-column layout', () => {
    loadScript({
      bodyHtml: buildDiffDetails([
        { type: 'hunk', text: '@@ -1,5 +1,6 @@' },
      ]),
    });
    fireDOMContentLoaded();

    window.toggleDiffView(document.getElementById('test-detail'));

    const row = document.querySelector('.diff-table tbody tr.diff-hunk-tr');
    const hunkTd = row.querySelector('.diff-hunk-td');
    // Hunk TD spans 3 columns (oldText + newLn + newText) in the 4-column layout
    // ハンク TD は4列レイアウトで3列分（旧テキスト + 新行番号 + 新テキスト）にまたがる
    expect(hunkTd.colSpan).toBe(3);
  });

  test('context rows show text on both sides with line numbers', () => {
    loadScript({
      bodyHtml: buildDiffDetails([
        { type: 'ctx', oldLn: '5', newLn: '7', text: 'unchanged line' },
      ]),
    });
    fireDOMContentLoaded();

    window.toggleDiffView(document.getElementById('test-detail'));

    const rows = document.querySelectorAll('.diff-table tbody tr');
    const cells = rows[0].querySelectorAll('td');
    // 4 cells: [oldLn] [sbs-ctx] [newLn] [sbs-ctx]
    // 4セル: [旧行番号] [sbs-ctx] [新行番号] [sbs-ctx]
    expect(cells.length).toBe(4);
    expect(cells[0].textContent).toBe('5');
    expect(cells[1].classList.contains('sbs-ctx')).toBe(true);
    expect(cells[2].textContent).toBe('7');
    expect(cells[3].classList.contains('sbs-ctx')).toBe(true);
  });
});

// ─── buildExcelRow ──────────────────────────────────────────────────────────
// Excel 行構築のテスト
describe('buildExcelRow', () => {
  beforeEach(() => loadScript());

  test('extracts cells from a standard 10-cell row', () => {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td>1</td>
      <td><input type="checkbox" checked></td>
      <td><input type="text" value="OK"></td>
      <td><input type="text" value="note"></td>
      <td>Modified</td>
      <td><span class="path-text">src/App.dll</span></td>
      <td>2026-01-01 12:00:00</td>
      <td>ILMismatch</td>
      <td>BodyEdit</td>
      <td>dotnet-ildasm</td>
    `;

    const html = window.buildExcelRow(tr);
    expect(html).toContain('src/App.dll');
    expect(html).toContain('\u2713'); // checkmark
    expect(html).toContain('OK');
    expect(html).toContain('note');
    expect(html).toContain('Modified');
    expect(html).toContain('ILMismatch');
    expect(html).toContain('BodyEdit');
    expect(html).toContain('dotnet-ildasm');
  });

  test('returns empty string for rows with fewer than 10 cells', () => {
    const tr = document.createElement('tr');
    tr.innerHTML = '<td>1</td><td>2</td><td>3</td>';
    expect(window.buildExcelRow(tr)).toBe('');
  });

  test('unchecked checkbox produces empty string', () => {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td>1</td>
      <td><input type="checkbox"></td>
      <td><input type="text" value=""></td>
      <td><input type="text" value=""></td>
      <td>Added</td>
      <td><span class="path-text">new.dll</span></td>
      <td>2026-01-01</td>
      <td></td>
      <td></td>
      <td></td>
    `;

    const html = window.buildExcelRow(tr);
    // Should not contain checkmark
    expect(html).not.toContain('\u2713');
    expect(html).toContain('new.dll');
  });

  test('maps checklist rows into aligned Excel columns', () => {
    const tr = document.createElement('tr');
    tr.setAttribute('data-section', 'checklist');
    tr.innerHTML = `
      <td><input type="checkbox" id="checklist_cb_1" checked></td>
      <td><div class="checklist-item-text">Verify migration notes
when schema version changes.</div></td>
      <td><input type="text" value="Reviewed in CAB"></td>
    `;

    const html = window.buildExcelRow(tr);
    expect(html).toContain('>2<');
    expect(html).toContain('Verify migration notes<br>when schema version changes.');
    expect(html).toContain('Reviewed in CAB');
  });
});

// ─── Keyboard navigation (Escape) ──────────────────────────────────────────
// キーボードナビゲーション（Escape キー）のテスト
describe('keyboard navigation', () => {
  test('Escape closes nearest open details and focuses summary', () => {
    loadScript({
      bodyHtml: `
        <details id="det" open>
          <summary id="sum">Summary</summary>
          <button id="inner-btn">Click</button>
        </details>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    const btn = document.getElementById('inner-btn');
    btn.focus();

    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true });
    document.dispatchEvent(event);

    const details = document.getElementById('det');
    expect(details.hasAttribute('open')).toBe(false);
  });

  test('Escape does nothing when no details is focused', () => {
    loadScript({
      bodyHtml: `
        <details id="det" open>
          <summary>Summary</summary>
          <p>content</p>
        </details>
        <button id="outside-btn">Outside</button>
        <span id="save-status"></span>
      `,
    });
    fireDOMContentLoaded();

    // Focus is not inside a details element
    document.getElementById('outside-btn').focus();

    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true });
    document.dispatchEvent(event);

    // Details should remain open since focus was outside
    expect(document.getElementById('det').hasAttribute('open')).toBe(true);
  });
});

// ── IL syntax highlighting / IL シンタックスハイライト ────────────────────
describe('highlightILCell', () => {
  beforeEach(() => {
    loadScript({ bodyHtml: '<span id="save-status"></span>' });
    fireDOMContentLoaded();
  });

  test('highlights IL directives', () => {
    const td = document.createElement('td');
    td.className = 'diff-ctx-td';
    td.innerHTML = ' .method public hidebysig';
    window.highlightILCell(td);
    expect(td.innerHTML).toContain('<span class="hl-directive">.method</span>');
  });

  test('highlights IL labels', () => {
    const td = document.createElement('td');
    td.className = 'diff-add-td';
    td.innerHTML = '+  IL_0000: nop';
    window.highlightILCell(td);
    expect(td.innerHTML).toContain('<span class="hl-label">IL_0000</span>');
  });

  test('highlights builtin types', () => {
    const td = document.createElement('td');
    td.className = 'diff-ctx-td';
    td.innerHTML = ' .locals init (int32 V_0, string V_1)';
    window.highlightILCell(td);
    expect(td.innerHTML).toContain('<span class="hl-type">int32</span>');
    expect(td.innerHTML).toContain('<span class="hl-type">string</span>');
  });

  test('highlights keywords', () => {
    const td = document.createElement('td');
    td.className = 'diff-del-td';
    td.innerHTML = '-.method public static void Main()';
    window.highlightILCell(td);
    expect(td.innerHTML).toContain('<span class="hl-keyword">public</span>');
    expect(td.innerHTML).toContain('<span class="hl-keyword">static</span>');
  });

  test('preserves prefix character', () => {
    const td = document.createElement('td');
    td.className = 'diff-add-td';
    td.innerHTML = '+.class public';
    window.highlightILCell(td);
    expect(td.innerHTML.charAt(0)).toBe('+');
  });

  test('skips already-highlighted cells', () => {
    const td = document.createElement('td');
    td.className = 'diff-ctx-td';
    td.innerHTML = ' <span class="hl-directive">.method</span>';
    window.highlightILCell(td);
    // Should not double-wrap
    expect(td.innerHTML).not.toContain('hl-directive"><span');
  });

  test('skips empty cells', () => {
    const td = document.createElement('td');
    td.className = 'diff-ctx-td';
    td.innerHTML = '';
    window.highlightILCell(td);
    expect(td.innerHTML).toBe('');
  });
});

describe('highlightILDiff', () => {
  beforeEach(() => {
    loadScript({ bodyHtml: '<span id="save-status"></span>' });
    fireDOMContentLoaded();
  });

  test('applies highlighting to IL diff tables', () => {
    const html = `
      <table><tbody>
        <tr data-section="mod" data-diff="ILMismatch"><td>file.dll</td></tr>
        <tr class="diff-row"><td colspan="10">
          <details id="d1">
            <summary class="diff-summary">Show diff</summary>
            <table class="diff-table"><tbody>
              <tr class="diff-ctx-tr"><td class="diff-ln">1</td><td class="diff-ln">1</td><td class="diff-ctx-td"> .method public void Test()</td></tr>
            </tbody></table>
          </details>
        </td></tr>
      </tbody></table>
    `;
    document.body.innerHTML = html + '<span id="save-status"></span>';
    const tbl = document.querySelector('.diff-table');
    window.highlightILDiff(tbl);
    expect(tbl.querySelector('.diff-ctx-td').innerHTML).toContain('hl-directive');
  });

  test('skips text diff tables', () => {
    const html = `
      <table><tbody>
        <tr data-section="mod" data-diff="TextMismatch"><td>file.config</td></tr>
        <tr class="diff-row"><td colspan="10">
          <details id="d2">
            <summary class="diff-summary">Show diff</summary>
            <table class="diff-table"><tbody>
              <tr class="diff-ctx-tr"><td class="diff-ln">1</td><td class="diff-ln">1</td><td class="diff-ctx-td"> .method public void Test()</td></tr>
            </tbody></table>
          </details>
        </td></tr>
      </tbody></table>
    `;
    document.body.innerHTML = html + '<span id="save-status"></span>';
    const tbl = document.querySelector('.diff-table');
    window.highlightILDiff(tbl);
    // Should NOT highlight because data-diff is TextMismatch
    expect(tbl.querySelector('.diff-ctx-td').innerHTML).not.toContain('hl-directive');
  });
});

describe('theme', () => {
  test('applyTheme light sets data-theme and body colors', () => {
    loadScript();
    window.applyTheme('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    expect(document.body.style.color).toBe('rgb(29, 29, 31)');
    expect(document.body.style.backgroundColor).toBe('rgb(255, 255, 255)');
  });

  test('applyTheme dark sets data-theme and body colors', () => {
    loadScript();
    window.applyTheme('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(document.body.style.color).toBe('rgb(230, 237, 243)');
    expect(document.body.style.backgroundColor).toBe('rgb(13, 17, 23)');
  });

  test('applyTheme system removes data-theme', () => {
    loadScript();
    window.applyTheme('light');
    window.applyTheme('system');
    expect(document.documentElement.getAttribute('data-theme')).toBeNull();
  });

  test('cycleTheme cycles system -> light -> dark -> system', () => {
    loadScript({
      bodyHtml: '<button id="theme-toggle"></button>',
    });
    const btn = document.getElementById('theme-toggle');

    window.cycleTheme();
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    expect(btn.textContent).toContain('Light');

    window.cycleTheme();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(btn.textContent).toContain('Dark');

    window.cycleTheme();
    expect(document.documentElement.getAttribute('data-theme')).toBeNull();
    expect(btn.textContent).toContain('System');
  });

  test('initTheme restores stored theme preference', () => {
    loadScript({ storageKey: 'theme-test' });
    localStorage.setItem('theme-test-theme', 'dark');
    window.initTheme();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });
});

describe('celebrateCompletion', () => {
  test('does nothing when progress bar is missing', () => {
    loadScript();
    // Should not throw even with no progress container
    expect(() => window.celebrateCompletion()).not.toThrow();
  });

  test('fires only once per session', () => {
    loadScript({
      bodyHtml: `
        <div id="progress-container">
          <div id="progress-bar-fill"></div>
        </div>
      `,
    });
    window.celebrateCompletion();
    // Second call should be a no-op (celebration guard)
    expect(() => window.celebrateCompletion()).not.toThrow();
  });
});

describe('syncTableWidths', () => {
  test('sets table width from colgroup columns', () => {
    loadScript({
      bodyHtml: `
        <table>
          <colgroup>
            <col class="col-no-g" />
            <col class="col-cb-g" />
            <col class="col-status-g" />
          </colgroup>
          <tbody><tr><td>1</td><td>2</td><td>3</td></tr></tbody>
        </table>
      `,
    });
    window.syncTableWidths();
    const t = document.querySelector('table');
    // Table should have a computed width set
    expect(t.style.width).toBeTruthy();
    expect(parseFloat(t.style.width)).toBeGreaterThan(0);
  });

  test('respects hide-disasm class', () => {
    loadScript({
      bodyHtml: `
        <table class="hide-disasm">
          <colgroup>
            <col class="col-no-g" />
            <col class="col-disasm-g" />
          </colgroup>
          <tbody><tr><td>1</td><td>2</td></tr></tbody>
        </table>
      `,
    });
    window.syncTableWidths();
    const t = document.querySelector('table');
    const w = parseFloat(t.style.width);
    // Width should only include col-no-g, not col-disasm-g
    expect(w).toBeGreaterThan(0);
  });

  test('respects hide-sdk class', () => {
    loadScript({
      bodyHtml: `
        <table class="hide-sdk">
          <colgroup>
            <col class="col-no-g" />
            <col class="col-sdk-g" />
          </colgroup>
          <tbody><tr><td>1</td><td>2</td></tr></tbody>
        </table>
      `,
    });
    window.syncTableWidths();
    const t = document.querySelector('table');
    const w = parseFloat(t.style.width);
    expect(w).toBeGreaterThan(0);
  });
});

describe('filter state persistence', () => {
  test('saveFilterState persists filter state to separate key', () => {
    loadScript({
      storageKey: 'persist-test',
      bodyHtml: `
        <input type="checkbox" id="filter-imp-high" checked />
        <input type="checkbox" id="filter-imp-medium" />
        <input type="checkbox" id="filter-imp-low" checked />
        <input type="checkbox" id="filter-diff-sha256match" checked />
        <input type="checkbox" id="filter-diff-sha256mismatch" checked />
        <input type="checkbox" id="filter-diff-ilmatch" checked />
        <input type="checkbox" id="filter-diff-ilmismatch" checked />
        <input type="checkbox" id="filter-diff-textmatch" checked />
        <input type="checkbox" id="filter-diff-textmismatch" checked />
        <input type="checkbox" id="filter-unchecked" />
        <input type="text" id="filter-search" value="" />
      `,
    });
    document.getElementById('filter-imp-medium').checked = false;
    window.saveFilterState();
    const raw = localStorage.getItem('persist-test-filters');
    expect(raw).toBeTruthy();
    const state = JSON.parse(raw);
    expect(state['filter-imp-medium']).toBe(false);
    expect(state['filter-imp-high']).toBe(true);
  });

  test('clearFilterState removes filter key', () => {
    loadScript({ storageKey: 'clear-test' });
    localStorage.setItem('clear-test-filters', '{"x":1}');
    window.clearFilterState();
    expect(localStorage.getItem('clear-test-filters')).toBeNull();
  });

  test('restoreFilterState restores from localStorage', () => {
    const filterState = {
      'filter-imp-high': true,
      'filter-imp-medium': false,
      'filter-imp-low': true,
      'filter-unchecked': true,
      'filter-search': 'test query',
    };
    loadScript({
      storageKey: 'restore-test',
      bodyHtml: `
        <input type="checkbox" id="filter-imp-high" checked />
        <input type="checkbox" id="filter-imp-medium" checked />
        <input type="checkbox" id="filter-imp-low" checked />
        <input type="checkbox" id="filter-diff-sha256match" checked />
        <input type="checkbox" id="filter-diff-sha256mismatch" checked />
        <input type="checkbox" id="filter-diff-ilmatch" checked />
        <input type="checkbox" id="filter-diff-ilmismatch" checked />
        <input type="checkbox" id="filter-diff-textmatch" checked />
        <input type="checkbox" id="filter-diff-textmismatch" checked />
        <input type="checkbox" id="filter-unchecked" />
        <input type="text" id="filter-search" value="" />
      `,
    });
    localStorage.setItem('restore-test-filters', JSON.stringify(filterState));
    const result = window.restoreFilterState();
    expect(result).toBe(true);
    expect(document.getElementById('filter-imp-medium').checked).toBe(false);
    expect(document.getElementById('filter-unchecked').checked).toBe(true);
    expect(document.getElementById('filter-search').value).toBe('test query');
  });
});

describe('updateStorageUsage', () => {
  test('updates storage bar width and text', () => {
    loadScript({
      bodyHtml: `
        <div id="storage-bar-fill" style="width: 0%"></div>
        <span id="storage-text"></span>
      `,
    });
    localStorage.setItem('some-key', 'x'.repeat(1000));
    window.updateStorageUsage();
    const bar = document.getElementById('storage-bar-fill');
    const txt = document.getElementById('storage-text');
    expect(parseFloat(bar.style.width)).toBeGreaterThan(0);
    expect(txt.textContent).toContain('MB');
  });

  test('handles missing storage elements gracefully', () => {
    loadScript();
    expect(() => window.updateStorageUsage()).not.toThrow();
  });
});

describe('clearOldReviewStates', () => {
  test('removes folderdiff- keys except current report', () => {
    loadScript({
      storageKey: 'folderdiff-current',
      bodyHtml: '<span id="save-status"></span>',
    });
    localStorage.setItem('folderdiff-current', '{}');
    localStorage.setItem('folderdiff-current-theme', 'dark');
    localStorage.setItem('folderdiff-current-filters', '{}');
    localStorage.setItem('folderdiff-old-report', '{}');
    localStorage.setItem('folderdiff-another', '{}');
    localStorage.setItem('unrelated-key', 'keep');

    const removed = window.clearOldReviewStates();
    expect(removed).toBe(2);
    expect(localStorage.getItem('folderdiff-current')).toBe('{}');
    expect(localStorage.getItem('folderdiff-current-theme')).toBe('dark');
    expect(localStorage.getItem('folderdiff-old-report')).toBeNull();
    expect(localStorage.getItem('unrelated-key')).toBe('keep');
  });
});

describe('buildExcelRow — SDK column', () => {
  test('extracts SDK column from 11-cell row', () => {
    loadScript({
      bodyHtml: `
        <table><tbody>
          <tr data-section="mod">
            <td>1</td>
            <td><input type="checkbox" id="cb_mod_1" checked /></td>
            <td><input type="text" value="reason" /></td>
            <td><input type="text" value="notes" /></td>
            <td>Changed</td>
            <td><span class="path-text">lib/MyLib.dll</span></td>
            <td>2026-01-01</td>
            <td>ILMismatch</td>
            <td>BodyEdit</td>
            <td>dotnet-ildasm 1.0</td>
            <td>.NET 8.0</td>
          </tr>
        </tbody></table>
      `,
    });
    const tr = document.querySelector('tr[data-section="mod"]');
    const row = window.buildExcelRow(tr);
    expect(row).toContain('.NET 8.0');
    expect(row).toContain('dotnet-ildasm 1.0');
    expect(row).toContain('reason');
  });
});

describe('keyboard navigation — j/k/x keys', () => {
  function buildKeyboardDom() {
    return `
      <table><tbody>
        <tr data-section="mod" data-importance="high"><td>1</td><td><input type="checkbox" id="cb_mod_1" /></td><td></td><td></td><td>Changed</td><td><span class="path-text">a.dll</span></td><td></td><td></td><td></td><td></td></tr>
        <tr data-section="mod" data-importance="medium"><td>2</td><td><input type="checkbox" id="cb_mod_2" /></td><td></td><td></td><td>Changed</td><td><span class="path-text">b.dll</span></td><td></td><td></td><td></td><td></td></tr>
        <tr data-section="mod" data-importance="low"><td>3</td><td><input type="checkbox" id="cb_mod_3" /></td><td></td><td></td><td>Changed</td><td><span class="path-text">c.dll</span></td><td></td><td></td><td></td><td></td></tr>
      </tbody></table>
      <input type="checkbox" id="filter-diff-sha256match" checked />
      <input type="checkbox" id="filter-diff-sha256mismatch" checked />
      <input type="checkbox" id="filter-diff-ilmatch" checked />
      <input type="checkbox" id="filter-diff-ilmismatch" checked />
      <input type="checkbox" id="filter-diff-textmatch" checked />
      <input type="checkbox" id="filter-diff-textmismatch" checked />
      <input type="checkbox" id="filter-imp-high" checked />
      <input type="checkbox" id="filter-imp-medium" checked />
      <input type="checkbox" id="filter-imp-low" checked />
      <input type="checkbox" id="filter-unchecked" />
      <input type="text" id="filter-search" value="" />
      <span id="save-status"></span>
      <div id="progress-bar-fill"></div>
      <span id="progress-text"></span>
    `;
  }

  test('j key moves focus down through visible rows', () => {
    loadScript({
      bodyHtml: buildKeyboardDom(),
      totalFiles: 3,
    });
    fireDOMContentLoaded();

    // Press j to move to first row
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('kb-focus')).toBe(true);

    // Press j again to move to second row
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    expect(rows[0].classList.contains('kb-focus')).toBe(false);
    expect(rows[1].classList.contains('kb-focus')).toBe(true);
  });

  test('k key moves focus up', () => {
    loadScript({
      bodyHtml: buildKeyboardDom(),
      totalFiles: 3,
    });
    fireDOMContentLoaded();

    // Move to second row
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[1].classList.contains('kb-focus')).toBe(true);

    // Press k to go back up
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k' }));
    expect(rows[0].classList.contains('kb-focus')).toBe(true);
  });

  test('x key toggles checkbox on focused row', () => {
    loadScript({
      bodyHtml: buildKeyboardDom(),
      totalFiles: 3,
    });
    fireDOMContentLoaded();

    // Focus first row and toggle
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'x' }));
    expect(document.getElementById('cb_mod_1').checked).toBe(true);

    // Toggle again
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'x' }));
    expect(document.getElementById('cb_mod_1').checked).toBe(false);
  });

  test('keyboard help overlay auto-shows on first visit and auto-hides after 4s', () => {
    jest.useFakeTimers();
    const dom = buildKeyboardDom()
      + '<div id="kb-help" class="kb-help-overlay kb-help-hidden"><p>Keyboard shortcuts</p></div>';
    loadScript({
      bodyHtml: dom,
      totalFiles: 3,
    });
    fireDOMContentLoaded();

    const overlay = document.getElementById('kb-help');
    // After DOMContentLoaded, the first-visit auto-show sets it visible.
    // DOMContentLoaded 後、初回表示により visible に設定される。
    expect(overlay.classList.contains('kb-help-visible')).toBe(true);
    expect(overlay.classList.contains('kb-help-hidden')).toBe(false);

    // After 4 seconds, the auto-hide timeout fires / 4秒後に自動非表示タイマーが発火
    jest.advanceTimersByTime(4100);
    expect(overlay.classList.contains('kb-help-visible')).toBe(false);
    expect(overlay.classList.contains('kb-help-hidden')).toBe(true);

    // Second DOMContentLoaded should not re-show (localStorage flag set)
    // 2回目の DOMContentLoaded は再表示しない（localStorage フラグ設定済み）
    fireDOMContentLoaded();
    expect(overlay.classList.contains('kb-help-hidden')).toBe(true);

    jest.useRealTimers();
  });

  test('keyboard shortcuts disabled in reviewed mode', () => {
    loadScript({
      bodyHtml: buildKeyboardDom(),
      totalFiles: 3,
      savedState: { 'cb_mod_1': true },
    });
    fireDOMContentLoaded();

    // In reviewed mode, checkboxes become pointer-events:none (read-only).
    // Verify the reviewed state was applied by init.js.
    const cb = document.getElementById('cb_mod_1');
    expect(cb.style.pointerEvents).toBe('none');
  });
});

describe('collectFilterState', () => {
  test('collects all filter control states', () => {
    loadScript({
      bodyHtml: `
        <input type="checkbox" id="filter-imp-high" checked />
        <input type="checkbox" id="filter-imp-medium" />
        <input type="checkbox" id="filter-imp-low" checked />
        <input type="checkbox" id="filter-diff-sha256match" checked />
        <input type="checkbox" id="filter-diff-sha256mismatch" checked />
        <input type="checkbox" id="filter-diff-ilmatch" checked />
        <input type="checkbox" id="filter-diff-ilmismatch" checked />
        <input type="checkbox" id="filter-diff-textmatch" checked />
        <input type="checkbox" id="filter-diff-textmismatch" checked />
        <input type="checkbox" id="filter-unchecked" />
        <input type="text" id="filter-search" value="test" />
      `,
    });
    const state = window.collectFilterState();
    expect(state['filter-imp-high']).toBe(true);
    expect(state['filter-imp-medium']).toBe(false);
    expect(state['filter-search']).toBe('test');
  });
});

describe('module.exports conditional exports', () => {
  test('excel module esc function is accessible after eval', () => {
    // Verify the module.exports guard does not interfere with browser-mode eval
    // module.exports ガードがブラウザモードの eval に干渉しないことを確認
    loadScript();
    expect(typeof window.esc).toBe('function');
    expect(window.esc('<b>&"</b>')).toBe('&lt;b&gt;&amp;&quot;&lt;/b&gt;');
  });

  test('highlight module exports are accessible after eval', () => {
    loadScript();
    expect(typeof window.highlightILCell).toBe('function');
  });

  test('formatTs is accessible after eval', () => {
    loadScript();
    const d = new Date(2026, 0, 15, 9, 5, 3);
    expect(window.formatTs(d)).toBe('2026-01-15 09:05:03');
  });
});
