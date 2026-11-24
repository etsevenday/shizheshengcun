using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B59a「晾制架拖入即投料、取出收敛」（S11／S07／S14「2026-11-19 B51c2a2原批Q1028—Q1030」§Q1029；S11／S14「2026-11-24 Q1033–Q1039」§Q1033–Q1035；
    /// 合同 assets/20261119/b51c2a2-q1028-q1030-facility-interaction.json dryingRack；验收 B51C2A2-Q1029-01／02、Q1033-01／02、Q1034-01／02、Q1035-01）：
    /// <list type="bullet">
    /// <item>材料卡合法拖到晾制位即提交这次投入（取代 Q926 A 的只预览再确认）：只用手中这一堆（可堆叠卡按同组，Q606），
    /// 默认份数＝min(本位这项原料还缺的份数, 这堆真正可投的份数)，Shift 松手可在 1 至该上限选更少；不从别处自动配齐其他原料（Q1033 A）。</item>
    /// <item>空位首投按原配方兼容确定这一位的配方（用这件物品的唯一晾制配方）；已有原料的位只收本位配方还缺的原料。
    /// 未齐的原料留在位里（与部分取出留下的余料同一种「余料」位，Q1034），齐料即按原事务开始晾制（新批 ID、进度 0、经验快照、暂停保质）。</item>
    /// <item>非法物品、无容量（满位、在制、成品、停止位、本项原料已齐）或无操作资格的目标一律静默：不高亮、松手不投放、不扣物、不弹提示（Q1029）。</item>
    /// <item>空的异常停止位（及任何已无物品的非晾制中记录）自动恢复成普通空位（Q1029），见 <see cref="PruneEmptyDryingSlots"/>。</item>
    /// </list>
    /// B59b（S11「2026-11-20 Q769、Q770」§Q769、「2026-11-24」§Q1033「合法投入后，这次物品实际进入原位并冻结保鲜」、§Q1034「新投未齐和部分取出留下的真实余料继续冻结」；
    /// 验收 Q769-01／02、Q1033-02）：拖入的这次实物进位即冻结真实剩余保鲜量 R＝原截止−投入分钟（截止暂为空，晾制位口径），未齐也冻结；齐料开晾沿用这份 R、不刷新，
    /// 实际移出架子的份额才在那次成功移出时恢复 C＋R。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>
        /// 拖到晾制位的落点短动作（暂用文案，见开发记录 B59a.6：S07 D11 只给了「投入加工」「投入材料」等示例，未给晾制投入的短句）：「投入{物名}×{份数}」。
        /// </summary>
        public static string DryingDropLabel(string name, int count) => $"投入{name}×{count}";

        /// <summary>手中这一堆里真正可投进晾制位的实物（配批顺序）：可堆叠的随身／当前地面卡按同组（S06 Q606），其余只这一实例；未预留、未占用、未到期、不是腐败物。</summary>
        private List<ItemInstance> DryingDropStacks(GameState s, ItemInstance head) =>
            (Groupable(head) ? GroupOf(s, head) : new List<ItemInstance> { head })
                .Where(i => i.ReservedBy == null && !IsLocked(s, i) && !IsSpoiled(s, i) && !IsSpoiledMatter(i) && i.LoanSlot == null)
                .ToList();

        /// <summary>这条晾制配方玩家现在可用（已知、技能够）。</summary>
        private bool DryingRecipeOpen(GameState s, RecipeConfig rc) =>
            RecipeUnknownReason(s, rc) == null && rc.MinSkills.All(kv => SkillLevel(s, kv.Key) >= kv.Value);

        /// <summary>
        /// 物品 itemId 拖到当前区晾制位 k 时这一位接收的配方与本项还缺的份数；不接收为 null（静默）。
        /// 空位：用这件物品的晾制配方唯一可用时即为该配方（Q1033「空位首投按原配方兼容确定所投类型」；多条配方共用同一原料时没有已定规则，静默不投，见开发记录遗留）。
        /// 余料位（含新投未齐）：本位原配方、位内原料全部合法（Q924 B）、这项原料还没齐。在制、已晾好、停止位不接收。
        /// </summary>
        private RecipeConfig? DryingDropRecipe(GameState s, int k, string itemId, out int need)
        {
            need = 0;
            var b = DryingSlotAt(s, s.Ground, k);
            if (b == null)
            {
                var open = DryingRecipes.Where(r => r.Inputs.ContainsKey(itemId) && DryingRecipeOpen(s, r)).ToList();
                if (open.Count != 1) return null;
                need = open[0].Inputs[itemId];
                return open[0];
            }
            if (b.Status != DryingSlot.Residual || !_recipes.TryGetValue(b.RecipeId, out var rc) || !rc.Inputs.ContainsKey(itemId)) return null;
            if (!DryingRecipeOpen(s, rc) || !DryingRefillable(s, s.Ground, b, rc)) return null;
            need = rc.Inputs[itemId] - DryingContents(s, s.Ground, k).Where(i => i.ItemId == itemId).Sum(i => i.Count);
            return need > 0 ? rc : null;
        }

        /// <summary>
        /// 材料卡拖到晾制位（B59a，§Q1029／§Q1033 A）：合法即投——即时请求带位号、配方与默认份数（min(缺口, 这堆可投)），Shift 可选 1 至该上限；
        /// 其余一律静默（<see cref="DropIntent.Quiet"/>；ResolveDrop 对晾制位落点的所有拒绝也统一静默）。
        /// </summary>
        private DropIntent DropOnDryingSlot(GameState s, DropSource src, ItemInstance head, string id)
        {
            if (!int.TryParse(id, out var k) || k < 0 || k >= DryingSlotCount) return DropIntent.Quiet();
            if (DryingIdleBlocked(s) != null) return DropIntent.Quiet();
            if (src.Kind != DropSourceKind.Card || head.InFacility || head.InContainer) return DropIntent.Quiet();
            if (!(head.IsCarried || head.Owner == s.Ground)) return DropIntent.Quiet();
            var rc = DryingDropRecipe(s, k, head.ItemId, out var need);
            if (rc == null) return DropIntent.Quiet();
            var max = Math.Min(need, DryingDropStacks(s, head).Sum(i => i.Count));
            if (max < 1) return DropIntent.Quiet();
            var req = InstantRequest.DryingDrop(head.InstanceId, k, rc.Id, max);
            if (Groupable(head)) AsGroup(req, head);
            var intent = Instant(DropVerb.LoadDrying, DryingDropLabel(InstanceName(head), max), req, max);
            intent.DefaultQuantity = max;
            return intent;
        }

        /// <summary>
        /// 提交拖入晾制位（0 世界分钟、不开行动）：按松手时的实时状态重验（门槛、位、配方、缺口、这堆可投份数），不合法整次静默拒绝（原因为空，不弹提示）。
        /// 成功：从这一堆按配批顺序移 r.Count 份进位（空位新建余料记录，配方定为这一次的配方）；位内原料齐了就按原事务开始晾制。
        /// 不写常规入位日志（§Q1029「同步撤下常规晾制入位……重复播报」）。同一 requestId 只生效一次（DoInstant）。
        /// </summary>
        private ActionResult LoadDryingByDrop(GameState s, InstantRequest r)
        {
            if (DryingIdleBlocked(s) != null) return ActionResult.Fail(SlotQuietReason);
            var k = r.Slot ?? -1;
            if (k < 0 || k >= DryingSlotCount) return ActionResult.Fail(SlotQuietReason);
            var head = r.Group && GroupKeyOf(s, r) is var (gOwner, gItem, gSide)
                ? GroupStacks(s, gOwner, gItem, gSide).FirstOrDefault()
                : FindItem(s, r.InstanceId);
            if (head == null || head.InFacility || head.InContainer || !(head.IsCarried || head.Owner == s.Ground)) return ActionResult.Fail(SlotQuietReason);
            var rc = DryingDropRecipe(s, k, head.ItemId, out var need);
            if (rc == null || rc.Id != r.CardKey) return ActionResult.Fail(SlotQuietReason);
            var stacks = DryingDropStacks(s, head);
            var max = Math.Min(need, stacks.Sum(i => i.Count));
            if (r.Count < 1 || r.Count > max) return ActionResult.Fail(SlotQuietReason, max);
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            var owner = ItemInstance.DryingOwner(place, k);
            var b = DryingSlotAt(s, place, k);
            if (b == null)
            {
                b = new DryingSlot
                {
                    Slot = k, BatchId = s.NextActionId++, RecipeId = rc.Id, Status = DryingSlot.Residual, StartMinute = s.GameMinute,
                    RequiredMinutes = rc.BaseMinutes, InitiatorActorId = GameState.PlayerId,
                };
                fac.DryingSlots.Add(b);
            }
            foreach (var (inst, n) in PlanGroup(stacks, r.Count))
            {
                var moved = n < inst.Count ? SplitOff(s, inst, n) : inst;
                moved.Owner = owner;
                moved.PlacedOrder = null;
                // B59b（§Q769、§Q1033「这次物品实际进入原位并冻结保鲜」）：未齐也冻结真实余量，位内时间推进不扣
                FreezeShelfAt(moved, s.GameMinute, clearExpiry: true);
            }
            Normalize(s, ItemInstance.Carried);
            Normalize(s, place);
            if (DryingInputsComplete(s, place, k, rc)) BeginDryingInPlace(s, place, b, rc);
            UpdateFlags(s);
            return ActionResult.Success(b.BatchId);
        }

        /// <summary>这一位里每种原料都已够配方所需。</summary>
        private bool DryingInputsComplete(GameState s, string place, int k, RecipeConfig rc)
        {
            var items = DryingContents(s, place, k);
            return rc.Inputs.All(kv => items.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) >= kv.Value);
        }

        /// <summary>
        /// 齐料即按原事务开始晾制（同 <see cref="StartDrying"/> 的开批：新批 ID、进度 0、开始分钟、经验快照、投料者；Q215／Q666 暂停位内原料保质 R＝E−S），
        /// 复用这一位，不继承已失效的进度（§Q1029「匹配新一批时不继承已失效的进度」）。B59b（§Q769「重新补足后开的是新批……保鲜量不刷新」）：位内已冻结的原料沿用原 R。
        /// </summary>
        private void BeginDryingInPlace(GameState s, string place, DryingSlot b, RecipeConfig rc)
        {
            var pause = DryingPausesShelf(rc);
            foreach (var it in DryingContents(s, place, b.Slot))
            {
                it.ReservedBy = null;
                if (pause && it.ExpiresAt is int e)
                {
                    it.ShelfPausedMinutes = e - s.GameMinute;
                    it.ExpiresAt = null;
                }
            }
            b.BatchId = s.NextActionId++;
            b.RecipeId = rc.Id;
            b.Status = DryingSlot.Drying;
            b.StartMinute = s.GameMinute;
            b.DoneMinutes = 0;
            b.RequiredMinutes = rc.BaseMinutes;
            b.EndMinute = null;
            b.StopReason = null;
            b.XpNote = null;
            b.InitiatorActorId = GameState.PlayerId;
            b.XpSnapshot = DryingXpFor(rc);
            b.ShelfPaused = pause;
        }

        /// <summary>
        /// §Q1029「空的异常停止位自动恢复成普通空位，无须玩家操作」「最后一件物品离开后回到可投放状态」：
        /// 各区晾制架上已没有物品、又不在晾制中的位记录（停止位、余料位、已晾好位）直接删掉，成普通空位。晾制中的空位仍由世界段按原规则停止。
        /// 每个变更边界（<see cref="UpdateFlags"/>）与世界段推进后各做一次，读档后的旧档空异常位也随之恢复。
        /// </summary>
        private void PruneEmptyDryingSlots(GameState s)
        {
            foreach (var (place, fac) in AllFacilityStates(s))
            {
                if (fac.DryingSlots.Count == 0) continue;
                fac.DryingSlots.RemoveAll(b => b.Status != DryingSlot.Drying
                                               && !s.Items.Any(i => i.Owner == ItemInstance.DryingOwner(place, b.Slot)));
            }
        }

        /// <summary>
        /// 晾制位收起时的标题（§Q1034 用户另定：占用位未展开时以展开列表里第一件实物的名称接「等」，逐字「{首件物品名}等」）：
        /// 展开顺序同 <see cref="DryingContents"/>（实例号升序）；空位为 null（沿原空槽）。
        /// </summary>
        public string? DryingCollapsedTitle(GameState s, string place, int slot) =>
            DryingContents(s, place, slot).FirstOrDefault() is ItemInstance first ? $"{ItemName(first.ItemId)}等" : null;
    }
}
