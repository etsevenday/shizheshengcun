// Material-specific browser acceptance. Uses a fresh Chrome profile and the
// existing deterministic game fixtures; never touches a player's saved game.
const { chromium } = require('playwright');
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const url = process.argv[2] || 'http://127.0.0.1:5178/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/主界面材质精修 v0.9');
const key = 'survival.save.v1';

(async () => {
  await fs.mkdir(path.join(out, 'screenshots'), { recursive: true });
  const fixtures = JSON.parse(await fs.readFile('doc/验收/主界面美术换肤 v0.8/fixtures.json', 'utf8'));
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 900 }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  page.setDefaultTimeout(10000);
  const report = { url, browser: browser.version(), deviceScaleFactor: 1, results: [], errors: [], httpErrors: [] };
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('response', r => { if (r.status() >= 400) report.httpErrors.push({ url: r.url(), status: r.status() }); });
  const write = () => fs.writeFile(path.join(out, 'material-results.json'), JSON.stringify(report, null, 2));
  const run = async (name, fn) => {
    try { report.results.push({ name, pass: true, detail: await fn() }); console.log('PASS ' + name); }
    catch (e) { report.results.push({ name, pass: false, error: e.stack }); console.log('FAIL ' + name + ': ' + e.message); }
    await write();
  };
  const state = () => page.evaluate(k => JSON.parse(localStorage.getItem(k)), key);
  const load = async s => {
    await page.evaluate(([k, s]) => localStorage.setItem(k, JSON.stringify(s)), [key, s]);
    await page.reload({ waitUntil: 'networkidle' });
    await page.getByRole('button', { name: '继续', exact: true }).click();
    await page.locator('.game').waitFor();
  };
  const shot = name => page.screenshot({ path: path.join(out, 'screenshots', name + '.png') });
  const clip = async (selector, name) => page.locator(selector).first().screenshot({ path: path.join(out, 'screenshots', name + '.png') });
  await page.goto(url, { waitUntil: 'networkidle' });

  await run('default, selected, hover, focus, pressed and exhausted at 100 percent', async () => {
    await load(fixtures.hardware);
    const before = await state();
    await shot('state-default');
    const card = page.locator('.res-card').filter({ hasText: '锅具箱' });
    await page.keyboard.press('Tab');
    await card.focus();
    await page.keyboard.press('Enter');
    assert(await card.evaluate(e => e.classList.contains('selected')), 'Enter selects the focused resource card');
    await clip('.weight', 'weight-detail');
    await card.screenshot({ path: path.join(out, 'screenshots/resource-selected.png') });
    const explore = page.locator('.map-card .loc-btn').first();
    await explore.hover();
    await shot('state-hover');
    await page.keyboard.press('Tab');
    await explore.focus();
    assert(await explore.evaluate(e => e.matches(':focus-visible')), 'real keyboard focus must be visible');
    await shot('state-keyboard-focus');
    const b = await explore.boundingBox();
    await page.mouse.move(b.x + b.width / 2, b.y + b.height / 2);
    await page.mouse.down();
    assert(await explore.evaluate(e => e.matches(':active')));
    await shot('state-pressed');
    await page.mouse.move(2, 2);
    await page.mouse.up();
    const exhausted = page.locator('.res-card.dim').first();
    assert(await exhausted.locator('button').isDisabled());
    await exhausted.screenshot({ path: path.join(out, 'screenshots/resource-exhausted.png') });
    const help = page.locator('.nav').getByRole('button', { name: /^系统说明/ });
    await help.focus();
    await page.keyboard.press('Enter');
    await page.getByRole('dialog').waitFor();
    await page.keyboard.press('Escape');
    await page.getByRole('dialog').waitFor({ state: 'hidden' });
    const after = await state();
    assert.equal(after.GameMinute, before.GameMinute);
    assert.equal(after.Rng.State, before.Rng.State);
    assert.deepEqual(after.Items, before.Items);
    return { minute: after.GameMinute, resourceDisabled: true, focusVisible: true, enterSelection: true, escapeClosesPanel: true };
  });

  await run('all theme resources decode, sprite crop and control reachability', async () => {
    const assets = ['frame-iron.svg', 'frame-iron-alt.svg', 'frame-paper.svg', 'frame-paper-alt.svg', 'frame-button.svg', 'frame-selected.svg', 'paper.svg', 'iron.svg'];
    const loaded = [];
    for (const asset of assets) {
      const href = new URL('art/v09/' + asset, url).href;
      const response = await page.request.get(href);
      assert(response.ok(), asset);
      assert.match(response.headers()['content-type'], /svg/);
      await page.evaluate(async href => { const img = new Image(); img.src = href; await img.decode(); }, href);
      loaded.push({ asset, bytes: (await response.body()).length });
    }
    const measures = [];
    for (const [width, height] of [[1280, 720], [1600, 900], [1920, 1080]]) {
      await page.setViewportSize({ width, height });
      await load(fixtures.camp);
      const fireText = await page.locator('.fc-btn.plain').evaluate(e => getComputedStyle(e).color);
      const rgb = fireText.match(/[\d.]+/g).slice(0, 3).map(Number);
      const light = rgb.map(c => c / 255).map(c => c <= .04045 ? c / 12.92 : ((c + .055) / 1.055) ** 2.4);
      const luminance = light[0] * .2126 + light[1] * .7152 + light[2] * .0722;
      // The lightest gradient stop on this dark control is #26302e.
      const contrast = (luminance + .05) / (.0277 + .05);
      assert(contrast >= 4.5, 'extinguish control text must contrast with its dark material');
      const data = await page.locator('.weight, .vital-icon, .pot-card.in-fire .pc-foods, .card-arrow, .row-arrow').evaluateAll(es => es.map(e => ({ class: e.className.baseVal || e.className, ...e.getBoundingClientRect().toJSON() })));
      const controls = [];
      const arrows = page.locator('.card-arrow, .row-arrow');
      for (let i = 0; i < await arrows.count(); i++) {
        const arrow = arrows.nth(i);
        if (!await arrow.isVisible()) continue;
        // An offscreen card can lie geometrically behind the detail column.
        // Reach it through its real horizontal scroller before hit testing.
        await arrow.scrollIntoViewIfNeeded();
        controls.push(await arrow.evaluate(e => {
          const r = e.getBoundingClientRect();
          const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
          return { label: e.getAttribute('aria-label'), w: r.width, h: r.height, hit: !!hit && (e === hit || e.contains(hit)) };
        }));
      }
      assert(controls.every(e => e.w >= 40 && e.h >= 40));
      assert(controls.every(e => e.hit), 'transfer controls must receive pointer input after scrolling into view');
      const crop = await page.locator('.ink-sprite > svg').evaluateAll(es => es.map(e => getComputedStyle(e).overflow));
      assert(crop.length > 0 && crop.every(v => v === 'hidden'), 'all atlas cell viewports must remain clipped');
      const food = await page.locator('.pot-card.in-fire .pc-foods').boundingBox();
      const row = await page.locator('.facility-row').boundingBox();
      assert(food.y + food.height <= row.y + row.height + 1, 'food slot stays in facility row');
      await page.locator('.row-scroll, .fac-scroll').evaluateAll(es => es.forEach(e => e.scrollLeft = 0));
      await shot('camp-material-' + width);
      measures.push({ width, height, data, controls });
    }
    return { loaded, measures };
  });

  await run('missing all v09 art keeps controls readable and operational', async () => {
    await page.route('**/art/v09/**', r => r.abort());
    await page.setViewportSize({ width: 1280, height: 720 });
    await load(fixtures.hardware);
    const before = await state();
    await page.locator('.res-card').filter({ hasText: '锅具箱' }).click();
    assert(await page.locator('.op-head').isVisible());
    await shot('missing-material-fallback');
    await page.locator('.nav').getByRole('button', { name: /^系统说明/ }).click();
    assert(await page.getByRole('dialog').isVisible());
    await page.getByRole('dialog').getByRole('button', { name: /^关闭/ }).click();
    const after = await state();
    assert.equal(after.GameMinute, before.GameMinute);
    assert.deepEqual(after.Items, before.Items);
    await page.unroute('**/art/v09/**');
    return { missingTextureFallback: true, selection: true, navigation: true };
  });

  assert.equal(report.errors.length, 0);
  assert.equal(report.httpErrors.length, 0);
  report.failed = report.results.filter(r => !r.pass).length;
  await write();
  await browser.close();
  if (report.failed) process.exitCode = 1;
})().catch(e => { console.error(e); process.exitCode = 1; });
