# Q120—Q127 资源、工具、名称与知识完整表

Q137补充：下方工具表仅30个明确锁闭点额外允许消防斧，沿原工具后基础分钟/产量1/成功磨1，再按破门×0.75与熟练装备共同受×0.70下限；不把所有撬棍行都算锁闭。Q136消防斧最大耐久正常抽40—52、当前等于该实例max。[完整消防斧合同](<../equipment/fire-axe-adjudications.json>)。


2026-11-02；总控按用户授权补齐的初值，原始ID与领取账稳定。完整执行边界见S05/A08本轮章。[机器数据](<resource-adjudications.json>)。

## 53条恢复及唯一来源

| 资源ID（旧别名） | 地点 | 名称 | 产出/知识 | 基础分钟 | 初始/上限/日补 | 揭示条件 | scope/计量 | 熟练/经验 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| m01_road_crate（m01/crate） | m01_road | 路旁旧货箱 | {"cloth":2} | 20 | 6/6/0 | 25% | world_stock/item_count | scavenging／沿原公式 |
| m01_road_barrier（m01/barrier） | m01_road | 路障金属堆 | {"scrap":2} | 20 | 8/8/0 | 50% | world_stock/item_count | scavenging／沿原公式 |
| m02_checkpoint_duty（m02/duty） | m02_checkpoint | 检查哨值勤补给箱 | {"canned_food":2,"clean_water":2} | 15 | 1/1/0 | 100% | campaignClaimId/batch_count | 无／0 |
| m02_checkpoint_bench（m02/bench） | m02_checkpoint | 哨站维修台遗料 | {"fastener":2} | 25 | 8/8/2 | 50% | world_stock/item_count | scavenging／沿原公式 |
| h02_square_water（h02/water） | h02_square | 营区净水配给处 | {"clean_water":2} | 10 | 16/16/12 | 实际到访 | world_stock/item_count | 无／0 |
| h02_square_ration（h02/ration） | h02_square | 营区罐头配给处 | {"canned_food":2} | 10 | 4/4/2 | 实际到访 | world_stock/item_count | 无／0 |
| h03_workshop_scrap（h03/scrap） | h03_workshop | 公共工坊旧件回收台 | {"parts":1} | 30 | 4/4/1 | 实际到访 | world_stock/item_count | 无／0 |
| h04_infirmary_dressing（h04/dressing） | h04_infirmary | 医务室纱布配给柜 | {"medical_gauze":1} | 15 | 4/4/1 | 实际到访 | world_stock/item_count | 无／0 |
| w01_farm_crop（w01/crop） | w01_farm | 农场块根畦 | {"raw_root":2} | 30 | 12/12/6 | 25% | world_stock/item_count | gathering／沿原公式 |
| w01_farm_barn（w01/barn） | w01_farm | 农场旧谷仓布料 | {"cloth":2} | 25 | 8/8/2 | 50% | world_stock/item_count | scavenging／沿原公式 |
| w02_forest_wood（w02/wood） | w02_forest | 林间倒木 | {"wood":2} | 30 | 20/20/10 | 25% | world_stock/item_count | gathering／沿原公式 |
| w02_forest_fiber（w02/fiber） | w02_forest | 林间纤维草丛 | {"fiber":2} | 20 | 16/16/8 | 50% | world_stock/item_count | gathering／沿原公式 |
| w03_quarry_stone（w03/stone） | w03_quarry | 坡脚碎石带 | {"stone":2} | 25 | 20/20/4 | 25% | world_stock/item_count | gathering／沿原公式 |
| w03_quarry_ore（w03/ore） | w03_quarry | 采石坡露矿 | {"iron_ore":2} | 30 | 12/12/2 | 50% | world_stock/item_count | gathering／沿原公式 |
| a01_yard_steel（a01/steel） | a01_yard | 货场废钢垛 | {"steel":2} | 30 | 12/12/2 | 25% | world_stock/item_count | scavenging／沿原公式 |
| a01_yard_fastener（a01/fastener） | a01_yard | 货场紧固件匣 | {"fastener":2} | 20 | 12/12/4 | 50% | world_stock/item_count | scavenging／沿原公式 |
| a02_pump_seal（a02/seal） | a02_pump | 泵站密封件架 | {"rubber":1} | 25 | 6/6/1 | 25% | world_stock/item_count | scavenging／沿原公式 |
| a02_pump_fuel（a02/fuel） | a02_pump | 泵站机油余存 | {"oil":1} | 30 | 6/6/1 | 50% | world_stock/item_count | scavenging／沿原公式 |
| a03_foundry_steel（a03/steel） | a03_foundry | 厂外钢料垛 | {"steel":2} | 30 | 16/16/4 | 25% | world_stock/item_count | scavenging／沿原公式 |
| a03_foundry_machine（a03/machine） | a03_foundry | 厂外机件堆 | {"precision_parts":1} | 40 | 6/6/1 | 50% | world_stock/item_count | scavenging／沿原公式 |
| b01_school_fabric（b01/fabric） | b01_school | 安置学校旧布架 | {"cloth":2} | 25 | 12/12/4 | 实际到访 | world_stock/item_count | 无／0 |
| b01_school_leather（b01/leather） | b01_school | 安置学校旧皮料 | {"leather":1} | 30 | 6/6/1 | 实际到访 | world_stock/item_count | 无／0 |
| b02_station_medical（b02/medical） | b02_station | 转运站纱布配给柜 | {"medical_gauze":2} | 30 | 8/8/2 | 实际到访 | world_stock/item_count | 无／0 |
| b02_station_electronic（b02/electronic） | b02_station | 转运站退役设备 | {"electronics":1} | 35 | 4/4/1 | 实际到访 | world_stock/item_count | 无／0 |
| b03_hospital_medical（b03/medical） | b03_hospital | 外院医用纱布箱 | {"medical_gauze":2} | 30 | 12/12/4 | 25% | world_stock/item_count | scavenging／沿原公式 |
| b03_hospital_electronic（b03/electronic） | b03_hospital | 外院旧设备间 | {"electronics":1} | 35 | 6/6/2 | 50% | world_stock/item_count | scavenging／沿原公式 |
| w02_forest_plank（w02/plank） | w02_forest | 林间旧木板堆 | {"plank":2} | 30 | 12/12/4 | 100% | world_stock/item_count | scavenging／沿原公式 |
| w02_forest_rope（w02/rope） | w02_forest | 林间旧绳卷 | {"rope":1} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| b01_school_thread（b01/thread） | b01_school | 安置学校缝线配给柜 | {"thread":2} | 25 | 4/4/1 | 实际到访 | world_stock/item_count | 无／0 |
| m01_road_plastic（m01/plastic） | m01_road | 公路塑料遗料 | {"plastic":2} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| a01_yard_wire（a01/wire） | a01_yard | 货场旧导线卷 | {"wire":2} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| a02_pump_adhesive（a02/adhesive） | a02_pump | 泵站黏合剂余存 | {"adhesive":1} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| a02_pump_charcoal（a02/charcoal） | a02_pump | 泵站旧炭袋 | {"charcoal":2} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| b02_station_battery（b02/battery） | b02_station | 转运站备用电池柜 | {"battery":1} | 25 | 4/4/1 | 实际到访 | world_stock/item_count | 无／0 |
| b02_station_disinfectant（b02/disinfectant） | b02_station | 转运站消毒液配给柜 | {"disinfectant":1} | 25 | 4/4/1 | 实际到访 | world_stock/item_count | 无／0 |
| h04_infirmary_saline（h04/saline） | h04_infirmary | 医务室盐水剂配给柜 | {"saline":1} | 25 | 4/4/1 | 实际到访 | world_stock/item_count | 无／0 |
| a03_foundry_weapon_parts（a03/weapon_parts） | a03_foundry | 厂外武器组件箱 | {"weapon_parts":1} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| b03_hospital_ballistic_fiber（b03/ballistic_fiber） | b03_hospital | 外院防护纤维存货 | {"ballistic_fiber":1} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| w01_farm_grain（w01/grain） | w01_farm | 农场谷物畦 | {"grain":2} | 30 | 12/12/4 | 100% | world_stock/item_count | gathering／沿原公式 |
| h02_square_beans（h02/beans） | h02_square | 营区豆罐头配给处 | {"canned_beans":1} | 10 | 4/4/2 | 实际到访 | world_stock/item_count | 无／0 |
| w01_farm_salt（w01/salt） | w01_farm | 农场食盐存货 | {"salt":1} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| w01_farm_cooking_oil（w01/cooking_oil） | w01_farm | 农场食用油存货 | {"cooking_oil":1} | 25 | 4/4/1 | 100% | world_stock/item_count | scavenging／沿原公式 |
| h01_gate_raw_water（h01/raw_water） | h01_gate | 营门蓄水渠 | {"dirty_water":4} | 10 | -1/-1/0 | 实际到访 | world_stock/item_count | 无／0 |
| w03_quarry_plate（w03/plate） | w03_quarry | 采石坡铁片回收堆 | {"iron_plate":2} | 25 | 8/8/2 | 100% | world_stock/item_count | scavenging／沿原公式 |
| h03_workshop_contract（h03/contract） | h03_workshop | 工坊首次支援包 | {"fastener":4,"precision_parts":2} | 0 | 1/1/0 | FA02_record_credit或可信旧QMAP02 | campaignClaimId/batch_count | 无／0 |
| firetruck_cache | m01_road | 消防车器材箱 | {"fire_axe":1} | 20 | 1/1/0 | 50% | campaignClaimId/batch_count | 无／0 |
| w03_quarry_blueprints（bp_wild_pack） | w03_quarry | 采石坡野外装备图册 | blueprint_barbed_spear,blueprint_hunter_bow,blueprint_scout_armor | 10 | 1/1/0 | 100% | campaignClaimId/knowledge_package | 无／0 |
| shield_cache | d03_03 | 器材室防暴盾箱 | {"riot_shield":1} | 20 | 1/1/0 | 100% | campaignClaimId/batch_count | 无／0 |
| officer_cache | d03_07 | 守卫战利品箱 | {"service_pistol":1} | 20 | 1/1/0 | 本代守卫胜利且主角存活 | campaignClaimId/batch_count | 无／0 |
| shotgun_cache | d03_08 | 封存仓霰弹枪箱 | {"shotgun":1} | 20 | 1/1/0 | 100% | campaignClaimId/batch_count | 无／0 |
| rifle_cache | d03_08 | 封存仓步枪箱 | {"rifle":1} | 20 | 1/1/0 | 100% | campaignClaimId/batch_count | 无／0 |
| armor_cache | d03_08 | 封存仓防护装备箱 | {"guard_armor":1} | 20 | 1/1/0 | 100% | campaignClaimId/batch_count | 无／0 |
| ammo_reserve_cache | d03_08 | 封存仓备用弹药箱 | {"ammo_9mm":24,"shell_12g":12,"ammo_rifle":15} | 20 | 1/1/0 | 100% | campaignClaimId/batch_count | 无／0 |

