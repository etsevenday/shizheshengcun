# -*- coding: utf-8 -*-
"""B5 十级熟练度与 75 个技能节点导入（开发计划 v0.9 B5 H1/H6）。

从 doc/策划案/系统策划总册 v1.0/04 附录/A06 十级熟练度与全技能目录.md 的 §1 与 §2「全技能主表」生成：
- config.skills：15 门（A06 §4 顺序与中文名），初始 1 级、门槛 50*(L+1)、上限 10；
- config.skillGrowth.skillLevelCap = 10；
- config.skillNodes：75 个节点（门类、等级、名称、AP、基准E、使用位→目标、效果原文、冷却、额外物资），
  求生被动按下面 SURVIVAL 表附上程序效果键（数值逐条取自 §2 表的效果列，不自补）；
- config.skillKindBaseEnergy：§1 各类基准E（拳.30/刀剑.40/……），供 B6/B7 主动技能 E 成本换算。
可重复运行，结果相同；旧技能/物品等其他配置不动。
用法：python tools/import-b5-skills.py
"""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
A06 = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "04 附录", "A06 十级熟练度与全技能目录.md")

# A06 §4：战斗门类 9 + 医疗（战内外）+ 求生 5，共 15 门
SKILLS = [
    ("unarmed", "拳", "combat"), ("blade", "刀剑", "combat"), ("axe", "斧", "combat"), ("spear", "矛", "combat"),
    ("blunt", "钝器", "combat"), ("archery", "弓弩", "combat"), ("firearms", "枪械", "combat"), ("shield", "盾", "combat"),
    ("command", "指挥", "combat"), ("medicine", "医疗", "both"), ("crafting", "制造", "survival"),
    ("gathering", "采集", "survival"), ("scavenging", "搜集", "survival"), ("cooking", "烹饪", "survival"),
    ("athletics", "行旅", "survival"),
]

# A06 §1：同类不同装备按 武器E÷该类基准E 等比例换算（拳.30/刀剑.40/斧.65/矛.50/钝器.60/弓.40/枪.25/盾.30/指挥.20/医疗.20）
KIND_BASE_E = {"unarmed": 0.30, "blade": 0.40, "axe": 0.65, "spear": 0.50, "blunt": 0.60, "archery": 0.40,
               "firearms": 0.25, "shield": 0.30, "command": 0.20, "medicine": 0.20}

# 求生被动（25 个）的程序口径。status：
#   live      已接入现行规则；
#   baseline  1 级“开放原有内容”，现有内容本来就开放，无额外数值；
#   dormant   效果明确但现在没有挂点（新食品配方、制作等级3/7/10 配方、合格搜集工具等后续批次才有），只存数据；
#   pending   有未定义的词，待策划确认（开发计划 v0.9 B5.4、待确认 Q53 起）。
SURVIVAL = {
    "crafting_01": ("pending", []),   # “原新手配方”=现有配方，1 级本就可做；“W0装备许可”未定义（待确认）
    "crafting_03": ("dormant", []),   # 制作等级3配方与工作台/缝纫台在 B10
    "crafting_05": ("live", [{"key": "repair.fractionBonus", "op": "add", "value": 0.05},
                             {"key": "repair.fractionCap", "op": "set", "value": 0.30}]),
    "crafting_07": ("dormant", []),
    "crafting_10": ("dormant", []),   # 定向词条需精密零件与 B10 工坊
    "gathering_01": ("baseline", []),
    "gathering_03": ("live", [{"key": "hazard.gatherChance", "op": "mul", "value": 0.9}]),
    "gathering_05": ("live", [{"key": "yield.gatherBasic", "op": "add", "value": 1}]),
    "gathering_07": ("live", [{"key": "work.gather", "op": "mul", "value": 0.9}]),
    "gathering_10": ("live", [{"key": "yield.gatherFirstDaily", "op": "add", "value": 1}]),
    "scavenging_01": ("baseline", []),
    "scavenging_03": ("pending", []), # “低阶装备”未定义；拆解现状不设熟练门槛（待确认）
    "scavenging_05": ("dormant", [{"key": "work.scavengeTool", "op": "mul", "value": 0.9}]),  # 现有搜集点都没有工具加成
    "scavenging_07": ("live", [{"key": "hazard.scavengeChance", "op": "mul", "value": 0.8}]),
    "scavenging_10": ("live", [{"key": "salvage.expertBonus", "op": "add", "value": 1}]),  # B7a Q46：同材料追加且受 80% 预算；displayText 由 import-b7a-equipment.py 覆盖
    "cooking_01": ("baseline", []),
    "cooking_03": ("dormant", [{"key": "work.cookNew", "op": "mul", "value": 0.95}]),
    "cooking_05": ("dormant", []),
    "cooking_07": ("dormant", [{"key": "work.cookNew", "op": "mul", "value": 0.90}]),
    "cooking_10": ("dormant", [{"key": "cook.rationSatiety", "op": "add", "value": 3}]),
    "athletics_01": ("baseline", []),
    "athletics_03": ("live", [{"key": "rate.move.energy", "op": "mul", "value": 0.95}]),
    "athletics_05": ("live", [{"key": "work.moveLight", "op": "mul", "value": 0.95}]),
    "athletics_07": ("live", [{"key": "rate.move.energy", "op": "mul", "value": 0.90}]),
    "athletics_10": ("live", [{"key": "work.moveVisited", "op": "mul", "value": 0.90}]),
}

# 被动的附加参数（取自 §2 效果原文）
PASSIVE_RULES = {
    "gatherBasicItems": ["wood", "stone", "fiber"],      # gathering_05“木/石/纤维定向采集”
    "industrialItems": ["scrap", "parts"],               # gathering_10“不生产工业材料”（开发假设：废金属、零件，待确认）
    "moveLightMaxGrams": 20000,                          # athletics_05“≤20kg”
}


