#!/usr/bin/env node
// 可重放试玩场景（零依赖：Node 自带 WebSocket + Chrome DevTools Protocol，本机 Chrome／Edge）。
// 一个进程跑完一组场景文件，每个场景在三档分辨率各跑一遍（各用独立的浏览器上下文，存档互不干扰），
// 每一步后自动检查：控制台错误与未捕获异常、页面横向溢出、关键按钮是否在视口内／被盖住、可见文字最小字号；
// 场景里的 shot 步与基线逐像素对比，超阈值的出差异图。用法与场景写法见 tools/playtest/README.md。
//
//   node tools/playtest/run.mjs [场景文件或目录…] [选项]
//     默认跑 tools/playtest/scenarios/ 下所有 .json（下划线开头的除外）
//     --base <网址>        游戏地址根（默认 http://localhost:5199），场景里的 open 路径接在后面
//     --sizes 1280x720,…   分辨率（默认 1280x720,1600x900,1920x1080；场景可自己限定）
//     --jobs <n>           同时跑几个（场景×分辨率）任务，默认 3
//     --out <目录>         输出根（默认 tools/playtest/out，已 gitignore）；本次结果在 <out>/runs/<时间>/
//     --update-baseline    本次截图直接覆盖基线（界面有意改动后用）
//     --write-fixtures     允许 saveFixture 步把存档写回 tools/playtest/fixtures/（造存档局面用）
//     --headful            有界面运行（调试场景用）
//     --keep-going         某步失败后继续后面的步骤（默认该任务就此停）
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync, readdirSync, statSync, rmSync, copyFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve, dirname, basename, extname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { decodePng, encodePng, diffPng } from './png.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const sleep = ms => new Promise(r => setTimeout(r, ms));

// ───────────── 参数 ─────────────
const argv = process.argv.slice(2);
const opt = { files: [], base: 'http://localhost:5199', sizes: '1280x720,1600x900,1920x1080', jobs: 3, out: join(HERE, 'out') };
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === '--base') opt.base = argv[++i];
  else if (a === '--sizes') opt.sizes = argv[++i];
  else if (a === '--jobs') opt.jobs = Number(argv[++i]);
  else if (a === '--out') opt.out = resolve(argv[++i]);
  else if (a === '--update-baseline') opt.updateBaseline = true;
  else if (a === '--write-fixtures') opt.writeFixtures = true;
  else if (a === '--headful') opt.headful = true;
  else if (a === '--keep-going') opt.keepGoing = true;
  else if (a.startsWith('--')) { console.error('未知选项 ' + a); process.exit(2); }
  else opt.files.push(a);
}
opt.base = opt.base.replace(/\/+$/, '');
const SIZES = opt.sizes.split(',').map(s => s.trim()).filter(Boolean);
const PORT = Number(process.env.PLAYTEST_RUN_PORT || 9350);
const CHROMES = [process.env.CHROME, 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].filter(Boolean);
const FIXTURES = join(HERE, 'fixtures');
const LIB = join(HERE, 'scenarios', '_lib');

function scenarioFiles() {
  const inputs = opt.files.length ? opt.files : [join(HERE, 'scenarios')];
  const out = [];
  for (const p of inputs) {
    const full = resolve(p);
    if (existsSync(full) && statSync(full).isDirectory()) {
      for (const f of readdirSync(full).sort()) if (extname(f) === '.json' && !f.startsWith('_')) out.push(join(full, f));
    } else if (existsSync(full)) out.push(full);
    else if (existsSync(join(HERE, 'scenarios', p))) out.push(join(HERE, 'scenarios', p));
    else if (existsSync(join(HERE, 'scenarios', p + '.json'))) out.push(join(HERE, 'scenarios', p + '.json'));
    else { console.error('找不到场景 ' + p); process.exit(2); }
  }
  return out;
}

// 展开 use（引用 scenarios/_lib/<名>.json 的步骤列表，可嵌套）
function expand(steps, depth = 0) {
  if (depth > 8) throw new Error('use 嵌套过深');
  const out = [];
  for (const s of steps) {
    if (s && typeof s === 'object' && 'use' in s) {
      const f = join(LIB, s.use + '.json');
      if (!existsSync(f)) throw new Error('找不到步骤库 ' + s.use);
      const lib = JSON.parse(readFileSync(f, 'utf8'));
      for (const x of expand(lib.steps || lib, depth + 1)) out.push({ ...x, from: s.use });
    } else out.push(s);
  }
  return out;
}

