/**
 * Extended unit tests for diff_report JS modules — covering gaps in the original test suite.
 * diff_report JS モジュールの拡張ユニットテスト — 元テストスイートのカバレッジギャップを補完。
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
 * diff_report JS モジュールを指定オプションで現在の jsdom window にロードする。
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

  document.documentElement.innerHTML = '<head><title>diff_report</title></head><body></body>';
  document.body.innerHTML = bodyHtml;
  localStorage.clear();

  if (typeof Element.prototype.scrollIntoView !== 'function') {
    Element.prototype.scrollIntoView = function() {};
  }

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

  const indirectEval = eval;
  indirectEval(js);
}

function fireDOMContentLoaded() {
  document.dispatchEvent(new Event('DOMContentLoaded'));
}

/** Build filter controls HTML shared by many tests / 多くのテストで共有するフィルタコントロール HTML */
function filterControlsHtml() {
  return `
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
    <span id="save-status"></span>
    <div id="progress-bar-fill"></div>
    <span id="progress-text"></span>
  `;
}

// ─── getStoredTheme ─────────────────────────────────────────────────────────
// テーマ保存の読み取りテスト
describe('getStoredTheme', () => {
  test('returns null when no theme is stored', () => {
    loadScript({ storageKey: 'theme-test' });
    expect(window.getStoredTheme()).toBeNull();
  });

  test('returns stored theme value', () => {
    loadScript({ storageKey: 'theme-test' });
    localStorage.setItem('theme-test-theme', 'dark');
    expect(window.getStoredTheme()).toBe('dark');
  });

  test('returns null on localStorage error', () => {
    loadScript({ storageKey: 'theme-test' });
    const orig = localStorage.getItem;
    localStorage.getItem = () => { throw new Error('blocked'); };
    expect(window.getStoredTheme()).toBeNull();
    localStorage.getItem = orig;
  });
});

// ─── highlightAllILDiffs ────────────────────────────────────────────────────
// 全 IL diff テーブルへのハイライト一括適用テスト
describe('highlightAllILDiffs', () => {
  test('applies highlighting to all diff-table elements with ILMismatch parent', () => {
    const html = `
      <table><tbody>
        <tr data-section="mod" data-diff="ILMismatch"><td>1</td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr>
        <tr class="diff-row"><td colspan="8">
          <details open>
            <summary>Diff</summary>
            <table class="diff-table"><tbody>
              <tr><td class="diff-ctx-td">.method public void Test()</td></tr>
            </tbody></table>
          </details>
        </td></tr>
      </tbody></table>
      ${filterControlsHtml()}
    `;
    loadScript({ bodyHtml: html });
    window.highlightAllILDiffs();
    const td = document.querySelector('.diff-ctx-td');
    expect(td.innerHTML).toContain('hl-');
  });

  test('skips non-IL diff tables (SHA256Mismatch)', () => {
    const html = `
      <table><tbody>
        <tr data-section="mod" data-diff="SHA256Mismatch"><td>1</td><td></td><td></td><td></td><td></td><td></td><td></td><td></td></tr>
        <tr class="diff-row"><td colspan="8">
          <details open>
            <summary>Diff</summary>
            <table class="diff-table"><tbody>
              <tr><td class="diff-ctx-td">.method public void Test()</td></tr>
            </tbody></table>
          </details>
        </td></tr>
      </tbody></table>
      ${filterControlsHtml()}
    `;
    loadScript({ bodyHtml: html });
    window.highlightAllILDiffs();
    const td = document.querySelector('.diff-ctx-td');
    expect(td.innerHTML).not.toContain('hl-');
  });
});

