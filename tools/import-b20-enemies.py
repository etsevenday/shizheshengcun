# -*- coding: utf-8 -*-
"""B20 新敌人、固定战与人口区的配置导入（开发计划 v0.9 批次 B20，需求 R62/R63/R64/R100 与并入的 Q31）→ data/config.v0.6.json。

数值与名单逐条取自策划案及其 JSON，不自补：
- R62 12 个新敌人：A09「E3 新增12个敌人面板与行为」、expansion-combat.json newEnemies（面板逐项读 JSON 并断言）。
  意图循环按 E3 表的文字编码（下方 NEW_INTENTS）；“普通抓击”没写咬击的按 E2“只有明确咬击意图才判感染、新精英默认无咬击”记咬击 0，
  缠线爬行者/货架护卫感染者明写“抓击80%/咬击20%”。受惊参数按 A09 §4：默认 35%/6，守卫（4AP，按精英）60%/14（开发假设 B20-2）。
  新敌人不另加附加掉落（E5“四新普通怪不另加稀有材料概率”；守卫回收材料与首通包属奖励分账 B21）。
- R63 固定战：expansion-combat.json enemySets（31 组，按 JSON 为准；R05 写 32 组是笔误口径，Q81 答复）；
  另加 m02 检查哨固定拦路战：A08「m02_checkpoint 检查哨 … 1迟缓尸固定任务战」+ gate-contract factProducers.checkpoint_obstruction_cleared。
- R64 人口区：node-expansion-rules.json worldPopulation.regions（9 区：mainline 4 区 + ex_pop 5 区，五区数值与 expansion-combat.json
  populationRegions 一致，脚本断言）；45 个野外节点的 worldPopulation.weights / firstPopulation（与 newWildPools、A12 mainlineEnemyPools 一致）。
- R100 五处旧精英可选挑战：legacy-elite-placements.json encounters（与 expansion-combat.json optionalLegacyEliteEncounters 一致）。

可重复运行，结果相同（覆盖 B20 新敌人、worldEnemies.regions/fixedBattles/legacyElites 与地点 populationWeights/firstPopulation）。
用法：python tools/import-b20-enemies.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
DOC = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0")
COMBAT = os.path.join(DOC, "assets", "20261030", "combat", "expansion-combat.json")
NODES = os.path.join(DOC, "assets", "20261030", "narrative-map", "node-expansion-rules.json")
ELITES = os.path.join(DOC, "assets", "20261031", "combat", "legacy-elite-placements.json")


def step(kind, **kw):
    d = {"kind": kind}
    d.update(kw)
    return d


GRAB = lambda verb="抓击", bite=0, **kw: step("attack", verb=verb, bitePct=bite, **kw)
MELEE12 = {"ownSlots": "12", "targetSlots": "12"}

# A09 E3 正常激活意图（按表文字；“→”为循环顺序；奇数/偶数激活即循环第 1、2 项）
NEW_INTENTS = {
    # 激活1 鼓动至多两名同伴下一击×1.10 不叠层；激活2 普通抓击，在后排先前移1位（抓击己位 12）
    "ec_cantor": {"cycle": [step("rally", verb="鼓动", nextHitFactor=1.10, limit=0), GRAB("抓击", **MELEE12)]},
    # 普通抓击；命中后目标下一次攻击失衡；无合法目标先前移；不施加毒素/病毒值
    "ec_labwalker": {"cycle": [GRAB("抓击", control="stagger")]},
    # 奇数：低伤×0.6拉扯，控制成功后目标前移1位；偶数：抓击80%/咬击20%
    "ec_cablecrawler": {"cycle": [step("pull", verb="缠线拉扯", factor=0.6, pull=1), GRAB("抓击", bite=20)]},
    # 奇数保护相邻同伴1次（不形成循环保护）；偶数普通抓击80%/咬击20%
    "ec_stockbracer": {"cycle": [step("protect", verb="护住"), GRAB("抓击", bite=20)]},
    # 格挡1次→重击×1.3→普通抓击
    "ec_boss_crypt": {"cycle": [step("guard", verb="格挡"), step("charge", verb="重击", factor=1.3), GRAB("抓击")]},
    # 标记我方1人（任意存活目标）→对该目标普通攻击→格挡
    "ec_boss_lab": {"cycle": [step("mark", verb="标记", markRule="random"), GRAB("抓击", targetMarked=True), step("guard", verb="格挡")]},
    # 蓄力→重击×1.4→保护相邻同伴1次
    "ec_boss_armory": {"cycle": [step("charge_prepare", verb="蓄力"), step("charge", verb="重击", factor=1.4), step("protect", verb="护住")]},
    # 公开扫击我方12各×0.6→单体重击×1.3→防御
    "ec_boss_relay": {"cycle": [step("sweep", verb="扫击", factor=0.6, ownSlots="12", targetSlots="12"),
                                step("charge", verb="重击", factor=1.3), step("guard", verb="防御")]},
    # 推挤伤害×0.7并击退1位→普通抓击→保护同伴
    "ec_boss_water": {"cycle": [step("charge", verb="推挤", factor=0.7, knockback=1), GRAB("抓击"), step("protect", verb="护住")]},
    # 蓄力→重击×1.4并破甲0.2两轮→普通抓击
    "ec_boss_mine": {"cycle": [step("charge_prepare", verb="蓄力"),
                               step("charge", verb="重击", factor=1.4, armorBreak=0.2, armorBreakRounds=2), GRAB("抓击")]},
    # 保护相邻后排1次→普通抓击→公开12扫击各×0.55
    "ec_boss_mall": {"cycle": [step("protect", verb="护住"), GRAB("抓击"),
                               step("sweep", verb="扫击", factor=0.55, ownSlots="12", targetSlots="12")]},
    # 标记我方后排1人（最靠后存活者，意图锁定后公开）→拉扯低伤×0.7（对被标记者）→对12普通抓击；无温度伤害
    "ec_boss_cold": {"cycle": [step("mark", verb="标记", markRule="rearmost"),
                               step("pull", verb="拉扯", factor=0.7, pull=1, targetSlots="1234", targetMarked=True), GRAB("抓击")]},
}

FEAR_DEFAULT, FEAR_ELITE = (0.35, 6), (0.60, 14)


def enemy(src):
    eid = src["id"]
    elite = src["ap"] == 4
    assert src["ap"] in (3, 4), eid
    slots = "".join(str(x) for x in src["ownSlots"])
    tgt = "".join(str(x) for x in src["targetSlots"])
    fear = FEAR_ELITE if elite else FEAR_DEFAULT
    return {
        "id": eid, "name": src["name"], "family": "zombie", "hp": src["hp"], "armor": src["armor"], "dodge": src["evasion"],
        "speed": src["speed"], "damageMin": src["damage"][0], "damageMax": src["damage"][1], "behavior": "intents", "fleeAtHp": None,
        "immuneBleed": True, "artRequired": False, "attackRating": src["attack"], "rarity": "elite" if elite else "common",
        "biteIntentWeightPct": 0, "worldPersistentId": True, "fearChance": fear[0], "fearValue": fear[1],
        "controlResist": src["controlResist"], "ownSlots": slots, "targetSlots": tgt, "ap": src["ap"], "elite": elite,
        "intents": NEW_INTENTS[eid],
    }


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    combat = json.load(open(COMBAT, encoding="utf-8"))
    nodes = json.load(open(NODES, encoding="utf-8"))
    elites = json.load(open(ELITES, encoding="utf-8"))

    # ───── R62 12 个新敌人 ─────
    new = combat["newEnemies"]
    assert len(new) == 12 and set(NEW_INTENTS) == {e["id"] for e in new}
    assert sum(1 for e in new if e["ap"] == 3) == 4 and sum(1 for e in new if e["ap"] == 4) == 8
    ids = {e["id"] for e in new}
    cfg["enemies"] = [e for e in cfg["enemies"] if e["id"] not in ids] + [enemy(e) for e in new]
    enemy_ids = {e["id"] for e in cfg["enemies"]}
    assert len(enemy_ids) == 42, len(enemy_ids)

    we = cfg["worldEnemies"]
    locs = {l["id"]: l for l in cfg["locations"]}

    # ───── R64 / Q31 人口区与节点池 ─────
    regions = nodes["worldPopulation"]["regions"]
    assert len(regions) == 9
    ex = {r["id"]: r for r in combat["populationRegions"]}
    out = []
    for r in regions:
        if r["id"] in ex:
            src = ex[r["id"]]
            for k in ("cap", "nodes", "spawnEveryMinutes", "spawnAttempt", "patrolEveryMinutes", "patrolAttempt"):
                assert r[k] == src[k], (r["id"], k)
        assert r["spawnEveryMinutes"] == we["spawnCheckIntervalMinutes"] and r["patrolEveryMinutes"] == we["patrolCheckIntervalMinutes"]
        reg = {
            "id": r["id"], "cap": r["cap"], "nodes": r["nodes"], "spawnEveryMinutes": r["spawnEveryMinutes"],
            "spawnAttempt": r["spawnAttempt"], "patrolEveryMinutes": r["patrolEveryMinutes"], "patrolAttempt": r["patrolAttempt"],
            "spawnStartWorldMinute": r.get("spawnStartWorldMinute", we["spawnChecksBeginAbsoluteMinute"]),
        }
        if "layerWeights" in r:
            reg["layerWeights"] = r["layerWeights"]
            reg["layerOpenWorldDay"] = r["layerOpenWorldDay"]
        out.append(reg)
    we["regions"] = out
    wild = {p["nodeId"]: p for p in combat["newWildPools"]}
    region_of = {n: r["id"] for r in regions for n in r["nodes"]}
    pooled = 0
    for n in nodes["nodes"]:
        wp = n["worldPopulation"]
        loc = locs[n["nodeId"]]
        loc.pop("populationWeights", None)
        loc.pop("firstPopulation", None)
        if wp.get("mode") != "existing_world_population":
            continue
        assert region_of[n["nodeId"]] == wp["regionId"] == loc["populationRegionId"], n["nodeId"]
        assert wp["localCap"] == loc["populationLocalCap"] == 4
        if n["nodeId"] in wild:
            assert wild[n["nodeId"]]["weights"] == wp["weights"] and wild[n["nodeId"]]["firstPopulation"] == wp["firstPopulation"]
        for e in wp["weights"]:
            assert e in enemy_ids, e
        loc["populationWeights"] = wp["weights"]
        loc["firstPopulation"] = wp["firstPopulation"]
        pooled += 1
    assert pooled == 45, pooled

    # ───── R63 固定战（31 组 + m02 检查哨拦路战） ─────
    dungeons = {d["id"]: d for d in combat["dungeonInterfaces"]}
    guard_of = {d["guardNode"]: d["id"] for d in combat["dungeonInterfaces"]}
    fixed = []
    for es in combat["enemySets"]:
        node = locs[es["nodeId"]]
        assert node["nodeKind"] == "dungeon" and es["reinforcement"] is False
        for e in es["enemyOrder"]:
            assert e in enemy_ids, e
        dg = node["dungeonId"]
        assert es["nodeId"] not in dungeons[dg]["safeNodes"]
        assert es["requiredGate"] == (es["nodeId"] in guard_of)
        fixed.append({
            "nodeId": es["nodeId"], "dungeonId": dg, "enemyOrder": es["enemyOrder"], "guardian": es["requiredGate"],
            "scope": "instance", "fact": "guardianDefeated" if es["requiredGate"] else None,
        })
    assert len(fixed) == 31 and sum(1 for f in fixed if f["guardian"]) == 8
    fixed.append({"nodeId": "m02_checkpoint", "dungeonId": None, "enemyOrder": ["z_slow"], "guardian": False,
                  "scope": "campaign", "fact": "checkpoint_obstruction_cleared"})
    we["fixedBattles"] = fixed

    # ───── R100 五处旧精英可选挑战 ─────
    enc = elites["encounters"]
    assert len(enc) == 5
    opt = {o["id"]: o for o in combat["optionalLegacyEliteEncounters"]}
    le = []
    for o in enc:
        assert o == opt[o["id"]] or (o["enemyId"] == opt[o["id"]]["enemyId"] and o["nodeId"] == opt[o["id"]]["nodeId"]), o["id"]
        assert o["enemyOrder"] == [o["enemyId"]] and o["scope"] == "campaign_once" and o["reinforcements"] is False
        assert locs[o["nodeId"]]["nodeKind"] == "world" and o["enemyId"] in enemy_ids
        items = o["reward"]["items"]
        assert len(items) == 1
        item, count = next(iter(items.items()))
        le.append({
            "id": o["id"], "enemyId": o["enemyId"], "nodeId": o["nodeId"], "revealAtExplorePercent": o["revealAtExplorePercent"],
            "requiresFact": "hub_registered" if o["requiresHubRegistered"] else None,
            "rewardItemId": item, "rewardCount": count, "rewardChance": o["reward"]["chance"], "claimKey": o["reward"]["claimKey"],
        })
    we["legacyElites"] = le

    # B20 起检查哨拦路战与地牢守卫战已上线：门槛未满足原因去掉“（后续版本开放）”（B11 措辞，开发方拟）
    fl = cfg["map"]["factLabels"]
    fl["checkpoint_obstruction_cleared"] = "解决检查哨的固定拦路战（在检查哨接战取胜）"
    fl["guardianDefeated"] = "击败本次地牢的守卫（在守卫房接战取胜）"

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"enemies {len(cfg['enemies'])}, regions {len(out)}, pooled nodes {pooled}, fixedBattles {len(fixed)}, legacyElites {len(le)}")


if __name__ == "__main__":
    main()
