# -*- coding: utf-8 -*-
"""B26a 代班专用交谈的配置导入（开发计划 v0.9 批次 B26a，需求 R26，Q236、Q239）→ data/config.v0.6.json。

文字与状态引用逐字取自策划数据，不自补：
- assets/20261030/narrative-map/narrative-texts.json：npcDialogueSets 每名岗位新增的三个状态引用
  （substitute_idle / substitute_quest_offer / substitute_quest_return）与 dialogueSets 里的 48 个对白组；
- assets/20261104/substitute-talk-adjudications.json：Q236 合同逐字稿（idleLines 16 条、sharedLines 2 条），这里只作比对；
- assets/20261104/substitute-title-adjudications.json：Q239 合同 groups，48 组 dialogueSetId 到对话记录组名的绑定。

写入：
- city.npcs[*].states 补三个状态引用；
- story.dialogues 写 48 句（speaker 用中文显示名“机构当班接续员”，本来就是代班员，不靠改 speaker 换人）；
- city.dialogueSets 写 48 组组名（title 取 Q239 合同，按 dialogueSetId 绑定，不拼人名）。

同时断言：16 岗位齐全、48 组各一句、lineId 全局唯一、0 分钟只读、文字与 Q236 合同逐字一致、发言人是本岗所属机构的当班接续员；
Q239 合同 48 组 ID 与标题各不相同，npcId、状态、speaker 与本组一致，标题与 narrative-texts 同组 title、合同岗位名加固定后缀都相同。

可重复运行，结果相同。
用法：python tools/import-b26a-substitute.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
DOC = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0")
NARR = os.path.join(DOC, "assets", "20261030", "narrative-map", "narrative-texts.json")
CONTRACT = os.path.join(DOC, "assets", "20261104", "substitute-talk-adjudications.json")
TITLES = os.path.join(DOC, "assets", "20261104", "substitute-title-adjudications.json")

STATES = ("substitute_idle", "substitute_quest_offer", "substitute_quest_return")
# Q239 binding 定的三类后缀，只作比对，title 本身取合同
SUFFIX = {"substitute_idle": "代班闲谈", "substitute_quest_offer": "代班事项提示", "substitute_quest_return": "代班进度提醒"}


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(NARR, encoding="utf-8") as f:
        nt = json.load(f)
    with open(CONTRACT, encoding="utf-8") as f:
        contract = json.load(f)
    with open(TITLES, encoding="utf-8") as f:
        titles = json.load(f)

    sets = {d["dialogueSetId"]: d for d in nt["dialogueSets"]}
    npc_sets = {x["npcId"]: x for x in nt["npcDialogueSets"]}
    names = {r["id"]: r["name"] for r in nt["speakerRegistry"]}
    idle = {x["npcId"]: x for x in contract["idleLines"]}
    shared = contract["sharedLines"]
    groups = {g["dialogueSetId"]: g for g in titles["groups"]}
    assert len(titles["groups"]) == 48 and len(groups) == 48 and len({g["title"] for g in titles["groups"]}) == 48
    assert contract["historyTitlesQ239"]["groups"] == titles["groups"]
    npcs = cfg["city"]["npcs"]
    assert len(npcs) == 16 and set(idle) == {n["npcId"] for n in npcs}

    dialogues = cfg["story"]["dialogues"]
    written = []
    set_titles = []
    for n in npcs:
        states = npc_sets[n["npcId"]]["states"]
        for st in STATES:
            set_id = states[st]
            ds = sets[set_id]
            assert ds["npcId"] == n["npcId"] and ds["locationId"] == n["locationId"], set_id
            assert ds["timeCostSource"] == "none_read_only" and ds["automaticOnce"] is False, set_id
            assert len(ds["lines"]) == 1, set_id
            line = ds["lines"][0]
            want = idle[n["npcId"]]["text"] if st == "substitute_idle" else shared[st]
            assert line["text"] == want, set_id
            assert names[line["speaker"]] == n["dutySpeaker"], set_id
            if st == "substitute_idle":
                c = idle[n["npcId"]]
                assert (line["lineId"], line["speaker"], set_id, n["locationId"]) == (c["lineId"], c["speaker"], c["dialogueSetId"], c["locationId"]), set_id
            g = groups[set_id]
            assert (g["npcId"], g["state"], g["speaker"]) == (n["npcId"], st, line["speaker"]), set_id
            assert g["title"] == ds["title"] == g["postName"] + "：" + SUFFIX[st], set_id
            n["states"][st] = set_id
            written.append({"id": line["lineId"], "eventId": set_id, "speaker": n["dutySpeaker"], "text": line["text"], "note": ""})
            set_titles.append({"id": set_id, "title": g["title"]})

    assert len(written) == 48 and len({d["id"] for d in written}) == 48
    new_events = {d["eventId"] for d in written}
    dialogues[:] = [d for d in dialogues if d["eventId"] not in new_events]
    other_ids = {d["id"] for d in dialogues}
    assert not other_ids & {d["id"] for d in written}
    dialogues.extend(written)

    city_sets = cfg["city"]["dialogueSets"]
    city_sets[:] = [d for d in city_sets if d["id"] not in new_events]
    city_sets.extend(set_titles)

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("16 岗位三个代班状态、48 句代班对白、48 组对话记录组名已写入")


if __name__ == "__main__":
    main()
