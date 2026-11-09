using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 食饮局部盘的逐字稿（Q264，assets/20261105/b29-entry-water-q258-268.json feeding.panelCopy）。字段用 FeedCopy.Fill 填。
    /// </summary>
    public static class FeedPanelCopy
    {
        public const string TitleFood = "给同行者食用";
        public const string TitleWater = "给同行者饮用";
        public const string SectionTarget = "选择同行者";
        public const string SectionSource = "选择来源";
        public const string SectionEffect = "预计变化";
        public const string SectionConfirm = "确认操作";
        public const string TargetPlaceholder = "请选择同行者";
        public const string SourcePlaceholder = "请选择来源";
        public const string BeforeTarget = "先选择同行者，再查看可用来源。";
        public const string SourceRequired = "请先选择一份可用的食物或饮水。";
        public const string SourceCleared = "当前情况已变化，请重新选择后再操作。";
        public const string NoRows = "你和{目标}随身没有同类食物或饮水。";
        public const string NoUsableRows = "当前没有可用的同类来源，请查看各批次的原因。";
        public const string EmptyItems = "没有随身物品。";
        public const string ExpandItems = "随身物品 {数量}/5";
        public const string CollapseItems = "收起随身物品";
        public const string Cancel = "取消";
        public const string CloseAria = "关闭食饮窗口";
        public const string Help = "查看食饮说明";
        public const string HelpBack = "返回食饮";
        public const string LabelTarget = "目标：{目标}";
        public const string LabelSource = "出物人：{出物人}";
        public const string LabelPortion = "本次用量：{用量}";
        public const string LabelRemaining = "当前余量：{余量}";
        public const string LabelDuration = "预计耗时：5分钟";
        public const string LabelExpiry = "到期：{截止}";
        public const string LabelBlocked = "不可用：{原因}";
        public const string LabelOverflow = "超出上限的部分不会增加。";
        public const string RowFood = "{出物人}的随身物品 · {物品}×{总份数} · 批次#{实例号}{截止段}";
        public const string RowPotFood = "{出物人}的随身锅#{锅号} · {物品}×{总份数} · 批次#{实例号}{截止段}";
        public const string RowWaterCard = "{出物人}的随身物品 · {水种}{毫升}毫升（{可饮份数}份，每份500毫升） · 编号#{实例号}";
        public const string RowPotWater = "{出物人}的随身锅#{锅号} · {水种}{毫升}毫升（{可饮份数}份，每份500毫升）";
        public const string RowCanteen = "{出物人}的随身{水壶}#{壶号} · {水种}{毫升}/{容量}毫升（{可饮次数}次，每次250毫升）";
        public const string PortionFood = "{物品}×1";
        public const string PortionWater = "{水种}500毫升";
        public const string PortionCanteen = "{水种}250毫升";
        public const string RemainingFood = "{物品}×{总份数}";
        public const string RemainingWater = "{水种}{毫升}毫升";
        public const string RemainingCanteen = "{水种}{毫升}/{容量}毫升";
        public const string HelpTitle = "给同行者吃喝";
        public const string HelpTopic = "人物、生存与医疗";

        public static readonly IReadOnlyList<string> HelpText = new[]
        {
            "可从食物、饮水卡或队伍面板里展开的同行者随身物品卡，选择“给同行者食用”或“给同行者饮用”。打开窗口不会自动选择目标。",
            "先选择同地点、同区域、存活且正在同行的人，再选择你或目标本人随身的来源。不能直接取用第三人、候补、地面或设施中的物品；随身锅内符合条件的食水可以直接选择。",
            "食物只列同一种物品，饮水只列同一水种。不可用批次会灰显并说明原因；确认前请核对目标、出物人、批次、预计变化和余量。",
            "一次只给一人食用一份。水卡和随身锅水每份500毫升，水壶每次250毫升；食饮按原规则推进5分钟，不能中途取消，不会先转移再扣一份。",
            "收益和风险作用于目标本人。脏水仍会造成腹泻，生食、腐败物和药品等仍受原入口限制；配给袋只看目标本人有效穿戴。",
            "战斗、撤出、敌踪、强制对白和其他占用仍按原限制处理。查看说明不吃喝、不耗时、不领奖，关闭后回到原食饮窗口并重新检查情况。",
        };

        /// <summary>合同 panelCopy 的路径 → 逐字稿（helpText 除外），测试逐句比对。</summary>
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            ["titleFood"] = TitleFood, ["titleWater"] = TitleWater,
            ["sections.target"] = SectionTarget, ["sections.source"] = SectionSource, ["sections.effect"] = SectionEffect, ["sections.confirm"] = SectionConfirm,
            ["targetPlaceholder"] = TargetPlaceholder, ["sourcePlaceholder"] = SourcePlaceholder, ["beforeTarget"] = BeforeTarget,
            ["sourceRequired"] = SourceRequired, ["sourceCleared"] = SourceCleared, ["noRows"] = NoRows, ["noUsableRows"] = NoUsableRows,
            ["emptyItems"] = EmptyItems, ["expandItems"] = ExpandItems, ["collapseItems"] = CollapseItems, ["cancel"] = Cancel,
            ["closeAria"] = CloseAria, ["help"] = Help, ["helpBack"] = HelpBack,
            ["labels.target"] = LabelTarget, ["labels.source"] = LabelSource, ["labels.portion"] = LabelPortion, ["labels.remaining"] = LabelRemaining,
            ["labels.duration"] = LabelDuration, ["labels.expiry"] = LabelExpiry, ["labels.blocked"] = LabelBlocked, ["labels.overflow"] = LabelOverflow,
            ["sourceRows.food"] = RowFood, ["sourceRows.potFood"] = RowPotFood, ["sourceRows.waterCard"] = RowWaterCard,
            ["sourceRows.potWater"] = RowPotWater, ["sourceRows.canteen"] = RowCanteen,
            ["portion.food"] = PortionFood, ["portion.water"] = PortionWater, ["portion.canteen"] = PortionCanteen,
            ["remaining.food"] = RemainingFood, ["remaining.water"] = RemainingWater, ["remaining.canteen"] = RemainingCanteen,
            ["helpTitle"] = HelpTitle, ["helpTopic"] = HelpTopic,
        };
    }

    /// <summary>
    /// 队伍面板随身展开行的逐字模板（Q273，assets/20261106/q269-q273-display-loot-adjudications.json companionRows）。
    /// 玩家可见文字不带实例编号；行与按钮仍按 InstanceId 绑定真实实例。
    /// </summary>
    public static class CompanionRowCopy
    {
        public const string Single = "{物名}";
        public const string Stack = "{物名}×{数量}";
        public const string FreshSuffix = "（保质到{截止}）";
        public const string SpoiledSuffix = "（已腐败）";
        public const string Pot = "{锅名}（{锅内水况}，食材{已占槽}/{总槽}）";
        public const string Canteen = "{壶名}（{壶内水况}）";

        /// <summary>合同 companionRows 的键 → 逐字模板，测试逐句比对。</summary>
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            ["single"] = Single, ["stack"] = Stack, ["freshSuffix"] = FreshSuffix, ["spoiledSuffix"] = SpoiledSuffix, ["pot"] = Pot, ["canteen"] = Canteen,
        };
    }

    /// <summary>物品卡上的食饮入口（Q263）：能显示才有，Blocked 非空时置灰。</summary>
    public sealed class FeedEntryInfo
    {
        public string Title { get; set; } = "";
        /// <summary>同类判定用的 itemId：食物为物品本身，水壶为壶内水种。</summary>
        public string ClassItemId { get; set; } = "";
        public bool IsWater { get; set; }
        public int InstanceId { get; set; }
        /// <summary>所点实例对应的来源行键，作开盘的待重验候选。</summary>
        public string SourceKey { get; set; } = "";
        public string OwnerActorId { get; set; } = "";
        /// <summary>全局或物品级原因；null 表示能开盘。</summary>
        public string? Blocked { get; set; }
    }

    /// <summary>未提交的食饮意图。只在界面，不进存档；FeedPanel 每次按真实状态校正。</summary>
    public sealed class FeedDraft
    {
        public string ClassItemId { get; set; } = "";
        public bool IsWater { get; set; }
        public string? TargetActorId { get; set; }
        public string? SourceKey { get; set; }
        /// <summary>候选被清过：显示 sourceCleared，直到玩家重新选目标或来源。</summary>
        public bool SourceCleared { get; set; }

        public static FeedDraft From(FeedEntryInfo e) => new FeedDraft { ClassItemId = e.ClassItemId, IsWater = e.IsWater, SourceKey = e.SourceKey };
    }

    /// <summary>来源下拉的一行：合法与不合法都列，不合法的 Blocked 写原物品级原因。</summary>
    public sealed class FeedRowInfo
    {
        public string Key { get; set; } = "";
        public string SourceActorId { get; set; } = "";
        public int InstanceId { get; set; }
        public int? PotInstanceId { get; set; }
        public FeedSourceKind Kind { get; set; }
        public string Text { get; set; } = "";
        public string PortionText { get; set; } = "";
        public string RemainingText { get; set; } = "";
        public int? ExpiresAt { get; set; }
        public string? Blocked { get; set; }
    }

    /// <summary>食饮局部盘四区的只读视图。</summary>
    public sealed class FeedPanelView
    {
        public string Title { get; set; } = "";
        public List<FeedTargetInfo> Targets { get; set; } = new List<FeedTargetInfo>();
        public List<FeedRowInfo> Rows { get; set; } = new List<FeedRowInfo>();
        public FeedRowInfo? Selected { get; set; }
        /// <summary>来源区的提示句。</summary>
        public List<string> SourceNotes { get; set; } = new List<string>();
        /// <summary>“不可用：原因”；没选来源时为 null。</summary>
        public string? BlockedLine { get; set; }
        /// <summary>预计变化区没有内容时的提示。</summary>
        public string? EffectHint { get; set; }
        public List<string> EffectLines { get; set; } = new List<string>();
        public List<string> StatLines { get; set; } = new List<string>();
        public string? RiskText { get; set; }
        public string? OverflowText { get; set; }
        public string ConfirmText { get; set; } = "";
        /// <summary>确认按钮不能点的原因；null 表示可以提交 Request。</summary>
        public string? ConfirmWhy { get; set; }
        public FeedRequest? Request { get; set; }
    }

    /// <summary>队伍面板展开的一行随身物品。</summary>
    public sealed class CompanionCarriedRow
    {
        public int InstanceId { get; set; }
        public string Text { get; set; } = "";
        /// <summary>锅里的内容，缩进显示。</summary>
        public bool InPot { get; set; }
        public FeedEntryInfo? Feed { get; set; }
    }

    /// <summary>
    /// 同行食饮的入口与局部盘（开发计划 v0.9 B29，R14、R67、R90—R93；Q262—Q265）：入口显示与置灰、同类来源行、四区视图、队伍面板随身物品。
    /// 全部只读：不消费、不改预留、不抽随机；真正提交仍走 Feed，判定优先顺序不改。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>战斗、撤出、敌踪、强制对白、行动占用等全局原因：拿空请求走 Validate，走到目标校验就说明全局没拦。</summary>
        private string? FeedGlobalBlocked(GameState s)
        {
            var why = CanStart(s, ActionRequest.FeedCompanion(new FeedRequest()));
            return why == FeedCopy.NoTarget ? null : why;
        }

        /// <summary>锅水按“锅+水种”一行，其他按实例一行。</summary>
        private string FeedRowKey(GameState s, ItemInstance i) =>
            i.InContainer && IsLiquid(i) && ContainerOf(s, i) is ItemInstance pot ? $"pot:{pot.InstanceId}:{i.ItemId}" : $"item:{i.InstanceId}";

        /// <summary>
        /// 物品卡入口（Q263）：没有在场合法同行、物品不在主角或在场同行者随身、不是食饮类型、空壶都隐藏（null）；基础满足后全局或物品级限制置灰。
        /// 需要目标名才能填的拒绝句留到选目标后。
        /// </summary>
        public FeedEntryInfo? FeedEntry(GameState s, int instanceId)
        {
            var targets = FeedTargets(s);
            var i = FindItem(s, instanceId);
            if (targets.Count == 0 || i == null || i.IsWorn) return null;
            var place = PlaceOf(s, i);
            var owner = place == ItemInstance.Carried ? GameState.PlayerId : targets.FirstOrDefault(t => ItemInstance.CarriedOwner(t.ActorId) == place)?.ActorId;
            if (owner == null) return null;
            var cfg = _items[i.ItemId];
            string classId;
            bool water;
            if (cfg.Canteen != null)
            {
                if (i.LiquidId == null || i.LiquidMl <= 0) return null;
                classId = i.LiquidId;
                water = true;
            }
            else
            {
                if (!IsFeedableUse(cfg)) return null;
                classId = i.ItemId;
                water = cfg.Kind == "water" || IsLiquid(i);
            }
            var probe = new FeedRequest { TargetActorId = owner == GameState.PlayerId ? "" : owner, SourceActorId = owner, InstanceId = instanceId };
            return new FeedEntryInfo
            {
                Title = water ? FeedPanelCopy.TitleWater : FeedPanelCopy.TitleFood, ClassItemId = classId, IsWater = water, InstanceId = instanceId,
                SourceKey = FeedRowKey(s, i), OwnerActorId = owner,
                Blocked = FeedGlobalBlocked(s) ?? FeedItemBlocked(s, probe, out _),
            };
        }

        /// <summary>
        /// 同类来源行（Q262）：主角在前、目标本人在后，同组按实例号；腐败、预留、加工、锁定、不足一份也列，第三人、地面、候补、设施不列。
        /// 目标不合法时为空。
        /// </summary>
        public List<FeedRowInfo> FeedClassRows(GameState s, string targetActorId, string classItemId)
        {
            var rows = new List<FeedRowInfo>();
            if (FeedTargetBlocked(s, targetActorId) != null) return rows;
            foreach (var source in new[] { GameState.PlayerId, targetActorId })
            {
                var root = ItemInstance.CarriedOwner(source);
                var seen = new HashSet<string>();
                foreach (var i in s.Items.Where(x => !x.IsWorn && PlaceOf(s, x) == root).OrderBy(x => x.InstanceId))
                {
                    var same = _items[i.ItemId].Canteen != null ? i.LiquidId == classItemId && i.LiquidMl > 0 : i.ItemId == classItemId;
                    if (same && seen.Add(FeedRowKey(s, i))) rows.Add(FeedRow(s, targetActorId, source, i));
                }
            }
            return rows;
        }

        private FeedRowInfo FeedRow(GameState s, string targetId, string source, ItemInstance i)
        {
            var why = FeedItemBlocked(s, new FeedRequest { TargetActorId = targetId, SourceActorId = source, InstanceId = i.InstanceId }, out var plan);
            var cfg = _items[i.ItemId];
            var pot = i.InContainer ? ContainerOf(s, i) : null;
            // 可饮份数：合法未锁未预留的水量；不合法批次写 0。毫升是物理余量，两者不混算
            var usable = (plan == null ? 0 : FeedAmounts(s, plan).portions).ToString();
            var who = ("出物人", ActorDisplayName(s, source));
            var id = i.InstanceId.ToString();
            var row = new FeedRowInfo { Key = FeedRowKey(s, i), SourceActorId = source, InstanceId = i.InstanceId, PotInstanceId = pot?.InstanceId, ExpiresAt = i.ExpiresAt, Blocked = why };
            if (cfg.Canteen != null)
            {
                var water = ("水种", ItemName(i.LiquidId!));
                var ml = ("毫升", i.LiquidMl.ToString());
                var cap = ("容量", cfg.Canteen.CapacityMl.ToString());
                row.Kind = FeedSourceKind.Canteen;
                row.Text = FeedCopy.Fill(FeedPanelCopy.RowCanteen, who, ("水壶", ItemName(i.ItemId)), ("壶号", id), water, ml, cap, ("可饮次数", usable));
                row.PortionText = FeedCopy.Fill(FeedPanelCopy.PortionCanteen, water);
                row.RemainingText = FeedCopy.Fill(FeedPanelCopy.RemainingCanteen, water, ml, cap);
            }
            else if (pot != null ? IsLiquid(i) : cfg.Kind == "water")
            {
                var water = ("水种", ItemName(i.ItemId));
                var ml = ("毫升", (pot != null
                    ? PotLiquids(s, pot.InstanceId).Where(w => w.ItemId == i.ItemId).Sum(w => w.Count) * PotConfig(pot).UnitMl
                    : i.Count * (cfg.UnitVolumeMl ?? 0)).ToString());
                row.Kind = pot != null ? FeedSourceKind.PotWater : FeedSourceKind.Water;
                row.Text = pot != null
                    ? FeedCopy.Fill(FeedPanelCopy.RowPotWater, who, ("锅号", pot.InstanceId.ToString()), water, ml, ("可饮份数", usable))
                    : FeedCopy.Fill(FeedPanelCopy.RowWaterCard, who, water, ml, ("可饮份数", usable), ("实例号", id));
                row.PortionText = FeedCopy.Fill(FeedPanelCopy.PortionWater, water);
                row.RemainingText = FeedCopy.Fill(FeedPanelCopy.RemainingWater, water, ml);
            }
            else
            {
                var item = ("物品", ItemName(i.ItemId));
                var count = ("总份数", i.Count.ToString());
                var expiry = ("截止段", i.ExpiresAt is int at ? " · " + FeedExpiry(at) : "");
                row.Kind = pot != null ? FeedSourceKind.PotFood : FeedSourceKind.Food;
                row.Text = pot != null
                    ? FeedCopy.Fill(FeedPanelCopy.RowPotFood, who, ("锅号", pot.InstanceId.ToString()), item, count, ("实例号", id), expiry)
                    : FeedCopy.Fill(FeedPanelCopy.RowFood, who, item, count, ("实例号", id), expiry);
                row.PortionText = FeedCopy.Fill(FeedPanelCopy.PortionFood, item);
                row.RemainingText = FeedCopy.Fill(FeedPanelCopy.RemainingFood, item, count);
            }
            return row;
        }

        private static string FeedExpiry(int at) => FeedCopy.Fill(FeedPanelCopy.LabelExpiry, ("截止", Clock.DayTime(at)));

        /// <summary>
        /// 局部盘视图，每次按真实状态重算并校正 draft：来源消失或不在两人范围就清源、记 SourceCleared，绝不自动换源；
        /// 来源还在但变非法就保留选中、禁确认、写原因。
        /// </summary>
        public FeedPanelView FeedPanel(GameState s, FeedDraft d)
        {
            var title = d.IsWater ? FeedPanelCopy.TitleWater : FeedPanelCopy.TitleFood;
            var v = new FeedPanelView { Title = title, ConfirmText = title, Targets = FeedTargets(s), ConfirmWhy = FeedCopy.NoTarget, EffectHint = FeedCopy.NoTarget };
            if (d.TargetActorId != null && v.Targets.All(t => t.ActorId != d.TargetActorId)) d.TargetActorId = null;
            if (d.TargetActorId == null)
            {
                v.SourceNotes.Add(FeedPanelCopy.BeforeTarget);
                return v;
            }

            var target = ("目标", ActorDisplayName(s, d.TargetActorId));
            v.Rows = FeedClassRows(s, d.TargetActorId, d.ClassItemId);
            var row = d.SourceKey == null ? null : v.Rows.FirstOrDefault(r => r.Key == d.SourceKey);
            if (d.SourceKey != null && row == null)
            {
                d.SourceKey = null;
                d.SourceCleared = true;
            }
            v.Selected = row;
            if (row == null)
            {
                v.ConfirmWhy = v.EffectHint = FeedPanelCopy.SourceRequired;
                if (d.SourceCleared) v.SourceNotes.Add(FeedPanelCopy.SourceCleared);
                if (v.Rows.Count == 0) v.SourceNotes.Add(FeedCopy.Fill(FeedPanelCopy.NoRows, target));
                else if (v.Rows.All(r => r.Blocked != null)) v.SourceNotes.Add(FeedPanelCopy.NoUsableRows);
                else if (!d.SourceCleared) v.SourceNotes.Add(FeedPanelCopy.SourceRequired);
                return v;
            }
            if (row.Blocked != null)
            {
                v.ConfirmWhy = row.Blocked;
                v.EffectHint = FeedPanelCopy.SourceRequired;
                v.BlockedLine = FeedCopy.Fill(FeedPanelCopy.LabelBlocked, ("原因", row.Blocked));
                return v;
            }

            var f = new FeedRequest { TargetActorId = d.TargetActorId, SourceActorId = row.SourceActorId, InstanceId = row.InstanceId };
            FeedItemBlocked(s, f, out var plan);
            f.ConfirmStamp = FeedStamp(s, plan!);
            var p = new ActionPreview();
            FillFeedPreview(s, p, plan!);
            v.EffectHint = null;
            v.EffectLines.Add(FeedCopy.Fill(FeedPanelCopy.LabelTarget, target));
            v.EffectLines.Add(FeedCopy.Fill(FeedPanelCopy.LabelSource, ("出物人", ActorDisplayName(s, row.SourceActorId))));
            v.EffectLines.Add(FeedCopy.Fill(FeedPanelCopy.LabelPortion, ("用量", row.PortionText)));
            v.EffectLines.Add(FeedCopy.Fill(FeedPanelCopy.LabelRemaining, ("余量", row.RemainingText)));
            if (row.ExpiresAt is int at) v.EffectLines.Add(FeedExpiry(at));
            v.EffectLines.Add(FeedPanelCopy.LabelDuration);
            v.StatLines = FeedStatLines(s, plan!, p.Feed!);
            v.RiskText = UseRiskText(_items[plan!.Item.LiquidId ?? plan.Item.ItemId]);
            v.OverflowText = p.Overflow.Count > 0 ? FeedPanelCopy.LabelOverflow : null;
            v.ConfirmText = p.ConfirmText!;
            v.Request = f;
            v.ConfirmWhy = FeedBlocked(s, f);
            if (v.ConfirmWhy != null) v.BlockedLine = FeedCopy.Fill(FeedPanelCopy.LabelBlocked, ("原因", v.ConfirmWhy));
            return v;
        }

        /// <summary>这份物品影响到的属性：目标本人确认那一刻的前后值（格式同卡下自用），不变的写{属性名}无变化。</summary>
        private List<string> FeedStatLines(GameState s, FeedPlan f, FeedPreviewInfo p)
        {
            var use = FeedUse(f);
            var lines = new List<string>();
            void Line(StatId id, double amount)
            {
                if (amount == 0) return;
                var name = StatName(id);
                var before = p.TargetBefore.Get(id);
                var after = p.TargetAfterImmediate.Get(id);
                lines.Add(before == after ? FeedCopy.Fill(FeedCopy.ZeroDelta, ("属性名", name)) : $"{name}{Fmt.N(before)}→{Fmt.N(after)}");
            }
            Line(StatId.Satiety, UseSatiety(s, use, f.TargetId));
            Line(StatId.Hydration, use.Hydration);
            Line(StatId.Morale, use.Morale);
            if (!use.IsBandage) Line(StatId.Health, use.Health);
            return lines;
        }

        /// <summary>
        /// 开发入口（B29）：记容量事实、招前两名固定队友同行，给两人随身放食物、腐败野果、水壶和带水的锅，主角要用的水与耗材放当前地面。
        /// Q273 起锅里另放一份野果、第二人随身放新鲜野果×2，让队伍展开区摆得出单份、多份、腐败、有保质、锅与锅内子项、水壶各种行。
        /// 同伴随身超过五卡不拦，只用来摆试玩场面。
        /// </summary>
        public string DebugFeedKit(GameState s)
        {
            s.Progress.Flags.Add("hub_registered");
            s.Progress.Flags.Add("church_route_record");
            s.Progress.Flags.Add("academy_safety_record");
            var recruited = Config.Party.NamedCompanions.Take(2).Select(n => n.ActorId).ToList();
            foreach (var id in recruited.Where(id => RosterOf(s, id) == null)) DebugRecruit(s, id);
            var first = ItemInstance.CarriedOwner(recruited[0]);
            var second = ItemInstance.CarriedOwner(recruited[1]);
            CreateItems(s, "canned_food", 2, first);
            CreateItems(s, "berry", 1, first).Single().ExpiresAt = s.GameMinute;
            var canteen = CreateItems(s, "ration_canteen", 1, first).Single();
            canteen.LiquidId = "clean_water";
            canteen.LiquidMl = 750;
            var pot = CreateItems(s, "pot", 1, first).Single();
            CreateItems(s, "clean_water", 2, ItemInstance.ContainerOwner(pot.InstanceId));
            CreateItems(s, "berry", 1, ItemInstance.ContainerOwner(pot.InstanceId));
            CreateItems(s, "canned_food", 1, second);
            CreateItems(s, "dirty_water", 1, second);
            CreateItems(s, "berry", 2, second);
            foreach (var (id, n) in new (string id, int n)[] { ("clean_water", 2), ("dirty_water", 3), ("canned_food", 1), ("pot", 1), ("purification_tablet", 1), ("filter_cartridge", 2), ("water_filter", 1) })
                CreateItems(s, id, n, s.Ground);
            var text = "开发：同行者已编入，两人随身放了食物、腐败野果、新鲜野果、水壶与带水和一份野果的锅；净水2、脏水3、罐头、锅、净水片、滤芯2、滤水器放在当前地面";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>
        /// 队伍面板展开的真实随身根卡与锅内内容（Q265）。只读，每行带这件东西的食饮入口。
        /// 行文字按 Q273 模板（CompanionRowCopy），不带实例编号；同名行不合并，按实例号稳定排序。
        /// </summary>
        public List<CompanionCarriedRow> CompanionCarried(GameState s, string actorId)
        {
            var rows = new List<CompanionCarriedRow>();
            CompanionCarriedRow Row(ItemInstance i, string text, bool inPot) =>
                new CompanionCarriedRow { InstanceId = i.InstanceId, Text = text, InPot = inPot, Feed = FeedEntry(s, i.InstanceId) };
            var root = ItemInstance.CarriedOwner(actorId);
            foreach (var i in s.Items.Where(x => x.Owner == root).OrderBy(x => x.InstanceId))
            {
                rows.Add(Row(i, IsPot(i) ? CompanionPotLine(s, i) : IsCanteen(i) ? CompanionCanteenLine(i) : CompanionItemLine(s, i), false));
                if (!IsPot(i)) continue;
                var inside = ItemInstance.ContainerOwner(i.InstanceId);
                rows.AddRange(s.Items.Where(x => x.Owner == inside).OrderBy(x => x.InstanceId).Select(x => Row(x, CompanionItemLine(s, x), true)));
            }
            return rows;
        }

        /// <summary>普通行与锅内子项：“罐头×2”“野果×2（已腐败）”“罐头（保质到第3日 08:00）”；一份省略×1，无截止不加后缀。</summary>
        public string CompanionItemLine(GameState s, ItemInstance i)
        {
            var name = InstanceName(i);
            var text = i.Count > 1
                ? FeedCopy.Fill(CompanionRowCopy.Stack, ("物名", name), ("数量", i.Count.ToString()))
                : FeedCopy.Fill(CompanionRowCopy.Single, ("物名", name));
            if (IsSpoiled(s, i)) return text + CompanionRowCopy.SpoiledSuffix;
            return i.ExpiresAt is int at ? text + FeedCopy.Fill(CompanionRowCopy.FreshSuffix, ("截止", Clock.DayTime(at))) : text;
        }

        /// <summary>“金属锅（净水100%·1000/1000毫升，食材1/2）”；水况沿 PotLabel（空锅原空态文字）。</summary>
        public string CompanionPotLine(GameState s, ItemInstance pot) =>
            FeedCopy.Fill(CompanionRowCopy.Pot, ("锅名", ItemName(pot.ItemId)), ("锅内水况", PotLabel(s, pot)),
                ("已占槽", PotFood(s, pot.InstanceId).Count.ToString()), ("总槽", PotConfig(pot).FoodSlots.ToString()));

        /// <summary>“分量水壶（净水 750/2000毫升）”；水况沿 CanteenLabel（空壶原空态文字）。</summary>
        public string CompanionCanteenLine(ItemInstance c) =>
            FeedCopy.Fill(CompanionRowCopy.Canteen, ("壶名", ItemName(c.ItemId)), ("壶内水况", CanteenLabel(c)));
    }
}
