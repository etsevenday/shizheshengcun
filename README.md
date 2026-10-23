# 适者生存

末日里的单人卡牌生存原型：探索地点、搜集物资、烧水做饭、搭棚过夜，撑过七天。

**在线试玩：** https://etsevenday.github.io/shizheshengcun/

- 用电脑浏览器打开，建议窗口不小于 1280×720。
- 首次载入需要下载约 10MB 运行时，之后会缓存。
- 存档保存在本浏览器里，换浏览器或清除网站数据会丢失。
- 快捷键：M 地图，C 手工制作，R 休息，L 行动日志，F 反馈，Esc 关闭当前面板。
- 遇到问题按 F 留言：会自动附上当前画面、系统环境和游戏状态，截图可以直接 Ctrl+V 粘贴。

## 这是什么

依据 `doc/` 下的策划案实现的可玩原型：

| 文档 | 内容 |
| --- | --- |
| 基础系统 v0.2（地图、物资搜集、基础求生） | 规则与数值的唯一来源 |
| UI 策划案 v0.1 | 界面布局（A 卡牌桌面）、交互状态机与验收用例 |
| 实施方案 v0.1 | 工程结构、内核接口、开发中补充的规则假设（第 9 节） |
| 开发计划 v0.1 | 里程碑与已确认决策 |
| 试玩记录 v0.1 | 界面验收结果与发布前检查 |
| 场景图替换说明 | B 方案地点场景的出图规格、安全区与替换步骤 |
| 留言板接入说明 | 反馈组件、Supabase 配置步骤、用 AI 处理留言的流程 |
| 试玩评价与改进建议 v0.1 | 一次完整试玩的数据、与策划案的逐条对照、已修复项与待拍板建议 |

## 工程结构

```
data/config.v0.4.json      全部数值配置（策划 v0.2 加交互与地图扩展 v0.3，数值不变）
src/Core/                  纯 C# 内核，不依赖 UnityEngine；同时是一个 Unity 本地包
web/Survival.Core/         .NET 类库，编译 src/Core 的源码
web/Survival.Core.Tests/   NUnit：M01–M09、G01–G08、S01–S12 验收用例与七日通关脚本
web/Survival.Web/          Blazor WebAssembly 网页壳（含通用留言组件 wwwroot/js/feedback-widget.js）
tools/                     留言板建表脚本与收件工具 feedback.py
```

将来接 Unity UGUI：在 Unity 工程的 `Packages/manifest.json` 里加一行本地包引用即可复用同一份内核。

```json
"com.survival.core": "file:../../src/Core"
```

## 本地运行

需要 .NET 8 SDK。

```bash
dotnet test web/Survival.sln
```

```bash
dotnet run --project web/Survival.Web --launch-profile http
```

然后打开 http://localhost:5177 。推送到 main 后，GitHub Actions 会跑测试并自动发布到 Pages。
