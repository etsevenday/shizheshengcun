/* 仅离线界面样板。图片取景以原图百分比表示 [x, y, 宽, 高]。 */
const groups = [
  ['P01','场景与主界面'], ['P02','火堆与加工'], ['P03','人物整备'],
  ['P04','同行食饮'], ['P05','战斗'], ['P06','制作'],
  ['P07','地图、三城与服务'], ['P08','队伍、成长与记录'],
  ['P09','对白与历史'], ['PX','启动、暂停与结算']
];

const ref = Object.fromEntries([
  ['I1','01-item','01-食物与饮水','食饮卡与锅内内容'],
  ['I2','01-item','02-医疗与求生工具','医疗物资与作业工具'],
  ['I3','01-item','03-材料与设施卡','材料、火堆、遮蔽棚与集水器'],
  ['U1','02-UI底图','01-锈铁框架','常态与装备边框方向，取顶部锈铁装饰'],
  ['U2','02-UI底图','02-旧布与档案纸','制作与档案阅读方向，取纸条装饰'],
  ['U3','02-UI底图','03-暗红刻痕与冷钢','战斗与危险方向，取冷钢警示装饰'],
  ['L1','03-UI布局','01-营地求生主界面','常态左右栏与四轨构图'],
  ['L2','03-UI布局','02-四对四战斗界面','八站位与下方操作台构图'],
  ['L3','03-UI布局','03-整备与人物界面','七槽、角色、候选比较构图'],
  ['C1','04-人物画风','01-幸存者全身造型','生存者与同行站位造型'],
  ['C2','04-人物画风','02-三方联系人造型','三城岗位角色方向'],
  ['C3','04-人物画风','03-头像与情绪表现','头像、人物栏与对白表情'],
  ['M1','05-势力鸟瞰','01-幸存者营区','营区鸟瞰与菜单背景'],
  ['M2','05-势力鸟瞰','02-教会圣所地区','教会鸟瞰'],
  ['M3','05-势力鸟瞰','03-科学院封存地区','科学院鸟瞰'],
  ['W1','06-武器防具图标','01-近战武器','近战装备、技能与制作图形参考'],
  ['W2','06-武器防具图标','02-远程武器与配件','远程武器与配件图形参考'],
  ['W3','06-武器防具图标','03-防具与穿戴装备','七槽与防具候选比较'],
  ['E1','07-怪物类型','01-常见感染者','常见感染者敌踪与站位参考'],
  ['E2','07-怪物类型','02-特殊感染者','特殊感染者敌踪与站位参考'],
  ['E3','07-怪物类型','03-野外动物与异变轮廓','异变动物敌踪与站位参考']
].map(([id,dir,name,use]) => [id,{name,use,file:'../参考原图/'+dir+'/'+name+'.png'}]));

const art = {
  water:['../UI分层样件/art/item-clean-water.png'],
  fire:['../UI分层样件/art/facility-fire-lit.png'],
  survivor:['../UI分层样件/art/actor-survivor-sample.png'],
  camp:['art/camp-outskirts.png'],
  checkpoint:['art/battle-checkpoint.png'],
  enemy:['art/enemy-wanderer.png'],
  can:['I1',1,1,23,20], meat:['I1',77,2,22,19], berry:['I1',51,35,23,20],
  pot:['I1',26,68,22,25], stew:['I1',51,68,22,25],
  bandage:['I2',1,1,23,30], meds:['I2',1,35,23,30], lighter:['I2',51,35,23,30],
  torch:['I2',1,68,23,31], rope:['I2',26,68,23,31],
  wood:['I3',1,5,31,33], metal:['I3',34,5,32,33], fiber:['I3',67,5,32,33],
  shelter:['I3',34,54,32,36], catchment:['I3',67,54,32,36],
  worker:['C1',34,0,31,99], scout:['C1',67,0,25,99],
  campNpc:['C2',1,1,30,98], churchNpc:['C2',32,1,29,98], scienceNpc:['C2',62,1,30,98],
  face:['C3',14,2,23,30], tense:['C3',39,2,22,30], scoutFace:['C3',14,35,23,30],
  medicFace:['C3',14,68,23,30], worried:['C3',39,68,22,30],
  axe:['W1',1,68,23,31], blade:['W1',26,1,23,31], club:['W1',1,35,23,30],
  gun:['W2',76,1,23,31], rifle:['W2',51,35,23,31], ammo:['W2',26,68,23,31],
  coat:['W3',1,1,23,30], vest:['W3',51,1,23,30], hood:['W3',26,35,23,30],
  glove:['W3',76,35,23,30], boot:['W3',1,68,23,31], pants:['W3',26,68,23,31],
  pack:['W3',76,68,23,31],
  common:['E1',0,9,23,84], runner:['E1',42,15,33,77],
  special:['E2',41,1,31,95], tall:['E2',0,5,21,90],
  wolf:['E3',20,28,27,48], boar:['E3',45,25,31,51],
  ironTrim:['U1',1,1,98,17], paperTrim:['U2',19,11,50,12], dangerTrim:['U3',4,0,93,11],
  M1:['M1'], M2:['M2'], M3:['M3']
};

