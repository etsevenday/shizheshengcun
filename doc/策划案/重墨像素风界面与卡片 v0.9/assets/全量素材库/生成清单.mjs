import {readFileSync,writeFileSync,mkdirSync,readdirSync,existsSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve,dirname,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import {svg,itemSvg,glyphSvg,glyphKeys,skillSvg,resourceSvg,facilityShape} from './像素图形.mjs';
import {uiFrames,frameSvg,textureDesigns,textureSvg} from './界面部件.mjs';
import {personSvg,baobaoSvg} from './角色合成.mjs';

const base=dirname(fileURLToPath(import.meta.url));
const root=resolve(process.argv[2]??resolve(base,'../../../../..'));
const read=p=>JSON.parse(readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const configFile=join(root,'data/config.v0.6.json');
const cfg=read(configFile);
const atlasIndex=read(join(base,'生成图集.json'));
const artByKey=new Map();
const catalog=[];
const seen=new Set();
const nameOf=row=>row.name??row.title??row.label??row.id;
const normalized=id=>id.replaceAll(':','--').replaceAll('/','--');
const fileAt=(dir,id,svgContent)=>{
  if(!/^[a-zA-Z0-9_-]+$/.test(normalized(id))) throw new Error('Unsafe art ID '+id);
  const rel='svg/'+dir+'/'+normalized(id)+'.svg';
  mkdirSync(dirname(join(base,rel)),{recursive:true});
  writeFileSync(join(base,rel),svgContent,'utf8');
  return {file:rel,format:'svg',alpha:true};
};
const add=(category,id,name,asset,more={})=>{
  const key=category+':'+id;
  if(seen.has(key)) throw new Error('Duplicate key '+key);
  seen.add(key);
  catalog.push({key,category,id,name:name||id,asset,...more});
  return key;
};
const ref=(category,id)=>({ref:category+':'+id});
const scoped=(...values)=>values.join(':');
const previous='../UI场景全览/art/';
const sample='../UI分层样件/art/';
const original='../参考原图/05-势力鸟瞰/';
const previousArt={
  camp:{file:previous+'camp-outskirts.png',format:'png',alpha:false},
  battle:{file:previous+'battle-checkpoint.png',format:'png',alpha:false},
  p0:{file:sample+'actor-survivor-sample.png',format:'png',alpha:true},
  fire:{file:sample+'facility-fire-lit.png',format:'png',alpha:true},
  water:{file:sample+'item-clean-water.png',format:'png',alpha:true}
};
for(const source of atlasIndex) {
  if(!existsSync(join(base,source.file))) throw new Error('Missing atlas '+source.file);
  for(const cell of source.cells) {
    if(artByKey.has(cell.id)) throw new Error('Duplicate atlas cell '+cell.id);
    artByKey.set(cell.id,{file:source.file,format:'atlas',alpha:source.alpha,size:source.size,crop:cell.crop,
      ...(cell.face?{portraitCrop:cell.face}:{}),source:source.id,review:cell.review??'造型待美术审阅'});
  }
}
const cell=id=>{
  const asset=artByKey.get(id);
  if(!asset) throw new Error('No atlas binding for '+id);
  return asset;
};

for(const item of cfg.items) {
  const design=itemSvg(item);
  const native=fileAt('item',item.id,design.source);
  const asset=item.id==='clean_water'?previousArt.water:native;
  add('item',item.id,item.name,asset,{kind:item.kind,shape:design.shape,native,display:'透明图标 · 32 格 / 常规显示 48–96 CSS px',
    source:'data.items',entry:'Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay'});
}
for(const item of cfg.gear) add('gear',item.id,item.name,ref('item',item.id),{slot:item.slot,source:'data.gear',entry:'GearOverlay · BattleEquip'});
for(const weapon of cfg.weapons) {
  const itemAsset=seen.has('item:'+weapon.id)?ref('item',weapon.id):fileAt('weapon',weapon.id,skillSvg(weapon.skillId,1));
  add('weapon',weapon.id,weapon.name,itemAsset,{skillId:weapon.skillId,source:'data.weapons',entry:'GearOverlay · BattleScreen'});
}
for(const skill of cfg.skills) add('skillFamily',skill.id,skill.name,fileAt('skill-family',skill.id,skillSvg(skill.id)),
  {kind:skill.category,source:'data.skills',entry:'GrowthOverlay · CharacterPanel'});
for(const skill of cfg.skillNodes) add('skill',skill.id,skill.name,fileAt('skill',skill.id,skillSvg(skill.skillId,skill.level,skill.name)),
  {kind:skill.kind,family:skill.skillId,level:skill.level,source:'data.skillNodes',entry:'Icons.Combat · BattleScreen / GrowthOverlay'});
const actions={basic_attack:['普通攻击','blade'],guard:['防御','shield'],breathe:['调息','bolt'],
  switch_weapon:['切换武器','rifle'],swap:['交换站位','arrow'],reload:['装填','cartridge'],bandage:['包扎','patch'],
  desperate_punch:['应急拳击','hand'],retreat:['撤退','map'],end_turn:['结束行动','clock'],
  use_antibiotics:['使用抗生素','tablet'],use_antiviral_cure:['使用特效药','vial']};
for(const action of cfg.combatActions){
  const [name,shape]=actions[action.id]??[action.id,'target'];
  add('action',action.id,name,fileAt('action',action.id,glyphSvg(shape)),
    {source:'data.combatActions',entry:'BattleScreen · BattlePanel · CardActions'});
}
for(const id of Object.keys(cfg.statuses)) add('condition',id,({bleed:'流血',stagger:'失衡',slow:'迟缓'})[id]??id,ref('glyph',id),{source:'data.statuses',entry:'StatusIcons · BattleScreen'});
const hudStatuses={
  status_shaken:['惊魂未定','warning','hazard'],status_diarrhea:['腹泻','water','steel'],
  status_bleed:['流血','water','hazard'],status_bandage_cd:['包扎冷却','patch','medical'],
  status_baobao:['饱饱','crystal','olive'],status_infected:['感染','crystal','violet'],
  status_antibiotic:['药效','tablet','medical'],status_antibiotic_cd:['药物冷却','tablet','steel'],
  status_bandage_virus_cd:['病毒相关包扎冷却','patch','violet'],status_zero:['状态归零','warning','hazard'],
  status_low_energy:['低精力','battery','amber'],status_low_morale:['低士气','heart','rust'],
  status_panic:['恐慌','heart','hazard'],status_unknown:['未分类状态','eye','muted']
};
for(const [id,[name,shape,palette]] of Object.entries(hudStatuses))
  add('status',id,name,fileAt('status',id,svg(shape,palette==='muted'?'iron':palette)),
    {source:'web/Survival.Web/Services/Icons.Status.cs',entry:'StatusIcons · StatusPop · 计时角标独立'});

for(const enemy of cfg.enemies) add('enemy',enemy.id,enemy.name,cell(enemy.id),
  {kind:enemy.family,source:'data.enemies',entry:'BattleScreen / EnemyDetail · enemy config ID',direction:'left',baseline:'feet',display:'主体 90–230 CSS px，头像取构图裁窗'});
for(const animal of cfg.animalCarcasses){
  const id=animal.enemyId??animal.id;
  if(!id) continue;
  const item=animal.carcassItemId??animal.itemId;
  add('carcassRule',id,item&&cfg.items.find(x=>x.id===item)?.name||id,item&&seen.has('item:'+item)?ref('item',item):null,
    {source:'data.animalCarcasses',entry:'FoodCard · ItemDetail · 尸体处理'});
}

add('actor','p0','主角',previousArt.p0,{source:'GameState.PlayerId',entry:'整备 / 对白 / 队伍 / 战斗',portrait:{file:previousArt.p0.file,size:[1024,1536],relativeCrop:[24,4,52,38]},direction:'right'});
add('actor','baobao','饱饱',fileAt('actor','baobao',baobaoSvg()),{source:'N01 / N02',entry:'DialogueLayer · 脑内声音专属符号',kind:'voice',portraitOnly:true});
const named=[
  ...cfg.party.namedCompanions.map(x=>({id:x.actorId,name:x.name,kind:'companion',role:x.affiliation,source:'data.party.namedCompanions'})),
  ...cfg.city.npcs.map(x=>({id:x.npcId,name:x.name,kind:'city',role:x.role,faction:x.factionId,location:x.locationId,source:'data.city.npcs'})),
  {id:cfg.prologue.visitor.npcId,name:cfg.prologue.visitor.name,kind:'visitor',role:cfg.prologue.visitor.role,source:'data.prologue.visitor'}
];
for(const actor of named) add('actor',actor.id,actor.name,cell(actor.id),{...actor,
  entry:actor.kind==='companion'?'GearOverlay · PartyPanel · BattleScreen · DialogueLayer':'CityServiceOverlay · CityOverview · DialogueLayer',
  direction:actor.kind==='companion'?'right':'neutral'});
for(const [i,npc] of cfg.city.npcs.entries()){
  const id=npc.npcId;
  const fields={template:npc.factionId,dutyMark:i+1,skinTone:['浅肤色','中浅肤色','中等肤色','中深肤色','深肤色'][i%5],
    hairStyle:['短发','齐耳发','齐肩发','束发','卷发','剃短发'][i%6],
    hairColor:['黑色','深棕色','灰白色','浅棕色'][i%4],faceDetail:['眉尾浅疤','雀斑','眼角细纹','无明显特征'][i%4]};
  add('duty',id,npc.role+' · 当班接续员',fileAt('duty',id,personSvg(fields)),{
    portrait:fileAt('duty-portrait',id,personSvg(fields,'portrait')),source:'data.city.npcs.states',
    entry:'实际代班状态 + 当前岗位 / 历史保存身份',faction:npc.factionId,location:npc.locationId,
    design:'独立机构肖像，与原人头像分开'});
}
for(const template of cfg.party.recruitment.templates) {
  const fields={template:template.id};
  add('recruitTemplate',template.id,template.name,fileAt('recruit',template.id,personSvg(fields)),{
    portrait:fileAt('recruit-portrait',template.id,personSvg(fields,'portrait')),source:'data.party.recruitment.templates',
    entry:'RecruitPanel · 动态人物合成',design:'只展示职业服装样例；正式绑定使用实际保存的外观'});
}
add('recruitTemplate','legacy_neutral','旧档未记录外观',fileAt('recruit','legacy_neutral',personSvg()),
  {portrait:fileAt('recruit-portrait','legacy_neutral',personSvg({},'portrait')),source:'Q180 / Q276',
    entry:'缺字段时只对缺项取中性部件；不猜测性别或肤色'});

const runtimeArt='../../../../../web/Survival.Web/wwwroot/art/';
const wildBase=id=>({file:runtimeArt+(id==='hardware'?'v10/':'v08/')+id+'.png',format:'png',alpha:false});
const places={
  hub:original+'01-幸存者营区.png',church:original+'02-教会圣所地区.png',academy:original+'03-科学院封存地区.png'
};
const locationPatterns=[
  [/诊|医|药|检测|观察|实验/, 'clinic'],[/溪|桥|河|湿地|湖|渠|滩|库岸|水库|水道/, 'stream'],
  [/林|山|谷|狩|沟|岩|荒径|高地/, 'woods'],[/农|果园|荒地|田|草/, 'field'],
  [/废料|水厂|泵|铸|铁|仓|货|工坊|矿|配电|设备/, 'scrapyard'],
  [/营|安全屋|校|安置/, 'camp'],[/公路|检查哨|路口|车站|车道|市区|商圈|城门/, 'battle']
];
const placeAsset=(place)=>{
  if(/^d0[1-8]$/.test(place.zone)) return {...cell(place.zone),review:'地牢区域底图 · 多节点共享'};
  if(places[place.zone]) return {file:places[place.zone],format:'png',alpha:false,review:'区域鸟瞰 · 建筑节点由程序叠加'};
  if(place.zone==='starter') return place.id==='camp'?previousArt.camp:wildBase(place.id);
  const which=locationPatterns.find(([regex])=>regex.test(place.name))?.[1]??'village';
  return which==='battle'?previousArt.battle:which==='camp'?previousArt.camp:wildBase(which);
};
for(const loc of cfg.locations) add('location',loc.id,loc.name,placeAsset(loc),{
  zone:loc.zone,kind:loc.nodeKind,source:'data.locations',entry:'ArtAssets.Scene / LocationCard / MapOverlay',
  layers:['scene','markers','routes','labels','status'],sharing:'同区域或地貌共用底图；不同节点由实际地图数据区分'});
for(const dungeon of cfg.map.dungeons) add('dungeon',dungeon.id,dungeon.name,cell(dungeon.id),
  {source:'data.map.dungeons',entry:'DungeonReset · JourneyOverlay · BattleScreen'});
for(const [id,name] of Object.entries(cfg.map.regionNames)) {
  const representative=cfg.locations.find(row=>row.zone===id);
  add('region',id,name,representative?ref('location',representative.id):ref('glyph','map'),
    {source:'data.map.regionNames',entry:'MapOverlay · 区域标签'});
}
for(const [id,name] of Object.entries(cfg.city.factions)){
  const style=({hub_survivor:['hub','wall','olive'],faction_church:['church','flame','rust'],faction_academy:['academy','eye','steel']})[id];
  add('faction',id,name,fileAt('faction',id,svg(style[1],style[2])),{backdrop:{file:places[style[0]],format:'png',alpha:false},
    source:'data.city.factions',entry:'CityOverview · CityServiceOverlay · DialogueLayer'});
}
for(const gate of cfg.map.gates) add('gate',gate.id,gate.label??gate.id,ref('glyph',gate.id==='open'?'route':'lock'),
  {source:'data.map.gates',entry:'MapOverlay · 实际通行状态'});
for(const [i,node] of cfg.resources.entries()) {
  const id=scoped(node.locationId,node.nodeId);
  const design=resourceSvg(node.name,i);
  add('resource',id,node.name,fileAt('resource',id,design.source),
    {location:node.locationId,nodeId:node.nodeId,shape:design.shape,kind:node.category,
      source:'data.resources',entry:'ObjectCard · DetailPanel · 稳定地点/资源点键'});
}
const facilities=new Map(Object.entries(facilityShape).map(([id,shape])=>[id,{id,shape,name:({
  hand:'手工',fire_lit:'火堆 · 燃烧',fire_out:'火堆 · 未燃',pot:'锅',bed:'铺垫',shelter:'遮蔽棚',
  safehouse:'安全屋',camping:'临时扎营',catchment:'集水设施',storage:'储藏设施',charcoal:'炭化'
})[id]??id}]));
for(const station of cfg.workshops.stations) facilities.get(station.id).name=station.name;
for(const item of facilities.values()){
  const native=fileAt('facility',item.id,svg(item.shape,item.id==='fire_lit'?'rust':'canvas'));
  add('facility',item.id,item.name,item.id==='fire_lit'?previousArt.fire:native,
    {native,source:'data.workshops / fire / safehouse / S09–S11',entry:'FacilityCard · FireCard · PotCard · DryingRackCard'});
}
for(const station of cfg.city.publicStations) add('publicStation',station.id,station.name,ref('facility',station.stationId),
  {source:'data.city.publicStations',location:station.locationId,entry:'PublicStationCard'});
for(const process of cfg.processes) add('process',process.id,process.name,
  process.outputId&&seen.has('item:'+process.outputId)?ref('item',process.outputId):ref('facility','pot'),
  {source:'data.processes',entry:'FireCard · PotCard · CentralProgress'});
for(const recipe of cfg.recipes){
  const outId=Object.keys(recipe.outputs??{})[0];
  const dst=recipe.facilityResult;
  const asset=outId&&seen.has('item:'+outId)?ref('item',outId):typeof dst==='string'&&facilities.has(dst)?ref('facility',dst):ref('glyph','recipe');
  add('recipe',recipe.id,recipe.name,asset,{station:recipe.station,output:outId??null,source:'data.recipes',entry:'CraftOverlay · Recipe 只读预览'});
}

for(const note of cfg.knowledge) add('knowledge',note.id,note.name,ref('glyph','knowledge'),
  {source:'data.knowledge',entry:'CraftOverlay · CityServiceOverlay'});
for(const [kind,list] of [['prologue',cfg.prologue.documents],['city',cfg.city.notices]]) for(const note of list)
  add('document',scoped(kind,note.id),nameOf(note),ref('glyph',note.id==='village_south_sign'?'route':'task'),
    {source:'data.'+(kind==='city'?'city.notices':'prologue.documents'),entry:'PrologueCards · CityServiceOverlay'});
for(const quest of cfg.quests.list) add('quest',quest.id,nameOf(quest),ref('frame','task'),
  {kind:quest.kind,source:'data.quests.list',entry:'GoalsOverlay · TaskCorner · CityServiceOverlay'});
for(const task of cfg.prologue.tasks) add('quest',scoped('opening',task.id),nameOf(task),ref('frame','task'),
  {source:'data.prologue.tasks',entry:'PrologueCards · TaskCorner · GoalsOverlay'});
for(const pack of cfg.expansionQuests.packs) add('knowledgePack',pack.id,nameOf(pack),ref('glyph','knowledge'),
  {source:'data.expansionQuests.packs',entry:'CityServiceOverlay · CraftOverlay · GoalsOverlay'});
for(const service of cfg.city.services) add('service',service.id,nameOf(service),
  ref('glyph',/rest/.test(service.kind)?'rest':/medical|clinic/.test(service.kind)?'health':'service'),
  {source:'data.city.services',location:service.locationId,kind:service.kind,entry:'CityServiceOverlay · AppraisalPanel · RecruitPanel'});
for(const affix of cfg.affixes) add('affix',affix.id,affix.name,ref('glyph','growth'),
  {source:'data.affixes',kind:affix.group,entry:'AttrDetail · GearOverlay · ItemDetail'});

const speakerIds=new Map(named.map(actor=>[actor.name,actor.id]));
speakerIds.set('你','p0'); speakerIds.set('饱饱','baobao');
const dutyEvent=new Map(cfg.city.npcs.flatMap(actor=>Object.values(actor.states).map(event=>[event,actor.npcId])));
for(const [index,line] of cfg.story.dialogues.entries()){
  const actor=speakerIds.get(line.speaker);
  const duty=line.speaker==='机构当班接续员'?dutyEvent.get(line.eventId):null;
  if(line.speaker&&!actor&&!duty) throw new Error('Unmapped story speaker '+line.speaker+' '+line.eventId);
  add('dialogueLine',line.id??scoped(line.eventId,String(index)),line.speaker||'旁白',
    actor?ref('actor',actor):duty?ref('duty',duty):null,
    {eventId:line.eventId,source:'data.story.dialogues',entry:'DialogueLayer / DialogueLogOverlay',frame:'frame:dialogue',
      layer:actor==='baobao'?'inner-voice':actor||duty?'portrait':'narration'});
}
for(const [ns,sets] of [['city',cfg.city.dialogueSets],['quest',cfg.quests.dialogueSets],['opening',cfg.prologue.dialogueSets]]){
  for(const set of sets) add('dialogueSet',scoped(ns,set.id),nameOf(set),ref('frame','dialogue'),
    {eventId:set.id,source:'data.'+(ns==='opening'?'prologue':ns==='quest'?'quests':ns)+'.dialogueSets',entry:'DialogueLayer / DialogueLogOverlay'});
}

const glyphNames={
  health:'生命',satiety:'饱食',hydration:'水分',energy:'精力',wakefulness:'清醒',morale:'士气',immunity:'免疫',virus:'感染',
  bleed:'流血',stagger:'失衡',slow:'迟缓',fear:'恐惧',panic:'恐慌',disabled:'锁定',warning:'风险',selected:'选中',
  drop_allow:'可放入',drop_deny:'不可放入',progress:'进度',loading:'读取中',focus:'焦点',drag:'拖放',transfer:'转移',
  close:'关闭',add:'增加',rest:'休息',map:'地图',dialogue:'对白',log:'日志',goal:'目标',help:'说明',menu:'菜单',
  battle:'战斗',aim:'瞄准',equipment:'整备',craft:'制作',formation:'编队',growth:'成长',pause:'暂停',
  continue:'继续',fresh:'新游戏',save:'存档',return:'返回',history:'历史',storage:'仓储',search:'查看',task:'任务',
  known:'已知地点',unknown:'未知地点',route:'路线',destination:'目标地点',camp:'营地',dungeon:'地牢',recruit:'招募',service:'服务',
  watch:'时间',expand:'展开',collapse:'折叠',recipe:'配方',knowledge:'知识',foot:'行走',weapon:'武器',armor:'护甲',
  utility:'工具',all:'全部',lock:'封锁',trap:'陷阱'
};
for(const id of glyphKeys) add('glyph',id,glyphNames[id],fileAt('glyph',id,glyphSvg(id)),{source:'UI 视觉部件',entry:'StatusIcons · VitalIcon · 菜单栏'});
for(const [id,[name,palette]] of Object.entries(uiFrames)) add('frame',id,name,fileAt('frame',id,frameSvg(id,palette)),
  {slice:16,source:'UI 视觉部件',entry:'materials.css · CSS border-image',display:'96 格 / 保持透明中心'});
for(const [id,[name,hex]] of Object.entries(textureDesigns)) add('texture',id,name,fileAt('texture',id,textureSvg(id,hex)),
  {repeat:true,source:'UI 视觉部件',entry:'CSS background-image'});
const componentGroups={
  dialogue:['DialogueLayer','DialogueLines','DialogueLogOverlay','PrologueCards'],
  menu:['StartScreen','MenuOverlay','PauseLayer','EndScreens','HelpOverlay','TipsOverlay'],
  map:['MapOverlay','JourneyOverlay','CityOverview','LocationStrip','LocationCard','ExpeditionSeal','ExpeditionInfo','ExpeditionAbort','DungeonReset'],
  battle:['BattleScreen','BattlePanel','BattleOverlay','BattleSupplies','BattleEquip','EnemyDetail','TargetPicker'],
  equipment:['GearOverlay','DuplicateWeaponOverlay','CharacterPanel','GrowthOverlay','FormationOverlay','PartyPanel','RecruitPanel'],
  craft:['CraftOverlay','DryingOverlay','DryingRackCard','FacilityCard','PublicStationCard','FireDetail','FireCard','PotCard','IgniteMenu','SafehouseOverlay','SafehouseCard'],
  record:['LogOverlay','GoalsOverlay','TaskCorner','CityServiceOverlay','AppraisalPanel','RestOverlay'],
  card:['ObjectCard','ItemShelf','ItemRow','ItemDetail','FoodCard','CardPop','CardActions','TransferArrow','DropQuantity','OverflowOverlay','FeedPop','AnchorPop','ActionPops','DetailPanel','AttrDetail','PreviewView'],
  feedback:['StatusPop','CentralProgress','VitalIcon','StatusIcons','AccountIcon'],
  shell:['Root','GameScreen','Hud']
};
const uiKits={
  dialogue:['dialogue','paper','dialogue','背景 / 遮罩 / 肖像 / 框 / 文本 / 继续'],
  menu:['menu','iron','menu','背景 / 遮罩 / 按钮框 / 状态 / 动态文字'],
  map:['map_node','paper','map','地景 / 路线 / 节点 / 状态 / 标签'],
  battle:['skill','danger','battle','背景 / 站位主体 / 目标 / HUD / 提示'],
  equipment:['equipment','iron','equipment','主体 / 七槽 / 选中 / 数值 / 候选'],
  craft:['panel','iron','craft','主体 / 台面框 / 原料状态 / 进度 / 动作'],
  record:['history','paper','log','边框 / 列表 / 当前条目 / 文字 / 返回'],
  card:['card','iron','drag','底 / 插画 / 边框 / 状态 / 真实文字'],
  feedback:['tooltip','shadow','warning','容器 / 状态图形 / 原因 / 按钮'],
  shell:['panel','iron','menu','场景 / HUD / 四轨 / 导航 / 全局反馈']
};
const componentDir=join(root,'web/Survival.Web/Components');
const components=readdirSync(componentDir).filter(x=>x.endsWith('.razor')).sort();
for(const filename of components){
  const id=filename.slice(0,-6);
  const group=Object.keys(componentGroups).find(key=>componentGroups[key].includes(id));
  if(!group) throw new Error('Unclassified component '+filename);
  const [frame,texture,glyph,layers]=uiKits[group];
  add('component',id,id,ref('frame',frame),{kind:group,texture:'texture:'+texture,glyph:'glyph:'+glyph,
    layers:layers.split(' / '),source:'web/Survival.Web/Components/'+filename,entry:filename});
}

const byKey=new Map(catalog.map(row=>[row.key,row]));
for(const row of catalog){
  if(row.asset?.ref&&!byKey.has(row.asset.ref)) throw new Error('Unbound asset reference '+row.asset.ref);
  if(row.asset?.file&&!existsSync(join(base,row.asset.file))) throw new Error('Missing art '+row.asset.file);
}
const counts=Object.fromEntries([...new Set(catalog.map(x=>x.category))].map(category=>[category,catalog.filter(x=>x.category===category).length]));
const identity=cfg.party.recruitment.identity;
const payload={
  schemaVersion:1,project:'适者生存',scope:'现有策划及运行配置的视觉绑定。素材候选与界面样板，需实际游戏验收。',
  config:'data/config.v0.6.json',configHash:createHash('sha256').update(readFileSync(configFile)).digest('hex'),
  builtAt:'2026-11-09',counts,identity:{fields:['skinTone','hairStyle','hairColor','faceDetail'],
    skinTones:identity.skinTones,hairStyles:identity.hairStyles,hairColors:identity.hairColors,faceDetails:identity.faceDetails},
  fonts:read(join(base,'字库来源.json')),
  visualStates:['默认','悬停','焦点','选中','不可用','危险','执行','可放入','不可放入'],
  artKeyRules:{runtime:'保留配置 ID 与身份。卡片、状态、文字与美术各层独立。',
    presentation:['关闭场景层不改位置','关闭主体层不清数据','关闭状态层保留控件','动态名字与说明由现有控件渲染'],
    final:'位图和像素图均待正式美术复审、发布授权记录与游戏验收。地点按地貌分组共享。'},
  assets:catalog
};
const json=JSON.stringify(payload,null,2);
writeFileSync(join(base,'全量视觉清单.json'),json+'\n','utf8');
writeFileSync(join(base,'清单预览数据.js'),'window.VisualCatalog='+json+';\n','utf8');
const actorSource=readFileSync(join(base,'角色合成.mjs'),'utf8');
writeFileSync(join(base,'角色预览.js'),actorSource.replace(/^export /gm,''),'utf8');
console.log(JSON.stringify(counts,null,2));
