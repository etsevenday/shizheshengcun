/*!
 * FeedbackWidget · 通用网页留言板组件（无依赖，单文件）
 *
 * 功能：文字留言、Ctrl+V 粘贴 / 拖入 / 选择图片、自动截取当前页面、自动识别系统与浏览器、
 *       附带应用自身的上下文与附件（例如存档），提交到 Supabase（表 + 存储桶）。
 *       未配置 Supabase 或提交失败时，可一键复制为文本、下载截图，改用聊天软件发送。
 *       「大家的留言」页：所有人都能看到全部留言、截图、处理状态和作者回复（读 feedback_wall 视图）。
 *
 * 用法：
 *   <script src="js/feedback-widget.js"></script>
 *   FeedbackWidget.init({
 *     project: 'my-game',                    // 区分不同项目的留言
 *     configUrl: 'feedback.json',            // { supabaseUrl, supabaseKey, table?, bucket?, wall? }
 *     getContext: async () => ({ ... }),     // 可选：应用当前状态（界面、关卡、进度……）
 *     getAttachment: async () => ({ name, text }), // 可选：大段附件，如存档
 *     floatingButton: true,                  // 可选：右下角悬浮按钮
 *   });
 *   FeedbackWidget.open();                   // 打开到“写留言”；open({ tab: 'wall' }) 直接打开“大家的留言”
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
  // 处理状态（与数据库 status 字段一致），显示在留言墙上
  var STATUSES = [
    { id: 'new', label: '待处理' },
    { id: 'doing', label: '处理中' },
    { id: 'done', label: '已处理' },
    { id: 'wontfix', label: '暂不处理' },
  ];
  var MAX_IMAGES = 6;
  var MAX_SIDE = 1920;
  var HTML2CANVAS = 'https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js';

  var opts = {};
  var cfg = null;          // Supabase 配置
  var root = null;         // 弹窗根节点
  var state = null;        // 当前一次留言的状态
  var wall = null;         // 留言墙的读取状态
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
    var c = { supabaseUrl: opts.supabaseUrl || '', supabaseKey: opts.supabaseKey || '', table: opts.table || 'feedback', bucket: opts.bucket || 'feedback-screenshots', wall: opts.wall || 'feedback_wall' };
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

  /** 上传截图再写入留言。补发时“图片已存在”“留言已存在”都视为成功，保证不重复入库。 */
  async function submitToSupabase(payload, blobs, progress) {
    var paths = [];
    for (var i = 0; i < blobs.length; i++) {
      var blob = blobs[i];
      var ext = blob.type === 'image/webp' ? 'webp' : blob.type === 'image/png' ? 'png' : 'jpg';
      var path = payload.project + '/' + payload.id + '/' + (i + 1) + '.' + ext;
      if (progress) progress('正在上传图片 ' + (i + 1) + '/' + blobs.length + '…');
      var up = await fetch(cfg.supabaseUrl + '/storage/v1/object/' + cfg.bucket + '/' + path, {
        method: 'POST',
        headers: headers({ 'Content-Type': blob.type, 'x-upsert': 'false' }),
        body: blob,
      });
      if (!(up.status === 409 || up.status === 400 && /exists|Duplicate/i.test(await up.clone().text()))) await check(up, '上传第' + (i + 1) + '张图片');
      paths.push(path);
    }
    payload.screenshots = paths;
    if (progress) progress('正在提交留言…');
    var res = await fetch(cfg.supabaseUrl + '/rest/v1/' + cfg.table, {
      method: 'POST',
      headers: headers({ 'Content-Type': 'application/json', Prefer: 'return=minimal' }),
      body: JSON.stringify(payload),
    });
    if (res.status === 409) return;
    await check(res, '提交留言');
  }

  // ───────────── 留言墙：所有人都能看到的留言、截图、处理状态与作者回复 ─────────────

  var WALL_PAGE = 30;

  function statusOf(id) { return STATUSES.find(function (s) { return s.id === id; }) || { id: id, label: id }; }
  function categoryOf(id) { return (CATEGORIES.find(function (c) { return c.id === id; }) || {}).label || id; }

  function fmtTime(iso) {
    var d = new Date(iso);
    if (isNaN(d)) return '';
    var o = { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false };
    if (d.getFullYear() !== new Date().getFullYear()) o.year = 'numeric';
    return d.toLocaleString('zh-CN', o);
  }

  function shotUrl(path) {
    return cfg.supabaseUrl + '/storage/v1/object/public/' + cfg.bucket + '/' + String(path).split('/').map(encodeURIComponent).join('/');
  }

  /** 读取一页留言。more 为 true 时接在已有列表后面；切换筛选时重新读取，晚到的旧请求结果会被丢弃。 */
  async function loadWall(more) {
    if (!wall) return;
    var seq = wall.seq = (wall.seq || 0) + 1;
    await loadConfig();
    if (seq !== wall.seq) return;
    if (!cfg.ready) { wall.error = '留言板尚未连接服务器，暂时看不到大家的留言。'; renderWall(); return; }
    if (!more) { wall.rows = []; wall.total = 0; }
    wall.loading = true; wall.error = null; renderWall();
    try {
      var q = 'select=*&project=eq.' + encodeURIComponent(opts.project || location.hostname) +
        '&order=created_at.desc&limit=' + WALL_PAGE + '&offset=' + wall.rows.length;
      if (wall.filter !== 'all') q += '&status=eq.' + wall.filter;
      var res = await fetch(cfg.supabaseUrl + '/rest/v1/' + cfg.wall + '?' + q, { headers: headers({ Prefer: 'count=exact' }) });
      if (seq !== wall.seq) return;
      if (res.status === 404) throw new Error('留言墙还没开通：需要作者在 Supabase 执行 feedback_wall 脚本');
      await check(res, '读取留言');
      var rows = await res.json();
      if (seq !== wall.seq) return;
      var range = (res.headers.get('Content-Range') || '').split('/')[1];
      wall.total = range && range !== '*' ? parseInt(range, 10) : wall.rows.length + rows.length;
      wall.rows = wall.rows.concat(rows);
    } catch (e) {
      if (seq !== wall.seq) return;
      wall.error = e.message;
    } finally {
      if (seq === wall.seq) { wall.loading = false; renderWall(); }
    }
  }

  function renderWall() {
    var pane = root && root.querySelector('.fbw-wall');
    if (!pane || !wall) return;
    var list = pane.querySelector('.fbw-wall-list');
    var foot = pane.querySelector('.fbw-wall-foot');
    list.innerHTML = '';
    wall.rows.forEach(function (r) { list.appendChild(wallItem(r)); });
    if (!wall.rows.length && !wall.loading && !wall.error) {
      list.appendChild(el('div', { class: 'fbw-wall-empty', text: wall.filter === 'all' ? '还没有留言，来写第一条吧。' : '没有这个状态的留言。' }));
    }
    foot.innerHTML = '';
    if (wall.error) {
      foot.appendChild(el('div', { class: 'fbw-status err fbw-grow', text: wall.error }));
      foot.appendChild(el('button', { type: 'button', class: 'fbw-btn', text: '重试', onclick: function () { loadWall(false); } }));
    } else if (wall.loading) {
      foot.appendChild(el('div', { class: 'fbw-muted', text: '读取中…' }));
    } else if (wall.rows.length) {
      foot.appendChild(el('div', { class: 'fbw-muted fbw-grow', text: '共 ' + wall.total + ' 条' }));
      if (wall.rows.length < wall.total) foot.appendChild(el('button', { type: 'button', class: 'fbw-btn', text: '加载更多', onclick: function () { loadWall(true); } }));
    }
  }

  function wallItem(r) {
    var st = statusOf(r.status);
    var kids = [
      el('div', { class: 'fbw-item-head' }, [
        el('b', { text: r.contact || '匿名玩家' }),
        el('span', { text: categoryOf(r.category) }),
        el('span', { text: fmtTime(r.created_at) }),
        r.app_version ? el('span', { text: r.app_version }) : null,
        el('span', { class: 'fbw-badge ' + st.id, text: st.label }),
      ]),
      el('div', { class: 'fbw-item-msg', text: r.message || '（只有截图）' }),
    ];
    if (r.screenshots && r.screenshots.length) {
      kids.push(el('div', { class: 'fbw-item-shots' }, r.screenshots.map(function (p, i) {
        return el('a', { href: shotUrl(p), target: '_blank', rel: 'noopener', title: '新窗口查看第 ' + (i + 1) + ' 张截图' }, [
          el('img', { src: shotUrl(p), alt: '截图 ' + (i + 1), loading: 'lazy' }),
        ]);
      })));
    }
    if (r.note) kids.push(el('div', { class: 'fbw-item-note' }, [el('small', { text: '作者回复' }), r.note]));
    return el('div', { class: 'fbw-item' }, kids);
  }

  /** 切换“写留言 / 大家的留言”两页；第一次进入写留言页时才自动截图。 */
  function setTab(name) {
    if (!root) return;
    root.querySelectorAll('.fbw-tab').forEach(function (t) {
      var on = t.getAttribute('data-tab') === name;
      t.classList.toggle('on', on); t.setAttribute('aria-selected', String(on));
    });
    root.querySelector('.fbw-form').hidden = name !== 'form';
    root.querySelector('.fbw-wall').hidden = name !== 'wall';
    if (name === 'wall') {
      if (wall && !wall.rows.length && !wall.loading && !wall.error) loadWall(false);
    } else if (state) {
      if (!state.captureStarted && opts.autoCapture !== false) { state.captureStarted = true; doCapture(true); }
      var ta = root.querySelector('textarea'); if (ta) ta.focus();
    }
  }

  // ───────────── 发件箱：提交失败时存在本机，之后自动补发 ─────────────

  var OUTBOX_MAX_CHARS = 3500000;
  var NL = String.fromCharCode(10);
  var flushing = false;

  function outboxKey() { return 'fbw.outbox.' + (opts.project || location.hostname); }
  function outboxLoad() { return store(outboxKey()) || []; }
  function outboxSave(list) {
    try { localStorage.setItem(outboxKey(), JSON.stringify(list)); return true; } catch (e) { return false; }
  }

  function blobToDataUrl(b) {
    return new Promise(function (ok) { var r = new FileReader(); r.onload = function () { ok(r.result); }; r.onerror = function () { ok(null); }; r.readAsDataURL(b); });
  }

  async function enqueue(payload, blobs, error) {
    payload.context = payload.context || {};
    payload.context.delivery = { firstError: String(error || ''), queuedAt: new Date().toISOString() };
    var imgs = [], total = 0;
    for (var i = 0; i < blobs.length; i++) {
      var d = await blobToDataUrl(blobs[i]);
      if (!d) continue;
      if (total + d.length > OUTBOX_MAX_CHARS) { payload.message += NL + '（离线保存时截图太大，只保存了前' + imgs.length + '张）'; break; }
      total += d.length;
      imgs.push(d);
    }
    var list = outboxLoad();
    list.push({ payload: payload, images: imgs, tries: 0 });
    if (!outboxSave(list)) {
      list[list.length - 1].images = [];
      payload.message += NL + '（本机空间不足，截图未能保存）';
      return outboxSave(list);
    }
    return true;
  }

  /** 逐条补发；遇到网络问题就停，等下次再试。 */
  async function flushOutbox() {
    if (flushing) return;
    var list = outboxLoad();
    if (!list.length) return;
    await loadConfig();
    if (!cfg.ready) return;
    flushing = true;
    var sent = 0;
    try {
      while (list.length) {
        var item = list[0];
        try {
          var blobs = [];
          for (var i = 0; i < item.images.length; i++) blobs.push(await (await fetch(item.images[i])).blob());
          await submitToSupabase(item.payload, blobs, null);
          list.shift();
          sent++;
          outboxSave(list);
        } catch (e) {
          item.tries = (item.tries || 0) + 1;
          item.payload.context.delivery.lastError = String(e && e.message || e);
          if (item.tries >= 10) { list.push(list.shift()); }
          outboxSave(list);
          break;
        }
      }
    } finally {
      flushing = false;
    }
    if (sent) toast('已补发 ' + sent + ' 条之前没送达的留言', 5000);
    renderOutboxNote();
  }

  function renderOutboxNote() {
    var n = root && root.querySelector('.fbw-outbox');
    if (!n) return;
    var c = outboxLoad().length;
    n.hidden = c === 0;
    n.textContent = c ? '本机还有 ' + c + ' 条之前没送达的留言，联网后会自动补发。' : '';
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
    '.fbw-foot{position:sticky;bottom:0;margin:0 -22px -20px;padding:10px 22px 16px;background:#222b24;border-top:1px solid #3b453c;display:flex;flex-direction:column;gap:10px;z-index:1}' +
    '.fbw-row .fbw-grow{flex:1}' +
    '.fbw details{background:#1b221e;border:1px solid #3b453c;border-radius:4px;padding:6px 10px}' +
    '.fbw summary{cursor:pointer;font-size:13px;color:#aab2a4}' +
    '.fbw pre{white-space:pre-wrap;word-break:break-all;font-size:12px;max-height:200px;overflow:auto;margin:6px 0 0;color:#c9cfbf}' +
    '.fbw-status{font-size:14px}' +
    '.fbw-copybox{width:100%;min-height:110px;font-size:12px!important}' +
    '.fbw-status.err{color:#f0c7b9}.fbw-status.ok{color:#9db786}' +
    '.fbw-toast{position:fixed;left:50%;top:20px;transform:translateX(-50%);background:#141d17;border:1px solid #d6b779;color:#eee9db;padding:8px 14px;border-radius:5px;z-index:2147483001;font-size:14px}' +
    '.fbw-fab{position:fixed;right:16px;bottom:16px;z-index:2147482999;background:#d6b779;color:#222a20;border:0;border-radius:20px;padding:9px 16px;font-weight:600;cursor:pointer;box-shadow:0 4px 14px rgba(0,0,0,.4)}' +
    // 写留言 / 大家的留言 两页
    '.fbw-tabs{display:flex;gap:4px;border-bottom:1px solid #3b453c;margin-top:-4px}' +
    '.fbw-tab{background:none;border:0;border-bottom:2px solid transparent;color:#aab2a4;padding:6px 12px;margin-bottom:-1px;cursor:pointer;font:inherit;font-size:15px}' +
    '.fbw-tab.on{color:#d6b779;border-bottom-color:#d6b779}' +
    '.fbw-pane{display:flex;flex-direction:column;gap:12px}' +
    '.fbw-wall-list{gap:8px}' +
    '.fbw-filters{display:flex;gap:6px;flex-wrap:wrap;align-items:center}' +
    '.fbw-item{background:#1b221e;border:1px solid #3b453c;border-radius:6px;padding:10px 12px;display:flex;flex-direction:column;gap:6px}' +
    '.fbw-item-head{display:flex;gap:10px;align-items:center;flex-wrap:wrap;font-size:13px;color:#aab2a4}' +
    '.fbw-item-head b{color:#eee9db;font-weight:500;font-size:14px}' +
    '.fbw-badge{margin-left:auto;border-radius:10px;padding:1px 9px;font-size:12px;border:1px solid #485143;color:#aab2a4;white-space:nowrap}' +
    '.fbw-badge.doing{border-color:#d6b779;color:#d6b779}.fbw-badge.done{border-color:#9db786;color:#9db786}.fbw-badge.wontfix{opacity:.7}' +
    '.fbw-item-msg{white-space:pre-wrap;word-break:break-word}' +
    '.fbw-item-note{border-left:3px solid #d6b779;background:#222b24;padding:6px 10px;font-size:14px;white-space:pre-wrap;word-break:break-word}' +
    '.fbw-item-note small{display:block;color:#d6b779;font-size:12px}' +
    '.fbw-item-shots{display:flex;gap:6px;flex-wrap:wrap}' +
    '.fbw-item-shots a{display:block;width:112px;height:70px;border:1px solid #485143;border-radius:4px;overflow:hidden;background:#111}' +
    '.fbw-item-shots img{width:100%;height:100%;object-fit:cover;display:block}' +
    '.fbw-wall-empty{color:#aab2a4;text-align:center;padding:24px 0}';

  function ensureCss() {
    if (document.getElementById('fbw-css')) return;
    var s = el('style', { id: 'fbw-css' });
    s.textContent = CSS;
    document.head.appendChild(s);
  }

  function toast(msg, ms) {
    var t = el('div', { class: 'fbw-toast fbw-ignore', role: 'status', text: msg });
    document.body.appendChild(t);
    setTimeout(function () { t.remove(); }, ms || 2600);
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
      if (state.capturePromise) { setStatus('正在截取当前画面，稍等…'); await state.capturePromise; if (!root) return; }
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
      await submitToSupabase(payload, state.images.map(function (i) { return i.blob; }), setStatus);
      lastSubmitAt = Date.now();
      flushOutbox();
      store('fbw.draft.' + (opts.project || ''), null);
      store('fbw.contact', state.contact);
      var ticket = payload.id.slice(0, 8);
      setStatus('已收到，谢谢！编号 ' + ticket, 'ok');
      state.message = '';
      root.querySelector('textarea').value = '';
      state.images.forEach(function (i) { URL.revokeObjectURL(i.url); });
      state.images = [];
      renderImages();
      // 关窗后再留一条较长的提示，避免玩家还没看清编号弹窗就没了
      setTimeout(function () {
        if (root && state && !state.message) { close(); toast('留言已收到，谢谢！编号 ' + ticket, 8000); }
      }, 1600);
    } catch (e) {
      var queued = cfg && cfg.ready && state.lastPayload && await enqueue(state.lastPayload, state.images.map(function (i) { return i.blob; }), e && e.message);
      if (queued) {
        store('fbw.draft.' + (opts.project || ''), null);
        state.queued = true;
        setStatus('暂时没送达（' + e.message + '）。留言已保存在本机，下次打开游戏会自动补发；也可以先复制为文字发给作者。', 'err');
        renderOutboxNote();
      } else {
        setStatus('提交失败：' + e.message + '。可以先复制为文字发给作者，草稿已保留。', 'err');
      }
      root.querySelector('.fbw-fallback').hidden = false;
    } finally {
      btn.disabled = !!(state && state.queued);
      if (state && state.queued) btn.textContent = '已存待补发';
    }
  }

  function build(o) {
    var draft = store('fbw.draft.' + (opts.project || '')) || {};
    state = {
      category: (o && o.category) || draft.category || 'bug',
      message: draft.message || '',
      contact: draft.contact || store('fbw.contact') || '',
      images: [],
      includeAttachment: true,
      capturing: false,
      capturePromise: null,
      captureStarted: false,
    };
    wall = { filter: 'all', rows: [], total: 0, loading: false, error: null, seq: 0 };

    var tabs = el('div', { class: 'fbw-tabs', role: 'tablist' }, [
      el('button', { type: 'button', class: 'fbw-tab on', role: 'tab', 'data-tab': 'form', 'aria-selected': 'true', text: '写留言', onclick: function () { setTab('form'); } }),
      el('button', { type: 'button', class: 'fbw-tab', role: 'tab', 'data-tab': 'wall', 'aria-selected': 'false', text: '大家的留言', onclick: function () { setTab('wall'); } }),
    ]);

    var cats = el('div', { class: 'fbw-cats', role: 'radiogroup', 'aria-label': '反馈类型' });
    CATEGORIES.forEach(function (c) {
      var b = el('button', { type: 'button', class: 'fbw-cat' + (c.id === state.category ? ' on' : ''), role: 'radio', 'aria-checked': String(c.id === state.category), text: c.label, onclick: function () {
        state.category = c.id; saveDraft();
        cats.querySelectorAll('.fbw-cat').forEach(function (x) { x.classList.remove('on'); x.setAttribute('aria-checked', 'false'); });
        b.classList.add('on'); b.setAttribute('aria-checked', 'true');
      } });
      cats.appendChild(b);
    });

    var ta = el('textarea', { placeholder: '遇到了什么？期望是什么？（截图可以直接 Ctrl+V 粘贴到这里）', 'aria-label': '留言内容', maxlength: '5000' });
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
        el('button', { type: 'button', class: 'fbw-btn', text: '截取当前画面', onclick: function () { doCapture(false); } }),
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

    // 写留言页：原有的表单与吸底操作区
    var formPane = el('div', { class: 'fbw-pane fbw-form' }, [
      el('small', { text: opts.subtitle || '小问题随手留言；会自动附上你的系统、浏览器和当前画面，方便作者定位。' }),
      cats, ta, drop,
      el('div', { class: 'fbw-row' }, [el('div', { class: 'fbw-grow' }, [contact])]),
      attachRow, preview,
      // 底部操作区吸附在弹窗底部：内容再长（展开自动信息、出现复制框）提交按钮也始终可见
      el('div', { class: 'fbw-foot' }, [
        el('div', { class: 'fbw-row' }, [
          el('div', { class: 'fbw-status fbw-grow' }),
          el('button', { type: 'button', class: 'fbw-btn', text: '取消', onclick: close }),
          el('button', { type: 'button', class: 'fbw-btn fbw-primary', text: '提交', onclick: submit }),
        ]),
        el('div', { class: 'fbw-outbox fbw-muted', hidden: 'hidden' }),
      fallback,
      ]),
    ]);

    // 大家的留言页：按状态筛选的公开列表
    var filters = el('div', { class: 'fbw-filters', role: 'radiogroup', 'aria-label': '按处理状态筛选' });
    [{ id: 'all', label: '全部' }].concat(STATUSES).forEach(function (s) {
      var b = el('button', { type: 'button', class: 'fbw-cat' + (s.id === 'all' ? ' on' : ''), role: 'radio', 'aria-checked': String(s.id === 'all'), text: s.label, onclick: function () {
        wall.filter = s.id;
        filters.querySelectorAll('.fbw-cat').forEach(function (x) { x.classList.remove('on'); x.setAttribute('aria-checked', 'false'); });
        b.classList.add('on'); b.setAttribute('aria-checked', 'true');
        loadWall(false);
      } });
      filters.appendChild(b);
    });
    var wallPane = el('div', { class: 'fbw-pane fbw-wall', hidden: 'hidden' }, [
      el('small', { text: '所有玩家的留言都在这里；作者处理后会标上状态、写回复。' }),
      filters,
      el('div', { class: 'fbw-pane fbw-wall-list' }),
      el('div', { class: 'fbw-row fbw-wall-foot' }),
    ]);

    var box = el('div', { class: 'fbw', role: 'dialog', 'aria-modal': 'true', 'aria-label': opts.title || '反馈与留言' }, [
      el('h2', {}, [el('span', { text: opts.title || '反馈与留言' }), el('button', { type: 'button', class: 'fbw-x', 'aria-label': '关闭', text: '×', onclick: close })]),
      tabs, formPane, wallPane,
    ]);

    root = el('div', { class: 'fbw-back' }, [box]);
    root.addEventListener('mousedown', function (e) { if (e.target === root) close(); });
    // 留言框内的按键不传给页面（避免触发应用快捷键）；Esc 关闭
    root.addEventListener('keydown', function (e) {
      e.stopPropagation();
      if (e.key === 'Escape') close();
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey) && !formPane.hidden) submit();
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

  /** 截取当前画面并加入图片列表。auto 为打开弹窗时的自动截图：限时、失败不打扰。 */
  function doCapture(auto) {
    if (!state || state.capturing) return state && state.capturePromise;
    var mine = state;
    mine.capturing = true; renderImages();
    mine.capturePromise = (async function () {
      try {
        var work = captureScreen();
        var b = auto
          ? await Promise.race([work, new Promise(function (r) { setTimeout(function () { r(null); }, 8000); })])
          : await work;
        if (b && state === mine) await addImage(b, '当前画面');
        else if (!b && !auto) toast('自动截图失败，可以用系统截图后粘贴');
      } catch (e) {
        if (!auto) toast('自动截图失败，可以用系统截图后粘贴');
      } finally {
        mine.capturing = false; mine.capturePromise = null;
        if (state === mine && root) renderImages();
      }
    })();
    return mine.capturePromise;
  }

  // ───────────── 对外接口 ─────────────

  function close() {
    if (!root) return;
    root.remove();
    if (state) state.images.forEach(function (i) { URL.revokeObjectURL(i.url); });
    root = null; state = null; wall = null;
    if (opts.onClose) try { opts.onClose(); } catch (e) { /* 忽略 */ }
  }

  /** 打开弹窗。o.tab 为 'wall' 时直接打开“大家的留言”，否则打开“写留言”。 */
  async function open(o) {
    if (root) return;
    ensureCss();
    loadConfig();
    // 立即显示弹窗，截图在后台进行并用占位格提示；截图时会排除弹窗本身（ignoreElements）
    build(o);
    document.body.appendChild(root);
    renderImages();
    renderOutboxNote();
    flushOutbox();
    setTab(o && o.tab === 'wall' ? 'wall' : 'form');
  }

  function init(o) {
    opts = Object.assign({}, o || {});
    ensureCss();
    setTimeout(flushOutbox, 3000);
    if (opts.floatingButton) {
      document.body.appendChild(el('button', { type: 'button', class: 'fbw-fab fbw-ignore', text: opts.buttonText || '反馈', onclick: function () { open(); } }));
    }
  }

  window.FeedbackWidget = { init: init, open: open, close: close, isOpen: function () { return !!root; }, detectEnv: detectEnv };
})();
