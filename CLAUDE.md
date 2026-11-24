# 适者生存项目规则（Claude Code）

## 子代理

- 本项目的子代理**固定用订阅的 Agent 工具**派（Opus 5.5 高：`model: "opus"`、`effort: "high"`），**绝对不允许用 claude 命令行**（`claude -p` 加令牌那种派法）派活，不看桌面"子代理开关"处于哪一档。2026-11-24 用户定。
- 本机钩子 `C:/Users/xujie/.claude/subagent-mode/hook.mjs` 对本仓库目录强制执行这条：在本仓库里 Agent 调用按订阅档放行，claude 命令行调用一律拦下。
- 别的项目的"用 claude 命令行加令牌"规矩（如 Ligma）不适用于本项目，也不要拿那边的令牌来这里用。
