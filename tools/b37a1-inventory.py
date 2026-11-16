# B37a1：由运行时清单 web/Survival.Web/Art/ink-v2.json 与配置 data/config.v0.6.json 生成
# 《doc/验收/重墨V2 UI重做 v1.0/素材清单.json》：每个游戏对象用哪个稳定 ID、取到哪张图、属于哪一类（正式／批准旧图／占位缺项／现有图待答）。
# 规则与 web/Survival.Web/Services/ArtCatalog.cs 一致（同义、缺项、批准旧地景、Q837 占位、Q883 现有图）；只读，不改游戏文件。
# B37a2 起加「人物 portrait」「顶栏键面 glyph」两组。
# 用法：python tools/b37a1-inventory.py
import json, re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MAN = json.loads((ROOT / 'web/Survival.Web/Art/ink-v2.json').read_text(encoding='utf-8'))
CFG = json.loads((ROOT / 'data/config.v0.6.json').read_text(encoding='utf-8'))
OUT = ROOT / 'doc/验收/重墨V2 UI重做 v1.0/素材清单.json'

SPRITES, SCENES = MAN['sprites'], MAN['scenes']
APPROVED_OLD = {'field': 'art/v08/field.png', 'stream': 'art/v08/stream.png', 'woods': 'art/v08/woods.png', 'village': 'art/v08/village.png',
                'hardware': 'art/v10/hardware.png', 'clinic': 'art/v08/clinic.png', 'scrapyard': 'art/v08/scrapyard.png'}
Q837 = {'a01', 'a02', 'a03', 'b02', 'b03', 'o03'} | {f'w{i:02d}' for i in range(1, 35) if i not in (19, 23, 27)}
MISSING = {'item:spoiled_matter': '腐败物', 'status:dying': '濒死', 'skillFamily:stealth': '潜行', 'skillFamily:willpower': '意志',
           'document:relief_page_village': '公开救助残页'}

S_FORMAL, S_OLD, S_HOLD, S_MISSING, S_Q883 = '正式（ink-v2）', '批准沿用旧地景（16号稿）', '统一占位（Q837 缺项）', '现有占位（Q838 缺项）', '现有图（Q883 待答）'


def sprite_row(obj, name, key, shown_as=None):
    if key in SPRITES:
        s = SPRITES[key]
        return {'object': obj, 'name': name, 'key': key, 'status': S_FORMAL, 'file': 'art/ink-v2/' + s['file'], 'crop': s['crop']}
    return {'object': obj, 'name': name, 'key': key, 'status': S_MISSING, 'shownAs': shown_as}


out = {'source': 'a9b93f0 ' + MAN['source'], 'sourceSha256': MAN['sourceSha256'], 'generatedBy': 'tools/b37a1-inventory.py', 'groups': {}}
G = out['groups']

G['物品 item'] = [sprite_row(it['id'], it['name'], 'item:' + it['id'], '原“材料”分类占位矢量图') for it in CFG['items']]
G['敌人 enemy'] = [sprite_row(e['id'], e['name'], 'enemy:' + e['id']) for e in CFG['enemies']]
G['技能 skill'] = [sprite_row(n['id'], n['name'], 'skill:' + n['id']) for n in CFG['skillNodes']]
G['熟练度门类 skillFamily'] = [sprite_row(s['id'], s['name'], 'skillFamily:' + s['id'], '无图位（门类页属 B36）') for s in CFG['skills']]
G['资源点 resource'] = [sprite_row(f"{r['locationId']}:{r['nodeId']}", r['name'], f"resource:{r['locationId']}:{r['nodeId']}") for r in CFG['resources']]
G['战斗动作 action'] = [sprite_row(a['id'], a['id'], 'action:' + a['id']) for a in CFG['combatActions']]

status_src = (ROOT / 'web/Survival.Web/Services/StatusView.cs').read_text(encoding='utf-8')
G['状态 status'] = [sprite_row(k, k, 'status:' + k) for k in dict.fromkeys(re.findall(r'"(status_[a-z_]+)"', status_src))]
G['状态 status'].append({'object': 'dying', 'name': '濒死', 'key': 'status:dying', 'status': S_MISSING, 'shownAs': 'status:status_unknown（清单内统一状态占位）'})

fac = []
for st in CFG['workshops']['stations']:
    fac.append(sprite_row(st['id'], st['name'], 'facility:' + st['id']))
for obj, name, key in [('fire_lit', '火堆（点燃）', 'facility:fire_lit'), ('fire_out', '火堆（未点）', 'facility:fire_out'), ('shelter', '遮蔽棚', 'facility:shelter'),
                       ('bed', '床垫', 'facility:bed'), ('safehouse', '安全屋', 'facility:safehouse'), ('workshops', '营地工坊建造卡（Q838 同义）', 'facility:workbench'),
                       ('camping', '野营布', 'facility:camping'), ('charcoal', '烧炭', 'facility:charcoal'), ('stove', '展开柴炉（Q838 同义）', 'item:folding_stove')]:
    fac.append(sprite_row(obj, name, key))
G['设施 facility'] = fac

