using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>B41c2 锁定行动「继续行动」复核的分类（S01 Q514、Q515）。</summary>
    public enum LockedResumeKind
    {
        /// <summary>可以继续，运行原工作。</summary>
        Ok,
        /// <summary>终局、未读对白、战斗、未结束子行动等更高输入锁：按原原因拦住，不借本条继续或中止背后的主行动。</summary>
        Higher,
        /// <summary>暂停态原本允许的操作能解除：保持暂停，只显示具体修复提示。</summary>
        Temporary,
        /// <summary>暂停态解除不了：真正点「继续行动」后按原取消规则自动中止。</summary>
        Permanent,
        /// <summary>锁定等待加工的原绑定目标全部有完成回执：真正点「继续行动」后走原正常完成路径（Q515）。</summary>
        CookDone,
    }

    /// <summary>锁定行动的只读复核结果。</summary>
    public sealed class LockedResumeVerdict
    {
        public LockedResumeKind Kind { get; set; }
        public string Reason { get; set; } = "";
        /// <summary>Q523：复核停在「只缺把同一原工具拾取到随身」这一支（含容量／重量不允许而转为永久的情形）时为该工具实例。</summary>
        public int? PickupToolId { get; set; }

        public static LockedResumeVerdict Ok() => new LockedResumeVerdict { Kind = LockedResumeKind.Ok };
        public static LockedResumeVerdict Higher(string why) => new LockedResumeVerdict { Kind = LockedResumeKind.Higher, Reason = why };
        public static LockedResumeVerdict Temp(string why) => new LockedResumeVerdict { Kind = LockedResumeKind.Temporary, Reason = why };
        public static LockedResumeVerdict Perm(string why) => new LockedResumeVerdict { Kind = LockedResumeKind.Permanent, Reason = why };
    }

    /// <summary>
    /// B41c2 开工 5 分钟的行动不可中途退出（S01「B41c Q506」「B41c2 Q514—Q519」「B41c2 Q523—Q527」，S07、S11、S14 同批节，R05 本批验收，
    /// 逐字稿「B41c2 Q514—Q519 锁定行动继续与结果逐字稿」「B41c2 Q523—Q527 例外、拒绝与帮助逐字稿」；开发记录 B41c2）。
    /// 认定、拒绝退出、继续复核与自动中止对所有行动生效（B41e3 起新版敌踪唯一流程，撤退离地保留等分支常开）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>认定不可退出的开工总时长（世界分钟）。</summary>
        public const int ExitLockMinutes = 5;

        // ───── 逐字稿原句 ─────
        public const string NoExitNote = "本次行动不可中途退出。";
        public const string NoCancelReason = "该行动不可中途取消";
        public const string LockedAbortNote = "本次行动无法继续，点击「继续行动」后将自动中止。";
        public const string LockedPausedBusy = "行动暂停中，请先继续行动。";
        public const string LockedSaveBlock = "行动暂停中，先继续行动再存档。";
        public const string LockedExpeditionAbortBlock = "行动进行中：先结束当前行动，再终止远征";
        public const string LockedTravelHover = "按真实路线移动。";
        public const string LockedTargetGone = "原行动目标已失效，无法继续。";
        public const string LockedReserveChanged = "本次行动的预留已变化，无法继续。";
        public const string LockedSpoiled = "原料已腐败，无法继续。";
        public const string LockedToolGone = "原工具已不在，无法继续。";
        public const string LockedPlaceGone = "原行动地点已失效，无法继续。";
        public const string LockedGatherToolGone = "这件工具已不在，无法继续。";
        public const string LockedGatherToolBroken = "这件工具已损坏，无法继续。";
        public const string LockedSubRunning = "当前行动还没结束。";
        public const string ElapsedKept = "已耗时间保留。";

        /// <summary>B46（Q532、逐字稿「B41c2 Q531—Q534 实施补充逐字稿」）：锁定采集／处理尸体只缺拾取同一有效原工具时的复核与继续拒绝句。</summary>
        public string LockedPickupOwnTool(ItemInstance tool) => $"需要先拾取原工具：{ItemName(tool.ItemId)}。";

        /// <summary>开工后不可中途退出（运行、暂停、子行动都一样）：本次开工认定的锁定，加上原本就不可取消的单段事务与潜行接近／躲避（旧档原限制保持）。</summary>
        public static bool ExitLocked(ActionState a) => a.NoExit || IsSingleTransaction(a.Type) || IsStealthCheck(a);

        /// <summary>主行动是锁定行动且处于暂停（锁定版暂停提示句的适用条件）。</summary>
        public static bool LockedMainPaused(GameState s) => s.Main is ActionState m && !m.IsSub && m.Status == ActionStatus.Paused && ExitLocked(m);

        /// <summary>
        /// 存档 48 及以前的在办行动迁移时的一次性认定（Q516、Q524）：只有原固定计划可信——固定时长计划为 5 分钟、固定等待处理的配方为 5 分钟，
        /// 以及原本就不可取消的单段事务与潜行接近／躲避。工作量型与移动没有开工同源预计，等待加工的计划分钟混有兜底值，一律不锁。
        /// </summary>
        public static bool LegacyFixedPlanLock(ActionState a)
        {
            if (IsSingleTransaction(a.Type) || IsStealthCheck(a)) return true;
            if (a.Type == ActionType.WaitCook) return false;
            if (IsFixedDuration(a.Type)) return a.PlannedMinutes == ExitLockMinutes;
            if (a.FixedWait) return System.Math.Abs(a.BaseWorkMinutes - ExitLockMinutes) < 1e-9;
            return false;
        }

        /// <summary>
        /// 成功开工同一事务里的认定（Q516）：固定时长取真实计划总时长；工作、移动、等待加工与固定等待处理取与中央条／正式预览同源的模拟预计
        /// （原倍率、余量、5 分钟段取整都在里面），恰好一段就结束才锁。模拟不出可信结果（燃料不够会停、途中死亡）不猜 5。只读：在副本上模拟。
        /// </summary>
        private bool StartLocks(GameState s, ActionState a)
        {
            if (IsFixedDuration(a.Type)) return a.PlannedMinutes == ExitLockMinutes;
            // 新规则工作每段最多推进 5 分钟工作量（时间倍率不小于 1）：剩余需求超过一段的不可能 5 分钟完成，省掉模拟
            if (a.IsWorkType && !a.FixedWait && a.RuleVersion >= WorkRuleVersion && a.BaseWorkMinutes - a.CarryReserved > ExitLockMinutes + Eps) return false;
            var end = EstimateEndMinute(s, 2);
            return end.HasValue && end.Value - s.GameMinute == ExitLockMinutes;
        }

        // ───────────────────────── Q514／Q515／Q523 继续复核 ─────────────────────────

        /// <summary>
        /// 锁定主行动「继续行动」的只读复核（Q514）：先更高输入锁，再撤退离地保留的原地点／世代（Q440），再按原目标、预留、工具、用途准入的实际失败分支分类，
        /// 只给当前第一条实际原因。不推进时间、不扣物、不释放、不改暂停原因。主行动不是锁定暂停时为 Higher。
        /// </summary>
        public LockedResumeVerdict LockedResume(GameState s)
        {
            if (s.Progress.Dead) return LockedResumeVerdict.Higher("角色已死亡");
            if (DialogueLocked(s)) return LockedResumeVerdict.Higher(DialogueLockedReason);
            var a = s.Main;
            if (a == null || a.Status != ActionStatus.Paused || !ExitLocked(a)) return LockedResumeVerdict.Higher("没有暂停中的行动");
            if (s.Battle != null) return LockedResumeVerdict.Higher("战斗中：请使用战斗指令");
            if (s.Sub != null) return LockedResumeVerdict.Higher(LockedSubRunning);
            if (a.Type == ActionType.Travel) return LockedResumeVerdict.Ok();   // Q474：已开始的移动只验通用状态
            if (a.RetreatKept)
            {
                if (a.StartGeneration is int g && DungeonGenerationAt(s, a.SourceLocation) != g) return LockedResumeVerdict.Perm(LockedPlaceGone);
                if (s.CurrentLocation != a.SourceLocation || s.CurrentZone != (a.StartZone ?? ExteriorZone))
                    return LockedResumeVerdict.Temp($"请返回{WorkPlaceName(s, a)}后再继续{a.Label}。");
            }
            return LockedWorkCheck(s, a);
        }

        /// <summary>Q514 分类表按用途的实际失败分支（人已在原地点／区域）。原因原句按逐字稿锁定版；其他已有具体原因沿原句。</summary>
        private LockedResumeVerdict LockedWorkCheck(GameState s, ActionState a)
        {
            switch (a.Type)
            {
                case ActionType.Gather:
                {
                    if (!_res.TryGetValue(a.TargetId, out var res) || res.LocationId != a.SourceLocation
                        || !s.Resources.TryGetValue(res.NodeId, out var rs) || !rs.Discovered) return LockedResumeVerdict.Perm(LockedTargetGone);
                    if (FixedBlockHere(s) is string fixedWhy) return LockedResumeVerdict.Perm(fixedWhy);
                    if (a.ReservedResourceUnits > 0 && rs.Reserved < a.ReservedResourceUnits) return LockedResumeVerdict.Perm(LockedReserveChanged);
                    if (a.ToolInstanceId is int tid)
                    {
                        var tool = FindItem(s, tid);
                        if (tool == null || (!OnPlayer(tool) && !OwnGroundTool(s, a, tool))) return LockedResumeVerdict.Perm(LockedGatherToolGone);
                        if (!HasWear(tool, GatherWear))
                            return LockedResumeVerdict.Perm(tool.Durability <= 0 ? LockedGatherToolBroken : $"这件工具耐久不足（{DurabilityText(tool)}），无法继续。");
                        if (!OnPlayer(tool)) return ToolPickupVerdict(s, tool, LockedPickupOwnTool(tool));   // B46（Q532）：与处理尸体同句，取原工具公开名
                    }
                    return LockedResumeVerdict.Ok();
                }
                case ActionType.Craft:
                case ActionType.Build:
                case ActionType.Repair:
                case ActionType.Salvage:
                {
                    if (!_recipes.TryGetValue(a.TargetId, out var rc)) return LockedResumeVerdict.Perm(LockedTargetGone);
                    if ((rc.IsRepair || rc.IsSalvage) && a.ToolInstanceId is int target && FindItem(s, target) == null) return LockedResumeVerdict.Perm(LockedTargetGone);
                    if ((rc.IsRepair || rc.IsSalvage) && a.ToolInstanceId is int tid2 && FindItem(s, tid2) is ItemInstance tgt
                        && ResumeTargetDurabilityBlock(s, a, rc, tgt, true) is string tgtWhy) return LockedResumeVerdict.Perm(tgtWhy);
                    if (a.PatchInstanceId is int patch && FindItem(s, patch) == null) return LockedResumeVerdict.Perm(LockedToolGone);
                    if (a.LockedItemIds.Any(id => FindItem(s, id) == null)) return LockedResumeVerdict.Perm(LockedToolGone);
                    if (ReservationChanged(s, a)) return LockedResumeVerdict.Perm(LockedReserveChanged);
                    if (AnyReservedSpoiled(s, a)) return LockedResumeVerdict.Perm(LockedSpoiled);
                    if (RecipeUnknownReason(s, rc) is string unknown) return LockedResumeVerdict.Perm(unknown);
                    foreach (var kv in rc.MinSkills)
                    {
                        var lv = SkillLevel(s, kv.Key);
                        if (lv < kv.Value) return LockedResumeVerdict.Perm($"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）");
                    }
                    if (rc.FacilityResult != null && BuildSiteBlockedAtCompletion(s, rc) is string site) return LockedResumeVerdict.Perm(site);
                    if (StationBlocked(s, rc) is string station) return LockedResumeVerdict.Perm(station);
                    if (rc.Requirement != null)
                    {
                        var hereCfg = _locs[s.CurrentLocation];
                        var ground = s.Ground;
                        var fac = FacOf(s, ground) ?? s.Locations[s.CurrentLocation].Facility;
                        switch (rc.Requirement.Facility)
                        {
                            case "fire":
                                if (!HasFireAt(s, ground)) return LockedResumeVerdict.Perm("只能在营地火堆操作");
                                if (!fac.FireLit) return LockedResumeVerdict.Temp("需要燃烧的火堆");
                                if (fac.FuelMinutes < Tick) return LockedResumeVerdict.Temp("火堆燃料不足，请先投入木材");
                                break;
                            case "camp":
                                if (rc.Group == "workshop" && rc.FacilityResult != null) break;
                                if (!hereCfg.CanBuildShelter) return LockedResumeVerdict.Perm(rc.IsBuild ? BuildOnlyAtCamp(hereCfg) : "只能在营地制作");
                                break;
                            case "shelter":
                                if (!hereCfg.CanBuildShelter) return LockedResumeVerdict.Perm(BuildOnlyAtCamp(hereCfg));
                                if (!s.Locations[s.CurrentLocation].Facility.ShelterBuilt) return LockedResumeVerdict.Perm("需要先建成遮蔽棚");
                                break;
                        }
                    }
                    return LockedResumeVerdict.Ok();
                }
                case ActionType.Butcher:
                {
                    var carcass = a.ReservedItemIds.Select(id => FindItem(s, id)).FirstOrDefault(i => i != null);
                    if (carcass == null || CarcassOf(carcass.ItemId) is not CarcassConfig c) return LockedResumeVerdict.Perm(LockedTargetGone);
                    if (ReservationChanged(s, a)) return LockedResumeVerdict.Perm(LockedReserveChanged);
                    if (IsSpoiled(s, carcass)) return LockedResumeVerdict.Perm("尸体已腐败，不能处理");
                    var missing = $"缺少本次处理所需的有效{string.Join("/", c.RequiredCarriedTools.Select(ItemName))}，无法继续。";
                    var tool = a.ToolInstanceId is int tid ? FindItem(s, tid) : null;
                    if (tool == null || !HasWear(tool, c.ToolWearOnSuccess) || (!OnPlayer(tool) && !OwnGroundTool(s, a, tool))) return LockedResumeVerdict.Perm(missing);
                    if (!OnPlayer(tool)) return ToolPickupVerdict(s, tool, LockedPickupOwnTool(tool));
                    return LockedResumeVerdict.Ok();
                }
                case ActionType.WaitCook:
                {
                    var fac = FacOf(s, a.TargetId);
                    if (fac == null || !HasFireAt(s, a.TargetId)) return LockedResumeVerdict.Perm(LockedTargetGone);
                    var open = a.CookTargets.Where(p => !a.CookDone.Contains(p) && !fac.Completed.Any(x => x.StartsWith(p + "@"))).ToList();
                    if (CookWaitAllDone(s, a)) return new LockedResumeVerdict { Kind = LockedResumeKind.CookDone };   // B46（Q536）：空绑定不算全部完成
                    if (open.Any(p => !fac.Processes.Any(x => x.ProcessId == p))) return LockedResumeVerdict.Perm(LockedTargetGone);
                    if (!fac.FireLit) return LockedResumeVerdict.Temp("火堆未燃烧：请先点火（等待加工不会自动点火）");
                    if (fac.FuelMinutes < Tick) return LockedResumeVerdict.Temp("火堆燃料不够下一段，请先投入木材（等待加工不会自动加木）");
                    return LockedResumeVerdict.Ok();
                }
                case ActionType.Wait when IsCampingTarget(a.TargetId):
                    return a.LockedItemIds.Any(id => FindItem(s, id) == null) ? LockedResumeVerdict.Perm(LockedTargetGone) : LockedResumeVerdict.Ok();
                default:
                    return LockedResumeVerdict.Ok();
            }
        }

        /// <summary>Q523：原工具确由本行动自己锁住、整件在当前地点当前区域地面、没有被预留／部署／他人占用。</summary>
        private bool OwnGroundTool(GameState s, ActionState a, ItemInstance tool) =>
            tool.Owner == s.Ground && a.LockedItemIds.Contains(tool.InstanceId) && tool.ReservedBy == null && !IsDeployed(s, tool) && !OccupiedByOther(s, tool, a);

        /// <summary>
        /// Q523：只缺把同一有效原工具拾取到本人随身。原容量／重量预演允许为暂时（保持暂停、开放这一项拾取）；不允许为永久（卡位优先于重量，只取第一条）。
        /// </summary>
        private LockedResumeVerdict ToolPickupVerdict(GameState s, ItemInstance tool, string tempWhy)
        {
            var v = LockedToolCapacityBlock(s, tool) is string cap ? LockedResumeVerdict.Perm(cap) : LockedResumeVerdict.Temp(tempWhy);
            v.PickupToolId = tool.InstanceId;
            return v;
        }

        /// <summary>例外拾取的原容量／重量预演（逐字稿 Q523 两句）；都允许为 null。</summary>
        private string? LockedToolCapacityBlock(GameState s, ItemInstance tool)
        {
            if (MaxIntoCarriedByCards(s, tool, tool.Count) < tool.Count) return $"随身卡位已满（{RootCardCount(s)}/{MaxRootCards}张），无法拾取原工具。";
            if (MaxPickupBatch(s, tool, tool.Count) < tool.Count) return $"拾取后将超过{Config.Carry.HardGrams / 1000}公斤，无法拾取原工具。";
            return null;
        }

        /// <summary>Q523：此刻按例外可拾取（或拾取请求会触发自动中止）的原工具实例；不适用为 null。只读。</summary>
        public ItemInstance? LockedPickupTool(GameState s)
        {
            if (s.Expedition?.IsExtracting == true) return null;
            var v = LockedResume(s);
            if (v.PickupToolId is not int id || (v.Kind != LockedResumeKind.Temporary && v.Kind != LockedResumeKind.Permanent)) return null;
            return FindItem(s, id) is ItemInstance t && t.Owner == s.Ground ? t : null;
        }

        /// <summary>这件物品此刻能按 Q523 例外从当前地面拾取到随身（容量／重量也允许）。</summary>
        public bool LockedToolPickupOpen(GameState s, ItemInstance inst) =>
            LockedPickupTool(s) is ItemInstance t && t.InstanceId == inst.InstanceId && LockedToolCapacityBlock(s, t) == null;

        /// <summary>
        /// Q523 例外拾取的真实提交：整件、0 分钟、同一实例、本行动的工具锁与暂停不变（成功后仍须手动继续）；原普通拾取的成功反馈照旧。
        /// 容量／重量重验不允许时按原取消规则自动收束本次行动（工具不移动）。返回 null 表示这件物品不适用例外，走原拾取校验。
        /// </summary>
        private ActionResult? LockedToolPickup(GameState s, InstantRequest r)
        {
            var tool = LockedPickupTool(s);
            if (tool == null || tool.InstanceId != r.InstanceId) return null;
            if (r.Count != tool.Count) return ActionResult.Fail("数量无效");
            var m = s.Main!;
            if (LockedToolCapacityBlock(s, tool) is string cap)
            {
                AbortLocked(s, m, cap);
                return new ActionResult { Ok = true, ActionId = m.ActionId, Aborted = true, Reason = cap };
            }
            MoveItems(s, tool, tool.Count, ItemInstance.Carried);
            NoteTransfer(s);   // B13 PRO11（同原普通拾取）
            UpdateFlags(s);
            return ActionResult.Success();
        }

        // ───────────────────────── Q514／Q515 真实继续 ─────────────────────────

        /// <summary>
        /// 锁定主行动的真实「继续行动」（在原 Resume 之前）：更高锁与暂时原因按原因拒绝、保持暂停；永久原因自动中止同一行动；
        /// 等待加工原目标全部有完成回执时按原完成路径结束（Q515）。可以继续时返回 null，由原 Resume 置回运行。
        /// commit 为 false（预览／估算副本）时永久与完成都只拒绝，不改状态。
        /// </summary>
        private ActionResult? LockedResumeCommit(GameState s, bool commit)
        {
            if (!LockedMainPaused(s)) return null;
            var v = LockedResume(s);
            var m = s.Main!;
            switch (v.Kind)
            {
                case LockedResumeKind.Ok:
                    return null;
                case LockedResumeKind.Permanent when commit:
                    AbortLocked(s, m, v.Reason);
                    return new ActionResult { Ok = true, ActionId = m.ActionId, Aborted = true, Reason = v.Reason };
                case LockedResumeKind.CookDone when commit:
                    FinishCookWaitNow(s, m);
                    return ActionResult.Success(m.ActionId);
                case LockedResumeKind.CookDone:
                    return ActionResult.Fail("原等待目标已全部完成");
                default:
                    return ActionResult.Fail(v.Reason);
            }
        }

        /// <summary>
        /// B46（S01「B41c2 Q535—Q536 试玩批定稿」Q536）：等待加工的原非空绑定目标逐一已有真实完成回执（已登记，或火堆完成回执里有它）。
        /// 只读。剩余显示为 0、目标消失／腐败而没有回执、空绑定列表都不算；后来放进火堆的新加工不在原绑定里，不影响。
        /// </summary>
        public bool CookWaitAllDone(GameState s, ActionState a)
        {
            if (a.Type != ActionType.WaitCook || a.CookTargets.Count == 0 || FacOf(s, a.TargetId) is not FacilityState fac) return false;
            return a.CookTargets.All(p => a.CookDone.Contains(p) || fac.Completed.Any(x => x.StartsWith(p + "@")));
        }

        /// <summary>
        /// Q515，B46 扩到非锁定与 Q524 旧在办等待（Q536）：原绑定目标全部有完成回执——记回执、清等待占用一次、写原完成句；
        /// 不推进时间、不进单段展示、不再产物／经验、不扣燃料或身体费用。已耗时只取本次等待自己实际提交的段（暂停中别的行动不计）。
        /// </summary>
        private void FinishCookWaitNow(GameState s, ActionState a)
        {
            var fac = FacOf(s, a.TargetId);
            foreach (var pid in a.CookTargets.Where(p => !a.CookDone.Contains(p)).ToList())
                if (fac != null && fac.Completed.Any(x => x.StartsWith(pid + "@"))) a.CookDone.Add(pid);
            ClearAction(s, a);
            Emit(s, GameEventKind.ActionCompleted, $"等待加工结束：{LocName(a.TargetId)}火堆{a.CookTargets.Count}项加工全部完成，成品留在原位（{Clock.Duration(a.ElapsedMinutes)}）。");
            UpdateFlags(s);
            EnemyApproachBoundary(s);   // 行动结束边界（同原完成）
        }

        // ───────────────────────── Q518 自动中止 ─────────────────────────

        /// <summary>
        /// 永久失效的锁定主行动自动中止（Q506 第 4、5 条，Q514、Q518）：按原取消规则释放仍实际归本行动的预留与锁一次（撤退离地保留者按开工来源），
        /// 不给未完成产物、经验；结果句「{行动名}已中止。原因：{原因正文}」＋实际返还分句＋原后果＋「已耗时间保留。」。
        /// </summary>
        private void AbortLocked(GameState s, ActionState a, string reason)
        {
            // Q518-A02：已丢失或改属别人的预留不在本次释放之列（不补造、不解别人的标记）
            a.ReservedItemIds.RemoveAll(id => FindItem(s, id) is not ItemInstance i || i.ReservedBy != a.ActionId);
            var kept = a.RetreatKept;
            var src = kept ? KeptSources(s, a) : default;
            var groundName = ZoneName(s, s.Ground);
            var hadMaterials = a.ReservedItemIds.Count > 0;
            var hadStock = a.Type == ActionType.Gather && a.ReservedResourceUnits > 0;
            var staged = _stagedCancelNote = new List<string>();
            try { Release(s, a, s.Ground); }
            finally { _stagedCancelNote = null; }
            if (a.IsSub) s.Sub = null; else s.Main = null;
            var keptWork = a.IsSub ? null : RestoreStashedWork(s);

            var text = $"{a.Label}已中止。原因：{reason}";
            if (!(text.EndsWith("。") || text.EndsWith("！") || text.EndsWith("？"))) text += "。";
            if (hadMaterials)
            {
                if (kept) text += KeptReturnClause(s, a, src, true);
                else text += a.Type == ActionType.Butcher ? $"尸体已返还{groundName}地面。" : $"预留材料已返还{groundName}地面。";
            }
            text += a.Type switch
            {
                ActionType.Craft => "未生成半成品。",
                ActionType.Build => "本次建造进度已清除，没有建成。",
                ActionType.Repair => "未完成本次维护。",
                ActionType.Salvage => "未获得拆解产物。",
                ActionType.Butcher => "保质期未刷新，没有产物。",
                ActionType.Explore => "探索度不变，未获得物资。",
                ActionType.Clear => "未获得物资；已扣除的1次清理机会不返还。",
                ActionType.Gather => "未获得产物。" + (hadStock ? "预留库存已释放。" : ""),
                ActionType.Survey => "未获得经验，不占今日勘整次数。",
                ActionType.WaitCook => "火堆、锅与受热进度原样保留，成品留在原位。",
                _ => "",
            };
            text += ElapsedKept;
            if (staged.Count > 0) text += string.Join("", staged);   // B23：可选地牢记录保留的整段（同原取消）
            if (keptWork != null) text += StillPausedText(keptWork);
            Emit(s, GameEventKind.ActionAborted, text);
            UpdateFlags(s);
            EnemyApproachBoundary(s);   // 行动结束边界（同原合法退出）
        }
    }
}
