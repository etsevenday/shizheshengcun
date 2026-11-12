using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B33 整备页（P03a—P03d）正式文字（《重墨 V2 开工裁定与界面逐字稿》P03a—P03d、B33 Q429—Q435、第二轮 Q448—Q453、Q458—Q462）。
    /// 页面只从这里取字，便于对照逐字稿；拒绝原因沿内核原句（EquipBlocked、GiveBlocked 等），不在这里另写。
    /// </summary>
    public static class GearPanelCopy
    {
        // P03a（Q429、Q433、Q448）
        public const string Title = "整备";
        public const string Members = "队员";
        public const string Slots = "装备槽";
        public const string Sources = "来源";
        public const string OwnCarried = "本人随身";
        public const string CurrentGround = "当前地面";
        public const string EmptyWork = "选择一件装备查看更换结果。";
        public const string CarriedEmpty = "随身没有物品";
        public const string GroundEmpty = "当前地面没有可穿的装备";
        public const string Close = "关闭";
        public const string Back = "返回";
        public static string PartyHead(int n, int c) => $"同行{n}/{c}";
        public static string MemberLine(long grams, int used, int max) => $"{Fmt.KgValue(grams)}公斤 · {used}/{max}";
        public static string SummaryCards(int used, int max) => $"随身物品 {used}/{max}";
        public static string SummaryWeight(long grams) => $"负重 {Fmt.KgValue(grams)}公斤";

        // P03b（Q430、Q434、Q452、Q458、Q461）
        public const string CompareTitle = "更换装备";
        public const string CompareCaption = "同槽比较";
        public const string CurrentGear = "当前装备";
        public const string CandidateGear = "候选装备";
        public const string SlotLabel = "装备槽";
        public const string Durability = "耐久";
        public const string Effects = "效果";
        public const string OldGoesTo = "换下的装备放到";
        public const string CardsAfter = "更换后随身";
        public const string WeightAfter = "更换后负重";
        public const string ConfirmEquip = "确认更换";
        /// <summary>空装备值（Q478：七槽列表与比较列统一写「空」，取代 Q430、P03b 比较列的「空槽」；不可比项目仍写「—」）。</summary>
        public const string EmptySlot = "空";
        /// <summary>来源卡与整理窗的品质标签（Q482：与地面卡同为「{品质}色」，非装备为空串、不伪造品质）。</summary>
        public static string QualityTag(string qualityName) => string.IsNullOrEmpty(qualityName) ? "" : qualityName + "色";
        public const string Dash = "—";
        public const string CombatGroup = "战斗";
        public const string SurvivalGroup = "求生";
        public const string None = "无";
        public static string Change(string name, string cur, string after) => $"{name} {cur} → {after}";
        public static string Cards(int used, int max) => $"{used}/{max}";
        public static string OldNoRoom(string name, int used, int max) =>
            $"随身卡位不足，无法收下换下的{name}（更换后{used}/{max}）。请选择「当前地面」，或先腾出随身卡位。";

        // P03c（Q432、Q449、Q451）
        public const string GiveEntry = "交给…";
        public const string GiveTitle = "交接物品";
        public const string Receiver = "接收人";
        public const string Quantity = "数量";
        public const string Max = "最大";
        public const string GiveCardsAfter = "交接后随身";
        public const string GiveWeightAfter = "交接后负重";
        public const string ConfirmGive = "确认交接";
        public const string PickReceiver = "请选择接收人。";
        public const string ContainerNote = "容器里的物品一并交接。";
        public static string GiveCardsSide(string name, int used, int max) => $"{name} {used}/{max}";
        public static string GiveWeightSide(string name, long grams) => $"{name} {Fmt.KgValue(grams)}公斤";
        public const string SideJoin = " · ";

        // P03d（Q434）
        public const string DupTitle = "整理同类型武器";
        public const string DupKeep = "选择保留的武器";
        public const string DupOther = "另一件放到";
        public const string DupConfirm = "确认整理";
        public const string DupToCarried = "放回随身";
        public const string DupToGround = "放当前地面";
        public const string DupPickKeep = "先选要保留的一把";
    }

    /// <summary>同槽比较的一行单件说明（Q430）：标签、当前装备一侧、候选装备一侧（缺项「—」）。</summary>
    public sealed class GearCompareRow
    {
        public string Label = "";
        public string Current = GearPanelCopy.Dash;
        public string Candidate = GearPanelCopy.Dash;
    }

    /// <summary>全身合计的一项真实值（Q452：可计算的 0、×1 照写，不适用「—」）；Suffix 是紧跟该侧数值的封顶注记。</summary>
    public sealed class GearTotalItem
    {
        public string Group = "";
        public string Key = "";
        public string Name = "";
        public string Value = "";
        public string Suffix = "";
        /// <summary>整行跟在「当前 → 更换后」之后的固定说明（工具维护「（替代，不相加）」）。</summary>
        public string Tail = "";
        public string Shown => Value + Suffix;
    }

    /// <summary>同槽比较只读预演（P03b）。</summary>
    public sealed class EquipPreviewInfo
    {
        public ItemInstance Candidate = new ItemInstance();
        public ItemInstance? Old;
        public string Slot = "";
        /// <summary>候选来源：本人随身 / 当前地面。</summary>
        public string Source = "";
        public List<GearCompareRow> Rows = new List<GearCompareRow>();
        public List<string> CombatChanges = new List<string>();
        public List<string> CombatNotes = new List<string>();
        public List<string> SurvivalChanges = new List<string>();
        public List<string> SurvivalNotes = new List<string>();
        /// <summary>耐久 0 暂停名单变化行；无变化为 null。</summary>
        public string? PausedChange;
        /// <summary>旧物去向选项（只列实际合法的，Q434）；旧槽为空时两者都 false。</summary>
        public bool CanToCarried;
        public bool CanToGround;
        /// <summary>生效的去向：null 未选（只剩地面且未点选，或两处都不合法）。</summary>
        public bool? OldToGround;
        /// <summary>更换后本人随身卡位与总重（未选地面时按旧件回本人随身预演，Q461，可超限）。</summary>
        public int CardsAfter;
        public int GramsAfter;
        /// <summary>不能确认的原因；null 可确认。</summary>
        public string? Blocked;
    }

    /// <summary>交接盘只读预演（P03c）。</summary>
    public sealed class GivePreviewInfo
    {
        public ItemInstance? Item;
        public string FromId = "";
        public string? ToId;
        public int Count;
        public int Max;
        public bool ShowQuantity;
        public bool ContainerNote;
        public int FromCards;
        public int FromGrams;
        public int ToCards;
        public int ToGrams;
        /// <summary>双方结果能否展示（来源有效、数量有效）。</summary>
        public bool HasOutcome;
        public string? Blocked;
    }

    public sealed partial class GameEngine
    {
        // ───────────── B33 P03b 同槽比较（只读预演） ─────────────

        /// <summary>
        /// 同槽比较只读预演（Q430、Q434、Q452、Q461）：两件装备的单件说明、按目标槽预演的全身合计变化、旧物去向选项与最终卡位／负重、不能确认的原因。
        /// 校验全部沿 EquipBlocked；全身合计用 EquipmentEffects 在“更换后”状态上重算（临时改两件的归属后立刻复原，不留任何改动）。
        /// oldToGround 是玩家明确点选的去向（null 未选）：两处都合法默认本人随身；本人随身不合法时只列当前地面且不自动选中。
        /// </summary>
        public EquipPreviewInfo? EquipPreview(GameState s, string actorId, int instanceId, string slot, bool? oldToGround)
        {
            var it = FindItem(s, instanceId);
            if (it == null || !EquipSlots.IsValid(slot)) return null;
            var old = WornItem(s, actorId, slot);
            if (old != null && old.InstanceId == it.InstanceId) old = null;
            var info = new EquipPreviewInfo
            {
                Candidate = it,
                Old = old,
                Slot = slot,
                Source = OnCurrentGround(s, it) ? GearPanelCopy.CurrentGround : GearPanelCopy.OwnCarried,
                Rows = CompareRows(s, old, it),
            };
            if (old == null)
            {
                info.Blocked = EquipBlocked(s, actorId, instanceId, slot, false);
                (info.GramsAfter, info.CardsAfter) = EquipOutcome(s, actorId, it, slot, false);
            }
            else
            {
                var carriedWhy = EquipBlocked(s, actorId, instanceId, slot, false);
                var groundWhy = EquipBlocked(s, actorId, instanceId, slot, true);
                info.CanToCarried = carriedWhy == null;
                info.CanToGround = groundWhy == null;
                info.OldToGround = oldToGround == true && info.CanToGround ? true
                    : oldToGround == false && info.CanToCarried ? false
                    : info.CanToCarried ? false : (bool?)null;
                (info.GramsAfter, info.CardsAfter) = EquipOutcome(s, actorId, it, slot, info.OldToGround == true);
                if (info.OldToGround == true) info.Blocked = groundWhy;
                else if (info.OldToGround == false) info.Blocked = carriedWhy;
                else if (info.CanToGround && !RootCardsAllowOf(s, actorId, info.CardsAfter))
                    info.Blocked = GearPanelCopy.OldNoRoom(ItemName(old.ItemId), info.CardsAfter, MaxRootCards);
                else info.Blocked = carriedWhy ?? groundWhy;
            }
            FillEffectChanges(s, actorId, info);
            return info;
        }

        /// <summary>候选默认比较的槽（Q431：武器默认武器Ⅰ）；不是装备为 null。</summary>
        public string? DefaultCompareSlot(string itemId) => SlotsFor(itemId).FirstOrDefault();

        /// <summary>单件说明两列（Q430、Q452）：品质、耐久、附加词条、固定特性／固定效果、暂停状态，逐字沿对象详情原句及注记；一侧缺项「—」，两侧都没有的行不列。</summary>
        private List<GearCompareRow> CompareRows(GameState s, ItemInstance? cur, ItemInstance cand)
        {
            var a = cur == null ? new List<(string Label, string Value)>() : EquipDetailLines(s, cur);
            var b = EquipDetailLines(s, cand);
            var rows = new List<GearCompareRow>();
            void Add(string label, string? x, string? y)
            {
                if (x == null && y == null) return;
                rows.Add(new GearCompareRow { Label = label, Current = x ?? GearPanelCopy.Dash, Candidate = y ?? GearPanelCopy.Dash });
            }
            string? One(List<(string Label, string Value)> l, string label) => l.Where(x => x.Label == label).Select(x => x.Value).FirstOrDefault();
            Add("品质", One(a, "品质"), One(b, "品质"));
            string? Dur(ItemInstance? i) => i == null ? null : DurabilityText(i) is string t && t != "" ? t : null;
            Add(GearPanelCopy.Durability, Dur(cur), Dur(cand));
            var ax = a.Where(x => x.Label == "附加词条").Select(x => x.Value).ToList();
            var bx = b.Where(x => x.Label == "附加词条").Select(x => x.Value).ToList();
            for (var i = 0; i < Math.Max(ax.Count, bx.Count); i++)
                Add("附加词条", i < ax.Count ? ax[i] : null, i < bx.Count ? bx[i] : null);
            Add("固定特性", One(a, "固定特性"), One(b, "固定特性"));
            Add("固定效果", One(a, "固定效果"), One(b, "固定效果"));
            Add("状态", One(a, "状态"), One(b, "状态"));
            return rows;
        }

        /// <summary>按“更换后”状态重算全身合计，只列值或说明变化的项（Q452、Q458）；攻击类按目标武器槽那一页。</summary>
        private void FillEffectChanges(GameState s, string actorId, EquipPreviewInfo info)
        {
            int? page = EquipSlots.IsWeaponSlot(info.Slot) ? (info.Slot == EquipSlots.Weapon1 ? 1 : 2) : (int?)null;
            var before = GearTotals(s, actorId, page, out var pausedBefore);
            List<GearTotalItem> after;
            string pausedAfter;
            var it = info.Candidate;
            var old = info.Old;
            var itOwner = it.Owner;
            var oldOwner = old?.Owner;
            try
            {
                if (old != null) old.Owner = ItemInstance.CarriedOwner(actorId);
                it.Owner = ItemInstance.EquipOwner(actorId, info.Slot);
                after = GearTotals(s, actorId, page, out pausedAfter);
            }
            finally
            {
                it.Owner = itOwner;
                if (old != null) old.Owner = oldOwner!;
            }
            var displaceChanged = false;
            foreach (var x in before)
            {
                var y = after.First(z => z.Key == x.Key);
                if (x.Shown == y.Shown) continue;
                var line = GearPanelCopy.Change(x.Name, x.Shown, y.Shown) + x.Tail;
                if (x.Group == GearPanelCopy.CombatGroup) info.CombatChanges.Add(line);
                else info.SurvivalChanges.Add(line);
                if (x.Key == "combat.displaceResist") displaceChanged = true;
            }
            // Q458：原战斗生效尾注、位移抵抗作用条件、求生下限说明沿原合计真实文字，各在对应组之后一次
            if (info.CombatChanges.Count > 0)
            {
                if (displaceChanged) info.CombatNotes.Add("只防拉扯/击退");
                info.CombatNotes.Add(displaceChanged ? "位移抵抗在我方多人时才有用（只剩你一人不会被移动），其余战斗中生效" : "战斗中生效");
            }
            if (info.SurvivalChanges.Count > 0)
            {
                var lim = Config.Equipment.Limits;
                info.SurvivalNotes.Add($"工作量合成下限×{Fmt.T(lim.SurvivalWorkFloor)}、负小时率下限×{Fmt.T(lim.NegativeRateFloor)}");
            }
            if (pausedBefore != pausedAfter) info.PausedChange = GearPanelCopy.Change("效果暂停", pausedBefore, pausedAfter);
        }

        private static readonly (string Key, string Name)[] SurvivalFactorNames =
        {
            ("work.craft", "制造/建造工作量"), ("work.scavenge", "搜集工作量"), ("work.move", "移动工作量"), ("work.route", "已发现路线移动工作量"),
            ("rate.gather.energy", "采集负体力"), ("rate.move.energy", "移动负体力"), ("rate.scavengeClear.energy", "搜集/清理负体力"),
            ("rate.nonCombat.satiety", "非战斗负饱食"), ("rate.nonCombat.hydration", "非战斗负水分"),
            ("hazard.health", "划伤健康损失"), ("hazard.woodScrapChance", "木材/废料划伤概率"), ("eat.satiety", "非战斗食用正饱食"),
        };

        /// <summary>
        /// 全身合计的全部项及真实值（Q452：没有附加值写 0、没有倍率写 ×1，不适用「—」），名称、符号、单位、精度与封顶注记沿 EquipSummaryLines；
        /// paused 为耐久 0 暂停名单（「{装备名}（{槽名}）」以「、」连接，空为「无」）。
        /// </summary>
        public List<GearTotalItem> GearTotals(GameState s, string actorId, int? attackPage, out string paused)
        {
            var fx = EquipmentEffects(s, actorId, attackPage);
            var lim = Config.Equipment.Limits;
            var c = GearPanelCopy.CombatGroup;
            var list = new List<GearTotalItem>
            {
                new GearTotalItem { Group = c, Key = "combat.speed", Name = "速度", Value = Fmt.SignedT(fx.Speed), Suffix = fx.SpeedPositiveRaw > lim.PositiveSpeedCap ? $"（正加成封顶+{Fmt.T(lim.PositiveSpeedCap)}）" : "" },
                new GearTotalItem { Group = c, Key = "combat.armor", Name = "护甲", Value = Fmt.T(fx.Armor), Suffix = fx.ArmorRaw > lim.ArmorCap ? $"（封顶{Fmt.T(lim.ArmorCap)}）" : "" },
                new GearTotalItem { Group = c, Key = "combat.accuracy", Name = "命中能力", Value = Fmt.SignedT(fx.Accuracy) },
                new GearTotalItem { Group = c, Key = "combat.crit", Name = "暴击能力", Value = Fmt.SignedT(fx.Crit) },
                new GearTotalItem { Group = c, Key = "combat.armorIgnore", Name = "忽略护甲", Value = Fmt.T(fx.ArmorIgnore) },
                new GearTotalItem { Group = c, Key = "combat.damageMultiplier", Name = "直接伤害乘数", Value = Fmt.SignedT(fx.DamageMultiplier) },
                new GearTotalItem { Group = c, Key = "combat.energyCost", Name = "招式E", Value = "×" + Fmt.T(fx.EnergyCostFactor) },
                new GearTotalItem { Group = c, Key = "combat.displaceResist", Name = "位移抵抗", Value = $"+{Fmt.T(fx.DisplaceResist)}个百分点",
                    Suffix = fx.DisplaceResistRaw > Config.ActorBaseDefense.DisplacementGearAggregateCap ? $"（合计封顶{Fmt.T(Config.ActorBaseDefense.DisplacementGearAggregateCap)}）" : "" },
                new GearTotalItem { Group = c, Key = "combat.noise", Name = "噪声", Value = Fmt.SignedT(fx.Noise) },
                new GearTotalItem { Group = c, Key = "combat.unarmedBasicTargets", Name = "徒手拳击可打位置", Value = PunchTargetsText(s, actorId) },
            };
            var sv = GearPanelCopy.SurvivalGroup;
            foreach (var (key, name) in SurvivalFactorNames)
                list.Add(new GearTotalItem { Group = sv, Key = key, Name = name, Value = "×" + Fmt.T(fx.Factor(key)) });
            list.Add(new GearTotalItem { Group = sv, Key = "pos.shelteredRest.energy", Name = "遮蔽休息体力恢复", Value = $"+{Fmt.T(fx.Add("pos.shelteredRest.energy"))}/小时" });
            list.Add(new GearTotalItem { Group = sv, Key = "pos.sleep.energy", Name = "睡眠体力恢复", Value = $"+{Fmt.T(fx.Add("pos.sleep.energy"))}/小时" });
            list.Add(new GearTotalItem { Group = sv, Key = "bandage.health", Name = "非战斗包扎回血", Value = $"+{Fmt.T(fx.Add("bandage.health"))}" });
            list.Add(new GearTotalItem { Group = sv, Key = "repair.fraction", Name = "工具维护恢复", Value = fx.Set("repair.fraction") is double rf ? Fmt.Pct(rf) : GearPanelCopy.Dash, Tail = "（替代，不相加）" });
            paused = fx.Paused.Count == 0 ? GearPanelCopy.None : string.Join("、", fx.Paused);
            return list;
        }

        /// <summary>空槽徒手页普通拳击的真实可打位置（Q452：沿原生效范围；只有主角的护拳绑带扩展，其他角色不适用写「—」）。</summary>
        private string PunchTargetsText(GameState s, string actorId)
        {
            if (actorId != MeId(s)) return GearPanelCopy.Dash;
            var node = Config.SkillNodes.FirstOrDefault(n => n.Id == UnarmedBasic);
            if (node == null || !_weapons.TryGetValue(Fists, out var fists)) return GearPanelCopy.Dash;
            return PosList(SkillTargetPositions(s, node, fists));
        }

        // ───────────── B33 P03c 交接（只读预演） ─────────────

        /// <summary>「交给…」可选的接收人（Q432、Q451）：其他同行者，与 GiveBlocked 的同行口径一致；满包、超重者也列出。</summary>
        public List<string> GiveReceivers(GameState s, string fromId) =>
            s.Party.Contains(fromId) ? s.Party.Where(id => id != fromId).ToList() : new List<string>();

        /// <summary>
        /// 交接入口整体受阻（Q449）：与接收人无关的第一条真实原因（行动、对白、战斗、预留、占用、来源失效等，顺序沿 GiveBlocked）；
        /// null 表示入口可用。没有其他同行者时页面直接隐藏入口，不走这里。
        /// </summary>
        public string? GiveEntryBlocked(GameState s, string fromId, int instanceId) =>
            GiveTimingBlocked(s) ?? (!s.Party.Contains(fromId) ? GiveNotPartyText : null) ?? GiveSourceBlocked(s, fromId, instanceId);

        /// <summary>
        /// 交接盘只读预演（Q432、Q451）：接收人未选时确认禁用并写「请选择接收人。」、只给出物人一侧；数量无效或来源失效写原原因、不给结果；
        /// 选了满包或超重的人照样给双方真实最终卡位与负重，原因沿 GiveBlocked。
        /// </summary>
        public GivePreviewInfo GivePreview(GameState s, string fromId, string? toId, int instanceId, int count)
        {
            var it = FindItem(s, instanceId);
            var info = new GivePreviewInfo { Item = it, FromId = fromId, ToId = toId, Count = count };
            var entry = GiveEntryBlocked(s, fromId, instanceId);
            if (it == null || it.Owner != ItemInstance.CarriedOwner(fromId))
            {
                info.Blocked = entry ?? "物品已不在";
                return info;
            }
            info.Max = it.Count;
            info.ShowQuantity = it.Count > 1;
            info.ContainerNote = s.Items.Any(i => i.Owner == ItemInstance.ContainerOwner(it.InstanceId));
            var countOk = count > 0 && count <= it.Count;
            if (countOk)
            {
                info.HasOutcome = true;
                var grams = count >= it.Count ? ItemGrams(s, it) : _items[it.ItemId].WeightGrams * count;
                info.FromCards = RootCardsAfterOf(s, fromId, null, 0, it, count);
                info.FromGrams = CarriedGramsOf(s, fromId) - grams;
                if (toId != null)
                {
                    var o = GiveOutcome(s, fromId, toId, it, count);
                    info.ToCards = o.ToCards;
                    info.ToGrams = o.ToGrams;
                }
            }
            info.Blocked = entry ?? (!countOk ? "数量无效" : toId == null ? GearPanelCopy.PickReceiver : GiveBlocked(s, fromId, toId, instanceId, count));
            return info;
        }

        // ───────────── B33 Q450、Q459 临时整备详情的位置行 ─────────────

        /// <summary>
        /// 临时整备详情的位置行（Q450、Q459）：主角「随身物品」「穿戴·{槽名}」；同行者「{姓名}随身」「{姓名} · 穿戴·{槽名}」；
        /// 地面、容器、设施沿 WherePath 的真实位置。不显示内部编号。
        /// </summary>
        public string GearPlaceText(GameState s, ItemInstance it)
        {
            if (ItemInstance.TryEquip(it.Owner, out var actor, out var slot))
                return actor == GameState.PlayerId ? $"穿戴·{EquipSlots.Name(slot)}" : $"{ActorDisplayName(s, actor)} · 穿戴·{EquipSlots.Name(slot)}";
            if (it.Owner == ItemInstance.Carried) return "随身物品";
            var mate = s.Actors.Keys.FirstOrDefault(a => a != GameState.PlayerId && it.Owner == ItemInstance.CarriedOwner(a));
            if (mate != null) return $"{ActorDisplayName(s, mate)}随身";
            if (ItemInstance.TryContainerId(it.Owner, out _) && ContainerOf(s, it) is ItemInstance pot && pot.InstanceId != it.InstanceId)
                return $"{GearPlaceText(s, pot)}／{ItemName(pot.ItemId)}";
            return WherePath(s, it);
        }
    }
}
