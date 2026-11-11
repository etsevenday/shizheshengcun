"""把《02 开发方待确认问题》里已结案的问题整节移到同目录的归档文件，原文件只留没结案的。

用法（在仓库根目录）：
  python tools/archive-questions.py            # 试跑：只打印会移走什么、留下什么，不改文件
  python tools/archive-questions.py --apply    # 真的改两个文件（不提交，提交由调用方做）
  可加 --keep-days N（默认 2）：最近 N 天内提出的节不动，哪怕已全部结案

规则（宁可少移，不可错移）：
  - 按"节"移：一节 = 一个"## <日期> 来自 …"标题和它下面的全部 Q；文件开头第一个日期标题之前的 Q 算首节。
  - 一节里**每个** Q 都结案了才整节移走，有一个没结案就整节留下。
    结案 = 最后一条"处理结果"以"已处理／已上线／已实现／内核已实现／与现状一致"开头，并且"策划答复"不是"待定"
    （没有答复栏但有处理结果的也算，如 Q4、Q5；"已登记""部分处理""另行立项"这类还有后续的不算）。
  - 文末「修改记录」里早于保留天数的条目一起移走，其余留下。
  - 不改任何一个 Q 的正文，原样搬。归档文件可重复追加。
执行前调用方要确认：没有别的会话正在改这份文件，策划那边没有未推送的答复。
"""

import re
import sys
from datetime import date, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DIR = ROOT / "doc" / "策划案" / "系统策划总册 v1.0" / "00 总则"
SRC = DIR / "02 开发方待确认问题.md"
DST = DIR / "02a 开发方待确认问题归档.md"
NOTE_MARK = "> **已结案的问题在归档里**"
LOG_HEAD = "## 修改记录"
DST_HEAD = (
    "# 开发方待确认问题（归档）\n\n"
    "这里是《02 开发方待确认问题》里已经结案的问题：策划已答复，开发方已按结论处理并写了\"处理结果\"。"
    "内容从原文件原样搬来，没有改写；结论仍然有效，新策划案不能和这里的答复冲突（要推翻就写明\"取代 Qn 的答复\"）。"
    "本文件很大，查某个 Q 请直接搜\"## Qn \"，不用整份读。没结案的问题和新问题都在"
    "[02 开发方待确认问题](<02 开发方待确认问题.md>)。\n"
)


def split_blocks(text):
    """按二级标题切块，返回 (文首, [块…])，每块以 '## ' 开头、不含结尾换行。"""
    parts = re.split(r"\n(?=## )", text.rstrip("\n"))
    return parts[0], parts[1:]


def q_number(block):
    m = re.match(r"## Q(\d+)\b", block)
    return int(m.group(1)) if m else None


DONE_WORDS = ("已处理", "已上线", "已实现", "内核已实现", "与现状一致")


def is_closed(block):
    results = re.findall(r"处理结果\*{0,2}[：:]\s*([^\n]*)", block)
    if not results or not results[-1].startswith(DONE_WORDS):
        return False  # "已登记""部分处理""另行立项"之类还有后续的，不算结案
    m = re.search(r"策划答复\*{0,2}[：:]\s*([^\n]*)", block)
    return not (m and m.group(1).strip().startswith("待定"))


def ranges(numbers):
    out, numbers = [], sorted(numbers)
    for n in numbers:
        if out and n == out[-1][1] + 1:
            out[-1][1] = n
        else:
            out.append([n, n])
    return "、".join(f"Q{a}" if a == b else f"Q{a}—Q{b}" for a, b in out)