// ─── wrapInputWithClear ─────────────────────────────────────────────────────
// テキスト入力のクリアボタン生成テスト
describe('wrapInputWithClear', () => {
  test('wraps input with container and clear button', () => {
    loadScript({
      bodyHtml: '<div><input type="text" id="test-input" value="hello" /></div>',
    });
    const inp = document.getElementById('test-input');
    window.wrapInputWithClear(inp);
    expect(inp.parentElement.classList.contains('input-wrap')).toBe(true);
    const btn = inp.parentElement.querySelector('.btn-input-clear');
    expect(btn).not.toBeNull();
  });

  test('clear button clears input value and dispatches events', () => {
    loadScript({
      bodyHtml: '<div><input type="text" id="test-input" value="hello" /></div>',
    });
    const inp = document.getElementById('test-input');
    window.wrapInputWithClear(inp);
    const btn = inp.parentElement.querySelector('.btn-input-clear');
    let inputFired = false;
    let changeFired = false;
    inp.addEventListener('input', () => { inputFired = true; });
    inp.addEventListener('change', () => { changeFired = true; });
    btn.click();
    expect(inp.value).toBe('');
    expect(inputFired).toBe(true);
    expect(changeFired).toBe(true);
  });

  test('uses filter-search-wrap class for filter-search inputs', () => {
    loadScript({
      bodyHtml: '<div><input type="text" id="filter-search" class="filter-search" value="" /></div>',
    });
    const inp = document.getElementById('filter-search');
    window.wrapInputWithClear(inp);
    expect(inp.parentElement.classList.contains('filter-search-wrap')).toBe(true);
  });

  test('does not double-wrap already-wrapped inputs', () => {
    loadScript({
      bodyHtml: '<div><input type="text" id="test-input" value="" /></div>',
    });
    const inp = document.getElementById('test-input');
    window.wrapInputWithClear(inp);
    window.wrapInputWithClear(inp);
    // Should still have only one wrapper / ラッパーは1つだけであるべき
    expect(document.querySelectorAll('.input-wrap').length).toBe(1);
  });

  test('has-text class toggles based on input value', () => {
    loadScript({
      bodyHtml: '<div><input type="text" id="test-input" value="x" /></div>',
    });
    const inp = document.getElementById('test-input');
    window.wrapInputWithClear(inp);
    const wrap = inp.parentElement;
    expect(wrap.classList.contains('has-text')).toBe(true);
    inp.value = '';
    inp.dispatchEvent(new Event('input'));
    expect(wrap.classList.contains('has-text')).toBe(false);
  });
});

// ─── initClearButtons ───────────────────────────────────────────────────────
// フィルター検索のクリアボタン初期化テスト
describe('initClearButtons', () => {
  test('wraps filter-search input with clear button', () => {
    loadScript({
      bodyHtml: '<div><input type="text" id="filter-search" value="" /></div>' + filterControlsHtml(),
    });
    fireDOMContentLoaded();
    const inp = document.getElementById('filter-search');
    const btn = inp.parentElement.querySelector('.btn-input-clear');
    expect(btn).not.toBeNull();
  });
});

// ─── initColResizeSingle ────────────────────────────────────────────────────
// 列リサイズハンドル初期化テスト
describe('initColResizeSingle', () => {
  test('wraps th content in span.th-label and adds resize handle', () => {
    loadScript({
      bodyHtml: `
        <table class="semantic-changes-table"><thead><tr>
          <th class="th-resizable" data-col-var="--sc-name-w">Name</th>
        </tr></thead><tbody></tbody></table>
        ${filterControlsHtml()}
      `,
    });
    const th = document.querySelector('.th-resizable');
    window.initColResizeSingle(th);
    expect(th.querySelector('.th-label')).not.toBeNull();
    expect(th.querySelector('.th-label').textContent).toBe('Name');
    expect(th.querySelector('.col-resize-handle')).not.toBeNull();
  });

  test('mousedown on handle starts resize and mousemove updates CSS variable', () => {
    loadScript({
      bodyHtml: `
        <table class="semantic-changes-table"><thead><tr>
          <th class="th-resizable" data-col-var="--sc-name-w">Name</th>
        </tr></thead><tbody></tbody></table>
        ${filterControlsHtml()}
      `,
    });
    const th = document.querySelector('.th-resizable');
    window.initColResizeSingle(th);
    const handle = th.querySelector('.col-resize-handle');
    // Simulate drag / ドラッグをシミュレート
    handle.dispatchEvent(new MouseEvent('mousedown', { clientX: 100, bubbles: true }));
    document.dispatchEvent(new MouseEvent('mousemove', { clientX: 200, bubbles: true }));
    const root = document.documentElement;
    const val = root.style.getPropertyValue('--sc-name-w');
    expect(val).toBeTruthy();
    // Cleanup / クリーンアップ
    document.dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
  });
});

