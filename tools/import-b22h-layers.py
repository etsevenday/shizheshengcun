# -*- coding: utf-8 -*-
"""B22h mainline 四区 21 节点逐敌层表导入（开发计划 v0.9 批次 B22h，需求 R195；Q192 选 C）→ data/config.v0.6.json 各地点 populationLayers。

数值逐条取自策划数据 assets/20261103/party-combat-expedition-adjudications.json 的 layering 合同，不自补：
- rows：21 个 mainline 节点（mainline_wild 12、mainline_works 3、mainline_aid 1、mainline_oldcity 5），每节点 ordinary/rare/strong 三层，
  层内是原池权重（层内相对归一化，不再直接当跨层概率）；
- 断言：每种原池敌恰归一层、三层并集等于原 populationWeights，节点所属人口区与合同一致；b01_school/b02_station 不入表；
- 层权重 96/3.5/0.5 与第 1/3/7 日开放沿 worldEnemies.regions 的 layerWeights/layerOpenWorldDay（同时断言与合同一致）。

可重复运行，结果相同（只覆盖这 21 个地点的 populationLayers）。
用法：python tools/import-b22h-layers.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
CONTRACT = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261103", "party-combat-expedition-adjudications.json")
LAYERS = ("ordinary", "rare", "strong")


def main():
    with open(CONTRACT, encoding="utf-8") as f:
        lay = json.load(f)["layering"]
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    locs = {l["id"]: l for l in cfg["locations"]}
    regions = {r["id"]: r for r in cfg["worldEnemies"]["regions"]}
    rows = lay["rows"]
    assert len(rows) == 21 and len({r["nodeId"] for r in rows}) == 21, "21 个节点"
    assert not {"b01_school", "b02_station"} & {r["nodeId"] for r in rows}, "b01/b02 不入表"
    for rid, reg in regions.items():
        if not rid.startswith("mainline_"):
            continue
        assert reg["layerWeights"] == {k: v for k, v in lay["weights"].items()} or all(
            float(reg["layerWeights"][k]) == float(v) for k, v in lay["weights"].items()), rid
        assert all(int(reg["layerOpenWorldDay"][k]) == int(v) for k, v in lay["openDay"].items()), rid
    for r in rows:
        loc = locs[r["nodeId"]]
        assert loc.get("populationRegionId") == r["regionId"], r["nodeId"]
        assert r["regionId"] in regions and r["nodeId"] in regions[r["regionId"]]["nodes"], r["nodeId"]
        layers = {k: dict(r["layers"].get(k, {})) for k in LAYERS}
        union = {}
        for k in LAYERS:
            for e, w in layers[k].items():
                assert e not in union, f"{r['nodeId']} {e} 重复入层"
                assert isinstance(w, int) and w > 0, f"{r['nodeId']} {e}"
                union[e] = w
        assert union == loc["populationWeights"], f"{r['nodeId']} 层表并集与原池不一致"
        loc["populationLayers"] = layers
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