// ───────────── CDP（一个浏览器级连接，多个 flatten 会话） ─────────────
let ws, msgId = 0; const waiters = new Map(); const listeners = new Map();
function send(method, params = {}, sessionId) {
  return new Promise((ok, bad) => {
    const i = ++msgId; waiters.set(i, { ok, bad, method });
    ws.send(JSON.stringify(sessionId ? { id: i, method, params, sessionId } : { id: i, method, params }));
  });
}
async function launch() {
  const exe = CHROMES.find(existsSync); if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const profile = join(tmpdir(), `survival-run-${PORT}-${process.pid}`);
  const args = [`--remote-debugging-port=${PORT}`, `--user-data-dir=${profile}`, '--no-first-run', '--no-default-browser-check',
    '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1', '--window-size=1920,1080', '--mute-audio',
    '--disable-background-timer-throttling', '--disable-renderer-backgrounding', '--disable-backgrounding-occluded-windows', 'about:blank'];
  if (!opt.headful) args.unshift('--headless=new');
  const proc = spawn(exe, args, { stdio: 'ignore' });
  let ver;
  for (let i = 0; i < 80 && !ver; i++) { try { ver = await (await fetch(`http://127.0.0.1:${PORT}/json/version`)).json(); } catch { await sleep(250); } }
  if (!ver) throw new Error(`Chrome 没在端口 ${PORT} 上起来（端口被占可设 PLAYTEST_RUN_PORT）`);
  ws = new WebSocket(ver.webSocketDebuggerUrl);
  await new Promise((ok, bad) => { ws.onopen = ok; ws.onerror = bad; });
  ws.onmessage = m => {
    const d = JSON.parse(m.data);
    if (d.id && waiters.has(d.id)) { const w = waiters.get(d.id); waiters.delete(d.id); d.error ? w.bad(new Error(`${w.method}: ${d.error.message}`)) : w.ok(d.result); }
    else if (d.method && d.sessionId && listeners.has(d.sessionId)) listeners.get(d.sessionId)(d);
  };
  return async () => {
    try { await send('Browser.close'); } catch { }
    try { proc.kill(); } catch { }
    await sleep(300); try { rmSync(profile, { recursive: true, force: true }); } catch { }
  };
}

// ───────────── 页面内工具（每个新文档注入） ─────────────
const PAGE_LIB = String.raw`(() => { if (window.__pt) return;
const vis = el => { const r = el.getBoundingClientRect(); if (r.width < 1 || r.height < 1) return false;
  const st = getComputedStyle(el); return st.visibility !== 'hidden' && st.display !== 'none' && st.opacity !== '0'; };
const desc = el => { if (!el || !el.tagName) return String(el); let s = el.tagName.toLowerCase();
  if (el.id) s += '#' + el.id; const pt = el.getAttribute('data-pt'); if (pt) s += '[data-pt=' + pt + ']';
  const c = String(el.className && el.className.baseVal !== undefined ? el.className.baseVal : el.className || '').trim().split(/\s+/).filter(Boolean).slice(0, 2);
  if (c.length) s += '.' + c.join('.'); const t = (el.innerText || el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 30); if (t) s += ' "' + t + '"'; return s; };
const ownText = el => { let t = ''; for (const n of el.childNodes) if (n.nodeType === 3) t += n.nodeValue; return t.trim(); };
const textOf = el => (el.innerText || el.getAttribute('aria-label') || el.title || '').replace(/\s+/g, ' ').trim();
const INTER = 'button,a,[role=button],input,select,textarea,summary,[data-pt],[tabindex]';
// 选择器：用 " >> " 串起来逐层缩小范围；每段可以是 pt=名（[data-pt="名"]）、text=子串、text==全文、或 CSS
function matchPart(roots, part) {
  part = part.trim().replace(/\s+/g, ' '); const out = new Set();
  let css = null, text = null, exact = false;
  if (part.startsWith('pt=')) css = '[data-pt="' + part.slice(3) + '"]';
  else if (part.startsWith('text==')) { text = part.slice(6); exact = true; }
  else if (part.startsWith('text=')) text = part.slice(5);
  else css = part;
  for (const root of roots) {
    if (css) { for (const el of root.querySelectorAll(css)) if (vis(el)) out.add(el); continue; }
    const cand = [];
    for (const el of root.querySelectorAll('*')) {
      const s = textOf(el); if (!(exact ? s === text : s.includes(text))) continue;
      if (!vis(el)) continue;
      if ([...el.children].some(c => { const cs = textOf(c); return (exact ? cs === text : cs.includes(text)) && vis(c); })) continue;
      cand.push(el);
    }
    // 命中文字的最小元素若在按钮里，取按钮；按钮排前
    for (const el of cand) out.add(el.closest('button,a,[role=button],summary,label') || el);
  }
  const arr = [...out]; arr.sort((a, b) => (b.tagName === 'BUTTON') - (a.tagName === 'BUTTON')); return arr;
}
function findAll(spec) { let roots = [document]; for (const p of String(spec).split(' >> ')) { roots = matchPart(roots, p); if (!roots.length) break; } return roots; }
function hitInfo(el) {
  const r = el.getBoundingClientRect(); const cx = r.x + r.width / 2, cy = r.y + r.height / 2;
  const inView = cx >= 0 && cy >= 0 && cx < innerWidth && cy < innerHeight;
  const full = r.left >= -1 && r.top >= -1 && r.right <= innerWidth + 1 && r.bottom <= innerHeight + 1;
  let covered = false, by = null;
  if (inView) { const h = document.elementFromPoint(cx, cy); if (h && h !== el && !el.contains(h)) { covered = true; by = desc(h); } }
  return { x: Math.round(cx), y: Math.round(cy), w: Math.round(r.width), h: Math.round(r.height), inView, full, covered, coveredBy: by,
    disabled: !!(el.disabled || el.closest('[disabled],[inert],[aria-disabled=true]')), title: (el.title || '').slice(0, 80), desc: desc(el) };
}
window.__pt = {
  desc, findAll,
  locate(spec, nth, scroll) {
    const all = findAll(spec); const el = all[nth || 0]; if (!el) return { found: false, count: all.length };
    // 像玩家一样先把它滚进来：不完整可见或中心被盖住就滚到容器中间（只滚它所在的滚动容器链）
    if (scroll) { const h = hitInfo(el); if (!h.full || h.covered) el.scrollIntoView({ block: 'center', inline: 'nearest' }); }
    return { found: true, count: all.length, ...hitInfo(el) };
  },
  text(spec) { if (!spec) return document.body.innerText; return findAll(spec).map(e => e.innerText).join('\n'); },
  check(watch, minFont) {
    const vw = innerWidth, vh = innerHeight; const res = { overflow: [], watch: [], smallFonts: [], clipped: [], errorUi: false };
    const eu = document.querySelector('#blazor-error-ui'); if (eu && getComputedStyle(eu).display !== 'none') res.errorUi = true;
    const all = document.body.querySelectorAll('*'); const off = new Set();
    for (const el of all) {
      const r = el.getBoundingClientRect(); if (r.width < 1 || r.height < 1) continue;
      if (r.right > vw + 1 || r.left < -1) {
        // 被有横滚／裁切的祖先容器（容器本身在视口内）兜住的不算
        let clip = false;
        for (let p = el.parentElement; p && p !== document.documentElement; p = p.parentElement) {
          const ox = getComputedStyle(p).overflowX;
          if (ox !== 'visible') { const pr = p.getBoundingClientRect(); if (pr.right <= vw + 1 && pr.left >= -1) { clip = true; break; } }
        }
        if (!clip && vis(el)) {
          if (!(el.parentElement && off.has(el.parentElement))) res.overflow.push({ el: desc(el), left: Math.round(r.left), right: Math.round(r.right) });
          off.add(el);
        }
      }
      const t = ownText(el);
      if (t && r.width > 2 && r.height > 2 && r.bottom > 0 && r.top < vh && r.right > 0 && r.left < vw && vis(el)) {
        const st = getComputedStyle(el); const fs = parseFloat(st.fontSize);
        if (fs < minFont) res.smallFonts.push({ el: desc(el), size: fs, text: t.slice(0, 20) });
        if ((st.overflowX === 'hidden' || st.overflowX === 'clip') && el.scrollWidth > el.clientWidth + 1 && st.textOverflow !== 'ellipsis')
          res.clipped.push({ el: desc(el), scrollWidth: el.scrollWidth, clientWidth: el.clientWidth });
      }
    }
    res.overflow = res.overflow.slice(0, 8); res.smallFonts = res.smallFonts.slice(0, 12); res.clipped = res.clipped.slice(0, 8);
    // 关键元素：默认只看第一个匹配（列表里靠后的行本来就可能要滚动）；{ "sel": …, "all": true } 看全部。对白盖着时不查（对白本来就盖全屏）
    const dimmed = document.querySelector('[data-dlg-next]') || [...document.querySelectorAll('.dlg-focus')].some(e => getComputedStyle(e).visibility === 'visible');
    if (!dimmed) for (const w of watch || []) {
      const spec = typeof w === 'string' ? w : w.sel; const els = findAll(spec);
      for (const el of (w.all ? els : els.slice(0, 1))) { const h = hitInfo(el); if (!h.inView || !h.full || h.covered) res.watch.push({ spec, ...h }); }
    }
    return res;
  },
  dump() {
    const items = [];
    for (const el of document.querySelectorAll(INTER)) { if (!vis(el)) continue; const h = hitInfo(el);
      items.push({ pt: el.getAttribute('data-pt') || undefined, tag: el.tagName.toLowerCase(), text: textOf(el).replace(/\s+/g, ' ').slice(0, 40), x: h.x, y: h.y,
        disabled: h.disabled || undefined, offscreen: !h.inView || undefined, covered: h.covered || undefined }); }
    return { text: document.body.innerText.slice(0, 6000), items: items.slice(0, 300) };
  },
  storage(prefix) { const o = {}; for (let i = 0; i < localStorage.length; i++) { const k = localStorage.key(i); if (k.startsWith(prefix)) o[k] = localStorage.getItem(k); } return o; },
};
})()`;

