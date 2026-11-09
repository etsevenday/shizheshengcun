# -*- coding: utf-8 -*-
"""B14 势力事实、营区登记与城市岗位 NPC 框架的配置导入（开发计划 v0.9 批次 B14，R44、R45；并入 Q32/Q33 答复）。

数值与文字逐条取自策划数据，不自补：
- city-npc-data.json：16 名岗位 NPC（npcId/displayName/role/factionId/locationId/questIds/serviceIds/recruitmentType/canRecruit/dialogueSet）。
- city-services.json（Q33 选 B）：19 项服务（地点、kind、worldMinutes、inputs、facts、choices、dailyPartyQuota、mode）与 npcBindings。
- narrative-texts.json（Q32 选 B 定稿）：16 组岗位对白（九种状态各一组）、serviceOfferLines（服务面板里的岗位短句）、
  gate_h01_registration（营门登记）、academy_public_observation_notice（r07 观察区公开通道告示，两个选项同写 academy_observation_pass）。
  speaker：system → 空（中性叙述）、protagonist → “你”、npc_xxx → 人物名、*_duty_staff → “机构当班接续员”。
- N01 §5 / A11 §5：六名固定队友改标营区工作职能（affiliation，只加标签，不洗背景与数值）。
- A07「expedition_basics：h03_workshop，营区登记后免费查看制作告示0分钟」：加一条知识（授予事实 h03_notice_read）。

本批实装的服务（不依赖后续批次）在 implemented 里标 true；其余只注册（B14b 起按 Q146 选 B 从“事项”里隐藏，后续实装同 ID 自然出现）。

B14b（Q141—Q146 答复，assets/20261102/city/city-adjudications.json）：
- 19 项服务的正式中文名/玩家摘要取 city-services.json displayName/playerSummary（Q145 选 B，与 city-adjudications serviceNames 断言一致）；
  知识 expedition_basics 名、两份告示名取 knowledgeNames/notices。
- 六名固定队友正式背景句取 companionBackgrounds（Q143 选 B），职能与 affiliation 断言一致。
- 公共工坊站取 publicStations.stations（Q141 选 B），并断言与 workshop-adjudications.json publicStations 同源；
  资格 legacy_QMAP02_completed 映射到 B13b 已用的事实键 trusted_legacy_QMAP02_completed。

可重复运行，结果相同（先删掉本脚本上次写入的对白与知识再追加）。
用法：python tools/import-b14-city.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
MAPDIR = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "narrative-map")
ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261102", "city", "city-adjudications.json")
WORKSHOP_ADJ = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261101", "workshop", "workshop-adjudications.json")

FACTIONS = {"hub_survivor": "幸存者营区", "faction_church": "新生教会", "faction_academy": "灰塔科学院"}
DUTY = {"hub_survivor": "hub_survivor_duty_staff", "faction_church": "faction_church_duty_staff", "faction_academy": "faction_academy_duty_staff"}

# 本批是否实装（B14b：Q146 选 B，未实装的从“事项”里隐藏，不写占位）。显示名与玩家摘要取 city-services.json displayName/playerSummary（Q145 选 B）
IMPLEMENTED = {
    "hub_registration_existing": True,
    "hub_affairs_record": False,
    "hub_workshop_access": True,
    "copy_public_knowledge": False,
    "clinic_consult": True,
    "clinic_dressing": True,
    "clinic_observation_rest": True,
    "appraisal_white_v1": False,
    "shelter_recruitment": True,
    "church_reception_existing": False,
    "church_relief_ration": True,
    "church_public_mediation": False,
    "church_archive_copy": False,
    "church_quiet_rest": True,
    "academy_reception_existing": False,
    "academy_noninvasive_observation": False,
    "academy_report_compare": False,
    "academy_workshop_access": True,
    "academy_observation_record": True,
}
# Q141 公共站的资格别名：legacy_QMAP02_completed = 可信旧 QMAP02 完成事实，与 B13b 工坊首次支援包同一事实键
QUAL_ALIAS = {"legacy_QMAP02_completed": "trusted_legacy_QMAP02_completed"}
CHOICE_LABELS = {"relief_food": "烤熟块根1份（饱食低于40）", "relief_water": "净水1份（水分低于40）"}

STATE_TITLES = {
    "first_greeting": "初次见面", "role": "岗位说明", "quest_offer": "委托", "quest_return": "委托回报", "idle": "闲谈",
    "locked": "暂不可办", "service_end": "办完事项", "institution_substitute": "岗位接续", "service_offer": "办理事项",
}

AFFILIATION = {  # N01 §5 / A11 §5（营区工作职能；不改名字、速度、武器、技能）
    "npc_lin_zhao": "营区巡路", "npc_zhou_yan": "营区工务维修人员", "npc_shen_he": "营区医疗转运人员",
    "npc_tang_li": "自由搜集者", "npc_gu_cheng": "营区外勤协作人员", "npc_ye_xun": "独立通信记录者",
}


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def main():
    cfg = load(CONFIG)
    npcd = load(os.path.join(MAPDIR, "city-npc-data.json"))
    svcd = load(os.path.join(MAPDIR, "city-services.json"))
    nt = load(os.path.join(MAPDIR, "narrative-texts.json"))
    adj = load(ADJ)
    wadj = load(WORKSHOP_ADJ)
    sets = {d["dialogueSetId"]: d for d in nt["dialogueSets"]}
    locs = {l["id"]: l for l in cfg["locations"]}
    items = {i["id"] for i in cfg["items"]}

    assert len(npcd["npcs"]) == 16 and len(svcd["services"]) == 19
    assert set(IMPLEMENTED) == {s["id"] for s in svcd["services"]}
    # Q145：city-services.json 的 displayName/playerSummary 与 city-adjudications serviceNames 同源
    names145 = {x["serviceId"]: x for x in adj["serviceNames"]}
    assert set(names145) == set(IMPLEMENTED)
    for s in svcd["services"]:
        assert s["displayName"] == names145[s["id"]]["displayName"] and s["playerSummary"] == names145[s["id"]]["playerSummary"], s["id"]
    # Q146：B14 未实装快照与本表一致（不是永久隐藏表；后续实装改 IMPLEMENTED 即自然显示）
    assert set(adj["displayPolicy"]["b14UnimplementedSnapshot"]) == {k for k, v in IMPLEMENTED.items() if not v}
    speaker = {"system": "", "protagonist": "你", "baobao": "饱饱"}
    for n in npcd["npcs"]:
        speaker[n["npcId"]] = n["displayName"]
    for d in DUTY.values():
        speaker[d] = "机构当班接续员"
    names = {r["id"]: r["name"] for r in nt["speakerRegistry"]}
    for n in npcd["npcs"]:
        assert names[n["npcId"]] == n["displayName"], n["npcId"]

    npc_sets = {x["npcId"]: x for x in nt["npcDialogueSets"]}
    npcs = []
    dialogue_sets = []
    for n in npcd["npcs"]:
        assert n["locationId"] in locs, n
        assert n["recruitmentType"] == "service_only" and not n["canRecruit"]
        assert svcd["npcBindings"][n["npcId"]] == n["serviceIds"], n["npcId"]
        ns = npc_sets[n["npcId"]]
        assert ns["dialogueSetId"] == n["dialogueSet"] and ns["locationId"] == n["locationId"]
        offer = sets[ns["states"]["service_offer"]]
        lines = {o["serviceId"]: o["text"] for o in offer.get("serviceOfferLines", [])}
        npcs.append({
            "npcId": n["npcId"], "name": n["displayName"], "role": n["role"], "factionId": n["factionId"],
            "locationId": n["locationId"], "questIds": n["questIds"], "serviceIds": n["serviceIds"],
            "recruitmentType": n["recruitmentType"], "canRecruit": n["canRecruit"], "dialogueSet": n["dialogueSet"],
            "states": ns["states"], "serviceOfferLines": lines, "dutySpeaker": "机构当班接续员",
        })
        for st, sid in ns["states"].items():
            dialogue_sets.append({"id": sid, "title": f"{n['displayName']}：{STATE_TITLES[st]}"})

    services = []
    for s in svcd["services"]:
        impl = IMPLEMENTED[s["id"]]
        assert s["locationId"] in locs, s["id"]
        for k in s.get("inputs", {}):
            assert k in items, k
        choices = []
        for c in s.get("choices", []):
            for k in c["outputs"]:
                assert k in items, k
            pred = c["targetPredicate"]
            stat, op, val = (pred.split("<")[0], "<", float(pred.split("<")[1]))
            choices.append({"id": c["id"], "label": CHOICE_LABELS[c["id"]], "stat": stat, "below": val, "outputs": c["outputs"]})
        services.append({
            "id": s["id"], "title": s["displayName"], "summary": s["playerSummary"], "locationId": s["locationId"], "kind": s["kind"],
            "worldMinutes": s.get("worldMinutes"), "inputs": s.get("inputs", {}), "effect": s["effect"],
            "questId": s.get("questId"), "facts": s.get("facts", []), "choices": choices,
            "dailyPartyQuota": s.get("dailyPartyQuota"), "mode": s.get("mode"), "target": s.get("target"),
            "actorSkillLevel": s.get("actorSkillLevel"), "implemented": impl,
        })

    reg = sets["gate_h01_registration"]
    assert reg["flagsOnFirstSubmit"] == ["registration_authorized"]
    notice = sets["academy_public_observation_notice"]
    assert all(c["flags"] == ["academy_observation_pass"] for c in notice["choices"])
    notice_names = {x["locationId"]: x["displayName"] for x in adj["notices"]}
    know_names = {x["id"]: x["displayName"] for x in adj["knowledgeNames"]}

    # Q141：公共工坊站（与 workshop-adjudications.json publicStations 同源）
    ps = adj["publicStations"]
    assert ps["stations"] == wadj["publicStations"]["stations"]
    station_ids = {x["id"] for x in cfg["workshops"]["stations"]}
    public_stations = []
    for x in ps["stations"]:
        assert x["locationId"] in locs and x["stationId"] in station_ids and x["requireActualPresence"], x["id"]
        fee = x["accessFee"]
        assert fee["currency"] == 0 and fee["items"] == {} and fee["worldMinutes"] == 0 and fee["xp"] == 0, x["id"]
        public_stations.append({
            "id": x["id"], "locationId": x["locationId"], "stationId": x["stationId"], "name": x["displayName"],
            "allFacts": x["allFacts"], "anyFacts": [QUAL_ALIAS.get(q, q) for q in (x["anyQualification"] or [])],
        })

    cfg["city"] = {
        "source": "city-npc-data.json + city-services.json（Q33 选 B）+ narrative-texts.json（Q32 选 B）；A11「城市建筑、岗位NPC与八支线」§1—§6、N01 §3/§6、S02 门槛规则 3/5/6",
        "factions": FACTIONS,
        "registration": {"serviceId": "hub_registration_existing", "gateLocation": "h01_gate", "squareLocation": "h02_square",
                         "dialogueSet": "gate_h01_registration", "authorizedFact": "registration_authorized", "registeredFact": "hub_registered",
                         "questId": "QMAP01"},
        "notices": [
            {"id": "h03_craft_notice", "locationId": "h03_workshop", "title": notice_names["h03_workshop"], "requiresFact": "hub_registered",
             "grantsFact": "h03_notice_read", "knowledgeId": "expedition_basics", "dialogueSet": None, "choices": []},
            {"id": "r07_observation_notice", "locationId": "r07_workshop", "title": notice_names["r07_workshop"], "requiresFact": None,
             "grantsFact": "academy_observation_pass", "knowledgeId": None, "dialogueSet": "academy_public_observation_notice",
             "choices": [{"id": c["choiceId"], "label": c["label"]} for c in notice["choices"]]},
        ],
        "npcs": npcs,
        "services": services,
        "publicStations": public_stations,
        "dialogueSets": dialogue_sets + [
            {"id": "gate_h01_registration", "title": names145["hub_registration_existing"]["displayName"]},
            {"id": "academy_public_observation_notice", "title": notice_names["r07_workshop"]},
        ],
    }

    # story.dialogues：先去掉上次导入的城市对白，再追加（eventId = dialogueSetId，lineId 原样）
    city_sets = [d["id"] for d in cfg["city"]["dialogueSets"]]
    city_line_ids = {l["lineId"] for sid in city_sets for l in sets[sid]["lines"]}
    dl = [d for d in cfg["story"]["dialogues"] if d["id"] not in city_line_ids]
    for sid in city_sets:
        for l in sets[sid]["lines"]:
            dl.append({"id": l["lineId"], "eventId": sid, "speaker": speaker[l["speaker"]], "text": l["text"], "note": ""})
    cfg["story"]["dialogues"] = dl

    # 知识：expedition_basics（A07：营区登记后 h03 免费查看制作告示 0 分钟；中文名取 Q145 knowledgeNames）
    cfg["knowledge"] = [k for k in cfg["knowledge"] if k["id"] != "expedition_basics"]
    cfg["knowledge"].append({"id": "expedition_basics", "name": know_names["expedition_basics"], "grantedByFacts": ["h03_notice_read"],
                             "source": "营区登记后在营区工坊查看制作告示（0分钟）"})

    # 六名固定队友改标营区工作职能（只加标签）；B14b（Q143 选 B）背景句换成正式句（名字、数值、技能、装备不动）
    bg = {x["actorId"]: x for x in adj["companionBackgrounds"]}
    assert set(bg) == {c["actorId"] for c in cfg["party"]["namedCompanions"]}
    for c in cfg["party"]["namedCompanions"]:
        c["affiliation"] = AFFILIATION[c["actorId"]]
        b = bg[c["actorId"]]
        assert b["name"] == c["name"] and b["affiliation"] == c["affiliation"], c["actorId"]
        c["background"] = b["background"]

    # B11 写的“后续版本开放”说明改为真实来源
    fl = cfg["map"]["factLabels"]
    fl["registration_authorized"] = "在营门向值守登记员办理登记"
    fl["hub_registered"] = "完成营区登记（在营门办理登记后走到营区广场）"
    fl["academy_observation_pass"] = "在试制工坊查看观察区公开通道告示并确认通行范围"

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"city：{len(npcs)} 名岗位、{len(services)} 项服务（实装 {sum(1 for s in services if s['implemented'])}）、{len(city_line_ids)} 句对白、{len(public_stations)} 个公共站")


if __name__ == "__main__":
    main()
