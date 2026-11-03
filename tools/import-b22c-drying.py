# -*- coding: utf-8 -*-
"""
B22c（开发计划 v0.9，需求 R169）：策划答复 Q170 八位晾架导入 data/config.v0.6.json。
可重复运行（按 ID 覆盖本工具生成的条目，不动其他配置）。

来源：
- assets/20261103/survival-adjudications.json key drying（station drying_rack 晾制架、slots 8、construction、recipes dry_berries/dry_meat）
- 04 附录/A07「工坊设施」drying_rack 行（wood:4,rope:2、60 分钟、制造1、Q111 不含炭化附件）、「二十种食品用品」果干/肉干
- 01 基础系统/S11「Q163—Q170」八位晾架完整定稿；02 开发方待确认问题 Q107/Q134（工坊分区许可，每区每类一座）
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261103", "survival-adjudications.json")

STATION = "drying_rack"
BUILD = {"id": "build_drying_rack", "name": "建晾制架", "kind": "build", "baseMinutes": 60,
         "inputs": {"wood": 4, "rope": 2}, "outputs": {}, "catalysts": [],
         "requirement": {"facility": "camp"}, "facilityResult": "workshop:" + STATION,
         "minSkills": {"crafting": 1}, "station": "hand", "group": "workshop"}
NAMES = {"dry_berries": "晾制果干", "dry_meat": "晾制肉干"}


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(ADJ, encoding="utf-8") as f:
        drying = json.load(f)["drying"]
    assert drying["station"] == STATION

    gen = {BUILD["id"]} | {r["id"] for r in drying["recipes"]}
    cfg["recipes"] = [r for r in cfg["recipes"] if r["id"] not in gen]
    cfg["recipes"].append(dict(BUILD))
    for r in drying["recipes"]:
        cfg["recipes"].append({
            "id": r["id"], "name": NAMES[r["id"]], "kind": "drying", "baseMinutes": r["minutes"],
            "inputs": dict(r["inputs"]), "outputs": dict(r["output"]), "catalysts": [],
            "requirement": None, "facilityResult": None, "minSkills": {r["skill"]: r["level"]},
            "station": STATION, "mode": "passive", "group": "food",
        })

    stations = cfg["workshops"]["stations"]
    cfg["workshops"]["stations"] = [x for x in stations if x["id"] != STATION]
    cfg["workshops"]["stations"].append({
        "id": STATION, "name": drying["name"], "buildRecipe": BUILD["id"],
        "note": f"{drying['slots']}个独立晾制位，每位一批，被动晾制果干/肉干；无炭化附件", "slots": drying["slots"],
    })

    items = {i["id"]: i for i in cfg["items"]}
    items["dried_berries"]["desc"] = "策划用途：饱食+16、水分−2。食用5分钟，战斗中不能吃。来源：晾制架晾制（野果2→果干1，120分钟）"
    items["dried_meat"]["desc"] = "策划用途：饱食+28、水分−5。食用5分钟，战斗中不能吃。来源：晾制架晾制（生肉1+盐1→肉干1，180分钟，需烹饪5级）、地图固定包"

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("B22c: drying rack config, recipes", sorted(gen))


if __name__ == "__main__":
    main()
