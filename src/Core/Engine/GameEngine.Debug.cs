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

        /// <summary>开发测试箱（v0.5 §15）：金属矛、弩、霰弹枪、步枪及弹药只经调试入口取得，放到随身。</summary>
        public void DebugGiveDevKit(GameState s)
        {
            foreach (var kv in Config.DevKit.Items) CreateItems(s, kv.Key, kv.Value, ItemInstance.Carried);
            Emit(s, GameEventKind.Info, "开发测试箱：武器与弹药已放到随身。");
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

        /// <summary>调试面板：在当前地点地面放一批常用物资，用于检查大量物资时的界面。</summary>
        public void DebugFillGround(GameState s)
        {
            foreach (var it in Config.Items)
                CreateItems(s, it.Id, it.IsStackable ? 3 : 1, s.CurrentLocation);
        }
    }
}
