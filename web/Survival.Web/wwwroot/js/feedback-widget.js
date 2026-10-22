/*!
 * FeedbackWidget · 通用网页留言板组件（无依赖，单文件）
 *
 * 功能：文字留言、Ctrl+V 粘贴 / 拖入 / 选择图片、自动截取当前页面、自动识别系统与浏览器、
 *       附带应用自身的上下文与附件（例如存档），提交到 Supabase（表 + 存储桶）。
 *       未配置 Supabase 或提交失败时，可一键复制为文本、下载截图，改用聊天软件发送。
 *
 * 用法：
 *   <script src="js/feedback-widget.js"></script>
 *   FeedbackWidget.init({
 *     project: 'my-game',                    // 区分不同项目的留言
 *     configUrl: 'feedback.json',            // { supabaseUrl, supabaseKey, table?, bucket? }
 *     getContext: async () => ({ ... }),     // 可选：应用当前状态（界面、关卡、进度……）
 *     getAttachment: async () => ({ name, text }), // 可选：大段附件，如存档
 *     floatingButton: true,                  // 可选：右下角悬浮按钮
 *   });
 *   FeedbackWidget.open();
 *
 * 建表与存储桶脚本见 tools/supabase-feedback.sql。
 */
(function () {
  'use strict';

  var CATEGORIES = [
    { id: 'bug', label: '出错了' },
    { id: 'ux', label: '体验/界面' },
    { id: 'balance', label: '数值/难度' },
    { id: 'idea', label: '建议' },
    { id: 'other', label: '其他' },
  ];
  var MAX_IMAGES = 6;
  var MAX_SIDE = 1920;
  var HTML2CANVAS = 'https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js';

  var opts = {};
  var cfg = null;          // Supabase 配置
  var root = null;         // 弹窗根节点
  var state = null;        // 当前一次留言的状态
  var lastSubmitAt = 0;

  // ───────────── 工具 ─────────────

  function el(tag, attrs, children) {
    var n = document.createElement(tag);
    if (attrs) Object.keys(attrs).forEach(function (k) {
      if (k === 'text') n.textContent = attrs[k];
      else if (k === 'class') n.className = attrs[k];
      else if (k.indexOf('on') === 0) n.addEventListener(k.slice(2), attrs[k]);
      else n.setAttribute(k, attrs[k]);
    });
    (children || []).forEach(function (c) { if (c) n.appendChild(typeof c === 'string' ? document.createTextNode(c) : c); });
    return n;
  }

  function uuid() {
    if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
    var b = new Uint8Array(16);
    (window.crypto || {}).getRandomValues ? crypto.getRandomValues(b) : b.forEach(function (_, i) { b[i] = Math.random() * 256 | 0; });
    b[6] = (b[6] & 15) | 64; b[8] = (b[8] & 63) | 128;
    var h = Array.prototype.map.call(b, function (x) { return (x + 256).toString(16).slice(1); }).join('');
    return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20);
  }

  function store(key, val) {
    try {
      if (val === undefined) return JSON.parse(localStorage.getItem(key) || 'null');
      if (val === null) localStorage.removeItem(key); else localStorage.setItem(key, JSON.stringify(val));
    } catch (e) { return null; }
  }

  function loadScript(src) {
    return new Promise(function (ok, fail) {
      var s = document.createElement('script');
      s.src = src; s.async = true; s.onload = ok; s.onerror = fail;
      document.head.appendChild(s);
    });
  }

  function fmtBytes(n) { return n > 1048576 ? (n / 1048576).toFixed(1) + 'MB' : Math.ceil(n / 1024) + 'KB'; }

  // ───────────── 环境识别 ─────────────

  function parseUa(ua) {
    var os = '未知系统', browser = '未知浏览器', m;
    if ((m = ua.match(/Windows NT ([\d.]+)/))) os = ({ '10.0': 'Windows 10/11', '6.3': 'Windows 8.1', '6.2': 'Windows 8', '6.1': 'Windows 7' })[m[1]] || 'Windows NT ' + m[1];
    else if ((m = ua.match(/(iPhone|iPad|iPod).*OS ([\d_]+)/))) os = 'iOS ' + m[2].replace(/_/g, '.') + '（' + m[1] + '）';
    else if ((m = ua.match(/Mac OS X ([\d_]+)/))) os = 'macOS ' + m[1].replace(/_/g, '.');
    else if ((m = ua.match(/Android ([\d.]+)/))) os = 'Android ' + m[1];
    else if (/CrOS/.test(ua)) os = 'ChromeOS';
    else if (/Linux/.test(ua)) os = 'Linux';
    if ((m = ua.match(/Edg(?:e|A|iOS)?\/([\d.]+)/))) browser = 'Edge ' + m[1];
    else if ((m = ua.match(/OPR\/([\d.]+)/))) browser = 'Opera ' + m[1];
    else if ((m = ua.match(/MicroMessenger\/([\d.]+)/))) browser = '微信内置浏览器 ' + m[1];
    else if ((m = ua.match(/QQBrowser\/([\d.]+)/))) browser = 'QQ浏览器 ' + m[1];
    else if ((m = ua.match(/(?:Firefox|FxiOS)\/([\d.]+)/))) browser = 'Firefox ' + m[1];
    else if ((m = ua.match(/(?:Chrome|CriOS)\/([\d.]+)/))) browser = 'Chrome ' + m[1];
    else if ((m = ua.match(/Version\/([\d.]+).*Safari/))) browser = 'Safari ' + m[1];
    return { os: os, browser: browser };
  }

  async function detectEnv() {
    var ua = navigator.userAgent;
    var p = parseUa(ua);
    try {
      var uad = navigator.userAgentData;
      if (uad && uad.getHighEntropyValues) {
        var h = await uad.getHighEntropyValues(['platform', 'platformVersion', 'architecture', 'bitness', 'fullVersionList']);
        if (h.platform === 'Windows') p.os = (parseInt(h.platformVersion, 10) >= 13 ? 'Windows 11' : 'Windows 10') + (h.architecture ? '（' + h.architecture + (h.bitness ? ' ' + h.bitness + '位' : '') + '）' : '');
        else if (h.platform === 'macOS' && h.platformVersion) p.os = 'macOS ' + h.platformVersion;
        var list = (h.fullVersionList || []).filter(function (b) { return !/Not.?A.?Brand|Chromium/i.test(b.brand); });
        var pick = list.find(function (b) { return /Edge|Opera/.test(b.brand); }) || list[0];
        if (pick) p.browser = pick.brand.replace('Microsoft ', '').replace('Google ', '') + ' ' + pick.version;
      }
    } catch (e) { /* 高精度信息不可用时沿用 UA 解析 */ }
    var now = new Date();
    return {
      os: p.os,
      browser: p.browser,
      language: navigator.language,
      timezone: (Intl.DateTimeFormat().resolvedOptions() || {}).timeZone || '',
      localTime: now.toLocaleString('zh-CN', { hour12: false }),
      viewport: window.innerWidth + '×' + window.innerHeight,
      screen: screen.width + '×' + screen.height,
      pixelRatio: window.devicePixelRatio || 1,
      touchPoints: navigator.maxTouchPoints || 0,
      cpuCores: navigator.hardwareConcurrency || null,
      memoryGB: navigator.deviceMemory || null,
      colorScheme: window.matchMedia && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light',
      online: navigator.onLine,
      url: location.href,
      appBuild: (document.querySelector('meta[name="app-build"]') || {}).content || '',
      userAgent: ua,
    };
  }

  // ───────────── 图片 ─────────────

  function blobToImage(blob) {
    return new Promise(function (ok, fail) {
      var url = URL.createObjectURL(blob);
      var img = new Image();
      img.onload = function () { ok({ img: img, url: url }); };
      img.onerror = function () { URL.revokeObjectURL(url); fail(new Error('图片无法读取')); };
      img.src = url;
    });
  }

  function canvasToBlob(canvas) {
    return new Promise(function (ok) {
      canvas.toBlob(function (b) {
        if (b && b.type === 'image/webp') return ok(b);
        canvas.toBlob(function (j) { ok(j); }, 'image/jpeg', 0.85);
      }, 'image/webp', 0.85);
    });
  }

  /** 缩到长边不超过 MAX_SIDE，转为 webp/jpeg。 */
  async function normalizeImage(blob) {
    var r = await blobToImage(blob);
    var w = r.img.naturalWidth, h = r.img.naturalHeight;
    var k = Math.min(1, MAX_SIDE / Math.max(w, h));
    var c = document.createElement('canvas');
    c.width = Math.round(w * k); c.height = Math.round(h * k);
    c.getContext('2d').drawImage(r.img, 0, 0, c.width, c.height);
    URL.revokeObjectURL(r.url);
    return canvasToBlob(c);
  }

  async function captureScreen() {
    if (!window.html2canvas) await loadScript(HTML2CANVAS);
    var canvas = await window.html2canvas(document.body, {
      backgroundColor: getComputedStyle(document.body).backgroundColor || '#000',
      scale: Math.min(1, window.devicePixelRatio || 1),
      logging: false,
      useCORS: true,
      width: window.innerWidth,
      height: window.innerHeight,
      windowWidth: document.documentElement.clientWidth,
      windowHeight: window.innerHeight,
      ignoreElements: function (n) { return n === root || (n.classList && n.classList.contains('fbw-ignore')); },
    });
    return canvasToBlob(canvas);
  }

  async function addImage(blob, label) {
    if (state.images.length >= MAX_IMAGES) return toast('最多 ' + MAX_IMAGES + ' 张图片');
    try {
      var b = await normalizeImage(blob);
      state.images.push({ blob: b, url: URL.createObjectURL(b), label: label || '图片' });
      renderImages();
    } catch (e) { toast('图片读取失败：' + e.message); }
  }

  // ───────────── Supabase ─────────────

  async function loadConfig() {
    if (cfg) return cfg;
    var c = { supabaseUrl: opts.supabaseUrl || '', supabaseKey: opts.supabaseKey || '', table: opts.table || 'feedback', bucket: opts.bucket || 'feedback-screenshots' };
    if (opts.configUrl && !c.supabaseUrl) {
      try {
        var r = await fetch(opts.configUrl, { cache: 'no-store' });
        if (r.ok) Object.assign(c, await r.json());
      } catch (e) { /* 没有配置文件：退化为复制文本 */ }
    }
    c.supabaseUrl = (c.supabaseUrl || '').replace(/\/+$/, '');
    c.ready = !!(c.supabaseUrl && c.supabaseKey);
    cfg = c;
    return c;
  }

  function headers(extra) {
    var h = { apikey: cfg.supabaseKey };
    // 旧版 anon key 是 JWT，需要同时放进 Authorization；新版 sb_publishable_ 只放 apikey
    if (cfg.supabaseKey.indexOf('sb_') !== 0) h.Authorization = 'Bearer ' + cfg.supabaseKey;
    return Object.assign(h, extra || {});
  }

  async function check(res, what) {
    if (res.ok) return;
    var t = '';
    try { t = await res.text(); } catch (e) { /* 忽略 */ }
    throw new Error(what + '失败（' + res.status + '）' + (t ? '：' + t.slice(0, 200) : ''));
  }

  async function submitToSupabase(payload) {
    var paths = [];
    for (var i = 0; i < state.images.length; i++) {
      var img = state.images[i];
      var ext = img.blob.type === 'image/webp' ? 'webp' : 'jpg';
      var path = payload.project + '/' + payload.id + '/' + (i + 1) + '.' + ext;
      setStatus('正在上传图片 ' + (i + 1) + '/' + state.images.length + '…');
      var up = await fetch(cfg.supabaseUrl + '/storage/v1/object/' + cfg.bucket + '/' + path, {
        method: 'POST',
        headers: headers({ 'Content-Type': img.blob.type, 'x-upsert': 'false' }),
        body: img.blob,
      });
      await check(up, '上传第' + (i + 1) + '张图片');
      paths.push(path);
    }
    payload.screenshots = paths;
    setStatus('正在提交留言…');
    var res = await fetch(cfg.supabaseUrl + '/rest/v1/' + cfg.table, {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json', Prefer: 'return=minimal' }),
      body: JSON.stringify(payload),
    });
    await check(res, '提交留言');
  }

  // ───────────── 组装与复制 ─────────────

  async function buildPayload() {
    var env = await detectEnv();
    var app = null, attachment = null;
    if (opts.getContext) { try { app = await opts.getContext(); } catch (e) { app = { error: String(e) }; } }
    if (state.includeAttachment && opts.getAttachment) { try { attachment = await opts.getAttachment(); } catch (e) { attachment = null; } }
    return {
      id: uuid(),
      project: opts.project || location.hostname,
      category: state.category,
      message: state.message.trim(),
      contact: state.contact.trim() || null,
      app_version: (app && app.version) || env.appBuild || null,
      page_url: env.url,
      user_agent: env.userAgent,
      context: { env: env, app: app },
      attachment_name: attachment ? attachment.name : null,
      attachment: attachment ? attachment.text : null,
    };
  }

  function asText(p) {
    var cat = (CATEGORIES.find(function (c) { return c.id === p.category; }) || {}).label || p.category;
    var e = p.context.env;
    var lines = [
      '【反馈 · ' + cat + '】' + (p.contact ? '来自 ' + p.contact : ''),
      p.message || '（无文字）',
      '',
      '— 环境 —',
      e.os + ' · ' + e.browser + ' · 窗口 ' + e.viewport + ' · 屏幕 ' + e.screen + ' ×' + e.pixelRatio,
      e.localTime + ' · ' + e.url + (e.appBuild ? ' · 构建 ' + e.appBuild : ''),
    ];
    if (p.context.app) {
      lines.push('', '— 应用状态 —');
      var s = p.context.app.summary;
      if (s) lines.push(s); else lines.push(JSON.stringify(p.context.app).slice(0, 1500));
    }
    if (state.images.length) lines.push('', '（另有 ' + state.images.length + ' 张截图，请单独发送）');
    return lines.join('\n');
  }

  async function copyText(text) {
    try { await navigator.clipboard.writeText(text); return true; }
    catch (e) {
      var ta = el('textarea', { class: 'fbw-ignore', style: 'position:fixed;left:-9999px' });
      ta.value = text; document.body.appendChild(ta); ta.select();
      var ok = false; try { ok = document.execCommand('copy'); } catch (x) { ok = false; }
      ta.remove(); return ok;
    }
  }

  function downloadImages() {
    state.images.forEach(function (img, i) {
      var a = el('a', { href: img.url, download: '反馈截图-' + (i + 1) + (img.blob.type === 'image/webp' ? '.webp' : '.jpg') });
      document.body.appendChild(a); a.click(); a.remove();
    });
  }

  // ───────────── 界面 ─────────────

  var CSS = '' +
    '.fbw-back{position:fixed;inset:0;z-index:2147483000;background:rgba(5,8,6,.72);display:flex;align-items:center;justify-content:center;font-family:"Microsoft YaHei","PingFang SC","Noto Sans SC",sans-serif;color:#eee9db}' +
    '.fbw{width:min(620px,calc(100vw - 32px));max-height:calc(100vh - 32px);overflow:auto;background:#222b24;border:1px solid #556051;border-radius:8px;box-shadow:0 12px 40px rgba(0,0,0,.6);padding:20px 22px;display:flex;flex-direction:column;gap:12px;font-size:15px;line-height:1.6}' +
    '.fbw h2{margin:0;font-size:21px;font-weight:500;display:flex;justify-content:space-between;align-items:center}' +
    '.fbw small,.fbw .fbw-muted{color:#aab2a4;font-size:13px}' +
    '.fbw-x{background:none;border:0;color:#aab2a4;font-size:22px;cursor:pointer;line-height:1}' +
    '.fbw-cats{display:flex;gap:6px;flex-wrap:wrap}' +
    '.fbw-cat{background:#1b221e;border:1px solid #485143;color:#eee9db;border-radius:16px;padding:4px 12px;cursor:pointer;font:inherit;font-size:14px}' +
    '.fbw-cat.on{border-color:#d6b779;color:#d6b779;background:#2c3427}' +
    '.fbw textarea,.fbw input[type=text]{width:100%;box-sizing:border-box;background:#151c17;color:#eee9db;border:1px solid #485143;border-radius:4px;padding:9px 10px;font:inherit;font-size:15px}' +
    '.fbw textarea{min-height:120px;resize:vertical}' +
    '.fbw textarea:focus,.fbw input:focus{outline:2px solid #d6b779;outline-offset:1px}' +
    '.fbw-drop{border:1px dashed #56604f;border-radius:5px;padding:10px;display:flex;gap:8px;flex-wrap:wrap;align-items:center;min-height:84px}' +
    '.fbw-drop.drag{border-color:#d6b779;background:#2c3427}' +
    '.fbw-thumb{position:relative;width:112px;height:70px;border:1px solid #485143;border-radius:4px;overflow:hidden;background:#111}' +
    '.fbw-thumb img{width:100%;height:100%;object-fit:cover;display:block}' +
    '.fbw-thumb span{position:absolute;left:0;right:0;bottom:0;font-size:11px;background:rgba(0,0,0,.6);padding:1px 4px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}' +
    '.fbw-thumb button{position:absolute;right:2px;top:2px;width:22px;height:22px;border-radius:11px;border:0;background:rgba(0,0,0,.7);color:#fff;cursor:pointer;font-size:14px;line-height:22px;padding:0}' +
    '.fbw-hint{flex:1;min-width:180px;font-size:13px;color:#aab2a4}' +
    '.fbw-btn{background:transparent;border:1px solid #485143;color:#eee9db;border-radius:4px;padding:7px 12px;cursor:pointer;font:inherit;font-size:14px}' +
    '.fbw-btn:disabled{opacity:.5;cursor:not-allowed}' +
    '.fbw-primary{background:#d6b779;color:#222a20;border:0;font-weight:600;font-size:16px;padding:10px 18px}' +
    '.fbw [hidden]{display:none!important}' +
    '.fbw-row{display:flex;gap:8px;align-items:center;flex-wrap:wrap}' +
    '.fbw-row .fbw-grow{flex:1}' +
    '.fbw details{background:#1b221e;border:1px solid #3b453c;border-radius:4px;padding:6px 10px}' +
    '.fbw summary{cursor:pointer;font-size:13px;color:#aab2a4}' +
    '.fbw pre{white-space:pre-wrap;word-break:break-all;font-size:12px;max-height:200px;overflow:auto;margin:6px 0 0;color:#c9cfbf}' +
    '.fbw-status{font-size:14px}' +
    '.fbw-copybox{width:100%;min-height:110px;font-size:12px!important}' +
    '.fbw-status.err{color:#f0c7b9}.fbw-status.ok{color:#9db786}' +
    '.fbw-toast{position:fixed;left:50%;top:20px;transform:translateX(-50%);background:#141d17;border:1px solid #d6b779;color:#eee9db;padding:8px 14px;border-radius:5px;z-index:2147483001;font-size:14px}' +
    '.fbw-fab{position:fixed;right:16px;bottom:16px;z-index:2147482999;background:#d6b779;color:#222a20;border:0;border-radius:20px;padding:9px 16px;font-weight:600;cursor:pointer;box-shadow:0 4px 14px rgba(0,0,0,.4)}';

  function ensureCss() {
    if (document.getElementById('fbw-css')) return;
    var s = el('style', { id: 'fbw-css' });
    s.textContent = CSS;
    document.head.appendChild(s);
  }

  function toast(msg) {
    var t = el('div', { class: 'fbw-toast fbw-ignore', text: msg });
    document.body.appendChild(t);
    setTimeout(function () { t.remove(); }, 2600);
  }

  function setStatus(msg, kind) {
    var s = root && root.querySelector('.fbw-status');
    if (!s) return;
    s.textContent = msg || '';
    s.className = 'fbw-status' + (kind ? ' ' + kind : '');
  }

  function renderImages() {
    var box = root.querySelector('.fbw-imgs');
    box.innerHTML = '';
    state.images.forEach(function (img, i) {
      box.appendChild(el('div', { class: 'fbw-thumb', title: img.label }, [
        el('img', { src: img.url, alt: img.label }),
        el('span', { text: img.label }),
        el('button', { type: 'button', 'aria-label': '移除这张图片', text: '×', onclick: function () {
          URL.revokeObjectURL(img.url);
          state.images.splice(i, 1);
          renderImages();
        } }),
      ]));
    });
    if (state.capturing) box.appendChild(el('div', { class: 'fbw-thumb' }, [el('span', { text: '正在截取当前画面…' })]));
  }

  function saveDraft() {
    store('fbw.draft.' + (opts.project || ''), { category: state.category, message: state.message, contact: state.contact });
  }

  async function renderPreview() {
    var pre = root.querySelector('.fbw-pre');
    var env = await detectEnv();
    var app = null;
    if (opts.getContext) { try { app = await opts.getContext(); } catch (e) { app = null; } }
    var lines = [
      '系统：' + env.os, '浏览器：' + env.browser, '窗口：' + env.viewport + '，屏幕：' + env.screen + '，缩放 ×' + env.pixelRatio,
      '语言/时区：' + env.language + ' / ' + env.timezone, '页面：' + env.url + (env.appBuild ? '（构建 ' + env.appBuild + '）' : ''),
    ];
    if (app) lines.push('', app.summary || JSON.stringify(app, null, 1).slice(0, 2000));
    pre.textContent = lines.join('\n');
  }

  async function submit() {
    if (!state.message.trim() && !state.images.length) { setStatus('写一句话或贴一张图再提交吧', 'err'); return; }
    if (Date.now() - lastSubmitAt < 15000) { setStatus('刚提交过，请稍等几秒再提交下一条', 'err'); return; }
    var btn = root.querySelector('.fbw-primary');
    btn.disabled = true;
    try {
      await loadConfig();
      var payload = await buildPayload();
      state.lastPayload = payload;
      if (!cfg.ready) {
        var text = asText(payload);
        var ok = await copyText(text);
        root.querySelector('.fbw-fallback').hidden = false;
        if (ok) setStatus('留言板尚未连接服务器，已复制为文字，请粘贴到聊天里发给作者' + (state.images.length ? '；截图可点“下载截图”另发' : ''), 'ok');
        else { showCopyBox(text); setStatus('留言板尚未连接服务器，请复制下面的文字发给作者', 'err'); }
        return;
      }
      await submitToSupabase(payload);
      lastSubmitAt = Date.now();
      store('fbw.draft.' + (opts.project || ''), null);
      store('fbw.contact', state.contact);
      setStatus('已收到，谢谢！编号 ' + payload.id.slice(0, 8), 'ok');
      state.message = '';
      root.querySelector('textarea').value = '';
      state.images.forEach(function (i) { URL.revokeObjectURL(i.url); });
      state.images = [];
      renderImages();
      setTimeout(function () { if (root && state && !state.message) close(); }, 1600);
    } catch (e) {
      setStatus('提交失败：' + e.message + '。可以先复制为文字发给作者，草稿已保留。', 'err');
      root.querySelector('.fbw-fallback').hidden = false;
    } finally {
      btn.disabled = false;
    }
  }

  function build() {
    var draft = store('fbw.draft.' + (opts.project || '')) || {};
    state = {
      category: draft.category || 'bug',
      message: draft.message || '',
      contact: draft.contact || store('fbw.contact') || '',
      images: [],
      includeAttachment: true,
      capturing: false,
    };

    var cats = el('div', { class: 'fbw-cats', role: 'radiogroup', 'aria-label': '反馈类型' });
    CATEGORIES.forEach(function (c) {
      var b = el('button', { type: 'button', class: 'fbw-cat' + (c.id === state.category ? ' on' : ''), role: 'radio', 'aria-checked': String(c.id === state.category), text: c.label, onclick: function () {
        state.category = c.id; saveDraft();
        cats.querySelectorAll('.fbw-cat').forEach(function (x) { x.classList.remove('on'); x.setAttribute('aria-checked', 'false'); });
        b.classList.add('on'); b.setAttribute('aria-checked', 'true');
      } });
      cats.appendChild(b);
    });

    var ta = el('textarea', { placeholder: '遇到了什么？期望是什么？（截图可以直接 Ctrl+V 粘贴到这里）', 'aria-label': '留言内容' });
    ta.value = state.message;
    ta.addEventListener('input', function () { state.message = ta.value; saveDraft(); });

    var file = el('input', { type: 'file', accept: 'image/*', multiple: 'multiple', hidden: 'hidden' });
    file.addEventListener('change', function () { Array.prototype.forEach.call(file.files, function (f) { addImage(f, f.name); }); file.value = ''; });

    var drop = el('div', { class: 'fbw-drop' }, [
      el('div', { class: 'fbw-imgs', style: 'display:contents' }),
      el('div', { class: 'fbw-hint' }, [
        '截图：Ctrl+V 粘贴、拖进来，或 ',
        el('button', { type: 'button', class: 'fbw-btn', text: '选择图片', onclick: function () { file.click(); } }),
        ' ',
        el('button', { type: 'button', class: 'fbw-btn', text: '截取当前画面', onclick: doCapture }),
      ]),
      file,
    ]);
    ['dragenter', 'dragover'].forEach(function (t) { drop.addEventListener(t, function (e) { e.preventDefault(); drop.classList.add('drag'); }); });
    ['dragleave', 'drop'].forEach(function (t) { drop.addEventListener(t, function (e) { e.preventDefault(); drop.classList.remove('drag'); }); });
    drop.addEventListener('drop', function (e) {
      Array.prototype.forEach.call(e.dataTransfer.files || [], function (f) { if (/^image\//.test(f.type)) addImage(f, f.name); });
    });

    var contact = el('input', { type: 'text', placeholder: '怎么称呼你（可选）', 'aria-label': '称呼', maxlength: '40' });
    contact.value = state.contact;
    contact.addEventListener('input', function () { state.contact = contact.value; saveDraft(); });

    var attachRow = null;
    if (opts.getAttachment) {
      var cb = el('input', { type: 'checkbox', id: 'fbw-attach', checked: 'checked' });
      cb.addEventListener('change', function () { state.includeAttachment = cb.checked; });
      attachRow = el('label', { for: 'fbw-attach', class: 'fbw-muted', style: 'display:flex;gap:6px;align-items:center;cursor:pointer' }, [cb, opts.attachmentLabel || '附带当前进度数据（方便作者复现问题）']);
    }

    var preview = el('details', {}, [el('summary', { text: '将自动附带的信息（系统、浏览器、当前状态）' }), el('pre', { class: 'fbw-pre', text: '读取中…' })]);
    preview.addEventListener('toggle', function () { if (preview.open) renderPreview(); });

    var fallback = el('div', { class: 'fbw-row fbw-fallback', hidden: 'hidden' }, [
      el('button', { type: 'button', class: 'fbw-btn', text: '复制为文字', onclick: async function () {
        var p = state.lastPayload || await buildPayload();
        var text = asText(p);
        if (await copyText(text)) toast('已复制，可以粘贴到聊天里');
        else showCopyBox(text);
      } }),
      el('button', { type: 'button', class: 'fbw-btn', text: '下载截图', onclick: downloadImages }),
      el('textarea', { class: 'fbw-copybox', readonly: 'readonly', hidden: 'hidden', 'aria-label': '可复制的反馈文字' }),
    ]);

    var box = el('div', { class: 'fbw', role: 'dialog', 'aria-modal': 'true', 'aria-label': opts.title || '反馈与留言' }, [
      el('h2', {}, [el('span', { text: opts.title || '反馈与留言' }), el('button', { type: 'button', class: 'fbw-x', 'aria-label': '关闭', text: '×', onclick: close })]),
      el('small', { text: opts.subtitle || '小问题随手留言；会自动附上你的系统、浏览器和当前画面，方便作者定位。' }),
      cats, ta, drop,
      el('div', { class: 'fbw-row' }, [el('div', { class: 'fbw-grow' }, [contact])]),
      attachRow, preview,
      el('div', { class: 'fbw-row' }, [
        el('div', { class: 'fbw-status fbw-grow' }),
        el('button', { type: 'button', class: 'fbw-btn', text: '取消', onclick: close }),
        el('button', { type: 'button', class: 'fbw-btn fbw-primary', text: '提交', onclick: submit }),
      ]),
      fallback,
    ]);

    root = el('div', { class: 'fbw-back' }, [box]);
    root.addEventListener('mousedown', function (e) { if (e.target === root) close(); });
    // 留言框内的按键不传给页面（避免触发应用快捷键）；Esc 关闭
    root.addEventListener('keydown', function (e) {
      e.stopPropagation();
      if (e.key === 'Escape') close();
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) submit();
    });
    root.addEventListener('paste', function (e) {
      var items = (e.clipboardData && e.clipboardData.items) || [];
      var got = false;
      Array.prototype.forEach.call(items, function (it) {
        if (it.kind === 'file' && /^image\//.test(it.type)) { got = true; addImage(it.getAsFile(), '粘贴的图片'); }
      });
      if (got) e.preventDefault();
    });
    return ta;
  }

  /** 剪贴板不可用时，把文字放进可全选的文本框。 */
  function showCopyBox(text) {
    var box = root && root.querySelector('.fbw-copybox');
    if (!box) return;
    box.value = text;
    box.hidden = false;
    box.focus();
    box.select();
    toast('浏览器不允许自动复制，请按 Ctrl+C 复制已选中的文字');
  }

  async function doCapture() {
    if (state.capturing) return;
    state.capturing = true; renderImages();
    try {
      var b = await captureScreen();
      if (b) await addImage(b, '当前画面');
    } catch (e) {
      toast('自动截图失败，可以用系统截图后粘贴');
    } finally {
      if (state) { state.capturing = false; if (root) renderImages(); }
    }
  }

  // ───────────── 对外接口 ─────────────

  function close() {
    if (!root) return;
    root.remove();
    if (state) state.images.forEach(function (i) { URL.revokeObjectURL(i.url); });
    root = null; state = null;
    if (opts.onClose) try { opts.onClose(); } catch (e) { /* 忽略 */ }
  }

  async function open(o) {
    if (root) return;
    ensureCss();
    loadConfig();
    // 先截图再显示弹窗，保证截到的是用户正在看的画面
    var shot = null;
    if (opts.autoCapture !== false) {
      try { shot = await Promise.race([captureScreen(), new Promise(function (r) { setTimeout(function () { r(null); }, 6000); })]); } catch (e) { shot = null; }
    }
    var ta = build();
    if (o && o.category) state.category = o.category;
    document.body.appendChild(root);
    if (shot) await addImage(shot, '当前画面'); else renderImages();
    ta.focus();
  }

  function init(o) {
    opts = Object.assign({}, o || {});
    ensureCss();
    if (opts.floatingButton) {
      document.body.appendChild(el('button', { type: 'button', class: 'fbw-fab fbw-ignore', text: opts.buttonText || '反馈', onclick: function () { open(); } }));
    }
  }

  window.FeedbackWidget = { init: init, open: open, close: close, isOpen: function () { return !!root; }, detectEnv: detectEnv };
})();
