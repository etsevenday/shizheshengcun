// 适者生存网页壳的少量浏览器接口：本地单槽存档与快捷键转发。
window.survival = {
  saveGet: function (key) {
    try { return window.localStorage.getItem(key); } catch (e) { return null; }
  },
  // 先写临时键再写正式键，正式键写入成功后删除临时键；失败返回错误文本。
  saveSet: function (key, value) {
    try {
      window.localStorage.setItem(key + '.tmp', value);
      window.localStorage.setItem(key, value);
      window.localStorage.removeItem(key + '.tmp');
      return '';
    } catch (e) {
      return String(e && e.message ? e.message : e);
    }
  },
  // 地点变化后地块两行的横滚容器（相邻地图子栏、第二行）复位到最左（渲染后可能还有一次布局，下一帧再复位一次）
  resetDesk: function () {
    var go = function () {
      document.querySelectorAll('.map-adjacent, .fac-scroll').forEach(function (d) { d.scrollLeft = 0; });
    };
    go();
    requestAnimationFrame(go);
  },
  // 二级面板打开、战斗或敌踪开始、读档恢复战斗时：中部纵滚回到顶部，让面板/覆盖层完整替换地块两行（试玩修复 G1、G2）
  centerTop: function () {
    var go = function () { var c = document.querySelector('.center-scroll'); if (c) c.scrollTop = 0; };
    go();
    setTimeout(go, 50);
  },
  // 选中卡片后卡下操作条可能落在横滚行的可视区外（卡片靠左/靠右）：横向把它滚进来；在中部下方看不到时中部纵滚到刚好露出（试玩修复 L5）
  revealBar: function () {
    var go = function () {
      var bar = document.querySelector('.tbar.cact');
      if (!bar) return;
      var p = bar.parentElement;
      while (p && p !== document.body) {
        var cs = getComputedStyle(p);
        if ((cs.overflowX === 'auto' || cs.overflowX === 'scroll') && p.scrollWidth > p.clientWidth) {
          var br = bar.getBoundingClientRect(), pr = p.getBoundingClientRect();
          if (br.right > pr.right) p.scrollLeft += br.right - pr.right + 8;
          br = bar.getBoundingClientRect();
          if (br.left < pr.left) p.scrollLeft -= pr.left - br.left + 8;
        }
        // 纵滚（所属区内部纵滚，或兜底的中部纵滚）：操作条底部在可视区外时下移到刚好露出（不超过把操作条顶部推出可视区）（S14 §1.3）
        if ((cs.overflowY === 'auto' || cs.overflowY === 'scroll') && p.scrollHeight > p.clientHeight) {
          var b2 = bar.getBoundingClientRect(), r2 = p.getBoundingClientRect();
          if (b2.bottom > r2.bottom) p.scrollTop += Math.min(b2.bottom - r2.bottom + 8, Math.max(0, b2.top - r2.top));
        }
        p = p.parentElement;
      }
    };
    // 渲染后立即执行一次，布局再变时下一轮再校正（不用 requestAnimationFrame：页面在后台时它会被暂停）
    go();
    setTimeout(go, 50);
  },
  // 点火工具菜单：点菜单外（忽略 ignoreSel 匹配的“点燃”按钮本身）时通知 .NET 关闭；只监听，不拦截那次点击
  watchOutside: function (el, dotnet, ignoreSel) {
    window.survival.unwatchOutside();
    var h = function (ev) {
      if (!el || !el.isConnected) { window.survival.unwatchOutside(); return; }
      var t = ev.target;
      if (el.contains(t) || (ignoreSel && t.closest && t.closest(ignoreSel))) return;
      dotnet.invokeMethodAsync('OutsideClick');
    };
    window.__survivalOutside = h;
    document.addEventListener('pointerdown', h, true);
  },
  unwatchOutside: function () {
    if (window.__survivalOutside) document.removeEventListener('pointerdown', window.__survivalOutside, true);
    window.__survivalOutside = null;
  },
  // 人物对话出现（S07 §7）：取消未提交的拖拽（不执行落点），收起背后按钮的键盘焦点
  dialogueOnset: function () {
    try { if (window.survivalDrag) survivalDrag.cancel(); } catch (e) { }
    var a = document.activeElement;
    if (a && a.blur && !(a.closest && a.closest('.dlg-layer'))) a.blur();
  },
  // 元素在视口中的位置 [left, top, right, bottom]（状态图标浮层锚点）；找不到返回空数组
  rectById: function (id) {
    var el = document.getElementById(id);
    if (!el) return [];
    var r = el.getBoundingClientRect();
    return [r.left, r.top, r.right, r.bottom];
  },
  // 聚焦并选中输入框内容，直接打字即替换
  focusSelect: function (el) {
    try { el.focus(); el.select(); } catch (e) { }
  },
  // 每位玩家自己的界面偏好（折叠面板等），不进存档
  prefGet: function (key) {
    try { return window.localStorage.getItem('survival.pref.' + key); } catch (e) { return null; }
  },
  prefSet: function (key, value) {
    try { window.localStorage.setItem('survival.pref.' + key, value); } catch (e) { }
  },
  saveRemove: function (key) {
    try { window.localStorage.removeItem(key); window.localStorage.removeItem(key + '.tmp'); } catch (e) { }
  },
  // 通用留言组件：游戏提供当前状态与存档，其余（截图、系统识别、提交）由组件完成
  initFeedback: function (bridge) {
    if (!window.FeedbackWidget) return;
    FeedbackWidget.init({
      project: 'shizheshengcun',
      configUrl: 'feedback.json',
      title: '反馈与留言',
      subtitle: '小问题随手留言：会自动附上你的系统、浏览器、当前画面和游戏状态，方便作者定位。',
      getContext: async function () { return JSON.parse(await bridge.invokeMethodAsync('GetFeedbackContext')); },
      getAttachment: async function () {
        var t = await bridge.invokeMethodAsync('GetSaveJson');
        return t ? { name: 'save.json', text: t } : null;
      },
      attachmentLabel: '附带当前存档（方便作者复现问题）',
    });
  },
  openFeedback: function () { if (window.FeedbackWidget) FeedbackWidget.open(); },
  hookKeys: function (dotnet) {
    if (window.__survivalKeys) document.removeEventListener('keydown', window.__survivalKeys);
    window.__survivalKeys = function (ev) {
      var t = ev.target && ev.target.tagName;
      // 人物对话读完锁（Q10、S14 §10.1）：数字跳数量框、回车执行操作条、M/C/R/L、Esc 一律不转给游戏；
      // 对话层自己的按钮仍可用键盘聚焦后按回车/空格（浏览器原生行为），F 反馈不属于游戏操作
      if (document.querySelector('.dlg-layer')) {
        if (ev.key === 'Escape') { ev.preventDefault(); return; }
        if ((ev.key === 'f' || ev.key === 'F') && t !== 'INPUT' && t !== 'TEXTAREA' && !ev.ctrlKey && !ev.altKey && !ev.metaKey && !(window.FeedbackWidget && FeedbackWidget.isOpen())) {
          ev.preventDefault();
          dotnet.invokeMethodAsync('OnKey', 'F');
        }
        return;
      }
      // 就近操作浮窗（S14 §14.1）里的按钮：回车/空格/数字只作用于浮窗自身，不转给卡下操作条；Esc 照常交给 Esc 栈（先收起浮窗）
      if (ev.target && ev.target.closest && ev.target.closest('.apop') && ev.key !== 'Escape') return;
      var inBar = ev.target && ev.target.closest && ev.target.closest('.tbar');
      // 选中物品卡后直接按数字：跳进卡片下方操作条的数量框（v0.7 起操作条在卡下文档流里）
      if (/^[0-9]$/.test(ev.key) && t !== 'INPUT' && t !== 'TEXTAREA' && !ev.ctrlKey && !ev.altKey && !ev.metaKey) {
        var inp = document.querySelector('.tbar input');
        if (inp) { inp.focus(); inp.select(); return; }
      }
      // 拾取条已弹出但光标不在条内（例如刚用鼠标点完卡片）时按回车：按条上当前数量直接拾取/放下
      if (ev.key === 'Enter' && !inBar && t !== 'INPUT' && t !== 'TEXTAREA' && !ev.ctrlKey && !ev.altKey && !ev.metaKey) {
        var go = document.querySelector('.tbar .tbar-go');
        if (go && !go.disabled) { ev.preventDefault(); go.click(); return; }
      }
      if (t === 'INPUT' || t === 'TEXTAREA') {
        // 拾取条输入框只收数字：字母快捷键照常生效，回车与 Esc 由拾取条自己处理
        if (!inBar && ev.key !== 'Escape') return;
        // 回车由操作条自己处理；Esc 在数量框里先只退出输入框（Esc 栈第一级），再按一次才交给 GameScreen 清选中（复验新发现 1）
        if (inBar && ev.key === 'Enter') return;
        if (inBar && ev.key === 'Escape') { ev.preventDefault(); ev.target.blur(); return; }
      }
      if (ev.ctrlKey || ev.altKey || ev.metaKey) return;
      var k = ev.key;
      if (window.FeedbackWidget && FeedbackWidget.isOpen()) return;
      // 数量框等弹窗自己处理按键（回车确认、Esc 取消），不再触发游戏快捷键
      if (ev.target && ev.target.closest && ev.target.closest('.modal')) return;
      if (['m', 'M', 'c', 'C', 'r', 'R', 'l', 'L', 'f', 'F', 'Escape'].indexOf(k) >= 0) {
        ev.preventDefault();
        // Esc 收起预览/选中后，焦点仍留在刚点过的按钮上会被画成键盘焦点框，看起来像“仍选中”（试玩修复 L6）
        if (k === 'Escape' && document.activeElement && document.activeElement.tagName === 'BUTTON') document.activeElement.blur();
        dotnet.invokeMethodAsync('OnKey', k.length === 1 ? k.toUpperCase() : k);
      }
    };
    document.addEventListener('keydown', window.__survivalKeys);
  }
};