const atlases = {
  battleAlly:{file:'art/allies-battle-atlas.png',size:[1536,1024]},
  battleFoe:{file:'art/infected-battle-atlas.png',size:[1536,1024]},
  battleWild:{file:'art/wild-battle-atlas.png',size:[1536,1024]}
};
Object.assign(art,{
  fightMedic:['battleAlly',2,0,29,100], fightWorker:['battleAlly',33,0,33,100],
  fightScout:['battleAlly',67,0,29,100], fightRunner:['battleFoe',1,2,32.8,96],
  fightHeavy:['battleFoe',34.3,2,33.7,96], fightTall:['battleFoe',69,2,29,96],
  fightWolf:['battleWild',1,22,47,56], fightBoar:['battleWild',50,22,49,56]
});
const artFile = key => ref[key]?.file || atlases[key]?.file || key;

const items = {
  water:'净水', can:'罐头', bandage:'绷带', lighter:'打火机', wood:'木材', metal:'金属',
  fiber:'纤维', pot:'金属锅', stew:'炖汤', meat:'生肉', berry:'野果', meds:'药品',
  torch:'手电筒', rope:'绳索', fire:'火堆', shelter:'遮蔽棚', catchment:'集水器',
  axe:'简易斧', blade:'短刀', club:'木棒', gun:'手枪', rifle:'长枪', ammo:'弹药',
  coat:'工作衣', vest:'防护背心', hood:'兜帽', glove:'手套', boot:'靴子',
  pants:'长裤', pack:'背包', common:'常见感染者', runner:'迅捷感染者',
  special:'特殊感染者', tall:'高个变体', wolf:'野外动物', boar:'异变动物'
};

