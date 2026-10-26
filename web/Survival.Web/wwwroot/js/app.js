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
        // 中部纵滚：操作条底部在可视区外时下移到刚好露出（不超过把操作条顶部推出可视区）
        if (p.classList.contains('center-scroll') && cs.overflowY === 'auto') {
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
