// 本地离线预览用。真正的操作由游戏组件处理。
(() => {
  const C=window.VisualCatalog;
  const $=id=>document.getElementById(id);
  const byKey=new Map(C.assets.map(row=>[row.key,row]));
  const names={
    item:'物品',gear:'穿戴装备绑定',weapon:'武器绑定',skillFamily:'熟练度类别',skill:'技能节点',action:'战斗动作',condition:'战斗状态',status:'HUD 状态',
    enemy:'敌人',carcassRule:'尸体处理绑定',actor:'固定人物',duty:'岗位代班',recruitTemplate:'招募外观模板',
    location:'地点',dungeon:'地牢',region:'地图区域',faction:'势力徽章',gate:'地图通行状态',resource:'资源点',
    facility:'设施工坊',publicStation:'公共工坊',process:'加工过程',recipe:'制作配方',knowledge:'知识资料',
    document:'场景文档',quest:'任务',knowledgePack:'知识包',service:'岗位服务',affix:'装备词条',
    dialogueLine:'对白身份',dialogueSet:'对白组',glyph:'UI 图标',frame:'UI 边框',texture:'UI 底纹',component:'界面组件'
  };
  const order=['item','actor','enemy','skill','location','resource','facility','frame','glyph','component','duty','recruitTemplate'];
  const sorted=[...new Set([...order,...Object.keys(C.counts)])].filter(key=>C.counts[key]);
  const esc=x=>String(x).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;').replaceAll("'",'&#39;');
  const resolve=(asset)=>{while(asset?.ref) asset=byKey.get(asset.ref)?.asset;return asset;};
  const art=(asset,cls='',layer='',portrait=false)=>{
    asset=resolve(asset);
    if(!asset) return '<span class="art '+cls+'"></span>';
    const f=esc(asset.file);
    const data=layer?' data-layer="'+layer+'"':'';
    if(asset.crop){
      const [x,y,w,h]=portrait&&asset.portraitCrop?asset.portraitCrop:asset.crop;
      return '<span class="art '+cls+'"'+data+'><svg viewBox="0 0 '+w+' '+h+'" aria-hidden="true"><svg width="'+w+
        '" height="'+h+'" viewBox="'+[x,y,w,h].join(' ')+'" overflow="hidden"><image href="'+f+'" width="'+asset.size[0]+
        '" height="'+asset.size[1]+'"/></svg></svg></span>';
    }
    if(asset.relativeCrop&&asset.size){
      const [sw,sh]=asset.size;
      const [x,y,w,h]=asset.relativeCrop.map((n,i)=>n*(i%2===0?sw:sh)/100);
      return art({...asset,crop:[x,y,w,h]},cls,layer);
    }
    return '<span class="art '+cls+'"'+data+'><img alt="" src="'+f+'"></span>';
  };
  const rowArt=(key,cls='',layer='',portrait=false)=>{
    const row=byKey.get(key);
    return art(portrait&&row?.portrait?row.portrait:row?.asset,cls,layer,portrait);
  };
  const text=(value,cls='')=>'<span class="text '+cls+'" data-layer="text">'+esc(value)+'</span>';
  const chip=value=>'<span class="chip" data-layer="status">'+esc(value)+'</span>';
  const glyph=(key,cls='inlineIcon')=>rowArt('glyph:'+key,cls,'status');
  const btn=(value,icon='',variant='')=>'<button type="button" class="skin '+variant+'">'+(icon?glyph(icon):'')+text(value)+'</button>';
  const skin=(contents,cls='')=>'<section class="skin '+cls+'">'+contents+'</section>';
  const scene=key=>rowArt(key,'scene','scene');
  const speaker=(key,name)=>'<div class="speaker">'+rowArt(key,'','subject',true)+text(name)+'</div>';
  const meter=(name,n)=>'<div class="meter" data-layer="status">'+text(name)+'<i style="--value:'+n+'%"></i></div>';
  const stateCard=(key,state,variant)=>skin(rowArt(key,'','subject')+text(byKey.get(key).name,'name')+chip(state),'uislot '+variant);
  const portrait=key=>rowArt(key,'','subject',true);
  const select=(label,name,values)=>'<label>'+esc(label)+'<select data-identity="'+name+'"><option value="">未记录</option>'+
    values.map(x=>'<option value="'+esc(x.id)+'">'+esc(x.name)+'</option>').join('')+'</select></label>';
  const identityUI=()=>skin('<h3>'+text('按职业名称重绘')+'</h3><div class="identityFields">'+
    select('服装样例','template',C.assets.filter(row=>row.category==='recruitTemplate'&&row.id!=='legacy_neutral'))+
    '</div><div id="pose" class="pose" data-layer="subject"></div>','identityDemo');
  const dialog=(kind)=>{
    const history=kind==='history',battle=kind==='battle';
    const other=battle?'actor:baobao':history?'duty:npc_lu_heng':'actor:npc_cheng_wang';
    return scene(battle?'location:m02_checkpoint':'location:camp')+skin('<div class="dialogueTop">'+
      speaker('actor:p0','主角')+chip(history?'历史记录':battle?'战斗对白':'当前对白')+
      speaker(other,battle?'饱饱':history?'机构当班接续员':'程望')+'</div>'+
      '<div class="dialogueText" tabindex="0"><p>'+text('正文从独立文本表加载。发言身份、正文、滚动区和继续按钮分别占位。')+'</p>'+
      '<p>'+text('这段文字用于检查换行和长段滚动。当前对白的继续操作保留在下方。')+'</p></div>'+
      '<div class="dialogueFoot">'+chip(history?'只读':'阅读中')+btn(history?'返回':'继续',history?'return':'continue','primary')+'</div>',
      'dialogue '+(battle?'battleDialog':''));
  };
  const demoNotes={
    hud:'导航图标、HUD 状态、框和文字分别接入原组件。菜单项由真实页面路由控制。',
    dialogue:'普通对白保留主角在左、实际发言人在右。正文独立滚动，继续保持在外层。',
    battle:'战斗对白有自己的边框和状态层。饱饱用脑内声音的专属符号。',
    history:'历史按保存的发言身份展示。代班头像与原岗位人物分开。',
    menu:'菜单底图、按钮框、禁用原因和说明可分别替换。选项与返回行为由原菜单提供。',
    items:'物品图、选中、焦点、可放入与拒绝原因独立。每张卡保留原展示位置。',
    recruit:'查看新绘制的职业造型样例。正式随机人物仍用实际保存外观。'
  };
  const demos={
    hud:()=>scene('location:camp')+
      skin('<strong>'+text('适者生存')+'</strong>'+[
        ['地图','map'],['制作','craft'],['休息','rest'],['编队','formation'],['整备','equipment'],['成长','growth'],
        ['目标','goal'],['日志','log'],['对白','history'],['菜单','menu']].map(([n,i])=>btn(n,i)).join(''),'navDemo')+
      skin(speaker('actor:p0','主角')+meter('生命',75)+meter('水分',55)+meter('清醒',83),'hudDemo'),
    dialogue:()=>dialog('dialogue'),battle:()=>dialog('battle'),history:()=>dialog('history'),
    menu:()=>scene('location:camp')+skin('<h2>'+text('暂停菜单')+'</h2><div class="buttons">'+
      btn('继续','continue','primary')+btn('动画速度','watch')+btn('系统说明','help')+btn('返回标题','return')+
      btn('当前操作不可用','disabled','disabled')+'</div>','menuDemo'),
    items:()=>scene('location:camp')+skin('<h3>'+text('物品与拖放反馈')+'</h3><div class="slots">'+
      [['item:clean_water','默认',''],['item:canned_food','选中','selected'],['item:bandage','键盘焦点','focus'],
       ['item:wood','可放入','allow'],['item:pot','容量不足','danger']].map(x=>stateCard(...x)).join('')+'</div>','cardDemo'),
    recruit:identityUI
  };
  function updatePose(){
    const pose=$('pose');
    if(!pose)return;
    const template=$('stage').querySelector('[data-identity=template]').value||'legacy_neutral';
    pose.innerHTML=rowArt('recruitTemplate:'+template);
  }
  function showDemo(){
    const kind=$('demoKind').value;
    $('stage').innerHTML=demos[kind]();
    $('layerNote').textContent=demoNotes[kind];
    updatePose();
  }
  $('demoKind').addEventListener('change',showDemo);
  $('readingMode').addEventListener('change',()=>{document.body.dataset.reading=$('readingMode').value;});
  $('stage').addEventListener('change',updatePose);
  const fieldsUi=[...$('layers').querySelectorAll('[data-layer-toggle]')];
  const mode=(preset,id)=>preset==='all'||(preset==='art'?['scene','subject'].includes(id):!['scene','subject'].includes(id));
  function applyLayers(){
    for(const el of fieldsUi)$('stage').dataset[el.dataset.layerToggle]=el.checked?'on':'off';
    $('stage').dataset.ui=fieldsUi.some(el=>el.checked&&!['scene','subject'].includes(el.dataset.layerToggle))?'on':'off';
    for(const b of $('layers').querySelectorAll('[data-preset]')) b.setAttribute('aria-pressed',String(fieldsUi.every(el=>el.checked===mode(b.dataset.preset,el.dataset.layerToggle))));
  }
  $('layers').addEventListener('click',event=>{
    const button=event.target.closest('[data-preset]');
    if(!button)return;
    for(const field of fieldsUi)field.checked=mode(button.dataset.preset,field.dataset.layerToggle);
    applyLayers();
  });
  $('layers').addEventListener('change',applyLayers);
  $('category').innerHTML='<option value="">全部类别</option>'+sorted.map(k=>'<option value="'+k+'">'+names[k]+'（'+C.counts[k]+'）</option>').join('');
  $('category').value='item';
  const limit=36;
  let page=0;
  let selected=null;
  function inspect(row){
    selected=row?.key??null;
    if(!row){$('inspect').textContent='没有符合条件的素材';return;}
    const binding=resolve(row.asset);
    const data=[
      ['分类',names[row.category]],['稳定 ID',row.id],['绑定键',row.key],['开发入口',row.entry],
      ['重制状态',row.rebuildStatus],['来源',row.source],['图层',row.layers?.join(' / ')],['切图',binding?.crop?.join(', ')],['头像裁窗',binding?.portraitCrop?.join(', ')],
      ['文件',binding?.file],['继承素材',row.asset?.ref],['替换说明',row.design||row.display||binding?.review]
    ].filter(([,v])=>v);
    $('inspect').innerHTML=rowArt(row.key)+'<h3>'+esc(row.name)+'</h3><dl>'+
      data.map(([k,v])=>'<dt>'+esc(k)+'</dt><dd>'+esc(v)+'</dd>').join('')+'</dl>'+
      (binding?.file?'<a href="'+esc(binding.file)+'" target="_blank" rel="noopener">'+(binding.crop?'打开图集原文件':'打开素材文件')+'</a>':'');
    for(const b of $('grid').querySelectorAll('[data-key]')) b.setAttribute('aria-pressed',String(b.dataset.key===selected));
  }
  function render(){
    const k=$('category').value,q=$('query').value.toLowerCase().trim();
    const hits=C.assets.filter(row=>(!k||row.category===k)&&(!q||[row.name,row.id,row.entry,row.source,row.kind].some(x=>String(x??'').toLowerCase().includes(q))));
    const max=Math.max(1,Math.ceil(hits.length/limit));
    page=Math.min(Math.max(page,0),max-1);
    const visible=hits.slice(page*limit,(page+1)*limit);
    $('grid').innerHTML=visible.map(row=>'<button type="button" class="tile" data-key="'+esc(row.key)+'" aria-pressed="false">'+
      (row.rebuildStatus&&!row.asset?'<span class="art pending">待复核</span>':rowArt(row.key,row.category==='frame'?'frameThumb':''))+
      '<b>'+esc(row.name)+'</b><small>'+esc(row.id)+'</small></button>').join('');
    $('pageInfo').textContent=(page+1)+' / '+max+' 页　'+hits.length+' 项';
    $('total').textContent=C.assets.length+' 个视觉绑定 · 重绘 '+C.rebuild.approved+'/'+C.rebuild.expected;
    $('prev').disabled=page<=0;$('next').disabled=page+1>=max;
    inspect(visible.find(row=>row.key===selected)??visible[0]);
  }
  $('query').addEventListener('input',()=>{page=0;render();});
  $('category').addEventListener('change',()=>{page=0;render();});
  $('prev').addEventListener('click',()=>{page--;render();});
  $('next').addEventListener('click',()=>{page++;render();});
  $('grid').addEventListener('click',event=>{
    const tile=event.target.closest('[data-key]');
    if(tile)inspect(byKey.get(tile.dataset.key));
  });
  showDemo();applyLayers();render();
})();
