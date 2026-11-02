#!/usr/bin/env node
// 开发计划 v0.9 批次 B12（R36—R38）：分层地图、资源四账卡面与相邻栏叠卡的三档分辨率实机检查。
// 零依赖：Node 18+ 自带 fetch/WebSocket + Chrome DevTools Protocol（CDP 帮助函数沿用 verify-ui-v16.cjs）。
//
// 用法：
//   node tools/verify-ui-v17.cjs [网址] [输出目录]
//   网址默认线上 https://etsevenday.github.io/shizheshengcun/?dev=1；本地可用 http://localhost:5180/?dev=1
//   输出默认系统临时目录下 survival-v17；环境变量：CHROME、PLAYTEST_PORT（默认 9339）、SIZES=1280x720,1600x900,1920x1080
//
// 路线（全部经 ?dev=1 开发入口“传送（按实际到达处理）”“当前地点探索度→100%”）：新局读完开场 → 旧公路/检查哨/果园/灌溉渠/水厂/朝圣路/
//   近郊农场探索到 100%，再到营门/营区工坊/营区广场（发现 20 处以上）→ 每档尺寸：
//   1. 营区广场相邻栏：10 条未开通（9 条同因）收成一张叠卡、相邻栏无横向溢出；叠卡“逐条查看”浮窗按条件列出 10 条。
//   2. 打开地图（M）：第一次为默认城内层、面包屑三段，之后恢复上次看的层；地图面板内可见文字 ≥14px；工具条与页面无横向溢出；切主世界、放大、总览（总览不画字）、
//      回到当前位置、图例（含单向出口），前后存档不变（浏览不改存档）。
//   3. 资源卡：果园“12/12”日补点、地牢普通箱“本代剩余2/2”、在地牢里默认地牢层；地牢核心“可领取”→领取后“已领取”；开发入口地牢换代到第 2 代。
// 结果写 <输出目录>/v17-results.json，截图写 <输出目录>/screenshots/。
'use strict';
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

const url = process.argv[2] || 'https://etsevenday.github.io/shizheshengcun/?dev=1';
const out = path.resolve(process.argv[3] || path.join(os.tmpdir(), 'survival-v17'));
const PORT = Number(process.env.PLAYTEST_PORT || 9339);
const SAVE_KEY = 'survival.save.v1';
const SIZES = (process.env.SIZES || '1280x720,1600x900,1920x1080').split(',').map(s => s.split('x').map(Number));
const CHROMES = [process.env.CHROME, 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].filter(Boolean);
const sleep = ms => new Promise(r => setTimeout(r, ms));

