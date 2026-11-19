# 可重放试玩场景（tools/playtest）

`run.mjs` 一个进程跑完一组场景：本机 Chrome 无头运行，每个「场景 × 分辨率」用独立的浏览器上下文（存档互不干扰），默认 3 个任务并行。零依赖，只要 Node 18+ 和 Chrome／Edge。

| 文件 | 用途 |
| --- | --- |
| `run.mjs` | 场景执行器（本文件说明） |
| `png.mjs` | 零依赖 PNG 读写与逐像素对比 |
| `cdp.mjs` | 旧的单步驱动（代理一步一条命令探索用，保留） |
| `serve.mjs` | 本地 Release 包静态服务器 |
| `scenarios/*.json` | 场景；`scenarios/_lib/*.json` 是可复用的步骤片段 |
| `fixtures/*.json` | 存档局面（localStorage 快照）；`fixtures/build/*.json` 是造它们的场景 |
| `out/` | 输出（已 gitignore）：`out/baseline/` 截图基线，`out/runs/<时间>/` 每次结果 |

## 跑

```bash
dotnet publish web/Survival.Web -c Release -o publish/local
```

```bash
node tools/playtest/serve.mjs start
```

```bash
node tools/playtest/run.mjs
```

- 不带参数跑 `scenarios/` 下全部（下划线开头的不算）；也可写文件名或名字：`run.mjs b53a-discard smoke-panels`。
- 常用选项：`--base <地址>`（默认用 `serve.mjs start` 为本仓库 publish/local/wwwroot 记下的实际地址，没有就 http://localhost:5199）、`--sizes 1280x720`（只跑一档）、`--jobs 3`、`--keep-going`（某步失败后继续）、`--headful`（看着它跑）、`--update-baseline`（界面有意改动后重置截图基线）。
- 退出码：全部通过 0，否则 1。控制台最后打印摘要；同样内容写在 `out/runs/<时间>/summary.txt`，完整记录在 `results.json`。
- 端口：Chrome 调试口默认 9350（`PLAYTEST_RUN_PORT` 可改），与 `cdp.mjs` 的 9333 不冲突，可以同时用。

## 每一步后自动检查

| 检查 | 级别 | 说明 |
| --- | --- | --- |
| 控制台错误、未捕获异常、资源加载失败、Blazor 错误条 | 错 | 场景可写 `"allowErrors": ["正则"]` 放过已知的 |
| 横向溢出 | 错 | 元素超出视口左右边，且没有被一个在视口内的滚动／裁切容器兜住 |
| 关键元素不可见 | 错 | 场景 `watch` 里列的选择器：存在时必须完整在视口内、中心没被别的元素盖住（默认只看第一个匹配；`{"sel": "…", "all": true}` 看全部）。对白遮罩盖着时不查 |
| 点击目标被盖住 | 错 | 点击前先把目标滚进视口；滚完中心仍被别的元素挡住就记一条（这时真实点击会落在挡住它的元素上） |
| 字号过小 | 警 | 视口内可见文字字号 < `minFont`（默认 12px） |
| 文字被截断 | 警 | `overflow: hidden` 的文字内容比可见宽度长、又没有省略号 |
| 截图与基线不同 | 警 | `shot` 步与 `out/baseline/` 逐像素比，变化比例 > `threshold`（默认 0.2%）时出差异图（变化处红色） |

同一问题在一个任务里只记一条，带「首见第几步、共几次」。

## 场景文件

```json
{
  "desc": "一句话说明＋来源（开发记录哪一节）",
  "batch": "B53a",
  "fixture": "discard-kit",
  "watch": ["pt=discard", "pt=discard-confirm"],
  "steps": [
    { "use": "continue" },
    { "click": "pt=shelf-ground >> pt=card-berry", "label": "选地面野果" },
    { "expect": "pt=discard", "inView": true },
    { "key": "Space" },
    { "expect": "pt=shelf-ground >> pt=card-berry", "text": "×8" },
    { "shot": "berry-sub" }
  ]
}
```

顶层可选：`fixture`（起点存档，不写就是空存档开 `url`）、`url`（默认 `/?dev=1`）、`sizes`、`watch`、`minFont`、`allowErrors`、`shotThreshold`、`autoDialogue`（默认 true，见下）、`checks: false`（关掉自动检查）。

