# -*- coding: utf-8 -*-
"""B19 周期招募的配置导入（开发计划 v0.9 批次 B19，需求 R60/R61）→ data/config.v0.6.json 的 config.party.recruitment。

数值逐条取自策划案，不自补：
- R08「2026-10-30 B3 实体收容所、周期招募与战利品owner」RC1：收容所 h06_shelter、服务 service_shelter_recruitment（城市服务表里是
  shelter_recruitment，A11 Q33）、72 世界小时（4320 分钟）一期、每期 3 人、随机+RB01 共享 K=min(3,C−1)、全名册存活硬上限 10（主角1+固定6+共享3）；
  开放条件 QMAP01 登记（hub_registered）后首次实际到达。
- RC2 九个职业模板表（本脚本直接解析 R08 原表）：主熟练 Lv2 70% / Lv3 30%，副熟练二选一等权、Lv1/Lv2 各 50%，其余 Lv1，经验级内 0。
- RC3 身体：H 14—20 整数均匀（上限 20）、E 10—16 整数均匀（上限 20）、N/W 40、S 50—75、M 40—65、I 60—75 整数均匀、V0 未感染。
  基础速度、控制抗性、闪避、基础甲不在这里：读已有 actorBaseDefense.randomRecruit（R08「Q62—Q74」当前优先节：抗性 0—10 步 0.5、
  闪避 34—38 步 0.5、甲 0、速度 3.0—5.0 步 0.1；取代 RC3“速度整数3—5”，开发计划 B19.2 B19-1）。
- RC4：登记 0 材料、10 世界分钟 wait（两个 5 分钟段）。
- 候选名前缀（开发假设 B19-2，待确认）：策划写“内容池+稳定流水号”但仓库没有姓名池，先用“幸存者·<模板名><三位流水号>”。

可重复运行，结果相同（只覆盖 config.party.recruitment）。
用法：python tools/import-b19-recruitment.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
R08 = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "02 参考扩展", "R08 队伍招募、编成与共同生存系统.md")

# 模板 ID（程序用；中文名、主副熟练取自 R08 RC2 表，按表内顺序）
TEMPLATE_IDS = ["front_guard", "blade_hand", "breaker", "spear_walker", "salvager", "hunter", "old_service", "coordinator", "camp_nurse"]


def templates():
    text = open(R08, encoding="utf-8").read()
    sec = text[text.index("### RC2."):text.index("### RC3.")]
    rows = []
    for line in sec.splitlines():
        if not line.startswith("|") or "---" in line or "模板" in line:
            continue
        cols = [c.strip() for c in line.strip().strip("|").split("|")]
        name, main, subs, use = cols[0], cols[1], cols[2], cols[3]
        rows.append({"name": name, "main": main, "subs": [x.strip() for x in subs.split("/")], "use": use})
    assert len(rows) == len(TEMPLATE_IDS), rows
    for tid, r in zip(TEMPLATE_IDS, rows):
        r["id"] = tid
    return [{"id": r["id"], "name": r["name"], "main": r["main"], "subs": r["subs"], "use": r["use"]} for r in rows]


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    skill_ids = {s["id"] for s in cfg["skills"]}
    tpl = templates()
    for t in tpl:
        assert t["main"] in skill_ids and all(s in skill_ids for s in t["subs"]) and t["main"] not in t["subs"], t
    cfg["party"]["recruitment"] = {
        "source": "R08 RC1—RC4（2026-10-30 B3 实体收容所、周期招募）、Q62—Q74 当前优先节（防御档在 actorBaseDefense.randomRecruit）；B19",
        "serviceId": "shelter_recruitment",
        "contractServiceId": "service_shelter_recruitment",
        "locationId": "h06_shelter",
        "openedByFact": "hub_registered",
        "refreshMinutes": 4320,
        "candidatesPerCycle": 3,
        "registrationMinutes": 10,
        "sharedRosterMax": 3,
        "rosterAliveCap": 10,
        "mainLevelWeights": {"2": 70, "3": 30},
        "subLevelWeights": {"1": 50, "2": 50},
        "otherLevel": 1,
        "body": {
            "health": [14, 20], "healthMax": 20,
            "energy": [10, 16], "energyMax": 20,
            "satiety": 40, "hydration": 40,
            "wakefulness": [50, 75], "morale": [40, 65], "immunity": [60, 75],
            "virus": 0,
        },
        "namePrefix": "幸存者·",
        "templates": tpl,
    }
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