// 人物对话层的对话流（S14 §10.3）：停在底部时新句追加后跟随到最新；向上回看时保持阅读位置，亮出不含句数的“回到最新”。
// 只改滚动位置，不推进对白；“回到最新”按钮所在的容器类名由这里切换（Blazor 不管理这个类）。
window.survivalDlg = (function () {
  function latestHost(el) {
    var layer = el && el.closest ? el.closest('.dlg-layer') : null;
    return layer ? layer.querySelector('.dlg-latest-host') : null;
  }
  function showLatest(el, on) {
    var h = latestHost(el);
    if (h) h.classList.toggle('show', !!on);
  }
  function bottom(el) { return el.scrollTop + el.clientHeight >= el.scrollHeight - 8; }
  return {
    sync: function (el, count) {
      if (!el) return;
      if (!el.__dlg) {
        el.__dlg = { count: 0, atBottom: true };
        el.addEventListener('scroll', function () {
          el.__dlg.atBottom = bottom(el);
          if (el.__dlg.atBottom) showLatest(el, false);
        });
      }
      var st = el.__dlg;
      // 新的一次对话（句数变少）或首次出现：从底部开始
      if (count <= st.count || st.count === 0) { st.count = count; el.scrollTop = el.scrollHeight; st.atBottom = true; showLatest(el, false); return; }
      st.count = count;
      if (st.atBottom) { el.scrollTop = el.scrollHeight; showLatest(el, false); }
      else showLatest(el, true);
    },
    latest: function (el) {
      if (!el) return;
      el.scrollTop = el.scrollHeight;
      if (el.__dlg) el.__dlg.atBottom = true;
      showLatest(el, false);
    }
  };
})();

