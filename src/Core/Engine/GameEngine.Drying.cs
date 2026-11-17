using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>晾制架卡上一条食谱在某个晾制位的只读预览（投料按钮的禁用原因与确认浮窗共用；不改状态）。</summary>
    public sealed class DryingOption
    {
        public string RecipeId { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>要投入的晾制位（0—7）；没有空位为 -1。</summary>
        public int Slot { get; set; } = -1;
        public bool Ok { get; set; }
        public string? Reason { get; set; }
        /// <summary>“生肉×1＋盐×1”。</summary>
        public string InputText { get; set; } = "";
        /// <summary>这次会取的实例：“随身生肉#3×1（保质到第2日 06:00）、地面盐#9×1”（能撑到完成的先取，再按原顺序：先到期、随身优先）。</summary>
        public List<string> Sources { get; set; } = new List<string>();
        public int Minutes { get; set; }
        public int FinishMinute { get; set; }
        public string OutputText { get; set; } = "";
        public string XpText { get; set; } = "";
        public string Note { get; set; } = "";
        /// <summary>旧「会在完成前腐败」红字（Q214）；B50a（S11 文首 Q666）起全部晾制原料暂停保质，恒为空表（保留字段供界面兼容）。</summary>
        public List<string> Warnings { get; set; } = new List<string>();
        /// <summary>开始制作后暂停原料保质计时，取消后恢复剩余时长（Q215 肉干；B50a Q666 起全部晾制配方）。</summary>
        public bool PausesShelf { get; set; }
        /// <summary>
        /// Q214：这份预览的位号与逐条来源（“3|carried:12x1,ground:9x1”）。确认投料时带上它，内核按实时状态重算，不一致就拒绝并要求看更新后的预览再确认，不悄悄换料。
        /// </summary>
        public string SourceKey { get; set; } = "";
    }

    /// <summary>晾制架卡上一个晾制位的状态（只读）。</summary>
    public sealed class DryingSlotView
    {
        public int Slot { get; set; }
        /// <summary>empty / drying / done / spoiled。</summary>
        public string Status { get; set; } = "empty";
        public string? RecipeId { get; set; }
        /// <summary>B32b P02c「配方」行：这一位的食谱名；空位为 null。</summary>
        public string? RecipeName { get; set; }
        /// <summary>B32b P02c：「工位{序号}」（原「第k位 · …」，状态、配方、原料与进度各占一行）。</summary>
        public string Title { get; set; } = "";
        public string Line { get; set; } = "";
        /// <summary>位内物品：“生肉#12×1（保质到第2日 06:00）”。</summary>
        public List<string> Contents { get; set; } = new List<string>();
        public int DoneMinutes { get; set; }
        public int RequiredMinutes { get; set; }
        public int RemainingMinutes { get; set; }
        public string? XpText { get; set; }
        /// <summary>取消/取出/清理到随身、到当地地面现在不能做的原因；能做为 null。</summary>
        public string? ToCarriedBlocked { get; set; }
        public string? ToGroundBlocked { get; set; }
        /// <summary>取消按钮的说明（S11 文首 Q666 晾制取消说明）。</summary>
        public string CancelHint { get; set; } = "";
        /// <summary>肉干位原料保质暂停中（Q215）。</summary>
        public bool ShelfPaused { get; set; }
        /// <summary>停止位的展示原因（B50a，S11 文首 Q713）：missing 缺料、unknown 未知、spoiled 腐败（旧档 legacy 按 Q764 待定前沿现行腐败文字）；非停止位为 null。</summary>
        public string? StopKind { get; set; }
        /// <summary>缺料／未知停止位的清理悬停「取出剩余物品，清空该位。」；腐败位为 null（沿现行清理悬停）。</summary>
        public string? CleanupHint { get; set; }
    }

    /// <summary>
    /// 晾制架八位（策划答复 Q170，S11/A07「八位晾架完整定稿」、survival-adjudications.json drying；开发计划 v0.9 B22c，需求 R169）：
    /// 晾制架 drying_rack（木材4＋绳索2、60 基础分钟、制造1）按 Q107/Q134 建在 camp 外部区或已完工安全屋屋内，每区一座。8 个独立晾制位，每位一批原配方：
    /// 果干 野果2→1、120 分钟、烹饪1；肉干 生肉1＋盐1→1、180 分钟、烹饪5。当地安全空闲时在晾制架卡选食谱与空位，预览写明取哪几份
    /// （只取主角随身与当前区地面，队友包、异地不取），确认 0 分钟把原料原子迁进该位（owner drying:&lt;place&gt;#&lt;位号&gt;）。
    /// 按世界 5 分钟段被动推进：不吃熟练/装备加速、不耗火与燃料、不占角色行动、不增加身体消耗；离开也继续，暂停/读档不补时间，天气昼夜不改速。
    /// 保质（Q215 肉干，B50a S11 文首 Q666 起全部晾制配方含果干）：投料成功即暂停本批食品原料保质，快照剩余 R＝原截止−开始分钟，生产中不减；
    /// 完成前取消/退料时新截止＝退料世界分钟＋R（原无截止仍无），盐原样退回，本批进度清零、已过时间不退；53 前未受保护的旧在制位到原截止仍按原腐败停止，只能清理（取出）；完成核销生肉与盐、肉干从完成起正常计保质 168 小时。
    /// 选料（Q214 选 A）：只取主角随身与当前区地面；确认按实时状态重算，与预览不一致就拒绝、要求看更新后的预览再确认。完成一次核销原料，成品（完成起保质 168 小时）留在本位占位，直到在当地取出到随身（验五卡与 30 公斤）或当地地面。
    /// 经验：每批成功给首次投料且完成时仍存活的角色烹饪 clamp(ceil(原分钟/10),1,12)＝12（Q60 公式），与食品、净化共用每人每日 30（Q207），
    /// 只在完成那一刻结算一次；腐败/取消 0，取出/读档不重发。投料、取消、取出都带请求号幂等，不复制原料或成品。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string DryingStationId = "drying_rack";

        /// <summary>晾制架的工坊配置；配置里没有为 null。</summary>
        public StationConfig? DryingStation => Config.Workshops.Stations.FirstOrDefault(x => x.Id == DryingStationId);

        /// <summary>晾制位数（Q170：8）。</summary>
        public int DryingSlotCount => DryingStation?.Slots ?? 0;

        /// <summary>晾制食谱（果干、肉干）。</summary>
        public IEnumerable<RecipeConfig> DryingRecipes => Config.Recipes.Where(r => r.IsDrying);

        /// <summary>当前区已建成晾制架。</summary>
        public bool DryingRackHere(GameState s) => WorkshopBuilt(s, s.Ground, DryingStationId);

        /// <summary>某区某位的记录；空位为 null。</summary>
        public DryingSlot? DryingSlotAt(GameState s, string place, int slot) =>
            FacOf(s, place)?.DryingSlots.FirstOrDefault(b => b.Slot == slot);

        /// <summary>某区某位里的真实物品（晾制中为原料，完成为成品，腐败为原料）。</summary>
        public List<ItemInstance> DryingContents(GameState s, string place, int slot)
        {
            var owner = ItemInstance.DryingOwner(place, slot);
            return s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId).ToList();
        }

        /// <summary>当前区第一个空晾制位；都在用为 null。</summary>
        public int? FirstFreeDryingSlot(GameState s)
        {
            var fac = FacOf(s, s.Ground);
            if (fac == null) return null;
            for (var k = 0; k < DryingSlotCount; k++)
                if (fac.DryingSlots.All(b => b.Slot != k)) return k;
            return null;
        }

        /// <summary>每批成功的烹饪经验：Q60 公式 clamp(ceil(原分钟/10),1,12)（果干 120、肉干 180 分钟都是 12）。</summary>
        public int DryingXpFor(RecipeConfig rc)
        {
            var c = Config.Progression.Cooking;
            if (c.FoodHeatStepMinutes <= 0) return 0;
            return Math.Max(c.FoodMin, Math.Min(c.FoodMax, (int)Math.Ceiling(rc.BaseMinutes / (double)c.FoodHeatStepMinutes)));
        }

        /// <summary>Q215 暂停保质的晾制成品（肉干）。</summary>
        public const string DryingShelfPauseOutput = "dried_meat";

        /// <summary>这条晾制食谱投料成功后暂停原料保质（Q215 原仅肉干；B50a，S11 文首 Q666：所有真正启动的晾制配方，含果干）。</summary>
        public bool DryingPausesShelf(RecipeConfig rc) => rc.IsDrying;

        /// <summary>确认投料时实时重算与玩家看过的预览不一致（Q214）。</summary>
        public const string DryingPreviewChanged = "原料或晾制位有变化，预览已按现在的情况更新：请看新的来源与保质后再确认投料（不会自动换成另一份）";

        /// <summary>BM11（S08／S11 Q882 用户新裁定）：撤原“，取消后恢复剩余时长”，只留暂停句（最小删改，暂用待文案确认）。</summary>
        public const string DryingShelfPauseText = "开始制作后暂停原料保质计时";

        /// <summary>晾制取消说明（逐字稿 Q666「晾制取消说明」）。</summary>
        // BM11（Q882：取消ⓘ重复退料恢复剩余时长也删该操作解释）：只删首分句，其余原样。
        public const string DryingCancelHintText = "已过制作时间不退，没有成品，不给经验。";

        /// <summary>晾制保质帮助（逐字稿 Q666「晾制保质帮助」；放置位置 Q768 待定，暂无界面引用）。</summary>
        /// <summary>
        /// 晾制保质帮助短全文（B53a，S11 文首 2026-11-15 §Q768 选 A、逐字稿 dryShelf）：晾制架面板 ⓘ 与系统说明「保质与腐败」各一次。
        /// </summary>
        // BM11（S08／S11 Q882 用户新裁定）：晾制既有短说明换“晾制时原料暂停保质；成品正常计时。”。
        public const string DryingShelfHelpText = "晾制时原料暂停保质；成品正常计时。";

        /// <summary>晾制架面板 ⓘ 的被动工作信息（B53a，S11 §Q768、逐字稿 dryPassive）。</summary>
        public const string DryingPassiveText = "按世界时间推进，不占行动；离开继续，暂停不推进。";

        /// <summary>位内真实有腐败物时，说明与清理日志末各接一次（B53a，S11 文首 2026-11-15 §Q765、逐字稿 rotNotEdible）。</summary>
        private bool DryingHasRot(GameState s, IEnumerable<ItemInstance> items) => items.Any(i => IsSpoiledMatter(i) || IsSpoiled(s, i));

        /// <summary>暂停保质的原料退出晾制位时的新截止：退料世界分钟＋快照剩余（Q215）；没有暂停的原样。</summary>
        private static int? ResumedExpiry(GameState s, ItemInstance i) => EffectiveExpiry(s, i);   // B51a：与生产输入同一通用 C＋R

        /// <summary>当地安全空闲（晾制架卡全部操作的共同前提）；可以为 null。</summary>
        private string? DryingIdleBlocked(GameState s)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中不能操作晾制架";
            if (s.Main != null || s.Sub != null) return "行动进行中，先完成或取消再操作晾制架";
            if (!DryingRackHere(s)) return $"{ZoneName(s, s.Ground)}没有晾制架（只能操作当前区的晾制架）";
            return null;
        }

        /// <summary>
        /// 这一批要取的实例（Q214 选 A）：只取主角随身与当前区地面未预留、未占用、未腐败的（队友包、屋柜、穿戴、锅里、别的区都不取）；
        /// BM5b2（Q606）：每种输入按 UsableBatches 的顺序（截止先到先用、同截止随身优先、再实例号）配够，不再把截止晚于完成的批排在前面。
        /// 肉干也按这个顺序取，但肉干受 Q215 保护，不把“原截止早于完成”当作投后会腐败。
        /// </summary>
        private List<(ItemInstance inst, int take)> DryingSources(GameState s, RecipeConfig rc, int finish)
        {
            var list = new List<(ItemInstance, int)>();
            foreach (var kv in rc.Inputs)
            {
                var left = kv.Value;
                // BM5b2（S06 Q606）：取代 Q214「截止晚于完成的先取」排序，同一合格来源内按最早有效到期配够（果干临期警告不变）
                foreach (var w in UsableBatches(s, kv.Key, true).Where(w => !IsLocked(s, w)))
                {
                    if (left <= 0) break;
                    var take = Math.Min(left, w.Count);
                    list.Add((w, take));
                    left -= take;
                }
            }
            return list;
        }

        /// <summary>在当前区晾制架某位（null 为第一个空位）投一批现在不能做的原因；能做为 null。</summary>
        public string? DryingBlocked(GameState s, RecipeConfig rc, int? slot = null)
        {
            if (!rc.IsDrying) return "这不是晾制食谱";
            if (DryingIdleBlocked(s) is string idle) return idle;
            if (RecipeUnknownReason(s, rc) is string unknown) return unknown;
            foreach (var kv in rc.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) return $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
            }
            var n = DryingSlotCount;
            if (slot == null)
            {
                if (FirstFreeDryingSlot(s) == null) return $"{n}个晾制位都在用（每位一批），先取出成品、清理或取消一位再投";
            }
            else
            {
                if (slot < 0 || slot >= n) return "没有这个晾制位";
                if (DryingSlotAt(s, s.Ground, slot.Value) is DryingSlot used) return $"工位{slot + 1}正在用（{DryingStatusWord(used)}），请选空位";
            }
            foreach (var kv in rc.Inputs)
            {
                var have = UsableBatches(s, kv.Key, true).Where(w => !IsLocked(s, w)).Sum(w => w.Count);
                if (have >= kv.Value) continue;
                var near = Accessible(s, true).Where(i => i.ItemId == kv.Key).ToList();
                var spoiled = near.Where(i => IsSpoiled(s, i)).Sum(i => i.Count);
                var held = near.Where(i => !IsSpoiled(s, i) && (i.ReservedBy != null || IsLocked(s, i) || i.LoanSlot != null)).Sum(i => i.Count);
                return $"缺少{ItemName(kv.Key)}（随身与当前地面可用{have}/{kv.Value}"
                       + (spoiled > 0 ? $"，已腐败{spoiled}份不能用" : "")
                       + (held > 0 ? $"，已预留或占用{held}份" : "")
                       + "；队友包和别处的不自动取）";
            }
            return null;
        }

        private string DryingStatusWord(DryingSlot b) => b.Status switch
        {
            DryingSlot.Done => $"{ItemName(_recipes[b.RecipeId].Outputs.Keys.First())}已晾好，待取出",
            DryingSlot.Spoiled => DryingStopKind(b) switch
            {
                DryingSlot.StopMissing => DryingMissingStatusText,
                DryingSlot.StopUnknown => DryingUnknownStatusText,
                _ => "原料已腐败，待清理",
            },
            _ => $"{_recipes[b.RecipeId].Name} {b.DoneMinutes}/{b.RequiredMinutes}分钟",
        };

        /// <summary>
        /// 停止位按真实停止原因展示（B50a，S11 文首 Q713）：缺料 missing、未知 unknown 各用自己的文字；腐败 spoiled 与旧档 legacy（Q764 待定）沿现行腐败文字。
        /// 原因在停止那一刻记下，之后剩料再腐败也不改（「原停止原因仍缺料」）。
        /// </summary>
        public static string DryingStopKind(DryingSlot b) => b.StopReason switch
        {
            DryingSlot.StopMissing => DryingSlot.StopMissing,
            DryingSlot.StopUnknown => DryingSlot.StopUnknown,
            _ => DryingSlot.StopSpoiled,
        };

        // 逐字稿 2026-11-15 Q711—Q719 表：晾制缺料／未知停止正式句
        public const string DryingMissingStatusText = "原料不全，待清理";
        public const string DryingMissingXpText = "原料不全，无成品和经验。";
        public const string DryingMissingCancelText = "原料不全：请清理剩余物品。";
        public const string DryingUnknownStatusText = "加工已停止，待清理";
        public const string DryingUnknownXpText = "本批已停止，无成品和经验。";
        public const string DryingUnknownCancelText = "本批已停止：请清理剩余物品。";
        public const string DryingCleanupHintText = "取出剩余物品，清空该位。";

        // B50a（S11 文首 Q666）：原果干临期红字「会在完成前腐败，届时停止且没有成品」（Q214／M470）随全部晾制暂停保质撤去。

        private string InputText(RecipeConfig rc) => string.Join("＋", rc.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"));

        private string ExpiryNote(GameState s, ItemInstance i) =>
            i.ShelfPausedMinutes is int r ? $"保质暂停，剩余{Clock.Duration(r)}"
            : IsSpoiled(s, i) ? "已腐败" : i.ExpiresAt is int x ? $"保质到{Clock.DayTime(x)}" : "不腐败";

        private static string SourceKeyOf(int slot, IEnumerable<(ItemInstance inst, int take)> src) =>
            slot + "|" + string.Join(",", src.Select(x => $"{(x.inst.IsCarried ? "carried" : "ground")}:{x.inst.InstanceId}x{x.take}"));

        /// <summary>晾制架卡上一条食谱在某位（null 为第一个空位）的预览（只读）。</summary>
        public DryingOption DryingPreview(GameState s, string recipeId, int? slot = null)
        {
            var rc = _recipes[recipeId];
            var k = slot ?? FirstFreeDryingSlot(s) ?? -1;
            var finish = s.GameMinute + rc.BaseMinutes;
            var o = new DryingOption
            {
                RecipeId = rc.Id, Name = rc.Name, Slot = k, Minutes = rc.BaseMinutes, FinishMinute = finish, InputText = InputText(rc),
            };
            var outs = rc.Outputs.Select(kv => (cfg: _items[kv.Key], n: kv.Value)).ToList();
            o.OutputText = string.Join("、", outs.Select(x => $"{x.cfg.Name}×{x.n}" + (x.cfg.ShelfLifeMinutes is int sl ? $"（完成起保质{Clock.Duration(sl)}）" : "")));
            o.XpText = DryingXpPreview(s, rc);
            var pause = DryingPausesShelf(rc);
            o.PausesShelf = pause;
            o.Note = $"被动晾制{Clock.Duration(rc.BaseMinutes)}，预计{Clock.DayTime(finish)}完成：按世界时间推进，不吃熟练和装备加速、不耗火和燃料、不占行动；"
                     + "离开也继续，暂停不推进。"
                     + DryingShelfPauseText + "。"
                     + "完成品留在这一位，回到这里取出到随身或地面";
            var why = DryingBlocked(s, rc, slot);
            if (why == null || !why.StartsWith("缺少"))
            {
                var src = DryingSources(s, rc, finish);
                foreach (var (inst, take) in src)
                {
                    o.Sources.Add($"{(inst.IsCarried ? "随身" : "地面")}{ItemName(inst.ItemId)}×{take}（{ExpiryNote(s, inst)}）");
                }
                o.SourceKey = SourceKeyOf(k, src);
            }
            o.Reason = why;
            o.Ok = why == null;
            return o;
        }

        /// <summary>
        /// 在当前区晾制架投一批（0 分钟）：原料原子迁入该位并锁住，开始被动晾制。slot 为 null 时用第一个空位。
        /// expectedSourceKey 为玩家看过的预览（DryingOption.SourceKey）：确认时按实时状态重算，位号或任一来源/实例/数量变了就拒绝、不投（Q214：要求看更新后的预览再确认，不悄悄换料）；
        /// 为 null 时不比对（开发入口、旧调用）。肉干投料成功即暂停原料保质（Q215）：快照 R＝E−S 记在原料上、ExpiresAt 暂为空。
        /// </summary>
        public ActionResult StartDrying(GameState s, string recipeId, int? slot = null, string? requestId = null, string? expectedSourceKey = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (!_recipes.TryGetValue(recipeId, out var rc) || !rc.IsDrying) return ActionResult.Fail("没有这个晾制食谱");
            if (DryingBlocked(s, rc, slot) is string why) return ActionResult.Fail(why);
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            var k = slot ?? FirstFreeDryingSlot(s)!.Value;
            var owner = ItemInstance.DryingOwner(place, k);
            var finish = s.GameMinute + rc.BaseMinutes;
            var sources = DryingSources(s, rc, finish);
            if (expectedSourceKey != null && expectedSourceKey != SourceKeyOf(k, sources))
                return ActionResult.Fail(DryingPreviewChanged);
            var pause = DryingPausesShelf(rc);
            var taken = new List<string>();
            foreach (var (inst, take) in sources)
            {
                taken.Add($"{(inst.IsCarried ? "随身" : "地面")}{ItemName(inst.ItemId)}×{take}");
                ItemInstance moved;
                if (take < inst.Count) moved = SplitOff(s, inst, take);
                else moved = inst;
                moved.Owner = owner;
                moved.PlacedOrder = null;
                // Q215：暂停这份食品原料的保质计时，快照剩余 R＝E−S（未腐败保证 E＞S，R 为正整数世界分钟）；无截止（盐）不记，按无限
                if (pause && moved.ExpiresAt is int e)
                {
                    moved.ShelfPausedMinutes = e - s.GameMinute;
                    moved.ExpiresAt = null;
                }
            }
            Normalize(s, ItemInstance.Carried);
            Normalize(s, place);
            var id = s.NextActionId++;
            var slotRec = new DryingSlot
            {
                Slot = k, BatchId = id, RecipeId = rc.Id, Status = DryingSlot.Drying, StartMinute = s.GameMinute,
                RequiredMinutes = rc.BaseMinutes, InitiatorActorId = GameState.PlayerId, XpSnapshot = DryingXpFor(rc), ShelfPaused = pause,
            };
            fac.DryingSlots.Add(slotRec);
            RememberRequest(s, requestId);
            // B22c 试玩修复 M470：日志经验按这一位自身（self）算，不把刚投的这一位当成“先完成的晾制位”；原料会在完成前到期时日志也写警告
            Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}晾制架工位{k + 1}开始{rc.Name}：投入{string.Join("、", taken)}；被动{Clock.Duration(rc.BaseMinutes)}，"
                                        + $"预计{Clock.DayTime(finish)}完成（不占行动，离开也继续）；{DryingXpNote(s, slotRec)}"
                                        + (pause ? "；" + DryingShelfPauseText : "") + "。");
            UpdateFlags(s);
            return ActionResult.Success(id);
        }

        /// <summary>一组物品放进主角随身后的根卡数（口径同 RootCardsAfterOf）。</summary>
        private int RootCardsAfterAdding(GameState s, IEnumerable<ItemInstance> adds)
        {
            var stacks = new Dictionary<(string, int?, string?), (int n, int cap)>();
            var singles = 0;
            foreach (var i in s.Items.Where(i => i.Owner == ItemInstance.Carried).Concat(adds.Where(a => a.Owner != ItemInstance.Carried)))
            {
                if (StacksAsBatch(i)) AddStack(stacks, i, i.Count);
                else singles++;
            }
            return singles + CardsOfStacks(stacks);
        }

        /// <summary>把这一位的物品整位移到随身（验五卡与 30 公斤）或当地地面（不限）现在不能做的原因。</summary>
        private string? DryingMoveBlocked(GameState s, List<ItemInstance> items, bool toCarried)
        {
            if (!toCarried) return null;
            // 暂停保质的原料按退回后的截止合堆计卡（Q215）
            items = items.Select(i => { var c = i.Clone(); c.ExpiresAt = ResumedExpiry(s, i); c.ShelfPausedMinutes = null; return c; }).ToList();
            var after = RootCardsAfterAdding(s, items);
            if (!RootCardsAllowOf(s, GameState.PlayerId, after))
                return $"随身卡位不够（放入后{after}/{MaxRootCards}张），可改放到{ZoneName(s, s.Ground)}地面";
            var total = CarriedGrams(s) + items.Sum(i => ItemGrams(s, i));
            var hard = Config.Carry.HardGrams;
            if (total > hard) return $"放入后会携带{Fmt.N(total / 1000.0)}公斤，超过{hard / 1000}公斤，可改放到{ZoneName(s, s.Ground)}地面";
            return null;
        }

        /// <summary>取消这一位（晾制中、原料未腐败）现在不能做的原因；能做为 null。</summary>
        public string? DryingCancelBlocked(GameState s, int slot, bool toCarried)
        {
            if (DryingIdleBlocked(s) is string idle) return idle;
            var b = DryingSlotAt(s, s.Ground, slot);
            if (b == null) return $"工位{slot + 1}是空的";
            if (b.Status == DryingSlot.Done) return "已经晾好了：请取出成品";
            if (b.Status == DryingSlot.Spoiled) return DryingStopKind(b) switch
            {
                DryingSlot.StopMissing => DryingMissingCancelText,
                DryingSlot.StopUnknown => DryingUnknownCancelText,
                _ => "原料已腐败：只能清理（取出腐败原料），不能退回原料",
            };
            var items = DryingContents(s, s.Ground, slot);
            if (items.Any(i => IsSpoiled(s, i))) return "原料已腐败：只能清理（取出腐败原料），不能退回原料";
            return DryingMoveBlocked(s, items, toCarried);
        }

        /// <summary>取出这一位（成品，或腐败原料的清理）现在不能做的原因；能做为 null。</summary>
        public string? DryingTakeBlocked(GameState s, int slot, bool toCarried)
        {
            if (DryingIdleBlocked(s) is string idle) return idle;
            var b = DryingSlotAt(s, s.Ground, slot);
            if (b == null) return $"工位{slot + 1}是空的";
            if (b.Status == DryingSlot.Drying) return "还在晾制：可以取消（退回原料）";
            return DryingMoveBlocked(s, DryingContents(s, s.Ground, slot), toCarried);
        }

        private void MoveDryingOut(GameState s, List<ItemInstance> items, string dest)
        {
            foreach (var it in items)
            {
                // Q215：暂停保质的原料退出晾制位，新截止＝退料世界分钟 C＋快照剩余 R（不补回投料前已耗时间）
                ResumeShelf(s, it);   // B51a：通用恢复（与生产输入同一口径）
                it.Owner = dest;
                it.PlacedOrder = null;
                it.ReservedBy = null;
            }
            Normalize(s, dest);
        }

        private string DestName(GameState s, bool toCarried) => toCarried ? "随身" : $"{ZoneName(s, s.Ground)}地面";

        private static string ItemsText(GameEngine e, List<ItemInstance> items) =>
            string.Join("、", items.GroupBy(i => i.ItemId).Select(g => $"{e.ItemName(g.Key)}×{g.Sum(i => i.Count)}"));

        /// <summary>
        /// 取消晾制中的一位：果干原料按原截止退回随身或当地地面；肉干原料（Q215）新截止＝退料世界分钟＋投料时快照的剩余保质，盐原样退回。
        /// 本批进度清零、已过时间不退，没有成品、不给经验；目的地不合法整次拒绝、不动快照。带请求号幂等。
        /// </summary>
        public ActionResult CancelDrying(GameState s, int slot, bool toCarried, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (DryingCancelBlocked(s, slot, toCarried) is string why) return ActionResult.Fail(why);
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            var b = DryingSlotAt(s, place, slot)!;
            var items = DryingContents(s, place, slot);
            var what = ItemsText(this, items);
            fac.DryingSlots.Remove(b);
            MoveDryingOut(s, items, toCarried ? ItemInstance.Carried : place);
            RememberRequest(s, requestId);
            // BM11（S08／S11 Q882 用户新裁定）：取消返料不再附恢复保质说明（原“恢复投料时的剩余保质（保质到…）”“按原保质”），只报实际物品与去向
            var resume = $"{what}退回{DestName(s, toCarried)}";
            Emit(s, GameEventKind.ActionCancelled, $"已取消{ZoneName(s, place)}晾制架工位{slot + 1}的{_recipes[b.RecipeId].Name}（已晾{b.DoneMinutes}分钟，不退时间）："
                                                   + $"{resume}，没有成品，不给经验。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>取出一位：已晾好的成品，或腐败停止的那一位（腐败原料与未腐败辅料原样取出，按腐败物处理）。带请求号幂等，取出后空出这一位。</summary>
        public ActionResult TakeDrying(GameState s, int slot, bool toCarried, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (DryingTakeBlocked(s, slot, toCarried) is string why) return ActionResult.Fail(why);
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            var b = DryingSlotAt(s, place, slot)!;
            var items = DryingContents(s, place, slot);
            var spoiled = b.Status == DryingSlot.Spoiled;
            var plainStop = spoiled && DryingStopKind(b) != DryingSlot.StopSpoiled;
            var what = spoiled
                ? string.Join("、", items.Select(i => $"{ItemName(i.ItemId)}×{i.Count}{(IsSpoiled(s, i) && !IsSpoiledMatter(i) ? "（已腐败）" : "")}"))
                : string.Join("、", items.Select(i => $"{ItemName(i.ItemId)}×{i.Count}（{ExpiryNote(s, i)}）"));
            fac.DryingSlots.Remove(b);
            MoveDryingOut(s, items, toCarried ? ItemInstance.Carried : place);
            RememberRequest(s, requestId);
            // B50a（Q713）：缺料／未知停止位用正式清理句；位内已无物时只写该位已空，不造虚拟物品
            // B53a（S11 §Q765）：缺料／未知停止位清理时位内确有腐败物，句末接一次「腐败物不可食用。」；腐败停止位原句已有「腐败的不能吃」括注，沿原句（Q868 待答）
            var rotNote = plainStop && DryingHasRot(s, items) ? SpoiledNotEdibleText : "";
            Emit(s, GameEventKind.Info, plainStop
                ? (items.Count == 0 ? $"清理{ZoneName(s, place)}晾制架工位{slot + 1}：该位已空。"
                                    : $"清理{ZoneName(s, place)}晾制架工位{slot + 1}：{what}取到{DestName(s, toCarried)}，该位已空。{rotNote}")
                : spoiled
                ? $"清理{ZoneName(s, place)}晾制架工位{slot + 1}：{what}取到{DestName(s, toCarried)}（腐败的不能吃，按腐败物处理），这一位空出。"
                : $"从{ZoneName(s, place)}晾制架工位{slot + 1}取出{what}到{DestName(s, toCarried)}，这一位空出。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 每个世界 5 分钟段：全部区的晾制中各位先查原料是否到期（到期即腐败停止，不出成品），再 +5 分钟，达标即一次核销原料、成品留在本位、
        /// 给投料者经验一次。读档按存档里的进度续算，不重复出、不重发。
        /// </summary>
        private void AdvanceDrying(GameState s)
        {
            foreach (var (place, fac) in AllFacilityStates(s).ToList())
            {
                foreach (var b in fac.DryingSlots.Where(x => x.Status == DryingSlot.Drying).ToList())
                {
                    if (!_recipes.TryGetValue(b.RecipeId, out var rc)) continue;
                    var items = DryingContents(s, place, b.Slot);
                    var bad = items.FirstOrDefault(i => IsSpoiled(s, i));
                    var whole = rc.Inputs.All(kv => items.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) >= kv.Value);
                    if (bad != null || !whole)
                    {
                        b.Status = DryingSlot.Spoiled;
                        // B50a（Q713）：停止那一刻记真实原因（有腐败原料记腐败，否则缺料），之后不再改
                        b.StopReason = bad != null ? DryingSlot.StopSpoiled : DryingSlot.StopMissing;
                        b.EndMinute = s.GameMinute;
                        // BM5b2（Q610）：腐败的原料就地转为腐败物（静默，原料仍留这一位待清理），结果句接停工分句
                        var badName = bad != null ? ItemName(OriginItemId(bad)) : null;
                        var turned = bad != null && SpoilWorkInputs(s, items.Select(i => i.InstanceId).ToList());
                        Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}晾制架工位{b.Slot + 1}："
                                                    + (bad != null ? $"{badName}已腐败" : "原料不全")
                                                    + $"，{rc.Name}停止，没有成品、不给经验；原料留在这一位，回到当地清理。"
                                                    + (turned ? SpoiledWorkClause : ""));
                        continue;
                    }
                    b.DoneMinutes += Tick;
                    if (b.DoneMinutes < b.RequiredMinutes) continue;
                    var owner = ItemInstance.DryingOwner(place, b.Slot);
                    foreach (var it in items) s.Items.Remove(it);
                    var made = new List<ItemInstance>();
                    foreach (var kv in rc.Outputs) made.AddRange(CreateItems(s, kv.Key, kv.Value, owner));
                    b.Status = DryingSlot.Done;
                    b.EndMinute = s.GameMinute;
                    b.XpNote = DryingXp(s, b);
                    var outText = string.Join("、", made.Select(i => $"{ItemName(i.ItemId)}×{i.Count}（{ExpiryNote(s, i)}）"));
                    Emit(s, GameEventKind.FacilityDone, $"{ZoneName(s, place)}晾制架工位{b.Slot + 1}：{outText}晾好了，留在这一位，回到当地取出；{b.XpNote}。", null, new[] { DryingSlotHighlight(place, b.Slot) });
                }
            }
        }

        /// <summary>完成时给首次投料者（仍存活）快照经验，受完成日烹饪每日 30（含净化）；返回日志附注。只在这里结算一次。</summary>
        private string DryingXp(GameState s, DryingSlot b)
        {
            if (b.XpSnapshot <= 0) return "这一批不给经验";
            if (b.InitiatorActorId == null || !s.Actors.TryGetValue(b.InitiatorActorId, out var actor)
                || IsDead(s, actor)) return "投料者已不在，不给经验";
            if (SkillLevel(actor, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，不再获得经验";
            var day = Clock.Day(s.GameMinute);
            var grant = CookFoodRoom(actor, day, b.XpSnapshot);
            if (grant <= 0) return $"今日{SkillName(Cooking)}经验已满{Config.Progression.Cooking.FoodDailyCap}（含净化），不给经验";
            BumpDaily(s, actor, DailyXp, Cooking, day, grant);
            GainXp(s, actor, Cooking, grant);
            return grant < b.XpSnapshot
                ? $"{ActorPrefix(actor)}{SkillName(Cooking)}经验+{grant}（{CookTotalCapText}，今日只剩{grant}）"
                : $"{ActorPrefix(actor)}{SkillName(Cooking)}经验+{grant}";
        }

        /// <summary>
        /// 预估用：同一投料者其他晾制中的位里，预计在同一完成日、且比这一批先（同一刻按结算顺序排在前面）完成的快照经验合计。只用于显示，结算仍按完成那一刻的余额。
        /// </summary>
        private int DryingXpPending(GameState s, string actorId, int day, int finish, DryingSlot? self)
        {
            var flat = AllFacilityStates(s).SelectMany(x => x.Fac.DryingSlots).Where(b => b.Status == DryingSlot.Drying).ToList();
            var selfIdx = self == null ? flat.Count : flat.IndexOf(self);
            var sum = 0;
            for (var i = 0; i < flat.Count; i++)
            {
                var b = flat[i];
                if (b == self || b.XpSnapshot <= 0 || b.InitiatorActorId != actorId) continue;
                var f = s.GameMinute + Math.Max(0, b.RequiredMinutes - b.DoneMinutes);
                if (Clock.Day(f) != day) continue;
                if (f < finish || (f == finish && i < selfIdx)) sum += b.XpSnapshot;
            }
            return sum;
        }

        private string DryingXpText(GameState s, int xp, int finish, DryingSlot? self)
        {
            if (xp <= 0) return "不给经验";
            var cap = Config.Progression.Cooking.FoodDailyCap;
            var basic = $"完成后{SkillName(Cooking)}经验+{xp}（{CookTotalCapText}）";
            var who = s.Actors.TryGetValue(self?.InitiatorActorId ?? GameState.PlayerId, out var a) ? a : s.Actors[GameState.PlayerId];
            if (SkillLevel(who, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，完成不再获得经验";
            var day = Clock.Day(finish);
            var free = CookTotalRoom(who, day);
            var pending = DryingXpPending(s, who.Id, day, finish, self);
            var room = Math.Max(0, free - pending);
            // B22c 试玩修复 M470：当日已满时只写已满，不再附“先完成的晾制位预计占0”
            if (free <= 0) return $"{DayWord(s, day)}{SkillName(Cooking)}经验已满{cap}（含净化），完成不再给经验";
            var ahead = pending > 0 && room < xp ? $"；先完成的晾制位预计占{Math.Min(pending, free)}" : "";
            return room <= 0 ? $"{DayWord(s, day)}{SkillName(Cooking)}经验预计已满{cap}（含净化）{ahead}，完成不再给经验"
                : room < xp ? $"完成后{SkillName(Cooking)}经验+{room}（{CookTotalCapText}，{DayWord(s, day)}只剩{room}{ahead}）"
                : basic + (day == Clock.Day(s.GameMinute) ? "" : $"，按{DayWord(s, day)}额度");
        }

        /// <summary>投料前预估：这一批完成后的烹饪经验（按预计完成日余额，扣掉同日先完成的晾制位）。</summary>
        public string DryingXpPreview(GameState s, RecipeConfig rc) => DryingXpText(s, DryingXpFor(rc), s.GameMinute + rc.BaseMinutes, null);

        /// <summary>某位的经验说明：晾制中按快照与预计完成日余额；完成写实际结算；腐败写 0。</summary>
        public string DryingXpNote(GameState s, DryingSlot b) => b.Status switch
        {
            DryingSlot.Done => b.XpNote ?? "",
            DryingSlot.Spoiled => DryingStopKind(b) switch
            {
                DryingSlot.StopMissing => DryingMissingXpText,
                DryingSlot.StopUnknown => DryingUnknownXpText,
                _ => "原料腐败，不给经验",
            },
            _ => DryingXpText(s, b.XpSnapshot, s.GameMinute + Math.Max(0, b.RequiredMinutes - b.DoneMinutes), b),
        };

        /// <summary>当前区晾制架的 8 个位（只读，界面用）。</summary>
        public List<DryingSlotView> DryingSlotViews(GameState s)
        {
            var list = new List<DryingSlotView>();
            var place = s.Ground;
            for (var k = 0; k < DryingSlotCount; k++)
            {
                var v = new DryingSlotView { Slot = k };
                var b = DryingSlotAt(s, place, k);
                if (b == null)
                {
                    v.Title = CardStates.DryingSlotName(k);
                    v.Line = "可投一批果干或肉干";
                    list.Add(v);
                    continue;
                }
                var rc = _recipes[b.RecipeId];
                var items = DryingContents(s, place, k);
                v.Status = b.Status;
                v.RecipeId = b.RecipeId;
                v.RecipeName = rc.Name;
                v.DoneMinutes = b.DoneMinutes;
                v.RequiredMinutes = b.RequiredMinutes;
                v.Contents = items.Select(i => $"{ItemName(i.ItemId)}×{i.Count}（{ExpiryNote(s, i)}）").ToList();
                v.XpText = DryingXpNote(s, b);
                switch (b.Status)
                {
                    case DryingSlot.Drying:
                        var left = Math.Max(0, b.RequiredMinutes - b.DoneMinutes);
                        var finish = s.GameMinute + left;
                        v.RemainingMinutes = left;
                        v.Title = CardStates.DryingSlotName(k);
                        v.ShelfPaused = b.ShelfPaused || items.Any(i => i.ShelfPausedMinutes != null);
                        // B50a（S11 文首 Q666）：在制位说明用正式句，原果干临期红字撤去；取消说明换新句
                        v.Line = $"还需{Clock.Duration(left)}，预计{Clock.DayTime(finish)}完成" + (v.ShelfPaused ? "；" + DryingShelfPauseText : "");
                        v.CancelHint = DryingCancelHintText;
                        v.ToCarriedBlocked = DryingCancelBlocked(s, k, true);
                        v.ToGroundBlocked = DryingCancelBlocked(s, k, false);
                        break;
                    case DryingSlot.Done:
                        v.Title = CardStates.DryingSlotName(k);
                        v.Line = $"{(b.EndMinute is int e ? Clock.DayTime(e) + "完成" : "已完成")}，留在这一位占位，取出后空出";
                        v.ToCarriedBlocked = DryingTakeBlocked(s, k, true);
                        v.ToGroundBlocked = DryingTakeBlocked(s, k, false);
                        break;
                    default:
                        v.Title = CardStates.DryingSlotName(k);
                        v.StopKind = DryingStopKind(b);
                        v.Line = v.StopKind switch
                        {
                            DryingSlot.StopMissing => $"{rc.Name}已因缺料停止，无成品；清理可取回剩余物品。" + (DryingHasRot(s, items) ? SpoiledNotEdibleText : ""),
                            DryingSlot.StopUnknown => $"{rc.Name}已停止，无成品；清理可取回剩余物品。" + (DryingHasRot(s, items) ? SpoiledNotEdibleText : ""),
                            _ => $"{rc.Name}{(b.EndMinute is int m ? $"在{Clock.DayTime(m)}" : "")}停止，没有成品；清理即把原料原样取出（腐败的不能吃）",
                        };
                        v.CleanupHint = v.StopKind == DryingSlot.StopSpoiled ? null : DryingCleanupHintText;
                        v.ToCarriedBlocked = DryingTakeBlocked(s, k, true);
                        v.ToGroundBlocked = DryingTakeBlocked(s, k, false);
                        break;
                }
                list.Add(v);
            }
            return list;
        }

        /// <summary>晾制架卡一行摘要：“晾制中3 · 待取2 · 腐败1 · 空2”。</summary>
        public string DryingSummary(GameState s) => string.Join(" · ", DryingSummaryParts(s));

        /// <summary>摘要各段（卡面逐段不拆行显示，B22c 试玩修复 M470）。</summary>
        public List<string> DryingSummaryParts(GameState s)
        {
            var slots = FacOf(s, s.Ground)?.DryingSlots ?? new List<DryingSlot>();
            var drying = slots.Count(b => b.Status == DryingSlot.Drying);
            var done = slots.Count(b => b.Status == DryingSlot.Done);
            var stopped = slots.Where(b => b.Status == DryingSlot.Spoiled).Select(DryingStopKind).ToList();
            var missing = stopped.Count(k => k == DryingSlot.StopMissing);
            var unknown = stopped.Count(k => k == DryingSlot.StopUnknown);
            var bad = stopped.Count(k => k == DryingSlot.StopSpoiled);
            var free = Math.Max(0, DryingSlotCount - slots.Count);
            var parts = new List<string>();
            if (drying > 0) parts.Add($"晾制中{drying}");
            if (done > 0) parts.Add($"待取{done}");
            // B50a（Q713）：按停止原因分开计工位数（不是原料份数）
            if (missing > 0) parts.Add($"缺料{missing}");
            if (unknown > 0) parts.Add($"停止{unknown}");
            if (bad > 0) parts.Add($"腐败{bad}");
            parts.Add($"空{free}");
            return parts;
        }

        /// <summary>开发入口（?dev=1，B22c）：晾制架材料（木材4、绳索2）与 4 肉 4 果八批原料（生肉4、盐4、野果8）放当前地面。</summary>
        public string DebugDryingKit(GameState s)
        {
            foreach (var (id, n) in new[] { ("wood", 4), ("rope", 2), ("raw_meat", 4), ("salt", 4), ("berry", 8) }) CreateItems(s, id, n, s.Ground);
            Normalize(s, s.Ground);
            UpdateFlags(s);
            var text = $"开发：晾制架材料（木材×4、绳索×2）与八批原料（生肉×4、盐×4、野果×8）放在{ZoneName(s, s.Ground)}地面";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }
    }
}
