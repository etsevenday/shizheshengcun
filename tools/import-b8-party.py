# -*- coding: utf-8 -*-
"""B8 队伍与共同生存的配置导入（开发计划 v0.9 批次 B8，R31/R32）。

数值逐条取自策划案，不自补：
- A11 §6「六名命名可招募角色」表 → config.party.namedCompanions：人物 ID、姓名、背景一句、初始技能（其余 1 级）、
  基础速度、武器槽1/武器槽2 的武器 ID（其余五槽与五卡空，A11 §6 首段）。控制抗性/闪避/甲仍读已有 actorBaseDefense，不复制。
- A11 §7 RB01 行 → config.party.escortTemplate：15 门技能均 1、基础速度 4、借用 wooden_spear 1 件、30 分钟 wait 登记；
  同时存活通用护送员最多 C−1（R08 RC1 写 min(3,C−1)，C≤4 时同值）。
- A11 §5.2 / R08 §2 → config.party.recruitInitialStats：H20 且 H 上限 20、N85/W85/E18/S90/M70/I70/V0 未感染。
- R08 §3 / A11 §5.3 → config.party.reserveCare。B8c（策划答复 Q90，R08/S13 「Q90 休息睡眠与候补自主恢复」本轮章、
  assets/20261031/party/party-adjudications.json reserveCare）取代原 N/W 托底 40 与 22:00—06:00 固定作息：候补按自身 S/E 自动 rest/sleep
  （段首 S<60 或 E<6 进 sleep，S≥90 且 E≥14 回 rest），公共补给 N+5.2/W+6.4/M+1 每小时、只补到 80；规则版本 2。
- 营区替身（待确认 Q89）：v3 地图（B11）前编队、候补居住、RB01 与报失都在 camp 办理（hubStandIn），B11 导入后改为 h02/h06。

可重复运行，结果相同。
用法：python tools/import-b8-party.py
"""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
A11 = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "04 附录", "A11 主线任务、势力与队伍角色目录.md")


def named_companions():
    text = open(A11, encoding="utf-8").read()
    sec = text[text.index("## 6 六名命名可招募角色"):text.index("## 7 招募与补充任务表")]
    rows = []
    for line in sec.splitlines():
        if not line.startswith("| npc_"):
            continue
        cols = [c.strip() for c in line.strip().strip("|").split("|")]
        actor_id, name = cols[0].split(" ", 1)
        background = cols[1]
        skills = {}
        for part in re.split("[、，,]", cols[2]):
            m = re.fullmatch(r"([a-z_]+)(\d+)", part.strip())
            assert m, part
            skills[m.group(1)] = int(m.group(2))
        speed = float(cols[3])
        w1, w2 = [w.strip() for w in cols[5].split("/")]
        rows.append({
            "actorId": actor_id,
            "name": name.strip(),
            "background": background,
            "skills": skills,
            "baseSpeed": speed,
            "weapon1": w1,
            "weapon2": w2,
            "recruitTask": cols[6].split("；")[0],
        })
    assert len(rows) == 6, rows
    return rows


def main():
    cfg = json.load(open(CONFIG, encoding="utf-8"))
    cfg["party"] = {
        "namedCompanions": named_companions(),
        "escortTemplate": {
            "namePrefix": "护送员",
            "skillLevel": 1,
            "baseSpeed": 4,
            "loanWeapon": "wooden_spear",
            "registrationMinutes": 30,
            "maxAlive": 3,
        },
        "recruitInitialStats": {
            "health": 20, "healthMax": 20, "satiety": 85, "hydration": 85, "energy": 18,
            "wakefulness": 90, "morale": 70, "immunity": 70, "virusLoad": 0,
        },
        "reserveCare": {
            # Q90（B8c）：照护规则版本（存档记录，旧档迁移时初始化模式一次）
            "ruleVersion": 2,
            "enterSleepWakefulnessBelow": 60,
            "enterSleepEnergyBelow": 6,
            "exitSleepWakefulnessAtLeast": 90,
            "exitSleepEnergyAtLeast": 14,
            "publicSatietyPerHour": 5.2,
            "publicHydrationPerHour": 6.4,
            "publicNutritionTarget": 80,
            "publicMoralePerHour": 1,
            "publicMoraleTarget": 80,
            "redHealth": 5,
            "redVirus": 80,
        },
        "hubStandIn": "camp",
        # Q88/Q89 选 A（B8b）：收容所服务（RB01、报失）另一个配置位；v3 地图有 h02_square/h06_shelter 时迁址
        "shelterStandIn": "camp",
        "hubV3": "h02_square",
        "shelterV3": "h06_shelter",
    }
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