const scenes = [
  ['P01a','P01','营地主界面','home',['L1','U1','I1','I2','I3','C3','M1'],'GameScreen · Hud · ItemShelf'],
  ['P01b','P01','野外作业与资源详情','resource',['L1','U1','I3','I2'],'ObjectCard · DetailPanel'],
  ['P01c','P01','敌踪与接敌查看','encounter',['U3','E1','E2','E3'],'ObjectCard · EnemyDetail'],
  ['P01d','P01','物品卡、拖放与转移','transfer',['U1','I1','I2','I3'],'ItemShelf · CardPop · DropQuantity'],
  ['P02a','P02','火堆与两个加工位','fire',['L1','I3','U1'],'FireCard · FireDetail'],
  ['P02b','P02','锅内容与加工反馈','pot',['I1','I3','U1'],'PotCard · CentralProgress'],
  ['P03a','P03','七槽人物整备','gear',['L3','C1','C3','W1','W2','W3','I2','U1'],'GearOverlay · CharacterPanel'],
  ['P03b','P03','同槽比较与旧物去向','compare',['L3','W3','W1','U1'],'GearOverlay · PreviewView'],
  ['P03c','P03','随身根卡与交接','handoff',['L3','I3','I1','U1'],'GearOverlay · ItemShelf · TransferArrow'],
  ['P04a','P04','队伍随身展开与食饮盘','food',['I1','C3','U2'],'PartyPanel · FeedPop'],
  ['P04b','P04','锅内容饮水与来源选择','drink',['I1','C3','U2'],'PotCard · PartyPanel · FeedPop'],
  ['P05a','P05','战斗站位与瞄准','battle',['L2','U3','C1','E1','E2','W1','W2'],'BattleScreen · BattlePanel'],
  ['P05b','P05','技能锁级与临时不可用','skills',['L2','U3','W1','W2'],'BattleScreen · StatusPop'],
  ['P05c','P05','战斗物资查看','supplies',['L2','U3','I1','I2'],'BattleSupplies'],
  ['P05d','P05','通用动作、医疗与回合确认','commands',['L2','U3','I2'],'BattleScreen · BattlePanel'],
  ['P05e','P05','异变动物遭遇','wild',['L2','U3','C1','E3'],'BattleScreen · EnemyDetail'],
  ['P05f','P05','战斗装备只读查看','battleEquip',['L2','U3','W1','W2','W3'],'BattleEquip'],
  ['P06a','P06','制作配方与材料','craft',['U2','I3','W1','I2'],'CraftOverlay · ItemRow'],
  ['P06b','P06','缺料、来源与返回','source',['U2','I3','W1'],'CraftOverlay · MapOverlay'],
  ['P07a','P07','区域地图与下一路段','map',['U1','M1','M2','M3'],'MapOverlay · JourneyOverlay'],
  ['P07b','P07','幸存者营区鸟瞰','cityCamp',['U1','M1','C2'],'CityOverview · CityServiceOverlay'],
  ['P07c','P07','教会圣所鸟瞰','cityChurch',['U1','M2','C2'],'CityOverview · CityServiceOverlay'],
  ['P07d','P07','科学院封存区鸟瞰','cityLab',['U1','M3','C2'],'CityOverview · CityServiceOverlay'],
  ['P07e','P07','岗位服务与招募','service',['U2','C2','C3','M1','M2','M3'],'CityServiceOverlay · RecruitPanel'],
  ['P08a','P08','队伍名册与编队','party',['U1','C1','C3'],'PartyPanel · FormationOverlay'],
  ['P08b','P08','人物成长与熟练度','growth',['U2','C1','C3','W1','W2'],'GrowthOverlay · CharacterPanel'],
  ['P08c','P08','休息与条件','rest',['U2','I3','C3'],'RestOverlay · SafehouseOverlay'],
  ['P08d','P08','生存目标','goals',['U2','I3'],'GoalsOverlay'],
  ['P08e','P08','生存提示','tips',['U2','I2'],'TipsOverlay'],
  ['P08f','P08','行动日志','log',['U2','C3','I3'],'LogOverlay'],
  ['P08g','P08','系统说明','help',['U2','I2'],'HelpOverlay'],
  ['P09a','P09','营地对白与阅读锁','dialogue',['U2','C2','C3','M1'],'DialogueLayer · DialogueLines'],
  ['P09b','P09','战斗中对白覆盖','battleDialogue',['U3','L2','C3','E2'],'DialogueLayer · BattleScreen'],
  ['P09c','P09','对话历史与发言人','history',['U2','C2','C3'],'DialogueLogOverlay'],
  ['PXa','PX','标题、开始与继续','start',['U1','M1','C1'],'StartScreen · PrologueCards'],
  ['PXb','PX','暂停、设置与返回','pause',['U1','L1','M1'],'MenuOverlay · PauseLayer'],
  ['PXc','PX','结算与重新开始','end',['U3','M1','C3'],'EndScreens'],
  ['PXd','PX','探索撤出与异常提示','notice',['U3','L1','E1'],'ExpeditionAbort · OverflowOverlay · StatusPop'],
  ['PXe','PX','行动暂停与补给','actionPause',['U1','L1','I1','I2'],'PauseLayer · CentralProgress'],
  ['PXf','PX','新局序章','prologue',['U2','M1','C3'],'PrologueCards']
].map(([id,group,title,view,sources,components]) => ({id,group,title,view,sources,components}));
