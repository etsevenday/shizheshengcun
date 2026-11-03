using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 陶窑与固定净水台（A07 X1.4、S11 X4、expansion-supply.json facilities/operationalRules.water；开发计划 v0.9 B17 R56）：
    /// 只建在临时营地外部或已完工安全屋屋内（Q107 每区每类一座）。被动批次：在当前区的工坊投料（0 分钟、不占行动），原料从随身/当前地面取、
    /// 移到该区地面并锁定；按世界时间推进（陶窑 60 分钟、净水台 30 分钟，不吃熟练/装备/夜间倍率），离开也继续、不能远程投取；
    /// 完成一次核销原料与燃料（陶窑每批另耗木炭1），产物落该区地面、不自动入包；取消释放原料、时间不退、没有产物。每座每次一批。
    /// B22d（Q174 选 B，passiveCraftXp）：每成功整批给首次投料且完成时仍存活的角色制造经验（陶窑 6、净水台 3，投料时快照），
    /// 两类共享每角色每世界日 12（按完成日，剩余不足给剩余，满级 0），与主动制造/建造经验独立，不另给烹饪；取消/原料不全/读档/领取都不给。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>被动批次的配方：工坊是陶窑或固定净水台、模式为 passive/passive_heat。</summary>
        public bool IsStationBatchRecipe(RecipeConfig rc) => rc.IsPassive && !rc.IsPot && rc.Station is "kiln" or "water_station";

        /// <summary>当前区这座工坊正在进行的批次；没有为 null。</summary>
        public StationBatch? StationBatchHere(GameState s, string stationId) =>
            FacOf(s, s.Ground)?.StationBatches.FirstOrDefault(b => b.StationId == stationId);

        /// <summary>在当前区开始一批被动加工现在不能做的原因；能做为 null。</summary>
        public string? StationBatchBlocked(GameState s, RecipeConfig rc)
        {
            if (!IsStationBatchRecipe(rc)) return "这不是固定工坊的被动加工";
            if (s.Progress.Dead) return "本局已结束";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中不能投料";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (s.Main != null || s.Sub != null) return "行动进行中，先完成或取消再投料";
            if (RecipeUnknownReason(s, rc) is string unknown) return unknown;
            foreach (var kv in rc.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) return $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
            }
            if (StationBlocked(s, rc) is string station) return station;
            if (StationBatchHere(s, rc.Station) is StationBatch running)
                return $"{StationName(rc.Station)}正在加工一批（{running.DoneMinutes}/{running.RequiredMinutes}分钟），每次只能一批";
            foreach (var kv in rc.AllInputs)
            {
                var have = UsableBatches(s, kv.Key, true).Where(i => !IsLocked(s, i)).Sum(i => i.Count);
                if (have < kv.Value) return $"缺少{ItemName(kv.Key)}（{have}/{kv.Value}，只取随身与当前地面）";
            }
            return null;
        }

        /// <summary>投料说明（预览）：原料、燃料、产物、分钟与落点。</summary>
        public string StationBatchSummary(RecipeConfig rc)
        {
            var ins = string.Join("、", rc.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"));
            var fuel = rc.Fuel.Count > 0 ? "＋燃料" + string.Join("、", rc.Fuel.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}")) : "";
            var outs = string.Join("、", rc.Outputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"));
            return $"{ins}{fuel} → {outs}，被动{rc.BaseMinutes}世界分钟（不占行动，离开也继续），完成后放在本区地面；取消退回原料，时间不退";
        }

        private const string DailyStationXp = "stationXp";

        /// <summary>这座工坊每成功整批的基础经验（Q174：陶窑 6、净水台 3）；没配置为 0。</summary>
        public int StationBatchXpFor(string stationId) =>
            Config.Progression.StationXp.PerBatch.TryGetValue(stationId, out var n) ? n : 0;

        /// <summary>某角色某世界日被动批次日额（陶窑+净水台共享 12）还剩多少。</summary>
        private int StationXpRoom(ActorState actor, int day)
        {
            var c = Config.Progression.StationXp;
            return Math.Max(0, c.DailyCap - DailyCount(actor, day, DailyStationXp, c.Skill));
        }

        /// <summary>
        /// 经验说明（预估/进行中共用）：基础经验与完成日余额。xp 为这一批的快照（0 = 旧批次按原规则不给）；finishMinute 为预计完成时刻。
        /// </summary>
        private string StationXpText(GameState s, int xp, int finishMinute, bool started)
        {
            var c = Config.Progression.StationXp;
            if (xp <= 0) return started ? "这一批开始时被动加工不给经验（按原规则完成）" : "被动加工不给经验";
            var skill = c.Skill;
            var basic = $"完成后{SkillName(skill)}经验+{xp}（陶窑与净水台被动批次合计每日最多{c.DailyCap}）";
            if (SkillLevel(s.Player, skill) >= SkillCap(skill)) return $"{SkillName(skill)}已满{SkillCap(skill)}级，完成不再获得经验";
            var day = Clock.Day(finishMinute);
            var room = StationXpRoom(s.Player, day);
            return room <= 0 ? $"{DayWord(s, day)}被动批次{SkillName(skill)}经验已满{c.DailyCap}，完成不再给经验"
                : room < xp ? $"完成后{SkillName(skill)}经验+{room}（陶窑与净水台被动批次合计每日最多{c.DailyCap}，{DayWord(s, day)}只剩{room}）"
                : basic + (day == Clock.Day(s.GameMinute) ? "" : $"，按{DayWord(s, day)}额度");
        }

        /// <summary>投料前预估：这一批完成后的制造经验（按预计完成日的余额）。</summary>
        public string StationBatchXpPreview(GameState s, RecipeConfig rc) =>
            StationXpText(s, StationBatchXpFor(rc.Station), s.GameMinute + rc.BaseMinutes, false);

        /// <summary>进行中的批次：完成后的制造经验（按快照与预计完成日余额）。</summary>
        public string StationBatchXpNote(GameState s, StationBatch b) =>
            StationXpText(s, b.XpSnapshot, s.GameMinute + Math.Max(0, b.RequiredMinutes - b.DoneMinutes), true);

        /// <summary>
        /// 批次成功完成：给投料者（完成时仍存活）快照经验，受完成日共享日额；返回日志附注。只在这里结算一次（产物同一处提交），读档/领取不重给。
        /// </summary>
        private string StationBatchXp(GameState s, StationBatch b)
        {
            var c = Config.Progression.StationXp;
            if (b.XpSnapshot <= 0) return "这一批开始时被动加工不给经验";
            if (b.InitiatorActorId == null || !s.Actors.TryGetValue(b.InitiatorActorId, out var actor)
                || (actor.IsPlayer ? s.Progress.Dead : actor.Stats.Health <= 0)) return "投料者已不在，不给经验";
            var skill = c.Skill;
            if (SkillLevel(actor, skill) >= SkillCap(skill)) return $"{SkillName(skill)}已满{SkillCap(skill)}级，不再获得经验";
            var day = Clock.Day(s.GameMinute);
            var grant = Math.Min(b.XpSnapshot, StationXpRoom(actor, day));
            if (grant <= 0) return $"今日被动批次{SkillName(skill)}经验已满{c.DailyCap}（陶窑与净水台合计），不再给经验";
            BumpDaily(s, actor, DailyStationXp, skill, day, grant);
            GainXp(s, actor, skill, grant);
            return grant < b.XpSnapshot
                ? $"{ActorPrefix(actor)}{SkillName(skill)}经验+{grant}（陶窑与净水台合计每日{c.DailyCap}，只剩{grant}）"
                : $"{ActorPrefix(actor)}{SkillName(skill)}经验+{grant}";
        }

        /// <summary>在当前区的陶窑/净水台投一批（0 分钟）。</summary>
        public ActionResult StartStationBatch(GameState s, string recipeId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (!_recipes.TryGetValue(recipeId, out var rc)) return ActionResult.Fail("配方不存在");
            if (StationBatchBlocked(s, rc) is string why) return ActionResult.Fail(why);
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            var id = s.NextActionId++;
            var locked = new List<int>();
            foreach (var kv in rc.AllInputs)
            {
                var need = kv.Value;
                foreach (var b in UsableBatches(s, kv.Key, true).Where(i => !IsLocked(s, i)).ToList())
                {
                    if (need <= 0) break;
                    var take = System.Math.Min(need, b.Count);
                    ItemInstance moved;
                    if (take < b.Count)
                    {
                        b.Count -= take;
                        moved = b.Clone();
                        moved.InstanceId = s.NextInstanceId++;
                        moved.Count = take;
                        s.Items.Add(moved);
                    }
                    else moved = b;
                    moved.Owner = place;
                    moved.PlacedOrder = null;
                    moved.ReservedBy = id;
                    locked.Add(moved.InstanceId);
                    need -= take;
                }
            }
            Normalize(s, ItemInstance.Carried);
            fac.StationBatches.Add(new StationBatch
            {
                BatchId = id, StationId = rc.Station, RecipeId = rc.Id, InputInstanceIds = locked,
                RequiredMinutes = rc.BaseMinutes, StartMinute = s.GameMinute, InitiatorActorId = GameState.PlayerId,
                XpSnapshot = StationBatchXpFor(rc.Station),
            });
            RememberRequest(s, requestId);
            Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(rc.Station)}投料：{StationBatchSummary(rc)}；{StationBatchXpPreview(s, rc)}。");
            UpdateFlags(s);
            return ActionResult.Success(id);
        }

        /// <summary>取消当前区这座工坊的批次：原料解锁留在本区地面，已过时间不退，没有产物。只能在当地操作。</summary>
        public ActionResult CancelStationBatch(GameState s, string stationId)
        {
            if (s.Progress.Dead) return ActionResult.Fail("本局已结束");
            if (s.Battle != null || s.Encounter != null) return ActionResult.Fail("战斗或敌踪待处理时不能操作");
            var place = s.Ground;
            var fac = FacOf(s, place);
            var b = fac?.StationBatches.FirstOrDefault(x => x.StationId == stationId);
            if (fac == null || b == null) return ActionResult.Fail($"{StationName(stationId)}没有进行中的批次（只能操作当前区的工坊）");
            fac.StationBatches.Remove(b);
            foreach (var it in s.Items.Where(i => i.ReservedBy == b.BatchId)) it.ReservedBy = null;
            Normalize(s, place);
            Emit(s, GameEventKind.ActionCancelled, $"已取消{ZoneName(s, place)}{StationName(stationId)}的这一批（已进行{b.DoneMinutes}分钟，不退时间）：原料退回{ZoneName(s, place)}地面，没有产物。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>每个世界 5 分钟段：全部区的批次各 +5 分钟，达标即一次核销原料与燃料、产物落该区地面（读档按存档进度续算，不重复出）。</summary>
        private void AdvanceStationBatches(GameState s)
        {
            foreach (var (place, fac) in AllFacilityStates(s).ToList())
            {
                foreach (var b in fac.StationBatches.ToList())
                {
                    b.DoneMinutes += Tick;
                    if (b.DoneMinutes < b.RequiredMinutes) continue;
                    fac.StationBatches.Remove(b);
                    var rc = _recipes[b.RecipeId];
                    var inputs = s.Items.Where(i => i.ReservedBy == b.BatchId).ToList();
                    var ok = rc.AllInputs.All(kv => inputs.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) >= kv.Value);
                    if (!ok)
                    {
                        foreach (var it in inputs) it.ReservedBy = null;
                        Normalize(s, place);
                        Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(b.StationId)}：原料不全，这一批没有产物，剩下的原料已解锁。");
                        continue;
                    }
                    foreach (var it in inputs) s.Items.Remove(it);
                    var made = new List<string>();
                    foreach (var kv in rc.Outputs)
                    {
                        CreateItems(s, kv.Key, kv.Value, place);
                        made.Add($"{ItemName(kv.Key)}×{kv.Value}");
                    }
                    Normalize(s, place);
                    var xp = StationBatchXp(s, b);
                    Emit(s, GameEventKind.FacilityDone, $"{ZoneName(s, place)}{StationName(b.StationId)}：{string.Join("、", made)}已完成，放在{ZoneName(s, place)}地面；{xp}。");
                }
            }
        }
    }
}
