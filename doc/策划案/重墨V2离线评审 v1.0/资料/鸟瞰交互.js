window.bindInkBuildings=function(svg,onSelect){
 const areas=[...svg.querySelectorAll('[data-building]')];
 if(!areas.length)return;
 const ns='http://www.w3.org/2000/svg';
 const put=(type,attr)=>{const node=document.createElementNS(ns,type);for(const [name,v] of Object.entries(attr))node.setAttribute(name,v);return node;};
 const tooltip=put('g',{'aria-hidden':'true','pointer-events':'none',visibility:'hidden'});
 const plate=put('rect',{height:34,fill:'#21140df5',stroke:'#d5a269','stroke-width':2});
 const label=put('text',{'font-size':17,fill:'#ffe6b6','text-anchor':'middle'});
 tooltip.append(plate,label);areas[0].parentNode.append(tooltip);
 function show(target){
  const w=Math.max(78,target.dataset.name.length*17+22), box=target.getBBox();
  const x=Math.min(1065-w-13,Math.max(13,box.x+box.width/2-w/2));
  const y=Math.max(14,box.y-42);
  tooltip.setAttribute('transform','translate('+x+' '+y+')');tooltip.setAttribute('visibility','visible');
  plate.setAttribute('width',w);label.setAttribute('x',w/2);label.setAttribute('y',23);label.textContent=target.dataset.name;
 }
 function select(target){
  for(const area of areas){area.setAttribute('aria-pressed',String(area===target));area.classList.remove('flash');}
  target.classList.add('flash');
  const name=svg.querySelector('[data-building-name]');if(name)name.textContent=target.dataset.name;
  onSelect?.(target.dataset.name,target.dataset.building);
 }
 for(const area of areas){
  const hide=()=>tooltip.setAttribute('visibility','hidden');
  area.addEventListener('pointerenter',()=>show(area));area.addEventListener('pointerleave',hide);
  area.addEventListener('focus',()=>show(area));area.addEventListener('blur',hide);
  area.addEventListener('click',()=>select(area));
  area.addEventListener('keydown',ev=>{if(ev.key==='Enter'||ev.key===' '){ev.preventDefault();select(area);}});
 }
};
