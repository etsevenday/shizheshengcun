/* HTML 屏幕样板。场景清单提供来源与取景，界面组件由此独立组装。 */
const esc = value => String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const artBox = (id, cls = '', layer = 'art') => {
  const [key,x,y,w,h] = art[id];
  const file = artFile(key);
  let content;
  if (x === undefined) {
    content = '<img src="'+esc(file)+'" alt="">';
  } else {
    const squareSheet = key === 'I1' || key.startsWith('W');
    const [sw,sh] = atlases[key]?.size || (squareSheet ? [1448,1086] : [1672,941]);
    const clipWidth=w*sw/100, clipHeight=h*sh/100;
    const bounds = [x*sw/100,y*sh/100,clipWidth,clipHeight].join(' ');
    const align = cls==='unitArt'?' preserveAspectRatio="xMidYMax meet"':'';
    content = '<svg viewBox="0 0 '+clipWidth+' '+clipHeight+'"'+align+' aria-hidden="true"><svg width="'+clipWidth+'" height="'+clipHeight+'" viewBox="'+bounds+'" overflow="hidden">'+
      '<image href="'+esc(file)+'" width="'+sw+'" height="'+sh+'"/></svg></svg>';
  }
  return '<span class="artbox '+cls+'" data-layer="'+layer+'" data-art="'+id+'" aria-hidden="true">'+content+'</span>';
};

const text = (value, cls='') => '<span class="'+cls+'" data-layer="text">'+esc(value)+'</span>';
const status = (value, tone='') => '<span class="badge '+tone+'" data-layer="status">'+esc(value)+'</span>';
const title = value => '<h3>'+text(value)+'</h3>';
const para = (value, cls='') => '<p class="'+cls+'">'+text(value)+'</p>';
const button = (value, action='', tone='') => '<button class="ink-button '+tone+'" type="button" '+(action ? 'data-jump="'+action+'"' : 'aria-disabled="true"')+' '+(tone==='primary'?'data-ink-action="primary"':'')+'>'+text(value)+'</button>';
const controls = (...buttons) => '<div class="buttons">'+buttons.join('')+'</div>';
const progress = (label, percent) => '<div class="meter" data-layer="status"><span>'+esc(label)+'</span><i style="--fill:'+percent+'%"></i></div>';
const panel = (label, html, cls='') => '<section class="ink-panel surface '+cls+'">'+(label?title(label):'')+html+'</section>';
const rows = (list) => '<ul class="dataRows">'+list.map(row=>'<li>'+text(row[0])+ (row[1]?status(row[1],row[2]||''):'')+'</li>').join('')+'</ul>';
const card = (key, note='', state='', opts={}) => '<article class="ink-card tile '+(opts.cls||'')+'" '+(opts.selected?'data-ink-selected="true"':'')+(opts.drop?' data-ink-drop="'+opts.drop+'"':'')+'>'+
  artBox(key,'tileArt')+'<div class="ink-card-title">'+text(opts.name||items[key]||key)+'</div>'+
  (note?status(note,state):'')+(opts.extra||'')+'</article>';
const cards = (list, cls='') => '<div class="tiles '+cls+'">'+list.map(spec=>card(...(Array.isArray(spec)?spec:[spec]))).join('')+'</div>';
const portrait = (key,name,role='') => '<div class="person">'+artBox(key,'portrait')+'<div>'+text(name,'personName')+(role?status(role):'')+'</div></div>';
const illustration = (key, cls='') => artBox(key,'illustration '+cls);
const cover = key => artBox(key,'cover','scene');
const tray = (list=['water','can','bandage','lighter','wood'],caption='随身 · 五根卡') => '<section class="ink-panel surface tray">'+title(caption)+cards(list,'five')+'</section>';
const ground = () => tray([['wood','地面'],['metal','地面'],['fiber','地面'],['pot','地面']],'地面');

