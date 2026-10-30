#!/usr/bin/env node
// 开发计划 v0.9 批次 B3（策划提交 f172677，M102）：多角色 owner 模型与七槽五卡的界面实机验证。
// 零依赖：Node 18+ 自带 fetch/WebSocket + Chrome DevTools Protocol（写法同 verify-ui-v12.cjs，CDP 帮助函数原样沿用）。
//
// 用法：
//   node tools/verify-ui-v13.cjs [网址] [输出目录]
//   网址默认线上 https://etsevenday.github.io/shizheshengcun/?dev=1；输出默认 doc/验收/多角色七槽五卡 v1.3
//   环境变量：CHROME（浏览器路径）、PLAYTEST_PORT（调试端口，默认 9337）、SIZES=1280x720,1600x900（默认两档）、STEPS=equip,full,battle,migrate,dup（只跑部分）
//
// 路线（每档尺寸）：
//   equip   新局读完开场对白 → 往随身放石刀/厨房刀/木矛 → 右栏人物“装备”区七槽全空 → 选石刀卡“装备到武器Ⅰ”→ 武器Ⅰ显示石刀、随身 N/5 少一张、
//           左栏总重不变（悬停写“含穿戴”）→ 选厨房刀卡：“装备到武器Ⅱ”禁用且原因“已装备同类型武器：石刀” → 木矛装备到武器Ⅱ → 卸下武器Ⅱ回随身。
//   full    随身五张卡 + 武器Ⅰ石刀：装备区“卸下”禁用、出现“放当前地面”，点后石刀到地面、随身仍 5/5。
//   battle  石刀进武器Ⅰ、木矛进武器Ⅱ后去农野荒地进首战：操作台两个武器页签、开战在武器Ⅰ页；选技能瞄准后切到武器Ⅱ页 → 瞄准取消、AP 不变；
//           刷新后“继续”仍在武器Ⅱ页（存读档保留当前页）。
//   migrate 把新局存档改写成 schema 8 旧档（主角个人字段放回顶层、EquippedWeaponId 指向随身石刀）写入 localStorage → 继续 → 武器Ⅰ为石刀、
//           石刀不在随身行；刷新再继续仍然如此（迁移只做一次）。
//   dup     武器Ⅰ石刀 + 武器Ⅱ厨房刀（非法同类）→ 安全空闲弹“整理同类型武器”窗：单选保留石刀、厨房刀选“放回随身”→ 确认后窗关闭、厨房刀在随身。
// 另测 1280×720 下右栏装备区展开时七行可见（可在右栏内滚动，不压出屏）。结果写 <输出目录>/v13-results.json，截图写 <输出目录>/screenshots/。
'use strict';
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

const url = process.argv[2] || 'https://etsevenday.github.io/shizheshengcun/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/多角色七槽五卡 v1.3');
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

// ───────────── 测试存档：在新局（schema 9）存档上改出随身武器、穿戴与旧档 ─────────────
const ITEM_BASE = { ExpiresAt: null, LoadedAmmo: 0, LoadedAmmoId: null, ReservedBy: null, PlacedOrder: null, Source: null,
  InContainer: false, InFacility: false, IsCarried: false, IsWorn: false };
const DUR = { stone_knife: 20, kitchen_knife: 35, wooden_spear: 25 };
function stage(json, spec) {
  const s = JSON.parse(json);
  let id = s.NextInstanceId;
  const ids = {};
  for (const [itemId, owner] of spec.items || []) {
    const it = { ...ITEM_BASE, InstanceId: id++, ItemId: itemId, Count: 1, Owner: owner, IsCarried: owner === 'carried', IsWorn: owner.startsWith('equip:') };
    if (DUR[itemId]) { it.Durability = DUR[itemId]; it.MaxDurability = DUR[itemId]; } else { it.Durability = null; it.MaxDurability = null; }
    s.Items.push(it); (ids[itemId] ||= []).push(it.InstanceId);
  }
  for (const [itemId, n] of spec.stacks || []) s.Items.push({ ...ITEM_BASE, InstanceId: id++, ItemId: itemId, Count: n, Owner: 'carried', IsCarried: true, Durability: null, MaxDurability: null });
  s.NextInstanceId = id;
  return { json: JSON.stringify(s), ids };
}
/** 把 schema 9 存档改写成 schema 8 旧档：主角个人字段回顶层、去掉 Actors/Party，EquippedWeaponId 指向给定实例。 */
function toSchema8(json, equippedId) {
  const s = JSON.parse(json);
  const p0 = s.Actors.p0;
  for (const f of ['Stats', 'Body', 'Skills', 'DiarrheaUntil', 'BandageReadyAt', 'AlertLatched', 'ZeroLatched']) s[f] = p0[f];
  delete s.Actors; delete s.Party;
  s.SchemaVersion = '8';
  s.EquippedWeaponId = equippedId;
  return JSON.stringify(s);
}