def num(s):
    s = s.strip()
    return float("0" + s) if s.startswith(".") else float(s)


STAT_NAMES = {"N": "饱食", "W": "水分", "M": "精神", "H": "健康", "E": "体力", "S": "清醒度", "I": "免疫", "V": "病毒"}
# 整句改写（A06 原文的内部写法 → 玩家能读的说法，数值不改；B5 试玩修复 M127）
PHRASES = [
    ("负E小时率", "每小时体力消耗"),
    ("完全按R04绷带效果/目标冷却", "效果与目标冷却完全同绷带"),
    ("基础E成本", "基础体力成本"),
    ("E使用武器基础成本", "体力使用武器基础成本"),
    ("正N额外", "饱食额外"),
    ("AP+1", "行动点+1"),
    ("W0装备许可", "0阶（W0）装备许可（含义待策划确认）"),
    ("总回收不得超过配方投入价值80%", "总回收不得超过配方投入价值80%（80%上限暂未实装）"),
    ("(1+0.02L)", "(1+0.02×医疗等级)"),
]


def readable(text, items):
    """技能效果原文里的物品 ID、状态缩写、文档编号改成中文（同 import-b4-catalog.py readable() 的做法）：
    bandage1 → 草药绷带×1，H/E/N/V → 健康/体力/饱食/病毒，kg → 公斤，R04 等编号去掉；数值不改。"""
    for a, b in PHRASES:
        text = text.replace(a, b)
    text = re.sub(r"([a-z][a-z_]*[a-z])(\d+)", lambda m: f"{items.get(m.group(1), m.group(1))}×{m.group(2)}", text)
    text = re.sub(r"(\d)kg", r"\g<1>公斤", text)
    text = re.sub(r"\b[RSA]\d\d\b", "", text)
    text = re.sub(r"(?<![A-Za-z（])([NWMHESIV])(?![A-Za-z0-9）])", lambda m: STAT_NAMES[m.group(1)], text)
    text = text.replace("/", "、")
    return text


def parse_nodes(text):
    nodes = []
    for line in text.splitlines():
        m = re.match(r"^\|\s*([a-z]+)_(\d\d)\s*\|", line)
        if not m:
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        sid, lv_cell, name, ap_e, pos, effect, cd, extra = cells[:8]
        skill, lv = lv_cell.split("／")
        ap, base_e = ap_e.split("／")
        node = {
            "id": sid,
            "skillId": skill,
            "level": int(lv),
            "name": name,
            "ap": int(ap),
            "baseEnergy": num(base_e),
            "positions": pos,
            "effectText": effect,
            "cooldownRounds": int(cd),
            "extraCost": extra,
        }
        if sid in SURVIVAL:
            status, effects = SURVIVAL[sid]
            node["kind"] = "survival"
            node["status"] = status
            node["effects"] = effects
        else:
            node["kind"] = "combat"
            node["status"] = "combat"   # 主动技能/战斗被动在 B6/B7 执行，本批只存数据
            node["effects"] = []
        nodes.append(node)
    return nodes


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(A06, encoding="utf-8") as f:
        nodes = parse_nodes(f.read())
    assert len(nodes) == 75, len(nodes)
    assert set(SURVIVAL) <= {n["id"] for n in nodes}
    items = {i["id"]: i["name"] for i in cfg["items"]}
    for n in nodes:
        n["displayText"] = readable(n["effectText"], items)   # 界面显示用；effectText 保留 A06 原文
        assert not re.search(r"[a-z_]{3,}|\bR\d\d", n["displayText"]), n["displayText"]

    old = {s["id"]: s for s in cfg["skills"]}
    cfg["skills"] = [{
        "id": sid,
        "name": name,
        "category": cat,
        "initialLevel": old.get(sid, {}).get("initialLevel", 1),
        "nextLevelXpFormula": "50*(L+1)",
        "levelCap": 10,
    } for sid, name, cat in SKILLS]
    cfg["skillGrowth"]["skillLevelCap"] = 10
    cfg["skillNodes"] = nodes
    cfg["skillKindBaseEnergy"] = KIND_BASE_E
    cfg["skillPassiveRules"] = PASSIVE_RULES

    # A06 §3 新增经验来源与防刷额度：采集/搜集每成功提交+2、同资源点每日最多3次（按世界日）；行旅每完整移动+1、同向边每日最多1次
    cfg["xp"]["gatherSubmit"] = 2
    cfg["xp"]["gatherDailyPerNode"] = 3
    cfg["xp"]["travelEdge"] = 1
    cfg["xp"]["travelDailyPerDirectedEdge"] = 1

    # 斧类武器改练“斧”，盾/指挥器具挂各自门类（A06 §4；开发假设 H2）；医疗器具仍练医疗
    for w in cfg["weapons"]:
        if w.get("weaponKind") in ("axe", "shield", "command"):
            w["skillId"] = w["weaponKind"]
    # 现行两行战斗的劈砍/割伤原只认 blade；斧改门类后仍可用（旧档斧等级=旧刀斧等级，行为不变；B7 换成五节点）
    for a in cfg["combatActions"]:
        if a["id"] in ("cleave", "cut") and "axe" not in a.get("skills", []):
            a["skills"].append("axe")
    # 旧特效药门槛 医疗15/制造12 废止，改 医疗10/制造10（A06 §4、R01 §5；主线蓝图条件待 B11/B15，开发假设 H9）
    for r in cfg["recipes"]:
        if r["id"] == "make_antiviral_cure":
            r["minSkills"] = {"medicine": 10, "crafting": 10}

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print(f"skills {len(cfg['skills'])}, skillNodes {len(nodes)}")


if __name__ == "__main__":
    main()
