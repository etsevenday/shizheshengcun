# -*- coding: utf-8 -*-
"""B22 地牢奖励分账、72 小时重置与战术品的配置导入（开发计划 v0.9 批次 B22，需求 R67/R69/R70）→ data/config.v0.6.json 的 dungeonRewards 块。

数值逐条取自策划案与同源数据，不自补：
- bossSalvage：A09「E6.2 每代守卫战的固定回收材料」= expansion-combat.json bossSalvage（8 条，instance_once，键 instanceId:<守卫节点>:combat_salvage）。
- firstClear：A09「E6.3 本局首次通关补给包」= expansion-combat.json firstClearRewards（8 条，campaign_once，键 campaignId:<地牢>:combat_first）。
- resetCooldownMinutes：A09「E8」、R03 D8、expansion-combat.json lifecycle.resetCooldownMinutes = 4320（72 世界小时，从本代 clearedAt 起算）。
- tactical：A09「E7」、R03「战术物合法性与效果引用」、A07 X1.13 = expansion-combat.json tacticalItems：
  flash_marker 2AP/E0.1、敌方任一存活单体（1234 位）、标记 2 大回合全队对其攻击能力+6（与猎标同名取强）、噪声+5；
  brace_pack 1AP/E0、自身位移抵抗+25 个百分点 2 大回合（与稳固同类取强）；
  rally_patch 2AP/E0、任一存活友方当前恐慌−6（最低 0），每目标每大回合来自此道具合计最多 6；三者共享“施用者每大回合最多 1 次”。
  效果文字是中文字符串，这里按字面断言数值后写成结构化字段（effect 原文一并存 text）。

同时断言（B22 开发假设的前提，R67 验收 EC07/EC09/COMP14）：
- mapRewardRefs 32 条全部复用资源表同一 resourceId（24 条 scope=instanceId 每代普通箱、8 条 campaignClaimId 核心记录），节点与产出一致——不另建第二份领取；
- legacyUniqueRewards 6 箱 = 资源表同名 claim（位置 d03_03/d03_07/d03_08、产出、20 基础分钟、campaign_physical），野外没有同 claim 的第二份；
- 守卫回收节点 = 地牢 guardNode，首通提交点 = recordNode；物品 ID 都在物品表；
- repeatableTotals 每地牢重量 = 3 个实例侧箱 + 守卫回收（合计 11.95kg，其中侧箱 10.10kg），没有隐藏的 parts2 通关包；
- 三个战术品的物品与配方（ex_flash_marker 等）已在 A07 供给导入里。

可重复运行，结果相同（覆盖 dungeonRewards 块，不动 dungeonExpedition）。
用法：python tools/import-b22-rewards.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
DOC = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0")
COMBAT = os.path.join(DOC, "assets", "20261030", "combat", "expansion-combat.json")


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(COMBAT, encoding="utf-8") as f:
        combat = json.load(f)

    items = {i["id"]: i for i in cfg["items"]}
    res = {r["nodeId"]: r for r in cfg["resources"]}
    locs = {l["id"]: l for l in cfg["locations"]}
    dungeons = {d["id"]: d for d in cfg["map"]["dungeons"]}
    recipes = {r["id"] for r in cfg["recipes"]}

    # ── E6.1 地图箱：32 条引用，复用资源表 ──
    refs = combat["mapRewardRefs"]
    assert len(refs) == 32, len(refs)
    inst = [r for r in refs if r["scope"] == "instanceId"]
    camp = [r for r in refs if r["scope"] == "campaignClaimId"]
    assert len(inst) == 24 and len(camp) == 8, (len(inst), len(camp))
    for r in refs:
        c = res[r["sourceResourceId"]]
        assert c["locationId"] == r["nodeId"], r
        assert c["outputs"] == {r["itemId"]: r["quantity"]}, r
        assert c["scope"] == r["scope"], r
        if r["scope"] == "instanceId":
            assert c["cap"] == 2 and c["dailyRefill"] == 0 and c["baseMinutes"] == 20, r
            assert r["key"] == "instanceId:" + r["sourceResourceId"], r
        else:
            assert c["kind"] == "campaign" and r["key"] == "campaignId:" + r["sourceResourceId"], r
    # 地牢房间里实例普通箱恰好就是这 24 条（没有隐藏第 33 箱）
    dungeon_rooms = {l["id"] for l in cfg["locations"] if l.get("dungeonId")}
    inst_in_dungeon = sorted(r["nodeId"] for r in cfg["resources"] if r["locationId"] in dungeon_rooms and r.get("scope") == "instanceId")
    assert inst_in_dungeon == sorted(r["sourceResourceId"] for r in inst), inst_in_dungeon

    # ── E6.4 旧唯一箱：6 箱只迁位置 ──
    legacy = combat["legacyUniqueRewards"]
    assert len(legacy) == 6
    for u in legacy:
        c = res[u["campaignClaimId"]]
        assert c["locationId"] == u["nodeId"] and c["outputs"] == u["items"], u
        assert c["kind"] == "campaign_physical" and c["baseMinutes"] == u["workMinutes"] == 20, u
        assert locs[c["locationId"]].get("dungeonId") == "d03", u
        same = [r["nodeId"] for r in cfg["resources"] if r["nodeId"] == u["campaignClaimId"]]
        assert len(same) == 1, same

    # ── E6.2 守卫回收、E6.3 首通包 ──
    salvage = []
    for b in combat["bossSalvage"]:
        d = b["nodeId"][:3]
        assert dungeons[d]["guardNode"] == b["nodeId"], b
        assert b["scope"] == "instance_once" and b["key"] == "instanceId:%s:combat_salvage" % b["nodeId"], b
        for k in b["items"]:
            assert k in items, k
        salvage.append({"dungeonId": d, "nodeId": b["nodeId"], "items": b["items"]})
    assert len(salvage) == 8
    first = []
    for fc in combat["firstClearRewards"]:
        d = fc["dungeonId"]
        assert dungeons[d]["recordNode"] == fc["clearNode"], fc
        assert fc["scope"] == "campaign_once" and fc["key"] == "campaignId:%s:combat_first" % d, fc
        for k in fc["items"]:
            assert k in items, k
        first.append({"dungeonId": d, "clearNode": fc["clearNode"], "items": fc["items"]})
    assert len(first) == 8

    # ── 每代固定重复产出合计（COMP14：11.95kg 已含侧箱 10.10kg） ──
    total = side_total = 0
    for t in combat["repeatableTotals"]:
        d = t["dungeonId"]
        side = sum(items[k]["weightGrams"] * v for r in inst if r["nodeId"].startswith(d) for k, v in {r["itemId"]: r["quantity"]}.items())
        guard = sum(items[k]["weightGrams"] * v for b in salvage if b["dungeonId"] == d for k, v in b["items"].items())
        assert side + guard == t["weightGrams"], (d, side, guard, t["weightGrams"])
        total += side + guard
        side_total += side
    assert (total, side_total) == (11950, 10100), (total, side_total)

    # ── 72 小时重置 ──
    life = combat["lifecycle"]
    assert life["resetCooldownMinutes"] == 4320 and life["startsAt"] == "clearedAt", life

    # ── E7 战术品 ──
    tac = {t["id"]: t for t in combat["tacticalItems"]}
    fm, bp, rp = tac["flash_marker"], tac["brace_pack"], tac["rally_patch"]
    assert (fm["ap"], fm["energy"], fm["noise"]) == (2, 0.1, 5), fm
    assert "2完整大回合" in fm["effect"] and "+6" in fm["effect"] and "猎标" in fm["effect"] and "1234" in fm["target"], fm
    assert (bp["ap"], bp["energy"], bp["noise"]) == (1, 0, 0), bp
    assert "+25" in bp["effect"] and "2大回合" in bp["effect"] and "稳固" in bp["effect"] and bp["target"] == "自身", bp
    assert (rp["ap"], rp["energy"], rp["noise"]) == (2, 0, 0), rp
    assert "-6" in rp["effect"] and "最多6" in rp["effect"] and "友方" in rp["target"], rp
    for t in (fm, bp, rp):
        assert t["id"] in items and t["recipeRef"] in recipes and "每大回合" in t["cooldown"], t
    tactical = [
        {"id": "flash_marker", "action": "tactic_flash_marker", "target": "enemy", "ap": 2, "energy": 0.1, "noise": 5,
         "markBonus": 6, "rounds": 2, "text": fm["effect"]},
        {"id": "brace_pack", "action": "tactic_brace_pack", "target": "self", "ap": 1, "energy": 0, "noise": 0,
         "displaceResist": 25, "rounds": 2, "text": bp["effect"]},
        {"id": "rally_patch", "action": "tactic_rally_patch", "target": "ally", "ap": 2, "energy": 0, "noise": 0,
         "panicReduce": 6, "perTargetRoundCap": 6, "text": rp["effect"]},
    ]

    cfg["dungeonRewards"] = {
        "source": "A09「E6.1—E6.4」「E7」「E8」「每地牢每代固定重复产出合计」、R03 D7/D8、A07 X1.13、expansion-combat.json（策划已批准可调初值）",
        "resetCooldownMinutes": life["resetCooldownMinutes"],
        "bossSalvage": salvage,
        "firstClear": first,
        "tactical": tactical,
    }

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"dungeonRewards: {len(salvage)} boss salvage, {len(first)} first clear, {len(tactical)} tactical, reset {life['resetCooldownMinutes']} min")


if __name__ == "__main__":
    main()
