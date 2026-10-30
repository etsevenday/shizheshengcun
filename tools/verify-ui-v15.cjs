#!/usr/bin/env node
// 开发计划 v0.9 批次 B5（策划提交 f172677，M125）：十级熟练度与求生被动的界面实机验证（R18–R19）。
// 零依赖：Node 18+ 自带 fetch/WebSocket + Chrome DevTools Protocol（CDP 帮助函数原样沿用 verify-ui-v14.cjs）。
//
// 用法：
//   node tools/verify-ui-v15.cjs [网址] [输出目录]
//   网址默认线上 https://etsevenday.github.io/shizheshengcun/?dev=1；输出默认 doc/验收/十级熟练度与求生被动 v1.5
//   环境变量：CHROME、PLAYTEST_PORT（默认 9337）、SIZES=1280x720,1600x900、STEPS=skills,passives,migrate
//
// 路线（每档尺寸，新局读完开场对白后）：
//   skills   右栏“人物”→展开“熟练度”：战斗/医疗（战内外）/求生三组共 15 行，每行“1/10级”“经验0/100”；
//            “已获得的求生被动（5个）”列 1 级五个；悬停“采集”行 title 列出 1/3/5/7/10 五个技能。
//   passives 菜单“开发入口”→“求生熟练度全部10级”→ 熟练度区采集行“10/10级”“满级，不再获得经验”，求生被动 25 个，
//            含“采集7级·熟练采伐：定向采集基础工作量×0.9（生效中）”。
//   migrate  把新局存档改写成 schema 10 旧档（8 门，刀斧 7 级经验 123、制造 13 级）→ 继续 → 熟练度区刀剑、斧都是“7/10级”，
//            制造“10/10级”；存档 SchemaVersion 为 "11"，日志有“刀斧7级（经验123）复制为刀剑与斧”。
// 结果写 <输出目录>/v15-results.json，截图写 <输出目录>/screenshots/。
'use strict';
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