const slotText = slot => ev(`(() => { const r = document.querySelector('.equip-row[data-slot=${slot}] .eq-item'); return r ? r.innerText.trim() : null; })()`);
const cardsText = () => ev(`(document.querySelector('.panel.weight .weight-cards') || {}).innerText || ''`);
const weightText = () => ev(`(document.querySelector('.panel.weight strong') || {}).innerText || ''`);
const carriedNames = () => ev(`[...document.querySelectorAll('.section.carried .card.item')].map(c => c.innerText.replace(/\\s+/g, ' ').trim())`);
async function openEquipFold() {
  await clickText('人物');
  if (!await ev(`!!document.querySelector('[data-fold=equip].open')`)) await clickSel('[data-fold=equip] .fold-head');
  return ev(`!!document.querySelector('[data-fold=equip].open')`);
}
async function selectCarried(name) {
  const i = await ev(`[...document.querySelectorAll('.section.carried .card.item')].findIndex(c => c.innerText.includes(${JSON.stringify(name)}))`);
  if (i < 0) return false;
  return clickSel('.section.carried .card.item', i);
}
const buttonState = text => ev(`(() => { const b = [...document.querySelectorAll('button')].find(e => (e.innerText || '').trim().includes(${JSON.stringify(text)}) && e.getBoundingClientRect().width > 0);
  return b ? { disabled: b.disabled, title: b.title || '' } : null; })()`);
