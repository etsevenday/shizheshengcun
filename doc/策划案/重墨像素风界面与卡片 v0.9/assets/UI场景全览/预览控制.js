/* 离线预览导航与图层开关。 */
const screen = document.getElementById('screen');
const select = document.getElementById('scenePick');
const catalog = document.getElementById('catalog');
const layerBar = document.getElementById('layerBar');
const toggles = [...layerBar.querySelectorAll('[data-toggle]')];
const sceneById = new Map(scenes.map(scene=>[scene.id,scene]));
let current = scenes[0];

const choice = (scene,tag) => tag==='option'
  ? '<option value="'+scene.id+'">'+scene.id+' · '+esc(scene.title)+'</option>'
  : '<button type="button" class="sceneLink" data-jump="'+scene.id+'"><span class="id">'+scene.id+'</span>'+esc(scene.title)+'</button>';

for (const [id,name] of groups) {
  const pages=scenes.filter(scene=>scene.group===id);
  catalog.insertAdjacentHTML('beforeend','<details data-group="'+id+'"><summary>'+id+' · '+esc(name)+'</summary>'+pages.map(scene=>choice(scene,'button')).join('')+'</details>');
  select.insertAdjacentHTML('beforeend','<optgroup label="'+esc(id+' · '+name)+'">'+pages.map(scene=>choice(scene,'option')).join('')+'</optgroup>');
}

function show(scene) {
  current=scene;
  screen.dataset.page=scene.id;
  screen.innerHTML=renderers[scene.view]();
  document.getElementById('sceneTitle').textContent=scene.id+' · '+scene.title;
  document.getElementById('components').textContent='开发对照　'+scene.components;
  select.value=scene.id;
  const navButton=catalog.querySelector('[data-jump="'+scene.id+'"]');
  catalog.querySelector('[aria-current]')?.removeAttribute('aria-current');
  navButton.setAttribute('aria-current','true');
  navButton.closest('details').open=true;
  const inUse=new Set([...screen.querySelectorAll('[data-art]')].map(el=>art[el.dataset.art][0]).filter(id=>ref[id]));
  const ids=new Set([...scene.sources,...inUse]);
  document.getElementById('sources').innerHTML=[...ids].map(id=>{
    const {name,file,use}=ref[id];
    return '<a class="sourceCard" href="'+esc(file)+'" target="_blank" rel="noopener"><img alt="" src="'+esc(file)+'" loading="lazy">'+
      '<strong>'+id+' · '+esc(name)+'</strong><span>'+esc(inUse.has(id)?'本页实际取景':use)+'</span></a>';
  }).join('');
}

function jump(id) {
  const scene=sceneById.get(id);
  if(!scene) return;
  if(location.hash!=='#'+id) location.hash=id;
  if(current!==scene) show(scene);
}
window.addEventListener('hashchange',()=>{
  const scene=sceneById.get(location.hash.slice(1))||scenes[0];
  if(current!==scene) show(scene);
});
select.addEventListener('change',()=>jump(select.value));
document.addEventListener('click',event=>{
  const control=event.target.closest('[data-jump]');
  if(control) jump(control.dataset.jump);
});
document.getElementById('prev').addEventListener('click',()=>jump(scenes[(scenes.indexOf(current)+scenes.length-1)%scenes.length].id));
document.getElementById('next').addEventListener('click',()=>jump(scenes[(scenes.indexOf(current)+1)%scenes.length].id));

function applyLayers() {
  for(const field of toggles) screen.dataset[field.dataset.toggle]=field.checked?'on':'off';
  screen.dataset.ui=toggles.some(field=>!['scene','art'].includes(field.dataset.toggle)&&field.checked)?'on':'off';
  for(const preset of layerBar.querySelectorAll('[data-mode]')) {
    const active=toggles.every(field=>field.checked===layerOn(preset.dataset.mode,field.dataset.toggle));
    preset.setAttribute('aria-pressed',String(active));
  }
}
function layerOn(mode,id) {
  const visual=id==='scene'||id==='art';
  return mode==='combined'||(mode==='art'?visual:!visual);
}
layerBar.addEventListener('change',applyLayers);
layerBar.addEventListener('click',event=>{
  const preset=event.target.closest('[data-mode]');
  if(!preset) return;
  for(const field of toggles) field.checked=layerOn(preset.dataset.mode,field.dataset.toggle);
  applyLayers();
});

show(sceneById.get(location.hash.slice(1))||scenes[0]);
applyLayers();