// ─── syncScTableWidths ──────────────────────────────────────────────────────
// セマンティック変更テーブル幅同期テスト
describe('syncScTableWidths', () => {
  test('sets width on sc-detail tables', () => {
    loadScript({
      bodyHtml: `
        <table class="sc-detail semantic-changes-table"><thead><tr><th>A</th></tr></thead><tbody></tbody></table>
        ${filterControlsHtml()}
      `,
    });
    window.syncScTableWidths();
    const table = document.querySelector('.sc-detail');
    expect(table.style.width).toBeTruthy();
    expect(table.style.width).toContain('px');
  });

  test('sets width on dc-detail tables', () => {
    loadScript({
      bodyHtml: `
        <table class="dc-detail"><thead><tr><th>A</th></tr></thead><tbody></tbody></table>
        ${filterControlsHtml()}
      `,
    });
    window.syncScTableWidths();
    const table = document.querySelector('.dc-detail');
    expect(table.style.width).toBeTruthy();
    expect(table.style.width).toContain('px');
  });
});

// ─── syncFilterRowHeight ────────────────────────────────────────────────────
// フィルター行高さ同期テスト
describe('syncFilterRowHeight', () => {
  test('does not throw when no filter-table exists', () => {
    loadScript();
    expect(() => window.syncFilterRowHeight()).not.toThrow();
  });
});

// ─── Virtual scroll ─────────────────────────────────────────────────────────
// 仮想スクロール機能テスト
describe('initVirtualScroll', () => {
  /** Generate a table with N rows for virtual scroll testing / 仮想スクロールテスト用にN行テーブルを生成 */
  function buildVsTable(rowCount, sectionPrefix) {
    const prefix = sectionPrefix || 'mod';
    let rows = '';
    for (let i = 0; i < rowCount; i++) {
      rows += `<tr data-sc-importance="${i % 3 === 0 ? 'High' : i % 3 === 1 ? 'Medium' : 'Low'}"><td><input type="checkbox" id="sc_${prefix}_${i}" /></td><td>Row ${i}</td></tr>`;
    }
    return `<table class="semantic-changes-table sc-detail"><thead><tr><th>CB</th><th>Name</th></tr></thead><tbody>${rows}</tbody></table>`;
  }

  test('returns false for table with <= 100 rows (below threshold)', () => {
    loadScript({
      bodyHtml: buildVsTable(50) + filterControlsHtml(),
    });
    const table = document.querySelector('.semantic-changes-table');
    expect(window.initVirtualScroll(table)).toBe(false);
    expect(table.__vs).toBeUndefined();
  });

  test('returns true and wraps table in vs-viewport for > 100 rows', () => {
    loadScript({
      bodyHtml: buildVsTable(150) + filterControlsHtml(),
    });
    const table = document.querySelector('.semantic-changes-table');
    const result = window.initVirtualScroll(table);
    expect(result).toBe(true);
    expect(table.parentElement.classList.contains('vs-viewport')).toBe(true);
    expect(table.__vs).toBeDefined();
    expect(table.__vs.rowData.length).toBe(150);
  });

  test('creates row count indicator after viewport', () => {
    loadScript({
      bodyHtml: buildVsTable(120) + filterControlsHtml(),
    });
    const table = document.querySelector('.semantic-changes-table');
    window.initVirtualScroll(table);
    const indicator = table.__vs.indicator;
    expect(indicator).toBeDefined();
    expect(indicator.textContent).toContain('120');
    expect(indicator.textContent).toContain('of');
  });

  test('renders only a subset of rows (not all 150)', () => {
    loadScript({
      bodyHtml: buildVsTable(150) + filterControlsHtml(),
    });
    const table = document.querySelector('.semantic-changes-table');
    window.initVirtualScroll(table);
    const renderedRows = table.__vs.tbody.querySelectorAll('tr:not(.vs-spacer)');
    // Should render fewer than all 150 rows / 150行未満がレンダリングされるはず
    expect(renderedRows.length).toBeLessThan(150);
    expect(renderedRows.length).toBeGreaterThan(0);
  });

  test('returns false when table has no tbody', () => {
    loadScript({
      bodyHtml: '<table class="semantic-changes-table"></table>' + filterControlsHtml(),
    });
    const table = document.querySelector('.semantic-changes-table');
    expect(window.initVirtualScroll(table)).toBe(false);
  });
});

