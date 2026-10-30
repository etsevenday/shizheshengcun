#!/usr/bin/env node
// S14「2026-10-30 主界面截图复核」实机测量（开发计划 v0.9 批次 B2，M88）：火堆与棚/床等高、图文与边框安全区、主图完整、
// 名称与箭头不同格、右栏熟练度/词条折叠记忆。零依赖：Node 18+ 自带 fetch/WebSocket + Chrome DevTools Protocol（同 verify-ui-v11.cjs）。
//
// 用法：
//   node tools/verify-ui-v12.cjs [网址] [输出目录]
//   网址默认线上 https://etsevenday.github.io/shizheshengcun/?dev=1；输出默认 doc/验收/主界面截图复核 v1.2
//   环境变量：CHROME（浏览器路径）、PLAYTEST_PORT（调试端口，默认 9336）、SIZES=1280x720,1600x900（只跑部分档）、SKIP_FOLD=1
//
// 场景（每档 1280×720 / 1600×900 / 1920×1080）：
//   new      新局读完开场对白：火堆未点燃、空加工位、锅在随身
//   empty    营地：火堆熄灭0燃料、两个空加工位、棚/床未建
//   full     营地：火堆燃烧、锅满（水1000毫升+2份生块根）、直烤位1份生块根、遮蔽棚已建、随身五张卡（含“未鉴定的活性物质”最长名）
//   fuelfull 营地：同 full，燃料满240、床垫已建
//   hardware 五金店：资源卡（可搜/已搜空、不能开始的原因）
//   field    农野荒地：块根地、荒地纤维丛（长名+存量角标+日补）、可能有游荡尸敌踪卡（M90 加）
//   battle   首战两行战斗（B2a M94 加）：默认、选技能瞄准、悬停目标、选中敌卡、最长意图文案“咬击 0.70—1.10（可能感染）”；
//            测敌卡信息栏不溢出、文字到可见框内缘 ≥8px。两行战斗面板标题/操作台标签离铁框不足是 B2 已记录项，只记在 known 不判失败。
//   环境变量 SCENES=new,full… 只跑部分场景。
// 每个场景测默认、选地面卡、选随身最长名卡、选火堆、选火堆里的锅、悬停地面卡，键盘聚焦锅/直烤（Q35）、第一档切屏高800/801（Q35），开地图/制作面板。
// 测量口径（S14 截图复核 §B）：
//   - 火堆外框与同组遮蔽棚/床垫顶、底各差 ≤1px，各状态前后火堆高度不变（UI-M04）。
//   - 可见框体厚度：border-image 画出的框线约占 border-image-width 的 15/24（纸框约6.3px），铁框约 13/24（约6.5px），无 border-image 时取边框宽；
//     文字（按字形行框）到可见框内缘 ≥8px，极紧凑角标（数量/占格/状态角标/转移箭头按钮框）≥6px（UI-M06）。只计在所在滚动区可见的部分。
//   - 主图：按 viewBox 等比适配算出主体实际所在矩形，不被裁（祖先 overflow 与卡框内缘）、不被同卡文字/角标/按钮压住（>2%面积才报）、
//     精灵图无 clip-path（UI-M05）；记录主体高/卡高，供人工对照“约70%”。
//   - 物品卡名称与转移箭头不相交；卡片外缘不压所在行框。
//   - 首屏可见（M90）：设施行在最左时火堆（全部按钮与加工位）、遮蔽棚、床垫整张可见，屏高≥1000 时采集资源标题与第一项可见；
//     火堆宽度在同一档尺寸的所有场景/状态里不变；单行省略文字不被盒高切掉字底；卡内文字不被同卡按钮压住。
//   - B2a 答复 Q36（UI-B2-36a，M93）：资源/敌踪/相邻地点卡各尺寸上图下文、不套横卡；1280×720 主图主体约35%—45%（低于约0.33报错）；相邻地点首卡与角标在子栏内可见。
//   - B2a 答复 Q35（UI-B2-35a，M92）：不常驻的锅编号/重量、加工位编号、长名有悬停与键盘聚焦全文；≥801 常驻；800/801 切换不改选择/批次/折叠。
//   - B2a 答复 Q34（UI-B2-34a/b/c，M91）：③④为竖排行首标签（无横向区标题带、标签不截、卡区不压标签）；左栏携重摘要常驻两位小数总重量、
//     轻装/超重、随身 N/5张卡；结束时按尺寸×卡类汇总主体高/卡高（report.ratios）。
//   - 右栏折叠（UI-M07—M09）：四种组合各经地图/制作/休息/日志/系统说明开关、对象信息↔人物切换、刷新、读旧档后保持；对白锁期间点击不改变。
// 结果写 <输出目录>/v12-results.json，截图写 <输出目录>/screenshots/。自动测量不替代人工逐图确认纹理与装饰实边（S14 §B 最后一段）。
'use strict';
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

const url = process.argv[2] || 'https://etsevenday.github.io/shizheshengcun/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/主界面截图复核 v1.2');
const PORT = Number(process.env.PLAYTEST_PORT || 9336);
const SAVE_KEY = 'survival.save.v1';
const SIZES = (process.env.SIZES || '1280x720,1600x900,1920x1080').split(',').map(s => s.split('x').map(Number));
const CHROMES = [process.env.CHROME, 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].filter(Boolean);
const FIXTURES = 'doc/验收/主界面美术换肤 v0.8/fixtures.json';
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
async function closeLayers() {
  for (let i = 0; i < 3; i++) {
    const has = await ev(`!!document.querySelector('.apop, .sub-panel, .card.selected, .pot-card.selected, .fc-food.selected, .ocard.selected, .pc-row.selected')`);
    if (!has) break;
    await key('Escape');
  }
  if (await ev(`!!document.querySelector('.overlay[role=dialog]')`)) await key('Escape');
}
const savedJson = () => ev(`localStorage.getItem(${JSON.stringify(SAVE_KEY)})`);

