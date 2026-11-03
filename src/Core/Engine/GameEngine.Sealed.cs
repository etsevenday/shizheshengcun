using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 封存战利品账、远征回收箱与旧实例遗留物（开发计划 v0.9 批次 B22，需求 R68；R03 D7「已得物资、沿途奖励与owner」、D8、D10，R08「地牢封存与遗物归属」）：
    /// <list type="bullet">
    /// <item>封存（D7.1 第 2 类）：只收带本实例来源（<see cref="ItemInstance.RewardOrigin"/>）的已领奖励，从当前地面或同行者随身移入 owner “sealed:instanceId”；
    /// 封存物不占五卡、不可吃/射/穿/做，要用须在本地牢非战斗时明确取出（取到随身验五卡/30kg，或放当前地面）。外带物、自制物、候补装备没有来源，拒绝（DR12）。</item>
    /// <item>远征回收箱：远征成功抵达父地点（终止撤出或通关离开）时，封存物一次换 owner 到父地点“远征回收箱”（遗物容器 Kind=recovery，无卡位上限、只能实地取）；
    /// 各人随身照旧归本人，不复制；本实例来源标记清掉（再带回去不能二次封存）。主角死亡不迁（终局优先，DR13）。</item>
    /// <item>旧实例遗留物：地牢房间地面、死在地牢里的队友遗物、未带出的封存物仍属旧实例，终止确认窗列清单；重置前一次原子迁到父地点“旧实例遗物认领箱”
    /// （Kind=relic），保质照原 expiresAt，不静默删除（DR16）。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>封存战利品账 owner 前缀（R03 D7.1）：完整写法 "sealed:&lt;instanceId&gt;"。</summary>
        public const string SealedPrefix = "sealed:";

        public static string SealedOwner(string instanceId) => SealedPrefix + instanceId;

        /// <summary>父地点“远征回收箱”的遗物容器键（每座地牢一个，多次带回累加）。</summary>
        public static string RecoveryBoxKey(string dungeonId) => "recovery_" + dungeonId;

        /// <summary>父地点“旧实例遗物认领箱”的遗物容器键（每座地牢一个）。</summary>
        public static string RelicBoxKey(string dungeonId) => "relic_" + dungeonId;

        /// <summary>某实例封存账里的物品（根物品）。</summary>
        public List<ItemInstance> SealedItems(GameState s, string instanceId) =>
            s.Items.Where(i => i.Owner == SealedOwner(instanceId)).OrderBy(i => i.InstanceId).ToList();

        /// <summary>当前所在地牢实例的封存物（不在地牢为空）。</summary>
        public List<ItemInstance> SealedHere(GameState s) =>
            DungeonAt(s.CurrentLocation) is string d && ExpeditionEnabled(d) ? SealedItems(s, InstanceId(s, d)) : new List<ItemInstance>();

        /// <summary>可以封存的物品（本次远征实例来源、在当前地面或同行者随身、未穿戴/未占用）：封存面板列出。</summary>
        public List<ItemInstance> SealableItems(GameState s)
        {
            var e = s.Expedition;
            if (e == null) return new List<ItemInstance>();
            return s.Items.Where(i => i.RewardOrigin == e.InstanceId && SealBlocked(s, i.InstanceId) == null).OrderBy(i => i.InstanceId).ToList();
        }

        /// <summary>同行者的随身 owner 集合。</summary>
        private HashSet<string> PartyCarriedOwners(GameState s) => new HashSet<string>(s.Party.Select(ItemInstance.CarriedOwner));

        /// <summary>封存的只读预演（R03 D7.1、R08）；null 为可以。</summary>
        public string? SealBlocked(GameState s, int instanceId)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (s.Battle != null || s.Encounter != null) return "战斗结束后才能封存";
            var e = s.Expedition;
            if (e == null || DungeonAt(s.CurrentLocation) != e.DungeonId) return "只有在本次远征的地牢里才能封存";
            var it = FindItem(s, instanceId);
            if (it == null) return "这件物品已不在";
            if (it.Owner.StartsWith(SealedPrefix)) return "已经封存了";
            if (it.RewardOrigin != e.InstanceId)
                return "只能封存本次地牢里已经领到的奖励（物资箱、守卫回收、首通补给、胜利掉落）；外带物、自制物、候补装备不能封存";
            if (it.IsWorn) return "先卸下这件装备再封存";
            if (it.Owner != s.Ground && !PartyCarriedOwners(s).Contains(it.Owner)) return "要在当前地点地面或同行者随身里才能封存";
            if (it.ReservedBy != null || IsLocked(s, it)) return "这件物品正被使用";
            return null;
        }

        /// <summary>封存（0 时间、整堆、requestId 幂等）：换 owner 到本实例封存账，不占五卡、暂不可用。</summary>
        public ActionResult SealReward(GameState s, int instanceId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = SealBlocked(s, instanceId);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var it = FindItem(s, instanceId)!;
            var from = it.Owner;
            var owner = SealedOwner(s.Expedition!.InstanceId);
            it.Owner = owner;
            it.PlacedOrder = null;
            Normalize(s, from);
            Normalize(s, owner);
            Emit(s, GameEventKind.Info, $"封存{ItemName(it.ItemId)}×{it.Count}：放进本次地牢的封存战利品（不占随身卡位，暂不能使用；成功撤出或通关离开后放进{LocName(DungeonCfg(s.Expedition.DungeonId)!.Parent)}的远征回收箱）。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>取出封存物的只读预演：要在该地牢里、非战斗；取到随身验五卡与 30kg（放地面不验）。null 为可以。</summary>
        public string? UnsealBlocked(GameState s, int instanceId, bool toGround)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (s.Battle != null || s.Encounter != null) return "战斗中不能从封存里拿东西";
            var it = FindItem(s, instanceId);
            if (it == null || !it.Owner.StartsWith(SealedPrefix)) return "这件物品不在封存里";
            var d = DungeonAt(s.CurrentLocation);
            if (d == null || !ExpeditionEnabled(d) || it.Owner != SealedOwner(InstanceId(s, d))) return "要回到这座地牢里才能取出封存物";
            if (!toGround && PickupBlockedText(s, it, it.Count) is string p) return p;
            return null;
        }

        /// <summary>取出封存物（0 时间、整堆、requestId 幂等）：到你的随身或当前地面，封存账同步扣除。</summary>
        public ActionResult UnsealReward(GameState s, int instanceId, bool toGround, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = UnsealBlocked(s, instanceId, toGround);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var it = FindItem(s, instanceId)!;
            var from = it.Owner;
            var dest = toGround ? s.Ground : ItemInstance.Carried;
            it.Owner = dest;
            Normalize(s, from);
            Normalize(s, dest);
            Emit(s, GameEventKind.Info, $"从封存里取出{ItemName(it.ItemId)}×{it.Count}" + (toGround ? "，放在地面。" : "。"));
            UpdateFlags(s);
            return ActionResult.Success();
        }

        // ───────────────────────── 远征回收箱（D7.1 末段、D10 “存活抵达原子移动角色与封存owner”） ─────────────────────────

        /// <summary>
        /// 远征成功抵达外部父地点（终止撤出完成或通关离开）：本实例封存物一次换 owner 到父地点远征回收箱，并清掉随队带出物品的本实例来源标记
        /// （留在地牢里的地面物/遗物保留，仍属旧实例）。返回给日志的短语；没有封存物为 null。
        /// </summary>
        private string? MoveSealedToRecovery(GameState s, ExpeditionState e)
        {
            var d = DungeonCfg(e.DungeonId);
            if (d == null) return null;
            var sealedItems = SealedItems(s, e.InstanceId);
            string? text = null;
            if (sealedItems.Count > 0)
            {
                var key = RecoveryBoxKey(e.DungeonId);
                if (!s.Remains.TryGetValue(key, out var box))
                    s.Remains[key] = box = new RemainsState { Name = $"{d.Name}远征回收箱", LocationId = d.Parent, Cause = "成功撤出时带回的封存战利品", Kind = RemainsState.RecoveryKind };
                box.Minute = s.GameMinute;
                foreach (var it in sealedItems) { it.Owner = ItemInstance.RemainsOwner(key); it.PlacedOrder = null; }
                text = $"封存的{sealedItems.Sum(i => i.Count)}份战利品（{string.Join("、", sealedItems.Select(i => $"{ItemName(i.ItemId)}×{i.Count}"))}）已放进{LocName(d.Parent)}的远征回收箱（只能在这里取，取到随身照常受五卡与负重限制）";
            }
            ClearCarriedOrigin(s, e.DungeonId, e.InstanceId);
            return text;
        }

        /// <summary>清掉已离开地牢的本实例来源标记（随身、穿戴、回收箱、地牢外地面）；地牢房间里与死在地牢里的遗物保留。合堆键变化后重新合堆。</summary>
        private void ClearCarriedOrigin(GameState s, string dungeonId, string instanceId)
        {
            var touched = new HashSet<string>();
            foreach (var it in s.Items.Where(i => i.RewardOrigin == instanceId).ToList())
            {
                var place = PlaceOf(s, it);
                if (DungeonAt(place) == dungeonId) continue;
                if (place.StartsWith(ItemInstance.RemainsPrefix) && s.Remains.TryGetValue(place.Substring(ItemInstance.RemainsPrefix.Length), out var rem)
                    && DungeonAt(rem.LocationId) == dungeonId) continue;
                it.RewardOrigin = null;
                touched.Add(it.Owner);
            }
            foreach (var o in touched) Normalize(s, o);
        }

        // ───────────────────────── 旧实例遗留物（D7.1 末段、D8） ─────────────────────────

        /// <summary>旧实例里尚存的根物品及出处（地牢房间地面、死在地牢里的队友遗物、未带出的封存物），按出处、实例号排序。</summary>
        public List<(ItemInstance Item, string From)> DungeonLeftovers(GameState s, string dungeonId)
        {
            var rooms = new HashSet<string>(DungeonRooms(dungeonId).Select(l => l.Id));
            var list = new List<(ItemInstance, string)>();
            foreach (var it in s.Items.OrderBy(i => i.InstanceId))
            {
                if (rooms.Contains(it.Owner)) list.Add((it, $"{LocName(it.Owner)}地面"));
                else if (it.Owner.StartsWith(SealedPrefix) && it.Owner.Contains(":" + dungeonId + ":g")) list.Add((it, "封存战利品（未带出）"));
                else if (it.InRemains && s.Remains.TryGetValue(it.Owner.Substring(ItemInstance.RemainsPrefix.Length), out var rem)
                         && rem.Kind == null && rooms.Contains(rem.LocationId)) list.Add((it, $"{rem.Name}的遗物（{LocName(rem.LocationId)}）"));
            }
            return list;
        }

        /// <summary>遗留物清单短句（终止确认窗与重置确认窗）：“布料×2（捐献仓地面）”，已腐败的标出。</summary>
        public List<string> LeftoverLines(GameState s, string dungeonId) =>
            DungeonLeftovers(s, dungeonId).Select(x => $"{ItemName(x.Item.ItemId)}×{x.Item.Count}（{x.From}{(IsSpoiled(s, x.Item) ? "，已腐败" : "")}）").ToList();

        /// <summary>
        /// 旧实例遗留物一次迁到父地点“旧实例遗物认领箱”（D8：重置前原子迁存；保质照原 expiresAt、不延寿；队友遗物容器随之清空移除；本实例来源标记清掉）。返回迁移件数。
        /// </summary>
        private int MoveLeftoversToRelicBox(GameState s, string dungeonId)
        {
            var d = DungeonCfg(dungeonId);
            if (d == null) return 0;
            var items = DungeonLeftovers(s, dungeonId).Select(x => x.Item).ToList();
            if (items.Count == 0) return 0;
            var key = RelicBoxKey(dungeonId);
            if (!s.Remains.TryGetValue(key, out var box))
                s.Remains[key] = box = new RemainsState { Name = $"{d.Name}旧实例遗物认领箱", LocationId = d.Parent, Cause = "地牢重置前迁出的旧实例遗留物", Kind = RemainsState.RelicKind };
            box.Minute = s.GameMinute;
            foreach (var it in items)
            {
                it.Owner = ItemInstance.RemainsOwner(key);
                it.PlacedOrder = null;
                it.RewardOrigin = null;
            }
            foreach (var kv in s.Remains.Where(kv => kv.Key != key && RemainsItems(s, kv.Key).Count == 0).ToList()) s.Remains.Remove(kv.Key);
            return items.Count;
        }
    }
}
