"""/git-watch 的预检：没有要处理的新提交时，不必让模型加载技能、逐条跑 git。

用法：
  python tools/git-watch-precheck.py          # 打印一段结论，首行以 IDLE / WORK / ERROR 开头
  python tools/git-watch-precheck.py --hook   # 作 UserPromptSubmit hook：只管带 [git-watch-scheduled] 的提示词

检查顺序与 git-watch 技能 §1 一致（只读，不加锁、不改状态文件）：
  1. 状态文件 .git/claude-git-watch.json 里 runningSince 不到 6 小时 → IDLE（上一轮还在跑）
  2. git fetch origin；origin/main 等于 notifiedHead → IDLE（已通知过用户，等处理）
  3. lastSeen..origin/main 里作者不是 etsevenday 的提交为空 → IDLE（无新提交）
  4. 否则 WORK，附新提交列表与本地未推送/未提交情况，交给技能继续处理
fetch 失败等异常一律 ERROR，hook 模式下不拦截，照常交给模型处理。

hook 模式：IDLE 时输出 {"decision":"block"} 拦下提示词，模型不启动；WORK 时把结论作为附加上下文
交给模型，技能第 1 节的检测就不用再跑一遍。每次结论追加到 .git/claude-git-watch.log（留最近 200 行）。
"""

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STATE = ROOT / ".git" / "claude-git-watch.json"
LOG = ROOT / ".git" / "claude-git-watch.log"
MARKER = "[git-watch-scheduled]"
OWN_AUTHOR = "etsevenday"
LOCK_HOURS = 6


def git(*args, timeout=60):
    r = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True,
                       encoding="utf-8", errors="replace", timeout=timeout)
    if r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} 失败：{r.stderr.strip()}")
    return r.stdout


def check():
    """返回 (状态, 说明)，状态为 IDLE / WORK / ERROR。"""
    try:
        state = json.loads(STATE.read_text(encoding="utf-8")) if STATE.exists() else {}
    except (OSError, ValueError) as e:
        return "ERROR", f"状态文件读不了：{e}"

    since = state.get("runningSince")
    if since:
        try:
            started = datetime.fromisoformat(since.replace("Z", "+00:00"))
            if started.tzinfo is None:
                started = started.astimezone()
            hours = (datetime.now(timezone.utc) - started).total_seconds() / 3600
            if hours < LOCK_HOURS:
                return "IDLE", f"上一轮还在跑（runningSince {since}）"
        except ValueError:
            pass  # 时间写坏了按无锁处理，交给技能

    try:
        git("fetch", "origin", "--quiet", timeout=90)
        head = git("rev-parse", "origin/main").strip()
        if state.get("notifiedHead") and head.startswith(state["notifiedHead"]):
            return "IDLE", f"origin/main {head[:7]} 已通知过用户，等用户处理或策划再推"

        last = state.get("lastSeen")
        if not last:
            return "WORK", "状态文件没有 lastSeen，需按技能确定从哪开始"
        log = git("log", "--format=%h\x1f%an\x1f%ad\x1f%s", "--date=format:%m-%d %H:%M",
                  f"{last}..origin/main")
        new = [l.split("\x1f") for l in log.splitlines() if l]
        theirs = [c for c in new if c[1] != OWN_AUTHOR]
        if not theirs:
            return "IDLE", f"无新提交（origin/main {head[:7]}，lastSeen {last[:7]}）"

        unpushed = git("log", "--oneline", "origin/main..HEAD").strip()
        dirty = [l for l in git("status", "--porcelain").splitlines()
                 if l and not l.startswith("??")]
    except (RuntimeError, subprocess.TimeoutExpired, OSError) as e:
        return "ERROR", str(e)

    lines = [f"origin/main {head[:7]}，lastSeen {last[:7]}，策划等人的新提交 {len(theirs)} 个："]
    lines += [f"  {h} {a} {d} {s}" for h, a, d, s in theirs]
    if unpushed:
        lines.append("本地有未推送提交：\n  " + unpushed.replace("\n", "\n  "))
    if dirty:
        lines.append("本地有已跟踪文件被改：\n  " + "\n  ".join(dirty))
    if not unpushed and not dirty:
        lines.append("本地干净，可以按技能继续（加锁、分类、处理）。")
    return "WORK", "\n".join(lines)


def log_result(status, text):
    try:
        old = LOG.read_text(encoding="utf-8").splitlines() if LOG.exists() else []
        stamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        old.append(f"{stamp} {status} {text.splitlines()[0]}")
        LOG.write_text("\n".join(old[-200:]) + "\n", encoding="utf-8")
    except OSError:
        pass


def main():
    if "--hook" not in sys.argv:
        status, text = check()
        sys.stdout.reconfigure(encoding="utf-8")
        print(f"{status} {text}")
        return

    try:
        prompt = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace")).get("prompt", "")
    except ValueError:
        return
    if MARKER not in prompt:
        return

    status, text = check()
    log_result(status, text)
    if status == "IDLE":
        out = {"decision": "block", "reason": f"git-watch 预检：{text}，本轮不启动。"}
    else:
        out = {"hookSpecificOutput": {
            "hookEventName": "UserPromptSubmit",
            "additionalContext": f"git-watch 预检结果（tools/git-watch-precheck.py，刚跑过）：\n{status} {text}",
        }}
    print(json.dumps(out, ensure_ascii=True))


if __name__ == "__main__":
    main()