// ───────────── 测试状态：在已迁移到当前存档版本的营地存档上改出火堆/锅/随身组合 ─────────────
function stage(json, mode) {
  const s = JSON.parse(json);
  const base = { ExpiresAt: null, Durability: null, MaxDurability: null, LoadedAmmo: 0, LoadedAmmoId: null, ReservedBy: null, PlacedOrder: null, Source: null, InContainer: false, InFacility: false, IsCarried: false };
  let id = s.NextInstanceId;
  const add = (ItemId, Count, Owner, extra = {}) => { const it = { ...base, InstanceId: id++, ItemId, Count, Owner, ...extra }; s.Items.push(it); return it; };
  const fac = s.Locations.camp.Facility;
  s.Items = s.Items.filter(i => i.Owner === 'camp' || i.ItemId === 'lighter' || i.ItemId === 'pot');
  const pot = s.Items.find(i => i.ItemId === 'pot');
  if (mode === 'empty') {
    Object.assign(pot, { Owner: 'carried', IsCarried: true, InFacility: false });
    Object.assign(fac, { FireLit: false, FuelMinutes: 0, WentOut: true, AttachedPotId: null, ProcessingSlots: [null, null], Processes: [], Completed: [], ShelterBuilt: false, BedBuilt: false });
  } else {
    Object.assign(pot, { Owner: 'facility:camp:fire', IsCarried: false, InFacility: true });
    add('clean_water', 2, 'pot:' + pot.InstanceId, { InContainer: true });
    add('raw_root', 1, 'pot:' + pot.InstanceId, { InContainer: true, ExpiresAt: s.GameMinute + 4000 });
    add('raw_root', 1, 'pot:' + pot.InstanceId, { InContainer: true, ExpiresAt: s.GameMinute + 4200 });
    const r = add('raw_root', 1, 'facility:camp:fire', { InFacility: true, ExpiresAt: s.GameMinute + 4300 });
    Object.assign(fac, { FireLit: true, FuelMinutes: mode === 'fuelfull' ? 240 : 180, WentOut: false, AttachedPotId: pot.InstanceId,
      ProcessingSlots: [pot.InstanceId, r.InstanceId], Processes: [], Completed: [], ShelterBuilt: true, BedBuilt: mode === 'fuelfull' });
    add('fiber', 3, 'carried', { IsCarried: true });
    add('active_substance_white_unidentified', 1, 'carried', { IsCarried: true });
    add('canned_food', 2, 'carried', { IsCarried: true });
    add('bandage', 1, 'carried', { IsCarried: true });
    add('active_substance_white_unidentified', 1, 'camp');
    add('antiviral_precursor', 1, 'camp');
    add('cooked_root', 3, 'camp', { ExpiresAt: s.GameMinute + 1440 });
    add('cooked_root', 1, 'camp', { ExpiresAt: s.GameMinute + 4320 });   // 地面同物品第二批（M95：手动选批后的转移提示）
  }
  s.NextInstanceId = id;
  return JSON.stringify(s);
}
/** 先按一次 Tab（让后续 focus() 算键盘聚焦），再聚焦目标；返回提示是否出现、内容是否含期望全文。元素不存在时算未执行。 */
async function focusTip(sel, re) {
  const ok = await ev(`(() => { const e = [...document.querySelectorAll(${JSON.stringify(sel)})].find(x => x.getBoundingClientRect().width > 0); if (!e) return false; e.scrollIntoView({ block: 'nearest', inline: 'nearest' }); return true; })()`);
  if (!ok) return false;
  await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Shift', code: 'ShiftLeft', windowsVirtualKeyCode: 16 });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Shift', code: 'ShiftLeft', windowsVirtualKeyCode: 16 });
  const r = await ev(`(() => { const e = [...document.querySelectorAll(${JSON.stringify(sel)})].find(x => x.getBoundingClientRect().width > 0); if (document.activeElement === e) e.blur(); e.focus(); return new Promise(ok => setTimeout(() => {
    const t = document.querySelector('.focus-tip'); const shown = !!t && !t.hidden && t.getBoundingClientRect().width > 0;
    ok({ shown, text: shown ? t.textContent : '', fv: e.matches(':focus-visible'), active: document.activeElement === e, tip: e.getAttribute('data-tip') }); }, 200)); })()`);
  const issues = [];
  if (!r.shown) issues.push('键盘聚焦后没有浮出提示：' + sel + (r.fv ? '' : '（未处于键盘聚焦）') + (r.active ? '' : '（焦点不在元素上）') + (r.tip ? '' : '（元素无 data-tip）'));
  else if (!re.test(r.text)) issues.push('键盘聚焦提示缺全文：' + r.text);
  return { issues };
}
/** 选中随身最长名卡（多批时点后层选批），记下选择/批次/折叠，切到屏高801再切回800与原尺寸，比较是否不变。 */
async function resizeKeep(w, h) {
  const pick = async () => {
    const layer = await centerOf('.section.carried .stack-layer');
    if (layer) { await clickAt(layer.x, layer.y); return; }
    const i = await ev(`[...document.querySelectorAll('.section.carried .card.item')].findIndex(c => c.innerText.includes('未鉴定'))`);
    await clickSel('.section.carried .card.item', Math.max(0, i));
  };
  await pick();
  const snap = () => ev(`(() => { const s = document.getElementById('card-sel'); const on = document.querySelector('.stack-layer.on, .ca-batch.on');
    const f = k => { const x = document.querySelector('[data-fold=' + k + ']'); return x ? x.classList.contains('open') : null; };
    return JSON.stringify({ sel: s && (s.getAttribute('data-card') || s.closest('[data-card]')?.getAttribute('data-card')), batch: on && (on.getAttribute('aria-label') || on.innerText), skills: f('skills'), traits: f('traits'), save: localStorage.getItem(${JSON.stringify(SAVE_KEY)}) }); })()`);
  const a = await snap();
  await size(w, 801); const b = await snap();
  await size(w, 800); const c = await snap();
  await size(w, h); const d = await snap();
  const issues = [];
  if (!JSON.parse(a).sel) issues.push('切尺寸前没选中卡');
  for (const [n, x] of [['801', b], ['800', c], ['原尺寸', d]]) if (x !== a) issues.push('切到屏高' + n + '后选择/批次/折叠/存档变化');
  return { issues };
}
let campBase = null;
async function loadScene(scene, fixtures) {
  if (scene === 'new') { await loadState(null); await readDialogues(); return; }
  if (scene === 'battle') {
    // 首战（两行战斗）：新局读完开场对白 → 前往农野荒地 → 读完首战对白
    await loadState(null); await readDialogues();
    await clickSel('.map-adjacent .loc-card');
    if (!await clickText('确认移动')) throw new Error('找不到“确认移动”');
    if (!await waitFor(`!!document.querySelector('.bt-team') || !!document.querySelector('.dlg-layer')`, 60000)) throw new Error('没进入首战');
    await readDialogues();
    if (!await waitFor(`!!document.querySelector('.bt-team')`, 10000)) throw new Error('没进入两行战斗');
    await sleep(400); return;
  }
  if (scene === 'hardware') {
    await loadState(fixtures.hardware);
    if (await ev(`!!document.querySelector('.overflow-layer')`)) {
      await ev(`(() => { const ins = [...document.querySelectorAll('.overflow-layer .ov-item input')]; ins.slice(0, 2).forEach(i => i.click()); return ins.length; })()`);
      await sleep(300); await clickText('保留所选');
    }
    await sleep(400); return;
  }
  if (!campBase) { await loadState(fixtures.camp); campBase = await savedJson(); }
  if (scene === 'field') {
    const st = JSON.parse(campBase); st.CurrentLocation = 'field';
    for (const k of ['roots', 'field_fiber']) if (st.Resources[k]) st.Resources[k].Discovered = true;
    await loadState(JSON.stringify(st)); return;
  }
  await loadState(stage(campBase, scene));
}