def main():
    apply = "--apply" in sys.argv
    keep_days = int(sys.argv[sys.argv.index("--keep-days") + 1]) if "--keep-days" in sys.argv else 2
    cutoff = date.today() - timedelta(days=keep_days)
    sys.stdout.reconfigure(encoding="utf-8")

    raw = SRC.read_bytes().decode("utf-8")
    crlf = "\r\n" in raw
    head, blocks = split_blocks(raw.replace("\r\n", "\n"))

    log_block = blocks.pop() if blocks and blocks[-1].startswith(LOG_HEAD) else None
    if log_block is None or any(b.startswith(LOG_HEAD) for b in blocks):
        sys.exit("没找到文末唯一的「## 修改记录」，文件结构和预期不同，不处理")

    # 归成节：[标题块或 None, 日期, [Q 块…]]
    sections = [[None, date(2026, 10, 27), []]]
    for b in blocks:
        if q_number(b) is not None:
            sections[-1][2].append(b)
            continue
        m = re.match(r"## (\d{4})-(\d{2})-(\d{2}) 来自", b)
        if not m:
            sys.exit(f"遇到认不出的二级标题，不处理：{b.splitlines()[0][:60]}")
        sections.append([b, date(*map(int, m.groups())), []])

    moved, kept, moved_q, kept_q, why_kept = [], [], [], [], []
    for title, day, qs in sections:
        nums = [q_number(q) for q in qs]
        open_q = [q_number(q) for q in qs if not is_closed(q)]
        name = title.splitlines()[0][3:60] if title else "首节（2026-10-27 第一批）"
        if qs and not open_q and day < cutoff:
            moved.append((title, qs))
            moved_q += nums
        else:
            kept.append((title, qs))
            kept_q += nums
            if qs:
                reason = f"未结案 {ranges(open_q)}" if open_q else f"{day} 提出，未满 {keep_days} 天"
                why_kept.append(f"  {name} | {ranges(nums)} | {reason}")

    log_lines = log_block.split("\n")[1:]
    log_old, log_new = [], []
    for line in log_lines:
        m = re.match(r"- (\d{4})-(\d{2})-(\d{2})", line)
        (log_old if m and date(*map(int, m.groups())) < cutoff else log_new).append(line)
    log_new = [l for i, l in enumerate(log_new) if l.strip() or (i and log_new[i - 1].strip())]

    def render(items):
        out = []
        for title, qs in items:
            out += ([title.rstrip("\n")] if title else []) + [q.rstrip("\n") for q in qs]
        return "\n\n".join(out)

    all_before = sorted(n for _, _, qs in sections for n in map(q_number, qs))
    if sorted(moved_q + kept_q) != all_before or len(set(all_before)) != len(all_before):
        sys.exit("移走的加留下的对不上原来的 Q 清单（或有重复编号），不处理")

    # 归档文件：已有内容里取出旧的节和旧的修改记录，接着往后加
    old_body, old_log, old_nums = "", [], []
    if DST.exists():
        d_head, d_blocks = split_blocks(DST.read_bytes().decode("utf-8").replace("\r\n", "\n"))
        if d_blocks and d_blocks[-1].startswith(LOG_HEAD):
            old_log = [l for l in d_blocks.pop().split("\n")[1:] if l.strip()]
        old_body = "\n\n".join(b.rstrip("\n") for b in d_blocks)
        old_nums = [n for n in map(q_number, d_blocks) if n is not None]
        if set(old_nums) & set(moved_q):
            sys.exit("归档文件里已经有这次要移的编号，不处理")
    archived_all = old_nums + moved_q

    note = (f"{NOTE_MARK}：策划已答复、开发方已处理完的问题，整节移到了同目录的"
            f"[02a 开发方待确认问题归档](<02a 开发方待确认问题归档.md>)，本文件只留还没结案的。"
            f"已归档：{ranges(archived_all)}（结论仍然有效）。查某个 Q 先在本文件找，找不到到归档里搜\"## Qn \"。"
            f"新问题的编号接着两份文件里最大的 Q 往下排，不重用。")
    head_lines = [l for l in head.split("\n") if not l.startswith(NOTE_MARK)]
    while head_lines and not head_lines[-1].strip():
        head_lines.pop()
    new_src = "\n".join(head_lines) + "\n\n" + note + "\n\n" + render(kept) + "\n\n" \
        + LOG_HEAD + "\n\n" + "\n".join(l for l in log_new if l.strip()) + "\n"
    new_dst = DST_HEAD + "\n" + "\n\n".join(x for x in (old_body, render(moved)) if x) + "\n\n" \
        + LOG_HEAD + "\n\n" + "\n".join(old_log + [l for l in log_old if l.strip()]) + "\n"

    kb = lambda s: len(s.encode("utf-8")) // 1024
    print(f"{'执行' if apply else '试跑（不改文件）'}：保留最近 {keep_days} 天（{cutoff} 及以后提出的节不动）")
    print(f"原文件：{len(sections)} 节、{len(all_before)} 个 Q、{kb(raw)} KB")
    print(f"移走：{len(moved)} 节、{len(moved_q)} 个 Q；修改记录 {len(log_old)} 条")
    print(f"  {ranges(moved_q) or '（无）'}")
    print(f"留下：{sum(1 for _, q in kept if q)} 节、{len(kept_q)} 个 Q；修改记录 {len([l for l in log_new if l.strip()])} 条")
    print("\n".join(why_kept) or "  （无）")
    print(f"之后：原文件约 {kb(new_src)} KB，归档文件约 {kb(new_dst)} KB")
    if not moved:
        print("没有可移的节。")
        return
    if apply:
        nl = "\r\n" if crlf else "\n"
        SRC.write_bytes(new_src.replace("\n", nl).encode("utf-8"))
        DST.write_bytes(new_dst.replace("\n", nl).encode("utf-8"))
        print(f"已写：{SRC.name}、{DST.name}（未提交）")


if __name__ == "__main__":
    main()
