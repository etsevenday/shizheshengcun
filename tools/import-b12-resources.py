# -*- coding: utf-8 -*-
"""B12 资源四账、claim 迁移与新节点清理的配置导入（开发计划 v0.9 批次 B12，R36/R37 + Q31 清理部分）。

数值与结构逐条取自策划数据，不自补：
- map-v3-normalized.json resources：96 条增量资源（地点、产物与数量、工作分钟、cap、日补、unit、scope、kind、揭示百分比）。
  已含 A07 X1.7 的 6 条调整与 3 个新保底点（本脚本逐条断言核对）。
  world_stock：renewable/cistern/world_addition → renewable_units（日补夹到 cap），finite → finite_units（不补），
  flowing（cap "-1"）→ unlimited；instanceId → finite_units + scope instanceId（每代恢复，S04 四账）；
  campaignClaimId → campaign_claim（单局永久一次，领取写事实，不发物资卡）。
- progression-adjudications.json fieldExperience.resourceRows（Q55/Q59 答复）：116 行熟练归属与 xpAllowed（records 与城市配给不给经验）。
- node-expansion-rules.json（Q31 答复）：135 新节点清理开放（clearEnabled、clearLabel “清理翻找/整理搜集”、20 基础分钟、
  world_minute_serial 480/720/1440 或 instance_generation_once）。
- gate-contract.json factProducers：核心领取产生的门槛事实（d01_08→FA02_record_credit、d02_08→FA03_record_credit、
  d03_08→communication_power_claimed）；R03 容量事实 church_route_record / academy_safety_record 用记录 ID 本身。
- legacy-node-claim-migration.json + expansion-combat.json legacyUniqueRewards：旧核心节点映射与 6 个旧唯一箱 claimId→新节点（R37 迁移账）。

资源点显示名与 8 份核心记录的中文名是开发方措辞（开发假设 H6/H10），策划数据只有物品/记录 ID。

可重复运行，结果相同。
用法：python tools/import-b12-resources.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ASSETS = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets")
MAPDIR = os.path.join(ASSETS, "20261030", "narrative-map")

# 8 份核心记录的显示名（开发方措辞，H10）；前两份沿用容量说明里已有的“教会路线记录/学院安全记录”
RECORD_NAMES = {
    "church_route_record": "教会路线记录",
    "academy_safety_record": "学院安全记录",
    "communication_power": "通信电源",
    "chapter_contact": "分会联络记录",
    "waterworks_record": "水厂运行记录",
    "mining_blueprint_pack": "矿道工作方法包",
    "civilian_cache_knowledge": "民用储备记录",
    "coldchain_knowledge": "冷链仓储记录",
}

# gate-contract factProducers 明写由核心领取产生的事实
CLAIM_PRODUCED_FACTS = {
    "d01_08_unique": ["FA02_record_credit"],
    "d02_08_unique": ["FA03_record_credit"],
    "d03_08_unique": ["communication_power_claimed"],
}

POOL = {
    "renewable": "renewable_units",
    "cistern": "renewable_units",
    "world_addition": "renewable_units",
    "finite": "finite_units",
    "flowing": "unlimited",
    "instance": "finite_units",
    "campaign": "campaign_claim",
}

# X1.7 六条调整与三个新保底点（A07 X1.7 表）：(resourceId, itemId, 每次, cap, 日补[, nodeId, 揭示, 分钟])
X17_ADJUST = [
    ("w11_orchard_a", "raw_root", 2, 12, 4),
    ("w26_island_a", "raw_root", 2, 16, 6),
    ("w34_depot_a", "canned_beans", 1, 6, 0),
    ("w34_depot_b", "saline", 1, 6, 0),
    ("w11_orchard_b", "cloth", 2, 8, 2),
    ("w14_cemeteryroad_b", "cloth", 2, 8, 2),
]
X17_NEW = [
    ("h03_workshop_supply_disinfectant", "h03_workshop", 100, 20, "disinfectant", 1, 4, 1),
    ("w13_mill_supply_salt", "w13_mill", 50, 25, "salt", 1, 6, 2),
    ("w15_shrinewoods_supply_berry", "w15_shrinewoods", 50, 25, "berry", 2, 8, 2),
]


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def resource_name(r, item_names):
    """资源点显示名（开发方按物品名起，H6）。"""
    item = next(iter(r["items"]))
    kind = r["kind"]
    if kind == "campaign":
        return RECORD_NAMES[item]
    n = item_names[item]
    if kind == "instance":
        return f"{n}物资箱"
    if kind == "flowing":
        return f"{n}水流"
    if kind == "cistern":
        return f"{n}蓄水池"
    if kind == "world_addition":
        return f"{n}配给点" if r["nodeId"].startswith("h") else f"{n}补给点"
    if kind == "finite":
        return f"{n}存货"
    return f"{n}点"


def main():
    cfg = load(CONFIG)
    mp = load(os.path.join(MAPDIR, "map-v3-normalized.json"))
    prog = load(os.path.join(ASSETS, "20261031", "progression", "progression-adjudications.json"))
    rules = load(os.path.join(MAPDIR, "node-expansion-rules.json"))
    legacy = load(os.path.join(MAPDIR, "legacy-node-claim-migration.json"))
    combat = load(os.path.join(ASSETS, "20261030", "combat", "expansion-combat.json"))

    res96 = mp["resources"]
    assert len(res96) == 96
    rows = {r["resourceId"]: r for r in prog["fieldExperience"]["resourceRows"]}
    assert len(rows) == 116
    item_names = {i["id"]: i["name"] for i in cfg["items"]}
    loc_ids = {l["id"] for l in cfg["locations"]}

    by_id = {r["id"]: r for r in res96}
    for rid, item, per, cap, refill in X17_ADJUST:
        r = by_id[rid]
        assert r["items"] == {item: per} and int(r["cap"]) == cap and r["dailyRefill"] == refill, rid
    assert by_id["w34_depot_a"]["kind"] == "finite" and by_id["w34_depot_b"]["kind"] == "finite"
    for rid, node, reveal, minutes, item, per, cap, refill in X17_NEW:
        r = by_id[rid]
        assert (r["nodeId"], r["reveal"], r["workMinutes"], r["items"], int(r["cap"]), r["dailyRefill"]) == \
            (node, reveal, minutes, {item: per}, cap, refill), rid

    new_ids = {r["id"] for r in res96}
    kept = [r for r in cfg["resources"] if r["nodeId"] not in new_ids]
    assert len(kept) == 20, len(kept)
    for r in kept:
        assert rows[r["nodeId"]]["proficiencyId"] == r.get("proficiencyId"), r["nodeId"]

    added = []
    for r in res96:
        assert r["nodeId"] in loc_ids, r["id"]
        assert r["unit"] == "item_count", r["id"]
        assert len(r["items"]) == 1, r["id"]
        row = rows[r["id"]]
        kind = r["kind"]
        cap = int(r["cap"])
        pool = POOL[kind]
        scope = r["scope"]
        assert (scope == "instanceId") == (kind == "instance") and (scope == "campaignClaimId") == (kind == "campaign"), r["id"]
        item = next(iter(r["items"]))
        if kind != "campaign":
            assert item in item_names, r["id"]
        entry = {
            "nodeId": r["id"],
            "name": resource_name(r, item_names),
            "locationId": r["nodeId"],
            "revealAt": r["reveal"] if r["reveal"] > 0 else None,
            "revealOnVisit": r["reveal"] == 0,
            "baseMinutes": r["workMinutes"],
            "cap": None if pool == "unlimited" else cap,
            "dailyRefill": r["dailyRefill"],
            "poolMode": pool,
            "outputs": dict(r["items"]),
            "toolBonus": None,
            "hazard": None,
            "proficiencyId": row["proficiencyId"],
            "scope": scope,
            "kind": kind,
            "xpAllowed": bool(row["xpAllowed"]),
        }
        if kind == "flowing":
            assert cap == -1
        if kind == "campaign":
            entry["recordId"] = item
            entry["claimFacts"] = CLAIM_PRODUCED_FACTS.get(r["id"], [])
        added.append(entry)
    cfg["resources"] = kept + added

    # Q31 新节点清理开放（S04 Q31 节、node-expansion-rules.json）
    exp = {n["nodeId"]: n for n in rules["nodes"]}
    assert len(exp) == 135
    for l in cfg["locations"]:
        x = exp.get(l["id"])
        if x is None:
            continue
        assert x["clearEnabled"] is True
        assert l["clear"] == {"cap": x["cap"], "initialCharges": x["initial"], "refillIntervalMinutes": x["serialRefillMinutes"] or 0}, l["id"]
        assert l["clearMinutes"] == x["workMinutes"] and l["clearRefillMode"] == x["clearRefillMode"], l["id"]
        l["clearEnabled"] = True
        l["clearLabel"] = x["clearLabel"]

    # R37 旧 claim 迁移账（A08「来源、知识与旧档迁移」）
    lur = {u["campaignClaimId"]: u for u in combat["legacyUniqueRewards"]}
    claims = []
    for cid, node in legacy["claimNodes"].items():
        assert node in loc_ids, cid
        u = lur.get(cid)
        claims.append({
            "claimId": cid,
            "node": node,
            "grants": (u or {}).get("grants") or (u or {}).get("items") or {},
        })
    mpcfg = cfg["map"]
    mpcfg.pop("resourceMarkers", None)
    mpcfg["legacyCoreNodes"] = dict(legacy["coreNodeByOldSixNode"], **{k: v for k, v in legacy["coreNodeByOldSevenNode"].items()})
    mpcfg["legacyClaims"] = claims
    mpcfg["recordNodes"] = legacy["recordNodes"]

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
