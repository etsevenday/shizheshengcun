# -*- coding: utf-8 -*-
"""B22h E3 保护无同伴落空标记（开发计划 v0.9 批次 B22h，需求 R198；Q195 按用户修订）→ data/config.v0.6.json enemies[].protectLapseNoTarget。

合同 party-combat-expedition-adjudications.json protection：“此无同伴分支适用于E3写有保护的4普通/8守卫相关意图，不改玩家技能规则。”
E3（A09「E3 新增12个敌人面板与行为」，tools/import-b20-enemies.py NEW_INTENTS）里写有保护（kind=protect）的是：
ec_stockbracer 货架护卫感染者（4 普通之一）、ec_boss_armory 军械库守卫、ec_boss_water 泵房推挤者、ec_boss_mall 扶梯拦截者（8 守卫）。
本工具按“ec_ 前缀（E3 新敌）且意图里有 protect”找出并断言正好是这 4 个；旧敌（z_guard、z_isolation_guard、raider_melee）不标、保持现状。

可重复运行，结果相同。
用法：python tools/import-b22h-protect.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
EXPECTED = {"ec_stockbracer", "ec_boss_armory", "ec_boss_water", "ec_boss_mall"}


def has_protect(e):
    it = e.get("intents") or {}
    return any(st.get("kind") == "protect" for st in (it.get("opening") or []) + (it.get("cycle") or []))


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    found = {e["id"] for e in cfg["enemies"] if e["id"].startswith("ec_") and has_protect(e)}
    assert found == EXPECTED, found
    for e in cfg["enemies"]:
        if e["id"] in EXPECTED:
            e["protectLapseNoTarget"] = True
        else:
            e.pop("protectLapseNoTarget", None)
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
