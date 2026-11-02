# -*- coding: utf-8 -*-
"""B15 主线与支线任务的配置导入（开发计划 v0.9 批次 B15，需求 R46、R47）。须在 tools/import-b14-city.py 之后运行。

规则与文字逐条取自策划案，不自补数值：
- A11「4 主线任务骨架修订与容量」FA01—FA05（前置、完成方式、FA04 固定 wait10 两段）；A08 门槛规则 6/7；S15「gate事实接口」。
- A11「5 支线委托最小表」HU01—HU04、CH01—CH02、AC01—AC02（对话 0 分钟；抄录/核对 wait10；奖励只有日志或信息）。
- A11「7 招募与补充任务表」RN01—RN06；B15a（策划答复 Q152 选 A）：RN02—RN06 前置改为 FA 对应（取代旧 QMAP02—04）；
  Q156 选 B：六人逐字邀约对白取 narrative-texts recruitInvitations（交谈打开对白，读完选“登记同行”/“暂不登记”）。
- narrative-texts.json（Q32 选 B 定稿）：8 支线接取/工作/交付/回看对白、CH02_hub_backup_turnin、AC02_external_medical_note、
  FA02_route_choice、FA03_environment_choice、FA01_public_brief、FA04_merge_accept/complete、FA05_power_contact/contact_complete。
  speaker：system → 空（中性叙述）、protagonist → “你”、baobao → “饱饱”（只在饱饱在体内时播放）、npc_xxx → 人物名。
- city-services.json：任务类服务（hub_affairs_record、church_reception_existing、church_public_mediation、church_archive_copy、
  academy_reception_existing、academy_noninvasive_observation、academy_report_compare）的选项取同一任务对白的 choices。
- R03 §3：战术训练阶位 1（C3 后 h03，parts2+cloth2，60 分钟）、阶位 2（h03，precision_parts2+steel2，90 分钟），每角色各一次；
  B15a（Q153 选 A）：阶位 2 的旧 QMAP04 前置改为“已领警备仓备用电源或 FA04 完成”。
- B15a（Q155 选 A）：登记后右栏主目标切到当前主线；暂无可显示/全部完成两句写进 prologue.specialObjectives（main_quest_none / main_quest_done）。
- B15a（Q157 选 A）：FA05 须先接取，联络日志已在手的接取后安全边界完成（completeRequiresAccepted）。

玩家看到的“下一步”短句是开发方按上述表格整理的措辞（策划未给逐字文案，开发假设 B15-6）。
可重复运行，结果相同（先删掉本脚本上次写入的对白与对白组名再追加）。
用法：python tools/import-b15-quests.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
MAPDIR = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261030", "narrative-map")

# 本批实装的任务（B15a：RN02—RN06 按 Q152 答复开放）
IMPLEMENTED = {
    "FA01": True, "FA02": True, "FA03": True, "FA04": True, "FA05": True,
    "HU01": True, "HU02": True, "HU03": True, "HU04": True, "CH01": True, "CH02": True, "AC01": True, "AC02": True,
    "RN01": True, "RN02": True, "RN03": True, "RN04": True, "RN05": True, "RN06": True,
}
# 本批开放的任务类城市服务（Q146：实装后同 ID 自然出现）
SERVICES_ON = ["hub_affairs_record", "church_reception_existing", "church_public_mediation", "church_archive_copy",
               "academy_reception_existing", "academy_noninvasive_observation", "academy_report_compare"]
# h03 战术训练入口（R03 §3）
TRAINING = True


def qf(q, kind):
    return f"quest:{q}:{kind}"


# A11 §4 / §5 / §7。accept：auto（到达即触发）/ talk（与岗位交谈接取，播 offer）/ service（办理该服务接取）/ work（开始工作即接取）/ roster（选择登记）
QUESTS = [
    # ───────── 主线（A11 §4）─────────
    dict(id="FA01", title="营区之外", kind="main", npcId="npc_xu_an", locationId="h02_square",
         showAny=["hub_registered"], requiresAll=["hub_registered"], accept="auto",
         offerDialogue="FA01_public_brief",
         stepOffer="在营区广场听许安说外面的路。",
         stepActive="在营区广场听许安说外面的路。",
         result="两方接触线索已记入日志：外面的路分向新生教会与灰塔科学院，可以都去，也可以先挑一边，不选效忠对象（不发第二份登记物资）"),
    dict(id="FA02", title="被称作新生的人", kind="main", npcId="npc_qi_wen", locationId="c02_church",
         showAny=[qf("FA01", "completed"), "church_contacted"], requiresAll=[], accept="service", acceptServiceId="church_reception_existing",
         offerDialogue="FA02_route_choice", completeByAny=["FA02_record_credit"],
         stepOffer="到教会接待庭向祁闻询问公开路线（可拒绝宣誓）。",
         stepActive="从教堂墓园进入教会地下圣所，在核心领取圣所旧路档册。",
         result="已取得圣所旧路档册（首次关键记录）"),
    dict(id="FA03", title="样本之外", kind="main", npcId="npc_wen_cheng", locationId="r04_reception",
         showAny=[qf("FA01", "completed"), "academy_contacted"], requiresAll=[], accept="service", acceptServiceId="academy_reception_existing",
         offerDialogue="FA03_environment_choice", completeByAny=["FA03_record_credit"],
         stepOffer="到科学院接待处向温澄询问环境调查路线（可拒绝身体检测）。",
         stepActive="从封存实验楼进入灰塔封存区，在核心领取封存区安全规程。",
         result="已取得封存区安全规程（首次关键记录）"),
    dict(id="FA04", title="同一条归路", kind="main", npcId="npc_xu_an", locationId="h02_square",
         showAny=[qf("FA02", "completed"), qf("FA03", "completed")], requiresAll=["FA02_record_credit", "FA03_record_credit"], accept="work",
         offerDialogue="FA04_merge_accept", completeDialogue="FA04_merge_complete",
         workMinutes=10, workServiceIds=["hub_affairs_record"], completeFacts=["FA04_merged"],
         stepOffer="两份调查记录到齐后，到营区广场请许安整理调查记录（10分钟）。",
         stepActive="在营区广场整理调查记录（10分钟，两段都完成才算完成；中途停下保留已完成的段）。",
         result="调查记录整理完成，旧城后段（警备仓）通行已按这份记录办理"),
    dict(id="FA05", title="联络仍在继续", kind="main", npcId="npc_luo_zheng", locationId="h03_workshop",
         showAny=[qf("FA04", "completed"), "communication_power_claimed"], requiresAll=["communication_power_claimed"], accept="talk",
         offerDialogue="FA05_power_contact", completeDialogue="FA05_contact_complete", completeByAny=["chapter_contact"], completeRequiresAccepted=True,
         # B15a 试玩修复 M339：未领电源（未满足条件）与已领电源（可接）分两句，开发方措辞（策划未给逐字下一步，B15-6）
         stepLocked="从警备仓外进入旧城警备仓，在封存仓领取警备仓备用电源，再到营区工坊和罗铮交谈。",
         stepOffer="到营区工坊和罗铮交谈，接取联络任务。",
         stepActive="从山顶瞭望台进入深野通信站，在联络台完成联络。",
         result="联络记录已保存，本章通信目标完成（不提供根治药、永久免疫或新的扩容）"),
    # ───────── 支线（A11 §5）─────────
    dict(id="HU01", title="鉴定之前", kind="side", npcId="npc_tao_cen", locationId="h05_appraisal",
         showAny=["hub_registered"], requiresAll=["hub_registered"], accept="talk", workMinutes=0,
         stepOffer="到鉴定所和陶岑交谈。", stepActive="在鉴定所阅读鉴定说明（0分钟，不用交出样本）。",
         workLabel="阅读说明", result="鉴定说明已读（不发物、不交样本）"),
    dict(id="HU02", title="样本去向", kind="side", npcId="npc_tao_cen", locationId="h05_appraisal",
         showAny=[qf("HU01", "completed")], requiresAll=[qf("HU01", "completed")], accept="talk", workMinutes=0,
         stepOffer="再到鉴定所和陶岑交谈。", stepActive="在鉴定所了解样本保留、融合与交易的范围（0分钟，不要求交易一次）。",
         workLabel="了解范围", result="交易说明已读（不奖励可回卖物）"),
    dict(id="HU03", title="同行不是归属", kind="side", npcId="npc_du_sui", locationId="h06_shelter",
         showAny=["hub_registered"], requiresAll=["hub_registered"], accept="talk", workMinutes=0,
         stepOffer="到收容所和杜穗交谈。", stepActive="在收容所阅读固定同行者、周期候选与同行补员的区别（0分钟）。",
         workLabel="阅读说明", result="招募说明已读（不招人、不刷新名单、不扩大队伍）"),
    dict(id="HU04", title="留守之前", kind="side", npcId="npc_du_sui", locationId="h06_shelter",
         showAny=["hub_registered"], requiresAll=["hub_registered"], requiresRosterMember=True, accept="talk", workMinutes=0,
         stepOffer="到收容所和杜穗交谈。", stepActive="在收容所查看候补照护与伤病风险说明（0分钟，可以不让任何人留守）。",
         workLabel="查看说明", result="留守说明已读（不改队伍、不清伤病）"),
    dict(id="CH01", title="施济清单", kind="side", npcId="npc_bai_ling", locationId="c04_alms",
         showAny=[qf("FA02", "accepted")], requiresAll=[qf("FA02", "accepted")], accept="talk",
         workMinutes=10, workRequiresAll=["FA02_record_credit"],
         stepOffer="到施济处和白苓交谈。", stepActive="取得圣所旧路档册后，在施济处核对物资账（10分钟）。",
         workLabel="核对物资账", result="公开账核对记录已保存（不领取另一份地牢箱）"),
    dict(id="CH02", title="被扣下的记录", kind="side", npcId="npc_ji_mu", locationId="c05_admonition",
         showAny=[qf("CH01", "completed")], requiresAll=[qf("CH01", "completed")], accept="talk",
         workMinutes=10, workServiceIds=["church_public_mediation", "church_archive_copy"], hubTurninDialogue="CH02_hub_backup_turnin",
         stepOffer="到训诫堂和纪牧交谈。", stepActive="在训诫堂交接公开资料（选抄录或回营区备份），或到典藏室抄录路线备份（10分钟）。",
         result="路线备份日志已保存（不宣誓、不交出任何人）"),
    dict(id="AC01", title="观察的边界", kind="side", npcId="npc_ji_wen", locationId="r05_testing",
         showAny=[qf("FA03", "accepted")], requiresAll=[qf("FA03", "accepted")], accept="talk",
         workMinutes=10, workServiceIds=["academy_noninvasive_observation"],
         stepOffer="到检测室和季闻交谈。", stepActive="在检测室核对观察范围，选只核对环境记录或只读现有状态（10分钟，不改身体数值）。",
         result="同意范围日志与已知指标说明已保存（拒绝身体观察不影响主线）"),
    dict(id="AC02", title="相互矛盾的报告", kind="side", npcId="npc_shao_ning", locationId="r06_archive",
         showAny=["FA03_record_credit"], requiresAll=["FA03_record_credit"], accept="talk",
         workMinutes=10, workServiceIds=["academy_report_compare"], noteDialogue="AC02_external_medical_note",
         stepOffer="到档案室和邵宁交谈。", stepActive="在档案室比对观察报告（10分钟）。",
         result="核对笔记与外部医疗线索已保存（不授新药、永久免疫或容量）"),
    # ───────── 招募（A11 §7；B15a Q152 选 A：前置改为 FA 对应，地点、0 分钟 0 物资与首次装备沿原规则）─────────
    # accept=roster：同行邀约卡“交谈”打开逐字邀约（Q156 选 B），读完选“登记同行”才入名册；“暂不登记”可再谈。
    dict(id="RN01", title="相互照应", kind="recruit", npcId="npc_lin_zhao", locationId="h02_square",
         showAny=[], requiresAll=["hub_registered"], accept="roster", workMinutes=0, offerDialogue="RN01_recruit_offer",
         stepOffer="在营区广场和林昭交谈，读完邀约后选择是否登记同行。", stepActive="在营区广场和林昭交谈，读完邀约后选择是否登记同行。",
         workLabel="交谈", result="林昭登记入名册（首次装备随登记一次生成）"),
    dict(id="RN02", title="检修交接", kind="recruit", npcId="npc_zhou_yan", locationId="h03_workshop",
         showAny=[], requiresAll=[], requiresAny=[qf("FA02", "accepted"), qf("FA02", "completed")], requiresExplore={"a01_yard": 100},
         accept="roster", workMinutes=0, offerDialogue="RN02_recruit_offer",
         stepOffer="在营区工坊和周砚交谈，读完邀约后选择是否登记同行。", stepActive="在营区工坊和周砚交谈，读完邀约后选择是否登记同行。",
         workLabel="交谈", result="周砚登记入名册（首次装备随登记一次生成）"),
    dict(id="RN03", title="转运手册", kind="recruit", npcId="npc_shen_he", locationId="h04_infirmary",
         showAny=[], requiresAll=[], requiresAny=[qf("FA03", "accepted"), qf("FA03", "completed")], requiresExplore={"b01_school": 100},
         accept="roster", workMinutes=0, offerDialogue="RN03_recruit_offer",
         stepOffer="在营区医务室和沈禾交谈，读完邀约后选择是否登记同行。", stepActive="在营区医务室和沈禾交谈，读完邀约后选择是否登记同行。",
         workLabel="交谈", result="沈禾登记入名册（首次装备随登记一次生成）"),
    dict(id="RN04", title="林间归路", kind="recruit", npcId="npc_tang_li", locationId="h02_square",
         showAny=[], requiresAll=[qf("FA03", "completed")], requiresExplore={"w02_forest": 100},
         accept="roster", workMinutes=0, offerDialogue="RN04_recruit_offer",
         stepOffer="在营区广场和唐梨交谈，读完邀约后选择是否登记同行。", stepActive="在营区广场和唐梨交谈，读完邀约后选择是否登记同行。",
         workLabel="交谈", result="唐梨登记入名册（首次装备随登记一次生成）"),
    dict(id="RN05", title="封存清单", kind="recruit", npcId="npc_gu_cheng", locationId="h03_workshop",
         showAny=[], requiresAll=[], requiresAny=["communication_power_claimed", qf("FA04", "completed")],
         accept="roster", workMinutes=0, offerDialogue="RN05_recruit_offer",
         stepOffer="在营区工坊和顾承交谈，读完邀约后选择是否登记同行。", stepActive="在营区工坊和顾承交谈，读完邀约后选择是否登记同行。",
         workLabel="交谈", result="顾承登记入名册（首次装备随登记一次生成）"),
    dict(id="RN06", title="值守记录", kind="recruit", npcId="npc_ye_xun", locationId="h02_square",
         showAny=[], requiresAll=[], requiresAny=["communication_power_claimed", qf("FA04", "completed")], requiresExplore={"b03_hospital": 100},
         accept="roster", workMinutes=0, offerDialogue="RN06_recruit_offer",
         stepOffer="在营区广场和叶洵交谈，读完邀约后选择是否登记同行。", stepActive="在营区广场和叶洵交谈，读完邀约后选择是否登记同行。",
         workLabel="交谈", result="叶洵登记入名册（首次装备随登记一次生成）"),
]

KIND_TITLES = {"offer": "接取", "work": "工作", "turnin": "交付", "repeat": "回看"}


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def main():
    cfg = load(CONFIG)
    nt = load(os.path.join(MAPDIR, "narrative-texts.json"))
    svcd = load(os.path.join(MAPDIR, "city-services.json"))
    sets = {d["dialogueSetId"]: d for d in nt["dialogueSets"]}
    locs = {l["id"]: l for l in cfg["locations"]}
    items = {i["id"] for i in cfg["items"]}
    npcs = {n["npcId"]: n for n in cfg["city"]["npcs"]}
    named = {c["actorId"]: c for c in cfg["party"]["namedCompanions"]}
    svc_design = {s["id"]: s for s in svcd["services"]}
    assert set(IMPLEMENTED) == {q["id"] for q in QUESTS}
    # Q156 逐字邀约（narrative-texts recruitInvitations）：questId/人物/地点与 A11 §6/§7 一致；h04_clinic 是营区医务室 h04_infirmary 的叙事别名
    invites = {x["questId"]: x for x in nt["recruitInvitations"]}
    assert len(invites) == 6
    loc_alias = {"h04_clinic": "h04_infirmary"}
    for q in QUESTS:
        if q["kind"] != "recruit":
            continue
        inv = invites[q["id"]]
        assert inv["npcId"] == q["npcId"] and named[q["npcId"]]["name"] == inv["displayName"], q["id"]
        assert loc_alias.get(inv["locationId"], inv["locationId"]) == q["locationId"], q["id"]
        assert inv["dialogueId"] == q["offerDialogue"], q["id"]
        for node in q.get("requiresExplore", {}):
            assert node in locs, node

    speaker = {"system": "", "protagonist": "你", "baobao": "饱饱"}
    for r in nt["speakerRegistry"]:
        speaker.setdefault(r["id"], r["name"])

    # 8 支线：narrative-texts sideQuests（标题、岗位、地点）与 A11 §5 表一致
    side = {x["questId"]: x for x in nt["sideQuests"]}
    assert len(side) == 8
    quests = []
    dialogue_sets = []
    for q in QUESTS:
        q = dict(q)
        q["implemented"] = IMPLEMENTED[q["id"]]
        assert q["locationId"] in locs, q["id"]
        if q["kind"] == "recruit":
            assert q["npcId"] in named and named[q["npcId"]]["recruitTask"] == q["id"], q["id"]
        else:
            assert q["npcId"] in npcs, q["id"]
            # FA05 由罗铮说电源与联络（FA05_power_contact 的 speaker）；city-npc-data 里他名下仍写旧 QMAP04（FA05 沿 QMAP04/05 技术阶段）
            assert q["id"] in npcs[q["npcId"]]["questIds"] or (q["id"] == "FA05" and "QMAP04" in npcs[q["npcId"]]["questIds"]), q["id"]
            assert npcs[q["npcId"]]["locationId"] == q["locationId"], q["id"]
        if q["id"] in side:
            sq = side[q["id"]]
            assert sq["title"] == q["title"] and sq["npcId"] == q["npcId"] and sq["locationId"] == q["locationId"], q["id"]
            q["offerDialogue"] = sq["offerDialogue"]
            q["workDialogue"] = sq["workHint"]
            q["turninDialogue"] = sq["turnInDialogue"]
            q["repeatDialogue"] = sq["repeatDialogue"]
        for sid in ("workServiceIds",):
            for s in q.get(sid, []):
                assert s in svc_design and svc_design[s].get("questId") == q["id"], (q["id"], s)
        if q.get("acceptServiceId"):
            assert q["acceptServiceId"] in svc_design
        for k, title in (("offerDialogue", "接取"), ("workDialogue", "工作"), ("turninDialogue", "交付"), ("repeatDialogue", "回看"),
                         ("completeDialogue", "完成"), ("hubTurninDialogue", "营区备份"), ("noteDialogue", "外部医疗线索")):
            sid = q.get(k)
            if not sid:
                continue
            assert sid in sets or (q["kind"] == "recruit" and sid in {x["dialogueId"] for x in invites.values()}), sid
            if q["kind"] == "recruit" and k == "offerDialogue":
                title = "邀约"
            if q["kind"] == "main" and k == "offerDialogue":
                title = {"FA01": "公开说明", "FA02": "公开路线", "FA03": "环境调查路线", "FA04": "整理记录", "FA05": "电源与联络"}[q["id"]]
            dialogue_sets.append({"id": sid, "title": f"{q['title']}：{title}"})
        quests.append({
            "id": q["id"], "title": q["title"], "kind": q["kind"], "npcId": q["npcId"], "locationId": q["locationId"],
            "implemented": q["implemented"], "showAny": q.get("showAny", []), "requiresAll": q.get("requiresAll", []),
            "requiresAny": q.get("requiresAny", []), "requiresExplore": q.get("requiresExplore", {}),
            "requiresRosterMember": q.get("requiresRosterMember", False), "accept": q["accept"],
            "acceptServiceId": q.get("acceptServiceId"),
            "offerDialogue": q.get("offerDialogue"), "workDialogue": q.get("workDialogue"), "turninDialogue": q.get("turninDialogue"),
            "repeatDialogue": q.get("repeatDialogue"), "completeDialogue": q.get("completeDialogue"),
            "hubTurninDialogue": q.get("hubTurninDialogue"), "noteDialogue": q.get("noteDialogue"),
            "workMinutes": q.get("workMinutes", 0), "workServiceIds": q.get("workServiceIds", []),
            "workRequiresAll": q.get("workRequiresAll", []), "workLabel": q.get("workLabel", ""),
            "completeByAny": q.get("completeByAny", []), "completeRequiresAccepted": q.get("completeRequiresAccepted", False),
            "completeFacts": q.get("completeFacts", []),
            "stepOffer": q["stepOffer"], "stepLocked": q.get("stepLocked", ""), "stepActive": q["stepActive"], "result": q["result"],
        })

    # 任务类服务开放，选项取同一任务对白的 choices（两种选择共一本任务账）
    choice_src = {"church_reception_existing": "FA02_route_choice", "academy_reception_existing": "FA03_environment_choice",
                  "church_public_mediation": "CH02_offer", "academy_noninvasive_observation": "AC01_offer"}
    for s in cfg["city"]["services"]:
        if s["id"] not in SERVICES_ON:
            continue
        s["implemented"] = True
        if s["id"] in choice_src:
            d = sets[choice_src[s["id"]]]
            s["choices"] = [{"id": c["choiceId"], "label": c["label"], "stat": "", "below": 0.0, "outputs": {},
                             "facts": c.get("flags", [])} for c in d["choices"]]
            assert len(s["choices"]) == 2, s["id"]
        if s["id"] in ("church_reception_existing", "academy_reception_existing"):
            # S15「gate事实接口」/A08 规则 6：两个选择同写同一个路线事实
            assert all(c["facts"] == [s["facts"][-1]] for c in s["choices"]), s["id"]
        if s["id"] == "church_archive_copy":
            s["choices"] = []
        if svc_design[s["id"]].get("questId"):
            s["questId"] = svc_design[s["id"]]["questId"]

    # 战术训练（R03 §3）：入口在 h03
    cfg["scope"].pop("training", None)
    for tier in ((1, 3, [], {"parts": 2, "cloth": 2}, 60), (2, 0, [], {"precision_parts": 2, "steel": 2}, 90)):
        for k in tier[3]:
            assert k in items, k
    if TRAINING:
      cfg["scope"]["training"] = {
        "source": "R03 §3 AP成长与通用动作",
        "locationId": "h03_workshop", "npcId": "npc_luo_zheng", "title": "战术训练",
        "summary": "为一名同行者做战术训练，提高每次普通行动的行动点。",
        "tiers": [
            {"tier": 1, "minCapacity": 3, "requiresAll": [], "requiresAny": [], "inputs": {"parts": 2, "cloth": 2}, "minutes": 60},
            # Q153 选 A：旧 QMAP04 前置改为已领警备仓备用电源或 FA04 完成，不单设 10 分钟接口检测
            {"tier": 2, "minCapacity": 0, "requiresAll": [], "requiresAny": ["communication_power_claimed", qf("FA04", "completed")],
             "inputs": {"precision_parts": 2, "steel": 2}, "minutes": 90},
        ],
    }

    cfg["quests"] = {
        "source": "A11「4 主线任务骨架修订与容量」「5 支线委托最小表」「7 招募与补充任务表」；narrative-texts.json（Q32 选 B）；A08 门槛规则 6/7；S15 gate事实接口",
        "list": quests,
        "dialogueSets": dialogue_sets,
    }

    # story.dialogues：先去掉上次导入的任务对白，再追加（eventId = dialogueSetId，lineId 原样）
    qsets = list(dict.fromkeys(d["id"] for d in dialogue_sets))
    inv_by_set = {x["dialogueId"]: x for x in nt["recruitInvitations"]}
    line_ids = {l["lineId"] for sid in qsets if sid not in inv_by_set for l in sets[sid]["lines"]}
    line_ids |= {x["lineId"] for x in nt["recruitInvitations"]}
    dl = [d for d in cfg["story"]["dialogues"] if d["id"] not in line_ids]
    for sid in qsets:
        if sid in inv_by_set:
            # Q156：每人一句逐字邀约，由本人说（不由机构代班人代说）
            x = inv_by_set[sid]
            dl.append({"id": x["lineId"], "eventId": sid, "speaker": x["displayName"], "text": x["text"], "note": ""})
            continue
        for l in sets[sid]["lines"]:
            dl.append({"id": l["lineId"], "eventId": sid, "speaker": speaker[l["speaker"]], "text": l["text"], "note": ""})
    cfg["story"]["dialogues"] = dl

    # Q155 选 A：登记后主目标切到当前主线；暂无可显示未完成主线 / 全部完成的两句（策划原文）
    so = [x for x in cfg["prologue"]["specialObjectives"] if x["id"] not in ("main_quest_none", "main_quest_done")]
    so.append({"id": "main_quest_none", "title": "查看主线与委托", "step": "打开任务册，查看当前主线进度与接取条件。"})
    so.append({"id": "main_quest_done", "title": "保持联络，继续求生", "step": "本轮主线已完成，可在任务册回看记录，继续探索与求生。"})
    cfg["prologue"]["specialObjectives"] = so

    # 门槛说明：任务已上线，去掉“后续版本开放”
    fl = cfg["map"]["factLabels"]
    fl["FA02_route_briefed"] = "在教会接待庭询问公开路线"
    fl["FA03_environment_briefed"] = "在科学院接待处询问环境调查路线"
    fl["FA04_merged"] = "在营区广场整理两份调查记录"
    fl["chapter_contact"] = "在深野通信站联络台完成联络"
    # Q153：阶位 2 不再读旧 QMAP04，去掉“这项主线暂未开放”的说明
    fl.pop(qf("QMAP04", "completed"), None)

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"quests：{len(quests)} 项（实装 {sum(1 for q in quests if q['implemented'])}）、{len(line_ids)} 句对白、开放服务 {len(SERVICES_ON)} 项")


if __name__ == "__main__":
    main()
