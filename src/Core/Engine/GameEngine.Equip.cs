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

        /// <summary>该实例是否在当前地点地面（顶层，不在锅里、设施里、别处或他人身上；Q40 只认当前实际可访问的地面）。</summary>
        public static bool OnCurrentGround(GameState s, ItemInstance it) => it.Owner == s.CurrentLocation;

        /// <summary>
        /// 地面取放的时机（Q40/Q42 选定：地面→穿戴、旧件放当前地面沿原地面操作条件）：与拾取/放下相同，要求无敌踪待选、无主/子行动（含暂停）。
        /// 死亡、未读对白、战斗由 EquipTimingBlocked 先拦。
        /// </summary>
        public string? GroundEquipBlocked(GameState s, bool fromGround)
        {
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (s.Main != null || s.Sub != null) return fromGround ? "行动进行中，不能从地面穿上" : "行动进行中，不能放到地面";
            return null;
        }

        /// <summary>
        /// 穿上后的最终状态（只读预演，Q40 选 B“按最终个人总重量/卡位一次校验”）：本人总重（随身+穿戴+弹仓+容器后代，各计一次）与随身根卡数。
        /// 来源是本人随身时新件离开五卡；来源是当前地面时新件不经五卡、重量加到本人；旧件按 oldToGround 回本人随身或放当前地面。
        /// </summary>
        public (int Grams, int Cards) EquipOutcome(GameState s, string actorId, ItemInstance it, string slot, bool oldToGround)
        {
            var old = WornItem(s, actorId, slot);
            var fromCarried = it.Owner == ItemInstance.CarriedOwner(actorId);
            var grams = CarriedGramsOf(s, actorId);
            if (!fromCarried && !it.IsWornBy(actorId)) grams += ItemGrams(s, it);
            if (old != null && oldToGround) grams -= ItemGrams(s, old);
            var cards = RootCardsAfter(s, old != null && !oldToGround ? old : null, 1, fromCarried ? it : null, fromCarried ? it.Count : 0);
            return (grams, cards);
        }

        /// <summary>
        /// 能否把这件物品穿进该槽（只读，按最终状态一次校验；B3 H5、B3a H1/H2）：来源是本人随身五卡，或当前地点地面（Q40 选 B，地面直穿，
        /// 不先临时进五卡）；槽位按物品 equipSlot（B4 H2：七槽全开放）；同 kind 已在另一武器槽 →“已装备同类型武器：名称”；新旧件都不能被预留或行动占用。
        /// 目标槽已有物品：oldToGround=false 旧件回本人随身（默认），按最终卡数校验，放不下时提示可选“旧件放当前地面”；oldToGround=true 是玩家明确选的放当前地面。
        /// 涉及地面（地面来源或旧件放地）时沿拾取/放下的地面操作条件。最终本人总重不得超过 30 公斤硬线（旧档已超重的不得再增加）。
        /// </summary>
        public string? EquipBlocked(GameState s, string actorId, int instanceId, string slot, bool oldToGround = false)
        {
            var t = EquipTimingBlocked(s, actorId);
            if (t != null) return t;
            if (!EquipSlots.IsValid(slot)) return "没有这个装备槽";
            var it = FindItem(s, instanceId);
            if (it == null) return "物品已不在";
            if (it.IsWornBy(actorId))
                return ItemInstance.TryEquip(it.Owner, out _, out var cur) && cur == slot ? $"已经装备在{EquipSlots.Name(slot)}" : $"已装备在{EquipSlots.Name(cur)}，先卸下";
            var fromGround = OnCurrentGround(s, it);
            if (it.Owner != ItemInstance.CarriedOwner(actorId) && !fromGround)
                return it.IsWorn ? "穿在别人身上，不能直接穿上" : "只能穿上本人随身或当前地面的装备：其他地方的先拿到当前地点";
            var w = Weapon(it.ItemId);
            // B4 H2：七槽全开放，按物品的 equipSlot 匹配（武器→武器Ⅰ/Ⅱ，防具、战斗饰品、上衣、下衣、求生饰品各一槽）
            var allowed = SlotsFor(it.ItemId);
            if (!allowed.Contains(slot))
            {
                if (allowed.Count == 0)
                    return EquipSlots.IsWeaponSlot(slot) ? $"{ItemName(it.ItemId)}不是武器，不能放进{EquipSlots.Name(slot)}" : $"{ItemName(it.ItemId)}不是装备，不能穿戴";
                return $"{ItemName(it.ItemId)}只能装备到{string.Join("或", allowed.Select(EquipSlots.Name))}";
            }
            if (it.ReservedBy != null) return "该物品已被预留";
            if (IsLocked(s, it)) return "行动占用：该物品正被当前行动使用";
            if (it.Count != 1) return "一次只能穿上一件";
            var old = WornItem(s, actorId, slot);
            if (fromGround || (old != null && oldToGround))
            {
                var g = GroundEquipBlocked(s, fromGround);
                if (g != null) return g;
            }
            var other = EquipSlots.IsWeaponSlot(slot) ? WornItem(s, actorId, OtherWeaponSlot(slot)) : null;
            if (other != null && w != null && WeaponKindOf(other.ItemId) == w.WeaponKind)
                return $"已装备同类型武器：{ItemName(other.ItemId)}（{EquipSlots.Name(OtherWeaponSlot(slot))}，{WeaponKinds.Name(w.WeaponKind)}类最多1件）";
            var (grams, cards) = EquipOutcome(s, actorId, it, slot, oldToGround);
            if (old != null)
            {
                if (old.ReservedBy != null || IsLocked(s, old)) return $"{EquipSlots.Name(slot)}里的{ItemName(old.ItemId)}正被行动使用，不能换下";
                // 交换：按交换后的最终卡数校验（不因中间瞬态误拒，也不因“先拾取会六张”误拒）
                if (!oldToGround && !RootCardsAllow(s, cards))
                {
                    // 只在“旧件放当前地面”本身合法时才提这个选项（B3a 试玩修复 2：会超 30 公斤、行动中等情况不再误导）
                    var alt = EquipBlocked(s, actorId, instanceId, slot, oldToGround: true);
                    return $"随身卡位不够换下{ItemName(old.ItemId)}（换后{cards}/{MaxRootCards}张）：" +
                           (alt == null ? "可以选择旧件放当前地面，或先腾出一张卡" : $"请先腾出一张卡（旧件放当前地面也不行：{alt}）");
                }
            }
            // 30 公斤硬线按最终本人总重判断：随身↔穿戴不改总重；地面直穿加上新件、旧件放地减去旧件（旧档已超重的不得再增加）
            var hard = Config.Carry.HardGrams;
            if (grams > hard && grams > CarriedGramsOf(s, actorId))
                return $"穿上后携带{Fmt.N(grams / 1000.0)}公斤，将超过{hard / 1000}公斤";
            return null;
        }

        /// <summary>
        /// 把本人随身五卡或当前地面上的物品穿进该槽（0 时间、原子：全部校验通过才一次改 owner，失败不改任何状态；带 requestId 幂等）。
        /// 目标槽原有物品默认回本人五卡，oldToGround=true 时放当前地面（玩家明确选择）。不复制、不重抽实例属性。
        /// </summary>
        public ActionResult Equip(GameState s, string actorId, int instanceId, string slot, string? requestId = null, bool oldToGround = false)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = EquipBlocked(s, actorId, instanceId, slot, oldToGround);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var it = FindItem(s, instanceId)!;
            var fromGround = OnCurrentGround(s, it);
            var old = WornItem(s, actorId, slot);
            var carried = ItemInstance.CarriedOwner(actorId);
            var here = s.CurrentLocation;
            if (old != null)
            {
                old.Owner = oldToGround ? here : carried;
                old.PlacedOrder = null;
            }
            it.Owner = ItemInstance.EquipOwner(actorId, slot);
            it.PlacedOrder = null;
            Normalize(s, carried);
            if (fromGround || (old != null && oldToGround)) Normalize(s, here);
            var name = $"{ItemName(it.ItemId)}#{it.InstanceId}";
            var src = fromGround ? $"从{LocName(here)}地面" : "";
            var oldText = old == null ? "" : $"，换下的{ItemName(old.ItemId)}#{old.InstanceId}{(oldToGround ? $"放在{LocName(here)}地面" : "回到随身")}";
            Emit(s, GameEventKind.Info, $"{src}装备{name}到{EquipSlots.Name(slot)}{oldText}。");
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
            {
                // 按实际能否放地写（B3a 试玩修复 3：行动中、敌踪待选时没有放地按钮）
                var g = UnequipBlocked(s, actorId, slot, true);
                return $"随身卡位已满（{RootCardCount(s)}/{MaxRootCards}张）：卸下会多出一张卡，" +
                       (g == null ? "可以选择放当前地面" : $"请先腾出一张卡（现在不能放当前地面：{g}）");
            }
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

        // ───────────── 旧档非法同类双武器整理（各案“同类型武器限制”末句、B3 H8、COMP08） ─────────────

        /// <summary>两武器槽里同 kind 的武器（非法时为 2 件，按槽序）；合法为空表。</summary>
        public List<ItemInstance> DuplicateKindWeapons(GameState s, string actorId = GameState.PlayerId)
        {
            var worn = EquipSlots.Weapons.Select(slot => WornItem(s, actorId, slot)).Where(i => i != null && Weapon(i.ItemId) != null).Select(i => i!).ToList();
            return worn.GroupBy(i => WeaponKindOf(i.ItemId)).Where(g => g.Key != null && g.Count() > 1).SelectMany(g => g).ToList();
        }

        public bool DuplicateWeaponsIllegal(GameState s, string actorId = GameState.PlayerId) => DuplicateKindWeapons(s, actorId).Count > 1;

        /// <summary>
        /// 只读预演整理：保留 keep，其余每件按玩家明确选择进本人五卡（false）或放当前地面（true）；未选去向、卡位超限、时机不安全都给原因。
        /// </summary>
        public string? ResolveDuplicateWeaponsBlocked(GameState s, string actorId, int keepInstanceId, IDictionary<int, bool> toGround)
        {
            var dup = DuplicateKindWeapons(s, actorId);
            if (dup.Count < 2) return "两把武器类型不同，不需要整理";
            if (!OverflowResolvable(s)) return "现在不能整理装备：请先结束战斗、行动或对话";
            if (dup.All(i => i.InstanceId != keepInstanceId)) return "请选择要保留的那把武器";
            var others = dup.Where(i => i.InstanceId != keepInstanceId).ToList();
            foreach (var o in others)
            {
                if (!toGround.ContainsKey(o.InstanceId)) return $"请为{ItemName(o.ItemId)}选择去向：进本人随身或放当前地面";
                if (o.ReservedBy != null || IsLocked(s, o)) return "行动占用：有武器正被行动使用";
            }
            var intoCards = others.Where(o => !toGround[o.InstanceId]).ToList();
            if (intoCards.Count > 0)
            {
                // 最终状态：这些武器各占一张根卡（武器不合堆），按整次结果校验
                var after = RootCardCount(s) + intoCards.Count;
                if (!RootCardsAllow(s, after)) return $"随身卡位不够（整理后{after}/{MaxRootCards}张）：请改为放当前地面或先腾出卡位";
            }
            return null;
        }

        /// <summary>
        /// 原子整理非法同类双武器：保留一把，其余各自进本人五卡或放当前地面（玩家明确选择，默认不勾，同 Q27）；按最终状态校验，失败不改任何状态；
        /// 不删除、不重抽属性。仅安全空闲可用（同超五卡整理）；带 requestId 幂等。
        /// </summary>
        public ActionResult ResolveDuplicateWeapons(GameState s, string actorId, int keepInstanceId, IDictionary<int, bool> toGround, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = ResolveDuplicateWeaponsBlocked(s, actorId, keepInstanceId, toGround);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var keep = FindItem(s, keepInstanceId)!;
            var parts = new List<string>();
            foreach (var o in DuplicateKindWeapons(s, actorId).Where(i => i.InstanceId != keepInstanceId))
            {
                var ground = toGround[o.InstanceId];
                o.Owner = ground ? s.CurrentLocation : ItemInstance.CarriedOwner(actorId);
                o.PlacedOrder = null;
                parts.Add($"{ItemName(o.ItemId)}#{o.InstanceId}{(ground ? $"放在{LocName(s.CurrentLocation)}地面" : "放回随身")}");
            }
            Normalize(s, s.CurrentLocation);
            Normalize(s, ItemInstance.CarriedOwner(actorId));
            Emit(s, GameEventKind.Info, $"整理装备：保留{ItemName(keep.ItemId)}#{keep.InstanceId}，{string.Join("，", parts)}（同类型武器最多装备1件）。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 这件随身或当前地面的装备能装到哪些槽：武器为武器Ⅰ/Ⅱ，其余为各自一槽（B4 H2）；每槽给出不可用原因（旧件默认回随身；界面“装备到××”按钮用）。
        /// 不是装备为空表。
        /// </summary>
        public List<(string slot, string? reason)> EquipChoices(GameState s, string actorId, int instanceId)
        {
            var it = FindItem(s, instanceId);
            if (it == null) return new List<(string, string?)>();
            return SlotsFor(it.ItemId).Select(slot => (slot, EquipBlocked(s, actorId, instanceId, slot))).ToList();
        }

        /// <summary>物品能进的穿戴槽（按配置 equipSlot；武器=武器Ⅰ/Ⅱ；拳与非装备为空）。</summary>
        public List<string> SlotsFor(string itemId)
        {
            var cfg = _items.TryGetValue(itemId, out var c) ? c : null;
            if (cfg?.EquipSlot == null) return new List<string>();
            if (cfg.EquipSlot == "weapon") return Weapon(itemId) is WeaponConfig w && !w.IsFists ? EquipSlots.Weapons.ToList() : new List<string>();
            return EquipSlots.IsValid(cfg.EquipSlot) ? new List<string> { cfg.EquipSlot } : new List<string>();
        }

        /// <summary>能穿戴（有合法槽位）。</summary>
        public bool IsWearable(string itemId) => SlotsFor(itemId).Count > 0;
    }
}
