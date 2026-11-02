# -*- coding: utf-8 -*-
"""
B10（开发计划 v0.9）：工坊六类设施、配方 station/knowledge 字段、62 个装备配方、A07 材料加工链导入 data/config.v0.6.json。
可重复运行（按 ID 覆盖本工具生成的条目，不动其他配置）。

来源：
- 04 附录/A12 全系统策划数据索引.json equipment.weapons/gear（inputs/station/craft_level/minutes/acquisition/source）
- 04 附录/A05 §3（设施／等级／基础分钟；forge 每批另付 charcoal1）、「2026-10-30 B4 蓝图包与旧唯一装备位置迁移」（d01_06→d01_08 等）
- 04 附录/A07「工坊设施」「材料加工链」（drying_rack 不导；木炭行 Q111 已改为火堆被动过程 fire_charcoal，不导成主动 craft）
- B10b（答复 Q107–Q111）：Q109 军械台蓝图由 d03_08_unique 领取事实（或旧档 communication_power_claimed）授予；
  Q110 医疗台 saline/disinfectant/herb_extract 完成经验给 medicine（xpSkill）；Q111 火堆煅烧 fire_charcoal 写入 processes
"""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
BASE = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0")
A12 = os.path.join(BASE, "04 附录", "A12 全系统策划数据索引.json")
A07 = os.path.join(BASE, "04 附录", "A07 材料、制作工坊与求生物资目录.md")

STATION_NAMES = {
    "hand": "手工",
    "workbench": "工作台",
    "sewing": "缝纫台",
    "forge": "锻炉",
    "medical": "医疗台",
    "armory": "军械台",
}
# 现有配方 ID（保留，不改 ID，只补 station/knowledge/group）
LEGACY_RECIPE = {
    "stone_knife": "knife",
    "hatchet": "axe",
    "wooden_spear": "make_wooden_spear",
    "wooden_bow": "make_wooden_bow",
    "metal_spear": "make_metal_spear",
    "crossbow": "make_crossbow",
    "knuckle_wraps": "make_knuckle_wraps",
}
# S08 2026-10-30：原石刀/斧/木矛/木弓与已批准金属矛/弩的配方与初始知识保留 → 开局已会
INITIAL_KNOWN = {"stone_knife", "hatchet", "wooden_spear", "wooden_bow", "metal_spear", "crossbow"}
# A05「B4 蓝图包与旧唯一装备位置迁移」：四旧蓝图终点
NODE_MOVE = {"d01_06": "d01_08", "d02_06": "d02_08", "d03_06": "d03_08", "d04_06": "d04_07"}
FORGE_FUEL = {"charcoal": 1}
# A05「B4 蓝图包与旧唯一装备位置迁移」：唯一装备按原 claim 迁移后的节点（equipment-acquisition-migration.json 的 to）
MIGRATION = os.path.join(BASE, "assets", "20261030", "index", "equipment-acquisition-migration.json")
UNIQUE_MOVE = {}
ARMORY_KNOWLEDGE = "blueprint_armory_station"
# Q109 选 A（A07/A08/A11 本轮章）：d03_08 封存仓终点唯一领取 d03_08_unique 成功时同事务授予；旧档明确已领或原等效 communication_power_claimed 补知识
ARMORY_GRANT_FACTS = ["claim:d03_08_unique", "communication_power_claimed"]
# Q110 选 B（A07/S08/A06 本轮章）：这三条医疗台材料加工的完成经验给医疗，不给制造；其他医疗台配方沿原归属
MEDICAL_XP_OUTPUTS = {"saline", "disinfectant", "herb_extract"}
# Q111（A07/S09/S11 本轮章，workshop-adjudications.json fireCharcoal）：火堆被动煅烧，取代晾架炭化
FIRE_CHARCOAL = {
    "id": "fire_charcoal",
    "name": "煅烧",
    "target": "fire_slot_batch",
    "inputId": "wood",
    "outputId": "charcoal",
    "allowedBatchMl": [],
    "heatMinutes": 60,
    "waterCostMl": 0,
    "requiredOwner": "fire_processing_slot",
    "requiredLiquid": None,
    "inputCount": 4,
    "outputCount": 3,
}
CHARCOAL_DESC = "锻炉燃料与材料。在火堆卡点“煅烧木炭”：木材×4→木炭×3，占1个加工位、需60有效受热分钟，火堆燃料另计。"


