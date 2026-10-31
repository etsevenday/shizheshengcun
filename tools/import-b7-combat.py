# -*- coding: utf-8 -*-
"""B7 技能执行与敌人扩充的数据导入（开发计划 v0.9 批次 B7，R25/R26）。

数值逐条取自策划案，不自补：
- config.skillExec：50 个战斗/医疗主动技能的执行口径，来自
  doc/策划案/系统策划总册 v1.0/04 附录/A06 十级熟练度与全技能目录.md §2「全技能主表」效果列
  （AP、基准E、使用位→目标、冷却、额外物资仍读 config.skillNodes，B5 已原样导入）；
- config.weapons[].traitEffects：A05 §1 武器固定特性里挂在技能上的部分（锋口、扫切、快装、反击、破甲……），
  挂在多人/保护/换位上的（轻巧、包铁、护送、集合）留 B8，只存原文；
- config.enemies：旧 10 种补 A09 §1「敌人面板与行为」的控制抗性、己位→目标、AP、意图循环；
  新增 A09 §1 表里运行时还没有的 20 种（面板、族类、己位→目标、意图、控制抗性、附加掉落），
  受惊参数按 A09 §4「新敌受惊参数默认q35%/值6，迅捷类q55%/10、尖啸类q65%/16、精英q60%/14」；
  新敌不进旧 8 地点刷新池（开发假设 B7，新敌池在后续批次），只能经 ?dev=1 开发入口放出；
- config.animalCarcasses：野犬首领→灰狼尸体、暴躁野猪→野猪尸体（A09 §1“附加掉落”列）。
可重复运行，结果相同。
用法：python tools/import-b7-combat.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")


def st(status, **kw):
    """技能附带状态：status 为 stagger/slow/weak/mark/armor_break/knockback/cancel_charge/bleed。"""
    d = {"status": status}
    d.update(kw)
    return d


# ───────────── A06 §2 全技能主表（战斗/医疗 50 个） ─────────────
# type：attack 攻击 / guard 自身格挡类 / buff 自身增益 / ally 友方增益或调整 / medical 医疗 / reload 装填
# targetMode：single 单体 / pair 相邻双位（贯穿）/ self 自身 / ally 友方（单人时即自己）
# control=true：R03 §4 需命中后再判控制；positive=true：须正伤后才施加
SKILL_EXEC = [
    # 拳
    {"id": "unarmed_01", "type": "attack"},
    {"id": "unarmed_03", "type": "attack", "damageFactor": 0, "effects": [st("stagger", control=True)]},
    {"id": "unarmed_05", "type": "guard", "targetMode": "self", "guardCharges": 1},
    {"id": "unarmed_07", "type": "buff", "targetMode": "self", "cleanse": True, "moveBack": 1},
    {"id": "unarmed_10", "type": "attack", "hits": 2, "damageFactor": 0.85},
    # 刀剑
    {"id": "blade_01", "type": "attack"},
    {"id": "blade_03", "type": "attack", "damageFactor": 0.65, "effects": [st("bleed", positive=True)]},
    {"id": "blade_05", "type": "guard", "targetMode": "self", "guardCharges": 1, "counterFactor": 0.70},
    {"id": "blade_07", "type": "attack", "damageFactor": 1.20, "advance": 1},
    {"id": "blade_10", "type": "attack", "damageFactor": 1.70, "executeHpRatio": 0.30, "executeFactor": 1.20},
    # 斧
    {"id": "axe_01", "type": "attack"},
    {"id": "axe_03", "type": "attack", "damageFactor": 0.80, "effects": [st("armor_break", value=0.25, rounds=2)]},
    {"id": "axe_05", "type": "buff", "targetMode": "self", "chargeFactor": 1.35},
    {"id": "axe_07", "type": "attack", "damageFactor": 0.90, "effects": [st("knockback", value=1, control=True)]},
    {"id": "axe_10", "type": "attack", "damageFactor": 1.75, "removeGuard": 1},
    # 矛
    {"id": "spear_01", "type": "attack"},
    {"id": "spear_03", "type": "attack", "damageFactor": 0.60, "effects": [st("knockback", value=1, control=True)]},
    {"id": "spear_05", "type": "buff", "targetMode": "self", "braceCharges": 1, "braceMax": 2, "braceFactor": 0.75},
    {"id": "spear_07", "type": "attack", "targetMode": "pair", "damageFactor": 0.65},
    {"id": "spear_10", "type": "attack", "hits": 2, "damageFactor": 0.85},
    # 钝器
    {"id": "blunt_01", "type": "attack"},
    {"id": "blunt_03", "type": "attack", "damageFactor": 0.70, "effects": [st("stagger", control=True)]},
    {"id": "blunt_05", "type": "attack", "damageFactor": 0.50, "effects": [st("slow", control=True)]},
    {"id": "blunt_07", "type": "attack", "damageFactor": 0.50, "effects": [st("cancel_charge", control=True)]},
    {"id": "blunt_10", "type": "attack", "damageFactor": 1.60, "effects": [st("weak", value=0.8, rounds=2, control=True)]},
    # 弓弩
    {"id": "archery_01", "type": "attack"},
    {"id": "archery_03", "type": "attack", "damageFactor": 0.75, "effects": [st("slow", control=True, positive=True)]},
    {"id": "archery_05", "type": "attack", "damageFactor": 0, "ammo": 1, "effects": [st("mark", value=6, rounds=2)]},
    {"id": "archery_07", "type": "attack", "damageFactor": 1.20, "armorIgnore": 0.25},
    {"id": "archery_10", "type": "attack", "hits": 2, "damageFactor": 0.85, "ammo": 2, "minAmmo": 2, "noSingleShot": True},
    # 枪械
    {"id": "firearms_01", "type": "attack"},
    {"id": "firearms_03", "type": "attack", "damageFactor": 0.70, "effects": [st("weak", value=0.8, rounds=2)]},
    {"id": "firearms_05", "type": "reload", "targetMode": "self", "reloadFull": True},
    {"id": "firearms_07", "type": "attack", "damageFactor": 1.35, "areaWeapons": ["shotgun"], "areaDamageFactor": 0.65},
    {"id": "firearms_10", "type": "attack", "damageFactor": 1.80, "accuracyBonus": 6},
    # 盾
    {"id": "shield_01", "type": "attack", "effects": [st("stagger", control=True)]},
    {"id": "shield_03", "type": "ally", "targetMode": "ally", "needsAllies": True, "protect": True},
    {"id": "shield_05", "type": "guard", "targetMode": "self", "guardCharges": 1, "displaceResist": 25, "buffRounds": 2},
    {"id": "shield_07", "type": "ally", "targetMode": "ally", "needsAllies": True, "protect": True, "teamGuardFactor": 0.8, "buffRounds": 2},
    {"id": "shield_10", "type": "guard", "targetMode": "self", "guardCharges": 2, "teamGuardFactor": 0.8, "buffRounds": 1},
    # 指挥（单人时“任一友方”“全队”即自己；“任一其他友方”“交换站位”要队友，B8）
    {"id": "command_01", "type": "ally", "targetMode": "ally", "inspireAccuracy": 6, "buffRounds": 2},
    {"id": "command_03", "type": "ally", "targetMode": "ally", "needsAllies": True},
    {"id": "command_05", "type": "ally", "targetMode": "ally", "panicReduce": 8},
    {"id": "command_07", "type": "ally", "targetMode": "ally", "needsAllies": True, "apBonus": 1},
    {"id": "command_10", "type": "ally", "targetMode": "ally", "allOutFactor": 1.15, "buffRounds": 2},
    # 医疗（单人时“任一友方”即自己；治疗不暴击、不吃攻击成长倍率）
    {"id": "medicine_01", "type": "medical", "targetMode": "ally", "items": {"bandage": 1}, "useBandage": True},
    {"id": "medicine_03", "type": "medical", "targetMode": "ally", "items": {"medical_gauze": 1}, "stopBleed": True, "heal": 0.6},
    {"id": "medicine_05", "type": "medical", "targetMode": "ally", "items": {"saline": 1}, "heal": 0.8},
    {"id": "medicine_07", "type": "medical", "targetMode": "ally", "items": {"medical_gauze": 1}, "removeWeakOrStagger": True},
    {"id": "medicine_10", "type": "medical", "targetMode": "ally", "items": {"saline": 1, "medical_gauze": 1}, "heal": 1.5, "healMaxRatio": 0.25},
]

# ───────────── A05 §1 武器固定特性里挂在技能上的部分 ─────────────
TRAITS = {
    "kitchen_knife": {"bleedTriggers": 1},            # 锋口：流血持续轮数+1（仍不叠层）
    "machete": {"bleedAccuracy": 4},                  # 扫切：本武器流血命中能力+4（割伤）
    "guard_sword": {"counterDamage": 1.15},           # 反击：反击技能伤害×1.15
    "breach_axe": {"armorBreakBonus": 0.1},           # 破甲：破甲技能减甲量+0.1
    "rescue_axe": {"protectorDamage": 1.15},          # 救援：攻击正保护队友的目标时伤害×1.15
    "metal_spear": {"braceDamage": 1.1},              # 金属尖：迎击伤害×1.1
    "pike": {"braceExtra": 1},                        # 架阵：每轮迎击额外触发次数+1（最多2）
    "barbed_spear": {"knockbackAccuracy": 8},         # 钩刺：逼退命中能力+8
    "steel_mace": {"staggerUntilAttack": 1},          # 震荡：失衡持续到目标下次攻击后
    "shock_baton": {"interruptSlow": 1},              # 压制：打断成功后目标下轮速度额外−1（不叠）
    "recurve_bow": {"markedAccuracy": 5},             # 稳弓：对标记目标命中能力+5
    "hunter_bow": {"animalDamage": 1.15},             # 狩猎：对动物伤害×1.15
    "repeater_crossbow": {"reloadMax": 2},            # 快装：每次装填最多2发
    "service_pistol": {"suppressAccuracy": 6},        # 稳枪：压制命中能力+6（火力压制）
    "marksman_rifle": {"markedDamage": 1.15},         # 精校：对标记目标伤害×1.15
    "field_radio": {"commandRounds": 1},              # 协同：指挥增益持续多1轮；同名不叠
    "command_terminal": {"firstCommandAccuracy": 4},  # 预判：每场首次号令额外赋予目标下一击命中能力+4
    "trauma_bag": {"medicalHealBonus": 0.2},          # 创伤处置：非病毒医疗恢复+0.2H
    "rescue_medkit": {"postMedicalDamage": 0.9},      # 稳定：有效医疗后目标获下一次直接伤害−10%
    "triage_kit": {"criticalCareBonus": 0.3},         # 分诊：危急救护的回血额外+0.3H
    # 短枪（手枪允许我方2号位）= 使用位 234 已含 2；散布（霰弹枪散射仅作用敌位12）= skillExec firearms_07 areaWeapons；
    # 无声/劲弩/穿甲/劈砍 B4/B6 已接；轻巧（换位）、包铁/护送（保护）、集合（整队）属多人，B8。
}


def step(kind, **kw):
    d = {"kind": kind}
    d.update(kw)
    return d


GRAB = lambda verb="抓击", bite=20, **kw: step("attack", verb=verb, bitePct=bite, **kw)
MELEE12 = {"ownSlots": "12", "targetSlots": "12"}

# ───────────── A09 §1：旧 10 种补字段（面板数值不变） ─────────────
OLD = {
    "shambler": dict(controlResist=10, ownSlots="12", targetSlots="12", intents={"cycle": [GRAB("抓击")]}),
    "crawler": dict(controlResist=10, ownSlots="12", targetSlots="12", intents={"cycle": [GRAB("扯腿", control="slow")]}),
    "runner": dict(controlResist=15, ownSlots="12", targetSlots="12", intents={"cycle": [GRAB("扑击", control="stagger")]}),
    "worker": dict(controlResist=20, ownSlots="12", targetSlots="12", intents={"cycle": [GRAB("挥击")]}),
    "riot": dict(controlResist=30, ownSlots="12", targetSlots="12", intents={"opening": [step("guard")], "cycle": [GRAB("撞击")]}),
    # 嚎叫尸：A09 §1“34→12；奇数轮全队恐慌+4；偶数轮近战/前移”（旧 v0.5 正文 +8 由本表取代）
    "howler": dict(controlResist=15, ownSlots="34", targetSlots="12",
                   intents={"cycle": [step("howl", verb="嚎叫", panic=4), GRAB("抓击", **MELEE12)]}),
    "feral_dog": dict(controlResist=15, ownSlots="12", targetSlots="12", intents={"cycle": [step("attack", verb="撕咬", bleedPct=25)]}),
    "wolf": dict(controlResist=20, ownSlots="12", targetSlots="12", intents={"cycle": [step("attack", verb="撕咬", bleedPct=25)]}),
    "boar": dict(controlResist=30, ownSlots="12", targetSlots="12",
                 intents={"cycle": [step("charge_prepare"), step("charge", verb="冲撞", factor=1.5)]}),
    "stag": dict(controlResist=20, ownSlots="12", targetSlots="12", intents={"cycle": [step("attack", verb="顶撞")]}),
}


def enemy(id, name, family, hp, armor, dodge, atk, dmin, dmax, speed, own, tgt, ctrl, intents, fear, rarity="common",
          elite=False, extra=None, **kw):
    d = {
        "id": id, "name": name, "family": family, "hp": hp, "armor": armor, "dodge": dodge, "speed": speed,
        "damageMin": dmin, "damageMax": dmax, "behavior": "intents", "fleeAtHp": None,
        "immuneBleed": family == "zombie", "artRequired": False, "attackRating": atk, "rarity": rarity,
        "biteIntentWeightPct": 0, "worldPersistentId": True, "fearChance": fear[0], "fearValue": fear[1],
        "controlResist": ctrl, "ownSlots": own, "targetSlots": tgt, "ap": 4 if elite else 3, "elite": elite,
        "intents": intents,
    }
    if extra:
        d["extraDrop"] = extra
    d.update(kw)
    return d


def drop(item, count, chance):
    return {"itemId": item, "count": count, "chance": chance}


FEAR_DEFAULT, FEAR_FAST, FEAR_SCREAM, FEAR_ELITE = (0.35, 6), (0.55, 10), (0.65, 16), (0.60, 14)

# ───────────── A09 §1：新增 20 种 ─────────────
NEW = [
    enemy("z_slow", "迟缓感染者", "zombie", 4.2, 0, 22, 21, 0.70, 1.10, 2, "12", "12", 10,
          {"cycle": [GRAB("抓击")]}, FEAR_DEFAULT, extra=drop("scrap", 1, 0.20)),
    enemy("z_helmet", "护盔感染者", "zombie", 5.8, 0.35, 22, 22, 0.90, 1.30, 3, "12", "12", 25,
          {"cycle": [GRAB("抓击")]}, FEAR_DEFAULT, extra=drop("iron_plate", 1, 0.25), staggerImmuneOnce=True),
    enemy("z_spitter", "酸液感染者", "zombie", 4.2, 0, 28, 24, 0.60, 0.90, 4, "34", "1234", 20,
          {"cycle": [step("spit", verb="喷吐", factor=0.8, armorBreak=0.15, armorBreakRounds=2),
                     step("attack", verb="抓击", **MELEE12)]}, FEAR_DEFAULT, extra=drop("plastic", 1, 0.25)),
    enemy("z_hauler", "搬运感染者", "zombie", 6.2, 0.2, 20, 22, 1.00, 1.50, 2, "12", "12", 25,
          {"opening": [step("charge_prepare"), step("charge", verb="推撞", factor=1.3, knockback=1)],
           "cycle": [GRAB("抓击")]}, FEAR_DEFAULT, extra=drop("fastener", 1, 0.30)),
    enemy("z_darter", "迅捷感染者", "zombie", 3.8, 0, 36, 25, 0.60, 1.00, 8, "123", "123", 15,
          {"cycle": [GRAB("扑击")]}, FEAR_FAST, extra=drop("cloth", 1, 0.25)),
    enemy("z_screamer", "尖啸感染者", "zombie", 4.8, 0, 28, 20, 0.50, 0.80, 4, "34", "1234", 20,
          {"cycle": [step("howl", verb="尖啸", panic=5), step("attack", verb="抓击", **MELEE12)]}, FEAR_SCREAM,
          extra=drop("cloth", 1, 0.30)),
    enemy("z_guard", "重装守卫感染者", "zombie", 9, 0.65, 20, 23, 1.10, 1.70, 2, "12", "12", 40,
          {"cycle": [step("protect"), step("charge", verb="重击")]}, FEAR_DEFAULT, extra=drop("steel", 1, 0.40)),
    enemy("z_clinger", "攀附感染者", "zombie", 4.6, 0.1, 32, 24, 0.70, 1.10, 6, "23", "123", 25,
          {"cycle": [step("pull", verb="拉扯", factor=0, pull=1), GRAB("抓击")]}, FEAR_DEFAULT, extra=drop("rope", 1, 0.30)),
    enemy("z_bloater", "膨胀感染者", "zombie", 7.2, 0.1, 18, 20, 0.80, 1.20, 1, "12", "12", 35,
          {"cycle": [GRAB("抓击", bite=0)]}, FEAR_DEFAULT, extra=drop("rubber", 1, 0.25), deathSplash=0.3),
    enemy("z_nurse", "制服感染者", "zombie", 4.4, 0, 27, 23, 0.60, 1.00, 4, "12", "12", 15,
          {"cycle": [GRAB("抓击")]}, FEAR_DEFAULT, extra=drop("medical_gauze", 1, 0.30)),
    enemy("z_engineer", "机修感染者", "zombie", 6, 0.35, 24, 24, 1.00, 1.40, 3, "12", "12", 25,
          {"cycle": [GRAB("敲击", control="stagger")]}, FEAR_DEFAULT, extra=drop("parts", 1, 0.30)),
    enemy("z_signal", "鸣响感染者", "zombie", 5, 0, 28, 22, 0.60, 0.90, 5, "34", "12", 20,
          {"opening": [step("signal", verb="鸣响")], "cycle": [step("attack", verb="抓击", **MELEE12)]}, FEAR_DEFAULT,
          extra=drop("wire", 1, 0.30)),
    enemy("z_pump_foreman", "泵室领班", "zombie", 10, 0.3, 24, 26, 1.10, 1.60, 4, "12", "12", 45,
          {"cycle": [step("charge_prepare"), step("charge", verb="重击", factor=1.4), step("attack", verb="抓击")]},
          FEAR_ELITE, rarity="elite", elite=True, extra=drop("precision_parts", 1, 1.0)),
    enemy("z_isolation_guard", "隔离守卫", "zombie", 10, 0.4, 25, 25, 1.00, 1.50, 4, "12", "12", 45,
          {"cycle": [step("protect", limit=1), step("attack", verb="抓击"), step("bite", verb="咬击")]},
          FEAR_ELITE, rarity="elite", elite=True, extra=drop("medical_gauze", 2, 1.0)),
    enemy("z_power_guard", "配电守卫", "zombie", 12, 0.55, 25, 27, 1.20, 1.80, 4, "12", "12", 50,
          {"cycle": [step("charge_prepare"), step("charge", verb="冲撞", factor=1.4, knockback=1), step("guard")]},
          FEAR_ELITE, rarity="elite", elite=True, extra=drop("electronics", 1, 1.0)),
    enemy("z_relay_guard", "天线守卫", "zombie", 14, 0.45, 28, 28, 1.20, 1.80, 5, "12", "123", 50,
          {"cycle": [step("sweep", verb="扫击", factor=0.6, ownSlots="12", targetSlots="12"),
                     step("charge", verb="重击", factor=1.3), step("guard")]},
          FEAR_ELITE, rarity="elite", elite=True, extra=drop("precision_parts", 2, 1.0)),
    enemy("raider_melee", "流窜掠夺者", "human", 5.6, 0.15, 30, 27, 0.90, 1.30, 5, "12", "12", 25,
          {"cycle": [step("attack", verb="斩击"), step("protect"), step("attack", verb="斩击")]}, FEAR_DEFAULT,
          extra=drop("cloth", 1, 1.0)),
    enemy("raider_gunner", "流窜枪手", "human", 5, 0.1, 29, 26, 1.10, 1.60, 4, "34", "1234", 20,
          {"cycle": [step("shoot", verb="射击"), step("shoot", verb="射击"), step("reload", verb="装填")]}, FEAR_DEFAULT,
          extra=drop("ammo_9mm", 2, 1.0), ammoTotal=6, magazine=2),
    enemy("wild_dog_leader", "野犬首领", "animal", 6.4, 0.1, 34, 25, 0.80, 1.20, 7, "12", "12", 30,
          {"opening": [step("buff", verb="蓄势", nextHitFactor=1.2)], "cycle": [step("attack", verb="撕咬", bleedPct=25)]},
          FEAR_DEFAULT),
    enemy("feral_boar", "暴躁野猪", "animal", 8, 0.25, 24, 23, 1.00, 1.60, 4, "12", "12", 35,
          {"cycle": [step("charge_prepare"), step("charge", verb="冲撞", factor=1.4)]}, FEAR_DEFAULT),
]

CARCASS_ALIAS = {"wild_dog_leader": "wolf", "feral_boar": "boar"}


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    nodes = {n["id"]: n for n in cfg["skillNodes"]}
    for e in SKILL_EXEC:
        assert e["id"] in nodes, e["id"]
    cfg["skillExec"] = SKILL_EXEC

    for w in cfg["weapons"]:
        if w["id"] in TRAITS:
            w["traitEffects"] = TRAITS[w["id"]]
        else:
            w.pop("traitEffects", None)

    enemies = [e for e in cfg["enemies"] if e["id"] in OLD]
    for e in enemies:
        o = OLD[e["id"]]
        e["controlResist"] = o["controlResist"]
        e["ownSlots"] = o["ownSlots"]
        e["targetSlots"] = o["targetSlots"]
        e["ap"] = 3
        e["elite"] = False
        e["intents"] = o["intents"]
    cfg["enemies"] = enemies + NEW

    carcasses = [c for c in cfg["animalCarcasses"] if c["enemyId"] not in CARCASS_ALIAS]
    for eid, src in CARCASS_ALIAS.items():
        base = next(c for c in carcasses if c["enemyId"] == src)
        c = dict(base)
        c["enemyId"] = eid
        carcasses.append(c)
    cfg["animalCarcasses"] = carcasses

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"skillExec {len(SKILL_EXEC)}, weapons with traits {len(TRAITS)}, enemies {len(cfg['enemies'])}")


if __name__ == "__main__":
    main()