const url = process.argv[2] || 'https://etsevenday.github.io/shizheshengcun/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/十级熟练度与求生被动 v1.5');
const PORT = Number(process.env.PLAYTEST_PORT || 9337);
const SAVE_KEY = 'survival.save.v1';
const SIZES = (process.env.SIZES || '1280x720,1600x900').split(',').map(s => s.split('x').map(Number));
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
async function size(w, h) { await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false }); await sleep(400); }
async function mouseAt(x, y, type = 'mouseMoved') {
  await send('Input.dispatchMouseEvent', { type, x, y, button: type === 'mouseMoved' ? 'none' : 'left', buttons: type === 'mousePressed' ? 1 : 0, clickCount: 1, pointerType: 'mouse' });
}
async function clickAt(x, y) { await mouseAt(x, y); await mouseAt(x, y, 'mousePressed'); await mouseAt(x, y, 'mouseReleased'); await sleep(250); }
async function centerOf(sel, index = 0) {
  return ev(`(() => { const es = [...document.querySelectorAll(${JSON.stringify(sel)})].filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0; });
    const e = es[${index}]; if (!e) return null; e.scrollIntoView({ block: 'nearest', inline: 'nearest' }); const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
}
async function clickSel(sel, index = 0) { const r = await centerOf(sel, index); if (!r) return false; await clickAt(r.x, r.y); return true; }
async function hoverSel(sel, index = 0) { const r = await centerOf(sel, index); if (!r) return false; await mouseAt(r.x, r.y); await sleep(300); return true; }
async function clickText(text) {
  const r = await ev(`(() => { const t = ${JSON.stringify(text)}; for (const e of document.querySelectorAll('button')) {
    if ((e.innerText || '').trim().includes(t) && !e.disabled) { const r = e.getBoundingClientRect(); if (r.width > 0) return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; } } return null; })()`);
  if (!r) return false; await clickAt(r.x, r.y); return true;
}
async function key(k) {
  const one = k.length === 1;
  const code = one ? 'Key' + k.toUpperCase() : k;
  const vk = { Escape: 27, Enter: 13 }[k] ?? (one ? k.toUpperCase().charCodeAt(0) : 0);
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
/** 写入存档（null=清空）后打开游戏并“继续”/“新游戏”。keepPrefs：保留界面偏好（折叠记忆测试用）。 */
async function loadState(state) {
  await send('Page.navigate', { url: new URL('icon-192.png', url).href }); await sleep(800);
  if (state) await ev(`localStorage.setItem(${JSON.stringify(SAVE_KEY)}, ${JSON.stringify(typeof state === 'string' ? state : JSON.stringify(state))})`);
  else await ev(`localStorage.removeItem(${JSON.stringify(SAVE_KEY)})`);
  await send('Page.navigate', { url }); await sleep(800);
  if (!await waitFor(`!!document.querySelector('.title-screen') || !!document.querySelector('.game')`, 90000)) throw new Error('页面加载超时');
  if (state) await clickText('继续'); else await clickText('新游戏');
  if (!await waitFor(`!!document.querySelector('.game .center-scroll')`, 30000)) throw new Error('没进入主界面');
  await ev(`document.fonts.ready.then(() => true)`); await sleep(600);
}
async function reloadContinue() {
  await send('Page.reload', { ignoreCache: true }); await sleep(800);
  if (!await waitFor(`!!document.querySelector('.title-screen') || !!document.querySelector('.game')`, 90000)) throw new Error('页面加载超时');
  await clickText('继续');
  if (!await waitFor(`!!document.querySelector('.game .center-scroll')`, 30000)) throw new Error('没进入主界面');
  await sleep(600);
}
async function readDialogues(max = 40) {
  for (let i = 0; i < max; i++) {
    if (!await ev(`!!document.querySelector('.dlg-layer')`)) return true;
    if (!await clickText('结束对话') && !await clickText('继续')) await sleep(300);
  }
  return !await ev(`!!document.querySelector('.dlg-layer')`);
}
const savedJson = () => ev(`localStorage.getItem(${JSON.stringify(SAVE_KEY)})`);


const fileSafe = name => name.replace(/[\\/:*?"<>|\s×]+/g, '_');
const groundNames = () => ev(`[...document.querySelectorAll('.section.ground .card.item')].map(c => c.innerText.replace(/\\s+/g, ' ').trim())`);
async function selectGround(name) {
  const i = await ev(`[...document.querySelectorAll('.section.ground .card.item')].findIndex(c => c.innerText.includes(${JSON.stringify(name)}))`);
  if (i < 0) return false;
  return clickSel('.section.ground .card.item', i);
}
async function devButton(text) {
  if (!await ev(`!!document.querySelector('.dev-box')`)) { await clickText('菜单'); await sleep(400); }
  const ok = await clickText(text); await sleep(500);
  return ok;
}
async function openEquipFold() {
  await clickText('人物');
  if (!await ev(`!!document.querySelector('[data-fold=equip].open')`)) await clickSel('[data-fold=equip] .fold-head');
  return ev(`!!document.querySelector('[data-fold=equip].open')`);
}
const slotText = slot => ev(`(() => { const r = document.querySelector('.equip-row[data-slot=${slot}] .eq-item'); return r ? r.innerText.trim() : null; })()`);
const fxText = () => ev(`[...document.querySelectorAll('.equip-fx')].map(e => e.innerText).join(' | ')`);
const detailLines = () => ev(`[...document.querySelectorAll('.detail-scroll .lines > div')].map(d => d.innerText.replace(/\\s+/g, ' ').trim())`);
const buttonState = text => ev(`(() => { const b = [...document.querySelectorAll('button')].find(e => (e.innerText || '').trim().includes(${JSON.stringify(text)}) && e.getBoundingClientRect().width > 0);
  return b ? { disabled: b.disabled, title: b.title || '' } : null; })()`);
const notesText = () => ev(`[...document.querySelectorAll('.cact .ca-note')].map(e => e.innerText).join(' | ')`);
/** 统一缺图占位（斜线方框 M12 12L88 88）出现在哪些卡上。 */
const genericPlaceholders = () => ev(`[...document.querySelectorAll('.card .art svg')].filter(s => s.innerHTML.includes('M12 12L88 88')).map(s => s.closest('.card').innerText.split('\\n')[0])`);
async function waitIdle(ms = 120000) {
  await sleep(500);
  return waitFor(`![...document.querySelectorAll('button')].some(b => (b.innerText || '').includes('取消') && b.closest('.central-progress, .cprog') && b.getBoundingClientRect().width > 0)`, ms);
}

async function openSkillsFold() {
  await clickText('人物');
  if (!await ev(`!!document.querySelector('[data-fold=skills].open')`)) await clickSel('[data-fold=skills] .fold-head');
  return ev(`!!document.querySelector('[data-fold=skills].open')`);
}
const skillRows = () => ev(`[...document.querySelectorAll('[data-fold=skills] .skill-row')].map(r => ({ id: r.dataset.skill, text: r.innerText.replace(/\s+/g, ' ').trim(), title: r.title }))`);
const passiveLines = () => ev(`[...document.querySelectorAll('[data-fold=skills] [data-passive]')].map(r => r.innerText.trim())`);
const groups = () => ev(`[...document.querySelectorAll('[data-fold=skills] .skill-group')].map(r => r.innerText.trim())`);
const row = (rows, id) => (rows.find(r => r.id === id) || {}).text || '';

// ───────────── 主流程 ─────────────
(async () => {
  fs.mkdirSync(path.join(out, 'screenshots'), { recursive: true });
  const exe = CHROMES.find(p => fs.existsSync(p));
  if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'survival-v15-'));
  const chrome = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${prof}`, '--window-size=1920,1080',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
  const report = { url, startedAt: new Date().toISOString(), checks: [] };
  const save = () => fs.writeFileSync(path.join(out, 'v15-results.json'), JSON.stringify(report, null, 2));
  let size0 = '';
  const check = async (name, pass, detail) => {
    const file = await shot(`${size0}-${fileSafe(name)}`);
    report.checks.push({ size: size0, name, pass: !!pass, detail: detail ?? null, screenshot: file });
    console.log(`${pass ? 'PASS' : 'FAIL'} ${size0} ${name}${pass ? '' : '：' + JSON.stringify(detail)}`);
    save();
  };
  try {
    await connect();
    await send('Page.enable'); await send('Runtime.enable');
    const steps = (process.env.STEPS || 'skills,passives,migrate').split(',');
    for (const [w, h] of SIZES) {
      await size(w, h);
      size0 = `${w}x${h}`;
      await loadState(null); await readDialogues();
      const base = await savedJson();
      try {
        if (steps.includes('skills')) {
          await loadState(base);
          await openSkillsFold();
          const rows = await skillRows();
          const g = await groups();
          const p = await passiveLines();
          await check('熟练度区：三组 15 门，每门 1/10级、经验0/100；求生被动 5 个',
            rows.length === 15 && g.some(x => x.includes('战斗')) && g.some(x => x.includes('求生')) && rows.every(r => r.text.includes('1/10级') && r.text.includes('经验0/100'))
            && p.length === 5, { rows: rows.map(r => r.text), g, p });
          const gat = rows.find(r => r.id === 'gathering') || {};
          await check('悬停采集行：列出 1/3/5/7/10 五个专属技能', /1级 辨识采集/.test(gat.title || '') && /10级 精取/.test(gat.title || ''), { title: gat.title });
        }
        if (steps.includes('passives')) {
          await loadState(base);
          await devButton('求生熟练度全部10级');
          await key('Escape');
          await openSkillsFold();
          const rows = await skillRows();
          const p = await passiveLines();
          await check('开发入口求生10级：采集“10/10级 满级”，求生被动 25 个且熟练采伐生效中',
            /10\/10级/.test(row(rows, 'gathering')) && /满级/.test(row(rows, 'gathering')) && p.length === 25
            && p.some(x => x.includes('采集7级·熟练采伐') && x.includes('生效中')), { gathering: row(rows, 'gathering'), n: p.length });
        }
        if (steps.includes('migrate')) {
          const s = JSON.parse(base);
          const old = { crafting: [13, 40], medicine: [1, 0], firearms: [1, 0], blade: [7, 123], spear: [1, 0], unarmed: [1, 0], archery: [1, 0], blunt: [1, 0] };
          s.Actors.p0.Skills = Object.fromEntries(Object.entries(old).map(([k, [l, x]]) => [k, { Level: l, Xp: x }]));
          delete s.Actors.p0.DailyCounts; delete s.TraveledEdges;
          s.SchemaVersion = '10';
          await loadState(JSON.stringify(s));
          await openSkillsFold();
          const rows = await skillRows();
          await sleep(1500);
          const after = JSON.parse(await savedJson() || '{}');
          const log = (after.Log || []).map(l => l.Text).join(' | ');
          await check('10→11 迁移：刀剑与斧 7/10级、制造 10/10级、存档版本 11',
            /7\/10级/.test(row(rows, 'blade')) && /7\/10级/.test(row(rows, 'axe')) && /10\/10级/.test(row(rows, 'crafting')) && after.SchemaVersion === '11'
            && /复制为刀剑与斧/.test(log), { blade: row(rows, 'blade'), axe: row(rows, 'axe'), crafting: row(rows, 'crafting'), schema: after.SchemaVersion });
        }
      } catch (e) {
        report.checks.push({ size: size0, name: '异常', pass: false, detail: String(e && e.message || e) });
        console.log(`FAIL ${size0} 异常：${e && e.message}`);
        save();
      }
    }
    const failed = report.checks.filter(c => !c.pass).length;
    report.summary = { checks: report.checks.length, failed };
    save();
    console.log(`完成：${report.checks.length}项，失败${failed}；结果 ${path.join(out, 'v15-results.json')}`);
    if (failed) process.exitCode = 1;
  } finally {
    try { await send('Browser.close'); } catch { }
    try { ws && ws.close(); } catch { }
    setTimeout(() => { try { chrome.kill(); } catch { } }, 500);
  }
})().catch(e => { console.error('失败：' + (e.stack || e)); process.exitCode = 1; });
