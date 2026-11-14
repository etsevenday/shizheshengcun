using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 独立腐败物（BM5b2，S06 文首 Q610、Q640、Q641）：有保质截止的物品到期且不在既有烹饪保护中时，原实例原位改为「腐败物」
    /// （份数、每份精确克重、原单堆上限、归属、来源、地牢奖励身份都保留），不再以原物名混在原堆里。
    /// 随身已满（转换前快照 ≥5 张或旧档超额）时该持有者本次腐败的根物品落到其真实所在区地面；未满时转换后仍超 5 张的只把放不下的新腐败物落地。
    /// 锅内子物不算根物品、不外扔。日志只写身边（主角随身、同区在场同伴随身、当前区地面／柜／设施／锅）；远处静默转换。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>腐败物物品 ID（策划 ID，S06 Q610）。</summary>
        public const string SpoiledMatterId = "spoiled_matter";

        /// <summary>腐败物拒绝原用途（逐字稿「拒绝原用途」）。</summary>
        public const string SpoiledMatterRejectText = "腐败物不能用于此操作。";

        /// <summary>输入腐败停工分句（逐字稿「输入腐败停工分句」）。</summary>
        public const string SpoiledWorkClause = "腐败部分已变为腐败物。";

        /// <summary>旧档一次迁移提示（逐字稿「旧档一次迁移提示」）。</summary>
        public const string SpoiledMigrationNote = "已将存档中过期的物品转换为腐败物。";

        /// <summary>取出保护风险（逐字稿「取出后果」）。</summary>
        public const string TakeOutSpoilRiskText = "取出后解除烹饪保护，已过期的食材将变为腐败物。";

        public static bool IsSpoiledMatter(ItemInstance i) => i.ItemId == SpoiledMatterId;

        /// <summary>原物品 ID：腐败物取转换前的原物（日志原名、锅食谱完整性、尸体判定），其余为本身。</summary>
        public static string OriginItemId(ItemInstance i) => i.SpoiledFrom ?? i.ItemId;

        /// <summary>每份克重：腐败物沿原物单件克重（实例属性），其余按配置。</summary>
        public int UnitGrams(ItemInstance i) => i.SpoiledUnitGrams ?? _items[i.ItemId].WeightGrams;

        /// <summary>单堆上限：腐败物沿原物单堆上限（实例属性），其余按配置。</summary>
        public int StackMaxOf(ItemInstance i) => Math.Max(1, i.SpoiledStackMax ?? _items[i.ItemId].StackMax);

        /// <summary>这一实例是否可堆叠：腐败物按继承的原单堆上限（原不可堆叠的尸体仍为 1），其余按配置。</summary>
        public bool IsStackableInst(ItemInstance i) => IsSpoiledMatter(i) ? StackMaxOf(i) > 1 : _items[i.ItemId].IsStackable;

        /// <summary>本实例此刻应转为腐败物：有截止、截止不晚于当前分钟、不在本次有效烹饪保护中（肉干暂停保质时截止为空）。</summary>
        private bool DueToSpoil(GameState s, ItemInstance i) =>
            !IsSpoiledMatter(i) && i.ExpiresAt is int e && e <= s.GameMinute && _items.ContainsKey(i.ItemId) && !IsHeatProtected(s, i);

        /// <summary>原位改为腐败物：记原物、每份克重与单堆上限；不再有保质期限。其余字段（份数、归属、预留、来源、奖励身份、放入序号）不动。</summary>
        private void ToSpoiledMatter(ItemInstance i)
        {
            var cfg = _items[i.ItemId];
            i.SpoiledFrom = i.ItemId;
            i.SpoiledUnitGrams = cfg.WeightGrams;
            i.SpoiledStackMax = cfg.IsStackable ? cfg.StackMax : 1;
            i.ItemId = SpoiledMatterId;
            i.ExpiresAt = null;
        }

        /// <summary>
        /// 一次到期转换事务（每个世界段末、每个变更边界 UpdateFlags、读档迁移、取出解除保护、停工结算前调用；无到期物时什么都不做）。
        /// only 非空时只转换这些实例（停工结算、取出事务）；cardSnapshot 给出事务前各持有者的根卡数（取出事务在搬动前快照），缺省为此刻。
        /// 返回身边应写的腐败句（emit 为 true 时已逐句写入日志）。
        /// </summary>
        internal List<string> ConvertDueSpoilage(GameState s, ICollection<int>? only = null, IDictionary<string, int>? cardSnapshot = null, bool emit = true, bool dropWhenFull = true)
        {
            var lines = new List<string>();
            var due = s.Items.Where(i => (only == null || only.Contains(i.InstanceId)) && DueToSpoil(s, i)).ToList();
            if (due.Count == 0) return lines;

            // 1. 事务前快照各随身持有者的根卡数（S06 Q610「先在同一到期事务前快照该持有者根卡数」）
            var carriers = due.Select(i => CarrierOf(i.Owner)).Where(a => a != null).Select(a => a!).Distinct().ToList();
            var before = carriers.ToDictionary(a => a, a => cardSnapshot != null && cardSnapshot.TryGetValue(a, out var n) ? n : RootCardCountOf(s, a));
            var work = new HashSet<int>(due.Where(i => WorkOwned(s, i)).Select(i => i.InstanceId));
            var origin = due.ToDictionary(i => i.InstanceId, i => (Item: i.ItemId, Owner: i.Owner));

            // 2. 原位转换
            foreach (var i in due) ToSpoiledMatter(i);
            var touched = new HashSet<string>(due.Select(i => i.Owner));

            // 3. 随身满：快照已满／超额的整批落地；未满但转换后超 5 张的只把放不下的新腐败物（实例号大的先）落地
            var dropped = new Dictionary<int, string>();
            if (dropWhenFull)
                foreach (var actor in carriers)
                {
                    var owner = ItemInstance.CarriedOwner(actor);
                    var place = ActorPlaceOf(s, actor);
                    if (place == null) continue;
                    var roots = due.Where(i => i.Owner == owner && i.ReservedBy == null && !IsLocked(s, i)).OrderBy(i => i.InstanceId).ToList();
                    if (roots.Count == 0) continue;
                    if (before[actor] >= MaxRootCards)
                    {
                        foreach (var i in roots) { i.Owner = place; dropped[i.InstanceId] = place; }
                    }
                    else
                    {
                        for (var k = roots.Count - 1; k >= 0 && RootCardCountOf(s, actor) > MaxRootCards; k--)
                        {
                            roots[k].Owner = place;
                            dropped[roots[k].InstanceId] = place;
                        }
                    }
                    if (dropped.Values.Contains(place)) touched.Add(place);
                }
            foreach (var o in touched) Normalize(s, o);

            // 4. 身边日志：按原名与地点／归属聚合；在办工作的输入（预留、晾制中）不写普通句，由该工作的停工结果接分句
            var inPlace = new List<(string owner, string name, int n)>();
            var drops = new List<(string actor, string name, string place, int n)>();
            foreach (var i in due)
            {
                if (work.Contains(i.InstanceId)) continue;
                var (item, owner) = origin[i.InstanceId];
                if (NearPlaceOf(s, owner) != s.Ground) continue;
                var name = ItemName(item);
                if (dropped.TryGetValue(i.InstanceId, out var place))
                {
                    var actor = CarrierOf(owner)!;
                    var k = drops.FindIndex(x => x.actor == actor && x.name == name && x.place == place);
                    if (k < 0) drops.Add((actor, name, place, i.Count)); else drops[k] = (actor, name, place, drops[k].n + i.Count);
                }
                else
                {
                    var k = inPlace.FindIndex(x => x.owner == owner && x.name == name);
                    if (k < 0) inPlace.Add((owner, name, i.Count)); else inPlace[k] = (owner, name, inPlace[k].n + i.Count);
                }
            }
            foreach (var x in inPlace) lines.Add($"{x.name}×{x.n}已腐败，变为腐败物×{x.n}。");
            foreach (var x in drops) lines.Add($"{ActorDisplayName(s, x.actor)}的{x.name}×{x.n}已腐败，变为腐败物并放在{GroundPlaceText(s, x.place)}地面。");
            if (emit) foreach (var l in lines) Emit(s, GameEventKind.Info, l);
            return lines;
        }

        /// <summary>旧档迁移（存档 50→51）：一次转换全部到期物、不写逐物句；有转换时写一次迁移提示。返回是否有转换。</summary>
        internal bool MigrateSpoilage(GameState s)
        {
            if (!s.Items.Any(i => DueToSpoil(s, i))) return false;
            ConvertDueSpoilage(s, emit: false);
            Emit(s, GameEventKind.Info, SpoiledMigrationNote);
            return true;
        }

        /// <summary>转换一项工作的输入（停工结算前，静默）：只转这些实例里已到期的；返回其中是否有腐败物（用于接「腐败部分已变为腐败物。」）。</summary>
        private bool SpoilWorkInputs(GameState s, ICollection<int> ids)
        {
            ConvertDueSpoilage(s, ids, emit: false, dropWhenFull: false);
            return s.Items.Any(i => ids.Contains(i.InstanceId) && IsSpoiledMatter(i));
        }

        /// <summary>随身 owner 的持有者（主角 carried、同伴 carried:&lt;id&gt;）；不是随身为 null。</summary>
        private static string? CarrierOf(string owner)
        {
            if (owner == ItemInstance.Carried) return GameState.PlayerId;
            const string prefix = ItemInstance.Carried + ":";
            return owner.StartsWith(prefix) ? owner.Substring(prefix.Length) : null;
        }

        /// <summary>在办工作的输入：行动预留，或晾制中（含已腐败停止待清理）那一位里的原料。</summary>
        private bool WorkOwned(GameState s, ItemInstance i)
        {
            if (i.ReservedBy != null) return true;
            if (!ItemInstance.TryDrying(i.Owner, out var place, out var slot)) return false;
            var b = DryingSlotAt(s, place, slot);
            return b != null && b.Status != DryingSlot.Done;
        }

        /// <summary>
        /// 角色此刻真实所在的区（place）：主角为当前地面；同行同伴随主角；候补在其居所区；都不是（不在名册）为 null。
        /// </summary>
        private string? ActorPlaceOf(GameState s, string actorId)
        {
            if (actorId == GameState.PlayerId) return s.Ground;
            if (IsReserve(s, actorId)) return ReservePlace(s, actorId);
            if (s.Party.Contains(actorId)) return s.Ground;
            return null;
        }

        /// <summary>物品所在的区（place），用于判定是否在玩家身边：锅取锅所在；随身／穿戴取持有者所在；设施、晾制位取其区；屋柜取屋内；遗物箱等为 null。</summary>
        private string? NearPlaceOf(GameState s, string owner)
        {
            for (var depth = 0; depth < 4; depth++)
            {
                if (ItemInstance.TryContainerId(owner, out var potId))
                {
                    var pot = FindItem(s, potId);
                    if (pot == null) return null;
                    owner = pot.Owner;
                    continue;
                }
                if (CarrierOf(owner) is string actor) return ActorPlaceOf(s, actor);
                if (ItemInstance.TryEquip(owner, out var wearer, out _)) return ActorPlaceOf(s, wearer);
                if (ItemInstance.TryFacilityId(owner, out var fid)) return FacilityLocation(fid);
                if (ItemInstance.TryDrying(owner, out var dplace, out _)) return dplace;
                if (ItemInstance.TrySafehouseStorage(owner, out var shid)) return ItemInstance.SafehouseFloorOwner(shid);
                return IsGroundOwner(owner) ? owner : null;
            }
            return null;
        }

        /// <summary>「{地点}{区域后缀}」：已建安全屋分区的地点接「屋内」「屋外」，无分区省略（S06 Q610 随身满落地句）。</summary>
        private string GroundPlaceText(GameState s, string place)
        {
            if (!TryPlace(place, out var loc, out var zone)) return LocName(place);
            if (zone == SafehouseZone) return LocName(loc) + "屋内";
            return SafehouseBuilt(s, loc) ? LocName(loc) + "屋外" : LocName(loc);
        }
    }
}
