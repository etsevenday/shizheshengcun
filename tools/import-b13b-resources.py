# -*- coding: utf-8 -*-
"""B13b 答复 Q120/Q121/Q125（资源名部分）的配置导入（开发计划 v0.9 批次 B13b，R129）。

数值与名称逐条取自策划数据 resource-adjudications.json（A08 附表 resource-tables.md 的机器版），不自补：
- resources.additionalSources（53 条）：本批导入 47 条——44 条主世界来源（worldSources）、工坊首次支援包（taskSupply）、
  消防车器材箱（firetruckCache）、采石坡野外装备图册（quarryKnowledge）。6 个旧 d03 唯一箱（uniqueEquipmentCaches）的领取规则
  属 Q122（批次 B13c），本批不建资源点，只把正式名登记进 map.legacyClaims（开发假设 B13b-2）。
  world_stock：kind renewable → renewable_units，finite → finite_units（不补），flowing（cap -1）→ unlimited；
  reveal 0 = 实际到访（revealOnVisit），否则 revealAt；proficiencyId / xpAllowed 照表。
  campaignClaimId：campaign_claim（没有 recordId）；实物包 outputs 整包落地，知识包靠 knowledge.grantedByFacts "claim:<id>"。
- tools.new96 / tools.restored（Q121）：逐行合格工具 itemIds、工具后基础分钟、产量倍率 1、成功磨损 1、toolUsePassive；
  tools.legacy20 只核对旧 20 条原规则不变（3 条有工具：field_fiber/woods_fiber 石刀 10 分钟、wood 简易斧产量×2）。
- resourceDisplayNames（149，Q125）：本批改 135 条资源点显示名（96 条中除 8 份核心记录 + 本批导入的 47 条）；
  8 份核心记录正式名（recordNames）属 B13c，不改；6 个旧唯一箱名登记到 legacyClaims。
- services（h04_public_boil 公共锅煮水）：要求营区登记，随营区城市批次（B14）接入，本批不导入（开发假设 B13b-5）。

可重复运行，结果相同。
用法：python tools/import-b13b-resources.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261102", "map", "resource-adjudications.json")

POOL = {"renewable": "renewable_units", "finite": "finite_units", "flowing": "unlimited"}
QUARRY_SOURCE = "蓝图：采石坡（w03_quarry）探索100%后领取“采石坡野外装备图册”（10分钟，本局一次，Q120）"


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def tool_entry(t):
    if not t["itemIds"]:
        assert t["baseMinutesOverride"] is None and t["successWear"] == 0 and not t["toolUsePassive"], t
        return None
    assert t["outputMultiplier"] == 1 and t["successWear"] == 1, t
    assert t["baseMinutesOverride"] == max(5, t["originalBaseMinutes"] - 5), t
    return {
        "toolTag": "",
        "itemIds": list(t["itemIds"]),
        "outputMultiplier": 1,
        "baseMinutesOverride": t["baseMinutesOverride"],
        "toolUsePassive": bool(t["toolUsePassive"]),
    }


def main():
    cfg = load(CONFIG)
    adj = load(ADJ)
    res = adj["resources"]
    tools = adj["tools"]
    assert res["totalResourceEntries"] == 169 and len(res["additionalSources"]) == 53
    assert len(res["worldSources"]) == 44 and len(res["uniqueEquipmentCaches"]) == 6
    assert len(tools["new96"]) == 96 and len(tools["restored"]) == 53 and len(tools["legacy20"]) == 20
    assert tools["extraTimeReductionMinutes"] == 5 and tools["cancelWear"] == 0

    item_ids = {i["id"] for i in cfg["items"]}
    loc_ids = {l["id"] for l in cfg["locations"]}
    unique_ids = {u["id"] for u in res["uniqueEquipmentCaches"]}

    # 原 116 条（20 旧 + 96 新）不删；去掉本脚本上次导入的条目后重建（幂等）
    add_ids = {s["id"] for s in res["additionalSources"]}
    kept = [r for r in cfg["resources"] if r["nodeId"] not in add_ids]
    assert len(kept) == 116, len(kept)
    by_id = {r["nodeId"]: r for r in kept}

    # 旧 20 条工具规则保留核对
    for t in tools["legacy20"]:
        r = by_id[t["resourceId"]]
        tb = r.get("toolBonus")
        if not t["itemIds"]:
            assert tb is None, r["nodeId"]
        else:
            assert tb["toolTag"] == t["itemIds"][0] and tb["outputMultiplier"] == t["outputMultiplier"], r["nodeId"]
            assert tb.get("baseMinutesOverride") == t["baseMinutesOverride"], r["nodeId"]

    # 96 条加工具表
    for t in tools["new96"]:
        r = by_id[t["resourceId"]]
        assert r["locationId"] == t["nodeId"] and r["baseMinutes"] == t["originalBaseMinutes"], t["resourceId"]
        r["toolBonus"] = tool_entry(t)
        if r["toolBonus"] and r["toolBonus"]["toolUsePassive"]:
            assert r["proficiencyId"] == "scavenging" and r["xpAllowed"], t["resourceId"]
    assert sum(1 for t in tools["new96"] if t["itemIds"]) == 74

    # 47 条新来源
    restored_tools = {t["resourceId"]: t for t in tools["restored"]}
    added = []
    for s in res["additionalSources"]:
        if s["id"] in unique_ids:
            continue
        assert s["nodeId"] in loc_ids, s["id"]
        assert s["id"] not in by_id, s["id"]
        for k in s["items"]:
            assert k in item_ids, (s["id"], k)
        t = restored_tools[s["id"]]
        assert t["nodeId"] == s["nodeId"] and t["originalBaseMinutes"] == s["workMinutes"], s["id"]
        entry = {
            "nodeId": s["id"],
            "name": s["displayName"],
            "locationId": s["nodeId"],
            "revealAt": s["reveal"] if s["reveal"] else None,
            "revealOnVisit": not s["reveal"],
            "baseMinutes": s["workMinutes"],
            "cap": None,
            "dailyRefill": s["dailyRefill"],
            "poolMode": None,
            "outputs": dict(s["items"]),
            "toolBonus": tool_entry(t),
            "hazard": None,
            "proficiencyId": s["proficiencyId"],
            "scope": s["scope"],
            "kind": s["kind"],
            "xpAllowed": bool(s["xpAllowed"]),
            "legacyAliases": list(s.get("legacyAliases") or []),
        }
        assert s["hazard"] is None
        if s["scope"] == "world_stock":
            assert s["unit"] == "item_count" and len(s["items"]) == 1, s["id"]
            pool = POOL[s["kind"]]
            if pool == "renewable_units":
                assert s["dailyRefill"] > 0, s["id"]
            if pool == "finite_units":
                assert s["dailyRefill"] == 0, s["id"]
            if pool == "unlimited":
                assert s["cap"] == -1, s["id"]
            entry["poolMode"] = pool
            entry["cap"] = None if pool == "unlimited" else s["cap"]
            assert s["initial"] == s["cap"], s["id"]
        else:
            assert s["scope"] == "campaignClaimId" and s["claimId"] == s["id"] and s["cap"] == 1 and s["initial"] == 1, s["id"]
            assert not s["xpAllowed"] and s["proficiencyId"] is None, s["id"]
            entry["poolMode"] = "campaign_claim"
            entry["cap"] = 1
            if s.get("unlockAnyFacts"):
                entry["unlockAnyFacts"] = list(s["unlockAnyFacts"])
            if s.get("equipmentQuality"):
                entry["equipmentQuality"] = s["equipmentQuality"]
            if s["kind"] == "campaign_knowledge":
                assert s["items"] == {}
                for kid in s["knowledgeIds"]:
                    k = next(k for k in cfg["knowledge"] if k["id"] == kid)
                    fact = "claim:" + s["id"]
                    k["grantedByFacts"] = [fact]
                    k["source"] = QUARRY_SOURCE
        added.append(entry)
    assert len(added) == 47, len(added)
    assert entry is not None

    # Q125：资源点正式名（8 份核心记录名属 B13c，不改）
    names = {n["resourceId"]: n for n in adj["resourceDisplayNames"]}
    assert len(names) == 149
    all_rows = kept + added
    renamed = 0
    for r in all_rows:
        n = names.get(r["nodeId"])
        if n is None:
            continue
        assert n["nodeId"] == r["locationId"], r["nodeId"]
        if r["poolMode"] == "campaign_claim" and r.get("recordId"):
            continue
        r["name"] = n["name"]
        renamed += 1
    assert renamed == 135, renamed

    # 6 个旧唯一箱：只登记正式名（领取规则 Q122 在 B13c）
    uq = {u["id"]: u for u in res["uniqueEquipmentCaches"]}
    for c in cfg["map"]["legacyClaims"]:
        u = uq[c["claimId"]]
        assert c["grants"] == u["items"], c["claimId"]
        c["name"] = u["displayName"]

    cfg["resources"] = kept + added
    assert len(cfg["resources"]) == 163

    # Q121：搜集 5 级“工具运用”随工具表接入启用（tools.toolUsePassive scavenging_05 ×0.90）
    tp = tools["toolUsePassive"]
    node = next(n for n in cfg["skillNodes"] if n["id"] == tp["id"])
    assert node["skillId"] == tp["skill"] and node["level"] == tp["level"]
    assert node["effects"] == [{"key": "work.scavengeTool", "op": "mul", "value": tp["workMultiplier"]}]
    node["status"] = "live"
    node["displayText"] = "用资源点逐行列明的合格工具（石刀/简易斧/撬棍）搜集时，搜集工作量再×0.9；徒手、采集、领取与配给不吃"

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