## 149条新增与恢复资源正式名称、合格工具

新工具行：仅列出的物品ID可用，倍率1、原工时减5，成功磨1；无工具行不磨损。搜集5仅另对实际合格工具的搜集行×0.90，不能加速唯一claim。

| resourceId | 地点 | 正式名称 | 合格工具itemId | 工具后基础分钟 | 产量倍率 | 成功磨损 | 可用工具运用 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| w11_orchard_a | w11_orchard | 果园块根地 | stone_knife | 20 | 1 | 1 | 否 |
| w11_orchard_b | w11_orchard | 果园旧布堆 | stone_knife | 25 | 1 | 1 | 是 |
| w12_canal_a | w12_canal | 渠岸纤维草丛 | stone_knife | 20 | 1 | 1 | 否 |
| w12_canal_b | w12_canal | 渠边旧密封件 | crowbar | 25 | 1 | 1 | 是 |
| w13_mill_a | w13_mill | 磨坊遗留谷物 | stone_knife | 20 | 1 | 1 | 否 |
| w13_mill_b | w13_mill | 磨坊旧木板 | crowbar | 25 | 1 | 1 | 是 |
| w14_cemeteryroad_a | w14_cemeteryroad | 荒径碎石带 | crowbar | 20 | 1 | 1 | 否 |
| w14_cemeteryroad_b | w14_cemeteryroad | 荒径弃衣堆 | stone_knife | 25 | 1 | 1 | 是 |
| w15_shrinewoods_a | w15_shrinewoods | 林下草药丛 | stone_knife | 20 | 1 | 1 | 否 |
| w15_shrinewoods_b | w15_shrinewoods | 祈祷林倒木 | hatchet | 25 | 1 | 1 | 否 |
| w16_service_a | w16_service | 服务站废金属堆 | crowbar | 20 | 1 | 1 | 是 |
| w16_service_b | w16_service | 服务站塑料遗料 | crowbar | 25 | 1 | 1 | 是 |
| w17_switchback_a | w17_switchback | 山路碎石带 | crowbar | 20 | 1 | 1 | 否 |
| w17_switchback_b | w17_switchback | 山路废导线 | crowbar | 25 | 1 | 1 | 是 |
| w18_tunnel_a | w18_tunnel | 洞外露矿 | crowbar | 20 | 1 | 1 | 否 |
| w18_tunnel_b | w18_tunnel | 隧道维护紧固件 | crowbar | 25 | 1 | 1 | 是 |
| w19_survey_a | w19_survey | 测绘设备旧件 | crowbar | 20 | 1 | 1 | 是 |
| w19_survey_b | w19_survey | 测绘营备用电池 | crowbar,fire_axe | 25 | 1 | 1 | 是 |
| w20_powercut_a | w20_powercut | 塔基旧导线 | crowbar | 20 | 1 | 1 | 是 |
| w20_powercut_b | w20_powercut | 高压走廊旧电器 | crowbar,fire_axe | 25 | 1 | 1 | 是 |
| w21_pine_a | w21_pine | 针叶林倒木 | hatchet | 20 | 1 | 1 | 否 |
| w21_pine_b | w21_pine | 针叶林纤维草丛 | stone_knife | 25 | 1 | 1 | 否 |
| w22_ravine_a | w22_ravine | 沟谷露矿 | crowbar | 20 | 1 | 1 | 否 |
| w22_ravine_b | w22_ravine | 沟谷碎石带 | crowbar | 25 | 1 | 1 | 否 |
| w23_hunting_a | w23_hunting | 旧营皮料 | stone_knife | 20 | 1 | 1 | 否 |
| w23_hunting_b | w23_hunting | 旧营绳卷 | stone_knife | 25 | 1 | 1 | 是 |
| w24_cave_a | w24_cave | 洞口矿料堆 | crowbar | 20 | 1 | 1 | 否 |
| w24_cave_b | w24_cave | 洞口旧炭袋 | crowbar | 25 | 1 | 1 | 是 |
| w25_wetland_a | w25_wetland | 湿地草药丛 | stone_knife | 20 | 1 | 1 | 否 |
| w25_wetland_b | w25_wetland | 湿地纤维草丛 | stone_knife | 25 | 1 | 1 | 否 |
| w26_island_a | w26_island | 陆洲块根地 | stone_knife | 20 | 1 | 1 | 否 |
| w26_island_b | w26_island | 陆洲盐晶 | crowbar | 25 | 1 | 1 | 否 |
| w27_relay_a | w27_relay | 中继设备遗件 | crowbar,fire_axe | 20 | 1 | 1 | 是 |
| w27_relay_b | w27_relay | 中继营精密机件 | crowbar,fire_axe | 25 | 1 | 1 | 是 |
| w28_highland_a | w28_highland | 高地碎石带 | crowbar | 20 | 1 | 1 | 否 |
| w28_highland_b | w28_highland | 高地草药丛 | stone_knife | 25 | 1 | 1 | 否 |
| w29_riverbend_a | w29_riverbend | 河弯纤维草丛 | stone_knife | 20 | 1 | 1 | 否 |
| w29_riverbend_b | w29_riverbend | 河弯黏土层 | crowbar | 25 | 1 | 1 | 否 |
| w30_truckstop_a | w30_truckstop | 休息区机油余存 | crowbar | 20 | 1 | 1 | 是 |
| w30_truckstop_b | w30_truckstop | 休息区旧胶料 | crowbar | 25 | 1 | 1 | 是 |
| w31_suburb_a | w31_suburb | 棚户旧布堆 | stone_knife | 20 | 1 | 1 | 是 |
| w31_suburb_b | w31_suburb | 棚户医用纱布余存 | stone_knife | 25 | 1 | 1 | 是 |
| w32_railcut_a | w32_railcut | 铁路废钢料 | crowbar | 20 | 1 | 1 | 是 |
| w32_railcut_b | w32_railcut | 铁路维护紧固件 | crowbar | 25 | 1 | 1 | 是 |
| w33_waterworks_a | w33_waterworks | 水厂黏合剂余存 | crowbar,fire_axe | 20 | 1 | 1 | 是 |
| w33_waterworks_b | w33_waterworks | 水厂消毒液余存 | crowbar,fire_axe | 25 | 1 | 1 | 是 |
| w34_depot_a | w34_depot | 应急仓豆罐头存货 | crowbar,fire_axe | 20 | 1 | 1 | 是 |
| w34_depot_b | w34_depot | 应急仓盐水剂存货 | crowbar,fire_axe | 25 | 1 | 1 | 是 |
| w12_canal_water | w12_canal | 灌溉渠岸取水口 | 无 | 原值 | 1 | 0 | 否 |
| w25_wetland_water | w25_wetland | 黑水湿地取水口 | 无 | 原值 | 1 | 0 | 否 |
| w29_riverbend_water | w29_riverbend | 河道弯口取水口 | 无 | 原值 | 1 | 0 | 否 |
| w33_waterworks_water | w33_waterworks | 荒废水厂取水口 | 无 | 原值 | 1 | 0 | 否 |
| w08_reservoir_water | w08_reservoir | 旧水库取水口 | 无 | 原值 | 1 | 0 | 否 |
| w16_service_cistern | w16_service | 山间服务站蓄水池 | 无 | 原值 | 1 | 0 | 否 |
| w19_survey_cistern | w19_survey | 测绘营址蓄水池 | 无 | 原值 | 1 | 0 | 否 |
| w23_hunting_cistern | w23_hunting | 猎人旧营蓄水池 | 无 | 原值 | 1 | 0 | 否 |
| w27_relay_cistern | w27_relay | 废中继营蓄水池 | 无 | 原值 | 1 | 0 | 否 |
| w30_truckstop_cistern | w30_truckstop | 货车休息区蓄水池 | 无 | 原值 | 1 | 0 | 否 |
| w13_mill_bottles | w13_mill | 旧磨坊瓶装净水 | 无 | 原值 | 1 | 0 | 否 |
| w31_suburb_bottles | w31_suburb | 城郊棚户瓶装净水 | 无 | 原值 | 1 | 0 | 否 |
| w34_depot_bottles | w34_depot | 应急仓外瓶装净水 | 无 | 原值 | 1 | 0 | 否 |
| d01_04_cache0 | d01_04 | 藏书密室·布料箱 | stone_knife | 15 | 1 | 1 | 是 |
| d01_06_cache1 | d01_06 | 骨龛回道·缝线箱 | stone_knife | 15 | 1 | 1 | 是 |
| d01_04_cache2 | d01_04 | 藏书密室·皮革箱 | stone_knife | 15 | 1 | 1 | 是 |
| d01_08_unique | d01_08 | 圣所旧路档册 | 无 | 原值 | 1 | 0 | 否 |
| d02_04_cache0 | d02_04 | 控制室·医用纱布箱 | stone_knife | 15 | 1 | 1 | 是 |
| d02_06_cache1 | d02_06 | 检修竖井·实验室试剂箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d02_04_cache2 | d02_04 | 控制室·电子组件箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d02_08_unique | d02_08 | 封存区安全规程 | 无 | 原值 | 1 | 0 | 否 |
| d03_04_cache0 | d03_04 | 值勤室·武器组件箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d03_06_cache1 | d03_06 | 配电房·钢材箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d03_04_cache2 | d03_04 | 值勤室·紧固件箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d03_08_unique | d03_08 | 警备仓备用电源 | 无 | 原值 | 1 | 0 | 否 |
| d04_03_cache0 | d04_03 | 蓄电室·导线箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d04_05_cache1 | d04_05 | 屋顶梯井·电池箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d04_03_cache2 | d04_03 | 蓄电室·电子组件箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d04_07_unique | d04_07 | 通信站联络日志 | 无 | 原值 | 1 | 0 | 否 |
| d05_04_cache0 | d05_04 | 滤料库·橡胶箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d05_07_cache1 | d05_07 | 阀控室·消毒液箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d05_04_cache2 | d05_04 | 滤料库·黏合剂箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d05_07_unique | d05_07 | 水厂阀控记录 | 无 | 原值 | 1 | 0 | 否 |
| d06_03_cache0 | d06_03 | 浅矿脉·铁矿料箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d06_06_cache1 | d06_06 | 矿工营室·铁片箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d06_03_cache2 | d06_03 | 浅矿脉·木炭箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d06_08_unique | d06_08 | 矿道作业手册 | 无 | 原值 | 1 | 0 | 否 |
| d07_04_cache0 | d07_04 | 服装层·豆罐头箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d07_07_cache1 | d07_07 | 屋顶避难点·布料箱 | stone_knife | 15 | 1 | 1 | 是 |
| d07_04_cache2 | d07_04 | 服装层·塑料箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d07_07_unique | d07_07 | 商场避难物资簿 | 无 | 原值 | 1 | 0 | 否 |
| d08_03_cache0 | d08_03 | 干货库·盐水剂箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d08_06_cache1 | d08_06 | 备用电房·医用纱布箱 | stone_knife | 15 | 1 | 1 | 是 |
| d08_03_cache2 | d08_03 | 干货库·肉干箱 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| d08_08_unique | d08_08 | 冷链调度档案 | 无 | 原值 | 1 | 0 | 否 |
| h03_workshop_supply_disinfectant | h03_workshop | 公共工坊消毒液配给柜 | 无 | 原值 | 1 | 0 | 否 |
| w13_mill_supply_salt | w13_mill | 磨坊食盐存货 | crowbar | 20 | 1 | 1 | 否 |
| w15_shrinewoods_supply_berry | w15_shrinewoods | 祈祷林野果丛 | stone_knife | 20 | 1 | 1 | 否 |
| m01_road_crate | m01_road | 路旁旧货箱 | stone_knife,fire_axe | 15 | 1 | 1 | 是 |
| m01_road_barrier | m01_road | 路障金属堆 | crowbar | 15 | 1 | 1 | 是 |
| m02_checkpoint_duty | m02_checkpoint | 检查哨值勤补给箱 | 无 | 原值 | 1 | 0 | 否 |
| m02_checkpoint_bench | m02_checkpoint | 哨站维修台遗料 | crowbar | 20 | 1 | 1 | 是 |
| h02_square_water | h02_square | 营区净水配给处 | 无 | 原值 | 1 | 0 | 否 |
| h02_square_ration | h02_square | 营区罐头配给处 | 无 | 原值 | 1 | 0 | 否 |
| h03_workshop_scrap | h03_workshop | 公共工坊旧件回收台 | 无 | 原值 | 1 | 0 | 否 |
| h04_infirmary_dressing | h04_infirmary | 医务室纱布配给柜 | 无 | 原值 | 1 | 0 | 否 |
| w01_farm_crop | w01_farm | 农场块根畦 | stone_knife | 25 | 1 | 1 | 否 |
| w01_farm_barn | w01_farm | 农场旧谷仓布料 | stone_knife,fire_axe | 20 | 1 | 1 | 是 |
| w02_forest_wood | w02_forest | 林间倒木 | hatchet | 25 | 1 | 1 | 否 |
| w02_forest_fiber | w02_forest | 林间纤维草丛 | stone_knife | 15 | 1 | 1 | 否 |
| w03_quarry_stone | w03_quarry | 坡脚碎石带 | crowbar | 20 | 1 | 1 | 否 |
| w03_quarry_ore | w03_quarry | 采石坡露矿 | crowbar | 25 | 1 | 1 | 否 |
| a01_yard_steel | a01_yard | 货场废钢垛 | crowbar | 25 | 1 | 1 | 是 |
| a01_yard_fastener | a01_yard | 货场紧固件匣 | crowbar,fire_axe | 15 | 1 | 1 | 是 |
| a02_pump_seal | a02_pump | 泵站密封件架 | crowbar | 20 | 1 | 1 | 是 |
| a02_pump_fuel | a02_pump | 泵站机油余存 | crowbar | 25 | 1 | 1 | 是 |
| a03_foundry_steel | a03_foundry | 厂外钢料垛 | crowbar | 25 | 1 | 1 | 是 |
| a03_foundry_machine | a03_foundry | 厂外机件堆 | crowbar | 35 | 1 | 1 | 是 |
| b01_school_fabric | b01_school | 安置学校旧布架 | 无 | 原值 | 1 | 0 | 否 |
| b01_school_leather | b01_school | 安置学校旧皮料 | 无 | 原值 | 1 | 0 | 否 |
| b02_station_medical | b02_station | 转运站纱布配给柜 | 无 | 原值 | 1 | 0 | 否 |
| b02_station_electronic | b02_station | 转运站退役设备 | 无 | 原值 | 1 | 0 | 否 |
| b03_hospital_medical | b03_hospital | 外院医用纱布箱 | stone_knife | 25 | 1 | 1 | 是 |
| b03_hospital_electronic | b03_hospital | 外院旧设备间 | crowbar | 30 | 1 | 1 | 是 |
| w02_forest_plank | w02_forest | 林间旧木板堆 | crowbar | 25 | 1 | 1 | 是 |
| w02_forest_rope | w02_forest | 林间旧绳卷 | stone_knife | 20 | 1 | 1 | 是 |
| b01_school_thread | b01_school | 安置学校缝线配给柜 | 无 | 原值 | 1 | 0 | 否 |
| m01_road_plastic | m01_road | 公路塑料遗料 | crowbar | 20 | 1 | 1 | 是 |
| a01_yard_wire | a01_yard | 货场旧导线卷 | crowbar | 20 | 1 | 1 | 是 |
| a02_pump_adhesive | a02_pump | 泵站黏合剂余存 | crowbar | 20 | 1 | 1 | 是 |
| a02_pump_charcoal | a02_pump | 泵站旧炭袋 | crowbar | 20 | 1 | 1 | 是 |
| b02_station_battery | b02_station | 转运站备用电池柜 | 无 | 原值 | 1 | 0 | 否 |
| b02_station_disinfectant | b02_station | 转运站消毒液配给柜 | 无 | 原值 | 1 | 0 | 否 |
| h04_infirmary_saline | h04_infirmary | 医务室盐水剂配给柜 | 无 | 原值 | 1 | 0 | 否 |
| a03_foundry_weapon_parts | a03_foundry | 厂外武器组件箱 | crowbar,fire_axe | 20 | 1 | 1 | 是 |
| b03_hospital_ballistic_fiber | b03_hospital | 外院防护纤维存货 | stone_knife | 20 | 1 | 1 | 是 |
| w01_farm_grain | w01_farm | 农场谷物畦 | stone_knife | 25 | 1 | 1 | 否 |
| h02_square_beans | h02_square | 营区豆罐头配给处 | 无 | 原值 | 1 | 0 | 否 |
| w01_farm_salt | w01_farm | 农场食盐存货 | crowbar | 20 | 1 | 1 | 是 |
| w01_farm_cooking_oil | w01_farm | 农场食用油存货 | crowbar | 20 | 1 | 1 | 是 |
| h01_gate_raw_water | h01_gate | 营门蓄水渠 | 无 | 原值 | 1 | 0 | 否 |
| w03_quarry_plate | w03_quarry | 采石坡铁片回收堆 | crowbar | 20 | 1 | 1 | 是 |
| h03_workshop_contract | h03_workshop | 工坊首次支援包 | 无 | 原值 | 1 | 0 | 否 |
| firetruck_cache | m01_road | 消防车器材箱 | 无 | 原值 | 1 | 0 | 否 |
| w03_quarry_blueprints | w03_quarry | 采石坡野外装备图册 | 无 | 原值 | 1 | 0 | 否 |
| shield_cache | d03_03 | 器材室防暴盾箱 | 无 | 原值 | 1 | 0 | 否 |
| officer_cache | d03_07 | 守卫战利品箱 | 无 | 原值 | 1 | 0 | 否 |
| shotgun_cache | d03_08 | 封存仓霰弹枪箱 | 无 | 原值 | 1 | 0 | 否 |
| rifle_cache | d03_08 | 封存仓步枪箱 | 无 | 原值 | 1 | 0 | 否 |
| armor_cache | d03_08 | 封存仓防护装备箱 | 无 | 原值 | 1 | 0 | 否 |
| ammo_reserve_cache | d03_08 | 封存仓备用弹药箱 | 无 | 原值 | 1 | 0 | 否 |