const fileSafe = name => name.replace(/[\\/:*?"<>|\s×]+/g, '_');

// ───────────── 主流程 ─────────────
(async () => {
  fs.mkdirSync(path.join(out, 'screenshots'), { recursive: true });
  const exe = CHROMES.find(p => fs.existsSync(p));
  if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'survival-v13-'));
  const chrome = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${prof}`, '--window-size=1920,1080',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
  const report = { url, startedAt: new Date().toISOString(), checks: [] };
  const save = () => fs.writeFileSync(path.join(out, 'v13-results.json'), JSON.stringify(report, null, 2));
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
    const steps = (process.env.STEPS || 'equip,full,battle,migrate,dup').split(',');
    for (const [w, h] of SIZES) {
      await size(w, h);
      size0 = `${w}x${h}`;
      // 新局基础存档（读完开场对白）
      await loadState(null); await readDialogues();
      const base = await savedJson();
      try {
        if (steps.includes('equip')) {
          const { json } = stage(base, { items: [['stone_knife', 'carried'], ['kitchen_knife', 'carried'], ['wooden_spear', 'carried']] });
          await loadState(json);
          const open = await openEquipFold();
          const rows = await ev(`document.querySelectorAll('.equip-row').length`);
          const empty = await ev(`[...document.querySelectorAll('.equip-row')].every(r => r.classList.contains('empty'))`);
          await check('装备区七槽全空', open && rows === 7 && empty, { open, rows, empty });
          if (w === 1280) {
            const fit = await ev(`(() => { const rs = [...document.querySelectorAll('.equip-row')]; const sc = document.querySelector('.char-panel'); if (!sc || !rs.length) return null;
              rs[rs.length - 1].scrollIntoView({ block: 'nearest' }); const b = sc.getBoundingClientRect(); const r = rs[rs.length - 1].getBoundingClientRect();
              return { inside: r.bottom <= b.bottom + 1 && r.top >= b.top - 1, panelBottom: b.bottom, rowBottom: r.bottom, vh: innerHeight }; })()`);
            await check('1280×720 装备区七行在右栏内可见', fit && fit.inside && fit.panelBottom <= fit.vh, fit);
          }
          const cards0 = await cardsText(); const kg0 = await weightText();
          await selectCarried('石刀');
          const b1 = await buttonState('装备到武器Ⅰ');
          await check('选中随身石刀出“装备到武器Ⅰ/Ⅱ”', b1 && !b1.disabled && !!await buttonState('装备到武器Ⅱ'), b1);
          await clickText('装备到武器Ⅰ');
          const s1 = await slotText('weapon1'); const cards1 = await cardsText(); const kg1 = await weightText();
          const tip = await ev(`(document.querySelector('.panel.weight') || {}).title || ''`);
          await check('石刀进武器Ⅰ：N/5 少一张、总重不变、悬停含穿戴', (s1 || '').includes('石刀') && parseInt(cards1) === parseInt(cards0) - 1 && kg1 === kg0 && tip.includes('含穿戴'),
            { s1, cards0, cards1, kg0, kg1, tip });
          const names = await carriedNames();
          await check('石刀不在随身行', !names.some(n => n.includes('石刀')), names);
          await selectCarried('厨房刀');
          const b2 = await buttonState('装备到武器Ⅱ');
          const note = await ev(`[...document.querySelectorAll('.ca-note.bad')].map(e => e.innerText).join('|')`);
          await check('同类第二把被拒并写原因', b2 && b2.disabled && (b2.title + note).includes('已装备同类型武器：石刀'), { b2, note });
          await key('Escape');
          await selectCarried('木矛');
          await clickText('装备到武器Ⅱ');
          const s2 = await slotText('weapon2');
          await check('木矛进武器Ⅱ（刀+矛合法）', (s2 || '').includes('木矛'), s2);
          await clickSel('.equip-row[data-slot=weapon2] .eq-btn');
          const back = await carriedNames();
          await check('卸下武器Ⅱ回随身', (await ev(`document.querySelector('.equip-row[data-slot=weapon2]').classList.contains('empty')`)) && back.some(n => n.includes('木矛')), back);
        }
        if (steps.includes('full')) {
          const { json } = stage(base, { items: [['stone_knife', 'equip:p0:weapon1']], stacks: [['wood', 1], ['fiber', 1], ['stone', 1]] });
          await loadState(json);
          await openEquipFold();
          const un = await ev(`(() => { const b = document.querySelector('.equip-row[data-slot=weapon1] .eq-btn'); return b ? { disabled: b.disabled, title: b.title } : null; })()`);
          const ground = await ev(`!!document.querySelector('.equip-row[data-slot=weapon1] .eq-btn.alt')`);
          const cards = await cardsText();
          await check('五卡满：卸下禁用并给“放当前地面”', un && un.disabled && ground && cards.startsWith('5/5'), { un, ground, cards });
          await clickSel('.equip-row[data-slot=weapon1] .eq-btn.alt');
          const onGround = await ev(`[...document.querySelectorAll('.section.ground .card.item')].some(c => c.innerText.includes('石刀'))`);
          await check('明确选择后石刀放到当前地面', onGround && (await slotText('weapon1')) === '空', { onGround });
        }
        if (steps.includes('battle')) {
          const { json } = stage(base, { items: [['stone_knife', 'equip:p0:weapon1'], ['wooden_spear', 'equip:p0:weapon2']] });
          await loadState(json);
          await clickSel('.map-adjacent .loc-card');
          if (!await clickText('确认移动')) throw new Error('找不到“确认移动”');
          if (!await waitFor(`!!document.querySelector('.bt-team') || !!document.querySelector('.dlg-layer')`, 60000)) throw new Error('没进入首战');
          await readDialogues();
          if (!await waitFor(`!!document.querySelector('.bt-team')`, 10000)) throw new Error('没进入两行战斗');
          await waitFor(`!!document.querySelector('.bc-group.skills .cmdt:not(.off)')`, 20000);
          const tabs = await ev(`[...document.querySelectorAll('.bc-page')].map(b => ({ on: b.classList.contains('on'), text: b.innerText.replace(/\\s+/g, ' ').trim() }))`);
          await check('两个武器页签、开战在武器Ⅰ页', tabs.length === 2 && tabs[0].on && tabs[0].text.includes('石刀') && tabs[1].text.includes('木矛'), tabs);
          const noSwitch = await ev(`![...document.querySelectorAll('.bt-console button')].some(b => b.innerText.includes('换武器'))`);
          await check('没有旧“换武器”指令与浮窗', noSwitch, null);
          await clickSel('.bc-group.skills .cmdt:not(.off)');
          const aiming = await ev(`!!document.querySelector('.battle-overlay.aiming')`);
          const ap0 = await ev(`(document.querySelector('.bt-head') || {}).innerText || ''`);
          await clickSel('.bc-page', 1);
          const after = await ev(`({ on2: document.querySelectorAll('.bc-page')[1].classList.contains('on'), aiming: !!document.querySelector('.battle-overlay.aiming'), head: (document.querySelector('.bt-head') || {}).innerText || '' })`);
          await check('切到武器Ⅱ页：0AP、取消已选瞄准', after.on2 && aiming && !after.aiming && after.head === ap0, { aiming, ...after, ap0 });
          await reloadContinue();
          await waitFor(`!!document.querySelector('.bc-page')`, 20000);
          const kept = await ev(`(() => { const p = document.querySelectorAll('.bc-page')[1]; return !!p && p.classList.contains('on'); })()`);
          await check('刷新后仍在武器Ⅱ页（战斗存档保留当前页）', kept, null);
        }
        if (steps.includes('migrate')) {
          const { json, ids } = stage(base, { items: [['stone_knife', 'carried']] });
          await loadState(toSchema8(json, ids.stone_knife[0]));
          await openEquipFold();
          const s1 = await slotText('weapon1');
          const names = await carriedNames();
          await check('schema 8 旧档：当前武器迁到武器Ⅰ、不在随身行', (s1 || '').includes('石刀') && !names.some(n => n.includes('石刀')), { s1, names });
          const ver = JSON.parse(await savedJson()).SchemaVersion;
          await reloadContinue(); await openEquipFold();
          await check('刷新再继续：存档 schema 9、武器Ⅰ仍是石刀', ver === '9' && ((await slotText('weapon1')) || '').includes('石刀'), { ver });
        }
        if (steps.includes('dup')) {
          const { json } = stage(base, { items: [['stone_knife', 'equip:p0:weapon1'], ['kitchen_knife', 'equip:p0:weapon2']] });
          await loadState(json);
          const shown = await waitFor(`[...document.querySelectorAll('.overflow-layer h2')].some(h => h.innerText.includes('整理同类型武器'))`, 8000);
          await check('非法同类双武器：弹出整理窗', shown, null);
          await ev(`(() => { const r = document.querySelectorAll('.overflow-layer input[name=dup-keep]'); r[0] && r[0].click(); return r.length; })()`); await sleep(300);
          const dis0 = (await buttonState('确认整理'))?.disabled;
          await ev(`(() => { const r = [...document.querySelectorAll('.overflow-layer .dup-dest input')]; r[0] && r[0].click(); return r.length; })()`); await sleep(300);
          await clickText('确认整理');
          const closed = !await ev(`!!document.querySelector('.overflow-layer')`);
          const names = await carriedNames();
          await check('默认不选去向不能确认；选“放回随身”后整理完成', dis0 === true && closed && names.some(n => n.includes('厨房刀')), { dis0, closed, names });
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
    console.log(`完成：${report.checks.length}项，失败${failed}；结果 ${path.join(out, 'v13-results.json')}`);
    if (failed) process.exitCode = 1;
  } finally {
    try { await send('Browser.close'); } catch { }
    try { ws && ws.close(); } catch { }
    setTimeout(() => { try { chrome.kill(); } catch { } }, 500);
  }
})().catch(e => { console.error('失败：' + (e.stack || e)); process.exitCode = 1; });