// 就近操作浮窗（S14 §14.1）：锚定到对象卡/指令按钮旁。优先 data-place 顺序里第一个放得下的方向（附近空位），都放不下取空间最大的一侧，
// 最后限制在视口内；锚点所在的滚动区滚动、窗口改变或内容变化时跟随重算；锚点消失或滚出可视区（可见不足四分之一）时通知 .NET 收起，
// 不留下脱离目标的可执行旧按钮。只改位置，不推进任何玩法。
window.survivalPop = (function () {
  var cur = null;
  function visibleRect(el) {
    var r = el.getBoundingClientRect();
    var L = r.left, T = r.top, R = r.right, B = r.bottom;
    for (var p = el.parentElement; p && p !== document.body; p = p.parentElement) {
      var cs = getComputedStyle(p);
      if (cs.overflowX !== 'visible' || cs.overflowY !== 'visible') {
        var pr = p.getBoundingClientRect();
        L = Math.max(L, pr.left); T = Math.max(T, pr.top); R = Math.min(R, pr.right); B = Math.min(B, pr.bottom);
      }
    }
    L = Math.max(L, 0); T = Math.max(T, 0); R = Math.min(R, window.innerWidth); B = Math.min(B, window.innerHeight);
    var area = Math.max(1, r.width * r.height);
    var vis = R > L && B > T ? (R - L) * (B - T) / area : 0;
    return { left: L, top: T, right: R, bottom: B, vis: vis };
  }
  function place() {
    if (!cur) return;
    var el = cur.el;
    if (!el || !el.isConnected) { stop(); return; }
    var a = cur.anchor ? document.getElementById(cur.anchor) : null;
    if (!a) { lost(); return; }
    var A = visibleRect(a);
    if (A.vis < 0.25) { lost(); return; }
    var W = window.innerWidth, H = window.innerHeight, m = 8, g = 8;
    var w = el.offsetWidth, h = el.offsetHeight;
    var order = (el.getAttribute('data-place') || 'right,left,below,above').split(',');
    var room = { right: W - m - A.right - g, left: A.left - g - m, below: H - m - A.bottom - g, above: A.top - g - m };
    function pos(side) {
      if (side === 'right') return { x: A.right + g, y: A.top };
      if (side === 'left') return { x: A.left - g - w, y: A.top };
      if (side === 'below') return { x: A.left, y: A.bottom + g };
      return { x: A.left, y: A.top - g - h };
    }
    function fits(side) { return side === 'right' || side === 'left' ? room[side] >= w : room[side] >= h; }
    var side = null;
    for (var i = 0; i < order.length; i++) { if (fits(order[i])) { side = order[i]; break; } }
    if (!side) {
      // 都放不下：取相对空间最大的一侧（向上/另一侧翻转），再整体限制在屏内
      side = order.slice().sort(function (x, y) {
        var fx = room[x] / (x === 'right' || x === 'left' ? w : h), fy = room[y] / (y === 'right' || y === 'left' ? w : h);
        return fy - fx;
      })[0];
    }
    var p = pos(side);
    var x = Math.max(m, Math.min(p.x, W - m - w));
    var y = Math.max(m, Math.min(p.y, H - m - h));
    el.style.left = Math.round(x) + 'px';
    el.style.top = Math.round(y) + 'px';
    el.style.visibility = 'visible';
    el.setAttribute('data-side', side);
  }
  function lost() {
    var d = cur && cur.dotnet;
    stop();
    if (d) { try { d.invokeMethodAsync('AnchorLost'); } catch (e) { } }
  }
  function onMove() { if (cur && !cur.raf) cur.raf = requestAnimationFrame(function () { if (cur) { cur.raf = 0; place(); } }); }
  function stop() {
    if (!cur) return;
    window.removeEventListener('scroll', onMove, true);
    window.removeEventListener('resize', onMove);
    if (cur.ro) cur.ro.disconnect();
    if (cur.raf) cancelAnimationFrame(cur.raf);
    cur = null;
  }
  return {
    attach: function (el, anchorId, dotnet) {
      if (!el) return;
      if (cur && cur.el === el) { cur.anchor = anchorId; cur.dotnet = dotnet; place(); return; }
      stop();
      cur = { el: el, anchor: anchorId, dotnet: dotnet, raf: 0, ro: null };
      window.addEventListener('scroll', onMove, true);
      window.addEventListener('resize', onMove);
      if (window.ResizeObserver) { cur.ro = new ResizeObserver(onMove); cur.ro.observe(el); }
      place();
    },
    detach: function (el) { if (cur && cur.el === el) stop(); },
    place: place
  };
})();

