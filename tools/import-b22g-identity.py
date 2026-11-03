# -*- coding: utf-8 -*-
"""B22g 周期候选姓名、性别与文字外观池导入（开发计划 v0.9 批次 B22g，需求 R179；Q180 选 C）→ data/config.v0.6.json 的
config.party.recruitment.identity。

数值逐条取自策划数据 assets/20261103/party-combat-expedition-adjudications.json 的 identity 合同，不自补：
- surnames 32、givenNames 32、gender 3（female 女 / male 男 / undisclosed 未说明）、skinTone 5、hairStyle 6、hairColor 4、faceDetail 6；
- 各池独立等概率有放回取一，不按性别限制姓名或发型；与模板/身体/人物档数值独立（展示随机子流）；
- 显示“姓名·004”，序号至少三位，超过 999 不截断、不循环。

可重复运行，结果相同（只覆盖 config.party.recruitment.identity）。
用法：python tools/import-b22g-identity.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONFIG = os.path.join(ROOT, "data", "config.v0.6.json")
CONTRACT = os.path.join(ROOT, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261103", "party-combat-expedition-adjudications.json")


def main():
    with open(CONTRACT, encoding="utf-8") as f:
        ident = json.load(f)["identity"]
    with open(CONFIG, encoding="utf-8") as f:
        cfg = json.load(f)
    pools = {
        "surnames": ident["surnames"],
        "givenNames": ident["givenNames"],
        "genders": [{"id": g["id"], "name": g["name"]} for g in ident["gender"]],
        "skinTones": ident["skinTone"],
        "hairStyles": ident["hairStyle"],
        "hairColors": ident["hairColor"],
        "faceDetails": ident["faceDetail"],
    }
    assert len(pools["surnames"]) == 32 and len(pools["givenNames"]) == 32, "姓名池 32×32"
    assert [g["id"] for g in pools["genders"]] == ["female", "male", "undisclosed"]
    assert (len(pools["skinTones"]), len(pools["hairStyles"]), len(pools["hairColors"]), len(pools["faceDetails"])) == (5, 6, 4, 6)
    for k, v in pools.items():
        if k != "genders":
            assert len(set(v)) == len(v) and all(isinstance(x, str) and x for x in v), k
    cfg["party"]["recruitment"]["identity"] = {
        "source": "Q180 选C：party-combat-expedition-adjudications.json identity（姓名池、性别与文字外观；展示子流一次生成持久化）；B22g",
        **pools,
        "nameSeparator": "·",
        "seqMinDigits": 3,
        "legacyGender": "未说明",
        "legacyAppearance": "未记录",
    }
    with open(CONFIG, "w", encoding="utf-8", newline="\n") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
