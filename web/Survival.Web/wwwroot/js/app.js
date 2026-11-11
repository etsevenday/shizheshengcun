// 适者生存网页壳的少量浏览器接口：本地多存档（B40）与快捷键转发。
window.survival = {
  // B40 多存档（S01 Q335）：每个槽一个独立键，单次 setItem 原子替换。slotGet 区分“没有”与“读不出”（Q403）：
  // 返回 '0' 没有、'1'+值、'2'+错误文本；slotSet 失败回传“异常类别名 + \u001f + 错误文本”（Q282：C# 按类别名识别配额用尽，原始异常同时打到控制台）。
  slotGet: function (key) {
    try {
      var v = window.localStorage.getItem(key);
      return v === null ? '0' : '1' + v;
    } catch (e) {
      try { console.warn('[survival] 存档读取失败', key, e); } catch (e2) { }
      return '2' + String(e && e.message ? e.message : e);
    }
  },
  slotSet: function (key, value) {
    try {
      window.localStorage.setItem(key, value);
      return '';
    } catch (e) {
      try { console.warn('[survival] 存档写入失败', key, e); } catch (e2) { }
      var name = e && e.name ? String(e.name) : '';
      return name + '\u001f' + String(e && e.message ? e.message : e);
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
  // B32b（Q363）：晾制「查看配方」把食谱区滚到可见（只定位，不改选择以外的状态）
  scrollIntoView: function (el) {
    if (!el || !el.scrollIntoView) return;
    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    try { el.scrollIntoView({ block: 'start', behavior: reduce ? 'auto' : 'smooth' }); } catch (e) { el.scrollIntoView(); }
  },
  // B22 试玩修复 M408：滚到某元素（居中）并短暂高亮（回收箱卡“取出…”跳到人物页的回收箱块）
  scrollFlash: function (id) {
    var el = document.getElementById(id);
    if (!el) return false;
    try { el.scrollIntoView({ block: 'center', behavior: 'smooth' }); } catch (e) { el.scrollIntoView(); }
    el.classList.remove('flash-focus');
    void el.offsetWidth;
    el.classList.add('flash-focus');
    var k = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--motion-k')) || 1; // B31a：随动画速度
    setTimeout(function () { el.classList.remove('flash-focus'); }, 2200 * k);
    return true;
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
  // B40 多存档页面接口：storage 事件（另一页面写了存档键，Q381 重新确认写权限）、每秒一次的现实计时（Q335 / Q408），
  // 有未保存进度时浏览器关闭／刷新／离开请求原生确认（Q388：只调 preventDefault，文字由浏览器决定，离开时不追加写入）。
  initSaves: function (bridge) {
    window.addEventListener('storage', function (ev) {
      if (!ev.key || ev.key.indexOf('survival.save.v2.') === 0) bridge.invokeMethodAsync('OnStorage');
    });
    setInterval(function () { bridge.invokeMethodAsync('OnTimer'); }, 1000);
    window.addEventListener('beforeunload', function (ev) {
      if (!window.__survivalUnsaved) return;
      ev.preventDefault();
      ev.returnValue = '';
    });
  },
  setUnsaved: function (u) { window.__survivalUnsaved = !!u; },
  // Q406：存档时刻按浏览器本地格式到分钟（卡面与菜单带年月日，底栏只有时分）
  fmtLocal: function (ms, withDate) {
    var d = new Date(ms);
    try {
      return withDate
        ? d.toLocaleString(undefined, { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
        : d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    } catch (e) { return d.toString(); }
  },
  saveRemove: function (key) {
    try { window.localStorage.removeItem(key); window.localStorage.removeItem(key + '.tmp'); } catch (e) { }
  },
  // 阅读字体与动画速度（B31a，Q295/Q321）：font 为 pixel/clear，k 为时长倍率 0.5/1/1.5。
  // 偏好读写与默认回落在内核 UiPresentationPrefs；这里只把结果落到 <html>，CSS 据此换字体、乘动效时长。
  applyPresentation: function (font, k) {
    var r = document.documentElement;
    if (font === 'clear') r.setAttribute('data-read-font', 'clear'); else r.removeAttribute('data-read-font');
    r.style.setProperty('--motion-k', String(k > 0 ? k : 1));
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
      // B31b（Q322「返回」）：反馈关闭后回到游戏菜单，键盘焦点回到菜单里的按钮
      onClose: function () {
        var b = document.querySelector('.menu-modal .mm-foot .primary');
        if (b) { try { b.focus(); } catch (e) { } }
      },
    });
  },
  openFeedback: function () { if (window.FeedbackWidget) FeedbackWidget.open(); },
  // B31d（Q336）：实际构建（发布流程把 <meta name="app-build"> 写成「日期 · 提交短号」，本地为「本地开发版」）
  appBuild: function () { var m = document.querySelector('meta[name="app-build"]'); return m && m.content ? String(m.content) : ''; },
  hookKeys: function (dotnet) {
    if (window.__survivalKeys) document.removeEventListener('keydown', window.__survivalKeys);
    window.__survivalKeys = function (ev) {
      var t = ev.target && ev.target.tagName;
      // 反馈与留言窗口开着：按键归窗口自己（它的 Esc 只关窗口，关掉后回到游戏菜单，Q322）
      if (window.FeedbackWidget && FeedbackWidget.isOpen()) return;
      // B31b（Q322）：游戏菜单／战斗菜单打开时，只有 Esc 交给游戏（收起菜单）；回车、空格、数字留给菜单里的按钮，不落到背后的操作条、对白或战斗指令
      if (document.querySelector('.menu-modal')) {
        if (ev.key === 'Escape' && !ev.ctrlKey && !ev.altKey && !ev.metaKey) {
          ev.preventDefault();
          dotnet.invokeMethodAsync('OnKey', 'Escape');
        }
        return;
      }
      // 人物对话读完锁（Q10、S14 §10.1）：数字跳数量框、回车执行操作条、M/C/R/L 一律不转给游戏；对话层自己的按钮仍可用键盘聚焦后按回车/空格（浏览器原生行为）。
      // Esc 不结束对白，交给游戏打开当前情境的游戏菜单（B31b，Q322）
      if (document.querySelector('.dlg-layer')) {
        if (ev.key === 'Escape' && !ev.ctrlKey && !ev.altKey && !ev.metaKey) {
          ev.preventDefault();
          dotnet.invokeMethodAsync('OnKey', 'Escape');
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
      // 全屏战斗（A10 §6.4）：1—5 只选本页技能位、Q/E 切武器Ⅰ/Ⅱ页（0AP）、←/→ 在合法目标间移动预测、回车提交当前预测的目标；
      // 焦点在按钮或可点锚点上时回车/空格交给它自己（浏览器原生），不重复提交
      if (document.querySelector('.bfs') && t !== 'INPUT' && t !== 'TEXTAREA') {
        var onCtl = document.activeElement && document.activeElement.closest && document.activeElement.closest('button, [role=button]');
        // 试玩修复 B9-7：刚点过的技能/指令按钮仍带焦点时，已有待确认指令或正在瞄准，回车是“提交”而不是再点一次该按钮（会把选择收起）
        if (k === 'Enter' && onCtl && onCtl.closest('.bfs-console') && document.querySelector('#bt-confirm, .bfs .anc.targetable')) onCtl = null;
        if (/^[1-5]$/.test(k) || k === 'q' || k === 'Q' || k === 'e' || k === 'E' || k === 'ArrowLeft' || k === 'ArrowRight' || (k === 'Enter' && !onCtl)) {
          ev.preventDefault();
          dotnet.invokeMethodAsync('OnKey', k.length === 1 ? k.toUpperCase() : k);
          return;
        }
      }
      // 数量框等弹窗自己处理按键（回车确认、Esc 取消），不再触发游戏快捷键
      if (ev.target && ev.target.closest && ev.target.closest('.modal')) return;
      // B31b（Q322）：反馈只从游戏菜单进入，F 不再直达
      if (['m', 'M', 'c', 'C', 'r', 'R', 'l', 'L', 'Escape'].indexOf(k) >= 0) {
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
  // B39（N02 附录 §1、DLG-A06，B26b/B38 遗留“1280×720 长句顶部被裁”）：新句出现时按句首锚定——新句整段放得下就停在底部，
  // 放不下就让它的第一行贴住可视区上沿（不再滚到底把句首裁掉）。“正在跟读”= 已在底部，或最新一句的句首没有落到可视区下沿之下；
  // 向上回看（最新句首在可视区下方）时保持位置并亮出“回到最新”。只滚动本流，不推进对白。
  function latestHost(el) {
    var layer = el && el.closest ? el.closest('.dlg-layer') : null;
    return layer ? layer.querySelector('.dlg-latest-host') : null;
  }
  function showLatest(el, on) {
    var h = latestHost(el);
    if (h) h.classList.toggle('show', !!on);
  }
  function bottom(el) { return el.scrollTop + el.clientHeight >= el.scrollHeight - 8; }
  function nowTop(el) {
    var n = el.querySelector('.now');
    if (!n) return null;
    return el.scrollTop + n.getBoundingClientRect().top - el.getBoundingClientRect().top;
  }
  function following(el) {
    if (bottom(el)) return true;
    var t = nowTop(el);
    return t !== null && t - el.scrollTop <= el.clientHeight - 24;
  }
  function anchor(el) {
    var max = Math.max(0, el.scrollHeight - el.clientHeight);
    var t = nowTop(el);
    el.scrollTop = t === null ? max : Math.min(max, Math.max(0, t - 6));
  }
  return {
    sync: function (el, count) {
      if (!el) return;
      if (!el.__dlg) {
        el.__dlg = { count: 0, follow: true };
        el.addEventListener('scroll', function () {
          el.__dlg.follow = following(el);
          if (el.__dlg.follow) showLatest(el, false);
        });
      }
      var st = el.__dlg;
      // 新的一次对话（句数变少）或首次出现：从这一段的句首开始
      if (count <= st.count || st.count === 0) { st.count = count; anchor(el); st.follow = true; showLatest(el, false); return; }
      st.count = count;
      if (st.follow) { anchor(el); st.follow = true; showLatest(el, false); }
      else showLatest(el, true);
    },
    latest: function (el) {
      if (!el) return;
      anchor(el);
      if (el.__dlg) el.__dlg.follow = true;
      showLatest(el, false);
    }
  };
})();

// 就近操作浮窗（S14 §14.1）：锚定到对象卡/指令按钮旁。优先 data-place 顺序里第一个放得下的方向（附近空位），都放不下取空间最大的一侧，
// 最后限制在视口内；锚点所在的滚动区滚动、窗口改变或内容变化时跟随重算；锚点消失或滚出可视区（可见不足四分之一）时通知 .NET 收起，
// 不留下脱离目标的可执行旧按钮。只改位置，不推进任何玩法。
window.survivalPop = (function () {
  var cur = null;
  // B31d（逐字稿 §1.2 Q338 选 B）：居中局部盘真正显示时，四轨（.center-scroll）加 inert——不能点、拖、取得键盘焦点；收盘时去掉。
  // 只动自己加上的 inert（data-disk-inert 标记），Blazor 不管理这个属性。选卡盘没有操作内容时被样式隐藏（.card-pop:not(:has(.tbar))），不算显示。
  function railsInert(on) {
    var r = document.querySelector('.wasteland .center-scroll');
    if (!r) return;
    if (on) {
      if (!r.hasAttribute('inert')) { r.setAttribute('inert', ''); r.setAttribute('data-disk-inert', '1'); }
    } else if (r.getAttribute('data-disk-inert')) {
      r.removeAttribute('inert'); r.removeAttribute('data-disk-inert');
    }
  }
  // 点遮罩、Esc、× 或盘里的「取消」收盘后：焦点回到仍存在、可聚焦（不在 inert 里）的触发控件——先找发起它的对象卡／按钮，再找打开前最后点过的控件
  var lastTrigger = null;
  function noteTrigger(ev) {
    var t = ev.target;
    if (!t || !t.closest || t.closest('.apop, .pop-ghosts, .disk-scrim')) return;
    var c = t.closest('button, [tabindex], a[href], input, select, textarea');
    if (c) lastTrigger = c;
  }
  document.addEventListener('pointerdown', noteTrigger, true);
  document.addEventListener('focusin', noteTrigger, true);
  function restoreFocus(list) {
    setTimeout(function () {
      for (var i = 0; i < list.length; i++) {
        var c = list[i];
        if (!c || !c.isConnected || c.disabled || (c.closest && c.closest('[inert]')) || typeof c.focus !== 'function') continue;
        try { c.focus({ preventScroll: true }); } catch (e) { try { c.focus(); } catch (e2) { } }
        if (document.activeElement === c) return;
      }
    }, 0);
  }
  function onCancelClick(ev) {
    var t = ev.target;
    if (cur && t && t.closest && t.closest('.apop-x, .apop-cancel, [data-feed-cancel], [data-disk-cancel]')) cur.restore = true;
  }
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
  // B32d（逐字稿 §1.5 Q378）：打开会覆盖来源卡的面板时按来源失效收盘。以实际面板覆盖范围判断——中部一级面板（.panel-host：地图、制作、
  // 安全屋、整备……，随面板高低盖住①②、①—③或四行）与全屏确认层（.overflow-layer：终止远征、远征物资、重置地牢等）；来源卡在可见范围内
  // 露在这些面板之外的部分不足四分之一（与滚出同一口径）即算被盖住。来源卡本身在该面板里的不算。返回盖住它的面板，没被盖住为 null。
  var COVER_SEL = '.center-scroll.four .panel-host, .overflow-layer';
  function coverOf(a, A) {
    var covers = Array.prototype.filter.call(document.querySelectorAll(COVER_SEL), function (c) {
      return !c.contains(a) && c.offsetWidth > 0 && c.offsetHeight > 0;
    });
    if (!covers.length || A.right <= A.left || A.bottom <= A.top) return null;
    var rects = covers.map(function (c) { return c.getBoundingClientRect(); });
    var n = 6, open = 0, hits = covers.map(function () { return 0; });
    for (var i = 0; i < n; i++) {
      for (var j = 0; j < n; j++) {
        var x = A.left + (A.right - A.left) * (i + 0.5) / n, y = A.top + (A.bottom - A.top) * (j + 0.5) / n, k;
        for (k = 0; k < rects.length; k++) { var r = rects[k]; if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) break; }
        if (k === rects.length) open++; else hits[k]++;
      }
    }
    if (A.vis * open / (n * n) >= 0.25) return null;
    var best = 0;
    for (var m = 1; m < hits.length; m++) if (hits[m] > hits[best]) best = m;
    return covers[best];
  }
  function place() {
    if (!cur) return;
    var el = cur.el;
    if (!el || !el.isConnected) { stop(); return; }
    // B31c 试玩修复（B1，Q324）：居中盘的位置只由 CSS 决定（shell.css .apop-c：inset 0 + margin auto），不按某一刻量到的尺寸算、
    // 也不受锚点可见度影响；先居中再做来源失效判定（锚点连续 250ms 看不见仍照原规则收起，收起前不偏位）
    var centered = !!el.getAttribute('data-center');
    if (centered) {
      el.style.left = ''; el.style.top = '';
      el.style.visibility = 'visible';
      el.setAttribute('data-side', 'center');
      railsInert(el.offsetWidth > 0 && el.offsetHeight > 0);
    }
    var a = cur.anchor ? document.getElementById(cur.anchor) : null;
    if (a) cur.anchorEl = a;
    var A = a ? visibleRect(a) : null;
    // B21 试玩修复：页面重排（右栏内容变化引起的一闪滚动条等）会让锚点瞬间“看不见”；连续 250ms 仍看不见才算滚出，避免浮窗刚弹出就被收起
    if (!a || A.vis < 0.25) {
      var now = Date.now();
      if (!cur.lostAt) { cur.lostAt = now; setTimeout(function () { if (cur) place(); }, 260); return; }
      if (now - cur.lostAt >= 250) lost();
      return;
    }
    cur.lostAt = 0;
    // B32d（Q378）：来源卡被新开的面板盖住（渲染在背景也算不可用）→ 同样连续 250ms 才算，按来源失效收盘、焦点交给盖住它的面板
    var cover = coverOf(a, A);
    if (cover) {
      var t = Date.now();
      if (!cur.coveredAt) { cur.coveredAt = t; setTimeout(function () { if (cur) place(); }, 260); return; }
      if (t - cur.coveredAt >= 250) covered(cover);
      return;
    }
    cur.coveredAt = 0;
    // B31c（Q324）：局部操作盘统一在屏幕中央打开（上面已处理）；锚点仍用于“来源失效”判定：发起它的卡滚出或消失时照原规则收起
    if (centered) return;
    var W = window.innerWidth, H = window.innerHeight, m = 8, g = 8;
    var w = el.offsetWidth, h = el.offsetHeight;
    var order = (el.getAttribute('data-place') || 'right,left,below,above').split(',');
    // data-bound：浮窗限制在指定容器（中部列）里——上下可用空间也按容器算，放不下就翻到另一侧，不压左右栏和底栏（M77-B4、M78）
    var bsel = el.getAttribute('data-bound');
    var bel = bsel ? document.querySelector(bsel) : null;
    var B = bel ? bel.getBoundingClientRect() : { left: 0, top: 0, right: W, bottom: H };
    var room = { right: Math.min(W, B.right) - m - A.right - g, left: A.left - g - Math.max(m, B.left), below: Math.min(H, B.bottom) - m - A.bottom - g, above: A.top - g - Math.max(m, B.top) };
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
    if (bel) {
      x = Math.max(B.left + 4, Math.min(x, B.right - 4 - w));
      y = Math.max(B.top + 4, Math.min(y, B.bottom - 4 - h));
    }
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
  // B32d（Q378）：来源被面板盖住而收盘——只撤回本盘未提交的意图（.NET AnchorCovered：选卡盘保留选中，其余盘同各自 ×），不还焦点给触发控件，
  // 焦点交给盖住来源的面板（焦点已在该面板里就不动）。关面板后玩家从仍合法的来源重新打开。
  function covered(cover) {
    var d = cur && cur.dotnet;
    stop();
    if (d) { try { d.invokeMethodAsync('AnchorCovered'); } catch (e) { } }
    setTimeout(function () {
      if (!cover || !cover.isConnected) return;
      var ae = document.activeElement;
      if (ae && ae !== document.body && cover.contains(ae)) return;
      var list = cover.querySelectorAll('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
      for (var i = 0; i < list.length; i++) {
        var c = list[i];
        if (c.disabled || (c.closest && c.closest('[inert]')) || !(c.offsetWidth || c.offsetHeight)) continue;
        try { c.focus({ preventScroll: true }); } catch (e) { try { c.focus(); } catch (e2) { } }
        if (document.activeElement === c) return;
      }
    }, 0);
  }
  function onMove() { if (cur && !cur.raf) cur.raf = requestAnimationFrame(function () { if (cur) { cur.raf = 0; place(); } }); }
  function stop() {
    if (!cur) return;
    railsInert(false);
    if (cur.el) cur.el.removeEventListener('click', onCancelClick, true);
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
      cur = { el: el, anchor: anchorId, dotnet: dotnet, raf: 0, ro: null, anchorEl: document.getElementById(anchorId), trigger: lastTrigger, restore: false };
      el.addEventListener('click', onCancelClick, true);
      window.addEventListener('scroll', onMove, true);
      window.addEventListener('resize', onMove);
      if (window.ResizeObserver) { cur.ro = new ResizeObserver(onMove); cur.ro.observe(el); }
      place();
    },
    detach: function (el) {
      if (!cur || cur.el !== el) return;
      var back = cur.restore ? [cur.anchorEl, cur.trigger] : null;
      stop();
      if (back) restoreFocus(back);
    },
    // B31d（Q338）：点遮罩或 Esc 撤回本盘前由 GameScreen 调用，收盘后把焦点还给触发控件
    markCancel: function () { if (cur) cur.restore = true; },
    // B32g（逐字稿 §1.5 Q401 选 C）：打开新的一级浏览／工具面板时 .NET 已一律收起物品操作盘（不论是否盖住来源）；等盘卸下、四轨遮罩（inert）解除后，
    // 焦点交给新面板（.panel-host；全屏确认层 .overflow-layer）第一个可用控件。焦点已在该面板里就不动；盘仍在（未卸下）时逐帧再等，最多约 20 帧。
    focusPanel: function () {
      var tries = 0;
      function go() {
        if (document.querySelector('.wasteland .center-scroll[data-disk-inert]')) { if (++tries < 20) requestAnimationFrame(go); return; }
        var hosts = Array.prototype.filter.call(document.querySelectorAll(COVER_SEL), function (c) { return c.offsetWidth > 0 && c.offsetHeight > 0 && !(c.closest && c.closest('[inert]')); });
        var ae = document.activeElement;
        for (var h = 0; h < hosts.length; h++) if (ae && ae !== document.body && hosts[h].contains(ae)) return;
        for (var k = 0; k < hosts.length; k++) {
          var list = hosts[k].querySelectorAll('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
          for (var i = 0; i < list.length; i++) {
            var c = list[i];
            if (c.disabled || (c.closest && c.closest('[inert]')) || !(c.offsetWidth || c.offsetHeight)) continue;
            try { c.focus({ preventScroll: true }); } catch (e) { try { c.focus(); } catch (e2) { } }
            if (document.activeElement === c) return;
          }
        }
        if (!hosts.length && ++tries < 20) requestAnimationFrame(go);
      }
      setTimeout(go, 0);
    },
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

// B2a 答复 Q35（开发计划 v0.9 §B2a，M92）：键盘聚焦提示。带 data-tip 的卡/标签被键盘聚焦（:focus-visible）时，在旁边浮出一行完整说明
// （紧凑模式下不常驻的锅编号/单锅重量、直烤加工位编号、物品全名等）；鼠标悬停仍用原生 title。只读展示，不改选择、不触发操作。
(function () {
  var tip = null, anchor = null, timer = 0;
  function el() {
    if (!tip) { tip = document.createElement('div'); tip.className = 'focus-tip'; tip.id = 'focus-tip'; tip.setAttribute('role', 'tooltip'); document.body.appendChild(tip); }
    return tip;
  }
  function hide() {
    if (timer) { clearInterval(timer); timer = 0; }
    if (anchor) anchor.removeAttribute('aria-describedby');
    anchor = null; if (tip) tip.hidden = true;
  }
  function place() {
    if (!anchor || !anchor.isConnected || document.activeElement !== anchor) { hide(); return; }
    var r = anchor.getBoundingClientRect(), t = el();
    var w = t.offsetWidth, h = t.offsetHeight, m = 6;
    var y = r.bottom + m; if (y + h > innerHeight - 4) y = Math.max(4, r.top - h - m);
    var x = Math.min(Math.max(4, r.left), Math.max(4, innerWidth - w - 4));
    t.style.left = x + 'px'; t.style.top = y + 'px';
  }
  document.addEventListener('focusin', function (ev) {
    var a = ev.target;
    if (!a || !a.getAttribute || !a.getAttribute('data-tip')) { hide(); return; }
    var fv = false; try { fv = a.matches(':focus-visible'); } catch (e) { fv = true; }
    if (!fv) { hide(); return; }
    hide(); anchor = a;
    var t = el(); t.textContent = a.getAttribute('data-tip'); t.hidden = false;
    a.setAttribute('aria-describedby', 'focus-tip');
    place(); timer = setInterval(place, 250);
  });
  document.addEventListener('focusout', function (ev) { if (ev.target === anchor) hide(); });
  document.addEventListener('pointerdown', hide, true);
})();

// B31a（Q295/Q321）：页面一载入就按浏览器偏好套用阅读字体与动画速度（标题页也生效），无有效偏好时用默认（细像素、标准）。
// 取值与内核 UiPresentationPrefs 的 ParseFont/ParseSpeed 一致；进游戏后由网页壳再按同一偏好设置一次。
(function () {
  var font = null, speed = null;
  try { font = window.localStorage.getItem('survival.pref.readFont'); speed = window.localStorage.getItem('survival.pref.animSpeed'); } catch (e) { }
  var k = speed === 'fast' ? 0.5 : speed === 'slow' ? 1.5 : 1;
  window.survival.applyPresentation(font === 'clear' ? 'clear' : 'pixel', k);
})();

// B31c（R114 popup 退场；a9b93f0 动效说明「打开和关闭菜单、确认框、说明」退场 120–160ms、遮罩分轨）：
// 居中弹层（局部操作盘、游戏／战斗菜单、确认框、数量框）被移除时，把它的外观复制成一个不接收输入的影子，在原位淡出 140ms×动画速度后删掉。
// 只是表现：真实弹层已经关闭，输入立即回到背后；同一帧里又出现新弹层（例如从一个局部盘换到另一个）时影子直接删掉，不叠影。
// 系统减少动态效果时不生成影子（直接落到结果）。影子放在 body 下独立容器里，不进 Blazor 管理的节点。
(function () {
  var SEL = '.apop.apop-c, .menu-scrim, .overflow-layer, .modal-back';
  var host = null;
  function reduced() { try { return window.matchMedia('(prefers-reduced-motion: reduce)').matches; } catch (e) { return false; } }
  function k() { return parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--motion-k')) || 1; }
  function hostEl() {
    if (!host || !host.isConnected) {
      host = document.createElement('div');
      host.className = 'wasteland pop-ghosts';
      host.setAttribute('aria-hidden', 'true');
      document.body.appendChild(host);
    }
    return host;
  }
  function spawn(node) {
    var g = node.cloneNode(true);
    g.removeAttribute('id');
    g.querySelectorAll('[id]').forEach(function (x) { x.removeAttribute('id'); });
    g.setAttribute('inert', '');
    hostEl().appendChild(g);
    var done = function () { if (g.parentNode) g.parentNode.removeChild(g); };
    setTimeout(done, Math.round(160 * k()) + 40);
    // 新弹层已经接上（换盘）：不留影子
    requestAnimationFrame(function () {
      var live = document.querySelectorAll(SEL);
      for (var i = 0; i < live.length; i++) { if (!host.contains(live[i])) { done(); return; } }
    });
  }
  // B31c（B31b 遗留）：中央行动位收成窄条时，把窄条实际高度写到中部 --dock-live，一级面板只让出这么高（不多留空、不盖③④）
  var dockRo = window.ResizeObserver ? new ResizeObserver(syncDock) : null, dockEl = null;
  function syncDock() {
    var c = document.querySelector('section.center');
    if (!c) return;
    var bar = c.querySelector(':scope > .cprog.docked, :scope > .pause-dock');
    if (bar !== dockEl) {
      if (dockRo && dockEl) dockRo.unobserve(dockEl);
      dockEl = bar;
      if (dockRo && bar) dockRo.observe(bar);
    }
    var v = bar ? bar.offsetHeight + 'px' : '';
    if (c.style.getPropertyValue('--dock-live') !== v) { if (v) c.style.setProperty('--dock-live', v); else c.style.removeProperty('--dock-live'); }
  }
  function start() {
    if (!window.MutationObserver || !document.body) return;
    new MutationObserver(function (muts) {
      syncDock();
      if (reduced()) return;
      for (var i = 0; i < muts.length; i++) {
        var m = muts[i];
        if (host && (m.target === host || host.contains(m.target))) continue;
        for (var j = 0; j < m.removedNodes.length; j++) {
          var n = m.removedNodes[j];
          if (n.nodeType === 1 && n.matches && n.matches(SEL) && m.target.isConnected) spawn(n);
        }
      }
    }).observe(document.body, { childList: true, subtree: true });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();

// ───── B32b 即时操作与场景反馈（逐字稿 §1.2 Q341，§1.5 Q367、Q368、Q370）─────
// 时长随 Q321 动画速度（--motion-k）；系统减少动态效果时不做位移与淡变，直接显示结果（目标只静态描一次框）。
window.survivalFx = (function () {
  var last = null;          // 最近一次按下的物品卡：{ el, rect, art, t }
  var scene = null;         // 上一次绘制的当前地景：{ serial, bg, filter }
  function k() {
    var v = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--motion-k'));
    return isFinite(v) && v > 0 ? v : 1;
  }
  function reduced() { return !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches); }
  function visible(r) { return r && r.width > 0 && r.height > 0 && r.bottom > 0 && r.right > 0 && r.top < window.innerHeight && r.left < window.innerWidth; }
  function note(el) {
    if (!el) return;
    var card = el.closest ? el.closest('[data-card]') : null;
    if (!card) return;
    var art = card.querySelector('.k2-art svg, .k2-art img, .pc-icon svg, .pc-ico svg, svg, img');
    last = { el: card, rect: card.getBoundingClientRect(), art: art ? art.outerHTML : '', t: Date.now() };
  }
  document.addEventListener('pointerdown', function (ev) { if (ev.target && ev.target.closest) note(ev.target); }, true);
  function lit(el) {
    if (!el) return;
    el.classList.remove('fx-lit');
    void el.offsetWidth;
    el.classList.add('fx-lit');
    setTimeout(function () { el.classList.remove('fx-lit'); }, 1700 * k());
  }
  // Q367：成功的拾取、放下与拖拽转移，播一次来源→去向轨迹（180–250ms 基准，取 220ms）；只有一端可见时亮可见的那端；两端都看不见时只留原结果提示
  function swap(owner, itemId) {
    var from = last && Date.now() - last.t < 4000 ? last : null;
    last = null;
    // 结果由各卡架组件各自重绘，GameScreen 的渲染后回调可能先于卡架到达；等这一轮重绘都落到 DOM 再找去向卡
    setTimeout(function () { play(owner, itemId, from); }, 30);
  }
  function play(owner, itemId, from) {
    var zone = document.querySelector('[data-drop="zone:' + owner + '"]');
    // 堆叠卡 data-card 为「owner|itemId」，实例卡为「owner|itemId|实例号」
    var to = zone ? zone.querySelector('[data-card="' + owner + '|' + itemId + '"], [data-card^="' + owner + '|' + itemId + '|"]') : null;
    playTo(to, from);
  }
  // B33（Q435、Q453-7）：整备的更换、交接、卸下与右栏快捷卸下——提交前由页面记下起点（mark），成功后播起点→去向一次；
  // 同 Q367：只有一端可见时亮可见的那端，两端都看不见只留原结果提示，减少动态效果时只静态描一次框
  var marked = null;
  function mark(sel) {
    var el = sel ? document.querySelector(sel) : null;
    if (!el) { marked = null; return; }
    var art = el.querySelector('.eq-ico svg, .eq-ico img, svg, img');
    marked = { el: el, rect: el.getBoundingClientRect(), art: art ? art.outerHTML : '', t: Date.now() };
  }
  function fly(toSel) {
    var from = marked && Date.now() - marked.t < 4000 ? marked : null;
    marked = null;
    setTimeout(function () { playTo(toSel ? document.querySelector(toSel) : null, from); }, 30);
  }
  function playTo(to, from) {
    var tr = to ? to.getBoundingClientRect() : null;
    var fr = from ? from.rect : null;
    var toOk = visible(tr), fromOk = visible(fr);
    if (!toOk) {
      if (from && from.el && from.el.isConnected && visible(from.el.getBoundingClientRect())) lit(from.el);
      return;
    }
    if (!fromOk || reduced() || !document.body.animate) { lit(to); return; }
    var g = document.createElement('div');
    g.className = 'fx-swap';
    g.innerHTML = from.art;
    var w = Math.min(64, fr.width), h = Math.min(64, fr.height);
    g.style.width = w + 'px'; g.style.height = h + 'px';
    g.style.left = (fr.left + fr.width / 2 - w / 2) + 'px'; g.style.top = (fr.top + fr.height / 2 - h / 2) + 'px';
    document.body.appendChild(g);
    var dx = (tr.left + tr.width / 2) - (fr.left + fr.width / 2), dy = (tr.top + tr.height / 2) - (fr.top + fr.height / 2);
    var a = g.animate([{ transform: 'translate(0,0)', opacity: 0.95 }, { transform: 'translate(' + dx + 'px,' + dy + 'px)', opacity: 0.6 }],
      { duration: 220 * k(), easing: 'cubic-bezier(.2,.7,.3,1)' });
    a.onfinish = a.oncancel = function () { g.remove(); lit(to); };
  }
  // Q370：所在地点或场景实际切换（serial 变化）时，旧地景 140ms 淡出、新地景随后 180ms 淡入；同一 serial 只记下当前地景
  function sceneSync(serial) {
    var art = document.querySelector('.map-card .mc-art');
    if (!art) return;
    var cur = { serial: serial, bg: art.style.backgroundImage, filter: art.style.filter };
    var prev = scene;
    scene = cur;
    if (!prev || prev.serial === serial || reduced()) return;
    // 旧景影子挂在 body 上、按地景卡位置固定（不插进 Blazor 管理的节点），不接收输入
    var r = art.getBoundingClientRect();
    if (!visible(r)) return;
    var old = document.createElement('div');
    old.className = 'fx-scene-old';
    old.style.backgroundImage = prev.bg;
    old.style.filter = prev.filter;
    old.style.left = r.left + 'px'; old.style.top = r.top + 'px'; old.style.width = r.width + 'px'; old.style.height = r.height + 'px';
    old.setAttribute('aria-hidden', 'true');
    document.body.appendChild(old);
    var out = 140 * k(), inn = 180 * k();
    if (old.animate) {
      old.animate([{ opacity: 1 }, { opacity: 0 }], { duration: out, easing: 'ease-in', fill: 'forwards' }).onfinish = function () { old.remove(); };
      art.animate([{ opacity: 0 }, { opacity: 0, offset: out / (out + inn) }, { opacity: 1 }], { duration: out + inn, easing: 'ease-out' });
    } else old.remove();
  }
  return { note: note, swap: swap, lit: lit, sceneSync: sceneSync, mark: mark, fly: fly };
})();
