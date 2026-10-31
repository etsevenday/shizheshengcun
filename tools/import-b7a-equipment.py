# -*- coding: utf-8 -*-
"""B7a 答复 Q44–Q52 的装备数据导入（开发计划 v0.9 批次 B7a，R93/R96）。

数值逐条取自策划案，不自补：
- config.weapons[].salvage / config.gear[].salvage：72 件逐项拆解表（回收材料、白/绿/蓝基础数量、
  标准配方材料价值 I、是否不可自制固定表、回收专家最多追加数），来自
  doc/策划案/系统策划总册 v1.0/assets/20261031/equipment/equipment-adjudications.json 的 dismantle.rows
  （A05「2026-10-31 Q44—Q52装备裁定（当前优先）」§拆解价值与完整产出 同表）；
  该文件后来（d75d149）多了 Q54/Q55 的护拳绑带等行，config 里没有的装备跳过（B7b 再导）；
- config.equipment.salvage.materialValues 与 capNumerator/capDenominator（5R≤4I）：同上 dismantle.materialValues / cap；
- config.affixes：战斗饰品池 accurate/swift/anchored、求生饰品池 craftsman/economical/patient/surefoot
  （A12 equipment.affixes 的 additionalCompatibleSlots/additionalSlotGroups，Q51）；只加池与饰品专用互斥组，原衣物组不变；
- config.skillNodes scavenging_10 回收专家：Q46 改为“同材料追加1且受预算”（取代固定额外废金属1）。
  Q54 把它迁到制造10级、拆解给制造经验2、按制造等级开放 T0—T3 拆解，属下一批 B7b，这里不动等级与门槛。
本脚本取代 import-b4-catalog.py 写的旧拆解字段（byQuality/count、scrapByQuality）：重跑 B4 脚本后须再跑本脚本。
可重复运行，结果相同。
用法：python tools/import-b7a-equipment.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261031", "equipment", "equipment-adjudications.json")
A12 = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "04 附录", "A12 全系统策划数据索引.json")

# A05 槽名 → 代码槽名（B4 H2）
SLOT = {"combat_charm": "combat_acc", "survival_charm": "survival_acc"}

# 饰品上的生效域（A05 Q44—Q52 章 §饰品兼容池原文；E/W 写成体力/水分以免界面出现内部字母）
CHARM_TEXT = {
    "accurate": "佩戴者全部合法攻击命中能力+4（含空槽拳击）",
    "swift": "装备速度+1",
    "anchored": "位移抵抗+10个百分点",
    "craftsman": "制造/建造/维护/拆解工作量×0.97",
    "economical": "非战斗负水分小时率×0.97",
    "patient": "遮蔽休息正体力恢复+0.1/小时",
    "surefoot": "移动负体力小时率×0.95",
}

EXPERT_TEXT = ("拆解完成后尝试追加1份同种回收材料：可自制装备只在总回收价值仍不超过标准配方材料价值80%时追加，"
               "否则不追加；不可自制装备按固定表追加1")


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(ADJ, encoding="utf-8") as f:
        adj = json.load(f)
    with open(A12, encoding="utf-8") as f:
        a12 = json.load(f)

    dis = adj["dismantle"]
    rows = {r["itemId"]: r for r in dis["rows"]}
    known = {w["id"] for w in cfg["weapons"]} | {g["id"] for g in cfg["gear"]}
    used = 0
    for coll in (cfg["weapons"], cfg["gear"]):
        for x in coll:
            if x["id"] == "fists":
                continue
            r = rows.get(x["id"])
            if r is None:
                x["salvage"] = None   # 未列新装备拒绝拆解（unknownNewItem）
                continue
            assert r["nonCraftableFixedTable"] or (r["capNumerator"] == 4 and r["capDenominator"] == 5), r
            x["salvage"] = {
                "itemId": r["material"],
                "counts": {"white": r["white"], "green": r["green"], "blue": r["blue"]},
                "recipeValue": None if r["nonCraftableFixedTable"] else r["canonicalRecipeMaterialValue"],
                "nonCraftable": r["nonCraftableFixedTable"],
                "expertMaterial": r["expertBonusMaterial"],
                "expertMax": r["expertBonusMax"],
            }
            used += 1
    skipped = sorted(set(rows) - known)

    sal = cfg["equipment"]["salvage"]
    cfg["equipment"]["salvage"] = {
        "recipeId": sal["recipeId"],
        "materialValues": dis["materialValues"],
        "capNumerator": 4,
        "capDenominator": 5,
    }

    a12_affix = {a["id"]: a for a in a12["equipment"]["affixes"]}
    for a in cfg["affixes"]:
        src = a12_affix[a["id"]]
        extra = src.get("additionalCompatibleSlots") or []
        base_pools = [p for p in a["pools"] if p not in ("slot:combat_acc", "slot:survival_acc")]
        base_text = a["poolText"].split("；")[0]
        if not extra:
            a.pop("slotGroups", None)
            a.pop("slotEffectText", None)
            a["pools"] = base_pools
            a["poolText"] = base_text
            continue
        a["pools"] = base_pools + ["slot:" + SLOT[sl] for sl in extra]
        a["slotGroups"] = {SLOT[sl]: src["additionalSlotGroups"][sl] for sl in extra}
        a["slotEffectText"] = {SLOT[sl]: CHARM_TEXT[a["id"]] for sl in extra}
        a["poolText"] = base_text   # 保持 A05/A12 原池文字；饰品池在界面另写（Q51）

    node = next(n for n in cfg["skillNodes"] if n["id"] == "scavenging_10")
    node["effects"] = [{"key": "salvage.expertBonus", "op": "add", "value": 1}]
    node["displayText"] = EXPERT_TEXT

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"salvage rows {used}, skipped (not in config) {skipped}")


if __name__ == "__main__":
    main()
