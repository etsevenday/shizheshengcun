#!/usr/bin/env node
// 无头 Chrome 试玩驱动（零依赖：Node 自带 WebSocket + Chrome DevTools Protocol）。
// 用于无人值守回合：内置浏览器的 preview_start 需要人点确认，这个工具不需要。
// 每条命令连上同一个常驻的无头 Chrome 执行一步就退出，页面状态（含 localStorage）在命令之间保留。
//
//   node tools/playtest/cdp.mjs start [宽 高]          启动常驻无头 Chrome（默认 1280 720）
//   node tools/playtest/cdp.mjs stop                   关闭
//   node tools/playtest/cdp.mjs open <url>             打开网址并等待加载（Blazor 首次约 10MB）
//   node tools/playtest/cdp.mjs reload                 刷新（忽略缓存）
//   node tools/playtest/cdp.mjs size <宽> <高>          改视口
//   node tools/playtest/cdp.mjs text [CSS选择器]        取可见文本（默认 body）
//   node tools/playtest/cdp.mjs find <文字>            列出含该文字的可见元素及中心坐标
//   node tools/playtest/cdp.mjs clicktext <文字> [序号] 点击含该文字的第 N 个（默认 0）最小可见元素
//   node tools/playtest/cdp.mjs click <x> <y>          在坐标点击
//   node tools/playtest/cdp.mjs drag <x1> <y1> <x2> <y2>  按住拖动后松开
//   node tools/playtest/cdp.mjs hover <x> <y>
//   node tools/playtest/cdp.mjs wheel <x> <y> <dx> <dy> [shift]
//   node tools/playtest/cdp.mjs key <键名>              如 Escape、Enter、m、1
//   node tools/playtest/cdp.mjs eval <JS表达式>         在页面执行（可 await），打印 JSON 结果
//   node tools/playtest/cdp.mjs shot <文件.png>         截图
//   node tools/playtest/cdp.mjs errs                   打印页面控制台错误与未捕获异常
//   node tools/playtest/cdp.mjs wait <毫秒>
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const PORT = Number(process.env.PLAYTEST_PORT || 9333);
const DIR = join(tmpdir(), 'survival-playtest');
const CHROMES = [
  process.env.CHROME,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
].filter(Boolean);

const sleep = ms => new Promise(r => setTimeout(r, ms));
const [cmd, ...args] = process.argv.slice(2);

// 页面里装一个错误收集钩子（每次加载新文档都会注入）
const HOOK = `(() => { if (window.__pt) return; window.__pt = { errs: [] };
  const push = (k, m) => window.__pt.errs.push(k + ': ' + String(m).slice(0, 500));
  const ce = console.error; console.error = (...a) => { push('console.error', a.map(String).join(' ')); ce.apply(console, a); };
  addEventListener('error', e => push('error', e.message));
  addEventListener('unhandledrejection', e => push('rejection', e.reason && (e.reason.stack || e.reason))); })();`;

async function pageWs() {
  const list = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
  const p = list.find(t => t.type === 'page');
  if (!p) throw new Error('没有页面，先 start');
  return p.webSocketDebuggerUrl;
}

async function session() {
  const ws = new WebSocket(await pageWs());
  await new Promise((ok, bad) => { ws.onopen = ok; ws.onerror = bad; });
  let id = 0; const wait = new Map(); const events = [];
  ws.onmessage = m => {
    const d = JSON.parse(m.data);
    if (d.id && wait.has(d.id)) { const w = wait.get(d.id); wait.delete(d.id); d.error ? w.bad(new Error(d.error.message)) : w.ok(d.result); }
    else if (d.method) events.push(d);
  };
  const send = (method, params = {}) => new Promise((ok, bad) => { const i = ++id; wait.set(i, { ok, bad }); ws.send(JSON.stringify({ id: i, method, params })); });
  return { send, events, close: () => ws.close() };
}

async function evaluate(s, expr) {
  const r = await s.send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true, userGesture: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
  return r.result.value;
}

