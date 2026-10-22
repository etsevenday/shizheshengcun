#!/usr/bin/env python3
"""
留言板收件工具：把 Supabase 里的留言拉到本地，供作者和 AI 阅读、修改、标记处理状态。

配置（任选其一，密钥只放本机，不要提交）：
  1. 仓库根目录 .feedback.local.json：{"supabaseUrl": "https://xxx.supabase.co", "serviceKey": "sb_secret_..."}
  2. 环境变量 SUPABASE_URL 与 SUPABASE_SERVICE_KEY

用法：
  python tools/feedback.py list                 列出未处理留言
  python tools/feedback.py list --status all    列出全部
  python tools/feedback.py pull                 把未处理留言（含截图、存档）下载到 feedback-inbox/
  python tools/feedback.py mark 1a2b3c4d done --note "已在 abc123 修复"
  python tools/feedback.py ping                 检查连接与权限

每条留言下载为 feedback-inbox/<时间>-<编号>/ 目录：
  feedback.md   留言正文、环境、游戏状态摘要、最近日志（AI 从这里读）
  1.webp ...    截图
  save.json     玩家当时的存档（可直接用来复现）
  context.json  完整的自动采集信息
"""
import argparse
import datetime
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INBOX = os.path.join(ROOT, "feedback-inbox")
LOCAL = os.path.join(ROOT, ".feedback.local.json")
PUBLIC_CFG = os.path.join(ROOT, "web", "Survival.Web", "wwwroot", "feedback.json")
CATEGORY = {"bug": "出错了", "ux": "体验/界面", "balance": "数值/难度", "idea": "建议", "other": "其他"}
STATUS = ("new", "doing", "done", "wontfix")

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass


def load_config():
    url = os.environ.get("SUPABASE_URL", "")
    key = os.environ.get("SUPABASE_SERVICE_KEY", "")
    table, bucket = "feedback", "feedback-screenshots"
    if os.path.exists(LOCAL):
        with open(LOCAL, encoding="utf-8") as f:
            c = json.load(f)
        url = url or c.get("supabaseUrl", "")
        key = key or c.get("serviceKey", "")
        table = c.get("table", table)
        bucket = c.get("bucket", bucket)
    if not url and os.path.exists(PUBLIC_CFG):
        with open(PUBLIC_CFG, encoding="utf-8") as f:
            url = json.load(f).get("supabaseUrl", "")
    if not url or not key:
        sys.exit("缺少配置：请在仓库根目录创建 .feedback.local.json（supabaseUrl 与 serviceKey），详见 doc/留言板接入说明.md")
    return url.rstrip("/"), key, table, bucket


class Api:
    def __init__(self):
        self.url, self.key, self.table, self.bucket = load_config()

    def headers(self, extra=None):
        h = {"apikey": self.key}
        if not self.key.startswith("sb_"):
            h["Authorization"] = "Bearer " + self.key
        h.update(extra or {})
        return h

    def request(self, method, path, body=None, extra=None, raw=False):
        data = None
        hdr = self.headers(extra)
        if body is not None:
            data = json.dumps(body).encode("utf-8")
            hdr["Content-Type"] = "application/json"
        req = urllib.request.Request(self.url + path, data=data, method=method, headers=hdr)
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                content = r.read()
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:300]
            sys.exit(f"请求失败 {method} {path}：{e.code} {detail}")
        except urllib.error.URLError as e:
            sys.exit(f"无法连接 {self.url}：{e.reason}")
        if raw:
            return content
        return json.loads(content.decode("utf-8")) if content else None

    def rows(self, project, status, limit):
        q = {"project": f"eq.{project}", "order": "created_at.asc", "limit": str(limit), "select": "*"}
        if status != "all":
            q["status"] = f"eq.{status}"
        return self.request("GET", f"/rest/v1/{self.table}?" + urllib.parse.urlencode(q)) or []

    def download(self, path):
        return self.request("GET", f"/storage/v1/object/{self.bucket}/" + urllib.parse.quote(path), raw=True)

    def update(self, fid, patch):
        q = urllib.parse.urlencode({"id": f"eq.{fid}"})
        return self.request("PATCH", f"/rest/v1/{self.table}?{q}", patch, {"Prefer": "return=representation"})


def default_project():
    try:
        js = open(os.path.join(ROOT, "web", "Survival.Web", "wwwroot", "js", "app.js"), encoding="utf-8").read()
        i = js.find("project: '")
        if i >= 0:
            return js[i + 10: js.find("'", i + 10)]
    except OSError:
        pass
    return "shizheshengcun"


def short(row):
    return row["id"][:8]


def local_time(row):
    """把服务器的 UTC 时间换成本机时间。"""
    raw = row["created_at"].replace("Z", "+00:00")
    for candidate in (raw, raw[:19] + raw[-6:]):
        try:
            return datetime.datetime.fromisoformat(candidate).astimezone()
        except ValueError:
            continue
    return None


def stamp(row):
    t = local_time(row)
    return t.strftime("%Y-%m-%d %H:%M") if t else row["created_at"][:16].replace("T", " ")


def folder_for(row):
    t = local_time(row)
    s = t.strftime("%Y%m%d-%H%M") if t else row["created_at"][:16].replace("-", "").replace(":", "").replace("T", "-")
    return os.path.join(INBOX, f"{s}-{short(row)}")


