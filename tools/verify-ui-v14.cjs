#!/usr/bin/env node
// 开发计划 v0.9 批次 B4（策划提交 f172677，M118）：物理目录与装备数据的界面实机验证（R13–R17）。
// 零依赖：Node 18+ 自带 fetch/WebSocket + Chrome DevTools Protocol（CDP 帮助函数原样沿用 verify-ui-v13.cjs）。
//
// 用法：
//   node tools/verify-ui-v14.cjs [网址] [输出目录]
//   网址默认线上 https://etsevenday.github.io/shizheshengcun/?dev=1；输出默认 doc/验收/物理目录与装备数据 v1.4
//   环境变量：CHROME（浏览器路径）、PLAYTEST_PORT（调试端口，默认 9337）、SIZES=1280x720,1600x900（默认两档）、STEPS=gear,detail,salvage,repair,migrate,icons
//
// 路线（每档尺寸，新局读完开场对白后）：
//   gear    菜单“开发入口”点“给一组装备（蓝）”→ 当前地面出现 7 张卡（砍刀、木盾、皮革护具、稳手指环、耐磨工作衣、工作长裤、维护手册），
//           绿/蓝卡有品质描边与“蓝”小标签 → 选皮革护具卡点“装备到防具”→ 人物页装备区防具行显示皮革护具；装备区效果汇总写“护甲…当前只显示”
//           → 选耐磨工作衣点“装备到上衣”→ 汇总写“制造/建造工作量×0.95…生效中”→ 选木盾“装备到武器Ⅰ”可用、对象信息写“技能尚未开放”。
//   detail  选砍刀卡：对象信息栏有“品质 蓝（附加词条2条）”、两行“附加词条”、“类型 刀剑·T1”、“站位 使用位12 → 目标位12”、“拆解 废金属×3”。
//   repair  存档里把皮革护具耐久改成 10 再读档 + 开发入口“维护材料”→ 选皮革护具卡“维修·30分钟”可用；选稳手指环卡“拆解”禁用、原因含“待策划确认”。
//   salvage 选砍刀卡点“拆解·15分钟”→ 行动结束后砍刀卡消失、当前地面出现废金属。
//   migrate 把新局存档改写成 schema 9 旧档（去掉 Quality/Affixes、武器Ⅰ放石刀）→ 继续 → 装备区武器Ⅰ石刀；存档里石刀记为白色、耐久不变。
//   icons   开发入口“新物品（材料/食品/医疗）”与白装一组都放到地面：没有一张卡用统一缺图占位（斜线方框）；
//           分类占位图（按武器类型/槽/类别）不算缺图，地面卡名单写进结果 placeholderIds，供美术补图对照。
// 结果写 <输出目录>/v14-results.json，截图写 <输出目录>/screenshots/。
'use strict';
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

const url = process.argv[2] || 'https://etsevenday.github.io/shizheshengcun/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/物理目录与装备数据 v1.4');
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

