using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 给同行者吃喝的逐字稿（Q257，assets/20261105/ui-card-feeding-q252-257.json feeding.copy）。字段写法同合同，用 Fill 按实际对象填。
    /// 腹泻、腐败、预留、敌踪等既有句不在这里，沿原文字。
    /// </summary>
    public static class FeedCopy
    {
        public const string ConfirmFood = "给{目标}食用：{物品}×1 · 5分钟";
        public const string ConfirmWater = "给{目标}饮用：{水种}×1 · 5分钟";
        public const string ConfirmCanteen = "给{目标}饮用：{水壶}内{水种}250毫升 · 5分钟";
        public const string ResultFood = "{目标}食用了{物品}×1（取自{出物人}的随身物品）：{变化}。";
        public const string ResultPotFood = "{目标}食用了{物品}×1（取自{出物人}的随身锅#{锅号}）：{变化}。";
        public const string ResultWater = "{目标}饮用了{水种}×1（取自{出物人}的随身物品）：{变化}。";
        public const string ResultPotWater = "{目标}饮用了{水种}×1（取自{出物人}的随身锅#{锅号}）：{变化}。";
        public const string ResultCanteen = "{目标}饮用了{水壶}内的{水种}250毫升（取自{出物人}的随身物品）：{变化}；壶内剩余{壶内水量}。";
        public const string NoTarget = "请先选择一名同行者。";
        public const string InvalidTarget = "当前无法给{目标}吃喝：目标不在同地点、同区域的同行队伍中。";
        public const string DeadTarget = "无法给{目标}吃喝：目标已死亡。";
        public const string InvalidOwner = "只能使用你或{目标}随身的食物和饮水。";
        public const string InvalidItem = "这件物品不能用于给同行者食用或饮用。";
        public const string InsufficientPotWater = "锅内{水种}不足500毫升，无法饮用1份。";
        public const string Battle = "战斗中无法给同行者吃喝。";
        public const string Extracting = "正在撤出，无法给同行者吃喝。";
        public const string Changed = "当前情况已变化，请重新选择后再操作。";

        /// <summary>合同键 → 逐字稿，测试逐句比对。</summary>
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            ["confirmFood"] = ConfirmFood, ["confirmWater"] = ConfirmWater, ["confirmCanteen"] = ConfirmCanteen,
            ["resultFood"] = ResultFood, ["resultPotFood"] = ResultPotFood, ["resultWater"] = ResultWater,
            ["resultPotWater"] = ResultPotWater, ["resultCanteen"] = ResultCanteen, ["noTarget"] = NoTarget,
            ["invalidTarget"] = InvalidTarget, ["deadTarget"] = DeadTarget, ["invalidOwner"] = InvalidOwner,
            ["invalidItem"] = InvalidItem, ["insufficientPotWater"] = InsufficientPotWater, ["battle"] = Battle,
            ["extracting"] = Extracting, ["changed"] = Changed,
        };

        public static string Fill(string template, params (string field, string value)[] fields)
        {
            foreach (var (field, value) in fields) template = template.Replace("{" + field + "}", value);
            return template;
        }
    }

    /// <summary>
    /// 食饮结算（开发计划 v0.9 B28 R12，合同 feeding.effect）与给同行者吃喝的事务（R13、R23、R45—R47）。
    /// 事务就是一次 ActionType.Use：校验走 Validate（战斗、撤出、敌踪、强制对白、主行动暂停时的补给子行动都与主角自用同序），
    /// 开始时原子扣一份并给效果，随后按 use（费率 wait）走一段 5 分钟、不可取消；requestId 由 Start 的 SeenRequest 去重。
    /// 出物人只能是主角或目标本人；不产生临时水卡，不动存档结构。
    /// </summary>
    public sealed partial class GameEngine
    {
        private const string ReservedText = "该物品已被预留";

        /// <summary>校验通过后的这次吃喝。Item 是真实实例：水卡/食物/水壶/锅内食物/锅内水。</summary>
        private sealed class FeedPlan
        {
            public string TargetId = "";
            public string SourceId = "";
            public ItemInstance Item = null!;
            public ItemInstance? Pot;
            public FeedSourceKind Kind;
        }

        // ───────────── 结算 ─────────────

        /// <summary>
        /// 食品/净水/脏水的即时效果落到 actor：配给袋看 actor 本人穿戴，夹紧 0—上限。
        /// actualDelta=false 时文字写配置量（主角自用原文字，如“饱食+35”）；true 时写裁上限后的实际变化（Q257 的{变化}）。
        /// </summary>
        private void ApplyUseEffect(GameState s, ActorState actor, ItemUseConfig u, List<string> parts, bool actualDelta)
        {
            var sat = UseSatiety(s, u, actor.Id);   // 配给袋：非战斗食用正饱食×1.05（B4）
            if (sat != 0) AddUseStat(actor, StatId.Satiety, "饱食", sat, parts, actualDelta);
            if (u.Hydration != 0) AddUseStat(actor, StatId.Hydration, "水分", u.Hydration, parts, actualDelta);
            if (u.Morale != 0) AddUseStat(actor, StatId.Morale, "精神", u.Morale, parts, actualDelta);
            if (u.Health != 0 && !u.IsBandage) AddUseStat(actor, StatId.Health, "健康", u.Health, parts, actualDelta);
            if (u.CausesDiarrhea)
            {
                actor.DiarrheaUntil = s.GameMinute + Config.ItemActions.DiarrheaMinutes;
                parts.Add("出现腹泻（12小时）");
            }
        }

        private static void AddUseStat(ActorState actor, StatId id, string name, double amount, List<string> parts, bool actualDelta)
        {
            var before = actor.Stats.Get(id);
            actor.Stats.Add(id, amount);
            parts.Add(Signed(name, actualDelta ? actor.Stats.Get(id) - before : amount));
        }

        /// <summary>原子扣一份并把效果给目标本人，返回{变化}。空锅与水壶留原 owner，不生成临时水卡。</summary>
        private string ConsumeFeed(GameState s, FeedPlan f)
        {
            var actor = s.Actors[f.TargetId];
            var parts = new List<string>();
            if (f.Kind == FeedSourceKind.Canteen) ApplyCanteenEffect(s, f.Item, actor, parts, true);
            else
            {
                var use = _items[f.Item.ItemId].Use!;
                ConsumeOne(s, f.Item);
                if (f.Pot != null) SyncFacilities(s);
                ApplyUseEffect(s, actor, use, parts, true);
            }
            return string.Join("，", parts);
        }

        private string FeedConfirmText(GameState s, FeedPlan f)
        {
            var target = ("目标", ActorDisplayName(s, f.TargetId));
            return f.Kind switch
            {
                FeedSourceKind.Canteen => FeedCopy.Fill(FeedCopy.ConfirmCanteen, target, ("水壶", ItemName(f.Item.ItemId)), ("水种", ItemName(f.Item.LiquidId!))),
                FeedSourceKind.Water or FeedSourceKind.PotWater => FeedCopy.Fill(FeedCopy.ConfirmWater, target, ("水种", ItemName(f.Item.ItemId))),
                _ => FeedCopy.Fill(FeedCopy.ConfirmFood, target, ("物品", ItemName(f.Item.ItemId))),
            };
        }

        /// <summary>结果句。f.Item 已结算：物名与水壶水种用结算前取的（喝空后实例上已清空）。</summary>
        private string FeedResultText(GameState s, FeedPlan f, string itemName, string? canteenLiquid, string change)
        {
            var target = ("目标", ActorDisplayName(s, f.TargetId));
            var source = ("出物人", ActorDisplayName(s, f.SourceId));
            var delta = ("变化", change);
            var pot = ("锅号", f.Pot?.InstanceId.ToString() ?? "");
            return f.Kind switch
            {
                FeedSourceKind.Canteen => FeedCopy.Fill(FeedCopy.ResultCanteen, target, ("水壶", itemName), ("水种", ItemName(canteenLiquid!)), source, delta, ("壶内水量", CanteenLabel(f.Item))),
                FeedSourceKind.Water => FeedCopy.Fill(FeedCopy.ResultWater, target, ("水种", itemName), source, delta),
                FeedSourceKind.PotWater => FeedCopy.Fill(FeedCopy.ResultPotWater, target, ("水种", itemName), source, pot, delta),
                FeedSourceKind.PotFood => FeedCopy.Fill(FeedCopy.ResultPotFood, target, ("物品", itemName), source, pot, delta),
                _ => FeedCopy.Fill(FeedCopy.ResultFood, target, ("物品", itemName), source, delta),
            };
        }

        /// <summary>Start 的 Use 分支：结算并发结果句。不记 PRO02（只算主角自己吃喝）。</summary>
        private void StartFeed(GameState s, FeedPlan f)
        {
            var name = ItemName(f.Item.ItemId);
            var liquid = f.Item.LiquidId;
            var change = ConsumeFeed(s, f);
            Emit(s, GameEventKind.ActionCompleted, FeedResultText(s, f, name, liquid, change));
        }

        // ───────────── 校验 ─────────────

        private string? FeedTargetBlocked(GameState s, string? targetId)
        {
            if (string.IsNullOrEmpty(targetId) || targetId == GameState.PlayerId) return FeedCopy.NoTarget;
            var name = ("目标", ActorDisplayName(s, targetId!));
            if (RosterOf(s, targetId!)?.Dead == true) return FeedCopy.Fill(FeedCopy.DeadTarget, name);
            return ActiveCompanions(s).Any(r => r.ActorId == targetId) ? null : FeedCopy.Fill(FeedCopy.InvalidTarget, name);
        }

        /// <summary>Q256：类型沿主角现有食用/饮用动作（UseVerb 里的“使用”是药品与护理品），不看认知，也不看主角此刻饥不饥渴。</summary>
        private static bool IsFeedableUse(ItemConfig cfg) => cfg.Use != null && UseVerb(cfg) != "使用";

        /// <summary>
        /// 这次选中的物品现在能不能出：判定顺序沿 Validate 的 Use 分支，每次只出一句。带确认快照时，物品不在了或换了地方算情况变化。
        /// </summary>
        private string? FeedItemBlocked(GameState s, FeedRequest f, out FeedPlan? plan)
        {
            plan = null;
            var stamped = f.ConfirmStamp != null;
            var ownerWhy = FeedCopy.Fill(FeedCopy.InvalidOwner, ("目标", ActorDisplayName(s, f.TargetActorId)));
            if (f.SourceActorId != GameState.PlayerId && f.SourceActorId != f.TargetActorId) return ownerWhy;
            var root = ItemInstance.CarriedOwner(f.SourceActorId);

            var pot = f.PotInstanceId is int potId ? FindItem(s, potId) : null;
            if (f.PotInstanceId.HasValue && (pot == null || !IsPot(pot))) return FeedCopy.Changed;
            ItemInstance? inst;
            if (f.InstanceId is int id)
            {
                inst = FindItem(s, id);
                if (inst == null) return FeedCopy.Changed;
                if (pot != null && inst.Owner != ItemInstance.ContainerOwner(pot.InstanceId)) return FeedCopy.Changed;
                pot ??= inst.InContainer ? ContainerOf(s, inst) : null;
            }
            else if (pot != null && f.WaterItemId != null && IsLiquidItem(f.WaterItemId))
            {
                if (PlaceOf(s, pot) != root) return stamped ? FeedCopy.Changed : ownerWhy;
                inst = PotLiquids(s, pot.InstanceId).FirstOrDefault(i => i.ItemId == f.WaterItemId);
                if (inst == null) return FeedCopy.Fill(FeedCopy.InsufficientPotWater, ("水种", ItemName(f.WaterItemId)));
            }
            else return FeedCopy.Changed;
            if (PlaceOf(s, inst) != root) return stamped ? FeedCopy.Changed : ownerWhy;

            var cfg = _items[inst.ItemId];
            if (inst.ReservedBy != null || IsLocked(s, inst) || (pot != null && (pot.ReservedBy != null || IsLocked(s, pot)))) return ReservedText;
            FeedSourceKind kind;
            if (cfg.Canteen != null)
            {
                if (CanteenSipBlocked(inst) is string sipWhy) return sipWhy;
                kind = FeedSourceKind.Canteen;
            }
            else
            {
                if (pot != null && IsLiquid(inst) && PotFreeWaterUnits(s, inst) < 1)
                    return PotFreeMl(s, pot) > 0 ? FeedCopy.Fill(FeedCopy.InsufficientPotWater, ("水种", ItemName(inst.ItemId))) : ReservedWaterText(s, inst);
                // 锁中加工原料不能通过给同行吃喝拆掉（Q233 boundaries）；主角 Q23 受热保护直接食用入口不变
                if (pot != null && (ProcessOf(s, inst) != null || IsPotAux(s, inst))) return ReservedText;
                if (cfg.Use == null) return cfg.RequiresCooking ? $"{cfg.Name}需先烹饪，不可直接食用" : FeedCopy.InvalidItem;
                if (IsSpoiled(s, inst)) return "已腐败，不能食用";
                if (!IsFeedableUse(cfg)) return FeedCopy.InvalidItem;
                kind = pot != null ? (IsLiquid(inst) ? FeedSourceKind.PotWater : FeedSourceKind.PotFood)
                    : cfg.Kind == "water" ? FeedSourceKind.Water : FeedSourceKind.Food;
            }

            var p = new FeedPlan { TargetId = f.TargetActorId, SourceId = f.SourceActorId, Item = inst, Pot = pot, Kind = kind };
            if (stamped && f.ConfirmStamp != FeedStamp(s, p)) return FeedCopy.Changed;
            plan = p;
            return null;
        }

        /// <summary>锅里没被预留的毫升。</summary>
        private int PotFreeMl(GameState s, ItemInstance pot) => PotUnits(s, pot.InstanceId) * PotConfig(pot).UnitMl - PotReservedMl(s, pot.InstanceId);

        /// <summary>确认重验用的快照：目标、出物人、实例、归属、份数、截止、水量。锁和预留已在校验里判，这里不重复。</summary>
        private string FeedStamp(GameState s, FeedPlan f)
        {
            var i = f.Item;
            var pot = f.Pot == null ? "" : $"{f.Pot.InstanceId}:{f.Pot.Owner}:{PotFreeMl(s, f.Pot)}";
            return $"{f.TargetId}|{f.SourceId}|{i.InstanceId}|{i.ItemId}|{i.Owner}|{i.Count}|{i.ExpiresAt}|{i.LiquidId}|{i.LiquidMl}|{pot}";
        }

        private string? ValidateFeed(GameState s, FeedRequest f, Plan plan)
        {
            if (FeedTargetBlocked(s, f.TargetActorId) is string targetWhy) return targetWhy;
            if (FeedItemBlocked(s, f, out var feed) is string itemWhy) return itemWhy;
            plan.Feed = feed;
            plan.UseItem = feed!.Item;
            plan.TargetId = feed.Item.ItemId;
            plan.Planned = Config.ItemActions.UseMinutes;
            var confirm = FeedConfirmText(s, feed);
            plan.Label = confirm.Substring(0, confirm.LastIndexOf(" · "));
            return null;
        }

        // ───────────── 对外（B29 界面用） ─────────────

        /// <summary>提交一次给同行者吃喝。同 Start：失败不扣不改不耗时，同 requestId 只结算一次。</summary>
        public ActionResult Feed(GameState s, FeedRequest f) => Start(s, ActionRequest.FeedCompanion(f));

        /// <summary>只读预览，同 Preview：Ok、Reason、ConfirmText、Overflow 与 Feed 字段。</summary>
        public ActionPreview FeedPreview(GameState s, FeedRequest f) => Preview(s, ActionRequest.FeedCompanion(f));

        /// <summary>现在不能提交的原因；可以时为 null。</summary>
        public string? FeedBlocked(GameState s, FeedRequest f) => CanStart(s, ActionRequest.FeedCompanion(f));

        /// <summary>当前合法目标：同地点同区、存活、已编入同行的非主角。只读。</summary>
        public List<FeedTargetInfo> FeedTargets(GameState s) =>
            ActiveCompanions(s).Where(r => s.Actors.ContainsKey(r.ActorId))
                .Select(r => new FeedTargetInfo { ActorId = r.ActorId, Name = ActorDisplayName(s, r.ActorId), Stats = s.Actors[r.ActorId].Stats.Clone() })
                .ToList();

        /// <summary>
        /// 目标的合法出物批次：主角随身在前、目标本人随身在后，含这两人随身锅内未锁食水；只看物品本身，战斗等全局限制看 FeedBlocked。只读。
        /// 目标不合法时为空。“同类”筛选由界面按所点物品做（B29）。
        /// </summary>
        public List<FeedSourceInfo> FeedSources(GameState s, string targetActorId)
        {
            var list = new List<FeedSourceInfo>();
            if (FeedTargetBlocked(s, targetActorId) != null) return list;
            foreach (var source in new[] { GameState.PlayerId, targetActorId })
            {
                var root = ItemInstance.CarriedOwner(source);
                foreach (var i in s.Items.Where(x => !x.IsWorn && PlaceOf(s, x) == root).OrderBy(x => x.InstanceId))
                {
                    var f = new FeedRequest { TargetActorId = targetActorId, SourceActorId = source, InstanceId = i.InstanceId };
                    if (FeedItemBlocked(s, f, out var plan) == null) list.Add(FeedSourceRow(s, plan!));
                }
            }
            return list;
        }

        private FeedSourceInfo FeedSourceRow(GameState s, FeedPlan f)
        {
            var i = f.Item;
            var row = new FeedSourceInfo
            {
                SourceActorId = f.SourceId, SourceName = ActorDisplayName(s, f.SourceId), InstanceId = i.InstanceId, ItemId = i.ItemId,
                ItemName = ItemName(i.LiquidId ?? i.ItemId), Kind = f.Kind, PotInstanceId = f.Pot?.InstanceId, ExpiresAt = i.ExpiresAt, Stamp = FeedStamp(s, f),
            };
            (row.Portions, row.PortionMl, row.LiquidMl) = FeedAmounts(s, f);
            return row;
        }

        private (int portions, int portionMl, int liquidMl) FeedAmounts(GameState s, FeedPlan f)
        {
            var i = f.Item;
            switch (f.Kind)
            {
                case FeedSourceKind.Canteen:
                    var sip = CanteenOf(i)!.SipMl;
                    return (i.LiquidMl / sip, sip, i.LiquidMl);
                case FeedSourceKind.PotWater:
                    return (PotFreeWaterUnits(s, i), PotConfig(f.Pot!).UnitMl, PotFreeMl(s, f.Pot!));
                case FeedSourceKind.Water:
                    var ml = _items[i.ItemId].UnitVolumeMl ?? 0;
                    return (i.Count, ml, i.Count * ml);
                default:
                    return (i.Count, 0, 0);
            }
        }

        /// <summary>FillStatic 的 Use 分支：结构化预览。效果在副本上走真实结算函数，与确认时同一路径。</summary>
        private void FillFeedPreview(GameState s, ActionPreview p, FeedPlan f)
        {
            p.ConfirmText = FeedConfirmText(s, f);
            var actor = s.Actors[f.TargetId];
            var cfg = _items[f.Item.ItemId];
            var use = cfg.Canteen != null ? new ItemUseConfig { Hydration = cfg.Canteen.SipHydration } : cfg.Use!;
            p.Overflow = OverflowLines(actor.Stats, use, use.Health, UseSatiety(s, use, f.TargetId));

            var c = s.Clone();
            var sim = new FeedPlan
            {
                TargetId = f.TargetId, SourceId = f.SourceId, Kind = f.Kind, Item = FindItem(c, f.Item.InstanceId)!,
                Pot = f.Pot == null ? null : FindItem(c, f.Pot.InstanceId),
            };
            var change = ConsumeFeed(c, sim);
            var after = c.Actors[f.TargetId].Stats;
            p.Feed = new FeedPreviewInfo
            {
                TargetActorId = f.TargetId, TargetName = ActorDisplayName(s, f.TargetId), Source = FeedSourceRow(s, f),
                LiquidMlAfter = f.Kind switch
                {
                    FeedSourceKind.Canteen => sim.Item.LiquidMl,
                    FeedSourceKind.PotWater => PotFreeMl(c, sim.Pot!),
                    FeedSourceKind.Water => (f.Item.Count - 1) * (cfg.UnitVolumeMl ?? 0),
                    _ => 0,
                },
                TargetBefore = actor.Stats.Clone(), TargetAfterImmediate = after.Clone(), TargetAfter = after.Clone(),
                ChangeText = change,
                ResultText = FeedResultText(c, sim, ItemName(f.Item.ItemId), f.Item.LiquidId, change),
            };
        }
    }
}
