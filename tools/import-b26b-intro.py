# -*- coding: utf-8 -*-
"""B26b 代班接续说明的配置更新（开发计划 v0.9 批次 B26b，需求 R34、R35，Q244、Q245）→ data/config.v0.6.json。

文字逐字取自 assets/20261105/substitute-introduction-adjudications.json 的 groups，不自补：
- story.dialogues：16 组 institution_substitute 第一句换定稿，lineId、speaker、第二句 system 不动；
- city.dialogueSets：16 组组名改“{岗位名}：岗位接续”，按 dialogueSetId 绑定。

同时断言：16 岗位齐全、组 ID 与标题各不相同、标题 = 岗位名 + 固定后缀，配置里本组两句的 lineId 与顺序等于合同，
第二句正文等于合同，narrative-texts 同组 title 与两句都等于合同。

可重复运行，结果相同。
用法：python tools/import-b26b-intro.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
DOC = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0")
NARR = os.path.join(DOC, "assets", "20261030", "narrative-map", "narrative-texts.json")
CONTRACT = os.path.join(DOC, "assets", "20261105", "substitute-introduction-adjudications.json")


def main():
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    with open(NARR, encoding="utf-8") as f:
        nt = json.load(f)
    with open(CONTRACT, encoding="utf-8") as f:
        contract = json.load(f)

    groups = contract["groups"]
    assert len(groups) == 16 and len({g["dialogueSetId"] for g in groups}) == 16 and len({g["title"] for g in groups}) == 16
    npcs = {n["npcId"]: n for n in cfg["city"]["npcs"]}
    assert set(npcs) == {g["npcId"] for g in groups}
    sets = {d["dialogueSetId"]: d for d in nt["dialogueSets"]}
    city_sets = {d["id"]: d for d in cfg["city"]["dialogueSets"]}
    dialogues = cfg["story"]["dialogues"]

    for g in groups:
        set_id = g["dialogueSetId"]
        n = npcs[g["npcId"]]
        assert g["state"] == "institution_substitute" and n["states"]["institution_substitute"] == set_id, set_id
        assert n["locationId"] == g["locationId"], set_id
        assert g["title"] == g["postName"] + "：岗位接续", set_id
        ds = sets[set_id]
        assert ds["title"] == g["title"], set_id
        assert [(x["lineId"], x["speaker"], x["text"]) for x in ds["lines"]] == [
            (g["lineId"], g["speaker"], g["text"]), (g["systemLineId"], g["systemSpeaker"], g["systemText"])], set_id
        lines = [d for d in dialogues if d["eventId"] == set_id]
        assert [d["id"] for d in lines] == [g["lineId"], g["systemLineId"]], set_id
        assert lines[0]["speaker"] == n["dutySpeaker"] and lines[1]["speaker"] == "" and lines[1]["text"] == g["systemText"], set_id
        lines[0]["text"] = g["text"]
        city_sets[set_id]["title"] = g["title"]

    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(cfg, ensure_ascii=False, indent=2) + "\n")
    print("16 组接续说明的组名与第一句已写入")


if __name__ == "__main__":
    main()