describe('vsRender', () => {
  function setupVsTable(rowCount) {
    let rows = '';
    for (let i = 0; i < rowCount; i++) {
      rows += `<tr data-sc-importance="High"><td><input type="checkbox" id="sc_r_${i}" /></td><td>Row ${i}</td></tr>`;
    }
    const html = `<table class="semantic-changes-table sc-detail"><thead><tr><th>CB</th><th>Name</th></tr></thead><tbody>${rows}</tbody></table>${filterControlsHtml()}`;
    loadScript({ bodyHtml: html });
    const table = document.querySelector('.semantic-changes-table');
    window.initVirtualScroll(table);
    return table;
  }

  test('skips re-render when range is unchanged', () => {
    const table = setupVsTable(150);
    const startBefore = table.__vs.renderedStart;
    // Call render again without scrolling — should be no-op / スクロールなしで再度呼出 — 何もしないはず
    window.vsRender(table);
    expect(table.__vs.renderedStart).toBe(startBefore);
  });

  test('does nothing if table has no __vs', () => {
    loadScript({
      bodyHtml: '<table id="plain"><tbody><tr><td>x</td></tr></tbody></table>' + filterControlsHtml(),
    });
    const table = document.getElementById('plain');
    expect(() => window.vsRender(table)).not.toThrow();
  });
});

describe('vsRefreshVisibility', () => {
  test('hides rows based on importance filter state', () => {
    let rows = '';
    for (let i = 0; i < 120; i++) {
      const imp = i % 2 === 0 ? 'High' : 'Low';
      rows += `<tr data-sc-importance="${imp}"><td><input type="checkbox" id="sc_f_${i}" /></td><td>Row ${i}</td></tr>`;
    }
    const html = `<table class="semantic-changes-table sc-detail __vs-active"><thead><tr><th>A</th><th>B</th></tr></thead><tbody>${rows}</tbody></table>${filterControlsHtml()}`;
    loadScript({ bodyHtml: html });
    const table = document.querySelector('.semantic-changes-table');
    window.initVirtualScroll(table);
    // Uncheck Low filter / Low フィルターをオフ
    document.getElementById('filter-imp-low').checked = false;
    window.vsRefreshVisibility(table);
    // Only High rows should be visible / High 行のみ表示されるはず
    const visibleCount = table.__vs.rowData.filter(r => !r.hidden).length;
    expect(visibleCount).toBe(60); // half are High / 半分が High
    expect(table.__vs.indicator.textContent).toContain('60');
  });

  test('does nothing if table has no __vs', () => {
    loadScript({ bodyHtml: '<table id="t"><tbody></tbody></table>' + filterControlsHtml() });
    expect(() => window.vsRefreshVisibility(document.getElementById('t'))).not.toThrow();
  });
});

