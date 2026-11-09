'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),http=require('node:http'),vm=require('node:vm');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=path.resolve(__dirname,'..'), scratch=path.resolve(process.argv[2]||path.join(root,'临时图层'));
const sandbox={window:{}};vm.runInNewContext(fs.readFileSync(path.join(__dirname,'预览数据.js'),'utf8'),sandbox);
const model=sandbox.window.INK_BOARDS, pages=process.argv[3]?model.pages.filter(p=>process.argv[3].split(',').includes(p.id)):model.pages;
const type={'.webp':'image/webp','.png':'image/png','.svg':'image/svg+xml','.woff2':'font/woff2','.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.md':'text/plain; charset=utf-8'};
const server=http.createServer((req,res)=>{
 const url=new URL(req.url,'http://localhost');
 if(url.pathname==='/render'){
  const board=model.pages.find(b=>b.id===url.searchParams.get('id'));
  if(!board){res.writeHead(404);res.end();return;}
  res.setHeader('Content-Type',type['.html']);
  res.end('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><style>html,body{margin:0;background:transparent;width:1600px;height:900px;overflow:hidden}svg{display:block}</style>'+board.svg+'</html>');return;
 }
 const full=path.resolve(root,'.'+decodeURIComponent(url.pathname));
 if(!full.startsWith(root+path.sep)||!fs.existsSync(full)||!fs.statSync(full).isFile()){res.writeHead(404);res.end();return;}
 res.setHeader('Content-Type',type[path.extname(full)]||'application/octet-stream');
 fs.createReadStream(full).pipe(res);
});
function mkdir(p){fs.mkdirSync(p,{recursive:true});}
(async()=>{
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME,args:['--allow-file-access-from-files']});
 const url='http://127.0.0.1:'+server.address().port;
 mkdir(scratch);mkdir(path.join(root,'效果图'));
 const results=[], errors=[];
 async function worker(jobs){
  const page=await browser.newPage({viewport:{width:1600,height:900},deviceScaleFactor:1});
  page.on('pageerror',error=>errors.push(error.message));
  page.on('response',r=>{if(r.status()>=400)errors.push(r.url()+' '+r.status());});
  try{
   for(const board of jobs){
    await page.goto(url+'/render?id='+board.id);
    await page.evaluate(async()=>{await document.fonts.ready;await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));});
    const check=await page.evaluate(()=>{
     const groups=[...document.querySelectorAll('svg > g[data-layer]')], images=[...document.querySelectorAll('image')];
     const outside=[...document.querySelectorAll('text')].map(el=>[el.textContent,el.getBBox()]).filter(([t,b])=>b.x<0||b.y<0||b.x+b.width>1600.1||b.y+b.height>900.1).map(a=>a[0]);
     return {layers:groups.map(g=>g.childElementCount),images:images.length,outside,font:[...document.fonts].some(f=>f.family==='Ink16'&&f.status==='loaded')};
    });
    assert.equal(check.layers.length,7,board.id);assert.ok(check.layers.every(n=>n>0),board.id+' empty layer');assert.ok(check.images>=3,board.id);assert.ok(check.font,board.id);assert.deepEqual(check.outside,[],board.id);
    const folder=path.join(scratch,board.id);mkdir(folder);
    await page.screenshot({path:path.join(root,'效果图',board.id+'.png'),omitBackground:true});
    for(let i=1;i<=7;i++){
     await page.evaluate(i=>{for(const g of document.querySelectorAll('svg > g[data-layer]'))g.style.display=Number(g.dataset.layer)===i?'':'none';},i);
     await page.screenshot({path:path.join(folder,i+'.png'),omitBackground:true});
    }
    results.push({id:board.id,...check});
    if(results.length%10===0||pages.length<10)console.log('Exported '+results.length+' / '+pages.length);
   }
  }finally{await page.close();}
 }
 try{
  const parallel=Math.min(3,pages.length);
  await Promise.all(Array.from({length:parallel},(_,i)=>worker(pages.filter((_,j)=>j%parallel===i))));
  if(!process.argv[3]){
   const pg=await browser.newPage();
   await pg.goto(pathToFileURL(path.join(root,'打开全界面.html')).href+'#P05a');
   for(const width of [1280,1366,1600,1920]){
    await pg.setViewportSize({width,height:900});
    assert.equal(await pg.locator('select#page option').count(),67);
    await pg.getByRole('button',{name:'仅美术',exact:true}).click();
    assert.equal(await pg.locator('#sheet g[data-layer]:visible').count(),2);
    await pg.getByRole('button',{name:'仅 UI',exact:true}).click();
    assert.equal(await pg.locator('#sheet g[data-layer]:visible').count(),5);
    await pg.getByRole('button',{name:'合成',exact:true}).click();
   }
   await pg.getByRole('button',{name:'清晰阅读'}).click();
   assert.ok(await pg.locator('#sheet svg').first().evaluate(e=>e.classList.contains('clear')));
   await pg.locator('#search').fill('代班');
   assert.ok(await pg.locator('[data-id="P09d"]').isVisible());
   await pg.locator('[data-id="P09d"]').click();
   assert.equal(await pg.locator('select#page').inputValue(),'P09d');
   await pg.close();
  }
  assert.deepEqual(errors,[]);
  fs.writeFileSync(path.join(scratch,'渲染记录.json'),JSON.stringify(results.sort((a,b)=>a.id.localeCompare(b.id)),null,2));
 }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;server.close();});
