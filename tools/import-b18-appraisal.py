# -*- coding: utf-8 -*-
"""
B18（开发计划 v0.9）：鉴定交易（需求 R57—R59）导入 data/config.v0.6.json。
可重复运行（覆盖 config.appraisal，并把城市服务 appraisal_white_v1 标为已实装，不动其他配置）。

来源：
- 02 参考扩展/R07「X3 营区有限鉴定与活性物质换物」X3.2（基础说明 0 分钟；详细鉴定 10 分钟 wait、fiber1、样本不消耗；
  换物 5 分钟 wait、普通活性物质 1、全队每世界日 2 份）、X3.3（三档报价与报价版本 white_barter_v2）、X3.4（appraisalRecord：
  识别范围版本 r07_white_v1、结果 ordinary_no_effect / fixed_tutorial_attack）
- assets/20261030/facilities/appraisal-service-contract.json（同上数值的机器可读版）
- A11「Q33 城市岗位完整服务表」appraisal_white_v1：沿所引原契约，不叠收本表费用（Q33 选 B）
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
CONTRACT = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "facilities", "appraisal-service-contract.json")


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(CONTRACT, encoding="utf-8") as f:
        c = json.load(f)
    sv = c["services"]
    appraise, trade = sv["appraise"], sv["trade"]
    sample_item = next(iter(trade["consume"]))
    cfg["appraisal"] = {
        "source": "R07 X3.2—X3.5、appraisal-service-contract.json（B18）",
        "serviceId": c["serviceId"],
        "locationId": c["locationId"],
        "sampleItemId": sample_item,
        "scopeVersion": "r07_white_v1",
        "infoMinutes": sv["information"]["worldMinutes"],
        "appraiseMinutes": appraise["worldMinutes"],
        "appraiseConsume": appraise["consume"],
        "tradeMinutes": trade["worldMinutes"],
        "tradeDailyPartyQuota": trade["dailyPartyQuota"],
        "quoteVersion": c["quoteVersion"],
        "quoteKeepDays": 7,
        "identifiedName": "活性物质（白色·已鉴定）",   # B22d Q178 选 A（appraisal-service-contract.json q176Q178.identifiedName）
        "tutorialName": "未鉴定的活性物质（首战教学）",   # B22f Q179 选 A（PCE rules.Q179）
        "tutorialIdentifiedName": "活性物质（白色·已鉴定·首战教学）",   # B22f Q179 选 A
        "offers": [
            {"id": o["id"], "itemId": o["itemId"], "min": o["inclusiveQuantityBounds"][0], "max": o["inclusiveQuantityBounds"][1],
             "minGrams": o["inclusiveWeightGrams"][0], "maxGrams": o["inclusiveWeightGrams"][1]}
            for o in c["offers"]
        ],
    }
    for s in cfg["city"]["services"]:
        if s["id"] == c["serviceId"]:
            s["implemented"] = True
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