// 试玩修复 B7：点中央条/暂停层“退出”后，地块两行（相邻地图、设施行）保持原滚动位置。
// 退出会让中央条消失、火堆卡与选中项重绘，期间若有程序性滚动（不是玩家的滚轮/拖动/点击）把设施行横向推到别处，
// 在随后 1.5 秒内还原到点“退出”那一刻的位置；玩家自己滚动或点按这两行时立即放弃还原。只改滚动位置，不涉及玩法。
(function () {
  var snap = null, until = 0;
  function rows() { return Array.prototype.slice.call(document.querySelectorAll('.map-adjacent, .fac-scroll')); }
  function fix() {
    if (!snap) return;
    if (Date.now() > until) { release(); return; }
    snap.forEach(function (s) {
      if (s.el.isConnected && (s.el.scrollLeft !== s.x || s.el.scrollTop !== s.y)) { s.el.scrollLeft = s.x; s.el.scrollTop = s.y; }
    });
    setTimeout(fix, 40);
  }
  function take() {
    snap = rows().map(function (el) { return { el: el, x: el.scrollLeft, y: el.scrollTop }; });
    until = Date.now() + 1500;
    setTimeout(fix, 0);
  }
  function release() { snap = null; until = 0; }
  document.addEventListener('pointerdown', function (ev) {
    var t = ev.target;
    if (t && t.closest && t.closest('.cp-exit')) { take(); return; }
    if (snap && t && t.closest && t.closest('.map-adjacent, .fac-scroll')) release();
  }, true);
  document.addEventListener('keydown', function (ev) {
    var t = ev.target;
    if ((ev.key === 'Enter' || ev.key === ' ') && t && t.closest && t.closest('.cp-exit')) take();
  }, true);
  document.addEventListener('wheel', function () { if (snap) release(); }, true);
  document.addEventListener('scroll', function (ev) {
    if (!snap) return;
    if (Date.now() > until) { release(); return; }
    snap.forEach(function (s) {
      if (s.el === ev.target && s.el.isConnected && (s.el.scrollLeft !== s.x || s.el.scrollTop !== s.y)) {
        s.el.scrollLeft = s.x; s.el.scrollTop = s.y;
      }
    });
  }, true);
})();
