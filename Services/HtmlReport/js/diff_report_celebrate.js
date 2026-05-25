  /** @type {boolean} Whether the celebration animation has already been triggered */
  var __celebrationFired__ = false;

  /**
   * Fire a subtle particle animation when all reviewable files are checked.
   * Only triggers once per session; skipped in reviewed (read-only) mode.
   */
  function celebrateCompletion() {
    if (__celebrationFired__) return;
    if (__savedState__ !== null) return; // Do not celebrate in reviewed (read-only) copies / レビュー済み（読み取り専用）コピーでは祝わない
    __celebrationFired__ = true;

    var bar = document.getElementById('progress-bar-fill');
    if (!bar) return;
    var wrap = bar.closest('.progress-bar');
    if (!wrap) return;

    // Add glow effect to the progress bar / プログレスバーにグロー効果を追加
    wrap.classList.add('celebrate-glow');

    // Create particle container relative to the progress bar / プログレスバーを基準にパーティクルコンテナを作成
    var container = document.createElement('div');
    container.className = 'celebrate-container';
    container.setAttribute('aria-hidden', 'true');
    wrap.style.position = 'relative';
    wrap.style.overflow = 'visible';
    wrap.appendChild(container);

    // Spawn particles along the bar width / バーの幅に沿ってパーティクルを生成
    var barWidth = wrap.offsetWidth || 160;
    var count = 14;
    var accentColor = getComputedStyle(document.documentElement).getPropertyValue('--color-progress-fill').trim() || '#0051c3';

    for (var i = 0; i < count; i++) {
      var p = document.createElement('div');
      p.className = 'celebrate-particle';
      var size = 3 + Math.random() * 4;
      var left = (i / count) * barWidth + (Math.random() - 0.5) * (barWidth / count);
      var delay = i * 0.08 + Math.random() * 0.3;
      var hueShift = Math.round((Math.random() - 0.5) * 30);
      p.style.cssText =
        'width:' + size + 'px;height:' + size + 'px;' +
        'left:' + Math.max(0, left) + 'px;bottom:0;' +
        'background:' + accentColor + ';' +
        'filter:hue-rotate(' + hueShift + 'deg) brightness(1.3);' +
        'animation-delay:' + delay.toFixed(2) + 's;';
      container.appendChild(p);
    }

    // Show completion message above the bar / バーの上に完了メッセージを表示
    var msg = document.createElement('div');
    msg.className = 'celebrate-msg';
    msg.textContent = '\u2714 All files reviewed';
    container.appendChild(msg);

    // Clean up DOM after animation completes / アニメーション完了後にDOMをクリーンアップ
    setTimeout(function() {
      if (container.parentNode) container.parentNode.removeChild(container);
      wrap.classList.remove('celebrate-glow');
      wrap.style.position = '';
      wrap.style.overflow = '';
    }, 4000);
  }

  /* Export functions for Node.js/Jest testing (no-op in browser) */
  /* Node.js/Jest テスト用に関数をエクスポート（ブラウザでは無効） */
  if (typeof module !== 'undefined' && module.exports) { module.exports = { celebrateCompletion: celebrateCompletion }; }