// ───────────── 主流程 ─────────────
(async () => {
  fs.mkdirSync(path.join(out, 'screenshots'), { recursive: true });
  const exe = CHROMES.find(p => fs.existsSync(p));
  if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'survival-v14-'));
  const chrome = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${prof}`, '--window-size=1920,1080',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
  const report = { url, startedAt: new Date().toISOString(), checks: [], placeholderIds: [] };
  const save = () => fs.writeFileSync(path.join(out, 'v14-results.json'), JSON.stringify(report, null, 2));
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
    const steps = (process.env.STEPS || 'gear,detail,repair,salvage,migrate,icons').split(',');
    for (const [w, h] of SIZES) {
      await size(w, h);
      size0 = `${w}x${h}`;
      await loadState(null); await readDialogues();
      const base = await savedJson();
      try {
        if (steps.some(s => ['gear', 'detail', 'salvage', 'repair'].includes(s))) {
          await loadState(base);
          await devButton('给一组装备（蓝）');
          const names = await groundNames();
          const blue = await ev(`document.querySelectorAll('.section.ground .card.item.q-blue').length`);
          await check('开发入口：蓝色装备一组 7 件在当前地面，卡面蓝色描边与标签',
            names.filter(n => /砍刀|木盾|皮革护具|稳手指环|耐磨工作衣|工作长裤|维护手册/.test(n)).length === 7 && blue >= 7, { names, blue });
          if (steps.includes('gear')) {
            await selectGround('皮革护具');
            const b = await buttonState('装备到防具');
            await clickText('装备到防具'); await sleep(400);
            await openEquipFold();
            const armor = await slotText('armor');
            const fx1 = await fxText();
            await check('皮革护具装备到防具；效果汇总写护甲且“当前只显示”', b && !b.disabled && /皮革护具/.test(armor || '') && /护甲/.test(fx1) && /当前只显示/.test(fx1), { b, armor, fx1 });
            await selectGround('耐磨工作衣'); await clickText('装备到上衣'); await sleep(400);
            await openEquipFold();
            const fx2 = await fxText();
            await check('耐磨工作衣装备到上衣；汇总写制造/建造工作量×0.95 生效中', /制造\/建造工作量×0\.95/.test(fx2) && /生效中/.test(fx2), { fx2 });
            await selectGround('木盾');
            const sb = await buttonState('装备到武器Ⅰ');
            const dl = await detailLines();
            await check('木盾可装备到武器Ⅰ，对象信息写技能尚未开放', sb && !sb.disabled && dl.some(l => l.includes('技能尚未开放')), { sb, dl });
          }
          if (steps.includes('detail')) {
            await selectGround('砍刀');
            const dl = await detailLines();
            await check('砍刀对象信息：品质蓝、两条附加词条、类型/站位/拆解',
              dl.some(l => l.includes('品质') && l.includes('蓝')) && dl.filter(l => l.startsWith('附加词条')).length === 2 && dl.some(l => l.includes('刀剑·T1'))
              && dl.some(l => l.includes('使用位12 → 目标位12')) && dl.some(l => l.includes('废金属×3')), { dl });
          }
          if (steps.includes('repair')) {
            const j = JSON.parse(await savedJson());
            const la = j.Items.find(i => i.ItemId === 'leather_armor');
            if (la) { la.Durability = 10; la.Owner = j.CurrentLocation; }
            await loadState(JSON.stringify(j));
            await devButton('维护材料');
            await selectGround('皮革护具');
            const rb = await buttonState('维修·30分钟');
            await selectGround('稳手指环');
            const sb = await buttonState('拆解·15分钟');
            const notes = await notesText();
            await check('皮革护具“维修·30分钟”可用；稳手指环“拆解”禁用且写待策划确认', rb && !rb.disabled && sb && sb.disabled && /待策划确认/.test(notes), { rb, sb, notes });
          }
          if (steps.includes('salvage')) {
            await selectGround('砍刀');
            const sb = await buttonState('拆解·15分钟');
            await clickText('拆解·15分钟');
            await waitIdle();
            await waitFor(`![...document.querySelectorAll('.section.ground .card.item')].some(c => c.innerText.includes('砍刀'))`, 60000);
            const names = await groundNames();
            await check('拆解砍刀：砍刀卡消失、当前地面出现废金属', sb && !sb.disabled && !names.some(n => n.includes('砍刀')) && names.some(n => n.includes('废金属')), { sb, names });
          }
        }
        if (steps.includes('migrate')) {
          const s = JSON.parse(base);
          let id = s.NextInstanceId;
          s.Items.push({ InstanceId: id++, ItemId: 'stone_knife', Count: 1, ExpiresAt: null, Durability: 18.5, MaxDurability: 20, LoadedAmmo: 0, LoadedAmmoId: null,
            Owner: 'equip:p0:weapon1', ReservedBy: null, PlacedOrder: null, Source: null });
          s.NextInstanceId = id;
          for (const it of s.Items) { delete it.Quality; delete it.Affixes; }
          s.SchemaVersion = '9';
          await loadState(JSON.stringify(s));
          await openEquipFold();
          const w1 = await slotText('weapon1');
          await sleep(1500);
          const after = JSON.parse(await savedJson() || '{}');
          const knife = (after.Items || []).find(i => i.ItemId === 'stone_knife');
          await check('9→10 迁移：武器Ⅰ石刀记为白色、0 词条、耐久不变',
            /石刀/.test(w1 || '') && (!knife || after.SchemaVersion !== '10' || (knife.Quality === 'white' && knife.Durability === 18.5)), { w1, schema: after.SchemaVersion, knife });
        }
        if (steps.includes('icons')) {
          await loadState(base);
          for (const t of ['新物品（材料）', '新物品（食品）', '新物品（医疗）', '给一组装备（白）']) await devButton(t);
          const generic = await genericPlaceholders();
          const cards = await ev(`[...document.querySelectorAll('.section.ground .card.item .art svg')].length`);
          report.placeholderIds = await groundNames();
          await check('新物品与新装备没有一张卡用统一缺图占位（只用分类占位图）', generic.length === 0 && cards > 60, { generic, cards });
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
    console.log(`完成：${report.checks.length}项，失败${failed}；结果 ${path.join(out, 'v14-results.json')}`);
    if (failed) process.exitCode = 1;
  } finally {
    try { await send('Browser.close'); } catch { }
    try { ws && ws.close(); } catch { }
    setTimeout(() => { try { chrome.kill(); } catch { } }, 500);
  }
})().catch(e => { console.error('失败：' + (e.stack || e)); process.exitCode = 1; });
