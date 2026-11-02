# -*- coding: utf-8 -*-
"""B13 新手序章的配置导入（开发计划 v0.9 批次 B13，R39—R43）。

数值与文字逐条取自策划数据，不自补：
- prologue-data.json：18 项 PRO 任务（标题、建议阶段、完成条件、教学表达、是否离开门槛）、来客程望（npc_cheng_wang、camp、
  notBeforeWorldMinute 6240、首战胜利、下一安全边界、常驻到交谈、不传送）、出区（village→m01_road，首战或 Legacy 等效 +
  camp_route_known + visited(village)，不要村庄 100、不要融合、不要全任务）。
- narrative-texts.json（Q32 选 B 定稿）：taskTexts（PROxx_hint 中性系统提示、PROxx_reaction 饱饱/主角短对白、PROxx_done 完成反馈）、
  contentBindings.prologueTalk 八组程望对白、documents 里的 PRO15 救助残页 / 南口路标两种 / 求医路线日志。speaker 映射到现有对白
  系统：system → 空（中性叙述），protagonist → “你”（左侧），baobao → “饱饱”，npc_cheng_wang → “程望”。
- S15 §2 建议五日节奏表“当前叙事问题”列作为各阶段主目标标题；§5 “前往幸存者营区寻找医疗帮助”作为取得路线后的主目标；
  §6 PRO17 基础路线 village→m01(30)→m02(20)→h01(15)→h02(5) 合计 70（分钟从 edges 配置读并断言）、建议净水2份+可食2份+防身物。

可重复运行，结果相同（先删掉本脚本上次写入的对白再追加）。
用法：python tools/import-b13-prologue.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
MAPDIR = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "narrative-map")

SPEAKER = {"system": "", "protagonist": "你", "baobao": "饱饱", "npc_cheng_wang": "程望"}

# S15 §2 建议五日节奏表“当前叙事问题”列；mainline 取 S15 §5 “更新主目标为……”
STAGES = [
    ("D1", "第1日", "先活下来，弄清声音从哪来"),
    ("D2", "第2日", "手里的补给会吃完"),
    ("D3", "第3日", "怎样活得省力一些"),
    ("D4", "第4日", "周围还剩什么人和地方"),
    ("D5", "第5日起", "也许有人能告诉我身体怎么了"),
    ("mainline", "主线接续", "前往幸存者营区寻找医疗帮助"),
]
STAGE_OF = {"D1": "D1", "D1以后": "D1", "D2": "D2", "D3": "D3", "D4": "D4", "D5以后": "D5", "主线接续": "mainline"}

# 对话记录里的事件名（开发方措辞，按策划 dialogueSetId 含义）
SET_TITLES = {
    "visitor_arrival_camp": "来客抵达",
    "visitor_late_or_legacy": "来客抵达",
    "visitor_route_first": "程望：营区路线",
    "visitor_after_route_baobao": "交谈之后",
    "visitor_after_route_without_baobao": "交谈之后",
    "visitor_route_repeat": "程望：再问路线",
    "visitor_not_ready": "程望：先准备",
    "PRO15_relief_note": "公开救助残页",
    "village_south_sign": "南口路标",
    "village_south_sign_route_unknown": "南口路标",
    "camp_route_log": "求医路线日志",
}
REPEATABLE = {"visitor_route_repeat", "visitor_not_ready", "PRO15_relief_note", "village_south_sign",
              "village_south_sign_route_unknown", "camp_route_log"}

JOURNEY = [("village", "m01_road"), ("m01_road", "m02_checkpoint"), ("m02_checkpoint", "h01_gate"), ("h01_gate", "h02_square")]


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def main():
    cfg = load(CONFIG)
    pro = load(os.path.join(MAPDIR, "prologue-data.json"))
    nt = load(os.path.join(MAPDIR, "narrative-texts.json"))
    sets = {d["dialogueSetId"]: d for d in nt["dialogueSets"]}
    assert pro["taskCount"] == 18 and len(pro["tasks"]) == 18

    def lines_of(set_id):
        return [{"lineId": l["lineId"], "speaker": SPEAKER[l["speaker"]], "text": l["text"]} for l in sets[set_id]["lines"]]

    def one_text(set_id):
        ls = sets[set_id]["lines"]
        assert len(ls) == 1 and ls[0]["speaker"] == "system", set_id
        return ls[0]["text"]

    tasks = []
    texts = {t["taskId"]: t for t in nt["taskTexts"]}
    for t in pro["tasks"]:
        tt = texts[t["id"]]
        tasks.append({
            "id": t["id"],
            "title": t["title"],
            "suggestedStage": t["suggestedStage"],
            "stage": STAGE_OF[t["suggestedStage"]],
            "completionRule": t["completionRule"],
            "guidance": t["guidance"],
            "exitGate": t["exitGate"].startswith("是"),
            "exitGateText": t["exitGate"],
            "hint": one_text(tt["initialHint"]),
            "reactionSet": tt["companionReaction"],
            "done": one_text(tt["completed"]),
        })

    edges = {}
    for e in cfg["edges"]:
        edges[(e["a"], e["b"])] = e
        edges[(e["b"], e["a"])] = e
    legs = []
    for a, b in JOURNEY:
        e = edges[(a, b)]
        legs.append({"from": a, "to": b, "edgeId": e["id"], "baseMinutes": e["baseMinutes"]})
    assert [l["baseMinutes"] for l in legs] == [30, 20, 15, 5], legs

    v = pro["visitor"]
    ex = pro["exit"]
    exit_edge = edges[(ex["from"], ex["to"])]
    assert exit_edge["gate"] == "QMAP01" and exit_edge["baseMinutes"] == 30

    cfg["prologue"] = {
        "source": "prologue-data.json + narrative-texts.json（Q32B 定稿）；S15 §2—§8、A10-TUTOR",
        "visitor": {
            "npcId": v["npcId"],
            "name": v["name"],
            "locationId": v["location"],
            "notBeforeWorldMinute": v["notBeforeWorldMinute"],
            "needsTutorialBattleWon": v["needsTutorialBattleWon"],
            "role": "幸存者营区的传讯跑腿",
        },
        "exit": {"edgeId": exit_edge["id"], "from": ex["from"], "to": ex["to"]},
        "documents": [
            {"id": "relief_page_village", "locationId": "village", "title": "公开救助残页", "dialogueSet": "PRO15_relief_note", "taskId": "PRO15"},
            {"id": "village_south_sign", "locationId": "village", "title": "南口路标", "dialogueSet": "village_south_sign",
             "unknownDialogueSet": "village_south_sign_route_unknown", "fact": "south_sign_known"},
        ],
        "talk": {
            "arrivalCamp": "visitor_arrival_camp",
            "arrivalLate": "visitor_late_or_legacy",
            "arrivalAwayText": one_text("visitor_arrival_away"),
            "routeFirst": "visitor_route_first",
            "afterRouteBaobao": "visitor_after_route_baobao",
            "afterRouteWithoutBaobao": "visitor_after_route_without_baobao",
            "routeRepeat": "visitor_route_repeat",
            "notReady": "visitor_not_ready",
            "routeLog": "camp_route_log",
        },
        "journey": {
            "legs": legs,
            "baseMinutesTotal": sum(l["baseMinutes"] for l in legs),
            "suggestedCleanWater": 2,
            "suggestedFood": 2,
            "suggestedWeapon": True,
        },
        "stages": [{"id": i, "label": lab, "headline": h} for i, lab, h in STAGES],
        "tasks": tasks,
        "dialogueSets": [{"id": k, "title": SET_TITLES.get(k, "序章"), "repeatable": k in REPEATABLE}
                         for k in list(SET_TITLES.keys()) + [t["reactionSet"] for t in tasks]],
    }
    for t in tasks:
        for d in cfg["prologue"]["dialogueSets"]:
            if d["id"] == t["reactionSet"]:
                d["title"] = t["title"]

    # story.dialogues：先去掉上次导入的（id 以 NTL_ 开头），再按 set 追加；eventId = dialogueSetId
    dl = [d for d in cfg["story"]["dialogues"] if not d["id"].startswith("NTL_")]
    for sid in SET_TITLES.keys():
        if sid == "visitor_arrival_away":
            continue
        for l in lines_of(sid):
            dl.append({"id": l["lineId"], "eventId": sid, "speaker": l["speaker"], "text": l["text"], "note": ""})
    for t in tasks:
        for l in lines_of(t["reactionSet"]):
            dl.append({"id": l["lineId"], "eventId": t["reactionSet"], "speaker": l["speaker"], "text": l["text"], "note": ""})
    cfg["story"]["dialogues"] = dl

    # B11 写的“后续版本开放”说明改为真实来源
    cfg["map"]["factLabels"]["camp_route_known"] = "在临时营地与来客程望交谈，得知营区路线"

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
    print(f"prologue：{len(tasks)} 项任务、{len(dl)} 条对白（含原 7 条）、路线 {cfg['prologue']['journey']['baseMinutesTotal']} 分钟")


if __name__ == "__main__":
    main()
