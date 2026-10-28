// Run against a local build or deployed Pages build. Uses an isolated browser profile.
// NODE_PATH must include Playwright; CHROME_PATH can override the local Chrome binary.
const { chromium } = require('playwright');
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const url = process.argv[2] || 'http://127.0.0.1:5177/?dev=1';
const out = path.resolve(process.argv[3] || 'doc/验收/主界面美术换肤 v0.8');
const key = 'survival.save.v1';
const sizes = [[1280,720],[1600,900],[1920,1080]];
(async () => {
  const fixtures = JSON.parse(await fs.readFile(path.resolve('doc/验收/主界面美术换肤 v0.8/fixtures.json'),'utf8'));
  await fs.mkdir(path.join(out,'screenshots'), {recursive:true});
  const browser = await chromium.launch({executablePath:process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
  const context = await browser.newContext({viewport:{width:1280,height:720},deviceScaleFactor:1});
  const page = await context.newPage();
  page.setDefaultTimeout(7000);
  const report = {url,browser:browser.version(),deviceScaleFactor:1,results:[],errors:[],httpErrors:[]};
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('response', r => {if(r.status()>=400) report.httpErrors.push({url:r.url(),status:r.status()});});
  const write = () => fs.writeFile(path.join(out,'runtime-results.json'),JSON.stringify(report,null,2));
  const state = () => page.evaluate(k => JSON.parse(localStorage.getItem(k)),key);
  const checkpoint = async name => {const s=await state();report.results.push({name,minute:s.GameMinute,items:s.Items,main:s.Main,round:s.Battle?.Round,ap:s.Battle?.PlayerAp});await write();};
  const run = async (name,fn) => {try{const detail=await fn();report.results.push({name,pass:true,detail});console.log('PASS '+name);}catch(e){report.results.push({name,pass:false,error:e.message});console.log('FAIL '+name+': '+e.message);}await write();};
  const load = async s => {await page.evaluate(([k,s])=>localStorage.setItem(k,JSON.stringify(s)),[key,s]);await page.reload({waitUntil:'networkidle'});await page.getByRole('button',{name:'继续',exact:true}).click();await page.locator('.game').waitFor();};
  const shot = name => page.screenshot({path:path.join(out,'screenshots',name+'.png')});
  const waitIdle = async () => {await page.waitForFunction(k=>{const s=JSON.parse(localStorage.getItem(k));return !s.Main&&!s.Sub;},key);await page.locator('.cprog').waitFor({state:'hidden'});};
  const resize = ([width,height]) => page.setViewportSize({width,height});
  const drag = async (a,b) => {await a.scrollIntoViewIfNeeded();let r=await a.boundingBox();await page.mouse.move(r.x+r.width/2,r.y+r.height/2);await page.mouse.down();await b.scrollIntoViewIfNeeded();r=await b.boundingBox();await page.mouse.move(r.x+r.width/2,r.y+r.height/2,{steps:15});await page.mouse.up();};
  const count = (s,id,owner) => s.Items.filter(i=>i.ItemId===id&&(!owner||i.Owner===owner)).reduce((n,i)=>n+i.Count,0);
  const clearDialogs = async () => {for(let n=0;n<30&&await page.locator('.dlg-layer').count();n++){await page.locator('.dlg-layer').getByRole('button',{name:/^(继续|结束对话)$/}).click();}};
  const firstStart=Date.now();await page.goto(url,{waitUntil:'networkidle'});report.firstTitleMs=Date.now()-firstStart;

  await run('main layout, text, targets and six screenshots',async()=>{
    const measures=[];
    for(const [id,s] of [['hardware',fixtures.hardware],['camp',fixtures.camp]]){
      await load(s);
      for(const size of sizes){await resize(size);if(id==='hardware')await page.locator('.res-card').filter({hasText:'锅具箱'}).click();
        const boxes=await page.locator('.map-row,.facility-row,.section.ground,.section.carried').evaluateAll(es=>es.map(e=>({class:e.className,...e.getBoundingClientRect().toJSON()})));
        assert.equal(boxes.length,4);for(const b of boxes){assert(b.height>70&&b.y>=0&&b.bottom<=size[1],JSON.stringify(b));}
        const targets=await page.locator('.nav button,.card-arrow,.row-arrow').evaluateAll(es=>es.filter(e=>e.offsetWidth).map(e=>({w:e.getBoundingClientRect().width,h:e.getBoundingClientRect().height})));
        assert(targets.every(b=>b.w>=40&&b.h>=40));
        const fonts=await page.locator('.vital-label,.row-scroll .ctitle').evaluateAll(es=>es.map(e=>parseFloat(getComputedStyle(e).fontSize)));assert(fonts.every(f=>f>=16));
        if(id==='camp'){const food=await page.locator('.pot-card.in-fire .pc-foods').boundingBox();assert(food.y+food.height<=boxes[1].bottom+1,'food drop zone must fit facility row');}
        await shot(id+'-'+size[0]);measures.push({id,size,boxes,targets,fonts});
      }
    }return measures;
  });
  await run('navigation, detail, hover and no idle time/RNG cost',async()=>{
    await load(fixtures.hardware);const before=await state();const checks=[];
    for(const size of sizes){await resize(size);for(const label of ['区域地图','手工制作','休息','行动日志','生存目标','生存提示','对话记录','系统说明','菜单']){
      const start=Date.now();await page.locator('.nav').getByRole('button',{name:new RegExp('^'+label)}).click();const d=page.getByRole('dialog');await d.waitFor();checks.push({size,label,ms:Date.now()-start});await d.getByRole('button',{name:/^关闭/}).click();
    }await page.locator('.vital[data-stat="hydration"]').click();await page.locator('.map-card button').first().hover();}
    const after=await state();assert.equal(after.GameMinute,before.GameMinute);assert.equal(after.Rng.State,before.Rng.State);assert.deepEqual(after.Items,before.Items);return checks;
  });
  await run('edge popovers and sub-baseline viewport',async()=>{
    await load(fixtures.hardware);const checks=[];
    for(const size of [...sizes,[1024,640]]){await resize(size);await page.locator('.map-adjacent .oc-btn').last().click();const pop=page.locator('.apop');await pop.waitFor({state:'visible'});const b=await pop.boundingBox();assert(b.x>=0&&b.y>=0&&b.x+b.width<=size[0]+1&&b.y+b.height<=size[1]+1);checks.push({size,b});await pop.locator('.apop-x').click();}return checks;
  });
  await run('continuous transfer, drag, ignition, cooking and save recovery',async()=>{
    await resize(sizes[0]);const s=structuredClone(fixtures.base);s.Items.push({...s.Items[3],InstanceId:30,ItemId:'raw_root',Count:2,Owner:'camp',ExpiresAt:4800});s.NextInstanceId=31;await load(s);await checkpoint('flow 01 initial');
    const water=()=>page.locator('.ground .card.item').filter({hasText:'净水'});
    await water().click();assert.equal(count(await state(),'clean_water','camp'),2);await water().getByRole('button',{name:'拾取1件'}).click();assert.equal(count(await state(),'clean_water','carried'),1);
    await page.locator('.ground .card.item').filter({hasText:'木材'}).getByRole('button',{name:'拾取1件'}).hover();await page.mouse.down();await page.waitForTimeout(750);await page.mouse.up();assert.equal(count(await state(),'wood','carried'),2);await checkpoint('flow 02 arrow and hold');
    await drag(page.locator('.carried .pc-head'),page.locator('.slot-empty').first());assert.equal((await state()).Items.find(i=>i.InstanceId===1).Owner,'facility:camp:fire');
    await drag(page.locator('.carried .card.item').filter({hasText:'净水'}),page.locator('[data-drop="potfill:1"]'));assert.equal(count(await state(),'clean_water','pot:1'),1);
    const invalid=await state();await drag(page.locator('.carried .card.item').filter({hasText:'木材'}),page.locator('[data-drop="potfill:1"]'));assert.deepEqual((await state()).Items,invalid.Items);
    await drag(page.locator('.ground .card.item').filter({hasText:'生块根'}),page.locator('[data-drop="potfood:1"]'));assert.equal(count(await state(),'raw_root','pot:1'),1);await checkpoint('flow 03 valid and invalid drops');
    await page.getByRole('button',{name:/^投入木材/}).click();await page.locator('#fc-ignite').click();await page.locator('.im-item').first().click();await waitIdle();assert((await state()).Locations.camp.Facility.FireLit);await checkpoint('flow 04 ignition');
    await page.getByRole('button',{name:/^等待烹饪/}).click();await waitIdle();assert.equal(count(await state(),'boiled_root','pot:1'),1);await checkpoint('flow 05 cooked in place');
    await page.locator('.pot-card.in-fire .pc-head > .row-arrow').click();assert.equal((await state()).Items.find(i=>i.InstanceId===1).Owner,'carried');assert.equal(count(await state(),'boiled_root','pot:1'),1);
    if(await page.locator('.toast button').count())await page.locator('.toast button').click();
    await page.locator('.carried .pot-card .pc-row.food .row-arrow').click();assert.equal(count(await state(),'boiled_root','carried'),1);const before=await state();await page.reload({waitUntil:'networkidle'});await page.getByRole('button',{name:'继续',exact:true}).click();await page.locator('.game').waitFor();assert.deepEqual((await state()).Items,before.Items);assert.equal((await state()).GameMinute,before.GameMinute);await checkpoint('flow 06 whole pot, child and refresh');
  });
  await run('action exit preserves committed cost and forfeits result',async()=>{
    await load(fixtures.hardware);await resize(sizes[0]);await page.locator('.nav').getByRole('button',{name:/^菜单/}).click();await page.getByRole('button',{name:'慢',exact:true}).click();await page.getByRole('dialog').getByRole('button',{name:/^关闭/}).click();
    const before=await state();await page.locator('.res-card').filter({hasText:'锅具箱'}).locator('button').click();await page.locator('.cprog:not(.fading) .cp-exit').click();await waitIdle();const after=await state();assert(after.GameMinute>before.GameMinute&&after.GameMinute<before.GameMinute+45);assert.equal(count(after,'pot','hardware'),count(before,'pot','hardware'));return {before:before.GameMinute,after:after.GameMinute,stock:after.Resources.pot_box.Available};
  });
  await run('pause, resume and exit',async()=>{
    const s=structuredClone(fixtures.base);s.Stats.Hydration=20.05;await load(s);await page.locator('.nav').getByRole('button',{name:/^休息/}).click();await page.locator('.rg-start').click();await page.locator('.cprog.paused').waitFor();await checkpoint('paused at hydration threshold');await page.locator('.cprog .cp-go').click();await page.locator('.cprog .cp-exit').click();await waitIdle();await checkpoint('resumed then exited');
  });
  await run('new game dialogue input lock at three sizes',async()=>{
    await page.evaluate(k=>localStorage.removeItem(k),key);await page.reload({waitUntil:'networkidle'});await page.getByRole('button',{name:'新游戏',exact:true}).click();await page.locator('.dlg-layer').waitFor();const before=await state();
    for(const size of sizes){await resize(size);await page.locator('.dlg-stream').hover();await page.mouse.wheel(0,200);assert.equal((await state()).GameMinute,before.GameMinute);assert(await page.locator('.sidebar').evaluate(e=>e.inert));const fonts=await page.locator('.dlg-bubble,.dlg-narr').evaluateAll(es=>es.map(e=>parseFloat(getComputedStyle(e).fontSize)));assert(fonts.every(f=>f>=20));await shot('dialogue-'+size[0]);}await clearDialogs();
  });
  await run('travel and a battle round at each size',async()=>{
    for(const size of sizes){await resize(size);await load(fixtures.base);await page.locator('.map-adjacent .oc-btn').first().click();await page.getByRole('button',{name:'确认移动',exact:true}).click();await page.locator('.dlg-layer').waitFor();await clearDialogs();await page.locator('.cprog').waitFor({state:'hidden'});assert.equal((await state()).CurrentLocation,'field');await shot('battle-'+size[0]);
      await page.waitForFunction(k=>JSON.parse(localStorage.getItem(k)).Battle?.PlayerAp>0,key);const ap=(await state()).Battle.PlayerAp;await page.locator('.battle-overlay').getByRole('button',{name:/^攻击/}).click();await page.getByRole('button',{name:'执行 · 攻击 · 1AP',exact:true}).click();assert.equal((await state()).Battle.PlayerAp,ap-1);await page.locator('.battle-overlay').getByRole('button',{name:/^结束行动/}).click();await page.getByRole('button',{name:/^执行 · 结束行动/}).click();await page.waitForFunction(k=>JSON.parse(localStorage.getItem(k)).Battle?.Round===2,key);await checkpoint('battle round complete '+size[0]);
    }
  });
  await run('eight scenes, missing image, cache reload and night',async()=>{
    const data=[];for(const id of ['camp','field','woods','stream','village','hardware','scrapyard','clinic']){const r=await page.request.get(new URL('art/v08/'+id+'.png',url).href);assert(r.ok());data.push({id,bytes:(await r.body()).length});}
    await page.route('**/art/v08/hardware.png',r=>r.abort());await load(fixtures.hardware);assert(await page.locator('.map-card button').first().isEnabled());await page.locator('.res-card').filter({hasText:'锅具箱'}).click();await page.unroute('**/art/v08/hardware.png');
    const night=structuredClone(fixtures.hardware);night.GameMinute=23*60;await load(night);assert((await page.locator('.mc-top').innerText()).includes('夜'));const start=Date.now();await page.reload({waitUntil:'networkidle'});report.cachedTitleMs=Date.now()-start;assert.equal(report.errors.length,0);assert.equal(report.httpErrors.length,0);return data;
  });
  report.failed=report.results.filter(r=>r.pass===false).length;await write();await browser.close();if(report.failed)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;});
