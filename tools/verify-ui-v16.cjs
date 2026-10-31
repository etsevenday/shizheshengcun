#!/usr/bin/env node
// 开发计划 v0.9 批次 B9（策划提交 f172677，需求 R28）：全屏四对四战斗界面的三档分辨率实机检查。
// 零依赖：Node 18+ 自带 fetch/WebSocket + Chrome DevTools Protocol（CDP 帮助函数沿用 verify-ui-v15.cjs）。
//
// 用法：
//   node tools/verify-ui-v16.cjs [网址] [输出目录]
//   网址默认线上 https://etsevenday.github.io/shizheshengcun/?dev=1；本地可用 http://localhost:5180/?dev=1（.claude/launch.json 的 web-alt）
//   输出默认系统临时目录下 survival-v16；环境变量：CHROME、PLAYTEST_PORT（默认 9338）、SIZES=1280x720,1600x900,1920x1080
//
// 路线：新局读完开场对白 → 开发入口“熟练度全部10级（含战斗）”（拳五个技能位都解锁）→ 走到农野荒地触发首战（1 敌，全屏战斗）→
//   每档尺寸检查一次（单敌）；然后用拳打完首战，开发入口记下 C3 容量、放 3 只游荡尸、形成敌踪并接敌（三敌）再检查一次。
// 每次检查（A10 §3 预算，±2px）：
//   1. 五条纵区：顶条/队列/底反馈 1600×900 为 48/64/40、1280×720 为 40/48/36；场景/操作台 460/240、352/196；1920×1080 场景:操作台≈460:240 放大；
//      五区 + 外边距 16 + 四个间隔 32 = 视口高；右对象栏 1600 为 300 宽、1280 为 240 宽、1920 为 360 宽，与场景同行。
//   2. 八锚点都在场景内（我方 4/3/2/1、敌方 1/2/3/4 顺序），本页五个技能位、两个武器页签、通用区、“医疗”“结束行动”首屏可见。
//   3. 无横向溢出（页面与五个纵区）；敌方意图短名/区间不被截（scrollWidth ≤ clientWidth）且在锚点内。
//   4. 正文 ≥16px：战斗视图内所有可见文字（.bdg 短徽标与 kbd 键帽除外，A10 §2.1 允许 14px）。
//   5. 技能位热区：1280 档 ≥48×48，其余 ≥56×56。
//   另截图：瞄准悬停（预计条纹）、物资查看抽屉、Esc 分层（抽屉→瞄准→菜单）。
// 结果写 <输出目录>/v16-results.json，截图写 <输出目录>/screenshots/。
'use strict';
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