### 选择器

用 ` >> ` 串起来逐层缩小范围，每段是下面之一：

- `pt=名字` → `[data-pt="名字"]`。**首选**：界面上专门给场景用的稳定标记，只加属性、不影响行为和样式。
- 现成的 `data-*`／`id`（CSS）：如 `#fac-kiln`、`#mn-field`、`[data-recipe=build_kiln]`、`[data-station-take]`、`[data-remains^=dev_b53b_recovery]`、`[aria-label=展开成员]`。
- `text=子串`／`text==全文`：按可见文字找（空白折成一个空格），取包含它的最小元素，落在按钮里就取按钮。**文案会被文案策划改，只在没有别的办法时用**（开发入口按钮的文字不归文案策划，可以用）。

已加的 `data-pt`：`shelf-ground`／`shelf-carried`（地面／随身卡片区）、`card-{物品id}`（物品卡，锅是 `card-pot`）、`discard`／`discard-water`／`discard-sub`／`discard-qty`／`discard-all`／`discard-cancel`／`discard-minus`／`discard-plus`／`discard-next`／`discard-back`／`discard-confirm`（丢弃子区）、`row-discard`／`row-discard-water`（人物页清单行末丢弃）、`map-depart`／`map-next`／`map-abort`（地图路线区）、`city-depart`／`city-next`（城市层）、`craft-start`（开始制作／建造）、`station-load`（设施投料）、`start-new`／`start-new-confirm`（标题页）。需要新的就在 Razor 上加 `data-pt="…"`；注意有些网页接线测试按整段源码字符串断言，属性插进被断言的那段会让测试变红，换个位置（放在被断言的片段之外）或改用现成标记。

### 步骤

| 步骤 | 写法 | 说明 |
| --- | --- | --- |
| open | `{"open": "/?dev=1"}` | 接在 `--base` 后面 |
| reload | `{"reload": true}` | |
| size | `{"size": "1280x720"}` | 中途改视口 |
| loadSave | `{"loadSave": "new-game"}` | 清空 `survival.*` 再写入 fixture，然后开 `url`（停在标题页） |
| saveFixture | `{"saveFixture": "名字", "note": "说明"}` | 只在加了 `--write-fixtures` 时写 `fixtures/名字.json` |
| click／dblclick／rclick | `{"click": "选择器", "nth": 0}` | 等它出现并可用（最多 `timeout` 毫秒，默认 5000），滚进视口后按真实鼠标点中心 |
| hover | `{"hover": "选择器"}` 或 `[x, y]` | |
| drag | `{"drag": "选择器", "to": "选择器"}` | 坐标也行 |
| wheel | `{"wheel": "选择器", "dy": 300, "shift": false}` | |
| key | `{"key": "Escape", "repeat": 1}` | 支持 `ctrl+s` 这类组合 |
| type | `{"type": "文字"}` | |
| select | `{"select": "选择器", "value": "h02_square"}` | 下拉框，按 value（找不到再按显示文字） |
| wait／waitGone | `{"wait": 500}`、`{"wait": "选择器"}`、`{"waitGone": "text=行动进行中", "timeout": 90000}` | 等出现／消失 |
| expect | `{"expect": "选择器", "text": "…", "textIs": "…", "count": 2, "absent": true, "disabled": false, "inView": true}` | 条件不满足时在 `timeout`（默认 4000）内重试；`inView` 不滚动、要求完整可见且没被盖住 |
| expectText／expectNoText | `{"expectText": ["…", "…"], "in": "选择器"}` | 页面（或范围内）文字包含／不包含 |
| eval | `{"eval": "JS 表达式", "equals": 值}` | 不写 `equals` 时要求为真；`"noAssert": true` 只记录值 |
| shot | `{"shot": "名字", "mask": ["选择器"], "clip": "选择器", "threshold": 0.002, "settle": 400}` | 截图并与基线比；`mask` 把会变的区域藏掉（如存档时间），`clip` 只截某个元素 |
| dump | `{"dump": "名字"}` | 把可交互元素（含 `data-pt`、坐标、禁用／视口外／被盖住）和页面文字写到 `dumps/`，写场景时找选择器用 |
| note | `{"note": "…"}` | 只是分段说明 |
| use | `{"use": "continue"}` | 展开 `scenarios/_lib/continue.json` 的步骤 |

