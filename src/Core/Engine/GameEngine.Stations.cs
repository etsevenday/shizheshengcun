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
            });
            RememberRequest(s, requestId);
            Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(rc.Station)}投料：{StationBatchSummary(rc)}。");
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
                    Emit(s, GameEventKind.FacilityDone, $"{ZoneName(s, place)}{StationName(b.StationId)}：{string.Join("、", made)}已完成，放在{ZoneName(s, place)}地面（被动加工不给经验）。");
                }
            }
        }
    }
}
