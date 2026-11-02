# -*- coding: utf-8 -*-
"""B11 v3 地图数据、门槛与探索的配置导入（开发计划 v0.9 批次 B11，R33–R35）。

数值与结构逐条取自策划数据，不自补：
- map-v3-normalized.json：143 节点（zone、node_kind、parent_city、worldKind、landOwner、buildAllowed.safehouse/temporaryCamp、
  danger、网格 x/y）、180 条边（分钟、双向/单向、gate 令牌、gateContext）、8 座地牢字段、96 条资源（只作 B11 的“揭示标记”，
  采集与四账在 B12）。
- gate-contract.json（Q30 选 B 完整门槛契约）：26 个规范门槛的谓词表达式、解锁生命周期（campaign / current_instance /
  revalidate_current_instance）、别名 from_explore_100→from_explore100。谓词原样写进 config.map.gates，由内核解释执行。
- node-expansion-rules.json（Q31 答复）：135 个新节点逐点探索开关/到访自动100/每次分钟与增量、掉落表、危险度（只作显示）、
  随机接敌开关与基础百分比、人口区本地上限；9 张 q31_* 掉落表；清理参数照抄但 B11 不开放（clearEnabled=false，B12 接入）。

旧 8 地点与 8 条边：ID、分钟、清理、掉落与资源原样保留（node-expansion-rules legacyOverrides 为空）；只补 v3 字段，
区域图坐标按 v3 网格重算（待确认 Q-E：S02 §3.1 方位表 village→clinic、hardware→scrapyard 两行与新网格不同，按新网格）。
旧 unlockAny 条件由同义 gate（field25…hardware100）取代。

玩家可见的门槛未满足原因（factLabels）是开发方措辞（开发假设），对应事实的产生系统在哪一批写在括号里。

可重复运行，结果相同。
用法：python tools/import-b11-map.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
MAPDIR = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "narrative-map")

# 区域图坐标：网格每格 280×200，原点 (130,100)——旧营地/溪流/诊所的像素位置不变，避免整图大幅漂移
GRID_X0, GRID_Y0, GRID_DX, GRID_DY = 130, 100, 280, 200
PLACEHOLDER_SCENE = "scenes/placeholder.svg"

# 门槛事实的玩家可见说明（开发方措辞）；括号里写产生该事实的系统还没上线时的提示
FACT_LABELS = {
    "tutorialBattleWon": "赢下第一场战斗（荒地的游荡尸）",
    "legacyTutorialEquivalent": "赢下第一场战斗（荒地的游荡尸）",
    "camp_route_known": "从来客程望处得知营区路线（序章来客后续版本开放）",
    "checkpoint_obstruction_cleared": "解决检查哨的固定拦路战（后续版本开放）",
    "registration_authorized": "在营门向登记人办理登记（营区登记后续版本开放）",
    "hub_registered": "完成营区登记（营区登记后续版本开放）",
    "church_contacted": "先到达教会接待庭",
    "academy_contacted": "先到达科学院外院",
    "FA02_route_briefed": "在教会接待庭领取公开路线（教会任务后续版本开放）",
    "FA03_environment_briefed": "在科学院接待处领取环境调查路线（科学院任务后续版本开放）",
    "academy_observation_pass": "在试制工坊门旁确认只沿观察通道（后续版本开放）",
    "FA02_record_credit": "在教会地下圣所核心领取路线记录",
    "FA03_record_credit": "在封存实验楼核心领取流程记录",
    "FA04_merged": "在营区广场完成两份记录的合并（主线后续版本开放）",
    "communication_power_claimed": "在警备仓封存仓核心领取通信电源",
    "guardianDefeated": "击败本次地牢的守卫（地牢守卫战后续版本开放）",
    "recordClaimedAndClear": "领取（或核对）本次地牢的核心记录",
}

# 本批已有真实产生来源的事实（gate-contract factProducers）：c02/r02 实际到达成功即提交
ARRIVAL_FACTS = {"c02_church": ["church_contacted"], "r02_academy": ["academy_contacted"]}


def load(name):
    with open(os.path.join(MAPDIR, name), encoding="utf-8") as f:
        return json.load(f)


def loot_table(t):
    assert t["weightTotal"] == 100 and t["drawsPerCompletion"] == 1, t["id"]
    entries = [{"itemId": o["itemId"], "count": o["quantity"], "weight": o["weightPercent"]} for o in t["outcomes"]]
    assert sum(e["weight"] for e in entries) == 100, t["id"]
    return {"version": "Q31_nodes_v1", "entries": entries}


def main():
    cfg = json.load(open(CONFIG, encoding="utf-8"))
    mp = load("map-v3-normalized.json")
    gc = load("gate-contract.json")
    rules = load("node-expansion-rules.json")

    assert len(mp["nodes"]) == 143 and len(mp["edges"]) == 180
    legacy_ids = rules["legacyNodeIds"]
    assert rules["legacyOverrides"] == []
    exp = {n["nodeId"]: n for n in rules["nodes"]}
    assert len(exp) == 135

    old_locs = {l["id"]: l for l in cfg["locations"] if l["id"] in legacy_ids}
    assert len(old_locs) == 8
    old_edges = {(e["a"], e["b"]): e for e in cfg["edges"] if e["a"] in legacy_ids and e["b"] in legacy_ids}
    assert len(old_edges) == 8

    dungeon_of = {}
    for d in mp["dungeons"]:
        for n in mp["nodes"]:
            if n["zone"] == d["id"]:
                dungeon_of[n["id"]] = d["id"]

    locations = []
    for n in mp["nodes"]:
        gx, gy = n["x"], n["y"]
        common = {
            "zone": n["zone"],
            "nodeKind": n["node_kind"],
            "parentCity": n["parent_city"] or None,
            "worldKind": n["worldKind"],
            "landOwner": n["landOwner"],
            "buildAllowedSafehouse": bool(n["buildAllowed"].get("safehouse", False)),
            "temporaryCamp": bool(n["buildAllowed"].get("temporaryCamp", False)),
            "gridX": gx,
            "gridY": gy,
            "mapX": GRID_X0 + gx * GRID_DX,
            "mapY": GRID_Y0 + gy * GRID_DY,
        }
        if n["id"] in old_locs:
            loc = dict(old_locs[n["id"]])
            for k in ("unlockAny",):
                loc.pop(k, None)
            loc.update(common)
            locations.append(loc)
            continue
        x = exp[n["id"]]
        assert x["zone"] == n["zone"] and x["nodeKind"] == n["node_kind"], n["id"]
        assert x["danger"] == n["danger"], n["id"]
        wp = x["worldPopulation"]
        loc = {
            "id": n["id"],
            "name": n["name"],
            # 室内/露天策划数据没给：按露天（不给睡眠遮蔽，最保守；开发假设）
            "exposure": "outdoor",
            "initialProgress": 0,
            "initiallyDiscovered": False,
            "initiallyVisited": False,
            "hasFire": False,
            "canBuildShelter": False,
            "scene": PLACEHOLDER_SCENE,
            "lootTableId": x["lootTableId"],
            # 清理照抄 Q31 逐点初值，但本批不开放（B12 接资源点与清理）
            "clearEnabled": False,
            "clear": {"cap": x["cap"], "initialCharges": x["initial"], "refillIntervalMinutes": x["serialRefillMinutes"] or 0},
            "clearMinutes": x["workMinutes"],
            "clearRefillMode": x["clearRefillMode"],
        }
        loc.update(common)
        loc.update({
            "danger": x["danger"],
            "exploreEnabled": x["exploreEnabled"],
            "exploreAuto100OnVisit": x["exploreAuto100OnVisit"],
            "exploreMinutes": x["exploreMinutes"] if x["exploreEnabled"] else None,
            "exploreIncrement": x["exploreIncrement"] if x["exploreEnabled"] else None,
            "explorationLootEnabled": x["explorationLootEnabled"],
            "randomEncounterEnabled": x["randomEncounterEnabled"],
            "encounterBaseExplorePercent": x["encounterBaseExplorePercent"],
            "encounterBaseClearPercent": x["encounterBaseClearPercent"],
            "populationLocalCap": wp.get("localCap", 0),
            "populationRegionId": wp.get("regionId"),
            "dungeonId": dungeon_of.get(n["id"]),
            "safeExterior": (n.get("safety") or {}).get("exterior") == "safe",
        })
        locations.append(loc)

    # 新增节点与旧 8 的相对顺序：旧 8 在前（保持原顺序），其余按 v3 数据顺序
    order = {lid: i for i, lid in enumerate(legacy_ids)}
    locations.sort(key=lambda l: (0, order[l["id"]]) if l["id"] in order else (1, 0))
    cfg["locations"] = locations

    gates = {g["id"]: g for g in gc["definitions"]}
    aliases = gc["aliases"]
    edges = []
    pairs = set()
    for e in mp["edges"]:
        a, b = e["from"], e["to"]
        assert (a, b) not in pairs and (b, a) not in pairs, e["edgeId"]
        pairs.add((a, b))
        canonical = e["gateContractId"]
        assert canonical in gates and aliases.get(e["gate"], e["gate"]) == canonical, e["edgeId"]
        ctx = e["gateContext"]
        if (a, b) in old_edges:
            old = old_edges[(a, b)]
            assert old["baseMinutes"] == e["minutes"], e["edgeId"]
            eid = old["id"]
        else:
            assert e["edgeId"].startswith("edge_")
            eid = e["edgeId"][len("edge_"):]
        edges.append({
            "id": eid,
            "a": a,
            "b": b,
            "baseMinutes": e["minutes"],
            "initiallyUnlocked": eid == "camp__field",
            "bidirectional": bool(e["bidirectional"]),
            "gate": canonical,
            "gateAlias": e["gateAlias"],
            "role": ctx["edgeRole"],
            "dungeonId": ctx["dungeonId"],
            "dungeonParent": ctx["dungeonParent"],
        })
    assert sum(1 for e in edges if e["bidirectional"]) == 172 and sum(1 for e in edges if not e["bidirectional"]) == 8
    cfg["edges"] = edges

    for t in rules["lootTables"]:
        cfg["lootTables"][t["id"]] = loot_table(t)

    cfg["map"] = {
        "version": mp["version"],
        "gateContractVersion": gc["version"],
        "expansionRulesVersion": rules["version"],
        # Q89 服务迁址（B8b）：h02/h06 在 B11 已入图，但营区登记（register 门槛的事实来源）在 B14，迁过去会走不到；
        # 营区登记上线前保持在 camp 代办（开发假设），B14 打开
        "servicesAtV3Nodes": False,
        "gateAliases": aliases,
        "gates": [{"id": g["id"], "predicate": g["predicate"], "unlockScope": g["unlockScope"]} for g in gc["definitions"]],
        "factLabels": FACT_LABELS,
        "arrivalFacts": ARRIVAL_FACTS,
        "clueText": "一条安全线索：附近的路况与危险迹象已记下（不另发物资）",
        "resourceMarkers": [
            {"id": r["id"], "nodeId": r["nodeId"], "reveal": r["reveal"], "items": r["items"], "scope": r["scope"]}
            for r in mp["resources"]
        ],
        "dungeons": [
            {"id": d["id"], "name": d["name"], "parent": d["parent"], "guardNode": d["guardNode"], "recordNode": d["recordNode"],
             "exitNode": d["exitNode"], "safeNodes": d["safeNodes"]}
            for d in mp["dungeons"]
        ],
    }

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
