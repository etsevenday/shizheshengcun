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
// 每个场景测默认、选地面卡、选随身最长名卡、选火堆、选火堆里的锅、悬停地面卡，开地图/制作面板。
// 测量口径（S14 截图复核 §B）：
//   - 火堆外框与同组遮蔽棚/床垫顶、底各差 ≤1px，各状态前后火堆高度不变（UI-M04）。
//   - 可见框体厚度：border-image 画出的框线约占 border-image-width 的 15/24（纸框约6.3px），铁框约 13/24（约6.5px），无 border-image 时取边框宽；
//     文字（按字形行框）到可见框内缘 ≥8px，极紧凑角标（数量/占格/状态角标/转移箭头按钮框）≥6px（UI-M06）。只计在所在滚动区可见的部分。
//   - 主图：按 viewBox 等比适配算出主体实际所在矩形，不被裁（祖先 overflow 与卡框内缘）、不被同卡文字/角标/按钮压住（>2%面积才报）、
//     精灵图无 clip-path（UI-M05）；记录主体高/卡高，供人工对照“约70%”。
//   - 物品卡名称与转移箭头不相交；卡片外缘不压所在行框。
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
  }
  s.NextInstanceId = id;
  return JSON.stringify(s);
}
let campBase = null;
async function loadScene(scene, fixtures) {
  if (scene === 'new') { await loadState(null); await readDialogues(); return; }
  if (scene === 'hardware') {
    await loadState(fixtures.hardware);
    if (await ev(`!!document.querySelector('.overflow-layer')`)) {
      await ev(`(() => { const ins = [...document.querySelectorAll('.overflow-layer .ov-item input')]; ins.slice(0, 2).forEach(i => i.click()); return ins.length; })()`);
      await sleep(300); await clickText('保留所选');
    }
    await sleep(400); return;
  }
  if (!campBase) { await loadState(fixtures.camp); campBase = await savedJson(); }
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
  const issues = [];
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
      const clip = clipOf(el); const ec = rr(el.getBoundingClientRect());
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
    if (worst && worst.d < worst.need - 0.5) issues.push('安全区 ' + label(B) + '「' + worst.text + '」距框内缘' + worst.d + 'px<' + worst.need);
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
    const info = { key: svg.getAttribute('data-art'), card: label(card), subH: f1(sh), cardH: f1(cr.h), ratio: f1(sh / cr.h * 100) / 100, kept: f1(kept * 100) };
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
  return { viewport: [innerWidth, innerHeight], tracks, fire: fireInfo, textChecked, safety: safety.sort((a, b) => (a.d - a.need) - (b.d - b.need)).slice(0, 12), arts, pop, issues };
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
    const scenes = ['new', 'empty', 'full', 'fuelfull', 'hardware'];
    for (const [w, h] of SIZES) {
      await size(w, h);
      for (const scene of scenes) {
        const rec = { size: `${w}x${h}`, scene, states: [] };
        report.results.push(rec);
        try {
          await loadScene(scene, fixtures);
          let fireH0 = null;
          const step = async (name, act) => {
            let did = true;
            try { did = (await act()) !== false; } catch (e) { did = false; }
            await sleep(350);
            const m = await ev(MEASURE);
            if (m.fire) { if (fireH0 == null) fireH0 = m.fire.h; else if (Math.abs(m.fire.h - fireH0) > 1) m.issues.push('火堆高度随状态变化：' + fireH0 + '→' + m.fire.h); }
            const file = await shot(`${scene}-${w}-${name}`);
            rec.states.push({ name, did, pass: m.issues.length === 0, screenshot: file, ...m });
            console.log(`${m.issues.length ? 'FAIL' : 'PASS'} ${w}x${h} ${scene} ${name}${did ? '' : '（未执行到）'}` +
              (m.fire ? ` 火堆高${m.fire.h}/棚床差${m.fire.maxDiff}` : '') + (m.issues.length ? '：' + [...new Set(m.issues)].join('；') : ''));
            save();
          };
          await step('default', async () => true);
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