## 原20条工具规则保留核对

| resourceId | 工具itemId | 产量倍率 | 基础分钟覆盖 | 成功磨损 |
| --- | --- | --- | --- | --- |
| roots | 无 | 1 | 原值 | 0 |
| field_fiber | stone_knife | 1 | 10 | 1 |
| water | 无 | 1 | 原值 | 0 |
| herbs | 无 | 1 | 原值 | 0 |
| wood | hatchet | 2 | 原值 | 1 |
| stones | 无 | 1 | 原值 | 0 |
| berries | 无 | 1 | 原值 | 0 |
| woods_fiber | stone_knife | 1 | 10 | 1 |
| supply_box | 无 | 1 | 原值 | 0 |
| misc_box | 无 | 1 | 原值 | 0 |
| pot_box | 无 | 1 | 原值 | 0 |
| tool_box | 无 | 1 | 原值 | 0 |
| med_cabinet | 无 | 1 | 原值 | 0 |
| cloth_bag | 无 | 1 | 原值 | 0 |
| metal_heap | 无 | 1 | 原值 | 0 |
| parts_box | 无 | 1 | 原值 | 0 |
| hardware_weapon_cache | 无 | 1 | 原值 | 0 |
| village_weapon_locker | 无 | 1 | 原值 | 0 |
| clinic_medical_supply | 无 | 1 | 原值 | 0 |
| village_cold_box | 无 | 1 | 原值 | 0 |

