# -*- coding: utf-8 -*-
"""B7b 策划答复 Q53—Q61 成长与行动合同导入（开发计划 v0.9 批次 B7b）。

来源：doc/策划案/系统策划总册 v1.0/assets/20261031/progression/progression-adjudications.json
（= A06/S01/S05/S08/S11 本轮章「2026-10-31 Q53—Q61成长与行动裁定（当前优先）」）。写入 data/config.v0.6.json：
- resources[].proficiencyId：现有 20 个资源点按合同 resourceRows 的熟练归属（Q55；废金属堆改练搜集）。
  合同另 96 行属 v3 地图（B11），配置里没有这些资源点，只登记不导；
- progression.dismantle：制造 1/3/7/10 级开放 T0/T1/T2/T3 拆解、每件 +2 制造经验、回收专家制造 10 级（Q54）；
- progression.fieldXp：采集/搜集完成经验 clamp(2*ceil(原基础分钟/5),2,12)、同点每日前 3 次、每门每日 60（Q55）；
- progression.survey：四旧遗迹“遗迹勘整”30 基础分钟、每点每日 2 次、搜集 +12、风险按点（Q55）；
- progression.cooking：四种食品 clamp(ceil(受热分钟/10),1,12)、日 30，净化每 500ml 1、日 4（Q60）；
- progression.workCarry：余额上限 4.9999、截到 0.0001（Q61）；
- skillNodes crafting_01 / scavenging_03 / scavenging_05 / scavenging_10 的状态、效果键与显示文字（Q53/Q54/Q59）。
可重复运行，结果相同；其他配置不动。须在 import-b5-skills.py、import-b7a-equipment.py 之后运行。
用法：python tools/import-b7b-progression.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
CONTRACT = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261031", "progression",
                        "progression-adjudications.json")

# 求生节点的程序口径（数值取自 A06 §2 本轮改写的效果列，不自补）
NODES = {
    # Q53：W0 是 T0 误记；既有手工配方原门槛不变，T0 装备 1 级可制（现有配方本来就开放）
    "crafting_01": ("baseline", [], "保留既有手工配方原门槛；新增0阶装备制造1级许可（不新增穿戴限制）"),
    # Q54：搜集 3/10 改为普通搜集工作量被动（不作用于遗迹勘整），回收专家迁到制造 10 级（见 progression.dismantle）
    "scavenging_03": ("live", [{"key": "work.scavenge", "op": "mul", "value": 0.95}],
                      "普通遗迹与工业资源搜集基础工作量×0.95；不改变固定箱数量，不作用于遗迹勘整"),
    "scavenging_10": ("live", [{"key": "work.scavenge", "op": "mul", "value": 0.90}],
                      "普通遗迹与工业资源搜集基础工作量×0.90（替代3级）；不改变固定箱数量，不作用于遗迹勘整"),
    # Q59：合格工具等 B11 逐点配置后生效；之前显示暂未开放，不加成不磨损
    "scavenging_05": ("closed", [{"key": "work.scavengeTool", "op": "mul", "value": 0.9}],
                      "选择合格工具时搜集工作量×0.9（暂未开放：新地图逐个搜集点标明合格工具后生效）"),
}

# A06 §2 本轮改写后的节点名与效果原文（effectText 保留原文供对照）
A06_ROWS = {
    "crafting_01": ("基础手作", "保留既有手工配方原门槛；新增T0装备制造1级许可；W0废弃误记，不新增穿戴限制"),
    "scavenging_03": ("分类勘整", "普通遗迹与工业资源搜集基础工作量×0.95；不改变固定箱数量，不作用于勘整训练"),
    "scavenging_10": ("周密检索", "普通遗迹与工业资源搜集基础工作量×0.90（替代3级）；不改变固定箱数量，不作用于勘整训练"),
}


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(CONTRACT, encoding="utf-8") as f:
        con = json.load(f)

    rows = {r["resourceId"]: r for r in con["fieldExperience"]["resourceRows"]}
    used = 0
    for r in cfg["resources"]:
        row = rows.get(r["nodeId"])
        assert row is not None, f"合同里没有资源点 {r['nodeId']}"
        assert row["baseMinutes"] == r["baseMinutes"], r["nodeId"]
        r["proficiencyId"] = row["proficiencyId"] if row["xpAllowed"] else None
        used += 1

    man = con["manufacturing"]
    fx = con["fieldExperience"]
    sv = fx["survey"]
    ck = con["cooking"]
    wc = con["workCarry"]
    assert fx["xpFormula"] == "clamp(2*ceil(originalBaseMinutes/5),2,12)"
    assert ck["foodBaseXp"] == "clamp(ceil(originalHeatMinutes/10),1,12)"
    assert man["levelWorkMultiplier"] == 1
    cfg["progression"] = {
        "source": con["version"],
        "dismantle": {
            "stages": man["dismantleStages"],
            "xp": man["dismantleXp"]["amount"],
            "expertCraftingLevel": man["expertRecoveryCraftingLevel"],
            "expertBonus": 1,
        },
        "fieldXp": {
            "formulaText": fx["xpFormula"],
            "stepXp": 2, "stepMinutes": 5, "min": 2, "max": 12,
            "perResourceDaily": fx["fullXpCompletionsPerActorResourceDay"],
            "dailyPerActorSkill": fx["dailyPerActorSkillCap"],
        },
        "survey": {
            "id": sv["id"], "name": sv["name"], "nodeIds": sv["nodeIds"],
            "baseMinutes": sv["baseMinutes"], "xp": sv["xp"], "proficiencyId": sv["proficiencyId"],
            "dailyPerActorNode": sv["actorNodeDailyCompletionLimit"],
            "requiresExplorePercent": sv["requiresExplorePercent"],
            "hazardByNode": {k: {"probability": v["probability"], "healthLoss": v["healthLoss"], "label": "划伤"}
                             for k, v in sv["hazardByNode"].items()},
        },
        "cooking": {
            "foodProcesses": ck["rawFoodProcesses"],
            "foodHeatStepMinutes": 10, "foodMin": 1, "foodMax": 12,
            "foodDailyCap": ck["foodAndActiveRecipeDailyActorCap"],
            "waterProcess": "boil_water",
            "waterMlPerXp": ck["waterXpPerPurifiedMl"],
            "waterDailyCap": ck["waterDailyActorCap"],
        },
        "workCarry": {"maxCredit": wc["maxCredit"], "roundingDecimals": 4},
    }
    # Q57：取消制造等级工作量倍率 1/(1+0.025L)（保留字段只作历史，程序不再读）
    cfg["crafting"]["legacyLevelWorkFactorRemoved"] = True

    for n in cfg["skillNodes"]:
        if n["id"] in NODES:
            status, effects, text = NODES[n["id"]]
            n["status"] = status
            n["effects"] = effects
            n["displayText"] = text
            if n["id"] in A06_ROWS:
                n["name"], n["effectText"] = A06_ROWS[n["id"]]

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"resources {used}, progression ok")


if __name__ == "__main__":
    main()
