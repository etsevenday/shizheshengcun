"""批次 B4（策划提交 f172677，需求 R13–R17）：把 A12 全系统策划数据索引里的物理物品与 72 件装备导入 data/config.v0.6.json。

用法（仓库根目录，可重复运行，结果相同）：
  python -X utf8 tools/import-b4-catalog.py                # 物品定义 + 装备静态表 + 词条与品质规则
  python -X utf8 tools/import-b4-catalog.py --stage items  # 只导物品定义（R13）

规则（开发计划 v0.9 B4 开发假设 H1–H3）：
- 旧 ID 与旧数值不动：已有物品、武器只补新字段（装备槽、阶、速度、使用位/目标位、固定特性原文、拆解产出），不改原有值。
- 新物品只导定义（名称、重量、堆叠、保质、类别、说明），不导使用效果：不能食用、不能当药用，用途在后续批次接入。
- 72 件装备与 18 条词条的数值逐项来自 A12 equipment（与 A05 §1、§2、§4 表一致）；效果原文全文保存，
  能被现有引擎消费的效果另外写成结构化 effects（数值取自原文，ConfigTests 会核对数字出现在原文里）。
- 品质概率只取 A05 §4 正文写明的数值（新手拾荒 / 主线拾荒 / 副本首领 / 普通制造默认绿）；未写的来源按白色。
"""

import argparse
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
A12 = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "04 附录", "A12 全系统策划数据索引.json")

# A05 槽名 → 代码槽名（开发假设 H2：代码槽名不改，配置里写代码槽名）
SLOT_MAP = {"armor": "armor", "combat_charm": "combat_acc", "top": "top", "bottom": "bottom", "survival_charm": "survival_acc"}
KIND_NAMES = {"blade": "刀剑", "axe": "斧", "spear": "矛", "blunt": "钝器", "archery": "弓弩", "firearms": "枪械",
              "shield": "盾", "command": "指挥器具", "medical": "医疗器具"}
SLOT_NAMES = {"armor": "防具", "combat_acc": "战斗饰品", "top": "上衣", "bottom": "下衣", "survival_acc": "求生饰品"}
# 熟练度门类沿用现有 8 门（十五门熟练度在 B5）：斧沿简易斧练刀斧；盾、指挥没有对应熟练度，留空（B4 不能攻击）
SKILL_OF_KIND = {"blade": "blade", "axe": "blade", "spear": "spear", "blunt": "blunt", "archery": "archery",
                 "firearms": "firearms", "shield": "", "command": "", "medical": "medicine"}

# ── 物理物品的类别（开发假设 H1：材料归 material，食品归 food，医疗归 medical，拿不准归 material） ──
FOOD_USE = {"食品"}
MEDICAL_USE = {"医疗", "医疗材料", "口服非注射"}


def item_kind(p):
    cat = p.get("category")
    if cat is not None:
        return {"food": "food", "medical": "medical"}.get(cat, "material")
    skill = p.get("skill")
    if skill == "cooking":
        return "food"
    if skill == "medicine":
        return "medical"
    if skill is not None:
        return "material"
    use = p.get("use") or ""
    if use in FOOD_USE:
        return "food"
    if use in MEDICAL_USE:
        return "medical"
    return "material"


def item_desc(p):
    text = p.get("effect") or p.get("use") or ""
    text = text.strip()
    return (f"策划用途：{text}。" if text else "") + "本版只有物品定义，用途在后续版本开放"


def rng(text):
    lo, hi = text.replace("—", "–").split("–")
    return [float(lo), float(hi)]


def num(x):
    """整数值写成整数（与现有配置写法一致）。"""
    return int(x) if float(x) == int(x) else x


