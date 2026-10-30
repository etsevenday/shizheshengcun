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
            s.CurrentLocation = locationId;
            Visit(s, locationId);
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
            s.UnlockedEdges.Add(edgeId);
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
                else { MoveItems(s, m, m.Count, s.CurrentLocation); ground++; }
            }
            return ground == 0 ? $"{carried}张已放到随身。"
                : carried == 0 ? $"随身最多{MaxRootCards}张卡已满，{ground}张放在当前地面。"
                : $"{carried}张放到随身，随身最多{MaxRootCards}张卡，放不下的{ground}张放在当前地面。";
        }

        /// <summary>调试：立即以当前地点的现有敌对敌人形成敌踪（第1日也可，用于检查界面）。没有敌人返回 false。</summary>
        public bool DebugForceEncounter(GameState s)
        {
            if (InCombatFlow(s) || s.Main != null || s.Sub != null) return false;
            var group = LocalHostiles(s, s.CurrentLocation).Take(Config.Encounters.GroupMax).ToList();
            if (group.Count == 0) return false;
            s.Encounter = new PendingEncounter { LocationId = s.CurrentLocation, EnemyIds = group.Select(w => w.Id).ToList(), Source = "explore", Minute = s.GameMinute };
            Emit(s, GameEventKind.Encounter, "（调试）发现敌踪。");
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
        public string DebugGiveEquipmentSet(GameState s, string quality)
        {
            var made = DevEquipmentSet.Select(id => DebugCreateEquipment(s, id, quality, s.CurrentLocation)).ToList();
            Normalize(s, s.CurrentLocation);
            var text = $"开发：{Config.Equipment.QualityName(quality)}色装备{made.Count}件放在{LocName(s.CurrentLocation)}地面：" +
                       string.Join("、", made.Select(i => ItemName(i.ItemId) + (i.Affixes is { Count: > 0 } a ? "（" + string.Join("、", a.Select(x => Affix(x)?.Name ?? x)) + "）" : "")));
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发入口（B4）：B4 新导入、只有定义的某类物品（material/food/medical）各 1 份放在当前地面。</summary>
        public string DebugGiveNewItems(GameState s, string kind)
        {
            var ids = Config.Items.Where(i => i.Kind == kind && !i.IsEquipment && i.Desc.Contains("本版只有物品定义")).Select(i => i.Id).ToList();
            foreach (var id in ids) CreateItems(s, id, 1, s.CurrentLocation);
            var name = kind switch { "material" => "材料", "food" => "食品", "medical" => "医疗", _ => kind };
            var text = $"开发：新物品（{name}）{ids.Count}种各1份放在{LocName(s.CurrentLocation)}地面";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>调试面板：在当前地点地面放一批常用物资，用于检查大量物资时的界面。</summary>
        public void DebugFillGround(GameState s)
        {
            foreach (var it in Config.Items)
                CreateItems(s, it.Id, it.IsStackable ? 3 : 1, s.CurrentLocation);
        }
    }
}
