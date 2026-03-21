  const __storageKey__  = '{{STORAGE_KEY}}';
  const __reportDate__  = '{{REPORT_DATE}}';
  // NOTE: 'const __savedState__ = null;' is replaced by downloadReviewed(). Do not change whitespace.
  const __savedState__  = null;

  function formatTs(d) {
    return d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0')
      +' '+String(d.getHours()).padStart(2,'0')+':'+String(d.getMinutes()).padStart(2,'0')+':'+String(d.getSeconds()).padStart(2,'0');
  }

  document.addEventListener('DOMContentLoaded', function() {
    var toRestore = __savedState__ || JSON.parse(localStorage.getItem(__storageKey__) || 'null');
    if (toRestore) {
      Object.entries(toRestore).forEach(function(entry) {
        var el = document.getElementById(entry[0]);
        if (!el) return;
        if (el.type === 'checkbox') el.checked = Boolean(entry[1]);
        else el.value = String(entry[1] || '');
      });
    }
    if (__savedState__ !== null) {
      document.querySelectorAll('input[type="checkbox"]').forEach(function(cb){ cb.style.pointerEvents='none'; cb.style.cursor='default'; });
      document.querySelectorAll('input[type="text"]').forEach(function(inp){
        inp.readOnly=true; inp.style.cursor='default'; inp.style.userSelect='text';
      });
    } else {
      document.querySelectorAll('input, textarea').forEach(function(el) {
        el.addEventListener('change', autoSave);
        el.addEventListener('input',  autoSave);
      });
    }
    initColResize();
    syncTableWidths();
    syncScTableWidths();
    setupLazyDiff();
  });

  function collectState() {
    var s = {};
    document.querySelectorAll('input[id], textarea[id]').forEach(function(el) {
      s[el.id] = (el.type === 'checkbox') ? el.checked : el.value;
    });
    return s;
  }

  function autoSave() {
    localStorage.setItem(__storageKey__, JSON.stringify(collectState()));
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'Auto-saved at ' + formatTs(new Date());
  }

  async function downloadReviewed() {
    var state   = collectState();
    var slug    = 'diff_report_' + __reportDate__;
    var root    = document.documentElement;
    // 1. Collapse all diff-detail elements so exported file starts with diffs closed
    var openDetails = Array.from(document.querySelectorAll('details[open]'));
    openDetails.forEach(function(d){ d.removeAttribute('open'); });
    // 2. Capture current effective column widths to bake into reviewed HTML as defaults
    var colVarNames = ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w','--sc-cnt-class-w'];
    var cs = getComputedStyle(root);
    var curWidths = {};
    colVarNames.forEach(function(v){ curWidths[v] = (root.style.getPropertyValue(v) || cs.getPropertyValue(v)).trim(); });
    var html    = document.documentElement.outerHTML;
    // Restore open details in the live page
    openDetails.forEach(function(d){ d.setAttribute('open', ''); });
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
      + '; --col-disasm-w: '  + curWidths['--col-disasm-w']
      + '; --sc-class-w: '    + curWidths['--sc-class-w']
      + '; --sc-basetype-w: ' + curWidths['--sc-basetype-w']
      + '; --sc-type-w: '     + curWidths['--sc-type-w']
      + '; --sc-name-w: '     + curWidths['--sc-name-w']
      + '; --sc-rettype-w: '  + curWidths['--sc-rettype-w']
      + '; --sc-params-w: '   + curWidths['--sc-params-w']
      + '; --sc-body-w: '     + curWidths['--sc-body-w']
      + '; --sc-cnt-class-w: ' + curWidths['--sc-cnt-class-w'] + '; }');
    // Remove inline col-var overrides from <html> element (now baked into :root)
    html = html.replace(/(<html\b[^>]*?) style="[^"]*"/, '$1');
    // Replace controls bar with reviewed banner
    html = html.replace(/<!--CTRL-->[\s\S]*?<!--\/CTRL-->/g,
      '<div class="reviewed-banner">&#x1F512; Reviewed: ' + formatTs(new Date()) + ' &#x2014; read-only</div>');
    // 3. Compute SHA256 integrity hash of the reviewed HTML for tamper detection
    var reviewedFileName = slug + '_reviewed.html';
    var encoded = new TextEncoder().encode(html);
    var hashBuffer = await crypto.subtle.digest('SHA-256', encoded);
    var hashArray = Array.from(new Uint8Array(hashBuffer));
    var hashHex = hashArray.map(function(b) { return b.toString(16).padStart(2, '0'); }).join('');
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
      var sha256Text = hashHex + '  ' + reviewedFileName + '\n';
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

  function collapseAll() {
    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });
    scheduleSave();
  }

  function clearAll() {
    if (!confirm('Clear all checkboxes and text inputs?')) return;
    document.querySelectorAll('input[type="checkbox"]').forEach(function(cb){ cb.checked=false; });
    document.querySelectorAll('input[type="text"], textarea').forEach(function(inp){ inp.value=''; });
    // Reset column widths to defaults
    var root = document.documentElement;
    ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w','--sc-cnt-class-w'].forEach(function(v){ root.style.removeProperty(v); });
    syncTableWidths();
    // Close all open diff/IL-diff details
    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });
    localStorage.removeItem(__storageKey__);
    var status = document.getElementById('save-status');
    if (status) status.textContent = 'Cleared.';
  }

  function decodeDiffHtml(b64) {
    var binary = atob(b64);
    var bytes = new Uint8Array(binary.length);
    for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return new TextDecoder('utf-8').decode(bytes);
  }

  // Lazy-render: diff table HTML is stored base64-encoded in data-diff-html.
  // On first open, decode and insert into DOM, then remove the attribute.
  function setupLazyDiff() {
    document.querySelectorAll('details[data-diff-html]').forEach(function(d) {
      d.addEventListener('toggle', function onToggle() {
        if (!d.open) return;
        var b64 = d.getAttribute('data-diff-html');
        if (!b64) return;
        d.removeAttribute('data-diff-html');
        d.removeEventListener('toggle', onToggle);
        try {
          d.insertAdjacentHTML('beforeend', decodeDiffHtml(b64));
          // Re-init column resize handles on newly rendered tables
          d.querySelectorAll('th.th-resizable').forEach(function(th) {
            if (th.querySelector('.col-resize-handle')) return;
            initColResizeSingle(th);
          });
          syncScTableWidths();
          // Wire up save events on new checkboxes
          if (__savedState__ === null) {
            d.querySelectorAll('input').forEach(function(el) {
              el.addEventListener('change', autoSave);
            });
          }
          // Restore state for new inputs
          var toRestore = __savedState__ || JSON.parse(localStorage.getItem(__storageKey__) || 'null');
          if (toRestore) {
            d.querySelectorAll('input[id]').forEach(function(el) {
              if (el.id in toRestore) {
                if (el.type === 'checkbox') el.checked = Boolean(toRestore[el.id]);
                else el.value = String(toRestore[el.id] || '');
              }
            });
          }
          if (__savedState__ !== null) {
            d.querySelectorAll('input[type="checkbox"]').forEach(function(cb){ cb.style.pointerEvents='none'; cb.style.cursor='default'; });
          }
        } catch(e) {}
      });
    });
  }

  function syncTableWidths() {
    var root = document.documentElement;
    var emPx = parseFloat(getComputedStyle(root).fontSize) || 16;
    var px = function(v, fb) {
      var s = root.style.getPropertyValue(v) || getComputedStyle(root).getPropertyValue(v);
      return (parseFloat(s) || fb) * emPx;
    };
    var w = (3.2 + 2.2 + 16) * emPx
          + px('--col-reason-w', 10) + px('--col-notes-w', 10)
          + px('--col-path-w', 22)   + px('--col-diff-w', 9)
          + px('--col-disasm-w', 28);
    document.querySelectorAll('table:not(.stat-table):not(.diff-table):not(.semantic-changes-table)').forEach(function(t) {
      t.style.width = w + 'px';
    });
  }

  function syncScTableWidths() {
    var scEmPx = 12;
    var root = document.documentElement;
    var px = function(v, fb) {
      var s = root.style.getPropertyValue(v) || getComputedStyle(root).getPropertyValue(v);
      return (parseFloat(s) || fb) * scEmPx;
    };
    var detW = 3.2 * scEmPx
            + px('--sc-class-w', 14) + px('--sc-basetype-w', 16)
            + 7 * scEmPx + 10 * scEmPx + 8 * scEmPx + 11 * scEmPx
            + px('--sc-type-w', 12) + px('--sc-name-w', 10)
            + px('--sc-rettype-w', 12) + px('--sc-params-w', 18)
            + px('--sc-body-w', 5);
    document.querySelectorAll('table.sc-detail').forEach(function(t) { t.style.width = detW + 'px'; });
    var cntW = px('--sc-cnt-class-w', 22) + 7 * scEmPx + 4 * scEmPx;
    document.querySelectorAll('table.sc-count').forEach(function(t) { t.style.width = cntW + 'px'; });
  }

  function initColResizeSingle(th) {
    var label = document.createElement('span');
    label.className = 'th-label';
    while (th.childNodes.length) label.appendChild(th.childNodes[0]);
    th.appendChild(label);
    var handle = document.createElement('div');
    handle.className = 'col-resize-handle';
    th.appendChild(handle);
    var varName = th.dataset.colVar;
    var isSc = !!th.closest('.semantic-changes-table');
    handle.addEventListener('mousedown', function(e) {
      e.preventDefault();
      var startX = e.clientX;
      var root   = document.documentElement;
      var emPx   = isSc ? 12 : (parseFloat(getComputedStyle(root).fontSize) || 16);
      var cur    = root.style.getPropertyValue(varName) || getComputedStyle(root).getPropertyValue(varName);
      var startPx = (parseFloat(cur) || 10) * emPx;
      function onMove(ev) {
        var newPx = Math.max(48, startPx + (ev.clientX - startX));
        root.style.setProperty(varName, (newPx / emPx).toFixed(2) + 'em');
        syncTableWidths();
        if (isSc) syncScTableWidths();
      }
      function onUp() {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup', onUp);
      }
      document.addEventListener('mousemove', onMove);
      document.addEventListener('mouseup', onUp);
    });
  }

  function initColResize() {
    document.querySelectorAll('th.th-resizable').forEach(function(th) {
      initColResizeSingle(th);
    });
  }

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
