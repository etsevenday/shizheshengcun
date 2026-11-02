# -*- coding: utf-8 -*-
"""B13 新手序章的配置导入（开发计划 v0.9 批次 B13，R39—R43）。

数值与文字逐条取自策划数据，不自补：
- prologue-data.json：18 项 PRO 任务（标题、建议阶段、完成条件、教学表达、是否离开门槛）、来客程望（npc_cheng_wang、camp、
  notBeforeWorldMinute 6240、首战胜利、下一安全边界、常驻到交谈、不传送）、出区（village→m01_road，首战或 Legacy 等效 +
  camp_route_known + visited(village)，不要村庄 100、不要融合、不要全任务）。
- narrative-texts.json（Q32 选 B 定稿）：taskTexts（PROxx_hint 中性系统提示、PROxx_reaction 饱饱/主角短对白、PROxx_done 完成反馈）、
  contentBindings.prologueTalk 八组程望对白、documents 里的 PRO15 救助残页 / 南口路标两种 / 求医路线日志。speaker 映射到现有对白
  系统：system → 空（中性叙述），protagonist → “你”（左侧），baobao → “饱饱”，npc_cheng_wang → “程望”。
- S15 §6 PRO17 基础路线 village→m01(30)→m02(20)→h01(15)→h02(5) 合计 70（分钟从 edges 配置读并断言）、建议净水2份+可食2份+防身物。
- B13d（Q128 选 C、Q129 选 B，2026-11-02）：prologue-data.json 的 goal（玩家“下一步”短句）、stageTexts（6 阶段标题与重点）、
  specialObjectiveTexts（6 种特殊主目标）直接导入，不再用本脚本里的静态表覆盖；prologue-adjudications.json 的 objectivePolicy.adaptiveSteps
  （PRO06/PRO14/exit_need 按实际缺项的下一步）一并导入，并断言 guides 的标题/hint/nextStep/done 与 prologue-data、narrative-texts 逐字一致。
- B14a（Q139 选 B、Q140 选 B，2026-11-02）：objectivePolicy.travelNextStep（下一真实路段未知/已知可走/已知未开通/未知且不能探索四句）
  与 guidancePolicy.nonContextFallback.battleOnlyHintTaskIds（战外不作主目标回退提示的战斗类任务，PRO04）导入，断言与 prologue-data
  的 objectiveTravelNextStep、guideNonContextFallback 同源；Q138 的来源分类（legacyPolicy.originClassification = visitor.runOriginPolicy）
  只断言同源，引擎按可信来源标记（Legacy / 从 schema 21 及以前迁入的 MigratedAt）判定，不需要配置。

可重复运行，结果相同（先删掉本脚本上次写入的对白再追加）。
用法：python tools/import-b13-prologue.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
MAPDIR = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "narrative-map")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261102", "prologue", "prologue-adjudications.json")

SPEAKER = {"system": "", "protagonist": "你", "baobao": "饱饱", "npc_cheng_wang": "程望"}

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
    adj = load(ADJ)
    sets = {d["dialogueSetId"]: d for d in nt["dialogueSets"]}
    assert pro["taskCount"] == 18 and len(pro["tasks"]) == 18
    # Q128/Q129 机器合同与 prologue-data / narrative-texts 逐字同源
    guides = {g["taskId"]: g for g in adj["guides"]}
    assert len(guides) == 18
    assert pro["stageTexts"] == adj["stages"] and pro["specialObjectiveTexts"] == adj["specialObjectives"]
    # Q138—Q140（B14a）：合同与主数据同源
    travel = adj["objectivePolicy"]["travelNextStep"]
    fallback = adj["guidancePolicy"]["nonContextFallback"]
    assert travel == pro["objectiveTravelNextStep"] and fallback == pro["guideNonContextFallback"]
    assert adj["legacyPolicy"]["originClassification"] == pro["visitor"]["runOriginPolicy"]

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
        g = guides[t["id"]]
        assert (g["title"], g["nextStep"], g["hint"], g["done"]) == (t["title"], t["goal"], one_text(tt["initialHint"]), one_text(tt["completed"])), t["id"]
        assert [l["text"] for l in sets[tt["companionReaction"]]["lines"]] == [r["text"] for r in g["reaction"]], t["id"]
        tasks.append({
            "id": t["id"],
            "title": t["title"],
            "suggestedStage": t["suggestedStage"],
            "stage": STAGE_OF[t["suggestedStage"]],
            "completionRule": t["completionRule"],
            "goal": t["goal"],
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
        "source": "prologue-data.json + narrative-texts.json（Q32B 定稿）+ prologue-adjudications.json（Q128—Q133、Q138—Q140）；S15 §2—§8、A10-TUTOR",
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
        "stages": [{"id": x["id"], "headline": x["headline"], "focus": x["focus"]} for x in pro["stageTexts"]],
        "specialObjectives": [{"id": x["id"], "title": x["title"], "step": x["step"]} for x in pro["specialObjectiveTexts"]],
        "adaptiveSteps": [{"objective": k, "when": w["when"], "step": w["step"]}
                          for k, ws in adj["objectivePolicy"]["adaptiveSteps"].items() for w in ws],
        "travelStep": {
            "explore": travel["step"],
            "knownOpen": travel["knownAndOpenStep"],
            "knownBlocked": travel["knownButBlockedStep"],
            "unknownNoExplore": travel["unknownAndCannotExploreStep"],
        },
        "battleOnlyHintTaskIds": list(fallback["battleOnlyHintTaskIds"]),
        "tasks": tasks,
        "dialogueSets": [{"id": k, "title": SET_TITLES.get(k, "序章"), "repeatable": k in REPEATABLE}
                         for k in list(SET_TITLES.keys()) + [t["reactionSet"] for t in tasks]],
    }
    for t in tasks:
        for d in cfg["prologue"]["dialogueSets"]:
            if d["id"] == t["reactionSet"]:
                d["title"] = t["title"]

    # story.dialogues：先去掉本脚本上次导入的（id 以 NTL_ 开头且属本脚本的对白组；B14 城市对白同为 NTL_ 编号，不能一并删掉），
    # 再按 set 追加；eventId = dialogueSetId
    own = set(SET_TITLES.keys()) | {t["reactionSet"] for t in tasks}
    old_dl = cfg["story"]["dialogues"]
    mine = [i for i, d in enumerate(old_dl) if d["id"].startswith("NTL_") and d["eventId"] in own]
    at = mine[0] if mine else len(old_dl)   # 放回原位置（不打乱其他脚本导入的对白顺序）
    dl = []
    for sid in SET_TITLES.keys():
        if sid == "visitor_arrival_away":
            continue
        for l in lines_of(sid):
            dl.append({"id": l["lineId"], "eventId": sid, "speaker": l["speaker"], "text": l["text"], "note": ""})
    for t in tasks:
        for l in lines_of(t["reactionSet"]):
            dl.append({"id": l["lineId"], "eventId": t["reactionSet"], "speaker": l["speaker"], "text": l["text"], "note": ""})
    rest = [d for i, d in enumerate(old_dl) if i not in set(mine)]
    head = [d for i, d in enumerate(old_dl[:at]) if i not in set(mine)]
    cfg["story"]["dialogues"] = head + dl + rest[len(head):]

    # B11 写的“后续版本开放”说明改为真实来源
    cfg["map"]["factLabels"]["camp_route_known"] = "在临时营地与来客程望交谈，得知营区路线"

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"prologue：{len(tasks)} 项任务、{len(dl)} 条序章对白、路线 {cfg['prologue']['journey']['baseMinutesTotal']} 分钟")


if __name__ == "__main__":
    main()
