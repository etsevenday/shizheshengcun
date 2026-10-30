using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 七个穿戴槽（S06 2026-10-30 全系统更新目标 §1、R02 §1/§5、A05 §5；开发计划 v0.9 B3 H2–H5、H8）：
    /// 穿戴 owner = "equip:&lt;actorId&gt;:&lt;槽&gt;"，不占五张物资卡，重量计本人；两武器槽同 kind 最多 1 件。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string WornMoveBlocked = "穿戴中：先在装备区卸下";

        /// <summary>某角色某槽当前穿戴的物品；空槽为 null（同槽多件只会出现在损坏数据里，取实例号最小者）。</summary>
        public ItemInstance? WornItem(GameState s, string actorId, string slot)
        {
            var owner = ItemInstance.EquipOwner(actorId, slot);
            return s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId).FirstOrDefault();
        }

        /// <summary>某角色全部穿戴物（按七槽顺序）。</summary>
        public List<ItemInstance> WornItems(GameState s, string actorId = GameState.PlayerId) =>
            EquipSlots.All.Select(slot => WornItem(s, actorId, slot)).Where(i => i != null).Select(i => i!).ToList();

        /// <summary>物品的武器类型（A05 kind）；不是武器为 null。</summary>
        public string? WeaponKindOf(string itemId) => Weapon(itemId)?.WeaponKind;

        /// <summary>另一个武器槽（weapon1↔weapon2）。</summary>
        public static string OtherWeaponSlot(string slot) => slot == EquipSlots.Weapon1 ? EquipSlots.Weapon2 : EquipSlots.Weapon1;

        // ───────────── 换装时机 ─────────────

        /// <summary>
        /// 战外换装的时机拦截（沿用旧 EquipWeapon：死亡、未读对白、战斗中不可；B3 H5）。战斗中两把武器已锁定，只能切页（A05 §5、A10 §4.2）。
        /// 只有同行角色可换装（本批只有主角）。
        /// </summary>
        public string? EquipTimingBlocked(GameState s, string actorId)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中两把武器已锁定，只能切换武器页；换装请在战后进行";
            if (!s.Party.Contains(actorId)) return "只能为同行的角色换装";
            return null;
        }

        // ───────────── 穿上 ─────────────

        /// <summary>
        /// 能否把本人随身五卡里的这件物品穿进该槽（只读，按最终状态一次校验；B3 H5）：只从本人五卡穿上（不从地面直接穿，Q40 待确认）；
        /// 本批只有武器能进 weapon1/weapon2；同 kind 已在另一武器槽 →“已装备同类型武器：名称”；目标槽已有物品则交换回五卡，
        /// 交换后五卡与 30 公斤硬线都要合法；预留或行动占用的实例不可穿。
        /// </summary>
        public string? EquipBlocked(GameState s, string actorId, int instanceId, string slot)
        {
            var t = EquipTimingBlocked(s, actorId);
            if (t != null) return t;
            if (!EquipSlots.IsValid(slot)) return "没有这个装备槽";
            var it = FindItem(s, instanceId);
            if (it == null) return "物品已不在";
            if (it.IsWornBy(actorId))
                return ItemInstance.TryEquip(it.Owner, out _, out var cur) && cur == slot ? $"已经装备在{EquipSlots.Name(slot)}" : $"已装备在{EquipSlots.Name(cur)}，先卸下";
            if (it.Owner != ItemInstance.CarriedOwner(actorId))
                return it.Owner == s.CurrentLocation ? "只能从本人随身物资卡穿上：先拾取到随身" : "只能从本人随身物资卡穿上";
            var w = Weapon(it.ItemId);
            if (!EquipSlots.IsWeaponSlot(slot))
                return $"{EquipSlots.Name(slot)}槽暂未开放：本批只有武器能穿戴（防具、饰品与衣物在后续批次）";
            if (w == null || w.IsFists) return $"{ItemName(it.ItemId)}不是武器，不能放进{EquipSlots.Name(slot)}";
            if (it.ReservedBy != null) return "该物品已被预留";
            if (IsLocked(s, it)) return "行动占用：该物品正被当前行动使用";
            var other = WornItem(s, actorId, OtherWeaponSlot(slot));
            if (other != null && WeaponKindOf(other.ItemId) == w.WeaponKind)
                return $"已装备同类型武器：{ItemName(other.ItemId)}（{EquipSlots.Name(OtherWeaponSlot(slot))}，{WeaponKinds.Name(w.WeaponKind)}类最多1件）";
            var old = WornItem(s, actorId, slot);
            if (old != null)
            {
                if (old.ReservedBy != null || IsLocked(s, old)) return $"{EquipSlots.Name(slot)}里的{ItemName(old.ItemId)}正被行动使用，不能换下";
                // 交换：新武器离开五卡、旧武器回五卡，按交换后的最终卡数校验（不因中间瞬态误拒）
                var after = RootCardsAfter(s, old, 1, it, it.Count);
                if (!RootCardsAllow(s, after))
                    return $"随身卡位不够换下{ItemName(old.ItemId)}（换后{after}/{MaxRootCards}张）：先卸下到当前地面或腾出一张卡";
            }
            // 30 公斤硬线：穿上与交换都发生在本人身上（随身↔穿戴），最终总重与现在相同，不会越线（旧档已超重的也不因换装增加）
            return null;
        }

        /// <summary>把本人随身五卡里的物品穿进该槽（0 时间、原子；失败不改任何状态；带 requestId 幂等）。目标槽原有物品回本人五卡。</summary>
        public ActionResult Equip(GameState s, string actorId, int instanceId, string slot, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = EquipBlocked(s, actorId, instanceId, slot);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var it = FindItem(s, instanceId)!;
            var old = WornItem(s, actorId, slot);
            var carried = ItemInstance.CarriedOwner(actorId);
            if (old != null)
            {
                old.Owner = carried;
                old.PlacedOrder = null;
            }
            it.Owner = ItemInstance.EquipOwner(actorId, slot);
            it.PlacedOrder = null;
            Normalize(s, carried);
            var name = $"{ItemName(it.ItemId)}#{it.InstanceId}";
            Emit(s, GameEventKind.Info, old == null
                ? $"装备{name}到{EquipSlots.Name(slot)}。"
                : $"装备{name}到{EquipSlots.Name(slot)}，换下的{ItemName(old.ItemId)}#{old.InstanceId}回到随身。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        // ───────────── 卸下 ─────────────

        /// <summary>
        /// 能否卸下该槽（只读）：toGround=false 卸进本人五卡，卡位不够时拒绝并提示可选“放当前地面”（不自动丢）；
        /// toGround=true 明确放当前地面（与放下随身物品同样要求无进行中的行动）。
        /// </summary>
        public string? UnequipBlocked(GameState s, string actorId, string slot, bool toGround)
        {
            var t = EquipTimingBlocked(s, actorId);
            if (t != null) return t;
            if (!EquipSlots.IsValid(slot)) return "没有这个装备槽";
            var it = WornItem(s, actorId, slot);
            if (it == null) return $"{EquipSlots.Name(slot)}是空的";
            if (it.ReservedBy != null || IsLocked(s, it)) return "行动占用：该物品正被当前行动使用";
            if (toGround)
            {
                if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
                if (IsBusy(s)) return "行动进行中，不能放到地面";
                return null;
            }
            var after = RootCardsAfter(s, it, 1, null, 0);
            if (!RootCardsAllow(s, after))
                return $"随身卡位已满（{RootCardCount(s)}/{MaxRootCards}张）：卸下会多出一张卡，可以选择放当前地面";
            return null;
        }

        /// <summary>卸下该槽物品到本人五卡或当前地面（0 时间、原子；带 requestId 幂等）。</summary>
        public ActionResult Unequip(GameState s, string actorId, string slot, bool toGround, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = UnequipBlocked(s, actorId, slot, toGround);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var it = WornItem(s, actorId, slot)!;
            var dest = toGround ? s.CurrentLocation : ItemInstance.CarriedOwner(actorId);
            it.Owner = dest;
            it.PlacedOrder = null;
            Normalize(s, dest);
            Emit(s, GameEventKind.Info, $"卸下{EquipSlots.Name(slot)}的{ItemName(it.ItemId)}#{it.InstanceId}，{(toGround ? $"放在{LocName(s.CurrentLocation)}地面" : "放回随身")}。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>这件随身武器能装到哪些武器槽：每槽给出不可用原因（界面“装备到武器Ⅰ/武器Ⅱ”按钮用）。</summary>
        public List<(string slot, string? reason)> EquipChoices(GameState s, string actorId, int instanceId) =>
            EquipSlots.Weapons.Select(slot => (slot, EquipBlocked(s, actorId, instanceId, slot))).ToList();
    }
}
