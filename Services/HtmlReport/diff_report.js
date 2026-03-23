  const __storageKey__  = '{{STORAGE_KEY}}';
  const __reportDate__  = '{{REPORT_DATE}}';
  // NOTE: 'const __savedState__ = null;' is replaced by downloadReviewed(). Do not change whitespace.
  const __savedState__  = null;
  // NOTE: replaced with SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __reviewedSha256__  = null;
  // NOTE: replaced with final SHA256 hash by downloadReviewed(). Do not change whitespace.
  const __finalSha256__     = null;

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
    // Pre-create hidden file input for Verify integrity so the accept
    // filter is ready before the first click (some browsers ignore accept
    // on dynamically created inputs that are clicked immediately)
    var vi = document.createElement('input');
    vi.type = 'file';
    vi.accept = '.sha256';
    vi.style.display = 'none';
    vi.id = '__verifyInput__';
    document.body.appendChild(vi);
  });

  var __filterIds__ = ['filter-diff-sha256','filter-diff-il','filter-diff-text','filter-imp-high','filter-imp-medium','filter-imp-low','filter-ft-dll','filter-ft-exe','filter-ft-config','filter-ft-resource','filter-ft-other','filter-unchecked','filter-search'];
  function collectState() {
    var s = {};
    document.querySelectorAll('input[id], textarea[id]').forEach(function(el) {
      if (__filterIds__.indexOf(el.id) >= 0) return;
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
    // 2. Clear all filter-hidden state so reviewed HTML shows all rows
    document.querySelectorAll('tr.filter-hidden').forEach(function(tr){ tr.classList.remove('filter-hidden'); });
    document.querySelectorAll('tr.filter-hidden-parent').forEach(function(tr){ tr.classList.remove('filter-hidden-parent'); });
    // 3. Capture current effective column widths to bake into reviewed HTML as defaults
    var colVarNames = ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w'];
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
      + '; --sc-body-w: '     + curWidths['--sc-body-w'] + '; }');
    // Remove inline col-var overrides from <html> element (now baked into :root)
    html = html.replace(/(<html\b[^>]*?) style="[^"]*"/, '$1');
    // Replace controls bar with reviewed banner (includes Verify integrity button)
    html = html.replace(/<!--CTRL-->[\s\S]*?<!--\/CTRL-->/g,
      '<div class="reviewed-banner">Reviewed: ' + formatTs(new Date()) + ' &#x2014; read-only'
      + ' <button class="btn" onclick="verifyIntegrity()" style="margin-left:1em;font-size:12px">&#x2713; Verify integrity</button>'
      + '</div>');
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

  function collapseAll() {
    document.querySelectorAll('details[open]').forEach(function(d){ d.removeAttribute('open'); });
    autoSave();
  }

  function clearAll() {
    if (!confirm('Clear all checkboxes and text inputs?')) return;
    document.querySelectorAll('input[type="checkbox"]').forEach(function(cb){ cb.checked=false; });
    document.querySelectorAll('input[type="text"], textarea').forEach(function(inp){ inp.value=''; });
    // Reset column widths to defaults
    var root = document.documentElement;
    ['--col-reason-w','--col-notes-w','--col-path-w','--col-diff-w','--col-disasm-w','--sc-class-w','--sc-basetype-w','--sc-type-w','--sc-name-w','--sc-rettype-w','--sc-params-w','--sc-body-w'].forEach(function(v){ root.style.removeProperty(v); });
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
    var colW = {
      'col-no-g': 3.2 * emPx,
      'col-cb-g': 2.2 * emPx,
      'col-reason-g': px('--col-reason-w', 10),
      'col-notes-g': px('--col-notes-w', 10),
      'col-path-g': px('--col-path-w', 22),
      'col-ts-g': 16 * emPx,
      'col-diff-g': px('--col-diff-w', 9),
      'col-disasm-g': px('--col-disasm-w', 28)
    };
    document.querySelectorAll('table:not(.stat-table):not(.diff-table):not(.semantic-changes-table):not(.legend-table):not(.il-ignore-table)').forEach(function(t) {
      var cg = t.querySelector('colgroup');
      if (!cg) return;
      var hideDisasm = t.classList.contains('hide-disasm');
      var hideCol6 = t.classList.contains('hide-col6');
      var w = 0;
      cg.querySelectorAll('col').forEach(function(col) {
        if (hideDisasm && col.classList.contains('col-disasm-g')) return;
        if (hideCol6 && col.classList.contains('col-diff-g')) return;
        if (colW[col.className] !== undefined) w += colW[col.className];
      });
      if (w > 0) t.style.width = w + 'px';
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
            + 7 * scEmPx + 7 * scEmPx + 10 * scEmPx + 8 * scEmPx + 11 * scEmPx
            + px('--sc-type-w', 12) + px('--sc-name-w', 10)
            + px('--sc-rettype-w', 12) + px('--sc-params-w', 18)
            + px('--sc-body-w', 5);
    document.querySelectorAll('table.sc-detail').forEach(function(t) { t.style.width = detW + 'px'; });
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

  // ── Filtering ──────────────────────────────────────────────────────────
  var __configExts__  = ['.json','.xml','.config','.yaml','.yml','.ini','.toml','.env','.props','.targets','.csproj','.vbproj','.fsproj','.sln'];
  var __resourceExts__ = ['.resx','.resources','.png','.jpg','.jpeg','.ico','.svg','.gif','.bmp','.wav','.mp3','.ttf','.woff','.woff2','.eot'];
  function getFileTypeCategory(ext) {
    if (!ext) return 'other';
    ext = ext.toLowerCase();
    if (ext === '.dll') return 'dll';
    if (ext === '.exe') return 'exe';
    if (__configExts__.indexOf(ext) >= 0) return 'config';
    if (__resourceExts__.indexOf(ext) >= 0) return 'resource';
    return 'other';
  }
  function applyFilters() {
    var diffSha256 = document.getElementById('filter-diff-sha256');
    var diffIl     = document.getElementById('filter-diff-il');
    var diffText   = document.getElementById('filter-diff-text');
    var impHigh    = document.getElementById('filter-imp-high');
    var impMedium  = document.getElementById('filter-imp-medium');
    var impLow     = document.getElementById('filter-imp-low');
    var ftDll      = document.getElementById('filter-ft-dll');
    var ftExe      = document.getElementById('filter-ft-exe');
    var ftConfig   = document.getElementById('filter-ft-config');
    var ftResource = document.getElementById('filter-ft-resource');
    var ftOther    = document.getElementById('filter-ft-other');
    var unchecked  = document.getElementById('filter-unchecked');
    var searchEl   = document.getElementById('filter-search');
    // If any element is missing (e.g. reviewed mode), skip
    if (!impHigh) return;
    var diffFilter = { sha256: diffSha256.checked, il: diffIl.checked, text: diffText.checked };
    var impFilter  = { High: impHigh.checked, Medium: impMedium.checked, Low: impLow.checked };
    var ftFilter   = { dll: ftDll.checked, exe: ftExe.checked, config: ftConfig.checked, resource: ftResource.checked, other: ftOther.checked };
    var onlyUnchecked = unchecked.checked;
    var searchText = (searchEl.value || '').toLowerCase().trim();
    // All checked = no filtering for that category
    var diffActive = !(diffFilter.sha256 && diffFilter.il && diffFilter.text);
    var impActive  = !(impFilter.High && impFilter.Medium && impFilter.Low);
    var ftActive   = !(ftFilter.dll && ftFilter.exe && ftFilter.config && ftFilter.resource && ftFilter.other);
    document.querySelectorAll('tbody > tr[data-section]').forEach(function(tr) {
      var show = true;
      // Diff detail filter
      if (diffActive) {
        var diff = tr.getAttribute('data-diff');
        if (diff) {
          if (!diffFilter[diff]) show = false;
        }
      }
      // File type filter
      if (show && ftActive) {
        var ext = tr.getAttribute('data-ext') || '';
        var cat = getFileTypeCategory(ext);
        if (!ftFilter[cat]) show = false;
      }
      // Importance filter (only for rows with importance)
      if (show && impActive) {
        var imp = tr.getAttribute('data-importance');
        if (imp) {
          if (!impFilter[imp]) show = false;
        }
      }
      // Unchecked only filter
      if (show && onlyUnchecked) {
        var cb = tr.querySelector('input[type="checkbox"]');
        if (cb && cb.checked) show = false;
      }
      // Search filter
      if (show && searchText) {
        var pathEl = tr.querySelector('.path-text');
        var pathText = pathEl ? pathEl.textContent.toLowerCase() : '';
        if (pathText.indexOf(searchText) < 0) show = false;
      }
      if (show) {
        tr.classList.remove('filter-hidden');
      } else {
        tr.classList.add('filter-hidden');
      }
      // Hide associated diff-row/semantic-changes rows
      var next = tr.nextElementSibling;
      while (next && !next.hasAttribute('data-section')) {
        if (show) {
          next.classList.remove('filter-hidden-parent');
        } else {
          next.classList.add('filter-hidden-parent');
        }
        next = next.nextElementSibling;
      }
    });
    autoSave();
  }
  function resetFilters() {
    ['filter-diff-sha256','filter-diff-il','filter-diff-text','filter-imp-high','filter-imp-medium','filter-imp-low','filter-ft-dll','filter-ft-exe','filter-ft-config','filter-ft-resource','filter-ft-other'].forEach(function(id) {
      var el = document.getElementById(id);
      if (el) el.checked = true;
    });
    var uc = document.getElementById('filter-unchecked');
    if (uc) uc.checked = false;
    var se = document.getElementById('filter-search');
    if (se) se.value = '';
    applyFilters();
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
