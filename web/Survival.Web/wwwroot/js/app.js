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
  // B46 试玩修复：完整暂停弹窗打开时滚回顶部（标题、说明可见）；渲染后与下一帧、50ms 后各补一次，压过打开时焦点等带来的滚动
  pauseTop: function () {
    var go = function () { var g = document.querySelector('.pause-scrim > .pause-grid'); if (g && g.scrollTop > 0) g.scrollTop = 0; };
    go();
    requestAnimationFrame(go);
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
    window.__survivalDlgPrev = a && a !== document.body && !(a.closest && a.closest('.dlg-layer')) ? a : null;
    if (a && a.blur && !(a.closest && a.closest('.dlg-layer'))) a.blur();
  },
  // B45b（Q585 首段）：引导对白读完、回到原火堆加工菜单时恢复焦点——对白前的焦点仍在该菜单里且可用就回它，
  // 否则进菜单第一个可用控件（FACPROC 第 4 条「焦点进入菜单标题／首个操作」）。只聚焦、不执行；菜单已不在就不动。
  dialogueEnd: function (hostSel) {
    var prev = window.__survivalDlgPrev;
    window.__survivalDlgPrev = null;
    var tries = 0;
    function ok(c) { return c && c.isConnected && !c.disabled && !(c.closest && c.closest('[inert]')) && (c.offsetWidth || c.offsetHeight); }
    function go() {
      var host = document.querySelector(hostSel);
      if (!host) return;
      if (host.closest && host.closest('[inert]')) { if (++tries < 20) requestAnimationFrame(go); return; }
      var ae = document.activeElement;
      if (ae && ae !== document.body && host.contains(ae)) return;
      if (prev && host.contains(prev) && ok(prev)) {
        try { prev.focus({ preventScroll: true }); } catch (e) { }
        if (document.activeElement === prev) return;
      }
      var title = host.querySelector('#fm-title');
      var list = [title].concat(Array.prototype.slice.call(host.querySelectorAll('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])')));
      for (var i = 0; i < list.length; i++) {
        if (!ok(list[i])) continue;
        try { list[i].focus({ preventScroll: true }); } catch (e) { }
        if (document.activeElement === list[i]) return;
      }
    }
    setTimeout(go, 0);
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
  // B43（S14 2026-11-12「按键路由与防止重复执行」）：键盘快捷键的统一路由。顺序：输入法组合 → 反馈窗 → 游戏菜单 → 对白层 → 局部唯一确认（Enter）
  // → 就近浮窗 → 数量框 → 输入控件 → 组合键 → 全屏战斗 → 模态层（数量框、强制整理、确认窗、居中局部盘）→ 地图局部键 → 顶栏字母 → Esc。
  // 规则：最上层先处理自己的键；输入框、下拉、可编辑文字区与输入法组合中普通字母不生效；Ctrl/Alt/Meta 组合不当单键；
  // 已聚焦的按钮上 Enter/Space 走原生语义；按住键的重复 keydown 不重复确认、推进对白、开关顶栏（地图缩放、战斗目标方向键的连续调节沿原规则）。
  TOP_LETTERS: ['M', 'C', 'R', 'L', 'P', 'G', 'K', 'J', 'T', 'D', 'H'],
  hookKeys: function (dotnet) {
    if (window.__survivalKeys) document.removeEventListener('keydown', window.__survivalKeys);
    if (window.__survivalKeyGuard) window.removeEventListener('keydown', window.__survivalKeyGuard, true);
    var TOP = window.survival.TOP_LETTERS;
    function editable(el) {
      if (!el || !el.tagName) return false;
      var tg = el.tagName;
      if (tg === 'TEXTAREA' || tg === 'SELECT' || el.isContentEditable) return true;
      if (tg !== 'INPUT') return false;
      var ty = (el.type || 'text').toLowerCase();
      return ['button', 'submit', 'reset', 'checkbox', 'radio', 'range', 'color', 'file', 'image'].indexOf(ty) < 0;
    }
    // 已聚焦的具体控件（按钮、链接、可点卡、单选、页签、复选框等）：Enter/Space 交给它自己的原生语义
    function control(el) {
      if (!el || el === document.body || !el.closest) return false;
      if (editable(el)) return false;
      return !!el.closest('button, a[href], [role=button], [role=radio], [role=tab], [role=checkbox], [role=option], input, summary');
    }
    function shown(el) { return !!(el && (el.offsetWidth || el.offsetHeight)); }
    function focusVisible(el) { try { return !!el.matches(':focus-visible'); } catch (e) { return false; } }
    // 当前最上层的模态层（数量框、强制整理与确认窗、居中局部盘）；没有时为 null
    function topModal() {
      var sels = ['.modal-back', '.overflow-layer', '.disk-scrim'];
      for (var i = 0; i < sels.length; i++) {
        var m = document.querySelector(sels[i]);
        if (m) return m;
      }
      return null;
    }
    // 局部唯一主确认（S14「局部盘唯一主确认」「物品转移条唯一提交按钮」）：焦点所在的活动盘／工作区（data-enter-scope），
    // 焦点不在任何控件上（如点过盘内空白、盘刚打开）时取最上层模态里可见的那一个；该区内只认恰好一个可用的 [data-enter]。
    function enterScope(ae) {
      if (ae && ae !== document.body && ae !== document.documentElement && ae.closest) {
        var s = ae.closest('[data-enter-scope]');
        if (s) return s;
        // 焦点在别处（背后四轨、顶栏等）：不从那里抢走按键
        if (!ae.closest('.apop, .modal, .overflow-layer, .sub-panel')) return null;
      }
      var hosts = ['.modal', '.overflow-layer', '.apop-c'];
      for (var i = 0; i < hosts.length; i++) {
        var list = document.querySelectorAll(hosts[i]);
        for (var j = list.length - 1; j >= 0; j--) {
          if (!shown(list[j]) || !list[j].matches('[data-enter-scope]')) continue;
          return list[j];
        }
      }
      // B43 试玩修复：没有居中盘／确认窗时，打开着的二级面板就是活动工作区（按 R 打开休息、进整备比较后焦点在 body 也能回车）
      return panelScope();
    }
    // 最上层打开着的二级面板（.sub-panel，不在 inert 里）
    function topPanel() {
      var ps = Array.prototype.filter.call(document.querySelectorAll('.sub-panel'), function (p) { return shown(p) && !(p.closest && p.closest('[inert]')); });
      return ps.length ? ps[ps.length - 1] : null;
    }
    // 该面板的确认区：面板本身是区就取它；否则取面板里唯一一个顶层的区（如整备里的比较／交接工作区）
    function panelScope() {
      var p = topPanel();
      if (!p) return null;
      if (p.matches('[data-enter-scope]')) return p;
      var top = Array.prototype.filter.call(p.querySelectorAll('[data-enter-scope]'), function (x) {
        var up = x.parentElement && x.parentElement.closest('[data-enter-scope]');
        return shown(x) && !(up && p.contains(up));
      });
      return top.length === 1 ? top[0] : null;
    }
    // 该区自己的 [data-enter]（不算嵌套区里的）；自己一个都没有时，若只有一个嵌套区（如选卡盘里的转移条）就看那一个
    function uniqueEnter(scope) {
      var own = Array.prototype.filter.call(scope.querySelectorAll('[data-enter]'), function (b) {
        return b.closest('[data-enter-scope]') === scope && shown(b);
      });
      if (!own.length) {
        var inner = Array.prototype.filter.call(scope.querySelectorAll('[data-enter-scope]'), function (x) {
          return shown(x) && x.parentElement && x.parentElement.closest('[data-enter-scope]') === scope;
        });
        return inner.length === 1 ? uniqueEnter(inner[0]) : null;
      }
      return own.length === 1 && !own[0].disabled ? own[0] : null;
    }
    // 按住 Enter／Space 的重复 keydown：一律不再触发（浏览器原生按钮激活、卡片的回车选择、对白继续、局部确认都不重复）。
    // 捕获阶段最先执行，连同组件自己的按键处理一起挡住；可编辑文字区里的空格照常。
    window.__survivalKeyGuard = function (ev) {
      if (!ev.repeat) return;
      if (ev.key === 'Enter' || (ev.key === ' ' && !editable(ev.target))) {
        if (window.FeedbackWidget && FeedbackWidget.isOpen()) return;
        ev.preventDefault();
        ev.stopImmediatePropagation();
      }
    };
    window.addEventListener('keydown', window.__survivalKeyGuard, true);
    // B43 试玩修复：鼠标点确认区／二级面板里的按钮与可点卡、顶栏按钮、战斗目标切换按钮时不把焦点留在被点的按钮上
    // （否则接着按 Enter 会按原生语义再点一次它，而不是该区的唯一确认）。焦点交给所在确认区（tabindex=-1 的容器）；
    // 区里正在输入的数量框保持焦点；不在区里时把留在面板外的焦点放掉。键盘 Tab 聚焦与按钮原生 Enter/Space 不受影响。
    function parkTarget(el) {
      if (!el || !el.closest || editable(el)) return null;
      var b = el.closest('button, [role=button], [role=tab], [role=radio], [role=option]');
      if (!b || b.matches('input, select, textarea') || (b.closest('[data-dlg-next]'))) return null;
      if (b.closest('.top .nav, .bp-cycle')) return { b: b, host: null, scope: null };
      var scope = b.closest('[data-enter-scope]');
      var host = scope || b.closest('.sub-panel');
      return host ? { b: b, host: host, scope: scope } : null;
    }
    function park(t) {
      var ae = document.activeElement;
      if (t.scope) {
        if (ae && t.scope.contains(ae) && (editable(ae) || ae === t.scope)) return;
        try { t.scope.focus({ preventScroll: true }); } catch (e) { }
      } else if (ae && ae !== document.body && (!t.host || !t.host.contains(ae) || ae === t.b)) {
        ae.blur();
      }
    }
    if (window.__survivalMouseDown) document.removeEventListener('mousedown', window.__survivalMouseDown, true);
    if (window.__survivalMouseClick) document.removeEventListener('click', window.__survivalMouseClick, true);
    window.__survivalMouseDown = function (ev) {
      if (ev.button !== 0) return;
      var t = parkTarget(ev.target);
      if (!t) return;
      ev.preventDefault();   // 不让按钮拿到焦点（click 照常触发）
      park(t);
    };
    // 兜底：若浏览器仍把焦点给了按钮（或点击后重渲染把焦点落到别处），鼠标点击后再归位一次
    window.__survivalMouseClick = function (ev) {
      if (!ev.detail) return;   // 键盘回车／空格触发的 click（detail 为 0）不动焦点
      var t = parkTarget(ev.target);
      if (t && document.activeElement === t.b) park(t);
    };
    document.addEventListener('mousedown', window.__survivalMouseDown, true);
    document.addEventListener('click', window.__survivalMouseClick, true);
    window.__survivalKeys = function (ev) {
      var tgt = ev.target, t = tgt && tgt.tagName, k = ev.key;
      // 输入法组合输入中：一律不处理
      if (ev.isComposing || ev.keyCode === 229) return;
      var mod = ev.ctrlKey || ev.altKey || ev.metaKey;
      var send = function (key) { ev.preventDefault(); dotnet.invokeMethodAsync('OnKey', key); };
      // 反馈与留言窗口开着：按键归窗口自己（它的 Esc 只关窗口，关掉后回到游戏菜单，Q322）
      if (window.FeedbackWidget && FeedbackWidget.isOpen()) return;
      // B31b（Q322）：游戏菜单／战斗菜单打开时，只有 Esc 交给游戏（收起菜单）；回车、空格、数字留给菜单里的按钮，不落到背后的操作条、对白或战斗指令
      if (document.querySelector('.menu-modal')) {
        if (k === 'Escape' && !mod) send('Escape');
        return;
      }
      // B34b（S14 Q572）：战斗特写播放层在最上层时，ESC 打开菜单，其余键不穿透到背后的战斗、对白或结算；
      // BM7a（S14 Q633）：敌我出手都不可跳过——Enter／Space 照样被这里接住（交给 C# 的 'Skip' 对特写与收束停留什么也不做），不跳、不点穿、不排队；
      // 只读子页盖在播放层上面时（.under）按键归子页，照原规则处理
      // BM5c（S14 Q605）：探索遭遇短提示同一套按键（Enter／Space 只跳过提示，ESC 打开菜单）
      var fxl = document.querySelector('.bfx-layer[data-active], .bfx-hold[data-active], .enc-prompt[data-active]');
      if (fxl && !fxl.classList.contains('under')) {
        if (mod) return;
        if (k === 'Escape') { send('Escape'); return; }
        if (k === 'Enter' || k === ' ') { ev.preventDefault(); if (!ev.repeat) dotnet.invokeMethodAsync('OnKey', 'Skip'); return; }
        if (k !== 'Tab') ev.preventDefault();
        return;
      }
      // 人物对话读完锁（Q10、S14 §10.1）：数字、字母一律不转给游戏；Esc 不结束对白，交给游戏打开当前情境的菜单（B31b，Q322）。
      // B43：唯一无分支的「继续／结束对话」（data-dlg-next）在焦点不在具体控件上时收 Enter，一次只确认当前行一次；招募等分支不由 Enter 代选。
      if (document.querySelector('.dlg-layer')) {
        if (mod) return;
        if (k === 'Escape') { send('Escape'); return; }
        if (k === 'Enter' && !control(document.activeElement)) {
          var nx = document.querySelector('.dlg-layer [data-dlg-next]');
          if (nx && !nx.disabled && !(nx.closest && nx.closest('[inert]'))) { ev.preventDefault(); nx.click(); }
        }
        return;
      }
      // BM5b1（S14 Q602）：中央加工菜单是当前最高输入层时（由 C# 判定并在菜单根写 data-tab-trap＝安全落点 id，JS 不自己猜层级），
      // Tab／Shift+Tab 只在该层当前可见、可用、可聚焦的控件间按文档顺序首尾循环，跳过禁用、隐藏与 inert；不点按钮、不换来源。
      // 一时没有可聚焦控件时留在菜单标题（安全位置）。菜单、对白、反馈窗等更高层已在上面先拦下，被盖住时 C# 不写该属性、这里不接管。
      if (k === 'Tab' && !mod) {
        var trap = document.querySelector('[data-tab-trap]');
        if (trap && shown(trap) && !(trap.closest && trap.closest('[inert]'))) {
          var items = Array.prototype.filter.call(trap.querySelectorAll('button, a[href], input, select, textarea, [tabindex]'), function (el) {
            return !el.disabled && el.tabIndex >= 0 && shown(el) && !(el.closest && el.closest('[inert], [hidden]'));
          });
          ev.preventDefault();
          if (!items.length) {
            var safe = document.getElementById(trap.getAttribute('data-tab-trap'));
            if (safe) safe.focus();
            return;
          }
          var at = items.indexOf(document.activeElement);
          var nx2 = at < 0 ? (ev.shiftKey ? items.length - 1 : 0) : (at + (ev.shiftKey ? -1 : 1) + items.length) % items.length;
          items[nx2].focus();
          return;
        }
      }
      // B46 试玩修复（Q535-A04）：单段 5 分钟行动的展示等待中（中央条 data-single-show），空格就是「跳过动画」——焦点在卡片等控件上或
      // 刚收起的操作盘还在淡出时也一样，不重开操作盘；焦点就在跳过按钮上时交给原生（不重复），输入框里不处理（上面菜单／对白已先拦）
      if (k === ' ' && !mod && !editable(tgt)) {
        var ss = document.querySelector('.cprog[data-single-show]:not(.fading) [data-skip-anim]');
        if (ss && !ss.disabled && !(ss.closest && ss.closest('[inert]')) && document.activeElement !== ss && !topModal()) {
          ev.preventDefault();
          ss.click();
          return;
        }
      }
      // 局部唯一主确认（B43）：数量框、转移条、整备比较／交接、就近盘、强制整理与确认窗、二级面板……
      // 焦点停在被打开着的二级面板盖住的背后控件上（如先点过地面卡再按 R）时，当作焦点不在任何控件上，归该面板
      var fae = document.activeElement, pnl = topPanel();
      // B44（Q492）：右栏不被二级面板盖住，键盘 Tab 聚焦在右栏控件（人物页名称与行内按钮）上时回车只作用该控件，不穿透到面板的唯一确认
      if (pnl && fae && fae !== document.body && control(fae) && !pnl.contains(fae) && !fae.closest('.top, .apop, .modal, .overflow-layer')
          && !(fae.closest('[data-detail-rail]') && (focusVisible(fae) || fae.hasAttribute('data-peek-name')))) fae = null;
      if (k === 'Enter' && !mod && !control(fae)) {
        var sc = enterScope(fae);
        if (sc) {
          var go = uniqueEnter(sc);
          if (go) { ev.preventDefault(); go.click(); return; }
        }
      }
      // 就近操作浮窗（S14 §14.1）里的按钮：回车/空格/数字只作用于浮窗自身，不转给卡下操作条；Esc 照常交给 Esc 栈（先收起浮窗）
      if (tgt && tgt.closest && tgt.closest('.apop') && k !== 'Escape' && !tgt.closest('.tbar')) return;
      var inBar = tgt && tgt.closest && tgt.closest('.tbar');
      // 数量框等弹窗自己处理 Esc（取消）；回车已由上面的局部确认处理；其余键不触发游戏快捷键
      if (tgt && tgt.closest && tgt.closest('.modal')) return;
      if (editable(tgt)) {
        // 输入框、数字框、下拉与可编辑文字区：普通字母、数字导航不生效（S14）；
        // Esc 在转移条数量框里先只退出输入框（Esc 栈第一级），再按一次才交给 GameScreen 清选中（复验新发现 1）；其他输入框的 Esc 交给 Esc 栈
        if (k !== 'Escape' || mod) return;
        if (inBar) { ev.preventDefault(); tgt.blur(); return; }
      }
      if (mod) return;
      // BM2（Q502）：中央行动进度条「跳过动画 空格」。只在跳过按钮真实可见、可用、所在行动层可受理输入时，点同一按钮一次（与鼠标点击同效）；
      // 编辑／输入法（上面已挡）、菜单、对白、模态盘、前景面板与浮窗、拖拽中不穿透；焦点在其他具体控件或就在该按钮上时按原生语义（不重复）；
      // 按住的重复 keydown 已由捕获阶段的 __survivalKeyGuard 挡掉。处理时阻止本次空格滚动页面。没有可用的跳过按钮时空格不做任何事。
      if (k === ' ') {
        var sk = document.querySelector('.cprog:not(.fading) [data-skip-anim]');
        if (!sk || sk.disabled || !shown(sk) || (sk.closest && sk.closest('[inert]'))) return;
        var sae = document.activeElement;
        if (sae === sk || control(sae)) return;
        if (topModal() || topPanel() || document.querySelector('.bfs') || document.body.classList.contains('dragging')) return;
        // B46 试玩修复：正在淡出的旧盘（.pop-ghosts 里的残影）不算开着
        if (Array.prototype.some.call(document.querySelectorAll('.apop, .st-pop.pinned'), function (el) { return shown(el) && !(el.closest && el.closest('.pop-ghosts')); })) return;
        ev.preventDefault();
        sk.click();
        return;
      }
      // 全屏战斗（A10 §6.4）：1—5 只选本页技能位、Q/E 切武器Ⅰ/Ⅱ页（0AP）、←/→ 在合法目标间移动预测、回车提交当前预测的目标；
      // 焦点在按钮或可点锚点上时回车/空格交给它自己（浏览器原生），不重复提交
      if (document.querySelector('.bfs')) {
        var onCtl = document.activeElement && document.activeElement.closest && document.activeElement.closest('button, [role=button]');
        // 试玩修复 B9-7：刚点过的技能/指令按钮仍带焦点时，已有待确认指令或正在瞄准，回车是“提交”而不是再点一次该按钮（会把选择收起）
        if (k === 'Enter' && onCtl && onCtl.closest('.bfs-console') && document.querySelector('#bt-confirm, .bfs .anc.targetable')) onCtl = null;
        if (/^[1-5]$/.test(k) || k === 'q' || k === 'Q' || k === 'e' || k === 'E' || k === 'ArrowLeft' || k === 'ArrowRight' || (k === 'Enter' && !onCtl)) {
          ev.preventDefault();
          // 方向键沿原规则可连续移动预测；技能位、切页与回车按住不重复
          if (ev.repeat && k !== 'ArrowLeft' && k !== 'ArrowRight') return;
          send(k.length === 1 ? k.toUpperCase() : k);
          return;
        }
      }
      var modal = topModal();
      var mapPanel = document.querySelector('.map-panel');
      var inMap = !!(mapPanel && tgt && tgt.closest && tgt.closest('.map-panel'));
      // 选中物品卡后直接按数字：跳进选卡操作盘里转移条的数量框（数量框、强制整理、确认窗开着时不跳到背后；焦点在地图里时数字归地图）
      if (/^[0-9]$/.test(k) && !inMap && (!modal || modal.classList.contains('disk-scrim'))) {
        // B45（Q562）：中央加工菜单通用来源的「数量」框（data-qty-jump，可编辑时）优先；焦点本已在框里时数字照常编辑、不再全选
        var okInp = function (x) { return shown(x) && !x.disabled && !(x.closest && x.closest('[inert]')); };
        var inp = Array.prototype.filter.call(document.querySelectorAll('input[data-qty-jump]'), okInp)[0]
          || Array.prototype.filter.call(document.querySelectorAll('.tbar input'), okInp)[0];
        if (inp && inp === document.activeElement) return;
        if (inp) { inp.focus(); inp.select(); return; }
      }
      // 当前最上层是模态层（数量框、强制整理、确认窗、居中局部盘）：只有 Esc 交给 Esc 栈（先收这一层），字母、数字不触发背后的顶栏或转移条
      if (modal) {
        if (k === 'Escape') {
          ev.preventDefault();
          if (document.activeElement && document.activeElement.tagName === 'BUTTON') document.activeElement.blur();
          dotnet.invokeMethodAsync('OnKey', 'Escape');
        }
        return;
      }
      // 地图局部键（S14「地图放大／缩小」「地图定位」）：区域地图开着时 +/= 放大、-/_ 缩小（按住连续）、0 回到当前位置；
      // 与地图上同名按钮同一入口（按钮禁用时键也无效）。转移条数量框开着且焦点不在地图里时，数字留给数量框（地图局部键不抢数量输入）。
      if (mapPanel && ['+', '=', '-', '_', '0'].indexOf(k) >= 0) {
        var which = k === '+' || k === '=' ? 'in' : k === '0' ? 'here' : 'out';
        var mb = mapPanel.querySelector('[data-map-cmd="' + which + '"]');
        ev.preventDefault();
        if (mb && !mb.disabled && !(ev.repeat && which === 'here')) mb.click();
        return;
      }
      // 顶栏字母（B43：M C R L P G K J T D H，不区分大小写、不要求 Shift）：按一次触发同一按钮一次，按住不重复开关；F 不直达反馈（B31b Q322）
      var up = k && k.length === 1 ? k.toUpperCase() : k;
      if (TOP.indexOf(up) >= 0) {
        ev.preventDefault();
        if (!ev.repeat) dotnet.invokeMethodAsync('OnKey', up);
        return;
      }
      if (k === 'Escape') {
        ev.preventDefault();
        // Esc 收起预览/选中后，焦点仍留在刚点过的按钮上会被画成键盘焦点框，看起来像“仍选中”（试玩修复 L6）
        if (document.activeElement && document.activeElement.tagName === 'BUTTON') document.activeElement.blur();
        dotnet.invokeMethodAsync('OnKey', 'Escape');
      }
    };
    document.addEventListener('keydown', window.__survivalKeys);
  }
};

