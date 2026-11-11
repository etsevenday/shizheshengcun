# -*- coding: utf-8 -*-
"""B41a 新版敌踪：警惕等级与上线开关的配置导入 → data/config.v0.6.json。

数值逐条取自策划案，不自补：
- R05「Q423 警惕逐项定稿」表（42 种敌人 × 普通常驻／固定战成员／守卫本体／可选强敌四列，单位级，合法 1–10）
  → enemies[].vigilance{resident,fixedMember,guardianBody,optionalElite}。
- 同节正文「教学首战的游荡尸特例为 1」→ story.tutorialBattle.vigilance = 1。
- 同节正文「固定战成员至少 3，守卫本体至少 5，可选强敌至少 6」只作断言（表中已是取高后的值，脚本不改表值）。
- encounters.enemyCardsV2：新版敌踪总开关。B41a—B41e 内核与界面先做在关着的开关后面（待确认文档 B41a 节「上线方式」），
  本脚本只在该键不存在时写 false，已有值不动（B41f 翻开关不被重跑覆盖）。

可重复运行，结果相同。用法：python tools/import-b41-vigilance.py
"""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
R05 = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "02 参考扩展", "R05 敌人卡、种群刷新与遭遇系统.md")

HEADING = "### Q423 警惕逐项定稿"
HEADER = ["敌人ID", "名称", "普通常驻", "固定战成员", "守卫本体", "可选强敌"]
KEYS = ["resident", "fixedMember", "guardianBody", "optionalElite"]
FLOORS = {"fixedMember": 3, "guardianBody": 5, "optionalElite": 6}
TUTORIAL_SENTENCE = re.compile(r"教学首战的游荡尸特例为\s*(\d+)")


def read_table():
    """读「Q423 警惕逐项定稿」一节：返回 (rows, 教学首战特例值)。rows: [(敌人ID, 名称, {列键: 值})]。"""
    with open(R05, encoding="utf-8") as f:
        lines = f.read().splitlines()
    start = lines.index(HEADING)
    end = next(i for i in range(start + 1, len(lines)) if lines[i].startswith("### ") or lines[i].startswith("## "))
    section = lines[start + 1:end]
    m = TUTORIAL_SENTENCE.search("\n".join(section))
    assert m, "R05 Q423 节缺少教学首战特例句"
    tutorial = int(m.group(1))
    rows = []
    seen_header = False
    for line in section:
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if not seen_header:
            assert cells == HEADER, cells
            seen_header = True
            continue
        if set(cells[0]) <= set("-: "):
            continue
        assert len(cells) == len(HEADER), cells
        vals = {k: int(v) for k, v in zip(KEYS, cells[2:])}
        rows.append((cells[0], cells[1], vals))
    assert seen_header, "R05 Q423 节缺少警惕表"
    return rows, tutorial


def main():
    rows, tutorial = read_table()
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    enemies = {e["id"]: e for e in cfg["enemies"]}

    ids = [r[0] for r in rows]
    assert len(ids) == len(set(ids)), "警惕表敌人ID重复"
    assert set(ids) == set(enemies), ("警惕表与配置敌种不一致", sorted(set(ids) ^ set(enemies)))
    assert len(rows) == 42, len(rows)
    for eid, name, vals in rows:
        assert enemies[eid]["name"] == name, (eid, name, enemies[eid]["name"])
        for k, v in vals.items():
            assert 1 <= v <= 10, (eid, k, v)
            assert v >= FLOORS.get(k, 1), (eid, k, v)
        enemies[eid]["vigilance"] = vals

    tb = cfg["story"]["tutorialBattle"]
    assert 1 <= tutorial <= 10
    tb["vigilance"] = tutorial

    enc = cfg["encounters"]
    enc.setdefault("enemyCardsV2", False)

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"vigilance rows {len(rows)}, tutorial {tutorial}, enemyCardsV2 {enc['enemyCardsV2']}")


if __name__ == "__main__":
    main()
