using System;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 烹饪经验（策划答复 Q60 选 B、S11/A06 本轮章，开发计划 v0.9 B7b）：
    /// 烤根/煮根/烤肉/煮肉成功产出时，给首次投料/启用进程的存活角色 clamp(ceil(原受热分钟/10),1,12)（30 分钟 = 3），每份一次；
    /// 复煮 0；净化每实际完成 500ml 给 1，每角色每世界日最多 4（B22a Q166：与净水片/滤芯固定等待净水共享这 4）；食品（含 B16 主动配方）每角色每日合计最多 30。
    /// B22f（Q207 选B，280f8be，取代 B22a 开发假设 B22a-1“净化不计 30”）：所有净化（锅烧水、净水片、滤芯）也计入烹饪每日 30 总上限，
    /// 净化子上限仍是每日 4；食品/主动烹饪与净化共用 30，按完成日剩余发。总额 = 当日食品账（xp|cooking）+ 净化账（waterXp|cooking），
    /// 不另记新账，旧档当天已发的两账直接相加即总额（已超 30 不倒扣，当天新增 0）。
    /// 只在成品提交时发；投料、点火、等待、取消、取出、转锅都不发；旧进程没有发起者（B7b 之前建立）不补。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>某角色某世界日烹饪经验已发总额（食品/主动烹饪账 + 净化账，Q207）。</summary>
        private int CookTotalUsed(ActorState actor, int day) =>
            DailyXpUsed(actor, day, Cooking) + DailyCount(actor, day, DailyWaterXp, Cooking);

        /// <summary>某角色某世界日烹饪总上限 30（Q207 含净化）还剩多少，下限 0。</summary>
        private int CookTotalRoom(ActorState actor, int day) =>
            Math.Max(0, Config.Progression.Cooking.FoodDailyCap - CookTotalUsed(actor, day));

        /// <summary>食品/主动烹饪这次实际可给：min(应得, 30 − 今日烹饪全部已得)。</summary>
        private int CookFoodRoom(ActorState actor, int day, int amount) => Math.Max(0, Math.Min(amount, CookTotalRoom(actor, day)));

        /// <summary>“与净化合计每日最多30”（食品/主动烹饪说明里用，Q207）。</summary>
        private string CookTotalCapText => $"与净化合计每日最多{Config.Progression.Cooking.FoodDailyCap}";

        /// <summary>“烹饪每日总上限30”（净化说明里用）。</summary>
        private string CookTotalLimitText => $"{SkillName(Cooking)}每日总上限{Config.Progression.Cooking.FoodDailyCap}";

        /// <summary>净化这次实际可给的余量：min(4 − 今日净化已得, 30 − 今日烹饪全部已得)（Q207）。</summary>
        private int CookWaterRoom(ActorState actor, int day) => Math.Min(WaterRoom(actor, day), CookTotalRoom(actor, day));

        /// <summary>净化给不了时的原因：净化子限 4 已满，或烹饪总额 30 已满。</summary>
        private string CookWaterFullText(ActorState actor, int day, string dayText) =>
            WaterRoom(actor, day) <= 0 ? $"{dayText}净化经验已满{Config.Progression.Cooking.WaterDailyCap}"
                : $"{dayText}{SkillName(Cooking)}经验已满{Config.Progression.Cooking.FoodDailyCap}（含净化）";
        /// <summary>该进程完成时给发起者的烹饪经验（单份）；不在四种食品进程里为 0。</summary>
        public int CookFoodXpFor(string processId)
        {
            var c = Config.Progression.Cooking;
            if (!c.FoodProcesses.Contains(processId) || c.FoodHeatStepMinutes <= 0) return 0;
            var heat = Process(processId).HeatMinutes;
            return Math.Max(c.FoodMin, Math.Min(c.FoodMax, (int)Math.Ceiling(heat / (double)c.FoodHeatStepMinutes)));
        }

        /// <summary>发起者：存活且是现有角色；旧进程无发起者为 null。</summary>
        private ActorState? CookCredit(GameState s, FacilityProcess p)
        {
            if (p.InitiatorActorId == null || !s.Actors.TryGetValue(p.InitiatorActorId, out var actor)) return null;
            if (actor.IsPlayer ? s.Progress.Dead : actor.Stats.Health <= 0) return null;
            return actor;
        }

        /// <summary>食品进程完成：按份给经验（受每日 30）。返回日志附注；不给经验时为 null 或说明。</summary>
        private string? CookFoodXp(GameState s, FacilityProcess p, Survival.Core.Config.ProcessConfig cfg, int portions)
        {
            var per = CookFoodXpFor(cfg.Id);
            if (per <= 0) return null;
            var actor = CookCredit(s, p);
            if (actor == null) return null;
            var c = Config.Progression.Cooking;
            var day = Clock.Day(s.GameMinute);
            var room = CookFoodRoom(actor, day, per * Math.Max(1, portions));   // Q207：与净化共用 30
            if (SkillLevel(actor, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，不再获得经验";
            if (room <= 0) return $"今日{SkillName(Cooking)}经验已满{c.FoodDailyCap}（含净化），不再给经验";
            BumpDaily(s, actor, DailyXp, Cooking, day, room);
            GainXp(s, actor, Cooking, room);
            return $"{ActorPrefix(actor)}{SkillName(Cooking)}经验+{room}";
        }

        /// <summary>
        /// 净化完成：本日锅里累计净化毫升每满 500 给 1。每日 4 与净水片/滤芯固定等待净水共享（Q166 选 B）：
        /// 锅已得 = 共享已得 − 固定等待已得，这次最多给到共享余量；B22f（Q207）再受烹饪每日总额 30 的余量限制。
        /// </summary>
        private string? CookWaterXp(GameState s, FacilityProcess p, int ml)
        {
            var c = Config.Progression.Cooking;
            if (p.RecipeId != c.WaterProcess || c.WaterMlPerXp <= 0 || ml <= 0) return null;
            var actor = CookCredit(s, p);
            if (actor == null) return null;
            var day = Clock.Day(s.GameMinute);
            var totalMl = BumpDaily(s, actor, DailyWaterMl, Cooking, day, ml);
            var given = DailyCount(actor, day, DailyWaterXp, Cooking);
            var potGiven = given - DailyCount(actor, day, DailyWaterFixedXp, Cooking);
            var owed = Math.Min(Math.Min(c.WaterDailyCap, totalMl / c.WaterMlPerXp) - potGiven, c.WaterDailyCap - given);
            var due = Math.Min(owed, CookTotalRoom(actor, day));   // Q207：也计烹饪每日 30
            if (SkillLevel(actor, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，不再获得经验";
            if (due <= 0) return given >= c.WaterDailyCap ? $"今日净化经验已满{c.WaterDailyCap}"
                : owed > 0 ? $"今日{SkillName(Cooking)}经验已满{c.FoodDailyCap}（含净化），不再给经验" : null;
            BumpDaily(s, actor, DailyWaterXp, Cooking, day, due);
            GainXp(s, actor, Cooking, due);
            return $"{ActorPrefix(actor)}{SkillName(Cooking)}经验+{due}";
        }

        /// <summary>固定等待净水（Q166 选 B）这一批实际产出的净水毫升（只算有每份体积的产物，按批数）。</summary>
        private int FixedWaitWaterMl(RecipeConfig rc, int batch) =>
            rc.Outputs.Sum(kv => (Item(kv.Key).UnitVolumeMl ?? 0) * kv.Value) * Math.Max(1, batch);

        /// <summary>固定等待净水本批应给的烹饪经验（每整 500ml 给 1，不向上取整、不跨批积余），未计日额。</summary>
        private int FixedWaitWaterUnits(RecipeConfig rc, int batch)
        {
            var c = Config.Progression.Cooking;
            return c.WaterMlPerXp <= 0 ? 0 : FixedWaitWaterMl(rc, batch) / c.WaterMlPerXp;
        }

        /// <summary>
        /// 净水片/滤芯固定等待净水完成（Q166 选 B，survival-adjudications.json water）：成功实际净化输出每 500ml 给执行者烹饪 1，
        /// 与锅烧水共享每角色每世界日 4（按完成日剩余额度，不另给 4）；B22f（Q207 选B）起也计烹饪每日 30（取代开发假设 B22a-1）。
        /// 开工快照不是烹饪（新规则上线前已开工的批次）按原快照不给。取消/腐败中止不走这里，领取/读档不重给。
        /// </summary>
        private string FixedWaitWaterXp(GameState s, ActionState a, RecipeConfig rc, int batch)
        {
            if (a.XpSkill != Cooking) return "固定等待不给经验（按开工时的规则）";
            var c = Config.Progression.Cooking;
            var units = FixedWaitWaterUnits(rc, batch);
            if (units <= 0) return "净水不足500毫升，不给经验";
            var actor = s.Player;
            if (SkillLevel(actor, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，不再获得经验";
            var day = Clock.Day(s.GameMinute);
            var waterLeft = WaterRoom(actor, day);
            var grant = Math.Min(units, CookWaterRoom(actor, day));   // Q207：净化子限 4 与烹饪总额 30 取小
            if (grant <= 0) return waterLeft <= 0 ? $"今日净化经验已满{c.WaterDailyCap}（与锅烧水合计），不再给经验"
                : $"今日{SkillName(Cooking)}经验已满{c.FoodDailyCap}（含净化），不再给经验";
            BumpDaily(s, actor, DailyWaterXp, Cooking, day, grant);
            BumpDaily(s, actor, DailyWaterFixedXp, Cooking, day, grant);
            GainXp(s, actor, Cooking, grant);
            return grant >= units ? $"{SkillName(Cooking)}经验+{grant}"
                : waterLeft > grant ? $"{SkillName(Cooking)}经验+{grant}（{CookTotalLimitText}，只剩{grant}）"
                : $"{SkillName(Cooking)}经验+{grant}（净化日额与锅烧水合计{c.WaterDailyCap}，只剩{grant}）";
        }

        /// <summary>
        /// 预估用：固定等待净水完成后的烹饪经验说明。按预计完成时刻（现在 + 固定分钟）所在世界日的剩余额度写（B22a 试玩修复 M412：
        /// 23:55 开工、00:15 完成按次日额度），完成日不是今天时写“完成日第N日”。
        /// </summary>
        public string FixedWaitWaterXpPreview(GameState s, RecipeConfig rc, int batch)
        {
            var c = Config.Progression.Cooking;
            var units = FixedWaitWaterUnits(rc, batch);
            if (SkillLevel(s.Player, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，不再获得经验";
            var day = Clock.Day(s.GameMinute + rc.BaseMinutes * Math.Max(1, batch));
            var room = CookWaterRoom(s.Player, day);   // Q207：净化子限 4 与烹饪总额 30 取小
            var grant = Math.Min(units, room);
            var cap = $"与锅烧水合计每日最多{c.WaterDailyCap}，也计{CookTotalLimitText}";
            var dayText = DayWord(s, day);
            return grant <= 0 ? $"{CookWaterFullText(s.Player, day, dayText)}（{cap}），完成不再给经验"
                : grant < units ? $"{SkillName(Cooking)}经验+{grant}（每500毫升+1，{cap}，{dayText}只剩{grant}）"
                : $"{SkillName(Cooking)}经验+{grant}（每500毫升+1，{cap}{(day == Clock.Day(s.GameMinute) ? "" : $"，按{dayText}额度")}）";
        }

        /// <summary>某角色某世界日净化日额（锅与固定等待共享）还剩多少。</summary>
        private int WaterRoom(ActorState actor, int day) =>
            Math.Max(0, Config.Progression.Cooking.WaterDailyCap - DailyCount(actor, day, DailyWaterXp, Cooking));

        /// <summary>“今日”或“完成日第N日”。</summary>
        private static string DayWord(GameState s, int day) => day == Clock.Day(s.GameMinute) ? "今日" : $"完成日第{day}日";

        /// <summary>
        /// 进程预计完成的世界分钟（B22a 试玩修复 M412，经验说明按完成日写）：锅/食材在燃烧的火堆上按剩余受热分钟连续受热估算；
        /// 火没烧、锅不在火堆上算不了，按现在。
        /// </summary>
        private int ProcessFinishEstimate(GameState s, FacilityProcess p)
        {
            var holder = FindItem(s, p.PotInstanceId ?? p.TargetInstanceId);
            var loc = holder == null ? null : IsPot(holder) ? PotFireLocation(holder)
                : ItemInstance.TryFacilityId(holder.Owner, out var fid) ? FacilityLocation(fid) : null;
            if (loc == null || !FireHot(s, loc)) return s.GameMinute;
            return s.GameMinute + Math.Max(0, p.RequiredHeatMinutes - p.HeatDoneMinutes);
        }

        /// <summary>
        /// 某实例（直烤食材、锅内食材，或锅/锅内液体＝净化批次）正在进行的加工完成后的烹饪经验说明；没有进程为 null。
        /// 锅食谱（硬饼/炖食）每批一次：锅内各份原料不再逐行写，只在锅上写一次（PotRecipeXpLine，B22a 试玩修复 M412）。
        /// </summary>
        public string? CookXpLine(GameState s, ItemInstance inst)
        {
            var p = ProcessOf(s, inst);
            if (p?.InputInstanceIds != null && p.PotInstanceId != inst.InstanceId) return null;
            if (p == null && inst.InContainer && ContainerOf(s, inst) is ItemInstance pot) p = ProcessOf(s, pot);
            return p == null ? null : CookXpNote(s, p);
        }

        /// <summary>锅上一处：这口锅正在做的锅食谱这一批完成后的烹饪经验（每批一次，不按份数）；没有锅食谱为 null。</summary>
        public string? PotRecipeXpLine(GameState s, ItemInstance pot)
        {
            var p = AllProcesses(s).FirstOrDefault(x => x.PotInstanceId == pot.InstanceId && x.InputInstanceIds != null);
            if (p == null) return null;
            var note = CookXpNote(s, p);
            if (note == null) return null;
            var pc = Process(p.RecipeId);
            var name = pc.RecipeId != null && _recipes.TryGetValue(pc.RecipeId, out var rc) ? rc.Name : ItemName(pc.OutputId);
            return $"{name}这一批（出{ItemName(pc.OutputId)}×{pc.OutputCount}）：{note}，每批一次，不按份数";
        }

        /// <summary>火堆/锅卡与预估用：这一项完成后的烹饪经验说明（旧进程写不补）；日额已满/只剩 N 按预计完成日标出（M412）。</summary>
        public string? CookXpNote(GameState s, FacilityProcess p)
        {
            var c = Config.Progression.Cooking;
            if (Process(p.RecipeId).IsSlotBatch) return "煅烧不给经验";
            if (p.RecipeId == c.WaterProcess)
            {
                if (p.InitiatorActorId == null) return "旧进程，不给烹饪经验";
                var basic = $"完成后每500毫升{SkillName(Cooking)}经验+1（与净水片/滤芯净水合计每日最多{c.WaterDailyCap}，也计{CookTotalLimitText}）";
                var actor = CookCredit(s, p);
                if (actor == null || SkillLevel(actor, Cooking) >= SkillCap(Cooking)) return basic;
                var day = Clock.Day(ProcessFinishEstimate(s, p));
                var room = CookWaterRoom(actor, day);   // Q207
                return room <= 0 ? $"{CookWaterFullText(actor, day, DayWord(s, day))}（{(WaterRoom(actor, day) <= 0 ? "与净水片/滤芯净水合计" : "食品与净化合计")}），完成不再给经验"
                    : room < c.WaterDailyCap ? basic + $"，{DayWord(s, day)}只剩{room}" : basic;
            }
            var per = CookFoodXpFor(p.RecipeId);
            if (per <= 0) return c.FoodProcesses.Count > 0 && !c.FoodProcesses.Contains(p.RecipeId) ? "复煮不给烹饪经验" : null;
            if (p.InitiatorActorId == null) return "旧进程，不给烹饪经验";
            var text = $"完成后{SkillName(Cooking)}经验+{per}（{CookTotalCapText}）";
            var who = CookCredit(s, p);
            if (who == null || SkillLevel(who, Cooking) >= SkillCap(Cooking)) return text;
            var fday = Clock.Day(ProcessFinishEstimate(s, p));
            var froom = CookTotalRoom(who, fday);   // Q207：与净化共用 30
            return froom <= 0 ? $"{DayWord(s, fday)}{SkillName(Cooking)}经验已满{c.FoodDailyCap}（含净化），完成不再给经验"
                : froom < per ? $"完成后{SkillName(Cooking)}经验+{froom}（{CookTotalCapText}，{DayWord(s, fday)}只剩{froom}）" : text;
        }

        /// <summary>预估用：主动烹饪配方（如远征口粮棒）完成后的烹饪经验说明（Q207：与食品、净化共用每日 30；按现在所在世界日）。</summary>
        public string CookActiveXpPreview(GameState s, int xp)
        {
            if (SkillMaxed(s, Cooking)) return XpText(s, Cooking, xp);
            var room = CookFoodRoom(s.Player, Clock.Day(s.GameMinute), xp);
            var c = Config.Progression.Cooking;
            return room <= 0 ? $"今日{SkillName(Cooking)}经验已满{c.FoodDailyCap}（含净化），完成不再给经验"
                : room < xp ? $"{SkillName(Cooking)}经验+{room}（{CookTotalCapText}，今日只剩{room}）"
                : $"{SkillName(Cooking)}经验+{xp}（{CookTotalCapText}）";
        }
    }
}
