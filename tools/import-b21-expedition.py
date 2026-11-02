# -*- coding: utf-8 -*-
"""B21 地牢远征（终止惩罚包、惊魂未定、撤出时长与入口缓冲）的配置导入（开发计划 v0.9 批次 B21，需求 R65/R66）→ data/config.v0.6.json。

数值逐条取自策划案，不自补：
- abortPenalty：R03「2026-10-30 B4 地牢远征、战术品与生命周期」D5.1 表（S12「B4 撤出事件包与生存消耗分界」、A01 同表一致）：
  每名仍存活同行 饱食N −25、水分W −30、体力E −8、清醒S −20、精神M −15（最低 0）；健康H 实扣 min(2, max(0, H−1))
  （healthMax=2、healthKeep=1）。
- shakenMinutes / shakenPanic：同表“惊魂未定 6 世界小时，下一次入战恐慌初值 +10（夹紧 0—100）”。
- extractMinMinutes / extractRoundMinutes：R03 D5.2“T = max(30, 向上取 5 分钟整数(基础路程×倍率))”。
- warn：R03 D3“N/W<40、E<8、S<40、M<30、H≤5 或 V≥80 任一成员标明显警告”。
- bufferNodes：R03 D3“01 是实例内安全缓冲”；取每座地牢 safeNodes 第 1 项（断言为 dXX_01、与父地点有双向入口边）。

同时断言（B21 开发假设 B21-4 的前提）：map.dungeons 与 expansion-combat.json dungeonInterfaces 逐字段一致（id/name/parent/guardNode/
recordNode/exitNode/safeNodes）；每座地牢内部边全部双向（只有 exitNode→父地点单向）——撤出返程按“本代已开通、两端到过的边”求最短路，
将来出现单向内部边会在这里报错（待确认 Q198“应急可返回边”）。

可重复运行，结果相同（覆盖 dungeonExpedition 块）。
用法：python tools/import-b21-expedition.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
DOC = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0")
COMBAT = os.path.join(DOC, "assets", "20261030", "combat", "expansion-combat.json")


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(COMBAT, encoding="utf-8") as f:
        combat = json.load(f)

    dungeons = cfg["map"]["dungeons"]
    iface = {d["id"]: d for d in combat["dungeonInterfaces"]}
    assert len(dungeons) == 8 and len(iface) == 8
    locs = {l["id"]: l for l in cfg["locations"]}
    buffers = {}
    for d in dungeons:
        j = iface[d["id"]]
        for k in ("name", "parent", "guardNode", "recordNode", "exitNode", "safeNodes"):
            assert d[k] == j[k], (d["id"], k)
        buf = d["safeNodes"][0]
        assert buf == d["id"] + "_01", buf
        assert locs[buf].get("dungeonId") == d["id"] and locs[buf].get("danger", 0) == 0, buf
        entry = [e for e in cfg["edges"] if {e["a"], e["b"]} == {buf, d["parent"]}]
        assert len(entry) == 1 and entry[0]["bidirectional"] is True, buf
        buffers[d["id"]] = buf
        rooms = {l["id"] for l in cfg["locations"] if l.get("dungeonId") == d["id"]}
        for e in cfg["edges"]:
            if e["a"] in rooms and e["b"] in rooms:
                assert e["bidirectional"] is True, ("内部边不是双向", e["id"])
            elif e.get("dungeonId") == d["id"] and e["a"] == d["exitNode"] and e["b"] == d["parent"]:
                assert e["bidirectional"] is False, e["id"]

    cfg["dungeonExpedition"] = {
        "source": "R03 D3/D5.1/D5.2、S12 B4 撤出事件包、A01 B4 事件属性索引（策划已批准可调初值）",
        "abortPenalty": {"satiety": -25, "hydration": -30, "energy": -8, "wakefulness": -20, "morale": -15, "healthMax": 2, "healthKeep": 1},
        "shakenMinutes": 360,
        "shakenPanic": 10,
        "extractMinMinutes": 30,
        "extractRoundMinutes": 5,
        "warn": {"satietyBelow": 40, "hydrationBelow": 40, "energyBelow": 8, "wakefulnessBelow": 40, "moraleBelow": 30, "healthAtMost": 5, "virusAtLeast": 80},
        "bufferNodes": buffers,
    }

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"dungeonExpedition: {len(buffers)} buffer nodes")


if __name__ == "__main__":
    main()