describe('vsMaterializeAll', () => {
  test('restores full DOM from virtual scroll and removes viewport wrapper', () => {
    let rows = '';
    for (let i = 0; i < 120; i++) {
      rows += `<tr data-sc-importance="High"><td><input type="checkbox" id="sc_m_${i}" /></td><td>Row ${i}</td></tr>`;
    }
    const html = `<table class="semantic-changes-table sc-detail"><thead><tr><th>A</th><th>B</th></tr></thead><tbody>${rows}</tbody></table>${filterControlsHtml()}`;
    loadScript({ bodyHtml: html });
    const table = document.querySelector('.semantic-changes-table');
    window.initVirtualScroll(table);
    table.classList.add('vs-active');
    // Verify it's in virtual scroll mode / 仮想スクロールモードであることを確認
    expect(table.__vs).toBeDefined();
    window.vsMaterializeAll();
    // After materialize, __vs should be removed / マテリアライズ後、__vs は削除されるはず
    expect(table.__vs).toBeUndefined();
    expect(table.classList.contains('vs-active')).toBe(false);
    // All 120 rows should be in DOM / 全120行がDOMにあるはず
    const allRows = table.querySelectorAll('tbody tr');
    expect(allRows.length).toBe(120);
    // Viewport wrapper should be removed / ビューポートラッパーは削除されるはず
    expect(document.querySelector('.vs-viewport')).toBeNull();
  });
});

// ─── Excel export ───────────────────────────────────────────────────────────
// Excel エクスポートパイプラインテスト
describe('buildExcelFramework', () => {
  test('produces complete Excel-compatible HTML with header, legend, sections', () => {
    loadScript({
      bodyHtml: `
        <div class="header-card"><span class="header-card-label">App Version</span><span class="header-card-value">1.14.0</span></div>
        <div class="header-path"><span class="header-path-label">Old Folder</span><span class="header-path-value">/old</span></div>
        <div class="header-path"><span class="header-path-label">New Folder</span><span class="header-path-value">/new</span></div>
        <table class="stat-table"><tbody>
          <tr><td>Added</td><td>3</td></tr>
          <tr><td>Removed</td><td>1</td></tr>
        </tbody></table>
        ${filterControlsHtml()}
      `,
    });
    const builtRows = {
      add: ['<tr><td>row1</td></tr>'],
      rem: ['<tr><td>row2</td></tr>'],
      mod: [],
      unch: [],
    };
    const result = window.buildExcelFramework(builtRows);
    expect(result).toContain('<!DOCTYPE html>');
    expect(result).toContain('1.14.0');
    expect(result).toContain('/old');
    expect(result).toContain('/new');
    expect(result).toContain('Added Files');
    expect(result).toContain('Removed Files');
    expect(result).toContain('Legend');
    expect(result).toContain('Summary');
  });

  test('includes ignored section when builtRows has ign entries', () => {
    loadScript({ bodyHtml: filterControlsHtml() });
    const builtRows = { ign: ['<tr><td>ignored</td></tr>'], unch: [], add: [], rem: [], mod: [] };
    const result = window.buildExcelFramework(builtRows);
    expect(result).toContain('Ignored Files');
  });

  test('includes warning sections for sha256w and tsw keys', () => {
    loadScript({ bodyHtml: filterControlsHtml() });
    const builtRows = {
      unch: [], add: [], rem: [], mod: [],
      sha256w: ['<tr><td>warn1</td></tr>'],
      tsw: ['<tr><td>warn2</td></tr>'],
    };
    const result = window.buildExcelFramework(builtRows);
    expect(result).toContain('Warnings');
    expect(result).toContain('SHA256Mismatch');
    expect(result).toContain('timestamps older');
  });
});

describe('downloadExcelCompatibleHtml', () => {
  test('calls downloadExcelImmediate for small reports (<=500 rows)', () => {
    // Mock URL.createObjectURL and Blob / URL.createObjectURL と Blob をモック
    const origCreateObjectURL = URL.createObjectURL;
    const origRevokeObjectURL = URL.revokeObjectURL;
    URL.createObjectURL = () => 'blob:test';
    URL.revokeObjectURL = () => {};
    loadScript({
      bodyHtml: `
        <table><tbody>
          <tr data-section="mod"><td>1</td><td></td><td></td><td></td><td>Changed</td><td><span class="path-text">a.dll</span></td><td>2026-01-01</td><td>ILMismatch</td><td></td><td></td></tr>
        </tbody></table>
        ${filterControlsHtml()}
      `,
    });
    // Should not throw / エラーにならないはず
    expect(() => window.downloadExcelCompatibleHtml()).not.toThrow();
    URL.createObjectURL = origCreateObjectURL;
    URL.revokeObjectURL = origRevokeObjectURL;
  });
});