## 核心记录同批蓝图28项

| 核心resourceId | 节点 | 同事务授予知识ID | 额外分钟/XP |
| --- | --- | --- | --- |
| d03_08_unique | d03_08 | blueprint_guard_sword, blueprint_pike, blueprint_shock_baton, blueprint_repeater_crossbow, blueprint_rescue_shield, blueprint_field_radio, blueprint_focus_lens, blueprint_resolve_medal | 0/0 |
| d01_08_unique | d01_08 | blueprint_breach_axe, blueprint_rescue_axe, blueprint_steel_mace, blueprint_heavy_armor, blueprint_craft_coat, blueprint_reinforced_trousers, blueprint_repair_manual | 0/0 |
| d04_07_unique | d04_07 | blueprint_command_terminal, blueprint_balanced_armor, blueprint_tactics_watch, blueprint_expedition_coat, blueprint_expedition_trousers, blueprint_ration_pouch | 0/0 |
| d02_08_unique | d02_08 | blueprint_rescue_medkit, blueprint_triage_kit, blueprint_medic_armor, blueprint_aid_badge, blueprint_rest_coat, blueprint_recovery_trousers, blueprint_medical_notes | 0/0 |

## 13分区与8核心记录正式名

| 分区ID | 中文名 |
| --- | --- |
| starter | 初醒荒野 |
| hub | 幸存者营区 |
| city | 旧城废墟 |
| church | 教会辖区 |
| academy | 灰塔院区 |
| wild | 近郊荒野 |
| deep_wild | 深野腹地 |
| church_approach | 朝圣沿途 |
| academy_approach | 灰塔山路 |
| city_approach | 旧城外缘 |
| works | 铁路工务区 |
| aid | 救援安置区 |
| utility | 公共设施区 |

| 记录ID | 正式名 |
| --- | --- |
| church_route_record | 圣所旧路档册 |
| academy_safety_record | 封存区安全规程 |
| communication_power | 警备仓备用电源 |
| chapter_contact | 通信站联络日志 |
| waterworks_record | 水厂阀控记录 |
| mining_blueprint_pack | 矿道作业手册 |
| civilian_cache_knowledge | 商场避难物资簿 |
| coldchain_knowledge | 冷链调度档案 |