async function mouse(s, type, x, y, extra = {}) {
  await s.send('Input.dispatchMouseEvent', { type, x, y, button: 'left', buttons: type === 'mouseReleased' ? 0 : 1, clickCount: 1, pointerType: 'mouse', ...extra });
}

async function click(s, x, y) {
  await s.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, buttons: 0 });
  await mouse(s, 'mousePressed', x, y);
  await mouse(s, 'mouseReleased', x, y);
}

// 找含文字的最小可见元素（按钮、链接优先）
const FIND = t => `(() => { const t = ${JSON.stringify(t)}; const out = [];
  for (const el of document.querySelectorAll('body *')) {
    const s = (el.innerText || el.getAttribute('aria-label') || el.title || '').trim();
    if (!s.includes(t)) continue;
    const r = el.getBoundingClientRect(); if (r.width < 1 || r.height < 1) continue;
    const st = getComputedStyle(el); if (st.visibility === 'hidden' || st.display === 'none') continue;
    if ([...el.children].some(c => (c.innerText || '').includes(t) && c.getBoundingClientRect().width > 0)) continue;
    const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
    out.push({ tag: el.tagName.toLowerCase(), cls: String(el.className).slice(0, 60), text: s.slice(0, 80),
      x: Math.round(r.x + r.width / 2), y: Math.round(r.y + r.height / 2), w: Math.round(r.width), h: Math.round(r.height),
      disabled: !!el.closest('[disabled],[inert]'), covered: !!hit && hit !== el && !el.contains(hit) });
  }
  out.sort((a, b) => (b.tag === 'button') - (a.tag === 'button'));
  return out.slice(0, 20); })()`;

async function waitReady(s, ms = 60000) {
  const end = Date.now() + ms;
  while (Date.now() < end) {
    try {
      // index.html 的载入占位是 #app > .boot，Blazor 渲染后被替换
      const ok = await evaluate(s, `document.readyState === 'complete' && !!document.querySelector('#app') && !document.querySelector('#app > .boot') && (document.querySelector('#app').innerText || '').trim().length > 0`);
      if (ok) return true;
    } catch { }
    await sleep(500);
  }
  return false;
}