const navList = [['区域地图','P07a'],['手工制作','P06a'],['休息','P08c'],['编队','P08a'],['整备','P03a'],['成长','P08b'],['生存目标','P08d'],['生存提示','P08e'],['行动日志','P08f'],['对话记录','P09c'],['系统说明','P08g']];
const nav = () => '<header class="gameNav"><div class="brand">'+text('适者生存')+'</div><div class="navButtons">'+navList.map(([name,id])=>button(name,id)).join('')+'</div>'+button('菜单','PXb')+'</header>';
const head = (key,caption) => '<div class="strip">'+artBox(key,'trim','ornament')+text(caption)+'</div>';
const hud = () => panel('生存',portrait('face','主角','第 3 日　16:10')+[
  ['生命',78],['饱食',58],['水分',65],['体力',72],['清醒',89],['精神',64]
].map(([name,value])=>progress(name,value)).join('')+rows([['负重','正常'],['随身','五根卡']]),'hud');
const rail = (label, contents) => '<section class="ink-panel surface rail"><h3>'+text(label)+'</h3>'+contents+'</section>';
const area = (key, label, desc) => '<div class="land">'+cover(key)+'<div class="landCaption">'+text(label)+status(desc)+'</div></div>';
const tracks = (variant='home') => {
  const work = variant==='encounter'
    ? cards([['common','敌踪','danger'],['special','威胁','danger'],['wolf','远处异动','danger']])
    : cards([['fire','加工位'],['shelter','设施'],['catchment','设施'],['wood','资源点','',{selected:variant==='resource'}]]);
  return '<div class="tracks">'+rail('场景',area('camp','营地外围','当前位置')+area('M1','幸存者营区','已知地点'))+
    rail('作业',work)+rail('地面',cards([['wood','地面'],['metal','地面'],['fiber','地面'],['pot','地面']]))+
    rail('随身',cards([['water','×2','',{selected:variant==='transfer',drop:variant==='transfer'?'allow':''}],['can','×1'],['bandage','×1'],['lighter','×1'],['axe','×1']],'five'))+'</div>';
};
const sideContent = {
  home:()=>illustration('fire')+para('营地火堆')+rows([['加工位','锅 / 食材'],['当前操作','查看']])+button('查看工位','P02a','primary'),
  resource:()=>illustration('wood')+para('林地木材')+rows([['来源','当前地点'],['动作','按实际可用性']])+button('查看制作来源','P06b'),
  encounter:()=>illustration('common')+para('发现敌踪','warning')+rows([['接敌','待选择'],['风险','以实际敌踪为准','danger']])+button('查看战斗样板','P05a','primary'),
  transfer:()=>illustration('water')+rows([['数量','选择实例'],['合法落点','可放入'],['拒绝示例','容量不足','danger']])+button('返回主界面','P01a'),
  fire:()=>illustration('fire')+rows([['工位 1','锅'],['工位 2','食材'],['当前反馈','燃烧中']])+button('查看锅内容','P02b','primary'),
  pot:()=>illustration('pot')+rows([['锅内液体','净水'],['锅内食材','炖汤']])+progress('加工进度样例',60)+button('返回工位','P02a'),
};
const world = variant => nav()+head(variant==='encounter'?'dangerTrim':'ironTrim','营地外围　／　当前地点')+
  '<div class="gameGrid">'+hud()+tracks(variant)+panel('对象详情',sideContent[variant](),'inspector')+'</div>'+
  (['fire','pot'].includes(variant)?'<aside class="floating ink-panel surface">'+
  (variant==='fire'?title('火堆 · 工位')+cards([['pot','加工位 1'],['stew','加工位 2']])+controls(button('查看锅','P02b','primary'),button('关闭','P01a')):
    title('锅内容 · 实例详情')+cards([['water','锅内水'],['stew','锅内食材']])+controls(button('返回火堆','P02a'),button('关闭','P01a')) )+'</aside>':'');