# ── 结构化效果（数值逐字取自 A05 §2 固定效果与 §4 词条效果原文） ──
# key：combat.*（B6 前只显示）、work.*（基础工作量倍率）、rate.*（负小时率倍率）、pos.*（正恢复加值）、hazard.*、repair/bandage/eat
# op：add 加值；mul 乘数；set 替代比例。scope=attack 的词条只作用于本页武器（R16“攻击附加不跨武器借用”）。
GEAR_EFFECTS = {
    "padded_vest": [("combat.armor", "add", 0.10)],
    "leather_armor": [("combat.armor", "add", 0.20)],
    "scrap_armor": [("combat.armor", "add", 0.40), ("combat.speed", "add", -2)],
    "guard_armor": [("combat.armor", "add", 0.45), ("combat.speed", "add", -1)],
    "scout_armor": [("combat.armor", "add", 0.20), ("combat.speed", "add", 1)],
    "heavy_armor": [("combat.armor", "add", 0.65), ("combat.speed", "add", -2), ("combat.displaceResist", "add", 15)],
    "medic_armor": [("combat.armor", "add", 0.20)],
    "balanced_armor": [("combat.armor", "add", 0.40)],
    "steady_ring": [("combat.accuracy", "add", 3)],
    "swift_pendant": [("combat.speed", "add", 1), ("combat.armor", "add", -0.05)],
    "focus_lens": [("combat.crit", "add", 4), ("combat.speed", "add", -1)],
    "work_shirt": [("work.craft", "mul", 0.95)],
    "forage_jacket": [("rate.gather.energy", "mul", 0.90)],
    "scavenge_vest": [("work.scavenge", "mul", 0.90)],
    "craft_coat": [("work.craft", "mul", 0.90)],
    "rest_coat": [("pos.shelteredRest.energy", "add", 0.3)],
    "expedition_coat": [("rate.nonCombat.satiety", "mul", 0.95), ("rate.nonCombat.hydration", "mul", 0.95)],
    "work_trousers": [("rate.gather.energy", "mul", 0.95)],
    "trail_trousers": [("rate.move.energy", "mul", 0.90)],
    "utility_trousers": [("rate.scavengeClear.energy", "mul", 0.90)],
    "reinforced_trousers": [("hazard.health", "mul", 0.80)],
    "recovery_trousers": [("pos.sleep.energy", "add", 0.2)],
    "expedition_trousers": [("work.move", "mul", 0.90)],
    "gather_gloves": [("hazard.woodScrapChance", "mul", 0.80)],
    "route_compass": [("work.route", "mul", 0.95)],
    "filter_token": [("work.purificationTablet", "mul", 0.90)],
    "repair_manual": [("repair.fraction", "set", 0.25)],
    "medical_notes": [("bandage.health", "add", 0.2)],
    "ration_pouch": [("eat.satiety", "mul", 1.05)],
}

# 词条兼容池（A05 §4“兼容池”列）→ 结构化：kind:<武器类型>、slot:<代码槽>、durable（有耐久的装备）
ATTACK_KINDS = ["kind:blade", "kind:axe", "kind:spear", "kind:blunt", "kind:archery", "kind:firearms"]
ALL_WEAPON_KINDS = ATTACK_KINDS + ["kind:shield", "kind:command", "kind:medical"]
POOLS = {
    "攻击武器": ATTACK_KINDS,
    "刀剑/斧/矛": ["kind:blade", "kind:axe", "kind:spear"],
    "武器/防具": ALL_WEAPON_KINDS + ["slot:armor"],
    "防具": ["slot:armor"],
    "盾": ["kind:shield"],
    "盾/防具": ["kind:shield", "slot:armor"],
    "指挥器具": ["kind:command"],
    "医疗器具": ["kind:medical"],
    "钝器/盾": ["kind:blunt", "kind:shield"],
    "矛/盾": ["kind:spear", "kind:shield"],
    "有耐久装备": ["durable"],
    "弓弩": ["kind:archery"],
    "求生上衣": ["slot:top"],
    "求生衣物": ["slot:top", "slot:bottom"],
    "求生下衣": ["slot:bottom"],
}
AFFIX_EFFECTS = {
    "accurate": [("combat.accuracy", "add", 4)],
    "keen": [("combat.damageMultiplier", "add", 0.06)],
    "piercing": [("combat.armorIgnore", "add", 0.10)],
    "balanced": [("combat.energyCost", "mul", 0.92)],
    "swift": [("combat.speed", "add", 1)],
    "heavy": [("combat.armor", "add", 0.08), ("combat.speed", "add", -1)],
    "guarded": [],
    "anchored": [("combat.displaceResist", "add", 10)],
    "tactical": [],
    "restorative": [],
    "staggering": [],
    "pushing": [],
    "durable": [("durability.max", "mul", 1.15)],
    "quiet": [("combat.noise", "add", -3)],
    "craftsman": [("work.craft", "mul", 0.97)],
    "economical": [("rate.nonCombat.hydration", "mul", 0.97)],
    "patient": [("pos.shelteredRest.energy", "add", 0.1)],
    "surefoot": [("rate.move.energy", "mul", 0.95)],
}
# 只作用于本次所用武器的攻击类词条（A05 §4 末段、§5“攻击附加不跨武器借用”）
ATTACK_SCOPED = {"accurate", "keen", "piercing", "balanced", "quiet", "staggering", "pushing"}


