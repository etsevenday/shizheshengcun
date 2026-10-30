#!/usr/bin/env node
// S14 修订1.10 §18 实机测量（开发计划 v0.8 M76）：固定四行等高、选卡浮窗、约70%图卡（Q28 取代约80%，v0.9 M81）、随身五卡位、两行战斗。
// 零依赖：Node 18+ 自带 fetch/WebSocket + Chrome DevTools Protocol（做法同 tools/playtest/cdp.mjs），不需要 playwright。
//
// 用法：
//   node tools/verify-ui-v11.cjs [网址] [输出目录]
//   网址默认线上 https://etsevenday.github.io/shizheshengcun/?dev=1；输出默认 doc/验收/固定四行与烹饪保质 v1.1
//   环境变量：CHROME（浏览器路径）、PLAYTEST_PORT（调试端口，默认 9335）、SKIP_BATTLE=1（跳过首战场景）
//
// 每档分辨率（1280×720 / 1600×900 / 1920×1080）× 每个场景状态：
//   - 四行高度：中部网格四条轨道（①②③④）最大最小差 ≤1px，地面/随身行实际高度与轨道一致，①②合并区=两轨+间隔；
//     各状态（选地面卡/随身卡/锅、开地图/制作面板、战斗瞄准）前后轨道不变。
//   - 卡片：图片区占卡高比例（目标约70%，Q28）、图与名称不相压、卡不被所在行纵向裁切、同行卡片两两不重叠、箭头可点中且≥40px；
//     ②行资源/设施/敌踪卡与①行相邻地点卡也量图片区占比（行矮时名称与按钮保底，低于0.5才报）。
//   - 随身行：卡位数=5（或超额时的实际卡数）、无横/纵滚动条。
//   - 选卡浮窗：在视口内、不压所选卡主图、不压顶栏、浮窗中心点命中浮窗自身（无穿透/错层）。
//   - 浏览前后游戏分钟与随机状态不变。
// 注：B2（开发计划 v0.9 M86 起）物品卡改为“主体完整+文字离框≥8px”，图片区占比按旧口径会低于0.6；屏高≤800 资源/敌踪/相邻地点卡左图右文，
//     占比会高于0.82。这两项以 tools/verify-ui-v12.cjs 的主体完整与安全区测量为准，其余检查仍有效。
// 结果写 <输出目录>/v11-results.json，截图写 <输出目录>/screenshots/。自动测量不替代人工逐图目测（S14 §18.5）。
'use strict';
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');