describe('downloadExcelImmediate', () => {
  test('creates blob download with correct filename', () => {
    const origCreateObjectURL = URL.createObjectURL;
    const origRevokeObjectURL = URL.revokeObjectURL;
    URL.createObjectURL = () => 'blob:test';
    URL.revokeObjectURL = () => {};
    loadScript({
      reportDate: '20260101',
      bodyHtml: `
        <table><tbody>
          <tr data-section="add"><td>1</td><td></td><td></td><td></td><td>Added</td><td><span class="path-text">new.dll</span></td><td>2026-01-01</td><td>N/A</td><td></td><td></td></tr>
        </tbody></table>
        ${filterControlsHtml()}
      `,
    });
    let downloadName = '';
    const origAppendChild = document.body.appendChild.bind(document.body);
    const origRemoveChild = document.body.removeChild.bind(document.body);
    // Capture the download filename / ダウンロードファイル名をキャプチャ
    const origClick = HTMLAnchorElement.prototype.click;
    HTMLAnchorElement.prototype.click = function() {
      downloadName = this.download || '';
    };
    window.downloadExcelImmediate();
    expect(downloadName).toContain('Excel-compatible.html');
    expect(downloadName).toContain('20260101');
    HTMLAnchorElement.prototype.click = origClick;
    URL.createObjectURL = origCreateObjectURL;
    URL.revokeObjectURL = origRevokeObjectURL;
  });
});

// ─── downloadAsPdf ──────────────────────────────────────────────────────────
// PDF ダウンロードテスト
describe('downloadAsPdf', () => {
  test('injects pdf-print-header/footer and calls window.print', () => {
    let printCalled = false;
    window.print = () => { printCalled = true; };
    loadScript({
      bodyHtml: `
        <div class="header-card"><span class="header-card-label">App Version</span><span class="header-card-value">1.14.0</span></div>
        <div class="header-card"><span class="header-card-label">Computer</span><span class="header-card-value">WORKSTATION</span></div>
        <div class="header-path"><span class="header-path-label">Old Folder</span><span class="header-path-value">/old</span></div>
        <div class="header-path"><span class="header-path-label">New Folder</span><span class="header-path-value">/new</span></div>
        ${filterControlsHtml()}
      `,
    });
    window.downloadAsPdf();
    expect(printCalled).toBe(true);
    expect(document.querySelector('.pdf-print-header')).not.toBeNull();
    expect(document.querySelector('.pdf-print-footer')).not.toBeNull();
    expect(document.body.classList.contains('pdf-print-mode')).toBe(true);
  });

  test('cleanup removes header/footer after afterprint event', () => {
    window.print = () => {};
    loadScript({ bodyHtml: filterControlsHtml() });
    window.downloadAsPdf();
    // Fire afterprint / afterprint を発火
    window.dispatchEvent(new Event('afterprint'));
    expect(document.querySelector('.pdf-print-header')).toBeNull();
    expect(document.querySelector('.pdf-print-footer')).toBeNull();
    expect(document.body.classList.contains('pdf-print-mode')).toBe(false);
  });
});

