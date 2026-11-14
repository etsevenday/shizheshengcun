using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41b 暂停主行动的「继续行动」完整只读重验与战后撤退离地保留行动的分来源返还文案
    /// （R05「B41b Q463—Q471」Q465、Q466、Q471，「Q472—Q476」Q474—Q476；逐字稿「B41b Q463—Q471 续接与返还逐字稿」；开发记录 B41b）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string ResumeTargetGone = "原行动目标已失效，无法继续，请退出行动。";
        public const string ResumeReserveChanged = "本次行动的预留已变化，无法继续，请退出行动。";
        public const string ResumeSpoiled = "原料已腐败，无法继续，请退出行动。";
        public const string ResumeToolGone = "原工具已不在，无法继续，请退出行动。";
        /// <summary>B46（Q536）起只剩空绑定列表这一种（原有）拒绝；原绑定目标全部完成时「继续行动」直接正常完成。</summary>
        public const string ResumeCookAllDone = "原等待目标已全部完成，请退出等待。";

        /// <summary>
        /// 「继续行动」现在为什么不能继续（null 为可以）。检查与正式提交共用这一判定，只读：不推进时间、不扣物、不占新库存、不重抽、不改预留与原暂停原因。
        /// 新版敌踪下按 Q465 的顺序取第一条实际原因：通用状态 → 原地点／区域与地牢世代（仅撤退离地保留的行动）→ 原目标、本行动预留与工具 → 该用途开工条件；
        /// 已经开始的移动按 Q474 只验通用状态。
        /// </summary>
        public string? ResumeBlockReason(GameState s)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            var a = s.Main;
            if (a == null || a.Status != ActionStatus.Paused) return "没有暂停中的行动";
            // B41b：敌方接近打断后，战斗中不能继续原行动（战后按当前合法条件继续或退出）
            if (s.Battle != null) return "战斗中：请使用战斗指令";
            // B41c（R05 Q491）、B46（Q534）：子行动未结束（含已开始的移动）统一用「当前行动还没结束。」
            if (s.Sub != null) return "当前行动还没结束。";
            if (a.Type == ActionType.Travel) return null;   // Q474：已开始的移动只验通用状态
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
                    // B41c（R05 Q490）：维护、现场修补与拆解在原目标资格阶段复核当前目标（真实归属、用途资格、耐久），跳过本行动自己的锁与预留
                    if ((rc.IsRepair || rc.IsSalvage) && a.ToolInstanceId is int tid2 && FindItem(s, tid2) is ItemInstance tgt
                        && ResumeTargetDurabilityBlock(s, a, rc, tgt) is string tgtWhy) return tgtWhy;
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
                    if (carcass == null || CarcassOf(OriginItemId(carcass)) is not CarcassConfig c) return ResumeTargetGone;
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
                    // B46（Q536）：原非空绑定目标全部有完成回执时可以继续，由 Resume 直接走原正常完成；空绑定仍沿原拒绝
                    if (open.Count == 0) return CookWaitAllDone(s, a) ? null : ResumeCookAllDone;
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

        /// <summary>这件物品被本行动以外的行动预留或锁住（本行动自己的目标锁／预留不算占用）。</summary>
        private bool OccupiedByOther(GameState s, ItemInstance i, ActionState a) =>
            (i.ReservedBy != null && i.ReservedBy != a.ActionId)
            || new[] { s.Main, s.Sub, s.StashedWork }.Any(x => x != null && !ReferenceEquals(x, a) && (x.LockedItemIds.Contains(i.InstanceId) || x.ToolInstanceId == i.InstanceId));

        /// <summary>
        /// Q490：维护（含现场修补）与拆解「继续行动」时的当前目标复核（只读，取第一条真实原因）。耐久与开工时不同本身不拒绝；
        /// 非有限的耐久按目标失效。维护：仍有耐久且未满（0 可维护）；现场修补：未满且缺损至少 0.1，原包仍须够恢复 0.1；拆解：仍符合拆解用途，耐久不限。
        /// </summary>
        /// <param name="locked">B41c2（逐字稿 Q525）：锁定行动的五句耐久原因只删去「，请退出行动」，具体原因与原判定顺序、阈值不变。</param>
        private string? ResumeTargetDurabilityBlock(GameState s, ActionState a, RecipeConfig rc, ItemInstance target, bool locked = false)
        {
            var tail = locked ? "" : "，请退出行动";
            var cfg = _items[target.ItemId];
            var gone = locked ? LockedTargetGone : ResumeTargetGone;
            if ((target.Durability is double d0 && !double.IsFinite(d0)) || (target.MaxDurability is double m0 && !double.IsFinite(m0))) return gone;
            if (rc.IsSalvage)
            {
                if (!cfg.IsEquipment) return $"{cfg.Name}不是装备，不能拆解";
                if (target.LoanSlot != null) return $"借用的{cfg.Name}不能拆解（RB01 借用物，不产出材料）";
                if (target.IsWorn) return target.IsWornBy(GameState.PlayerId) ? "穿戴中：先在装备区卸下再拆解" : "穿在别人身上，不能拆解";
                if (!(target.IsCarried || target.Owner == s.Ground)) return "只能拆解随身或当前地面的装备";
                if (OccupiedByOther(s, target, a)) return "行动占用：这件装备正被使用";
                if (target.LoadedAmmo > 0) return $"弹仓里还有{target.LoadedAmmo}发{ItemName(target.LoadedAmmoId ?? "")}，打空后再拆解";
                if (SalvageOutput(target) == null) return $"{cfg.Name}没有拆解表，暂不能拆解";
                var tier = EquipTier(target.ItemId);
                if (tier == null) return $"{cfg.Name}没有装备阶级，不能拆解";
                var need = SalvageCraftingLevel(tier.Value);
                if (need == null) return $"{cfg.Name}（{tier}阶）暂无可拆解的制造等级";
                var lv = SkillLevel(s, Crafting);
                if (lv < need) return $"拆解{tier}阶装备需要{SkillName(Crafting)}{need}级（当前{lv}级）";
                return null;
            }
            var patch = IsFieldPatchRecipe(rc) || a.PatchInstanceId != null;
            if (!patch)
            {
                if (IsFieldPatch(target)) return gone;
                if (!target.Durability.HasValue || !target.MaxDurability.HasValue) return $"{cfg.Name}没有耐久，无法继续维护{tail}。";
                if (!(OnPlayer(target) || target.Owner == s.Ground))
                    return target.InFacility ? $"先把{cfg.Name}从火堆取下" : "只能维护随身或当前地面的工具";
                if (OccupiedByOther(s, target, a)) return "行动占用：这件工具正被使用";
                if (!RepairFilterOk(rc, target.ItemId)) return $"{rc.Name}只修有金属制作或拆解依据的武器、工具或防具，{cfg.Name}不能用它修（枪械、锅、打火器和木石布皮制品不修）";
                if (target.Durability.Value >= target.MaxDurability.Value - 1e-9) return $"{cfg.Name}耐久已满，无法继续维护{tail}。";
                if (IsPot(target) && PotContents(s, target.InstanceId).Count > 0 && RepairRecipe?.RequireEmptyDetachedPotWhenTargetIsPot == true)
                    return "锅须先取下并清空才能维护";
                return null;
            }
            if (IsFieldPatch(target)) return gone;
            if (!target.Durability.HasValue || !target.MaxDurability.HasValue) return $"{cfg.Name}没有耐久，无法继续修补{tail}。";
            if (!(OnPlayer(target) || target.Owner == s.Ground))
                return target.InFacility ? $"先把{cfg.Name}从火堆取下" : "只能修补本人随身、穿戴中或当前地面的物品";
            if (OccupiedByOther(s, target, a)) return "行动占用：这件物品正被使用";
            // 旧档进行中的 B16 小修（没有绑定修补包）按 Q168 保留旧快照的范围，只复核耐久
            if (a.PatchInstanceId != null)
            {
                if (!FieldPatchEligible(target.ItemId))
                    return $"{rc.Name}只修有金属制作或拆解依据的武器、工具或防具，{cfg.Name}不能用它修（枪械、锅、打火器和木石布皮制品不修）";
                if (EquipmentRank(target.Quality) == null) return $"{cfg.Name}的品质不明，不能修补";
            }
            var d = target.Durability.Value;
            var m = target.MaxDurability.Value;
            if (d >= m - 1e-9) return $"{cfg.Name}耐久已满，无法继续修补{tail}。";
            if (System.Math.Floor((m - d) * 10 + 1e-6) < 1) return $"{cfg.Name}缺损不足0.1，无需修补{tail}。";
            if (a.PatchInstanceId is int pid && FindItem(s, pid) is ItemInstance pack && FieldPatchQuote(target, pack).restoreTenths < 1)
                return $"{ItemName(pack.ItemId)}剩余耐久{PatchRemaining(pack)}，不够恢复0.1";
            return null;
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

        /// <summary>撤退离地保留、按 Q471 改写退出句的行动。</summary>
        private bool KeptExitApplies(GameState s, ActionState a) => a.RetreatKept && !a.IsSub && KeptExitHasQ471Row(a);
    }
}
