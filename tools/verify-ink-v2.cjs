// Run against the published game. Requires Playwright and a Chromium browser.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

const url = process.argv[2] || 'http://localhost:5180/?dev=1';
const output = path.resolve(process.argv[3] || 'ink-v2-check');
const plan = require('../doc/策划案/重墨像素风界面与卡片 v0.9/assets/重墨V2重制/替换清单.json');
const config = require('../data/config.v0.6.json');
const expected = Object.keys(plan.bindings);
const prefix = 'art/ink-v2/';

(async () => {
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch({ executablePath: process.env.CHROME, headless: true });
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    page.setDefaultTimeout(10000);
    const errors = [];
    const captures = [];
    const skills = new Set();
    const duties = new Set();
    const nav = label => page.locator('.top .nav').getByRole('button', { name: label });
    const click = (label, options = {}) => page.getByRole('button', { name: label, ...options }).first().click();
    const art = key => page.locator('[data-art="' + key + '"]');
    const menu = async () => { await readDialogue(); await nav(/^菜单/).click(); };
    const close = () => page.keyboard.press('Escape');
    const dev = async label => { await menu(); await click(label, { exact: true }); };
    const teleport = async id => {
        await menu();
        await page.getByRole('combobox', { name: '传送目标' }).selectOption(id);
        await click('传送（按实际到达处理）', { exact: true });
    };
    const readDialogue = async () => {
        const next = page.locator('.dlg-layer .dlg-next').first();
        if (!await next.isVisible()) {
            try { await next.waitFor({ timeout: 450 }); } catch { return; }
        }
        for (let i = 0; i < 55; i++) {
            if (!await next.isVisible()) return;
            await next.click();
        }
        throw new Error('对白仍在播放');
    };
    const checkPage = async name => {
        await page.evaluate(() => document.fonts.ready);
        const old = await page.evaluate(() => [...document.querySelectorAll('svg image, img')].map(node =>
            node.getAttribute('href') || node.getAttribute('src') || '').concat(
            performance.getEntriesByType('resource').map(entry => entry.name)).filter(src =>
            /img\/portraits\/|art\/v08\/(?:supplies|tools|equipment|creatures|resources)\.png|art\/v10\/resource-focus/.test(src)));
        assert.deepEqual(old, [], name + ': old art source');
        assert.equal(await page.locator('#blazor-error-ui').isVisible(), false, name);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
        assert.equal(overflow, false, name + ': viewport overflow');
    };
    const capture = async (name) => {
        await checkPage(name);
        const filename = name + '.png';
        await page.screenshot({ path: path.join(output, filename) });
        captures.push(filename);
    };

    page.on('pageerror', e => errors.push(e.message));
    page.on('console', e => { if (e.type() === 'error') errors.push(e.text()); });
    page.on('response', r => { if (r.status() >= 400 && r.url().includes(prefix)) errors.push(r.status() + ' ' + r.url()); });

    try {
        await page.goto(url);
        await page.locator('.title-card').waitFor({ timeout: 60000 });
        await capture('01-标题');
        const deployed = await page.evaluate(async (root) => {
            const response = await fetch(root + 'manifest.json');
            if (!response.ok) throw new Error('missing runtime manifest');
            const data = await response.json();
            const files = [...new Set([...Object.values(data.sprites).map(x => x.file),
                ...Object.values(data.portraits), ...Object.values(data.scenes)])];
            for (const file of files) {
                const image = new Image();
                image.src = root + file;
                await image.decode();
            }
            return data;
        }, prefix);
        for (const id of expected) {
            assert.deepEqual(deployed.sprites[id]?.crop, plan.bindings[id].crop, id);
            assert.match(deployed.sprites[id].file, /^atlas-.*\.webp$/);
        }
        for (const item of config.items) assert.ok(deployed.sprites['item:' + item.id], item.id);

        await click('新游戏', { exact: true });
        await page.locator('.dlg-layer').waitFor();
        await page.locator('.dlg-side.right img[src$="portrait-actor-baobao.webp"]').waitFor();
        const typography = await page.locator('.dlg-bubble, .dlg-narr').first().evaluate(el => {
            const style = getComputedStyle(el);
            return [style.fontFamily, style.fontSize, style.lineHeight, style.fontSynthesis];
        });
        assert.match(typography[0], /Chill Bitmap 16/);
        assert.deepEqual(typography.slice(1), ['20px', '34px', 'none']);
        await page.evaluate(() => document.fonts.ready);
        const cdp = await page.context().newCDPSession(page);
        await cdp.send('DOM.enable');
        await cdp.send('CSS.enable');
        const { root } = await cdp.send('DOM.getDocument');
        const { nodeId } = await cdp.send('DOM.querySelector', { nodeId: root.nodeId, selector: '.dlg-bubble, .dlg-narr' });
        const { fonts } = await cdp.send('CSS.getPlatformFontsForNode', { nodeId });
        assert.ok(fonts.some(font => font.isCustomFont && font.glyphCount > 0), JSON.stringify(fonts));
        await capture('02-开场对白');
        await readDialogue();
        const saveKey = 'survival.save.v1';
        const inventorySave = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), saveKey);
        const normalUrl = new URL(url);
        normalUrl.searchParams.delete('dev');
        const menuPage = await browser.newPage({ viewport: page.viewportSize() });
        await menuPage.addInitScript(([key, save]) => {
            if (!localStorage.getItem(key)) localStorage.setItem(key, save);
        }, [saveKey, JSON.stringify(inventorySave)]);
        await menuPage.goto(normalUrl.href);
        await menuPage.locator('.title-card').getByRole('button', { name: '继续', exact: true }).click();
        await menuPage.locator('.top .nav').getByRole('button', { name: /^菜单/ }).click();
        const reading = menuPage.getByRole('combobox', { name: '阅读字体' });
        await reading.selectOption('clear');
        assert.equal(await menuPage.evaluate(() => localStorage.getItem('survival.reading-mode')), 'clear');
        await menuPage.screenshot({ path: path.join(output, '03-游戏菜单-清晰阅读.png') });
        captures.push('03-游戏菜单-清晰阅读.png');
        await menuPage.reload();
        await menuPage.locator('.title-card').getByRole('combobox', { name: '阅读字体' }).waitFor();
        await menuPage.waitForFunction(() => document.querySelector('.title-card select').value === 'clear');
        await menuPage.locator('.title-card').getByRole('button', { name: '继续', exact: true }).click();
        await menuPage.locator('.top .nav').getByRole('button', { name: /^菜单/ }).click();
        await reading.selectOption('pixel');
        await menuPage.evaluate(() => document.fonts.ready);
        await menuPage.screenshot({ path: path.join(output, '04-游戏菜单-细像素.png') });
        captures.push('04-游戏菜单-细像素.png');
        await menuPage.close();

        inventorySave.Items = config.items.map((item, i) => ({
            ItemId: item.id, InstanceId: i + 1000, Count: 1, Owner: inventorySave.CurrentLocation,
            ExpiresAt: item.shelfLifeMinutes ? inventorySave.GameMinute + item.shelfLifeMinutes : null,
            Durability: item.durabilityMaxRange ? 20 : null, MaxDurability: item.durabilityMaxRange ? 20 : null,
        }));
        inventorySave.NextInstanceId = 1000 + inventorySave.Items.length;
        const inventoryPage = await browser.newPage();
        await inventoryPage.addInitScript(([key, save]) => localStorage.setItem(key, save), [saveKey, JSON.stringify(inventorySave)]);
        await inventoryPage.goto(url);
        await inventoryPage.locator('.title-card').getByRole('button', { name: '继续', exact: true }).click();
        await inventoryPage.locator('.ground [data-art^="item:"]').first().waitFor();
        const itemArts = new Set(await inventoryPage.locator('.ground [data-art^="item:"]').evaluateAll(nodes => nodes.map(n => n.dataset.art)));
        assert.deepEqual([...itemArts].sort(), config.items.map(x => 'item:' + x.id).sort());
        await inventoryPage.close();

        await dev('开发测试箱');
        await readDialogue();
        const item = page.locator('[data-card]').filter({ has: art('item:wood') }).first();
        await item.click();
        await page.locator('.detail-icon').filter({ has: art('item:wood') }).waitFor();
        await readDialogue();
        await capture('05-物品卡与详情');
        await nav(/^手工制作/).click();
        await page.locator('.recipe [data-art^="item:"]').first().waitFor();
        await readDialogue();
        await capture('06-制作');
        await close();

        await nav(/^成长/).click();
        await readDialogue();
        for (const button of await page.locator('button[data-skill]').all()) {
            const id = await button.getAttribute('data-skill');
            await button.click();
            assert.equal(await button.locator('[data-art="skillFamily:' + id + '"]').count(), 1);
            const rows = await page.locator('.growth-node').all();
            assert.equal(rows.length, 5);
            for (const row of rows) {
                const skill = await row.getAttribute('data-node');
                assert.equal(await row.locator('[data-art="skill:' + skill + '"]').count(), 1);
                skills.add(skill);
            }
        }
        assert.equal(skills.size, 75);
        await capture('07-成长技能');
        await close();

        for (const post of config.city.npcs) {
            await teleport(post.locationId);
            await readDialogue();
            const card = page.locator('#npc-' + post.npcId);
            await card.locator('[data-art="actor:' + post.npcId + '"]').waitFor();
            await dev('当前地点岗位离岗/回岗');
            await card.locator('[data-art="duty:' + post.npcId + '"]').waitFor();
            duties.add(post.npcId);
            if (post.npcId === 'npc_xu_an') await capture('08-代班人物卡');
            await card.getByRole('button', { name: /^交谈/ }).click();
            const portrait = 'portrait-duty-' + post.npcId + '.webp';
            await page.locator('.dlg-side.right img[src$="' + portrait + '"]').waitFor();
            if (post.npcId === 'npc_xu_an') await capture('09-代班对白');
            await readDialogue();
            await dev('当前地点岗位离岗/回岗');
            await card.locator('[data-art="actor:' + post.npcId + '"]').waitFor();
            if (post.npcId === 'npc_xu_an') {
                await nav(/^对话记录/).click();
                await page.locator('.dlog-read img[src$="' + portrait + '"]').waitFor();
                await capture('10-代班历史回看');
                await close();
            }
        }
        assert.equal(duties.size, 16);

        await dev('到收容所（记登记）');
        await readDialogue();
        await page.locator('#npc-npc_du_sui').getByRole('button', { name: /^事项/ }).click();
        const candidate = page.locator('.rc-card img[src*="portrait-recruitTemplate-"]').first();
        await candidate.waitFor();
        await candidate.evaluate(el => el.scrollIntoView({ block: 'center' }));
        await capture('11-招募职业样例');
        await close();
        await click('主城鸟瞰', { exact: true });
        await art('faction:hub_survivor').waitFor();
        await capture('12-势力鸟瞰');
        await close();

        await teleport('field');
        await readDialogue();
        if (!await page.locator('.bfs-console').isVisible()) {
            await dev('本地敌人立即形成敌踪');
            await click(/^接敌/);
        }
        await page.locator('.bfs-console [data-art^="skill:"]').first().waitFor();
        assert.ok(await page.locator('.bfs-console [data-art^="action:"]').count() > 0);
        assert.ok(await page.locator('.bfs-scene [data-art^="enemy:"]').count() > 0);
        await capture('13-战斗技能与敌人');
        await page.locator('.bfs-top').getByRole('button', { name: /^菜单/ }).click();
        await page.getByRole('dialog', { name: '战斗菜单' }).getByRole('combobox', { name: '阅读字体' }).waitFor();
        await capture('14-战斗菜单');

        for (const [width, height] of [[1280, 720], [1366, 768], [1600, 900], [1920, 1080]]) {
            await page.setViewportSize({ width, height });
            await checkPage(width + 'x' + height);
        }
        assert.deepEqual(errors, []);
        const result = { build: await page.locator('meta[name=app-build]').getAttribute('content'),
            bindings: expected.length, items: itemArts.size, skillNodes: skills.size, posts: duties.size, typography, fonts, captures };
        fs.writeFileSync(path.join(output, '验收结果.json'), JSON.stringify(result, null, 2));
        console.log(JSON.stringify(result));
    } catch (error) {
        await page.screenshot({ path: path.join(output, '失败现场.png') }).catch(() => {});
        throw error;
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
