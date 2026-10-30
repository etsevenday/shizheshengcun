using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Survival.Core.Config
{
    public sealed class ConfigException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public ConfigException(IReadOnlyList<string> errors)
            : base("配置校验失败：" + string.Join("；", errors))
        {
            Errors = errors;
        }
    }

    public static class ConfigLoader
    {
        public static GameConfig FromJson(string json, bool validate = true)
        {
            var cfg = JsonConvert.DeserializeObject<GameConfig>(json)
                      ?? throw new ConfigException(new[] { "配置为空" });
            if (validate)
            {
                var errors = Validate(cfg);
                if (errors.Count > 0) throw new ConfigException(errors);
            }
            return cfg;
        }

        public static List<string> Validate(GameConfig c)
        {
            var e = new List<string>();
            void Dup<T>(IEnumerable<T> list, Func<T, string> key, string what)
            {
                foreach (var g in list.GroupBy(key).Where(g => g.Count() > 1))
                    e.Add($"{what} ID 重复：{g.Key}");
            }

            Dup(c.Items, i => i.Id, "物品");
            Dup(c.Locations, l => l.Id, "地点");
            Dup(c.Edges, x => x.Id, "连接");
            Dup(c.Resources, r => r.NodeId, "资源点");
            Dup(c.Recipes, r => r.Id, "配方");

            var items = new HashSet<string>(c.Items.Select(i => i.Id));
            var locs = new HashSet<string>(c.Locations.Select(l => l.Id));
            var tick = c.Time.TickMinutes;
            var milestoneSet = new HashSet<int> { 25, 50, 75, 100 };

            foreach (var it in c.Items)
            {
                if (it.WeightGrams <= 0) e.Add($"物品 {it.Id} 重量必须为正");
                if (it.StackMax < 1) e.Add($"物品 {it.Id} 堆叠上限必须≥1");
                if (it.ShelfLifeMinutes.HasValue && it.ShelfLifeMinutes.Value % tick != 0)
                    e.Add($"物品 {it.Id} 保质期须为{tick}的倍数");
                if (Array.IndexOf(ItemConfig.Kinds, it.Kind) < 0) e.Add($"物品 {it.Id} 的类别 {it.Kind} 未知");
                if (it.Edible && it.Use == null) e.Add($"物品 {it.Id} 可食用却没有使用效果");
            }

            foreach (var ed in c.Edges)
            {
                if (!locs.Contains(ed.A) || !locs.Contains(ed.B)) e.Add($"连接 {ed.Id} 引用了不存在的地点");
                if (ed.BaseMinutes <= 0 || ed.BaseMinutes % tick != 0) e.Add($"连接 {ed.Id} 时长须为{tick}的正倍数");
                if (!ed.InitiallyUnlocked && ed.UnlockAny.Count == 0) e.Add($"连接 {ed.Id} 既非开局解锁也无解锁条件");
                foreach (var u in ed.UnlockAny)
                {
                    if (!locs.Contains(u.Location)) e.Add($"连接 {ed.Id} 解锁条件引用了不存在的地点 {u.Location}");
                    if (!milestoneSet.Contains(u.Progress)) e.Add($"连接 {ed.Id} 解锁阈值 {u.Progress} 不在 25/50/75/100 中");
                }
            }

            foreach (var r in c.Resources)
            {
                if (!locs.Contains(r.LocationId)) e.Add($"资源点 {r.NodeId} 地点不存在");
                if (r.BaseMinutes <= 0 || r.BaseMinutes % tick != 0) e.Add($"资源点 {r.NodeId} 时长须为{tick}的正倍数");
                if (r.RevealAt.HasValue && !milestoneSet.Contains(r.RevealAt.Value)) e.Add($"资源点 {r.NodeId} revealAt 不在 25/50/75/100 中");
                if (!r.RevealAt.HasValue && !r.RevealOnVisit) e.Add($"资源点 {r.NodeId} 没有发现条件");
                foreach (var o in r.Outputs.Keys)
                    if (!items.Contains(o)) e.Add($"资源点 {r.NodeId} 产出物品 {o} 不存在");
                if (r.IsUnits && r.Outputs.Count != 1) e.Add($"资源点 {r.NodeId} 按件数计库存时只能有一种产物");
                if (!r.IsUnlimited && !r.Cap.HasValue) e.Add($"资源点 {r.NodeId} 缺少上限");
                if (r.ToolBonus != null && !c.Items.Any(i => i.ToolTag == r.ToolBonus.ToolTag))
                    e.Add($"资源点 {r.NodeId} 工具标签 {r.ToolBonus.ToolTag} 无对应工具");
            }

            foreach (var rc in c.Recipes)
            {
                if (rc.BaseMinutes <= 0 || rc.BaseMinutes % tick != 0) e.Add($"配方 {rc.Id} 时长须为{tick}的正倍数");
                foreach (var k in rc.Inputs.Keys.Concat(rc.Outputs.Keys))
                    if (!items.Contains(k)) e.Add($"配方 {rc.Id} 引用了不存在的物品 {k}");
                foreach (var cat in rc.Catalysts)
                    if (!c.Items.Any(i => i.ToolTag == cat)) e.Add($"配方 {rc.Id} 催化工具 {cat} 不存在");
            }

            foreach (var k in c.StartingCarried.Keys.Concat(c.StartingCampGround.Keys))
                if (!items.Contains(k)) e.Add($"开局物品 {k} 不存在");

            foreach (var a in c.Actions.RateAliases.Values.Distinct())
                if (!c.HourlyRates.ContainsKey(a)) e.Add($"速率别名指向不存在的类别 {a}");
            foreach (var k in new[] { "sleep_sheltered", "sleep_exposed" })
                if (!c.HourlyRates.ContainsKey(k)) e.Add($"缺少速率类别 {k}");

            foreach (var k in new[] { "satiety", "hydration", "wakefulness", "energy", "morale", "health" })
                if (!c.Thresholds.ContainsKey(k)) e.Add($"缺少阈值 {k}");

            foreach (var m in c.Actions.WaitMinutes.Concat(c.Actions.RestMinutes).Concat(c.Actions.SleepMinutes))
                if (m <= 0 || m % tick != 0) e.Add($"行动时长 {m} 须为{tick}的正倍数");

            if (c.Locations.Count(l => l.InitiallyVisited) != 1) e.Add("必须恰好有一个开局所在地点");
            if (c.Actions.ClearMinutes <= 0 || c.Actions.ClearMinutes % tick != 0) e.Add($"清理时长须为{tick}的正倍数");

            // 清理与共享掉落表（地图清理案 5.1、探索收获案 7）
            foreach (var l in c.Locations)
            {
                if (string.IsNullOrEmpty(l.LootTableId)) e.Add($"地点 {l.Id} 缺少 lootTableId");
                else if (!c.LootTables.ContainsKey(l.LootTableId)) e.Add($"地点 {l.Id} 的掉落表 {l.LootTableId} 不存在");
                var cl = l.Clear;
                if (cl.Cap < 1) e.Add($"地点 {l.Id} 清理次数上限须≥1");
                if (cl.RefillIntervalMinutes <= 0 || cl.RefillIntervalMinutes % tick != 0) e.Add($"地点 {l.Id} 清理恢复间隔须为{tick}的正倍数");
                if (cl.InitialCharges < 0 || cl.InitialCharges > cl.Cap) e.Add($"地点 {l.Id} 初始清理次数须在0到上限之间");
            }
            foreach (var kv in c.LootTables)
            {
                var t = kv.Value;
                if (string.IsNullOrEmpty(t.Version)) e.Add($"掉落表 {kv.Key} 缺少版本");
                if (t.Entries.Count == 0) e.Add($"掉落表 {kv.Key} 没有条目");
                foreach (var en in t.Entries)
                {
                    if (en.Weight < 0) e.Add($"掉落表 {kv.Key} 权重不能为负");
                    if (en.ItemId == null)
                    {
                        if (en.Count != 0) e.Add($"掉落表 {kv.Key} 的空结果不能带数量");
                    }
                    else
                    {
                        if (!items.Contains(en.ItemId)) e.Add($"掉落表 {kv.Key} 引用了不存在的物品 {en.ItemId}");
                        if (en.Count != 1) e.Add($"掉落表 {kv.Key} 物品 {en.ItemId} 数量须为1");
                    }
                }
                if (t.Entries.Sum(x => x.Weight) != 100) e.Add($"掉落表 {kv.Key} 权重合计须为100");
            }

            // 容器（拖拽与容器案 6.1）
            foreach (var kv in c.Containers)
            {
                var ct = kv.Value;
                if (ct.UnitMl <= 0 || ct.CapacityMl <= 0 || ct.CapacityMl % ct.UnitMl != 0) e.Add($"容器 {kv.Key} 容量须为每份体积的整数倍");
                foreach (var a in ct.Accepts)
                {
                    var it = c.Items.FirstOrDefault(i => i.Id == a);
                    if (it == null) e.Add($"容器 {kv.Key} 可装物品 {a} 不存在");
                    else if (it.UnitVolumeMl != ct.UnitMl) e.Add($"容器 {kv.Key} 可装物品 {a} 的每份体积须为{ct.UnitMl}毫升");
                }
            }
            foreach (var it in c.Items.Where(i => i.ContainerId != null))
                if (!c.Containers.ContainsKey(it.ContainerId!)) e.Add($"物品 {it.Id} 的容器定义 {it.ContainerId} 不存在");
            foreach (var kv in c.Containers)
            {
                if (kv.Value.FoodSlots < 0) e.Add($"容器 {kv.Key} 食材位不能为负");
                foreach (var f in kv.Value.StoredFoodIds)
                {
                    if (!items.Contains(f)) e.Add($"容器 {kv.Key} 可存放食材 {f} 不存在");
                    else if (kv.Value.Accepts.Contains(f)) e.Add($"容器 {kv.Key} 食材 {f} 不能同时是液体");
                }
                foreach (var f in kv.Value.StoreOnlyFoodIds)
                    if (!kv.Value.StoredFoodIds.Contains(f)) e.Add($"容器 {kv.Key} 只存放食材 {f} 须在可存放白名单内");
            }

            // 火堆加工位与设施进程（设施与烹饪案第 2、5 节）
            var fire = c.Fire;
            if (fire.ProcessingSlots < 1) e.Add("火堆加工位须≥1");
            if (fire.MaxPots < 0 || fire.MaxPots > fire.ProcessingSlots) e.Add("火堆锅数上限须在0到加工位数之间");
            foreach (var f in fire.DirectFoodInputIds)
                if (!items.Contains(f)) e.Add($"火堆直烤食材 {f} 不存在");
            Dup(c.Processes, p => p.Id, "设施进程");
            var potCt = c.Items.Where(i => i.ContainerId != null).Select(i => c.Containers.TryGetValue(i.ContainerId!, out var x) ? x : null)
                .FirstOrDefault(x => x != null);
            foreach (var p in c.Processes)
            {
                if (!items.Contains(p.InputId)) e.Add($"设施进程 {p.Id} 的输入物品 {p.InputId} 不存在");
                if (!items.Contains(p.OutputId)) e.Add($"设施进程 {p.Id} 的输出物品 {p.OutputId} 不存在");
                if (p.HeatMinutes <= 0 || p.HeatMinutes % tick != 0) e.Add($"设施进程 {p.Id} 受热分钟须为{tick}的正倍数");
                if (p.Target != ProcessTargets.PotLiquidBatch && p.Target != ProcessTargets.SingleFood) e.Add($"设施进程 {p.Id} 目标类型未知");
                if (p.RequiredOwner != ProcessOwners.FireSlot && p.RequiredOwner != ProcessOwners.PotInFire) e.Add($"设施进程 {p.Id} 位置要求未知");
                if (p.WaterCostMl < 0) e.Add($"设施进程 {p.Id} 耗水不能为负");
                if (p.IsPotBatch)
                {
                    if (potCt == null || !potCt.Accepts.Contains(p.InputId) || !potCt.Accepts.Contains(p.OutputId))
                        e.Add($"设施进程 {p.Id} 的锅内输入输出须是容器可装物品");
                }
                else if (p.InPot)
                {
                    if (potCt == null || !potCt.StoredFoodIds.Contains(p.InputId) || !potCt.StoredFoodIds.Contains(p.OutputId))
                        e.Add($"设施进程 {p.Id} 的食材须在锅白名单内");
                    if (p.RequiredLiquid != null && (potCt == null || !potCt.Accepts.Contains(p.RequiredLiquid)))
                        e.Add($"设施进程 {p.Id} 所需液体须是容器可装物品");
                    if (p.WaterCostMl > 0 && potCt != null && p.WaterCostMl % potCt.UnitMl != 0)
                        e.Add($"设施进程 {p.Id} 耗水须为每份体积的整数倍");
                }
                else if (!fire.DirectFoodInputIds.Contains(p.InputId))
                    e.Add($"设施进程 {p.Id} 的直烤食材须在火堆直烤白名单内");
            }

            ValidateCombat(c, e, items, locs);
            return e;
        }

        /// <summary>公式形状（数字换成 #）。策划改了公式结构时这里报错，提醒同步改代码。</summary>
        public static readonly IReadOnlyDictionary<string, string> FormulaShapes = new Dictionary<string, string>
        {
            ["skillGrowth.damageFactor"] = "#+#*L",
            ["skillGrowth.accuracyRating"] = "weaponBase+#*L",
            ["skillGrowth.energyCost"] = "baseEnergy/(#+#*L)",
            ["skillGrowth.medicalHeal"] = "#*(#+#*L)",
            ["crafting.workFactor"] = "#/(#+#*L)",
            ["combatMath.critRating"] = "baseCritRating+#*L",
            ["combatMath.critProbability"] = "critRating/(critRating+#)",
            ["combatMath.initiative"] = "max(#,base+gear+status-[E<#]-[S<#])",
            ["combatMath.guardAppliedAfterArmor"] = "damage*#",
            ["skills.nextLevelXpFormula"] = "#*(L+#)",
            ["xp.craft"] = "clamp(ceil(originalBaseMinutes/#),#,#)",
            ["panic.initial"] = "(#-morale)/#",
            ["panic.directDamageGain"] = "actualDamage*#",
            ["panic.postBattleMoraleLoss"] = "min(#,panic/#)",
            ["retreat.chance"] = "sigmoid((playerEffectiveSpeed-maxEnemyEffectiveSpeed)/#+#)*panicFactor*lowEnergyFactor",
            ["infection.exposureProbability"] = "#+#*(#-immunity/#)",
            ["infection.bandage.loadReduction"] = "#*(#+#*medicineLevel)",
        };

        private static void Shape(List<string> e, string key, string? formula)
        {
            if (Formula.Shape(formula) != FormulaShapes[key]) e.Add($"公式 {key} 的结构变了（{formula}），需要同步修改内核");
        }

        /// <summary>人物与战斗 v0.6 各段的引用与范围校验（Newtonsoft 会静默丢弃未映射字段，这里兜底检查关键段非空）。</summary>
        private static void ValidateCombat(GameConfig c, List<string> e, HashSet<string> items, HashSet<string> locs)
        {
            var tick = c.Time.TickMinutes;
            if (!c.CombatEnabled) return;
            if (c.StateScale.HealthMax <= 0 || c.StateScale.EnergyMax <= 0) e.Add("健康与体力上限须为正");
            if (c.InitialStats.Health > c.StateScale.HealthMax || c.InitialStats.Energy > c.StateScale.EnergyMax) e.Add("初始健康/体力不能超过上限");
            if (c.Weapons.Count == 0) e.Add("战斗已启用但缺少 weapons");
            if (c.Skills.Count == 0) e.Add("战斗已启用但缺少 skills");
            if (c.Enemies.Count == 0) e.Add("战斗已启用但缺少 enemies");
            if (c.CombatActions.Count == 0) e.Add("战斗已启用但缺少 combatActions");
            var skills = new HashSet<string>(c.Skills.Select(x => x.Id));
            var enemies = c.Enemies.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
            Dup(c.Skills, x => x.Id, "技能", e);
            // A06 §1/§4、验收 SK01/RG01：每门恰好 1/3/5/7/10 五个专属节点，等级上限 10
            if (c.SkillNodes.Count > 0)
            {
                Dup(c.SkillNodes, x => x.Id, "技能节点", e);
                foreach (var n in c.SkillNodes.Where(n => !skills.Contains(n.SkillId))) e.Add($"技能节点 {n.Id} 的门类 {n.SkillId} 不存在");
                foreach (var sk in c.Skills)
                {
                    var lv = c.SkillNodes.Where(n => n.SkillId == sk.Id).Select(n => n.Level).OrderBy(x => x).ToList();
                    if (!lv.SequenceEqual(new[] { 1, 3, 5, 7, 10 })) e.Add($"技能 {sk.Id} 须恰好有 1/3/5/7/10 级五个节点（现有 {string.Join("/", lv)}）");
                }
                foreach (var n in c.SkillNodes.Where(n => n.IsSurvival && n.Kind != "survival")) e.Add($"技能节点 {n.Id} 类型无效");
            }
            foreach (var sk in c.Skills.Where(x => x.LevelCap is int cap && cap < x.InitialLevel)) e.Add($"技能 {sk.Id} 的初始等级超过上限");
            Dup(c.Weapons, x => x.Id, "武器", e);
            Dup(c.Enemies, x => x.Id, "敌人", e);
            Dup(c.CombatActions, x => x.Id, "战斗动作", e);

            foreach (var it in c.Items.Where(i => i.DurabilityMaxRange != null))
            {
                var r = it.DurabilityMaxRange!;
                if (r.Length != 2 || r[0] <= 0 || r[1] < r[0]) e.Add($"物品 {it.Id} 最大耐久区间无效");
            }
            foreach (var w in c.Weapons)
            {
                if (!w.IsFists && !items.Contains(w.Id)) e.Add($"武器 {w.Id} 没有对应物品");
                // 盾、指挥器具的熟练度在 B5（十五门），B4 不能攻击，允许留空（开发假设 H2）
                if (!skills.Contains(w.SkillId) && !(w.SkillId == "" && !w.CanAttack)) e.Add($"武器 {w.Id} 的技能 {w.SkillId} 不存在");
                if (w.AmmoId != null && !items.Contains(w.AmmoId)) e.Add($"武器 {w.Id} 的弹药 {w.AmmoId} 不存在");
                if (w.DamageMin < 0 || w.DamageMax < w.DamageMin) e.Add($"武器 {w.Id} 伤害区间无效");
                if (w.AccuracyRating <= 0 && w.CanAttack) e.Add($"武器 {w.Id} 命中能力须为正");   // 指挥/医疗器具命中 0（A05 §1，不可直接攻击）
                if (w.IsFists && w.WeaponKind != null) e.Add("拳不占武器槽，不能有 weaponKind");
                if (!w.IsFists && !State.WeaponKinds.IsValid(w.WeaponKind)) e.Add($"武器 {w.Id} 的 weaponKind {w.WeaponKind ?? "（缺）"} 不是九种武器类型之一");
                if (!w.IsFists)
                {
                    var it = c.Items.FirstOrDefault(i => i.Id == w.Id);
                    if (it != null && !(it.DurabilityMaxRange ?? new double[0]).SequenceEqual(w.DurabilityMaxRange ?? new double[0]))
                        e.Add($"武器 {w.Id} 的最大耐久区间与物品不一致");
                    if (it != null && it.WeightGrams != w.WeightGrams) e.Add($"武器 {w.Id} 的重量与物品不一致");
                }
            }
            ValidateEquipment(c, e, items);
            foreach (var a in c.CombatActions)
            {
                foreach (var sk in a.Skills) if (!skills.Contains(sk)) e.Add($"战斗动作 {a.Id} 的技能 {sk} 不存在");
                if (a.ItemId != null && !items.Contains(a.ItemId)) e.Add($"战斗动作 {a.Id} 的物品 {a.ItemId} 不存在");
                if (a.WeaponId != null && c.Weapons.All(w => w.Id != a.WeaponId)) e.Add($"战斗动作 {a.Id} 的武器 {a.WeaponId} 不存在");
                // 切换武器页 0AP（A10 §4.2、A05 §5，B3 H6）；其余动作至少 1AP
                if ((a.ApCost == 0 && a.Id != "switch_weapon") || a.ApCost > c.Scope.HumanApPerActivation) e.Add($"战斗动作 {a.Id} 的行动点无效");
            }
            foreach (var en in c.Enemies)
            {
                if (en.Hp <= 0 || en.Dodge <= 0 || en.AttackRating <= 0) e.Add($"敌人 {en.Id} 的健康、闪避与攻击能力须为正");
                if (!c.WorldEnemies.RarityWeights.ContainsKey(en.Rarity)) e.Add($"敌人 {en.Id} 的稀有层级 {en.Rarity} 不在刷新权重里");
            }
            if (c.Scope.BasePlayerDodge <= 0) e.Add("玩家闪避能力须为正");

            var we = c.WorldEnemies;
            foreach (var k in we.LocalCaps.Keys) if (!locs.Contains(k)) e.Add($"世界敌人上限引用了不存在的地点 {k}");
            foreach (var kv in we.Initial)
            {
                if (!locs.Contains(kv.Key)) e.Add($"世界敌人初始分布引用了不存在的地点 {kv.Key}");
                foreach (var id in kv.Value) if (!enemies.ContainsKey(id)) e.Add($"世界敌人初始分布引用了不存在的敌人 {id}");
                if (kv.Value.Count > we.CapOf(kv.Key)) e.Add($"{kv.Key} 的初始敌人超过本地上限");
            }
            if (we.Initial.Values.Sum(v => v.Count) > we.GlobalCap) e.Add("初始世界敌人超过全局上限");
            foreach (var kv in we.Pools)
            {
                if (!locs.Contains(kv.Key)) e.Add($"刷新池引用了不存在的地点 {kv.Key}");
                foreach (var tier in kv.Value)
                {
                    if (!we.RarityWeights.ContainsKey(tier.Key)) e.Add($"刷新池 {kv.Key} 的层级 {tier.Key} 未知");
                    foreach (var w in tier.Value)
                    {
                        if (!enemies.TryGetValue(w.EnemyId, out var en)) e.Add($"刷新池 {kv.Key} 引用了不存在的敌人 {w.EnemyId}");
                        else if (en.Rarity != tier.Key) e.Add($"刷新池 {kv.Key} 的 {w.EnemyId} 不属于 {tier.Key} 层");
                        if (w.Weight <= 0) e.Add($"刷新池 {kv.Key} 的权重须为正");
                    }
                }
            }
            if (we.SpawnCheckIntervalMinutes <= 0 || we.SpawnCheckIntervalMinutes % tick != 0) e.Add($"刷新检查间隔须为{tick}的正倍数");
            if (we.PatrolCheckIntervalMinutes <= 0 || we.PatrolCheckIntervalMinutes % tick != 0) e.Add($"巡游检查间隔须为{tick}的正倍数");
            foreach (var sl in c.Encounters.SafeLocations) if (!locs.Contains(sl)) e.Add($"安全地点 {sl} 不存在");
            if (c.Encounters.BypassMinutes <= 0 || c.Encounters.BypassMinutes % tick != 0) e.Add($"绕行时长须为{tick}的正倍数");
            if (!c.HourlyRates.ContainsKey(c.Encounters.BypassRateCategory)) e.Add($"绕行速率类别 {c.Encounters.BypassRateCategory} 不存在");
            if (c.BattleTime.RoundWorldMinutes != tick) e.Add("战斗每轮世界分钟须等于时间段长度");

            foreach (var en in c.ZombieLoot.Entries)
                if (en.ItemId != null && !items.Contains(en.ItemId)) e.Add($"丧尸掉落引用了不存在的物品 {en.ItemId}");
            if (c.ZombieLoot.Entries.Sum(x => x.Weight) != 100) e.Add("丧尸掉落权重合计须为100");
            foreach (var cc in c.AnimalCarcasses)
            {
                if (!enemies.ContainsKey(cc.EnemyId)) e.Add($"尸体配置引用了不存在的敌人 {cc.EnemyId}");
                if (!items.Contains(cc.CarcassItemId)) e.Add($"尸体物品 {cc.CarcassItemId} 不存在");
                foreach (var t in cc.RequiredCarriedTools) if (!items.Contains(t)) e.Add($"处理尸体工具 {t} 不存在");
                foreach (var o in cc.Outputs.Keys) if (!items.Contains(o)) e.Add($"处理尸体产物 {o} 不存在");
                if (cc.ButcherBaseMinutes <= 0 || cc.ButcherBaseMinutes % tick != 0) e.Add($"处理尸体时长须为{tick}的正倍数");
            }
            foreach (var rc in c.Recipes)
            {
                foreach (var k in rc.MinSkills.Keys) if (!skills.Contains(k)) e.Add($"配方 {rc.Id} 的技能门槛 {k} 不存在");
                if (rc.IsRepair && !rc.TargetInstanceRequired) e.Add($"维护配方 {rc.Id} 须指定目标实例");
            }
            foreach (var r in c.Resources)
            {
                if (r.IndependentRare != null && !items.Contains(r.IndependentRare.ItemId)) e.Add($"资源点 {r.NodeId} 稀有附带物不存在");
                if (r.ExclusiveLoot != null)
                {
                    foreach (var x in r.ExclusiveLoot) if (!items.Contains(x.ItemId)) e.Add($"资源点 {r.NodeId} 互斥抽取物品 {x.ItemId} 不存在");
                    if (r.ExclusiveLoot.Sum(x => x.Weight) != 100) e.Add($"资源点 {r.NodeId} 互斥抽取权重合计须为100");
                }
            }
            foreach (var k in c.DevKit.Items.Keys) if (!items.Contains(k)) e.Add($"开发测试箱物品 {k} 不存在");
            // v1.0 P02：金属锅没有耐久，只校验打火器
            if (!c.Durability.Tools.ContainsKey("lighter")) e.Add("缺少 lighter 的耐久配置");
            foreach (var it in c.Items.Where(i => i.ContainerId != null && i.HasDurability))
                e.Add($"容器 {it.Id} 不应有耐久（P02 锅无耐久）");
            foreach (var en in c.Enemies)
            {
                if (en.FearChance < 0 || en.FearChance > 1) e.Add($"敌人 {en.Id} 基础惊吓概率须在0—1");
                if (en.FearValue < 0) e.Add($"敌人 {en.Id} 惊吓值不能为负");
            }
            foreach (var k in we.RarityOpenAbsoluteMinute.Keys)
                if (!we.RarityWeights.ContainsKey(k)) e.Add($"开放时间的层级 {k} 未知");
            var sub = c.ActiveSubstance;
            if (!string.IsNullOrEmpty(sub.ItemId) && !items.Contains(sub.ItemId)) e.Add($"活性物质 {sub.ItemId} 不存在");
            if (sub.NormalDrop.ChancePerEnemy < 0 || sub.NormalDrop.ChancePerEnemy > 1) e.Add("活性物质普通掉率须在0—1");
            if (sub.Fusion.Minutes < 0 || sub.Fusion.Minutes % tick != 0) e.Add($"吞噬融合时长须为{tick}的倍数");
            if (!string.IsNullOrEmpty(sub.Fusion.RateKey) && !c.HourlyRates.ContainsKey(sub.Fusion.RateKey)) e.Add($"吞噬融合速率类别 {sub.Fusion.RateKey} 不存在");
            var tb = c.Story.TutorialBattle;
            if (!string.IsNullOrEmpty(tb.EnemyId) && !enemies.ContainsKey(tb.EnemyId)) e.Add($"首战敌人 {tb.EnemyId} 不存在");
            if (!string.IsNullOrEmpty(tb.LocationId) && !locs.Contains(tb.LocationId)) e.Add($"首战地点 {tb.LocationId} 不存在");
            Dup(c.Story.Dialogues, d => d.Id, "对白", e);
            if (c.Infection.ProgressPerHour.Count == 0) e.Add("缺少病毒进展速率");

            Shape(e, "skillGrowth.damageFactor", c.SkillGrowth.DamageFactor);
            Shape(e, "skillGrowth.accuracyRating", c.SkillGrowth.AccuracyRating);
            Shape(e, "skillGrowth.energyCost", c.SkillGrowth.EnergyCost);
            Shape(e, "skillGrowth.medicalHeal", c.SkillGrowth.MedicalHeal);
            Shape(e, "crafting.workFactor", c.Crafting.WorkFactor);
            Shape(e, "combatMath.critRating", c.CombatMath.CritRating);
            Shape(e, "combatMath.critProbability", c.CombatMath.CritProbability);
            Shape(e, "combatMath.initiative", c.CombatMath.Initiative);
            Shape(e, "combatMath.guardAppliedAfterArmor", c.CombatMath.GuardAppliedAfterArmor);
            foreach (var sk in c.Skills) Shape(e, "skills.nextLevelXpFormula", sk.NextLevelXpFormula);
            Shape(e, "xp.craft", c.Xp.Craft);
            Shape(e, "panic.initial", c.Panic.Initial);
            Shape(e, "panic.directDamageGain", c.Panic.DirectDamageGain);
            Shape(e, "panic.postBattleMoraleLoss", c.Panic.PostBattleMoraleLoss);
            Shape(e, "retreat.chance", c.Retreat.Chance);
            Shape(e, "infection.exposureProbability", c.Infection.ExposureProbability);
            Shape(e, "infection.bandage.loadReduction", c.Infection.Bandage.LoadReduction);
        }

        /// <summary>B4 装备静态表（A05 §1、§2、§4、§5）：装备槽、武器/防具与物品一致、词条兼容池、品质概率与拆解产出。</summary>
        private static void ValidateEquipment(GameConfig c, List<string> e, HashSet<string> items)
        {
            Dup(c.Gear, x => x.Id, "装备", e);
            Dup(c.Affixes, x => x.Id, "词条", e);
            var weapons = c.Weapons.Where(w => !w.IsFists).ToDictionary(w => w.Id);
            var gear = c.Gear.GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First());
            var eq = c.Equipment;
            foreach (var it in c.Items.Where(i => i.EquipSlot != null))
            {
                if (Array.IndexOf(ItemConfig.EquipSlotKinds, it.EquipSlot) < 0) { e.Add($"物品 {it.Id} 的装备槽 {it.EquipSlot} 未知"); continue; }
                if (it.IsStackable) e.Add($"装备 {it.Id} 不能堆叠");
                if (it.EquipSlot == "weapon")
                {
                    if (!weapons.ContainsKey(it.Id)) e.Add($"物品 {it.Id} 能进武器槽却没有武器定义");
                }
                else if (!gear.TryGetValue(it.Id, out var g)) e.Add($"物品 {it.Id} 能进{it.EquipSlot}槽却没有装备定义");
                else if (g.Slot != it.EquipSlot) e.Add($"物品 {it.Id} 的装备槽与装备定义不一致");
            }
            foreach (var w in weapons.Values)
                if (c.Items.FirstOrDefault(i => i.Id == w.Id) is ItemConfig wi && wi.EquipSlot != "weapon") e.Add($"武器 {w.Id} 的物品须写 equipSlot weapon");
            foreach (var g in c.Gear)
            {
                var it = c.Items.FirstOrDefault(i => i.Id == g.Id);
                if (it == null) { e.Add($"装备 {g.Id} 没有对应物品"); continue; }
                if (it.WeightGrams != g.WeightGrams) e.Add($"装备 {g.Id} 的重量与物品不一致");
                if (!(it.DurabilityMaxRange ?? new double[0]).SequenceEqual(g.DurabilityMaxRange ?? new double[0])) e.Add($"装备 {g.Id} 的最大耐久区间与物品不一致");
                if (it.EquipSlot != g.Slot) e.Add($"装备 {g.Id} 的物品须写 equipSlot {g.Slot}");
            }
            foreach (var s in c.Gear.Select(g => g.Salvage).Concat(c.Weapons.Select(w => w.Salvage)).Where(x => x != null))
                if (!items.Contains(s!.ItemId) || (!s.ByQuality && s.Count <= 0)) e.Add($"拆解产出 {s.ItemId} 无效");
            if (c.Gear.Count == 0) return;
            foreach (var a in c.Affixes)
            {
                if (string.IsNullOrEmpty(a.Group)) e.Add($"词条 {a.Id} 缺少互斥组");
                if (a.Pools.Count == 0) e.Add($"词条 {a.Id} 没有兼容池");
                foreach (var p in a.Pools)
                {
                    var ok = p == "durable"
                             || p.StartsWith("kind:") && State.WeaponKinds.IsValid(p.Substring(5))
                             || p.StartsWith("slot:") && Array.IndexOf(ItemConfig.EquipSlotKinds, p.Substring(5)) > 0;
                    if (!ok) e.Add($"词条 {a.Id} 的兼容池 {p} 未知");
                }
            }
            foreach (var q in eq.QualityOrder)
                if (!eq.AffixCounts.ContainsKey(q) || !eq.QualityNames.ContainsKey(q)) e.Add($"品质 {q} 缺少词条数或名称");
            foreach (var kv in eq.QualityOrigins)
            {
                foreach (var q in kv.Value.Keys) if (!eq.QualityOrder.Contains(q)) e.Add($"品质来源 {kv.Key} 的品质 {q} 未知");
                if (Math.Abs(kv.Value.Values.Sum() - 1) > 1e-9) e.Add($"品质来源 {kv.Key} 的概率合计须为1");
            }
            if (eq.ScavengedResourceOrigin != "" && !eq.QualityOrigins.ContainsKey(eq.ScavengedResourceOrigin)) e.Add("拾荒箱品质来源未定义");
            if (eq.CraftOrigin != "" && !eq.QualityOrigins.ContainsKey(eq.CraftOrigin)) e.Add("制造品质来源未定义");
            if (eq.DurabilityRounding <= 0) e.Add("最大耐久取整精度须为正");
            if (!c.Recipes.Any(r => r.Id == eq.Salvage.RecipeId && r.IsSalvage)) e.Add($"拆解配方 {eq.Salvage.RecipeId} 不存在");
            foreach (var q in eq.QualityOrder) if (!eq.Salvage.ScrapByQuality.ContainsKey(q)) e.Add($"拆解缺少品质 {q} 的废金属数");
        }

        private static void Dup<T>(IEnumerable<T> list, Func<T, string> key, string what, List<string> e)
        {
            foreach (var g in list.GroupBy(key).Where(g => g.Count() > 1))
                e.Add($"{what} ID 重复：{g.Key}");
        }

    }
}
