// Real browser acceptance for the illustrated cards, using isolated save copies.
const { chromium } = require('playwright');
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const url = process.argv[2] || 'http://127.0.0.1:5180/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/主界面视觉精修 v1.0');
const key = 'survival.save.v1';
const sizes = [[1280, 720], [1600, 900], [1920, 1080]];

(async () => {
  const fixtures = JSON.parse(await fs.readFile('doc/验收/主界面美术换肤 v0.8/fixtures.json', 'utf8'));
  await fs.mkdir(path.join(out, 'screenshots'), { recursive: true });
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 900 }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  page.setDefaultTimeout(12000);
  const report = { url, browser: browser.version(), results: [], errors: [], httpErrors: [] };
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('response', r => { if (r.status() >= 400) report.httpErrors.push({ url: r.url(), status: r.status() }); });
  const save = () => fs.writeFile(path.join(out, 'illustration-results.json'), JSON.stringify(report, null, 2));
  const run = async (name, fn) => {
    try { report.results.push({ name, pass: true, detail: await fn() }); console.log('PASS ' + name); }
    catch (e) { report.results.push({ name, pass: false, error: e.stack }); console.log('FAIL ' + name + ': ' + e.message); }
    await save();
  };
  const load = async state => {
    await page.evaluate(([k, s]) => localStorage.setItem(k, JSON.stringify(s)), [key, state]);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: '继续', exact: true }).click();
    await page.locator('.game').waitFor();
    await page.evaluate(async () => {
      await document.fonts.ready;
      const sources = new Set([...document.querySelectorAll('svg image')].map(i => i.getAttribute('href')));
      for (const e of document.querySelectorAll('.mc-art, .loc-art')) {
        const m = getComputedStyle(e).backgroundImage.match(/url\(["']?(.*?)["']?\)/);
        if (m) sources.add(m[1]);
      }
      await Promise.all([...sources].map(src => { const i = new Image(); i.src = src; return i.decode(); }));
    });
  };
  const shot = name => page.screenshot({ path: path.join(out, 'screenshots', name + '.png') });
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: /^(新游戏|继续)$/ }).first().waitFor();

  await run('six real screenshots, card art and controls remain inside all four rows', async () => {
    const measurements = [];
    for (const [scene, state] of [['hardware', fixtures.hardware], ['camp', fixtures.camp]]) {
      for (const [width, height] of sizes) {
        await page.setViewportSize({ width, height });
        await load(state);
        if (scene === 'hardware') await page.locator('.res-card').filter({ hasText: '锅具箱' }).click();
        const before = await page.evaluate(k => JSON.parse(localStorage.getItem(k)), key);
        const rows = await page.locator('.map-row,.facility-row,.section.ground,.section.carried').evaluateAll(es => es.map(e => ({ cls: e.className, ...e.getBoundingClientRect().toJSON() })));
        assert.equal(rows.length, 4);
        for (const row of rows) assert(row.height > 70 && row.y > 0 && row.bottom <= height, JSON.stringify(row));
        const items = await page.locator('.row-scroll .card.item').evaluateAll(es => es.map(e => {
          const r = e.getBoundingClientRect();
          const art = e.querySelector('.art').getBoundingClientRect();
          const title = e.querySelector('.ctitle').getBoundingClientRect();
          const info = e.querySelector('.cinfo').getBoundingClientRect();
          return { h: r.height, artHeight: art.height, artRight: art.right, titleLeft: title.left, infoLeft: info.left, titleBottom: title.bottom, artTop: art.top, artBottom: art.bottom, infoTop: info.top, cardBottom: r.bottom, infoBottom: info.bottom };
        }));
        assert(items.length > 0);
        if (height > 800) for (const item of items) {
          assert(item.artHeight >= 50, 'full card art must have at least 50 CSS pixels');
          if (height >= 1000) assert(item.artTop >= item.titleBottom - 1 && item.artBottom <= item.infoTop + 1, 'portrait art must not collide with title or summary');
          else assert(item.artRight <= item.titleLeft + 1 && item.artRight <= item.infoLeft + 1, 'landscape art must not collide with text');
          assert(item.infoBottom <= item.cardBottom + 1, 'summary fits card');
        }
        const arrows = page.locator('.card-arrow,.row-arrow');
        const targets = [];
        for (let n = 0; n < await arrows.count(); n++) {
          const a = arrows.nth(n);
          if (!await a.isVisible()) continue;
          await a.scrollIntoViewIfNeeded();
          targets.push(await a.evaluate(e => {
            const r = e.getBoundingClientRect();
            const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
            return { label: e.getAttribute('aria-label'), width: r.width, height: r.height, hit: e === hit || e.contains(hit) };
          }));
        }
        assert(targets.every(t => t.width >= 40 && t.height >= 40 && t.hit), 'all transfers reachable after real scrolling');
        if (scene === 'camp') {
          const food = await page.locator('.pot-card.in-fire .pc-foods').boundingBox();
          assert(food.y + food.height <= rows[1].bottom + 1, 'pot food fits facility row');
        }
        await page.locator('.row-scroll,.fac-scroll,.map-adjacent').evaluateAll(es => es.forEach(e => { e.scrollLeft = 0; e.scrollTop = 0; }));
        await shot(scene + '-' + width);
        const after = await page.evaluate(k => JSON.parse(localStorage.getItem(k)), key);
        assert.equal(after.GameMinute, before.GameMinute);
        assert.equal(after.Rng.State, before.Rng.State);
        assert.deepEqual(after.Items, before.Items);
        measurements.push({ scene, width, height, rows, items, targets });
      }
    }
    return measurements;
  });
  await run('selected, exhausted, hover and keyboard focus retain meaning', async () => {
    await page.setViewportSize({ width: 1600, height: 900 });
    await load(fixtures.hardware);
    const card = page.locator('.res-card').filter({ hasText: '锅具箱' });
    await card.focus(); await page.keyboard.press('Enter');
    assert(await card.evaluate(e => e.classList.contains('selected')));
    assert(await page.locator('.res-card.dim button').first().isDisabled());
    await card.screenshot({ path: path.join(out, 'screenshots/resource-selected.png') });
    await page.locator('.res-card.dim').first().screenshot({ path: path.join(out, 'screenshots/resource-exhausted.png') });
    const button = card.locator('button');
    await button.hover(); await page.keyboard.press('Tab'); await button.focus();
    assert(await button.evaluate(e => e.matches(':focus-visible')));
    await shot('keyboard-focus');
    return { selected: true, exhausted: true, keyboardFocus: true };
  });
  await run('new art decodes in the deployed base path; missing art never blocks play', async () => {
    const assets = ['art/v10/resource-focus.png', 'art/v10/hardware.png'];
    const decoded = await page.evaluate(async paths => {
      return Promise.all(paths.map(async src => {
        const image = new Image(); image.src = src; await image.decode();
        return { src, width: image.naturalWidth, height: image.naturalHeight };
      }));
    }, assets);
    assert.equal(decoded[0].width, decoded[0].height * 3, 'resource atlas keeps square source cells');
    await load(fixtures.hardware);
    assert.equal(await page.locator('.res-card .focus-art').count(), 3, 'three distinct hardware resource illustrations');
    const time = await page.evaluate(k => JSON.parse(localStorage.getItem(k)).GameMinute, key);
    await page.route('**/art/v10/**', r => r.abort());
    // Deliberately omit decode here: this is the missing-image fallback case.
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.getByRole('button', { name: '继续', exact: true }).click();
    await page.locator('.res-card').filter({ hasText: '锅具箱' }).click();
    assert(await page.locator('.op-head').isVisible());
    assert(await page.locator('.res-card').filter({ hasText: '锅具箱' }).locator('button').isEnabled());
    await page.locator('.nav').getByRole('button', { name: /^系统说明/ }).click();
    await page.getByRole('dialog').waitFor(); await page.keyboard.press('Escape');
    assert.equal(await page.evaluate(k => JSON.parse(localStorage.getItem(k)).GameMinute, key), time);
    await shot('missing-v10-art');
    await page.unroute('**/art/v10/**');
    return { decoded, selection: true, actionAvailable: true, navigation: true, timeUnchanged: true };
  });
  report.failed = report.results.filter(r => !r.pass).length;
  if (report.errors.length || report.httpErrors.length) report.failed++;
  await save(); await browser.close();
  if (report.failed) process.exitCode = 1;
})().catch(e => { console.error(e); process.exitCode = 1; });
