# -*- coding: utf-8 -*-
"""
B17（开发计划 v0.9）：安全屋与分区设施（需求 R52—R56）导入 data/config.v0.6.json。
可重复运行（按 ID 覆盖本工具生成的条目，不动其他配置）。

来源：
- 01 基础系统/S09「2026-10-30 用户最终裁定：全部非势力野外可建」、X2.1—X2.9（安全屋基础 wood8/fiber6/cloth2/stone4、240 分钟、制造1、营区登记；
  屋内床垫 fiber4/cloth2、60 分钟；屋内固定火堆 stone4/wood2、30 分钟；固定柜 200000g）
- 04 附录/A07「X1.4 两种新固定设施」（陶窑 stone8/clay6/iron_plate2、180 分钟、制造3、ceramic_methods；
  固定净水台 ceramic_shell4/plank2/cloth2、90 分钟、制造3、ceramic_methods）、「X1.6 野营、小修与医疗限制」、「工坊设施」
- assets/20261030/facilities/expansion-supply.json facilities、operationalRules.camping（野营布铺/收各 10 分钟、至多 4 人；
  折叠柴炉部署/收起各 5 分钟、燃料上限 60、木材每份 60）
- 02 开发方待确认问题 Q107（屋内每区每类一座工坊）、Q118（住宅许可只看节点 buildAllowed.safehouse 等）、Q134（住宅禁建原因归住宅入口）
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
SUPPLY = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "facilities", "expansion-supply.json")

SAFEHOUSE_RECIPES = [
    {"id": "safehouse_base", "name": "建安全屋", "kind": "build", "baseMinutes": 240,
     "inputs": {"wood": 8, "fiber": 6, "cloth": 2, "stone": 4}, "outputs": {}, "catalysts": [],
     "requirement": {"facility": "safehouse_site"}, "facilityResult": "safehouse",
     "minSkills": {"crafting": 1}, "station": "hand", "group": "safehouse"},
    {"id": "safehouse_bed", "name": "铺屋内床垫", "kind": "build", "baseMinutes": 60,
     "inputs": {"fiber": 4, "cloth": 2}, "outputs": {}, "catalysts": [],
     "requirement": {"facility": "safehouse_interior"}, "facilityResult": "bedBuilt",
     "minSkills": {"crafting": 1}, "station": "hand", "group": "safehouse"},
    {"id": "safehouse_fire", "name": "建屋内火堆", "kind": "build", "baseMinutes": 30,
     "inputs": {"stone": 4, "wood": 2}, "outputs": {}, "catalysts": [],
     "requirement": {"facility": "safehouse_interior"}, "facilityResult": "fireBuilt",
     "minSkills": {"crafting": 1}, "station": "hand", "group": "safehouse"},
]

NEW_STATIONS = {
    "kiln": ("陶窑", "build_kiln", "建陶窑", "每批另耗木炭1；只烧陶壳"),
    "water_station": ("固定净水台", "build_water_station", "建固定净水台", "每批耗过滤材料1，无火；一批脏水4份/2L"),
}


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(SUPPLY, encoding="utf-8") as f:
        supply = json.load(f)
    camping = supply["operationalRules"]["camping"]
    tarp = camping["field_tarp"]
    stove = camping["folding_stove"]

    cfg["safehouse"] = {
        "source": "S09 X2.1—X2.9、A07 X1.4/X1.6、expansion-supply.json operationalRules.camping（B17）",
        "buildRecipe": "safehouse_base",
        "registeredFact": "hub_registered",
        "requiredExplore": 100,
        "storageCapacityGrams": 200000,
        "camping": {
            "tarpItemId": "field_tarp", "tarpDeployMinutes": tarp["deployMinutes"], "tarpPackMinutes": tarp["packMinutes"],
            "tarpPartyCapacity": tarp["partyCapacity"],
            "stoveItemId": "folding_stove", "stoveDeployMinutes": stove["deployMinutes"], "stovePackMinutes": stove["packMinutes"],
            "stoveMaxFuelMinutes": stove["maxFuelMinutes"],
        },
    }

    gen = {r["id"] for r in SAFEHOUSE_RECIPES} | {v[1] for v in NEW_STATIONS.values()}
    cfg["recipes"] = [r for r in cfg["recipes"] if r["id"] not in gen]
    cfg["recipes"].extend(SAFEHOUSE_RECIPES)

    facilities = {x["id"]: x for x in supply["facilities"]}
    for sid, (name, rid, rname, note) in NEW_STATIONS.items():
        fx = facilities[sid]
        cfg["recipes"].append({
            "id": rid, "name": rname, "kind": "build", "baseMinutes": fx["baseMinutes"],
            "inputs": dict(fx["inputs"]), "outputs": {}, "catalysts": [],
            "requirement": {"facility": "camp"}, "facilityResult": "workshop:" + sid,
            "minSkills": {"crafting": fx["craftingLevel"]}, "station": "hand", "group": "workshop",
            "knowledge": fx["knowledgeId"],
        })
        st = next(x for x in cfg["workshops"]["stations"] if x["id"] == sid)
        st["name"] = name
        st["buildRecipe"] = rid
        st["note"] = note

    items = {i["id"]: i for i in cfg["items"]}
    items["field_tarp"]["desc"] = "野营遮雨布：在普通野外的外部区（地牢、城市与势力公共地、任务保留地除外）铺开10分钟、收起10分钟；铺开后当地睡眠算遮蔽（至多4人），不给安全屋休息精神加成，也不挡遭遇。"
    items["folding_stove"]["desc"] = "折叠柴炉：在普通野外临时营地许可点部署5分钟，当作一处火堆（2个加工位、最多1口锅、燃料上限60分钟）；收起5分钟，须先取空、熄火、燃料清0。"

    # B17 试玩修复 M362（B4）：已有配方用到的物品，悬停说明不再写“本版只有物品定义”，改写现有用途（配方名，最多 4 个）
    placeholder = "本版只有物品定义，用途在后续版本开放"
    uses = {}
    for r in cfg["recipes"]:
        for k in list(r.get("inputs", {})) + list((r.get("fuel") or {})):
            uses.setdefault(k, [])
            if r["name"] not in uses[k]:
                uses[k].append(r["name"])
    for it in cfg["items"]:
        names = uses.get(it["id"])
        if names and placeholder in it.get("desc", ""):
            shown = "、".join(names[:4]) + ("等" if len(names) > 4 else "")
            it["desc"] = it["desc"].replace(placeholder, f"现在可用于：{shown}")

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("B17: safehouse config, recipes", sorted(gen))


if __name__ == "__main__":
    main()
