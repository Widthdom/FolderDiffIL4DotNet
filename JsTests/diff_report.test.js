/**
 * Unit tests for diff_report.js — the client-side JavaScript embedded in the HTML report.
 * diff_report.js のユニットテスト — HTML レポートに埋め込まれるクライアントサイド JavaScript。
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

const JS_SOURCE = fs.readFileSync(
  path.join(__dirname, '..', 'Services', 'HtmlReport', 'diff_report.js'),
  'utf-8'
);

/**
 * Load diff_report.js into the current jsdom window with given options.
 * Replaces template placeholders and evaluates the script.
 * diff_report.js を指定オプションで現在の jsdom window にロードする。
 * テンプレートプレースホルダーを置換し、スクリプトを評価する。
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
      `<tr data-section="${r.section}" ${r.importance ? 'data-importance="' + r.importance + '"' : ''} ${r.diff ? 'data-diff="' + r.diff + '"' : ''}>
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