const equipment = variant => {
  const left = [['hood','头部'],['glove','手部'],['axe','武器一'],['gun','武器二']];
  const right = [['coat','上衣'],['pants','下装'],['boot','鞋靴']];
  const detail = variant==='compare' ? cards([['coat','当前'],['vest','候选','',{selected:true}]])+rows([['旧物去向','本人随身 / 当前地面'],['最终容量','显示真实预演']])+controls(button('确认装备','','primary'),button('取消','P03a')) :
    variant==='handoff' ? cards([['wood','本人随身'],['pot','仅根卡']])+rows([['接收人','请选择'],['容量与重量','按双方实际预演']])+controls(button('确认交接','','primary'),button('取消','P03a')) :
    cards([['vest','地面候选'],['rifle','远程候选'],['pack','外观参考']])+controls(button('同槽比较','P03b'),button('交接','P03c'));
  return nav()+head('ironTrim','整备　／　当前成员')+
    '<div class="equipGrid">'+hud()+'<div>'+panel('',portrait('face','主角','当前成员')+controls(button('查看队伍','P08a'),button('返回','P01a')),'memberbar')+
    panel('装备槽', '<div class="slots">'+cards(left,'slotColumn')+illustration('survivor','fullFigure')+cards(right,'slotColumn')+'</div>','equipBody')+
    tray(['water','can','bandage','wood','pot'])+'</div>'+panel(variant==='compare'?'当前 / 候选':variant==='handoff'?'交接预览':'候选与来源',detail,'inspector')+'</div>';
};

const food = variant => nav()+head('paperTrim','队伍随身　／　同行食饮')+
  '<div class="gameGrid">'+hud()+tracks('home')+
  panel('队伍',portrait('scoutFace','同行者','在场 · 随身展开')+cards([['can','随身根卡'],['pot','随身根卡'],['water','锅内子行']])+button('打开饮水来源','P04b'),'inspector')+'</div>'+
  '<aside class="floating wide ink-panel surface">'+title(variant==='drink'?'给同行者饮用':'给同行者食用')+
  cards(variant==='drink'?[['water','随身水'],['pot','锅内水']]:[['can','点开的来源'],['stew','食物外观参考']])+
  rows(variant==='drink'?[['目标','已手动选择（示例）'],['来源','两人同类实例'],['不可用批次','显示真实原因','danger']]:[['目标','请选择'],['来源','保留所点实例'],['确认','待选目标']])+
  controls(button('确认','','primary'),button('取消','P01a'))+'</aside>';

const battleUnits = (wild=false) => {
  const left=[['fightMedic','我方 4'],['fightWorker','我方 3'],['fightScout','我方 2'],['survivor','我方 1']];
  const right=wild?[['fightWolf','敌方 1'],['fightBoar','敌方 2'],['fightWolf','敌方 3'],['fightBoar','敌方 4']]:
    [['enemy','敌方 1'],['fightRunner','敌方 2'],['fightHeavy','敌方 3'],['fightTall','敌方 4']];
  return '<div class="units">'+[...left,...right].map(([key,name],i)=>'<div class="unit '+(i>=4?'foe':'ally')+(i===4?' targeted':'')+'">'+artBox(key,'unitArt')+
    '<div class="unitMeta">'+text(name)+progress(i>=4?'生命':'生命 / 行动',i===4?58:82)+'</div></div>').join('')+'</div>';
};
const skillbar = variant => panel('武器与招式',controls(button('近战页','P05a'),button('远程页','P05b'))+
  cards(variant==='skills'?[['gun','可用','',{name:'射击'}],['rifle','当前行动点不足','danger',{name:'精准射击'}],['ammo','锁级','danger',{name:'快速装填'}],['gun','锁级','danger',{name:'压制'}],['ammo','锁级','danger',{name:'战术装填'}]]:
    [['axe','当前选择','',{name:'挥斧',selected:true}],['blade','可用','',{name:'短刃'}],['club','自用','',{name:'招架'}],['axe','锁级','danger',{name:'横扫'}],['blade','锁级','danger',{name:'进阶技'}]],'five'),'skillbar');