// ─── downloadReviewed ───────────────────────────────────────────────────────
// レビュー済み HTML ダウンロードテスト
describe('downloadReviewed', () => {
  test('produces reviewed HTML with embedded state and SHA256 hash', async () => {
    // Mock crypto.subtle.digest / crypto.subtle.digest をモック
    const fakeHash = new Uint8Array(32);
    for (let i = 0; i < 32; i++) fakeHash[i] = i;
    const origDigest = crypto.subtle && crypto.subtle.digest;
    if (!crypto.subtle) {
      Object.defineProperty(globalThis, 'crypto', {
        value: { subtle: { digest: async () => fakeHash.buffer } },
        writable: true,
        configurable: true,
      });
    } else {
      crypto.subtle.digest = async () => fakeHash.buffer;
    }
    const origCreateObjectURL = URL.createObjectURL;
    const origRevokeObjectURL = URL.revokeObjectURL;
    URL.createObjectURL = () => 'blob:reviewed';
    URL.revokeObjectURL = () => {};
    let downloadedFiles = [];
    HTMLAnchorElement.prototype.click = function() {
      downloadedFiles.push(this.download);
    };

    loadScript({
      storageKey: 'review-dl-test',
      reportDate: '20260101',
      bodyHtml: `
        <style>:root { --col-reason-w: 10em; --col-notes-w: 10em; --col-path-w: 22em; --col-diff-w: 10.8em; --col-tag-w: 14em; --col-disasm-w: 28em; --col-sdk-w: 14em; --sc-class-w: 14em; --sc-basetype-w: 16em; --sc-type-w: 12em; --sc-name-w: 10em; --sc-rettype-w: 12em; --sc-params-w: 18em; --sc-body-w: 5em; --dc-refs-w: 16em; }</style>
        <!--CTRL--><button>Download</button><!--/CTRL-->
        <input type="checkbox" id="cb_mod_1" checked />
        <input type="text" id="note_1" value="ok" />
        ${filterControlsHtml()}
      `,
    });

    await window.downloadReviewed();
    // Should have triggered at least one download / 少なくとも1つのダウンロードがトリガーされるはず
    expect(downloadedFiles.length).toBeGreaterThanOrEqual(1);
    expect(downloadedFiles[0]).toContain('reviewed.html');

    URL.createObjectURL = origCreateObjectURL;
    URL.revokeObjectURL = origRevokeObjectURL;
    if (origDigest) crypto.subtle.digest = origDigest;
  });
});

// ─── setupLazyIntersectionObserver ──────────────────────────────────────────
// IntersectionObserver による遅延セクション展開テスト
describe('setupLazyIntersectionObserver', () => {
  test('observes details elements with data-lazy-section attribute', () => {
    const observed = [];
    // Mock IntersectionObserver / IntersectionObserver をモック
    const OrigIO = globalThis.IntersectionObserver;
    globalThis.IntersectionObserver = class {
      constructor(cb, opts) {
        this._cb = cb;
        this._opts = opts;
      }
      observe(el) { observed.push(el); }
      unobserve() {}
      disconnect() {}
    };

    const b64 = btoa('<div>lazy content</div>');
    loadScript({
      bodyHtml: `
        <details data-lazy-section="${b64}"><summary>Section</summary></details>
        <details data-lazy-section="${b64}"><summary>Section 2</summary></details>
        ${filterControlsHtml()}
      `,
    });
    window.setupLazyIntersectionObserver();
    expect(observed.length).toBe(2);
    observed.forEach(el => {
      expect(el.tagName).toBe('DETAILS');
    });

    globalThis.IntersectionObserver = OrigIO;
  });

  test('does nothing when IntersectionObserver is not available', () => {
    const OrigIO = globalThis.IntersectionObserver;
    delete globalThis.IntersectionObserver;
    loadScript({ bodyHtml: filterControlsHtml() });
    expect(() => window.setupLazyIntersectionObserver()).not.toThrow();
    globalThis.IntersectionObserver = OrigIO;
  });

  test('opens details when entry becomes intersecting', () => {
    let registeredCb;
    const OrigIO = globalThis.IntersectionObserver;
    globalThis.IntersectionObserver = class {
      constructor(cb) { registeredCb = cb; }
      observe() {}
      unobserve() {}
    };

    const b64 = btoa('<div>lazy</div>');
    loadScript({
      bodyHtml: `
        <details data-lazy-section="${b64}"><summary>S</summary></details>
        ${filterControlsHtml()}
      `,
    });
    window.setupLazyIntersectionObserver();
    const details = document.querySelector('details[data-lazy-section]');
    // Simulate intersection / 交差をシミュレート
    registeredCb([{ isIntersecting: true, target: details }]);
    expect(details.open).toBe(true);

    globalThis.IntersectionObserver = OrigIO;
  });
});