每一步都可加：`label`（结果里显示的名字）、`optional: true`（失败只记录不中断）、`sizes: ["1280x720"]`（只在这些分辨率跑）、`checks: false`、`while: "选择器"`／`until: "选择器"`（重复这一步直到那个元素消失／出现，最多 `max` 次）。

**对白**：剧情对白会随各种首次行为插进来。场景默认在每个动作步前后把对白按空格读完（结果里记 `autoDialogueLines`）。要测对白本身写 `"autoDialogue": false`，或在那一步写 `"dialogue": true`。

### 步骤片段（`scenarios/_lib/`）

| 名字 | 做什么 |
| --- | --- |
| `new-game` | 标题页新游戏、读完开场对白 |
| `continue` | 标题页继续游戏 → 读取存档 → 读完对白（读 fixture 后用） |
| `dlg` | 有对白就读完 |
| `save-to-title` | Esc 到游戏菜单 →「保存并返回标题」。自动存档每 5 分钟才写一次，读档往返、造 fixture 前都要先这样存 |

## 存档 fixture

| 名字 | 局面 |
| --- | --- |
| `new-game` | 新局，开场对白读完，人在临时营地 |
| `stations-built` | 临时营地已建陶窑与固定净水台（空闲），制造 3 级 |
| `discard-kit` | 新局＋B53a 丢弃试用组：地面野果两批、锅（净水 1000＋生肉）；随身壶水 750、1 分钟后到期的野果 |
| `discard-list` | 新局＋B53b 清单丢弃试用组：同伴林昭随身三样、临时营地四种箱 |
| `drying-rack` | 临时营地有晾制架，工位 1 晾制果干中；随身一份腐败物 |
| `city-square` | 营区登记已记，人在营区广场（首次到城已处理） |

重造（按文件名顺序串行跑，后面的以前面的为起点）：

```bash
node tools/playtest/run.mjs tools/playtest/fixtures/build --jobs 1 --write-fixtures
```

- fixture 是 localStorage 快照（自动存档槽＋偏好），不含多页签占用标记。存档带校验和，不要手改；要新局面就写一个 `fixtures/build/NN-名字.json`。
- 存档升版本后旧 fixture 照样能读（读档时按迁移升级），不必每批重造；重造后新局随机数不同，截图基线要跟着 `--update-baseline`。

## 截图基线

- 第一次跑某个 `shot` 时直接存为基线（`out/baseline/<场景>/<分辨率>/<名字>.png`），之后逐像素比。截图前会冻结动画与过渡、模拟「减少动态效果」、藏掉键盘焦点框（出现时机不稳定）、把鼠标挪到左上角，并等 400ms。
- 界面有意改动后：看一眼 `out/runs/<时间>/diffs/` 的差异图确认是预期的，再 `--update-baseline` 跑一遍。
- 截图里有真实时间等每次不同的东西，用 `mask` 藏掉；按钮有短暂防连点锁的，`settle` 加长。

## 写场景的经验

- 一条路线一个场景，按「批次＋主题」命名（`b53a-discard.json`），`desc` 写来源（开发记录哪一节）。坏了先看 `summary.txt` 的失败步与 `fail/` 截图，多半是选择器要改，不用重写整条路线。
- 断言写「玩家能看到的结果」：数量变化、日志句、按钮可用与否、`inView`。只点不断言的场景会假绿。
- 写新场景时先用 `dump` 看页面上有哪些元素、哪些带 `data-pt`。

## 2026-11-19 调整

- `serve.mjs start` 不写端口时，5199 被别的程序（或另一份包）占着会自动换下一个空闲端口并打印实际地址；以前只看“能访问”就说已在运行，可能拿别的包去试。服务响应带 `X-Survival-Root` 头，用来认出是不是同一目录。
- `expectText`／`expectNoText`／`expect … text` 只认玩家看得见的文字：`.sr-only` 这类只给读屏、渲染成 ≤1px 的文字不算。
- `shot` 的 `mask` 截图后按 `data-pt-masked` 标记恢复被隐藏的元素。