const battle = variant => {
  const brief = variant==='skills'?rows([['锁级','技能条件未满足','danger'],['临时不足','当前行动点不足','danger']]):
    rows([['已查看','敌方 1'],['瞄准','独立目标反馈'],['位置','按实际站位']]);
  let modal = '';
  if(variant==='supplies') modal=title('物资查看 · 只读')+para('战斗中只读，使用与转移入口隐藏。')+cards(['water','bandage','meds','pot','can'])+controls(button('查看装备','P05f'),button('返回战斗','P05a'));
  if(variant==='battleEquip') modal=title('装备查看 · 只读')+portrait('face','当前行动者')+cards(['hood','glove','axe','gun','coat','pants','boot'])+controls(button('查看物资','P05c'),button('返回战斗','P05a'));
  if(variant==='commands') modal=title('通用动作与医疗')+cards([['bandage','本人自用'],['meds','按可用条件']])+rows([['通用','防御 / 调息 / 撤退'],['结束','需确认']])+button('返回战斗','P05a');
  if(variant==='battleDialogue') modal=dialogueBody('战斗中对白','当前发言人',true);
  return head('dangerTrim','遭遇战　／　高速检查站')+
    '<div class="battleTop">'+status('第 2 回合')+Array.from({length:8},(_,i)=>status((i<4?'我方 ':'敌方 ')+(i%4+1),i<4?'':'danger')).join('')+button('战斗菜单','P05d')+'</div>'+
    '<div class="arena">'+cover('checkpoint')+battleUnits(variant==='wild')+'</div>'+
    '<div class="battleConsole">'+panel('当前行动者',portrait('tense','主角','行动点 · 示例')+controls(button('医疗','P05d'),button('物资查看','P05c'),button('装备查看','P05f')))+skillbar(variant)+
    panel('目标与行动',brief+controls(button('通用','P05d'),button('结束行动','P05d','primary')))+ '</div>'+
    (modal?'<aside class="floating battleFloat ink-panel surface">'+modal+'</aside>':'');
};

const craft = variant => nav()+head('paperTrim','手工制作　／　配方与来源')+
  '<div class="three">'+panel('分类与配方',rows([['手工','简易斧'],['设施','遮蔽棚'],['工具','绳索']])+cards(['axe','rope','shelter']))+
  panel('简易斧 · 材料',illustration('axe')+cards([['wood','有来源'],['metal','缺料','danger'],['fiber','待重验']]))+
  panel(variant==='source'?'材料来源 · 金属':'本次制作',variant==='source'?area('M1','已知来源','只定位')+rows([['范围','按已公开来源'],['结果','未取料、未移动']])+controls(button('区域地图','P07a'),button('返回配方','P06a','primary')):
    rows([['耗时','显示实际耗时'],['缺料','金属','danger']])+controls(button('查看来源','P06b'),button('开工','','primary')))+ '</div>'+tray();

const cityNames={M1:['幸存者营区','campNpc','P07b'],M2:['教会圣所','churchNpc','P07c'],M3:['科学院封存区','scienceNpc','P07d']};
const cityTabs=()=>controls(...Object.values(cityNames).map(([n,,id])=>button(n,id)));
const region = () => nav()+head('ironTrim','区域地图　／　公开节点')+
  '<div class="cityGrid">'+panel('区域地图','<div class="regionMap"><svg viewBox="0 0 400 200" aria-hidden="true"><path d="M50 130L150 55L250 132L350 60M150 55L350 60" /></svg>'+
  ['当前位置','北侧路口','已知地点','待查看'].map((name,i)=>'<div class="mapNode n'+i+'" data-layer="status"><b>◆</b><span>'+name+'</span></div>').join('')+'</div>'+cityTabs())+
  panel('下一路段',rows([['起点','当前位置'],['目标','查看已知相邻边'],['反馈','只读预览']])+button('返回当前地点','P01a'))+'</div>'+ground()+tray();
