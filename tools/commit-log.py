"""把 git 提交记录转成可粘贴进飞书「生存日志 › 提交记录」的 HTML。

用法：
  python tools/commit-log.py                 # 全部提交
  python tools/commit-log.py --since <短号>   # 只要 <短号> 之后的提交（飞书文档最后一条之后）
  python tools/commit-log.py --out <文件>     # 写到文件（默认打印）

格式：按时间正序，每天一个二级标题（日期），每次提交一段加粗标题
「短号（链到 GitHub）  说明首行」，正文的 "- " 行转成列表、缩进续行并进上一条，
Co-Authored-By 行去掉。--since 时若第一条新提交与 <短号> 同一天，不重复写日期标题。
"""

import argparse
import html
import subprocess
import sys

REPO_URL = "https://github.com/etsevenday/shizheshengcun/commit/"
SEP = "\x1e"


def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True,
                          encoding="utf-8", check=True).stdout


def commits(since):
    rng = [f"{since}..HEAD"] if since else []
    out = git("log", "--reverse", "--date=short",
              f"--format=%H%x1f%h%x1f%ad%x1f%B{SEP}", *rng)
    for rec in out.split(SEP):
        rec = rec.strip("\n")
        if rec:
            full, short, date, body = rec.split("\x1f", 3)
            yield full, short, date, body


def body_html(lines):
    parts, items, para = [], [], []

    def flush_para():
        if para:
            parts.append(f"<p>{html.escape(' '.join(para))}</p>")
            para.clear()

    def flush_items():
        if items:
            lis = "".join(f"<li>{html.escape(i)}</li>" for i in items)
            parts.append(f"<ul>{lis}</ul>")
            items.clear()

    for raw in lines:
        if raw.startswith("Co-Authored-By"):
            continue
        line = raw.strip()
        if not line:
            flush_para()
            continue
        if line.startswith("- "):
            flush_para()
            items.append(line[2:])
        elif raw[:1].isspace() and items:
            items[-1] += line
        else:
            flush_items()
            para.append(line)
    flush_para()
    flush_items()
    return "".join(parts)


def render(since):
    last_date = git("log", "-1", "--date=short", "--format=%ad", since).strip() if since else None
    out = []
    for full, short, date, body in commits(since):
        if date != last_date:
            out.append(f"<h2>{date}</h2>")
            last_date = date
        lines = body.split("\n")
        title = html.escape(lines[0])
        out.append(f'<p><b><a href="{REPO_URL}{full}">{short}</a>　{title}</b></p>')
        out.append(body_html(lines[1:]))
    return "".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--since")
    ap.add_argument("--out")
    a = ap.parse_args()
    text = render(a.since)
    if a.out:
        with open(a.out, "w", encoding="utf-8") as f:
            f.write(text)
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(text)


if __name__ == "__main__":
    main()
