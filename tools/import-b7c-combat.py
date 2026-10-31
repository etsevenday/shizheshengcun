# -*- coding: utf-8 -*-
"""B7c 答复 Q62–Q74 的战斗配置导入（开发计划 v0.9 批次 B7c，R98）。

数值逐条取自策划案，不自补：
- doc/策划案/系统策划总册 v1.0/assets/20261031/combat/combat-adjudications.json（R03「2026-10-31 Q62—Q74战斗数值与操作裁定」同源）
  - capacity → config.scope.capacity（Q62 选 B：新局及无真实事实 C1；登记 C2、两种关键记录之一 C3、二者都有 C4；只增不减）；
    删除临时 scope.defaultCapacity=3（oldNoFactsDefault3Removed）。

可重复运行，结果相同。
用法：python tools/import-b7c-combat.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261031", "combat", "combat-adjudications.json")

# combat-adjudications.json capacity.unlockEvents 的事实名 → 存档 Progress.Flags 里的事实（R03 本轮章写法 hub_registered / church_route_record / academy_safety_record）
CAPACITY_UNLOCKS = {
    "hub_registered": {"allOf": ["hub_registered"], "anyOf": [], "text": "营区登记"},
    "first_of_church_route_record_or_academy_safety_record": {
        "allOf": [], "anyOf": ["church_route_record", "academy_safety_record"], "text": "教会路线记录或学院安全记录之一"},
    "both_distinct_church_and_academy_records": {
        "allOf": ["church_route_record", "academy_safety_record"], "anyOf": [], "text": "教会路线记录与学院安全记录都有"},
}


def capacity(cfg, adj):
    cap = adj["capacity"]
    assert cap["oldNoFactsDefault3Removed"] and cap["newBattleCapacityLocked"] and cap["activeLegacyBattleCapacityPreserved"]
    scope = cfg["scope"]
    scope.pop("defaultCapacity", None)
    unlocks = []
    for ev in cap["unlockEvents"]:
        rule = CAPACITY_UNLOCKS[ev["fact"]]
        unlocks.append({"capacity": ev["capacity"], **rule})
    scope["capacity"] = {"newGame": cap["newAndNoMilestoneDefault"], "max": cap["max"], "unlocks": unlocks}


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(ADJ, encoding="utf-8") as f:
        adj = json.load(f)
    capacity(cfg, adj)
    with open(CONFIG, "w", encoding="utf-8") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("scope.capacity", cfg["scope"]["capacity"])


if __name__ == "__main__":
    main()
