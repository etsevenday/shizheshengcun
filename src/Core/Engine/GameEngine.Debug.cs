using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>测试与调试面板专用的状态改写入口。正式流程不得调用。</summary>
    public sealed partial class GameEngine
    {
        /// <summary>直接把角色放到某地点（标记已发现、已访问并揭示进入即显示的资源）。</summary>
        public void DebugTeleport(GameState s, string locationId)
        {
            s.LastMove = null;   // B22h：开发传送不是已完成的移动，之前的移动记录不再可信
            s.CurrentLocation = locationId;
            s.CurrentZone = ExteriorZone;
            Visit(s, locationId);
            DevExpeditionAfterTeleport(s, locationId);   // B21-14
            UpdateFlags(s);
        }

        /// <summary>直接设置探索度并结算对应里程碑。</summary>
        public void DebugSetProgress(GameState s, string locationId, int progress)
        {
            var ls = s.Locations[locationId];
            ls.Progress = progress;
            foreach (var t in new[] { 25, 50, 75, 100 })
                if (t <= progress) ls.TriggeredMilestones.Add(t);
            foreach (var r in Config.Resources.Where(r => r.LocationId == locationId && r.RevealAt.HasValue && r.RevealAt.Value <= progress))
                s.Resources[r.NodeId].Discovered = true;
            UnlockEdges(s);
            UpdateFlags(s);
        }

        public void DebugUnlockEdge(GameState s, string edgeId)
        {
            var e = _edges[edgeId];
            if (GateScope(e) == Survival.Core.Config.GateScopes.CurrentInstance && e.DungeonId != null) EnsureInstance(s, e.DungeonId).UnlockedEdges.Add(edgeId);
            else s.UnlockedEdges.Add(edgeId);
            s.Locations[e.A].Discovered = true;
            s.Locations[e.B].Discovered = true;
        }

        public void DebugGive(GameState s, string itemId, int count, string owner = ItemInstance.Carried) =>
            CreateItems(s, itemId, count, owner);

        /// <summary>设置状态值，并把提醒锁存同步为“已提醒过”，避免测试被开局提醒打断。</summary>
        public void DebugSetStats(GameState s, double h, double n, double w, double e, double sl, double m)
        {
            // 按项上限夹紧（健康/体力上限 20）：旧用例里的“100”即满值
            var st = new Stats { HealthMax = s.Stats.HealthMax, EnergyMax = s.Stats.EnergyMax };
            st.Set(StatId.Health, h);
            st.Set(StatId.Satiety, n);
            st.Set(StatId.Hydration, w);
            st.Set(StatId.Energy, e);
            st.Set(StatId.Wakefulness, sl);
            st.Set(StatId.Morale, m);
            s.Stats = st;
            SyncLatches(s);
        }

        public void SyncLatches(GameState s)
        {
            foreach (var id in Stats.All)
            {
                s.AlertLatched[id] = Config.Thresholds[StatKey(id)].IsBelow(s.Stats.Get(id));
                s.ZeroLatched[id] = s.Stats.Get(id) <= 0;
            }
        }

        /// <summary>
        /// 开发测试箱（v0.5 §15）：金属矛、弩、霰弹枪、步枪及弹药只经调试入口取得，放到随身；随身五张根卡放不下的放当前地面（开发假设 H4）。
        /// 返回给玩家看的准确说明（放到随身几张、地面几张）。
        /// </summary>
        public string DebugGiveDevKit(GameState s)
        {
            var text = "开发测试箱：" + DebugGiveCarriedOrGround(s, Config.DevKit.Items.Select(kv => (kv.Key, kv.Value)));
            Emit(s, GameEventKind.Info, text);
            return text;
        }

        /// <summary>
        /// 调试发物：先生成到临时位置，逐张能放进随身（不超过五张根卡）的放随身，其余放当前地面；返回“N张放到随身，M张放在当前地面”。
        /// </summary>
        public string DebugGiveCarriedOrGround(GameState s, IEnumerable<(string itemId, int count)> items)
        {
            const string tmp = "devkit:tmp";
            foreach (var (id, n) in items) CreateItems(s, id, n, tmp);
            int carried = 0, ground = 0;
            foreach (var m in s.Items.Where(i => i.Owner == tmp).OrderBy(i => i.InstanceId).ToList())
            {
                if (RootCardsAfter(s, m, m.Count, null, 0) <= MaxRootCards) { MoveItems(s, m, m.Count, ItemInstance.Carried); carried++; }
                else { MoveItems(s, m, m.Count, s.Ground); ground++; }
            }
            return ground == 0 ? $"{carried}张已放到随身。"
                : carried == 0 ? $"随身最多{MaxRootCards}张卡已满，{ground}张放在当前地面。"
                : $"{carried}张放到随身，随身最多{MaxRootCards}张卡，放不下的{ground}张放在当前地面。";
        }

        /// <summary>调试：立即以当前地点的现有敌对敌人形成敌踪（第1日也可，用于检查界面）。没有敌人返回 false。</summary>
        public bool DebugForceEncounter(GameState s)
        {
            if (InCombatFlow(s) || s.Main != null || s.Sub != null) return false;
            // B41a：新版敌踪下直接形成一张随机组卡（同正式规则：排除动物、按容量锁名单、已有可见组不再成组）
            if (CardsV2(s))
            {
                if (VisibleGroupCardAt(s, s.CurrentLocation) != null) return false;
                var members = GroupCardCandidates(s, s.CurrentLocation);
                if (members.Count == 0) return false;
                var g = AddGroupCard(s, s.CurrentLocation, members, "explore", s.GameMinute);
                Emit(s, GameEventKind.Encounter, "（调试）" + GroupCardFoundText(GroupCardTitle(s, g)), null, members.Select(w => $"enemy:{w.Id}"));
                return true;
            }
            var group = LocalHostiles(s, s.CurrentLocation).Take(Config.Encounters.GroupMax).ToList();
            if (group.Count == 0) return false;
            s.Encounter = new PendingEncounter { LocationId = s.CurrentLocation, EnemyIds = group.Select(w => w.Id).ToList(), Source = "explore", Minute = s.GameMinute };
            Emit(s, GameEventKind.Encounter, "（调试）发现敌踪。");
            return true;
        }

        /// <summary>
        /// 测试与调试：不经敌踪流程，直接用指定的世界敌人开一场普通战斗（按给定顺序截前 C 个入场）。context 取
        /// <see cref="FearEncounter"/>（旧版“接敌”口径：战报写“遭遇”、受惊系数 1.0）或 <see cref="FearApproach"/>（主动接近 0.5）。
        /// 不属于任何敌踪卡（CardKey 为空）。已在战斗、主角已死或没有可入战的敌人时返回 false。
        /// </summary>
        public bool DebugStartBattle(GameState s, IEnumerable<int> worldEnemyIds, string context = FearEncounter)
        {
            if (s.Battle != null || s.Progress.Dead) return false;
            var group = worldEnemyIds.Select(id => FindWorldEnemy(s, id)).Where(w => w != null && !w.Engaged && w.LocationId == s.CurrentLocation).Select(w => w!).ToList();
            if (group.Count == 0) return false;
            s.Encounter = null;
            var encounter = context != FearApproach;
            StartBattle(s, group, encounter, encounter ? "遭遇" : "主动接近", context);
            return true;
        }

        /// <summary>调试：设置感染与病毒程度（0 为未感染）。</summary>
        public void DebugSetVirus(GameState s, double load)
        {
            s.Body.Infected = load > 0;
            s.Body.VirusLoad = load;
            if (load <= 0) s.Body.HighImmunityMinutes = 0;
        }

        /// <summary>
        /// 测试/调试：把这一局改成“没有本轮新剧情”的状态（同旧档 T06：不跑开场对白与首战门控、不启用饱饱输入），
        /// 供只验证其他系统的旧用例使用。新剧情与饱饱相关用例不调用它。
        /// </summary>
        public void DebugLegacyStory(GameState s)
        {
            s.Story = new StoryState { Legacy = true, BaobaoPresent = false };
        }

        /// <summary>开发入口“给一组装备”的清单：每个槽各一件（武器两件不同 kind），覆盖有耐久/无耐久、可拆/待确认。</summary>
        public static readonly string[] DevEquipmentSet =
            { "machete", "wooden_shield", "leather_armor", "steady_ring", "work_shirt", "work_trousers", "repair_manual" };

        /// <summary>
        /// 开发入口（B4）：在当前地面放一组指定品质（white/green/blue）的装备，词条按兼容池照常抽；放地面不占随身卡位。返回给玩家看的说明。
        /// </summary>
        /// <summary>开发入口（B5）：把一组熟练度设为某等级、经验 0（survival=求生五门，否则全部十五门）。不经过 GainXp、不发事件。</summary>
        /// <summary>开发入口（B13b，Q121）：石刀、简易斧、撬棍各一件（随身，放不下的放地面），试资源点逐行合格工具。</summary>
        public string DebugGiveResourceTools(GameState s) =>
            "石刀、简易斧、撬棍各1（开发用，试资源点合格工具）：" + DebugGiveCarriedOrGround(s, new[] { ("stone_knife", 1), ("hatchet", 1), ("crowbar", 1) });

        /// <summary>开发入口（B13a2，Q137）：一把白色无词条消防斧（最大耐久 40—52 正常抽、当前满），随身放不下放地面；试 30 个锁闭储物点的破门。</summary>
        public string DebugGiveFireAxe(GameState s)
        {
            var axe = DebugCreateEquipment(s, "fire_axe", "white", "devkit:tmp");
            var where = RootCardsAfter(s, axe, 1, null, 0) <= MaxRootCards ? ItemInstance.Carried : s.Ground;
            MoveItems(s, axe, 1, where);
            return $"消防斧1（开发用，白色无词条，耐久{Fmt.N(axe.Durability ?? 0)}/{Fmt.N(axe.MaxDurability ?? 0)}，试锁闭储物点破门）：" +
                   (where == ItemInstance.Carried ? "已放到随身。" : $"随身最多{MaxRootCards}张卡已满，放在当前地面。");
        }

        /// <summary>开发入口（B13b）：某一门熟练度直接设为指定等级（经验清零）。</summary>
        public string DebugSetSkillLevel(GameState s, string skillId, int level)
        {
            var lv = Math.Max(1, Math.Min(SkillCap(skillId), level));
            s.Skills[skillId] = new SkillState { Level = lv, Xp = 0 };
            return $"{SkillName(skillId)}熟练度设为{lv}级（开发用）。";
        }

        public string DebugSetSkillLevels(GameState s, int level, bool survivalOnly)
        {
            var ids = SkillsInPlay.Where(k => !survivalOnly || (k.Category == "survival" && !k.EnemyCardsV2Only)).Select(k => k.Id).ToList();
            foreach (var id in ids)
            {
                var lv = Math.Max(1, Math.Min(SkillCap(id), level));
                s.Skills[id] = new SkillState { Level = lv, Xp = 0 };
            }
            return $"{(survivalOnly ? "求生五门（制造、采集、搜集、烹饪、行旅）" : (ids.Count == 16 ? "全部十六门" : "全部十五门"))}熟练度设为{level}级（开发用）。";
        }

        /// <summary>开发入口“技能器具一组”的清单（B7）：每类武器各一件（含盾、指挥、医疗器具）与技能耗材。</summary>
        public static readonly (string Id, int Count)[] DevSkillKit =
        {
            ("kitchen_knife", 1), ("hatchet", 1), ("wooden_spear", 1), ("shock_baton", 1), ("wooden_bow", 1), ("arrow", 10),
            ("pistol", 1), ("ammo_9mm", 16), ("shotgun", 1), ("shell_12g", 8), ("wooden_shield", 1), ("signal_whistle", 1),
            ("trauma_bag", 1), ("bandage", 2), ("medical_gauze", 3), ("saline", 3),
        };

        /// <summary>开发入口（B7）：技能器具一组放在当前地面（白色、无词条），返回给玩家看的说明。</summary>
        public string DebugGiveSkillKit(GameState s)
        {
            foreach (var (id, n) in DevSkillKit)
            {
                if (_weapons.ContainsKey(id) || Config.Gear.Any(g => g.Id == id)) for (var i = 0; i < n; i++) DebugCreateEquipment(s, id, "white", s.Ground);
                else CreateItems(s, id, n, s.Ground);
            }
            Normalize(s, s.Ground);
            var text = $"开发：技能器具一组放在{ZoneName(s, s.Ground)}地面：" + string.Join("、", DevSkillKit.Select(x => ItemName(x.Id) + (x.Count > 1 ? $"×{x.Count}" : "")));
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        public string DebugGiveEquipmentSet(GameState s, string quality)
        {
            var made = DevEquipmentSet.Select(id => DebugCreateEquipment(s, id, quality, s.Ground)).ToList();
            Normalize(s, s.Ground);
            var text = $"开发：{Config.Equipment.QualityName(quality)}色装备{made.Count}件放在{ZoneName(s, s.Ground)}地面：" +
                       string.Join("、", made.Select(i => ItemName(i.ItemId) + (i.Affixes is { Count: > 0 } a ? "（" + string.Join("、", a.Select(x => Affix(x)?.Name ?? x)) + "）" : "")));
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发入口（B4）：B4 新导入、只有定义的某类物品（material/food/medical）各 1 份放在当前地面。</summary>
        public string DebugGiveNewItems(GameState s, string kind)
        {
            var ids = Config.Items.Where(i => i.Kind == kind && !i.IsEquipment && (i.Desc.Contains("本版只有物品定义") || i.Desc.Contains("现在可用于："))).Select(i => i.Id).ToList();
            foreach (var id in ids) CreateItems(s, id, 1, s.Ground);
            var name = kind switch { "material" => "材料", "food" => "食品", "medical" => "医疗", _ => kind };
            var text = $"开发：新物品（{name}）{ids.Count}种各1份放在{ZoneName(s, s.Ground)}地面";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发入口（B7c，Q64）：白色护拳绑带 1 件与一次制作的材料（布2、皮革1、纤维2）放当前地面。返回说明。</summary>
        public string DebugGiveWraps(GameState s)
        {
            DebugCreateEquipment(s, "knuckle_wraps", "white", s.Ground);
            var rc = Recipe("make_knuckle_wraps");
            foreach (var kv in rc.Inputs) CreateItems(s, kv.Key, kv.Value, s.Ground);
            Normalize(s, s.Ground);
            var text = $"开发：护拳绑带1件（白）与一次制作材料（{string.Join("、", rc.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}）放在{ZoneName(s, s.Ground)}地面"
                       + (RecipeKnown(s, rc) ? "" : "；做法要营区登记后才会（可点“记下营区登记”）");
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>调试面板：在当前地点地面放一批常用物资，用于检查大量物资时的界面。</summary>
        public void DebugFillGround(GameState s)
        {
            foreach (var it in Config.Items)
                CreateItems(s, it.Id, it.IsStackable ? 3 : 1, s.Ground);
        }
    }
}