// BM4a（S14 Q590「每层记住自己的……滚动和发起焦点」）：中部一级面板按面板种类（.panel-host[data-panel]）随时记下面板内各滚动区的位置与最后获得焦点的控件；
// 该面板被顶栏面板盖住后再恢复时（.NET 渲染后调 restore）取回；控件已失效或不可用则落到该层标题。新开的面板先 forget，不沿用上一次的记忆。
// 只动滚动与焦点，不点击、不确认任何东西。
window.survivalPanels = (function () {
  var mem = {};
  var pending = false;
  function host() { return document.querySelector('.panel-host[data-panel]'); }
  function keyOf(h) { return h ? h.getAttribute('data-panel') || '' : ''; }
  function slot(k) { return mem[k] || (mem[k] = { scroll: [], focus: null }); }
  function snapScroll() {
    pending = false;
    var h = host(), k = keyOf(h);
    if (!k) return;
    var all = h.querySelectorAll('*'), list = [];
    for (var i = 0; i < all.length; i++) {
      var el = all[i];
      if (el.scrollTop > 0 || el.scrollLeft > 0) list.push([i, el.scrollTop, el.scrollLeft]);
    }
    slot(k).scroll = list;
  }
  document.addEventListener('scroll', function (ev) {
    var t = ev.target;
    if (!t || !t.closest || !t.closest('.panel-host[data-panel]') || pending) return;
    pending = true;
    requestAnimationFrame(snapScroll);
  }, true);
  document.addEventListener('focusin', function (ev) {
    var h = host(), k = keyOf(h), t = ev.target;
    if (!k || !t || !h.contains(t)) return;
    slot(k).focus = t.id ? { id: t.id } : { idx: Array.prototype.indexOf.call(h.querySelectorAll('*'), t) };
  }, true);
  function usable(el) {
    return !!(el && !el.disabled && !(el.closest && el.closest('[inert]')) && (el.offsetWidth || el.offsetHeight));
  }
  function focusEl(el) { try { el.focus({ preventScroll: true }); } catch (e) { try { el.focus(); } catch (e2) { } } }
  function restore(k) {
    var tries = 0;
    function go() {
      var h = host();
      if (!h || keyOf(h) !== k) { if (++tries < 20) requestAnimationFrame(go); return; }
      var m = mem[k] || { scroll: [], focus: null };
      var all = h.querySelectorAll('*');
      m.scroll.forEach(function (x) { var el = all[x[0]]; if (el) { el.scrollTop = x[1]; el.scrollLeft = x[2]; } });
      var f = m.focus, el = null;
      if (f) el = f.id ? document.getElementById(f.id) : all[f.idx];
      if (el && h.contains(el) && usable(el)) { focusEl(el); if (document.activeElement === el) return; }
      // 原焦点已失效：回该层安全标题（不自动确认任何动作）
      var title = h.querySelector('#fm-title, h2, h3');
      if (title) {
        if (!title.hasAttribute('tabindex')) title.setAttribute('tabindex', '-1');
        focusEl(title);
      }
    }
    requestAnimationFrame(go);
  }
  function forget(k) { delete mem[k]; }
  return { restore: restore, forget: forget };
})();