// ───────────── CDP ─────────────
let ws, msgId = 0; const waiters = new Map();
function send(method, params = {}) {
  return new Promise((ok, bad) => { const i = ++msgId; waiters.set(i, { ok, bad }); ws.send(JSON.stringify({ id: i, method, params })); });
}
async function connect() {
  for (let i = 0; i < 60; i++) {
    try {
      const list = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
      const p = list.find(t => t.type === 'page');
      if (p) {
        ws = new WebSocket(p.webSocketDebuggerUrl);
        await new Promise((ok, bad) => { ws.onopen = ok; ws.onerror = bad; });
        ws.onmessage = m => { const d = JSON.parse(m.data); const w = d.id && waiters.get(d.id); if (w) { waiters.delete(d.id); d.error ? w.bad(new Error(d.error.message)) : w.ok(d.result); } };
        return;
      }
    } catch { }
    await sleep(250);
  }
  throw new Error('连不上无头浏览器');
}
async function ev(expr) {
  const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true, userGesture: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
  return r.result.value;
}
async function size(w, h) { await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false }); await sleep(600); }
async function mouseAt(x, y, type = 'mouseMoved') {
  await send('Input.dispatchMouseEvent', { type, x, y, button: type === 'mouseMoved' ? 'none' : 'left', buttons: type === 'mousePressed' ? 1 : 0, clickCount: 1, pointerType: 'mouse' });
}
async function clickAt(x, y) { await mouseAt(x, y); await mouseAt(x, y, 'mousePressed'); await mouseAt(x, y, 'mouseReleased'); await sleep(250); }
async function centerOf(sel, index = 0) {
  return ev(`(() => { const es = [...document.querySelectorAll(${JSON.stringify(sel)})].filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0; });
    const e = es[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
}
async function clickSel(sel, index = 0) { const r = await centerOf(sel, index); if (!r) return false; await clickAt(r.x, r.y); return true; }
async function hoverSel(sel, index = 0) { const r = await centerOf(sel, index); if (!r) return false; await mouseAt(r.x, r.y); await sleep(300); return true; }
/** 点含文字的按钮（DOM click，滚动容器里看不见的开发入口按钮也能点）。 */
async function domClick(text) {
  return ev(`(() => { const b = [...document.querySelectorAll('button')].find(x => (x.innerText || '').includes(${JSON.stringify(text)}) && !x.disabled); if (!b) return false; b.scrollIntoView({ block: 'center' }); b.click(); return true; })()`);
}
async function key(k) {
  const one = k.length === 1;
  const code = one ? (/[0-9]/.test(k) ? 'Digit' + k : 'Key' + k.toUpperCase()) : k;
  const vk = { Escape: 27, Enter: 13, ArrowLeft: 37, ArrowRight: 39 }[k] ?? (one ? k.toUpperCase().charCodeAt(0) : 0);
  await send('Input.dispatchKeyEvent', { type: 'keyDown', key: k, code, windowsVirtualKeyCode: vk, text: one ? k : undefined });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code, windowsVirtualKeyCode: vk });
  await sleep(300);
}
async function shot(name) {
  const r = await send('Page.captureScreenshot', { format: 'png' });
  fs.writeFileSync(path.join(out, 'screenshots', name + '.png'), Buffer.from(r.data, 'base64'));
  return path.join('screenshots', name + '.png');
}
async function waitFor(expr, ms = 60000) {
  const end = Date.now() + ms;
  while (Date.now() < end) { try { if (await ev(expr)) return true; } catch { } await sleep(300); }
  return false;
}
async function readDialogues(max = 40) {
  for (let i = 0; i < max; i++) {
    if (!await ev(`!!document.querySelector('.dlg-layer')`)) return true;
    if (!await domClick('结束对话') && !await domClick('继续')) await sleep(300);
    await sleep(200);
  }
  return !await ev(`!!document.querySelector('.dlg-layer')`);
}
async function devButton(text) {
  if (!await ev(`!!document.querySelector('.dev-box')`)) { await domClick('菜单'); await sleep(400); }
  const ok = await domClick(text); await sleep(400);
  return ok;
}
async function closeMenu() { if (await ev(`!!document.querySelector('.dev-box')`)) await key('Escape'); }

// ───────────── 量测 ─────────────

async function teleport(id) {
  if (!await ev(`!!document.querySelector('.dev-box')`)) { await domClick('菜单'); await sleep(400); }
  const ok = await ev(`(() => { const s = document.querySelector('select[aria-label="传送目标"]'); if (!s) return false; s.value = ${JSON.stringify(id)}; s.dispatchEvent(new Event('change', { bubbles: true })); return true; })()`);
  await sleep(200);
  await domClick('传送（按实际到达处理）'); await sleep(300);
  return ok;
}
async function explore100() { await devButton('当前地点探索度→100%'); }
async function saveText() { return ev(`localStorage.getItem(${JSON.stringify(SAVE_KEY)})`); }
async function closeMap() {
  if (await ev(`!!document.querySelector('.map-panel')`)) await ev(`(() => { const b = [...document.querySelectorAll('.map-tools button')].find(x => x.innerText.includes('关闭')); if (b) b.click(); })()`);
  await sleep(300);
}
const MAPCHECK = `(() => {
  const panel = document.querySelector('.map-panel'); if (!panel) return null;
  const small = [];
  for (const el of panel.querySelectorAll('*')) {
    if (!el.childNodes || ![...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim())) continue;
    const r = el.getBoundingClientRect(); if (r.width === 0 || r.height === 0) continue;
    const fs = parseFloat(getComputedStyle(el).fontSize); if (fs < 14) small.push({ t: el.textContent.trim().slice(0, 20), fs });
  }
  const bar = panel.querySelector('.map-bar');
  return { small: small.slice(0, 10), smallCount: small.length, barOverflow: bar.scrollWidth > bar.clientWidth + 1,
    pageOverflow: document.documentElement.scrollWidth > innerWidth + 1, crumbs: [...panel.querySelectorAll('.map-crumbs .crumb')].map(c => c.textContent.trim()),
    texts: panel.querySelectorAll('svg.map-canvas text').length, nodes: panel.querySelectorAll('.map-node').length,
    layer: panel.querySelector('.map-layer') ? panel.querySelector('.map-layer').value : null,
    vp: (() => { const v = panel.querySelector('.map-viewport'); const b = v.getBoundingClientRect(); return { w: b.width, h: b.height }; })() };
})()`;
const cardTexts = `[...document.querySelectorAll('.res-card')].map(c => c.innerText.replace(/\\s+/g, ' '))`;

(async () => {
  fs.mkdirSync(path.join(out, 'screenshots'), { recursive: true });
  const exe = CHROMES.find(p => fs.existsSync(p));
  if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'survival-v17-'));
  const chrome = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${prof}`, '--window-size=1920,1080',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
  const report = { url, startedAt: new Date().toISOString(), checks: [] };
  const save = () => fs.writeFileSync(path.join(out, 'v17-results.json'), JSON.stringify(report, null, 2));
  let tag = '';
  let failed = false;
  const check = async (name, pass, detail) => {
    const file = await shot(`${tag}-${name.replace(/[\\/:*?"<>|\s×]+/g, '_').slice(0, 40)}`);
    report.checks.push({ case: tag, name, pass: !!pass, detail: detail ?? null, screenshot: file });
    if (!pass) failed = true;
    console.log(`${pass ? 'PASS' : 'FAIL'} ${tag} ${name}${pass ? '' : '：' + JSON.stringify(detail).slice(0, 900)}`);
    save();
  };
  try {
    await connect();
    await send('Page.enable'); await send('Runtime.enable');
    await size(1600, 900);
    await send('Page.navigate', { url: new URL('icon-192.png', url).href }); await sleep(800);
    await ev(`localStorage.removeItem(${JSON.stringify(SAVE_KEY)})`);
    await send('Page.navigate', { url }); await sleep(800);
    if (!await waitFor(`!!document.querySelector('.title-screen') || !!document.querySelector('.game')`, 90000)) throw new Error('页面加载超时');
    await domClick('新游戏');
    if (!await waitFor(`!!document.querySelector('.game .center-scroll')`, 30000)) throw new Error('没进入主界面');
    await readDialogues();
    for (const id of ['m01_road', 'm02_checkpoint', 'w11_orchard', 'w12_canal', 'w33_waterworks', 'c01_pilgrim', 'w01_farm']) { await teleport(id); await explore100(); }
    for (const id of ['h01_gate', 'h03_workshop', 'h02_square']) await teleport(id);
    await closeMenu();
    const known = await ev(`(() => { const s = JSON.parse(localStorage.getItem(${JSON.stringify(SAVE_KEY)})); return Object.values(s.Locations).filter(l => l.Discovered).length; })()`);
    tag = 'setup'; await check('已发现地点 ≥20', known >= 20, { known });

    for (const [w, h] of SIZES) {
      await size(w, h); tag = `${w}x${h}`;
      // 1. 相邻栏叠卡
      const adj = await ev(`(() => { const a = document.querySelector('.map-adjacent'); return { sw: a.scrollWidth, cw: a.clientWidth, stacks: a.querySelectorAll('.lock-stack').length, cards: a.children.length }; })()`);
      await check('营区广场相邻栏未开通收成一张叠卡、无横向溢出', adj.stacks === 1 && adj.cards === 1 && adj.sw <= adj.cw + 1, adj);
      await domClick('逐条查看'); await sleep(400);
      const list = await ev(`document.querySelectorAll('.apop.lockstack .lockstack-list li').length`);
      await check('叠卡浮窗按条件逐条列出 10 条', list === 10, { list });
      await key('Escape');
      // 2. 分层地图
      const before = await saveText();
      await key('m'); await sleep(700);
      let m = await ev(MAPCHECK);
      // 第一次打开为默认城内层；之后当前位置没变，恢复上次看的主世界层（阅读状态，EXUI13）
      const first = w === SIZES[0][0] && h === SIZES[0][1];
      await check(first ? '默认城内层、面包屑三段' : '阅读状态恢复（主世界层）', m && (first ? m.layer === 'city:h02_square' && m.crumbs.length === 3 : m.layer === 'world'), m);
      await check('地图面板可见文字≥14px、工具条与页面无横向溢出', m && m.smallCount === 0 && !m.barOverflow && !m.pageOverflow, m);
      await ev(`(() => { const s = document.querySelector('.map-layer'); s.value = 'world'; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(600);
      m = await ev(MAPCHECK);
      await check('切到主世界：只切查看', m && m.layer === 'world' && m.crumbs[0] === '主世界' && m.smallCount === 0, m);
      await clickSel('.map-tools button[title^="放大"]'); await sleep(500);
      m = await ev(MAPCHECK);
      await check('放大一档后文字仍≥14px', m && m.smallCount === 0, m);
      await domClick('总览'); await sleep(500);
      m = await ev(MAPCHECK);
      await check('总览只画点、不画字', m && m.texts === 0 && m.nodes >= 10, m);
      await domClick('总览'); await sleep(300);
      await domClick('回到当前位置'); await sleep(400);
      await domClick('图例'); await sleep(300);
      const legend = await ev(`(() => { const l = document.querySelector('.map-legend-pop'); return l ? { text: l.innerText.includes('单向出口'), fs: parseFloat(getComputedStyle(l).fontSize) } : null; })()`);
      await check('图例浮窗含单向出口说明、14px', legend && legend.text && legend.fs >= 14, legend);
      await closeMap();
      await sleep(600);
      const after = await saveText();
      const strip = t => { try { const j = JSON.parse(t); delete j.SavedAt; return JSON.stringify(j); } catch { return t; } };
      await check('浏览地图前后存档不变', strip(before) === strip(after), { same: strip(before) === strip(after) });
    }

    // 3. 资源卡（1600×900）
    await size(1600, 900); tag = '1600x900-资源';
    await teleport('w11_orchard'); await closeMenu(); await sleep(300);
    let badges = await ev(cardTexts);
    await check('果园资源卡：日补点 12/12', badges.some(t => t.includes('12/12')), badges);
    await teleport('d05_04'); await explore100(); await closeMenu(); await sleep(300);
    badges = await ev(cardTexts);
    await check('地牢普通箱：本代剩余2/2', badges.some(t => t.includes('本代剩余2/2')), badges);
    await key('m'); await sleep(600);
    const dl = await ev(MAPCHECK);
    await check('在地牢里默认地牢层', dl && dl.layer === 'dungeon:d05' && dl.smallCount === 0, dl);
    await closeMap();
    await teleport('d05_07'); await explore100(); await closeMenu(); await sleep(300);
    badges = await ev(cardTexts);
    await check('地牢核心：可领取', badges.some(t => t.includes('可领取')), badges);
    await ev(`(() => { const c = [...document.querySelectorAll('.res-card')].find(x => x.innerText.includes('可领取')); const b = c && c.querySelector('.res-btn'); if (b) b.click(); return !!b; })()`);
    await waitFor(`[...document.querySelectorAll('.res-card')].some(c => c.innerText.includes('已领取'))`, 60000);
    badges = await ev(cardTexts);
    await check('领取后：已领取', badges.some(t => t.includes('已领取')), badges);
    await teleport('w33_waterworks');
    if (!await ev(`!!document.querySelector('.dev-box')`)) { await domClick('菜单'); await sleep(400); }
    await ev(`(() => { const s = document.querySelector('select[aria-label="换代的地牢"]'); s.value = 'd05'; s.dispatchEvent(new Event('change', { bubbles: true })); })()`); await sleep(200);
    await domClick('地牢换代'); await sleep(500);
    const gen = await ev(`(() => { const s = JSON.parse(localStorage.getItem(${JSON.stringify(SAVE_KEY)})); return s.DungeonInstances.d05.Generation; })()`);
    await check('开发入口地牢换代：第2代', gen === 2, { gen });
    await closeMenu();
    report.finishedAt = new Date().toISOString(); save();
    if (failed) process.exitCode = 1;
  } catch (e) {
    console.error('失败：' + (e.stack || e));
    report.error = String(e.stack || e); save();
    process.exitCode = 1;
  } finally {
    try { ws && ws.close(); } catch { }
    setTimeout(() => { try { chrome.kill(); } catch { } }, 500);
  }
})().catch(e => { console.error('失败：' + (e.stack || e)); process.exitCode = 1; });
