# -*- coding: utf-8 -*-
"""B13a 答复 Q114—Q119 的地图配置补丁（开发计划 v0.9 批次 B13a，R127/R128）。

数值与文字逐条取自策划数据 assets/20261102/map/map-adjudications.json，不自补：
- exposure（Q118，135 个新节点 indoor 76 / outdoor 59）：写进 locations[].exposure；旧 8 地点不在表里、原标签不动。
- clues（Q117，45 个新普通野外各一条 75% 文案）：写进 locations[].clue；其余节点没有 clue（旧 8 地点、公开自动 100、地牢房间）。
  日志引导语 map.clueText 改为“一条安全线索：”（开发方措辞），后接逐点文案。
- serviceMigration（Q119 选 B）：map.servicesAtV3Nodes = true。
同时断言表内 worldKind / landOwner / safehouseAllowed 与 B11 已导入的节点字段一致（不一致就报错，不覆盖）。

在 import-b11-map.py 之后运行；可重复运行，结果相同。
用法：python tools/import-b13a-map.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261102", "map", "map-adjudications.json")
STARTER = {"camp", "field", "stream", "woods", "village", "hardware", "clinic", "scrapyard"}


def main():
    cfg = json.load(open(CONFIG, encoding="utf-8"))
    adj = json.load(open(ADJ, encoding="utf-8"))
    locs = {l["id"]: l for l in cfg["locations"]}

    exposure = adj["exposure"]
    assert len(exposure) == 135, len(exposure)
    for x in exposure:
        l = locs[x["nodeId"]]
        assert x["nodeId"] not in STARTER
        assert x["exposure"] in ("indoor", "outdoor"), x
        assert (l["worldKind"], l["landOwner"], l["buildAllowedSafehouse"]) == (x["worldKind"], x["landOwner"], x["safehouseAllowed"]), x["nodeId"]
        l["exposure"] = x["exposure"]
    assert sum(1 for x in exposure if x["exposure"] == "indoor") == 76

    clues = adj["clues"]
    assert len(clues) == 45, len(clues)
    for l in cfg["locations"]:
        l.pop("clue", None)
    for c in clues:
        l = locs[c["nodeId"]]
        assert c["atPercent"] == 75 and not c["extraItems"] and not c["revealEdges"] and not c["revealResources"] and c["extraExperience"] == 0, c
        assert l["zone"] != "starter" and l["nodeKind"] != "dungeon" and not l.get("exploreAuto100OnVisit"), c["nodeId"]
        l["clue"] = c["text"]

    cfg["map"]["clueText"] = "一条安全线索："
    sm = adj["serviceMigration"]
    assert sm["servicesAtV3Nodes"] is True and sm["hub"] == cfg["party"]["hubV3"] and sm["shelter"] == cfg["party"]["shelterV3"]
    cfg["map"]["servicesAtV3Nodes"] = True

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"exposure：{len(exposure)} 点（室内 76）；clue：{len(clues)} 点；servicesAtV3Nodes=true")


if __name__ == "__main__":
    main()
