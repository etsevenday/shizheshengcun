'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'),contract=require('./动效合同.json');
const out=path.join(root,'动效分镜'),scratch=process.argv[2];
fs.mkdirSync(out,{recursive:true});fs.mkdirSync(scratch,{recursive:true});
const url=pathToFileURL(path.join(root,'打开动效样机.html')).href;
const gallery=pathToFileURL(path.join(root,'打开全界面.html')).href;
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME});
 const page=await browser.newPage({viewport:{width:1626,height:1120}}),errors=[];
 page.on('pageerror',err=>errors.push(err.message));page.on('requestfailed',req=>{if(req.failure()?.errorText!=='net::ERR_ABORTED')errors.push(req.url()+' '+req.failure()?.errorText);});
 page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 const seek=t=>page.locator('#time').evaluate((el,value)=>{el.value=value;el.dispatchEvent(new Event('input',{bubbles:true}));},t);
 const select=id=>page.locator('#profile').selectOption(id);
 const shot=file=>page.locator('#stage').screenshot({path:file});
 const results=[];
 try{
  await page.goto(url);await page.evaluate(()=>document.fonts.ready);
  assert.equal(await page.locator('#profile option').count(),23);
  for(const p of contract.profiles){
   await select(p.id);
   if(p.mode==='map'){
    const key=await page.locator('.building[aria-pressed=false]').first().getAttribute('data-building');
    const target=page.locator('[data-building="'+key+'"]'),name=await target.getAttribute('data-name');
    await target.hover();await page.waitForFunction(id=>Number.parseFloat(getComputedStyle(document.querySelector('[data-building="'+id+'"] .building-outline')).strokeWidth)>2.5,key);
    assert.equal(await page.locator('.building[aria-pressed=true]').count(),1);
    await target.focus();await page.keyboard.press('Enter');
    assert.equal(await target.getAttribute('aria-pressed'),'true');
    assert.equal(await page.locator('[data-building-name]').textContent(),name);
    assert.ok((await page.locator('#phase').textContent()).includes(name));
    await shot(path.join(out,'建筑-'+p.id+'.png'));
   }else{
    const midpoint=p.mode==='battle'?560:p.mode==='dialogue'?1000:p.mode==='action'?700:450;
    await seek(midpoint);
    if(p.mode==='battle')assert.ok(Number(await page.locator('.cine').evaluate(e=>e.style.opacity))>.75,p.id);
    if(p.mode==='dialogue'){
      assert.equal(await page.locator('[data-utterance="1"]').first().getAttribute('data-side'),'right');
      assert.equal(await page.locator('[data-utterance="2"]').first().getAttribute('data-side'),'left');
      assert.equal(await page.locator('[data-utterance="3"]').first().evaluate(el=>el.style.opacity),'0');
    }
    if(p.id==='melee')await shot(path.join(out,'战斗特写-接触后.png'));
    if(p.id==='gather')await shot(path.join(out,'行动推进-中央浮窗.png'));
    if(p.id==='dialogue')await shot(path.join(out,'对白-左右交错.png'));
    if(p.id==='popup')await shot(path.join(out,'菜单-入场完成.png'));
    await page.getByRole('button',{name:'直接看结果'}).click();
    assert.equal(await page.locator('#time').inputValue(),String(p.duration),p.id);
    if(p.mode==='battle')assert.equal(await page.locator('.cine').evaluate(el=>el.style.opacity),'0');
    if(p.id==='reject')assert.ok((await page.locator('.actionInfo').textContent()).includes('无法'));
    if(p.id==='interrupt')assert.equal(await page.locator('.meter>i').evaluate(el=>el.style.width),'50%');
   }
   results.push({id:p.id,mode:p.mode,duration:p.duration});
  }
  await select('melee');await page.getByText('减少动态效果',{exact:true}).click();await seek(600);
  assert.equal(await page.locator('.cine').evaluate(el=>el.style.opacity),'0');
  assert.equal(await page.locator('.recap').evaluate(el=>el.style.opacity),'1');
  await page.getByText('减少动态效果',{exact:true}).click();
  await page.getByRole('button',{name:'从头播放'}).click();
  await page.waitForFunction(()=>Number(document.getElementById('time').value)>130);
  await page.getByRole('button',{name:'暂停',exact:true}).click();
  await page.setViewportSize({width:986,height:850});
  const frames=[];
  for(let i=0;i<=24;i++){
   const t=Math.min(i*50,1200);
   await seek(t);const file=path.join(scratch,'melee-'+String(i).padStart(2,'0')+'.png');
   await shot(file);frames.push(file);
  }
  fs.writeFileSync(path.join(scratch,'关键帧路径.json'),JSON.stringify(frames));
  await page.goto(gallery+'#P07b');
  for(const w of [1280,1366,1600,1920]){
   await page.setViewportSize({width:w,height:940});
   assert.equal(await page.locator('#sheet [data-building]').count(),6);
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  }
  await page.locator('#page').selectOption('P07a');assert.equal(await page.locator('#sheet [data-building]').count(),0);
  await page.locator('#page').selectOption('P07i');assert.equal(await page.locator('#sheet [data-building]').count(),0);
  assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(root,'资料','动效验收.json'),JSON.stringify({profiles:results,cityHoverAndKeyboard:true,nonFactionBuildingTargets:0,reducedMotion:true,previewWidths:[1280,1366,1600,1920],errors},null,2));
  console.log('PASS 23 motion profiles; timeline, preview, reduced motion, 3 faction maps, 4 widths');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
