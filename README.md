# 适者生存

末日里的单人卡牌生存原型：探索地点、清理地块、搜集物资、烧水做饭、搭棚过夜，撑过七天。卡片可以拖动：整理、拾取放下、木材投火堆、水倒进锅、锅和生块根放进火堆——火堆随世界时间自动烧水、烤制、水煮。

**在线试玩：** https://etsevenday.github.io/shizheshengcun/

- 用电脑浏览器打开，建议窗口不小于 1280×720。
- 首次载入需要下载约 10MB 运行时，之后会缓存。
- 存档保存在本浏览器里，换浏览器或清除网站数据会丢失。
- 快捷键：M 地图，C 手工制作，R 休息，L 行动日志，F 反馈，Esc 关闭当前面板。
- 遇到问题按 F 留言：会自动附上当前画面、系统环境和游戏状态，截图可以直接 Ctrl+V 粘贴。

## 这是什么

依据 `doc/策划案/` 下的策划案实现的可玩原型：

| 文档 | 内容 |
| --- | --- |
| 基础系统 v0.2（地图、物资搜集、基础求生） | 规则与数值的唯一来源 |
| 交互与地图扩展 v0.3（开发接入、卡片拖拽与容器、地图清理、探索随机收获） | 拖拽、清理与探索收获的规则与数值来源，覆盖关系见其中 00 号文件 |
| 卡片统一与设施烹饪 v0.4（需求裁定、统一卡片与次级菜单 UI、设施内容物与烤制水煮、增量配置） | 本版新增规则与数值的来源：地块卡片桌面、火堆与锅的真实内容物、自动烧水/烤制/水煮、水煮块根 |
| UI 策划案 v0.1 | 界面布局（A 卡牌桌面）、交互状态机与验收用例 |
| 实施方案 v0.1 | 工程结构、内核接口、开发中补充的规则假设（第 9 节） |
| 开发计划 v0.1 | 里程碑与已确认决策 |
| 开发计划 v0.2 交互与地图扩展 | v0.3 策划案的工程映射、M9–M16 里程碑、开发假设与已确认的 B1–B5 |
| 开发计划 v0.3 卡片统一与设施烹饪 | v0.4 策划案的工程映射、M17–M24 里程碑、开发假设、实施记录、验收结论与待确认问题 |
| 试玩记录 v0.1 | 界面验收结果与发布前检查 |
| 场景图替换说明 | 地点场景图（地块区弱化背景与地图卡主图）的出图规格、安全区与替换步骤 |
| 留言板接入说明 | 反馈组件、Supabase 配置步骤、用 AI 处理留言的流程 |
| 试玩评价与改进建议 v0.1 | 一次完整试玩的数据、与策划案的逐条对照、已修复项与待拍板建议 |

## 工程结构

```
data/config.v0.4.json      全部数值配置（策划 v0.2 加交互与地图扩展 v0.3，再加卡片统一与设施烹饪 v0.4 的增量）
src/Core/                  纯 C# 内核，不依赖 UnityEngine；同时是一个 Unity 本地包
web/Survival.Core/         .NET 类库，编译 src/Core 的源码
web/Survival.Core.Tests/   NUnit：M、G、S 验收用例，v0.3 的 D、C、E 用例，v0.4 的 P401–P421，旧存档迁移（0.2 与 schema 3）与七日通关脚本
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

## 资源缺失清单

界面图片目前全部是同画风的 SVG 占位，等美术替换（详见 doc/开发计划 v0.3 卡片统一与设施烹饪.md 第 8 节）：

| 资源 | 现在用的占位 | 位置 |
| --- | --- | --- |
| 8 张地点主图（地图卡） | 场景底图 scenes/&lt;地点ID&gt;.svg 裁切 | web/Survival.Web/wwwroot/scenes/ |
| 火堆燃烧 / 熄灭 | fire_lit、fire_out | web/Survival.Web/Services/Icons.cs |
| 遮蔽棚、床垫（已建 / 未建） | shelter、bed；未建为虚线框灰化 + “未建造”角标 | 同上 |
| 水煮块根 | boiled_root | 同上 |
| 空加工位框、统一缺图占位 | slot_empty、placeholder | 同上 |
| 其余物品图标 | UI 预览稿提取或补画的 SVG | Icons.Extracted.cs、Icons.cs |

程序请求了却没有图的键会记入 `Icons.Missing` 并显示统一占位图，不会出现破图；当前版本没有缺图。
