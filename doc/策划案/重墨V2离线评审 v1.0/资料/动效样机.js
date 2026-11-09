'use strict';
const get=id=>document.getElementById(id),profiles=INK_MOTION.profiles,boards=INK_BOARDS.pages;
const base=get('base'),fx=get('fx'),stage=get('stage'),timer=get('time'),picker=get('profile'),less=get('less');
const prefers=matchMedia('(prefers-reduced-motion: reduce)');
const clamp=(x,a=0,b=1)=>Math.min(b,Math.max(a,x));
const smooth=x=>{const t=clamp(x);return t*t*(3-2*t);};
const span=(t,a,b)=>smooth((t-a)/(b-a));
const envelope=(t,a,b,c,d)=>span(t,a,b)*(1-span(t,c,d));
const esc=v=>String(v??'').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const actorNames={'actor:p0':'你','actor:npc_lin_zhao':'林昭','enemy:shambler':'游荡尸','enemy:z_helmet':'护盔感染者'};
let active,at=0,run=0,playing=false,last=0,lastPhase='';
function sprite(k){
 const {crop,size,file}=INK_MOTION_ART[k];
 return '<svg viewBox="'+crop.join(' ')+'" preserveAspectRatio="xMidYMax meet" aria-hidden="true"><image href="素材/ink-v2/'+esc(file)+'" width="'+size[0]+'" height="'+size[1]+'" preserveAspectRatio="none"/></svg>';
}
for(const p of profiles){
 const option=document.createElement('option');option.value=p.id;option.textContent=p.label;picker.append(option);
}
new ResizeObserver(()=>{fx.style.transform='scale('+stage.clientWidth/1600+')';}).observe(stage);
less.checked=prefers.matches;
function halt(){
 playing=false;cancelAnimationFrame(run);get('play').textContent='播放';
}
function mode(p,resetURL=true){
 halt();active=p;at=0;last=0;lastPhase='';timer.max=p.duration;
 if(resetURL&&location.hash!=='#'+p.id)history.replaceState(null,'','#'+p.id);
 picker.value=p.id;get('note').textContent=p.note;
 base.innerHTML=boards.find(b=>b.id===(p.id==='interrupt'?'P01b':p.board)).svg;
 fx.innerHTML='';
 if(p.mode==='battle'){
  fx.innerHTML='<div class="cine"><div class="shot"></div><div class="rig"><div class="sprite actor">'+sprite(p.attacker)+'<small>'+esc(actorNames[p.attacker])+'</small></div><div class="sprite target">'+sprite(p.defender)+'<small>'+esc(actorNames[p.defender])+'</small></div>'+
   (p.second?'<div class="sprite target2">'+sprite(p.second)+'<small>'+esc(actorNames[p.second])+'</small></div>':'')+
   '<svg class="contact" viewBox="0 0 1600 900"><path class="slash" d="M693 522Q844 343 1024 312Q870 405 731 535Z" fill="#fbb680"/><path class="trace" d="M678 472L996 387" fill="none" stroke="#fbe0a7" stroke-width="6"/><circle class="spark" cx="1023" cy="423" r="38" fill="none" stroke="#ffe6ae" stroke-width="9"/><path class="shield" d="M963 356L1004 373L1046 356V410Q1046 443 1004 463Q963 443 963 410Z" fill="#8e683850" stroke="#f6d18c" stroke-width="5"/></svg></div>'+
   '<div class="letter top"></div><div class="letter bottom"></div><div class="skill">'+esc(p.action)+'</div>'+
   (p.second?'<div class="result" style="left:232px">−3</div><div class="result" style="left:453px">−2</div>':'<div class="result'+(p.id==='heal'?' heal':'')+'" style="left:245px">'+esc(p.outcome)+'</div>')+
   '<div class="cutHint">'+esc(actorNames[p.attacker])+'  /  '+esc(actorNames[p.defender])+'</div></div>';
 }
 if(p.mode==='action'){
  fx.innerHTML=(p.id==='move'?'<div class="arrival" style="position:absolute;inset:0;background:linear-gradient(#21180c55,#130e08d4),url(素材/ink-v2/woods.png) center/cover"></div>':'')+
   '<div class="veil"></div><section class="popup'+(p.immediate?' toast':'')+'"><div class="icon">'+sprite('glyph:'+p.glyph)+'</div><h2>'+esc(p.labelAction)+'</h2><p class="actionInfo"></p>'+
   (p.immediate?'':'<div class="meter"><i></i></div>')+'<div class="pill actionControl"></div></section>';
  if(p.immediate)fx.insertAdjacentHTML('beforeend','<svg class="contact" viewBox="0 0 1600 900"><path class="transferTrace" d="M531 321Q719 142 1011 318" fill="none" stroke="#d8ad70" stroke-width="3" stroke-dasharray="8 7"/><circle class="transferOrigin" cx="531" cy="321" r="43" fill="none" stroke="#c2a57d" stroke-width="4"/><circle class="transferEnd" cx="1011" cy="318" r="49" fill="none" stroke="#b7d89d" stroke-width="5"/><circle class="transferDot" r="9" fill="#ffd89c"/></svg>');
 }
 if(p.mode==='panel'){
  const kind=p.id==='drawer'?'side':p.id==='toast'?'toast':'';
  fx.innerHTML='<div class="veil"></div><section class="popup '+kind+'"><h2>'+esc(kind==='side'?'对象详情':kind==='toast'?'已取得木材 ×2':'游戏菜单')+'</h2><p>'+esc(kind==='side'?'查看当前对象与可用操作。':kind==='toast'?'物品已放到当前地面。':'阅读字体 · 动画速度 · 返回标题')+'</p>'+(kind==='toast'?'':'<div class="pill">关闭</div>')+'</section>';
 }
 if(p.mode==='map'){
  window.bindInkBuildings(base.firstElementChild,(name)=>report('已选：'+name+' · 等待下一路段确认'));
 }
 const noPlayer=p.mode==='map';
 for(const id of ['play','again','end','time'])get(id).disabled=noPlayer;
 render(0);
}
const q=s=>fx.querySelector(s);
function report(s){if(lastPhase!==s){get('phase').textContent=s;lastPhase=s;}}
function battle(t,p,reduced){
 const visible=envelope(t,122,270,905,1122);
 const final= t>=p.duration;
 const contact=span(t,350,417)*(1-span(t,485,595));
 const lunge=span(t,220,403)*(1-span(t,469,682));
 const response=span(t,391,490)*(1-span(t,536,687));
 const restore=span(t,830,1098);
 const resultFade=envelope(t,440,517,800,1100);
 const isRanged=p.id==='ranged',isMiss=p.id==='miss',isHeal=p.id==='heal',isGuard=p.id==='guard',isDefeat=p.id==='defeat';
 const cine=q('.cine');
 // At the final keyframe return to the whole field. Reduced motion still shows the result on that field.
 cine.style.opacity=String(reduced?0:visible);
 q('.shot').style.transform='scale('+(1+.18*(span(t,142,382)-restore))+')';
 q('.rig').style.transform='translate('+(contact*(isGuard||isHeal||isMiss?0:4)*Math.sin(t*.19))+'px,0) scale('+(1+.06*(span(t,167,422)-restore))+')';
 q('.actor').style.transform='translateX('+((isRanged?21:isHeal?52:124)*lunge)+'px) rotate('+(-3*lunge)+'deg)';
 q('.target').style.transform='translateX('+((isMiss?111:isHeal?0:38)*response)+'px) rotate('+((isDefeat?66*span(t,442,790):isHeal?0:8*response))+'deg)';
 q('.target').style.opacity=String(isDefeat?1-span(t,575,831):1);
 if(p.second)q('.target2').style.transform='translateX('+(49*response)+'px) rotate('+(8*response)+'deg)';
 q('.slash').style.opacity=String(isRanged||isMiss||isHeal||isGuard?0:contact);
 q('.trace').style.opacity=String(isRanged?envelope(t,348,390,416,475):0);
 q('.spark').style.opacity=String(isMiss?0:isHeal||isGuard?contact:contact*.66);
 q('.spark').setAttribute('stroke',isHeal?'#b7dc9d':isGuard?'#e1c49e':'#ffd2a0');
 q('.shield').style.opacity=String(isGuard?contact:0);
 for(const number of fx.querySelectorAll('.result')){
  number.style.opacity=String(resultFade);number.style.transform='translateY('+(-28*span(t,432,700))+'px)';
 }
 let recap=fx.querySelector('.recap');
 if(!recap){recap=document.createElement('div');recap.className='popup toast recap';fx.append(recap);}
 recap.innerHTML='<h2>'+esc(p.action)+' · '+esc(p.outcome)+'</h2><p>数值与状态来自同一次指令结果</p>';
 recap.style.opacity=String((reduced&&t>=390)||final?1:0);
 const cue=INK_MOTION.battle.cues.filter(c=>t>=c.at).at(-1);
 return cue.name;
}
function action(t,p,reduced){
 const progress=Math.round(clamp((t-150)/1000)*100);
 const last=p.immediate?520:1240,done=t>=last;
 const box=q('.popup');
 const appearance=reduced?1:span(t,0,160);
 box.style.opacity=appearance;
 box.style.transform=reduced?'none':'translateY('+((1-appearance)*14)+'px)';
 q('.veil').style.opacity=String(appearance*.52);
 q('.actionInfo').textContent=done?p.result:p.immediate?'确认来源、目标与条件':'按游戏阶段更新  '+(p.id==='interrupt'?Math.min(50,progress):progress)+'%';
 const fill=q('.meter>i');if(fill)fill.style.width=(p.id==='interrupt'?Math.min(50,progress):progress)+'%';
 const arrival=q('.arrival');if(arrival)arrival.style.opacity=String(reduced?Number(done):span(t,1240,1530));
 if(p.immediate){
  const fail=p.id==='reject',trace=q('.transferTrace'),point=q('.transferDot'),f=span(t,235,540);
  trace.style.opacity=String(fail||reduced?0:envelope(t,180,340,680,935)*.7);
  const loc=trace.getPointAtLength(f*trace.getTotalLength());point.setAttribute('cx',loc.x);point.setAttribute('cy',loc.y);
  point.style.opacity=String(fail||reduced?0:envelope(t,210,330,542,650));
  q('.transferOrigin').style.opacity=String(envelope(t,125,235,427,600)*.9);
  q('.transferOrigin').setAttribute('stroke',fail?'#df8067':'#c2a57d');
  q('.transferEnd').style.opacity=String(fail?0:envelope(t,457,585,849,1160));
 }
 q('.actionControl').textContent=done?(p.id==='interrupt'?'继续 / 退出':p.id==='reject'?'返回原操作':'查看结果'):p.immediate?'处理中':'退出行动';
 return done?p.id==='interrupt'?'暂停 · 进度保留':'结果反馈':p.immediate?'核对请求':'阶段推进';
}
function panel(t,p,reduced){
 const a=reduced?(t>=70&&t<1300?1:0):envelope(t,30,220,1110,1460);
 const box=q('.popup');box.style.opacity=a;
 box.style.transform=reduced?'none':p.id==='drawer'?'translateX('+((1-a)*62)+'px)':'translateY('+((1-a)*16)+'px) scale('+(.98+.02*a)+')';
 q('.veil').style.opacity=p.id==='toast'?0:a*.62;
 return t<230?'入场 · 焦点准备':t<1110?'驻留 · 内容可读':'退场 · 焦点归还';
}
function dialogue(t,reduced){
 const groups=base.querySelectorAll('[data-utterance]');let count=0;
 for(const group of groups){
  const i=Number(group.dataset.utterance),begin=[0,100,770,1440][i],fade=reduced?Number(t>=begin):span(t,begin,begin+190);
  count=Math.max(count,fade>0?i:0);group.style.opacity=String(fade);
  group.style.transform=reduced?'none':'translateX('+((group.dataset.side==='left'?-1:1)*(1-fade)*20)+'px)';
 }
 return count?'第 '+count+' 句 · '+(count===2?'左侧发言':'右侧发言'):'等待开始';
}
function render(t){
 at=clamp(t,0,active.duration);timer.value=at;get('stamp').textContent=Math.round(at)+' / '+active.duration+' ms';
 const p=active,reduced=less.checked;
 if(p.mode==='map'){report('把光标移到建筑上，或用 Tab 移动焦点。');return;}
 report(p.mode==='battle'?battle(at,p,reduced):p.mode==='action'?action(at,p,reduced):p.mode==='panel'?panel(at,p,reduced):dialogue(at,reduced));
}
function step(now){
 if(!playing)return;
 if(!last)last=now;
 const next=at+(now-last);last=now;render(next);
 if(at<active.duration)run=requestAnimationFrame(step);else halt();
}
function play(reset){
 if(!active||active.mode==='map')return;
 halt();if(reset||at>=active.duration)render(0);last=0;playing=true;get('play').textContent='暂停';run=requestAnimationFrame(step);
}
get('play').onclick=()=>playing?halt():play(false);
get('again').onclick=()=>play(true);
get('end').onclick=()=>{halt();render(active.duration);};
picker.onchange=()=>mode(profiles.find(p=>p.id===picker.value));
timer.oninput=()=>{halt();render(Number(timer.value));};
less.onchange=()=>render(at);
window.addEventListener('hashchange',()=>mode(profiles.find(p=>p.id===location.hash.slice(1))||profiles[0],false));
mode(profiles.find(p=>p.id===location.hash.slice(1))||profiles[0],false);
