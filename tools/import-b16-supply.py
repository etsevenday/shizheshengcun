# -*- coding: utf-8 -*-
"""
B16（开发计划 v0.9）：配方框架、水壶与复合锅（需求 R48—R51）导入 data/config.v0.6.json。
可重复运行（按 ID 覆盖本工具生成的条目，不动其他配置）。

来源：
- 04 附录/A07「X1.2 新增物理目录」「X1.3 每个配方及知识」「X1.5 水处理、携水及锅容量」「X1.6 野营、小修与医疗限制」「二十种食品用品」
- assets/20261030/facilities/expansion-supply.json recipes（19 条；mode 缺省的 14 条按 A07 X1.3 表“模式/额外成本”列，开发假设 B16-1）
- 01 基础系统/S08 X4、S10 X4、S11 X4；02 参考扩展/R02 X4、R06 X4
- 02 开发方待确认问题 Q60（烹饪经验：食品与 B16 主动配方合计每日 30，主动配方与被动产出不双发）、Q111（木炭由火堆煅烧，晾架只保留晾制）
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
SUPPLY = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "facilities", "expansion-supply.json")

# A07 X1.3 表“模式/额外成本”列（JSON 只给了 5 条 mode）
MODE = {
    "ex_ceramic_shell": "passive_heat",
    "ex_purification_tablet": "active_work",
    "ex_tablet_water": "fixed_wait",
    "ex_filter_cartridge": "active_work",
    "ex_cartridge_water": "fixed_wait",
    "ex_ration_canteen": "active_work",
    "ex_hardtack": "passive_heat",
    "ex_expedition_bar": "active_work",
    "ex_stew_portion": "passive_heat",
    "ex_field_tarp": "active_work",
    "ex_folding_stove": "active_work",
    "ex_field_patch": "active_work",
    "ex_cable_bundle": "active_work",
    "ex_flash_marker": "active_work",
    "ex_brace_pack": "active_work",
    "ex_rally_patch": "active_work",
    "ex_antidiarrheal_powder": "active_work",
    "ex_emergency_tonic": "active_work",
    "ex_station_water": "passive",
}
# 配方显示名（开发方按产物名拟；净水三条写清用什么净水）
NAMES = {
    "ex_ceramic_shell": "陶壳",
    "ex_purification_tablet": "净水片",
    "ex_tablet_water": "净水片处理脏水",
    "ex_filter_cartridge": "长效滤芯",
    "ex_cartridge_water": "滤芯处理脏水",
    "ex_ration_canteen": "分量水壶",
    "ex_hardtack": "远行硬饼",
    "ex_expedition_bar": "远征口粮棒",
    "ex_stew_portion": "谷肉炖食",
    "ex_field_tarp": "野营遮雨布",
    "ex_folding_stove": "折叠柴炉",
    "ex_field_patch": "野外修补包",
    "ex_cable_bundle": "绝缘线束",
    "ex_flash_marker": "闪光标记器",
    "ex_brace_pack": "支撑护带包",
    "ex_rally_patch": "鼓舞提示贴",
    "ex_antidiarrheal_powder": "止泻药粉",
    "ex_emergency_tonic": "应急恢复剂",
    "ex_station_water": "净水台处理脏水",
}
GROUP_MATERIAL = {"ex_ceramic_shell", "ex_cable_bundle", "ex_filter_cartridge"}
POT = {"ex_hardtack", "ex_stew_portion"}

# 锅食谱进程（S10 X4、A07 X1.5）：主食材每份占一位、辅料不占位但随进程锁定、锅水只扣一次
POT_PROCESSES = [
    {"id": "pot_hardtack", "name": "烘制", "recipeId": "ex_hardtack", "foodInputs": {"grain": 2}, "auxInputs": {"salt": 1},
     "waterCostMl": 500, "outputId": "hardtack", "outputCount": 2, "heatMinutes": 60},
    {"id": "pot_stew", "name": "炖煮", "recipeId": "ex_stew_portion", "foodInputs": {"grain": 1, "raw_meat": 1}, "auxInputs": {},
     "waterCostMl": 1000, "outputId": "stew_portion", "outputCount": 2, "heatMinutes": 45},
]

# 食用/使用效果（A07「二十种食品用品」效果列、X1.2 用途列）
FOOD_USE = {
    "root_porridge": (38, 10, 2), "meat_stew": (55, 12, 3), "grain_cake": (28, -2, 0), "berry_jam": (22, 0, 3),
    "dried_berries": (16, -2, 0), "dried_meat": (28, -5, 0), "canned_beans": (30, 3, 0), "fried_root": (35, -3, 3),
    "trail_ration": (48, -6, 0), "herbal_drink": (0, 25, 2),
    "hardtack": (24, -3, 0), "expedition_bar": (38, -2, 0), "stew_portion": (35, 12, 2),
}
CARE_USE = {
    "oral_rehydration": {"hydration": 35, "diarrheaReduceMinutes": 60, "cooldownGroup": "relief", "cooldownMinutes": 360,
                         "blockWhenNoHydrationOrDiarrheaGain": True, "outOfBattleOnly": True},
    "antidiarrheal_powder": {"diarrheaReduceMinutes": 120, "requiresDiarrhea": True, "cooldownGroup": "relief", "cooldownMinutes": 360,
                             "outOfBattleOnly": True},
    "emergency_tonic": {"energy": 2, "wakefulness": -2, "morale": -1, "cooldownGroup": "tonic", "cooldownMinutes": 360,
                        "blockWhenEnergyFull": True, "outOfBattleOnly": True},
}
# 物品说明：只把 B4 导入时的“本版只有物品定义，用途在后续版本开放”换成现在的用法，“策划用途：…”前缀保留（B4 M120）
PLACEHOLDER = "本版只有物品定义，用途在后续版本开放"
TAIL = {
    "oral_rehydration": "战外使用5分钟；与止泻药粉共用缓解冷却；水分已满且没有腹泻时没有效果；战斗中不能用",
    "antidiarrheal_powder": "战外使用5分钟",
    "emergency_tonic": "战外使用5分钟：体力+2、清醒−2、精神−1，6小时给药冷却；体力已满不能用；战斗中使用在后续版本开放",
    "field_patch": "战外15分钟手工修一件非枪金属武器/工具，恢复其最大耐久的10%（制作面板“现场小修”）",
    "ration_canteen": "只装同一种水；装入/倒出按500毫升整份水，壶内每次可直接喝250毫升（5分钟，水分+15）",
    "purification_tablet": "一片配4份脏水，固定等待20分钟得4份净水（制作面板“净水片处理脏水”）",
    "filter_cartridge": "配便携滤水器，一芯处理4份脏水，固定等待20分钟（制作面板“滤芯处理脏水”）",
    "water_filter": "作为工具配长效滤芯处理脏水（不消耗）",
}
FOOD_TAIL = "食用5分钟，战斗中不能吃"


def use_block(n=0, w=0, m=0, extra=None):
    u = {"satiety": n, "hydration": w, "morale": m, "health": 0, "causesDiarrhea": False, "isBandage": False,
         "isAntibiotic": False, "isAntiviral": False}
    if extra:
        u.update(extra)
    return u


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(SUPPLY, encoding="utf-8") as f:
        supply = json.load(f)
    items = {i["id"]: i for i in cfg["items"]}

    # ── 知识 ceramic_methods（A07 X1.3）：营区工坊“基础陶工说明”（制造3，0分钟）或 d06_08 矿道作业手册领取 ──
    cfg["knowledge"] = [k for k in cfg["knowledge"] if k["id"] != "ceramic_methods"]
    cfg["knowledge"].append({
        "id": "ceramic_methods", "name": "基础陶工做法",
        "grantedByFacts": ["h03_ceramic_notice_read", "mining_blueprint_pack"],
        "source": "营区工坊“基础陶工说明”（制造3级，0分钟）；或矿道 d06_08 领取“矿道作业手册”时一并补得",
    })
    notices = cfg["city"]["notices"]
    cfg["city"]["notices"] = [n for n in notices if n["id"] != "h03_ceramic_notice"]
    idx = next(i for i, n in enumerate(cfg["city"]["notices"]) if n["id"] == "h03_craft_notice") + 1
    cfg["city"]["notices"].insert(idx, {
        "id": "h03_ceramic_notice", "locationId": "h03_workshop", "title": "基础陶工说明",
        "requiresFact": None, "minSkills": {"crafting": 3}, "grantsFact": "h03_ceramic_notice_read",
        "knowledgeId": "ceramic_methods", "dialogueSet": None, "choices": [],
    })

    # ── 工坊：陶窑、固定净水台（建造在 B17；本批只登记，配方显示“后续版本开放”） ──
    st = cfg["workshops"]["stations"]
    cfg["workshops"]["stations"] = [x for x in st if x["id"] not in ("kiln", "water_station")]
    cfg["workshops"]["stations"].append({"id": "kiln", "name": "陶窑", "note": "陶窑建造在后续版本开放（B17）"})
    cfg["workshops"]["stations"].append({"id": "water_station", "name": "固定净水台", "note": "净水台建造在后续版本开放（B17）"})

    # ── 19 条新配方 ──
    gen = {r["id"] for r in supply["recipes"]} | {"field_patch_repair"}
    cfg["recipes"] = [r for r in cfg["recipes"] if r["id"] not in gen]
    for sr in supply["recipes"]:
        rid = sr["id"]
        mode = sr.get("mode") or MODE[rid]
        assert mode == MODE[rid], (rid, mode)
        r = {
            "id": rid, "name": NAMES[rid], "kind": "pot" if rid in POT else "craft",
            "baseMinutes": sr["baseMinutes"], "inputs": dict(sr["inputs"]), "outputs": dict(sr["outputs"]),
            "catalysts": list(sr.get("tools", {}).keys()), "requirement": None, "facilityResult": None,
            "minSkills": {sr["skill"]: sr["level"]}, "station": sr["station"],
            "knowledge": sr["knowledgeId"], "mode": mode,
        }
        if rid in GROUP_MATERIAL:
            r["group"] = "material"
        if sr["skill"] != "crafting":
            r["xpSkill"] = sr["skill"]
        if isinstance(sr.get("fuel"), dict):
            r["fuel"] = dict(sr["fuel"])
        if "shelfRule" in sr:
            r["shelfRule"] = "min_inputs"
        cfg["recipes"].append(r)

    # ── 野外修补包维护（A07 X1.6、R02 X4）：hand、制造1、15 分钟、恢复最大耐久 10%、只修非枪金属武器/工具 ──
    cfg["recipes"].append({
        "id": "field_patch_repair", "name": "现场小修", "kind": "repair", "baseMinutes": 15,
        "inputs": {"field_patch": 1}, "outputs": {}, "catalysts": [], "requirement": None, "facilityResult": None,
        "targetInstanceRequired": True, "restoreOwnDurabilityMaxFraction": 0.1, "rerollDurability": False,
        "requireEmptyDetachedPotWhenTargetIsPot": False, "station": "hand", "minSkills": {"crafting": 1},
        "repairFilter": "metal_non_gun",
    })

    # ── 锅食谱进程 ──
    cfg["processes"] = [p for p in cfg["processes"] if p["id"] not in {x["id"] for x in POT_PROCESSES}]
    for pp in POT_PROCESSES:
        cfg["processes"].append({
            "id": pp["id"], "name": pp["name"], "target": "pot_recipe", "inputId": "", "outputId": pp["outputId"],
            "allowedBatchMl": [], "heatMinutes": pp["heatMinutes"], "waterCostMl": pp["waterCostMl"],
            "requiredOwner": "pot_inside_lit_fire", "requiredLiquid": "clean_water",
            "inputCount": 1, "outputCount": pp["outputCount"], "recipeId": pp["recipeId"],
            "foodInputs": pp["foodInputs"], "auxInputs": pp["auxInputs"],
        })
    cook = cfg["progression"]["cooking"]
    for pid in ("pot_hardtack", "pot_stew"):
        if pid not in cook["foodProcesses"]:
            cook["foodProcesses"].append(pid)

    # ── 物品：食用/使用效果、水壶、滤水器工具标签 ──
    def set_tail(iid, tail):
        it = items[iid]
        if PLACEHOLDER in it.get("desc", ""):
            it["desc"] = it["desc"].replace(PLACEHOLDER, tail)

    for iid, (n, w, m) in FOOD_USE.items():
        it = items[iid]
        it["edible"] = True
        it["use"] = use_block(n, w, m)
        set_tail(iid, "现在可以食用" if "食用5分钟" in it.get("desc", "") else FOOD_TAIL)
    for iid, extra in CARE_USE.items():
        it = items[iid]
        extra = dict(extra)
        hyd = extra.pop("hydration", 0)
        mor = extra.pop("morale", 0)
        it["use"] = use_block(0, hyd, mor, extra)
    for iid, tail in TAIL.items():
        set_tail(iid, tail)
    items["ration_canteen"]["canteen"] = {"capacityMl": 2000, "sipMl": 250, "transferMl": 500,
                                          "accepts": ["dirty_water", "clean_water"], "sipHydration": 15, "gramsPerMl": 1}
    items["water_filter"]["toolTag"] = "water_filter"

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("B16: recipes", len(supply["recipes"]), "+ field_patch_repair; processes pot_hardtack/pot_stew; knowledge ceramic_methods")


if __name__ == "__main__":
    main()