const city = id => {
  const [name,npc]=cityNames[id];
  return nav()+head('ironTrim',name+'　／　主城鸟瞰')+
    '<div class="cityGrid">'+panel('', '<div class="cityCanvas">'+cover(id)+['查看入口','建筑节点','岗位方向'].map((label,i)=>
    '<div class="mapNode cityNode n'+i+'" data-layer="status"><b>◆</b><span>'+label+'</span></div>').join('')+'</div>'+cityTabs())+
    panel('地点信息',illustration(npc,'serviceFigure')+rows([['选择建筑','预览下一段'],['岗位服务','到场后可查看']])+controls(button('岗位样板','P07e'),button('回当前地点','P01a')))+ '</div>'+tray(undefined,'随身 · 只读');
};
const service = () => nav()+head('paperTrim','岗位服务　／　三方联系')+
  '<div class="three factions">'+Object.entries(cityNames).map(([key,[name,npc,id]])=>
    panel(name,area(key,name,'地点参考')+illustration(npc,'serviceFigure')+rows([['当班身份','由实际岗位提供'],['服务与招募','按真实条件']])+button('查看鸟瞰',id))).join('')+'</div>';

const recordData = {
  party:['队伍与编队','在场成员', [['主角','位置 1'],['同行者','位置 2'],['同行者','位置 3'],['同行者','位置 4']],['survivor','scout','worker']],
  growth:['成长与熟练度','当前人物',[['近战','显示实际熟练度'],['远程','显示实际熟练度'],['词条','查看生效说明']],['axe','rifle','coat']],
  rest:['休息','本地休息条件',[['休息点','按真实设施'],['恢复','按预演显示'],['中断','显示专属原因']],['shelter','fire','water']],
  goals:['生存目标','目标进度',[['当前目标','进行中'],['已完成项','折叠查看'],['下一步','沿任务现状']],['wood','catchment','shelter']],
  tips:['生存提示','当前生存主题',[['补给与容器','提示'],['作业工具','提示'],['受伤与治疗','提示']],['water','rope','bandage']],
  help:['系统说明','主题列表',[['物品与容器','阅读'],['战斗','阅读'],['键盘操作','阅读']],['torch','rope','bandage']],
  log:['行动日志','时间与事件',[['查看物品','记录样例'],['作业反馈','记录样例'],['返回地点','记录样例']],['wood','pot','fire']],
  history:['对话历史','发言人索引',[['主角','历史条目'],['营区联系人','历史条目'],['岗位当班人','历史条目']],['face','medicFace','worried']]
};
const record = variant => {
  const [name,label,entries,assets]=recordData[variant];
  const people = variant==='party'||variant==='history';
  return nav()+head(variant==='party'?'ironTrim':'paperTrim',name+'　／　'+label)+
    '<div class="recordGrid">'+panel(label,(people?portrait('face','主角','当前查看'):'')+rows(entries)+button('返回场景','P01a'))+
    panel(name,(variant==='growth'?illustration('survivor','growthFigure'):'')+
      cards(assets.map((id,i)=>[id,entries[i][1],'',{name:people?entries[i][0]:items[id]}]))+
      (variant==='history'?para('历史阅读与当前对白分离，保留原发言身份。'):para('正文、条件与反馈分别呈现，内容按正式数据加载。')))+ '</div>'+
    (['growth','party'].includes(variant)?'':ground())+tray();
};