// 人物对话层的对话流（S14 §10.3）：停在底部时新句追加后跟随到最新；向上回看时保持阅读位置，亮出不含句数的“回到最新”。
// 只改滚动位置，不推进对白；“回到最新”按钮所在的容器类名由这里切换（Blazor 不管理这个类）。
window.survivalDlg = (function () {
  // B39（N02 附录 §1、DLG-A06，B26b/B38 遗留“1280×720 长句顶部被裁”）：新句出现时按句首锚定——新句整段放得下就停在底部，
  // 放不下就让它的第一行贴住可视区上沿（不再滚到底把句首裁掉）。只滚动本流，不推进对白。
  // BM6（S14 Q627 选 A，沿 Q614）：「回到最新」显隐只看本流真实滚动底部（沿原 8px 容差）——正文没到底就显示，到底或无可滚动内容就隐藏；
  // 与“是否在跟读”（follow）分开：程序按新句句首锚定不算用户回看（锚定后仍跟读，但长句下方还有内容时按钮照样显示）；
  // 用户真往上翻过当前句句首才退出跟读，此后新句进入不抢位置；翻回句首以下或点「回到最新」恢复跟读。
  // 点「回到最新」滚到已显示正文最底并隐藏按钮，不推进台词、不提交选项。窗口缩放、字体或头像布局改变后按同一滚动容器重验。
  function latestHost(el) {
    var layer = el && el.closest ? el.closest('.dlg-layer') : null;
    return layer ? layer.querySelector('.dlg-latest-host') : null;
  }
  function showLatest(el, on) {
    var h = latestHost(el);
    if (h) h.classList.toggle('show', !!on);
  }
  function bottom(el) { return el.scrollTop + el.clientHeight >= el.scrollHeight - 8; }
  // 按钮显隐：只看真实底部
  function refresh(el) { showLatest(el, !bottom(el)); }
  // 锚点句：最近一次 sync 时的当前句（新句已渲染、sync 还没跑的间隙里发生的滚动不拿新句判断，免得误判成回看）
  function nowEl(el) {
    var n = el.__dlg && el.__dlg.now;
    return n && n.isConnected ? n : el.querySelector('.now');
  }
  function nowTop(el) {
    var n = nowEl(el);
    if (!n) return null;
    return el.scrollTop + n.getBoundingClientRect().top - el.getBoundingClientRect().top;
  }
  function anchorTop(el) {
    var max = Math.max(0, el.scrollHeight - el.clientHeight);
    var t = nowTop(el);
    return t === null ? max : Math.min(max, Math.max(0, t - 6));
  }
  // 程序滚动：记下目标位置，随后到来的那次 scroll 事件不当作用户操作（不改跟读状态）
  function setTop(el, top) {
    var st = el.__dlg, before = el.scrollTop;
    el.scrollTop = top;
    if (st) st.prog = el.scrollTop !== before ? el.scrollTop : null;
  }
  function anchor(el) { setTop(el, anchorTop(el)); if (el.__dlg) el.__dlg.anchored = true; }
  // 跟读判定（只在用户滚动时用）：在底部，或不高于当前句句首锚点
  function following(el) { return bottom(el) || el.scrollTop >= anchorTop(el) - 8; }
  function init(el) {
    if (el.__dlg) return el.__dlg;
    var st = el.__dlg = { count: 0, follow: true, prog: null, anchored: false };
    el.addEventListener('scroll', function () {
      if (st.prog !== null && Math.abs(el.scrollTop - st.prog) <= 2) st.prog = null;   // 程序锚定／回到最新引起的滚动
      else { st.prog = null; st.anchored = false; st.follow = following(el); }
      refresh(el);
    });
    // 缩放、字体、头像布局改变：仍停在程序锚点（没手动滚过）时按当前句句首重锚，其余保持位置；按钮按真实底部重验
    if (window.ResizeObserver) {
      st.ro = new ResizeObserver(function () {
        if (!el.isConnected) { st.ro.disconnect(); return; }
        if (st.follow && st.anchored) anchor(el);
        refresh(el);
      });
      st.ro.observe(el);
    }
    return st;
  }
  function watchLines(el, st) {
    if (!st.ro) return;
    for (var c = el.firstElementChild; c; c = c.nextElementSibling) {
      if (c.__dlgRo) continue;
      c.__dlgRo = true;
      st.ro.observe(c);
    }
  }
  return {
    sync: function (el, count) {
      if (!el) return;
      var st = init(el);
      st.now = el.querySelector('.now');
      watchLines(el, st);
      // 新的一次对话（句数变少）或首次出现：从这一段的句首开始
      if (count <= st.count || st.count === 0) { st.count = count; st.follow = true; anchor(el); refresh(el); return; }
      st.count = count;
      if (st.follow) anchor(el);   // 跟读：新句句首锚定（不算回看）
      refresh(el);                 // 回看：不抢位置；两种情况按钮都只看是否到底
    },
    latest: function (el) {
      if (!el) return;
      var st = init(el);
      st.now = el.querySelector('.now');
      setTop(el, Math.max(0, el.scrollHeight - el.clientHeight));
      st.follow = true;
      st.anchored = false;
      refresh(el);
    }
  };
})();