// ───────────── 页面内测量 ─────────────
const MEASURE = `(() => {
  const R = e => { const r = e.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height, r: r.right, b: r.bottom }; };
  const rr = r => ({ x: r.x, y: r.y, w: r.width ?? r.w, h: r.height ?? r.h, r: r.right ?? r.r, b: r.bottom ?? r.b });
  const isect = (a, b) => { const x = Math.max(a.x, b.x), y = Math.max(a.y, b.y), r = Math.min(a.r, b.r), bt = Math.min(a.b, b.b); return r > x && bt > y ? { x, y, r, b: bt, w: r - x, h: bt - y } : null; };
  const area = a => a ? a.w * a.h : 0;
  const vis = e => { const r = e.getBoundingClientRect(); const st = getComputedStyle(e); return r.width > 0 && r.height > 0 && st.visibility !== 'hidden' && st.display !== 'none' && +st.opacity !== 0; };
  const f1 = v => Math.round(v * 10) / 10;
  const clipOf = e => {
    let c = { x: 0, y: 0, r: innerWidth, b: innerHeight };
    for (let p = e.parentElement; p && p !== document.body; p = p.parentElement) {
      const cs = getComputedStyle(p);
      if (cs.overflowX !== 'visible' || cs.overflowY !== 'visible') { const pr = rr(p.getBoundingClientRect()); c = { x: Math.max(c.x, pr.x), y: Math.max(c.y, pr.y), r: Math.min(c.r, pr.r), b: Math.min(c.b, pr.b) }; }
    }
    c.w = c.r - c.x; c.h = c.b - c.y; return c;
  };
  const frameOf = e => {
    const cs = getComputedStyle(e);
    if (cs.borderImageSource && cs.borderImageSource !== 'none') {
      const w = cs.borderImageWidth.split(' ')[0]; const bw = parseFloat(cs.borderTopWidth) || 0;
      const px = w.endsWith('px') ? parseFloat(w) : w === 'auto' ? bw : parseFloat(w) * bw;
      // 纸框/选中/悬停框：画出的框线约到 15/24；铁框（地块行、左右栏、浮窗）约到 13/24（frame-iron.svg 内轮廓在第 11—13 单位）
      const k = /frame-iron/.test(cs.borderImageSource) ? 13 / 24 : 15 / 24;
      return Math.max(bw, px * k);
    }
    return parseFloat(cs.borderTopWidth) || 0;
  };
  const issues = [], known = [];
  const label = e => { const c = e.closest('.card.item, .ocard, .pot-card, .fc-food, .apop, .panel, .section'); const t = c && (c.getAttribute('aria-label') || (c.querySelector('.ctitle span, .oc-head b, .pc-title b, .pc-name, .apop-title') || {}).innerText || c.className); return String(t || '').trim().replace(/\\s+/g, ' ').slice(0, 16); };
  // 1 火堆与同组设施等高（UI-M04）
  const fire = document.querySelector('.fire-card');
  let fireInfo = null;
  if (fire && vis(fire)) {
    const fr = R(fire); const facs = [...fire.parentElement.querySelectorAll(':scope > .fac-card')].filter(vis).map(R);
    fireInfo = { top: f1(fr.y), bottom: f1(fr.b), h: f1(fr.h), w: f1(fr.w), facs: facs.map(f => ({ top: f1(f.y), bottom: f1(f.b), h: f1(f.h), w: f1(f.w) })) };
    fireInfo.maxDiff = facs.length ? f1(Math.max(...facs.map(f => Math.max(Math.abs(f.y - fr.y), Math.abs(f.b - fr.b))))) : null;
    if (fireInfo.maxDiff != null && fireInfo.maxDiff > 1) issues.push('火堆与棚/床顶底差' + fireInfo.maxDiff + 'px >1px');
    const dg = fire.parentElement && R(fire.parentElement);
    if (dg && (fr.b > dg.b + 1 || fr.y < dg.y - 1)) issues.push('火堆超出设施组高度');
    const art = fire.querySelector('.fc-art'); if (art) fireInfo.art = f1(R(art).h);
    // 必要燃料状态、槽位与主要动作在首屏（卡内可见）
    for (const [sel, name] of [['.fuel-line', '燃料数值'], ['.fuel-track', '燃料条'], ['.fc-foot .fc-btn', '投木/点火按钮'], ['.fc-slots > *', '加工位']]) {
      const es = [...fire.querySelectorAll(sel)].filter(vis);
      if (!es.length) { issues.push('火堆缺少' + name); continue; }
      for (const e of es) { const a = R(e); if (a.b > fr.b + 0.5 || a.y < fr.y - 0.5) issues.push('火堆' + name + '被卡底/顶裁切'); }
    }
  }
  // 1b 首屏可见（M90）：设施行滚动区在最左时，火堆（含全部按钮与两个加工位）、遮蔽棚、床垫整张在可见区内；
  //    屏高≥1000 时“采集资源”分组标题与第一项也要整块可见。火堆宽度不随内容变（各状态比较见主流程）。
  const facScroll = document.querySelector('.center-scroll.four .fac-scroll');
  const firstScreen = {};
  if (facScroll && facScroll.scrollLeft < 1) {
    const z = R(facScroll);
    const frac = e => { const a = R(e); return a.w > 0 ? Math.max(0, Math.min(a.r, z.r) - Math.max(a.x, z.x)) / a.w : 0; };
    const must = [];
    if (fire && vis(fire)) must.push([fire, '火堆'], ...[...fire.querySelectorAll('.fc-btn, .fc-slots > *, .fc-slot :is(.card-arrow, .row-arrow)')].filter(vis).map(e => [e, '火堆内' + ((e.innerText || e.getAttribute('aria-label') || '').trim().split('\\n')[0].slice(0, 8))]));
    for (const f of [...document.querySelectorAll('.desk-group.facilities .fac-card')].filter(vis)) must.push([f, (f.querySelector('.oc-head b') || {}).innerText || '设施卡']);
    if (innerHeight >= 1000) { const g = document.querySelector('.desk-group.resources'); if (g) { const t = g.querySelector('.dg-title'), c = g.querySelector('.dg-cards > *'); if (t) must.push([t, '采集资源标题']); if (c) must.push([c, '采集资源第一项']); } }
    for (const [e, n] of must) { const v = frac(e); firstScreen[n] = f1(v * 100); if (v < 0.99) issues.push('首屏不可见：' + n + ' 可见' + f1(v * 100) + '%'); }
  }
  // 1c 文字被盒子切掉（M90）：overflow 隐藏、未用多行截断的单行/省略文字，若内容高于盒高（字底被切）报错；
  //    卡内文字行与同卡按钮相交（文字被按钮压住）报错
  for (const e of [...document.querySelectorAll('.center :is(.ff-status, .oc-head b, .oc-line, .hp-line, .ctitle > span, .cinfo, .pot-fill-head b, .pc-sum-label, .fc-btn small, .pc-title b, .pc-name, .fuel-line)')].filter(vis)) {
    const cs = getComputedStyle(e); if (cs.webkitLineClamp && cs.webkitLineClamp !== 'none') continue;
    // scrollHeight 取整、clientHeight 向下取整，按元素实际高度比较，超出 1px 以上才算切字（排除取整误差）
    if (cs.overflowY !== 'visible' && e.scrollHeight > e.getBoundingClientRect().height - (parseFloat(cs.borderTopWidth) || 0) - (parseFloat(cs.borderBottomWidth) || 0) + 1) issues.push('字底被切 ' + label(e) + '「' + e.innerText.trim().slice(0, 10) + '」' + e.scrollHeight + '>' + e.clientHeight);
  }
  for (const c of [...document.querySelectorAll('.center :is(.ocard, .pot-card, .fc-food, .card.item)')].filter(vis)) {
    const btns = [...c.querySelectorAll('button')].filter(vis).map(R);
    for (const t of [...c.querySelectorAll('.oc-line, .hp-line, .oc-head b, .oc-badge, .cinfo, .ctitle > span, .ff-status, .fc-title b, .fuel-line')].filter(vis)) {
      if (t.closest('button')) continue;
      // 角标/文字盒本身越出所在列压到按钮（如 1920“燃烧中”角标压“等待烹饪”）也算
      for (const b of btns) { const i = isect(R(t), b); if (i && i.w > 0.5 && i.h > 0.5) { issues.push('文字盒压住按钮 ' + label(c) + '「' + t.innerText.trim().slice(0, 10) + '」'); break; } }
      const rg = document.createRange(); rg.selectNodeContents(t);
      const clip = clipOf(t);
      for (const r0 of rg.getClientRects()) { const r = isect(rr(r0), clip); if (!r) continue; for (const b of btns) { const i = isect(r, b); if (i && i.w > 0.5 && i.h > 0.5) issues.push('文字被按钮压住 ' + label(c) + '「' + t.innerText.trim().slice(0, 10) + '」'); } }
    }
  }
  // 1d B2a 答复 Q34（UI-B2-34a/b）：③④为竖排行首标签、横向区标题带已释放；携重总重量（公斤两位）/负重状态/N/5张卡在左栏携重摘要常驻
  const q34 = {};
  for (const sec of [...document.querySelectorAll('.center-scroll.four > .section.ground, .center-scroll.four > .section.carried')].filter(vis)) {
    const k = sec.classList.contains('ground') ? '当前地面' : '随身';
    const tag = sec.querySelector(':scope > .row-tag');
    if (!tag || !vis(tag)) { issues.push('③④缺竖排行首标签：' + k); continue; }
    if (!/^vertical/.test(getComputedStyle(tag).writingMode)) issues.push('③④行首标签不是竖排：' + k);
    if (sec.querySelector(':scope > .section-head')) issues.push('③④仍有横向区标题带：' + k);
    const tr = R(tag), sr = R(sec), sc = sec.querySelector(':scope > .row-scroll');
    // 标签可读完整（文字不被标签盒裁掉）、卡区与标签同一行起（标题带释放后卡区顶端紧跟行内边距）
    if (tag.scrollHeight > tr.h + 1) issues.push('③④行首标签被截：' + k);
    if (sc) { const cr = R(sc); q34[k] = { tagW: f1(tr.w), rowH: f1(sr.h), scrollTop: f1(cr.y - sr.y), scrollH: f1(cr.h) }; if (cr.x < tr.r - 0.5) issues.push('③④卡区压住行首标签：' + k); }
  }
  const wt = document.querySelector('.sidebar .weight');
  if (wt && vis(wt)) {
    const t = wt.innerText.replace(/\\s+/g, ' ');
    q34.weight = t;
    if (!/\\d+\\.\\d{2}公斤/.test(t)) issues.push('左栏携重缺两位小数总重量');
    if (!/(轻装|超重)/.test(t)) issues.push('左栏携重缺负重状态');
    if (!/\\d+\\/\\d+张卡/.test(t)) issues.push('左栏携重缺随身占格 N/5');
    const wc = wt.querySelector('.weight-cards'); if (wc) { const a = R(wc), b = R(wt); if (a.r > b.r + 0.5 || a.b > b.b + 0.5) issues.push('左栏随身占格越出携重面板'); }
  }
  // 1e B2a 答复 Q35（UI-B2-35a，M92）：屏高≤800 可不常驻锅“#编号·重量”、直烤“加工位N”、物品长名，但悬停（title）与键盘聚焦（data-tip）
  //    必须有等价全文；屏高≥801 恢复常驻。键盘聚焦提示浮层在视口内、内容与焦点元素 data-tip 一致。
  const compact = innerHeight <= 800;
  const q35 = { compact, potHidden: 0, slotHidden: 0, nameCut: 0 };
  for (const h of [...document.querySelectorAll('.center .pot-card.compact .pc-head')].filter(vis)) {
    const sm = h.querySelector('.pc-title small'); const tipT = h.getAttribute('data-tip') || ''; const ttl = h.getAttribute('title') || '';
    const shown = sm && vis(sm) && sm.scrollWidth <= sm.clientWidth + 1;
    if (!shown) { q35.potHidden++; if (!/#\\d+/.test(tipT) || !/公斤/.test(tipT) || !/#\\d+/.test(ttl)) issues.push('锅编号/重量不常驻但提示缺全文'); }
    if (!compact && !(sm && vis(sm) && /#\\d+.*公斤/.test(sm.textContent) && sm.scrollWidth <= sm.clientWidth + 1)) issues.push('屏高≥801 锅“#编号·重量”未完整常驻：' + (sm ? sm.textContent : ''));
  }
  for (const f of [...document.querySelectorAll('.center .fire-card .fc-food')].filter(vis)) {
    const st = f.querySelector('.pc-status'); const tipT = f.getAttribute('data-tip') || ''; const ttl = f.getAttribute('title') || '';
    if (!(st && vis(st))) { q35.slotHidden++; if (!/加工位\\d/.test(tipT) || !/加工位\\d/.test(ttl)) issues.push('直烤加工位编号不常驻但提示缺全文'); }
    if (!compact && !(st && vis(st))) issues.push('屏高≥801 直烤“加工位N”未常驻');
  }
  for (const c of [...document.querySelectorAll('.center .card.item')].filter(vis)) {
    const n = c.querySelector('.ctitle > span:first-child'); if (!n) continue;
    if (n.scrollWidth > n.clientWidth + 1 || n.scrollHeight > n.clientHeight + 1) {
      q35.nameCut++; const full = n.getAttribute('title') || '';
      if (!full || !(c.getAttribute('data-tip') || '').includes(full.split('#')[0])) issues.push('物品长名省略但缺全名提示 ' + full);
    }
  }
  const ft = document.querySelector('.focus-tip'); if (ft && vis(ft)) {
    const fr = R(ft); q35.focusTip = ft.textContent;
    if (fr.x < -0.5 || fr.y < -0.5 || fr.r > innerWidth + 0.5 || fr.b > innerHeight + 0.5) issues.push('键盘聚焦提示超出视口');
    const a = document.activeElement; if (!a || a.getAttribute('data-tip') !== ft.textContent) issues.push('键盘聚焦提示与焦点元素不一致');
  }
  // 2 文字与可见框内缘（UI-M06）
  const FRAMED = '.card.item, .ocard, .map-card, .pot-card, .fc-food, .apop, .panel, .stack-layer, .st-pop';
  const BADGE = '.qty, .slot-span, .oc-badge, .badge, .tag, .card-arrow, .row-arrow, .apop-x';
  const boxes = [...document.querySelectorAll(FRAMED)].filter(vis);
  // 横滚区里被滚到边缘、只露出一部分的卡/内容是“滚动可达”，不算贴边（与主图同一口径）
  const partlyScrolled = (el, B) => {
    const sc = el.closest('.row-scroll, .fac-scroll, .map-adjacent, .bc-groups'); if (!sc) return false;
    const a = R(sc.contains(B) ? B : el), z = R(sc); return a.x < z.x - 0.5 || a.r > z.r + 0.5;
  };
  const safety = []; let textChecked = 0;
  for (const B of boxes) {
    const fb = frameOf(B); const br = R(B);
    const inner = { x: br.x + fb, y: br.y + fb, r: br.r - fb, b: br.b - fb };
    const walker = document.createTreeWalker(B, NodeFilter.SHOW_TEXT);
    let minD = Infinity, worst = null;
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      if (!n.textContent.trim()) continue;
      const el = n.parentElement; if (!el || !vis(el) || el.closest(FRAMED) !== B) continue;
      if (partlyScrolled(el, B)) continue;
      const badge = !!el.closest(BADGE); const need = badge ? 6 : 8;
      const rg = document.createRange(); rg.selectNodeContents(n);
      // 元素自身 overflow 隐藏（省略号单行）时，被省略掉的那段字不显示，也要按元素自身裁掉
      let clip = clipOf(el); const ec = rr(el.getBoundingClientRect());
      { const ecs = getComputedStyle(el); if (ecs.overflowX !== 'visible' || ecs.overflowY !== 'visible') clip = isect(clip, ec) || { x: 0, y: 0, r: 0, b: 0, w: 0, h: 0 }; }
      for (const r0 of rg.getClientRects()) {
        let r = isect(rr(r0), clip); if (!r) continue;
        r = isect(r, { x: Math.min(ec.x, br.x), y: Math.min(ec.y, br.y), r: Math.max(ec.r, br.r), b: Math.max(ec.b, br.b) }); if (!r) continue;
        // 在框外的（溢出被父级裁掉的）不算
        r = isect(r, br); if (!r || r.w < 1 || r.h < 1) continue;
        textChecked++;
        const d = Math.min(r.x - inner.x, inner.r - r.r, r.y - inner.y, inner.b - r.b);
        if (d - need < minD) { minD = d - need; worst = { text: n.textContent.trim().slice(0, 12), d: f1(d), need, badge }; }
      }
    }
    // 按钮/角标框本身（极紧凑元素，≥6px）
    for (const b of [...B.querySelectorAll('button, .qty, .slot-span, .oc-badge')].filter(vis)) {
      if (b.closest(FRAMED) !== B || partlyScrolled(b, B)) continue;
      let r = isect(R(b), clipOf(b)); if (!r) continue; r = isect(r, br); if (!r || r.w < 2 || r.h < 2) continue;
      const d = Math.min(r.x - inner.x, inner.r - r.r, r.y - inner.y, inner.b - r.b);
      if (d - 6 < minD) { minD = d - 6; worst = { text: '[' + (b.getAttribute('aria-label') || b.innerText || b.className).trim().slice(0, 10) + ']', d: f1(d), need: 6, badge: true }; }
    }
    if (worst) safety.push({ box: label(B), cls: String(B.className).split(' ').slice(0, 2).join('.'), frame: f1(fb), ...worst });
    // 两行战斗的队伍面板标题、操作台“装备”标签离铁框不足 8px 是 B2 已记录的已知项（两行战斗由 B9 全屏战斗取代），只记录不判失败
    if (worst && worst.d < worst.need - 0.5) (B.matches('.battle-overlay') ? known : issues).push('安全区 ' + label(B) + '「' + worst.text + '」距框内缘' + worst.d + 'px<' + worst.need);
  }
  // 3 物品卡：名称与箭头不相交；卡片外缘不压行框
  for (const c of [...document.querySelectorAll('.center .card.item')].filter(vis)) {
    const n = c.querySelector('.ctitle > span'), a = c.querySelector('.card-arrow');
    if (n && a && vis(a)) { const i = isect(R(n), R(a)); if (i && i.w > 0.5 && i.h > 0.5) issues.push('名称与箭头重叠 ' + label(c) + ' ' + f1(i.w) + 'px'); }
  }
  for (const c of [...document.querySelectorAll('.center :is(.card.item, .ocard, .pot-card)')].filter(vis)) {
    const row = c.closest('.panel, .section'); if (!row || row === c) continue;
    const fb = frameOf(row); const rw = R(row); const cr = R(c);
    if (cr.y < rw.y + fb - 0.5 || cr.b > rw.b - fb + 0.5) issues.push('卡片压住行框 ' + label(c));
  }
  // 4 主图完整（UI-M05）：主体矩形 = viewBox 等比适配；不被裁、不被同卡文字/角标/按钮压住；无 clip-path
  const arts = [];
  for (const svg of [...document.querySelectorAll('.center svg.ink-sprite, .detail svg.ink-sprite')].filter(vis)) {
    const card = svg.closest('.card.item, .ocard, .pot-card, .fc-food, .stack-layer, .detail-icon, .fire-card');
    if (!card || card.classList.contains('stack-layer')) continue;
    const vb = (svg.getAttribute('viewBox') || '0 0 100 100').split(/\\s+/).map(Number);
    const sr = R(svg); const k = Math.min(sr.w / vb[2], sr.h / vb[3]);
    const sw = vb[2] * k, sh = vb[3] * k; const sub = { x: sr.x + (sr.w - sw) / 2, y: sr.y + (sr.h - sh) / 2, w: sw, h: sh }; sub.r = sub.x + sw; sub.b = sub.y + sh;
    const cs = getComputedStyle(svg);
    let clipBox = clipOf(svg); const fb = frameOf(card); const cr = R(card);
    clipBox = isect(clipBox, { x: cr.x + fb, y: cr.y + fb, r: cr.r - fb, b: cr.b - fb }) || { x: 0, y: 0, r: 0, b: 0, w: 0, h: 0 };
    const kept = area(isect(sub, clipBox)) / Math.max(1, area(sub));
    const inView = isect(sub, { x: 0, y: 0, r: innerWidth, b: innerHeight });
    const scroller = svg.closest('.row-scroll, .fac-scroll, .map-adjacent, .detail-scroll');
    const scrolledOut = scroller && !isect(R(card), R(scroller));
    const cl = card.classList;
    const kind = cl.contains('item') ? 'item' : cl.contains('res-card') ? 'res' : cl.contains('enemy-card') ? 'enemy' : cl.contains('fac-card') ? 'fac'
      : cl.contains('pot-card') ? 'pot' : cl.contains('fc-food') ? 'food' : cl.contains('fire-card') ? 'fire' : cl.contains('detail-icon') ? 'detail' : 'other';
    const info = { kind, key: svg.getAttribute('data-art'), card: label(card), subH: f1(sh), cardH: f1(cr.h), ratio: f1(sh / cr.h * 100) / 100, kept: f1(kept * 100) };
    if (cs.clipPath && cs.clipPath !== 'none') issues.push('主图有 clip-path：' + info.key);
    // 部分滚出所在横滚区的卡（地面/设施行最后一张）是“滚动可达”，不算裁切
    const partlyScrolled = scroller && !scrolledOut && (cr.x < R(scroller).x - 0.5 || cr.r > R(scroller).r + 0.5);
    if (inView && !scrolledOut && !partlyScrolled && kept < 0.99) issues.push('主图被裁 ' + info.card + ' 保留' + info.kept + '%');
    let covered = 0, by = '';
    for (const o of [...card.querySelectorAll('button, .qty, .slot-span, .oc-badge, .cinfo, .ctitle, .oc-head b, .oc-body, .oc-foot, .pc-title, .fc-title, .fuel-line, .ff-status')].filter(vis)) {
      if (o.contains(svg)) continue;
      const a = area(isect(sub, R(o))); if (a > covered) { covered = a; by = (o.innerText || o.className).trim().slice(0, 10); }
    }
    info.covered = f1(covered / Math.max(1, area(sub)) * 100);
    if (!scrolledOut && info.covered > 2) issues.push('主图被压 ' + info.card + '：' + by + ' ' + info.covered + '%');
    arts.push(info);
  }
  // 4b 相邻地点卡主图是场景背景（background-size: contain），按图片原始比例在框内等比适配算主体矩形
  for (const la of [...document.querySelectorAll('.center .loc-card.neighbor .loc-art')].filter(vis)) {
    const card = la.closest('.loc-card'); const cr = R(card), ar = R(la);
    const m = /url\\(["']?([^"')]+)/.exec(getComputedStyle(la).backgroundImage || ''); let sw = ar.w, sh = ar.h;
    if (m) { const im = new Image(); im.src = m[1]; if (im.naturalWidth && im.naturalHeight) { const k = Math.min(ar.w / im.naturalWidth, ar.h / im.naturalHeight); sw = im.naturalWidth * k; sh = im.naturalHeight * k; } }
    arts.push({ kind: 'loc', key: 'loc-art', card: label(card) || (card.querySelector('.oc-head b') || {}).innerText, subH: f1(sh), cardH: f1(cr.h), ratio: f1(sh / cr.h * 100) / 100, kept: 100, covered: 0 });
  }
  // 4c B2a 答复 Q36（UI-B2-36a，M93）：资源/敌踪/相邻地点卡各尺寸都是上图下文（主图在名称之上、不占满卡高），不再套 236/280px 横卡；
  //     1280×720 主图主体高/卡高约 35%—45%（取约 0.33 为下限，低于此报错）；相邻地点卡在子栏最左时整张（含右上角标与右框）可见。
  const q36 = [];
  for (const c of [...document.querySelectorAll('.center-scroll.four :is(.facility-row .res-card, .facility-row .enemy-card.world, .map-adjacent .loc-card.neighbor)')].filter(vis)) {
    const cr = R(c), art = c.querySelector('.oc-art'), head = c.querySelector('.oc-head');
    if (!art || !head) continue;
    const a = R(art), hd = R(head); const nm = (head.querySelector('b') || {}).innerText || '';
    q36.push({ name: nm, w: f1(cr.w), artH: f1(a.h), cardH: f1(cr.h) });
    if (a.b > hd.y + 1) issues.push('不是上图下文（主图不在名称之上）：' + nm);
    if (a.h > cr.h * 0.8) issues.push('主图占满卡高（疑似左图右文）：' + nm);
    if (cr.w > 220) issues.push('卡宽' + f1(cr.w) + 'px，疑似横卡：' + nm);
  }
  if (innerHeight <= 800) for (const a of arts) if (['res', 'enemy', 'loc'].includes(a.kind) && a.ratio < 0.33) issues.push('主图低于约35%：' + a.card + ' ' + a.ratio);
  const adj = document.querySelector('.center-scroll.four .map-adjacent');
  if (adj && adj.scrollLeft < 1) {
    const z = R(adj); const first = adj.querySelector('.loc-card.neighbor');
    if (first && vis(first)) {
      const fr = R(first); if (fr.r > z.r + 0.5) issues.push('相邻地点首卡比子栏可见宽还宽（右框被截）' + f1(fr.w) + '>' + f1(z.w));
      const bd = first.querySelector('.oc-badge'); if (bd && vis(bd)) { const b = R(bd); if (b.r > z.r + 0.5 || b.x < z.x - 0.5) issues.push('相邻地点访问角标被截'); }
    }
  }
  // 4d B2 遗留（B2a M94）：两行战斗敌卡信息栏（健康/意图）不溢出卡面，文字到敌卡可见框内缘 ≥8px（不缩字）
  const foes = [];
  for (const f of [...document.querySelectorAll('.bt-scene .foe')].filter(vis)) {
    const fb = frameOf(f), fr = R(f); const inner = { x: fr.x + fb, y: fr.y + fb, r: fr.r - fb, b: fr.b - fb };
    const body = f.querySelector('.foe-body'); if (!body) continue;
    const name = (f.querySelector('.foe-head b') || {}).innerText || '敌卡';
    if (body.scrollHeight > body.clientHeight + 1) issues.push('敌卡信息栏溢出 ' + name + ' ' + body.scrollHeight + '>' + body.clientHeight);
    const main = f.querySelector('.foe-main'); if (main && R(body).b > R(main).b + 0.5) issues.push('敌卡信息栏越出卡面 ' + name);
    let minD = Infinity, worst = '';
    const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      if (!n.textContent.trim() || !vis(n.parentElement)) continue;
      const rg = document.createRange(); rg.selectNodeContents(n);
      for (const r0 of rg.getClientRects()) { const r = isect(rr(r0), fr); if (!r || r.w < 1 || r.h < 1) continue;
        const d = Math.min(r.x - inner.x, inner.r - r.r, r.y - inner.y, inner.b - r.b); if (d < minD) { minD = d; worst = n.textContent.trim().slice(0, 12); } }
    }
    foes.push({ name, w: f1(fr.w), h: f1(fr.h), minD: f1(minD), worst });
    if (minD < 7.5) issues.push('敌卡文字距可见框内缘 ' + name + '「' + worst + '」' + f1(minD) + 'px<8');
  }
  // 5 选卡浮窗：在视口内、不撑高行、不压所选卡主图
  let pop = null;
  const pe = document.querySelector('.apop'); if (pe && vis(pe)) {
    const pr = R(pe); pop = { cls: String(pe.className), x: f1(pr.x), y: f1(pr.y), w: f1(pr.w), h: f1(pr.h) };
    if (pr.x < -0.5 || pr.y < -0.5 || pr.r > innerWidth + 0.5 || pr.b > innerHeight + 0.5) issues.push('浮窗超出视口');
    const sel = document.getElementById('card-sel'); const art = sel && (sel.querySelector('svg.ink-sprite') || null);
    if (art) { const a = area(isect(pr, R(art))); if (a > R(art).w * R(art).h * 0.05) issues.push('浮窗压住所选卡主图'); }
  }
  const grid = document.querySelector('.center-scroll.four');
  const tracks = grid ? getComputedStyle(grid).gridTemplateRows.split(' ').map(parseFloat) : [];
  if (tracks.length === 4 && Math.max(...tracks) - Math.min(...tracks) > 1) issues.push('四行不等高：' + tracks.join(','));
  return { viewport: [innerWidth, innerHeight], tracks, fire: fireInfo, firstScreen, q34, q35, q36, foes, known, textChecked, safety: safety.sort((a, b) => (a.d - a.need) - (b.d - b.need)).slice(0, 12), arts, pop, issues };
})()`;