def effects(rows, scope=None):
    out = []
    for key, op, value in rows:
        e = {"key": key, "op": op, "value": num(value)}
        if scope:
            e["scope"] = scope
        out.append(e)
    return out


def targets_of(target):
    """现行两行战斗的 targets（开发假设 H3：旧武器不动；新武器按 A05 目标位推导）：含 3/4 号敌位的单体武器可打后排，
    其中最远到 3 号位（矛 123）沿旧矛“后排×0.75”，到 4 号位（弓弩、枪 1234）沿旧弓枪“全排”；只到 1/2 号位只打前排；友方器具不能攻击，记为前排。"""
    if "4" in target:
        return "all"
    if "3" in target:
        return "all_rear_damage_0.75"
    return "front"


def salvage_of_weapon(w):
    # A05 §5：白/绿/蓝武器固定 scrap1/2/3；电台 electronics1
    if w["id"] == "field_radio":
        return {"itemId": "electronics", "count": 1}
    return {"itemId": "scrap", "byQuality": True}


def salvage_of_gear(g, slot):
    # A05 §5：布皮装备 cloth1 或 leather1（就主材选）：防具、上衣、下衣按 A05 §3 配方里布料与皮革谁多；
    # 没有配方或主材不是布/皮的（拼接铁甲、重型防护甲、守备护甲）、战斗/求生饰品没有写产出 → 不给拆解定义（开发假设 H9）
    if slot not in ("armor", "top", "bottom"):
        return None
    inputs = g.get("inputs") or {}
    if not inputs:
        return None
    main = max(inputs.items(), key=lambda kv: kv[1])[0]
    cloth, leather = inputs.get("cloth", 0), inputs.get("leather", 0)
    if main not in ("cloth", "leather"):
        return None
    pick = "cloth" if cloth >= leather else "leather"
    return {"itemId": pick, "count": 1}


def import_items(cfg, a12):
    have = {i["id"] for i in cfg["items"]}
    added = 0
    for p in a12["physicalItems"]:
        if p["id"] in have:
            continue
        hours = p.get("shelfLifeHours") or 0
        cfg["items"].append({
            "id": p["id"], "name": p["name"], "weightGrams": p["weightGrams"], "stackMax": p["stackMax"],
            "shelfLifeMinutes": hours * 60 if hours > 0 else None, "kind": item_kind(p), "toolTag": None,
            "durabilityMaxRange": None, "edible": False, "requiresCooking": False, "use": None, "desc": item_desc(p),
        })
        have.add(p["id"])
        added += 1
    return added


