# 主界面美术资源 v0.8

2026-10-28。运行时映射在 `Services/ArtAssets.cs`；完整坐标、尺寸、字节数和 SHA-256 见 `manifest.json`。

## 制作与接入

参考：`doc/策划案/主界面美术换肤 v0.8/assets/主界面风格参考.png`。使用 ImageGen 按该参考制作位图。场景与物品均不包含界面文字、数字或按钮；所有游戏信息仍由 Razor 组件根据真实状态渲染。没有把参考整屏当作游戏背景。

- 8 张 1536×1024 场景：camp、field、woods、stream、village、hardware、scrapyard、clinic。
- 5 张 1254×1254 透明图集：supplies、tools、equipment、creatures、resources；每张 4×4，共 80 个图位，另有 fire 别名。覆盖当前配置全部 46 个物品。
- paper.svg、iron.svg：代码绘制的低对比纹理。HUD 的 8 个小图标由 VitalIcon.razor 绘制，保持缩放清晰。
- 物品通过 SVG 内层 viewport 裁切，外层适应不同卡片尺寸；内层显式设置 overflow，避免旧的全局 SVG 样式使相邻图位漏出。没有复制 80 张解码后的大图。
- 未知未来场景保留原配置路径，未收录的未来图标保留原有 fallback。当前已知物品不存在缺图键。

## 生成提示词集合（制作约束）

场景共同约束：横向 3:2 游戏背景；参考图的废土漫画风格；粗粝黑色墨线、交叉排线、冷蓝黑阴影、低饱和赭金暖光；有空间深度；无人物、无 UI、无文字、无数字、无边框。

| 文件 | 场景内容 |
| --- | --- |
| camp.png | 废弃村落边缘的临时营地、帐篷与简易火堆、杂草、碎石 |
| field.png | 荒废农田、泥土小径、倒伏围栏、村舍与电线杆 |
| woods.png | 浓密树林边缘的小径、粗树干、灌木、石块与倒木 |
| stream.png | 石质浅溪、破旧木桥、远处村舍与水面暖光 |
| village.png | 废弃村庄街道、老屋、破损墙面、电线杆 |
| hardware.png | 废弃五金店内部、工具货架、旧柜台、从破窗照入的暖光 |
| scrapyard.png | 乡镇废料场、锈铁堆、齿轮与管道、拆解卡车、铁皮棚 |
| clinic.png | 废弃乡镇诊所、药柜、铁制诊疗床、破墙、药瓶与窗外暖光 |

图集共同约束：单张透明背景生产素材，4 列×4 行、等大图位，逐项独立居中并留安全边距；厚黑轮廓、手绘排线、磨损材质、低饱和暖纸色反光；没有网格线、标签、数字、UI 或文字。图位顺序按 manifest.json 的 cell 从 0 开始逐行读取。

| 图集 | 逐行图位内容 |
| --- | --- |
| supplies | 木材、石块、野果、纤维 / 净水、脏水、罐头、斧 / 金属锅、打火器、绷带、石刀 / 布料、生块根、烤块根、水煮块根 |
| tools | 草药、废金属、零件、棚 / 床垫、燃烧火堆、熄灭火堆、拳 / 维护工具、厨房刀、撬棍、木矛 / 金属矛、木弓、弩、手枪 |
| equipment | 霰弹枪、步枪、箭、弩矢 / 手枪弹、霰弹、步枪弹、生肉 / 兽皮、烤肉、水煮肉、未鉴定白色活性物质 / 抗生素、特效药、药物前体、试剂 |
| creatures | 游荡尸、爬行尸、奔跑尸、工人尸 / 防暴尸、嚎叫尸、野狗、狼 / 野猪、雄鹿、狗尸、狼尸 / 野猪尸、鹿尸、背包、木箱 |
| resources | 块根、水源、草药、废铁堆 / 零件箱、药柜、布料袋、锅具箱 / 工具箱、物资箱、杂物箱、五金武器箱 / 村庄武器柜、诊所药品、冷藏箱、倒木 |

诊所/废料场最终生成使用的完整共同提示：

> Create a landscape 3:2 game background illustration. [场景内容] Match attached game's visual art direction closely: bold rough black ink, intricate crosshatching, worn printed graphic-novel illustration, cold blue-black shadows and muted ochre evening daylight. Cinematic depth, no people, NO UI, NO buttons, NO lettering, NO border. Important playable scene background, full bleed. Distinct from every other location. Produce one polished finished background.

其中诊所内容：`Abandoned small rural medical clinic interior, empty pharmacy cabinets, cracked plaster, a metal examination bed, dusty medicine bottles, scattered papers with NO legible text, a window casting pale golden light.`

废料场内容：`Abandoned outdoor scrapyard in a Chinese rural town, heaps of rusty iron, gears and metal pipes, a dismantled truck and tin shed, distant power poles, path through piles of scrap.`

## 原始生成文件（本机留存）

目录：`C:/Users/270KP/.codex/generated_images/01a18183-efb1-7d32-873f-dca33a5ada61/`。原图未删除。

| 接入文件 | 原始文件 |
| --- | --- |
| camp | exec-1c8b77d1-a468-4a7b-885d-65462047ce94.png |
| field | exec-016734b8-076a-4430-a68d-2fac75212b9e.png |
| woods | exec-c72c807f-f79f-412a-a7bd-aa5697cfbbc2.png |
| stream | exec-44378322-d895-45bb-b60e-a0e4b90bfbb6.png |
| village | exec-79913656-e096-467f-a858-d796464fe818.png |
| hardware | exec-9546072d-3eb8-4161-a0ac-4fff9fdc504f.png |
| scrapyard | exec-e8176111-6dbc-4476-a3f1-2e6dce86061b.png |
| clinic | exec-80485eb0-72e4-4c9f-a370-cc3bd8a28251.png |
| supplies | exec-4136e029-1df6-4f50-9b76-0ce0e9125adb.png |
| tools | exec-3cab6cd4-7569-4bbb-b799-626b24c72b9f.png |
| equipment | exec-5e1f08e9-6dc0-47d9-bb42-d972602f7b9c.png |
| creatures | exec-246cdeb5-78e1-4ad1-a0d7-635d118b2f9c.png |
| resources | exec-01656523-933b-46ad-a90c-639652b00b93.png |