function dialogueBody(name,speaker,battleView=false) {
  return title(name)+'<div class="dialoguePeople">'+portrait('face','主角')+portrait(battleView?'tense':'medicFace',speaker)+'</div>'+
    '<div class="dialogueScroll" tabindex="0">'+para('发言人、当前正文和操作提示使用独立控件。此处展示长文本时的排版与滚动区域。')+
    para('实际对白从正式文本表加载，身份按发言人保存。长段落换行时，底部继续按钮保持可见。')+
    para('结束阅读后回到当前上下文。历史回看独立打开。')+'</div>'+
    controls(button('继续',battleView?'P05a':'P01a','primary'),button('历史','P09c'));
}
const dialogue = () => nav()+head('paperTrim','营地　／　剧情阅读')+'<div class="gameGrid">'+hud()+tracks()+panel('当前人物',portrait('medicFace','营区当班人'))+'</div>'+
  '<aside class="floating battleFloat ink-panel surface">'+dialogueBody('当前对白','营区当班人')+'</aside>';

const shells={
  start:['适者生存','营地入口','M1',['新游戏','继续','玩法说明'],['PXf','P01a','P08g']],
  pause:['菜单','当前会话','camp',['继续','系统说明','返回标题'],['P01a','P08g','PXa']],
  end:['本局结算','结束状态示例','M1',['查看行动日志','重新开始','返回标题'],['P08f','P01a','PXa']],
  notice:['撤出与状态提示','异常状态示例','checkpoint',['查看敌踪','回到场景'],['P01c','P01a']]
};
const shell = variant => {
  const [name,label,scene,actions,links]=shells[variant];
  return head(variant==='notice'||variant==='end'?'dangerTrim':'ironTrim',label)+
    '<div class="shellHero">'+cover(scene)+panel('', '<h2>'+text(name)+'</h2>'+para(label)+
    (variant==='notice'?status('未执行撤出 · 展示原因位置','danger'):'')+
    (variant==='pause'?rows([['设置','敌方行动动画速度']])+button('查看行动暂停样板','PXe'):'')+
    controls(...actions.map((a,i)=>button(a,links[i],i===0?'primary':''))),'shellMenu')+'</div>';
};
const actionPause = () => nav()+head('ironTrim','当前行动　／　暂停')+
  '<div class="gameGrid">'+hud()+tracks()+panel('行动反馈',progress('保留原行动进度',52)+rows([['状态','暂停'],['原预留','继续保留']]))+'</div>'+
  '<aside class="floating wide ink-panel surface">'+title('行动已暂停')+rows([['原因','读取真实暂停原因'],['剩余','读取原进度']])+
  cards([['water','随身补给'],['can','随身补给'],['lighter','按原条件显示']])+controls(button('继续行动','P01a','primary'),button('查看退出','PXd'))+'</aside>';
const prologue = () => head('paperTrim','新局　／　序章')+
  '<div class="shellHero">'+cover('M1')+panel('', '<h2>'+text('序章')+'</h2>'+portrait('face','当前叙事视角')+
  para('序章叙事分段呈现，正式内容按 N02。')+button('继续','P01a','primary'),'shellMenu')+'</div>';

const renderers = {
  home:()=>world('home'), resource:()=>world('resource'), encounter:()=>world('encounter'),
  transfer:()=>world('transfer'), fire:()=>world('fire'), pot:()=>world('pot'),
  gear:()=>equipment('gear'), compare:()=>equipment('compare'), handoff:()=>equipment('handoff'),
  food:()=>food('food'), drink:()=>food('drink'),
  battle:()=>battle('battle'), skills:()=>battle('skills'), supplies:()=>battle('supplies'),
  commands:()=>battle('commands'), wild:()=>battle('wild'), battleEquip:()=>battle('battleEquip'),
  craft:()=>craft('craft'), source:()=>craft('source'), map:region,
  cityCamp:()=>city('M1'), cityChurch:()=>city('M2'), cityLab:()=>city('M3'), service,
  party:()=>record('party'), growth:()=>record('growth'), rest:()=>record('rest'),
  goals:()=>record('goals'), tips:()=>record('tips'), help:()=>record('help'), log:()=>record('log'), history:()=>record('history'),
  dialogue, battleDialogue:()=>battle('battleDialogue'),
  start:()=>shell('start'), pause:()=>shell('pause'), end:()=>shell('end'), notice:()=>shell('notice'), actionPause, prologue
};