// 截图前冻结动画与光标（配合 prefers-reduced-motion），减少基线对比的噪声；
// 键盘焦点框是否出现取决于脚本把焦点挪过去的时机（同一版本两次截图会时有时无），截图时也藏掉
const FREEZE_CSS = `*,*::before,*::after{animation-duration:0s!important;animation-delay:0s!important;animation-iteration-count:1!important;transition:none!important;caret-color:transparent!important}:focus,:focus-visible{outline-color:transparent!important}`;

// ───────────── 一个任务：一个场景 × 一档分辨率 ─────────────
class Job {
  constructor(sc, size, runDir) {
    this.sc = sc; this.size = size; this.runDir = runDir;
    [this.w, this.h] = size.split('x').map(Number);
    this.errs = []; this.issues = new Map(); this.steps = [];
  }
  async ev(expr) {
    const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true, userGesture: true }, this.sid);
    if (r.exceptionDetails) throw new Error('页面执行出错：' + (r.exceptionDetails.exception?.description || r.exceptionDetails.text).split('\n')[0]);
    return r.result.value;
  }
  pt(call) { return this.ev(`(window.__pt || (${PAGE_LIB}, window.__pt)).${call}`); }
  async open() {
    this.ctx = (await send('Target.createBrowserContext', { disposeOnDetach: true })).browserContextId;
    const { targetId } = await send('Target.createTarget', { url: 'about:blank', browserContextId: this.ctx });
    this.tid = targetId;
    this.sid = (await send('Target.attachToTarget', { targetId, flatten: true })).sessionId;
    listeners.set(this.sid, d => this.onEvent(d));
    const s = (m, p) => send(m, p, this.sid);
    await s('Page.enable'); await s('Runtime.enable'); await s('Log.enable');
    await s('Emulation.setDeviceMetricsOverride', { width: this.w, height: this.h, deviceScaleFactor: 1, mobile: false });
    await s('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }] });
    await s('Page.addScriptToEvaluateOnNewDocument', { source: PAGE_LIB });
  }
  onEvent(d) {
    const p = d.params;
    if (d.method === 'Runtime.exceptionThrown') this.errs.push('未捕获异常：' + (p.exceptionDetails.exception?.description || p.exceptionDetails.text).slice(0, 400));
    else if (d.method === 'Runtime.consoleAPICalled' && (p.type === 'error' || p.type === 'assert'))
      this.errs.push('console.error：' + p.args.map(a => a.value ?? a.description ?? '').join(' ').slice(0, 400));
    else if (d.method === 'Log.entryAdded' && p.entry.level === 'error') this.errs.push(`日志错误（${p.entry.source}）：${(p.entry.text + ' ' + (p.entry.url || '')).slice(0, 400)}`);
    else if (d.method === 'Page.javascriptDialogOpening') send('Page.handleJavaScriptDialog', { accept: true }, this.sid).catch(() => { });
    else if (d.method === 'Page.loadEventFired') this.loaded = true;
  }
  async close() { listeners.delete(this.sid); try { await send('Target.closeTarget', { targetId: this.tid }); } catch { } try { await send('Target.disposeBrowserContext', { browserContextId: this.ctx }); } catch { } }

  async navigate(url) {
    this.loaded = false;
    await send('Page.navigate', { url }, this.sid);
    for (let i = 0; i < 200 && !this.loaded; i++) await sleep(50);
    await this.waitReady();
  }
  async waitReady(ms = 60000) {
    const end = Date.now() + ms;
    while (Date.now() < end) {
      // index.html 的载入占位是 #app > .boot，Blazor 渲染后被替换
      try { if (await this.ev(`document.readyState === 'complete' && !!document.querySelector('#app') && !document.querySelector('#app > .boot') && (document.querySelector('#app').innerText || '').trim().length > 0`)) { await this.settle(); return; } } catch { }
      await sleep(200);
    }
    throw new Error('页面加载超时（60 秒）');
  }
  // Blazor 渲染是异步的：等两帧再给 120ms
  async settle(ms = 120) { try { await this.ev('new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))'); } catch { } await sleep(ms); }

  async locate(spec, nth = 0, timeout = 5000, scroll = true) {
    const end = Date.now() + timeout; let last;
    for (; ;) {
      last = await this.pt(`locate(${JSON.stringify(spec)}, ${nth}, ${scroll})`);
      if (last.found) { if (scroll) last = await this.pt(`locate(${JSON.stringify(spec)}, ${nth}, false)`); return last; }
      if (Date.now() > end) throw new Error(`找不到 ${spec}${nth ? ' 第' + nth + '个' : ''}（${last.count} 个匹配）`);
      await sleep(150);
    }
  }
  async mouse(type, x, y, extra = {}) {
    await send('Input.dispatchMouseEvent', { type, x, y, button: type === 'mouseMoved' ? 'none' : 'left', buttons: type === 'mousePressed' ? 1 : 0, clickCount: 1, pointerType: 'mouse', ...extra }, this.sid);
  }
  async clickAt(x, y) { await this.mouse('mouseMoved', x, y); await this.mouse('mousePressed', x, y); await this.mouse('mouseReleased', x, y); }
  async point(t, nth) { if (Array.isArray(t)) return { x: t[0], y: t[1] }; const h = await this.locate(t, nth); return h; }
  async key(k) {
    const parts = k.split('+'); const key = parts.pop(); const mods = parts.reduce((m, p) => m | ({ alt: 1, ctrl: 2, meta: 4, shift: 8 }[p.toLowerCase()] || 0), 0);
    const one = key.length === 1;
    const code = key === ' ' || key === 'Space' ? 'Space' : one ? (/[a-z]/i.test(key) ? 'Key' + key.toUpperCase() : /\d/.test(key) ? 'Digit' + key : key) : key;
    const realKey = key === 'Space' ? ' ' : key;
    const vk = { Escape: 27, Enter: 13, Tab: 9, Backspace: 8, Delete: 46, Space: 32, ' ': 32, ArrowUp: 38, ArrowDown: 40, ArrowLeft: 37, ArrowRight: 39, Home: 36, End: 35, PageUp: 33, PageDown: 34 }[key] ?? (one ? key.toUpperCase().charCodeAt(0) : 0);
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key: realKey, code, windowsVirtualKeyCode: vk, modifiers: mods, text: realKey.length === 1 && !(mods & 6) ? realKey : undefined }, this.sid);
    await send('Input.dispatchKeyEvent', { type: 'keyUp', key: realKey, code, windowsVirtualKeyCode: vk, modifiers: mods }, this.sid);
  }
  // 对白自动读完：剧情对白随首次行为随时插进来，场景默认不关心，遇到就按空格读完（记次数）；
  // 要测对白本身时场景写 "autoDialogue": false，或在这一步写 "dialogue": true
  async dismissDialogue(st) {
    if (this.sc.autoDialogue === false || st.dialogue) return;
    let n = 0;
    while (n < 60 && (await this.pt(`locate('[data-dlg-next]', 0, false)`)).found) { await this.key('Space'); await this.settle(); n++; }
    if (n) { this.dialogues = (this.dialogues || 0) + n; await sleep(300); }   // 对白遮罩 .dlg-focus 淡出约 140ms，期间仍挡点击
  }
  issue(kind, sev, key, detail, stepNo) {
    const k = kind + '|' + key; if (this.issues.has(k)) { this.issues.get(k).count++; return; }
    this.issues.set(k, { kind, sev, detail, firstStep: stepNo, count: 1 });
  }
  async waitFor(pred, timeout, what) {
    const end = Date.now() + timeout;
    for (; ;) { const v = await pred(); if (v === true) return; if (Date.now() > end) throw new Error(typeof v === 'string' ? v : what); await sleep(150); }
  }

  // 执行一步
  async step(st, no) {
    const sc = this.sc; const v = Object.keys(st).find(k => VERBS.has(k)); if (!v) throw new Error('无法识别的步骤：' + JSON.stringify(st));
    const a = st[v]; const rec = { no, verb: v, arg: typeof a === 'string' || typeof a === 'number' ? a : undefined };
    if (st.label) rec.label = st.label;
    switch (v) {
      case 'open': await this.navigate(opt.base + (a.startsWith('/') ? a : '/' + a)); break;
      case 'reload': this.loaded = false; await send('Page.reload', { ignoreCache: false }, this.sid); for (let i = 0; i < 200 && !this.loaded; i++) await sleep(50); await this.waitReady(); break;
      case 'size': { const [w, h] = a.split('x').map(Number); await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false }, this.sid); this.w = w; this.h = h; await this.settle(300); break; }
      case 'loadSave': await this.loadSave(a); break;
      case 'saveFixture': {
        if (!opt.writeFixtures) { rec.skipped = '未加 --write-fixtures'; break; }
        const store = await this.pt(`storage('survival.')`);
        const keep = Object.fromEntries(Object.entries(store).filter(([k]) => !k.endsWith('.tmp') && !k.endsWith('.owner')));
        mkdirSync(FIXTURES, { recursive: true });
        writeFileSync(join(FIXTURES, a + '.json'), JSON.stringify({ note: st.note || '', from: relative(join(HERE, '..', '..'), sc.file).replace(/\\/g, '/'), storage: keep }, null, 1) + '\n');
        rec.wrote = a + '.json'; break;
      }
      case 'click': case 'dblclick': case 'rclick': {
        let h = await this.locate(a, st.nth || 0, st.timeout ?? 5000);
        // 禁用时等它变可用（对白锁、行动收尾等），期间照样读掉插进来的对白
        for (const end = Date.now() + (st.timeout ?? 5000); h.disabled && !st.allowDisabled && Date.now() < end;) {
          await sleep(200); await this.dismissDialogue(st); h = await this.locate(a, st.nth || 0, 2000);
        }
        rec.target = h.desc;
        if (h.disabled && !st.allowDisabled) throw new Error(`${a} 是禁用的${h.title ? '（悬停：' + h.title + '）' : ''}`);
        if (!h.inView) throw new Error(`${a} 不在视口内（滚动后仍看不到）`);
        if (h.covered) this.issue('点击目标被盖住', 'error', a, `${h.desc} 被 ${h.coveredBy} 盖住，点击落在后者上`, no);
        if (v === 'rclick') { await this.mouse('mouseMoved', h.x, h.y); await this.mouse('mousePressed', h.x, h.y, { button: 'right', buttons: 2 }); await this.mouse('mouseReleased', h.x, h.y, { button: 'right' }); }
        else { await this.clickAt(h.x, h.y); if (v === 'dblclick') { await this.mouse('mousePressed', h.x, h.y, { clickCount: 2 }); await this.mouse('mouseReleased', h.x, h.y, { clickCount: 2 }); } }
        break;
      }
      case 'hover': { const p = await this.point(a, st.nth); await this.mouse('mouseMoved', p.x, p.y); await sleep(250); break; }
      case 'drag': {
        const p1 = await this.point(a, st.nth); const p2 = await this.point(st.to, st.toNth);
        await this.mouse('mouseMoved', p1.x, p1.y); await this.mouse('mousePressed', p1.x, p1.y);
        for (let i = 1; i <= 12; i++) { await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: p1.x + (p2.x - p1.x) * i / 12, y: p1.y + (p2.y - p1.y) * i / 12, button: 'left', buttons: 1, pointerType: 'mouse' }, this.sid); await sleep(16); }
        await this.mouse('mouseReleased', p2.x, p2.y); break;
      }
      case 'wheel': { const p = await this.point(a, st.nth); await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: p.x, y: p.y, deltaX: st.dx || 0, deltaY: st.dy ?? 300, modifiers: st.shift ? 8 : 0 }, this.sid); await sleep(150); break; }
      case 'key': for (let i = 0; i < (st.repeat || 1); i++) await this.key(a); break;
      case 'type': await send('Input.insertText', { text: a }, this.sid); break;
      case 'select': {
        // 下拉框选值（按 option 的 value，找不到再按显示文字），触发 change 让 Blazor 的 @bind/@onchange 收到
        const h = await this.locate(a, st.nth || 0, st.timeout ?? 5000); rec.target = h.desc;
        const r = await this.ev(`(() => { const el = window.__pt.findAll(${JSON.stringify(a)})[${st.nth || 0}];
          const o = [...el.options].find(o => o.value === ${JSON.stringify(st.value)}) || [...el.options].find(o => o.text.includes(${JSON.stringify(st.value)}));
          if (!o) return 'no'; el.value = o.value; el.dispatchEvent(new Event('change', { bubbles: true })); return o.value; })()`);
        if (r === 'no') throw new Error(`${a} 没有选项 ${st.value}`);
        break;
      }
      case 'wait':
        if (typeof a === 'number') await sleep(a);
        else await this.locate(a, st.nth || 0, st.timeout ?? 8000, false);
        break;
      case 'waitGone': await this.waitFor(async () => !(await this.pt(`locate(${JSON.stringify(a)}, 0, false)`)).found, st.timeout ?? 8000, `${a} 一直没消失`); break;
      case 'expectText': case 'expectNoText': {
        const texts = Array.isArray(a) ? a : [a]; const want = v === 'expectText';
        await this.waitFor(async () => {
          const body = await this.pt(`text(${JSON.stringify(st.in || '')})`);
          const bad = texts.filter(t => body.includes(t) !== want);
          return bad.length ? `${want ? '没出现' : '仍出现'}：${bad.join('｜')}${st.in ? '（范围 ' + st.in + '）' : ''}` : true;
        }, st.timeout ?? (want ? 4000 : 1500));
        break;
      }
      case 'expect': {
        await this.waitFor(async () => {
          const all = await this.ev(`(window.__pt.findAll(${JSON.stringify(a)})).map(e => ({ t: e.innerText || e.value || '', d: !!(e.disabled || e.closest('[disabled],[inert],[aria-disabled=true]')) }))`);
          if (st.absent) return all.length === 0 || `${a} 应不存在，实有 ${all.length} 个`;
          if (st.count !== undefined && all.length !== st.count) return `${a} 应有 ${st.count} 个，实有 ${all.length} 个`;
          if (!all.length) return `找不到 ${a}`;
          const e = all[st.nth || 0]; if (!e) return `${a} 没有第 ${st.nth} 个`;
          if (st.disabled !== undefined && e.d !== st.disabled) return `${a} 应${st.disabled ? '' : '不'}禁用`;
          if (st.text !== undefined && !e.t.includes(st.text)) return `${a} 文字应含「${st.text}」，实为「${e.t.slice(0, 80)}」`;
          if (st.textIs !== undefined && e.t.trim() !== st.textIs) return `${a} 文字应为「${st.textIs}」，实为「${e.t.slice(0, 80)}」`;
          if (st.inView) { const h = await this.locate(a, st.nth || 0, 1000, false); if (!h.inView || !h.full || h.covered) return `${a} 不完整可见：${h.covered ? '被 ' + h.coveredBy + ' 盖住' : '超出视口'}（不滚动）`; }
          return true;
        }, st.timeout ?? 4000);
        break;
      }
      case 'eval': {
        const val = await this.ev(`(async () => (${a}))()`); rec.value = val;
        if ('equals' in st) { if (JSON.stringify(val) !== JSON.stringify(st.equals)) throw new Error(`eval 结果 ${JSON.stringify(val)}，应为 ${JSON.stringify(st.equals)}`); }
        else if (!st.noAssert && !val) throw new Error(`eval 为假：${a}`);
        break;
      }
      case 'shot': rec.shot = await this.shot(a, st); break;
      case 'dump': {
        const d = await this.pt('dump()');
        const f = join(this.runDir, 'dumps', `${sc.name}@${this.size}-${a}.txt`); mkdirSync(dirname(f), { recursive: true });
        writeFileSync(f, d.items.map(i => `${i.pt ? '[' + i.pt + '] ' : ''}<${i.tag}> ${i.text} @${i.x},${i.y}${i.disabled ? ' 禁用' : ''}${i.offscreen ? ' 视口外' : ''}${i.covered ? ' 被盖住' : ''}`).join('\n') + '\n\n──── 页面文字 ────\n' + d.text);
        rec.dump = relative(this.runDir, f); break;
      }
      case 'note': break;
    }
    return rec;
  }
  async loadSave(name) {
    const f = join(FIXTURES, name.endsWith('.json') ? name : name + '.json');
    if (!existsSync(f)) throw new Error('找不到存档 fixture ' + name);
    const { storage } = JSON.parse(readFileSync(f, 'utf8'));
    // 先停在同源的一个不存在的静态文件上（不启动游戏），写好 localStorage 再进游戏
    const cur = await this.ev('location.origin').catch(() => '');
    if (cur !== new URL(opt.base).origin) { await send('Page.navigate', { url: opt.base + '/__pt_blank.txt' }, this.sid); await sleep(300); }
    await this.ev(`(() => { for (const k of Object.keys(localStorage)) if (k.startsWith('survival.')) localStorage.removeItem(k);
      const s = ${JSON.stringify(storage)}; for (const k in s) localStorage.setItem(k, s[k]); return true; })()`);
    await this.navigate(opt.base + (this.sc.url || '/?dev=1'));
  }
  async shot(name, st) {
    await this.ev(`(() => { const s = document.createElement('style'); s.id = '__pt_freeze'; s.textContent = ${JSON.stringify(FREEZE_CSS)}; document.head.appendChild(s); return true; })()`);
    if (st.mask) await this.ev(`(() => { for (const spec of ${JSON.stringify(st.mask)}) for (const el of window.__pt.findAll(spec)) { el.style.setProperty('visibility', 'hidden', 'important'); el.setAttribute('data-pt-masked', ''); } return true; })()`);
    // 鼠标挪到左上角（避免上一步点击留下的悬停态让截图时有时无），等动画收尾
    if (st.park !== false) await this.mouse('mouseMoved', 0, 0);
    await this.settle(st.settle ?? 400);
    const p = {};
    if (st.clip) { await this.locate(st.clip, 0, 3000, false); p.clip = await this.ev(`(() => { const r = window.__pt.findAll(${JSON.stringify(st.clip)})[0].getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height, scale: 1 }; })()`); }
    const r = await send('Page.captureScreenshot', { format: 'png', ...p }, this.sid);
    await this.ev(`(() => { document.getElementById('__pt_freeze')?.remove(); return true; })()`);
    // 按标记恢复：findAll 只认可见元素，隐藏后再按选择器找会找不到（B56 并行试用发现）
    if (st.mask) await this.ev(`(() => { for (const el of document.querySelectorAll('[data-pt-masked]')) { el.style.removeProperty('visibility'); el.removeAttribute('data-pt-masked'); } return true; })()`);
    const rel = join(this.sc.name, this.size, name + '.png');
    const cur = join(this.runDir, 'shots', rel); mkdirSync(dirname(cur), { recursive: true });
    const buf = Buffer.from(r.data, 'base64'); writeFileSync(cur, buf);
    const base = join(opt.out, 'baseline', rel); const out = { file: relative(this.runDir, cur) };
    if (opt.updateBaseline || !existsSync(base)) { mkdirSync(dirname(base), { recursive: true }); copyFileSync(cur, base); out.baseline = opt.updateBaseline ? 'updated' : 'new'; return out; }
    const d = diffPng(decodePng(readFileSync(base)), decodePng(buf), st.tol ?? 24);
    out.ratio = Number(d.ratio.toFixed(5)); out.box = d.box;
    const limit = st.threshold ?? this.sc.shotThreshold ?? 0.002;
    if (d.sizeChanged || d.ratio > limit) {
      const df = join(this.runDir, 'diffs', rel); mkdirSync(dirname(df), { recursive: true });
      if (d.image) writeFileSync(df, encodePng(d.image));
      out.diff = relative(this.runDir, df);
      this.issue('截图与基线不同', 'warn', name, d.sizeChanged ? '尺寸变了' : `变化 ${(d.ratio * 100).toFixed(2)}%，区域 ${JSON.stringify(d.box)}，差异图 ${out.diff}（基线 ${relative(this.runDir, base)}）`, -1);
    }
    return out;
  }
  async autoCheck(no, st) {
    if (st.checks === false || this.sc.checks === false) return;
    const errs = this.errs.splice(0);
    const allow = (this.sc.allowErrors || []).map(s => new RegExp(s));
    for (const e of errs) if (!allow.some(r => r.test(e))) this.issue('控制台错误', 'error', e.slice(0, 120), e, no);
    let c; try { c = await this.pt(`check(${JSON.stringify(this.sc.watch || [])}, ${this.sc.minFont ?? 12})`); } catch (e) { return; }
    if (c.errorUi) this.issue('Blazor 错误条', 'error', 'blazor-error-ui', '页面底部出现 Blazor 未处理异常提示条', no);
    for (const o of c.overflow) this.issue('横向溢出', 'error', o.el, `${o.el} 超出视口（left ${o.left}，right ${o.right}，视口宽 ${this.w}）`, no);
    for (const wv of c.watch) this.issue('关键元素不可见', 'error', wv.spec + '|' + wv.desc,
      `${wv.desc}（${wv.spec}）${wv.covered ? '被 ' + wv.coveredBy + ' 盖住' : wv.inView ? '只露出一部分' : '在视口外'} @${wv.x},${wv.y} ${wv.w}×${wv.h}`, no);
    for (const f of c.smallFonts) this.issue('字号过小', 'warn', f.el, `${f.el} 字号 ${f.size}px（下限 ${this.sc.minFont ?? 12}px）「${f.text}」`, no);
    for (const f of c.clipped) this.issue('文字被截断', 'warn', f.el, `${f.el} 内容宽 ${f.scrollWidth} > 可见宽 ${f.clientWidth}，且没有省略号`, no);
  }

  async run() {
    const t0 = Date.now(); const sc = this.sc; let failed = null;
    await this.open();
    try {
      // 起点：有 fixture 就先读档，否则直接开网址（新浏览器上下文，存档为空）
      if (sc.fixture) await this.loadSave(sc.fixture); else await this.navigate(opt.base + (sc.url || '/?dev=1'));
      this.errs.length = 0;
      const steps = expand(sc.steps);
      for (let i = 0; i < steps.length; i++) {
        const st = steps[i]; const no = i + 1;
        if (st.sizes && !st.sizes.includes(this.size)) continue;
        const ts = Date.now(); let rec;
        try {
          // 对白自动读完：剧情对白随首次行为随时插进来，场景默认不关心，遇到就按空格读完再做这一步（记次数）；
          // 要测对白本身时场景写 "autoDialogue": false，或在这一步写 "dialogue": true
          const verb = Object.keys(st).find(k => VERBS.has(k));
          const talk = !['wait', 'note', 'dump', 'shot', 'saveFixture', 'open', 'reload', 'loadSave'].includes(verb);
          if (talk) await this.dismissDialogue(st);
          if (st.while || st.until) {
            // 重复本步：while 指的元素还在就继续／until 指的元素出现就停（最多 max 次，默认 50）
            const spec = st.while || st.until, want = !!st.while;
            const go = async () => (await this.pt(`locate(${JSON.stringify(spec)}, 0, false)`)).found === want;
            let n = 0; const max = st.max ?? 50;
            while (n < max && await go()) { rec = await this.step(st, no); await this.settle(st.settle ?? 150); n++; }
            if (await go()) throw new Error(`重复 ${max} 次后 ${spec} ${want ? '仍在' : '仍没出现'}`);
            rec = rec || { no, verb }; rec.repeated = n;
          } else rec = await this.step(st, no);
          if (!['wait', 'note', 'dump', 'shot', 'saveFixture', 'expect', 'expectText', 'expectNoText', 'eval'].includes(rec.verb)) await this.settle(st.settle ?? 150);
          if (talk) await this.dismissDialogue(st);
          await this.autoCheck(no, st);
          rec.ok = true;
        } catch (e) {
          rec = rec || { no, verb: Object.keys(st).find(k => VERBS.has(k)), label: st.label };
          rec.ok = false; rec.error = e.message;
          if (st.from) rec.from = st.from;
          const f = join(this.runDir, 'fail', `${sc.name}@${this.size}-step${no}.png`); mkdirSync(dirname(f), { recursive: true });
          try { const r = await send('Page.captureScreenshot', { format: 'png' }, this.sid); writeFileSync(f, Buffer.from(r.data, 'base64')); rec.failShot = relative(this.runDir, f); } catch { }
          await this.autoCheck(no, {}).catch(() => { });
          if (st.optional) rec.optional = true;
          else if (!failed) failed = `第 ${no} 步（${rec.label || rec.verb}${rec.arg !== undefined ? ' ' + rec.arg : ''}）：${e.message}`;
        }
        rec.ms = Date.now() - ts; if (st.from) rec.from = st.from; this.steps.push(rec);
        if (failed && !opt.keepGoing) break;
      }
    } catch (e) { failed = failed || '起点失败：' + e.message; }
    finally { await this.close(); }
    const issues = [...this.issues.values()];
    return { scenario: sc.name, file: relative(join(HERE, '..', '..'), sc.file).replace(/\\/g, '/'), size: this.size, ok: !failed && !issues.some(i => i.sev === 'error'),
      failed, ms: Date.now() - t0, autoDialogueLines: this.dialogues || 0, issues, steps: this.steps };
  }
}
const VERBS = new Set(['open', 'reload', 'size', 'loadSave', 'saveFixture', 'click', 'dblclick', 'rclick', 'hover', 'drag', 'wheel', 'key', 'type', 'select', 'wait', 'waitGone', 'expectText', 'expectNoText', 'expect', 'eval', 'shot', 'dump', 'note']);

