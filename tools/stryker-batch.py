"""对一批开发改动过的引擎代码（src/Core）跑变异测试（Stryker.NET），列出改坏了测试也没发现的位置。

用法（在仓库根目录）：
  python tools/stryker-batch.py <基线提交> [目标提交，默认 HEAD]

做什么：
  1. 在系统临时目录开一个 detached worktree（目标提交），不碰共用工作区的 bin/obj，可和本地试玩同时跑。
  2. 取 基线..目标 在 src/Core 里新增/修改的行，换算成 Stryker 的字符位置（mutate 里 {a..b} 是字符偏移，不是行号），
     只变异这些行。开发入口代码不变异（2026-11-19 起）：GameEngine.Debug.cs 整个文件、名字以 Debug 开头的方法
     （DebugFeedKit、DebugStationKit、DebugToLegacyWaterUnits 等，只供 ?dev=1 造测试局面）——B56 存活＋无覆盖 97 条里
     72 条在这里，逐条写“不补”理由白花时间；扣掉的行数会打印出来。
  3. 跑 dotnet stryker（测试过滤同 CI：排除 Tuning、DesignSync）。约 7 分钟，其中 6 分钟是编译整个引擎变异体的固定开销，
     批次大小影响不大。定时回合里用 Bash run_in_background 跑。
  4. 把"存活"（Survived：改坏了测试仍全绿）与"无覆盖"（NoCoverage：没有任何测试跑到这行）逐条写进
     <临时目录>/survival-stryker-<目标短号>/survivors.md（文件:行、改法、原代码），同目录留 HTML 报告；最后删掉 worktree。
退出码：0 跑完（不论有无存活），1 出错；本批没改 src/Core 时打印说明并退出 0。
需要全局工具 dotnet-stryker 4.x（本机 .NET SDK 8.0 跑不了 5.x）：dotnet tool install -g dotnet-stryker --version "4.*"
"""

import json
import re
import shutil
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def git(*args, cwd=ROOT):
    return subprocess.run(["git", *args], cwd=cwd, check=True, capture_output=True, text=True, encoding="utf-8").stdout


DEBUG_METHOD = re.compile(r"^\s*(?:(?:public|private|internal|protected|static|override|async|unsafe|partial|virtual|sealed|new)\s+)+[\w<>\[\],.?() ]+?\s+Debug\w*\s*[<(]")


def debug_lines(text):
    """开发入口代码占的行号（1 起）：Debug 开头的方法从签名行到配对的右花括号（表达式体方法到分号）。"""
    lines = text.splitlines()
    out = set()
    i = 0
    while i < len(lines):
        if not DEBUG_METHOD.match(lines[i]):
            i += 1
            continue
        depth, seen, j = 0, False, i
        while j < len(lines):
            code = lines[j].split("//")[0]
            depth += code.count("{") - code.count("}")
            seen = seen or "{" in code
            if (seen and depth <= 0) or (not seen and code.rstrip().endswith(";")):
                break
            j += 1
        out.update(range(i + 1, j + 2))
        i = j + 1
    return out


