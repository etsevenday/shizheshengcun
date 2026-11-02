# -*- coding: utf-8 -*-
"""B13c 答复 Q122—Q125 的配置导入（开发计划 v0.9 批次 B13c，R130）。

数值、名字、知识 ID 逐条取自策划数据 resource-adjudications.json（A08 附表 resource-tables.md 的机器版），不自补：
- resources.uniqueEquipmentCaches（Q122）：6 个旧 d03 唯一箱入资源表（campaign_claim 实物包，claimId=资源 ID 不变）。
  5 箱 revealCondition explore_at_least_100 → revealAt 100；officer_cache（守卫战利品箱）改在 d03_07，
  revealCondition current_d03_generation_guard_victory_committed_player_alive → revealInstanceFact "guardianDefeated"
  （本代地牢实例事实；守卫战本身在 B20，开发入口可记）。各 20 基础分钟、0 经验、无工具，产物整包落当地地面。
  map.legacyClaims 的 officer_cache 新节点由 d03_04 改为 d03_07（legacyMigration.newOfficerNode）。
- coreBlueprintPacks（Q123）：28 张蓝图知识 grantedByFacts 改为该核心的 claim:/campaignId: 事实（d03 另接受
  communication_power_claimed，沿 Q109），来源文字改写为“领取核心记录时一并获得”。
- recordNames（Q125 记录部分）：8 份核心记录改用正式名；容量说明、地图门槛事实说明里的记录名同步。
- regionNames（Q124）：13 个世界分区正式名写入 map.regionNames（面包屑与分区导航同源）。

须在 import-b13b-resources.py 之后运行；可重复运行，结果相同。
用法：python tools/import-b13c-claims.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261102", "map", "resource-adjudications.json")

GUARD_CONDITION = "current_d03_generation_guard_victory_committed_player_alive"


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def main():
    cfg = load(CONFIG)
    adj = load(ADJ)
    res = adj["resources"]
    caches = res["uniqueEquipmentCaches"]
    assert len(caches) == 6
    item_ids = {i["id"] for i in cfg["items"]}
    locs = {l["id"]: l for l in cfg["locations"]}
    d03 = next(d for d in cfg["map"]["dungeons"] if d["id"] == "d03")

    # ───── Q122：6 个旧唯一箱 ─────
    cache_ids = {c["id"] for c in caches}
    kept = [r for r in cfg["resources"] if r["nodeId"] not in cache_ids]
    assert len(kept) == 163, len(kept)
    added = []
    for c in caches:
        assert c["claimId"] == c["id"] and c["scope"] == "campaignClaimId" and c["kind"] == "campaign_physical", c["id"]
        assert c["workMinutes"] == 20 and c["initial"] == 1 and c["cap"] == 1 and c["dailyRefill"] == 0, c["id"]
        assert not c["xpAllowed"] and c["proficiencyId"] is None and c["hazard"] is None, c["id"]
        assert locs[c["nodeId"]]["dungeonId"] == "d03", c["id"]
        for k in c["items"]:
            assert k in item_ids, (c["id"], k)
        entry = {
            "nodeId": c["id"],
            "name": c["displayName"],
            "locationId": c["nodeId"],
            "revealAt": None,
            "revealOnVisit": False,
            "baseMinutes": c["workMinutes"],
            "cap": 1,
            "dailyRefill": 0,
            "poolMode": "campaign_claim",
            "outputs": dict(c["items"]),
            "toolBonus": None,
            "hazard": None,
            "proficiencyId": None,
            "scope": "campaignClaimId",
            "kind": "campaign_physical",
            "xpAllowed": False,
            "legacyAliases": [],
            "equipmentQuality": c["equipmentQuality"],
        }
        if c["revealCondition"] == "explore_at_least_100":
            assert c["reveal"] == 100, c["id"]
            entry["revealAt"] = 100
        else:
            assert c["revealCondition"] == GUARD_CONDITION and c["reveal"] is None, c["id"]
            assert c["nodeId"] == d03["guardNode"], "警官箱在 d03 守卫房"
            entry["revealInstanceFact"] = "guardianDefeated"
        added.append(entry)
    cfg["resources"] = kept + added
    assert len(cfg["resources"]) == 169

    mig = adj["legacyMigration"]
    assert mig["sameClaim"] == "officer_cache" and mig["oldOfficerNode"] == "d03_04"
    uq = {c["id"]: c for c in caches}
    for lc in cfg["map"]["legacyClaims"]:
        u = uq[lc["claimId"]]
        assert lc["grants"] == u["items"] and lc["name"] == u["displayName"], lc["claimId"]
        lc["node"] = u["nodeId"]
    assert next(lc for lc in cfg["map"]["legacyClaims"] if lc["claimId"] == "officer_cache")["node"] == mig["newOfficerNode"]

    # ───── Q125：8 份核心记录正式名 ─────
    rec_names = adj["recordNames"]
    assert len(rec_names) == 8
    records = {r["recordId"]: r for r in cfg["resources"] if r.get("recordId")}
    assert sorted(records) == sorted(rec_names)
    disp = {n["resourceId"]: n["name"] for n in adj["resourceDisplayNames"]}
    for rid, name in rec_names.items():
        r = records[rid]
        if r["nodeId"] in disp:
            assert disp[r["nodeId"]] == name, r["nodeId"]
        r["name"] = name
    church = rec_names["church_route_record"]
    academy = rec_names["academy_safety_record"]
    for u in cfg["scope"]["capacity"]["unlocks"]:
        if u["anyOf"] == ["church_route_record", "academy_safety_record"]:
            u["text"] = f"{church}或{academy}之一"
        elif u["allOf"] == ["church_route_record", "academy_safety_record"]:
            u["text"] = f"{church}与{academy}两种"
    labels = cfg["map"]["factLabels"]
    labels["FA02_record_credit"] = f"在教会地下圣所核心领取{church}"
    labels["FA03_record_credit"] = f"在灰塔封存区核心领取{academy}"
    labels["communication_power_claimed"] = f"在警备仓封存仓核心领取{rec_names['communication_power']}"

    # ───── Q123：四核心同授 28 张蓝图 ─────
    packs = adj["coreBlueprintPacks"]
    assert sorted(len(p["knowledgeIds"]) for p in packs) == [6, 7, 7, 8]
    know = {k["id"]: k for k in cfg["knowledge"]}
    by_res = {r["nodeId"]: r for r in cfg["resources"]}
    dungeons = {d["id"]: d for d in cfg["map"]["dungeons"]}
    total = 0
    for p in packs:
        assert p["grantWithCoreClaim"] and p["extraTime"] == 0 and p["extraXp"] == 0 and not p["physicalBlueprint"], p
        r = by_res[p["resourceId"]]
        assert r["locationId"] == p["nodeId"] and r.get("recordId"), p["resourceId"]
        loc = locs[p["nodeId"]]
        dname = dungeons[loc["dungeonId"]]["name"]
        facts = ["claim:" + p["resourceId"], "campaignId:" + p["resourceId"]]
        facts += [f for f in r.get("claimFacts", []) if f == "communication_power_claimed"]
        for kid in p["knowledgeIds"]:
            k = know[kid]
            assert k.get("blueprint"), kid
            assert k["id"] != "blueprint_armory_station"
            k["grantedByFacts"] = list(facts)
            k["source"] = f"蓝图：{dname}·{loc['name']}（{p['nodeId']}）领取核心记录“{r['name']}”时一并获得（Q123）"
            total += 1
    assert total == 28
    armory = know["blueprint_armory_station"]
    assert armory["grantedByFacts"] == ["claim:d03_08_unique", "communication_power_claimed"]
    armory["source"] = f"蓝图：旧城警备仓·封存仓（d03_08）领取核心记录“{rec_names['communication_power']}”时一并获得（Q109）"

    # ───── Q124：13 个世界分区正式名 ─────
    regions = adj["regionNames"]
    assert len(regions) == 13
    world_zones = {l["zone"] for l in cfg["locations"] if l["nodeKind"] != "dungeon"}
    assert world_zones == set(regions), world_zones ^ set(regions)
    for d in cfg["map"]["dungeons"]:
        assert locs[d["parent"]]["zone"] in regions, d["id"]
    cfg["map"]["regionNames"] = dict(regions)

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