// ───────────── 主流程 ─────────────
async function main() {
  const files = scenarioFiles();
  const scs = files.map(f => { let sc; try { sc = JSON.parse(readFileSync(f, 'utf8')); } catch (e) { throw new Error(`场景文件 ${f} 不是合法 JSON：${e.message}`); } sc.file = f; sc.name = sc.name || basename(f, '.json'); return sc; });
  for (const sc of scs) expand(sc.steps); // 先展开一遍，步骤库写错尽早报
  try { await fetch(opt.base + '/', { cache: 'no-store' }); } catch { console.error(`${opt.base} 访问不了：先 node tools/playtest/serve.mjs start`); process.exit(1); }
  const d = new Date(), p2 = n => String(n).padStart(2, '0');
  const stamp = `${d.getFullYear()}${p2(d.getMonth() + 1)}${p2(d.getDate())}-${p2(d.getHours())}${p2(d.getMinutes())}${p2(d.getSeconds())}`;
  const runDir = join(opt.out, 'runs', stamp); mkdirSync(runDir, { recursive: true });
  const queue = []; for (const sc of scs) for (const size of (sc.sizes || SIZES).filter(s => SIZES.includes(s) || sc.sizes)) queue.push([sc, size]);
  const t0 = Date.now(); const quit = await launch(); const results = [];
  console.log(`跑 ${scs.length} 个场景 × 分辨率 = ${queue.length} 个任务，并发 ${opt.jobs}，结果目录 ${runDir}`);
  try {
    await Promise.all(Array.from({ length: Math.min(opt.jobs, queue.length) }, async () => {
      while (queue.length) {
        const [sc, size] = queue.shift(); const r = await new Job(sc, size, runDir).run(); results.push(r);
        const e = r.issues.filter(i => i.sev === 'error').length, w = r.issues.length - e;
        console.log(`${r.ok ? '通过' : '失败'}  ${sc.name} @${size}  ${(r.ms / 1000).toFixed(1)}s  步骤 ${r.steps.filter(s => s.ok).length}/${r.steps.length}  问题 ${e} 错 ${w} 警${r.failed ? '\n      ' + r.failed : ''}`);
      }
    }));
  } finally { await quit(); }
  results.sort((a, b) => a.scenario.localeCompare(b.scenario) || a.size.localeCompare(b.size, undefined, { numeric: true }));
  const total = Date.now() - t0;
  const summary = { base: opt.base, startedAt: stamp, totalMs: total, jobs: results.length, passed: results.filter(r => r.ok).length, results };
  writeFileSync(join(runDir, 'results.json'), JSON.stringify(summary, null, 1));
  // 给人／代理看的摘要
  const lines = [`试玩场景回归 ${stamp}  ${opt.base}  用时 ${(total / 1000).toFixed(1)} 秒  通过 ${summary.passed}/${summary.jobs}`, ''];
  for (const r of results) {
    lines.push(`${r.ok ? '✔' : '✘'} ${r.scenario} @${r.size}  ${(r.ms / 1000).toFixed(1)}s`);
    if (r.failed) lines.push(`    失败：${r.failed}`);
    for (const i of r.issues) lines.push(`    [${i.sev === 'error' ? '错' : '警'}] ${i.kind}：${i.detail}${i.firstStep > 0 ? `（首见第 ${i.firstStep} 步${i.count > 1 ? '，共 ' + i.count + ' 次' : ''}）` : ''}`);
  }
  writeFileSync(join(runDir, 'summary.txt'), lines.join('\n') + '\n');
  console.log('\n' + lines.join('\n'));
  console.log(`\n结果：${join(runDir, 'results.json')}`);
  process.exitCode = summary.passed === summary.jobs ? 0 : 1;
}
main().catch(e => { console.error('失败：' + (e.stack || e.message)); process.exit(1); });
