'use strict';
const config = require('./基线配置.json');
const artManifest = require('../素材/ink-v2/manifest.json');
const {pages,families,extraComponents}=require('./场景清单.cjs');
const fs=require('node:fs');
const W=1600,H=900;
const layers=['01 场景与光影','02 底板与分区','03 主体美术与图标','04 边框与装饰','05 操作控件','06 选中与状态反馈','07 文字与数值'];
const p={black:'#0f0d0b',dark:'#161311',panel:'#1e1915',sub:'#271f19',line:'#54412f',iron:'#8c6b47',gold:'#dfad6c',light:'#f1dfbb',muted:'#b8a488',dim:'#8e7e6e',red:'#a34a36',danger:'#ffb396',sage:'#a4bf9a'};
const sceneFiles={camp:'scene-camp-outskirts.webp',battle:'scene-battle-checkpoint.webp',hub:'scene-01-幸存者营区.webp',church:'scene-02-教会圣所地区.webp',lab:'scene-03-科学院封存地区.webp',woods:'woods.png',field:'field.png',stream:'stream.png',ruin:'scene-d01_07.webp',village:'village.png'};
const e=v=>String(v??'').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const d=(list,key='id')=>Object.fromEntries(list.map(row=>[row[key],row]));
const info={item:d(config.items),enemy:d(config.enemies),skill:d(config.skillNodes),skillFamily:d(config.skills),actor:d(config.city.npcs,'npcId')};
Object.assign(info.actor,d(config.party.namedCompanions,'actorId'));
const names={p0:'你',baobao:'饱饱',fire_lit:'火堆',fire_out:'熄灭的火堆',shelter:'遮蔽棚',bed:'铺位',safehouse:'安全屋',drying_rack:'晾制架',workbench:'工作台',water_station:'净水台',charcoal:'木炭工位',guard:'防御',breathe:'调息',swap:'调整站位',retreat:'撤退',end_turn:'结束行动',bandage:'包扎'};
const nameOf=(key)=>{
 const parts=key.split(':');return parts[0]==='resource' ? config.resources.find(r=>r.locationId===parts[1]&&r.nodeId===parts[2])?.name : info[parts[0]]?.[parts[1]]?.name||names[parts[1]]||parts[1];
};
const list=(...ids)=>ids.map(i=>'item:'+i);
const cup=list('clean_water','canned_food','pot','lighter','bandage');
const ground=list('wood','stone','fiber','berry','raw_root');
const tm='沙土仍带着湿气。先找能安全饮用的水，再检查能不能点着火。';
const rect=(x,y,w,h,fill,stroke='',sw=1)=>'<rect x="'+x+'" y="'+y+'" width="'+w+'" height="'+h+'" fill="'+fill+'"'+(stroke?' stroke="'+stroke+'" stroke-width="'+sw+'"':'')+'/>';
const path=(shape,fill,stroke='',sw=1)=>'<path d="'+shape+'" fill="'+fill+'"'+(stroke?' stroke="'+stroke+'" stroke-width="'+sw+'"':'')+'/>';
function wrap(s,max){
 const rows=[]; let row='',len=0;
 for(const c of String(s)){
   const n=/^[\x00-\x7F]$/.test(c)?0.57:1;
   if(c==='\n'||(len+n>max&&row)){rows.push(row);row='';len=0;if(c==='\n')continue;}
   row+=c;len+=n;
 }
 if(row)rows.push(row);return rows;
}
class Board{
 constructor(page){this.page=page;this.L=layers.map(()=>[]);this.defs=[];this.artKeys=new Set();this.layout=[];this.n=0;}
 mark(type,label,x,y,w,h){this.layout.push({type,label,box:[x,y,w,h].map(v=>Math.round(v*10)/10),context:this.n?'overlay':'base'});}
 add(l,s){this.L[l].push(s);}
 text(x,y,s,opt={}){
  const {size=18,color=p.light,align='start',weight=400,spacing=0}=opt;
  this.add(6,'<text x="'+x+'" y="'+y+'" font-size="'+size+'" fill="'+color+'" text-anchor="'+align+'" font-weight="'+weight+'" letter-spacing="'+spacing+'">'+e(s)+'</text>');
 }
 para(x,y,s,w,opt={}){
  const size=opt.size||19,gap=opt.line||Math.ceil(size*1.65);
  const rows=wrap(s,w/size);rows.forEach((v,i)=>this.text(x,y+i*gap,v,opt));return rows.length*gap;
 }
 rect(l,x,y,w,h,fill,stroke='',sw=1){this.add(l,rect(x,y,w,h,fill,stroke,sw));}
 line(l,x,y,xx,yy,c=p.line,sw=1,dash=''){this.add(l,'<line x1="'+x+'" y1="'+y+'" x2="'+xx+'" y2="'+yy+'" stroke="'+c+'" stroke-width="'+sw+'"'+(dash?' stroke-dasharray="'+dash+'"':'')+'/>');}
 image(l,file,x,y,w,h,fit='xMidYMid meet',opacity=1){
  this.add(l,'<image x="'+x+'" y="'+y+'" width="'+w+'" height="'+h+'" opacity="'+opacity+'" preserveAspectRatio="'+fit+'" href="@ASSET@/'+e(file)+'"/>');
 }
 backdrop(key){
  this.rect(0,0,0,W,H,p.black);
  this.image(0,sceneFiles[key]||sceneFiles.camp,0,0,W,H,'xMidYMid slice');
  this.rect(0,0,0,W,H,'url(#roomShade)');
 }
 art(key,x,y,w,h,opt={}){
  this.mark(opt.portrait?'portrait':'art',key,x,y,w,h);
  const isPortrait=opt.portrait&&(key==='actor:p0'?'portrait-p0-design.webp':artManifest.portraits[key]);
  if(isPortrait){this.artKeys.add(key);this.image(2,isPortrait,x,y,w,h,opt.anchor||'xMidYMid meet');return;}
  const a=artManifest.sprites[key];
  if(!a)throw Error('Unknown visual ID '+key+' '+this.page[0]);
  this.artKeys.add(key);
  const [l,t,cw,ch]=a.crop;
  this.add(2,'<svg data-art-id="'+e(key)+'" x="'+x+'" y="'+y+'" width="'+w+'" height="'+h+'" viewBox="'+[l,t,cw,ch].join(' ')+'" preserveAspectRatio="'+(opt.anchor||'xMidYMid meet')+'" opacity="'+(opt.opacity??1)+'" overflow="hidden"><image width="'+a.size[0]+'" height="'+a.size[1]+'" preserveAspectRatio="none" href="@ASSET@/'+e(a.file)+'"/></svg>');
 }
 frame(x,y,w,h,{ink=p.iron,corner=10,line=p.line}={}){
  const thick=h>=280?8:h>=75?6:4,cut=Math.min(15,corner+3,Math.floor(h/5));
  const bevel=(a,b,c,d,r)=>'M'+(a+r)+' '+b+' H'+(a+c-r)+' l'+r+' '+r+' V'+(b+d-r)+' l-'+r+' '+r+' H'+(a+r)+' l-'+r+' -'+r+' V'+(b+r)+' Z';
  this.add(3,path(bevel(x+1,y+1,w-2,h-2,cut),'none','#070605',thick+2));
  this.add(3,path(bevel(x+2,y+2,w-4,h-4,cut),'none',line,thick-1));
  this.add(3,path(bevel(x+thick,y+thick,w-thick*2,h-thick*2,Math.max(2,cut-thick)),'none','#aa7a4d70',1.4));
  this.line(3,x+cut+4,y+3,x+w-cut-4,y+3,'#cd925b66',1.5);
  const xs=[x+5,x+w-5],ys=[y+5,y+h-5];
  for(const [i,a] of xs.entries())for(const [j,b] of ys.entries()){
   const sx=i?-1:1,sy=j?-1:1;
   this.add(3,path('M'+a+' '+(b+sy*cut)+' l'+sx*cut+' '+(-sy*cut)+' h'+sx*6,'none',ink,2.6));
   this.add(3,path('M'+(a+sx*9)+' '+(b+sy*7)+' l'+sx*3+' '+sy*3+' l'+(-sx*3)+' '+sy*3+' l'+(-sx*3)+' '+(-sy*3)+'z',ink));
  }
 }
 panel(x,y,w,h,title='',tag=''){
  this.mark('panel',title,x,y,w,h);
  this.rect(1,x,y,w,h,'url(#panelTone)');
  this.rect(1,x,y,w,h,'url(#scuffs)');
  this.frame(x,y,w,h);
  if(title){
   this.rect(1,x+5,y+5,w-10,41,'#272019');
   this.text(x+16,y+32,title,{size:21});
   this.line(3,x+7,y+46,x+w-7,y+46,p.line);
   if(tag)this.text(x+w-17,y+31,tag,{size:15,align:'end',color:p.gold});
  }
 }
 button(x,y,w,s,style='normal',glyph=''){
  this.mark('control',s,x,y,w,40);
  const c=style==='primary'?['#703829','#c18458',p.light]:style==='muted'?['#25201a','#4c3e31','#a59782']:['#2e251c','#785a3e',p.light];
  this.rect(1,x,y,w,40,c[0]);
  this.add(4,path('M'+(x+6)+' '+(y+1)+' H'+(x+w-6)+' l5 5 V'+(y+34)+' l-5 5 H'+(x+6)+' l-5 -5 V'+(y+6)+'z','none','#0b0806',4));
  this.rect(4,x+5,y+5,w-10,30,'none',c[1]);
  this.line(4,x+10,y+3,x+w-10,y+3,style==='primary'?'#d19c69':'#725333',1.6);
  if(glyph)this.art('glyph:'+glyph,x+11,y+9,18,18);
  this.text(x+w/2+(glyph?8:0),y+25,s,{size:16,color:c[2],align:'middle'});
 }
 badge(x,y,s,tone='gold'){
  const w=Math.max(40,Math.ceil([...s].reduce((n,c)=>n+(/[\x00-\x7F]/.test(c)?8:15),16)));
  const danger=tone==='red', calm=tone==='sage';
  this.rect(5,x,y,w,23,danger?'#3e211c':calm?'#243126':'#32271c',danger?'#a45140':calm?'#5a775c':'#886845');
  this.text(x+w/2,y+16,s,{size:14,color:danger?p.danger:calm?p.sage:p.gold,align:'middle'});
  return w;
 }
 meter(x,y,w,value,tone='gold',h=6){
  this.rect(5,x,y,w,h,'#0e0c0a','#564537');
  this.rect(5,x+1,y+1,Math.round((w-2)*value),h-2,tone==='red'?'#ac5744':tone==='sage'?'#78966d':'#ba8a50');
 }
 rows(x,y,w,rows,gap=45){
  rows.forEach(([a,b,t],i)=>{
   const pos=y+i*gap;this.line(3,x,pos+11,x+w,pos+11,'#47382a');
   this.text(x+2,pos,a,{size:17,color:p.muted});
   this.text(x+w-2,pos,b,{size:17,align:'end',color:t==='red'?p.danger:t==='sage'?p.sage:p.light});
  });
 }
 tile(x,y,w,h,key,opt={}){
  this.mark('card',opt.title||key||'空位',x,y,w,h);
  const fill=opt.paper?'url(#cardPaper)':'url(#tileTone)';
  this.rect(1,x,y,w,h,fill);
  const selected=opt.selected, locked=opt.locked;
  this.frame(x,y,w,h,{ink:selected?p.gold:p.iron,corner:7});
  if(selected)this.rect(5,x+3,y+3,w-6,h-6,'none','#ce925b',2);
  const bar=48;
  this.rect(1,x+4,y+h-bar,w-8,bar-4,opt.paper?'#41311f':'#251c14');
  this.line(3,x+7,y+h-bar,x+w-7,y+h-bar,'#725534');
  const cap=opt.cap||95,iw=Math.min(w-24,cap),ih=Math.min(h-bar-12,cap);
  if(key)this.art(key,x+(w-iw)/2,y+7+(h-bar-12-ih)/2,iw,ih,{opacity:locked?.43:1});
  if(locked)this.rect(5,x+5,y+5,w-10,h-bar-6,'url(#hatch)');
  const title=opt.title|| (key?nameOf(key):'空位');
  this.text(x+w/2,y+h-bar+20,title,{size:Math.min(19,Math.floor((w-12)/Math.max(title.length,1))),align:'middle'});
  if(opt.sub)this.text(x+w/2,y+h-10,opt.sub,{size:14,color:opt.danger?p.danger:p.muted,align:'middle'});
 }
 artCardRow(x,y,w,h,keys,opts={}){
  const gap=opts.gap||8,tw=(w-(keys.length-1)*gap)/keys.length;
  keys.forEach((key,i)=>this.tile(x+i*(tw+gap),y,tw,h,key,{...opts, ...(opts.each?.[i]||{})}));
 }
 pageHead(loc='临时营地',active=''){
  this.rect(1,0,0,1600,96,'#1a1511');
  this.rect(1,0,0,1600,96,'url(#scuffs)');
  this.line(3,0,94,1600,94,'#8a6444',2);
  this.text(32,39,'适者生存',{size:28,color:p.gold,spacing:3});
  this.text(190,38,'第 3 日  /  '+loc,{size:18,color:p.muted});
  this.text(1568,38,'重墨 V2    '+this.page[0],{size:16,align:'end',color:p.muted,spacing:2});
  const nav=[['区域地图','map'],['手工制作','craft'],['休息','rest'],['行动日志','log'],['编队','formation'],['整备','equipment'],['成长','growth'],['生存目标','goal'],['生存提示','warning'],['对话记录','dialogue'],['系统说明','help'],['菜单','menu']];
  nav.forEach(([s,g],i)=>this.button(32+129*i,52,119,s,active===s?'primary':'normal',g));
 }
 footer(msg='浏览与整理不推进时间',state='● 已保存'){
  this.rect(1,0,862,1600,38,'#1b1510');this.line(3,0,862,1600,862,p.iron);
  this.text(30,887,msg,{size:16,color:p.muted});
  const warning=state.includes('失败');
  this.rect(5,1559,874,9,9,warning?p.red:p.sage);
  this.text(1548,887,state.replace('● ',''),{size:16,align:'end',color:warning?p.danger:p.sage});
 }
 standard(scene='camp',active=''){
  const labels={woods:'林地',field:'农野荒地',stream:'溪流',hub:'幸存者营区',church:'教会圣所',lab:'科学院封存区',ruin:'地下圣所'};
  this.backdrop(scene);this.pageHead(labels[scene]||'临时营地',active);this.footer();
 }
 maskUnder(x,y,w,h){
  const id='overlay'+(++this.n);
  this.defs.push('<clipPath id="'+id+'"><path fill-rule="evenodd" clip-rule="evenodd" d="M0 0H1600V900H0Z M'+x+' '+y+'h'+w+'v'+h+'h-'+w+'z"/></clipPath>');
  for(let i=1;i<this.L.length;i++)this.L[i]=['<g clip-path="url(#'+id+')">'+this.L[i].join('')+'</g>'];
  this.rect(1,x-5,y-5,w+10,h+10,'#080605b0');
 }
 svg(){
  const defs='<defs><style>@font-face{font-family:Ink16;src:url("@ASSET@/chill-bitmap-16.woff2")} text{font-family:Ink16,"Microsoft YaHei",sans-serif;font-synthesis:none} .clear text{font-family:"Microsoft YaHei","Noto Sans CJK SC",sans-serif}</style>'+
   '<linearGradient id="roomShade" x2="0" y2="1"><stop stop-color="#15100bc0"/><stop offset=".50" stop-color="#130e0a9c"/><stop offset="1" stop-color="#0a0807ee"/></linearGradient>'+
   '<linearGradient id="panelTone" x2="0" y2="1"><stop stop-color="#241d17"/><stop offset="1" stop-color="#191511"/></linearGradient>'+
   '<linearGradient id="tileTone" x2="0" y2="1"><stop stop-color="#3a2c1f"/><stop offset=".85" stop-color="#201911"/></linearGradient>'+
   '<linearGradient id="cardPaper" x2="0" y2="1"><stop stop-color="#b19a73"/><stop offset="1" stop-color="#655137"/></linearGradient>'+
   '<pattern id="scuffs" width="131" height="77" patternUnits="userSpaceOnUse"><path d="M7 8h14m70 6h8M23 35h6m51 32h12m-29-21h19m30-7h8m-110 30h8" stroke="#9c7552" opacity=".14"/><path d="M4 70h11m42-54h8m54 37h10M35 59h5" stroke="#050403" opacity=".6"/></pattern>'+
   '<pattern id="hatch" width="8" height="8" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><rect width="2" height="8" fill="#17130f" opacity=".40"/></pattern>'+
   this.defs.join('')+'</defs>';
  return '<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="900" viewBox="0 0 1600 900" role="img" aria-label="'+e(this.page[1])+'"><title>'+e(this.page[1])+'</title>'+defs+this.L.map((s,i)=>'<g id="layer-'+(i+1)+'" data-layer="'+(i+1)+'" aria-label="'+layers[i]+'">'+s.join('')+'</g>').join('')+'</svg>';
 }
}
const worldInfo={
 营地:['camp','营地火堆','facility:fire_lit','可投入木材','加工位 1  金属锅','加工位 2  空位'],
 资源:['woods','林地木材','resource:woods:wood','限量采集 · 每日补充','工具要求  可徒手','来源  当前地块'],
 敌踪:['field','游荡尸的敌踪','enemy:shambler','敌踪未处理','接敌前查看威胁','绕行  10 分钟'],
 转移:['camp','净水','item:clean_water','随身实例 ×2','目标  当前地面','重量  0.50 公斤 / 份'],
 岗位:['hub','许安 · 公共事务','actor:npc_xu_an','当前在岗','可查看事项和委托','交谈不耗时'],
 状态:['camp','当前身体状态','status:status_infected','病毒 · 当前未感染','属性可以逐项查看','状态原因由当前数据提供'],
 火堆:['camp','燃烧中的火堆','facility:fire_lit','燃料  18 分钟','加工位 1  烧水','加工位 2  烤制'],
 锅:['camp','金属锅 · 处理进度','item:pot','净水  500 / 1000 毫升','食材  生块根 ×1','食材槽  1 / 2'],
 保存失败:['camp','存档写入失败','glyph:save','浏览器存储空间已满','重试沿用当前存档流程',''],
 行动推进:['woods','林地木材','resource:woods:wood','对象详情','地点  林地','来源  可采集的木材']
};
function hud(b){
 b.panel(24,110,192,736,'生存状况');
 b.text(43,185,'第 3 日 · 白昼',{size:16,color:p.muted});
 b.text(42,223,'16:10',{size:38,color:p.gold});
 b.line(3,40,240,199,240,p.iron);
 const stats=[['健康','health','18 / 20',.90],['饱食','satiety','58 / 100',.58],['水分','hydration','65 / 100',.65],['体力','energy','12 / 20',.6],['清醒','wakefulness','89 / 100',.89],['精神','morale','64 / 100',.64],['免疫','immunity','70 / 100',.7],['病毒','virus','未感染',.02]];
 stats.forEach(([s,g,v,n],i)=>{
  const y=274+55*i;b.art('glyph:'+g,42,y-20,20,20);b.text(69,y-3,s,{size:17});b.text(199,y-3,v,{size:14,color:p.muted,align:'end'});b.meter(43,y+5,153,n,i===0?'red':i===2?'sage':'gold');
 });
 b.line(3,41,720,198,720,p.line);
 b.art('glyph:storage',43,737,26,26);b.text(78,756,'携重  5.2 公斤',{size:17});b.text(44,795,'随身根卡  5 / 5',{size:17,color:p.gold});b.text(44,824,'轻装',{size:16,color:p.muted});
}
function area(b,x,y,w,h,key,name,label){
 b.rect(1,x,y,w,h,'#161310');b.image(0,sceneFiles[key],x,y,w,h,'xMidYMid slice');
 // Scene images are painted under transparent location panes so they stay independent.
 b.L[1].pop();b.rect(1,x,y+h-43,w,43,'#15110fe8');b.rect(1,x,y,w,h-43,'#0e0b0852');
 b.frame(x,y,w,h);b.text(x+14,y+h-15,name,{size:23});b.text(x+w-13,y+h-16,label,{size:15,color:p.gold,align:'end'});
}
function world(b,v='营地'){
 const [scene,title,key,tag,a,c]=worldInfo[v]||worldInfo.营地;
 b.standard(scene);hud(b);
 if(v==='保存失败')b.footer('存档失败：浏览器存储空间已满。','存档失败  ·  重试');
 const tx=230, tw=1070, rw=176, gap=9;
 for(const [i,n] of ['地图与场景','设施与资源','当前地面','随身物品'].entries()){
  const y=110+i*(rw+gap);b.rect(1,tx,y,tw,rw,'#1a1511dd');b.frame(tx,y,tw,rw);
  b.rect(1,tx+5,y+5,42,rw-10,'#3a2b1c');
  [...n].forEach((ch,j)=>b.text(tx+25,y+50+j*19,ch,{size:16,align:'middle',color:p.gold}));
 }
 const mapY=120;
 b.rect(1,tx+55,mapY,tw-65,156,'#161310');b.image(2,sceneFiles[scene],tx+56,mapY+1,520,154,'xMidYMid slice');
 b.rect(3,tx+56,mapY+110,520,44,'#14100ce8');
 b.text(tx+71,mapY+139,scene==='camp'?'临时营地':scene==='hub'?'营区广场':scene==='field'?'农野荒地':'林地',{size:25});
 b.text(tx+561,mapY+137,'当前地点',{size:16,align:'end',color:p.gold});
 [['field','农野荒地'],['stream','溪流']].forEach(([k,n],i)=>{
  const x=tx+587+i*217;b.image(2,sceneFiles[k],x,mapY+1,207,154,'xMidYMid slice',.72);
  b.rect(3,x,mapY+110,207,44,'#14100cde');b.text(x+12,mapY+138,n,{size:20});
 });
 let fac=['facility:fire_lit','facility:shelter','facility:bed','facility:safehouse','facility:workbench'];
 if(v==='资源')fac=['resource:woods:wood','resource:woods:berries','resource:woods:stones','resource:woods:woods_fiber','facility:camping'];
 if(v==='敌踪')fac=['enemy:shambler','enemy:z_helmet','enemy:feral_dog','facility:shelter',null];
 if(v==='岗位')fac=['actor:npc_xu_an','actor:npc_lin_zhao','facility:workbench','facility:sewing','glyph:knowledge'];
 if(v==='火堆')fac=['facility:fire_lit','item:pot','item:raw_root','facility:charcoal','facility:shelter'];
 if(v==='锅')fac=['facility:fire_lit','item:pot','item:clean_water','item:raw_root','facility:bed'];
 const facNames=v==='岗位'?{4:'营区告示'}:v==='资源'?{4:'野营遮雨布'}:{};
 b.artCardRow(tx+56,305,tw-67,155,fac,{cap:80,each:fac.map((_,i)=>({title:facNames[i],sub:i===0?tag:i===1&&v==='火堆'?'加工位 1':i===2&&v==='火堆'?'加工位 2':i===4&&v==='敌踪'?'':'查看',selected:i===0&&['资源','敌踪','火堆','岗位','锅'].includes(v)}))});
 b.artCardRow(tx+56,490,tw-67,155,ground,{paper:true,cap:100,each:ground.map((_,i)=>({sub:'×'+[3,2,2,1,1][i]}))});
 b.artCardRow(tx+56,675,tw-67,155,cup,{paper:true,cap:106,each:cup.map((_,i)=>({sub:['×2','×1','净水 500 mL · 食材 1/2','耐久 28 / 40','×1'][i],selected:v==='转移'&&i===0}))});
 b.panel(1314,110,262,736,'对象详情');
 b.button(1330,166,109,'对象信息','primary');b.button(1445,166,115,'人物');
 b.text(1333,239,title,{size:22});
 b.text(1333,267,tag,{size:16,color:p.gold});
 b.art(key,1354,289,180,160,{portrait:key.startsWith('actor:')});
 b.line(3,1332,464,1558,464,p.iron);
 b.para(1333,503,a,223,{size:17});b.para(1333,549,c,223,{size:17});
 if(v==='敌踪'){b.button(1330,625,229,'接敌','primary','battle');b.button(1330,671,229,'绕行 10 分钟','normal','route');}
 else if(v==='资源')b.button(1330,628,229,'查看采集','primary','search');
 else if(v==='转移'){b.button(1330,628,229,'选择转移数量','primary','transfer');b.badge(1330,683,'合法落点示例','sage');}
 else if(v==='保存失败')b.button(1330,628,229,'重试','primary','save');
 else if(v==='行动推进')b.badge(1330,626,'行动进度见中央浮窗','sage');
 else if(v==='岗位'){b.button(1330,628,229,'交谈','primary','dialogue');b.button(1330,674,229,'事项','normal','service');}
 else b.badge(1330,626,'查看与浏览不耗时','sage');
 if(v==='行动推进')centralProgress(b);
}
function centralProgress(b){
 const x=512,y=310,w=576,h=281;
 b.maskUnder(x,y,w,h);b.panel(x,y,w,h,'搜集林地木材','正在推进');
 b.art('glyph:progress',x+23,y+63,46,45);
 b.text(x+85,y+86,'已推进 15 / 30 分钟',{size:23});
 b.text(x+w-26,y+86,'剩余 15 分钟',{size:17,align:'end',color:p.muted});
 for(let i=0;i<6;i++)b.rect(5,x+25+88*i,y+125,82,13,i<3?'#bd8650':'#2e231b','#7a5030');
 b.text(x+25,y+180,'当前批次已预留 · 退出按已完成阶段结算',{size:17,color:p.muted});
 b.button(x+25,y+218,222,'退出行动','normal','return');
 b.badge(x+w-166,y+223,'行动进行中','sage');
}
const gearKeys=[['武器一','item:hatchet'],['武器二','item:crossbow'],['防具','item:leather_armor'],['战斗饰品','item:combat_badge'],['上衣','item:work_jacket'],['下衣','item:work_pants'],['求生饰品','item:ration_pouch']];
function validGear(){
 return ['weapon1','weapon2','armor','combat_acc','top','bottom','survival_acc'].map((slot,i)=>{
  const [label,k]=gearKeys[i];
  if(artManifest.sprites[k])return [label,k];
  const row=config.items.find(r=>r.equipSlot===slot);
  return [label,row?'item:'+row.id:'item:cloth'];
 });
}
function gear(b,v){
 b.standard('camp','整备');b.panel(26,112,324,730,'队员与位置','同行 2 / 4');
 for(const [i,k] of ['actor:p0','actor:npc_lin_zhao','actor:npc_zhou_yan'].entries()){
  const y=176+i*96;b.rect(1,42,y,291,83,'#31251a');b.frame(42,y,291,83);
  b.art(k,52,y+4,72,74,{portrait:true});b.text(135,y+35,nameOf(k),{size:23});b.text(135,y+63,i===0?'当前角色':i===2?'留守候补':'当前同行',{size:15,color:p.muted});
 }
 b.rows(46,502,282,[['位置','临时营地'],['行动','浏览与整备'],['状态','可查看'],['持有','随身 5 / 5']],51);
 b.panel(367,112,682,730,'人物立绘与七槽',v);
 const g=validGear();
 for(const [i,[label,key]] of g.entries()){
  const x=i<4?389:876,y=173+(i<4?i:i-4)*127;
  b.tile(x,y,151,115,key,{sub:label,selected:v==='比较'&&i===2,cap:60});
 }
 b.rect(1,552,175,311,501,'#211911');b.frame(552,175,311,501);
 b.art('actor:p0',575,205,265,419,{anchor:'xMidYMax meet'});
 b.text(708,654,'当前角色  /  你',{size:22,align:'middle',color:p.gold});
 b.rows(396,733,628,[['随身','5 / 5 根卡'],['负重','5.2 公斤']],38);
 b.panel(1065,112,510,730,v==='比较'?'同槽比较':v==='交接'?'交接预演':'来源与候选','当前地块');
 if(v==='比较'){
  b.artCardRow(1086,175,466,170,[g[2][1],'item:padded_vest'],{each:[{sub:'当前'},{sub:'候选',selected:true}]});
  b.rows(1088,397,459,[['当前槽位','防具'],['耐久','18 / 24 → 24 / 24'],['旧物去向','本人随身'],['总重量','显示本次预演']],54);
  b.button(1090,709,226,'确认更换','primary','equipment');
 }else if(v==='交接'){
  b.artCardRow(1085,176,467,165,['item:pot','item:clean_water'],{each:[{sub:'锅只显示根卡',selected:true},{sub:'同组可交接'}]});
  b.rows(1089,393,455,[['来源','本人随身'],['接收人','林昭'],['目标容量','预演可接收'],['容器内容','随根卡交接']],54);
  b.button(1089,709,225,'确认交接','primary','transfer');
 }else{
  b.artCardRow(1085,174,469,160,cup.slice(0,3),{paper:true});b.artCardRow(1085,350,469,160,[g[0][1],g[2][1],g[4][1]]);
  b.rows(1087,558,461,[['候选来源','随身 / 当前地面'],['当前类别','全部'],['容量预演','点击候选后查看']],45);
  b.button(1089,709,225,'查看同槽比较','primary','equipment');
 }
 b.button(1324,709,225,'返回');
}
const battleAllies=['actor:npc_zhou_yan','actor:npc_shen_he','actor:npc_lin_zhao','actor:p0'];
const foes=['enemy:shambler','enemy:z_helmet','enemy:z_spitter','enemy:z_hauler'];
function battle(b,v){
 b.backdrop('battle');b.rect(1,20,16,1560,106,'#191511e9');b.frame(20,16,1560,106);
 b.art('glyph:battle',38,27,27,27);b.text(75,49,'战斗  /  检查哨',{size:28,color:p.gold});
 b.text(380,47,'第 2 回合    当前行动  你    AP 3 / 3',{size:18});b.button(1392,25,165,'菜单','normal','menu');
 const ally=v==='兽战'?[null,null,'actor:npc_lin_zhao','actor:p0']:battleAllies;
 const enemy=v==='兽战'?['enemy:feral_dog','enemy:boar',null,'enemy:crawler']:foes;
 const queue=[ally[3],enemy[0],ally[2],enemy[1],ally[1],enemy[2],ally[0],enemy[3]].filter(Boolean);
 queue.forEach((k,i)=>{const x=45+i*158;b.rect(1,x,66,148,42,'#2a2018');b.frame(x,66,148,42,{corner:5});b.art(k,x+5,70,32,32,{portrait:k.startsWith('actor:')});b.text(x+43,92,nameOf(k),{size:15,color:i===0?p.gold:p.light});});
 const y=137;
 b.rect(1,20,y,1560,402,'#23181033');b.frame(20,y,1560,402);
 const feet=466;
 b.line(5,42,feet,1558,feet,'#b3976e63',1,'7 7');
 b.text(37,165,'我方  /  4 → 1',{size:18,color:p.gold});b.text(1562,165,'敌方  /  1 → 4',{size:18,color:p.danger,align:'end'});
 b.add(3,path('M790 335l10-14 10 14-10 14z','#1e1410','#82523a',2));
 const centers=[113,282,451,620,980,1149,1318,1487];
 [...ally,...enemy].forEach((k,i)=>{
  const cx=centers[i],pos=i<4?4-i:i-3;
  const attackSelected=i===4&&k;
  if(k){
   const isAnimal=/feral_dog|boar/.test(k),isCrawler=k==='enemy:crawler';
   const [,,cw,ch]=artManifest.sprites[k].crop;
   const targetHeight=isCrawler?75:isAnimal?105:244;
   const h=Math.min(targetHeight,Math.floor(168*ch/cw));
   b.add(2,'<ellipse cx="'+cx+'" cy="'+(feet+1)+'" rx="'+(isAnimal?52:38)+'" ry="9" fill="#090706c7"/>');
   b.art(k,cx-84,feet-h,168,h,{anchor:'xMidYMax meet'});
   const by=feet-h-41;
   b.rect(1,cx-76,by,152,30,'#18100fed');b.frame(cx-76,by,152,30,{ink:attackSelected?'#d78660':p.iron,corner:4});
   b.text(cx,by+21,pos+' · '+nameOf(k),{size:16,align:'middle',color:i<4?p.gold:p.danger});
   if(attackSelected)b.add(5,path('M'+(cx-5)+' '+(by+35)+'h10l-5 6z','#dc9163'));
   b.rect(1,cx-75,481,150,38,'#1d1510f0');b.frame(cx-75,481,150,38,{ink:attackSelected?p.red:p.iron,corner:5});
   b.art('glyph:health',cx-66,489,16,16);b.text(cx-46,503,i<4?'16/20':'7/12',{size:14});
   if(i<4){b.art('glyph:fear',cx+6,489,16,16);b.text(cx+27,503,'20',{size:14});}
   b.meter(cx-62,510,124,i<4?.8:.58,i<4?'sage':'red',5);
  }else{
   b.rect(5,cx-56,237,112,229,'none','#795e4370');b.text(cx,344,pos+' · 空位',{size:17,align:'middle',color:p.dim});
  }
 });
 b.panel(20,552,235,296,'当前行动者');
 b.art('actor:p0',36,609,76,79,{portrait:true});b.text(123,646,'你',{size:22});b.badge(120,660,'AP 3 / 3');
 b.rows(40,725,197,[['体力','12 / 20'],['恐慌','20'],['当前武器','徒手']],36);
 b.panel(267,552,776,296,'武器页与技能','武器 I');
 b.button(285,605,158,'武器 I · 拳','primary');b.button(450,605,158,'武器 II · 空槽','muted');
 const nodes=config.skillNodes.filter(n=>n.skillId==='unarmed').sort((a,c)=>a.level-c.level);
 b.artCardRow(285,657,740,142,nodes.map(n=>'skill:'+n.id),{cap:45,each:nodes.map((n,i)=>({sub:i===0?'2 AP · 可用':'达到 '+n.level+' 级',locked:i>0,selected:i===0&&v!=='锁级'}))});
 b.text(287,830,v==='锁级'?'锁级技能可阅读说明；当前行动点不足时保留原因。':'目标位  1   2   3   4  /  先选技能，再选择亮起的目标。',{size:16,color:p.muted});
 b.panel(1056,552,524,296,'通用与回合');
 const actions=[['guard','1 AP'],['breathe','2 AP'],['swap','1 AP'],['retreat','2 AP'],['bandage','物资']];
 b.artCardRow(1070,607,495,126,actions.map(a=>'action:'+a[0]),{cap:42,each:actions.map(a=>({sub:a[1]}))});
 b.button(1070,765,158,'物资查看','normal','storage');b.button(1235,765,158,'查看装备','normal','equipment');b.button(1400,765,166,'结束行动','primary','continue');
 b.footer('战斗回合按当前目标与指令预演');
 if(v==='战斗对白'){b.maskUnder(323,181,955,351);b.panel(323,181,955,351,'战斗对白','对话中 · 世界时间不推进');speech(b,758,246,471,'饱饱','等它露出破绽再动。','right');speech(b,365,347,446,'你','我先看清离我最近的那一个。','left');b.button(1040,475,190,'继续对白','primary','continue');}
 else if(['物资','装备','医疗','战斗菜单','战报'].includes(v))battlePanel(b,v);
}
function battlePanel(b,v){
 const x=380,y=207,w=840,h=390;b.maskUnder(x,y,w,h);b.panel(x,y,w,h,v==='物资'?'战斗物资查看':v==='装备'?'装备查看':v==='医疗'?'医疗与目标':v==='战报'?'战斗记录':v==='战斗对白'?'战斗中的对白':'战斗菜单','战斗上下文');
 if(v==='物资'||v==='装备'){
  const k=v==='物资'?cup:validGear().slice(0,5).map(r=>r[1]);
  b.text(x+23,y+83,'战斗中只读 · 卡片没有使用与转移入口',{size:18,color:p.muted});
  b.artCardRow(x+22,y+103,w-44,181,k,{paper:true,each:k.map(()=>({sub:'只读'}))});
 }else if(v==='医疗'){
  b.artCardRow(x+22,y+76,w-44,159,['item:bandage','item:antibiotics','item:antiviral_cure'],{each:[{sub:'包扎 · 1 AP',selected:true},{sub:'需满足适用条件'},{sub:'随身无库存',locked:true}]});
  b.text(x+24,y+273,'选择合法目标，核对本次消耗与可用原因。',{size:19});
 }else if(v==='战报'){
  b.rows(x+26,y+94,w-52,[['第 2 回合','你获得 3 AP'],['游荡尸','展示已观察到的意图'],['命中与状态','实际结算后记录'],['队友','按行动顺序追加']],51);
 }else if(v==='战斗对白'){
  b.art('actor:baobao',x+28,y+92,126,137,{portrait:true});b.text(x+180,y+105,'饱饱',{size:22,color:p.gold});b.para(x+180,y+154,'等它露出破绽再动。先看清身边那一只。',w-230,{size:22,line:37});b.badge(x+180,y+251,'对话中 · 时间未推进');
 }else{
  b.rows(x+24,y+95,w-48,[['动画速度','标准'],['阅读字体','细像素'],['系统说明','可查看'],['存档','可保存并返回标题']],48);
 }
 b.button(x+w-223,y+h-63,199,v==='战斗对白'?'继续对白':'返回战斗','primary',v==='战斗对白'?'continue':'return');
}
const modalData={
 点火:['选择点火工具','点火需要一把当前可用的打火器。先确认耐久、火堆和本次耗时。',['item:lighter','facility:fire_out'],[['火堆','当前未点燃'],['打火器','耐久 28 / 40'],['耗时','按所选工具确认']],'选择打火器'],
 投炭:['煅烧木炭 · 确认投料','按当前火堆的工位选择投料。燃料和木材来源各自单列。',['item:wood','facility:charcoal','item:charcoal'],[['投入','木材 ×10'],['工位','木炭加工'],['燃料','另计；原火堆状态保留']],'确认投料'],
 重复武器:['同类武器冲突','武器类型重复。选择一件保留，确认移出装备页的那件去向。',['item:hatchet','item:fire_axe'],[['当前装备','保留在原武器页'],['候选装备','检查类型冲突'],['处理去向','显示随身或地面']],'确认处理'],
 菜单:['游戏菜单','浏览设置时保留本局画面。动画速度与阅读字体使用分开的选项。',['glyph:menu','glyph:save','glyph:return'],[['阅读字体','细像素 / 清晰阅读'],['动画速度','快 / 标准 / 慢'],['存档','本浏览器单槽，自动保存']],'继续游戏'],
 撤出:['终止本次远征','确认撤出后按实际路线与风险执行。已确认战利品与途中可能发生的事分别展示。',['glyph:route','glyph:dungeon','glyph:warning'],[['位置','地下圣所 · 外环'],['路线','按现有撤出路径'],['风险','途中仍可能遇敌']],'确认撤出'],
 暂停:['行动暂停','剩余进度与已经预留的材料继续保留。可用补给按当前状态展示。',['item:clean_water','item:canned_food','item:lighter'],[['当前行动','搜集营地周边'],['进度','15 / 30 分钟'],['可用操作','继续 / 退出 / 补给']],'继续行动'],
 超额:['随身物品超额','选择处理超额物品。数量、归属和地面去向分开显示。',['item:wood','item:fiber','item:pot'],[['随身根卡','已满'],['待处理','选择物品与份数'],['处理后','重新核对容量']],'确认放下'],
 数量:['转移份数','选择本批次要转移的数量。单位、来源和目的地均可直接辨认。',['item:clean_water'],[['当前批次','净水 ×4'],['目的地','本人随身'],['选择数量','−     2     +     最大']],'转移 2 份'],
 封存:['远征战利品封存','确认同一实例的战利品与可带回物品。留存条目按结算去向分组。',['glyph:storage','item:cloth','item:clean_water'],[['封存范围','本次远征'],['待处理条目','已领取物资'],['结算','保留具体去向和原因']],'确认封存'],
 重置:['地牢实例重置','预览将变化的实例和代际记录。重置前确认当前是否允许执行。',['glyph:dungeon','glyph:warning','glyph:history'],[['实例','地下圣所 · 第 2 代'],['开放条件','以当前进度为准'],['注意','本代记录将结束']],'确认重置'],
 反馈:['反馈与留言','填写遇到的问题或想法。界面区域展示可预览的本局上下文与截图附件。',['glyph:dialogue','glyph:history'],[['说明','请描述发生了什么'],['上下文','当前界面与本局状态'],['附件','可粘贴截图']],'提交反馈'],
};
function modal(b,v){
 world(b,v==='点火'||v==='投炭'?'火堆':'营地');
 const [title,description,keys,rows,action]=modalData[v];
 const x=420,y=187,w=760,h=547;b.maskUnder(x,y,w,h);b.panel(x,y,w,h,title,'待确认');
 b.para(x+28,y+86,description,w-56,{size:18,line:29});
 b.artCardRow(x+27,y+139,w-54,147,keys,{cap:95,each:keys.map((k,i)=>({title:k.startsWith('glyph:')?['当前操作','相关信息','风险提示'][i]:undefined}))});
 b.rows(x+29,y+326,w-58,rows,48);
 b.button(x+31,y+h-65,190,'取消');b.button(x+w-265,y+h-65,238,action,'primary','continue');
}
function slots(b){
 b.standard('camp','手工制作');b.panel(24,110,1041,735,'晾制架','8 工位');
 const k=['item:raw_meat','item:dried_meat','item:berry','item:dried_berries',null,null,null,null];
 for(let i=0;i<8;i++)b.tile(47+(i%4)*248,179+Math.floor(i/4)*249,232,231,k[i],{sub:['进行中','可收取','排队','可收取','可分配','可分配','可分配','可分配'][i],selected:i===0,cap:125,title:k[i]?undefined:'空工位'});
 b.panel(1080,110,496,735,'工位 1 · 生肉');
 b.art('item:raw_meat',1224,178,202,171);
 b.rows(1105,401,440,[['配方','肉干'],['批次进度','35 / 60 分钟'],['原料','生肉与盐'],['取出时机','完成后收取']],56);
 b.meter(1110,643,435,.58);b.button(1110,703,196,'查看配方','normal','recipe');
}
function house(b){
 b.standard('camp');b.panel(25,112,991,731,'安全屋 · 建造与出入','当前位置');
 b.image(2,sceneFiles.camp,45,171,951,414,'xMidYMid slice');b.frame(45,171,951,414);
 b.rect(3,49,531,943,49,'#19120edb');b.text(65,563,'营外安全屋地块',{size:24});
 b.artCardRow(45,605,951,178,['facility:safehouse','facility:bed','facility:fire_lit','facility:storage'],{cap:85,each:[{sub:'已建造'},{sub:'屋内休息'},{sub:'火源'},{sub:'可用储物'}]});
 b.panel(1032,112,542,731,'房屋状态与可用入口');
 b.rows(1059,210,480,[['当前区域','屋外地块'],['切换区域','屋外 / 屋内'],['休息条件','到达可用床位后查看'],['返回路线','显示已知安全路线']],60);
 b.para(1062,507,'屋内与屋外使用各自的地面物品、火堆和设施。先选择入口，再核对目标区域。',475,{size:20});
 b.button(1063,715,227,'进入屋内','primary','continue');b.button(1300,715,230,'返回当前地点');
}
function feed(b,v){
 b.standard('hub');b.panel(25,113,381,732,'队伍随身与锅内容','林昭');
 b.art('actor:npc_lin_zhao',48,176,84,95,{portrait:true});b.text(148,223,'林昭',{size:25});b.text(148,253,'同行 · 随身 3 / 5',{size:16,color:p.gold});
 b.rows(50,321,323,[['罐头 ×2','可食用'],['金属锅','净水 1000 mL'],['  └ 锅内净水','可饮用'],['  └ 野果','按批次检查'],['腐败野果','不可用','red']],55);
 const drink=v==='饮用',blocked=v==='不可用',choice=drink?'item:clean_water':'item:canned_food';
 b.panel(422,113,766,732,drink?'给同行者饮用':'给同行者食用','查看来源与效果');
 b.artCardRow(443,178,722,181,[choice,drink?'item:pot':'item:berry','actor:npc_lin_zhao'],{each:[{sub:'所点来源',selected:true},{sub:drink?'锅内实例':'同类来源'},{sub:'目标角色'}]});
 b.rows(448,422,707,[['目标','林昭'],['来源',drink?'随身锅中的净水':'所点罐头批次'],['预计变化',drink?'水分 65 → 95':'饱食 58 → 93'],['操作原因',blocked?'当前行动状态阻断':'核对后确认',blocked?'red':'sage']],57);
 b.button(448,728,271,drink?'确认饮用':'确认食用',blocked?'muted':'primary','continue');b.button(731,728,181,'取消');
 b.panel(1203,113,373,732,'食饮说明');
 b.para(1223,199,'目标与来源按当前角色及物品可用性联动。切换目标后重新确认来源。',320,{size:20});
 b.para(1223,338,'锅内容从队伍子行或锅浮窗发起；战斗物资页只读。',320,{size:20});
 b.art(choice,1290,496,173,173);
}
const craftCfg={
 制作:['手工制作','简易斧','item:hatchet',['item:wood','item:stone','item:fiber'],'基础工具 · 可制作'],
 来源:['材料来源','植物纤维','item:fiber',['resource:woods:woods_fiber','item:fiber','glyph:route'],'材料不足 · 查找已知来源'],
 维护:['维护与修补','简易斧','item:hatchet',['item:scrap','item:parts','item:cloth'],'显示当前耐久与修复预演'],
 工坊:['工坊队列','净水台','facility:water_station',['item:dirty_water','item:clean_water','facility:water_station'],'队列处理与产物']
};
function craft(b,v){
 const [t,label,key,materials,infoLine]=craftCfg[v];
 b.standard('camp','手工制作');b.panel(24,112,332,731,'工坊分类');
 ['全部配方','手工','工作台','锻炉','缝纫台','医疗台','维修','本地设施'].forEach((s,i)=>b.button(44,177+i*57,291,s,i===(v==='维护'?6:v==='工坊'?2:1)?'primary':'normal','craft'));
 b.panel(371,112,737,731,t,infoLine);
 b.art(key,590,182,286,241);b.text(741,457,label,{align:'middle',size:27,color:p.gold});
 b.artCardRow(392,488,696,191,materials,{cap:103,each:materials.map((k,i)=>({title:k.startsWith('glyph:')?'路线定位':undefined,sub:i===1&&v==='来源'?'来源不在当前地点':i===0?'已选材料':'查看详情'}))});
 b.text(395,737,'材料格、缺料提示与配方预演互相独立。',{size:17,color:p.muted});
 b.panel(1122,112,454,731,v==='来源'?'已知来源与返回':'本次操作预演');
 b.rows(1144,204,404,v==='来源'?[['来源','林地'],['状态','已知地块'],['按钮','只定位，不移动'],['返回','恢复原配方']]:[['耗时','显示真实预估'],['材料','按本次计划扣取'],['产物',label],['操作状态','等待确认']],58);
 b.button(1147,675,396,v==='来源'?'定位地图':v==='维护'?'确认维护':'开始制作','primary',v==='来源'?'map':'craft');
 b.button(1147,724,396,v==='来源'?'返回配方':'关闭');
}
const mapCfg={地图:['hub','区域地图','下一路段','临时营地','农野荒地','溪流','废弃村庄'],营区:['hub','幸存者营区','营门到广场','营门','营区广场','营区工坊','收容所'],教会:['church','教会圣所','接待庭到施济处','接待庭','施济处','典藏室','圣堂'],科学院:['lab','科学院封存区','接待处到检测室','外院','接待处','检测室','试制工坊'],饮水预告:['ruin','远行准备','远征饮水预算','入口','已知路径','核心','出口'],地牢地图:['ruin','地下圣所','当前实例 · 第 2 代','入口','捐献仓','环形礼拜厅','封锁祭室']};
// Coordinates follow the art inside its 1065×583 centered cover crop.
const cityShapes={
 营区:[['h01_gate','M429 408L467 402 470 382 518 382 522 399 644 409 653 399 699 405 703 422 749 425 749 530 426 526Z'],['h02_square','M490 311L524 301 553 306 583 304 616 319 623 344 597 357 526 355 491 342Z'],['h03_workshop','M110 253L159 207 371 205 408 240 404 303 338 324 138 312Z'],['h06_shelter','M61 135L103 111 251 88 320 102 318 165 278 190 85 218 72 198Z'],['h04_infirmary','M469 122L513 105 518 78 566 74 576 111 621 115 626 97 682 95 693 112 735 126 733 225 681 243 476 231Z'],['h05_appraisal','M207 94L270 66 416 54 448 73 450 141 324 181 323 107Z']],
 教会:[['c02_church','M377 184L425 173 427 151 476 151 478 173 540 185 538 266 504 281 391 262Z'],['c04_alms','M92 218L190 177 336 178 375 204 367 283 298 307 120 309Z'],['c05_admonition','M679 219L715 189 802 184 832 202 829 282 792 304 682 286Z'],['c06_archive','M798 281L864 239 948 246 987 277 981 360 918 385 797 351Z'],['c07_sanctuary','M408 126L458 62 532 54 572 27 609 61 658 74 660 7 732 10 736 85 790 109 790 161 742 182 677 190 630 255 528 267 505 189 421 182Z']],
 科学院:[['r02_academy','M394 448L428 417 469 431 472 460 591 462 613 429 660 439 657 563 393 563Z'],['r04_reception','M269 43L349 29 489 26 510 47 508 177 476 217 319 215 274 179Z'],['r05_testing','M666 91L729 75 866 85 877 105 918 106 947 127 932 238 834 242 777 197 661 174Z'],['r06_archive','M68 271L128 229 245 231 328 265 339 350 266 396 93 372Z'],['r07_workshop','M548 318L619 282 707 285 808 323 867 357 859 431 789 462 610 445 556 403Z'],['r08_observation','M39 143L67 113 234 105 281 122 282 207 250 237 107 246 51 216Z']]
};
const locationById=d(config.locations);
function cityTargets(b,v){
 b.add(5,'<g transform="translate(42 170)">'+cityShapes[v].map(([id,shape],i)=>{
   const title=locationById[id].name;
   return '<g class="building" data-building="'+id+'" data-name="'+e(title)+'" tabindex="0" role="button" aria-label="'+e(title)+'" aria-pressed="'+(i===1)+'"><title>'+e(title)+'</title><path class="building-shadow" d="'+shape+'" fill="none" stroke="#090603" stroke-width="5" opacity=".45"/><path class="building-outline" d="'+shape+'" fill="'+(i===1?'#d79b4b24':'#eac48906')+'" stroke="'+(i===1?'#ffe0a2':'#e0b276')+'" stroke-opacity="'+(i===1?'.95':'.48')+'" stroke-width="'+(i===1?'3.5':'1.8')+'"/></g>';
 }).join('')+'</g>');
}
function mapBoard(b,v){
 const [s,title,sub,...nodes]=mapCfg[v];
 const city=Boolean(cityShapes[v]);
 b.standard(v==='地图'?'woods':s,'区域地图');b.panel(24,112,1100,731,title,city?'选择图上的建筑':'已知路线');
 if(city)b.image(2,sceneFiles[s],42,170,1065,583,'xMidYMid slice',.8);
 else{
   b.rect(1,42,170,1065,583,'#171815');
   for(let i=0;i<10;i++)b.line(3,42+i*117,172,42+i*117,750,'#817d552d');
   for(let i=0;i<7;i++)b.line(3,42,174+i*96,1104,174+i*96,'#817d552d');
   b.add(3,path('M67 561L152 465 251 485 358 401 490 443 652 312 775 318 872 245 1092 247M67 575L169 491 260 511 369 427 502 474 661 346 793 345 895 273 1092 275','none','#76695247',4));
 }
 b.frame(42,170,1065,583);
 if(city)cityTargets(b,v);
 else{
  const dots=[[172,522],[397,356],[688,470],[928,287]];
  for(const [i,[x,y]] of dots.entries()){
   if(i)b.line(5,dots[i-1][0],dots[i-1][1],x,y,i===1?'#e6b875':'#c8ab80',3,i===3?'7 6':'');
   b.rect(5,x-9,y-9,18,18,'#be8b52','#f0ce94');b.rect(5,x-10,y+17,151,36,'#17100eea','#8f6742');
   b.text(x+1,y+41,nodes[i],{size:17});
  }
 }
 b.button(48,773,150,'主世界',city?'normal':'primary','map');b.button(209,773,150,city?'势力鸟瞰':'当前层',city?'primary':'normal','known');b.button(370,773,150,'回当前位置','normal','destination');
 b.panel(1139,112,437,731,city?'建筑与路线':sub);
 const faction=v==='教会'?'faction:faction_church':v==='科学院'?'faction:faction_academy':'faction:hub_survivor';
 b.art(v==='饮水预告'?'item:clean_water':faction,1267,184,173,153);
 if(city){
  const id=cityShapes[v][1][0];b.text(1165,391,'已选建筑',{size:17,color:p.gold});
  b.add(6,'<text x="1165" y="429" font-size="28" fill="#f1dfbb" data-building-name="">'+e(locationById[id].name)+'</text>');
  b.rows(1163,487,389,[['当前位置',nodes[0]],['下一段','仅预览本路段'],['默认','建筑外轮廓微亮'],['悬停','描边增强 · 名牌出现']],48);
 }else b.rows(1162,403,391,v==='饮水预告'?[['出发前净水','按队伍实际持有'],['已知路程','用已知路线估算'],['处理资源','锅 / 净水片 / 滤芯'],['结论','检查饮水缺口']]:[['当前位置',nodes[0]],['已选目的地',nodes[1]],['下一段','仅预览本路段'],['限制','未开通节点保留原因']],58);
 b.button(1164,695,390,v==='饮水预告'?'查看准备明细':'确认下一路段','primary','route');
}
function service(b,v){
 b.standard('hub');const id=v==='鉴定'?'actor:npc_tao_cen':v==='招募'?'actor:npc_du_sui':'duty:npc_xu_an';
 b.panel(24,113,396,731,'当前岗位','到场身份');
 b.art(id,80,185,280,444,{anchor:'xMidYMax meet'});
 b.text(223,687,v==='岗位事项'?'机构当班接续员':nameOf(id),{size:25,align:'middle',color:p.gold});
 b.text(223,726,v==='鉴定'?'鉴定所':v==='招募'?'收容所':'幸存者营区 · 代班',{size:18,align:'middle',color:p.muted});
 b.panel(437,113,1139,731,v==='招募'?'同行者名册':v==='鉴定'?'活性样本鉴定':'岗位事项与委托',v==='招募'?'按实际周期显示':'只读浏览不耗时');
 if(v==='招募'){
  const k=['recruitTemplate:front_guard','recruitTemplate:coordinator','recruitTemplate:camp_nurse'];
  b.artCardRow(461,184,1090,351,k,{cap:230,each:[{title:'前排护卫',sub:'职业示意 · 当前可登记'},{title:'协调员',sub:'职业示意 · 资料可查看'},{title:'营地护理员',sub:'职业示意 · 资料可查看'}]});
  b.rows(465,593,1075,[['候选资料','姓名、体貌与属性按记录显示'],['登记去向','编入同行 / 安排候补'],['周期状态','保留换批时间和可用名额']],51);
 }else if(v==='鉴定'){
  b.artCardRow(462,180,1090,250,['item:active_substance_white_unidentified','item:cloth','item:scrap'],{cap:138,each:[{sub:'样本'},{sub:'本日换物方案'},{sub:'产物预览'}]});
  b.rows(465,500,1075,[['免费说明','阅读'],['详细鉴定','查看当前样本'],['以物换物','当日报价与额度'],['产物去向','当前地点地面']],51);
 }else{
  b.rows(465,216,1075,[['岗位身份','机构当班接续员'],['事项列表','路线 / 服务 / 可查看内容'],['待接委托','显示实际可接或阻断原因'],['当前进度','接续同一登记与领取记录'],['与岗位交谈','可查看接续说明']],76);
 }
 b.button(464,760,240,v==='招募'?'查看登记去向':v==='鉴定'?'查看本日报价':'查看事项','primary','service');b.button(719,760,189,'返回');
}
function people(b,v){
 b.standard('hub','编队');b.panel(24,113,1552,733,v==='候补'?'留守候补与认领箱':'队伍与编队','真实队员 · 不消耗时间');
 const keys=['actor:p0','actor:npc_lin_zhao','actor:npc_zhou_yan',null];
 b.artCardRow(47,187,1506,405,keys,{cap:282,each:[{sub:'1 号位 · 当前行动者',selected:true},{sub:v==='候补'?'留守候补 · 营区公共居所':'2 号位 · 当前同行'},{sub:v==='候补'?'候补 · 可查看认领箱':'3 号位 · 当前同行'},{sub:'4 号位 · 未分配'}]});
 b.rows(52,648,1487,v==='候补'?[['候补位置','营区公共居所'],['遗物与认领箱','按归属展开查看']]:[['入场位置','前排 1 → 后排 4'],['可用操作','安排站位 · 核对角色 · 保存编队']],52);
 b.button(51,765,251,v==='候补'?'查看认领箱':'确认编队','primary','formation');
}
function growth(b){
 b.standard('camp','成长');b.panel(24,113,361,731,'十五门熟练度','你');
 config.skills.forEach((s,i)=>{
  const col=i>=8?1:0,x=40+col*167,y=180+(i%8)*62;
  b.button(x,y,156,s.name,i===0?'primary':'normal');b.art('skillFamily:'+s.id,x+6,y+9,20,20);
 });
 b.panel(401,113,1175,731,'拳 · 十级轨道','已解锁节点 1 / 5');
 for(let i=1;i<=10;i++){
  const x=425+(i-1)*113; b.rect(5,x,190,106,48,i===1?'#57402a':'#2f2319',i===1?p.gold:p.line);
  b.text(x+53,222,String(i),{size:21,align:'middle',color:i===1?p.gold:p.light});
 }
 const nodes=config.skillNodes.filter(s=>s.skillId==='unarmed').sort((x,y)=>x.level-y.level);
 b.artCardRow(425,275,1127,236,nodes.map(s=>'skill:'+s.id),{cap:122,each:nodes.map((s,i)=>({sub:i===0?'可用 · 达到 1 级':'未解锁 · 达到 '+s.level+' 级',selected:i===0,locked:i>0}))});
 b.para(430,561,nodes[0].displayText,1090,{size:21,line:35});
 b.rows(430,647,1113,[['当前等级','1 / 10'],['经验','0 / 100'],['下一个节点','3 级 · 推挡']],48);
}
const readingData={
 休息:['本地休息条件','按可用床位与当前状态预演恢复。','facility:bed',[['短休','30 分钟'],['休整','60 分钟'],['睡眠','按条件启用'],['风险','环境与安全状态']]],
 目标:['序章任务册','先保持食水，再查看探索与战斗目标。','glyph:goal',[['寻找安全饮水','进行中'],['第一次交手','未完成'],['远行准备','检查营区路线'],['已完成项','可展开']]],
 提示:['当前求生建议','结合角色状态、物品库存和当前位置显示建议。','glyph:warning',[['当前优先','保持水分'],['临时营地','可使用火堆'],['敌踪','处理前查看风险'],['当前提示','可收起']]],
 日志:['行动日志','按游戏内时间记录已结算事件。','glyph:log',[['16:10','查看当前地点'],['15:40','开始搜集营地周边'],['15:10','回到临时营地'],['14:55','获得净水']]],
 说明:['系统说明','选择主题后，在右侧阅读与当前规则对应的说明。','glyph:help',[['物品与容器','容量、根卡与批次'],['人物与医疗','状态、药品与食饮'],['战斗','技能、站位与瞄准'],['键盘操作','按键与关闭顺序']]]
};
function reading(b,v){
 const [name,desc,k,rows]=readingData[v];b.standard('camp',{休息:'休息',目标:'生存目标',提示:'生存提示',日志:'行动日志',说明:'系统说明'}[v]);
 b.panel(24,113,395,731,'索引');rows.forEach(([s],i)=>b.button(48,177+i*60,344,s,i===0?'primary':'normal'));
 b.panel(434,113,1141,731,name,v==='日志'?'按时间排列':'可滚动阅读');
 b.art(k,1324,162,202,196);b.para(464,198,desc,774,{size:23,line:37});
 b.rows(466,334,1057,rows,69);
 b.button(467,746,224,v==='休息'?'查看休息预演':'返回当前地点','primary',v==='休息'?'rest':'return');
}
function speech(b,x,y,w,speaker,line,side,active=false){
 const start=b.L.map(g=>g.length),utterance=(b.utterances=(b.utterances||0)+1);
 const color=side==='left'?'#282219':'#302019';
 const count=wrap(line,(w-38)/20).length,h=61+count*34;
 const pivot=side==='left'?x:x+w,direction=side==='left'?-1:1;
 b.rect(1,x,y,w,h,color);b.frame(x,y,w,h,{ink:active?'#e3ae71':'#876344'});
 b.add(3,path('M'+(pivot-direction*3)+' '+(y+41)+'l'+(direction*16)+' 13l'+(-direction*16)+' 10z',color,active?'#d59b65':'#8b6142',2.5));
 b.text(x+20,y+31,speaker,{size:16,color:active?p.gold:p.muted});
 b.para(x+20,y+65,line,w-38,{size:20,line:34});
 for(let i=0;i<b.L.length;i++){
  if(b.L[i].length>start[i])b.L[i].push('<g data-utterance="'+utterance+'" data-side="'+side+'">'+b.L[i].splice(start[i]).join('')+'</g>');
 }
}
function dialogue(b,v){
 const hist=v==='对话记录',duty=v==='代班对白',s=duty||hist?'hub':'camp';
 b.standard(s,hist?'对话记录':'');
 const counterpart=duty||hist?'duty:npc_xu_an':'actor:baobao';
 const speaker=duty||hist?'机构当班接续员':'饱饱';
 const body=duty||hist?'这里由我代班接续公共事务。登记和任务记录仍沿用。':'醒醒……醒醒，我饿了。先找点能喝的水。';
 b.panel(24,113,1552,730,hist?'对话记录':'当前对白',hist?'按原发言身份回看':'对话中 · 时间未推进');
 const shift=hist?280:0;
 if(hist){
  b.panel(44,180,262,595,'已读记录');
  [['序章 · 醒来',0],['营区 · 初次来访',1],['营区 · 代班说明',2]].forEach(([k,i])=>b.button(61,253+i*57,230,k,i===2?'primary':'normal'));
 }
 const x=49+shift, right=1305;
 b.rect(1,x,208,204,350,'#2e2217');b.frame(x,208,204,350);
 b.art('actor:p0',x+38,255,128,227,{portrait:true,anchor:'xMidYMid meet'});
 b.text(x+102,527,'你',{size:22,align:'middle'});
 b.rect(1,right,208,229,350,'#39261a');b.frame(right,208,229,350,{ink:p.gold});
 b.art(counterpart,right+43,264,143,184,{portrait:true});
 b.text(right+114,526,speaker,{size:19,align:'middle',color:p.gold});
 const tx=x+230,tw=right-tx-24,bw=Math.min(535,Math.round(tw*.68));
 speech(b,tx+tw-bw,207,bw,speaker,body,'right');
 speech(b,tx,345,bw,'你',duty||hist?'知道了。原来的登记还能继续用，对吧？':'我听见了。这里有安全的水源吗？','left');
 speech(b,tx+tw-bw,483,bw,speaker,duty||hist?'能。办事时沿用之前的登记和领取记录。':'先看看身边的东西。能烧开的水更稳妥。','right',!hist);
 b.text(tx,659,hist?'只读回看 · 保留当时的发言人和地点':'正文可滚动 · 阅读结束后恢复当前操作',{size:17,color:p.muted});
 b.button(1285,740,247,hist?'关闭':'继续','primary',hist?'close':'continue');
}
function titleBoard(b,v){
 const type=v==='死亡'?'死亡':v;
 b.backdrop(type==='死亡'?'ruin':'hub');
 b.rect(1,60,102,652,685,'#1b130fe8');b.frame(60,102,652,685,{ink:'#aa7b53',corner:22});
 b.art('glyph:'+(type==='死亡'?'warning':type==='读档'?'save':'camp'),103,151,64,64);
 b.text(105,263,v==='标题'?'适者生存':v==='序章'?'序章 · 醒来':v==='读档'?'继续本局':v==='死亡'?'本局结束':'你活下来了',{size:58,color:p.gold,spacing:4});
 b.text(108,309,'重墨 V2     /     '+b.page[0],{size:17,color:p.muted,spacing:2});
 b.line(3,106,338,666,338,p.iron,2);
 const label={标题:'末日求生 · 卡牌原型',序章:tm,读档:'载入本浏览器的存档。选择新游戏时显示覆盖确认。',生存结算:'本局已达成求生目标。查看存活时间与行动记录。',死亡:'查看本局结束原因和行动日志，然后返回标题。'}[v];
 b.para(108,390,label,545,{size:22,line:39});
 const buttons={标题:[['新游戏','fresh'],['继续','continue'],['玩法说明','help']],序章:[['继续','continue'],['对话记录','history']],读档:[['继续存档','continue'],['新游戏 · 覆盖确认','warning'],['返回','return']],生存结算:[['查看行动日志','log'],['重新开始','fresh'],['返回标题','return']],死亡:[['查看结束原因','log'],['重新开始','fresh'],['返回标题','return']]}[v];
 buttons.forEach(([s,g],i)=>b.button(110,494+i*58,450,s,i===0?'primary':'normal',g));
 if(v==='读档'){b.meter(113,696,445,.63);b.text(113,727,'首次加载显示独立进度与异常提示',{size:16,color:p.muted});}
 if(v==='标题')b.text(110,728,'阅读字体  细像素 / 清晰阅读',{size:18,color:p.muted});
 if(v==='死亡'||v==='生存结算')b.text(110,727,v==='死亡'?'记录本局事件与死亡原因':'求生目标达成 · 可回看记录',{size:18,color:p.muted});
 b.art(v==='死亡'?'enemy:z_helmet':'actor:p0',819,164,631,628,{anchor:'xMidYMax meet',opacity:.98});
 b.footer('离线设计样板 · 标题与结算');
}
const render={world,gear,battle,modal,slots,house,feed,craft,map:mapBoard,service,people,growth,reading,dialogue,title:titleBoard};
const out=process.argv[2]||require('node:path').resolve(__dirname,'..');
const pathFS=require('node:path');
const src=fs.readFileSync(pathFS.join(__dirname,'旧版场景与参考.js'),'utf8');
const legacy=require('node:vm').runInNewContext(src+'\n({scenes,ref})');
const seenPages=new Set(pages.map(x=>x[0]));
for(const old of legacy.scenes)if(!seenPages.has(old.id))throw Error('Legacy page missing: '+old.id);
const compMap={};for(const [id,, ,components] of pages)for(const c of components.split(' '))(compMap[c]??=[]).push(id);
const baseComponents=fs.readFileSync(pathFS.join(__dirname,'基线组件.txt'),'utf8').replace(/^\uFEFF/,'').trim().split(/\r?\n/).map(s=>s.replace(/\.razor$/,''));
for(const c of baseComponents)if(!compMap[c]&&extraComponents[c])compMap[c]=[extraComponents[c]];
for(const c of baseComponents)if(!compMap[c])throw Error('Current component missing: '+c);
const srcMap={};
for(const [code,r] of Object.entries(legacy.ref))srcMap[code]={name:r.name,use:r.use,file:'素材/21张原始参考/'+r.file.split('../参考原图/')[1],pages:pages.filter(p=>p[4].split(' ').includes(code)).map(p=>p[0])};
for(const code in srcMap)if(!srcMap[code].pages.length)throw Error('Missing original art reference '+code);
const drawings=[], placements={};
fs.mkdirSync(pathFS.join(out,'SVG源稿'),{recursive:true});
for(const page of pages){
 const board=new Board(page);
 render[page[2]](board,page[5]);
 const svg=board.svg(), [id,title,template,components,refs,variant]=page;
 const group=id.slice(0,id.startsWith('PX')?2:3);
 const srcFile='SVG源稿/'+id+'.svg';
 fs.writeFileSync(pathFS.join(out,srcFile),svg.replaceAll('@ASSET@','../素材/ink-v2'));
 placements[id]=board.layout;
 drawings.push({id,title,template,components:components.split(' '),refs:refs.split(' '),variant,group,srcFile,artKeys:[...board.artKeys].sort(),svg:svg.replaceAll('@ASSET@','素材/ink-v2')});
}
const meta={baseline:'02ea6a299a3559b110ca66db58ad82e8ec2197ec',artBaseline:'fd7518c24b3a99a8262c60c6c5399db935a842fb',designSize:[W,H],families,layers,sourceReferences:srcMap,componentIndex:compMap,baselineComponentCount:baseComponents.length,pages:drawings.map(({svg,...r})=>r)};
fs.writeFileSync(pathFS.join(out,'资料','覆盖与素材映射.json'),JSON.stringify(meta,null,2));
fs.writeFileSync(pathFS.join(out,'资料','逐页坐标与组件槽.json'),JSON.stringify({origin:'左上',unit:'1600×900 设计像素',pages:placements},null,2));
fs.writeFileSync(pathFS.join(out,'资料','预览数据.js'),'window.INK_BOARDS = '+JSON.stringify({...meta,pages:drawings})+';\n');
console.log(JSON.stringify({pages:drawings.length,originals:Object.keys(srcMap).length,components:baseComponents.length,bindings:new Set(drawings.flatMap(x=>x.artKeys)).size}));