// BM6（S14 Q625／Q628，沿 Q612）：对白聚焦——整屏对白以外的背景一次性压暗（黑 0.48）＋模糊（2px），对白与白名单按钮保持清晰。
// 只有一层遮罩（.dlg-focus，主界面一层、全屏战斗内一层，同时只亮一层）：backdrop-filter 只作用于遮罩背后，clip-path 按白名单元素的
// 实际位置挖洞——洞里露出的对白、顶栏「菜单」、底栏「重试」「在此页面继续保存」、结果提示 × 不被压暗或模糊，也照原规则可点；
// 其余位置都落在遮罩上，从入场到退场完成都点不穿。白名单选择器写在遮罩的 data-keep 上（由 C# 按情境给出），不新开任何权限：
// 原本禁用的按钮仍禁用，不存在的提示不画按钮。更高层菜单、确认、播放层在遮罩之上，不被当作背景。
// 入场 180ms／退场 140ms × 动画速度（CSS）；入场期间第一下点击或 Enter／Space 只完成入场表现、不推进台词（显式菜单／存档按钮照常各自消费）；
// 菜单打开时冻结入场表现，关闭后续原进度。系统减少动态效果时 CSS 不做渐变，静态压暗与模糊照常。
window.survivalFocus = (function () {
  var raf = 0, timer = 0, paused = [];
  function scrims() { return document.querySelectorAll('.dlg-focus'); }
  function active(s) {
    if (s.classList.contains('on')) return true;
    try { return getComputedStyle(s).visibility !== 'hidden'; } catch (e) { return false; }
  }
  function holes(s) {
    var sel = s.getAttribute('data-keep');
    if (!sel) return [];
    var list;
    try { list = document.querySelectorAll(sel); } catch (e) { return []; }
    var out = [];
    for (var i = 0; i < list.length; i++) {
      var r = list[i].getBoundingClientRect();
      if (r.width < 1 || r.height < 1) continue;
      var pad = list[i].classList.contains('dlg-layer') ? 0 : 2;   // 按钮留出焦点框
      out.push([r.left - pad, r.top - pad, r.right + pad, r.bottom + pad]);
    }
    return out;
  }
  function n(v) { return Math.round(v * 2) / 2; }
  function pathOf(s, hs) {
    if (!hs.length) return '';
    var b = s.getBoundingClientRect();
    var d = 'M0 0H' + n(b.width) + 'V' + n(b.height) + 'H0Z';
    for (var i = 0; i < hs.length; i++) {
      var h = hs[i];
      d += 'M' + n(h[0] - b.left) + ' ' + n(h[1] - b.top) + 'H' + n(h[2] - b.left) + 'V' + n(h[3] - b.top) + 'H' + n(h[0] - b.left) + 'Z';
    }
    return "path(evenodd, '" + d + "')";
  }
  function entering(layer) {
    if (!layer || !layer.getAnimations) return [];
    return layer.getAnimations().filter(function (a) { return a.animationName === 'dlg-in' && a.playState !== 'finished'; });
  }
  function liveLayer() { return document.querySelector('.dlg-layer:not(.dlg-ghost)'); }
  function scrimAnims() {
    var out = [];
    var ss = scrims();
    for (var i = 0; i < ss.length; i++) if (ss[i].getAnimations) out = out.concat(ss[i].getAnimations());
    return out;
  }
  // 菜单打开：冻结入场表现；关闭：从原进度续（不重播）
  function freeze() {
    var menu = !!document.querySelector('.menu-scrim');
    if (menu) {
      var list = entering(liveLayer()).concat(scrimAnims());
      for (var i = 0; i < list.length; i++) {
        if (list[i].playState === 'running') { try { list[i].pause(); paused.push(list[i]); } catch (e) { } }
      }
    } else if (paused.length) {
      var p = paused; paused = [];
      for (var j = 0; j < p.length; j++) { try { p[j].play(); } catch (e) { } }
    }
  }
  function update() {
    raf = 0;
    var any = false, ss = scrims();
    for (var i = 0; i < ss.length; i++) {
      var s = ss[i];
      var p = active(s) ? pathOf(s, holes(s)) : '';
      if (p) any = true;
      if (s.__p !== p) { s.__p = p; s.style.clipPath = p; }
    }
    freeze();
    if (any && !timer) timer = setInterval(schedule, 400);   // 兜底：布局在没有 DOM 变化时改变（图片载入、过渡）
    else if (!any && timer) { clearInterval(timer); timer = 0; }
  }
  function schedule() { if (!raf) raf = requestAnimationFrame(update); }
  function finishEntry(layer) {
    var list = entering(layer).concat(scrimAnims());
    for (var i = 0; i < list.length; i++) { try { list[i].finish(); } catch (e) { } }
    paused = [];
  }
  // 入场期间第一下：只完成入场表现，同一下不推进台词、不选背景（白名单按钮照常消费自己的点击／按键）
  var swallowUntil = 0;
  function onBackgroundOrDialogue(t, layer) {
    return !!(t && t.closest && (layer.contains(t) || t.closest('.dlg-focus')));
  }
  function onKeep(t) {
    if (!t || !t.closest) return false;
    var ss = scrims();
    for (var i = 0; i < ss.length; i++) {
      var sel = ss[i].getAttribute('data-keep');
      if (!sel || !active(ss[i])) continue;
      try { var k = t.closest(sel); if (k && !k.classList.contains('dlg-layer')) return true; } catch (e) { }
    }
    return false;
  }
  function init() {
    if (window.__survivalFocusInit) return;
    window.__survivalFocusInit = true;
    new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'inert', 'disabled', 'data-read-font'] });
    window.addEventListener('resize', schedule);
    document.addEventListener('scroll', schedule, true);
    window.addEventListener('pointerdown', function (ev) {
      var L = liveLayer();
      if (!L || !entering(L).length || document.querySelector('.menu-scrim')) return;
      if (!onBackgroundOrDialogue(ev.target, L)) return;
      finishEntry(L);
      swallowUntil = Date.now() + 1500;
      ev.preventDefault();
      ev.stopImmediatePropagation();
    }, true);
    window.addEventListener('click', function (ev) {
      if (!swallowUntil) return;
      var due = Date.now() <= swallowUntil;
      swallowUntil = 0;
      var L = liveLayer();
      if (due && L && onBackgroundOrDialogue(ev.target, L)) { ev.preventDefault(); ev.stopImmediatePropagation(); }
    }, true);
    window.addEventListener('keydown', function (ev) {
      if (ev.repeat || (ev.key !== 'Enter' && ev.key !== ' ')) return;
      if ((window.FeedbackWidget && FeedbackWidget.isOpen()) || document.querySelector('.menu-scrim')) return;
      var L = liveLayer();
      if (!L || !entering(L).length || onKeep(document.activeElement)) return;
      finishEntry(L);
      ev.preventDefault();
      ev.stopImmediatePropagation();
    }, true);
    schedule();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
  return { refresh: schedule };
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
    // B45：关闭火堆加工菜单后把焦点交回原火堆卡（等面板卸下；元素不存在、被禁用或在 inert 里就不动）
    // B45b（Q583）：以 # 开头时按选择器找（如加工位卡里的锅卡「#fm-slot-0 .pc-head」）
    focusId: function (id) {
      var tries = 0;
      function go() {
        var c = id.charAt(0) === '#' ? document.querySelector(id) : document.getElementById(id);
        if (!c || (c.closest && c.closest('[inert]')) || !(c.offsetWidth || c.offsetHeight)) { if (++tries < 20) requestAnimationFrame(go); return; }
        try { c.focus({ preventScroll: true }); } catch (e) { try { c.focus(); } catch (e2) { } }
        // B45b（Q584）：焦点交给数量框时同数字跳框（Q562）一样全选，接着输入数字即替换
        if (c.tagName === 'INPUT' && c.select) { try { c.select(); } catch (e3) { } }
      }
      setTimeout(go, 0);
    },
    // BM5b1（S14 Q604）：自动内容变化后的焦点修复——只在焦点已失效（落在页面本身或原元素已卸下）时才交给目标；仍有效的输入／阅读焦点不动
    focusIdIfLost: function (id) {
      var tries = 0;
      function lost() { var a = document.activeElement; return !a || a === document.body || !a.isConnected; }
      function go() {
        if (!lost()) return;
        var c = id.charAt(0) === '#' ? document.querySelector(id) : document.getElementById(id);
        if (!c || (c.closest && c.closest('[inert]')) || !(c.offsetWidth || c.offsetHeight)) { if (++tries < 20) requestAnimationFrame(go); return; }
        try { c.focus({ preventScroll: true }); } catch (e) { try { c.focus(); } catch (e2) { } }
      }
      setTimeout(go, 0);
    },
    // B32g（逐字稿 §1.5 Q401 选 C）：打开新的一级浏览／工具面板时 .NET 已一律收起物品操作盘（不论是否盖住来源）；等盘卸下、四轨遮罩（inert）解除后，
    // 焦点交给新面板（.panel-host；全屏确认层 .overflow-layer）第一个可用控件。焦点已在该面板里就不动；盘仍在（未卸下）时逐帧再等，最多约 20 帧。
    // BM4a.6 试玩修复（Q590「焦点进入新面板首个合法控件」）：
    // B1 输入控件（下拉、文字框、数量框）不作为落点——它们按 B43 吞掉顶栏字母，覆盖打开地图后焦点落在层级下拉框，M／L／C 就都失效了；
    // 面板本身就是确认区（休息、制作、编队、远行准备：.sub-panel[data-enter-scope][tabindex]）时焦点交给面板，Enter 仍归该区唯一确认（B43）；
    // 面板里只有输入控件时回该面板标题（同 survivalPanels 恢复失效时）。
    focusPanel: function () {
      var tries = 0;
      function typing(c) {
        var tg = c.tagName;
        if (tg === 'SELECT' || tg === 'TEXTAREA' || c.isContentEditable) return true;
        if (tg !== 'INPUT') return false;
        return ['button', 'submit', 'reset', 'checkbox', 'radio', 'range', 'color', 'file', 'image'].indexOf((c.type || 'text').toLowerCase()) < 0;
      }
      function go() {
        if (document.querySelector('.wasteland .center-scroll[data-disk-inert]')) { if (++tries < 20) requestAnimationFrame(go); return; }
        var hosts = Array.prototype.filter.call(document.querySelectorAll(COVER_SEL), function (c) { return c.offsetWidth > 0 && c.offsetHeight > 0 && !(c.closest && c.closest('[inert]')); });
        var ae = document.activeElement;
        for (var h = 0; h < hosts.length; h++) if (ae && ae !== document.body && hosts[h].contains(ae)) return;
        for (var k = 0; k < hosts.length; k++) {
          var sp = hosts[k].querySelector('.sub-panel');
          if (sp && sp.matches('[data-enter-scope][tabindex]') && !(sp.closest && sp.closest('[inert]'))) {
            try { sp.focus({ preventScroll: true }); } catch (e) { try { sp.focus(); } catch (e2) { } }
            if (document.activeElement === sp) return;
          }
          var list = hosts[k].querySelectorAll('button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
          for (var i = 0; i < list.length; i++) {
            var c = list[i];
            if (c.disabled || typing(c) || (c.closest && c.closest('[inert]')) || !(c.offsetWidth || c.offsetHeight)) continue;
            try { c.focus({ preventScroll: true }); } catch (e) { try { c.focus(); } catch (e2) { } }
            if (document.activeElement === c) return;
          }
          var title = hosts[k].querySelector('#fm-title, h2, h3');
          if (title && !(title.closest && title.closest('[inert]'))) {
            if (!title.hasAttribute('tabindex')) title.setAttribute('tabindex', '-1');
            try { title.focus({ preventScroll: true }); } catch (e) { try { title.focus(); } catch (e2) { } }
            if (document.activeElement === title) return;
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
    setTimeout(function () {
      var to = toSel ? document.querySelector(toSel) : null;
      // 试玩修复 P2：去向卡被整备页等上层盖住（不在视口外，但看不见）也算不可见，只亮可见端
      if (to && covered(to)) to = null;
      playTo(to, from);
    }, 30);
  }
  // 元素中心点最上层不是它自己（或它的子元素）时视为被盖住
  function covered(el) {
    var r = el.getBoundingClientRect();
    if (!visible(r)) return false;
    var x = Math.min(Math.max(r.left + r.width / 2, 0), window.innerWidth - 1), y = Math.min(Math.max(r.top + r.height / 2, 0), window.innerHeight - 1);
    var hit = document.elementFromPoint(x, y);
    return !!hit && hit !== el && !el.contains(hit);
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
  // B33（Q435）：主界面快捷装备——右栏装备槽行可见才亮一次，看不见只留原结果提示
  function litIfVisible(sel) {
    setTimeout(function () { var el = sel ? document.querySelector(sel) : null; if (el && visible(el.getBoundingClientRect()) && !covered(el)) lit(el); }, 30);
  }
  return { note: note, swap: swap, lit: lit, sceneSync: sceneSync, mark: mark, fly: fly, litIfVisible: litIfVisible };
})();

// B42（S14「2026-11-12 悬停卡片／条目立即显示右栏详情」，HOVER-A01—A08）：悬停即看右栏。
// 页面上带 data-peek="对象键" 的卡片本体或条目整行是热区；鼠标（或笔）真实移动进入热区时把键交给 .NET（PeekController），
// 嵌套热区按指针所在最内层（closest）。移到空白、右栏正文、页签与普通按钮不发送（右栏保留最后一项）；同一热区内移动不重复发送；
// 指针没动而 DOM 重排、滚动换了脚下元素时没有 pointermove，不会连锁切换；按着键（拖拽、拖图、选字）与拖拽进行中不发送；
// 触屏不走悬停（原点击入口不变）；被 inert 遮挡（对白、全屏战斗下层、局部盘遮罩）的热区不发送。键盘焦点到达热区（:focus-visible）给同样的只读信息，焦点不执行。
window.survivalPeek = (function () {
  var net = null, lastKey = null, lx = null, ly = null;
  function hotOf(t) {
    var el = t && t.closest ? t.closest('[data-peek]') : null;
    if (!el || el.closest('[inert]')) return null;
    // BM1（Q485）：右栏（data-detail-rail）里内嵌的对象卡（锅、水、食材等）悬停或键盘聚焦都不替换整页，只按原点击进入。
    // B44（Q492 取代 Q483 的悬停即切）：右栏「人物」页的已穿戴装备槽行与队员行也不再是悬停热区（悬停只高亮），
    // 点击或激活名称（data-peek-name）才查看。右栏之外的嵌套对象仍按最内层
    if (el.closest('[data-detail-rail]')) return null;
    return el;
  }
  function busy() {
    return document.body.classList.contains('dragging') || !!(window.FeedbackWidget && FeedbackWidget.isOpen());
  }
  function send(k) {
    if (!net || !k) return;
    try { var pr = net.invokeMethodAsync('PeekEnter', k); if (pr && pr.catch) pr.catch(function () { }); } catch (e) { }
  }
  function onMove(ev) {
    if (ev.pointerType === 'touch' || ev.buttons) return;
    if (ev.clientX === lx && ev.clientY === ly) return;
    lx = ev.clientX; ly = ev.clientY;
    if (busy()) return;
    var el = hotOf(ev.target);
    var k = el ? el.getAttribute('data-peek') : null;
    if (k === lastKey) return;
    lastKey = k;
    send(k);
  }
  function onFocus(ev) {
    var t = ev.target, fv = false;
    try { fv = !!(t && t.matches && t.matches(':focus-visible')); } catch (e) { }
    if (!fv || busy()) return;
    var el = hotOf(t);
    if (!el) return;
    lastKey = el.getAttribute('data-peek');
    send(lastKey);
  }
  return {
    /* B42 试玩修复：悬停换成另一对象时，右栏详情区（含其中的滚动区）回到顶部；同一对象刷新不调用 */
    top: function (sel) {
      try {
        document.querySelectorAll(sel).forEach(function (root) {
          var host = root.closest('.detail-body') || root;
          if (host.scrollTop > 0) host.scrollTop = 0;
          host.querySelectorAll('*').forEach(function (el) { if (el.scrollTop > 0) el.scrollTop = 0; });
        });
      } catch (e) { }
    },
    /* B44（S14 Q492）：右栏焦点交接。'title'：点名称进入只读详情后焦点交给详情标题；'name:{键}'：点「人物」返回后焦点回到原名称，
       原名称已不在（卸下、离队等）时回「人物」页签。名称与标题都不是悬停热区，聚焦它们不会再进入详情 */
    railFocus: function (req) {
      var rail = document.querySelector('[data-detail-rail]');
      if (!rail || !req) return;
      var el = null;
      if (req === 'title') {
        el = rail.querySelector('[data-peek-shown] h2');
        if (el && !el.hasAttribute('tabindex')) el.setAttribute('tabindex', '-1');
      } else if (req.indexOf('name:') === 0) {
        var k = req.substring(5);
        el = Array.prototype.filter.call(rail.querySelectorAll('[data-peek-name]'), function (x) { return x.getAttribute('data-peek-name') === k; })[0]
          || rail.querySelector('[data-tab-character]');
      }
      if (el) { try { el.focus({ preventScroll: false }); } catch (e) { } }
    },
    init: function (ref) {
      net = ref;
      if (window.__survivalPeekHooked) return;
      window.__survivalPeekHooked = true;
      document.addEventListener('pointermove', onMove, true);
      document.addEventListener('focusin', onFocus, true);
    }
  };
})();

// BM3（S14「Q510—Q511 新瞄准初始化与同一明确目标」）：新一次敌方瞄准成立时，只读地报告此刻真实的键盘焦点敌人与鼠标所在敌人（全屏战斗敌方锚点 #foe-{序号}）。
// 鼠标按最近一次真实指针位置取当下最上层元素（被菜单、对白、遮罩等更高层盖住时取不到锚点；指针离开窗口后为空）；触屏不记指针、不伪造悬停。
// 不读任何旧悬停缓存；在 inert 里的锚点不算。合法与否由 .NET 按当前指令目标范围核验，这里只给候选。
window.survivalAim = (function () {
  var pt = null;
  function foeOf(el) {
    var a = el && el.closest ? el.closest('.bfs .anc.foe[id^="foe-"]') : null;
    if (!a || a.closest('[inert]')) return null;
    var n = parseInt(a.id.substring(4), 10);
    return isNaN(n) ? null : n;
  }
  function track(ev) {
    if (ev.pointerType === 'touch') { pt = null; return; }
    pt = { x: ev.clientX, y: ev.clientY };
  }
  document.addEventListener('pointermove', track, true);
  document.addEventListener('pointerdown', track, true);
  document.addEventListener('mouseout', function (ev) { if (!ev.relatedTarget) pt = null; }, true);
  return {
    probe: function () {
      var ae = document.activeElement;
      var focus = ae && ae !== document.body ? foeOf(ae) : null;
      var mouse = pt ? foeOf(document.elementFromPoint(pt.x, pt.y)) : null;
      return [focus, mouse];
    }
  };
})();

// B34a（S14 Q570、Q528.2、Q529）：全屏战斗阅读区与只读子页的焦点、滚动与战斗记录跟随（只读，不提交、不改游戏状态）。
window.survivalBattleUi = (function () {
  function byId(id) { return id ? document.getElementById(id) : null; }
  function usable(el) { return el && !el.disabled && !(el.closest && el.closest('[inert]')) && el.getClientRects().length > 0; }
  function atBottom(el) { return el.scrollHeight - el.scrollTop - el.clientHeight < 4; }
  return {
    // 聚焦 id；不在或不可用时聚焦 fallback（例如摘要行已不在最近5条时回战报标题）。requireEnabled：确认按钮禁用或不存在时不移焦点（Q529）。
    focus: function (id, fallback, requireEnabled) {
      var el = byId(id);
      if (!usable(el)) { if (requireEnabled) return; el = byId(fallback); }
      if (!el || (el.closest && el.closest('[inert]'))) return;
      try { el.focus({ preventScroll: true }); } catch (e) { }
    },
    // B34 试玩修复 E2（Q572-A01）：战斗已收束、画面冻结在最后一帧时，在行动者锚点上亮一次短反馈（同 .fx-pulse 与行动名牌）
    holdPulse: function (side, id, name) {
      var a = side === 'ally' ? document.querySelector('.bfs .anc.ally[data-actor="' + id + '"]')
        : document.getElementById('foe-' + String(id).replace(/^e/, ''));
      if (!a) return false;
      a.classList.remove('fx-pulse', 'a', 'b'); void a.offsetWidth; a.classList.add('fx-pulse', 'a');
      var old = a.querySelector('.fx-pulse-name'); if (old) old.remove();
      var tag = document.createElement('span'); tag.className = 'fx-pulse-name'; tag.setAttribute('aria-hidden', 'true'); tag.textContent = name;
      var art = a.querySelector('.anc-art'); if (art) a.insertBefore(tag, art); else a.appendChild(tag);
      return true;
    },
    // 展开阅读区时把它滚进视野；收起时战斗内容区回到顶部（原战场阅读位置）。
    scroll: function (where) {
      var host = document.querySelector('.bfs');
      if (!host) return;
      if (where === 'top') { host.scrollTop = 0; return; }
      // 阅读区是战斗内容的最后一块：展开后滚到底，让整块进入视野（等本帧排版完成再滚）
      var go = function () { host.scrollTop = host.scrollHeight; };
      go(); requestAnimationFrame(function () { requestAnimationFrame(go); });
    },
    // 打开战斗记录：定位所点那一条（滚到中间并聚焦），否则定位最新一条。
    logOpen: function (hostId, itemId) {
      var host = byId(hostId);
      if (!host) return;
      var item = byId(itemId);
      if (item) { item.scrollIntoView({ block: 'center' }); try { item.focus({ preventScroll: true }); } catch (e) { } }
      else host.scrollTop = host.scrollHeight;
      host.dataset.atBottom = atBottom(host) ? '1' : '0';
      if (!host.__blogBound) {
        host.__blogBound = true;
        host.addEventListener('scroll', function () { host.dataset.atBottom = atBottom(host) ? '1' : '0'; }, { passive: true });
      }
    },
    // 新记录到来：原已在末尾就跟随到最新（返回 true）；正在阅读旧段则不动滚动（返回 false，由界面显示新增条数）。
    logFollow: function (hostId) {
      var host = byId(hostId);
      if (!host) return true;
      if (host.dataset.atBottom === '1') { host.scrollTop = host.scrollHeight; return true; }
      return false;
    },
    logBottom: function (hostId) {
      var host = byId(hostId);
      if (!host) return;
      host.scrollTop = host.scrollHeight;
      host.dataset.atBottom = '1';
    }
  };
})();