const url = process.argv[2] || 'https://etsevenday.github.io/shizheshengcun/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/固定四行与烹饪保质 v1.1');
const PORT = Number(process.env.PLAYTEST_PORT || 9335);
const SAVE_KEY = 'survival.save.v1';
const SIZES = [[1280, 720], [1600, 900], [1920, 1080]];
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
/** 点第一个匹配 CSS 选择器的可见元素中心；返回是否点到。 */
async function clickSel(sel, index = 0) {
  const r = await ev(`(() => { const es = [...document.querySelectorAll(${JSON.stringify(sel)})].filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0; });
    const e = es[${index}]; if (!e) return null; e.scrollIntoView({ block: 'nearest', inline: 'nearest' }); const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
  if (!r) return false; await clickAt(r.x, r.y); return true;
}
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
async function loadState(state) {
  await send('Page.navigate', { url: new URL('icon-192.png', url).href }); await sleep(800);
  if (state) await ev(`localStorage.setItem(${JSON.stringify(SAVE_KEY)}, ${JSON.stringify(JSON.stringify(state))})`);
  else await ev(`localStorage.removeItem(${JSON.stringify(SAVE_KEY)})`);
  await send('Page.navigate', { url }); await sleep(800);
  if (!await waitFor(`!!document.querySelector('.title-screen') || !!document.querySelector('.game')`, 90000)) throw new Error('页面加载超时');
  if (state) await clickText('继续'); else await clickText('新游戏');
  if (!await waitFor(`!!document.querySelector('.game .center-scroll')`, 30000)) throw new Error('没进入主界面');
  await ev(`document.fonts.ready.then(() => true)`); await sleep(600);
}
async function readDialogues(max = 40) {
  for (let i = 0; i < max; i++) {
    const has = await ev(`!!document.querySelector('.dlg-layer')`);
    if (!has) return true;
    if (!await clickText('结束对话') && !await clickText('继续')) await sleep(300);
  }
  return !await ev(`!!document.querySelector('.dlg-layer')`);
}
/**
 * 收起本次打开的浮窗/面板/选中（M78：原来无条件按 Esc，没有可收起的东西时 Esc 会打开“菜单”，盖住箭头造成误报）。
 * 只在确有可收起的层时按 Esc，最多三次；万一菜单被打开了再按一次关掉。
 */
async function closeLayers() {
  for (let i = 0; i < 3; i++) {
    const has = await ev(`!!document.querySelector('.apop, .sub-panel, .card.selected, .pot-card.selected, .fc-food.selected, .ocard.selected, .pc-row.selected')`);
    if (!has) break;
    await key('Escape');
  }
  if (await ev(`!!document.querySelector('.overlay[role=dialog]')`)) await key('Escape');
}
const saved = () => ev(`(() => { const s = JSON.parse(localStorage.getItem(${JSON.stringify(SAVE_KEY)}) || 'null'); return s && { m: s.GameMinute, rng: JSON.stringify(s.Rng) }; })()`);

// ───────────── 页面内测量 ─────────────
const MEASURE = `(() => {
  const R = e => { const r = e.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height, r: r.right, b: r.bottom }; };
  const inter = (a, b) => Math.max(0, Math.min(a.r, b.r) - Math.max(a.x, b.x)) * Math.max(0, Math.min(a.b, b.b) - Math.max(a.y, b.y));
  const vis = e => { const r = e.getBoundingClientRect(); const st = getComputedStyle(e); return r.width > 0 && r.height > 0 && st.visibility !== 'hidden' && st.display !== 'none'; };
  const issues = [];
  const grid = document.querySelector('.center-scroll.four');
  if (!grid) return { issues: ['找不到 .center-scroll.four（不是固定四行布局）'] };
  const gs = getComputedStyle(grid);
  const tracks = gs.gridTemplateRows.split(' ').map(parseFloat);
  const gap = parseFloat(gs.rowGap) || 0;
  const rows2 = grid.querySelector(':scope > .rows2'), ground = grid.querySelector(':scope > .section.ground'), carried = grid.querySelector(':scope > .section.carried');
  const rows = { rows2: rows2 && R(rows2), ground: ground && R(ground), carried: carried && R(carried), grid: R(grid) };
  const spread = tracks.length ? Math.max(...tracks) - Math.min(...tracks) : 99;
  if (tracks.length !== 4) issues.push('中部不是4条轨道：' + gs.gridTemplateRows);
  if (spread > 1) issues.push('四行高度差' + spread.toFixed(2) + 'px >1px：' + gs.gridTemplateRows);
  if (ground && Math.abs(rows.ground.h - tracks[2]) > 1) issues.push('当前地面行实际高' + rows.ground.h.toFixed(1) + '≠轨道' + tracks[2]);
  if (carried && Math.abs(rows.carried.h - tracks[3]) > 1) issues.push('随身行实际高' + rows.carried.h.toFixed(1) + '≠轨道' + tracks[3]);
  if (rows2 && Math.abs(rows.rows2.h - (tracks[0] + tracks[1] + gap)) > 1) issues.push('①②合并区高' + rows.rows2.h.toFixed(1) + '≠两轨+间隔');
  if (rows.grid.b > innerHeight + 1) issues.push('中部超出视口');
  const inner = rows2 && rows2.querySelector('.rows2-inner');
  const upper = inner ? [...inner.children].filter(vis).map(e => ({ cls: String(e.className).slice(0, 60), ...R(e) })) : [];
  // 物品卡
  const cards = [];
  for (const sec of [ground, carried].filter(Boolean)) {
    const secR = R(sec.querySelector('.row-scroll') || sec);
    const list = [...sec.querySelectorAll('.row-scroll > .card-col > .card.item, .row-scroll > .pot-card')].filter(vis);
    list.forEach((c, i) => {
      const cr = R(c); const art = c.querySelector('.art'), title = c.querySelector('.ctitle');
      const info = { row: sec.classList.contains('carried') ? 'carried' : 'ground', i, ...cr };
      if (art && title) {
        const ar = R(art), tr = R(title);
        info.artRatio = +(ar.h / cr.h).toFixed(3);
        if (ar.b > tr.y + 1) issues.push(info.row + '第' + (i + 1) + '张卡图片压住名称');
        if (info.artRatio < 0.6 || info.artRatio > 0.78) issues.push(info.row + '第' + (i + 1) + '张卡图片区占比' + info.artRatio + '（目标约0.7）');
      }
      if (cr.y < secR.y - 1 || cr.b > secR.b + 1) issues.push(info.row + '第' + (i + 1) + '张卡被行纵向裁切');
      cards.push(info);
    });
    for (let a = 0; a < list.length; a++) for (let b = a + 1; b < list.length; b++)
      if (inter(R(list[a]), R(list[b])) > 1) issues.push((sec.classList.contains('carried') ? 'carried' : 'ground') + '卡片' + (a + 1) + '与' + (b + 1) + '重叠');
  }
  // Q28：资源、设施、敌踪、相邻地点卡的图片区占比（目标约0.7）
  const ocards = [...document.querySelectorAll('.center-scroll.four :is(.res-card, .fac-card, .enemy-card.world, .loc-card.neighbor)')].filter(vis).map((c, i) => {
    const cr = R(c); const art = c.querySelector('.oc-art');
    const o = { cls: String(c.className).split(' ').filter(Boolean).join(' ').slice(0, 40), i, ...cr, artRatio: art ? +(R(art).h / cr.h).toFixed(3) : null };
    if (o.artRatio != null && (o.artRatio < 0.5 || o.artRatio > 0.82)) issues.push(o.cls + '图片区占比' + o.artRatio + '（目标约0.7）');
    return o;
  });
  // 随身五卡位
  let carriedInfo = null;
  if (carried) {
    const rs = carried.querySelector('.row-scroll');
    const slots = [...rs.children].filter(vis).length;
    carriedInfo = { slots, scrollX: rs.scrollWidth - rs.clientWidth, scrollY: rs.scrollHeight - rs.clientHeight, slotVar: rs.style.getPropertyValue('--slots') };
    if (carriedInfo.scrollX > 1 || carriedInfo.scrollY > 1) issues.push('随身行出现滚动（' + carriedInfo.scrollX + ',' + carriedInfo.scrollY + '）');
    if (slots < 5) issues.push('随身行可见卡位' + slots + '<5');
  }
  // 箭头命中：先排除“在所在滚动区里被滚出可视范围”（滚动即可到达，不是遮挡）与“被有意打开的浮层盖住”（浮窗/整理窗/对白/面板），
  // 剩下点不中的才算遮挡问题（M77：原脚本把这两类都算成遮挡，1280 报了 117 次）
  const overlays = [...document.querySelectorAll('.apop, .overflow-layer, .dlg-layer, .sub-panel, .menu-box, .pause-layer, .cprog')].filter(vis).map(R);
  const inside = (x, y, o) => x >= o.x && x <= o.r && y >= o.y && y <= o.b;
  const clipOf = e => {
    let L = 0, T = 0, Rt = innerWidth, B = innerHeight;
    for (let p = e.parentElement; p && p !== document.body; p = p.parentElement) {
      const cs = getComputedStyle(p);
      if (cs.overflowX !== 'visible' || cs.overflowY !== 'visible') { const pr = p.getBoundingClientRect(); L = Math.max(L, pr.left); T = Math.max(T, pr.top); Rt = Math.min(Rt, pr.right); B = Math.min(B, pr.bottom); }
    }
    return { x: L, y: T, r: Rt, b: B };
  };
  const arrows = [...document.querySelectorAll('.center .card-arrow, .center .row-arrow')].filter(vis).map(e => {
    const r = e.getBoundingClientRect(); const cx = r.x + r.width / 2, cy = r.y + r.height / 2;
    const scrolledOut = !inside(cx, cy, clipOf(e));
    const underOverlay = !e.closest('.apop') && overlays.some(o => inside(cx, cy, o));
    const hitEl = document.elementFromPoint(cx, cy);
    return { label: e.getAttribute('aria-label'), w: r.width, h: r.height, scrolledOut, underOverlay, hit: !!hitEl && (hitEl === e || e.contains(hitEl)) };
  });
  arrows.filter(a => !a.hit && !a.scrolledOut && !a.underOverlay).forEach(a => issues.push('箭头点不中（被遮挡）：' + a.label));
  const overflowOpen = !!document.querySelector('.overflow-layer');
  // 选卡浮窗
  let pop = null;
  const pe = document.querySelector('.apop.card-pop');
  if (pe && vis(pe)) {
    const pr = R(pe); const anchor = document.getElementById('card-sel'); const top = document.querySelector('.top');
    const hitEl = document.elementFromPoint(pr.x + pr.w / 2, pr.y + Math.min(pr.h / 2, 30));
    pop = { ...pr, side: pe.getAttribute('data-side'), hitSelf: !!hitEl && pe.contains(hitEl) };
    if (pr.x < 0 || pr.y < 0 || pr.r > innerWidth + 1 || pr.b > innerHeight + 1) issues.push('选卡浮窗超出视口');
    if (!pop.hitSelf) issues.push('选卡浮窗中心被别的层盖住');
    if (top && inter(pr, R(top)) > 1) issues.push('选卡浮窗压住顶栏');
    const art = anchor && (anchor.querySelector('.art') || anchor);
    if (art && inter(pr, R(art)) > R(art).w * R(art).h * 0.05) issues.push('选卡浮窗压住所选卡主图');
    if (pr.w > rows.grid.w / 3 + 40) issues.push('选卡浮窗宽' + pr.w.toFixed(0) + '超过中部约1/3');
  }
  // 战斗两行
  const team = document.querySelector('.bt-team'), cons = document.querySelector('.bt-console');
  let battle = null;
  if (team || cons) {
    battle = { team: team && R(team), console: cons && R(cons) };
    if (team && Math.abs(battle.team.h - tracks[0]) > 1) issues.push('战斗队伍行高≠第①行');
    if (cons && Math.abs(battle.console.h - tracks[1]) > 1) issues.push('战斗操作台行高≠第②行');
    for (const b of [...document.querySelectorAll('.bt-console button')].filter(vis)) {
      const r = R(b); if (r.b > battle.console.b + 1 || r.y < battle.console.y - 1) issues.push('操作台按钮被裁切：' + (b.innerText || b.getAttribute('aria-label') || '').trim().slice(0, 12));
    }
  }
  // 文字被裁（非省略号的溢出）只计数
  const clipped = [...document.querySelectorAll('.center button, .center .ctitle, .center .cinfo, .apop')].filter(vis)
    .filter(e => e.scrollHeight > e.clientHeight + 2 && getComputedStyle(e).overflowY === 'hidden').length;
  return { viewport: [innerWidth, innerHeight], tracks, gap, spread, rows, upper, cards, ocards, carried: carriedInfo, overflowOpen,
    arrows: { total: arrows.length, scrolledOut: arrows.filter(a => a.scrolledOut).length, underOverlay: arrows.filter(a => a.underOverlay).length },
    pop, battle, clippedText: clipped, issues };
})()`;

// ───────────── 主流程 ─────────────
(async () => {
  fs.mkdirSync(path.join(out, 'screenshots'), { recursive: true });
  const exe = CHROMES.find(p => fs.existsSync(p));
  if (!exe) throw new Error('找不到 Chrome/Edge（可设环境变量 CHROME）');
  const prof = fs.mkdtempSync(path.join(os.tmpdir(), 'survival-v11-'));
  const chrome = spawn(exe, ['--headless=new', `--remote-debugging-port=${PORT}`, `--user-data-dir=${prof}`, '--window-size=1920,1080',
    '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
  const report = { url, startedAt: new Date().toISOString(), results: [] };
  const save = () => fs.writeFileSync(path.join(out, 'v11-results.json'), JSON.stringify(report, null, 2));
  try {
    await connect();
    await send('Page.enable'); await send('Runtime.enable');
    const fixtures = fs.existsSync(FIXTURES) ? JSON.parse(fs.readFileSync(FIXTURES, 'utf8')) : {};
    const scenes = [['new', null], ...Object.entries(fixtures)];
    for (const [w, h] of SIZES) {
      await size(w, h);
      for (const [scene, state] of scenes) {
        const rec = { size: `${w}x${h}`, scene, states: [] };
        report.results.push(rec);
        try {
          await loadState(state);
          if (!state) await readDialogues();
          const before = await saved();
          const base = await ev(MEASURE);
          const step = async (name, act) => {
            let did = true;
            try { did = (await act()) !== false; } catch (e) { did = false; }
            await sleep(350);
            const m = await ev(MEASURE);
            const changed = JSON.stringify(m.tracks) !== JSON.stringify(base.tracks);
            if (changed) m.issues.push('行高与初始不同：' + JSON.stringify(base.tracks) + ' → ' + JSON.stringify(m.tracks));
            const file = await shot(`${scene}-${w}-${name}`);
            rec.states.push({ name, did, pass: m.issues.length === 0, screenshot: file, ...m });
            console.log(`${m.issues.length ? 'FAIL' : 'PASS'} ${w}x${h} ${scene} ${name}${did ? '' : '（未执行到）'}${m.issues.length ? '：' + m.issues.join('；') : ''}`);
            save();
          };
          await step('default', async () => true);
          await step('select-ground', () => clickSel('.section.ground .card.item'));
          await closeLayers();
          await step('select-ground-last', () => clickSel('.section.ground .card-col:last-of-type .card.item'));
          await closeLayers();
          await step('select-carried', () => clickSel('.section.carried .card.item, .section.carried .pot-card .pc-head'));
          await closeLayers();
          await step('select-pot-in-fire', () => clickSel('.pot-card.in-fire .pc-head'));
          await closeLayers();
          await step('panel-map', () => key('m'));
          await closeLayers();
          await step('panel-craft', () => key('c'));
          await closeLayers();
          const after = await saved();
          rec.timeUnchanged = !before || !after || (before.m === after.m && before.rng === after.rng);
          if (!rec.timeUnchanged) console.log(`FAIL ${w}x${h} ${scene} 浏览后游戏时间或随机状态变化`);
        } catch (e) {
          rec.error = String(e.stack || e);
          console.log(`FAIL ${w}x${h} ${scene}：${e.message}`);
        }
        save();
      }
      // 首战：新局读完开场对白 → 前往农野荒地 → 读完首战对白 → 测两行战斗、选技能瞄准、悬停预测、Esc 取消不耗 AP
      if (!process.env.SKIP_BATTLE) {
        const rec = { size: `${w}x${h}`, scene: 'battle', states: [] };
        report.results.push(rec);
        try {
          await loadState(null); await readDialogues();
          await clickSel('.map-adjacent .loc-card');
          if (!await clickText('确认移动')) throw new Error('找不到“确认移动”');
          if (!await waitFor(`!!document.querySelector('.bt-team') || !!document.querySelector('.dlg-layer')`, 60000)) throw new Error('没进入首战');
          await readDialogues();
          await waitFor(`!!document.querySelector('.bt-team')`, 10000);
          const m0 = await ev(MEASURE);
          rec.states.push({ name: 'battle-default', pass: m0.issues.length === 0, screenshot: await shot(`battle-${w}-default`), ...m0 });
          const ap0 = await ev(`(document.querySelector('.ap-pips small')||{}).innerText||''`);
          await clickSel('.bc-group.skills .cmdt:not(.off)');
          const aim = await ev(MEASURE);
          aim.aiming = await ev(`!!document.querySelector('.bt-team.aiming') && document.querySelectorAll('.foe.targetable').length`);
          if (!aim.aiming) aim.issues.push('选技能后没有进入瞄准或没有亮起的目标');
          const foe = await ev(`(() => { const e = document.querySelector('.foe.targetable'); if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
          if (foe) { await mouseAt(foe.x, foe.y); await sleep(400); }
          aim.preview = await ev(`(document.querySelector('.detail') || document.body).innerText.includes('实际命中率')`);
          if (!aim.preview) aim.issues.push('悬停合法目标时右栏没有实际预测');
          rec.states.push({ name: 'battle-aim-hover', pass: aim.issues.length === 0, screenshot: await shot(`battle-${w}-aim-hover`), ...aim });
          await key('Escape');   // 瞄准中 Esc 是“取消瞄准”本身，要真的按
          const ap1 = await ev(`(document.querySelector('.ap-pips small')||{}).innerText||''`);
          const esc = await ev(MEASURE);
          if (ap0 !== ap1) esc.issues.push('Esc 取消瞄准后 AP 变化：' + ap0 + ' → ' + ap1);
          if (await ev(`!!document.querySelector('.bt-team.aiming')`)) esc.issues.push('Esc 后仍在瞄准');
          rec.states.push({ name: 'battle-esc', pass: esc.issues.length === 0, screenshot: await shot(`battle-${w}-esc`), ...esc });
          for (const st of rec.states) console.log(`${st.pass ? 'PASS' : 'FAIL'} ${w}x${h} battle ${st.name}${st.issues.length ? '：' + st.issues.join('；') : ''}`);
        } catch (e) {
          rec.error = String(e.stack || e);
          console.log(`FAIL ${w}x${h} battle：${e.message}`);
        }
        save();
      }
    }
    const states = report.results.flatMap(r => r.states);
    report.summary = { states: states.length, failed: states.filter(s => !s.pass).length, errors: report.results.filter(r => r.error).length,
      timeChanged: report.results.filter(r => r.timeUnchanged === false).length };
    save();
    console.log(`完成：${states.length}个状态，失败${report.summary.failed}，场景错误${report.summary.errors}；结果 ${path.join(out, 'v11-results.json')}`);
    if (report.summary.failed || report.summary.errors || report.summary.timeChanged) process.exitCode = 1;
  } finally {
    try { await send('Browser.close'); } catch { }
    try { ws && ws.close(); } catch { }
    setTimeout(() => { try { chrome.kill(); } catch { } }, 500);
  }
})().catch(e => { console.error('失败：' + (e.stack || e)); process.exitCode = 1; });
