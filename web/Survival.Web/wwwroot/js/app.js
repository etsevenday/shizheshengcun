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
  // 每位玩家自己的界面偏好（折叠面板等），不进存档
  // 聚焦并选中输入框内容，直接打字即替换
  focusSelect: function (el) {
    try { el.focus(); el.select(); } catch (e) { }
  },
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
      if (t === 'INPUT' || t === 'TEXTAREA') { if (ev.key !== 'Escape') return; }
      if (ev.ctrlKey || ev.altKey || ev.metaKey) return;
      var k = ev.key;
      if (window.FeedbackWidget && FeedbackWidget.isOpen()) return;
      // 数量框等弹窗自己处理按键（回车确认、Esc 取消），不再触发游戏快捷键
      if (ev.target && ev.target.closest && ev.target.closest('.modal')) return;
      if (['m', 'M', 'c', 'C', 'r', 'R', 'l', 'L', 'f', 'F', 'Escape'].indexOf(k) >= 0) {
        ev.preventDefault();
        dotnet.invokeMethodAsync('OnKey', k.length === 1 ? k.toUpperCase() : k);
      }
    };
    document.addEventListener('keydown', window.__survivalKeys);
  }
};
