# -*- coding: utf-8 -*-
"""B7d 答复 Q75–Q80、Q82 的战斗配置导入（开发计划 v0.9 批次 B7d，R99）。

数值逐条取自策划案，不自补：
- doc/策划案/系统策划总册 v1.0/assets/20261031/combat/combat-resolution-adjudications.json（R03「2026-10-31 Q75—Q82技能、真实站位与伤害裁定」同源）
  - positions.boundarySkillException + A12 equipment.skills 三行（A06 全技能主表本轮改行）：
    unarmed_07 挣脱 1234→自身（清减速/束缚/标记、后移1；4号位原地清；完全无收益拒绝）；
    blade_07 突进斩 123→12（1号位原地攻击，2/3号位前移1再攻）；spear_07 贯穿 123→1234（选起点k，真实k至4，按槽偏移衰减）。
  - pierce → skillExec spear_07：targetMode "suffix"、suffixFactors 0.65/0.50/0.35/0.25（AP2/E0.8/CD1 不变，仍在 skillNodes）。
  - damage.softCap → combatMath.damageBuffKnee/damageBuffTail（rawB≤1.35 原值，以上 1.35+0.25×(1−exp(−(rawB−1.35)/0.25))，渐近1.6），
    删除旧硬上限 damageBuffCap=1.5（oldGlobalHardCap1_5Removed）。
  - enemyActivation.exposureApproach → combatMath.exposureApproach（迫近1AP + 一次2AP弱普通近袭×0.70）。
  - positions.voluntaryMove → combatActions swap 的 ap/energy 校验（通用“调整站位”1AP/E0.1，沿用指令 ID swap）。

可重复运行，结果相同。
用法：python tools/import-b7d-combat.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261031", "combat", "combat-resolution-adjudications.json")
A12 = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "04 附录", "A12 全系统策划数据索引.json")

SKILLS = ("unarmed_07", "blade_07", "spear_07")


def skill_rows(cfg, a12):
    rows = {s["id"]: s for s in a12["equipment"]["skills"] if s["id"] in SKILLS}
    assert set(rows) == set(SKILLS)
    for n in cfg["skillNodes"]:
        r = rows.get(n["id"])
        if r is None:
            continue
        assert n["ap"] == r["ap"] and abs(n["baseEnergy"] - r["energy"]) < 1e-9 and n["cooldownRounds"] == r["cooldown_rounds"], n["id"]
        n["positions"] = f"{r['own']}→{r['target']}"
        n["effectText"] = r["effect"]
        n["displayText"] = r["effect"]


def pierce(cfg, adj):
    p = adj["pierce"]
    assert p["skillId"] == "spear_07" and p["ap"] == 2 and p["cooldown"] == 1 and p["weaponDurabilityPerCommand"] == 1
    assert p["emptySlotsSkippedButNotRenumbered"] and p["allTargetIdsAndSlotsSnapshottedAtSubmit"]
    x = next(x for x in cfg["skillExec"] if x["id"] == "spear_07")
    x["targetMode"] = "suffix"
    x["suffixFactors"] = p["damageFactorsBySlotOffset"]
    x["damageFactor"] = p["damageFactorsBySlotOffset"][0]


def struggle(cfg, adj):
    u = adj["positions"]["boundarySkillException"]["unarmed_07"]
    x = next(x for x in cfg["skillExec"] if x["id"] == "unarmed_07")
    x["cleanse"] = True
    x["moveBack"] = 1
    x["cleanseStatuses"] = u["clears"]


def lunge(cfg, adj):
    b = adj["positions"]["boundarySkillException"]["blade_07"]
    assert b["ownSlots"] == [1, 2, 3] and b["atFront1"] == "attack_in_place_no_forward_move"
    x = next(x for x in cfg["skillExec"] if x["id"] == "blade_07")
    x["advance"] = 1


def damage(cfg, adj):
    sc = adj["damage"]["softCap"]
    assert sc["oldGlobalHardCap1_5Removed"]
    m = cfg["combatMath"]
    m.pop("damageBuffCap", None)
    m["damageBuffKnee"] = sc["knee"]
    m["damageBuffTail"] = sc["tail"]
    ea = adj["enemyActivation"]["exposureApproach"]
    assert not ea["biteOrControlOrOtherSpecialIntent"] and not ea["changesOpponentActualSlot"]
    m["exposureApproach"] = {"approachAp": ea["approachAp"], "plainAttackAp": ea["plainAttackAp"], "damageFactor": ea["damageFactor"]}


def voluntary_move(cfg, adj):
    v = adj["positions"]["voluntaryMove"]
    sw = next(a for a in cfg["combatActions"] if a["id"] == "swap")
    assert sw["ap"] == v["ap"] and abs(sw["energy"] - v["energy"]) < 1e-9 and v["allowSolo"]


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(ADJ, encoding="utf-8") as f:
        adj = json.load(f)
    with open(A12, encoding="utf-8") as f:
        a12 = json.load(f)
    skill_rows(cfg, a12)
    pierce(cfg, adj)
    struggle(cfg, adj)
    lunge(cfg, adj)
    damage(cfg, adj)
    voluntary_move(cfg, adj)
    with open(CONFIG, "w", encoding="utf-8") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("combatMath soft cap", cfg["combatMath"]["damageBuffKnee"], cfg["combatMath"]["damageBuffTail"])


if __name__ == "__main__":
    main()
