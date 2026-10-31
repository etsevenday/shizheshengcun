# -*- coding: utf-8 -*-
"""B7c 答复 Q62–Q74 的战斗配置导入（开发计划 v0.9 批次 B7c，R98）。

数值逐条取自策划案，不自补：
- doc/策划案/系统策划总册 v1.0/assets/20261031/combat/combat-adjudications.json（R03「2026-10-31 Q62—Q74战斗数值与操作裁定」同源）
  - capacity → config.scope.capacity（Q62 选 B：新局及无真实事实 C1；登记 C2、两种关键记录之一 C3、二者都有 C4；只增不减）；
    删除临时 scope.defaultCapacity=3（oldNoFactsDefault3Removed）。
  - actorBaseDefense → config.actorBaseDefense（Q63：24 名固定人物基础防御档、随机候选区间、护送档、位移装备抵抗只防拉扯/击退、
    外部合计封顶 40、稳态 +30）；主角档 p0 同步到 scope.basePlayerControlResist/basePlayerDodge/basePlayerArmor/basePlayerSpeed。
    队友与随机人物在 B8 才参战，本批只进配置不接入。
  - 防暴盾固定特性“稳固：位移抵抗+25个百分点”（A05 武器表原文）写成 weapons[].traitEffects.displaceResist=25，
    作为位移装备抵抗来源之一（重甲 15、稳固词条 10 已在 gear/affixes）。
  - accuracy → config.combatMath（Q67：我方可控角色攻击能力加基础 8；支付成本后 E<4 或 S≤20 统一一次 ×0.95，双低不叠乘，
    取代旧 lowEnergyAccuracyFactor/lowWakefulnessAccuracyFactor 两条 ×0.85；恐慌 0.9/0.8、失衡 0.9、速度惩罚不变）。

可重复运行，结果相同。
用法：python tools/import-b7c-combat.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261031", "combat", "combat-adjudications.json")

# combat-adjudications.json capacity.unlockEvents 的事实名 → 存档 Progress.Flags 里的事实（R03 本轮章写法 hub_registered / church_route_record / academy_safety_record）
CAPACITY_UNLOCKS = {
    "hub_registered": {"allOf": ["hub_registered"], "anyOf": [], "text": "营区登记"},
    "first_of_church_route_record_or_academy_safety_record": {
        "allOf": [], "anyOf": ["church_route_record", "academy_safety_record"], "text": "教会路线记录或学院安全记录之一"},
    "both_distinct_church_and_academy_records": {
        "allOf": ["church_route_record", "academy_safety_record"], "anyOf": [], "text": "教会路线记录与学院安全记录都有"},
}


def capacity(cfg, adj):
    cap = adj["capacity"]
    assert cap["oldNoFactsDefault3Removed"] and cap["newBattleCapacityLocked"] and cap["activeLegacyBattleCapacityPreserved"]
    scope = cfg["scope"]
    scope.pop("defaultCapacity", None)
    unlocks = []
    for ev in cap["unlockEvents"]:
        rule = CAPACITY_UNLOCKS[ev["fact"]]
        unlocks.append({"capacity": ev["capacity"], **rule})
    scope["capacity"] = {"newGame": cap["newAndNoMilestoneDefault"], "max": cap["max"], "unlocks": unlocks}


def defense(cfg, adj):
    d = adj["actorBaseDefense"]
    assert d["displacementGearOnlyStatuses"] == ["pull", "knockback"] and d["displacementGearAndSameNamedStateTakeStrongest"]
    p0 = next(x for x in d["fixedProfiles"] if x["actorId"] == "p0")
    scope = cfg["scope"]
    scope["basePlayerControlResist"] = p0["controlResist"]
    scope["basePlayerDodge"] = p0["evasion"]
    scope["basePlayerArmor"] = p0["baseArmor"]
    scope["basePlayerSpeed"] = p0["baseSpeed"]
    cfg["actorBaseDefense"] = {
        "fixedProfiles": [{k: x[k] for k in ("actorId", "controlResist", "evasion", "baseArmor", "baseSpeed", "role")} | ({"canRecruit": x["canRecruit"]} if "canRecruit" in x else {})
                          for x in d["fixedProfiles"]],
        "randomRecruit": d["randomRecruit"],
        "rb01Escort": d["rb01Escort"],
        "displacementGearOnlyStatuses": d["displacementGearOnlyStatuses"],
        "displacementGearAggregateCap": d["displacementGearAggregateCap"],
        "steadyBonus": d["steadyBonus"],
    }
    assert cfg["combatMath"]["steadyResistBonus"] == d["steadyBonus"]
    shield = next(w for w in cfg["weapons"] if w["id"] == "riot_shield")
    assert "位移抵抗+25" in shield["traitText"]
    shield.setdefault("traitEffects", {})["displaceResist"] = 25


def accuracy(cfg, adj):
    a = adj["accuracy"]
    assert not a["fatiguePenaltyStacks"] and a["playerSideActorsOnly"] and a["enemyAccuracyAssistance"] == 0
    m = cfg["combatMath"]
    assert m["staggerAccuracyFactor"] == a["stagger"]
    m.pop("lowEnergyAccuracyFactor", None)
    m.pop("lowWakefulnessAccuracyFactor", None)
    m["playerAccuracyAssistance"] = a["baseAssistance"]
    m["fatigueAccuracyFactor"] = a["newLowEnergyOrWakefulnessFactor"]
    m["playerAccuracyRating"] = "(weaponBase+3*L+8+gear+skillMarkInspire)*actionFactor*fatigueFactor*panicFactor*staggerFactor"
    pen = {p["minimum"]: p["accuracyRatingFactor"] for p in cfg["panic"]["penalties"]}
    assert pen == {60: a["panic60"], 90: a["panic90"]}, pen


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(ADJ, encoding="utf-8") as f:
        adj = json.load(f)
    capacity(cfg, adj)
    defense(cfg, adj)
    accuracy(cfg, adj)
    with open(CONFIG, "w", encoding="utf-8") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("scope.capacity", cfg["scope"]["capacity"])


if __name__ == "__main__":
    main()