G['生命类小图标 glyph'] = [sprite_row(k, k, 'glyph:' + k) for k in ['health', 'satiety', 'hydration', 'energy', 'wakefulness', 'morale', 'immunity', 'virus']]
G['序章文书 document'] = [
    {'object': 'village_south_sign', 'name': '南口路标', 'key': 'glyph:route', 'status': S_FORMAL + '（Q838 同义）', 'file': 'art/ink-v2/' + SPRITES['glyph:route']['file'], 'crop': SPRITES['glyph:route']['crop']},
    {'object': 'relief_page_village', 'name': '公开救助残页', 'key': 'document:relief_page_village', 'status': S_MISSING, 'shownAs': '统一占位矢量图 placeholder'},
]
G['徒手 fists'] = [{'object': 'fists', 'name': '徒手（Q838 同义）', 'key': 'skillFamily:unarmed', 'status': S_FORMAL, 'file': 'art/ink-v2/' + SPRITES['skillFamily:unarmed']['file'], 'crop': SPRITES['skillFamily:unarmed']['crop']}]
G['Q883 待答图位'] = [
    {'object': 'backpack', 'name': '顶栏负重', 'status': S_Q883, 'shownAs': 'art/v08/creatures.png 第 14 格'},
    {'object': 'outdoor', 'name': '帮助“世界时钟与昼夜”', 'status': S_Q883, 'shownAs': '现有矢量图'},
    {'object': 'placeholder', 'name': '帮助“按键／Esc／中央推进条／保存与继续”四题、战斗未知指令兜底', 'status': S_Q883, 'shownAs': '统一占位矢量图'},
    {'object': 'slot_empty', 'name': '制作帮助“空加工位”', 'status': S_Q883, 'shownAs': '现有矢量图'},
    {'object': 'repair', 'name': '修理配方', 'status': S_Q883, 'shownAs': '现有矢量图'},
]

# B37a2：人物肖像（portraits 单张 webp）与全身图（sprites 同键裁窗）按身份接线，规则同 web/Survival.Web/Services/ActorArt.cs
PORTRAITS = MAN['portraits']
S_Q884 = '已随发未接（Q884 待答，随机招募同伴仍用原 img/portraits/unknown.svg）'


def person_row(obj, name, key, use):
    if key in PORTRAITS and key in SPRITES:
        return {'object': obj, 'name': name, 'key': key, 'status': S_FORMAL, 'use': use, 'portrait': 'art/ink-v2/' + PORTRAITS[key],
                'file': 'art/ink-v2/' + SPRITES[key]['file'], 'crop': SPRITES[key]['crop']}
    return {'object': obj, 'name': name, 'key': key, 'status': S_MISSING, 'use': use, 'shownAs': 'glyph:unknown'}


people = [person_row('p0', '主角', 'actor:p0', '对白、对话记录、战斗、整备、人物详情（Q839 A）'),
          person_row('baobao', '饱饱', 'actor:baobao', '对白、对话记录')]
for n in CFG['city']['npcs']:
    people.append(person_row(n['npcId'], n['name'], 'actor:' + n['npcId'], '在岗：人物卡、对白、对话记录'))
    people.append(person_row(n['npcId'] + '#duty', n['name'] + '岗位代班', 'duty:' + n['npcId'], '代班：人物卡、对白、对话记录（Q811 A）'))
v = CFG['prologue']['visitor']
people.append(person_row(v['npcId'], v['name'], 'actor:' + v['npcId'], '序章来客：人物卡、对白'))
for c in CFG['party']['namedCompanions']:
    people.append(person_row(c['actorId'], c['name'], 'actor:' + c['actorId'], '固定同行者：邀约卡、对白、整备、战斗、人物详情'))
people.append(person_row('escort', '无职业保底护送员', 'recruitTemplate:legacy_neutral', '整备、战斗、人物详情（Q835 补问 B）'))
people.append(person_row('unknown', '查不到身份', 'glyph:unknown', '中性标记（Q811 A 不猜）'))
for k in sorted(x for x in PORTRAITS if x.startswith('recruitTemplate:') and x != 'recruitTemplate:legacy_neutral'):
    people.append({'object': k.split(':', 1)[1], 'name': '名册职业模板', 'key': k, 'status': S_Q884, 'portrait': 'art/ink-v2/' + PORTRAITS[k]})
G['人物 portrait'] = people
G['顶栏键面 glyph'] = [sprite_row(k, k, 'glyph:' + k) for k in ['map', 'craft', 'rest', 'log', 'formation', 'equipment', 'growth', 'goal', 'warning', 'dialogue', 'help', 'menu']]

scenes = []
for loc in CFG['locations']:
    lid, name = loc['id'], loc['name']
    if lid in APPROVED_OLD:
        scenes.append({'object': lid, 'name': name, 'status': S_OLD, 'file': APPROVED_OLD[lid]})
    elif lid.split('_')[0] in Q837:
        scenes.append({'object': lid, 'name': name, 'status': S_HOLD, 'file': 'scenes/placeholder.svg'})
    else:
        scenes.append({'object': lid, 'name': name, 'status': S_FORMAL, 'file': 'art/ink-v2/' + SCENES[lid]})
G['地点场景 scene'] = scenes

summary = {}
for g, rows in G.items():
    c = {}
    for r in rows: c[r['status']] = c.get(r['status'], 0) + 1
    summary[g] = c
out['summary'] = summary
OUT.write_text(json.dumps(out, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False, indent=1))
