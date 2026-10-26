// 卡片拖拽（交互与地图扩展 v0.3 · 01 案第 3 节）。
// 用 Pointer Events 自己实现，类似 Unity 的 IPointerDownHandler/IDragHandler/IDropHandler：
//   [data-drag="来源键"]  可拖起的卡（物品卡、锅内水子卡）
//   [data-drop="落点键"] + data-drop-level="hot|card|zone"  落点；命中优先级 热区 > 卡面 > 区域插入线
// 起拖时一次性把页面上所有落点键交给 .NET 解析（GameEngine.ResolveDrop），之后只在本地做命中与高亮；
// 松手时 .NET 重新校验并执行。规则全部在内核，这里只管手感。
window.survivalDrag = (function () {
  var net = null, threshold = 6, st = null, line = null, ghost = null, lastHover = null, flashTimer = 0;
  var NO_SELF = '选中物品后在卡片下方使用';

  function init(ref, px) {
    net = ref;
    threshold = px || 6;
    if (window.__survivalDragHooked) return;
    window.__survivalDragHooked = true;
    document.addEventListener('pointerdown', onDown);
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
    document.addEventListener('pointercancel', function () { cancel(); });
    document.addEventListener('keydown', onKey, true);
    window.addEventListener('blur', function () { cancel(); });
    document.documentElement.addEventListener('mouseleave', function () { if (st && st.started) cancel(); });
  }

  // 数量框、危险弹窗、菜单打开时背后的卡片不接收拖拽
  function blocked() {
    return document.body.classList.contains('modal-open') ||
      !!document.querySelector('.modal-back, .end-back, .overlay') ||
      !!(window.FeedbackWidget && FeedbackWidget.isOpen());
  }

  function onDown(ev) {
    if (ev.button !== 0 || st) return;
    var el = ev.target.closest && ev.target.closest('[data-drag]');
    if (!el || blocked()) return;
    // 卡片里的小按钮（三角箭头等）照常点击
    var btn = ev.target.closest('button, input, textarea, select');
    if (btn && btn !== el && el.contains(btn)) return;
    st = { src: el.getAttribute('data-drag'), el: el, x0: ev.clientX, y0: ev.clientY, started: false, map: null, hoverKey: null };
  }

  function onMove(ev) {
    if (!st) return;
    if (!st.started) {
      var dx = ev.clientX - st.x0, dy = ev.clientY - st.y0;
      if (dx * dx + dy * dy < threshold * threshold) return;
      if (!begin()) { st = null; return; }
    }
    ev.preventDefault();
    place(ev.clientX, ev.clientY);
    hover(ev.clientX, ev.clientY);
  }

  function begin() {
    var keys = [];
    document.querySelectorAll('[data-drop]').forEach(function (d) {
      var k = d.getAttribute('data-drop');
      if (keys.indexOf(k) < 0) keys.push(k);
    });
    var json;
    try { json = net.invokeMethod('DragBegin', st.src, keys); } catch (e) { return false; }
    st.map = JSON.parse(json || '{}');
    st.started = true;
    document.body.classList.add('dragging');
    st.el.classList.add('drag-source');
    ghost = document.createElement('div');
    ghost.className = 'drag-ghost';
    var title = st.el.getAttribute('data-drag-title') || (st.el.querySelector('.ctitle') || st.el).innerText;
    ghost.innerHTML = '<div class="dg-title"></div><div class="dg-from"></div><div class="dg-label"></div>';
    ghost.querySelector('.dg-title').textContent = title.replace(/\s+/g, ' ').trim();
    ghost.querySelector('.dg-from').textContent = st.el.getAttribute('data-drag-from') || '';
    document.body.appendChild(ghost);
    line = document.createElement('div');
    line.className = 'drop-line';
    document.body.appendChild(line);
    // 合法落点整体提示：绿色即时、琥珀预览
    document.querySelectorAll('[data-drop]').forEach(function (d) {
      var r = st.map[d.getAttribute('data-drop')];
      if (!r || !r.ok || d.getAttribute('data-drop-level') === 'zone' || r.kind === 'reorder') return;
      d.classList.add(r.kind === 'preview' ? 'drop-can-preview' : 'drop-can');
    });
    return true;
  }

  function place(x, y) {
    if (!ghost) return;
    ghost.style.left = (x + 14) + 'px';
    ghost.style.top = (y + 12) + 'px';
  }

  // 命中：热区 > 卡面（卡面无组合时落到所在区） > 区域插入线
  function hit(x, y) {
    ghost.style.display = 'none';
    var el = document.elementFromPoint(x, y);
    ghost.style.display = '';
    if (!el) return null;
    var hot = null, card = null, zone = null;
    for (var n = el; n && n !== document.body; n = n.parentElement) {
      if (!n.getAttribute) continue;
      var lv = n.getAttribute('data-drop-level');
      if (!lv) continue;
      if (lv === 'hot' && !hot) hot = n;
      else if (lv === 'card' && !card) card = n;
      else if (lv === 'zone' && !zone) zone = n;
    }
    var pick = function (n) { return n ? { el: n, key: n.getAttribute('data-drop'), r: st.map[n.getAttribute('data-drop')] } : null; };
    if (hot && st.map[hot.getAttribute('data-drop')]) return pick(hot);
    if (card) {
      var c = pick(card);
      if (c.r && !c.r.fall) return c;
    }
    if (zone) {
      var z = pick(zone);
      if (z.r) { z.index = indexIn(zone, x); return z; }
    }
    if (card) return pick(card);
    return hot ? pick(hot) : null;
  }

  // 区内插入位置：按指针 x 与其他卡片中线比较（不含被拖的卡自己）
  function indexIn(zone, x) {
    var cards = Array.prototype.filter.call(zone.querySelectorAll('[data-drag^="card:"]'), function (c) { return c !== st.el; });
    var i = 0;
    for (; i < cards.length; i++) {
      var r = cards[i].getBoundingClientRect();
      if (x < r.left + r.width / 2) break;
    }
    var ref = cards[i] || cards[cards.length - 1];
    if (ref) {
      var rr = ref.getBoundingClientRect();
      line.style.left = ((cards[i] ? rr.left - 6 : rr.right + 3)) + 'px';
      line.style.top = rr.top + 'px';
      line.style.height = rr.height + 'px';
    } else {
      var zr = zone.getBoundingClientRect();
      line.style.left = (zr.left + 4) + 'px';
      line.style.top = (zr.top + 4) + 'px';
      line.style.height = Math.max(40, zr.height - 8) + 'px';
    }
    return i;
  }

  // 落点变化时通知 .NET：打火器悬停在合法火堆落点（kind=start）右栏显示点火预测，离开清空（v0.7 第 4.1 节）
  function notifyHover(h) {
    var key = h && h.r && h.r.ok && h.r.kind === 'start' ? h.key : null;
    if (key === st.hoverKey) return;
    st.hoverKey = key;
    try { net.invokeMethodAsync('DragHover', st.src, key); } catch (e) { }
  }

  // 左栏、右栏（含人物页签）不是落点：原“自身”投放已取消，拖到这里原物归位（v0.7 第 6.2 节）
  function overInfoColumn(x, y) {
    ghost.style.display = 'none';
    var el = document.elementFromPoint(x, y);
    ghost.style.display = '';
    return !!(el && el.closest && el.closest('.sidebar, .detail'));
  }

  function hover(x, y) {
    var h = hit(x, y);
    notifyHover(h);
    if (lastHover && (!h || lastHover !== h.el)) lastHover.classList.remove('drop-hover', 'drop-hover-no');
    line.style.display = 'none';
    var label = ghost.querySelector('.dg-label');
    st.hit = h;
    if (!h || !h.r) {
      ghost.classList.remove('ok', 'preview', 'no');
      label.textContent = '';
      if (overInfoColumn(x, y)) { ghost.classList.add('no'); label.textContent = '⛔ ' + NO_SELF; }
      lastHover = null;
      return;
    }
    lastHover = h.el;
    var zoneLike = h.el.getAttribute('data-drop-level') === 'zone';
    if (h.r.ok) {
      h.el.classList.add('drop-hover');
      h.el.classList.remove('drop-hover-no');
      if (zoneLike) line.style.display = 'block';
      ghost.classList.remove('no');
      ghost.classList.toggle('preview', h.r.kind === 'preview');
      ghost.classList.toggle('ok', h.r.kind !== 'preview');
      label.textContent = h.r.label + (h.r.qty ? ' · 按住 Shift 松手可选数量' : '');
    } else {
      h.el.classList.add('drop-hover-no');
      ghost.classList.remove('ok', 'preview');
      ghost.classList.add('no');
      label.textContent = '⛔ ' + (h.r.reason || '暂不支持此组合');
    }
  }

  function onUp(ev) {
    if (!st) return;
    if (!st.started) { st = null; return; }
    // 起拖后本次松手不再触发卡片点击
    var swallow = function (e) { e.stopPropagation(); e.preventDefault(); document.removeEventListener('click', swallow, true); };
    document.addEventListener('click', swallow, true);
    setTimeout(function () { document.removeEventListener('click', swallow, true); }, 0);
    hover(ev.clientX, ev.clientY);
    var h = st.hit, src = st.src;
    if (!h || !h.r) {
      if (overInfoColumn(ev.clientX, ev.clientY)) flash(NO_SELF, ev.clientX, ev.clientY);
      cleanup();
      return;
    }
    if (!h.r.ok) { flash(h.r.reason || '暂不支持此组合', ev.clientX, ev.clientY); cleanup(); return; }
    var shift = ev.shiftKey;
    cleanup();
    net.invokeMethodAsync('DragDrop', src, h.key, h.index || 0, shift).then(function (json) {
      var r = JSON.parse(json || '{}');
      if (!r.ok && r.reason) flash(r.reason, ev.clientX, ev.clientY);
    });
  }

  // 非法落点：禁止符号与原因停留 1.5 秒，卡片回原位
  function flash(text, x, y) {
    var f = document.querySelector('.drop-flash');
    if (!f) { f = document.createElement('div'); f.className = 'drop-flash'; document.body.appendChild(f); }
    f.textContent = '⛔ ' + text;
    f.style.left = (x + 14) + 'px';
    f.style.top = (y + 12) + 'px';
    f.style.display = 'block';
    clearTimeout(flashTimer);
    flashTimer = setTimeout(function () { f.style.display = 'none'; }, 1500);
  }

  function onKey(ev) {
    if (ev.key === 'Escape' && st && st.started) {
      ev.preventDefault();
      ev.stopImmediatePropagation();
      cancel();
    }
  }

  function cancel() { if (st) cleanup(); }

  function cleanup() {
    if (st && st.hoverKey) { try { net.invokeMethodAsync('DragHover', st.src, null); } catch (e) { } }
    if (st && st.el) st.el.classList.remove('drag-source');
    document.body.classList.remove('dragging');
    document.querySelectorAll('.drop-can, .drop-can-preview, .drop-hover, .drop-hover-no').forEach(function (d) {
      d.classList.remove('drop-can', 'drop-can-preview', 'drop-hover', 'drop-hover-no');
    });
    if (ghost) ghost.remove();
    if (line) line.remove();
    ghost = null; line = null; lastHover = null; st = null;
  }

  return { init: init, cancel: cancel, active: function () { return !!(st && st.started); } };
})();