def render_md(row):
    ctx = row.get("context") or {}
    env = ctx.get("env") or {}
    app = ctx.get("app") or {}
    lines = [
        f"# 留言 {short(row)} · {CATEGORY.get(row['category'], row['category'])}",
        "",
        f"- 时间：{stamp(row)}（本机时间）",
        f"- 状态：{row['status']}" + (f"（备注：{row['note']}）" if row.get("note") else ""),
        f"- 称呼：{row.get('contact') or '未留'}",
        f"- 版本：{row.get('app_version') or '未知'}",
        f"- 完整编号：{row['id']}",
        "",
        "## 留言",
        "",
        row.get("message") or "（无文字，见截图）",
        "",
    ]
    shots = row.get("screenshots") or []
    if shots:
        lines += ["## 截图", ""] + [f"- {os.path.basename(p)}" for p in shots] + [""]
    lines += [
        "## 环境",
        "",
        f"- 系统：{env.get('os', '?')}；浏览器：{env.get('browser', '?')}",
        f"- 窗口 {env.get('viewport', '?')}，屏幕 {env.get('screen', '?')}，缩放 ×{env.get('pixelRatio', '?')}，触屏点数 {env.get('touchPoints', '?')}",
        f"- 语言 {env.get('language', '?')}，时区 {env.get('timezone', '?')}，玩家本地时间 {env.get('localTime', '?')}",
        f"- 页面 {env.get('url', '?')}，构建 {env.get('appBuild') or '?'}",
        "",
    ]
    if app:
        lines += ["## 游戏状态", "", "```", app.get("summary", ""), "```", ""]
        for key, title in (("carried", "随身"), ("ground", "当前地面")):
            if app.get(key):
                lines.append(f"- {title}：{'、'.join(app[key])}")
        if app.get("fires"):
            lines.append(f"- 火堆与设施：{json.dumps(app['fires'], ensure_ascii=False)}")
        if app.get("flags"):
            lines.append(f"- 已达成标记：{'、'.join(app['flags'])}")
        if app.get("notice"):
            lines.append(f"- 界面提示：{app['notice']}")
        if app.get("recentLog"):
            lines += ["", "### 最近日志", ""] + [f"- {l}" for l in app["recentLog"]]
        lines.append("")
    if row.get("attachment"):
        lines += [f"附件 {row.get('attachment_name') or 'attachment.txt'} 已保存在本目录；存档可在游戏中通过修改浏览器 localStorage 的 survival.save.v1 载入复现。", ""]
    return "\n".join(lines)


def cmd_list(api, a):
    rows = api.rows(a.project, a.status, a.limit)
    if not rows:
        print("没有留言。")
        return
    for r in rows:
        msg = (r.get("message") or "").replace("\n", " ")
        shots = len(r.get("screenshots") or [])
        print(f"{short(r)}  {stamp(r)}  [{r['status']}] {CATEGORY.get(r['category'], r['category'])}  {r.get('contact') or '-'}  图{shots}  {msg[:60]}")
    print(f"共 {len(rows)} 条。")


def cmd_pull(api, a):
    rows = api.rows(a.project, a.status, a.limit)
    os.makedirs(INBOX, exist_ok=True)
    for r in rows:
        d = folder_for(r)
        os.makedirs(d, exist_ok=True)
        for p in r.get("screenshots") or []:
            target = os.path.join(d, os.path.basename(p))
            if not os.path.exists(target):
                with open(target, "wb") as f:
                    f.write(api.download(p))
        if r.get("attachment"):
            with open(os.path.join(d, r.get("attachment_name") or "attachment.txt"), "w", encoding="utf-8") as f:
                f.write(r["attachment"])
        with open(os.path.join(d, "context.json"), "w", encoding="utf-8") as f:
            json.dump(r.get("context"), f, ensure_ascii=False, indent=2)
        with open(os.path.join(d, "feedback.md"), "w", encoding="utf-8") as f:
            f.write(render_md(r))
        print(f"{short(r)}  {stamp(r)}  {CATEGORY.get(r['category'], r['category'])}  → {os.path.relpath(d, ROOT)}")
    print(f"已下载 {len(rows)} 条到 {os.path.relpath(INBOX, ROOT)}/。")


def cmd_mark(api, a):
    if a.status not in STATUS:
        sys.exit(f"状态只能是 {', '.join(STATUS)}")
    rows = [r for r in api.rows(a.project, "all", 1000) if r["id"].startswith(a.id)]
    if len(rows) != 1:
        sys.exit(f"编号 {a.id} 匹配到 {len(rows)} 条，请给出更长的编号")
    patch = {"status": a.status}
    if a.note is not None:
        patch["note"] = a.note
    api.update(rows[0]["id"], patch)
    print(f"{short(rows[0])} → {a.status}" + (f"（{a.note}）" if a.note else ""))


def cmd_ping(api, a):
    rows = api.rows(a.project, "all", 1)
    print(f"连接正常：{api.url}，项目 {a.project}，可读取留言（样本 {len(rows)} 条）。")


def main():
    p = argparse.ArgumentParser(description="留言板收件工具")
    p.add_argument("--project", default=default_project())
    sub = p.add_subparsers(dest="cmd", required=True)
    for name in ("list", "pull"):
        s = sub.add_parser(name)
        s.add_argument("--status", default="new", help="new / doing / done / wontfix / all")
        s.add_argument("--limit", type=int, default=100)
    m = sub.add_parser("mark")
    m.add_argument("id")
    m.add_argument("status")
    m.add_argument("--note")
    sub.add_parser("ping")
    a = p.parse_args()
    api = Api()
    {"list": cmd_list, "pull": cmd_pull, "mark": cmd_mark, "ping": cmd_ping}[a.cmd](api, a)


if __name__ == "__main__":
    main()