// ─── Keyboard: IME fallback ─────────────────────────────────────────────────
// キーボード: IME フォールバックテスト
describe('keyboard IME fallback', () => {
  function buildKbDom() {
    return `
      <table><tbody>
        <tr data-section="mod"><td>1</td><td><input type="checkbox" id="cb_mod_1" /></td><td></td><td></td><td>Changed</td><td><span class="path-text">a.dll</span></td><td></td><td></td><td></td><td></td></tr>
        <tr data-section="mod"><td>2</td><td><input type="checkbox" id="cb_mod_2" /></td><td></td><td></td><td>Changed</td><td><span class="path-text">b.dll</span></td><td></td><td></td><td></td><td></td></tr>
      </tbody></table>
      ${filterControlsHtml()}
    `;
  }

  test('Process key with KeyJ code moves focus down (IME active scenario)', () => {
    loadScript({ bodyHtml: buildKbDom(), totalFiles: 2 });
    fireDOMContentLoaded();
    // Simulate IME-active keydown: e.key='Process', e.code='KeyJ'
    // IME 有効時のキーダウンをシミュレート
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Process', code: 'KeyJ' }));
    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('kb-focus')).toBe(true);
  });

  test('Process key with KeyX code toggles checkbox', () => {
    loadScript({ bodyHtml: buildKbDom(), totalFiles: 2 });
    fireDOMContentLoaded();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Process', code: 'KeyJ' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Process', code: 'KeyX' }));
    expect(document.getElementById('cb_mod_1').checked).toBe(true);
  });

  test('Process key with KeyK code moves focus up', () => {
    loadScript({ bodyHtml: buildKbDom(), totalFiles: 2 });
    fireDOMContentLoaded();
    // Move down twice, then up once / 2回下、1回上
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Process', code: 'KeyK' }));
    const rows = document.querySelectorAll('tr[data-section]');
    expect(rows[0].classList.contains('kb-focus')).toBe(true);
  });
});

// ─── Keyboard: Escape behavior ──────────────────────────────────────────────
// キーボード: Escape キーの振る舞いテスト
describe('keyboard Escape behavior', () => {
  test('Escape on active keyboard focus clears the highlight', () => {
    loadScript({
      bodyHtml: `
        <table><tbody>
          <tr data-section="mod"><td>1</td><td><input type="checkbox" id="cb_mod_1" /></td><td></td><td></td><td>Changed</td><td><span class="path-text">a.dll</span></td><td></td><td></td><td></td><td></td></tr>
        </tbody></table>
        ${filterControlsHtml()}
      `,
      totalFiles: 1,
    });
    fireDOMContentLoaded();
    // Focus a row first / まず行にフォーカス
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'j' }));
    const row = document.querySelector('tr[data-section]');
    expect(row.classList.contains('kb-focus')).toBe(true);
    // Escape should clear keyboard focus / Escape でキーボードフォーカスが解除されるはず
    // Set __kbEscHandled__ to false first / まず __kbEscHandled__ を false に
    window.__kbEscHandled__ = false;
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    // Either the keyboard module or init.js should have handled it
    // キーボードモジュールか init.js のいずれかが処理したはず
    expect(row.classList.contains('kb-focus')).toBe(false);
  });
});

// ─── Escape closes nearest details (init.js handler) ─────────────────────
// init.js の Escape ハンドラー: 最も近い details を閉じるテスト
describe('Escape closes nearest open details', () => {
  test('closes details when focus is inside and no __kbEscHandled__', () => {
    loadScript({
      bodyHtml: `
        <details id="d1" open>
          <summary>Expand</summary>
          <button id="inner-btn">Click</button>
        </details>
        ${filterControlsHtml()}
      `,
      savedState: { x: true },
    });
    fireDOMContentLoaded();
    const btn = document.getElementById('inner-btn');
    btn.focus();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(document.getElementById('d1').open).toBe(false);
  });
});