def changed_spans(base, head, wt):
    """基线..目标 在 src/Core/*.cs 里新增/修改的行（扣掉开发入口代码）→ Stryker mutate 模式（字符偏移）。返回 (spans, 扣掉的行数)。"""
    diff = git("diff", "-U0", base, head, "--", "src/Core")
    spans, cur, line_starts, debug, skipped = [], None, {}, {}, 0
    for l in diff.splitlines():
        if l.startswith("+++ "):
            cur = l[6:].strip() if l.startswith("+++ b/") and l.endswith(".cs") else None
            continue
        m = re.match(r"@@ -\S+ \+(\d+)(?:,(\d+))? @@", l)
        if not (m and cur):
            continue
        a, c = int(m.group(1)), int(m.group(2) or 1)
        if c == 0:
            continue
        if cur not in line_starts:
            # newline="" 保留 CRLF：Stryker 按原文字符数算偏移，换行被折成 LF 会让后面的范围整体前移
            with open(wt / cur, encoding="utf-8-sig", newline="") as f:
                text = f.read()
            starts = [0] + [i + 1 for i, ch in enumerate(text) if ch == "\n"] + [len(text)]
            line_starts[cur] = starts
            debug[cur] = set(range(1, len(starts) + 1)) if Path(cur).name == "GameEngine.Debug.cs" else debug_lines(text)
        st = line_starts[cur]
        # 把本段里不属于开发入口的连续行各自作为一段
        run = []
        for ln in list(range(a, a + c)) + [None]:
            if ln is not None and ln not in debug[cur]:
                run.append(ln)
                continue
            if ln is not None:
                skipped += 1
            if run:
                ra, rc = run[0], len(run)
                spans.append((cur, ra, rc, f"**/{Path(cur).name}{{{st[ra - 1]}..{st[min(ra - 1 + rc, len(st) - 1)]}}}"))
                run = []
    return spans, skipped


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    base = sys.argv[1]
    head = git("rev-parse", "--short", sys.argv[2] if len(sys.argv) > 2 else "HEAD").strip()
    if not git("diff", "--name-only", base, head, "--", "src/Core").strip():
        print(f"{base}..{head} 没有改 src/Core，不用跑变异测试。")
        return 0

    out = Path(tempfile.gettempdir()) / f"survival-stryker-{head}"
    wt = out / "wt"
    if wt.exists():
        subprocess.run(["git", "worktree", "remove", "--force", str(wt)], cwd=ROOT)
        shutil.rmtree(wt, ignore_errors=True)
    out.mkdir(parents=True, exist_ok=True)
    git("worktree", "add", "--detach", str(wt), head)
    try:
        spans, skipped = changed_spans(base, head, wt)
        lines = sum(c for _, _, c, _ in spans)
        if skipped:
            print(f"开发入口代码（GameEngine.Debug.cs、Debug 开头的方法）{skipped} 行不变异")
        if not spans:
            print(f"{base}..{head}：src/Core 只改了开发入口代码，不用跑变异测试。")
            return 0
        print(f"{base}..{head}：src/Core 改动 {lines} 行、{len(spans)} 段，开始变异测试（约 7 分钟）")
        tests = wt / "web" / "Survival.Core.Tests"
        (tests / "stryker-config.json").write_text(json.dumps({"stryker-config": {
            "project": "Survival.Core.csproj",
            "mutate": [p for *_, p in spans],
            "reporters": ["html", "json", "progress"],
            "concurrency": 12,
            "coverage-analysis": "perTest",
            "test-case-filter": "TestCategory!=Tuning&TestCategory!=DesignSync",
        }}, ensure_ascii=False, indent=1), encoding="utf-8")
        subprocess.run(["dotnet", "restore", "web/Survival.sln", "-v", "q"], cwd=wt, check=True)
        log = out / "stryker.log"
        with open(log, "w", encoding="utf-8") as f:
            rc = subprocess.run(["dotnet", "stryker"], cwd=tests, stdout=f, stderr=subprocess.STDOUT).returncode
        reports = sorted(tests.glob("StrykerOutput/*/reports/mutation-report.json"))
        if rc != 0 or not reports:
            print(f"Stryker 失败（退出码 {rc}），日志：{log}")
            return 1
        shutil.copy(reports[-1], out / "mutation-report.json")
        shutil.copy(reports[-1].with_suffix(".html"), out / "mutation-report.html")

        report = json.loads(reports[-1].read_text(encoding="utf-8"))
        count, rows = Counter(), []
        for fn, fd in report["files"].items():
            src = fd["source"].split("\n")
            rel = "src/Core/" + fn.replace("\\", "/").split("/src/Core/")[-1]
            for m in fd["mutants"]:
                if m["status"] in ("Ignored", "CompileError"):
                    continue
                count[m["status"]] += 1
                if m["status"] in ("Survived", "NoCoverage"):
                    ln = m["location"]["start"]["line"]
                    rows.append((rel, ln, m["status"], m["mutatorName"], (m.get("replacement") or "").replace("\n", " ")[:120], src[ln - 1].strip()[:200]))
        tested = sum(count.values())
        caught = count["Killed"] + count["Timeout"]
        score = f"{caught * 100 / tested:.1f}%" if tested else "无"
        rows.sort()
        md = [f"# 变异测试 {base}..{head}", "",
              f"src/Core 改动 {lines} 行；参与变异 {tested} 个：被测试抓到 {count['Killed']}、超时 {count['Timeout']}、"
              f"存活 {count['Survived']}、无覆盖 {count['NoCoverage']}；得分 {score}。", "",
              "存活 = 把这里改坏了测试仍全绿；无覆盖 = 没有任何测试跑到这一行。逐条处理：补测试（期望值写出处），"
              "或写明不补的理由（等价改动、纯日志分隔符、开发入口工具、观察不到的清理）。", "",
              "| 位置 | 状态 | 改法 | 改成 | 原代码 |", "|---|---|---|---|---|"]
        for rel, ln, st, mut, rep, code in rows:
            esc = lambda t: t.replace("|", "\\|")
            md.append(f"| {rel}:{ln} | {'存活' if st == 'Survived' else '无覆盖'} | {mut} | `{esc(rep)}` | `{esc(code)}` |")
        (out / "survivors.md").write_text("\n".join(md) + "\n", encoding="utf-8")
        print(md[2])
        print(f"清单：{out / 'survivors.md'}")
        print(f"HTML 报告：{out / 'mutation-report.html'}")
        return 0
    finally:
        subprocess.run(["git", "worktree", "remove", "--force", str(wt)], cwd=ROOT, capture_output=True)
        shutil.rmtree(wt, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
