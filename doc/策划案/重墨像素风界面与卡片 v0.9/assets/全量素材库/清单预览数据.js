window.VisualCatalog={
  "schemaVersion": 1,
  "project": "适者生存",
  "scope": "现有策划及运行配置的视觉绑定。素材候选与界面样板，需实际游戏验收。",
  "config": "data/config.v0.6.json",
  "configHash": "6ce0dc4ebe021039ba165a2a8626ee8a02ec6b4793567d4c3fcda531aecca512",
  "builtAt": "2026-11-09",
  "counts": {
    "item": 180,
    "gear": 35,
    "weapon": 39,
    "skillFamily": 15,
    "skill": 75,
    "action": 12,
    "condition": 3,
    "status": 14,
    "enemy": 42,
    "carcassRule": 6,
    "actor": 25,
    "duty": 16,
    "recruitTemplate": 10,
    "location": 143,
    "dungeon": 8,
    "region": 13,
    "faction": 3,
    "gate": 26,
    "resource": 169,
    "facility": 19,
    "publicStation": 6,
    "process": 9,
    "recipe": 133,
    "knowledge": 66,
    "document": 5,
    "quest": 41,
    "knowledgePack": 4,
    "service": 19,
    "affix": 18,
    "dialogueLine": 383,
    "dialogueSet": 270,
    "glyph": 67,
    "frame": 27,
    "texture": 5,
    "component": 74
  },
  "identity": {
    "fields": [
      "skinTone",
      "hairStyle",
      "hairColor",
      "faceDetail"
    ],
    "skinTones": [
      "浅肤色",
      "中浅肤色",
      "中等肤色",
      "中深肤色",
      "深肤色"
    ],
    "hairStyles": [
      "短发",
      "齐耳发",
      "齐肩发",
      "束发",
      "卷发",
      "剃短发"
    ],
    "hairColors": [
      "黑色",
      "深棕色",
      "浅棕色",
      "灰白色"
    ],
    "faceDetails": [
      "无明显特征",
      "眉尾浅疤",
      "面颊浅疤",
      "雀斑",
      "左眉缺口",
      "眼角细纹"
    ]
  },
  "fonts": [
    {
      "id": "fusion_8px",
      "size": 8,
      "zh": "fonts/8/fusion-pixel-8px-proportional-zh_hans.otf.woff2",
      "latin": "fonts/8/fusion-pixel-8px-proportional-latin.otf.woff2",
      "license": "fonts/8/OFL.txt",
      "notices": "fonts/8/LICENSES/",
      "zhSha256": "5d91d10373bd03d921332b35b0f629bb6fdfacfb4213e7d235e606dfb0680e96",
      "latinSha256": "bcda1aa7eba3e15fe1418d6fce209f6063183ad4c91c7d4357e7a0569e1646bd",
      "release": "https://github.com/TakWolf/fusion-pixel-font/releases/tag/2026.10.25",
      "version": "2026.10.25"
    },
    {
      "id": "fusion_10px",
      "size": 10,
      "zh": "fonts/10/fusion-pixel-10px-proportional-zh_hans.otf.woff2",
      "latin": "fonts/10/fusion-pixel-10px-proportional-latin.otf.woff2",
      "license": "fonts/10/OFL.txt",
      "notices": "fonts/10/LICENSES/",
      "zhSha256": "37c5b5f15a84fda5f41f8592970cb0dbb7d2bb3b0d004c18006375dc36803c12",
      "latinSha256": "d4921f5693ef043e52594de41f8fc2387765db8bdf6a0d1e5575dd723698ef05",
      "release": "https://github.com/TakWolf/fusion-pixel-font/releases/tag/2026.10.25",
      "version": "2026.10.25"
    },
    {
      "id": "fusion_12px",
      "size": 12,
      "zh": "fonts/12/fusion-pixel-12px-proportional-zh_hans.otf.woff2",
      "latin": "fonts/12/fusion-pixel-12px-proportional-latin.otf.woff2",
      "license": "fonts/12/OFL.txt",
      "notices": "fonts/12/LICENSES/",
      "zhSha256": "b67eb7aa592622e5bc1152b800d32404bb887a1e3a2e669b0c2a873d388590d6",
      "latinSha256": "ae0b97b103598d39abc597bcd2ad9d3eac96575bfc86886c0b42539675ce9037",
      "release": "https://github.com/TakWolf/fusion-pixel-font/releases/tag/2026.10.25",
      "version": "2026.10.25"
    }
  ],
  "visualStates": [
    "默认",
    "悬停",
    "焦点",
    "选中",
    "不可用",
    "危险",
    "执行",
    "可放入",
    "不可放入"
  ],
  "artKeyRules": {
    "runtime": "保留配置 ID 与身份。卡片、状态、文字与美术各层独立。",
    "presentation": [
      "关闭场景层不改位置",
      "关闭主体层不清数据",
      "关闭状态层保留控件",
      "动态名字与说明由现有控件渲染"
    ],
    "final": "位图和像素图均待正式美术复审、发布授权记录与游戏验收。地点按地貌分组共享。"
  },
  "assets": [
    {
      "key": "item:dirty_water",
      "category": "item",
      "id": "dirty_water",
      "name": "脏水",
      "asset": {
        "file": "svg/item/dirty_water.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "water",
      "shape": "bottle",
      "native": {
        "file": "svg/item/dirty_water.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:clean_water",
      "category": "item",
      "id": "clean_water",
      "name": "净水",
      "asset": {
        "file": "../UI分层样件/art/item-clean-water.png",
        "format": "png",
        "alpha": true
      },
      "kind": "water",
      "shape": "bottle",
      "native": {
        "file": "svg/item/clean_water.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:canned_food",
      "category": "item",
      "id": "canned_food",
      "name": "罐头",
      "asset": {
        "file": "svg/item/canned_food.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "can",
      "native": {
        "file": "svg/item/canned_food.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:berry",
      "category": "item",
      "id": "berry",
      "name": "野果",
      "asset": {
        "file": "svg/item/berry.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "berry",
      "native": {
        "file": "svg/item/berry.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:raw_root",
      "category": "item",
      "id": "raw_root",
      "name": "生块根",
      "asset": {
        "file": "svg/item/raw_root.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "root",
      "native": {
        "file": "svg/item/raw_root.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:cooked_root",
      "category": "item",
      "id": "cooked_root",
      "name": "烤熟块根",
      "asset": {
        "file": "svg/item/cooked_root.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "root",
      "native": {
        "file": "svg/item/cooked_root.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:boiled_root",
      "category": "item",
      "id": "boiled_root",
      "name": "水煮块根",
      "asset": {
        "file": "svg/item/boiled_root.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "root",
      "native": {
        "file": "svg/item/boiled_root.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:wood",
      "category": "item",
      "id": "wood",
      "name": "木材",
      "asset": {
        "file": "svg/item/wood.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "plank",
      "native": {
        "file": "svg/item/wood.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:fiber",
      "category": "item",
      "id": "fiber",
      "name": "植物纤维",
      "asset": {
        "file": "svg/item/fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rope",
      "native": {
        "file": "svg/item/fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:stone",
      "category": "item",
      "id": "stone",
      "name": "石块",
      "asset": {
        "file": "svg/item/stone.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rock",
      "native": {
        "file": "svg/item/stone.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:scrap",
      "category": "item",
      "id": "scrap",
      "name": "废金属",
      "asset": {
        "file": "svg/item/scrap.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "scrap",
      "native": {
        "file": "svg/item/scrap.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:cloth",
      "category": "item",
      "id": "cloth",
      "name": "布料",
      "asset": {
        "file": "svg/item/cloth.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "cloth",
      "native": {
        "file": "svg/item/cloth.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:parts",
      "category": "item",
      "id": "parts",
      "name": "零件",
      "asset": {
        "file": "svg/item/parts.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "part",
      "native": {
        "file": "svg/item/parts.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:herb",
      "category": "item",
      "id": "herb",
      "name": "草药",
      "asset": {
        "file": "svg/item/herb.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "berry",
      "native": {
        "file": "svg/item/herb.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:bandage",
      "category": "item",
      "id": "bandage",
      "name": "草药绷带",
      "asset": {
        "file": "svg/item/bandage.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "patch",
      "native": {
        "file": "svg/item/bandage.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:stone_knife",
      "category": "item",
      "id": "stone_knife",
      "name": "石刀",
      "asset": {
        "file": "svg/item/stone_knife.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "tool",
      "shape": "blade",
      "native": {
        "file": "svg/item/stone_knife.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:hatchet",
      "category": "item",
      "id": "hatchet",
      "name": "简易斧",
      "asset": {
        "file": "svg/item/hatchet.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "tool",
      "shape": "axe",
      "native": {
        "file": "svg/item/hatchet.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:pot",
      "category": "item",
      "id": "pot",
      "name": "金属锅",
      "asset": {
        "file": "svg/item/pot.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "tool",
      "shape": "pot",
      "native": {
        "file": "svg/item/pot.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:lighter",
      "category": "item",
      "id": "lighter",
      "name": "打火器",
      "asset": {
        "file": "svg/item/lighter.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "tool",
      "shape": "firekit",
      "native": {
        "file": "svg/item/lighter.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:kitchen_knife",
      "category": "item",
      "id": "kitchen_knife",
      "name": "厨房刀",
      "asset": {
        "file": "svg/item/kitchen_knife.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "blade",
      "native": {
        "file": "svg/item/kitchen_knife.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:crowbar",
      "category": "item",
      "id": "crowbar",
      "name": "撬棍",
      "asset": {
        "file": "svg/item/crowbar.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "club",
      "native": {
        "file": "svg/item/crowbar.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:wooden_spear",
      "category": "item",
      "id": "wooden_spear",
      "name": "木矛",
      "asset": {
        "file": "svg/item/wooden_spear.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "spear",
      "native": {
        "file": "svg/item/wooden_spear.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:metal_spear",
      "category": "item",
      "id": "metal_spear",
      "name": "金属矛",
      "asset": {
        "file": "svg/item/metal_spear.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "spear",
      "native": {
        "file": "svg/item/metal_spear.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:wooden_bow",
      "category": "item",
      "id": "wooden_bow",
      "name": "木弓",
      "asset": {
        "file": "svg/item/wooden_bow.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "bow",
      "native": {
        "file": "svg/item/wooden_bow.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:crossbow",
      "category": "item",
      "id": "crossbow",
      "name": "弩",
      "asset": {
        "file": "svg/item/crossbow.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "crossbow",
      "native": {
        "file": "svg/item/crossbow.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:pistol",
      "category": "item",
      "id": "pistol",
      "name": "手枪",
      "asset": {
        "file": "svg/item/pistol.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "pistol",
      "native": {
        "file": "svg/item/pistol.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:shotgun",
      "category": "item",
      "id": "shotgun",
      "name": "霰弹枪",
      "asset": {
        "file": "svg/item/shotgun.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "rifle",
      "native": {
        "file": "svg/item/shotgun.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rifle",
      "category": "item",
      "id": "rifle",
      "name": "步枪",
      "asset": {
        "file": "svg/item/rifle.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "rifle",
      "native": {
        "file": "svg/item/rifle.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:arrow",
      "category": "item",
      "id": "arrow",
      "name": "箭矢",
      "asset": {
        "file": "svg/item/arrow.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "ammo",
      "shape": "spear",
      "native": {
        "file": "svg/item/arrow.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:bolt",
      "category": "item",
      "id": "bolt",
      "name": "弩矢",
      "asset": {
        "file": "svg/item/bolt.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "ammo",
      "shape": "spear",
      "native": {
        "file": "svg/item/bolt.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:ammo_9mm",
      "category": "item",
      "id": "ammo_9mm",
      "name": "9mm弹",
      "asset": {
        "file": "svg/item/ammo_9mm.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "ammo",
      "shape": "cartridge",
      "native": {
        "file": "svg/item/ammo_9mm.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:shell_12g",
      "category": "item",
      "id": "shell_12g",
      "name": "霰弹",
      "asset": {
        "file": "svg/item/shell_12g.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "ammo",
      "shape": "cartridge",
      "native": {
        "file": "svg/item/shell_12g.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:ammo_rifle",
      "category": "item",
      "id": "ammo_rifle",
      "name": "步枪弹",
      "asset": {
        "file": "svg/item/ammo_rifle.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "ammo",
      "shape": "cartridge",
      "native": {
        "file": "svg/item/ammo_rifle.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:carcass_feral_dog",
      "category": "item",
      "id": "carcass_feral_dog",
      "name": "野犬尸体",
      "asset": {
        "file": "svg/item/carcass_feral_dog.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "carcass",
      "shape": "carcass",
      "native": {
        "file": "svg/item/carcass_feral_dog.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:carcass_wolf",
      "category": "item",
      "id": "carcass_wolf",
      "name": "狼尸体",
      "asset": {
        "file": "svg/item/carcass_wolf.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "carcass",
      "shape": "carcass",
      "native": {
        "file": "svg/item/carcass_wolf.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:carcass_boar",
      "category": "item",
      "id": "carcass_boar",
      "name": "野猪尸体",
      "asset": {
        "file": "svg/item/carcass_boar.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "carcass",
      "shape": "carcass",
      "native": {
        "file": "svg/item/carcass_boar.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:carcass_stag",
      "category": "item",
      "id": "carcass_stag",
      "name": "鹿尸体",
      "asset": {
        "file": "svg/item/carcass_stag.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "carcass",
      "shape": "carcass",
      "native": {
        "file": "svg/item/carcass_stag.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:raw_meat",
      "category": "item",
      "id": "raw_meat",
      "name": "生肉",
      "asset": {
        "file": "svg/item/raw_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "meat",
      "native": {
        "file": "svg/item/raw_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:hide",
      "category": "item",
      "id": "hide",
      "name": "兽皮",
      "asset": {
        "file": "svg/item/hide.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "cloth",
      "native": {
        "file": "svg/item/hide.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:cooked_meat",
      "category": "item",
      "id": "cooked_meat",
      "name": "烤肉",
      "asset": {
        "file": "svg/item/cooked_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "meat",
      "native": {
        "file": "svg/item/cooked_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:boiled_meat",
      "category": "item",
      "id": "boiled_meat",
      "name": "水煮肉",
      "asset": {
        "file": "svg/item/boiled_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "meat",
      "native": {
        "file": "svg/item/boiled_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:antibiotics",
      "category": "item",
      "id": "antibiotics",
      "name": "抗生素",
      "asset": {
        "file": "svg/item/antibiotics.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "vial",
      "native": {
        "file": "svg/item/antibiotics.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:antiviral_cure",
      "category": "item",
      "id": "antiviral_cure",
      "name": "病毒特效药",
      "asset": {
        "file": "svg/item/antiviral_cure.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "vial",
      "native": {
        "file": "svg/item/antiviral_cure.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:antiviral_precursor",
      "category": "item",
      "id": "antiviral_precursor",
      "name": "抗病毒前体",
      "asset": {
        "file": "svg/item/antiviral_precursor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "vial",
      "native": {
        "file": "svg/item/antiviral_precursor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:lab_reagent",
      "category": "item",
      "id": "lab_reagent",
      "name": "实验室试剂",
      "asset": {
        "file": "svg/item/lab_reagent.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "vial",
      "native": {
        "file": "svg/item/lab_reagent.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:active_substance_white_unidentified",
      "category": "item",
      "id": "active_substance_white_unidentified",
      "name": "未鉴定的活性物质",
      "asset": {
        "file": "svg/item/active_substance_white_unidentified.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "substance",
      "shape": "crystal",
      "native": {
        "file": "svg/item/active_substance_white_unidentified.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:plank",
      "category": "item",
      "id": "plank",
      "name": "木板",
      "asset": {
        "file": "svg/item/plank.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "plank",
      "native": {
        "file": "svg/item/plank.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rope",
      "category": "item",
      "id": "rope",
      "name": "绳索",
      "asset": {
        "file": "svg/item/rope.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rope",
      "native": {
        "file": "svg/item/rope.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:iron_plate",
      "category": "item",
      "id": "iron_plate",
      "name": "铁片",
      "asset": {
        "file": "svg/item/iron_plate.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "sheet",
      "native": {
        "file": "svg/item/iron_plate.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:steel",
      "category": "item",
      "id": "steel",
      "name": "钢材",
      "asset": {
        "file": "svg/item/steel.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "scrap",
      "native": {
        "file": "svg/item/steel.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:fastener",
      "category": "item",
      "id": "fastener",
      "name": "紧固件",
      "asset": {
        "file": "svg/item/fastener.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "part",
      "native": {
        "file": "svg/item/fastener.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:leather",
      "category": "item",
      "id": "leather",
      "name": "皮革",
      "asset": {
        "file": "svg/item/leather.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "cloth",
      "native": {
        "file": "svg/item/leather.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:thread",
      "category": "item",
      "id": "thread",
      "name": "缝线",
      "asset": {
        "file": "svg/item/thread.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rope",
      "native": {
        "file": "svg/item/thread.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rubber",
      "category": "item",
      "id": "rubber",
      "name": "橡胶",
      "asset": {
        "file": "svg/item/rubber.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "sheet",
      "native": {
        "file": "svg/item/rubber.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:plastic",
      "category": "item",
      "id": "plastic",
      "name": "塑料",
      "asset": {
        "file": "svg/item/plastic.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "sheet",
      "native": {
        "file": "svg/item/plastic.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:wire",
      "category": "item",
      "id": "wire",
      "name": "导线",
      "asset": {
        "file": "svg/item/wire.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rope",
      "native": {
        "file": "svg/item/wire.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:electronics",
      "category": "item",
      "id": "electronics",
      "name": "电子组件",
      "asset": {
        "file": "svg/item/electronics.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "chip",
      "native": {
        "file": "svg/item/electronics.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:battery",
      "category": "item",
      "id": "battery",
      "name": "电池",
      "asset": {
        "file": "svg/item/battery.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "battery",
      "native": {
        "file": "svg/item/battery.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:adhesive",
      "category": "item",
      "id": "adhesive",
      "name": "黏合剂",
      "asset": {
        "file": "svg/item/adhesive.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "vial",
      "native": {
        "file": "svg/item/adhesive.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:oil",
      "category": "item",
      "id": "oil",
      "name": "机油",
      "asset": {
        "file": "svg/item/oil.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "bottle",
      "native": {
        "file": "svg/item/oil.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:charcoal",
      "category": "item",
      "id": "charcoal",
      "name": "木炭",
      "asset": {
        "file": "svg/item/charcoal.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rock",
      "native": {
        "file": "svg/item/charcoal.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:precision_parts",
      "category": "item",
      "id": "precision_parts",
      "name": "精密零件",
      "asset": {
        "file": "svg/item/precision_parts.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "part",
      "native": {
        "file": "svg/item/precision_parts.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:weapon_parts",
      "category": "item",
      "id": "weapon_parts",
      "name": "武器组件",
      "asset": {
        "file": "svg/item/weapon_parts.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "part",
      "native": {
        "file": "svg/item/weapon_parts.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:ballistic_fiber",
      "category": "item",
      "id": "ballistic_fiber",
      "name": "防护纤维",
      "asset": {
        "file": "svg/item/ballistic_fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "cloth",
      "native": {
        "file": "svg/item/ballistic_fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:medical_gauze",
      "category": "item",
      "id": "medical_gauze",
      "name": "医用纱布",
      "asset": {
        "file": "svg/item/medical_gauze.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "cloth",
      "native": {
        "file": "svg/item/medical_gauze.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:disinfectant",
      "category": "item",
      "id": "disinfectant",
      "name": "消毒液",
      "asset": {
        "file": "svg/item/disinfectant.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "bottle",
      "native": {
        "file": "svg/item/disinfectant.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:saline",
      "category": "item",
      "id": "saline",
      "name": "盐水剂",
      "asset": {
        "file": "svg/item/saline.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "bottle",
      "native": {
        "file": "svg/item/saline.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:repair_kit",
      "category": "item",
      "id": "repair_kit",
      "name": "维修包",
      "asset": {
        "file": "svg/item/repair_kit.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "crate",
      "native": {
        "file": "svg/item/repair_kit.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:gun_service_kit",
      "category": "item",
      "id": "gun_service_kit",
      "name": "枪械养护包",
      "asset": {
        "file": "svg/item/gun_service_kit.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "crate",
      "native": {
        "file": "svg/item/gun_service_kit.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:grain",
      "category": "item",
      "id": "grain",
      "name": "谷物",
      "asset": {
        "file": "svg/item/grain.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "sack",
      "native": {
        "file": "svg/item/grain.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:salt",
      "category": "item",
      "id": "salt",
      "name": "盐",
      "asset": {
        "file": "svg/item/salt.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "sachet",
      "native": {
        "file": "svg/item/salt.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:cooking_oil",
      "category": "item",
      "id": "cooking_oil",
      "name": "食用油",
      "asset": {
        "file": "svg/item/cooking_oil.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "bottle",
      "native": {
        "file": "svg/item/cooking_oil.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:canned_beans",
      "category": "item",
      "id": "canned_beans",
      "name": "豆罐头",
      "asset": {
        "file": "svg/item/canned_beans.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "can",
      "native": {
        "file": "svg/item/canned_beans.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:dried_meat",
      "category": "item",
      "id": "dried_meat",
      "name": "肉干",
      "asset": {
        "file": "svg/item/dried_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "strips",
      "native": {
        "file": "svg/item/dried_meat.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:iron_ore",
      "category": "item",
      "id": "iron_ore",
      "name": "铁矿料",
      "asset": {
        "file": "svg/item/iron_ore.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rock",
      "native": {
        "file": "svg/item/iron_ore.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:soap",
      "category": "item",
      "id": "soap",
      "name": "肥皂",
      "asset": {
        "file": "svg/item/soap.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "soap",
      "native": {
        "file": "svg/item/soap.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:filter_media",
      "category": "item",
      "id": "filter_media",
      "name": "滤材",
      "asset": {
        "file": "svg/item/filter_media.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "sachet",
      "native": {
        "file": "svg/item/filter_media.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:herb_extract",
      "category": "item",
      "id": "herb_extract",
      "name": "草药浸膏",
      "asset": {
        "file": "svg/item/herb_extract.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "vial",
      "native": {
        "file": "svg/item/herb_extract.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:metal_tube",
      "category": "item",
      "id": "metal_tube",
      "name": "金属管",
      "asset": {
        "file": "svg/item/metal_tube.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "tubes",
      "native": {
        "file": "svg/item/metal_tube.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:spring",
      "category": "item",
      "id": "spring",
      "name": "弹簧",
      "asset": {
        "file": "svg/item/spring.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "part",
      "native": {
        "file": "svg/item/spring.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:canvas",
      "category": "item",
      "id": "canvas",
      "name": "帆布",
      "asset": {
        "file": "svg/item/canvas.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "cloth",
      "native": {
        "file": "svg/item/canvas.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:root_porridge",
      "category": "item",
      "id": "root_porridge",
      "name": "块根粥",
      "asset": {
        "file": "svg/item/root_porridge.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "bowl",
      "native": {
        "file": "svg/item/root_porridge.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:meat_stew",
      "category": "item",
      "id": "meat_stew",
      "name": "炖肉",
      "asset": {
        "file": "svg/item/meat_stew.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "bowl",
      "native": {
        "file": "svg/item/meat_stew.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:grain_cake",
      "category": "item",
      "id": "grain_cake",
      "name": "谷饼",
      "asset": {
        "file": "svg/item/grain_cake.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "bread",
      "native": {
        "file": "svg/item/grain_cake.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:berry_jam",
      "category": "item",
      "id": "berry_jam",
      "name": "果酱",
      "asset": {
        "file": "svg/item/berry_jam.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "can",
      "native": {
        "file": "svg/item/berry_jam.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:dried_berries",
      "category": "item",
      "id": "dried_berries",
      "name": "果干",
      "asset": {
        "file": "svg/item/dried_berries.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "berry",
      "native": {
        "file": "svg/item/dried_berries.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:fried_root",
      "category": "item",
      "id": "fried_root",
      "name": "油煎块根",
      "asset": {
        "file": "svg/item/fried_root.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "root",
      "native": {
        "file": "svg/item/fried_root.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:trail_ration",
      "category": "item",
      "id": "trail_ration",
      "name": "行军口粮",
      "asset": {
        "file": "svg/item/trail_ration.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "sachet",
      "native": {
        "file": "svg/item/trail_ration.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:herbal_drink",
      "category": "item",
      "id": "herbal_drink",
      "name": "草药饮",
      "asset": {
        "file": "svg/item/herbal_drink.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "bottle",
      "native": {
        "file": "svg/item/herbal_drink.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:sterile_bandage",
      "category": "item",
      "id": "sterile_bandage",
      "name": "无菌绷带",
      "asset": {
        "file": "svg/item/sterile_bandage.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "patch",
      "native": {
        "file": "svg/item/sterile_bandage.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:compression_bandage",
      "category": "item",
      "id": "compression_bandage",
      "name": "加压绷带",
      "asset": {
        "file": "svg/item/compression_bandage.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "patch",
      "native": {
        "file": "svg/item/compression_bandage.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:first_aid_pack",
      "category": "item",
      "id": "first_aid_pack",
      "name": "急救包",
      "asset": {
        "file": "svg/item/first_aid_pack.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "medkit",
      "native": {
        "file": "svg/item/first_aid_pack.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:oral_rehydration",
      "category": "item",
      "id": "oral_rehydration",
      "name": "口服补液",
      "asset": {
        "file": "svg/item/oral_rehydration.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "bottle",
      "native": {
        "file": "svg/item/oral_rehydration.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:cleaning_wipe",
      "category": "item",
      "id": "cleaning_wipe",
      "name": "清洁巾",
      "asset": {
        "file": "svg/item/cleaning_wipe.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "cloth",
      "native": {
        "file": "svg/item/cleaning_wipe.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:water_filter",
      "category": "item",
      "id": "water_filter",
      "name": "便携滤水器",
      "asset": {
        "file": "svg/item/water_filter.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "canteen",
      "native": {
        "file": "svg/item/water_filter.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:hand_lamp",
      "category": "item",
      "id": "hand_lamp",
      "name": "手持灯",
      "asset": {
        "file": "svg/item/hand_lamp.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "lamp",
      "native": {
        "file": "svg/item/hand_lamp.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:fire_starter",
      "category": "item",
      "id": "fire_starter",
      "name": "引火包",
      "asset": {
        "file": "svg/item/fire_starter.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "firekit",
      "native": {
        "file": "svg/item/fire_starter.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:repair_patch",
      "category": "item",
      "id": "repair_patch",
      "name": "修补片",
      "asset": {
        "file": "svg/item/repair_patch.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "patch",
      "native": {
        "file": "svg/item/repair_patch.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:field_bedroll",
      "category": "item",
      "id": "field_bedroll",
      "name": "行旅铺垫",
      "asset": {
        "file": "svg/item/field_bedroll.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "bed",
      "native": {
        "file": "svg/item/field_bedroll.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:clay",
      "category": "item",
      "id": "clay",
      "name": "黏土",
      "asset": {
        "file": "svg/item/clay.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rock",
      "native": {
        "file": "svg/item/clay.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:ceramic_shell",
      "category": "item",
      "id": "ceramic_shell",
      "name": "陶壳",
      "asset": {
        "file": "svg/item/ceramic_shell.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "tubes",
      "native": {
        "file": "svg/item/ceramic_shell.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:purification_tablet",
      "category": "item",
      "id": "purification_tablet",
      "name": "净水片",
      "asset": {
        "file": "svg/item/purification_tablet.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "tablet",
      "native": {
        "file": "svg/item/purification_tablet.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:filter_cartridge",
      "category": "item",
      "id": "filter_cartridge",
      "name": "长效滤芯",
      "asset": {
        "file": "svg/item/filter_cartridge.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "tubes",
      "native": {
        "file": "svg/item/filter_cartridge.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:ration_canteen",
      "category": "item",
      "id": "ration_canteen",
      "name": "分量水壶",
      "asset": {
        "file": "svg/item/ration_canteen.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "canteen",
      "native": {
        "file": "svg/item/ration_canteen.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:hardtack",
      "category": "item",
      "id": "hardtack",
      "name": "远行硬饼",
      "asset": {
        "file": "svg/item/hardtack.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "bread",
      "native": {
        "file": "svg/item/hardtack.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:expedition_bar",
      "category": "item",
      "id": "expedition_bar",
      "name": "远征口粮棒",
      "asset": {
        "file": "svg/item/expedition_bar.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "sachet",
      "native": {
        "file": "svg/item/expedition_bar.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:stew_portion",
      "category": "item",
      "id": "stew_portion",
      "name": "谷肉炖食",
      "asset": {
        "file": "svg/item/stew_portion.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "food",
      "shape": "bowl",
      "native": {
        "file": "svg/item/stew_portion.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:field_tarp",
      "category": "item",
      "id": "field_tarp",
      "name": "野营遮雨布",
      "asset": {
        "file": "svg/item/field_tarp.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "tent",
      "native": {
        "file": "svg/item/field_tarp.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:folding_stove",
      "category": "item",
      "id": "folding_stove",
      "name": "折叠柴炉",
      "asset": {
        "file": "svg/item/folding_stove.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "stove",
      "native": {
        "file": "svg/item/folding_stove.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:field_patch",
      "category": "item",
      "id": "field_patch",
      "name": "野外修补包（白）",
      "asset": {
        "file": "svg/item/field_patch.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "patch",
      "native": {
        "file": "svg/item/field_patch.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:field_patch_green",
      "category": "item",
      "id": "field_patch_green",
      "name": "野外修补包（绿）",
      "asset": {
        "file": "svg/item/field_patch_green.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "patch",
      "native": {
        "file": "svg/item/field_patch_green.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:field_patch_blue",
      "category": "item",
      "id": "field_patch_blue",
      "name": "野外修补包（蓝）",
      "asset": {
        "file": "svg/item/field_patch_blue.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "patch",
      "native": {
        "file": "svg/item/field_patch_blue.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:cable_bundle",
      "category": "item",
      "id": "cable_bundle",
      "name": "绝缘线束",
      "asset": {
        "file": "svg/item/cable_bundle.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "rope",
      "native": {
        "file": "svg/item/cable_bundle.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:flash_marker",
      "category": "item",
      "id": "flash_marker",
      "name": "闪光标记器",
      "asset": {
        "file": "svg/item/flash_marker.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "lamp",
      "native": {
        "file": "svg/item/flash_marker.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:brace_pack",
      "category": "item",
      "id": "brace_pack",
      "name": "支撑护带包",
      "asset": {
        "file": "svg/item/brace_pack.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "patch",
      "native": {
        "file": "svg/item/brace_pack.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rally_patch",
      "category": "item",
      "id": "rally_patch",
      "name": "鼓舞提示贴",
      "asset": {
        "file": "svg/item/rally_patch.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "material",
      "shape": "patch",
      "native": {
        "file": "svg/item/rally_patch.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:antidiarrheal_powder",
      "category": "item",
      "id": "antidiarrheal_powder",
      "name": "止泻药粉",
      "asset": {
        "file": "svg/item/antidiarrheal_powder.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "tablet",
      "native": {
        "file": "svg/item/antidiarrheal_powder.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:emergency_tonic",
      "category": "item",
      "id": "emergency_tonic",
      "name": "应急恢复剂",
      "asset": {
        "file": "svg/item/emergency_tonic.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "medical",
      "shape": "bottle",
      "native": {
        "file": "svg/item/emergency_tonic.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:machete",
      "category": "item",
      "id": "machete",
      "name": "砍刀",
      "asset": {
        "file": "svg/item/machete.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "blade",
      "native": {
        "file": "svg/item/machete.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:guard_sword",
      "category": "item",
      "id": "guard_sword",
      "name": "守备短剑",
      "asset": {
        "file": "svg/item/guard_sword.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "blade",
      "native": {
        "file": "svg/item/guard_sword.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:fire_axe",
      "category": "item",
      "id": "fire_axe",
      "name": "消防斧",
      "asset": {
        "file": "svg/item/fire_axe.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "axe",
      "native": {
        "file": "svg/item/fire_axe.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:breach_axe",
      "category": "item",
      "id": "breach_axe",
      "name": "破障斧",
      "asset": {
        "file": "svg/item/breach_axe.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "axe",
      "native": {
        "file": "svg/item/breach_axe.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rescue_axe",
      "category": "item",
      "id": "rescue_axe",
      "name": "救援短斧",
      "asset": {
        "file": "svg/item/rescue_axe.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "axe",
      "native": {
        "file": "svg/item/rescue_axe.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:pike",
      "category": "item",
      "id": "pike",
      "name": "加固长枪",
      "asset": {
        "file": "svg/item/pike.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "spear",
      "native": {
        "file": "svg/item/pike.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:barbed_spear",
      "category": "item",
      "id": "barbed_spear",
      "name": "倒刺短矛",
      "asset": {
        "file": "svg/item/barbed_spear.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "spear",
      "native": {
        "file": "svg/item/barbed_spear.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:wooden_club",
      "category": "item",
      "id": "wooden_club",
      "name": "木棍",
      "asset": {
        "file": "svg/item/wooden_club.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "club",
      "native": {
        "file": "svg/item/wooden_club.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:steel_mace",
      "category": "item",
      "id": "steel_mace",
      "name": "钢制钉锤",
      "asset": {
        "file": "svg/item/steel_mace.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "club",
      "native": {
        "file": "svg/item/steel_mace.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:shock_baton",
      "category": "item",
      "id": "shock_baton",
      "name": "绝缘警棍",
      "asset": {
        "file": "svg/item/shock_baton.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "club",
      "native": {
        "file": "svg/item/shock_baton.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:recurve_bow",
      "category": "item",
      "id": "recurve_bow",
      "name": "复合木弓",
      "asset": {
        "file": "svg/item/recurve_bow.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "bow",
      "native": {
        "file": "svg/item/recurve_bow.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:hunter_bow",
      "category": "item",
      "id": "hunter_bow",
      "name": "猎手弓",
      "asset": {
        "file": "svg/item/hunter_bow.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "bow",
      "native": {
        "file": "svg/item/hunter_bow.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:repeater_crossbow",
      "category": "item",
      "id": "repeater_crossbow",
      "name": "轻型战术弩",
      "asset": {
        "file": "svg/item/repeater_crossbow.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "crossbow",
      "native": {
        "file": "svg/item/repeater_crossbow.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:service_pistol",
      "category": "item",
      "id": "service_pistol",
      "name": "勤务手枪",
      "asset": {
        "file": "svg/item/service_pistol.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "pistol",
      "native": {
        "file": "svg/item/service_pistol.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:marksman_rifle",
      "category": "item",
      "id": "marksman_rifle",
      "name": "精校步枪",
      "asset": {
        "file": "svg/item/marksman_rifle.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "rifle",
      "native": {
        "file": "svg/item/marksman_rifle.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:wooden_shield",
      "category": "item",
      "id": "wooden_shield",
      "name": "木盾",
      "asset": {
        "file": "svg/item/wooden_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "shield",
      "native": {
        "file": "svg/item/wooden_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:scrap_shield",
      "category": "item",
      "id": "scrap_shield",
      "name": "包铁盾",
      "asset": {
        "file": "svg/item/scrap_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "shield",
      "native": {
        "file": "svg/item/scrap_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:riot_shield",
      "category": "item",
      "id": "riot_shield",
      "name": "防暴盾",
      "asset": {
        "file": "svg/item/riot_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "shield",
      "native": {
        "file": "svg/item/riot_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rescue_shield",
      "category": "item",
      "id": "rescue_shield",
      "name": "救援盾",
      "asset": {
        "file": "svg/item/rescue_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "shield",
      "native": {
        "file": "svg/item/rescue_shield.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:signal_whistle",
      "category": "item",
      "id": "signal_whistle",
      "name": "信号哨",
      "asset": {
        "file": "svg/item/signal_whistle.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "radio",
      "native": {
        "file": "svg/item/signal_whistle.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:signal_flag",
      "category": "item",
      "id": "signal_flag",
      "name": "战术旗",
      "asset": {
        "file": "svg/item/signal_flag.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "flag",
      "native": {
        "file": "svg/item/signal_flag.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:field_radio",
      "category": "item",
      "id": "field_radio",
      "name": "战术电台",
      "asset": {
        "file": "svg/item/field_radio.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "radio",
      "native": {
        "file": "svg/item/field_radio.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:command_terminal",
      "category": "item",
      "id": "command_terminal",
      "name": "便携指挥终端",
      "asset": {
        "file": "svg/item/command_terminal.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "chip",
      "native": {
        "file": "svg/item/command_terminal.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:field_medkit",
      "category": "item",
      "id": "field_medkit",
      "name": "随行医疗包",
      "asset": {
        "file": "svg/item/field_medkit.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "medkit",
      "native": {
        "file": "svg/item/field_medkit.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:trauma_bag",
      "category": "item",
      "id": "trauma_bag",
      "name": "创伤救护包",
      "asset": {
        "file": "svg/item/trauma_bag.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "medkit",
      "native": {
        "file": "svg/item/trauma_bag.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rescue_medkit",
      "category": "item",
      "id": "rescue_medkit",
      "name": "救援医疗箱",
      "asset": {
        "file": "svg/item/rescue_medkit.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "medkit",
      "native": {
        "file": "svg/item/rescue_medkit.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:triage_kit",
      "category": "item",
      "id": "triage_kit",
      "name": "分诊器材组",
      "asset": {
        "file": "svg/item/triage_kit.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "weapon",
      "shape": "medkit",
      "native": {
        "file": "svg/item/triage_kit.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:padded_vest",
      "category": "item",
      "id": "padded_vest",
      "name": "衬垫护具",
      "asset": {
        "file": "svg/item/padded_vest.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/padded_vest.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:leather_armor",
      "category": "item",
      "id": "leather_armor",
      "name": "皮革护具",
      "asset": {
        "file": "svg/item/leather_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/leather_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:scrap_armor",
      "category": "item",
      "id": "scrap_armor",
      "name": "拼接铁甲",
      "asset": {
        "file": "svg/item/scrap_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/scrap_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:guard_armor",
      "category": "item",
      "id": "guard_armor",
      "name": "守备护甲",
      "asset": {
        "file": "svg/item/guard_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/guard_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:scout_armor",
      "category": "item",
      "id": "scout_armor",
      "name": "侦察护具",
      "asset": {
        "file": "svg/item/scout_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/scout_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:heavy_armor",
      "category": "item",
      "id": "heavy_armor",
      "name": "重型防护甲",
      "asset": {
        "file": "svg/item/heavy_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/heavy_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:medic_armor",
      "category": "item",
      "id": "medic_armor",
      "name": "救护防护背心",
      "asset": {
        "file": "svg/item/medic_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/medic_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:balanced_armor",
      "category": "item",
      "id": "balanced_armor",
      "name": "均衡战术甲",
      "asset": {
        "file": "svg/item/balanced_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/balanced_armor.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:steady_ring",
      "category": "item",
      "id": "steady_ring",
      "name": "稳手指环",
      "asset": {
        "file": "svg/item/steady_ring.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "ring",
      "native": {
        "file": "svg/item/steady_ring.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:swift_pendant",
      "category": "item",
      "id": "swift_pendant",
      "name": "轻捷吊坠",
      "asset": {
        "file": "svg/item/swift_pendant.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "badge",
      "native": {
        "file": "svg/item/swift_pendant.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:guard_badge",
      "category": "item",
      "id": "guard_badge",
      "name": "守护徽记",
      "asset": {
        "file": "svg/item/guard_badge.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "badge",
      "native": {
        "file": "svg/item/guard_badge.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:hunter_token",
      "category": "item",
      "id": "hunter_token",
      "name": "猎手护符",
      "asset": {
        "file": "svg/item/hunter_token.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "badge",
      "native": {
        "file": "svg/item/hunter_token.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:focus_lens",
      "category": "item",
      "id": "focus_lens",
      "name": "聚焦镜片",
      "asset": {
        "file": "svg/item/focus_lens.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "ring",
      "native": {
        "file": "svg/item/focus_lens.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:aid_badge",
      "category": "item",
      "id": "aid_badge",
      "name": "救护徽章",
      "asset": {
        "file": "svg/item/aid_badge.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "badge",
      "native": {
        "file": "svg/item/aid_badge.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:resolve_medal",
      "category": "item",
      "id": "resolve_medal",
      "name": "镇定勋章",
      "asset": {
        "file": "svg/item/resolve_medal.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "badge",
      "native": {
        "file": "svg/item/resolve_medal.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:tactics_watch",
      "category": "item",
      "id": "tactics_watch",
      "name": "战术计时表",
      "asset": {
        "file": "svg/item/tactics_watch.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "watch",
      "native": {
        "file": "svg/item/tactics_watch.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:knuckle_wraps",
      "category": "item",
      "id": "knuckle_wraps",
      "name": "护拳绑带",
      "asset": {
        "file": "svg/item/knuckle_wraps.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "glove",
      "native": {
        "file": "svg/item/knuckle_wraps.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:work_shirt",
      "category": "item",
      "id": "work_shirt",
      "name": "耐磨工作衣",
      "asset": {
        "file": "svg/item/work_shirt.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "coat",
      "native": {
        "file": "svg/item/work_shirt.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:forage_jacket",
      "category": "item",
      "id": "forage_jacket",
      "name": "采集外套",
      "asset": {
        "file": "svg/item/forage_jacket.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "coat",
      "native": {
        "file": "svg/item/forage_jacket.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:scavenge_vest",
      "category": "item",
      "id": "scavenge_vest",
      "name": "搜集背心",
      "asset": {
        "file": "svg/item/scavenge_vest.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "vest",
      "native": {
        "file": "svg/item/scavenge_vest.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:craft_coat",
      "category": "item",
      "id": "craft_coat",
      "name": "工匠围衣",
      "asset": {
        "file": "svg/item/craft_coat.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "coat",
      "native": {
        "file": "svg/item/craft_coat.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:rest_coat",
      "category": "item",
      "id": "rest_coat",
      "name": "休养外衣",
      "asset": {
        "file": "svg/item/rest_coat.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "coat",
      "native": {
        "file": "svg/item/rest_coat.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:expedition_coat",
      "category": "item",
      "id": "expedition_coat",
      "name": "远征上衣",
      "asset": {
        "file": "svg/item/expedition_coat.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "coat",
      "native": {
        "file": "svg/item/expedition_coat.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:work_trousers",
      "category": "item",
      "id": "work_trousers",
      "name": "工作长裤",
      "asset": {
        "file": "svg/item/work_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "pants",
      "native": {
        "file": "svg/item/work_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:trail_trousers",
      "category": "item",
      "id": "trail_trousers",
      "name": "行路裤",
      "asset": {
        "file": "svg/item/trail_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "pants",
      "native": {
        "file": "svg/item/trail_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:utility_trousers",
      "category": "item",
      "id": "utility_trousers",
      "name": "作业长裤",
      "asset": {
        "file": "svg/item/utility_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "pants",
      "native": {
        "file": "svg/item/utility_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:reinforced_trousers",
      "category": "item",
      "id": "reinforced_trousers",
      "name": "加固长裤",
      "asset": {
        "file": "svg/item/reinforced_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "pants",
      "native": {
        "file": "svg/item/reinforced_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:recovery_trousers",
      "category": "item",
      "id": "recovery_trousers",
      "name": "休养长裤",
      "asset": {
        "file": "svg/item/recovery_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "pants",
      "native": {
        "file": "svg/item/recovery_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:expedition_trousers",
      "category": "item",
      "id": "expedition_trousers",
      "name": "远征下衣",
      "asset": {
        "file": "svg/item/expedition_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "pants",
      "native": {
        "file": "svg/item/expedition_trousers.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:gather_gloves",
      "category": "item",
      "id": "gather_gloves",
      "name": "采集手套",
      "asset": {
        "file": "svg/item/gather_gloves.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "glove",
      "native": {
        "file": "svg/item/gather_gloves.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:route_compass",
      "category": "item",
      "id": "route_compass",
      "name": "路线罗盘",
      "asset": {
        "file": "svg/item/route_compass.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "compass",
      "native": {
        "file": "svg/item/route_compass.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:filter_token",
      "category": "item",
      "id": "filter_token",
      "name": "净水操作套件",
      "asset": {
        "file": "svg/item/filter_token.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "badge",
      "native": {
        "file": "svg/item/filter_token.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:repair_manual",
      "category": "item",
      "id": "repair_manual",
      "name": "维护手册",
      "asset": {
        "file": "svg/item/repair_manual.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "book",
      "native": {
        "file": "svg/item/repair_manual.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:medical_notes",
      "category": "item",
      "id": "medical_notes",
      "name": "护理笔记",
      "asset": {
        "file": "svg/item/medical_notes.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "book",
      "native": {
        "file": "svg/item/medical_notes.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "item:ration_pouch",
      "category": "item",
      "id": "ration_pouch",
      "name": "配给袋",
      "asset": {
        "file": "svg/item/ration_pouch.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "gear",
      "shape": "bag",
      "native": {
        "file": "svg/item/ration_pouch.svg",
        "format": "svg",
        "alpha": true
      },
      "display": "透明图标 · 32 格 / 常规显示 48–96 CSS px",
      "source": "data.items",
      "entry": "Icons.Markup(itemId) · ItemShelf / ItemDetail / CraftOverlay"
    },
    {
      "key": "gear:padded_vest",
      "category": "gear",
      "id": "padded_vest",
      "name": "衬垫护具",
      "asset": {
        "ref": "item:padded_vest"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:leather_armor",
      "category": "gear",
      "id": "leather_armor",
      "name": "皮革护具",
      "asset": {
        "ref": "item:leather_armor"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:scrap_armor",
      "category": "gear",
      "id": "scrap_armor",
      "name": "拼接铁甲",
      "asset": {
        "ref": "item:scrap_armor"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:guard_armor",
      "category": "gear",
      "id": "guard_armor",
      "name": "守备护甲",
      "asset": {
        "ref": "item:guard_armor"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:scout_armor",
      "category": "gear",
      "id": "scout_armor",
      "name": "侦察护具",
      "asset": {
        "ref": "item:scout_armor"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:heavy_armor",
      "category": "gear",
      "id": "heavy_armor",
      "name": "重型防护甲",
      "asset": {
        "ref": "item:heavy_armor"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:medic_armor",
      "category": "gear",
      "id": "medic_armor",
      "name": "救护防护背心",
      "asset": {
        "ref": "item:medic_armor"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:balanced_armor",
      "category": "gear",
      "id": "balanced_armor",
      "name": "均衡战术甲",
      "asset": {
        "ref": "item:balanced_armor"
      },
      "slot": "armor",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:steady_ring",
      "category": "gear",
      "id": "steady_ring",
      "name": "稳手指环",
      "asset": {
        "ref": "item:steady_ring"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:swift_pendant",
      "category": "gear",
      "id": "swift_pendant",
      "name": "轻捷吊坠",
      "asset": {
        "ref": "item:swift_pendant"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:guard_badge",
      "category": "gear",
      "id": "guard_badge",
      "name": "守护徽记",
      "asset": {
        "ref": "item:guard_badge"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:hunter_token",
      "category": "gear",
      "id": "hunter_token",
      "name": "猎手护符",
      "asset": {
        "ref": "item:hunter_token"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:focus_lens",
      "category": "gear",
      "id": "focus_lens",
      "name": "聚焦镜片",
      "asset": {
        "ref": "item:focus_lens"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:aid_badge",
      "category": "gear",
      "id": "aid_badge",
      "name": "救护徽章",
      "asset": {
        "ref": "item:aid_badge"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:resolve_medal",
      "category": "gear",
      "id": "resolve_medal",
      "name": "镇定勋章",
      "asset": {
        "ref": "item:resolve_medal"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:tactics_watch",
      "category": "gear",
      "id": "tactics_watch",
      "name": "战术计时表",
      "asset": {
        "ref": "item:tactics_watch"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:knuckle_wraps",
      "category": "gear",
      "id": "knuckle_wraps",
      "name": "护拳绑带",
      "asset": {
        "ref": "item:knuckle_wraps"
      },
      "slot": "combat_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:work_shirt",
      "category": "gear",
      "id": "work_shirt",
      "name": "耐磨工作衣",
      "asset": {
        "ref": "item:work_shirt"
      },
      "slot": "top",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:forage_jacket",
      "category": "gear",
      "id": "forage_jacket",
      "name": "采集外套",
      "asset": {
        "ref": "item:forage_jacket"
      },
      "slot": "top",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:scavenge_vest",
      "category": "gear",
      "id": "scavenge_vest",
      "name": "搜集背心",
      "asset": {
        "ref": "item:scavenge_vest"
      },
      "slot": "top",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:craft_coat",
      "category": "gear",
      "id": "craft_coat",
      "name": "工匠围衣",
      "asset": {
        "ref": "item:craft_coat"
      },
      "slot": "top",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:rest_coat",
      "category": "gear",
      "id": "rest_coat",
      "name": "休养外衣",
      "asset": {
        "ref": "item:rest_coat"
      },
      "slot": "top",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:expedition_coat",
      "category": "gear",
      "id": "expedition_coat",
      "name": "远征上衣",
      "asset": {
        "ref": "item:expedition_coat"
      },
      "slot": "top",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:work_trousers",
      "category": "gear",
      "id": "work_trousers",
      "name": "工作长裤",
      "asset": {
        "ref": "item:work_trousers"
      },
      "slot": "bottom",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:trail_trousers",
      "category": "gear",
      "id": "trail_trousers",
      "name": "行路裤",
      "asset": {
        "ref": "item:trail_trousers"
      },
      "slot": "bottom",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:utility_trousers",
      "category": "gear",
      "id": "utility_trousers",
      "name": "作业长裤",
      "asset": {
        "ref": "item:utility_trousers"
      },
      "slot": "bottom",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:reinforced_trousers",
      "category": "gear",
      "id": "reinforced_trousers",
      "name": "加固长裤",
      "asset": {
        "ref": "item:reinforced_trousers"
      },
      "slot": "bottom",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:recovery_trousers",
      "category": "gear",
      "id": "recovery_trousers",
      "name": "休养长裤",
      "asset": {
        "ref": "item:recovery_trousers"
      },
      "slot": "bottom",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:expedition_trousers",
      "category": "gear",
      "id": "expedition_trousers",
      "name": "远征下衣",
      "asset": {
        "ref": "item:expedition_trousers"
      },
      "slot": "bottom",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:gather_gloves",
      "category": "gear",
      "id": "gather_gloves",
      "name": "采集手套",
      "asset": {
        "ref": "item:gather_gloves"
      },
      "slot": "survival_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:route_compass",
      "category": "gear",
      "id": "route_compass",
      "name": "路线罗盘",
      "asset": {
        "ref": "item:route_compass"
      },
      "slot": "survival_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:filter_token",
      "category": "gear",
      "id": "filter_token",
      "name": "净水操作套件",
      "asset": {
        "ref": "item:filter_token"
      },
      "slot": "survival_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:repair_manual",
      "category": "gear",
      "id": "repair_manual",
      "name": "维护手册",
      "asset": {
        "ref": "item:repair_manual"
      },
      "slot": "survival_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:medical_notes",
      "category": "gear",
      "id": "medical_notes",
      "name": "护理笔记",
      "asset": {
        "ref": "item:medical_notes"
      },
      "slot": "survival_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "gear:ration_pouch",
      "category": "gear",
      "id": "ration_pouch",
      "name": "配给袋",
      "asset": {
        "ref": "item:ration_pouch"
      },
      "slot": "survival_acc",
      "source": "data.gear",
      "entry": "GearOverlay · BattleEquip"
    },
    {
      "key": "weapon:fists",
      "category": "weapon",
      "id": "fists",
      "name": "拳",
      "asset": {
        "file": "svg/weapon/fists.svg",
        "format": "svg",
        "alpha": true
      },
      "skillId": "unarmed",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:stone_knife",
      "category": "weapon",
      "id": "stone_knife",
      "name": "石刀",
      "asset": {
        "ref": "item:stone_knife"
      },
      "skillId": "blade",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:kitchen_knife",
      "category": "weapon",
      "id": "kitchen_knife",
      "name": "厨房刀",
      "asset": {
        "ref": "item:kitchen_knife"
      },
      "skillId": "blade",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:hatchet",
      "category": "weapon",
      "id": "hatchet",
      "name": "简易斧",
      "asset": {
        "ref": "item:hatchet"
      },
      "skillId": "axe",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:crowbar",
      "category": "weapon",
      "id": "crowbar",
      "name": "撬棍",
      "asset": {
        "ref": "item:crowbar"
      },
      "skillId": "blunt",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:wooden_spear",
      "category": "weapon",
      "id": "wooden_spear",
      "name": "木矛",
      "asset": {
        "ref": "item:wooden_spear"
      },
      "skillId": "spear",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:metal_spear",
      "category": "weapon",
      "id": "metal_spear",
      "name": "金属矛",
      "asset": {
        "ref": "item:metal_spear"
      },
      "skillId": "spear",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:wooden_bow",
      "category": "weapon",
      "id": "wooden_bow",
      "name": "木弓",
      "asset": {
        "ref": "item:wooden_bow"
      },
      "skillId": "archery",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:crossbow",
      "category": "weapon",
      "id": "crossbow",
      "name": "弩",
      "asset": {
        "ref": "item:crossbow"
      },
      "skillId": "archery",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:pistol",
      "category": "weapon",
      "id": "pistol",
      "name": "手枪",
      "asset": {
        "ref": "item:pistol"
      },
      "skillId": "firearms",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:shotgun",
      "category": "weapon",
      "id": "shotgun",
      "name": "霰弹枪",
      "asset": {
        "ref": "item:shotgun"
      },
      "skillId": "firearms",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:rifle",
      "category": "weapon",
      "id": "rifle",
      "name": "步枪",
      "asset": {
        "ref": "item:rifle"
      },
      "skillId": "firearms",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:machete",
      "category": "weapon",
      "id": "machete",
      "name": "砍刀",
      "asset": {
        "ref": "item:machete"
      },
      "skillId": "blade",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:guard_sword",
      "category": "weapon",
      "id": "guard_sword",
      "name": "守备短剑",
      "asset": {
        "ref": "item:guard_sword"
      },
      "skillId": "blade",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:fire_axe",
      "category": "weapon",
      "id": "fire_axe",
      "name": "消防斧",
      "asset": {
        "ref": "item:fire_axe"
      },
      "skillId": "axe",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:breach_axe",
      "category": "weapon",
      "id": "breach_axe",
      "name": "破障斧",
      "asset": {
        "ref": "item:breach_axe"
      },
      "skillId": "axe",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:rescue_axe",
      "category": "weapon",
      "id": "rescue_axe",
      "name": "救援短斧",
      "asset": {
        "ref": "item:rescue_axe"
      },
      "skillId": "axe",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:pike",
      "category": "weapon",
      "id": "pike",
      "name": "加固长枪",
      "asset": {
        "ref": "item:pike"
      },
      "skillId": "spear",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:barbed_spear",
      "category": "weapon",
      "id": "barbed_spear",
      "name": "倒刺短矛",
      "asset": {
        "ref": "item:barbed_spear"
      },
      "skillId": "spear",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:wooden_club",
      "category": "weapon",
      "id": "wooden_club",
      "name": "木棍",
      "asset": {
        "ref": "item:wooden_club"
      },
      "skillId": "blunt",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:steel_mace",
      "category": "weapon",
      "id": "steel_mace",
      "name": "钢制钉锤",
      "asset": {
        "ref": "item:steel_mace"
      },
      "skillId": "blunt",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:shock_baton",
      "category": "weapon",
      "id": "shock_baton",
      "name": "绝缘警棍",
      "asset": {
        "ref": "item:shock_baton"
      },
      "skillId": "blunt",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:recurve_bow",
      "category": "weapon",
      "id": "recurve_bow",
      "name": "复合木弓",
      "asset": {
        "ref": "item:recurve_bow"
      },
      "skillId": "archery",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:hunter_bow",
      "category": "weapon",
      "id": "hunter_bow",
      "name": "猎手弓",
      "asset": {
        "ref": "item:hunter_bow"
      },
      "skillId": "archery",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:repeater_crossbow",
      "category": "weapon",
      "id": "repeater_crossbow",
      "name": "轻型战术弩",
      "asset": {
        "ref": "item:repeater_crossbow"
      },
      "skillId": "archery",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:service_pistol",
      "category": "weapon",
      "id": "service_pistol",
      "name": "勤务手枪",
      "asset": {
        "ref": "item:service_pistol"
      },
      "skillId": "firearms",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:marksman_rifle",
      "category": "weapon",
      "id": "marksman_rifle",
      "name": "精校步枪",
      "asset": {
        "ref": "item:marksman_rifle"
      },
      "skillId": "firearms",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:wooden_shield",
      "category": "weapon",
      "id": "wooden_shield",
      "name": "木盾",
      "asset": {
        "ref": "item:wooden_shield"
      },
      "skillId": "shield",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:scrap_shield",
      "category": "weapon",
      "id": "scrap_shield",
      "name": "包铁盾",
      "asset": {
        "ref": "item:scrap_shield"
      },
      "skillId": "shield",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:riot_shield",
      "category": "weapon",
      "id": "riot_shield",
      "name": "防暴盾",
      "asset": {
        "ref": "item:riot_shield"
      },
      "skillId": "shield",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:rescue_shield",
      "category": "weapon",
      "id": "rescue_shield",
      "name": "救援盾",
      "asset": {
        "ref": "item:rescue_shield"
      },
      "skillId": "shield",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:signal_whistle",
      "category": "weapon",
      "id": "signal_whistle",
      "name": "信号哨",
      "asset": {
        "ref": "item:signal_whistle"
      },
      "skillId": "command",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:signal_flag",
      "category": "weapon",
      "id": "signal_flag",
      "name": "战术旗",
      "asset": {
        "ref": "item:signal_flag"
      },
      "skillId": "command",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:field_radio",
      "category": "weapon",
      "id": "field_radio",
      "name": "战术电台",
      "asset": {
        "ref": "item:field_radio"
      },
      "skillId": "command",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:command_terminal",
      "category": "weapon",
      "id": "command_terminal",
      "name": "便携指挥终端",
      "asset": {
        "ref": "item:command_terminal"
      },
      "skillId": "command",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:field_medkit",
      "category": "weapon",
      "id": "field_medkit",
      "name": "随行医疗包",
      "asset": {
        "ref": "item:field_medkit"
      },
      "skillId": "medicine",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:trauma_bag",
      "category": "weapon",
      "id": "trauma_bag",
      "name": "创伤救护包",
      "asset": {
        "ref": "item:trauma_bag"
      },
      "skillId": "medicine",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:rescue_medkit",
      "category": "weapon",
      "id": "rescue_medkit",
      "name": "救援医疗箱",
      "asset": {
        "ref": "item:rescue_medkit"
      },
      "skillId": "medicine",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "weapon:triage_kit",
      "category": "weapon",
      "id": "triage_kit",
      "name": "分诊器材组",
      "asset": {
        "ref": "item:triage_kit"
      },
      "skillId": "medicine",
      "source": "data.weapons",
      "entry": "GearOverlay · BattleScreen"
    },
    {
      "key": "skillFamily:unarmed",
      "category": "skillFamily",
      "id": "unarmed",
      "name": "拳",
      "asset": {
        "file": "svg/skill-family/unarmed.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:blade",
      "category": "skillFamily",
      "id": "blade",
      "name": "刀剑",
      "asset": {
        "file": "svg/skill-family/blade.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:axe",
      "category": "skillFamily",
      "id": "axe",
      "name": "斧",
      "asset": {
        "file": "svg/skill-family/axe.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:spear",
      "category": "skillFamily",
      "id": "spear",
      "name": "矛",
      "asset": {
        "file": "svg/skill-family/spear.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:blunt",
      "category": "skillFamily",
      "id": "blunt",
      "name": "钝器",
      "asset": {
        "file": "svg/skill-family/blunt.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:archery",
      "category": "skillFamily",
      "id": "archery",
      "name": "弓弩",
      "asset": {
        "file": "svg/skill-family/archery.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:firearms",
      "category": "skillFamily",
      "id": "firearms",
      "name": "枪械",
      "asset": {
        "file": "svg/skill-family/firearms.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:shield",
      "category": "skillFamily",
      "id": "shield",
      "name": "盾",
      "asset": {
        "file": "svg/skill-family/shield.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:command",
      "category": "skillFamily",
      "id": "command",
      "name": "指挥",
      "asset": {
        "file": "svg/skill-family/command.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:medicine",
      "category": "skillFamily",
      "id": "medicine",
      "name": "医疗",
      "asset": {
        "file": "svg/skill-family/medicine.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "both",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:crafting",
      "category": "skillFamily",
      "id": "crafting",
      "name": "制造",
      "asset": {
        "file": "svg/skill-family/crafting.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:gathering",
      "category": "skillFamily",
      "id": "gathering",
      "name": "采集",
      "asset": {
        "file": "svg/skill-family/gathering.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:scavenging",
      "category": "skillFamily",
      "id": "scavenging",
      "name": "搜集",
      "asset": {
        "file": "svg/skill-family/scavenging.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:cooking",
      "category": "skillFamily",
      "id": "cooking",
      "name": "烹饪",
      "asset": {
        "file": "svg/skill-family/cooking.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skillFamily:athletics",
      "category": "skillFamily",
      "id": "athletics",
      "name": "行旅",
      "asset": {
        "file": "svg/skill-family/athletics.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "source": "data.skills",
      "entry": "GrowthOverlay · CharacterPanel"
    },
    {
      "key": "skill:unarmed_01",
      "category": "skill",
      "id": "unarmed_01",
      "name": "拳击",
      "asset": {
        "file": "svg/skill/unarmed_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "unarmed",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:unarmed_03",
      "category": "skill",
      "id": "unarmed_03",
      "name": "推挡",
      "asset": {
        "file": "svg/skill/unarmed_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "unarmed",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:unarmed_05",
      "category": "skill",
      "id": "unarmed_05",
      "name": "护身",
      "asset": {
        "file": "svg/skill/unarmed_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "unarmed",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:unarmed_07",
      "category": "skill",
      "id": "unarmed_07",
      "name": "挣脱",
      "asset": {
        "file": "svg/skill/unarmed_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "unarmed",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:unarmed_10",
      "category": "skill",
      "id": "unarmed_10",
      "name": "连拳",
      "asset": {
        "file": "svg/skill/unarmed_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "unarmed",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blade_01",
      "category": "skill",
      "id": "blade_01",
      "name": "斩击",
      "asset": {
        "file": "svg/skill/blade_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blade",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blade_03",
      "category": "skill",
      "id": "blade_03",
      "name": "割伤",
      "asset": {
        "file": "svg/skill/blade_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blade",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blade_05",
      "category": "skill",
      "id": "blade_05",
      "name": "反击架势",
      "asset": {
        "file": "svg/skill/blade_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blade",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blade_07",
      "category": "skill",
      "id": "blade_07",
      "name": "突进斩",
      "asset": {
        "file": "svg/skill/blade_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blade",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blade_10",
      "category": "skill",
      "id": "blade_10",
      "name": "处决",
      "asset": {
        "file": "svg/skill/blade_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blade",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:axe_01",
      "category": "skill",
      "id": "axe_01",
      "name": "劈砍",
      "asset": {
        "file": "svg/skill/axe_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "axe",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:axe_03",
      "category": "skill",
      "id": "axe_03",
      "name": "破甲劈",
      "asset": {
        "file": "svg/skill/axe_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "axe",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:axe_05",
      "category": "skill",
      "id": "axe_05",
      "name": "蓄力",
      "asset": {
        "file": "svg/skill/axe_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "axe",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:axe_07",
      "category": "skill",
      "id": "axe_07",
      "name": "震退",
      "asset": {
        "file": "svg/skill/axe_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "axe",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:axe_10",
      "category": "skill",
      "id": "axe_10",
      "name": "破阵",
      "asset": {
        "file": "svg/skill/axe_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "axe",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:spear_01",
      "category": "skill",
      "id": "spear_01",
      "name": "刺击",
      "asset": {
        "file": "svg/skill/spear_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "spear",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:spear_03",
      "category": "skill",
      "id": "spear_03",
      "name": "逼退",
      "asset": {
        "file": "svg/skill/spear_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "spear",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:spear_05",
      "category": "skill",
      "id": "spear_05",
      "name": "架矛",
      "asset": {
        "file": "svg/skill/spear_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "spear",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:spear_07",
      "category": "skill",
      "id": "spear_07",
      "name": "贯穿",
      "asset": {
        "file": "svg/skill/spear_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "spear",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:spear_10",
      "category": "skill",
      "id": "spear_10",
      "name": "连刺",
      "asset": {
        "file": "svg/skill/spear_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "spear",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blunt_01",
      "category": "skill",
      "id": "blunt_01",
      "name": "敲击",
      "asset": {
        "file": "svg/skill/blunt_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blunt",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blunt_03",
      "category": "skill",
      "id": "blunt_03",
      "name": "震击",
      "asset": {
        "file": "svg/skill/blunt_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blunt",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blunt_05",
      "category": "skill",
      "id": "blunt_05",
      "name": "压制",
      "asset": {
        "file": "svg/skill/blunt_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blunt",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blunt_07",
      "category": "skill",
      "id": "blunt_07",
      "name": "打断",
      "asset": {
        "file": "svg/skill/blunt_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blunt",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:blunt_10",
      "category": "skill",
      "id": "blunt_10",
      "name": "重锤",
      "asset": {
        "file": "svg/skill/blunt_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "blunt",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:archery_01",
      "category": "skill",
      "id": "archery_01",
      "name": "射击",
      "asset": {
        "file": "svg/skill/archery_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "archery",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:archery_03",
      "category": "skill",
      "id": "archery_03",
      "name": "致残射击",
      "asset": {
        "file": "svg/skill/archery_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "archery",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:archery_05",
      "category": "skill",
      "id": "archery_05",
      "name": "猎标",
      "asset": {
        "file": "svg/skill/archery_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "archery",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:archery_07",
      "category": "skill",
      "id": "archery_07",
      "name": "穿透射击",
      "asset": {
        "file": "svg/skill/archery_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "archery",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:archery_10",
      "category": "skill",
      "id": "archery_10",
      "name": "连射",
      "asset": {
        "file": "svg/skill/archery_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "archery",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:firearms_01",
      "category": "skill",
      "id": "firearms_01",
      "name": "开火",
      "asset": {
        "file": "svg/skill/firearms_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "firearms",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:firearms_03",
      "category": "skill",
      "id": "firearms_03",
      "name": "火力压制",
      "asset": {
        "file": "svg/skill/firearms_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "firearms",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:firearms_05",
      "category": "skill",
      "id": "firearms_05",
      "name": "快速装填",
      "asset": {
        "file": "svg/skill/firearms_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "firearms",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:firearms_07",
      "category": "skill",
      "id": "firearms_07",
      "name": "专长射击",
      "asset": {
        "file": "svg/skill/firearms_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "firearms",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:firearms_10",
      "category": "skill",
      "id": "firearms_10",
      "name": "决胜射击",
      "asset": {
        "file": "svg/skill/firearms_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "firearms",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:shield_01",
      "category": "skill",
      "id": "shield_01",
      "name": "盾击",
      "asset": {
        "file": "svg/skill/shield_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "shield",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:shield_03",
      "category": "skill",
      "id": "shield_03",
      "name": "保护",
      "asset": {
        "file": "svg/skill/shield_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "shield",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:shield_05",
      "category": "skill",
      "id": "shield_05",
      "name": "稳固",
      "asset": {
        "file": "svg/skill/shield_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "shield",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:shield_07",
      "category": "skill",
      "id": "shield_07",
      "name": "掩护",
      "asset": {
        "file": "svg/skill/shield_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "shield",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:shield_10",
      "category": "skill",
      "id": "shield_10",
      "name": "坚守",
      "asset": {
        "file": "svg/skill/shield_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "shield",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:command_01",
      "category": "skill",
      "id": "command_01",
      "name": "鼓舞",
      "asset": {
        "file": "svg/skill/command_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "command",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:command_03",
      "category": "skill",
      "id": "command_03",
      "name": "整队",
      "asset": {
        "file": "svg/skill/command_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "command",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:command_05",
      "category": "skill",
      "id": "command_05",
      "name": "稳心",
      "asset": {
        "file": "svg/skill/command_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "command",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:command_07",
      "category": "skill",
      "id": "command_07",
      "name": "协同",
      "asset": {
        "file": "svg/skill/command_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "command",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:command_10",
      "category": "skill",
      "id": "command_10",
      "name": "总攻令",
      "asset": {
        "file": "svg/skill/command_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "command",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:medicine_01",
      "category": "skill",
      "id": "medicine_01",
      "name": "战地包扎",
      "asset": {
        "file": "svg/skill/medicine_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "medicine",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:medicine_03",
      "category": "skill",
      "id": "medicine_03",
      "name": "创伤处理",
      "asset": {
        "file": "svg/skill/medicine_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "medicine",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:medicine_05",
      "category": "skill",
      "id": "medicine_05",
      "name": "稳定伤势",
      "asset": {
        "file": "svg/skill/medicine_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "medicine",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:medicine_07",
      "category": "skill",
      "id": "medicine_07",
      "name": "应急护理",
      "asset": {
        "file": "svg/skill/medicine_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "medicine",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:medicine_10",
      "category": "skill",
      "id": "medicine_10",
      "name": "危急救护",
      "asset": {
        "file": "svg/skill/medicine_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "combat",
      "family": "medicine",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:crafting_01",
      "category": "skill",
      "id": "crafting_01",
      "name": "基础手作",
      "asset": {
        "file": "svg/skill/crafting_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "crafting",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:crafting_03",
      "category": "skill",
      "id": "crafting_03",
      "name": "工坊装配",
      "asset": {
        "file": "svg/skill/crafting_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "crafting",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:crafting_05",
      "category": "skill",
      "id": "crafting_05",
      "name": "精细加工",
      "asset": {
        "file": "svg/skill/crafting_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "crafting",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:crafting_07",
      "category": "skill",
      "id": "crafting_07",
      "name": "专业制造",
      "asset": {
        "file": "svg/skill/crafting_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "crafting",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:crafting_10",
      "category": "skill",
      "id": "crafting_10",
      "name": "大师装配",
      "asset": {
        "file": "svg/skill/crafting_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "crafting",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:gathering_01",
      "category": "skill",
      "id": "gathering_01",
      "name": "辨识采集",
      "asset": {
        "file": "svg/skill/gathering_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "gathering",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:gathering_03",
      "category": "skill",
      "id": "gathering_03",
      "name": "稳手采集",
      "asset": {
        "file": "svg/skill/gathering_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "gathering",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:gathering_05",
      "category": "skill",
      "id": "gathering_05",
      "name": "合理取材",
      "asset": {
        "file": "svg/skill/gathering_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "gathering",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:gathering_07",
      "category": "skill",
      "id": "gathering_07",
      "name": "熟练采伐",
      "asset": {
        "file": "svg/skill/gathering_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "gathering",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:gathering_10",
      "category": "skill",
      "id": "gathering_10",
      "name": "精取",
      "asset": {
        "file": "svg/skill/gathering_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "gathering",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:scavenging_01",
      "category": "skill",
      "id": "scavenging_01",
      "name": "基础搜集",
      "asset": {
        "file": "svg/skill/scavenging_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "scavenging",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:scavenging_03",
      "category": "skill",
      "id": "scavenging_03",
      "name": "分类勘整",
      "asset": {
        "file": "svg/skill/scavenging_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "scavenging",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:scavenging_05",
      "category": "skill",
      "id": "scavenging_05",
      "name": "工具运用",
      "asset": {
        "file": "svg/skill/scavenging_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "scavenging",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:scavenging_07",
      "category": "skill",
      "id": "scavenging_07",
      "name": "危险辨识",
      "asset": {
        "file": "svg/skill/scavenging_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "scavenging",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:scavenging_10",
      "category": "skill",
      "id": "scavenging_10",
      "name": "周密检索",
      "asset": {
        "file": "svg/skill/scavenging_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "scavenging",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:cooking_01",
      "category": "skill",
      "id": "cooking_01",
      "name": "安全烹饪",
      "asset": {
        "file": "svg/skill/cooking_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "cooking",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:cooking_03",
      "category": "skill",
      "id": "cooking_03",
      "name": "分量掌握",
      "asset": {
        "file": "svg/skill/cooking_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "cooking",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:cooking_05",
      "category": "skill",
      "id": "cooking_05",
      "name": "干粮制作",
      "asset": {
        "file": "svg/skill/cooking_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "cooking",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:cooking_07",
      "category": "skill",
      "id": "cooking_07",
      "name": "熟练炊事",
      "asset": {
        "file": "svg/skill/cooking_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "cooking",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:cooking_10",
      "category": "skill",
      "id": "cooking_10",
      "name": "远征备餐",
      "asset": {
        "file": "svg/skill/cooking_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "cooking",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:athletics_01",
      "category": "skill",
      "id": "athletics_01",
      "name": "行旅基础",
      "asset": {
        "file": "svg/skill/athletics_01.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "athletics",
      "level": 1,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:athletics_03",
      "category": "skill",
      "id": "athletics_03",
      "name": "节奏行进",
      "asset": {
        "file": "svg/skill/athletics_03.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "athletics",
      "level": 3,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:athletics_05",
      "category": "skill",
      "id": "athletics_05",
      "name": "轻装步伐",
      "asset": {
        "file": "svg/skill/athletics_05.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "athletics",
      "level": 5,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:athletics_07",
      "category": "skill",
      "id": "athletics_07",
      "name": "耐力行进",
      "asset": {
        "file": "svg/skill/athletics_07.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "athletics",
      "level": 7,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "skill:athletics_10",
      "category": "skill",
      "id": "athletics_10",
      "name": "远途规划",
      "asset": {
        "file": "svg/skill/athletics_10.svg",
        "format": "svg",
        "alpha": true
      },
      "kind": "survival",
      "family": "athletics",
      "level": 10,
      "source": "data.skillNodes",
      "entry": "Icons.Combat · BattleScreen / GrowthOverlay"
    },
    {
      "key": "action:basic_attack",
      "category": "action",
      "id": "basic_attack",
      "name": "普通攻击",
      "asset": {
        "file": "svg/action/basic_attack.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:guard",
      "category": "action",
      "id": "guard",
      "name": "防御",
      "asset": {
        "file": "svg/action/guard.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:breathe",
      "category": "action",
      "id": "breathe",
      "name": "调息",
      "asset": {
        "file": "svg/action/breathe.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:switch_weapon",
      "category": "action",
      "id": "switch_weapon",
      "name": "切换武器",
      "asset": {
        "file": "svg/action/switch_weapon.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:swap",
      "category": "action",
      "id": "swap",
      "name": "交换站位",
      "asset": {
        "file": "svg/action/swap.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:reload",
      "category": "action",
      "id": "reload",
      "name": "装填",
      "asset": {
        "file": "svg/action/reload.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:bandage",
      "category": "action",
      "id": "bandage",
      "name": "包扎",
      "asset": {
        "file": "svg/action/bandage.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:desperate_punch",
      "category": "action",
      "id": "desperate_punch",
      "name": "应急拳击",
      "asset": {
        "file": "svg/action/desperate_punch.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:retreat",
      "category": "action",
      "id": "retreat",
      "name": "撤退",
      "asset": {
        "file": "svg/action/retreat.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:end_turn",
      "category": "action",
      "id": "end_turn",
      "name": "结束行动",
      "asset": {
        "file": "svg/action/end_turn.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:use_antibiotics",
      "category": "action",
      "id": "use_antibiotics",
      "name": "使用抗生素",
      "asset": {
        "file": "svg/action/use_antibiotics.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "action:use_antiviral_cure",
      "category": "action",
      "id": "use_antiviral_cure",
      "name": "使用特效药",
      "asset": {
        "file": "svg/action/use_antiviral_cure.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.combatActions",
      "entry": "BattleScreen · BattlePanel · CardActions"
    },
    {
      "key": "condition:bleed",
      "category": "condition",
      "id": "bleed",
      "name": "流血",
      "asset": {
        "ref": "glyph:bleed"
      },
      "source": "data.statuses",
      "entry": "StatusIcons · BattleScreen"
    },
    {
      "key": "condition:stagger",
      "category": "condition",
      "id": "stagger",
      "name": "失衡",
      "asset": {
        "ref": "glyph:stagger"
      },
      "source": "data.statuses",
      "entry": "StatusIcons · BattleScreen"
    },
    {
      "key": "condition:slow",
      "category": "condition",
      "id": "slow",
      "name": "迟缓",
      "asset": {
        "ref": "glyph:slow"
      },
      "source": "data.statuses",
      "entry": "StatusIcons · BattleScreen"
    },
    {
      "key": "status:status_shaken",
      "category": "status",
      "id": "status_shaken",
      "name": "惊魂未定",
      "asset": {
        "file": "svg/status/status_shaken.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_diarrhea",
      "category": "status",
      "id": "status_diarrhea",
      "name": "腹泻",
      "asset": {
        "file": "svg/status/status_diarrhea.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_bleed",
      "category": "status",
      "id": "status_bleed",
      "name": "流血",
      "asset": {
        "file": "svg/status/status_bleed.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_bandage_cd",
      "category": "status",
      "id": "status_bandage_cd",
      "name": "包扎冷却",
      "asset": {
        "file": "svg/status/status_bandage_cd.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_baobao",
      "category": "status",
      "id": "status_baobao",
      "name": "饱饱",
      "asset": {
        "file": "svg/status/status_baobao.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_infected",
      "category": "status",
      "id": "status_infected",
      "name": "感染",
      "asset": {
        "file": "svg/status/status_infected.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_antibiotic",
      "category": "status",
      "id": "status_antibiotic",
      "name": "药效",
      "asset": {
        "file": "svg/status/status_antibiotic.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_antibiotic_cd",
      "category": "status",
      "id": "status_antibiotic_cd",
      "name": "药物冷却",
      "asset": {
        "file": "svg/status/status_antibiotic_cd.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_bandage_virus_cd",
      "category": "status",
      "id": "status_bandage_virus_cd",
      "name": "病毒相关包扎冷却",
      "asset": {
        "file": "svg/status/status_bandage_virus_cd.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_zero",
      "category": "status",
      "id": "status_zero",
      "name": "状态归零",
      "asset": {
        "file": "svg/status/status_zero.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_low_energy",
      "category": "status",
      "id": "status_low_energy",
      "name": "低精力",
      "asset": {
        "file": "svg/status/status_low_energy.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_low_morale",
      "category": "status",
      "id": "status_low_morale",
      "name": "低士气",
      "asset": {
        "file": "svg/status/status_low_morale.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_panic",
      "category": "status",
      "id": "status_panic",
      "name": "恐慌",
      "asset": {
        "file": "svg/status/status_panic.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "status:status_unknown",
      "category": "status",
      "id": "status_unknown",
      "name": "未分类状态",
      "asset": {
        "file": "svg/status/status_unknown.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "web/Survival.Web/Services/Icons.Status.cs",
      "entry": "StatusIcons · StatusPop · 计时角标独立"
    },
    {
      "key": "enemy:shambler",
      "category": "enemy",
      "id": "shambler",
      "name": "游荡尸",
      "asset": {
        "file": "art/foes-01.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          147,
          7,
          266,
          483
        ],
        "source": "foes-01",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:crawler",
      "category": "enemy",
      "id": "crawler",
      "name": "爬行尸",
      "asset": {
        "file": "art/foes-01.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          529,
          177,
          480,
          316
        ],
        "source": "foes-01",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:runner",
      "category": "enemy",
      "id": "runner",
      "name": "狂奔尸",
      "asset": {
        "file": "art/foes-01.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1059,
          26,
          430,
          461
        ],
        "source": "foes-01",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:worker",
      "category": "enemy",
      "id": "worker",
      "name": "厚衣工尸",
      "asset": {
        "file": "art/foes-01.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          137,
          513,
          320,
          506
        ],
        "source": "foes-01",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:riot",
      "category": "enemy",
      "id": "riot",
      "name": "防护尸",
      "asset": {
        "file": "art/foes-01.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          592,
          513,
          369,
          506
        ],
        "source": "foes-01",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:howler",
      "category": "enemy",
      "id": "howler",
      "name": "嚎叫尸",
      "asset": {
        "file": "art/foes-01.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1097,
          524,
          379,
          495
        ],
        "source": "foes-01",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:feral_dog",
      "category": "enemy",
      "id": "feral_dog",
      "name": "野犬",
      "asset": {
        "file": "art/foes-07.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          31,
          166,
          444,
          304
        ],
        "source": "foes-07",
        "review": "独立透明造型候选"
      },
      "kind": "animal",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:wolf",
      "category": "enemy",
      "id": "wolf",
      "name": "灰狼",
      "asset": {
        "file": "art/foes-07.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          525,
          73,
          477,
          398
        ],
        "source": "foes-07",
        "review": "独立透明造型候选"
      },
      "kind": "animal",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:boar",
      "category": "enemy",
      "id": "boar",
      "name": "野猪",
      "asset": {
        "file": "art/foes-07.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1038,
          62,
          477,
          409
        ],
        "source": "foes-07",
        "review": "独立透明造型候选"
      },
      "kind": "animal",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:stag",
      "category": "enemy",
      "id": "stag",
      "name": "警戒雄鹿",
      "asset": {
        "file": "art/foes-07.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          47,
          514,
          411,
          474
        ],
        "source": "foes-07",
        "review": "独立透明造型候选"
      },
      "kind": "animal",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_slow",
      "category": "enemy",
      "id": "z_slow",
      "name": "迟缓感染者",
      "asset": {
        "file": "art/foes-02.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          94,
          18,
          326,
          484
        ],
        "source": "foes-02",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_helmet",
      "category": "enemy",
      "id": "z_helmet",
      "name": "护盔感染者",
      "asset": {
        "file": "art/foes-02.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          636,
          9,
          298,
          493
        ],
        "source": "foes-02",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_spitter",
      "category": "enemy",
      "id": "z_spitter",
      "name": "酸液感染者",
      "asset": {
        "file": "art/foes-02.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1154,
          9,
          257,
          493
        ],
        "source": "foes-02",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_hauler",
      "category": "enemy",
      "id": "z_hauler",
      "name": "搬运感染者",
      "asset": {
        "file": "art/foes-02.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          82,
          517,
          403,
          499
        ],
        "source": "foes-02",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_darter",
      "category": "enemy",
      "id": "z_darter",
      "name": "迅捷感染者",
      "asset": {
        "file": "art/foes-02.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          558,
          610,
          432,
          398
        ],
        "source": "foes-02",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_screamer",
      "category": "enemy",
      "id": "z_screamer",
      "name": "尖啸感染者",
      "asset": {
        "file": "art/foes-02.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1127,
          513,
          317,
          505
        ],
        "source": "foes-02",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_guard",
      "category": "enemy",
      "id": "z_guard",
      "name": "重装守卫感染者",
      "asset": {
        "file": "art/foes-03.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          107,
          8,
          323,
          496
        ],
        "source": "foes-03",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_clinger",
      "category": "enemy",
      "id": "z_clinger",
      "name": "攀附感染者",
      "asset": {
        "file": "art/foes-03.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          536,
          141,
          445,
          360
        ],
        "source": "foes-03",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_bloater",
      "category": "enemy",
      "id": "z_bloater",
      "name": "膨胀感染者",
      "asset": {
        "file": "art/foes-03.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1069,
          10,
          386,
          491
        ],
        "source": "foes-03",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_nurse",
      "category": "enemy",
      "id": "z_nurse",
      "name": "制服感染者",
      "asset": {
        "file": "art/foes-03.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          93,
          518,
          315,
          496
        ],
        "source": "foes-03",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_engineer",
      "category": "enemy",
      "id": "z_engineer",
      "name": "机修感染者",
      "asset": {
        "file": "art/foes-03.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          596,
          518,
          340,
          497
        ],
        "source": "foes-03",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_signal",
      "category": "enemy",
      "id": "z_signal",
      "name": "鸣响感染者",
      "asset": {
        "file": "art/foes-03.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1084,
          512,
          381,
          506
        ],
        "source": "foes-03",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_pump_foreman",
      "category": "enemy",
      "id": "z_pump_foreman",
      "name": "泵室领班",
      "asset": {
        "file": "art/foes-04.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          78,
          10,
          352,
          493
        ],
        "source": "foes-04",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_isolation_guard",
      "category": "enemy",
      "id": "z_isolation_guard",
      "name": "隔离守卫",
      "asset": {
        "file": "art/foes-04.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          628,
          9,
          277,
          495
        ],
        "source": "foes-04",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_power_guard",
      "category": "enemy",
      "id": "z_power_guard",
      "name": "配电守卫",
      "asset": {
        "file": "art/foes-04.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1136,
          2,
          318,
          502
        ],
        "source": "foes-04",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:z_relay_guard",
      "category": "enemy",
      "id": "z_relay_guard",
      "name": "天线守卫",
      "asset": {
        "file": "art/foes-04.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          127,
          512,
          318,
          508
        ],
        "source": "foes-04",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:raider_melee",
      "category": "enemy",
      "id": "raider_melee",
      "name": "流窜掠夺者",
      "asset": {
        "file": "art/foes-06.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          573,
          517,
          312,
          503
        ],
        "source": "foes-06",
        "review": "独立透明造型候选"
      },
      "kind": "human",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:raider_gunner",
      "category": "enemy",
      "id": "raider_gunner",
      "name": "流窜枪手",
      "asset": {
        "file": "art/foes-06.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1103,
          514,
          355,
          506
        ],
        "source": "foes-06",
        "review": "独立透明造型候选"
      },
      "kind": "human",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:wild_dog_leader",
      "category": "enemy",
      "id": "wild_dog_leader",
      "name": "野犬首领",
      "asset": {
        "file": "art/foes-07.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          514,
          579,
          484,
          397
        ],
        "source": "foes-07",
        "review": "独立透明造型候选"
      },
      "kind": "animal",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:feral_boar",
      "category": "enemy",
      "id": "feral_boar",
      "name": "暴躁野猪",
      "asset": {
        "file": "art/foes-07.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1024,
          578,
          497,
          397
        ],
        "source": "foes-07",
        "review": "独立透明造型候选"
      },
      "kind": "animal",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_cantor",
      "category": "enemy",
      "id": "ec_cantor",
      "name": "低吟感染者",
      "asset": {
        "file": "art/foes-04.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          581,
          528,
          375,
          487
        ],
        "source": "foes-04",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_labwalker",
      "category": "enemy",
      "id": "ec_labwalker",
      "name": "试制区感染者",
      "asset": {
        "file": "art/foes-04.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1162,
          514,
          273,
          503
        ],
        "source": "foes-04",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_cablecrawler",
      "category": "enemy",
      "id": "ec_cablecrawler",
      "name": "缠线爬行者",
      "asset": {
        "file": "art/foes-05.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          20,
          153,
          476,
          338
        ],
        "source": "foes-05",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_stockbracer",
      "category": "enemy",
      "id": "ec_stockbracer",
      "name": "货架护卫感染者",
      "asset": {
        "file": "art/foes-05.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          579,
          9,
          390,
          491
        ],
        "source": "foes-05",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_crypt",
      "category": "enemy",
      "id": "ec_boss_crypt",
      "name": "封锁祭室守卫",
      "asset": {
        "file": "art/foes-05.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1071,
          6,
          424,
          494
        ],
        "source": "foes-05",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_lab",
      "category": "enemy",
      "id": "ec_boss_lab",
      "name": "隔离核心守卫",
      "asset": {
        "file": "art/foes-05.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          108,
          519,
          306,
          497
        ],
        "source": "foes-05",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_armory",
      "category": "enemy",
      "id": "ec_boss_armory",
      "name": "军械库守卫",
      "asset": {
        "file": "art/foes-05.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          546,
          520,
          432,
          494
        ],
        "source": "foes-05",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_relay",
      "category": "enemy",
      "id": "ec_boss_relay",
      "name": "天线台守卫",
      "asset": {
        "file": "art/foes-05.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1126,
          512,
          367,
          505
        ],
        "source": "foes-05",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_water",
      "category": "enemy",
      "id": "ec_boss_water",
      "name": "泵房推挤者",
      "asset": {
        "file": "art/foes-06.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          79,
          1,
          353,
          510
        ],
        "source": "foes-06",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_mine",
      "category": "enemy",
      "id": "ec_boss_mine",
      "name": "深井重击者",
      "asset": {
        "file": "art/foes-06.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          559,
          10,
          380,
          499
        ],
        "source": "foes-06",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_mall",
      "category": "enemy",
      "id": "ec_boss_mall",
      "name": "扶梯拦截者",
      "asset": {
        "file": "art/foes-06.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1111,
          0,
          360,
          509
        ],
        "source": "foes-06",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "enemy:ec_boss_cold",
      "category": "enemy",
      "id": "ec_boss_cold",
      "name": "仓管守卫",
      "asset": {
        "file": "art/foes-06.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          101,
          517,
          302,
          500
        ],
        "source": "foes-06",
        "review": "独立透明造型候选"
      },
      "kind": "zombie",
      "source": "data.enemies",
      "entry": "BattleScreen / EnemyDetail · enemy config ID",
      "direction": "left",
      "baseline": "feet",
      "display": "主体 90–230 CSS px，头像取构图裁窗"
    },
    {
      "key": "carcassRule:feral_dog",
      "category": "carcassRule",
      "id": "feral_dog",
      "name": "野犬尸体",
      "asset": {
        "ref": "item:carcass_feral_dog"
      },
      "source": "data.animalCarcasses",
      "entry": "FoodCard · ItemDetail · 尸体处理"
    },
    {
      "key": "carcassRule:wolf",
      "category": "carcassRule",
      "id": "wolf",
      "name": "狼尸体",
      "asset": {
        "ref": "item:carcass_wolf"
      },
      "source": "data.animalCarcasses",
      "entry": "FoodCard · ItemDetail · 尸体处理"
    },
    {
      "key": "carcassRule:boar",
      "category": "carcassRule",
      "id": "boar",
      "name": "野猪尸体",
      "asset": {
        "ref": "item:carcass_boar"
      },
      "source": "data.animalCarcasses",
      "entry": "FoodCard · ItemDetail · 尸体处理"
    },
    {
      "key": "carcassRule:stag",
      "category": "carcassRule",
      "id": "stag",
      "name": "鹿尸体",
      "asset": {
        "ref": "item:carcass_stag"
      },
      "source": "data.animalCarcasses",
      "entry": "FoodCard · ItemDetail · 尸体处理"
    },
    {
      "key": "carcassRule:wild_dog_leader",
      "category": "carcassRule",
      "id": "wild_dog_leader",
      "name": "狼尸体",
      "asset": {
        "ref": "item:carcass_wolf"
      },
      "source": "data.animalCarcasses",
      "entry": "FoodCard · ItemDetail · 尸体处理"
    },
    {
      "key": "carcassRule:feral_boar",
      "category": "carcassRule",
      "id": "feral_boar",
      "name": "野猪尸体",
      "asset": {
        "ref": "item:carcass_boar"
      },
      "source": "data.animalCarcasses",
      "entry": "FoodCard · ItemDetail · 尸体处理"
    },
    {
      "key": "actor:p0",
      "category": "actor",
      "id": "p0",
      "name": "主角",
      "asset": {
        "file": "../UI分层样件/art/actor-survivor-sample.png",
        "format": "png",
        "alpha": true
      },
      "source": "GameState.PlayerId",
      "entry": "整备 / 对白 / 队伍 / 战斗",
      "portrait": {
        "file": "../UI分层样件/art/actor-survivor-sample.png",
        "size": [
          1024,
          1536
        ],
        "relativeCrop": [
          24,
          4,
          52,
          38
        ]
      },
      "direction": "right"
    },
    {
      "key": "actor:baobao",
      "category": "actor",
      "id": "baobao",
      "name": "饱饱",
      "asset": {
        "file": "svg/actor/baobao.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "N01 / N02",
      "entry": "DialogueLayer · 脑内声音专属符号",
      "kind": "voice",
      "portraitOnly": true
    },
    {
      "key": "actor:npc_lin_zhao",
      "category": "actor",
      "id": "npc_lin_zhao",
      "name": "林昭",
      "asset": {
        "file": "art/people-companions.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          107,
          3,
          357,
          506
        ],
        "portraitCrop": [
          178,
          3,
          175,
          192
        ],
        "source": "people-companions",
        "review": "独立透明造型候选"
      },
      "kind": "companion",
      "role": "营区巡路",
      "source": "data.party.namedCompanions",
      "entry": "GearOverlay · PartyPanel · BattleScreen · DialogueLayer",
      "direction": "right"
    },
    {
      "key": "actor:npc_zhou_yan",
      "category": "actor",
      "id": "npc_zhou_yan",
      "name": "周砚",
      "asset": {
        "file": "art/people-companions.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          635,
          3,
          261,
          507
        ],
        "portraitCrop": [
          671,
          3,
          176,
          193
        ],
        "source": "people-companions",
        "review": "独立透明造型候选"
      },
      "kind": "companion",
      "role": "营区工务维修人员",
      "source": "data.party.namedCompanions",
      "entry": "GearOverlay · PartyPanel · BattleScreen · DialogueLayer",
      "direction": "right"
    },
    {
      "key": "actor:npc_shen_he",
      "category": "actor",
      "id": "npc_shen_he",
      "name": "沈禾",
      "asset": {
        "file": "art/people-companions.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1138,
          9,
          236,
          500
        ],
        "portraitCrop": [
          1185,
          9,
          173,
          190
        ],
        "source": "people-companions",
        "review": "独立透明造型候选"
      },
      "kind": "companion",
      "role": "营区医疗转运人员",
      "source": "data.party.namedCompanions",
      "entry": "GearOverlay · PartyPanel · BattleScreen · DialogueLayer",
      "direction": "right"
    },
    {
      "key": "actor:npc_tang_li",
      "category": "actor",
      "id": "npc_tang_li",
      "name": "唐梨",
      "asset": {
        "file": "art/people-companions.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          136,
          514,
          272,
          508
        ],
        "portraitCrop": [
          157,
          514,
          176,
          193
        ],
        "source": "people-companions",
        "review": "独立透明造型候选"
      },
      "kind": "companion",
      "role": "自由搜集者",
      "source": "data.party.namedCompanions",
      "entry": "GearOverlay · PartyPanel · BattleScreen · DialogueLayer",
      "direction": "right"
    },
    {
      "key": "actor:npc_gu_cheng",
      "category": "actor",
      "id": "npc_gu_cheng",
      "name": "顾承",
      "asset": {
        "file": "art/people-companions.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          602,
          513,
          321,
          509
        ],
        "portraitCrop": [
          657,
          513,
          176,
          193
        ],
        "source": "people-companions",
        "review": "独立透明造型候选"
      },
      "kind": "companion",
      "role": "营区外勤协作人员",
      "source": "data.party.namedCompanions",
      "entry": "GearOverlay · PartyPanel · BattleScreen · DialogueLayer",
      "direction": "right"
    },
    {
      "key": "actor:npc_ye_xun",
      "category": "actor",
      "id": "npc_ye_xun",
      "name": "叶洵",
      "asset": {
        "file": "art/people-companions.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1121,
          514,
          264,
          508
        ],
        "portraitCrop": [
          1167,
          514,
          176,
          193
        ],
        "source": "people-companions",
        "review": "独立透明造型候选"
      },
      "kind": "companion",
      "role": "独立通信记录者",
      "source": "data.party.namedCompanions",
      "entry": "GearOverlay · PartyPanel · BattleScreen · DialogueLayer",
      "direction": "right"
    },
    {
      "key": "actor:npc_lu_heng",
      "category": "actor",
      "id": "npc_lu_heng",
      "name": "陆衡",
      "asset": {
        "file": "art/people-camp.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          143,
          0,
          289,
          510
        ],
        "portraitCrop": [
          177,
          0,
          177,
          194
        ],
        "source": "people-camp",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "值守登记员",
      "faction": "hub_survivor",
      "location": "h01_gate",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_xu_an",
      "category": "actor",
      "id": "npc_xu_an",
      "name": "许安",
      "asset": {
        "file": "art/people-camp.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          632,
          6,
          255,
          501
        ],
        "portraitCrop": [
          666,
          6,
          173,
          190
        ],
        "source": "people-camp",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "公事务协调员",
      "faction": "hub_survivor",
      "location": "h02_square",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_luo_zheng",
      "category": "actor",
      "id": "npc_luo_zheng",
      "name": "罗铮",
      "asset": {
        "file": "art/people-camp.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1121,
          0,
          313,
          508
        ],
        "portraitCrop": [
          1195,
          0,
          176,
          193
        ],
        "source": "people-camp",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "工务领班",
      "faction": "hub_survivor",
      "location": "h03_workshop",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_chen_zhi",
      "category": "actor",
      "id": "npc_chen_zhi",
      "name": "陈芷",
      "asset": {
        "file": "art/people-camp.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          132,
          516,
          257,
          507
        ],
        "portraitCrop": [
          177,
          516,
          176,
          193
        ],
        "source": "people-camp",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "值班医师",
      "faction": "hub_survivor",
      "location": "h04_infirmary",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_tao_cen",
      "category": "actor",
      "id": "npc_tao_cen",
      "name": "陶岑",
      "asset": {
        "file": "art/people-camp.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          620,
          514,
          265,
          507
        ],
        "portraitCrop": [
          676,
          514,
          176,
          193
        ],
        "source": "people-camp",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "样本鉴定师",
      "faction": "hub_survivor",
      "location": "h05_appraisal",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_du_sui",
      "category": "actor",
      "id": "npc_du_sui",
      "name": "杜穗",
      "asset": {
        "file": "art/people-camp.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1145,
          515,
          271,
          508
        ],
        "portraitCrop": [
          1179,
          515,
          176,
          193
        ],
        "source": "people-camp",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "安置登记员",
      "faction": "hub_survivor",
      "location": "h06_shelter",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_qi_wen",
      "category": "actor",
      "id": "npc_qi_wen",
      "name": "祁闻",
      "asset": {
        "file": "art/people-church.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          132,
          2,
          269,
          504
        ],
        "portraitCrop": [
          178,
          2,
          175,
          192
        ],
        "source": "people-church",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "外来者接待者",
      "faction": "faction_church",
      "location": "c02_church",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_bai_ling",
      "category": "actor",
      "id": "npc_bai_ling",
      "name": "白苓",
      "asset": {
        "file": "art/people-church.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          632,
          9,
          316,
          498
        ],
        "portraitCrop": [
          666,
          9,
          172,
          189
        ],
        "source": "people-church",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "施济员",
      "faction": "faction_church",
      "location": "c04_alms",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_ji_mu",
      "category": "actor",
      "id": "npc_ji_mu",
      "name": "纪牧",
      "asset": {
        "file": "art/people-church.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1142,
          0,
          290,
          508
        ],
        "portraitCrop": [
          1190,
          0,
          176,
          193
        ],
        "source": "people-church",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "训诫执事",
      "faction": "faction_church",
      "location": "c05_admonition",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_song_zhu",
      "category": "actor",
      "id": "npc_song_zhu",
      "name": "宋烛",
      "asset": {
        "file": "art/people-church.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          136,
          517,
          250,
          501
        ],
        "portraitCrop": [
          173,
          517,
          173,
          190
        ],
        "source": "people-church",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "誊录员",
      "faction": "faction_church",
      "location": "c06_archive",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_fu_heng",
      "category": "actor",
      "id": "npc_fu_heng",
      "name": "傅珩",
      "asset": {
        "file": "art/people-church.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          617,
          518,
          314,
          500
        ],
        "portraitCrop": [
          695,
          518,
          173,
          190
        ],
        "source": "people-church",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "主持者",
      "faction": "faction_church",
      "location": "c07_sanctuary",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_wen_cheng",
      "category": "actor",
      "id": "npc_wen_cheng",
      "name": "温澄",
      "asset": {
        "file": "art/people-academy.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          128,
          4,
          283,
          502
        ],
        "portraitCrop": [
          179,
          4,
          174,
          191
        ],
        "source": "people-academy",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "协调员",
      "faction": "faction_academy",
      "location": "r04_reception",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_ji_wen",
      "category": "actor",
      "id": "npc_ji_wen",
      "name": "季闻",
      "asset": {
        "file": "art/people-academy.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          627,
          5,
          254,
          501
        ],
        "portraitCrop": [
          684,
          5,
          173,
          190
        ],
        "source": "people-academy",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "检测研究员",
      "faction": "faction_academy",
      "location": "r05_testing",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_shao_ning",
      "category": "actor",
      "id": "npc_shao_ning",
      "name": "邵宁",
      "asset": {
        "file": "art/people-academy.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1141,
          9,
          256,
          498
        ],
        "portraitCrop": [
          1177,
          9,
          172,
          189
        ],
        "source": "people-academy",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "档案员",
      "faction": "faction_academy",
      "location": "r06_archive",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_he_yuan",
      "category": "actor",
      "id": "npc_he_yuan",
      "name": "何远",
      "asset": {
        "file": "art/people-academy.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          103,
          513,
          277,
          507
        ],
        "portraitCrop": [
          165,
          513,
          176,
          193
        ],
        "source": "people-academy",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "设备技师",
      "faction": "faction_academy",
      "location": "r07_workshop",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_yin_ke",
      "category": "actor",
      "id": "npc_yin_ke",
      "name": "尹珂",
      "asset": {
        "file": "art/people-academy.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          627,
          515,
          282,
          504
        ],
        "portraitCrop": [
          660,
          515,
          175,
          192
        ],
        "source": "people-academy",
        "review": "独立透明造型候选"
      },
      "kind": "city",
      "role": "观察区负责人",
      "faction": "faction_academy",
      "location": "r08_observation",
      "source": "data.city.npcs",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "actor:npc_cheng_wang",
      "category": "actor",
      "id": "npc_cheng_wang",
      "name": "程望",
      "asset": {
        "file": "art/people-church.png",
        "format": "atlas",
        "alpha": true,
        "size": [
          1536,
          1024
        ],
        "crop": [
          1167,
          512,
          236,
          506
        ],
        "portraitCrop": [
          1194,
          512,
          175,
          192
        ],
        "source": "people-church",
        "review": "独立透明造型候选"
      },
      "kind": "visitor",
      "role": "幸存者营区的传讯跑腿",
      "source": "data.prologue.visitor",
      "entry": "CityServiceOverlay · CityOverview · DialogueLayer",
      "direction": "neutral"
    },
    {
      "key": "duty:npc_lu_heng",
      "category": "duty",
      "id": "npc_lu_heng",
      "name": "值守登记员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_lu_heng.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_lu_heng.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "hub_survivor",
      "location": "h01_gate",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_xu_an",
      "category": "duty",
      "id": "npc_xu_an",
      "name": "公事务协调员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_xu_an.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_xu_an.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "hub_survivor",
      "location": "h02_square",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_luo_zheng",
      "category": "duty",
      "id": "npc_luo_zheng",
      "name": "工务领班 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_luo_zheng.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_luo_zheng.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "hub_survivor",
      "location": "h03_workshop",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_chen_zhi",
      "category": "duty",
      "id": "npc_chen_zhi",
      "name": "值班医师 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_chen_zhi.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_chen_zhi.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "hub_survivor",
      "location": "h04_infirmary",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_tao_cen",
      "category": "duty",
      "id": "npc_tao_cen",
      "name": "样本鉴定师 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_tao_cen.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_tao_cen.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "hub_survivor",
      "location": "h05_appraisal",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_du_sui",
      "category": "duty",
      "id": "npc_du_sui",
      "name": "安置登记员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_du_sui.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_du_sui.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "hub_survivor",
      "location": "h06_shelter",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_qi_wen",
      "category": "duty",
      "id": "npc_qi_wen",
      "name": "外来者接待者 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_qi_wen.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_qi_wen.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_church",
      "location": "c02_church",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_bai_ling",
      "category": "duty",
      "id": "npc_bai_ling",
      "name": "施济员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_bai_ling.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_bai_ling.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_church",
      "location": "c04_alms",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_ji_mu",
      "category": "duty",
      "id": "npc_ji_mu",
      "name": "训诫执事 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_ji_mu.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_ji_mu.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_church",
      "location": "c05_admonition",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_song_zhu",
      "category": "duty",
      "id": "npc_song_zhu",
      "name": "誊录员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_song_zhu.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_song_zhu.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_church",
      "location": "c06_archive",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_fu_heng",
      "category": "duty",
      "id": "npc_fu_heng",
      "name": "主持者 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_fu_heng.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_fu_heng.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_church",
      "location": "c07_sanctuary",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_wen_cheng",
      "category": "duty",
      "id": "npc_wen_cheng",
      "name": "协调员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_wen_cheng.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_wen_cheng.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_academy",
      "location": "r04_reception",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_ji_wen",
      "category": "duty",
      "id": "npc_ji_wen",
      "name": "检测研究员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_ji_wen.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_ji_wen.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_academy",
      "location": "r05_testing",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_shao_ning",
      "category": "duty",
      "id": "npc_shao_ning",
      "name": "档案员 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_shao_ning.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_shao_ning.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_academy",
      "location": "r06_archive",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_he_yuan",
      "category": "duty",
      "id": "npc_he_yuan",
      "name": "设备技师 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_he_yuan.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_he_yuan.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_academy",
      "location": "r07_workshop",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "duty:npc_yin_ke",
      "category": "duty",
      "id": "npc_yin_ke",
      "name": "观察区负责人 · 当班接续员",
      "asset": {
        "file": "svg/duty/npc_yin_ke.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/duty-portrait/npc_yin_ke.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.city.npcs.states",
      "entry": "实际代班状态 + 当前岗位 / 历史保存身份",
      "faction": "faction_academy",
      "location": "r08_observation",
      "design": "独立机构肖像，与原人头像分开"
    },
    {
      "key": "recruitTemplate:front_guard",
      "category": "recruitTemplate",
      "id": "front_guard",
      "name": "前排护卫",
      "asset": {
        "file": "svg/recruit/front_guard.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/front_guard.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:blade_hand",
      "category": "recruitTemplate",
      "id": "blade_hand",
      "name": "刀具熟手",
      "asset": {
        "file": "svg/recruit/blade_hand.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/blade_hand.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:breaker",
      "category": "recruitTemplate",
      "id": "breaker",
      "name": "破障工",
      "asset": {
        "file": "svg/recruit/breaker.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/breaker.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:spear_walker",
      "category": "recruitTemplate",
      "id": "spear_walker",
      "name": "长柄巡路员",
      "asset": {
        "file": "svg/recruit/spear_walker.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/spear_walker.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:salvager",
      "category": "recruitTemplate",
      "id": "salvager",
      "name": "回收工",
      "asset": {
        "file": "svg/recruit/salvager.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/salvager.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:hunter",
      "category": "recruitTemplate",
      "id": "hunter",
      "name": "猎行者",
      "asset": {
        "file": "svg/recruit/hunter.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/hunter.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:old_service",
      "category": "recruitTemplate",
      "id": "old_service",
      "name": "旧勤务员",
      "asset": {
        "file": "svg/recruit/old_service.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/old_service.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:coordinator",
      "category": "recruitTemplate",
      "id": "coordinator",
      "name": "随行协调员",
      "asset": {
        "file": "svg/recruit/coordinator.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/coordinator.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:camp_nurse",
      "category": "recruitTemplate",
      "id": "camp_nurse",
      "name": "营地护理员",
      "asset": {
        "file": "svg/recruit/camp_nurse.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/camp_nurse.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.party.recruitment.templates",
      "entry": "RecruitPanel · 动态人物合成",
      "design": "只展示职业服装样例；正式绑定使用实际保存的外观"
    },
    {
      "key": "recruitTemplate:legacy_neutral",
      "category": "recruitTemplate",
      "id": "legacy_neutral",
      "name": "旧档未记录外观",
      "asset": {
        "file": "svg/recruit/legacy_neutral.svg",
        "format": "svg",
        "alpha": true
      },
      "portrait": {
        "file": "svg/recruit-portrait/legacy_neutral.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "Q180 / Q276",
      "entry": "缺字段时只对缺项取中性部件；不猜测性别或肤色"
    },
    {
      "key": "location:camp",
      "category": "location",
      "id": "camp",
      "name": "临时营地",
      "asset": {
        "file": "../UI场景全览/art/camp-outskirts.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:field",
      "category": "location",
      "id": "field",
      "name": "农野荒地",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/field.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:stream",
      "category": "location",
      "id": "stream",
      "name": "溪流浅滩",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:woods",
      "category": "location",
      "id": "woods",
      "name": "树林边缘",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:village",
      "category": "location",
      "id": "village",
      "name": "废弃村庄",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:hardware",
      "category": "location",
      "id": "hardware",
      "name": "五金店",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v10/hardware.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:clinic",
      "category": "location",
      "id": "clinic",
      "name": "诊所",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/clinic.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:scrapyard",
      "category": "location",
      "id": "scrapyard",
      "name": "废料场",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "starter",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:m01_road",
      "category": "location",
      "id": "m01_road",
      "name": "旧公路",
      "asset": {
        "file": "../UI场景全览/art/battle-checkpoint.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:m02_checkpoint",
      "category": "location",
      "id": "m02_checkpoint",
      "name": "检查哨",
      "asset": {
        "file": "../UI场景全览/art/battle-checkpoint.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:h01_gate",
      "category": "location",
      "id": "h01_gate",
      "name": "营门",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "hub",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:h02_square",
      "category": "location",
      "id": "h02_square",
      "name": "营区广场",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "hub",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:h03_workshop",
      "category": "location",
      "id": "h03_workshop",
      "name": "营区工坊",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "hub",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:h04_infirmary",
      "category": "location",
      "id": "h04_infirmary",
      "name": "营区医务室",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "hub",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:h05_homestead",
      "category": "location",
      "id": "h05_homestead",
      "name": "营外安全屋地块",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "hub",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w01_farm",
      "category": "location",
      "id": "w01_farm",
      "name": "近郊农场",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/field.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w02_forest",
      "category": "location",
      "id": "w02_forest",
      "name": "林间营址",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w03_quarry",
      "category": "location",
      "id": "w03_quarry",
      "name": "采石坡",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w04_crossroads",
      "category": "location",
      "id": "w04_crossroads",
      "name": "荒野岔路",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w05_marsh",
      "category": "location",
      "id": "w05_marsh",
      "name": "芦苇湿地",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w06_bridge",
      "category": "location",
      "id": "w06_bridge",
      "name": "断桥河滩",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w07_ridge",
      "category": "location",
      "id": "w07_ridge",
      "name": "山脊哨道",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w08_reservoir",
      "category": "location",
      "id": "w08_reservoir",
      "name": "旧水库",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w09_logging",
      "category": "location",
      "id": "w09_logging",
      "name": "深林伐木场",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w10_watchtower",
      "category": "location",
      "id": "w10_watchtower",
      "name": "山顶瞭望台",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:a01_yard",
      "category": "location",
      "id": "a01_yard",
      "name": "铁路货场",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "works",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:a02_pump",
      "category": "location",
      "id": "a02_pump",
      "name": "旧泵站",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "works",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:a03_foundry",
      "category": "location",
      "id": "a03_foundry",
      "name": "铸造厂外",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "works",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:b01_school",
      "category": "location",
      "id": "b01_school",
      "name": "安置学校",
      "asset": {
        "file": "../UI场景全览/art/camp-outskirts.png",
        "format": "png",
        "alpha": false
      },
      "zone": "aid",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:b02_station",
      "category": "location",
      "id": "b02_station",
      "name": "急救转运站",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "aid",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:b03_hospital",
      "category": "location",
      "id": "b03_hospital",
      "name": "医院外院",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/clinic.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:c01_pilgrim",
      "category": "location",
      "id": "c01_pilgrim",
      "name": "朝圣路",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "church",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:c02_church",
      "category": "location",
      "id": "c02_church",
      "name": "教会接待庭",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "church",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:c03_crypt",
      "category": "location",
      "id": "c03_crypt",
      "name": "教堂墓园",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "church",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r01_observatory",
      "category": "location",
      "id": "r01_observatory",
      "name": "灰塔观测站",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r02_academy",
      "category": "location",
      "id": "r02_academy",
      "name": "科学院外院",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r03_lab",
      "category": "location",
      "id": "r03_lab",
      "name": "封存实验楼",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:o01_oldgate",
      "category": "location",
      "id": "o01_oldgate",
      "name": "旧城门",
      "asset": {
        "file": "../UI场景全览/art/battle-checkpoint.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:o02_market",
      "category": "location",
      "id": "o02_market",
      "name": "旧城商圈",
      "asset": {
        "file": "../UI场景全览/art/battle-checkpoint.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:o03_police",
      "category": "location",
      "id": "o03_police",
      "name": "警备仓外",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:o04_transit",
      "category": "location",
      "id": "o04_transit",
      "name": "旧城车站",
      "asset": {
        "file": "../UI场景全览/art/battle-checkpoint.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:h05_appraisal",
      "category": "location",
      "id": "h05_appraisal",
      "name": "鉴定所",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "hub",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:h06_shelter",
      "category": "location",
      "id": "h06_shelter",
      "name": "收容所",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "hub",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:c04_alms",
      "category": "location",
      "id": "c04_alms",
      "name": "施济处",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "church",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:c05_admonition",
      "category": "location",
      "id": "c05_admonition",
      "name": "训诫堂",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "church",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:c06_archive",
      "category": "location",
      "id": "c06_archive",
      "name": "典藏室",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "church",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:c07_sanctuary",
      "category": "location",
      "id": "c07_sanctuary",
      "name": "圣堂",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "church",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r04_reception",
      "category": "location",
      "id": "r04_reception",
      "name": "接待处",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r05_testing",
      "category": "location",
      "id": "r05_testing",
      "name": "检测室",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r06_archive",
      "category": "location",
      "id": "r06_archive",
      "name": "档案室",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r07_workshop",
      "category": "location",
      "id": "r07_workshop",
      "name": "试制工坊",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:r08_observation",
      "category": "location",
      "id": "r08_observation",
      "name": "隔离观察区",
      "asset": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false,
        "review": "区域鸟瞰 · 建筑节点由程序叠加"
      },
      "zone": "academy",
      "kind": "city_building",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w11_orchard",
      "category": "location",
      "id": "w11_orchard",
      "name": "荒弃果园",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/field.png",
        "format": "png",
        "alpha": false
      },
      "zone": "church_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w12_canal",
      "category": "location",
      "id": "w12_canal",
      "name": "灌溉渠岸",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "church_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w13_mill",
      "category": "location",
      "id": "w13_mill",
      "name": "旧磨坊",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "church_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w14_cemeteryroad",
      "category": "location",
      "id": "w14_cemeteryroad",
      "name": "墓地荒径",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "church_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w15_shrinewoods",
      "category": "location",
      "id": "w15_shrinewoods",
      "name": "祈祷林",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "church_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w16_service",
      "category": "location",
      "id": "w16_service",
      "name": "山间服务站",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "academy_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w17_switchback",
      "category": "location",
      "id": "w17_switchback",
      "name": "盘山公路",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "academy_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w18_tunnel",
      "category": "location",
      "id": "w18_tunnel",
      "name": "塌隧道外",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "academy_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w19_survey",
      "category": "location",
      "id": "w19_survey",
      "name": "测绘营址",
      "asset": {
        "file": "../UI场景全览/art/camp-outskirts.png",
        "format": "png",
        "alpha": false
      },
      "zone": "academy_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w20_powercut",
      "category": "location",
      "id": "w20_powercut",
      "name": "高压走廊",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "academy_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w21_pine",
      "category": "location",
      "id": "w21_pine",
      "name": "深针叶林",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w22_ravine",
      "category": "location",
      "id": "w22_ravine",
      "name": "陡壁沟谷",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w23_hunting",
      "category": "location",
      "id": "w23_hunting",
      "name": "猎人旧营",
      "asset": {
        "file": "../UI场景全览/art/camp-outskirts.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w24_cave",
      "category": "location",
      "id": "w24_cave",
      "name": "岩洞入口",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w25_wetland",
      "category": "location",
      "id": "w25_wetland",
      "name": "黑水湿地",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w26_island",
      "category": "location",
      "id": "w26_island",
      "name": "湖心陆洲",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w27_relay",
      "category": "location",
      "id": "w27_relay",
      "name": "废中继营",
      "asset": {
        "file": "../UI场景全览/art/camp-outskirts.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w28_highland",
      "category": "location",
      "id": "w28_highland",
      "name": "风蚀高地",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/woods.png",
        "format": "png",
        "alpha": false
      },
      "zone": "deep_wild",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w29_riverbend",
      "category": "location",
      "id": "w29_riverbend",
      "name": "河道弯口",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/stream.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w30_truckstop",
      "category": "location",
      "id": "w30_truckstop",
      "name": "货车休息区",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w31_suburb",
      "category": "location",
      "id": "w31_suburb",
      "name": "城郊棚户",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/village.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w32_railcut",
      "category": "location",
      "id": "w32_railcut",
      "name": "铁路线切口",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "city_approach",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w33_waterworks",
      "category": "location",
      "id": "w33_waterworks",
      "name": "荒废水厂",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "utility",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:w34_depot",
      "category": "location",
      "id": "w34_depot",
      "name": "应急仓外",
      "asset": {
        "file": "../../../../../web/Survival.Web/wwwroot/art/v08/scrapyard.png",
        "format": "png",
        "alpha": false
      },
      "zone": "utility",
      "kind": "world",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_01",
      "category": "location",
      "id": "d01_01",
      "name": "墓门缓冲",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_02",
      "category": "location",
      "id": "d01_02",
      "name": "捐献仓",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_03",
      "category": "location",
      "id": "d01_03",
      "name": "忏悔长廊",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_04",
      "category": "location",
      "id": "d01_04",
      "name": "藏书密室",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_05",
      "category": "location",
      "id": "d01_05",
      "name": "环形礼拜厅",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_06",
      "category": "location",
      "id": "d01_06",
      "name": "骨龛回道",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_07",
      "category": "location",
      "id": "d01_07",
      "name": "封锁祭室",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d01_08",
      "category": "location",
      "id": "d01_08",
      "name": "旧路线档案",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d01",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_01",
      "category": "location",
      "id": "d02_01",
      "name": "消洗门厅",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_02",
      "category": "location",
      "id": "d02_02",
      "name": "观察廊",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_03",
      "category": "location",
      "id": "d02_03",
      "name": "样本冷库",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_04",
      "category": "location",
      "id": "d02_04",
      "name": "控制室",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_05",
      "category": "location",
      "id": "d02_05",
      "name": "试制间",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_06",
      "category": "location",
      "id": "d02_06",
      "name": "检修竖井",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_07",
      "category": "location",
      "id": "d02_07",
      "name": "隔离核心",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_08",
      "category": "location",
      "id": "d02_08",
      "name": "流程终端",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d02_09",
      "category": "location",
      "id": "d02_09",
      "name": "外排廊",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d02",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_01",
      "category": "location",
      "id": "d03_01",
      "name": "装卸门",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_02",
      "category": "location",
      "id": "d03_02",
      "name": "外仓",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_03",
      "category": "location",
      "id": "d03_03",
      "name": "器材室",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_04",
      "category": "location",
      "id": "d03_04",
      "name": "值勤室",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_05",
      "category": "location",
      "id": "d03_05",
      "name": "内仓闸口",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_06",
      "category": "location",
      "id": "d03_06",
      "name": "配电房",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_07",
      "category": "location",
      "id": "d03_07",
      "name": "军械守卫",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d03_08",
      "category": "location",
      "id": "d03_08",
      "name": "封存仓",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d03",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_01",
      "category": "location",
      "id": "d04_01",
      "name": "地堡入口",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_02",
      "category": "location",
      "id": "d04_02",
      "name": "缆线沟",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_03",
      "category": "location",
      "id": "d04_03",
      "name": "蓄电室",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_04",
      "category": "location",
      "id": "d04_04",
      "name": "山腹机房",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_05",
      "category": "location",
      "id": "d04_05",
      "name": "屋顶梯井",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_06",
      "category": "location",
      "id": "d04_06",
      "name": "天线守卫台",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_07",
      "category": "location",
      "id": "d04_07",
      "name": "联络台",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d04_08",
      "category": "location",
      "id": "d04_08",
      "name": "逃生索道",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d04",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_01",
      "category": "location",
      "id": "d05_01",
      "name": "检修井",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_02",
      "category": "location",
      "id": "d05_02",
      "name": "分流室",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_03",
      "category": "location",
      "id": "d05_03",
      "name": "沉淀池",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_04",
      "category": "location",
      "id": "d05_04",
      "name": "滤料库",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_05",
      "category": "location",
      "id": "d05_05",
      "name": "环水渠",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_06",
      "category": "location",
      "id": "d05_06",
      "name": "泵房守卫",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_07",
      "category": "location",
      "id": "d05_07",
      "name": "阀控室",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d05_08",
      "category": "location",
      "id": "d05_08",
      "name": "出水涵洞",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d05",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_01",
      "category": "location",
      "id": "d06_01",
      "name": "洞口",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_02",
      "category": "location",
      "id": "d06_02",
      "name": "岔井",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_03",
      "category": "location",
      "id": "d06_03",
      "name": "浅矿脉",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_04",
      "category": "location",
      "id": "d06_04",
      "name": "废轨道",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_05",
      "category": "location",
      "id": "d06_05",
      "name": "塌方绕道",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_06",
      "category": "location",
      "id": "d06_06",
      "name": "矿工营室",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_07",
      "category": "location",
      "id": "d06_07",
      "name": "深井守卫",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_08",
      "category": "location",
      "id": "d06_08",
      "name": "工具封库",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d06_09",
      "category": "location",
      "id": "d06_09",
      "name": "通风出口",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d06",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_01",
      "category": "location",
      "id": "d07_01",
      "name": "卷帘入口",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_02",
      "category": "location",
      "id": "d07_02",
      "name": "中庭",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_03",
      "category": "location",
      "id": "d07_03",
      "name": "食品层",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_04",
      "category": "location",
      "id": "d07_04",
      "name": "服装层",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_05",
      "category": "location",
      "id": "d07_05",
      "name": "药妆侧厅",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_06",
      "category": "location",
      "id": "d07_06",
      "name": "扶梯守卫",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_07",
      "category": "location",
      "id": "d07_07",
      "name": "屋顶避难点",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d07_08",
      "category": "location",
      "id": "d07_08",
      "name": "消防出口",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d07",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_01",
      "category": "location",
      "id": "d08_01",
      "name": "货台",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_02",
      "category": "location",
      "id": "d08_02",
      "name": "温区分拣",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_03",
      "category": "location",
      "id": "d08_03",
      "name": "干货库",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_04",
      "category": "location",
      "id": "d08_04",
      "name": "冷库走廊",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_05",
      "category": "location",
      "id": "d08_05",
      "name": "封存药架",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_06",
      "category": "location",
      "id": "d08_06",
      "name": "备用电房",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_07",
      "category": "location",
      "id": "d08_07",
      "name": "仓管守卫",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_08",
      "category": "location",
      "id": "d08_08",
      "name": "调度室",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "location:d08_09",
      "category": "location",
      "id": "d08_09",
      "name": "卸货出口",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "地牢区域底图 · 多节点共享"
      },
      "zone": "d08",
      "kind": "dungeon",
      "source": "data.locations",
      "entry": "ArtAssets.Scene / LocationCard / MapOverlay",
      "layers": [
        "scene",
        "markers",
        "routes",
        "labels",
        "status"
      ],
      "sharing": "同区域或地貌共用底图；不同节点由实际地图数据区分"
    },
    {
      "key": "dungeon:d01",
      "category": "dungeon",
      "id": "d01",
      "name": "教会地下圣所",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "dungeon:d02",
      "category": "dungeon",
      "id": "d02",
      "name": "灰塔封存区",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-01",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "dungeon:d03",
      "category": "dungeon",
      "id": "d03",
      "name": "旧城警备仓",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "dungeon:d04",
      "category": "dungeon",
      "id": "d04",
      "name": "深野通信站",
      "asset": {
        "file": "art/dungeons-01.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-01",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "dungeon:d05",
      "category": "dungeon",
      "id": "d05",
      "name": "废水厂地下管网",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "dungeon:d06",
      "category": "dungeon",
      "id": "d06",
      "name": "深林矿洞",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          0,
          836,
          471
        ],
        "source": "dungeons-02",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "dungeon:d07",
      "category": "dungeon",
      "id": "d07",
      "name": "洪淹商城",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          0,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "dungeon:d08",
      "category": "dungeon",
      "id": "d08",
      "name": "应急冷链仓",
      "asset": {
        "file": "art/dungeons-02.png",
        "format": "atlas",
        "alpha": false,
        "size": [
          1672,
          941
        ],
        "crop": [
          836,
          471,
          836,
          470
        ],
        "source": "dungeons-02",
        "review": "区域背景候选"
      },
      "source": "data.map.dungeons",
      "entry": "DungeonReset · JourneyOverlay · BattleScreen"
    },
    {
      "key": "region:starter",
      "category": "region",
      "id": "starter",
      "name": "初醒荒野",
      "asset": {
        "ref": "location:camp"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:hub",
      "category": "region",
      "id": "hub",
      "name": "幸存者营区",
      "asset": {
        "ref": "location:h01_gate"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:city",
      "category": "region",
      "id": "city",
      "name": "旧城废墟",
      "asset": {
        "ref": "location:b03_hospital"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:church",
      "category": "region",
      "id": "church",
      "name": "教会辖区",
      "asset": {
        "ref": "location:c01_pilgrim"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:academy",
      "category": "region",
      "id": "academy",
      "name": "灰塔院区",
      "asset": {
        "ref": "location:r01_observatory"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:wild",
      "category": "region",
      "id": "wild",
      "name": "近郊荒野",
      "asset": {
        "ref": "location:m01_road"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:deep_wild",
      "category": "region",
      "id": "deep_wild",
      "name": "深野腹地",
      "asset": {
        "ref": "location:w21_pine"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:church_approach",
      "category": "region",
      "id": "church_approach",
      "name": "朝圣沿途",
      "asset": {
        "ref": "location:w11_orchard"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:academy_approach",
      "category": "region",
      "id": "academy_approach",
      "name": "灰塔山路",
      "asset": {
        "ref": "location:w16_service"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:city_approach",
      "category": "region",
      "id": "city_approach",
      "name": "旧城外缘",
      "asset": {
        "ref": "location:w29_riverbend"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:works",
      "category": "region",
      "id": "works",
      "name": "铁路工务区",
      "asset": {
        "ref": "location:a01_yard"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:aid",
      "category": "region",
      "id": "aid",
      "name": "救援安置区",
      "asset": {
        "ref": "location:b01_school"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "region:utility",
      "category": "region",
      "id": "utility",
      "name": "公共设施区",
      "asset": {
        "ref": "location:w33_waterworks"
      },
      "source": "data.map.regionNames",
      "entry": "MapOverlay · 区域标签"
    },
    {
      "key": "faction:hub_survivor",
      "category": "faction",
      "id": "hub_survivor",
      "name": "幸存者营区",
      "asset": {
        "file": "svg/faction/hub_survivor.svg",
        "format": "svg",
        "alpha": true
      },
      "backdrop": {
        "file": "../参考原图/05-势力鸟瞰/01-幸存者营区.png",
        "format": "png",
        "alpha": false
      },
      "source": "data.city.factions",
      "entry": "CityOverview · CityServiceOverlay · DialogueLayer"
    },
    {
      "key": "faction:faction_church",
      "category": "faction",
      "id": "faction_church",
      "name": "新生教会",
      "asset": {
        "file": "svg/faction/faction_church.svg",
        "format": "svg",
        "alpha": true
      },
      "backdrop": {
        "file": "../参考原图/05-势力鸟瞰/02-教会圣所地区.png",
        "format": "png",
        "alpha": false
      },
      "source": "data.city.factions",
      "entry": "CityOverview · CityServiceOverlay · DialogueLayer"
    },
    {
      "key": "faction:faction_academy",
      "category": "faction",
      "id": "faction_academy",
      "name": "灰塔科学院",
      "asset": {
        "file": "svg/faction/faction_academy.svg",
        "format": "svg",
        "alpha": true
      },
      "backdrop": {
        "file": "../参考原图/05-势力鸟瞰/03-科学院封存地区.png",
        "format": "png",
        "alpha": false
      },
      "source": "data.city.factions",
      "entry": "CityOverview · CityServiceOverlay · DialogueLayer"
    },
    {
      "key": "gate:open",
      "category": "gate",
      "id": "open",
      "name": "open",
      "asset": {
        "ref": "glyph:route"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:field25",
      "category": "gate",
      "id": "field25",
      "name": "field25",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:field50",
      "category": "gate",
      "id": "field50",
      "name": "field50",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:field100",
      "category": "gate",
      "id": "field100",
      "name": "field100",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:village50",
      "category": "gate",
      "id": "village50",
      "name": "village50",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:village100",
      "category": "gate",
      "id": "village100",
      "name": "village100",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:hardware100",
      "category": "gate",
      "id": "hardware100",
      "name": "hardware100",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:woods100_or_scrapyard100",
      "category": "gate",
      "id": "woods100_or_scrapyard100",
      "name": "woods100_or_scrapyard100",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:QMAP01",
      "category": "gate",
      "id": "QMAP01",
      "name": "QMAP01",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:from_explore100",
      "category": "gate",
      "id": "from_explore100",
      "name": "from_explore100",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:clear_checkpoint",
      "category": "gate",
      "id": "clear_checkpoint",
      "name": "clear_checkpoint",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:register",
      "category": "gate",
      "id": "register",
      "name": "register",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:church_public_access",
      "category": "gate",
      "id": "church_public_access",
      "name": "church_public_access",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:FA02_accept_no_oath",
      "category": "gate",
      "id": "FA02_accept_no_oath",
      "name": "FA02_accept_no_oath",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:academy_public_access",
      "category": "gate",
      "id": "academy_public_access",
      "name": "academy_public_access",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:consent_or_decline_no_mainline_lock",
      "category": "gate",
      "id": "consent_or_decline_no_mainline_lock",
      "name": "consent_or_decline_no_mainline_lock",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:FA03_accept_environment_record",
      "category": "gate",
      "id": "FA03_accept_environment_record",
      "name": "FA03_accept_environment_record",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:supervised_no_experiment_required",
      "category": "gate",
      "id": "supervised_no_experiment_required",
      "name": "supervised_no_experiment_required",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:FA02_public_route",
      "category": "gate",
      "id": "FA02_public_route",
      "name": "FA02_public_route",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:instance_from_explore100",
      "category": "gate",
      "id": "instance_from_explore100",
      "name": "instance_from_explore100",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:guardian_defeated",
      "category": "gate",
      "id": "guardian_defeated",
      "name": "guardian_defeated",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:instance_complete",
      "category": "gate",
      "id": "instance_complete",
      "name": "instance_complete",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:FA03_environment_only",
      "category": "gate",
      "id": "FA03_environment_only",
      "name": "FA03_environment_only",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:FA04_merged",
      "category": "gate",
      "id": "FA04_merged",
      "name": "FA04_merged",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:FA05_power_owned",
      "category": "gate",
      "id": "FA05_power_owned",
      "name": "FA05_power_owned",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "gate:parent_explore100",
      "category": "gate",
      "id": "parent_explore100",
      "name": "parent_explore100",
      "asset": {
        "ref": "glyph:lock"
      },
      "source": "data.map.gates",
      "entry": "MapOverlay · 实际通行状态"
    },
    {
      "key": "resource:field:roots",
      "category": "resource",
      "id": "field:roots",
      "name": "块根地",
      "asset": {
        "file": "svg/resource/field--roots.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "field",
      "nodeId": "roots",
      "shape": "root",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:field:field_fiber",
      "category": "resource",
      "id": "field:field_fiber",
      "name": "荒地纤维丛",
      "asset": {
        "file": "svg/resource/field--field_fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "field",
      "nodeId": "field_fiber",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:stream:water",
      "category": "resource",
      "id": "stream:water",
      "name": "脏水源",
      "asset": {
        "file": "svg/resource/stream--water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "stream",
      "nodeId": "water",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:stream:herbs",
      "category": "resource",
      "id": "stream:herbs",
      "name": "草药丛",
      "asset": {
        "file": "svg/resource/stream--herbs.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "stream",
      "nodeId": "herbs",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:woods:wood",
      "category": "resource",
      "id": "woods:wood",
      "name": "可采木材",
      "asset": {
        "file": "svg/resource/woods--wood.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "woods",
      "nodeId": "wood",
      "shape": "plank",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:woods:stones",
      "category": "resource",
      "id": "woods:stones",
      "name": "地表石块",
      "asset": {
        "file": "svg/resource/woods--stones.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "woods",
      "nodeId": "stones",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:woods:berries",
      "category": "resource",
      "id": "woods:berries",
      "name": "野果丛",
      "asset": {
        "file": "svg/resource/woods--berries.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "woods",
      "nodeId": "berries",
      "shape": "berry",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:woods:woods_fiber",
      "category": "resource",
      "id": "woods:woods_fiber",
      "name": "林下纤维丛",
      "asset": {
        "file": "svg/resource/woods--woods_fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "woods",
      "nodeId": "woods_fiber",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:village:supply_box",
      "category": "resource",
      "id": "village:supply_box",
      "name": "补给箱",
      "asset": {
        "file": "svg/resource/village--supply_box.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "village",
      "nodeId": "supply_box",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:village:misc_box",
      "category": "resource",
      "id": "village:misc_box",
      "name": "杂物箱",
      "asset": {
        "file": "svg/resource/village--misc_box.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "village",
      "nodeId": "misc_box",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:hardware:pot_box",
      "category": "resource",
      "id": "hardware:pot_box",
      "name": "锅具箱",
      "asset": {
        "file": "svg/resource/hardware--pot_box.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "hardware",
      "nodeId": "pot_box",
      "shape": "pot",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:hardware:tool_box",
      "category": "resource",
      "id": "hardware:tool_box",
      "name": "工具箱",
      "asset": {
        "file": "svg/resource/hardware--tool_box.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "hardware",
      "nodeId": "tool_box",
      "shape": "part",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:clinic:med_cabinet",
      "category": "resource",
      "id": "clinic:med_cabinet",
      "name": "药柜",
      "asset": {
        "file": "svg/resource/clinic--med_cabinet.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "clinic",
      "nodeId": "med_cabinet",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:clinic:cloth_bag",
      "category": "resource",
      "id": "clinic:cloth_bag",
      "name": "布料包",
      "asset": {
        "file": "svg/resource/clinic--cloth_bag.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "clinic",
      "nodeId": "cloth_bag",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:scrapyard:metal_heap",
      "category": "resource",
      "id": "scrapyard:metal_heap",
      "name": "废金属堆",
      "asset": {
        "file": "svg/resource/scrapyard--metal_heap.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "scrapyard",
      "nodeId": "metal_heap",
      "shape": "scrap",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:scrapyard:parts_box",
      "category": "resource",
      "id": "scrapyard:parts_box",
      "name": "零件箱",
      "asset": {
        "file": "svg/resource/scrapyard--parts_box.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "scrapyard",
      "nodeId": "parts_box",
      "shape": "scrap",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:hardware:hardware_weapon_cache",
      "category": "resource",
      "id": "hardware:hardware_weapon_cache",
      "name": "武器柜",
      "asset": {
        "file": "svg/resource/hardware--hardware_weapon_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "hardware",
      "nodeId": "hardware_weapon_cache",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:village:village_weapon_locker",
      "category": "resource",
      "id": "village:village_weapon_locker",
      "name": "旧储物柜",
      "asset": {
        "file": "svg/resource/village--village_weapon_locker.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "village",
      "nodeId": "village_weapon_locker",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:clinic:clinic_medical_supply",
      "category": "resource",
      "id": "clinic:clinic_medical_supply",
      "name": "医药补给箱",
      "asset": {
        "file": "svg/resource/clinic--clinic_medical_supply.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "clinic",
      "nodeId": "clinic_medical_supply",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:village:village_cold_box",
      "category": "resource",
      "id": "village:village_cold_box",
      "name": "废弃冷藏箱",
      "asset": {
        "file": "svg/resource/village--village_cold_box.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "village",
      "nodeId": "village_cold_box",
      "shape": "can",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w11_orchard:w11_orchard_a",
      "category": "resource",
      "id": "w11_orchard:w11_orchard_a",
      "name": "果园块根地",
      "asset": {
        "file": "svg/resource/w11_orchard--w11_orchard_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w11_orchard",
      "nodeId": "w11_orchard_a",
      "shape": "berry",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w11_orchard:w11_orchard_b",
      "category": "resource",
      "id": "w11_orchard:w11_orchard_b",
      "name": "果园旧布堆",
      "asset": {
        "file": "svg/resource/w11_orchard--w11_orchard_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w11_orchard",
      "nodeId": "w11_orchard_b",
      "shape": "berry",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w12_canal:w12_canal_a",
      "category": "resource",
      "id": "w12_canal:w12_canal_a",
      "name": "渠岸纤维草丛",
      "asset": {
        "file": "svg/resource/w12_canal--w12_canal_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w12_canal",
      "nodeId": "w12_canal_a",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w12_canal:w12_canal_b",
      "category": "resource",
      "id": "w12_canal:w12_canal_b",
      "name": "渠边旧密封件",
      "asset": {
        "file": "svg/resource/w12_canal--w12_canal_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w12_canal",
      "nodeId": "w12_canal_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w13_mill:w13_mill_a",
      "category": "resource",
      "id": "w13_mill:w13_mill_a",
      "name": "磨坊遗留谷物",
      "asset": {
        "file": "svg/resource/w13_mill--w13_mill_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w13_mill",
      "nodeId": "w13_mill_a",
      "shape": "root",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w13_mill:w13_mill_b",
      "category": "resource",
      "id": "w13_mill:w13_mill_b",
      "name": "磨坊旧木板",
      "asset": {
        "file": "svg/resource/w13_mill--w13_mill_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w13_mill",
      "nodeId": "w13_mill_b",
      "shape": "plank",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w14_cemeteryroad:w14_cemeteryroad_a",
      "category": "resource",
      "id": "w14_cemeteryroad:w14_cemeteryroad_a",
      "name": "荒径碎石带",
      "asset": {
        "file": "svg/resource/w14_cemeteryroad--w14_cemeteryroad_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w14_cemeteryroad",
      "nodeId": "w14_cemeteryroad_a",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w14_cemeteryroad:w14_cemeteryroad_b",
      "category": "resource",
      "id": "w14_cemeteryroad:w14_cemeteryroad_b",
      "name": "荒径弃衣堆",
      "asset": {
        "file": "svg/resource/w14_cemeteryroad--w14_cemeteryroad_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w14_cemeteryroad",
      "nodeId": "w14_cemeteryroad_b",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w15_shrinewoods:w15_shrinewoods_a",
      "category": "resource",
      "id": "w15_shrinewoods:w15_shrinewoods_a",
      "name": "林下草药丛",
      "asset": {
        "file": "svg/resource/w15_shrinewoods--w15_shrinewoods_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w15_shrinewoods",
      "nodeId": "w15_shrinewoods_a",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w15_shrinewoods:w15_shrinewoods_b",
      "category": "resource",
      "id": "w15_shrinewoods:w15_shrinewoods_b",
      "name": "祈祷林倒木",
      "asset": {
        "file": "svg/resource/w15_shrinewoods--w15_shrinewoods_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w15_shrinewoods",
      "nodeId": "w15_shrinewoods_b",
      "shape": "plank",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w16_service:w16_service_a",
      "category": "resource",
      "id": "w16_service:w16_service_a",
      "name": "服务站废金属堆",
      "asset": {
        "file": "svg/resource/w16_service--w16_service_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w16_service",
      "nodeId": "w16_service_a",
      "shape": "scrap",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w16_service:w16_service_b",
      "category": "resource",
      "id": "w16_service:w16_service_b",
      "name": "服务站塑料遗料",
      "asset": {
        "file": "svg/resource/w16_service--w16_service_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w16_service",
      "nodeId": "w16_service_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w17_switchback:w17_switchback_a",
      "category": "resource",
      "id": "w17_switchback:w17_switchback_a",
      "name": "山路碎石带",
      "asset": {
        "file": "svg/resource/w17_switchback--w17_switchback_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w17_switchback",
      "nodeId": "w17_switchback_a",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w17_switchback:w17_switchback_b",
      "category": "resource",
      "id": "w17_switchback:w17_switchback_b",
      "name": "山路废导线",
      "asset": {
        "file": "svg/resource/w17_switchback--w17_switchback_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w17_switchback",
      "nodeId": "w17_switchback_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w18_tunnel:w18_tunnel_a",
      "category": "resource",
      "id": "w18_tunnel:w18_tunnel_a",
      "name": "洞外露矿",
      "asset": {
        "file": "svg/resource/w18_tunnel--w18_tunnel_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w18_tunnel",
      "nodeId": "w18_tunnel_a",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w18_tunnel:w18_tunnel_b",
      "category": "resource",
      "id": "w18_tunnel:w18_tunnel_b",
      "name": "隧道维护紧固件",
      "asset": {
        "file": "svg/resource/w18_tunnel--w18_tunnel_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w18_tunnel",
      "nodeId": "w18_tunnel_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w19_survey:w19_survey_a",
      "category": "resource",
      "id": "w19_survey:w19_survey_a",
      "name": "测绘设备旧件",
      "asset": {
        "file": "svg/resource/w19_survey--w19_survey_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w19_survey",
      "nodeId": "w19_survey_a",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w19_survey:w19_survey_b",
      "category": "resource",
      "id": "w19_survey:w19_survey_b",
      "name": "测绘营备用电池",
      "asset": {
        "file": "svg/resource/w19_survey--w19_survey_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w19_survey",
      "nodeId": "w19_survey_b",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w20_powercut:w20_powercut_a",
      "category": "resource",
      "id": "w20_powercut:w20_powercut_a",
      "name": "塔基旧导线",
      "asset": {
        "file": "svg/resource/w20_powercut--w20_powercut_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w20_powercut",
      "nodeId": "w20_powercut_a",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w20_powercut:w20_powercut_b",
      "category": "resource",
      "id": "w20_powercut:w20_powercut_b",
      "name": "高压走廊旧电器",
      "asset": {
        "file": "svg/resource/w20_powercut--w20_powercut_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w20_powercut",
      "nodeId": "w20_powercut_b",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w21_pine:w21_pine_a",
      "category": "resource",
      "id": "w21_pine:w21_pine_a",
      "name": "针叶林倒木",
      "asset": {
        "file": "svg/resource/w21_pine--w21_pine_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w21_pine",
      "nodeId": "w21_pine_a",
      "shape": "plank",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w21_pine:w21_pine_b",
      "category": "resource",
      "id": "w21_pine:w21_pine_b",
      "name": "针叶林纤维草丛",
      "asset": {
        "file": "svg/resource/w21_pine--w21_pine_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w21_pine",
      "nodeId": "w21_pine_b",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w22_ravine:w22_ravine_a",
      "category": "resource",
      "id": "w22_ravine:w22_ravine_a",
      "name": "沟谷露矿",
      "asset": {
        "file": "svg/resource/w22_ravine--w22_ravine_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w22_ravine",
      "nodeId": "w22_ravine_a",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w22_ravine:w22_ravine_b",
      "category": "resource",
      "id": "w22_ravine:w22_ravine_b",
      "name": "沟谷碎石带",
      "asset": {
        "file": "svg/resource/w22_ravine--w22_ravine_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w22_ravine",
      "nodeId": "w22_ravine_b",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w23_hunting:w23_hunting_a",
      "category": "resource",
      "id": "w23_hunting:w23_hunting_a",
      "name": "旧营皮料",
      "asset": {
        "file": "svg/resource/w23_hunting--w23_hunting_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w23_hunting",
      "nodeId": "w23_hunting_a",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w23_hunting:w23_hunting_b",
      "category": "resource",
      "id": "w23_hunting:w23_hunting_b",
      "name": "旧营绳卷",
      "asset": {
        "file": "svg/resource/w23_hunting--w23_hunting_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w23_hunting",
      "nodeId": "w23_hunting_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w24_cave:w24_cave_a",
      "category": "resource",
      "id": "w24_cave:w24_cave_a",
      "name": "洞口矿料堆",
      "asset": {
        "file": "svg/resource/w24_cave--w24_cave_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w24_cave",
      "nodeId": "w24_cave_a",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w24_cave:w24_cave_b",
      "category": "resource",
      "id": "w24_cave:w24_cave_b",
      "name": "洞口旧炭袋",
      "asset": {
        "file": "svg/resource/w24_cave--w24_cave_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w24_cave",
      "nodeId": "w24_cave_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w25_wetland:w25_wetland_a",
      "category": "resource",
      "id": "w25_wetland:w25_wetland_a",
      "name": "湿地草药丛",
      "asset": {
        "file": "svg/resource/w25_wetland--w25_wetland_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w25_wetland",
      "nodeId": "w25_wetland_a",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w25_wetland:w25_wetland_b",
      "category": "resource",
      "id": "w25_wetland:w25_wetland_b",
      "name": "湿地纤维草丛",
      "asset": {
        "file": "svg/resource/w25_wetland--w25_wetland_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w25_wetland",
      "nodeId": "w25_wetland_b",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w26_island:w26_island_a",
      "category": "resource",
      "id": "w26_island:w26_island_a",
      "name": "陆洲块根地",
      "asset": {
        "file": "svg/resource/w26_island--w26_island_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w26_island",
      "nodeId": "w26_island_a",
      "shape": "root",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w26_island:w26_island_b",
      "category": "resource",
      "id": "w26_island:w26_island_b",
      "name": "陆洲盐晶",
      "asset": {
        "file": "svg/resource/w26_island--w26_island_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w26_island",
      "nodeId": "w26_island_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w27_relay:w27_relay_a",
      "category": "resource",
      "id": "w27_relay:w27_relay_a",
      "name": "中继设备遗件",
      "asset": {
        "file": "svg/resource/w27_relay--w27_relay_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w27_relay",
      "nodeId": "w27_relay_a",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w27_relay:w27_relay_b",
      "category": "resource",
      "id": "w27_relay:w27_relay_b",
      "name": "中继营精密机件",
      "asset": {
        "file": "svg/resource/w27_relay--w27_relay_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w27_relay",
      "nodeId": "w27_relay_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w28_highland:w28_highland_a",
      "category": "resource",
      "id": "w28_highland:w28_highland_a",
      "name": "高地碎石带",
      "asset": {
        "file": "svg/resource/w28_highland--w28_highland_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w28_highland",
      "nodeId": "w28_highland_a",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w28_highland:w28_highland_b",
      "category": "resource",
      "id": "w28_highland:w28_highland_b",
      "name": "高地草药丛",
      "asset": {
        "file": "svg/resource/w28_highland--w28_highland_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w28_highland",
      "nodeId": "w28_highland_b",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w29_riverbend:w29_riverbend_a",
      "category": "resource",
      "id": "w29_riverbend:w29_riverbend_a",
      "name": "河弯纤维草丛",
      "asset": {
        "file": "svg/resource/w29_riverbend--w29_riverbend_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w29_riverbend",
      "nodeId": "w29_riverbend_a",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w29_riverbend:w29_riverbend_b",
      "category": "resource",
      "id": "w29_riverbend:w29_riverbend_b",
      "name": "河弯黏土层",
      "asset": {
        "file": "svg/resource/w29_riverbend--w29_riverbend_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w29_riverbend",
      "nodeId": "w29_riverbend_b",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w30_truckstop:w30_truckstop_a",
      "category": "resource",
      "id": "w30_truckstop:w30_truckstop_a",
      "name": "休息区机油余存",
      "asset": {
        "file": "svg/resource/w30_truckstop--w30_truckstop_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w30_truckstop",
      "nodeId": "w30_truckstop_a",
      "shape": "vial",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w30_truckstop:w30_truckstop_b",
      "category": "resource",
      "id": "w30_truckstop:w30_truckstop_b",
      "name": "休息区旧胶料",
      "asset": {
        "file": "svg/resource/w30_truckstop--w30_truckstop_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w30_truckstop",
      "nodeId": "w30_truckstop_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w31_suburb:w31_suburb_a",
      "category": "resource",
      "id": "w31_suburb:w31_suburb_a",
      "name": "棚户旧布堆",
      "asset": {
        "file": "svg/resource/w31_suburb--w31_suburb_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w31_suburb",
      "nodeId": "w31_suburb_a",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w31_suburb:w31_suburb_b",
      "category": "resource",
      "id": "w31_suburb:w31_suburb_b",
      "name": "棚户医用纱布余存",
      "asset": {
        "file": "svg/resource/w31_suburb--w31_suburb_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w31_suburb",
      "nodeId": "w31_suburb_b",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w32_railcut:w32_railcut_a",
      "category": "resource",
      "id": "w32_railcut:w32_railcut_a",
      "name": "铁路废钢料",
      "asset": {
        "file": "svg/resource/w32_railcut--w32_railcut_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w32_railcut",
      "nodeId": "w32_railcut_a",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w32_railcut:w32_railcut_b",
      "category": "resource",
      "id": "w32_railcut:w32_railcut_b",
      "name": "铁路维护紧固件",
      "asset": {
        "file": "svg/resource/w32_railcut--w32_railcut_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w32_railcut",
      "nodeId": "w32_railcut_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w33_waterworks:w33_waterworks_a",
      "category": "resource",
      "id": "w33_waterworks:w33_waterworks_a",
      "name": "水厂黏合剂余存",
      "asset": {
        "file": "svg/resource/w33_waterworks--w33_waterworks_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w33_waterworks",
      "nodeId": "w33_waterworks_a",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w33_waterworks:w33_waterworks_b",
      "category": "resource",
      "id": "w33_waterworks:w33_waterworks_b",
      "name": "水厂消毒液余存",
      "asset": {
        "file": "svg/resource/w33_waterworks--w33_waterworks_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w33_waterworks",
      "nodeId": "w33_waterworks_b",
      "shape": "vial",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w34_depot:w34_depot_a",
      "category": "resource",
      "id": "w34_depot:w34_depot_a",
      "name": "应急仓豆罐头存货",
      "asset": {
        "file": "svg/resource/w34_depot--w34_depot_a.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w34_depot",
      "nodeId": "w34_depot_a",
      "shape": "can",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w34_depot:w34_depot_b",
      "category": "resource",
      "id": "w34_depot:w34_depot_b",
      "name": "应急仓盐水剂存货",
      "asset": {
        "file": "svg/resource/w34_depot--w34_depot_b.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w34_depot",
      "nodeId": "w34_depot_b",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w12_canal:w12_canal_water",
      "category": "resource",
      "id": "w12_canal:w12_canal_water",
      "name": "灌溉渠岸取水口",
      "asset": {
        "file": "svg/resource/w12_canal--w12_canal_water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w12_canal",
      "nodeId": "w12_canal_water",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w25_wetland:w25_wetland_water",
      "category": "resource",
      "id": "w25_wetland:w25_wetland_water",
      "name": "黑水湿地取水口",
      "asset": {
        "file": "svg/resource/w25_wetland--w25_wetland_water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w25_wetland",
      "nodeId": "w25_wetland_water",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w29_riverbend:w29_riverbend_water",
      "category": "resource",
      "id": "w29_riverbend:w29_riverbend_water",
      "name": "河道弯口取水口",
      "asset": {
        "file": "svg/resource/w29_riverbend--w29_riverbend_water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w29_riverbend",
      "nodeId": "w29_riverbend_water",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w33_waterworks:w33_waterworks_water",
      "category": "resource",
      "id": "w33_waterworks:w33_waterworks_water",
      "name": "荒废水厂取水口",
      "asset": {
        "file": "svg/resource/w33_waterworks--w33_waterworks_water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w33_waterworks",
      "nodeId": "w33_waterworks_water",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w08_reservoir:w08_reservoir_water",
      "category": "resource",
      "id": "w08_reservoir:w08_reservoir_water",
      "name": "旧水库取水口",
      "asset": {
        "file": "svg/resource/w08_reservoir--w08_reservoir_water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w08_reservoir",
      "nodeId": "w08_reservoir_water",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w16_service:w16_service_cistern",
      "category": "resource",
      "id": "w16_service:w16_service_cistern",
      "name": "山间服务站蓄水池",
      "asset": {
        "file": "svg/resource/w16_service--w16_service_cistern.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w16_service",
      "nodeId": "w16_service_cistern",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w19_survey:w19_survey_cistern",
      "category": "resource",
      "id": "w19_survey:w19_survey_cistern",
      "name": "测绘营址蓄水池",
      "asset": {
        "file": "svg/resource/w19_survey--w19_survey_cistern.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w19_survey",
      "nodeId": "w19_survey_cistern",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w23_hunting:w23_hunting_cistern",
      "category": "resource",
      "id": "w23_hunting:w23_hunting_cistern",
      "name": "猎人旧营蓄水池",
      "asset": {
        "file": "svg/resource/w23_hunting--w23_hunting_cistern.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w23_hunting",
      "nodeId": "w23_hunting_cistern",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w27_relay:w27_relay_cistern",
      "category": "resource",
      "id": "w27_relay:w27_relay_cistern",
      "name": "废中继营蓄水池",
      "asset": {
        "file": "svg/resource/w27_relay--w27_relay_cistern.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w27_relay",
      "nodeId": "w27_relay_cistern",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w30_truckstop:w30_truckstop_cistern",
      "category": "resource",
      "id": "w30_truckstop:w30_truckstop_cistern",
      "name": "货车休息区蓄水池",
      "asset": {
        "file": "svg/resource/w30_truckstop--w30_truckstop_cistern.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w30_truckstop",
      "nodeId": "w30_truckstop_cistern",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w13_mill:w13_mill_bottles",
      "category": "resource",
      "id": "w13_mill:w13_mill_bottles",
      "name": "旧磨坊瓶装净水",
      "asset": {
        "file": "svg/resource/w13_mill--w13_mill_bottles.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w13_mill",
      "nodeId": "w13_mill_bottles",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w31_suburb:w31_suburb_bottles",
      "category": "resource",
      "id": "w31_suburb:w31_suburb_bottles",
      "name": "城郊棚户瓶装净水",
      "asset": {
        "file": "svg/resource/w31_suburb--w31_suburb_bottles.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w31_suburb",
      "nodeId": "w31_suburb_bottles",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w34_depot:w34_depot_bottles",
      "category": "resource",
      "id": "w34_depot:w34_depot_bottles",
      "name": "应急仓外瓶装净水",
      "asset": {
        "file": "svg/resource/w34_depot--w34_depot_bottles.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w34_depot",
      "nodeId": "w34_depot_bottles",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d01_04:d01_04_cache0",
      "category": "resource",
      "id": "d01_04:d01_04_cache0",
      "name": "藏书密室·布料箱",
      "asset": {
        "file": "svg/resource/d01_04--d01_04_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d01_04",
      "nodeId": "d01_04_cache0",
      "shape": "book",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d01_06:d01_06_cache1",
      "category": "resource",
      "id": "d01_06:d01_06_cache1",
      "name": "骨龛回道·缝线箱",
      "asset": {
        "file": "svg/resource/d01_06--d01_06_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d01_06",
      "nodeId": "d01_06_cache1",
      "shape": "carcass",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d01_04:d01_04_cache2",
      "category": "resource",
      "id": "d01_04:d01_04_cache2",
      "name": "藏书密室·皮革箱",
      "asset": {
        "file": "svg/resource/d01_04--d01_04_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d01_04",
      "nodeId": "d01_04_cache2",
      "shape": "book",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d01_08:d01_08_unique",
      "category": "resource",
      "id": "d01_08:d01_08_unique",
      "name": "圣所旧路档册",
      "asset": {
        "file": "svg/resource/d01_08--d01_08_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d01_08",
      "nodeId": "d01_08_unique",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d02_04:d02_04_cache0",
      "category": "resource",
      "id": "d02_04:d02_04_cache0",
      "name": "控制室·医用纱布箱",
      "asset": {
        "file": "svg/resource/d02_04--d02_04_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d02_04",
      "nodeId": "d02_04_cache0",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d02_06:d02_06_cache1",
      "category": "resource",
      "id": "d02_06:d02_06_cache1",
      "name": "检修竖井·实验室试剂箱",
      "asset": {
        "file": "svg/resource/d02_06--d02_06_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d02_06",
      "nodeId": "d02_06_cache1",
      "shape": "vial",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d02_04:d02_04_cache2",
      "category": "resource",
      "id": "d02_04:d02_04_cache2",
      "name": "控制室·电子组件箱",
      "asset": {
        "file": "svg/resource/d02_04--d02_04_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d02_04",
      "nodeId": "d02_04_cache2",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d02_08:d02_08_unique",
      "category": "resource",
      "id": "d02_08:d02_08_unique",
      "name": "封存区安全规程",
      "asset": {
        "file": "svg/resource/d02_08--d02_08_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d02_08",
      "nodeId": "d02_08_unique",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_04:d03_04_cache0",
      "category": "resource",
      "id": "d03_04:d03_04_cache0",
      "name": "值勤室·武器组件箱",
      "asset": {
        "file": "svg/resource/d03_04--d03_04_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_04",
      "nodeId": "d03_04_cache0",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_06:d03_06_cache1",
      "category": "resource",
      "id": "d03_06:d03_06_cache1",
      "name": "配电房·钢材箱",
      "asset": {
        "file": "svg/resource/d03_06--d03_06_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_06",
      "nodeId": "d03_06_cache1",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_04:d03_04_cache2",
      "category": "resource",
      "id": "d03_04:d03_04_cache2",
      "name": "值勤室·紧固件箱",
      "asset": {
        "file": "svg/resource/d03_04--d03_04_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_04",
      "nodeId": "d03_04_cache2",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_08:d03_08_unique",
      "category": "resource",
      "id": "d03_08:d03_08_unique",
      "name": "警备仓备用电源",
      "asset": {
        "file": "svg/resource/d03_08--d03_08_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_08",
      "nodeId": "d03_08_unique",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d04_03:d04_03_cache0",
      "category": "resource",
      "id": "d04_03:d04_03_cache0",
      "name": "蓄电室·导线箱",
      "asset": {
        "file": "svg/resource/d04_03--d04_03_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d04_03",
      "nodeId": "d04_03_cache0",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d04_05:d04_05_cache1",
      "category": "resource",
      "id": "d04_05:d04_05_cache1",
      "name": "屋顶梯井·电池箱",
      "asset": {
        "file": "svg/resource/d04_05--d04_05_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d04_05",
      "nodeId": "d04_05_cache1",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d04_03:d04_03_cache2",
      "category": "resource",
      "id": "d04_03:d04_03_cache2",
      "name": "蓄电室·电子组件箱",
      "asset": {
        "file": "svg/resource/d04_03--d04_03_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d04_03",
      "nodeId": "d04_03_cache2",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d04_07:d04_07_unique",
      "category": "resource",
      "id": "d04_07:d04_07_unique",
      "name": "通信站联络日志",
      "asset": {
        "file": "svg/resource/d04_07--d04_07_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d04_07",
      "nodeId": "d04_07_unique",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d05_04:d05_04_cache0",
      "category": "resource",
      "id": "d05_04:d05_04_cache0",
      "name": "滤料库·橡胶箱",
      "asset": {
        "file": "svg/resource/d05_04--d05_04_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d05_04",
      "nodeId": "d05_04_cache0",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d05_07:d05_07_cache1",
      "category": "resource",
      "id": "d05_07:d05_07_cache1",
      "name": "阀控室·消毒液箱",
      "asset": {
        "file": "svg/resource/d05_07--d05_07_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d05_07",
      "nodeId": "d05_07_cache1",
      "shape": "vial",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d05_04:d05_04_cache2",
      "category": "resource",
      "id": "d05_04:d05_04_cache2",
      "name": "滤料库·黏合剂箱",
      "asset": {
        "file": "svg/resource/d05_04--d05_04_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d05_04",
      "nodeId": "d05_04_cache2",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d05_07:d05_07_unique",
      "category": "resource",
      "id": "d05_07:d05_07_unique",
      "name": "水厂阀控记录",
      "asset": {
        "file": "svg/resource/d05_07--d05_07_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d05_07",
      "nodeId": "d05_07_unique",
      "shape": "book",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d06_03:d06_03_cache0",
      "category": "resource",
      "id": "d06_03:d06_03_cache0",
      "name": "浅矿脉·铁矿料箱",
      "asset": {
        "file": "svg/resource/d06_03--d06_03_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d06_03",
      "nodeId": "d06_03_cache0",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d06_06:d06_06_cache1",
      "category": "resource",
      "id": "d06_06:d06_06_cache1",
      "name": "矿工营室·铁片箱",
      "asset": {
        "file": "svg/resource/d06_06--d06_06_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d06_06",
      "nodeId": "d06_06_cache1",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d06_03:d06_03_cache2",
      "category": "resource",
      "id": "d06_03:d06_03_cache2",
      "name": "浅矿脉·木炭箱",
      "asset": {
        "file": "svg/resource/d06_03--d06_03_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d06_03",
      "nodeId": "d06_03_cache2",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d06_08:d06_08_unique",
      "category": "resource",
      "id": "d06_08:d06_08_unique",
      "name": "矿道作业手册",
      "asset": {
        "file": "svg/resource/d06_08--d06_08_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d06_08",
      "nodeId": "d06_08_unique",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d07_04:d07_04_cache0",
      "category": "resource",
      "id": "d07_04:d07_04_cache0",
      "name": "服装层·豆罐头箱",
      "asset": {
        "file": "svg/resource/d07_04--d07_04_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d07_04",
      "nodeId": "d07_04_cache0",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d07_07:d07_07_cache1",
      "category": "resource",
      "id": "d07_07:d07_07_cache1",
      "name": "屋顶避难点·布料箱",
      "asset": {
        "file": "svg/resource/d07_07--d07_07_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d07_07",
      "nodeId": "d07_07_cache1",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d07_04:d07_04_cache2",
      "category": "resource",
      "id": "d07_04:d07_04_cache2",
      "name": "服装层·塑料箱",
      "asset": {
        "file": "svg/resource/d07_04--d07_04_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d07_04",
      "nodeId": "d07_04_cache2",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d07_07:d07_07_unique",
      "category": "resource",
      "id": "d07_07:d07_07_unique",
      "name": "商场避难物资簿",
      "asset": {
        "file": "svg/resource/d07_07--d07_07_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d07_07",
      "nodeId": "d07_07_unique",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d08_03:d08_03_cache0",
      "category": "resource",
      "id": "d08_03:d08_03_cache0",
      "name": "干货库·盐水剂箱",
      "asset": {
        "file": "svg/resource/d08_03--d08_03_cache0.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d08_03",
      "nodeId": "d08_03_cache0",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d08_06:d08_06_cache1",
      "category": "resource",
      "id": "d08_06:d08_06_cache1",
      "name": "备用电房·医用纱布箱",
      "asset": {
        "file": "svg/resource/d08_06--d08_06_cache1.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d08_06",
      "nodeId": "d08_06_cache1",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d08_03:d08_03_cache2",
      "category": "resource",
      "id": "d08_03:d08_03_cache2",
      "name": "干货库·肉干箱",
      "asset": {
        "file": "svg/resource/d08_03--d08_03_cache2.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d08_03",
      "nodeId": "d08_03_cache2",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d08_08:d08_08_unique",
      "category": "resource",
      "id": "d08_08:d08_08_unique",
      "name": "冷链调度档案",
      "asset": {
        "file": "svg/resource/d08_08--d08_08_unique.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d08_08",
      "nodeId": "d08_08_unique",
      "shape": "book",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h03_workshop:h03_workshop_supply_disinfectant",
      "category": "resource",
      "id": "h03_workshop:h03_workshop_supply_disinfectant",
      "name": "公共工坊消毒液配给柜",
      "asset": {
        "file": "svg/resource/h03_workshop--h03_workshop_supply_disinfectant.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h03_workshop",
      "nodeId": "h03_workshop_supply_disinfectant",
      "shape": "vial",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w13_mill:w13_mill_supply_salt",
      "category": "resource",
      "id": "w13_mill:w13_mill_supply_salt",
      "name": "磨坊食盐存货",
      "asset": {
        "file": "svg/resource/w13_mill--w13_mill_supply_salt.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w13_mill",
      "nodeId": "w13_mill_supply_salt",
      "shape": "can",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w15_shrinewoods:w15_shrinewoods_supply_berry",
      "category": "resource",
      "id": "w15_shrinewoods:w15_shrinewoods_supply_berry",
      "name": "祈祷林野果丛",
      "asset": {
        "file": "svg/resource/w15_shrinewoods--w15_shrinewoods_supply_berry.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w15_shrinewoods",
      "nodeId": "w15_shrinewoods_supply_berry",
      "shape": "berry",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:m01_road:m01_road_crate",
      "category": "resource",
      "id": "m01_road:m01_road_crate",
      "name": "路旁旧货箱",
      "asset": {
        "file": "svg/resource/m01_road--m01_road_crate.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "m01_road",
      "nodeId": "m01_road_crate",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:m01_road:m01_road_barrier",
      "category": "resource",
      "id": "m01_road:m01_road_barrier",
      "name": "路障金属堆",
      "asset": {
        "file": "svg/resource/m01_road--m01_road_barrier.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "m01_road",
      "nodeId": "m01_road_barrier",
      "shape": "scrap",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:m02_checkpoint:m02_checkpoint_duty",
      "category": "resource",
      "id": "m02_checkpoint:m02_checkpoint_duty",
      "name": "检查哨值勤补给箱",
      "asset": {
        "file": "svg/resource/m02_checkpoint--m02_checkpoint_duty.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "m02_checkpoint",
      "nodeId": "m02_checkpoint_duty",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:m02_checkpoint:m02_checkpoint_bench",
      "category": "resource",
      "id": "m02_checkpoint:m02_checkpoint_bench",
      "name": "哨站维修台遗料",
      "asset": {
        "file": "svg/resource/m02_checkpoint--m02_checkpoint_bench.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "m02_checkpoint",
      "nodeId": "m02_checkpoint_bench",
      "shape": "part",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h02_square:h02_square_water",
      "category": "resource",
      "id": "h02_square:h02_square_water",
      "name": "营区净水配给处",
      "asset": {
        "file": "svg/resource/h02_square--h02_square_water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h02_square",
      "nodeId": "h02_square_water",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h02_square:h02_square_ration",
      "category": "resource",
      "id": "h02_square:h02_square_ration",
      "name": "营区罐头配给处",
      "asset": {
        "file": "svg/resource/h02_square--h02_square_ration.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h02_square",
      "nodeId": "h02_square_ration",
      "shape": "can",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h03_workshop:h03_workshop_scrap",
      "category": "resource",
      "id": "h03_workshop:h03_workshop_scrap",
      "name": "公共工坊旧件回收台",
      "asset": {
        "file": "svg/resource/h03_workshop--h03_workshop_scrap.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h03_workshop",
      "nodeId": "h03_workshop_scrap",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h04_infirmary:h04_infirmary_dressing",
      "category": "resource",
      "id": "h04_infirmary:h04_infirmary_dressing",
      "name": "医务室纱布配给柜",
      "asset": {
        "file": "svg/resource/h04_infirmary--h04_infirmary_dressing.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h04_infirmary",
      "nodeId": "h04_infirmary_dressing",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w01_farm:w01_farm_crop",
      "category": "resource",
      "id": "w01_farm:w01_farm_crop",
      "name": "农场块根畦",
      "asset": {
        "file": "svg/resource/w01_farm--w01_farm_crop.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w01_farm",
      "nodeId": "w01_farm_crop",
      "shape": "root",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w01_farm:w01_farm_barn",
      "category": "resource",
      "id": "w01_farm:w01_farm_barn",
      "name": "农场旧谷仓布料",
      "asset": {
        "file": "svg/resource/w01_farm--w01_farm_barn.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w01_farm",
      "nodeId": "w01_farm_barn",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w02_forest:w02_forest_wood",
      "category": "resource",
      "id": "w02_forest:w02_forest_wood",
      "name": "林间倒木",
      "asset": {
        "file": "svg/resource/w02_forest--w02_forest_wood.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w02_forest",
      "nodeId": "w02_forest_wood",
      "shape": "plank",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w02_forest:w02_forest_fiber",
      "category": "resource",
      "id": "w02_forest:w02_forest_fiber",
      "name": "林间纤维草丛",
      "asset": {
        "file": "svg/resource/w02_forest--w02_forest_fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w02_forest",
      "nodeId": "w02_forest_fiber",
      "shape": "foliage",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w03_quarry:w03_quarry_stone",
      "category": "resource",
      "id": "w03_quarry:w03_quarry_stone",
      "name": "坡脚碎石带",
      "asset": {
        "file": "svg/resource/w03_quarry--w03_quarry_stone.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w03_quarry",
      "nodeId": "w03_quarry_stone",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w03_quarry:w03_quarry_ore",
      "category": "resource",
      "id": "w03_quarry:w03_quarry_ore",
      "name": "采石坡露矿",
      "asset": {
        "file": "svg/resource/w03_quarry--w03_quarry_ore.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w03_quarry",
      "nodeId": "w03_quarry_ore",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a01_yard:a01_yard_steel",
      "category": "resource",
      "id": "a01_yard:a01_yard_steel",
      "name": "货场废钢垛",
      "asset": {
        "file": "svg/resource/a01_yard--a01_yard_steel.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a01_yard",
      "nodeId": "a01_yard_steel",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a01_yard:a01_yard_fastener",
      "category": "resource",
      "id": "a01_yard:a01_yard_fastener",
      "name": "货场紧固件匣",
      "asset": {
        "file": "svg/resource/a01_yard--a01_yard_fastener.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a01_yard",
      "nodeId": "a01_yard_fastener",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a02_pump:a02_pump_seal",
      "category": "resource",
      "id": "a02_pump:a02_pump_seal",
      "name": "泵站密封件架",
      "asset": {
        "file": "svg/resource/a02_pump--a02_pump_seal.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a02_pump",
      "nodeId": "a02_pump_seal",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a02_pump:a02_pump_fuel",
      "category": "resource",
      "id": "a02_pump:a02_pump_fuel",
      "name": "泵站机油余存",
      "asset": {
        "file": "svg/resource/a02_pump--a02_pump_fuel.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a02_pump",
      "nodeId": "a02_pump_fuel",
      "shape": "vial",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a03_foundry:a03_foundry_steel",
      "category": "resource",
      "id": "a03_foundry:a03_foundry_steel",
      "name": "厂外钢料垛",
      "asset": {
        "file": "svg/resource/a03_foundry--a03_foundry_steel.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a03_foundry",
      "nodeId": "a03_foundry_steel",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a03_foundry:a03_foundry_machine",
      "category": "resource",
      "id": "a03_foundry:a03_foundry_machine",
      "name": "厂外机件堆",
      "asset": {
        "file": "svg/resource/a03_foundry--a03_foundry_machine.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a03_foundry",
      "nodeId": "a03_foundry_machine",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b01_school:b01_school_fabric",
      "category": "resource",
      "id": "b01_school:b01_school_fabric",
      "name": "安置学校旧布架",
      "asset": {
        "file": "svg/resource/b01_school--b01_school_fabric.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b01_school",
      "nodeId": "b01_school_fabric",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b01_school:b01_school_leather",
      "category": "resource",
      "id": "b01_school:b01_school_leather",
      "name": "安置学校旧皮料",
      "asset": {
        "file": "svg/resource/b01_school--b01_school_leather.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b01_school",
      "nodeId": "b01_school_leather",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b02_station:b02_station_medical",
      "category": "resource",
      "id": "b02_station:b02_station_medical",
      "name": "转运站纱布配给柜",
      "asset": {
        "file": "svg/resource/b02_station--b02_station_medical.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b02_station",
      "nodeId": "b02_station_medical",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b02_station:b02_station_electronic",
      "category": "resource",
      "id": "b02_station:b02_station_electronic",
      "name": "转运站退役设备",
      "asset": {
        "file": "svg/resource/b02_station--b02_station_electronic.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b02_station",
      "nodeId": "b02_station_electronic",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b03_hospital:b03_hospital_medical",
      "category": "resource",
      "id": "b03_hospital:b03_hospital_medical",
      "name": "外院医用纱布箱",
      "asset": {
        "file": "svg/resource/b03_hospital--b03_hospital_medical.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b03_hospital",
      "nodeId": "b03_hospital_medical",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b03_hospital:b03_hospital_electronic",
      "category": "resource",
      "id": "b03_hospital:b03_hospital_electronic",
      "name": "外院旧设备间",
      "asset": {
        "file": "svg/resource/b03_hospital--b03_hospital_electronic.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b03_hospital",
      "nodeId": "b03_hospital_electronic",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w02_forest:w02_forest_plank",
      "category": "resource",
      "id": "w02_forest:w02_forest_plank",
      "name": "林间旧木板堆",
      "asset": {
        "file": "svg/resource/w02_forest--w02_forest_plank.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w02_forest",
      "nodeId": "w02_forest_plank",
      "shape": "plank",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w02_forest:w02_forest_rope",
      "category": "resource",
      "id": "w02_forest:w02_forest_rope",
      "name": "林间旧绳卷",
      "asset": {
        "file": "svg/resource/w02_forest--w02_forest_rope.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w02_forest",
      "nodeId": "w02_forest_rope",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b01_school:b01_school_thread",
      "category": "resource",
      "id": "b01_school:b01_school_thread",
      "name": "安置学校缝线配给柜",
      "asset": {
        "file": "svg/resource/b01_school--b01_school_thread.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b01_school",
      "nodeId": "b01_school_thread",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:m01_road:m01_road_plastic",
      "category": "resource",
      "id": "m01_road:m01_road_plastic",
      "name": "公路塑料遗料",
      "asset": {
        "file": "svg/resource/m01_road--m01_road_plastic.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "m01_road",
      "nodeId": "m01_road_plastic",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a01_yard:a01_yard_wire",
      "category": "resource",
      "id": "a01_yard:a01_yard_wire",
      "name": "货场旧导线卷",
      "asset": {
        "file": "svg/resource/a01_yard--a01_yard_wire.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a01_yard",
      "nodeId": "a01_yard_wire",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a02_pump:a02_pump_adhesive",
      "category": "resource",
      "id": "a02_pump:a02_pump_adhesive",
      "name": "泵站黏合剂余存",
      "asset": {
        "file": "svg/resource/a02_pump--a02_pump_adhesive.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a02_pump",
      "nodeId": "a02_pump_adhesive",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a02_pump:a02_pump_charcoal",
      "category": "resource",
      "id": "a02_pump:a02_pump_charcoal",
      "name": "泵站旧炭袋",
      "asset": {
        "file": "svg/resource/a02_pump--a02_pump_charcoal.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a02_pump",
      "nodeId": "a02_pump_charcoal",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b02_station:b02_station_battery",
      "category": "resource",
      "id": "b02_station:b02_station_battery",
      "name": "转运站备用电池柜",
      "asset": {
        "file": "svg/resource/b02_station--b02_station_battery.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b02_station",
      "nodeId": "b02_station_battery",
      "shape": "chip",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b02_station:b02_station_disinfectant",
      "category": "resource",
      "id": "b02_station:b02_station_disinfectant",
      "name": "转运站消毒液配给柜",
      "asset": {
        "file": "svg/resource/b02_station--b02_station_disinfectant.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b02_station",
      "nodeId": "b02_station_disinfectant",
      "shape": "vial",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h04_infirmary:h04_infirmary_saline",
      "category": "resource",
      "id": "h04_infirmary:h04_infirmary_saline",
      "name": "医务室盐水剂配给柜",
      "asset": {
        "file": "svg/resource/h04_infirmary--h04_infirmary_saline.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h04_infirmary",
      "nodeId": "h04_infirmary_saline",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:a03_foundry:a03_foundry_weapon_parts",
      "category": "resource",
      "id": "a03_foundry:a03_foundry_weapon_parts",
      "name": "厂外武器组件箱",
      "asset": {
        "file": "svg/resource/a03_foundry--a03_foundry_weapon_parts.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "a03_foundry",
      "nodeId": "a03_foundry_weapon_parts",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:b03_hospital:b03_hospital_ballistic_fiber",
      "category": "resource",
      "id": "b03_hospital:b03_hospital_ballistic_fiber",
      "name": "外院防护纤维存货",
      "asset": {
        "file": "svg/resource/b03_hospital--b03_hospital_ballistic_fiber.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "b03_hospital",
      "nodeId": "b03_hospital_ballistic_fiber",
      "shape": "cloth",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w01_farm:w01_farm_grain",
      "category": "resource",
      "id": "w01_farm:w01_farm_grain",
      "name": "农场谷物畦",
      "asset": {
        "file": "svg/resource/w01_farm--w01_farm_grain.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w01_farm",
      "nodeId": "w01_farm_grain",
      "shape": "root",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h02_square:h02_square_beans",
      "category": "resource",
      "id": "h02_square:h02_square_beans",
      "name": "营区豆罐头配给处",
      "asset": {
        "file": "svg/resource/h02_square--h02_square_beans.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h02_square",
      "nodeId": "h02_square_beans",
      "shape": "can",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w01_farm:w01_farm_salt",
      "category": "resource",
      "id": "w01_farm:w01_farm_salt",
      "name": "农场食盐存货",
      "asset": {
        "file": "svg/resource/w01_farm--w01_farm_salt.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w01_farm",
      "nodeId": "w01_farm_salt",
      "shape": "can",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w01_farm:w01_farm_cooking_oil",
      "category": "resource",
      "id": "w01_farm:w01_farm_cooking_oil",
      "name": "农场食用油存货",
      "asset": {
        "file": "svg/resource/w01_farm--w01_farm_cooking_oil.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w01_farm",
      "nodeId": "w01_farm_cooking_oil",
      "shape": "can",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h01_gate:h01_gate_raw_water",
      "category": "resource",
      "id": "h01_gate:h01_gate_raw_water",
      "name": "营门蓄水渠",
      "asset": {
        "file": "svg/resource/h01_gate--h01_gate_raw_water.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h01_gate",
      "nodeId": "h01_gate_raw_water",
      "shape": "water",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w03_quarry:w03_quarry_plate",
      "category": "resource",
      "id": "w03_quarry:w03_quarry_plate",
      "name": "采石坡铁片回收堆",
      "asset": {
        "file": "svg/resource/w03_quarry--w03_quarry_plate.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w03_quarry",
      "nodeId": "w03_quarry_plate",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:h03_workshop:h03_workshop_contract",
      "category": "resource",
      "id": "h03_workshop:h03_workshop_contract",
      "name": "工坊首次支援包",
      "asset": {
        "file": "svg/resource/h03_workshop--h03_workshop_contract.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "h03_workshop",
      "nodeId": "h03_workshop_contract",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:m01_road:firetruck_cache",
      "category": "resource",
      "id": "m01_road:firetruck_cache",
      "name": "消防车器材箱",
      "asset": {
        "file": "svg/resource/m01_road--firetruck_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "m01_road",
      "nodeId": "firetruck_cache",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:w03_quarry:w03_quarry_blueprints",
      "category": "resource",
      "id": "w03_quarry:w03_quarry_blueprints",
      "name": "采石坡野外装备图册",
      "asset": {
        "file": "svg/resource/w03_quarry--w03_quarry_blueprints.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "w03_quarry",
      "nodeId": "w03_quarry_blueprints",
      "shape": "rock",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_03:shield_cache",
      "category": "resource",
      "id": "d03_03:shield_cache",
      "name": "器材室防暴盾箱",
      "asset": {
        "file": "svg/resource/d03_03--shield_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_03",
      "nodeId": "shield_cache",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_07:officer_cache",
      "category": "resource",
      "id": "d03_07:officer_cache",
      "name": "守卫战利品箱",
      "asset": {
        "file": "svg/resource/d03_07--officer_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_07",
      "nodeId": "officer_cache",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_08:shotgun_cache",
      "category": "resource",
      "id": "d03_08:shotgun_cache",
      "name": "封存仓霰弹枪箱",
      "asset": {
        "file": "svg/resource/d03_08--shotgun_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_08",
      "nodeId": "shotgun_cache",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_08:rifle_cache",
      "category": "resource",
      "id": "d03_08:rifle_cache",
      "name": "封存仓步枪箱",
      "asset": {
        "file": "svg/resource/d03_08--rifle_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_08",
      "nodeId": "rifle_cache",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_08:armor_cache",
      "category": "resource",
      "id": "d03_08:armor_cache",
      "name": "封存仓防护装备箱",
      "asset": {
        "file": "svg/resource/d03_08--armor_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_08",
      "nodeId": "armor_cache",
      "shape": "crate",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "resource:d03_08:ammo_reserve_cache",
      "category": "resource",
      "id": "d03_08:ammo_reserve_cache",
      "name": "封存仓备用弹药箱",
      "asset": {
        "file": "svg/resource/d03_08--ammo_reserve_cache.svg",
        "format": "svg",
        "alpha": true
      },
      "location": "d03_08",
      "nodeId": "ammo_reserve_cache",
      "shape": "medkit",
      "source": "data.resources",
      "entry": "ObjectCard · DetailPanel · 稳定地点/资源点键"
    },
    {
      "key": "facility:hand",
      "category": "facility",
      "id": "hand",
      "name": "手工",
      "asset": {
        "file": "svg/facility/hand.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/hand.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:workbench",
      "category": "facility",
      "id": "workbench",
      "name": "工作台",
      "asset": {
        "file": "svg/facility/workbench.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/workbench.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:forge",
      "category": "facility",
      "id": "forge",
      "name": "锻炉",
      "asset": {
        "file": "svg/facility/forge.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/forge.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:sewing",
      "category": "facility",
      "id": "sewing",
      "name": "缝纫台",
      "asset": {
        "file": "svg/facility/sewing.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/sewing.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:medical",
      "category": "facility",
      "id": "medical",
      "name": "医疗台",
      "asset": {
        "file": "svg/facility/medical.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/medical.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:armory",
      "category": "facility",
      "id": "armory",
      "name": "军械台",
      "asset": {
        "file": "svg/facility/armory.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/armory.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:kiln",
      "category": "facility",
      "id": "kiln",
      "name": "陶窑",
      "asset": {
        "file": "svg/facility/kiln.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/kiln.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:water_station",
      "category": "facility",
      "id": "water_station",
      "name": "固定净水台",
      "asset": {
        "file": "svg/facility/water_station.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/water_station.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:drying_rack",
      "category": "facility",
      "id": "drying_rack",
      "name": "晾制架",
      "asset": {
        "file": "svg/facility/drying_rack.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/drying_rack.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:fire_lit",
      "category": "facility",
      "id": "fire_lit",
      "name": "火堆 · 燃烧",
      "asset": {
        "file": "../UI分层样件/art/facility-fire-lit.png",
        "format": "png",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/fire_lit.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:fire_out",
      "category": "facility",
      "id": "fire_out",
      "name": "火堆 · 未燃",
      "asset": {
        "file": "svg/facility/fire_out.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/fire_out.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:pot",
      "category": "facility",
      "id": "pot",
      "name": "锅",
      "asset": {
        "file": "svg/facility/pot.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/pot.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:bed",
      "category": "facility",
      "id": "bed",
      "name": "铺垫",
      "asset": {
        "file": "svg/facility/bed.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/bed.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:shelter",
      "category": "facility",
      "id": "shelter",
      "name": "遮蔽棚",
      "asset": {
        "file": "svg/facility/shelter.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/shelter.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:safehouse",
      "category": "facility",
      "id": "safehouse",
      "name": "安全屋",
      "asset": {
        "file": "svg/facility/safehouse.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/safehouse.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:camping",
      "category": "facility",
      "id": "camping",
      "name": "临时扎营",
      "asset": {
        "file": "svg/facility/camping.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/camping.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:catchment",
      "category": "facility",
      "id": "catchment",
      "name": "集水设施",
      "asset": {
        "file": "svg/facility/catchment.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/catchment.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:storage",
      "category": "facility",
      "id": "storage",
      "name": "储藏设施",
      "asset": {
        "file": "svg/facility/storage.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/storage.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "facility:charcoal",
      "category": "facility",
      "id": "charcoal",
      "name": "炭化",
      "asset": {
        "file": "svg/facility/charcoal.svg",
        "format": "svg",
        "alpha": true
      },
      "native": {
        "file": "svg/facility/charcoal.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "data.workshops / fire / safehouse / S09–S11",
      "entry": "FacilityCard · FireCard · PotCard · DryingRackCard"
    },
    {
      "key": "publicStation:public:h03_workshop:workbench",
      "category": "publicStation",
      "id": "public:h03_workshop:workbench",
      "name": "营区工坊工作台",
      "asset": {
        "ref": "facility:workbench"
      },
      "source": "data.city.publicStations",
      "location": "h03_workshop",
      "entry": "PublicStationCard"
    },
    {
      "key": "publicStation:public:h03_workshop:forge",
      "category": "publicStation",
      "id": "public:h03_workshop:forge",
      "name": "营区工坊锻炉",
      "asset": {
        "ref": "facility:forge"
      },
      "source": "data.city.publicStations",
      "location": "h03_workshop",
      "entry": "PublicStationCard"
    },
    {
      "key": "publicStation:public:h03_workshop:armory",
      "category": "publicStation",
      "id": "public:h03_workshop:armory",
      "name": "营区工坊军械台",
      "asset": {
        "ref": "facility:armory"
      },
      "source": "data.city.publicStations",
      "location": "h03_workshop",
      "entry": "PublicStationCard"
    },
    {
      "key": "publicStation:public:r07_workshop:workbench",
      "category": "publicStation",
      "id": "public:r07_workshop:workbench",
      "name": "试制工坊工作台",
      "asset": {
        "ref": "facility:workbench"
      },
      "source": "data.city.publicStations",
      "location": "r07_workshop",
      "entry": "PublicStationCard"
    },
    {
      "key": "publicStation:public:h02_square:sewing",
      "category": "publicStation",
      "id": "public:h02_square:sewing",
      "name": "营区公共缝纫台",
      "asset": {
        "ref": "facility:sewing"
      },
      "source": "data.city.publicStations",
      "location": "h02_square",
      "entry": "PublicStationCard"
    },
    {
      "key": "publicStation:public:h04_infirmary:medical",
      "category": "publicStation",
      "id": "public:h04_infirmary:medical",
      "name": "医务室公共医疗台",
      "asset": {
        "ref": "facility:medical"
      },
      "source": "data.city.publicStations",
      "location": "h04_infirmary",
      "entry": "PublicStationCard"
    },
    {
      "key": "process:boil_water",
      "category": "process",
      "id": "boil_water",
      "name": "烧水",
      "asset": {
        "ref": "item:clean_water"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:roast_root",
      "category": "process",
      "id": "roast_root",
      "name": "烤制",
      "asset": {
        "ref": "item:cooked_root"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:boil_root",
      "category": "process",
      "id": "boil_root",
      "name": "水煮",
      "asset": {
        "ref": "item:boiled_root"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:roast_meat",
      "category": "process",
      "id": "roast_meat",
      "name": "烤制",
      "asset": {
        "ref": "item:cooked_meat"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:boil_meat",
      "category": "process",
      "id": "boil_meat",
      "name": "水煮",
      "asset": {
        "ref": "item:boiled_meat"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:reboil_cooked_meat",
      "category": "process",
      "id": "reboil_cooked_meat",
      "name": "复煮",
      "asset": {
        "ref": "item:boiled_meat"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:fire_charcoal",
      "category": "process",
      "id": "fire_charcoal",
      "name": "煅烧",
      "asset": {
        "ref": "item:charcoal"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:pot_hardtack",
      "category": "process",
      "id": "pot_hardtack",
      "name": "烘制",
      "asset": {
        "ref": "item:hardtack"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "process:pot_stew",
      "category": "process",
      "id": "pot_stew",
      "name": "炖煮",
      "asset": {
        "ref": "item:stew_portion"
      },
      "source": "data.processes",
      "entry": "FireCard · PotCard · CentralProgress"
    },
    {
      "key": "recipe:knife",
      "category": "recipe",
      "id": "knife",
      "name": "石刀",
      "asset": {
        "ref": "item:stone_knife"
      },
      "station": "hand",
      "output": "stone_knife",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:axe",
      "category": "recipe",
      "id": "axe",
      "name": "简易斧",
      "asset": {
        "ref": "item:hatchet"
      },
      "station": "hand",
      "output": "hatchet",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:bandage",
      "category": "recipe",
      "id": "bandage",
      "name": "草药绷带",
      "asset": {
        "ref": "item:bandage"
      },
      "station": "hand",
      "output": "bandage",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:shelter",
      "category": "recipe",
      "id": "shelter",
      "name": "建遮蔽棚",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:bed",
      "category": "recipe",
      "id": "bed",
      "name": "铺床垫",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_wooden_spear",
      "category": "recipe",
      "id": "make_wooden_spear",
      "name": "木矛",
      "asset": {
        "ref": "item:wooden_spear"
      },
      "station": "hand",
      "output": "wooden_spear",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_wooden_bow",
      "category": "recipe",
      "id": "make_wooden_bow",
      "name": "木弓",
      "asset": {
        "ref": "item:wooden_bow"
      },
      "station": "hand",
      "output": "wooden_bow",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_arrows",
      "category": "recipe",
      "id": "make_arrows",
      "name": "箭矢",
      "asset": {
        "ref": "item:arrow"
      },
      "station": "hand",
      "output": "arrow",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:repair_selected_tool",
      "category": "recipe",
      "id": "repair_selected_tool",
      "name": "工具维护",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_antiviral_cure",
      "category": "recipe",
      "id": "make_antiviral_cure",
      "name": "病毒特效药",
      "asset": {
        "ref": "item:antiviral_cure"
      },
      "station": "hand",
      "output": "antiviral_cure",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_metal_spear",
      "category": "recipe",
      "id": "make_metal_spear",
      "name": "金属矛",
      "asset": {
        "ref": "item:metal_spear"
      },
      "station": "hand",
      "output": "metal_spear",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_crossbow",
      "category": "recipe",
      "id": "make_crossbow",
      "name": "弩",
      "asset": {
        "ref": "item:crossbow"
      },
      "station": "hand",
      "output": "crossbow",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_bolts",
      "category": "recipe",
      "id": "make_bolts",
      "name": "弩矢",
      "asset": {
        "ref": "item:bolt"
      },
      "station": "hand",
      "output": "bolt",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_knuckle_wraps",
      "category": "recipe",
      "id": "make_knuckle_wraps",
      "name": "护拳绑带",
      "asset": {
        "ref": "item:knuckle_wraps"
      },
      "station": "hand",
      "output": "knuckle_wraps",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:salvage_equipment",
      "category": "recipe",
      "id": "salvage_equipment",
      "name": "拆解装备",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_workbench",
      "category": "recipe",
      "id": "build_workbench",
      "name": "建工作台",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_forge",
      "category": "recipe",
      "id": "build_forge",
      "name": "建锻炉",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_sewing",
      "category": "recipe",
      "id": "build_sewing",
      "name": "建缝纫台",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_medical",
      "category": "recipe",
      "id": "build_medical",
      "name": "建医疗台",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_armory",
      "category": "recipe",
      "id": "build_armory",
      "name": "建军械台",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_machete",
      "category": "recipe",
      "id": "make_machete",
      "name": "砍刀",
      "asset": {
        "ref": "item:machete"
      },
      "station": "workbench",
      "output": "machete",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_guard_sword",
      "category": "recipe",
      "id": "make_guard_sword",
      "name": "守备短剑",
      "asset": {
        "ref": "item:guard_sword"
      },
      "station": "forge",
      "output": "guard_sword",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_breach_axe",
      "category": "recipe",
      "id": "make_breach_axe",
      "name": "破障斧",
      "asset": {
        "ref": "item:breach_axe"
      },
      "station": "forge",
      "output": "breach_axe",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_rescue_axe",
      "category": "recipe",
      "id": "make_rescue_axe",
      "name": "救援短斧",
      "asset": {
        "ref": "item:rescue_axe"
      },
      "station": "forge",
      "output": "rescue_axe",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_pike",
      "category": "recipe",
      "id": "make_pike",
      "name": "加固长枪",
      "asset": {
        "ref": "item:pike"
      },
      "station": "forge",
      "output": "pike",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_barbed_spear",
      "category": "recipe",
      "id": "make_barbed_spear",
      "name": "倒刺短矛",
      "asset": {
        "ref": "item:barbed_spear"
      },
      "station": "forge",
      "output": "barbed_spear",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_wooden_club",
      "category": "recipe",
      "id": "make_wooden_club",
      "name": "木棍",
      "asset": {
        "ref": "item:wooden_club"
      },
      "station": "hand",
      "output": "wooden_club",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_steel_mace",
      "category": "recipe",
      "id": "make_steel_mace",
      "name": "钢制钉锤",
      "asset": {
        "ref": "item:steel_mace"
      },
      "station": "forge",
      "output": "steel_mace",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_shock_baton",
      "category": "recipe",
      "id": "make_shock_baton",
      "name": "绝缘警棍",
      "asset": {
        "ref": "item:shock_baton"
      },
      "station": "workbench",
      "output": "shock_baton",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_recurve_bow",
      "category": "recipe",
      "id": "make_recurve_bow",
      "name": "复合木弓",
      "asset": {
        "ref": "item:recurve_bow"
      },
      "station": "workbench",
      "output": "recurve_bow",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_hunter_bow",
      "category": "recipe",
      "id": "make_hunter_bow",
      "name": "猎手弓",
      "asset": {
        "ref": "item:hunter_bow"
      },
      "station": "workbench",
      "output": "hunter_bow",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_repeater_crossbow",
      "category": "recipe",
      "id": "make_repeater_crossbow",
      "name": "轻型战术弩",
      "asset": {
        "ref": "item:repeater_crossbow"
      },
      "station": "armory",
      "output": "repeater_crossbow",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_wooden_shield",
      "category": "recipe",
      "id": "make_wooden_shield",
      "name": "木盾",
      "asset": {
        "ref": "item:wooden_shield"
      },
      "station": "hand",
      "output": "wooden_shield",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_scrap_shield",
      "category": "recipe",
      "id": "make_scrap_shield",
      "name": "包铁盾",
      "asset": {
        "ref": "item:scrap_shield"
      },
      "station": "workbench",
      "output": "scrap_shield",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_rescue_shield",
      "category": "recipe",
      "id": "make_rescue_shield",
      "name": "救援盾",
      "asset": {
        "ref": "item:rescue_shield"
      },
      "station": "workbench",
      "output": "rescue_shield",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_signal_whistle",
      "category": "recipe",
      "id": "make_signal_whistle",
      "name": "信号哨",
      "asset": {
        "ref": "item:signal_whistle"
      },
      "station": "hand",
      "output": "signal_whistle",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_signal_flag",
      "category": "recipe",
      "id": "make_signal_flag",
      "name": "战术旗",
      "asset": {
        "ref": "item:signal_flag"
      },
      "station": "sewing",
      "output": "signal_flag",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_field_radio",
      "category": "recipe",
      "id": "make_field_radio",
      "name": "战术电台",
      "asset": {
        "ref": "item:field_radio"
      },
      "station": "workbench",
      "output": "field_radio",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_command_terminal",
      "category": "recipe",
      "id": "make_command_terminal",
      "name": "便携指挥终端",
      "asset": {
        "ref": "item:command_terminal"
      },
      "station": "workbench",
      "output": "command_terminal",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_field_medkit",
      "category": "recipe",
      "id": "make_field_medkit",
      "name": "随行医疗包",
      "asset": {
        "ref": "item:field_medkit"
      },
      "station": "hand",
      "output": "field_medkit",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_trauma_bag",
      "category": "recipe",
      "id": "make_trauma_bag",
      "name": "创伤救护包",
      "asset": {
        "ref": "item:trauma_bag"
      },
      "station": "sewing",
      "output": "trauma_bag",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_rescue_medkit",
      "category": "recipe",
      "id": "make_rescue_medkit",
      "name": "救援医疗箱",
      "asset": {
        "ref": "item:rescue_medkit"
      },
      "station": "medical",
      "output": "rescue_medkit",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_triage_kit",
      "category": "recipe",
      "id": "make_triage_kit",
      "name": "分诊器材组",
      "asset": {
        "ref": "item:triage_kit"
      },
      "station": "medical",
      "output": "triage_kit",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_padded_vest",
      "category": "recipe",
      "id": "make_padded_vest",
      "name": "衬垫护具",
      "asset": {
        "ref": "item:padded_vest"
      },
      "station": "hand",
      "output": "padded_vest",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_leather_armor",
      "category": "recipe",
      "id": "make_leather_armor",
      "name": "皮革护具",
      "asset": {
        "ref": "item:leather_armor"
      },
      "station": "sewing",
      "output": "leather_armor",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_scrap_armor",
      "category": "recipe",
      "id": "make_scrap_armor",
      "name": "拼接铁甲",
      "asset": {
        "ref": "item:scrap_armor"
      },
      "station": "workbench",
      "output": "scrap_armor",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_scout_armor",
      "category": "recipe",
      "id": "make_scout_armor",
      "name": "侦察护具",
      "asset": {
        "ref": "item:scout_armor"
      },
      "station": "sewing",
      "output": "scout_armor",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_heavy_armor",
      "category": "recipe",
      "id": "make_heavy_armor",
      "name": "重型防护甲",
      "asset": {
        "ref": "item:heavy_armor"
      },
      "station": "workbench",
      "output": "heavy_armor",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_medic_armor",
      "category": "recipe",
      "id": "make_medic_armor",
      "name": "救护防护背心",
      "asset": {
        "ref": "item:medic_armor"
      },
      "station": "sewing",
      "output": "medic_armor",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_balanced_armor",
      "category": "recipe",
      "id": "make_balanced_armor",
      "name": "均衡战术甲",
      "asset": {
        "ref": "item:balanced_armor"
      },
      "station": "sewing",
      "output": "balanced_armor",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_steady_ring",
      "category": "recipe",
      "id": "make_steady_ring",
      "name": "稳手指环",
      "asset": {
        "ref": "item:steady_ring"
      },
      "station": "hand",
      "output": "steady_ring",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_swift_pendant",
      "category": "recipe",
      "id": "make_swift_pendant",
      "name": "轻捷吊坠",
      "asset": {
        "ref": "item:swift_pendant"
      },
      "station": "workbench",
      "output": "swift_pendant",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_guard_badge",
      "category": "recipe",
      "id": "make_guard_badge",
      "name": "守护徽记",
      "asset": {
        "ref": "item:guard_badge"
      },
      "station": "workbench",
      "output": "guard_badge",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_hunter_token",
      "category": "recipe",
      "id": "make_hunter_token",
      "name": "猎手护符",
      "asset": {
        "ref": "item:hunter_token"
      },
      "station": "hand",
      "output": "hunter_token",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_focus_lens",
      "category": "recipe",
      "id": "make_focus_lens",
      "name": "聚焦镜片",
      "asset": {
        "ref": "item:focus_lens"
      },
      "station": "workbench",
      "output": "focus_lens",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_aid_badge",
      "category": "recipe",
      "id": "make_aid_badge",
      "name": "救护徽章",
      "asset": {
        "ref": "item:aid_badge"
      },
      "station": "medical",
      "output": "aid_badge",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_resolve_medal",
      "category": "recipe",
      "id": "make_resolve_medal",
      "name": "镇定勋章",
      "asset": {
        "ref": "item:resolve_medal"
      },
      "station": "workbench",
      "output": "resolve_medal",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_tactics_watch",
      "category": "recipe",
      "id": "make_tactics_watch",
      "name": "战术计时表",
      "asset": {
        "ref": "item:tactics_watch"
      },
      "station": "workbench",
      "output": "tactics_watch",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_work_shirt",
      "category": "recipe",
      "id": "make_work_shirt",
      "name": "耐磨工作衣",
      "asset": {
        "ref": "item:work_shirt"
      },
      "station": "hand",
      "output": "work_shirt",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_forage_jacket",
      "category": "recipe",
      "id": "make_forage_jacket",
      "name": "采集外套",
      "asset": {
        "ref": "item:forage_jacket"
      },
      "station": "sewing",
      "output": "forage_jacket",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_scavenge_vest",
      "category": "recipe",
      "id": "make_scavenge_vest",
      "name": "搜集背心",
      "asset": {
        "ref": "item:scavenge_vest"
      },
      "station": "sewing",
      "output": "scavenge_vest",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_craft_coat",
      "category": "recipe",
      "id": "make_craft_coat",
      "name": "工匠围衣",
      "asset": {
        "ref": "item:craft_coat"
      },
      "station": "sewing",
      "output": "craft_coat",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_rest_coat",
      "category": "recipe",
      "id": "make_rest_coat",
      "name": "休养外衣",
      "asset": {
        "ref": "item:rest_coat"
      },
      "station": "sewing",
      "output": "rest_coat",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_expedition_coat",
      "category": "recipe",
      "id": "make_expedition_coat",
      "name": "远征上衣",
      "asset": {
        "ref": "item:expedition_coat"
      },
      "station": "sewing",
      "output": "expedition_coat",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_work_trousers",
      "category": "recipe",
      "id": "make_work_trousers",
      "name": "工作长裤",
      "asset": {
        "ref": "item:work_trousers"
      },
      "station": "hand",
      "output": "work_trousers",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_trail_trousers",
      "category": "recipe",
      "id": "make_trail_trousers",
      "name": "行路裤",
      "asset": {
        "ref": "item:trail_trousers"
      },
      "station": "sewing",
      "output": "trail_trousers",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_utility_trousers",
      "category": "recipe",
      "id": "make_utility_trousers",
      "name": "作业长裤",
      "asset": {
        "ref": "item:utility_trousers"
      },
      "station": "sewing",
      "output": "utility_trousers",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_reinforced_trousers",
      "category": "recipe",
      "id": "make_reinforced_trousers",
      "name": "加固长裤",
      "asset": {
        "ref": "item:reinforced_trousers"
      },
      "station": "sewing",
      "output": "reinforced_trousers",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_recovery_trousers",
      "category": "recipe",
      "id": "make_recovery_trousers",
      "name": "休养长裤",
      "asset": {
        "ref": "item:recovery_trousers"
      },
      "station": "sewing",
      "output": "recovery_trousers",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_expedition_trousers",
      "category": "recipe",
      "id": "make_expedition_trousers",
      "name": "远征下衣",
      "asset": {
        "ref": "item:expedition_trousers"
      },
      "station": "sewing",
      "output": "expedition_trousers",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_gather_gloves",
      "category": "recipe",
      "id": "make_gather_gloves",
      "name": "采集手套",
      "asset": {
        "ref": "item:gather_gloves"
      },
      "station": "hand",
      "output": "gather_gloves",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_route_compass",
      "category": "recipe",
      "id": "make_route_compass",
      "name": "路线罗盘",
      "asset": {
        "ref": "item:route_compass"
      },
      "station": "workbench",
      "output": "route_compass",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_filter_token",
      "category": "recipe",
      "id": "make_filter_token",
      "name": "净水操作套件",
      "asset": {
        "ref": "item:filter_token"
      },
      "station": "workbench",
      "output": "filter_token",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_repair_manual",
      "category": "recipe",
      "id": "make_repair_manual",
      "name": "维护手册",
      "asset": {
        "ref": "item:repair_manual"
      },
      "station": "workbench",
      "output": "repair_manual",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_medical_notes",
      "category": "recipe",
      "id": "make_medical_notes",
      "name": "护理笔记",
      "asset": {
        "ref": "item:medical_notes"
      },
      "station": "medical",
      "output": "medical_notes",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_ration_pouch",
      "category": "recipe",
      "id": "make_ration_pouch",
      "name": "配给袋",
      "asset": {
        "ref": "item:ration_pouch"
      },
      "station": "sewing",
      "output": "ration_pouch",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_plank",
      "category": "recipe",
      "id": "make_plank",
      "name": "木板",
      "asset": {
        "ref": "item:plank"
      },
      "station": "hand",
      "output": "plank",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_rope",
      "category": "recipe",
      "id": "make_rope",
      "name": "绳索",
      "asset": {
        "ref": "item:rope"
      },
      "station": "hand",
      "output": "rope",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_thread",
      "category": "recipe",
      "id": "make_thread",
      "name": "缝线",
      "asset": {
        "ref": "item:thread"
      },
      "station": "hand",
      "output": "thread",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_iron_plate",
      "category": "recipe",
      "id": "make_iron_plate",
      "name": "铁片",
      "asset": {
        "ref": "item:iron_plate"
      },
      "station": "workbench",
      "output": "iron_plate",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_fastener",
      "category": "recipe",
      "id": "make_fastener",
      "name": "紧固件",
      "asset": {
        "ref": "item:fastener"
      },
      "station": "workbench",
      "output": "fastener",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_steel",
      "category": "recipe",
      "id": "make_steel",
      "name": "钢材",
      "asset": {
        "ref": "item:steel"
      },
      "station": "forge",
      "output": "steel",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_leather",
      "category": "recipe",
      "id": "make_leather",
      "name": "皮革",
      "asset": {
        "ref": "item:leather"
      },
      "station": "sewing",
      "output": "leather",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_canvas",
      "category": "recipe",
      "id": "make_canvas",
      "name": "帆布",
      "asset": {
        "ref": "item:canvas"
      },
      "station": "sewing",
      "output": "canvas",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_metal_tube",
      "category": "recipe",
      "id": "make_metal_tube",
      "name": "金属管",
      "asset": {
        "ref": "item:metal_tube"
      },
      "station": "workbench",
      "output": "metal_tube",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_spring",
      "category": "recipe",
      "id": "make_spring",
      "name": "弹簧",
      "asset": {
        "ref": "item:spring"
      },
      "station": "forge",
      "output": "spring",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_precision_parts",
      "category": "recipe",
      "id": "make_precision_parts",
      "name": "精密零件",
      "asset": {
        "ref": "item:precision_parts"
      },
      "station": "workbench",
      "output": "precision_parts",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_weapon_parts",
      "category": "recipe",
      "id": "make_weapon_parts",
      "name": "武器组件",
      "asset": {
        "ref": "item:weapon_parts"
      },
      "station": "armory",
      "output": "weapon_parts",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_repair_kit",
      "category": "recipe",
      "id": "make_repair_kit",
      "name": "维修包",
      "asset": {
        "ref": "item:repair_kit"
      },
      "station": "workbench",
      "output": "repair_kit",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_gun_service_kit",
      "category": "recipe",
      "id": "make_gun_service_kit",
      "name": "枪械养护包",
      "asset": {
        "ref": "item:gun_service_kit"
      },
      "station": "armory",
      "output": "gun_service_kit",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_disinfectant",
      "category": "recipe",
      "id": "make_disinfectant",
      "name": "消毒液",
      "asset": {
        "ref": "item:disinfectant"
      },
      "station": "medical",
      "output": "disinfectant",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_saline",
      "category": "recipe",
      "id": "make_saline",
      "name": "盐水剂",
      "asset": {
        "ref": "item:saline"
      },
      "station": "medical",
      "output": "saline",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_soap",
      "category": "recipe",
      "id": "make_soap",
      "name": "肥皂",
      "asset": {
        "ref": "item:soap"
      },
      "station": "workbench",
      "output": "soap",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_filter_media",
      "category": "recipe",
      "id": "make_filter_media",
      "name": "滤材",
      "asset": {
        "ref": "item:filter_media"
      },
      "station": "hand",
      "output": "filter_media",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_herb_extract",
      "category": "recipe",
      "id": "make_herb_extract",
      "name": "草药浸膏",
      "asset": {
        "ref": "item:herb_extract"
      },
      "station": "medical",
      "output": "herb_extract",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_rubber",
      "category": "recipe",
      "id": "make_rubber",
      "name": "橡胶",
      "asset": {
        "ref": "item:rubber"
      },
      "station": "workbench",
      "output": "rubber",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_plastic",
      "category": "recipe",
      "id": "make_plastic",
      "name": "塑料",
      "asset": {
        "ref": "item:plastic"
      },
      "station": "workbench",
      "output": "plastic",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_wire",
      "category": "recipe",
      "id": "make_wire",
      "name": "导线",
      "asset": {
        "ref": "item:wire"
      },
      "station": "workbench",
      "output": "wire",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_battery",
      "category": "recipe",
      "id": "make_battery",
      "name": "电池",
      "asset": {
        "ref": "item:battery"
      },
      "station": "workbench",
      "output": "battery",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_grain",
      "category": "recipe",
      "id": "make_grain",
      "name": "谷物",
      "asset": {
        "ref": "item:grain"
      },
      "station": "workbench",
      "output": "grain",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_salt",
      "category": "recipe",
      "id": "make_salt",
      "name": "盐",
      "asset": {
        "ref": "item:salt"
      },
      "station": "workbench",
      "output": "salt",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:make_cooking_oil",
      "category": "recipe",
      "id": "make_cooking_oil",
      "name": "食用油",
      "asset": {
        "ref": "item:cooking_oil"
      },
      "station": "workbench",
      "output": "cooking_oil",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_ceramic_shell",
      "category": "recipe",
      "id": "ex_ceramic_shell",
      "name": "陶壳",
      "asset": {
        "ref": "item:ceramic_shell"
      },
      "station": "kiln",
      "output": "ceramic_shell",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_purification_tablet",
      "category": "recipe",
      "id": "ex_purification_tablet",
      "name": "净水片",
      "asset": {
        "ref": "item:purification_tablet"
      },
      "station": "medical",
      "output": "purification_tablet",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_tablet_water",
      "category": "recipe",
      "id": "ex_tablet_water",
      "name": "净水片处理脏水",
      "asset": {
        "ref": "item:clean_water"
      },
      "station": "hand",
      "output": "clean_water",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_filter_cartridge",
      "category": "recipe",
      "id": "ex_filter_cartridge",
      "name": "长效滤芯",
      "asset": {
        "ref": "item:filter_cartridge"
      },
      "station": "workbench",
      "output": "filter_cartridge",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_cartridge_water",
      "category": "recipe",
      "id": "ex_cartridge_water",
      "name": "滤芯处理脏水",
      "asset": {
        "ref": "item:clean_water"
      },
      "station": "hand",
      "output": "clean_water",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_ration_canteen",
      "category": "recipe",
      "id": "ex_ration_canteen",
      "name": "分量水壶",
      "asset": {
        "ref": "item:ration_canteen"
      },
      "station": "workbench",
      "output": "ration_canteen",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_hardtack",
      "category": "recipe",
      "id": "ex_hardtack",
      "name": "远行硬饼",
      "asset": {
        "ref": "item:hardtack"
      },
      "station": "fire_pot",
      "output": "hardtack",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_expedition_bar",
      "category": "recipe",
      "id": "ex_expedition_bar",
      "name": "远征口粮棒",
      "asset": {
        "ref": "item:expedition_bar"
      },
      "station": "hand",
      "output": "expedition_bar",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_stew_portion",
      "category": "recipe",
      "id": "ex_stew_portion",
      "name": "谷肉炖食",
      "asset": {
        "ref": "item:stew_portion"
      },
      "station": "fire_pot",
      "output": "stew_portion",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_field_tarp",
      "category": "recipe",
      "id": "ex_field_tarp",
      "name": "野营遮雨布",
      "asset": {
        "ref": "item:field_tarp"
      },
      "station": "sewing",
      "output": "field_tarp",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_folding_stove",
      "category": "recipe",
      "id": "ex_folding_stove",
      "name": "折叠柴炉",
      "asset": {
        "ref": "item:folding_stove"
      },
      "station": "workbench",
      "output": "folding_stove",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_field_patch",
      "category": "recipe",
      "id": "ex_field_patch",
      "name": "野外修补包（白）",
      "asset": {
        "ref": "item:field_patch"
      },
      "station": "workbench",
      "output": "field_patch",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_field_patch_green",
      "category": "recipe",
      "id": "ex_field_patch_green",
      "name": "野外修补包（绿）",
      "asset": {
        "ref": "item:field_patch_green"
      },
      "station": "workbench",
      "output": "field_patch_green",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_field_patch_blue",
      "category": "recipe",
      "id": "ex_field_patch_blue",
      "name": "野外修补包（蓝）",
      "asset": {
        "ref": "item:field_patch_blue"
      },
      "station": "workbench",
      "output": "field_patch_blue",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_cable_bundle",
      "category": "recipe",
      "id": "ex_cable_bundle",
      "name": "绝缘线束",
      "asset": {
        "ref": "item:cable_bundle"
      },
      "station": "workbench",
      "output": "cable_bundle",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_flash_marker",
      "category": "recipe",
      "id": "ex_flash_marker",
      "name": "闪光标记器",
      "asset": {
        "ref": "item:flash_marker"
      },
      "station": "workbench",
      "output": "flash_marker",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_brace_pack",
      "category": "recipe",
      "id": "ex_brace_pack",
      "name": "支撑护带包",
      "asset": {
        "ref": "item:brace_pack"
      },
      "station": "sewing",
      "output": "brace_pack",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_rally_patch",
      "category": "recipe",
      "id": "ex_rally_patch",
      "name": "鼓舞提示贴",
      "asset": {
        "ref": "item:rally_patch"
      },
      "station": "hand",
      "output": "rally_patch",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_antidiarrheal_powder",
      "category": "recipe",
      "id": "ex_antidiarrheal_powder",
      "name": "止泻药粉",
      "asset": {
        "ref": "item:antidiarrheal_powder"
      },
      "station": "medical",
      "output": "antidiarrheal_powder",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_emergency_tonic",
      "category": "recipe",
      "id": "ex_emergency_tonic",
      "name": "应急恢复剂",
      "asset": {
        "ref": "item:emergency_tonic"
      },
      "station": "medical",
      "output": "emergency_tonic",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:ex_station_water",
      "category": "recipe",
      "id": "ex_station_water",
      "name": "净水台处理脏水",
      "asset": {
        "ref": "item:clean_water"
      },
      "station": "water_station",
      "output": "clean_water",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:field_patch_repair",
      "category": "recipe",
      "id": "field_patch_repair",
      "name": "现场修补",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:safehouse_base",
      "category": "recipe",
      "id": "safehouse_base",
      "name": "建安全屋",
      "asset": {
        "ref": "facility:safehouse"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:safehouse_bed",
      "category": "recipe",
      "id": "safehouse_bed",
      "name": "铺屋内床垫",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:safehouse_fire",
      "category": "recipe",
      "id": "safehouse_fire",
      "name": "建屋内火堆",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:wild_fixed_fire",
      "category": "recipe",
      "id": "wild_fixed_fire",
      "name": "建野外火堆",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_kiln",
      "category": "recipe",
      "id": "build_kiln",
      "name": "建陶窑",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_water_station",
      "category": "recipe",
      "id": "build_water_station",
      "name": "建固定净水台",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:build_drying_rack",
      "category": "recipe",
      "id": "build_drying_rack",
      "name": "建晾制架",
      "asset": {
        "ref": "glyph:recipe"
      },
      "station": "hand",
      "output": null,
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:dry_berries",
      "category": "recipe",
      "id": "dry_berries",
      "name": "晾制果干",
      "asset": {
        "ref": "item:dried_berries"
      },
      "station": "drying_rack",
      "output": "dried_berries",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "recipe:dry_meat",
      "category": "recipe",
      "id": "dry_meat",
      "name": "晾制肉干",
      "asset": {
        "ref": "item:dried_meat"
      },
      "station": "drying_rack",
      "output": "dried_meat",
      "source": "data.recipes",
      "entry": "CraftOverlay · Recipe 只读预览"
    },
    {
      "key": "knowledge:blueprint_knuckle_wraps",
      "category": "knowledge",
      "id": "blueprint_knuckle_wraps",
      "name": "护拳绑带的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_armory_station",
      "category": "knowledge",
      "id": "blueprint_armory_station",
      "name": "军械台蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_stone_knife",
      "category": "knowledge",
      "id": "blueprint_stone_knife",
      "name": "石刀的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_machete",
      "category": "knowledge",
      "id": "blueprint_machete",
      "name": "砍刀的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_guard_sword",
      "category": "knowledge",
      "id": "blueprint_guard_sword",
      "name": "守备短剑蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_hatchet",
      "category": "knowledge",
      "id": "blueprint_hatchet",
      "name": "简易斧的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_breach_axe",
      "category": "knowledge",
      "id": "blueprint_breach_axe",
      "name": "破障斧蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_rescue_axe",
      "category": "knowledge",
      "id": "blueprint_rescue_axe",
      "name": "救援短斧蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_wooden_spear",
      "category": "knowledge",
      "id": "blueprint_wooden_spear",
      "name": "木矛的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_metal_spear",
      "category": "knowledge",
      "id": "blueprint_metal_spear",
      "name": "金属矛的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_pike",
      "category": "knowledge",
      "id": "blueprint_pike",
      "name": "加固长枪蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_barbed_spear",
      "category": "knowledge",
      "id": "blueprint_barbed_spear",
      "name": "倒刺短矛蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_wooden_club",
      "category": "knowledge",
      "id": "blueprint_wooden_club",
      "name": "木棍的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_steel_mace",
      "category": "knowledge",
      "id": "blueprint_steel_mace",
      "name": "钢制钉锤蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_shock_baton",
      "category": "knowledge",
      "id": "blueprint_shock_baton",
      "name": "绝缘警棍蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_wooden_bow",
      "category": "knowledge",
      "id": "blueprint_wooden_bow",
      "name": "木弓的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_recurve_bow",
      "category": "knowledge",
      "id": "blueprint_recurve_bow",
      "name": "复合木弓的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_hunter_bow",
      "category": "knowledge",
      "id": "blueprint_hunter_bow",
      "name": "猎手弓蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_crossbow",
      "category": "knowledge",
      "id": "blueprint_crossbow",
      "name": "弩的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_repeater_crossbow",
      "category": "knowledge",
      "id": "blueprint_repeater_crossbow",
      "name": "轻型战术弩蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_wooden_shield",
      "category": "knowledge",
      "id": "blueprint_wooden_shield",
      "name": "木盾的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_scrap_shield",
      "category": "knowledge",
      "id": "blueprint_scrap_shield",
      "name": "包铁盾的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_rescue_shield",
      "category": "knowledge",
      "id": "blueprint_rescue_shield",
      "name": "救援盾蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_signal_whistle",
      "category": "knowledge",
      "id": "blueprint_signal_whistle",
      "name": "信号哨的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_signal_flag",
      "category": "knowledge",
      "id": "blueprint_signal_flag",
      "name": "战术旗的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_field_radio",
      "category": "knowledge",
      "id": "blueprint_field_radio",
      "name": "战术电台蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_command_terminal",
      "category": "knowledge",
      "id": "blueprint_command_terminal",
      "name": "便携指挥终端蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_field_medkit",
      "category": "knowledge",
      "id": "blueprint_field_medkit",
      "name": "随行医疗包的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_trauma_bag",
      "category": "knowledge",
      "id": "blueprint_trauma_bag",
      "name": "创伤救护包的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_rescue_medkit",
      "category": "knowledge",
      "id": "blueprint_rescue_medkit",
      "name": "救援医疗箱蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_triage_kit",
      "category": "knowledge",
      "id": "blueprint_triage_kit",
      "name": "分诊器材组蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_padded_vest",
      "category": "knowledge",
      "id": "blueprint_padded_vest",
      "name": "衬垫护具的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_leather_armor",
      "category": "knowledge",
      "id": "blueprint_leather_armor",
      "name": "皮革护具的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_scrap_armor",
      "category": "knowledge",
      "id": "blueprint_scrap_armor",
      "name": "拼接铁甲的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_scout_armor",
      "category": "knowledge",
      "id": "blueprint_scout_armor",
      "name": "侦察护具蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_heavy_armor",
      "category": "knowledge",
      "id": "blueprint_heavy_armor",
      "name": "重型防护甲蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_medic_armor",
      "category": "knowledge",
      "id": "blueprint_medic_armor",
      "name": "救护防护背心蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_balanced_armor",
      "category": "knowledge",
      "id": "blueprint_balanced_armor",
      "name": "均衡战术甲蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_steady_ring",
      "category": "knowledge",
      "id": "blueprint_steady_ring",
      "name": "稳手指环的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_swift_pendant",
      "category": "knowledge",
      "id": "blueprint_swift_pendant",
      "name": "轻捷吊坠的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_guard_badge",
      "category": "knowledge",
      "id": "blueprint_guard_badge",
      "name": "守护徽记的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_hunter_token",
      "category": "knowledge",
      "id": "blueprint_hunter_token",
      "name": "猎手护符的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_focus_lens",
      "category": "knowledge",
      "id": "blueprint_focus_lens",
      "name": "聚焦镜片蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_aid_badge",
      "category": "knowledge",
      "id": "blueprint_aid_badge",
      "name": "救护徽章蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_resolve_medal",
      "category": "knowledge",
      "id": "blueprint_resolve_medal",
      "name": "镇定勋章蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_tactics_watch",
      "category": "knowledge",
      "id": "blueprint_tactics_watch",
      "name": "战术计时表蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_work_shirt",
      "category": "knowledge",
      "id": "blueprint_work_shirt",
      "name": "耐磨工作衣的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_forage_jacket",
      "category": "knowledge",
      "id": "blueprint_forage_jacket",
      "name": "采集外套的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_scavenge_vest",
      "category": "knowledge",
      "id": "blueprint_scavenge_vest",
      "name": "搜集背心的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_craft_coat",
      "category": "knowledge",
      "id": "blueprint_craft_coat",
      "name": "工匠围衣蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_rest_coat",
      "category": "knowledge",
      "id": "blueprint_rest_coat",
      "name": "休养外衣蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_expedition_coat",
      "category": "knowledge",
      "id": "blueprint_expedition_coat",
      "name": "远征上衣蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_work_trousers",
      "category": "knowledge",
      "id": "blueprint_work_trousers",
      "name": "工作长裤的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_trail_trousers",
      "category": "knowledge",
      "id": "blueprint_trail_trousers",
      "name": "行路裤的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_utility_trousers",
      "category": "knowledge",
      "id": "blueprint_utility_trousers",
      "name": "作业长裤的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_reinforced_trousers",
      "category": "knowledge",
      "id": "blueprint_reinforced_trousers",
      "name": "加固长裤蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_recovery_trousers",
      "category": "knowledge",
      "id": "blueprint_recovery_trousers",
      "name": "休养长裤蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_expedition_trousers",
      "category": "knowledge",
      "id": "blueprint_expedition_trousers",
      "name": "远征下衣蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_gather_gloves",
      "category": "knowledge",
      "id": "blueprint_gather_gloves",
      "name": "采集手套的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_route_compass",
      "category": "knowledge",
      "id": "blueprint_route_compass",
      "name": "路线罗盘的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_filter_token",
      "category": "knowledge",
      "id": "blueprint_filter_token",
      "name": "净水操作套件的做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_repair_manual",
      "category": "knowledge",
      "id": "blueprint_repair_manual",
      "name": "维护手册蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_medical_notes",
      "category": "knowledge",
      "id": "blueprint_medical_notes",
      "name": "护理笔记蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:blueprint_ration_pouch",
      "category": "knowledge",
      "id": "blueprint_ration_pouch",
      "name": "配给袋蓝图",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:expedition_basics",
      "category": "knowledge",
      "id": "expedition_basics",
      "name": "远行基础做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "knowledge:ceramic_methods",
      "category": "knowledge",
      "id": "ceramic_methods",
      "name": "基础陶工做法",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.knowledge",
      "entry": "CraftOverlay · CityServiceOverlay"
    },
    {
      "key": "document:prologue:relief_page_village",
      "category": "document",
      "id": "prologue:relief_page_village",
      "name": "公开救助残页",
      "asset": {
        "ref": "glyph:task"
      },
      "source": "data.prologue.documents",
      "entry": "PrologueCards · CityServiceOverlay"
    },
    {
      "key": "document:prologue:village_south_sign",
      "category": "document",
      "id": "prologue:village_south_sign",
      "name": "南口路标",
      "asset": {
        "ref": "glyph:route"
      },
      "source": "data.prologue.documents",
      "entry": "PrologueCards · CityServiceOverlay"
    },
    {
      "key": "document:city:h03_craft_notice",
      "category": "document",
      "id": "city:h03_craft_notice",
      "name": "制作告示",
      "asset": {
        "ref": "glyph:task"
      },
      "source": "data.city.notices",
      "entry": "PrologueCards · CityServiceOverlay"
    },
    {
      "key": "document:city:h03_ceramic_notice",
      "category": "document",
      "id": "city:h03_ceramic_notice",
      "name": "基础陶工说明",
      "asset": {
        "ref": "glyph:task"
      },
      "source": "data.city.notices",
      "entry": "PrologueCards · CityServiceOverlay"
    },
    {
      "key": "document:city:r07_observation_notice",
      "category": "document",
      "id": "city:r07_observation_notice",
      "name": "观察区公开通道告示",
      "asset": {
        "ref": "glyph:task"
      },
      "source": "data.city.notices",
      "entry": "PrologueCards · CityServiceOverlay"
    },
    {
      "key": "quest:FA01",
      "category": "quest",
      "id": "FA01",
      "name": "营区之外",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "main",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:FA02",
      "category": "quest",
      "id": "FA02",
      "name": "被称作新生的人",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "main",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:FA03",
      "category": "quest",
      "id": "FA03",
      "name": "样本之外",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "main",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:FA04",
      "category": "quest",
      "id": "FA04",
      "name": "同一条归路",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "main",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:FA05",
      "category": "quest",
      "id": "FA05",
      "name": "联络仍在继续",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "main",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:HU01",
      "category": "quest",
      "id": "HU01",
      "name": "鉴定之前",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:HU02",
      "category": "quest",
      "id": "HU02",
      "name": "样本去向",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:HU03",
      "category": "quest",
      "id": "HU03",
      "name": "同行不是归属",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:HU04",
      "category": "quest",
      "id": "HU04",
      "name": "留守之前",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:CH01",
      "category": "quest",
      "id": "CH01",
      "name": "施济清单",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:CH02",
      "category": "quest",
      "id": "CH02",
      "name": "被扣下的记录",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:AC01",
      "category": "quest",
      "id": "AC01",
      "name": "观察的边界",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:AC02",
      "category": "quest",
      "id": "AC02",
      "name": "相互矛盾的报告",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "side",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:RN01",
      "category": "quest",
      "id": "RN01",
      "name": "相互照应",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "recruit",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:RN02",
      "category": "quest",
      "id": "RN02",
      "name": "检修交接",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "recruit",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:RN03",
      "category": "quest",
      "id": "RN03",
      "name": "转运手册",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "recruit",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:RN04",
      "category": "quest",
      "id": "RN04",
      "name": "林间归路",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "recruit",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:RN05",
      "category": "quest",
      "id": "RN05",
      "name": "封存清单",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "recruit",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:RN06",
      "category": "quest",
      "id": "RN06",
      "name": "值守记录",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "recruit",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:EXQ05",
      "category": "quest",
      "id": "EXQ05",
      "name": "水从哪里来",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "optional",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:EXQ06",
      "category": "quest",
      "id": "EXQ06",
      "name": "矿道里的旧工具",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "optional",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:EXQ07",
      "category": "quest",
      "id": "EXQ07",
      "name": "最后一张疏散图",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "optional",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:EXQ08",
      "category": "quest",
      "id": "EXQ08",
      "name": "封存不是永存",
      "asset": {
        "ref": "frame:task"
      },
      "kind": "optional",
      "source": "data.quests.list",
      "entry": "GoalsOverlay · TaskCorner · CityServiceOverlay"
    },
    {
      "key": "quest:opening:PRO01",
      "category": "quest",
      "id": "opening:PRO01",
      "name": "看清眼前",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO02",
      "category": "quest",
      "id": "opening:PRO02",
      "name": "留住力气",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO03",
      "category": "quest",
      "id": "opening:PRO03",
      "name": "带点防身物",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO04",
      "category": "quest",
      "id": "opening:PRO04",
      "name": "第一次交手",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO05",
      "category": "quest",
      "id": "opening:PRO05",
      "name": "身体里的变化",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO06",
      "category": "quest",
      "id": "opening:PRO06",
      "name": "找到水源",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO07",
      "category": "quest",
      "id": "opening:PRO07",
      "name": "火要有燃料",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO08",
      "category": "quest",
      "id": "opening:PRO08",
      "name": "一份安全的水",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO09",
      "category": "quest",
      "id": "opening:PRO09",
      "name": "能再做的食物",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO10",
      "category": "quest",
      "id": "opening:PRO10",
      "name": "工具换来效率",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO11",
      "category": "quest",
      "id": "opening:PRO11",
      "name": "五格不是五份",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO12",
      "category": "quest",
      "id": "opening:PRO12",
      "name": "不要硬撑",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO13",
      "category": "quest",
      "id": "opening:PRO13",
      "name": "留个能回的地方",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO14",
      "category": "quest",
      "id": "opening:PRO14",
      "name": "走一趟再回来",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO15",
      "category": "quest",
      "id": "opening:PRO15",
      "name": "找一条求医线索",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO16",
      "category": "quest",
      "id": "opening:PRO16",
      "name": "来客与路",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO17",
      "category": "quest",
      "id": "opening:PRO17",
      "name": "为远行打包",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "quest:opening:PRO18",
      "category": "quest",
      "id": "opening:PRO18",
      "name": "走向有人烟的地方",
      "asset": {
        "ref": "frame:task"
      },
      "source": "data.prologue.tasks",
      "entry": "PrologueCards · TaskCorner · GoalsOverlay"
    },
    {
      "key": "knowledgePack:kp_waterworks",
      "category": "knowledgePack",
      "id": "kp_waterworks",
      "name": "净水/维护知识包",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.expansionQuests.packs",
      "entry": "CityServiceOverlay · CraftOverlay · GoalsOverlay"
    },
    {
      "key": "knowledgePack:kp_mining",
      "category": "knowledgePack",
      "id": "kp_mining",
      "name": "野外工作衣物知识包",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.expansionQuests.packs",
      "entry": "CityServiceOverlay · CraftOverlay · GoalsOverlay"
    },
    {
      "key": "knowledgePack:kp_civilian_cache",
      "category": "knowledgePack",
      "id": "kp_civilian_cache",
      "name": "衣物/医疗工具知识与疏散日志",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.expansionQuests.packs",
      "entry": "CityServiceOverlay · CraftOverlay · GoalsOverlay"
    },
    {
      "key": "knowledgePack:kp_coldchain",
      "category": "knowledgePack",
      "id": "kp_coldchain",
      "name": "配给知识与封存记录",
      "asset": {
        "ref": "glyph:knowledge"
      },
      "source": "data.expansionQuests.packs",
      "entry": "CityServiceOverlay · CraftOverlay · GoalsOverlay"
    },
    {
      "key": "service:hub_registration_existing",
      "category": "service",
      "id": "hub_registration_existing",
      "name": "营区入营登记",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "h01_gate",
      "kind": "registration",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:hub_affairs_record",
      "category": "service",
      "id": "hub_affairs_record",
      "name": "整理调查记录",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "h02_square",
      "kind": "quest_record",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:hub_workshop_access",
      "category": "service",
      "id": "hub_workshop_access",
      "name": "使用营区工坊",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "h03_workshop",
      "kind": "facility_menu",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:copy_public_knowledge",
      "category": "service",
      "id": "copy_public_knowledge",
      "name": "查抄公开资料",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "h03_workshop",
      "kind": "knowledge_copy",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:clinic_consult",
      "category": "service",
      "id": "clinic_consult",
      "name": "伤病咨询",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "h04_infirmary",
      "kind": "information",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:clinic_dressing",
      "category": "service",
      "id": "clinic_dressing",
      "name": "换药包扎",
      "asset": {
        "ref": "glyph:health"
      },
      "source": "data.city.services",
      "location": "h04_infirmary",
      "kind": "medical_item",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:clinic_observation_rest",
      "category": "service",
      "id": "clinic_observation_rest",
      "name": "留观休息",
      "asset": {
        "ref": "glyph:rest"
      },
      "source": "data.city.services",
      "location": "h04_infirmary",
      "kind": "sheltered_rest",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:appraisal_white_v1",
      "category": "service",
      "id": "appraisal_white_v1",
      "name": "活性样本鉴定",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "h05_appraisal",
      "kind": "delegated_appraisal",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:shelter_recruitment",
      "category": "service",
      "id": "shelter_recruitment",
      "name": "同行者名册",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "h06_shelter",
      "kind": "delegated_recruitment",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:church_reception_existing",
      "category": "service",
      "id": "church_reception_existing",
      "name": "询问教会公开路线",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "c02_church",
      "kind": "information",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:church_relief_ration",
      "category": "service",
      "id": "church_relief_ration",
      "name": "领取救助配给",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "c04_alms",
      "kind": "limited_relief",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:church_public_mediation",
      "category": "service",
      "id": "church_public_mediation",
      "name": "交接公开资料",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "c05_admonition",
      "kind": "quest_record",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:church_archive_copy",
      "category": "service",
      "id": "church_archive_copy",
      "name": "抄录路线备份",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "c06_archive",
      "kind": "quest_record",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:church_quiet_rest",
      "category": "service",
      "id": "church_quiet_rest",
      "name": "静处休息",
      "asset": {
        "ref": "glyph:rest"
      },
      "source": "data.city.services",
      "location": "c07_sanctuary",
      "kind": "sheltered_rest",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:academy_reception_existing",
      "category": "service",
      "id": "academy_reception_existing",
      "name": "询问环境调查路线",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "r04_reception",
      "kind": "information",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:academy_noninvasive_observation",
      "category": "service",
      "id": "academy_noninvasive_observation",
      "name": "观察范围核对",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "r05_testing",
      "kind": "quest_record",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:academy_report_compare",
      "category": "service",
      "id": "academy_report_compare",
      "name": "比对观察报告",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "r06_archive",
      "kind": "quest_record",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:academy_workshop_access",
      "category": "service",
      "id": "academy_workshop_access",
      "name": "使用试制工坊",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "r07_workshop",
      "kind": "facility_menu",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "service:academy_observation_record",
      "category": "service",
      "id": "academy_observation_record",
      "name": "查阅观察记录",
      "asset": {
        "ref": "glyph:service"
      },
      "source": "data.city.services",
      "location": "r08_observation",
      "kind": "information",
      "entry": "CityServiceOverlay · AppraisalPanel · RecruitPanel"
    },
    {
      "key": "affix:accurate",
      "category": "affix",
      "id": "accurate",
      "name": "校准",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "offense",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:keen",
      "category": "affix",
      "id": "keen",
      "name": "锐利",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "offense",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:piercing",
      "category": "affix",
      "id": "piercing",
      "name": "破甲",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "offense",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:balanced",
      "category": "affix",
      "id": "balanced",
      "name": "平衡",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "utility",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:swift",
      "category": "affix",
      "id": "swift",
      "name": "轻量",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "speed",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:heavy",
      "category": "affix",
      "id": "heavy",
      "name": "厚重",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "defense",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:guarded",
      "category": "affix",
      "id": "guarded",
      "name": "坚守",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "defense",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:anchored",
      "category": "affix",
      "id": "anchored",
      "name": "稳固",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "defense",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:tactical",
      "category": "affix",
      "id": "tactical",
      "name": "协作",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "support",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:restorative",
      "category": "affix",
      "id": "restorative",
      "name": "护理",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "support",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:staggering",
      "category": "affix",
      "id": "staggering",
      "name": "震荡",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "control",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:pushing",
      "category": "affix",
      "id": "pushing",
      "name": "推拒",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "control",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:durable",
      "category": "affix",
      "id": "durable",
      "name": "耐用",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "utility",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:quiet",
      "category": "affix",
      "id": "quiet",
      "name": "隐声",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "utility",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:craftsman",
      "category": "affix",
      "id": "craftsman",
      "name": "熟作",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "survival",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:economical",
      "category": "affix",
      "id": "economical",
      "name": "节用",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "survival",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:patient",
      "category": "affix",
      "id": "patient",
      "name": "舒缓",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "survival",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "affix:surefoot",
      "category": "affix",
      "id": "surefoot",
      "name": "稳步",
      "asset": {
        "ref": "glyph:growth"
      },
      "source": "data.affixes",
      "kind": "survival",
      "entry": "AttrDetail · GearOverlay · ItemDetail"
    },
    {
      "key": "dialogueLine:awakening_1",
      "category": "dialogueLine",
      "id": "awakening_1",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "opening",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:awakening_2",
      "category": "dialogueLine",
      "id": "awakening_2",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "opening",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:food_water",
      "category": "dialogueLine",
      "id": "food_water",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "opening",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:tutorial_arrive",
      "category": "dialogueLine",
      "id": "tutorial_arrive",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "tutorial_arrive",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:tutorial_won",
      "category": "dialogueLine",
      "id": "tutorial_won",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "tutorial_won",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:fusion_done_1",
      "category": "dialogueLine",
      "id": "fusion_done_1",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "fusion_done",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:fusion_done_2",
      "category": "dialogueLine",
      "id": "fusion_done_2",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "fusion_done",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0001",
      "category": "dialogueLine",
      "id": "NTL_0001",
      "name": "旁白",
      "asset": null,
      "eventId": "visitor_arrival_camp",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0002",
      "category": "dialogueLine",
      "id": "NTL_0002",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_arrival_camp",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0003",
      "category": "dialogueLine",
      "id": "NTL_0003",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_arrival_camp",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0004",
      "category": "dialogueLine",
      "id": "NTL_0004",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_arrival_camp",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0028",
      "category": "dialogueLine",
      "id": "NTL_0028",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_late_or_legacy",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0029",
      "category": "dialogueLine",
      "id": "NTL_0029",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_late_or_legacy",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0030",
      "category": "dialogueLine",
      "id": "NTL_0030",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_late_or_legacy",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0006",
      "category": "dialogueLine",
      "id": "NTL_0006",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0007",
      "category": "dialogueLine",
      "id": "NTL_0007",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0008",
      "category": "dialogueLine",
      "id": "NTL_0008",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0009",
      "category": "dialogueLine",
      "id": "NTL_0009",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0010",
      "category": "dialogueLine",
      "id": "NTL_0010",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0011",
      "category": "dialogueLine",
      "id": "NTL_0011",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0012",
      "category": "dialogueLine",
      "id": "NTL_0012",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0013",
      "category": "dialogueLine",
      "id": "NTL_0013",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0014",
      "category": "dialogueLine",
      "id": "NTL_0014",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0015",
      "category": "dialogueLine",
      "id": "NTL_0015",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0016",
      "category": "dialogueLine",
      "id": "NTL_0016",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0017",
      "category": "dialogueLine",
      "id": "NTL_0017",
      "name": "旁白",
      "asset": null,
      "eventId": "visitor_route_first",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0018",
      "category": "dialogueLine",
      "id": "NTL_0018",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "visitor_after_route_baobao",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0019",
      "category": "dialogueLine",
      "id": "NTL_0019",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_after_route_baobao",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0020",
      "category": "dialogueLine",
      "id": "NTL_0020",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "visitor_after_route_baobao",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0021",
      "category": "dialogueLine",
      "id": "NTL_0021",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_after_route_baobao",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0022",
      "category": "dialogueLine",
      "id": "NTL_0022",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_after_route_without_baobao",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0023",
      "category": "dialogueLine",
      "id": "NTL_0023",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0024",
      "category": "dialogueLine",
      "id": "NTL_0024",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_route_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0025",
      "category": "dialogueLine",
      "id": "NTL_0025",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_route_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0026",
      "category": "dialogueLine",
      "id": "NTL_0026",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "visitor_not_ready",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0027",
      "category": "dialogueLine",
      "id": "NTL_0027",
      "name": "程望",
      "asset": {
        "ref": "actor:npc_cheng_wang"
      },
      "eventId": "visitor_not_ready",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0031",
      "category": "dialogueLine",
      "id": "NTL_0031",
      "name": "旁白",
      "asset": null,
      "eventId": "PRO15_relief_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0032",
      "category": "dialogueLine",
      "id": "NTL_0032",
      "name": "旁白",
      "asset": null,
      "eventId": "PRO15_relief_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0033",
      "category": "dialogueLine",
      "id": "NTL_0033",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO15_relief_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0034",
      "category": "dialogueLine",
      "id": "NTL_0034",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO15_relief_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0035",
      "category": "dialogueLine",
      "id": "NTL_0035",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO15_relief_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0036",
      "category": "dialogueLine",
      "id": "NTL_0036",
      "name": "旁白",
      "asset": null,
      "eventId": "village_south_sign",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0037",
      "category": "dialogueLine",
      "id": "NTL_0037",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "village_south_sign",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0038",
      "category": "dialogueLine",
      "id": "NTL_0038",
      "name": "旁白",
      "asset": null,
      "eventId": "village_south_sign_route_unknown",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0039",
      "category": "dialogueLine",
      "id": "NTL_0039",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "village_south_sign_route_unknown",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0040",
      "category": "dialogueLine",
      "id": "NTL_0040",
      "name": "旁白",
      "asset": null,
      "eventId": "camp_route_log",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0041",
      "category": "dialogueLine",
      "id": "NTL_0041",
      "name": "旁白",
      "asset": null,
      "eventId": "camp_route_log",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0042",
      "category": "dialogueLine",
      "id": "NTL_0042",
      "name": "旁白",
      "asset": null,
      "eventId": "camp_route_log",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0044",
      "category": "dialogueLine",
      "id": "NTL_0044",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO01_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0045",
      "category": "dialogueLine",
      "id": "NTL_0045",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO01_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0048",
      "category": "dialogueLine",
      "id": "NTL_0048",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO02_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0049",
      "category": "dialogueLine",
      "id": "NTL_0049",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO02_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0052",
      "category": "dialogueLine",
      "id": "NTL_0052",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO03_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0053",
      "category": "dialogueLine",
      "id": "NTL_0053",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO03_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0056",
      "category": "dialogueLine",
      "id": "NTL_0056",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO04_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0057",
      "category": "dialogueLine",
      "id": "NTL_0057",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO04_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0060",
      "category": "dialogueLine",
      "id": "NTL_0060",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO05_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0061",
      "category": "dialogueLine",
      "id": "NTL_0061",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO05_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0064",
      "category": "dialogueLine",
      "id": "NTL_0064",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO06_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0065",
      "category": "dialogueLine",
      "id": "NTL_0065",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO06_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0068",
      "category": "dialogueLine",
      "id": "NTL_0068",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO07_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0069",
      "category": "dialogueLine",
      "id": "NTL_0069",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO07_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0072",
      "category": "dialogueLine",
      "id": "NTL_0072",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO08_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0073",
      "category": "dialogueLine",
      "id": "NTL_0073",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO08_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0076",
      "category": "dialogueLine",
      "id": "NTL_0076",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO09_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0077",
      "category": "dialogueLine",
      "id": "NTL_0077",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO09_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0080",
      "category": "dialogueLine",
      "id": "NTL_0080",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO10_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0081",
      "category": "dialogueLine",
      "id": "NTL_0081",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO10_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0084",
      "category": "dialogueLine",
      "id": "NTL_0084",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO11_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0085",
      "category": "dialogueLine",
      "id": "NTL_0085",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO11_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0088",
      "category": "dialogueLine",
      "id": "NTL_0088",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO12_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0089",
      "category": "dialogueLine",
      "id": "NTL_0089",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO12_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0092",
      "category": "dialogueLine",
      "id": "NTL_0092",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO13_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0093",
      "category": "dialogueLine",
      "id": "NTL_0093",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO13_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0096",
      "category": "dialogueLine",
      "id": "NTL_0096",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO14_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0097",
      "category": "dialogueLine",
      "id": "NTL_0097",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO14_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0100",
      "category": "dialogueLine",
      "id": "NTL_0100",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO15_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0101",
      "category": "dialogueLine",
      "id": "NTL_0101",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO15_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0104",
      "category": "dialogueLine",
      "id": "NTL_0104",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO16_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0105",
      "category": "dialogueLine",
      "id": "NTL_0105",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO16_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0108",
      "category": "dialogueLine",
      "id": "NTL_0108",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO17_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0109",
      "category": "dialogueLine",
      "id": "NTL_0109",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO17_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0112",
      "category": "dialogueLine",
      "id": "NTL_0112",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "PRO18_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0113",
      "category": "dialogueLine",
      "id": "NTL_0113",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "PRO18_reaction",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0167",
      "category": "dialogueLine",
      "id": "NTL_0167",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0168",
      "category": "dialogueLine",
      "id": "NTL_0168",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0169",
      "category": "dialogueLine",
      "id": "NTL_0169",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0170",
      "category": "dialogueLine",
      "id": "NTL_0170",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0171",
      "category": "dialogueLine",
      "id": "NTL_0171",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0172",
      "category": "dialogueLine",
      "id": "NTL_0172",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0173",
      "category": "dialogueLine",
      "id": "NTL_0173",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0174",
      "category": "dialogueLine",
      "id": "NTL_0174",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0175",
      "category": "dialogueLine",
      "id": "NTL_0175",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_lu_heng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0334",
      "category": "dialogueLine",
      "id": "NTL_0334",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0176",
      "category": "dialogueLine",
      "id": "NTL_0176",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0177",
      "category": "dialogueLine",
      "id": "NTL_0177",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0178",
      "category": "dialogueLine",
      "id": "NTL_0178",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0179",
      "category": "dialogueLine",
      "id": "NTL_0179",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0180",
      "category": "dialogueLine",
      "id": "NTL_0180",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0181",
      "category": "dialogueLine",
      "id": "NTL_0181",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0182",
      "category": "dialogueLine",
      "id": "NTL_0182",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0183",
      "category": "dialogueLine",
      "id": "NTL_0183",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0184",
      "category": "dialogueLine",
      "id": "NTL_0184",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_xu_an_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0335",
      "category": "dialogueLine",
      "id": "NTL_0335",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0185",
      "category": "dialogueLine",
      "id": "NTL_0185",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0186",
      "category": "dialogueLine",
      "id": "NTL_0186",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0187",
      "category": "dialogueLine",
      "id": "NTL_0187",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0188",
      "category": "dialogueLine",
      "id": "NTL_0188",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0189",
      "category": "dialogueLine",
      "id": "NTL_0189",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0190",
      "category": "dialogueLine",
      "id": "NTL_0190",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0191",
      "category": "dialogueLine",
      "id": "NTL_0191",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0192",
      "category": "dialogueLine",
      "id": "NTL_0192",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0193",
      "category": "dialogueLine",
      "id": "NTL_0193",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_luo_zheng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0336",
      "category": "dialogueLine",
      "id": "NTL_0336",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0194",
      "category": "dialogueLine",
      "id": "NTL_0194",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0195",
      "category": "dialogueLine",
      "id": "NTL_0195",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0196",
      "category": "dialogueLine",
      "id": "NTL_0196",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0197",
      "category": "dialogueLine",
      "id": "NTL_0197",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0198",
      "category": "dialogueLine",
      "id": "NTL_0198",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0199",
      "category": "dialogueLine",
      "id": "NTL_0199",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0200",
      "category": "dialogueLine",
      "id": "NTL_0200",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0201",
      "category": "dialogueLine",
      "id": "NTL_0201",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0202",
      "category": "dialogueLine",
      "id": "NTL_0202",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_chen_zhi_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0337",
      "category": "dialogueLine",
      "id": "NTL_0337",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0203",
      "category": "dialogueLine",
      "id": "NTL_0203",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0204",
      "category": "dialogueLine",
      "id": "NTL_0204",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0205",
      "category": "dialogueLine",
      "id": "NTL_0205",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0206",
      "category": "dialogueLine",
      "id": "NTL_0206",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0207",
      "category": "dialogueLine",
      "id": "NTL_0207",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0208",
      "category": "dialogueLine",
      "id": "NTL_0208",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0209",
      "category": "dialogueLine",
      "id": "NTL_0209",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0210",
      "category": "dialogueLine",
      "id": "NTL_0210",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0211",
      "category": "dialogueLine",
      "id": "NTL_0211",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_tao_cen_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0338",
      "category": "dialogueLine",
      "id": "NTL_0338",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0212",
      "category": "dialogueLine",
      "id": "NTL_0212",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0213",
      "category": "dialogueLine",
      "id": "NTL_0213",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0214",
      "category": "dialogueLine",
      "id": "NTL_0214",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0215",
      "category": "dialogueLine",
      "id": "NTL_0215",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0216",
      "category": "dialogueLine",
      "id": "NTL_0216",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0217",
      "category": "dialogueLine",
      "id": "NTL_0217",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0218",
      "category": "dialogueLine",
      "id": "NTL_0218",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0219",
      "category": "dialogueLine",
      "id": "NTL_0219",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0220",
      "category": "dialogueLine",
      "id": "NTL_0220",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_du_sui_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0339",
      "category": "dialogueLine",
      "id": "NTL_0339",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0221",
      "category": "dialogueLine",
      "id": "NTL_0221",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0222",
      "category": "dialogueLine",
      "id": "NTL_0222",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0223",
      "category": "dialogueLine",
      "id": "NTL_0223",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0224",
      "category": "dialogueLine",
      "id": "NTL_0224",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0225",
      "category": "dialogueLine",
      "id": "NTL_0225",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0226",
      "category": "dialogueLine",
      "id": "NTL_0226",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0227",
      "category": "dialogueLine",
      "id": "NTL_0227",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0228",
      "category": "dialogueLine",
      "id": "NTL_0228",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0229",
      "category": "dialogueLine",
      "id": "NTL_0229",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_qi_wen_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0340",
      "category": "dialogueLine",
      "id": "NTL_0340",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0230",
      "category": "dialogueLine",
      "id": "NTL_0230",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0231",
      "category": "dialogueLine",
      "id": "NTL_0231",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0232",
      "category": "dialogueLine",
      "id": "NTL_0232",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0233",
      "category": "dialogueLine",
      "id": "NTL_0233",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0234",
      "category": "dialogueLine",
      "id": "NTL_0234",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0235",
      "category": "dialogueLine",
      "id": "NTL_0235",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0236",
      "category": "dialogueLine",
      "id": "NTL_0236",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0237",
      "category": "dialogueLine",
      "id": "NTL_0237",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0238",
      "category": "dialogueLine",
      "id": "NTL_0238",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_bai_ling_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0341",
      "category": "dialogueLine",
      "id": "NTL_0341",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0239",
      "category": "dialogueLine",
      "id": "NTL_0239",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0240",
      "category": "dialogueLine",
      "id": "NTL_0240",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0241",
      "category": "dialogueLine",
      "id": "NTL_0241",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0242",
      "category": "dialogueLine",
      "id": "NTL_0242",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0243",
      "category": "dialogueLine",
      "id": "NTL_0243",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0244",
      "category": "dialogueLine",
      "id": "NTL_0244",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0245",
      "category": "dialogueLine",
      "id": "NTL_0245",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0246",
      "category": "dialogueLine",
      "id": "NTL_0246",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0247",
      "category": "dialogueLine",
      "id": "NTL_0247",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_ji_mu_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0342",
      "category": "dialogueLine",
      "id": "NTL_0342",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0248",
      "category": "dialogueLine",
      "id": "NTL_0248",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0249",
      "category": "dialogueLine",
      "id": "NTL_0249",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0250",
      "category": "dialogueLine",
      "id": "NTL_0250",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0251",
      "category": "dialogueLine",
      "id": "NTL_0251",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0252",
      "category": "dialogueLine",
      "id": "NTL_0252",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0253",
      "category": "dialogueLine",
      "id": "NTL_0253",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0254",
      "category": "dialogueLine",
      "id": "NTL_0254",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0255",
      "category": "dialogueLine",
      "id": "NTL_0255",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0256",
      "category": "dialogueLine",
      "id": "NTL_0256",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_song_zhu_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0343",
      "category": "dialogueLine",
      "id": "NTL_0343",
      "name": "宋烛",
      "asset": {
        "ref": "actor:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0257",
      "category": "dialogueLine",
      "id": "NTL_0257",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0258",
      "category": "dialogueLine",
      "id": "NTL_0258",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0259",
      "category": "dialogueLine",
      "id": "NTL_0259",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0260",
      "category": "dialogueLine",
      "id": "NTL_0260",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0261",
      "category": "dialogueLine",
      "id": "NTL_0261",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0262",
      "category": "dialogueLine",
      "id": "NTL_0262",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0263",
      "category": "dialogueLine",
      "id": "NTL_0263",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0264",
      "category": "dialogueLine",
      "id": "NTL_0264",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0265",
      "category": "dialogueLine",
      "id": "NTL_0265",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_fu_heng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0344",
      "category": "dialogueLine",
      "id": "NTL_0344",
      "name": "傅珩",
      "asset": {
        "ref": "actor:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0266",
      "category": "dialogueLine",
      "id": "NTL_0266",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0267",
      "category": "dialogueLine",
      "id": "NTL_0267",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0268",
      "category": "dialogueLine",
      "id": "NTL_0268",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0269",
      "category": "dialogueLine",
      "id": "NTL_0269",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0270",
      "category": "dialogueLine",
      "id": "NTL_0270",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0271",
      "category": "dialogueLine",
      "id": "NTL_0271",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0272",
      "category": "dialogueLine",
      "id": "NTL_0272",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0273",
      "category": "dialogueLine",
      "id": "NTL_0273",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0274",
      "category": "dialogueLine",
      "id": "NTL_0274",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_wen_cheng_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0345",
      "category": "dialogueLine",
      "id": "NTL_0345",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0275",
      "category": "dialogueLine",
      "id": "NTL_0275",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0276",
      "category": "dialogueLine",
      "id": "NTL_0276",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0277",
      "category": "dialogueLine",
      "id": "NTL_0277",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0278",
      "category": "dialogueLine",
      "id": "NTL_0278",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0279",
      "category": "dialogueLine",
      "id": "NTL_0279",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0280",
      "category": "dialogueLine",
      "id": "NTL_0280",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0281",
      "category": "dialogueLine",
      "id": "NTL_0281",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0282",
      "category": "dialogueLine",
      "id": "NTL_0282",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0283",
      "category": "dialogueLine",
      "id": "NTL_0283",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_ji_wen_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0346",
      "category": "dialogueLine",
      "id": "NTL_0346",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0284",
      "category": "dialogueLine",
      "id": "NTL_0284",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0285",
      "category": "dialogueLine",
      "id": "NTL_0285",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0286",
      "category": "dialogueLine",
      "id": "NTL_0286",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0287",
      "category": "dialogueLine",
      "id": "NTL_0287",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0288",
      "category": "dialogueLine",
      "id": "NTL_0288",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0289",
      "category": "dialogueLine",
      "id": "NTL_0289",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0290",
      "category": "dialogueLine",
      "id": "NTL_0290",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0291",
      "category": "dialogueLine",
      "id": "NTL_0291",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0292",
      "category": "dialogueLine",
      "id": "NTL_0292",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_shao_ning_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0347",
      "category": "dialogueLine",
      "id": "NTL_0347",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0293",
      "category": "dialogueLine",
      "id": "NTL_0293",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0294",
      "category": "dialogueLine",
      "id": "NTL_0294",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0295",
      "category": "dialogueLine",
      "id": "NTL_0295",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0296",
      "category": "dialogueLine",
      "id": "NTL_0296",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0297",
      "category": "dialogueLine",
      "id": "NTL_0297",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0298",
      "category": "dialogueLine",
      "id": "NTL_0298",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0299",
      "category": "dialogueLine",
      "id": "NTL_0299",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0300",
      "category": "dialogueLine",
      "id": "NTL_0300",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0301",
      "category": "dialogueLine",
      "id": "NTL_0301",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_he_yuan_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0348",
      "category": "dialogueLine",
      "id": "NTL_0348",
      "name": "何远",
      "asset": {
        "ref": "actor:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0302",
      "category": "dialogueLine",
      "id": "NTL_0302",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_first_greeting",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0303",
      "category": "dialogueLine",
      "id": "NTL_0303",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_role",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0304",
      "category": "dialogueLine",
      "id": "NTL_0304",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0305",
      "category": "dialogueLine",
      "id": "NTL_0305",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0306",
      "category": "dialogueLine",
      "id": "NTL_0306",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0307",
      "category": "dialogueLine",
      "id": "NTL_0307",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_locked",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0308",
      "category": "dialogueLine",
      "id": "NTL_0308",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_service_end",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0309",
      "category": "dialogueLine",
      "id": "NTL_0309",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0310",
      "category": "dialogueLine",
      "id": "NTL_0310",
      "name": "旁白",
      "asset": null,
      "eventId": "npc_yin_ke_dialogue_institution_substitute",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0349",
      "category": "dialogueLine",
      "id": "NTL_0349",
      "name": "尹珂",
      "asset": {
        "ref": "actor:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_service_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0311",
      "category": "dialogueLine",
      "id": "NTL_0311",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "gate_h01_registration",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0312",
      "category": "dialogueLine",
      "id": "NTL_0312",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "gate_h01_registration",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0313",
      "category": "dialogueLine",
      "id": "NTL_0313",
      "name": "陆衡",
      "asset": {
        "ref": "actor:npc_lu_heng"
      },
      "eventId": "gate_h01_registration",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0318",
      "category": "dialogueLine",
      "id": "NTL_0318",
      "name": "旁白",
      "asset": null,
      "eventId": "academy_public_observation_notice",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0319",
      "category": "dialogueLine",
      "id": "NTL_0319",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "academy_public_observation_notice",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0320",
      "category": "dialogueLine",
      "id": "NTL_0320",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "FA01_public_brief",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0321",
      "category": "dialogueLine",
      "id": "NTL_0321",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA01_public_brief",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0322",
      "category": "dialogueLine",
      "id": "NTL_0322",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "FA01_public_brief",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0314",
      "category": "dialogueLine",
      "id": "NTL_0314",
      "name": "祁闻",
      "asset": {
        "ref": "actor:npc_qi_wen"
      },
      "eventId": "FA02_route_choice",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0315",
      "category": "dialogueLine",
      "id": "NTL_0315",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA02_route_choice",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0316",
      "category": "dialogueLine",
      "id": "NTL_0316",
      "name": "温澄",
      "asset": {
        "ref": "actor:npc_wen_cheng"
      },
      "eventId": "FA03_environment_choice",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0317",
      "category": "dialogueLine",
      "id": "NTL_0317",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA03_environment_choice",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0323",
      "category": "dialogueLine",
      "id": "NTL_0323",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "FA04_merge_accept",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0324",
      "category": "dialogueLine",
      "id": "NTL_0324",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA04_merge_accept",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0325",
      "category": "dialogueLine",
      "id": "NTL_0325",
      "name": "旁白",
      "asset": null,
      "eventId": "FA04_merge_accept",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0326",
      "category": "dialogueLine",
      "id": "NTL_0326",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "FA04_merge_complete",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0327",
      "category": "dialogueLine",
      "id": "NTL_0327",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA04_merge_complete",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0328",
      "category": "dialogueLine",
      "id": "NTL_0328",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "FA05_power_contact",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0329",
      "category": "dialogueLine",
      "id": "NTL_0329",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA05_power_contact",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0330",
      "category": "dialogueLine",
      "id": "NTL_0330",
      "name": "旁白",
      "asset": null,
      "eventId": "FA05_contact_complete",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0331",
      "category": "dialogueLine",
      "id": "NTL_0331",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA05_contact_complete",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0332",
      "category": "dialogueLine",
      "id": "NTL_0332",
      "name": "饱饱",
      "asset": {
        "ref": "actor:baobao"
      },
      "eventId": "FA05_contact_complete",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "inner-voice"
    },
    {
      "key": "dialogueLine:NTL_0333",
      "category": "dialogueLine",
      "id": "NTL_0333",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "FA05_contact_complete",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0115",
      "category": "dialogueLine",
      "id": "NTL_0115",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "HU01_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0116",
      "category": "dialogueLine",
      "id": "NTL_0116",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "HU01_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0117",
      "category": "dialogueLine",
      "id": "NTL_0117",
      "name": "旁白",
      "asset": null,
      "eventId": "HU01_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0118",
      "category": "dialogueLine",
      "id": "NTL_0118",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "HU01_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0119",
      "category": "dialogueLine",
      "id": "NTL_0119",
      "name": "旁白",
      "asset": null,
      "eventId": "HU01_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0120",
      "category": "dialogueLine",
      "id": "NTL_0120",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "HU01_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0121",
      "category": "dialogueLine",
      "id": "NTL_0121",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "HU02_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0122",
      "category": "dialogueLine",
      "id": "NTL_0122",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "HU02_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0123",
      "category": "dialogueLine",
      "id": "NTL_0123",
      "name": "旁白",
      "asset": null,
      "eventId": "HU02_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0124",
      "category": "dialogueLine",
      "id": "NTL_0124",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "HU02_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0125",
      "category": "dialogueLine",
      "id": "NTL_0125",
      "name": "旁白",
      "asset": null,
      "eventId": "HU02_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0126",
      "category": "dialogueLine",
      "id": "NTL_0126",
      "name": "陶岑",
      "asset": {
        "ref": "actor:npc_tao_cen"
      },
      "eventId": "HU02_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0127",
      "category": "dialogueLine",
      "id": "NTL_0127",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "HU03_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0128",
      "category": "dialogueLine",
      "id": "NTL_0128",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "HU03_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0129",
      "category": "dialogueLine",
      "id": "NTL_0129",
      "name": "旁白",
      "asset": null,
      "eventId": "HU03_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0130",
      "category": "dialogueLine",
      "id": "NTL_0130",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "HU03_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0131",
      "category": "dialogueLine",
      "id": "NTL_0131",
      "name": "旁白",
      "asset": null,
      "eventId": "HU03_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0132",
      "category": "dialogueLine",
      "id": "NTL_0132",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "HU03_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0133",
      "category": "dialogueLine",
      "id": "NTL_0133",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "HU04_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0134",
      "category": "dialogueLine",
      "id": "NTL_0134",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "HU04_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0135",
      "category": "dialogueLine",
      "id": "NTL_0135",
      "name": "旁白",
      "asset": null,
      "eventId": "HU04_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0136",
      "category": "dialogueLine",
      "id": "NTL_0136",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "HU04_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0137",
      "category": "dialogueLine",
      "id": "NTL_0137",
      "name": "旁白",
      "asset": null,
      "eventId": "HU04_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0138",
      "category": "dialogueLine",
      "id": "NTL_0138",
      "name": "杜穗",
      "asset": {
        "ref": "actor:npc_du_sui"
      },
      "eventId": "HU04_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0139",
      "category": "dialogueLine",
      "id": "NTL_0139",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "CH01_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0140",
      "category": "dialogueLine",
      "id": "NTL_0140",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "CH01_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0141",
      "category": "dialogueLine",
      "id": "NTL_0141",
      "name": "旁白",
      "asset": null,
      "eventId": "CH01_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0142",
      "category": "dialogueLine",
      "id": "NTL_0142",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "CH01_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0143",
      "category": "dialogueLine",
      "id": "NTL_0143",
      "name": "旁白",
      "asset": null,
      "eventId": "CH01_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0144",
      "category": "dialogueLine",
      "id": "NTL_0144",
      "name": "白苓",
      "asset": {
        "ref": "actor:npc_bai_ling"
      },
      "eventId": "CH01_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0145",
      "category": "dialogueLine",
      "id": "NTL_0145",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "CH02_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0146",
      "category": "dialogueLine",
      "id": "NTL_0146",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "CH02_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0147",
      "category": "dialogueLine",
      "id": "NTL_0147",
      "name": "旁白",
      "asset": null,
      "eventId": "CH02_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0148",
      "category": "dialogueLine",
      "id": "NTL_0148",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "CH02_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0149",
      "category": "dialogueLine",
      "id": "NTL_0149",
      "name": "旁白",
      "asset": null,
      "eventId": "CH02_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0150",
      "category": "dialogueLine",
      "id": "NTL_0150",
      "name": "纪牧",
      "asset": {
        "ref": "actor:npc_ji_mu"
      },
      "eventId": "CH02_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0350",
      "category": "dialogueLine",
      "id": "NTL_0350",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "CH02_hub_backup_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0351",
      "category": "dialogueLine",
      "id": "NTL_0351",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "CH02_hub_backup_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0151",
      "category": "dialogueLine",
      "id": "NTL_0151",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "AC01_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0152",
      "category": "dialogueLine",
      "id": "NTL_0152",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "AC01_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0153",
      "category": "dialogueLine",
      "id": "NTL_0153",
      "name": "旁白",
      "asset": null,
      "eventId": "AC01_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0154",
      "category": "dialogueLine",
      "id": "NTL_0154",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "AC01_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0155",
      "category": "dialogueLine",
      "id": "NTL_0155",
      "name": "旁白",
      "asset": null,
      "eventId": "AC01_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0156",
      "category": "dialogueLine",
      "id": "NTL_0156",
      "name": "季闻",
      "asset": {
        "ref": "actor:npc_ji_wen"
      },
      "eventId": "AC01_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0157",
      "category": "dialogueLine",
      "id": "NTL_0157",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "AC02_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0158",
      "category": "dialogueLine",
      "id": "NTL_0158",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "AC02_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0159",
      "category": "dialogueLine",
      "id": "NTL_0159",
      "name": "旁白",
      "asset": null,
      "eventId": "AC02_work",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0160",
      "category": "dialogueLine",
      "id": "NTL_0160",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "AC02_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0161",
      "category": "dialogueLine",
      "id": "NTL_0161",
      "name": "旁白",
      "asset": null,
      "eventId": "AC02_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0162",
      "category": "dialogueLine",
      "id": "NTL_0162",
      "name": "邵宁",
      "asset": {
        "ref": "actor:npc_shao_ning"
      },
      "eventId": "AC02_repeat",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_0163",
      "category": "dialogueLine",
      "id": "NTL_0163",
      "name": "旁白",
      "asset": null,
      "eventId": "AC02_external_medical_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0164",
      "category": "dialogueLine",
      "id": "NTL_0164",
      "name": "旁白",
      "asset": null,
      "eventId": "AC02_external_medical_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0165",
      "category": "dialogueLine",
      "id": "NTL_0165",
      "name": "旁白",
      "asset": null,
      "eventId": "AC02_external_medical_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "narration"
    },
    {
      "key": "dialogueLine:NTL_0166",
      "category": "dialogueLine",
      "id": "NTL_0166",
      "name": "你",
      "asset": {
        "ref": "actor:p0"
      },
      "eventId": "AC02_external_medical_note",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_RN_01",
      "category": "dialogueLine",
      "id": "NTL_RN_01",
      "name": "林昭",
      "asset": {
        "ref": "actor:npc_lin_zhao"
      },
      "eventId": "RN01_recruit_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_RN_02",
      "category": "dialogueLine",
      "id": "NTL_RN_02",
      "name": "周砚",
      "asset": {
        "ref": "actor:npc_zhou_yan"
      },
      "eventId": "RN02_recruit_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_RN_03",
      "category": "dialogueLine",
      "id": "NTL_RN_03",
      "name": "沈禾",
      "asset": {
        "ref": "actor:npc_shen_he"
      },
      "eventId": "RN03_recruit_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_RN_04",
      "category": "dialogueLine",
      "id": "NTL_RN_04",
      "name": "唐梨",
      "asset": {
        "ref": "actor:npc_tang_li"
      },
      "eventId": "RN04_recruit_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_RN_05",
      "category": "dialogueLine",
      "id": "NTL_RN_05",
      "name": "顾承",
      "asset": {
        "ref": "actor:npc_gu_cheng"
      },
      "eventId": "RN05_recruit_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_RN_06",
      "category": "dialogueLine",
      "id": "NTL_RN_06",
      "name": "叶洵",
      "asset": {
        "ref": "actor:npc_ye_xun"
      },
      "eventId": "RN06_recruit_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ05_offer",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ05_offer",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "EXQ05_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ05_turnin",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ05_turnin",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "EXQ05_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ06_offer",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ06_offer",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "EXQ06_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ06_turnin",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ06_turnin",
      "name": "罗铮",
      "asset": {
        "ref": "actor:npc_luo_zheng"
      },
      "eventId": "EXQ06_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ07_offer",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ07_offer",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "EXQ07_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ07_turnin",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ07_turnin",
      "name": "许安",
      "asset": {
        "ref": "actor:npc_xu_an"
      },
      "eventId": "EXQ07_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ08_offer",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ08_offer",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "EXQ08_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q217_EXQ08_turnin",
      "category": "dialogueLine",
      "id": "NTL_Q217_EXQ08_turnin",
      "name": "陈芷",
      "asset": {
        "ref": "actor:npc_chen_zhi"
      },
      "eventId": "EXQ08_turnin",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_01",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_01",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_01",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_01",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_01",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_01",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_lu_heng"
      },
      "eventId": "npc_lu_heng_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_02",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_02",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_02",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_02",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_02",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_02",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_xu_an"
      },
      "eventId": "npc_xu_an_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_03",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_03",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_03",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_03",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_03",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_03",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_luo_zheng"
      },
      "eventId": "npc_luo_zheng_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_04",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_04",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_04",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_04",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_04",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_04",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_chen_zhi"
      },
      "eventId": "npc_chen_zhi_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_05",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_05",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_05",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_05",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_05",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_05",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_tao_cen"
      },
      "eventId": "npc_tao_cen_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_06",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_06",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_06",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_06",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_06",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_06",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_du_sui"
      },
      "eventId": "npc_du_sui_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_07",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_07",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_07",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_07",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_07",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_07",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_qi_wen"
      },
      "eventId": "npc_qi_wen_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_08",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_08",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_08",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_08",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_08",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_08",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_bai_ling"
      },
      "eventId": "npc_bai_ling_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_09",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_09",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_09",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_09",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_09",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_09",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_mu"
      },
      "eventId": "npc_ji_mu_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_10",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_10",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_10",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_10",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_10",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_10",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_song_zhu"
      },
      "eventId": "npc_song_zhu_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_11",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_11",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_11",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_11",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_11",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_11",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_fu_heng"
      },
      "eventId": "npc_fu_heng_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_12",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_12",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_12",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_12",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_12",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_12",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_wen_cheng"
      },
      "eventId": "npc_wen_cheng_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_13",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_13",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_13",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_13",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_13",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_13",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_ji_wen"
      },
      "eventId": "npc_ji_wen_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_14",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_14",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_14",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_14",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_14",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_14",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_shao_ning"
      },
      "eventId": "npc_shao_ning_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_15",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_15",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_15",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_15",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_15",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_15",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_he_yuan"
      },
      "eventId": "npc_he_yuan_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_IDLE_16",
      "category": "dialogueLine",
      "id": "NTL_Q236_IDLE_16",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_substitute_idle",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_OFFER_16",
      "category": "dialogueLine",
      "id": "NTL_Q236_OFFER_16",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_substitute_quest_offer",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueLine:NTL_Q236_PENDING_16",
      "category": "dialogueLine",
      "id": "NTL_Q236_PENDING_16",
      "name": "机构当班接续员",
      "asset": {
        "ref": "duty:npc_yin_ke"
      },
      "eventId": "npc_yin_ke_dialogue_substitute_quest_return",
      "source": "data.story.dialogues",
      "entry": "DialogueLayer / DialogueLogOverlay",
      "frame": "frame:dialogue",
      "layer": "portrait"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_first_greeting",
      "name": "陆衡：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_role",
      "name": "陆衡：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_quest_offer",
      "name": "陆衡：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_quest_return",
      "name": "陆衡：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_idle",
      "name": "陆衡：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_locked",
      "name": "陆衡：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_service_end",
      "name": "陆衡：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_institution_substitute",
      "name": "营门值守岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_service_offer",
      "name": "陆衡：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_first_greeting",
      "name": "许安：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_role",
      "name": "许安：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_quest_offer",
      "name": "许安：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_quest_return",
      "name": "许安：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_idle",
      "name": "许安：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_locked",
      "name": "许安：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_service_end",
      "name": "许安：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_institution_substitute",
      "name": "营区公事务岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_service_offer",
      "name": "许安：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_first_greeting",
      "name": "罗铮：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_role",
      "name": "罗铮：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_quest_offer",
      "name": "罗铮：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_quest_return",
      "name": "罗铮：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_idle",
      "name": "罗铮：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_locked",
      "name": "罗铮：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_service_end",
      "name": "罗铮：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_institution_substitute",
      "name": "营区工务岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_service_offer",
      "name": "罗铮：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_first_greeting",
      "name": "陈芷：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_role",
      "name": "陈芷：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_quest_offer",
      "name": "陈芷：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_quest_return",
      "name": "陈芷：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_idle",
      "name": "陈芷：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_locked",
      "name": "陈芷：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_service_end",
      "name": "陈芷：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_institution_substitute",
      "name": "营区医务岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_service_offer",
      "name": "陈芷：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_first_greeting",
      "name": "陶岑：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_role",
      "name": "陶岑：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_quest_offer",
      "name": "陶岑：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_quest_return",
      "name": "陶岑：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_idle",
      "name": "陶岑：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_locked",
      "name": "陶岑：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_service_end",
      "name": "陶岑：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_institution_substitute",
      "name": "营区鉴定岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_service_offer",
      "name": "陶岑：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_first_greeting",
      "name": "杜穗：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_role",
      "name": "杜穗：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_quest_offer",
      "name": "杜穗：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_quest_return",
      "name": "杜穗：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_idle",
      "name": "杜穗：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_locked",
      "name": "杜穗：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_service_end",
      "name": "杜穗：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_institution_substitute",
      "name": "收容安置岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_service_offer",
      "name": "杜穗：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_first_greeting",
      "name": "祁闻：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_role",
      "name": "祁闻：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_quest_offer",
      "name": "祁闻：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_quest_return",
      "name": "祁闻：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_idle",
      "name": "祁闻：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_locked",
      "name": "祁闻：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_service_end",
      "name": "祁闻：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_institution_substitute",
      "name": "教会接待岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_service_offer",
      "name": "祁闻：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_first_greeting",
      "name": "白苓：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_role",
      "name": "白苓：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_quest_offer",
      "name": "白苓：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_quest_return",
      "name": "白苓：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_idle",
      "name": "白苓：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_locked",
      "name": "白苓：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_service_end",
      "name": "白苓：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_institution_substitute",
      "name": "教会施济岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_service_offer",
      "name": "白苓：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_first_greeting",
      "name": "纪牧：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_role",
      "name": "纪牧：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_quest_offer",
      "name": "纪牧：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_quest_return",
      "name": "纪牧：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_idle",
      "name": "纪牧：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_locked",
      "name": "纪牧：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_service_end",
      "name": "纪牧：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_institution_substitute",
      "name": "教会训诫岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_service_offer",
      "name": "纪牧：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_first_greeting",
      "name": "宋烛：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_role",
      "name": "宋烛：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_quest_offer",
      "name": "宋烛：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_quest_return",
      "name": "宋烛：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_idle",
      "name": "宋烛：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_locked",
      "name": "宋烛：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_service_end",
      "name": "宋烛：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_institution_substitute",
      "name": "教会誊录岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_service_offer",
      "name": "宋烛：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_first_greeting",
      "name": "傅珩：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_role",
      "name": "傅珩：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_quest_offer",
      "name": "傅珩：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_quest_return",
      "name": "傅珩：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_idle",
      "name": "傅珩：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_locked",
      "name": "傅珩：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_service_end",
      "name": "傅珩：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_institution_substitute",
      "name": "圣堂主持岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_service_offer",
      "name": "傅珩：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_first_greeting",
      "name": "温澄：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_role",
      "name": "温澄：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_quest_offer",
      "name": "温澄：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_quest_return",
      "name": "温澄：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_idle",
      "name": "温澄：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_locked",
      "name": "温澄：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_service_end",
      "name": "温澄：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_institution_substitute",
      "name": "科学院接待岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_service_offer",
      "name": "温澄：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_first_greeting",
      "name": "季闻：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_role",
      "name": "季闻：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_quest_offer",
      "name": "季闻：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_quest_return",
      "name": "季闻：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_idle",
      "name": "季闻：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_locked",
      "name": "季闻：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_service_end",
      "name": "季闻：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_institution_substitute",
      "name": "科学院检测岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_service_offer",
      "name": "季闻：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_first_greeting",
      "name": "邵宁：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_role",
      "name": "邵宁：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_quest_offer",
      "name": "邵宁：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_quest_return",
      "name": "邵宁：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_idle",
      "name": "邵宁：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_locked",
      "name": "邵宁：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_service_end",
      "name": "邵宁：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_institution_substitute",
      "name": "科学院档案岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_service_offer",
      "name": "邵宁：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_first_greeting",
      "name": "何远：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_role",
      "name": "何远：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_quest_offer",
      "name": "何远：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_quest_return",
      "name": "何远：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_idle",
      "name": "何远：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_locked",
      "name": "何远：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_service_end",
      "name": "何远：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_institution_substitute",
      "name": "科学院设备岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_service_offer",
      "name": "何远：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_first_greeting",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_first_greeting",
      "name": "尹珂：初次见面",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_first_greeting",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_role",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_role",
      "name": "尹珂：岗位说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_role",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_quest_offer",
      "name": "尹珂：委托",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_quest_return",
      "name": "尹珂：委托回报",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_idle",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_idle",
      "name": "尹珂：闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_locked",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_locked",
      "name": "尹珂：暂不可办",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_locked",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_service_end",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_service_end",
      "name": "尹珂：办完事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_service_end",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_institution_substitute",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_institution_substitute",
      "name": "科学院观察岗：岗位接续",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_institution_substitute",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_service_offer",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_service_offer",
      "name": "尹珂：办理事项",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_service_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:gate_h01_registration",
      "category": "dialogueSet",
      "id": "city:gate_h01_registration",
      "name": "营区入营登记",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "gate_h01_registration",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:academy_public_observation_notice",
      "category": "dialogueSet",
      "id": "city:academy_public_observation_notice",
      "name": "观察区公开通道告示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "academy_public_observation_notice",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_substitute_idle",
      "name": "营门值守岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_substitute_quest_offer",
      "name": "营门值守岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_lu_heng_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_lu_heng_dialogue_substitute_quest_return",
      "name": "营门值守岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_lu_heng_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_substitute_idle",
      "name": "营区公事务岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_substitute_quest_offer",
      "name": "营区公事务岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_xu_an_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_xu_an_dialogue_substitute_quest_return",
      "name": "营区公事务岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_xu_an_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_substitute_idle",
      "name": "营区工务岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_substitute_quest_offer",
      "name": "营区工务岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_luo_zheng_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_luo_zheng_dialogue_substitute_quest_return",
      "name": "营区工务岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_luo_zheng_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_substitute_idle",
      "name": "营区医务岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_substitute_quest_offer",
      "name": "营区医务岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_chen_zhi_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_chen_zhi_dialogue_substitute_quest_return",
      "name": "营区医务岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_chen_zhi_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_substitute_idle",
      "name": "营区鉴定岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_substitute_quest_offer",
      "name": "营区鉴定岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_tao_cen_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_tao_cen_dialogue_substitute_quest_return",
      "name": "营区鉴定岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_tao_cen_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_substitute_idle",
      "name": "收容安置岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_substitute_quest_offer",
      "name": "收容安置岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_du_sui_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_du_sui_dialogue_substitute_quest_return",
      "name": "收容安置岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_du_sui_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_substitute_idle",
      "name": "教会接待岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_substitute_quest_offer",
      "name": "教会接待岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_qi_wen_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_qi_wen_dialogue_substitute_quest_return",
      "name": "教会接待岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_qi_wen_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_substitute_idle",
      "name": "教会施济岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_substitute_quest_offer",
      "name": "教会施济岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_bai_ling_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_bai_ling_dialogue_substitute_quest_return",
      "name": "教会施济岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_bai_ling_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_substitute_idle",
      "name": "教会训诫岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_substitute_quest_offer",
      "name": "教会训诫岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_mu_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_ji_mu_dialogue_substitute_quest_return",
      "name": "教会训诫岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_mu_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_substitute_idle",
      "name": "教会誊录岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_substitute_quest_offer",
      "name": "教会誊录岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_song_zhu_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_song_zhu_dialogue_substitute_quest_return",
      "name": "教会誊录岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_song_zhu_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_substitute_idle",
      "name": "圣堂主持岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_substitute_quest_offer",
      "name": "圣堂主持岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_fu_heng_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_fu_heng_dialogue_substitute_quest_return",
      "name": "圣堂主持岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_fu_heng_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_substitute_idle",
      "name": "科学院接待岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_substitute_quest_offer",
      "name": "科学院接待岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_wen_cheng_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_wen_cheng_dialogue_substitute_quest_return",
      "name": "科学院接待岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_wen_cheng_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_substitute_idle",
      "name": "科学院检测岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_substitute_quest_offer",
      "name": "科学院检测岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_ji_wen_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_ji_wen_dialogue_substitute_quest_return",
      "name": "科学院检测岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_ji_wen_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_substitute_idle",
      "name": "科学院档案岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_substitute_quest_offer",
      "name": "科学院档案岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_shao_ning_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_shao_ning_dialogue_substitute_quest_return",
      "name": "科学院档案岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_shao_ning_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_substitute_idle",
      "name": "科学院设备岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_substitute_quest_offer",
      "name": "科学院设备岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_he_yuan_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_he_yuan_dialogue_substitute_quest_return",
      "name": "科学院设备岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_he_yuan_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_substitute_idle",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_substitute_idle",
      "name": "科学院观察岗：代班闲谈",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_substitute_idle",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_substitute_quest_offer",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_substitute_quest_offer",
      "name": "科学院观察岗：代班事项提示",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_substitute_quest_offer",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:city:npc_yin_ke_dialogue_substitute_quest_return",
      "category": "dialogueSet",
      "id": "city:npc_yin_ke_dialogue_substitute_quest_return",
      "name": "科学院观察岗：代班进度提醒",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "npc_yin_ke_dialogue_substitute_quest_return",
      "source": "data.city.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:FA01_public_brief",
      "category": "dialogueSet",
      "id": "quest:FA01_public_brief",
      "name": "营区之外：公开说明",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "FA01_public_brief",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:FA02_route_choice",
      "category": "dialogueSet",
      "id": "quest:FA02_route_choice",
      "name": "被称作新生的人：公开路线",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "FA02_route_choice",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:FA03_environment_choice",
      "category": "dialogueSet",
      "id": "quest:FA03_environment_choice",
      "name": "样本之外：环境调查路线",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "FA03_environment_choice",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:FA04_merge_accept",
      "category": "dialogueSet",
      "id": "quest:FA04_merge_accept",
      "name": "同一条归路：整理记录",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "FA04_merge_accept",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:FA04_merge_complete",
      "category": "dialogueSet",
      "id": "quest:FA04_merge_complete",
      "name": "同一条归路：完成",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "FA04_merge_complete",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:FA05_power_contact",
      "category": "dialogueSet",
      "id": "quest:FA05_power_contact",
      "name": "联络仍在继续：电源与联络",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "FA05_power_contact",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:FA05_contact_complete",
      "category": "dialogueSet",
      "id": "quest:FA05_contact_complete",
      "name": "联络仍在继续：完成",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "FA05_contact_complete",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU01_offer",
      "category": "dialogueSet",
      "id": "quest:HU01_offer",
      "name": "鉴定之前：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU01_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU01_work",
      "category": "dialogueSet",
      "id": "quest:HU01_work",
      "name": "鉴定之前：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU01_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU01_turnin",
      "category": "dialogueSet",
      "id": "quest:HU01_turnin",
      "name": "鉴定之前：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU01_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU01_repeat",
      "category": "dialogueSet",
      "id": "quest:HU01_repeat",
      "name": "鉴定之前：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU01_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU02_offer",
      "category": "dialogueSet",
      "id": "quest:HU02_offer",
      "name": "样本去向：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU02_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU02_work",
      "category": "dialogueSet",
      "id": "quest:HU02_work",
      "name": "样本去向：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU02_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU02_turnin",
      "category": "dialogueSet",
      "id": "quest:HU02_turnin",
      "name": "样本去向：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU02_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU02_repeat",
      "category": "dialogueSet",
      "id": "quest:HU02_repeat",
      "name": "样本去向：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU02_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU03_offer",
      "category": "dialogueSet",
      "id": "quest:HU03_offer",
      "name": "同行不是归属：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU03_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU03_work",
      "category": "dialogueSet",
      "id": "quest:HU03_work",
      "name": "同行不是归属：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU03_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU03_turnin",
      "category": "dialogueSet",
      "id": "quest:HU03_turnin",
      "name": "同行不是归属：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU03_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU03_repeat",
      "category": "dialogueSet",
      "id": "quest:HU03_repeat",
      "name": "同行不是归属：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU03_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU04_offer",
      "category": "dialogueSet",
      "id": "quest:HU04_offer",
      "name": "留守之前：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU04_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU04_work",
      "category": "dialogueSet",
      "id": "quest:HU04_work",
      "name": "留守之前：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU04_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU04_turnin",
      "category": "dialogueSet",
      "id": "quest:HU04_turnin",
      "name": "留守之前：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU04_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:HU04_repeat",
      "category": "dialogueSet",
      "id": "quest:HU04_repeat",
      "name": "留守之前：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "HU04_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH01_offer",
      "category": "dialogueSet",
      "id": "quest:CH01_offer",
      "name": "施济清单：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH01_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH01_work",
      "category": "dialogueSet",
      "id": "quest:CH01_work",
      "name": "施济清单：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH01_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH01_turnin",
      "category": "dialogueSet",
      "id": "quest:CH01_turnin",
      "name": "施济清单：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH01_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH01_repeat",
      "category": "dialogueSet",
      "id": "quest:CH01_repeat",
      "name": "施济清单：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH01_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH02_offer",
      "category": "dialogueSet",
      "id": "quest:CH02_offer",
      "name": "被扣下的记录：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH02_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH02_work",
      "category": "dialogueSet",
      "id": "quest:CH02_work",
      "name": "被扣下的记录：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH02_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH02_turnin",
      "category": "dialogueSet",
      "id": "quest:CH02_turnin",
      "name": "被扣下的记录：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH02_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH02_repeat",
      "category": "dialogueSet",
      "id": "quest:CH02_repeat",
      "name": "被扣下的记录：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH02_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:CH02_hub_backup_turnin",
      "category": "dialogueSet",
      "id": "quest:CH02_hub_backup_turnin",
      "name": "被扣下的记录：营区备份",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "CH02_hub_backup_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC01_offer",
      "category": "dialogueSet",
      "id": "quest:AC01_offer",
      "name": "观察的边界：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC01_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC01_work",
      "category": "dialogueSet",
      "id": "quest:AC01_work",
      "name": "观察的边界：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC01_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC01_turnin",
      "category": "dialogueSet",
      "id": "quest:AC01_turnin",
      "name": "观察的边界：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC01_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC01_repeat",
      "category": "dialogueSet",
      "id": "quest:AC01_repeat",
      "name": "观察的边界：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC01_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC02_offer",
      "category": "dialogueSet",
      "id": "quest:AC02_offer",
      "name": "相互矛盾的报告：接取",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC02_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC02_work",
      "category": "dialogueSet",
      "id": "quest:AC02_work",
      "name": "相互矛盾的报告：工作",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC02_work",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC02_turnin",
      "category": "dialogueSet",
      "id": "quest:AC02_turnin",
      "name": "相互矛盾的报告：交付",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC02_turnin",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC02_repeat",
      "category": "dialogueSet",
      "id": "quest:AC02_repeat",
      "name": "相互矛盾的报告：回看",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC02_repeat",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:AC02_external_medical_note",
      "category": "dialogueSet",
      "id": "quest:AC02_external_medical_note",
      "name": "相互矛盾的报告：外部医疗线索",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "AC02_external_medical_note",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:RN01_recruit_offer",
      "category": "dialogueSet",
      "id": "quest:RN01_recruit_offer",
      "name": "相互照应：邀约",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "RN01_recruit_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:RN02_recruit_offer",
      "category": "dialogueSet",
      "id": "quest:RN02_recruit_offer",
      "name": "检修交接：邀约",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "RN02_recruit_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:RN03_recruit_offer",
      "category": "dialogueSet",
      "id": "quest:RN03_recruit_offer",
      "name": "转运手册：邀约",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "RN03_recruit_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:RN04_recruit_offer",
      "category": "dialogueSet",
      "id": "quest:RN04_recruit_offer",
      "name": "林间归路：邀约",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "RN04_recruit_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:RN05_recruit_offer",
      "category": "dialogueSet",
      "id": "quest:RN05_recruit_offer",
      "name": "封存清单：邀约",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "RN05_recruit_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:quest:RN06_recruit_offer",
      "category": "dialogueSet",
      "id": "quest:RN06_recruit_offer",
      "name": "值守记录：邀约",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "RN06_recruit_offer",
      "source": "data.quests.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:visitor_arrival_camp",
      "category": "dialogueSet",
      "id": "opening:visitor_arrival_camp",
      "name": "来客抵达",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "visitor_arrival_camp",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:visitor_late_or_legacy",
      "category": "dialogueSet",
      "id": "opening:visitor_late_or_legacy",
      "name": "来客抵达",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "visitor_late_or_legacy",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:visitor_route_first",
      "category": "dialogueSet",
      "id": "opening:visitor_route_first",
      "name": "程望：营区路线",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "visitor_route_first",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:visitor_after_route_baobao",
      "category": "dialogueSet",
      "id": "opening:visitor_after_route_baobao",
      "name": "交谈之后",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "visitor_after_route_baobao",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:visitor_after_route_without_baobao",
      "category": "dialogueSet",
      "id": "opening:visitor_after_route_without_baobao",
      "name": "交谈之后",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "visitor_after_route_without_baobao",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:visitor_route_repeat",
      "category": "dialogueSet",
      "id": "opening:visitor_route_repeat",
      "name": "程望：再问路线",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "visitor_route_repeat",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:visitor_not_ready",
      "category": "dialogueSet",
      "id": "opening:visitor_not_ready",
      "name": "程望：先准备",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "visitor_not_ready",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO15_relief_note",
      "category": "dialogueSet",
      "id": "opening:PRO15_relief_note",
      "name": "公开救助残页",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO15_relief_note",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:village_south_sign",
      "category": "dialogueSet",
      "id": "opening:village_south_sign",
      "name": "南口路标",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "village_south_sign",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:village_south_sign_route_unknown",
      "category": "dialogueSet",
      "id": "opening:village_south_sign_route_unknown",
      "name": "南口路标",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "village_south_sign_route_unknown",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:camp_route_log",
      "category": "dialogueSet",
      "id": "opening:camp_route_log",
      "name": "求医路线日志",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "camp_route_log",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO01_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO01_reaction",
      "name": "看清眼前",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO01_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO02_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO02_reaction",
      "name": "留住力气",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO02_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO03_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO03_reaction",
      "name": "带点防身物",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO03_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO04_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO04_reaction",
      "name": "第一次交手",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO04_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO05_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO05_reaction",
      "name": "身体里的变化",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO05_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO06_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO06_reaction",
      "name": "找到水源",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO06_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO07_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO07_reaction",
      "name": "火要有燃料",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO07_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO08_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO08_reaction",
      "name": "一份安全的水",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO08_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO09_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO09_reaction",
      "name": "能再做的食物",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO09_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO10_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO10_reaction",
      "name": "工具换来效率",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO10_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO11_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO11_reaction",
      "name": "五格不是五份",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO11_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO12_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO12_reaction",
      "name": "不要硬撑",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO12_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO13_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO13_reaction",
      "name": "留个能回的地方",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO13_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO14_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO14_reaction",
      "name": "走一趟再回来",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO14_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO15_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO15_reaction",
      "name": "找一条求医线索",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO15_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO16_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO16_reaction",
      "name": "来客与路",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO16_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO17_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO17_reaction",
      "name": "为远行打包",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO17_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "dialogueSet:opening:PRO18_reaction",
      "category": "dialogueSet",
      "id": "opening:PRO18_reaction",
      "name": "走向有人烟的地方",
      "asset": {
        "ref": "frame:dialogue"
      },
      "eventId": "PRO18_reaction",
      "source": "data.prologue.dialogueSets",
      "entry": "DialogueLayer / DialogueLogOverlay"
    },
    {
      "key": "glyph:health",
      "category": "glyph",
      "id": "health",
      "name": "生命",
      "asset": {
        "file": "svg/glyph/health.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:satiety",
      "category": "glyph",
      "id": "satiety",
      "name": "饱食",
      "asset": {
        "file": "svg/glyph/satiety.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:hydration",
      "category": "glyph",
      "id": "hydration",
      "name": "水分",
      "asset": {
        "file": "svg/glyph/hydration.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:energy",
      "category": "glyph",
      "id": "energy",
      "name": "精力",
      "asset": {
        "file": "svg/glyph/energy.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:wakefulness",
      "category": "glyph",
      "id": "wakefulness",
      "name": "清醒",
      "asset": {
        "file": "svg/glyph/wakefulness.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:morale",
      "category": "glyph",
      "id": "morale",
      "name": "士气",
      "asset": {
        "file": "svg/glyph/morale.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:immunity",
      "category": "glyph",
      "id": "immunity",
      "name": "免疫",
      "asset": {
        "file": "svg/glyph/immunity.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:virus",
      "category": "glyph",
      "id": "virus",
      "name": "感染",
      "asset": {
        "file": "svg/glyph/virus.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:bleed",
      "category": "glyph",
      "id": "bleed",
      "name": "流血",
      "asset": {
        "file": "svg/glyph/bleed.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:stagger",
      "category": "glyph",
      "id": "stagger",
      "name": "失衡",
      "asset": {
        "file": "svg/glyph/stagger.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:slow",
      "category": "glyph",
      "id": "slow",
      "name": "迟缓",
      "asset": {
        "file": "svg/glyph/slow.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:fear",
      "category": "glyph",
      "id": "fear",
      "name": "恐惧",
      "asset": {
        "file": "svg/glyph/fear.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:panic",
      "category": "glyph",
      "id": "panic",
      "name": "恐慌",
      "asset": {
        "file": "svg/glyph/panic.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:disabled",
      "category": "glyph",
      "id": "disabled",
      "name": "锁定",
      "asset": {
        "file": "svg/glyph/disabled.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:warning",
      "category": "glyph",
      "id": "warning",
      "name": "风险",
      "asset": {
        "file": "svg/glyph/warning.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:selected",
      "category": "glyph",
      "id": "selected",
      "name": "选中",
      "asset": {
        "file": "svg/glyph/selected.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:drop_allow",
      "category": "glyph",
      "id": "drop_allow",
      "name": "可放入",
      "asset": {
        "file": "svg/glyph/drop_allow.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:drop_deny",
      "category": "glyph",
      "id": "drop_deny",
      "name": "不可放入",
      "asset": {
        "file": "svg/glyph/drop_deny.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:progress",
      "category": "glyph",
      "id": "progress",
      "name": "进度",
      "asset": {
        "file": "svg/glyph/progress.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:loading",
      "category": "glyph",
      "id": "loading",
      "name": "读取中",
      "asset": {
        "file": "svg/glyph/loading.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:focus",
      "category": "glyph",
      "id": "focus",
      "name": "焦点",
      "asset": {
        "file": "svg/glyph/focus.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:drag",
      "category": "glyph",
      "id": "drag",
      "name": "拖放",
      "asset": {
        "file": "svg/glyph/drag.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:transfer",
      "category": "glyph",
      "id": "transfer",
      "name": "转移",
      "asset": {
        "file": "svg/glyph/transfer.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:close",
      "category": "glyph",
      "id": "close",
      "name": "关闭",
      "asset": {
        "file": "svg/glyph/close.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:add",
      "category": "glyph",
      "id": "add",
      "name": "增加",
      "asset": {
        "file": "svg/glyph/add.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:rest",
      "category": "glyph",
      "id": "rest",
      "name": "休息",
      "asset": {
        "file": "svg/glyph/rest.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:map",
      "category": "glyph",
      "id": "map",
      "name": "地图",
      "asset": {
        "file": "svg/glyph/map.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:dialogue",
      "category": "glyph",
      "id": "dialogue",
      "name": "对白",
      "asset": {
        "file": "svg/glyph/dialogue.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:log",
      "category": "glyph",
      "id": "log",
      "name": "日志",
      "asset": {
        "file": "svg/glyph/log.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:goal",
      "category": "glyph",
      "id": "goal",
      "name": "目标",
      "asset": {
        "file": "svg/glyph/goal.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:help",
      "category": "glyph",
      "id": "help",
      "name": "说明",
      "asset": {
        "file": "svg/glyph/help.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:menu",
      "category": "glyph",
      "id": "menu",
      "name": "菜单",
      "asset": {
        "file": "svg/glyph/menu.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:battle",
      "category": "glyph",
      "id": "battle",
      "name": "战斗",
      "asset": {
        "file": "svg/glyph/battle.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:aim",
      "category": "glyph",
      "id": "aim",
      "name": "瞄准",
      "asset": {
        "file": "svg/glyph/aim.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:equipment",
      "category": "glyph",
      "id": "equipment",
      "name": "整备",
      "asset": {
        "file": "svg/glyph/equipment.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:craft",
      "category": "glyph",
      "id": "craft",
      "name": "制作",
      "asset": {
        "file": "svg/glyph/craft.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:formation",
      "category": "glyph",
      "id": "formation",
      "name": "编队",
      "asset": {
        "file": "svg/glyph/formation.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:growth",
      "category": "glyph",
      "id": "growth",
      "name": "成长",
      "asset": {
        "file": "svg/glyph/growth.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:pause",
      "category": "glyph",
      "id": "pause",
      "name": "暂停",
      "asset": {
        "file": "svg/glyph/pause.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:continue",
      "category": "glyph",
      "id": "continue",
      "name": "继续",
      "asset": {
        "file": "svg/glyph/continue.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:fresh",
      "category": "glyph",
      "id": "fresh",
      "name": "新游戏",
      "asset": {
        "file": "svg/glyph/fresh.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:save",
      "category": "glyph",
      "id": "save",
      "name": "存档",
      "asset": {
        "file": "svg/glyph/save.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:return",
      "category": "glyph",
      "id": "return",
      "name": "返回",
      "asset": {
        "file": "svg/glyph/return.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:history",
      "category": "glyph",
      "id": "history",
      "name": "历史",
      "asset": {
        "file": "svg/glyph/history.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:storage",
      "category": "glyph",
      "id": "storage",
      "name": "仓储",
      "asset": {
        "file": "svg/glyph/storage.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:search",
      "category": "glyph",
      "id": "search",
      "name": "查看",
      "asset": {
        "file": "svg/glyph/search.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:task",
      "category": "glyph",
      "id": "task",
      "name": "任务",
      "asset": {
        "file": "svg/glyph/task.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:known",
      "category": "glyph",
      "id": "known",
      "name": "已知地点",
      "asset": {
        "file": "svg/glyph/known.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:unknown",
      "category": "glyph",
      "id": "unknown",
      "name": "未知地点",
      "asset": {
        "file": "svg/glyph/unknown.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:route",
      "category": "glyph",
      "id": "route",
      "name": "路线",
      "asset": {
        "file": "svg/glyph/route.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:destination",
      "category": "glyph",
      "id": "destination",
      "name": "目标地点",
      "asset": {
        "file": "svg/glyph/destination.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:camp",
      "category": "glyph",
      "id": "camp",
      "name": "营地",
      "asset": {
        "file": "svg/glyph/camp.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:dungeon",
      "category": "glyph",
      "id": "dungeon",
      "name": "地牢",
      "asset": {
        "file": "svg/glyph/dungeon.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:recruit",
      "category": "glyph",
      "id": "recruit",
      "name": "招募",
      "asset": {
        "file": "svg/glyph/recruit.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:service",
      "category": "glyph",
      "id": "service",
      "name": "服务",
      "asset": {
        "file": "svg/glyph/service.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:watch",
      "category": "glyph",
      "id": "watch",
      "name": "时间",
      "asset": {
        "file": "svg/glyph/watch.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:expand",
      "category": "glyph",
      "id": "expand",
      "name": "展开",
      "asset": {
        "file": "svg/glyph/expand.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:collapse",
      "category": "glyph",
      "id": "collapse",
      "name": "折叠",
      "asset": {
        "file": "svg/glyph/collapse.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:recipe",
      "category": "glyph",
      "id": "recipe",
      "name": "配方",
      "asset": {
        "file": "svg/glyph/recipe.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:knowledge",
      "category": "glyph",
      "id": "knowledge",
      "name": "知识",
      "asset": {
        "file": "svg/glyph/knowledge.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:foot",
      "category": "glyph",
      "id": "foot",
      "name": "行走",
      "asset": {
        "file": "svg/glyph/foot.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:weapon",
      "category": "glyph",
      "id": "weapon",
      "name": "武器",
      "asset": {
        "file": "svg/glyph/weapon.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:armor",
      "category": "glyph",
      "id": "armor",
      "name": "护甲",
      "asset": {
        "file": "svg/glyph/armor.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:utility",
      "category": "glyph",
      "id": "utility",
      "name": "工具",
      "asset": {
        "file": "svg/glyph/utility.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:all",
      "category": "glyph",
      "id": "all",
      "name": "全部",
      "asset": {
        "file": "svg/glyph/all.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:lock",
      "category": "glyph",
      "id": "lock",
      "name": "封锁",
      "asset": {
        "file": "svg/glyph/lock.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "glyph:trap",
      "category": "glyph",
      "id": "trap",
      "name": "陷阱",
      "asset": {
        "file": "svg/glyph/trap.svg",
        "format": "svg",
        "alpha": true
      },
      "source": "UI 视觉部件",
      "entry": "StatusIcons · VitalIcon · 菜单栏"
    },
    {
      "key": "frame:panel",
      "category": "frame",
      "id": "panel",
      "name": "面板",
      "asset": {
        "file": "svg/frame/panel.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:card",
      "category": "frame",
      "id": "card",
      "name": "卡片",
      "asset": {
        "file": "svg/frame/card.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:selected",
      "category": "frame",
      "id": "selected",
      "name": "选中",
      "asset": {
        "file": "svg/frame/selected.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:focus",
      "category": "frame",
      "id": "focus",
      "name": "键盘焦点",
      "asset": {
        "file": "svg/frame/focus.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:tab",
      "category": "frame",
      "id": "tab",
      "name": "页签",
      "asset": {
        "file": "svg/frame/tab.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:tab_active",
      "category": "frame",
      "id": "tab_active",
      "name": "激活页签",
      "asset": {
        "file": "svg/frame/tab_active.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:button",
      "category": "frame",
      "id": "button",
      "name": "普通按钮",
      "asset": {
        "file": "svg/frame/button.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:primary",
      "category": "frame",
      "id": "primary",
      "name": "主操作",
      "asset": {
        "file": "svg/frame/primary.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:danger",
      "category": "frame",
      "id": "danger",
      "name": "危险确认",
      "asset": {
        "file": "svg/frame/danger.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:disabled",
      "category": "frame",
      "id": "disabled",
      "name": "不可用",
      "asset": {
        "file": "svg/frame/disabled.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:tooltip",
      "category": "frame",
      "id": "tooltip",
      "name": "悬浮信息",
      "asset": {
        "file": "svg/frame/tooltip.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:overlay",
      "category": "frame",
      "id": "overlay",
      "name": "覆盖窗",
      "asset": {
        "file": "svg/frame/overlay.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:menu",
      "category": "frame",
      "id": "menu",
      "name": "菜单外框",
      "asset": {
        "file": "svg/frame/menu.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:dialogue",
      "category": "frame",
      "id": "dialogue",
      "name": "对白外框",
      "asset": {
        "file": "svg/frame/dialogue.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:dialogue_battle",
      "category": "frame",
      "id": "dialogue_battle",
      "name": "战斗对白外框",
      "asset": {
        "file": "svg/frame/dialogue_battle.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:history",
      "category": "frame",
      "id": "history",
      "name": "历史阅读",
      "asset": {
        "file": "svg/frame/history.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:portrait",
      "category": "frame",
      "id": "portrait",
      "name": "肖像框",
      "asset": {
        "file": "svg/frame/portrait.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:slot",
      "category": "frame",
      "id": "slot",
      "name": "空槽",
      "asset": {
        "file": "svg/frame/slot.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:equipment",
      "category": "frame",
      "id": "equipment",
      "name": "装备槽",
      "asset": {
        "file": "svg/frame/equipment.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:skill",
      "category": "frame",
      "id": "skill",
      "name": "招式槽",
      "asset": {
        "file": "svg/frame/skill.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:legal_drop",
      "category": "frame",
      "id": "legal_drop",
      "name": "可放入",
      "asset": {
        "file": "svg/frame/legal_drop.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:denied_drop",
      "category": "frame",
      "id": "denied_drop",
      "name": "不可放入",
      "asset": {
        "file": "svg/frame/denied_drop.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:meter",
      "category": "frame",
      "id": "meter",
      "name": "进度槽",
      "asset": {
        "file": "svg/frame/meter.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:task",
      "category": "frame",
      "id": "task",
      "name": "任务卡",
      "asset": {
        "file": "svg/frame/task.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:map_node",
      "category": "frame",
      "id": "map_node",
      "name": "地图节点",
      "asset": {
        "file": "svg/frame/map_node.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:end",
      "category": "frame",
      "id": "end",
      "name": "结算",
      "asset": {
        "file": "svg/frame/end.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "frame:banner",
      "category": "frame",
      "id": "banner",
      "name": "场景标题",
      "asset": {
        "file": "svg/frame/banner.svg",
        "format": "svg",
        "alpha": true
      },
      "slice": 16,
      "source": "UI 视觉部件",
      "entry": "materials.css · CSS border-image",
      "display": "96 格 / 保持透明中心"
    },
    {
      "key": "texture:iron",
      "category": "texture",
      "id": "iron",
      "name": "铁灰",
      "asset": {
        "file": "svg/texture/iron.svg",
        "format": "svg",
        "alpha": true
      },
      "repeat": true,
      "source": "UI 视觉部件",
      "entry": "CSS background-image"
    },
    {
      "key": "texture:paper",
      "category": "texture",
      "id": "paper",
      "name": "旧纸",
      "asset": {
        "file": "svg/texture/paper.svg",
        "format": "svg",
        "alpha": true
      },
      "repeat": true,
      "source": "UI 视觉部件",
      "entry": "CSS background-image"
    },
    {
      "key": "texture:cloth",
      "category": "texture",
      "id": "cloth",
      "name": "粗布",
      "asset": {
        "file": "svg/texture/cloth.svg",
        "format": "svg",
        "alpha": true
      },
      "repeat": true,
      "source": "UI 视觉部件",
      "entry": "CSS background-image"
    },
    {
      "key": "texture:danger",
      "category": "texture",
      "id": "danger",
      "name": "红刻痕",
      "asset": {
        "file": "svg/texture/danger.svg",
        "format": "svg",
        "alpha": true
      },
      "repeat": true,
      "source": "UI 视觉部件",
      "entry": "CSS background-image"
    },
    {
      "key": "texture:shadow",
      "category": "texture",
      "id": "shadow",
      "name": "暗底",
      "asset": {
        "file": "svg/texture/shadow.svg",
        "format": "svg",
        "alpha": true
      },
      "repeat": true,
      "source": "UI 视觉部件",
      "entry": "CSS background-image"
    },
    {
      "key": "component:AccountIcon",
      "category": "component",
      "id": "AccountIcon",
      "name": "AccountIcon",
      "asset": {
        "ref": "frame:tooltip"
      },
      "kind": "feedback",
      "texture": "texture:shadow",
      "glyph": "glyph:warning",
      "layers": [
        "容器",
        "状态图形",
        "原因",
        "按钮"
      ],
      "source": "web/Survival.Web/Components/AccountIcon.razor",
      "entry": "AccountIcon.razor"
    },
    {
      "key": "component:ActionPops",
      "category": "component",
      "id": "ActionPops",
      "name": "ActionPops",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/ActionPops.razor",
      "entry": "ActionPops.razor"
    },
    {
      "key": "component:AnchorPop",
      "category": "component",
      "id": "AnchorPop",
      "name": "AnchorPop",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/AnchorPop.razor",
      "entry": "AnchorPop.razor"
    },
    {
      "key": "component:AppraisalPanel",
      "category": "component",
      "id": "AppraisalPanel",
      "name": "AppraisalPanel",
      "asset": {
        "ref": "frame:history"
      },
      "kind": "record",
      "texture": "texture:paper",
      "glyph": "glyph:log",
      "layers": [
        "边框",
        "列表",
        "当前条目",
        "文字",
        "返回"
      ],
      "source": "web/Survival.Web/Components/AppraisalPanel.razor",
      "entry": "AppraisalPanel.razor"
    },
    {
      "key": "component:AttrDetail",
      "category": "component",
      "id": "AttrDetail",
      "name": "AttrDetail",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/AttrDetail.razor",
      "entry": "AttrDetail.razor"
    },
    {
      "key": "component:BattleEquip",
      "category": "component",
      "id": "BattleEquip",
      "name": "BattleEquip",
      "asset": {
        "ref": "frame:skill"
      },
      "kind": "battle",
      "texture": "texture:danger",
      "glyph": "glyph:battle",
      "layers": [
        "背景",
        "站位主体",
        "目标",
        "HUD",
        "提示"
      ],
      "source": "web/Survival.Web/Components/BattleEquip.razor",
      "entry": "BattleEquip.razor"
    },
    {
      "key": "component:BattleOverlay",
      "category": "component",
      "id": "BattleOverlay",
      "name": "BattleOverlay",
      "asset": {
        "ref": "frame:skill"
      },
      "kind": "battle",
      "texture": "texture:danger",
      "glyph": "glyph:battle",
      "layers": [
        "背景",
        "站位主体",
        "目标",
        "HUD",
        "提示"
      ],
      "source": "web/Survival.Web/Components/BattleOverlay.razor",
      "entry": "BattleOverlay.razor"
    },
    {
      "key": "component:BattlePanel",
      "category": "component",
      "id": "BattlePanel",
      "name": "BattlePanel",
      "asset": {
        "ref": "frame:skill"
      },
      "kind": "battle",
      "texture": "texture:danger",
      "glyph": "glyph:battle",
      "layers": [
        "背景",
        "站位主体",
        "目标",
        "HUD",
        "提示"
      ],
      "source": "web/Survival.Web/Components/BattlePanel.razor",
      "entry": "BattlePanel.razor"
    },
    {
      "key": "component:BattleScreen",
      "category": "component",
      "id": "BattleScreen",
      "name": "BattleScreen",
      "asset": {
        "ref": "frame:skill"
      },
      "kind": "battle",
      "texture": "texture:danger",
      "glyph": "glyph:battle",
      "layers": [
        "背景",
        "站位主体",
        "目标",
        "HUD",
        "提示"
      ],
      "source": "web/Survival.Web/Components/BattleScreen.razor",
      "entry": "BattleScreen.razor"
    },
    {
      "key": "component:BattleSupplies",
      "category": "component",
      "id": "BattleSupplies",
      "name": "BattleSupplies",
      "asset": {
        "ref": "frame:skill"
      },
      "kind": "battle",
      "texture": "texture:danger",
      "glyph": "glyph:battle",
      "layers": [
        "背景",
        "站位主体",
        "目标",
        "HUD",
        "提示"
      ],
      "source": "web/Survival.Web/Components/BattleSupplies.razor",
      "entry": "BattleSupplies.razor"
    },
    {
      "key": "component:CardActions",
      "category": "component",
      "id": "CardActions",
      "name": "CardActions",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/CardActions.razor",
      "entry": "CardActions.razor"
    },
    {
      "key": "component:CardPop",
      "category": "component",
      "id": "CardPop",
      "name": "CardPop",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/CardPop.razor",
      "entry": "CardPop.razor"
    },
    {
      "key": "component:CentralProgress",
      "category": "component",
      "id": "CentralProgress",
      "name": "CentralProgress",
      "asset": {
        "ref": "frame:tooltip"
      },
      "kind": "feedback",
      "texture": "texture:shadow",
      "glyph": "glyph:warning",
      "layers": [
        "容器",
        "状态图形",
        "原因",
        "按钮"
      ],
      "source": "web/Survival.Web/Components/CentralProgress.razor",
      "entry": "CentralProgress.razor"
    },
    {
      "key": "component:CharacterPanel",
      "category": "component",
      "id": "CharacterPanel",
      "name": "CharacterPanel",
      "asset": {
        "ref": "frame:equipment"
      },
      "kind": "equipment",
      "texture": "texture:iron",
      "glyph": "glyph:equipment",
      "layers": [
        "主体",
        "七槽",
        "选中",
        "数值",
        "候选"
      ],
      "source": "web/Survival.Web/Components/CharacterPanel.razor",
      "entry": "CharacterPanel.razor"
    },
    {
      "key": "component:CityOverview",
      "category": "component",
      "id": "CityOverview",
      "name": "CityOverview",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/CityOverview.razor",
      "entry": "CityOverview.razor"
    },
    {
      "key": "component:CityServiceOverlay",
      "category": "component",
      "id": "CityServiceOverlay",
      "name": "CityServiceOverlay",
      "asset": {
        "ref": "frame:history"
      },
      "kind": "record",
      "texture": "texture:paper",
      "glyph": "glyph:log",
      "layers": [
        "边框",
        "列表",
        "当前条目",
        "文字",
        "返回"
      ],
      "source": "web/Survival.Web/Components/CityServiceOverlay.razor",
      "entry": "CityServiceOverlay.razor"
    },
    {
      "key": "component:CraftOverlay",
      "category": "component",
      "id": "CraftOverlay",
      "name": "CraftOverlay",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/CraftOverlay.razor",
      "entry": "CraftOverlay.razor"
    },
    {
      "key": "component:DetailPanel",
      "category": "component",
      "id": "DetailPanel",
      "name": "DetailPanel",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/DetailPanel.razor",
      "entry": "DetailPanel.razor"
    },
    {
      "key": "component:DialogueLayer",
      "category": "component",
      "id": "DialogueLayer",
      "name": "DialogueLayer",
      "asset": {
        "ref": "frame:dialogue"
      },
      "kind": "dialogue",
      "texture": "texture:paper",
      "glyph": "glyph:dialogue",
      "layers": [
        "背景",
        "遮罩",
        "肖像",
        "框",
        "文本",
        "继续"
      ],
      "source": "web/Survival.Web/Components/DialogueLayer.razor",
      "entry": "DialogueLayer.razor"
    },
    {
      "key": "component:DialogueLines",
      "category": "component",
      "id": "DialogueLines",
      "name": "DialogueLines",
      "asset": {
        "ref": "frame:dialogue"
      },
      "kind": "dialogue",
      "texture": "texture:paper",
      "glyph": "glyph:dialogue",
      "layers": [
        "背景",
        "遮罩",
        "肖像",
        "框",
        "文本",
        "继续"
      ],
      "source": "web/Survival.Web/Components/DialogueLines.razor",
      "entry": "DialogueLines.razor"
    },
    {
      "key": "component:DialogueLogOverlay",
      "category": "component",
      "id": "DialogueLogOverlay",
      "name": "DialogueLogOverlay",
      "asset": {
        "ref": "frame:dialogue"
      },
      "kind": "dialogue",
      "texture": "texture:paper",
      "glyph": "glyph:dialogue",
      "layers": [
        "背景",
        "遮罩",
        "肖像",
        "框",
        "文本",
        "继续"
      ],
      "source": "web/Survival.Web/Components/DialogueLogOverlay.razor",
      "entry": "DialogueLogOverlay.razor"
    },
    {
      "key": "component:DropQuantity",
      "category": "component",
      "id": "DropQuantity",
      "name": "DropQuantity",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/DropQuantity.razor",
      "entry": "DropQuantity.razor"
    },
    {
      "key": "component:DryingOverlay",
      "category": "component",
      "id": "DryingOverlay",
      "name": "DryingOverlay",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/DryingOverlay.razor",
      "entry": "DryingOverlay.razor"
    },
    {
      "key": "component:DryingRackCard",
      "category": "component",
      "id": "DryingRackCard",
      "name": "DryingRackCard",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/DryingRackCard.razor",
      "entry": "DryingRackCard.razor"
    },
    {
      "key": "component:DungeonReset",
      "category": "component",
      "id": "DungeonReset",
      "name": "DungeonReset",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/DungeonReset.razor",
      "entry": "DungeonReset.razor"
    },
    {
      "key": "component:DuplicateWeaponOverlay",
      "category": "component",
      "id": "DuplicateWeaponOverlay",
      "name": "DuplicateWeaponOverlay",
      "asset": {
        "ref": "frame:equipment"
      },
      "kind": "equipment",
      "texture": "texture:iron",
      "glyph": "glyph:equipment",
      "layers": [
        "主体",
        "七槽",
        "选中",
        "数值",
        "候选"
      ],
      "source": "web/Survival.Web/Components/DuplicateWeaponOverlay.razor",
      "entry": "DuplicateWeaponOverlay.razor"
    },
    {
      "key": "component:EndScreens",
      "category": "component",
      "id": "EndScreens",
      "name": "EndScreens",
      "asset": {
        "ref": "frame:menu"
      },
      "kind": "menu",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "背景",
        "遮罩",
        "按钮框",
        "状态",
        "动态文字"
      ],
      "source": "web/Survival.Web/Components/EndScreens.razor",
      "entry": "EndScreens.razor"
    },
    {
      "key": "component:EnemyDetail",
      "category": "component",
      "id": "EnemyDetail",
      "name": "EnemyDetail",
      "asset": {
        "ref": "frame:skill"
      },
      "kind": "battle",
      "texture": "texture:danger",
      "glyph": "glyph:battle",
      "layers": [
        "背景",
        "站位主体",
        "目标",
        "HUD",
        "提示"
      ],
      "source": "web/Survival.Web/Components/EnemyDetail.razor",
      "entry": "EnemyDetail.razor"
    },
    {
      "key": "component:ExpeditionAbort",
      "category": "component",
      "id": "ExpeditionAbort",
      "name": "ExpeditionAbort",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/ExpeditionAbort.razor",
      "entry": "ExpeditionAbort.razor"
    },
    {
      "key": "component:ExpeditionInfo",
      "category": "component",
      "id": "ExpeditionInfo",
      "name": "ExpeditionInfo",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/ExpeditionInfo.razor",
      "entry": "ExpeditionInfo.razor"
    },
    {
      "key": "component:ExpeditionSeal",
      "category": "component",
      "id": "ExpeditionSeal",
      "name": "ExpeditionSeal",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/ExpeditionSeal.razor",
      "entry": "ExpeditionSeal.razor"
    },
    {
      "key": "component:FacilityCard",
      "category": "component",
      "id": "FacilityCard",
      "name": "FacilityCard",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/FacilityCard.razor",
      "entry": "FacilityCard.razor"
    },
    {
      "key": "component:FeedPop",
      "category": "component",
      "id": "FeedPop",
      "name": "FeedPop",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/FeedPop.razor",
      "entry": "FeedPop.razor"
    },
    {
      "key": "component:FireCard",
      "category": "component",
      "id": "FireCard",
      "name": "FireCard",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/FireCard.razor",
      "entry": "FireCard.razor"
    },
    {
      "key": "component:FireDetail",
      "category": "component",
      "id": "FireDetail",
      "name": "FireDetail",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/FireDetail.razor",
      "entry": "FireDetail.razor"
    },
    {
      "key": "component:FoodCard",
      "category": "component",
      "id": "FoodCard",
      "name": "FoodCard",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/FoodCard.razor",
      "entry": "FoodCard.razor"
    },
    {
      "key": "component:FormationOverlay",
      "category": "component",
      "id": "FormationOverlay",
      "name": "FormationOverlay",
      "asset": {
        "ref": "frame:equipment"
      },
      "kind": "equipment",
      "texture": "texture:iron",
      "glyph": "glyph:equipment",
      "layers": [
        "主体",
        "七槽",
        "选中",
        "数值",
        "候选"
      ],
      "source": "web/Survival.Web/Components/FormationOverlay.razor",
      "entry": "FormationOverlay.razor"
    },
    {
      "key": "component:GameScreen",
      "category": "component",
      "id": "GameScreen",
      "name": "GameScreen",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "shell",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "场景",
        "HUD",
        "四轨",
        "导航",
        "全局反馈"
      ],
      "source": "web/Survival.Web/Components/GameScreen.razor",
      "entry": "GameScreen.razor"
    },
    {
      "key": "component:GearOverlay",
      "category": "component",
      "id": "GearOverlay",
      "name": "GearOverlay",
      "asset": {
        "ref": "frame:equipment"
      },
      "kind": "equipment",
      "texture": "texture:iron",
      "glyph": "glyph:equipment",
      "layers": [
        "主体",
        "七槽",
        "选中",
        "数值",
        "候选"
      ],
      "source": "web/Survival.Web/Components/GearOverlay.razor",
      "entry": "GearOverlay.razor"
    },
    {
      "key": "component:GoalsOverlay",
      "category": "component",
      "id": "GoalsOverlay",
      "name": "GoalsOverlay",
      "asset": {
        "ref": "frame:history"
      },
      "kind": "record",
      "texture": "texture:paper",
      "glyph": "glyph:log",
      "layers": [
        "边框",
        "列表",
        "当前条目",
        "文字",
        "返回"
      ],
      "source": "web/Survival.Web/Components/GoalsOverlay.razor",
      "entry": "GoalsOverlay.razor"
    },
    {
      "key": "component:GrowthOverlay",
      "category": "component",
      "id": "GrowthOverlay",
      "name": "GrowthOverlay",
      "asset": {
        "ref": "frame:equipment"
      },
      "kind": "equipment",
      "texture": "texture:iron",
      "glyph": "glyph:equipment",
      "layers": [
        "主体",
        "七槽",
        "选中",
        "数值",
        "候选"
      ],
      "source": "web/Survival.Web/Components/GrowthOverlay.razor",
      "entry": "GrowthOverlay.razor"
    },
    {
      "key": "component:HelpOverlay",
      "category": "component",
      "id": "HelpOverlay",
      "name": "HelpOverlay",
      "asset": {
        "ref": "frame:menu"
      },
      "kind": "menu",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "背景",
        "遮罩",
        "按钮框",
        "状态",
        "动态文字"
      ],
      "source": "web/Survival.Web/Components/HelpOverlay.razor",
      "entry": "HelpOverlay.razor"
    },
    {
      "key": "component:Hud",
      "category": "component",
      "id": "Hud",
      "name": "Hud",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "shell",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "场景",
        "HUD",
        "四轨",
        "导航",
        "全局反馈"
      ],
      "source": "web/Survival.Web/Components/Hud.razor",
      "entry": "Hud.razor"
    },
    {
      "key": "component:IgniteMenu",
      "category": "component",
      "id": "IgniteMenu",
      "name": "IgniteMenu",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/IgniteMenu.razor",
      "entry": "IgniteMenu.razor"
    },
    {
      "key": "component:ItemDetail",
      "category": "component",
      "id": "ItemDetail",
      "name": "ItemDetail",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/ItemDetail.razor",
      "entry": "ItemDetail.razor"
    },
    {
      "key": "component:ItemRow",
      "category": "component",
      "id": "ItemRow",
      "name": "ItemRow",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/ItemRow.razor",
      "entry": "ItemRow.razor"
    },
    {
      "key": "component:ItemShelf",
      "category": "component",
      "id": "ItemShelf",
      "name": "ItemShelf",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/ItemShelf.razor",
      "entry": "ItemShelf.razor"
    },
    {
      "key": "component:JourneyOverlay",
      "category": "component",
      "id": "JourneyOverlay",
      "name": "JourneyOverlay",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/JourneyOverlay.razor",
      "entry": "JourneyOverlay.razor"
    },
    {
      "key": "component:LocationCard",
      "category": "component",
      "id": "LocationCard",
      "name": "LocationCard",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/LocationCard.razor",
      "entry": "LocationCard.razor"
    },
    {
      "key": "component:LocationStrip",
      "category": "component",
      "id": "LocationStrip",
      "name": "LocationStrip",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/LocationStrip.razor",
      "entry": "LocationStrip.razor"
    },
    {
      "key": "component:LogOverlay",
      "category": "component",
      "id": "LogOverlay",
      "name": "LogOverlay",
      "asset": {
        "ref": "frame:history"
      },
      "kind": "record",
      "texture": "texture:paper",
      "glyph": "glyph:log",
      "layers": [
        "边框",
        "列表",
        "当前条目",
        "文字",
        "返回"
      ],
      "source": "web/Survival.Web/Components/LogOverlay.razor",
      "entry": "LogOverlay.razor"
    },
    {
      "key": "component:MapOverlay",
      "category": "component",
      "id": "MapOverlay",
      "name": "MapOverlay",
      "asset": {
        "ref": "frame:map_node"
      },
      "kind": "map",
      "texture": "texture:paper",
      "glyph": "glyph:map",
      "layers": [
        "地景",
        "路线",
        "节点",
        "状态",
        "标签"
      ],
      "source": "web/Survival.Web/Components/MapOverlay.razor",
      "entry": "MapOverlay.razor"
    },
    {
      "key": "component:MenuOverlay",
      "category": "component",
      "id": "MenuOverlay",
      "name": "MenuOverlay",
      "asset": {
        "ref": "frame:menu"
      },
      "kind": "menu",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "背景",
        "遮罩",
        "按钮框",
        "状态",
        "动态文字"
      ],
      "source": "web/Survival.Web/Components/MenuOverlay.razor",
      "entry": "MenuOverlay.razor"
    },
    {
      "key": "component:ObjectCard",
      "category": "component",
      "id": "ObjectCard",
      "name": "ObjectCard",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/ObjectCard.razor",
      "entry": "ObjectCard.razor"
    },
    {
      "key": "component:OverflowOverlay",
      "category": "component",
      "id": "OverflowOverlay",
      "name": "OverflowOverlay",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/OverflowOverlay.razor",
      "entry": "OverflowOverlay.razor"
    },
    {
      "key": "component:PartyPanel",
      "category": "component",
      "id": "PartyPanel",
      "name": "PartyPanel",
      "asset": {
        "ref": "frame:equipment"
      },
      "kind": "equipment",
      "texture": "texture:iron",
      "glyph": "glyph:equipment",
      "layers": [
        "主体",
        "七槽",
        "选中",
        "数值",
        "候选"
      ],
      "source": "web/Survival.Web/Components/PartyPanel.razor",
      "entry": "PartyPanel.razor"
    },
    {
      "key": "component:PauseLayer",
      "category": "component",
      "id": "PauseLayer",
      "name": "PauseLayer",
      "asset": {
        "ref": "frame:menu"
      },
      "kind": "menu",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "背景",
        "遮罩",
        "按钮框",
        "状态",
        "动态文字"
      ],
      "source": "web/Survival.Web/Components/PauseLayer.razor",
      "entry": "PauseLayer.razor"
    },
    {
      "key": "component:PotCard",
      "category": "component",
      "id": "PotCard",
      "name": "PotCard",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/PotCard.razor",
      "entry": "PotCard.razor"
    },
    {
      "key": "component:PreviewView",
      "category": "component",
      "id": "PreviewView",
      "name": "PreviewView",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/PreviewView.razor",
      "entry": "PreviewView.razor"
    },
    {
      "key": "component:PrologueCards",
      "category": "component",
      "id": "PrologueCards",
      "name": "PrologueCards",
      "asset": {
        "ref": "frame:dialogue"
      },
      "kind": "dialogue",
      "texture": "texture:paper",
      "glyph": "glyph:dialogue",
      "layers": [
        "背景",
        "遮罩",
        "肖像",
        "框",
        "文本",
        "继续"
      ],
      "source": "web/Survival.Web/Components/PrologueCards.razor",
      "entry": "PrologueCards.razor"
    },
    {
      "key": "component:PublicStationCard",
      "category": "component",
      "id": "PublicStationCard",
      "name": "PublicStationCard",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/PublicStationCard.razor",
      "entry": "PublicStationCard.razor"
    },
    {
      "key": "component:RecruitPanel",
      "category": "component",
      "id": "RecruitPanel",
      "name": "RecruitPanel",
      "asset": {
        "ref": "frame:equipment"
      },
      "kind": "equipment",
      "texture": "texture:iron",
      "glyph": "glyph:equipment",
      "layers": [
        "主体",
        "七槽",
        "选中",
        "数值",
        "候选"
      ],
      "source": "web/Survival.Web/Components/RecruitPanel.razor",
      "entry": "RecruitPanel.razor"
    },
    {
      "key": "component:RestOverlay",
      "category": "component",
      "id": "RestOverlay",
      "name": "RestOverlay",
      "asset": {
        "ref": "frame:history"
      },
      "kind": "record",
      "texture": "texture:paper",
      "glyph": "glyph:log",
      "layers": [
        "边框",
        "列表",
        "当前条目",
        "文字",
        "返回"
      ],
      "source": "web/Survival.Web/Components/RestOverlay.razor",
      "entry": "RestOverlay.razor"
    },
    {
      "key": "component:Root",
      "category": "component",
      "id": "Root",
      "name": "Root",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "shell",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "场景",
        "HUD",
        "四轨",
        "导航",
        "全局反馈"
      ],
      "source": "web/Survival.Web/Components/Root.razor",
      "entry": "Root.razor"
    },
    {
      "key": "component:SafehouseCard",
      "category": "component",
      "id": "SafehouseCard",
      "name": "SafehouseCard",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/SafehouseCard.razor",
      "entry": "SafehouseCard.razor"
    },
    {
      "key": "component:SafehouseOverlay",
      "category": "component",
      "id": "SafehouseOverlay",
      "name": "SafehouseOverlay",
      "asset": {
        "ref": "frame:panel"
      },
      "kind": "craft",
      "texture": "texture:iron",
      "glyph": "glyph:craft",
      "layers": [
        "主体",
        "台面框",
        "原料状态",
        "进度",
        "动作"
      ],
      "source": "web/Survival.Web/Components/SafehouseOverlay.razor",
      "entry": "SafehouseOverlay.razor"
    },
    {
      "key": "component:StartScreen",
      "category": "component",
      "id": "StartScreen",
      "name": "StartScreen",
      "asset": {
        "ref": "frame:menu"
      },
      "kind": "menu",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "背景",
        "遮罩",
        "按钮框",
        "状态",
        "动态文字"
      ],
      "source": "web/Survival.Web/Components/StartScreen.razor",
      "entry": "StartScreen.razor"
    },
    {
      "key": "component:StatusIcons",
      "category": "component",
      "id": "StatusIcons",
      "name": "StatusIcons",
      "asset": {
        "ref": "frame:tooltip"
      },
      "kind": "feedback",
      "texture": "texture:shadow",
      "glyph": "glyph:warning",
      "layers": [
        "容器",
        "状态图形",
        "原因",
        "按钮"
      ],
      "source": "web/Survival.Web/Components/StatusIcons.razor",
      "entry": "StatusIcons.razor"
    },
    {
      "key": "component:StatusPop",
      "category": "component",
      "id": "StatusPop",
      "name": "StatusPop",
      "asset": {
        "ref": "frame:tooltip"
      },
      "kind": "feedback",
      "texture": "texture:shadow",
      "glyph": "glyph:warning",
      "layers": [
        "容器",
        "状态图形",
        "原因",
        "按钮"
      ],
      "source": "web/Survival.Web/Components/StatusPop.razor",
      "entry": "StatusPop.razor"
    },
    {
      "key": "component:TargetPicker",
      "category": "component",
      "id": "TargetPicker",
      "name": "TargetPicker",
      "asset": {
        "ref": "frame:skill"
      },
      "kind": "battle",
      "texture": "texture:danger",
      "glyph": "glyph:battle",
      "layers": [
        "背景",
        "站位主体",
        "目标",
        "HUD",
        "提示"
      ],
      "source": "web/Survival.Web/Components/TargetPicker.razor",
      "entry": "TargetPicker.razor"
    },
    {
      "key": "component:TaskCorner",
      "category": "component",
      "id": "TaskCorner",
      "name": "TaskCorner",
      "asset": {
        "ref": "frame:history"
      },
      "kind": "record",
      "texture": "texture:paper",
      "glyph": "glyph:log",
      "layers": [
        "边框",
        "列表",
        "当前条目",
        "文字",
        "返回"
      ],
      "source": "web/Survival.Web/Components/TaskCorner.razor",
      "entry": "TaskCorner.razor"
    },
    {
      "key": "component:TipsOverlay",
      "category": "component",
      "id": "TipsOverlay",
      "name": "TipsOverlay",
      "asset": {
        "ref": "frame:menu"
      },
      "kind": "menu",
      "texture": "texture:iron",
      "glyph": "glyph:menu",
      "layers": [
        "背景",
        "遮罩",
        "按钮框",
        "状态",
        "动态文字"
      ],
      "source": "web/Survival.Web/Components/TipsOverlay.razor",
      "entry": "TipsOverlay.razor"
    },
    {
      "key": "component:TransferArrow",
      "category": "component",
      "id": "TransferArrow",
      "name": "TransferArrow",
      "asset": {
        "ref": "frame:card"
      },
      "kind": "card",
      "texture": "texture:iron",
      "glyph": "glyph:drag",
      "layers": [
        "底",
        "插画",
        "边框",
        "状态",
        "真实文字"
      ],
      "source": "web/Survival.Web/Components/TransferArrow.razor",
      "entry": "TransferArrow.razor"
    },
    {
      "key": "component:VitalIcon",
      "category": "component",
      "id": "VitalIcon",
      "name": "VitalIcon",
      "asset": {
        "ref": "frame:tooltip"
      },
      "kind": "feedback",
      "texture": "texture:shadow",
      "glyph": "glyph:warning",
      "layers": [
        "容器",
        "状态图形",
        "原因",
        "按钮"
      ],
      "source": "web/Survival.Web/Components/VitalIcon.razor",
      "entry": "VitalIcon.razor"
    }
  ]
};