async function main() {
  if (cmd === 'start') {
    const [w = 1280, h = 720] = args.map(Number);
    try { await pageWs(); console.log('已在运行'); return; } catch { }
    const exe = CHROMES.find(existsSync); if (!exe) throw new Error('找不到 Chrome/Edge');
    mkdirSync(DIR, { recursive: true });
    const p = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${join(DIR, 'profile')}`,
      `--window-size=${w},${h}`, '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'],
      { detached: true, stdio: 'ignore' });
    p.unref(); writeFileSync(join(DIR, 'pid'), String(p.pid));
    for (let i = 0; i < 40; i++) { try { await pageWs(); break; } catch { await sleep(250); } }
    const s = await session();
    await s.send('Page.enable');
    writeFileSync(join(DIR, 'size'), `${w} ${h}`);
    s.close(); console.log(`已启动 无头 Chrome 端口 ${PORT}，视口 ${w}×${h}`); return;
  }
  if (cmd === 'stop') {
    try { const s = await session(); await s.send('Browser.close').catch(() => { }); s.close(); } catch { }
    const pf = join(DIR, 'pid'); if (existsSync(pf)) { try { process.kill(Number(readFileSync(pf, 'utf8'))); } catch { } }
    console.log('已关闭'); return;
  }
  if (cmd === 'wait') { await sleep(Number(args[0] || 1000)); console.log('ok'); return; }

  const s = await session();
  try {
    await s.send('Page.enable');
    // 视口覆盖随 DevTools 连接断开而失效，每条命令都按记下的尺寸重设
    const sf = join(DIR, 'size');
    const [vw, vh] = (existsSync(sf) ? readFileSync(sf, 'utf8') : '1280 720').split(' ').map(Number);
    await s.send('Emulation.setDeviceMetricsOverride', { width: vw, height: vh, deviceScaleFactor: 1, mobile: false });
    await s.send('Page.addScriptToEvaluateOnNewDocument', { source: HOOK });
    await evaluate(s, HOOK).catch(() => { });
    switch (cmd) {
      case 'open': {
        await s.send('Page.navigate', { url: args[0] });
        await sleep(500); console.log(await waitReady(s) ? '已加载' : '加载超时'); break;
      }
      case 'reload': {
        await s.send('Page.reload', { ignoreCache: true });
        await sleep(500); console.log(await waitReady(s) ? '已加载' : '加载超时'); break;
      }
      case 'size': {
        const [w, h] = args.map(Number);
        writeFileSync(join(DIR, 'size'), `${w} ${h}`);
        await s.send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
        await sleep(300); console.log(`视口 ${w}×${h}`); break;
      }
      case 'text': console.log(await evaluate(s, `(document.querySelector(${JSON.stringify(args[0] || 'body')})||{}).innerText||''`)); break;
      case 'find': console.log(JSON.stringify(await evaluate(s, FIND(args.join(' '))), null, 1)); break;
      case 'clicktext': {
        const hits = await evaluate(s, FIND(args[0])); const h = hits[Number(args[1] || 0)];
        if (!h) { console.log('没找到：' + args[0]); process.exitCode = 1; break; }
        await click(s, h.x, h.y); await sleep(200);
        console.log(`已点击 ${h.tag} "${h.text}" @${h.x},${h.y}${h.disabled ? '（禁用）' : ''}${h.covered ? '（被遮挡）' : ''}`); break;
      }
      case 'click': { const [x, y] = args.map(Number); await click(s, x, y); await sleep(200); console.log('ok'); break; }
      case 'hover': { const [x, y] = args.map(Number); await s.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, buttons: 0 }); await sleep(300); console.log('ok'); break; }
      case 'drag': {
        const [x1, y1, x2, y2] = args.map(Number);
        await s.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: x1, y: y1, buttons: 0 });
        await mouse(s, 'mousePressed', x1, y1);
        for (let i = 1; i <= 12; i++) { await mouse(s, 'mouseMoved', x1 + (x2 - x1) * i / 12, y1 + (y2 - y1) * i / 12); await sleep(16); }
        await mouse(s, 'mouseReleased', x2, y2); await sleep(200); console.log('ok'); break;
      }
      case 'wheel': {
        const [x, y, dx, dy] = args.map(Number);
        await s.send('Input.dispatchMouseEvent', { type: 'mouseWheel', x, y, deltaX: dx, deltaY: dy, modifiers: args[4] === 'shift' ? 8 : 0 });
        await sleep(200); console.log('ok'); break;
      }
      case 'key': {
        const k = args[0]; const one = k.length === 1;
        const code = one ? (/[a-z]/i.test(k) ? 'Key' + k.toUpperCase() : /\d/.test(k) ? 'Digit' + k : k) : k;
        const vk = { Escape: 27, Enter: 13, Tab: 9, Backspace: 8, ArrowUp: 38, ArrowDown: 40, ArrowLeft: 37, ArrowRight: 39 }[k] ?? (one ? k.toUpperCase().charCodeAt(0) : 0);
        await s.send('Input.dispatchKeyEvent', { type: 'keyDown', key: k, code, windowsVirtualKeyCode: vk, text: one ? k : undefined });
        await s.send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code, windowsVirtualKeyCode: vk });
        await sleep(200); console.log('ok'); break;
      }
      case 'eval': console.log(JSON.stringify(await evaluate(s, args.join(' ')), null, 1)); break;
      case 'shot': {
        const r = await s.send('Page.captureScreenshot', { format: 'png' });
        writeFileSync(args[0], Buffer.from(r.data, 'base64')); console.log('已保存 ' + args[0]); break;
      }
      case 'errs': console.log(JSON.stringify(await evaluate(s, `(window.__pt && window.__pt.errs) || []`), null, 1)); break;
      default: console.log('未知命令，见文件头注释'); process.exitCode = 2;
    }
  } finally { s.close(); }
}
main().catch(e => { console.error('失败：' + e.message); process.exit(1); });
