# -*- coding: utf-8 -*-
"""B23 可选地牢知识支线 EXQ05—08 的配置导入（开发计划 v0.9 批次 B23，需求 R71）→ data/config.v0.6.json。

数值与名字逐条取自策划案与同源数据，不自补：
- A11「可选地牢任务与知识」§2 四条可选任务表（接取岗位、记录点、wait10、返营交谈 0 分钟 0 材料、奖励文字）；
- A11 §3 蓝图包与确定替代来源表（四个知识包、七条知识、h03/h02/h04 替代抄录 wait30、0 材料）；
- A11 §4（claimId=dXX_YY_unique，campaign_once，不另造第二箱）；
- assets/20261030/narrative-map/expansion-quests.json（机器可读任务接口）。

写入：
- quests.list 追加/覆盖 EXQ05—EXQ08（kind=optional，talk 接取，recordFacts=claim:<claimId>，交谈 0 分钟回报）；
  接取/回报台词用三名岗位已有的通用 quest_offer/quest_return 组（开发假设 B23-1），不新写台词；
  “下一步”短句只用地图里的地点名、记录名拼出（父地点 → 地牢 → 记录房 → 记录名）。
- expansionQuests：四个知识包（knowledge、claimResourceId、questId、替代抄录地点/岗位/服务/分钟/来源名）。
- knowledge：七条知识的 grantedByFacts 加 claim:<claimId> 与替代抄录事实 kpcopy:<包>，source 补上新来源。
- city.services copy_public_knowledge（h03 罗铮名下已有、原隐藏）设为已实装；h02/h04 的抄录用合成服务（不进 19 项服务表）。

同时断言：入口/记录/出口节点存在且与地图 dungeons 一致、d02/d04 任务点不是出口（EXQ-A01）；记录资源是 campaign 记录、10 分钟、
不写容量事实（EXQ-A02）；七条知识的配方存在、门槛与 A11 §3 一致（EXQ-A03）；岗位存在。

可重复运行，结果相同。
用法：python tools/import-b23-exq.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
DOC = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0")
EXQ = os.path.join(DOC, "assets", "20261030", "narrative-map", "expansion-quests.json")

# A11 §2 接取岗位（board）对应的岗位人物（city-npc-data：h03 罗铮、h02 许安、h04 陈芷）
BOARD_NPC = {"h03_workshop": "npc_luo_zheng", "h02_square": "npc_xu_an", "h04_infirmary": "npc_chen_zhi"}

# A11 §2“奖励/限制”列与 §3“确定替代来源”列的原文短语（玩家可见）
PACK_TEXT = {
    "kp_waterworks": {"label": "净水/维护知识包", "copyLabel": "公开资料",
                      "result": "已核对水路日志，得到净水/维护知识包（水本身仍按原净化规则）"},
    "kp_mining": {"label": "野外工作衣物知识包", "copyLabel": "公开作业资料",
                  "result": "得到野外工作衣物知识包（矿料来自资源表，不再任务附发一包）"},
    "kp_civilian_cache": {"label": "衣物/医疗工具知识与疏散日志", "copyLabel": "公共告示资料",
                          "result": "得到衣物/医疗工具知识与疏散日志（不把记录包装成当前存活者名单）"},
    "kp_coldchain": {"label": "配给知识与封存记录", "copyLabel": "物资登记资料",
                     "result": "得到配给知识与封存记录（不赋予药品无限保质）"},
}

# A11 §3：制造等级/工坊（“净水操作套件制3/工坊，维护手册制7/工坊；采集手套制1/随身，作业长裤制3/缝制；采集外套制3/缝制，随行医疗包制1/随身；配给袋制10/缝制”）
RECIPE_GATES = {
    "blueprint_filter_token": (3, "workbench"), "blueprint_repair_manual": (7, "workbench"),
    "blueprint_gather_gloves": (1, "hand"), "blueprint_utility_trousers": (3, "sewing"),
    "blueprint_forage_jacket": (3, "sewing"), "blueprint_field_medkit": (1, "hand"),
    "blueprint_ration_pouch": (10, "sewing"),
}

COPY_SERVICE = {"h03_workshop": "copy_public_knowledge", "h02_square": "copy_public_knowledge_h02", "h04_infirmary": "copy_public_knowledge_h04"}


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(EXQ, encoding="utf-8") as f:
        src = json.load(f)

    locs = {l["id"]: l for l in cfg["locations"]}
    res = {r["nodeId"]: r for r in cfg["resources"]}
    dungeons = {d["id"]: d for d in cfg["map"]["dungeons"]}
    know = {k["id"]: k for k in cfg["knowledge"]}
    npcs = {n["npcId"]: n for n in cfg["city"]["npcs"]}
    recipes = {r.get("knowledge"): r for r in cfg["recipes"] if r.get("knowledge")}
    events = {d["eventId"] for d in cfg["story"]["dialogues"]}
    cap_records = set()
    for u in cfg["scope"]["capacity"]["unlocks"]:
        cap_records.update(u.get("allOf", []))
        cap_records.update(u.get("anyOf", []))

    assert src["recordRules"]["physicalCard"] is False and src["recordRules"]["generationScoped"] is False
    assert src["knowledgeRules"]["setUnion"] and not src["knowledgeRules"]["duplicateConversionReward"]
    assert not src["knowledgeRules"]["alternativeRequiresDungeonRecord"] and not src["knowledgeRules"]["alternativeRequiresMembership"]

    # ── EXQ-A01：主线记录点不是出口 ──
    assert dungeons["d02"]["recordNode"] == "d02_08" and dungeons["d02"]["exitNode"] == "d02_09"
    assert dungeons["d04"]["recordNode"] == "d04_07" and dungeons["d04"]["exitNode"] == "d04_08"

    quests = []
    packs = []
    for q in src["optionalQuests"]:
        d = dungeons[q["dungeonId"]]
        assert d["parent"] == q["entrance"] and d["recordNode"] == q["recordNode"] and d["exitNode"] == q["exitNode"], q["id"]
        for n in (q["entrance"], q["recordNode"], q["exitNode"], q["board"], q["alternativeKnowledgeNode"]):
            assert n in locs, n
        assert q["recordNode"] != q["exitNode"], q["id"]
        r = res[q["claimId"]]
        # EXQ-A02：记录是 campaign 核心记录、10 分钟、0 额外材料，不写主线容量事实
        assert r["locationId"] == q["recordNode"] and r["poolMode"] == "campaign_claim" and r["recordId"] == q["record"], q["id"]
        assert r["baseMinutes"] == q["workMinutes"] == 10 and q["extraMaterialCost"] == {} and q["capacityDelta"] == 0, q["id"]
        assert not r.get("claimFacts") and q["record"] not in cap_records, q["id"]
        assert q["rewardScope"] == "campaign_once" and q["requires"] == ["hub_registered"], q["id"]
        assert q["alternativeKnowledgeNode"] == q["board"] and q["alternativeWorkMinutes"] == 30, q["id"]
        npc = BOARD_NPC[q["board"]]
        assert q["id"] in npcs[npc]["questIds"], (npc, q["id"])
        offer = npcs[npc]["states"]["quest_offer"]
        back = npcs[npc]["states"]["quest_return"]
        assert offer in events and back in events, npc
        claim_fact = "claim:" + q["claimId"]
        board_name = locs[q["board"]]["name"]
        npc_name = npcs[npc]["name"]
        step_active = f"从{locs[q['entrance']]['name']}进入{d['name']}，在{locs[q['recordNode']]['name']}领取{r['name']}（10分钟）。"
        quests.append({
            "id": q["id"], "title": q["title"], "kind": "optional", "npcId": npc, "locationId": q["board"], "implemented": True,
            "showAny": [], "requiresAll": list(q["requires"]), "requiresAny": [], "requiresExplore": {}, "requiresRosterMember": False,
            "accept": "talk", "acceptServiceId": None, "offerDialogue": offer, "workDialogue": None, "turninDialogue": back,
            "repeatDialogue": None, "completeDialogue": None, "hubTurninDialogue": None, "noteDialogue": None,
            "workMinutes": 0, "workServiceIds": [], "workRequiresAll": [], "workLabel": "交谈",
            "completeByAny": [], "completeRequiresAccepted": False, "completeFacts": [],
            "recordFacts": [claim_fact], "dungeonId": q["dungeonId"],
            "stepOffer": f"到{board_name}和{npc_name}交谈。", "stepLocked": "",
            "stepActive": step_active,
            "stepTurnin": f"回{board_name}和{npc_name}交谈（0分钟，不交材料）。",
            "result": PACK_TEXT[q["knowledgePack"]]["result"],
        })
        packs.append({
            "id": q["knowledgePack"], "label": PACK_TEXT[q["knowledgePack"]]["label"], "questId": q["id"],
            "knowledge": list(q["knowledge"]), "claimResourceId": q["claimId"], "dungeonId": q["dungeonId"],
            "copyLocationId": q["alternativeKnowledgeNode"], "copyNpcId": npc, "copyServiceId": COPY_SERVICE[q["alternativeKnowledgeNode"]],
            "copyMinutes": q["alternativeWorkMinutes"], "copyLabel": PACK_TEXT[q["knowledgePack"]]["copyLabel"],
            "copyFact": "kpcopy:" + q["knowledgePack"],
        })

    # ── EXQ-A03：七条知识存在、配方存在、门槛与 A11 §3 一致 ──
    all_know = [k for p in packs for k in p["knowledge"]]
    assert len(all_know) == 7 and set(all_know) == set(RECIPE_GATES), all_know
    for kid, (lv, station) in RECIPE_GATES.items():
        rc = recipes[kid]
        assert rc["minSkills"].get("crafting") == lv and rc["station"] == station, (kid, rc["minSkills"], rc["station"])
        for out in rc["outputs"]:
            assert any(i["id"] == out for i in cfg["items"]), out

    # ── 知识：授予事实与来源文字 ──
    for p in packs:
        r = res[p["claimResourceId"]]
        room = locs[r["locationId"]]
        dname = dungeons[p["dungeonId"]]["name"]
        claim_src = f"{dname}·{room['name']}（{r['locationId']}）领取“{r['name']}”"
        copy_src = f"营区登记后在{locs[p['copyLocationId']]['name']}“查抄公开资料”抄{p['copyLabel']}（30分钟，0材料）"
        for kid in p["knowledge"]:
            k = know[kid]
            base = k["source"].split("；或")[0]
            facts = [f for f in k["grantedByFacts"] if not f.startswith("kpcopy:") and f != "claim:" + p["claimResourceId"]]
            k["grantedByFacts"] = facts + ["claim:" + p["claimResourceId"], p["copyFact"]]
            k["source"] = f"{base}；或{claim_src}；或{copy_src}"

    # ── 任务：追加/覆盖 EXQ ──
    ids = {q["id"] for q in quests}
    cfg["quests"]["list"] = [q for q in cfg["quests"]["list"] if q["id"] not in ids] + quests

    # ── 替代抄录：h03 原服务开放；h02/h04 用合成服务（不进服务表） ──
    for sv in cfg["city"]["services"]:
        if sv["id"] == "copy_public_knowledge":
            assert sv["locationId"] == "h03_workshop" and sv["worldMinutes"] == 30 and sv["inputs"] == {}
            sv["implemented"] = False   # 公开抄录随 M479 开放

    cfg["expansionQuests"] = {
        "source": "A11「可选地牢任务与知识」§2—§4、expansion-quests.json（2026-10-30-review-1）；开发计划 v0.9 B23（R71）",
        "packs": packs,
    }

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("EXQ05—08、四个知识包、七条知识来源已写入")


if __name__ == "__main__":
    main()