const url = process.argv[2] || 'https://etsevenday.github.io/shizheshengcun/?dev=1';
const out = path.resolve(process.argv[3] || path.join(os.tmpdir(), 'survival-v16'));
const PORT = Number(process.env.PLAYTEST_PORT || 9338);
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
const MEASURE = `(() => {
  const r = s => { const e = document.querySelector(s); if (!e) return null; const b = e.getBoundingClientRect(); return { x: b.x, y: b.y, w: b.width, h: b.height, sw: e.scrollWidth, cw: e.clientWidth }; };
  const zones = {}; for (const z of ['top', 'queue', 'scene', 'console', 'info', 'log', 'feed']) zones[z] = r('.bfs-' + z);
  const scene = zones.scene;
  const ancs = [...document.querySelectorAll('.bfs-scene .anc')].map(a => { const b = a.getBoundingClientRect(); return { side: a.classList.contains('ally') ? 'ally' : 'foe', slot: +a.dataset.slot, x: b.x, y: b.y, w: b.width, h: b.height, empty: a.classList.contains('empty') }; });
  const inScene = ancs.every(a => a.x >= scene.x - 1 && a.y >= scene.y - 1 && a.x + a.w <= scene.x + scene.w + 1 && a.y + a.h <= scene.y + scene.h + 1);
  const order = ancs.map(a => (a.side === 'ally' ? 'A' : 'F') + a.slot).join(',');
  const intents = [...document.querySelectorAll('.anc.foe:not(.empty)')].map(a => {
    const box = a.getBoundingClientRect();
    return [...a.querySelectorAll('.anc-brief, .anc-intent-row small, .anc-nums span, .anc-head b')].map(t => { const b = t.getBoundingClientRect();
      return { text: t.innerText, clipped: t.scrollWidth > t.clientWidth + 1 || t.scrollHeight > t.clientHeight + 1 || b.bottom > box.bottom + 1 || b.right > box.right + 1 }; }); }).flat();
  // M220：操作台按钮名与短原因不被省略；队列头像不被压扁；底部按钮不压在发布时注入的构建号水印上
  const cmdClip = [...document.querySelectorAll('.bfs-console .cmdt-name, .bfs-console .cmdt-why, .bc-tab-text b, .bc-tab-text small')].filter(t => t.getBoundingClientRect().width > 0 && (t.scrollWidth > t.clientWidth + 1 || t.scrollHeight > t.clientHeight + 1)).map(t => t.innerText);
  const qArts = [...document.querySelectorAll('.q-art')].map(e => Math.round(e.getBoundingClientRect().height));
  const wm = document.getElementById('v16-watermark'); const wr = wm && wm.getBoundingClientRect();
  const wmHit = !wr ? [] : [...document.querySelectorAll('.bfs-feed button')].filter(b => { const r = b.getBoundingClientRect(); return r.width > 0 && r.right > wr.left && r.left < wr.right && r.bottom > wr.top && r.top < wr.bottom; }).map(b => b.innerText);
  const small = []; const tw = document.createTreeWalker(document.querySelector('.bfs'), NodeFilter.SHOW_TEXT);
  while (tw.nextNode()) { const n = tw.currentNode; if (!n.textContent.trim()) continue; const el = n.parentElement;
    if (el.closest('.bdg, kbd, [aria-hidden=true]')) continue; const b = el.getBoundingClientRect(); if (b.width === 0 || b.height === 0) continue;
    const st = getComputedStyle(el); if (st.visibility === 'hidden' || st.display === 'none') continue;
    const fs = parseFloat(st.fontSize); if (fs < 16) small.push({ text: n.textContent.trim().slice(0, 20), fs, cls: el.className }); }
  const skills = [...document.querySelectorAll('.bc-skills > *')].map(e => { const b = e.getBoundingClientRect(); return { w: Math.round(b.width), h: Math.round(b.height), cls: e.className }; });
  const vis = s => { const e = document.querySelector(s); if (!e) return false; const b = e.getBoundingClientRect(); return b.width > 0 && b.y >= 0 && b.bottom <= innerHeight && b.x >= 0 && b.right <= innerWidth; };
  const overflowZones = ['top', 'queue', 'console', 'feed', 'scene'].filter(z => zones[z] && zones[z].sw > zones[z].cw + 1);
  return { vw: innerWidth, vh: innerHeight, pageOverflow: document.scrollingElement.scrollWidth > innerWidth, zones, ancs: ancs.length, inScene, order, intents, small: small.slice(0, 15), smallCount: small.length, skills,
    tabs: document.querySelectorAll('.bc-tab').length, med: vis('#cmd-med'), end: vis('.bfs-end'), supplies: [...document.querySelectorAll('.bfs-feed-btns button')].some(b => b.innerText.includes('物资查看')), overflowZones, cmdClip, qArts, wmHit };
})()`;

function budget(w, h) {
  const low = h <= 800;
  const top = low ? 40 : 48, queue = low ? 48 : 64, feed = low ? 36 : 40;
  const free = h - 16 - 32 - top - queue - feed;
  const sceneFr = low ? 352 : 460, conFr = low ? 196 : 240;
  const scene = free * sceneFr / (sceneFr + conFr), con = free * conFr / (sceneFr + conFr);
  const right = Math.min(360, Math.max(240, w * 0.1875));
  return { top, queue, feed, scene, con, right };
}
const near = (a, b, tol = 2) => Math.abs(a - b) <= tol;

