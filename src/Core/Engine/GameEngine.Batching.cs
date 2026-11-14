using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 自动配批（BM5b2，S06 文首 Q606、Q643；S14 Q642）：玩家只选物品组与总数，系统在同一来源组（同 owner：随身／当前区地面／同容器）、
    /// 同物品、同卡侧（CardSideOf：已鉴定、首战教学、地牢战利品、用过的修补包等原分侧身份，普通为 null）内，只取合法未占用的实物，
    /// 按最早有效到期升序（无期限在后，同期限按实例号）逐批填到总数，末批只拆实际缺额。预估只读；提交按同组实时实物重新配批，
    /// 全量与最终容量（五张根卡、30 公斤）校验后一次原子执行，不足或放不下整次拒绝，不跨侧、不缩量、不先转一部分。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>
        /// 同组可用实物（配批顺序）：同 owner、同物品、同卡侧，未预留、未被行动锁定、未部署、未到期待转换（到期等于当前分钟视为已到期）。
        /// </summary>
        public List<ItemInstance> GroupStacks(GameState s, string owner, string itemId, string? side) =>
            s.Items.Where(i => i.Owner == owner && i.ItemId == itemId && CardSideOf(i) == side && i.ReservedBy == null
                               && !IsLocked(s, i) && !IsDeployed(s, i) && !DueToSpoil(s, i))
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).ToList();

        /// <summary>这一实例所在的组（同 owner／物品／卡侧）。</summary>
        public List<ItemInstance> GroupOf(GameState s, ItemInstance anchor) => GroupStacks(s, anchor.Owner, anchor.ItemId, CardSideOf(anchor));

        /// <summary>这一实例所在组的可用总数。</summary>
        public int GroupCount(GameState s, ItemInstance anchor) => GroupOf(s, anchor).Sum(i => i.Count);

        /// <summary>按配批顺序取 count 份：逐批填满，末批只取缺额；不足时返回能取到的全部（调用方先校验总数）。</summary>
        public static List<(ItemInstance inst, int n)> PlanGroup(IEnumerable<ItemInstance> stacks, int count)
        {
            var plan = new List<(ItemInstance, int)>();
            var left = count;
            foreach (var st in stacks)
            {
                if (left <= 0) break;
                var n = Math.Min(left, st.Count);
                plan.Add((st, n));
                left -= n;
            }
            return plan;
        }

        /// <summary>把一组（实例, 份数）放进某角色随身后的根卡数（只读预演，口径同 RootCardsAfterOf）。</summary>
        public int RootCardsAfterPlan(GameState s, string actorId, IEnumerable<(ItemInstance inst, int n)> adds)
        {
            var owner = ItemInstance.CarriedOwner(actorId);
            var stacks = new Dictionary<(string, int?, string?), (int n, int cap)>();
            var singles = 0;
            foreach (var i in s.Items.Where(i => i.Owner == owner))
            {
                if (StacksAsBatch(i)) AddStack(stacks, i, i.Count);
                else singles++;
            }
            foreach (var (a, n) in adds)
            {
                if (n <= 0 || a.Owner == owner) continue;
                if (StacksAsBatch(a)) AddStack(stacks, a, n);
                else singles++;
            }
            return singles + CardsOfStacks(stacks);
        }

        /// <summary>从某角色随身拿走一组（实例, 份数）后的根卡数（只读预演）。</summary>
        public int RootCardsAfterRemovePlan(GameState s, string actorId, IEnumerable<(ItemInstance inst, int n)> removes)
        {
            var owner = ItemInstance.CarriedOwner(actorId);
            var take = new Dictionary<int, int>();
            foreach (var (a, n) in removes) take[a.InstanceId] = (take.TryGetValue(a.InstanceId, out var x) ? x : 0) + n;
            var stacks = new Dictionary<(string, int?, string?), (int n, int cap)>();
            var singles = 0;
            foreach (var i in s.Items.Where(i => i.Owner == owner))
            {
                var left = i.Count - (take.TryGetValue(i.InstanceId, out var t) ? t : 0);
                if (left <= 0) continue;
                if (StacksAsBatch(i)) AddStack(stacks, i, left);
                else singles++;
            }
            return singles + CardsOfStacks(stacks);
        }

        /// <summary>一组（实例, 份数）的合计克重（整堆按 ItemGrams，部分按每份克重）。</summary>
        public int PlanGrams(GameState s, IEnumerable<(ItemInstance inst, int n)> plan) =>
            plan.Sum(x => x.n >= x.inst.Count ? ItemGrams(s, x.inst) : UnitGrams(x.inst) * x.n);

        /// <summary>拾取这组的前 n 份：按根卡是否允许（旧档已超限时不得再增加）。</summary>
        private bool GroupCardsOk(GameState s, List<ItemInstance> stacks, int n) =>
            n <= 0 || RootCardsAllow(s, RootCardsAfterPlan(s, GameState.PlayerId, PlanGroup(stacks, n)));

        /// <summary>拾取这组的前 n 份：按 30 公斤是否允许（每份按实例真实克重）。</summary>
        private bool GroupWeightOk(GameState s, List<ItemInstance> stacks, int n) =>
            n <= 0 || CarriedGrams(s) + PlanGroup(stacks, n).Sum(x => x.n >= x.inst.Count ? ItemGrams(s, x.inst) : UnitGrams(x.inst) * x.n) <= Config.Carry.HardGrams;

        private static int MaxMonotone(int upTo, Func<int, bool> ok)
        {
            var lo = 0;
            var hi = upTo;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (ok(mid)) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>这组最多能拾取几份到随身（根卡与重量同时约束，按跨批最终结果预演）。</summary>
        public int MaxGroupPickup(GameState s, string owner, string itemId, string? side, int? upTo = null)
        {
            var stacks = GroupStacks(s, owner, itemId, side);
            var total = Math.Min(upTo ?? int.MaxValue, stacks.Sum(i => i.Count));
            return MaxMonotone(total, n => GroupCardsOk(s, stacks, n) && GroupWeightOk(s, stacks, n));
        }

        /// <summary>想拾取这组 want 份但超限时的原因（句式同 <see cref="PickupBlockedText"/>：先报根卡，再报重量）；都满足为 null。</summary>
        public string? GroupPickupBlockedText(GameState s, string owner, string itemId, string? side, int want)
        {
            var stacks = GroupStacks(s, owner, itemId, side);
            var total = Math.Min(want, stacks.Sum(i => i.Count));
            var byCards = MaxMonotone(total, n => GroupCardsOk(s, stacks, n));
            if (byCards < want) return byCards <= 0 ? RootCardsFullText(s) : $"{RootCardsFullText(s)}；最多可取{byCards}份";
            var max = MaxMonotone(total, n => GroupCardsOk(s, stacks, n) && GroupWeightOk(s, stacks, n));
            if (max < want) return max <= 0 ? $"携带后将超过{Config.Carry.HardGrams / 1000}公斤，拿不动了" : $"携带后将超过{Config.Carry.HardGrams / 1000}公斤，最多可取{max}份";
            return null;
        }

        /// <summary>请求里的组（Group=true 时按 GroupOwner／GroupItemId／GroupSide，缺省时按组首实例）。</summary>
        private (string owner, string itemId, string? side)? GroupKeyOf(GameState s, InstantRequest r)
        {
            if (r.GroupOwner != null && r.GroupItemId != null) return (r.GroupOwner, r.GroupItemId, r.GroupSide);
            return FindItem(s, r.InstanceId) is ItemInstance a ? (a.Owner, a.ItemId, CardSideOf(a)) : null;
        }

        /// <summary>同组自动配批的拾取／放下（Q606）：先做到期转换，再按实时实物配批；全量与最终容量通过才一次移动，失败什么都不变。</summary>
        private ActionResult TransferGroup(GameState s, InstantRequest r, bool toCarried)
        {
            if (IsBusy(s) && !ExtractTidyAllowed(s)) return ActionResult.Fail("行动进行中，不能整理物品");
            var here = s.Ground;
            if (GroupKeyOf(s, r) is not var (owner, itemId, side)) return ActionResult.Fail("物品已不在");
            if (toCarried && owner != here) return ActionResult.Fail("只能拾取当前地点地面的物品");
            if (!toCarried && owner != ItemInstance.Carried) return ActionResult.Fail("只能放下随身物品");
            // Q606-A02：确认前已到期的早批先按 Q610 转换，再用同组其他合格批次配够
            ConvertDueSpoilage(s);
            var stacks = GroupStacks(s, owner, itemId, side);
            var total = stacks.Sum(i => i.Count);
            if (total <= 0) return ActionResult.Fail("物品已不在");
            if (r.Count < 1 || r.Count > total) return ActionResult.Fail("数量无效", total);
            if (toCarried && GroupPickupBlockedText(s, owner, itemId, side, r.Count) is string blocked)
                return ActionResult.Fail(blocked, MaxGroupPickup(s, owner, itemId, side, r.Count));
            var to = toCarried ? ItemInstance.Carried : here;
            foreach (var (inst, n) in PlanGroup(stacks, r.Count)) MoveItems(s, inst, n, to);
            NoteTransfer(s);
            UpdateFlags(s);
            return ActionResult.Success();
        }
    }
}