const FOLD = `(() => { const g = k => { const sec = document.querySelector('[data-fold=' + k + ']'); const b = sec && sec.querySelector('.fold-head'); return b ? { open: sec.classList.contains('open'), disabled: b.disabled } : null; };
  return { skills: g('skills'), traits: g('traits') }; })()`;

// ───────────── 主流程 ─────────────
(async () => {
  fs.mkdirSync(path.join(out, 'screenshots'), { recursive: true });
  const exe = CHROMES.find(p => fs.existsSync(p));
  if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'survival-v12-'));
  const chrome = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${prof}`, '--window-size=1920,1080',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
  const report = { url, startedAt: new Date().toISOString(), results: [], fold: [] };
  const save = () => fs.writeFileSync(path.join(out, 'v12-results.json'), JSON.stringify(report, null, 2));
  try {
    await connect();
    await send('Page.enable'); await send('Runtime.enable');
    const fixtures = fs.existsSync(FIXTURES) ? JSON.parse(fs.readFileSync(FIXTURES, 'utf8')) : {};
    const scenes = (process.env.SCENES || 'new,empty,full,fuelfull,hardware,field,battle').split(',');
    const fireW0 = {};
    for (const [w, h] of SIZES) {
      await size(w, h);
      for (const scene of scenes) {
        const rec = { size: `${w}x${h}`, scene, states: [] };
        report.results.push(rec);
        try {
          await loadScene(scene, fixtures);
          let fireH0 = null;
          const step = async (name, act, check) => {
            let did = true, extra = null;
            try { const v = await act(); did = v !== false; extra = v && v.issues; } catch (e) { did = false; }
            await sleep(350);
            const m = await ev(MEASURE);
            if (extra && extra.length) m.issues.push(...extra);
            if (m.fire) { if (fireH0 == null) fireH0 = m.fire.h; else if (Math.abs(m.fire.h - fireH0) > 1) m.issues.push('火堆高度随状态变化：' + fireH0 + '→' + m.fire.h); }
            // 火堆宽度固定（M90）：同一档尺寸下所有场景、状态（空/放锅/直烤/选中）宽度一致
            if (m.fire) { const k = `${w}x${h}`; if (fireW0[k] == null) fireW0[k] = m.fire.w; else if (Math.abs(m.fire.w - fireW0[k]) > 1) m.issues.push('火堆宽度随内容变化：' + fireW0[k] + '→' + m.fire.w); }
            const file = await shot(`${scene}-${w}-${name}`);
            rec.states.push({ name, did, pass: m.issues.length === 0, screenshot: file, ...m });
            console.log(`${m.issues.length ? 'FAIL' : 'PASS'} ${w}x${h} ${scene} ${name}${did ? '' : '（未执行到）'}` +
              (m.fire ? ` 火堆高${m.fire.h}/棚床差${m.fire.maxDiff}` : '') + (m.issues.length ? '：' + [...new Set(m.issues)].join('；') : '') + (m.known && m.known.length ? '（已知未修：' + [...new Set(m.known)].join('；') + '）' : ''));
            save();
          };
          await step('default', async () => true);
          if (scene === 'battle') {
            // B2 遗留（B2a M94）：两行战斗敌卡意图长文案不溢出、文字到可见内缘 ≥8px；选技能瞄准、悬停目标、Esc 取消前后都测
            await step('battle-aim', () => clickSel('.bc-group.skills .cmdt:not(.off)'));
            await step('battle-hover-foe', () => hoverSel('.foe.targetable'));
            await key('Escape');
            await step('battle-select-foe', () => clickSel('.foe'));
            // 最长意图文案（咬击带“可能感染”，轮初随机抽到）：把首个敌卡意图文字换成该文案测排版（只改显示文字，不改战斗状态）
            await step('battle-long-intent', () => ev(`(() => { const it = document.querySelector('.bt-scene .foe .intent'); if (!it) return false;
              const tn = [...it.childNodes].filter(n => n.nodeType === 3 && n.textContent.trim()).pop(); if (!tn) return false;
              tn.textContent = '咬击 0.70\\u2060—\\u20601.10（可能感染）'; it.classList.add('bite');
              return true; })()`));
            continue;
          }
          await step('hover-ground', () => hoverSel('.section.ground .card.item'));
          await step('select-ground', () => clickSel('.section.ground .card.item'));
          await closeLayers();
          await step('select-carried-long', async () => {
            const i = await ev(`[...document.querySelectorAll('.section.carried .card.item')].findIndex(c => c.innerText.includes('未鉴定'))`);
            return clickSel('.section.carried .card.item', Math.max(0, i));
          });
          await closeLayers();
          await step('select-fire', () => clickSel('.fire-card .fc-fuel'));
          await closeLayers();
          await step('select-pot-in-fire', () => clickSel('.pot-card.in-fire .pc-head'));
          await closeLayers();
          // M95：地面同物品两批，未手动选批时提示“先取最早到期的有效批次”，在右栏手动选较晚一批后改写“转移所选批次”
          if (scene === 'full') await step('ground-batch-pick', async () => {
            const i = await ev(`[...document.querySelectorAll('.section.ground .card.item')].findIndex(c => c.innerText.includes('烤熟块根'))`);
            if (i < 0 || !await clickSel('.section.ground .card.item', i)) return false;
            const note = () => ev(`[...document.querySelectorAll('.apop .ca-note')].map(e => e.innerText).join(' / ')`);
            const a = await note();
            if (!await clickSel('.detail button.batch:not(.on)')) return false;
            await sleep(300); const b = await note();
            const issues = [];
            if (!/最早到期/.test(a)) issues.push('未选批时转移提示应写最早到期：' + a);
            if (!/所选批次/.test(b) || /最早到期/.test(b)) issues.push('手动选批后转移提示应写所选批次：' + b);
            return { issues };
          });
          await closeLayers();
          // Q35（UI-B2-35a）：键盘聚焦火堆里的锅/直烤食材，浮出提示写锅编号与重量、加工位编号的全文（提示内容本身在 MEASURE 里核对）
          await step('focus-pot-key', () => focusTip('.pot-card.in-fire .pc-head', /#\d+.*公斤/));
          await step('focus-food-key', () => focusTip('.fire-card .fc-food', /加工位\d/));
          await ev(`document.activeElement && document.activeElement.blur && document.activeElement.blur()`);
          // Q35：800/801 高切换只重排，不清选择/批次/折叠（每个场景在第一档尺寸上做一次）
          if (w === SIZES[0][0] && h === SIZES[0][1]) await step('resize-800-801', () => resizeKeep(w, h));
          await closeLayers();
          await step('panel-map', () => key('m'));
          await closeLayers();
          await step('panel-craft', () => key('c'));
          await closeLayers();
        } catch (e) {
          rec.error = String(e.stack || e);
          console.log(`FAIL ${w}x${h} ${scene}：${e.message}`);
        }
        save();
      }
    }
    // 右栏折叠记忆（UI-M07—M09），在第一档尺寸上跑一次
    if (!process.env.SKIP_FOLD) {
      const [w, h] = SIZES[0]; await size(w, h);
      const log = (name, ok, got) => { report.fold.push({ name, pass: !!ok, got }); console.log(`${ok ? 'PASS' : 'FAIL'} 折叠 ${name}：${JSON.stringify(got)}`); save(); };
      const st = () => ev(FOLD);
      const toPerson = async () => { await clickText('人物'); await sleep(300); };
      try {
        await ev(`localStorage.removeItem('survival.pref.fold')`).catch(() => { });
        await loadState(null);
        // 对白锁：开场对白期间折叠按钮不可点、点击不改变
        const locked0 = await st();
        await clickSel('[data-fold=skills] .fold-head');
        const locked1 = await st();
        log('对白期间不能点穿', !locked0.skills || (locked1.skills?.open === locked0.skills?.open), { before: locked0, after: locked1 });
        await readDialogues(); await toPerson();
        const d0 = await st();
        log('无偏好时默认两块展开', d0.skills?.open && d0.traits?.open, d0);
        const combos = [[false, true], [false, false], [true, false], [true, true]];
        for (const [sk, tr] of combos) {
          let cur = await st();
          if (cur.skills.open !== sk) await clickSel('[data-fold=skills] .fold-head');
          cur = await st();
          if (cur.traits.open !== tr) await clickSel('[data-fold=traits] .fold-head');
          const tag = `熟练度${sk ? '展开' : '收起'}/词条${tr ? '展开' : '收起'}`;
          for (const k of ['m', 'c', 'r', 'l']) { await key(k); await key('Escape'); }
          await clickText('系统说明'); await key('Escape');
          await clickText('对象信息'); await toPerson();
          await clickSel('.section.ground .card.item'); await key('Escape'); await toPerson();
          let got = await st();
          log(tag + ' 进出次级菜单/切页签后保持', got.skills.open === sk && got.traits.open === tr, got);
          await reloadContinue(); await toPerson();
          got = await st();
          log(tag + ' 刷新后保持', got.skills.open === sk && got.traits.open === tr, got);
        }
        // 收起熟练度后读旧档（营地固定存档），偏好不回滚
        await clickSel('[data-fold=skills] .fold-head');
        const before = await st();
        await loadState(fixtures.camp); await toPerson();
        const after = await st();
        log('读旧档不回滚', before.skills.open === after.skills.open && before.traits.open === after.traits.open, { before, after });
        await shot(`fold-${w}-after-load`);
      } catch (e) { log('折叠测试异常 ' + e.message, false, null); }
    }
    const states = report.results.flatMap(r => r.states);
    // 主体高/卡高按尺寸×卡类汇总（UI-B2-34c/36a：报实测值供人工对照；物品卡 1280 争取约0.50、高屏趋近约0.70，不设硬门槛）
    const ratios = {};
    for (const r of report.results) for (const st of r.states) for (const a of st.arts || []) {
      if (!['item', 'res', 'enemy', 'loc', 'fac'].includes(a.kind)) continue;
      const o = ((ratios[r.size] ||= {})[a.kind] ||= { min: 9, max: 0 });
      o.min = Math.min(o.min, a.ratio); o.max = Math.max(o.max, a.ratio);
    }
    report.ratios = ratios;
    for (const [sz, o] of Object.entries(ratios)) console.log('主体高/卡高 ' + sz + '：' + Object.entries(o).map(([k, v]) => `${k} ${v.min.toFixed(2)}—${v.max.toFixed(2)}`).join('，'));
    report.summary = { states: states.length, failed: states.filter(s => !s.pass).length, errors: report.results.filter(r => r.error).length,
      fold: report.fold.length, foldFailed: report.fold.filter(f => !f.pass).length };
    save();
    console.log(`完成：${states.length}个状态，失败${report.summary.failed}，场景错误${report.summary.errors}；折叠${report.summary.fold}项失败${report.summary.foldFailed}；结果 ${path.join(out, 'v12-results.json')}`);
    if (report.summary.failed || report.summary.errors || report.summary.foldFailed) process.exitCode = 1;
  } finally {
    try { await send('Browser.close'); } catch { }
    try { ws && ws.close(); } catch { }
    setTimeout(() => { try { chrome.kill(); } catch { } }, 500);
  }
})().catch(e => { console.error('失败：' + (e.stack || e)); process.exitCode = 1; });
