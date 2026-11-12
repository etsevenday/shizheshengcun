using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41b 暂停主行动的「继续行动」完整只读重验与战后撤退离地保留行动的分来源返还文案
    /// （R05「B41b Q463—Q471」Q465、Q466、Q471，「Q472—Q476」Q474—Q476；逐字稿「B41b Q463—Q471 续接与返还逐字稿」；开发记录 B41b）。
    /// 全部挂在 <see cref="CardsV2"/> 后面：开关关时继续只验原有通用状态、退出沿原返还当前地面的规则与原句。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string ResumeTargetGone = "原行动目标已失效，无法继续，请退出行动。";
        public const string ResumeReserveChanged = "本次行动的预留已变化，无法继续，请退出行动。";
        public const string ResumeSpoiled = "原料已腐败，无法继续，请退出行动。";
        public const string ResumeToolGone = "原工具已不在，无法继续，请退出行动。";
        public const string ResumeCookAllDone = "原等待目标已全部完成，请退出等待。";

        /// <summary>
        /// 「继续行动」现在为什么不能继续（null 为可以）。检查与正式提交共用这一判定，只读：不推进时间、不扣物、不占新库存、不重抽、不改预留与原暂停原因。
        /// 新版敌踪下按 Q465 的顺序取第一条实际原因：通用状态 → 原地点／区域与地牢世代（仅撤退离地保留的行动）→ 原目标、本行动预留与工具 → 该用途开工条件；
        /// 已经开始的移动按 Q474 只验通用状态。开关关时只验原有通用状态。
        /// </summary>
        public string? ResumeBlockReason(GameState s)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            var a = s.Main;
            if (a == null || a.Status != ActionStatus.Paused) return "没有暂停中的行动";
            var v2 = CardsV2(s);
            // B41b：敌方接近打断后，战斗中不能继续原行动（战后按当前合法条件继续或退出）
            if (v2 && s.Battle != null) return "战斗中：请使用战斗指令";
            if (s.Sub != null) return v2 && a.Type != ActionType.Travel ? "当前行动还没结束。" : "请等待当前补给或维护完成";
            if (!v2 || a.Type == ActionType.Travel) return null;   // Q474：已开始的移动只验通用状态
            if (KeptWorkResumeBlock(s, a) is string where) return where;
            return WorkResumeBlock(s, a);
        }

        /// <summary>Q465 原目标、本行动预留、工具与各用途开工条件（人已在原地点／区域）。自己的预留与工具锁不算他人占用，已付的次数与消耗不再索要。</summary>
        private string? WorkResumeBlock(GameState s, ActionState a)
        {
            switch (a.Type)
            {
                case ActionType.Gather:
                {
                    if (!_res.TryGetValue(a.TargetId, out var res) || res.LocationId != a.SourceLocation
                        || !s.Resources.TryGetValue(res.NodeId, out var rs) || !rs.Discovered) return ResumeTargetGone;
                    if (FixedBlockHere(s) is string fixedWhy) return fixedWhy;
                    if (a.ReservedResourceUnits > 0 && rs.Reserved < a.ReservedResourceUnits) return ResumeReserveChanged;
                    if (a.ToolInstanceId is int tid)
                    {
                        // Q475：原指定采集工具缺失用采集原句；损坏、耐久不足沿开工原句，不自动换工具（原单次磨损完成时才提交，仍须够付）
                        var tool = FindItem(s, tid);
                        if (tool == null) return "这件工具已不在，请重新选择";
                        if (!OnPlayer(tool)) return "先拾取工具";
                        if (!HasWear(tool, GatherWear)) return tool.Durability <= 0 ? "这件工具已损坏，请重新选择" : $"这件工具耐久不足（{DurabilityText(tool)}），请维护或重新选择";
                    }
                    return null;
                }
                case ActionType.Craft:
                case ActionType.Build:
                case ActionType.Repair:
                case ActionType.Salvage:
                {
                    if (!_recipes.TryGetValue(a.TargetId, out var rc)) return ResumeTargetGone;
                    // 维护／拆解的锁定目标、现场修补的包、锅与催化物：原实例不在就不能继续，不暗换另一件
                    if ((rc.IsRepair || rc.IsSalvage) && a.ToolInstanceId is int target && FindItem(s, target) == null) return ResumeTargetGone;
                    if (a.PatchInstanceId is int patch && FindItem(s, patch) == null) return ResumeToolGone;
                    if (a.LockedItemIds.Any(id => FindItem(s, id) == null)) return ResumeToolGone;
                    if (ReservationChanged(s, a)) return ResumeReserveChanged;
                    if (AnyReservedSpoiled(s, a)) return ResumeSpoiled;
                    if (RecipeUnknownReason(s, rc) is string unknown) return unknown;
                    foreach (var kv in rc.MinSkills)
                    {
                        var lv = SkillLevel(s, kv.Key);
                        if (lv < kv.Value) return $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
                    }
                    // 建造位置与唯一性按完工重验同一口径（不把本次在建当成别人占用）
                    if (rc.FacilityResult != null && BuildSiteBlockedAtCompletion(s, rc) is string site) return site;
                    if (StationBlocked(s, rc) is string station) return station;
                    if (rc.Requirement != null)
                    {
                        var hereCfg = _locs[s.CurrentLocation];
                        var ground = s.Ground;
                        var fac = FacOf(s, ground) ?? s.Locations[s.CurrentLocation].Facility;
                        switch (rc.Requirement.Facility)
                        {
                            case "fire":
                                if (!HasFireAt(s, ground)) return "只能在营地火堆操作";
                                if (!fac.FireLit) return "需要燃烧的火堆";
                                if (fac.FuelMinutes < Tick) return "火堆燃料不足，请先投入木材";
                                break;
                            case "camp":
                                if (rc.Group == "workshop" && rc.FacilityResult != null) break;
                                if (!hereCfg.CanBuildShelter) return rc.IsBuild ? BuildOnlyAtCamp(hereCfg) : "只能在营地制作";
                                break;
                            case "shelter":
                                if (!hereCfg.CanBuildShelter) return BuildOnlyAtCamp(hereCfg);
                                if (!s.Locations[s.CurrentLocation].Facility.ShelterBuilt) return "需要先建成遮蔽棚";
                                break;
                        }
                    }
                    return null;
                }
                case ActionType.Butcher:
                {
                    var carcass = a.ReservedItemIds.Select(id => FindItem(s, id)).FirstOrDefault(i => i != null);
                    if (carcass == null || CarcassOf(carcass.ItemId) is not CarcassConfig c) return ResumeTargetGone;
                    if (ReservationChanged(s, a)) return ResumeReserveChanged;
                    if (IsSpoiled(s, carcass)) return "尸体已腐败，不能处理";
                    var tool = a.ToolInstanceId is int tid ? FindItem(s, tid) : null;
                    if (tool == null) return ResumeToolGone;
                    if (!OnPlayer(tool) || !HasWear(tool, c.ToolWearOnSuccess)) return $"需要随身有效的{string.Join("/", c.RequiredCarriedTools.Select(ItemName))}";
                    return null;
                }
                case ActionType.WaitCook:
                {
                    // 只验原绑定工序：不把后来放进火堆的新物品加进目标；已完成的保留成果，不重做
                    var fac = FacOf(s, a.TargetId);
                    if (fac == null || !HasFireAt(s, a.TargetId)) return ResumeTargetGone;
                    var open = a.CookTargets.Where(p => !a.CookDone.Contains(p) && !fac.Completed.Any(x => x.StartsWith(p + "@"))).ToList();
                    if (open.Count == 0) return ResumeCookAllDone;
                    if (open.Any(p => !fac.Processes.Any(x => x.ProcessId == p))) return ResumeTargetGone;
                    if (!fac.FireLit) return "火堆未燃烧：请先点火（等待加工不会自动点火）";
                    if (fac.FuelMinutes < Tick) return "火堆燃料不够下一段，请先投入木材（等待加工不会自动加木）";
                    return null;
                }
                case ActionType.Wait when IsCampingTarget(a.TargetId):
                    return a.LockedItemIds.Any(id => FindItem(s, id) == null) ? ResumeTargetGone : null;
                default:
                    return null;
            }
        }

        /// <summary>本行动的预留批次还在、仍标记为本行动（数量随整批实例保存，实例不在或标记被改即视为变化）。</summary>
        private bool ReservationChanged(GameState s, ActionState a) =>
            a.ReservedItemIds.Any(id => FindItem(s, id) is not ItemInstance i || i.ReservedBy != a.ActionId);

        // ───────────── Q466／Q471 分来源返还文案 ─────────────

        /// <summary>按 Q471 单套完整句改写的行动：制作（含固定等待净水）、建造、维护（含现场修补）、拆解、处理尸体（Q476）。</summary>
        private static bool KeptExitHasQ471Row(ActionState a) =>
            a.Type is ActionType.Craft or ActionType.Build or ActionType.Repair or ActionType.Salvage or ActionType.Butcher;

        /// <summary>本行动预留的实际来源（只读）：有没有原随身部分、原地面部分落在哪个地点／区域。</summary>
        private (bool carried, string? groundPlace) KeptSources(GameState s, ActionState a)
        {
            var carried = false;
            string? ground = null;
            foreach (var id in a.ReservedItemIds)
            {
                var inst = FindItem(s, id);
                if (inst == null) continue;
                var src = a.ReserveSources != null && a.ReserveSources.TryGetValue(id, out var o) ? o : inst.Owner;
                if (TryPlace(src, out _, out _)) ground ??= src;
                else carried = true;
            }
            return (carried, ground);
        }

        /// <summary>逐字稿 Q471 返还分句（预告或结果）；没有可返还材料时为空串。</summary>
        private string KeptReturnClause(GameState s, ActionState a, (bool carried, string? ground) src, bool result)
        {
            var place = src.ground != null ? ZoneName(s, src.ground) : "";
            if (a.Type == ActionType.Butcher)
            {
                if (src.ground != null) return result ? $"尸体已返还{place}地面。" : $"尸体会返还{place}地面。";
                if (src.carried) return result ? "尸体已解除预留，仍在随身。" : "尸体解除预留后留在随身。";
                return "";
            }
            if (src.carried && src.ground != null)
                return result ? $"随身预留材料已解除预留，仍在随身；地面预留材料已返还{place}地面。"
                              : $"随身预留材料解除预留后留在随身，地面预留材料会返还{place}地面。";
            if (src.carried) return result ? "随身预留材料已解除预留，仍在随身。" : "随身预留材料解除预留后留在随身。";
            if (src.ground != null) return result ? $"地面预留材料已返还{place}地面。" : $"地面预留材料会返还{place}地面。";
            return "";
        }

        /// <summary>Q471 完整句：引句＋真实返还分句＋后果＋「已耗时间不退。」（预告）／「已耗时间保留。」（结果）。</summary>
        private string KeptExitText(GameState s, ActionState a, (bool carried, string? ground) src, bool result)
        {
            var (lead, after) = a.Type switch
            {
                ActionType.Build => result ? ("已取消建造。", "本次建造进度已清除，没有建成。") : ("退出将放弃本次建造。", "本次建造进度会清除，不会建成。"),
                ActionType.Repair => result ? ("已取消维护。", "未完成本次维护。") : ("退出将放弃本次维护。", "不会完成本次维护。"),
                ActionType.Salvage => result ? ("已取消拆解。", "未获得拆解产物。") : ("退出将放弃本次拆解。", "不会获得拆解产物。"),
                ActionType.Butcher => result ? ("已取消处理尸体。", "保质期未刷新，没有产物。") : ("退出将放弃本次处理。", "保质期不刷新，不会获得产物。"),
                _ => result ? ("已取消制作。", "未生成半成品。") : ("退出将放弃本次制作。", "不会生成半成品。"),
            };
            return lead + KeptReturnClause(s, a, src, result) + after + (result ? "已耗时间保留。" : "已耗时间不退。");
        }

        /// <summary>撤退离地保留、按 Q471 改写退出句的行动（新版敌踪下）。</summary>
        private bool KeptExitApplies(GameState s, ActionState a) => CardsV2(s) && a.RetreatKept && !a.IsSub && KeptExitHasQ471Row(a);
    }
}