def parse_md_table(text, header_prefix):
    lines = text.splitlines()
    for i, ln in enumerate(lines):
        if ln.startswith(header_prefix):
            rows = []
            for row in lines[i + 2:]:
                if not row.startswith("|"):
                    break
                rows.append([c.strip() for c in row.strip().strip("|").split("|")])
            return rows
    raise SystemExit("找不到表：" + header_prefix)


def parse_counts(s):
    out = {}
    for part in s.split(","):
        k, v = part.split(":")
        out[k.strip()] = int(v)
    return out


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(A12, encoding="utf-8") as f:
        a12 = json.load(f)
    with open(A07, encoding="utf-8") as f:
        a07 = f.read()
    with open(MIGRATION, encoding="utf-8") as f:
        for m in json.load(f):
            UNIQUE_MOVE[m["itemId"]] = m["to"]
    items = {i["id"]: i for i in cfg["items"]}
    recipes = cfg["recipes"]
    by_id = {r["id"]: r for r in recipes}
    knowledge = {k["id"]: k for k in cfg["knowledge"]}

    def put_recipe(r):
        if r["id"] in by_id:
            by_id[r["id"]].clear()
            by_id[r["id"]].update(r)
        else:
            recipes.append(r)
            by_id[r["id"]] = r

    def put_knowledge(k):
        knowledge[k["id"]] = k

    # 旧配方：原样补 station/group
    for rid in ("knife", "axe", "bandage", "make_wooden_spear", "make_wooden_bow", "make_arrows", "make_metal_spear",
                "make_crossbow", "make_bolts", "make_knuckle_wraps", "make_antiviral_cure", "repair_selected_tool", "salvage_equipment"):
        r = by_id[rid]
        r["station"] = "hand"
    for rid in ("shelter", "bed"):
        by_id[rid]["station"] = "hand"
        by_id[rid]["group"] = "camp"

    # ── 工坊建造（A07 工坊设施；drying_rack 不导）──
    ws_rows = parse_md_table(a07, "|ID|名称|材料|分钟|制造等级|燃料|")
    stations = [{"id": "hand", "name": "手工", "note": "无工坊，随身即可"}]
    for row in ws_rows:
        sid, name, mats, minutes, lv, fuel = row
        if sid not in STATION_NAMES:
            continue
        rec = {
            "id": "build_" + sid,
            "name": "建" + name,
            "kind": "build",
            "baseMinutes": int(minutes),
            "inputs": parse_counts(mats),
            "outputs": {},
            "catalysts": [],
            "requirement": {"facility": "camp"},
            "facilityResult": "workshop:" + sid,
            "minSkills": {"crafting": int(lv)},
            "station": "hand",
            "group": "workshop",
        }
        if sid == "armory":
            rec["knowledge"] = ARMORY_KNOWLEDGE
        put_recipe(rec)
        stations.append({"id": sid, "name": name, "buildRecipe": rec["id"],
                         "note": "锻造每批另付木炭1作燃料" if sid == "forge" else ""})
    put_knowledge({
        "id": ARMORY_KNOWLEDGE,
        "name": "军械台蓝图",
        "grantedByFacts": list(ARMORY_GRANT_FACTS),
        "blueprint": True,
        "source": "蓝图：d03_08 封存仓终点核心记录领取",
    })

    # ── 72 件装备：62 个配方 + 10 件不可自制 ──
    sources = []
    for x in a12["equipment"]["weapons"] + a12["equipment"]["gear"]:
        iid = x["id"]
        acq = x.get("acquisition", {})
        grant = acq.get("grant", "")
        node = NODE_MOVE.get(acq.get("node", ""), acq.get("node", ""))
        craftable = bool(x.get("inputs")) and x.get("minutes", 0) > 0
        if not craftable:
            # B10 试玩修复 10：玩家可见文字只留来源类别与地点，不显示策划内部备注
            unode = UNIQUE_MOVE.get(iid, node)
            kind = "一次性交换" if "交换" in grant else "固定箱拾获"
            sources.append({"itemId": iid, "craftable": False, "recipeId": None, "knowledge": None,
                            "text": f"不可自制：{kind}（{unode}）"})
            continue
        default = "默认知识" in grant
        kid = "blueprint_" + iid
        if iid in INITIAL_KNOWN:
            ktext = "开局已会（原有配方）"
            put_knowledge({"id": kid, "name": f"{x['name']}的做法", "grantedByFacts": [], "initial": True, "source": ktext})
        elif default:
            ktext = "营区登记默认知识（h02 广场）"
            old = knowledge.get(kid)
            put_knowledge({"id": kid, "name": (old or {}).get("name", f"{x['name']}的做法"), "grantedByFacts": ["hub_registered"], "source": ktext})
        else:
            ktext = f"蓝图：{node}（地图固定箱与任务在后续版本开放）"
            put_knowledge({"id": kid, "name": f"{x['name']}蓝图", "grantedByFacts": [], "blueprint": True, "source": ktext})
        rid = LEGACY_RECIPE.get(iid, "make_" + iid)
        old = by_id.get(rid, {})
        rec = {
            "id": rid,
            "name": x["name"],
            "kind": "craft",
            "baseMinutes": int(x["minutes"]),
            "inputs": dict(x["inputs"]),
            "outputs": {iid: 1},
            "catalysts": [],
            "requirement": None,
            "facilityResult": None,
            "station": x["station"],
            "group": "equipment",
            "knowledge": kid,
        }
        if x["station"] == "forge":
            rec["fuel"] = dict(FORGE_FUEL)
        lv = int(x["craft_level"])
        # 旧金属矛/弩写过 crafting 1；护拳绑带 crafting 3；其余按表（1 级不写也等价，统一写上便于计数）
        rec["minSkills"] = {"crafting": lv}
        if old.get("catalysts"):
            rec["catalysts"] = old["catalysts"]
        put_recipe(rec)
        sources.append({"itemId": iid, "craftable": True, "recipeId": rid, "knowledge": kid,
                        "text": f"{STATION_NAMES[x['station']]}／制造{lv}／{x['minutes']}分钟；{ktext}"})
        if iid not in items:
            raise SystemExit("缺物品定义：" + iid)

    # ── A07 材料加工链（drying_rack 炭化不导）──
    chain = parse_md_table(a07, "|产物|材料|分钟|等级|工坊|")
    for out, mats, minutes, lv, station in chain:
        if station not in STATION_NAMES:
            continue
        oid, n = out.split(":")
        rid = "make_" + oid
        skill = "medicine" if station == "medical" else "crafting"
        rec = {
            "id": rid,
            "name": items[oid]["name"],
            "kind": "craft",
            "baseMinutes": int(minutes),
            "inputs": parse_counts(mats),
            "outputs": {oid: int(n)},
            "catalysts": [],
            "requirement": None,
            "facilityResult": None,
            "minSkills": {skill: int(lv)},
            "station": station,
            "group": "material",
        }
        if station == "medical" and oid in MEDICAL_XP_OUTPUTS:
            rec["xpSkill"] = "medicine"
        if station == "forge":
            rec["fuel"] = dict(FORGE_FUEL)
        for k in list(rec["inputs"]) + [oid]:
            if k not in items:
                raise SystemExit("缺物品定义：" + k)
        put_recipe(rec)

    # B10 试玩修复 1：制造3级工坊装配、7级专业制造随 B10 生效；10级大师装配的等级配方已开放、定向词条未做（partial）
    for n in cfg["skillNodes"]:
        if n["id"] in ("crafting_03", "crafting_07"):
            n["status"] = "live"
        elif n["id"] == "crafting_10":
            n["status"] = "partial"
    # Q111：火堆煅烧进程（按 ID 覆盖）与木炭说明
    procs = cfg["processes"]
    procs[:] = [x for x in procs if x["id"] != FIRE_CHARCOAL["id"]] + [dict(FIRE_CHARCOAL)]
    items["charcoal"]["desc"] = CHARCOAL_DESC
    cfg["knowledge"] = list(knowledge.values())
    cfg["workshops"] = {
        "source": "A07「工坊设施」「材料加工链」；A05 §3；S08 X4；S09 X2.7；开发计划 v0.9 B10（tools/import-b10-workshops.py）",
        "buildLocations": ["camp"],
        "stations": stations,
        "equipmentSources": sources,
    }
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    eq = [r for r in recipes if r.get("group") == "equipment"]
    print("装备配方", len(eq), "不可自制", sum(1 for s in sources if not s["craftable"]),
          "加工链", sum(1 for r in recipes if r.get("group") == "material"),
          "工坊", sum(1 for r in recipes if r.get("group") == "workshop"))


if __name__ == "__main__":
    main()