def import_equipment(cfg, a12):
    eq = a12["equipment"]
    items = {i["id"]: i for i in cfg["items"]}
    weapons = {w["id"]: w for w in cfg["weapons"]}
    added_items = 0
    for w in eq["weapons"]:
        dur = rng(w["durability"])
        dmg = rng(w["damage"])
        extra = {
            "tier": w["tier"], "speed": num(w["speed"]), "ownPositions": w["own"], "targetPositions": w["target"],
            "traitText": w["trait"], "salvage": salvage_of_weapon(w),
        }
        if w["id"] in weapons:
            weapons[w["id"]].update(extra)          # 旧武器只补新字段，原有数值不动
        else:
            row = {
                "id": w["id"], "name": w["name"], "skillId": SKILL_OF_KIND[w["kind"]], "weaponKind": w["kind"],
                "damageMin": num(dmg[0]), "damageMax": num(dmg[1]), "energyCost": num(w["energy"]), "weightGrams": w["weight_g"],
                "targets": targets_of(w["target"]), "ammoId": w["ammo"] or None, "magazineCapacity": w["magazine"],
                "basicApCost": 1, "accuracyRating": w["accuracy"], "critRatingBase": w["crit"],
                "durabilityMaxRange": [num(dur[0]), num(dur[1])],
            }
            row.update(extra)
            cfg["weapons"].append(row)
            weapons[w["id"]] = row
        if w["id"] in items:
            items[w["id"]]["equipSlot"] = "weapon"
        else:
            row = {
                "id": w["id"], "name": w["name"], "weightGrams": w["weight_g"], "stackMax": 1, "shelfLifeMinutes": None,
                "kind": "weapon", "equipSlot": "weapon", "toolTag": None, "durabilityMaxRange": [num(dur[0]), num(dur[1])],
                "edible": False, "requiresCooking": False, "use": None,
                "desc": f"{KIND_NAMES[w['kind']]}·T{w['tier']}；固定特性：{w['trait']}",
            }
            cfg["items"].append(row)
            items[w["id"]] = row
            added_items += 1

    gear = []
    for g in eq["gear"]:
        slot = SLOT_MAP[g["slot"]]
        dur = None if g["durability"] in ("无", "", None) else [num(x) for x in rng(g["durability"])]
        row = {
            "id": g["id"], "name": g["name"], "slot": slot, "tier": g["tier"], "weightGrams": g["weight_g"],
            "durabilityMaxRange": dur, "effectText": g["effect"], "effects": effects(GEAR_EFFECTS.get(g["id"], [])),
            "salvage": salvage_of_gear(g, slot),
        }
        gear.append(row)
        if g["id"] not in items:
            irow = {
                "id": g["id"], "name": g["name"], "weightGrams": g["weight_g"], "stackMax": 1, "shelfLifeMinutes": None,
                "kind": "gear", "equipSlot": slot, "toolTag": None, "durabilityMaxRange": dur,
                "edible": False, "requiresCooking": False, "use": None,
                "desc": f"{SLOT_NAMES[slot]}·T{g['tier']}；{g['effect']}",
            }
            cfg["items"].append(irow)
            items[g["id"]] = irow
            added_items += 1
    cfg["gear"] = gear

    cfg["affixes"] = [{
        "id": a["id"], "name": a["name"], "poolText": a["pool"], "pools": POOLS[a["pool"]], "effectText": a["effect"],
        "group": a["group"], "effects": effects(AFFIX_EFFECTS[a["id"]], "attack" if a["id"] in ATTACK_SCOPED else None),
    } for a in eq["affixes"]]

    # 品质与生成规则（A05 §4 正文；R02 §2、§3；R16 合成上限：A05 §4 末段）
    cfg["equipment"] = {
        "qualityOrder": ["white", "green", "blue"],
        "qualityNames": {"white": "白", "green": "绿", "blue": "蓝"},
        "affixCounts": {"white": 0, "green": 1, "blue": 2},
        "qualityOrigins": {
            "novice_scavenge": {"white": 0.8, "green": 0.2},
            "mainline_scavenge": {"white": 0.5, "green": 0.45, "blue": 0.05},
            "dungeon_boss": {"green": 0.8, "blue": 0.2},
            "craft": {"green": 1},
        },
        "originNames": {"novice_scavenge": "新手普通拾荒", "mainline_scavenge": "主线普通拾荒", "dungeon_boss": "副本首领奖励", "craft": "普通制造"},
        # 开发假设 H4：现有 8 地点的拾荒箱按“新手普通拾荒”；其余未写概率的来源（开局、开发箱、固定箱以外的掉落、交换、剧情、旧档）按白色
        "scavengedResourceOrigin": "novice_scavenge",
        "craftOrigin": "craft",
        "durabilityRounding": 0.1,
        "limits": {"positiveSpeedCap": 3, "armorCap": 0.9, "survivalWorkFloor": 0.7, "negativeRateFloor": 0.75},
        "salvage": {"recipeId": "salvage_equipment", "scrapByQuality": {"white": 1, "green": 2, "blue": 3}},
        "armorWearPerHit": 1,
    }

    # 拆解（A05 §5：每次 15 基础分钟、输出当前地面）：走配方流程，不出现在手工制作列表里
    if not any(r["id"] == "salvage_equipment" for r in cfg["recipes"]):
        cfg["recipes"].append({
            "id": "salvage_equipment", "name": "拆解装备", "kind": "salvage", "baseMinutes": 15, "inputs": {}, "outputs": {},
            "catalysts": [], "requirement": None, "facilityResult": None, "targetInstanceRequired": True,
            "restoreOwnDurabilityMaxFraction": 0, "rerollDurability": False, "requireEmptyDetachedPotWhenTargetIsPot": False,
        })
    cfg["actions"]["rateAliases"].setdefault("salvage", "craft")
    return added_items


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", choices=["items", "all"], default="all")
    args = ap.parse_args()
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(A12, encoding="utf-8") as f:
        a12 = json.load(f)
    n = import_items(cfg, a12)
    m = import_equipment(cfg, a12) if args.stage == "all" else 0
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"物理物品新增 {n} 件；装备物品新增 {m} 件；物品共 {len(cfg['items'])} 件，武器 {len(cfg['weapons'])} 行")


if __name__ == "__main__":
    sys.exit(main())