// ───────────── 主流程 ─────────────
(async () => {
  fs.mkdirSync(path.join(out, 'screenshots'), { recursive: true });
  const exe = CHROMES.find(p => fs.existsSync(p));
  if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'survival-v16-'));
  const chrome = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${prof}`, '--window-size=1920,1080',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
  const report = { url, startedAt: new Date().toISOString(), checks: [] };
  const save = () => fs.writeFileSync(path.join(out, 'v16-results.json'), JSON.stringify(report, null, 2));
  let tag = '';
  const check = async (name, pass, detail) => {
    const file = await shot(`${tag}-${name.replace(/[\\/:*?"<>|\s×]+/g, '_').slice(0, 40)}`);
    report.checks.push({ case: tag, name, pass: !!pass, detail: detail ?? null, screenshot: file });
    console.log(`${pass ? 'PASS' : 'FAIL'} ${tag} ${name}${pass ? '' : '：' + JSON.stringify(detail).slice(0, 900)}`);
    save();
  };
  const layoutChecks = async (w, h) => {
    const m = await ev(MEASURE);
    const bd = budget(w, h);
    const z = m.zones;
    const sum = z.top.h + z.queue.h + z.scene.h + z.console.h + z.feed.h + 16 + 32;
    await check('五条纵区与右栏符合预算', near(z.top.h, bd.top) && near(z.queue.h, bd.queue) && near(z.feed.h, bd.feed) && near(z.scene.h, bd.scene, 3) && near(z.console.h, bd.con, 3)
      && near(sum, h, 3) && near(z.info.w, bd.right, 2) && near(z.info.y, z.scene.y, 1) && near(z.log.y, z.console.y, 1),
      { top: z.top.h, queue: z.queue.h, scene: z.scene.h, console: z.console.h, feed: z.feed.h, right: z.info.w, sum, budget: bd });
    await check('八锚点在场景内且顺序为我方4321/敌方1234', m.ancs === 8 && m.inScene && m.order === 'A4,A3,A2,A1,F1,F2,F3,F4', { ancs: m.ancs, inScene: m.inScene, order: m.order });
    await check('五技能位、两页签、医疗、结束行动、物资查看首屏可见', m.skills.length === 5 && m.tabs === 2 && m.med && m.end && m.supplies, { skills: m.skills.length, tabs: m.tabs, med: m.med, end: m.end, supplies: m.supplies });
    await check('无横向溢出', !m.pageOverflow && m.overflowZones.length === 0, { page: m.pageOverflow, zones: m.overflowZones });
    await check('敌方名字、意图与生命数字不被裁切（含长名）', m.intents.length > 0 && m.intents.every(t => !t.clipped), m.intents.filter(t => t.clipped));
    await check('操作台按钮名/短原因/页签不被省略', m.cmdClip.length === 0, m.cmdClip);
    const qMin = (h <= 800 ? 48 : 64) - 22 - 1;
    await check('队列头像不被压扁', m.qArts.length > 0 && m.qArts.every(x => x >= qMin), m.qArts);
    await check('底部按钮不压在构建号水印上', m.wmHit.length === 0, m.wmHit);
    await check('正文≥16px（短徽标/键帽除外）', m.smallCount === 0, m.small);
    const minHot = h <= 800 ? 48 : 56;
    const live = m.skills.filter(s => !/locked-slot/.test(s.cls));
    await check(`技能位热区≥${minHot}×${minHot}`, live.length > 0 && m.skills.every(s => s.w >= minHot && s.h >= minHot), m.skills);
  };
  try {
    await connect();
    await send('Page.enable'); await send('Runtime.enable');
    await size(1600, 900);
    await send('Page.navigate', { url: new URL('icon-192.png', url).href }); await sleep(800);
    await ev(`localStorage.removeItem(${JSON.stringify(SAVE_KEY)})`);
    await send('Page.navigate', { url }); await sleep(800);
    if (!await waitFor(`!!document.querySelector('.title-screen') || !!document.querySelector('.game')`, 90000)) throw new Error('页面加载超时');
    // 发布流程（.github/workflows/pages.yml）在 body 末尾注入右下角构建号；本地没有就照样注入一个，用来检查底部按钮让位
    await ev(`(() => { if (document.getElementById('v16-watermark')) return; const d = document.createElement('div'); d.id = 'v16-watermark';
      d.style.cssText = 'position:fixed;right:6px;bottom:2px;font-size:11px;color:#6d7466;pointer-events:none'; d.textContent = '构建 0000000'; document.body.appendChild(d); })()`);
    await domClick('新游戏');
    if (!await waitFor(`!!document.querySelector('.game .center-scroll')`, 30000)) throw new Error('没进入主界面');
    await readDialogues();
    await devButton('熟练度全部10级（含战斗）'); await closeMenu();
    // 走到农野荒地：首战（全屏战斗）
    await ev(`(() => { const b = [...document.querySelectorAll('button')].find(x => (x.innerText || '').includes('前往') && (x.parentElement.closest('div')?.parentElement?.innerText || '').includes('农野荒地')); if (b) b.click(); return !!b; })()`);
    await sleep(500); await domClick('确认移动');
    if (!await waitFor(`!!document.querySelector('.bfs') || !!document.querySelector('.dlg-layer')`, 60000)) throw new Error('没进入首战');
    await readDialogues();
    if (!await waitFor(`!!document.querySelector('.bfs')`, 30000)) throw new Error('首战没有全屏战斗视图');
    for (const [w, h] of SIZES) {
      await size(w, h); tag = `${w}x${h}-单敌`;
      await layoutChecks(w, h);
    }
    // Esc 分层与瞄准预测（1600×900）
    await size(1600, 900); tag = '1600x900-交互';
    await key('1'); await key('ArrowRight'); await sleep(300);
    const fc = await ev(`!!document.querySelector('.anc.foe .fc') && /预计/.test(document.querySelector('.bfs-scene').innerText)`);
    const route = await ev(`(document.querySelector('.pv-route') || {}).innerText || ''`);
    await check('键盘选技能+方向键：目标生命条出现“预计”条纹，右栏写行动者→实际目标', fc && route.includes('→'), { fc, route });
    await domClick('物资查看'); await sleep(300);
    const drawer = await ev(`!!document.querySelector('.bsup') && /只读/.test(document.querySelector('.bsup').innerText)`);
    await key('Escape'); const afterDrawer = await ev(`({ drawer: !!document.querySelector('.bfs-drawer'), aim: !!document.querySelector('.anc.targetable') })`);
    await key('Escape'); const afterAim = await ev(`({ aim: !!document.querySelector('.anc.targetable') })`);
    await check('物资查看只读抽屉；Esc 先关抽屉、再取消瞄准，不关闭战斗', drawer && !afterDrawer.drawer && afterDrawer.aim && !afterAim.aim && await ev(`!!document.querySelector('.bfs')`), { drawer, afterDrawer, afterAim });
    for (let i = 0; i < 4 && await ev(`!document.querySelector('.bfs > .overlay')`); i++) await key('Escape');
    const menu = await ev(`(() => { const o = document.querySelector('.bfs > .overlay'); return o ? o.innerText : ''; })()`);
    await check('Esc 最后打开战斗菜单（继续/系统说明/保存并返回标题，无脱战）', menu.includes('系统说明') && menu.includes('保存并返回标题') && !menu.includes('脱战按钮'), { menu: menu.slice(0, 200) });
    await key('Escape');
    const outer = await ev(`[...document.querySelectorAll('header.top, main.body, footer.footer')].every(e => e.inert)`);
    await check('外层顶栏/中部/底栏在战斗中不可操作（inert）', outer, { outer });
    // 打完首战（拳 1 级技能，AP 不够就结束行动）
    for (let i = 0; i < 80 && await ev(`!!document.querySelector('.bfs')`); i++) {
      await readDialogues(5);
      const mine = await ev(`(document.querySelector('.bfs-actor') || {}).className?.includes('now') && !document.querySelector('.bfs-feed .warn')`);
      if (!mine) { await sleep(500); continue; }
      const logBefore = await ev(`(document.querySelector('.bfs-log-list') || {}).innerText || ''`);
      await key('1'); await key('ArrowRight'); await key('Enter'); await sleep(500);
      const logAfter = await ev(`(document.querySelector('.bfs-log-list') || {}).innerText || ''`);
      if (logAfter === logBefore && await ev(`!!document.querySelector('.bfs')`)) {
        await key('Escape'); await domClick('结束行动'); await sleep(200); await clickSel('#bt-confirm'); await sleep(600);
      }
    }
    await readDialogues();
    if (await ev(`!!document.querySelector('.bfs')`)) throw new Error('首战没打完');
    // 三敌
    await devButton('记下营区登记（C2）'); await devButton('记下教会路线记录（C3）');
    // M220：长名敌人（重装守卫感染者）+ 两只游荡尸
    await devButton('放重装守卫感染者');
    for (let i = 0; i < 2; i++) await devButton('放游荡尸');
    await devButton('本地敌人立即形成敌踪'); await closeMenu();
    await domClick('接敌');
    if (!await waitFor(`!!document.querySelector('.bfs')`, 20000)) throw new Error('三敌没进入战斗');
    await readDialogues();
    for (const [w, h] of SIZES) {
      await size(w, h); tag = `${w}x${h}-三敌`;
      await layoutChecks(w, h);
    }
    // M220：旧“射程不足”不残留——拳击点 2 号位（拳只打 1 号位）→ Esc 取消瞄准 → 点队列里的敌人/自己
    await size(1280, 720); tag = '1280x720-交互';
    for (let i = 0; i < 40 && !await ev(`(document.querySelector('.bfs-actor') || {}).className?.includes('now')`); i++) await sleep(500);
    await key('1');
    await clickSel('.anc.foe[data-slot="2"]');
    const before = await ev(`/射程不足|只能打/.test(document.querySelector('.bfs-info').innerText)`);
    await key('Escape');
    await clickSel('.q-av.q-foe');
    const afterFoe = await ev(`/射程不足|只能打/.test(document.querySelector('.bfs-info').innerText)`);
    await clickSel('.q-av.me');
    const afterMe = await ev(`/射程不足|只能打/.test(document.querySelector('.bfs-info').innerText)`);
    await check('取消瞄准、换查看对象后不残留“射程不足”', before && !afterFoe && !afterMe, { before, afterFoe, afterMe });
    // M220：自用指令按回车提交（点按钮后焦点仍在按钮上）——调整站位两次到 3 号位，让敌人出现“迫近”长意图
    for (const to of [2, 3]) {
      if (await ev("!!document.querySelector('.bfs > .overlay')")) await key('Escape');
      for (let i = 0; i < 40 && !await ev("(document.querySelector('.bfs-actor') || {}).className?.includes('now')"); i++) await sleep(500);
      const pos0 = await ev(`(document.querySelector('.bc-who-name b') || {}).innerText || ''`);
      await clickSel(`#cmd-swap-${to}`); await sleep(200);
      await key('Enter'); await sleep(700);
      const pos1 = await ev(`(document.querySelector('.bc-who-name b') || {}).innerText || ''`);
      await check(`选“→${to}号位”后按回车即提交`, pos1.includes(`${to}号位`) && pos1 !== pos0, { pos0, pos1 });
    }
    const briefs = await ev(`[...document.querySelectorAll('.anc-brief')].map(b => b.innerText)`);
    const m2 = await ev(MEASURE);
    await check('你在3号位时长意图（迫近→弱袭等）完整', briefs.length > 0 && m2.intents.every(t => !t.clipped), { briefs, clipped: m2.intents.filter(t => t.clipped) });
    const errs = await ev(`(window.__pt && window.__pt.errs) || []`);
    report.consoleErrors = errs;
    const failed = report.checks.filter(c => !c.pass).length;
    report.summary = { checks: report.checks.length, failed };
    save();
    console.log(`完成：${report.checks.length}项，失败${failed}；结果 ${path.join(out, 'v16-results.json')}`);
    if (failed) process.exitCode = 1;
  } catch (e) {
    report.checks.push({ case: tag, name: '异常', pass: false, detail: String(e && e.message || e) });
    console.log(`FAIL ${tag} 异常：${e && e.message}`);
    save();
    process.exitCode = 1;
  } finally {
    try { await send('Browser.close'); } catch { }
    try { ws && ws.close(); } catch { }
    setTimeout(() => { try { chrome.kill(); } catch { } }, 500);
  }
})().catch(e => { console.error('失败：' + (e.stack || e)); process.exitCode = 1; });
