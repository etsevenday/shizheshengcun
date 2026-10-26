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

        /// <summary>调试面板：在当前地点地面放一批常用物资，用于检查大量物资时的界面。</summary>
        public void DebugFillGround(GameState s)
        {
            foreach (var it in Config.Items)
                CreateItems(s, it.Id, it.IsStackable ? 3 : 1, s.CurrentLocation);
        }
    }
}
