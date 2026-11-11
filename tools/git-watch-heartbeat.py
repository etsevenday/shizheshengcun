"""/git-watch 代理跑着时的后台续心跳（技能 §1）。

用法：
  python tools/git-watch-heartbeat.py <runId> <sessionId>          # 每 20 分钟刷新一次 heartbeat，直到退出条件
  python tools/git-watch-heartbeat.py <runId> <sessionId> --once   # 只刷新一次

退出条件：
  状态文件里的 runId 不是自己的（锁被别的轮次接手）→ 退出码 3；
  本轮会话记录已 45 分钟没写入（会话撞用量上限、请求卡死等，主会话停了而本脚本还活着）→ 不再续锁，退出码 4。
  2026-11-11 起：此前每轮临时写的心跳循环不看会话死活，会话停住后照样续锁，B33 那轮因此空等两小时。
"""

import importlib.util
import json
import os
import sys
import time
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STATE = ROOT / ".git" / "claude-git-watch.json"
INTERVAL = 20 * 60

_spec = importlib.util.spec_from_file_location("precheck", Path(__file__).with_name("git-watch-precheck.py"))
precheck = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(precheck)


def beat(run_id):
    state = json.loads(STATE.read_text(encoding="utf-8"))
    if state.get("runId") != run_id:
        print(f"LOCK TAKEN {state.get('runId')}", flush=True)
        sys.exit(3)
    state["heartbeat"] = datetime.now().astimezone().isoformat(timespec="seconds")
    tmp = STATE.with_suffix(".json.tmp")
    tmp.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(tmp, STATE)


def main():
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    run_id, session_id = sys.argv[1], sys.argv[2]
    once = "--once" in sys.argv
    while True:
        idle = precheck.transcript_idle_minutes(session_id)
        if idle is not None and idle >= precheck.TRANSCRIPT_IDLE_MINUTES:
            print(f"SESSION IDLE {idle:.0f} min，不再续锁", flush=True)
            sys.exit(4)
        beat(run_id)
        print("ok", flush=True)
        if once:
            break
        time.sleep(INTERVAL)


if __name__ == "__main__":
    main()
