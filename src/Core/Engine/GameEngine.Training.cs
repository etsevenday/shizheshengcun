using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// h03 战术训练（开发计划 v0.9 B15，R46 接 R22；R03 §3“阶位1：C3解锁后在h03花parts2+cloth2，60基础工作分钟，每角色一次；
    /// 阶位2：QMAP04完成后在h03花precision_parts2+steel2，90分钟，每角色一次”）。
    /// 选一名当前同行者（含你）训练到下一阶；材料取你随身或当地地面，开工预留、完成核销；全队按等待度过（开发假设 B15-7）；
    /// 取消不扣材料、不升阶，已过时间不退。训练不是熟练度、不给经验。
    /// </summary>
    public sealed partial class GameEngine
    {
        private TacticalTrainingConfig Training => Config.Scope.Training;

        /// <summary>某人下一阶的训练档；已到最高阶为 null。</summary>
        private TrainingTierConfig? NextTrainingTier(ActorState a) => Training.Tiers.FirstOrDefault(t => t.Tier == a.TacticalTier + 1);

        private string TierCost(TrainingTierConfig t) => string.Join("、", t.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"));

        /// <summary>某人现在不能训练下一阶的原因；可以为 null。</summary>
        private string? TrainingBlocked(GameState s, ActorState a)
        {
            var t = NextTrainingTier(a);
            if (t == null) return $"已完成全部战术训练（阶位{a.TacticalTier}）";
            if (t.MinCapacity > 0 && BattleCapacity(s) < t.MinCapacity)
                return $"阶位{t.Tier}需要：队伍容量达到C{t.MinCapacity}（{Config.Scope.Capacity.Unlocks.FirstOrDefault(u => u.Capacity == t.MinCapacity)?.Text ?? ""}）";
            var miss = t.RequiresAll.FirstOrDefault(f => !s.Progress.Flags.Contains(f));
            if (miss != null) return $"阶位{t.Tier}需要：{RequirementText(miss)}";
            var lack = t.Inputs.Where(kv => AvailableCount(s, kv.Key, true) < kv.Value).Select(kv => $"{ItemName(kv.Key)}{AvailableCount(s, kv.Key, true)}/{kv.Value}").ToList();
            if (lack.Count > 0) return $"阶位{t.Tier}材料不足（随身或当地地面）：{string.Join("、", lack)}";
            return null;
        }

        private void TrainingPreview(GameState s, CityServiceConfig svc, CityServiceView v)
        {
            var tiers = Training.Tiers.OrderBy(t => t.Tier).ToList();
            v.NeedsTarget = true;
            v.TimeText = string.Join("或", tiers.Select(t => $"{t.Minutes}分钟"));
            v.CostText = string.Join("；", tiers.Select(t => $"阶位{t.Tier}：{TierCost(t)}"));
            v.StartLabel = "开始训练";
            v.EffectText = "选一名当前同行者（含你）训练到下一阶：阶位0/1/2 每次普通行动分别有"
                + string.Join("/", Config.Scope.ApByTier) + "个行动点；"
                + string.Join("；", tiers.Select(t => $"阶位{t.Tier}{(t.MinCapacity > 0 ? $"在队伍容量C{t.MinCapacity}后" : "")}{(t.RequiresAll.Count > 0 ? "在" + string.Join("、", t.RequiresAll.Select(RequirementText)) + "后" : "")}花{TierCost(t)}、{t.Minutes}分钟"))
                + "，每人每阶一次。材料取你随身或当地地面，完成时扣；全队一起等待，中途停下不扣材料、不升阶。训练不是熟练度，不给经验";
            v.Targets = PresentTargets(s, a => TrainingBlocked(s, a), a =>
            {
                var t = NextTrainingTier(a);
                return t == null ? $"阶位{a.TacticalTier}（{NormalActivationAp(a)}AP）" : $"阶位{a.TacticalTier}→{t.Tier}（{NormalActivationAp(a)}→{Config.Scope.ApByTier[t.Tier]}AP），{TierCost(t)}，{t.Minutes}分钟";
            });
        }

        private ActionResult StartTraining(GameState s, CityServiceConfig svc, CityTargetView t, CityNpcConfig? npc, string? requestId)
        {
            var a = s.Actors[t.ActorId];
            var tier = NextTrainingTier(a)!;
            var req = ActionRequest.Wait(tier.Minutes);
            req.TargetId = CityServicePrefix + svc.Id;
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            var act = s.Main!;
            act.Label = $"{svc.Title}（阶位{tier.Tier}，{Clock.Duration(tier.Minutes)}）：{t.Name}";
            var txn = new CityServiceTxn
            {
                TxnId = s.City.NextTxn++, ActionId = act.ActionId, ServiceId = svc.Id, NpcId = Config.Scope.Training.NpcId, TargetActorId = t.ActorId,
                ChoiceId = tier.Tier.ToString(), LocationId = s.CurrentLocation, StartDay = Clock.Day(s.GameMinute),
            };
            foreach (var kv in tier.Inputs)
            {
                var ids = Reserve(s, kv.Key, kv.Value, act.ActionId);
                act.ReservedItemIds.AddRange(ids);
                txn.ReservedItemIds.AddRange(ids);
            }
            s.City.Pending = txn;
            return res;
        }

        /// <summary>训练完成：重验目标仍差这一阶、预留材料还在，核销材料并升阶。返回失败原因（null 为成功）。</summary>
        private string? CompleteTraining(GameState s, CityServiceTxn p, ActorState target, List<ItemInstance> reserved, List<string> made)
        {
            var tier = Training.Tiers.FirstOrDefault(t => t.Tier.ToString() == p.ChoiceId);
            if (tier == null || target.TacticalTier != tier.Tier - 1) return "训练阶位已变";
            if (tier.Inputs.Any(kv => reserved.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) < kv.Value)) return "预留的材料已不在";
            foreach (var kv in tier.Inputs)
            {
                var left = kv.Value;
                foreach (var i in reserved.Where(i => i.ItemId == kv.Key).ToList())
                    while (left > 0 && i.Count > 0) { ConsumeOne(s, i); left--; }
            }
            foreach (var i in reserved) i.ReservedBy = null;
            target.TacticalTier = tier.Tier;
            made.Add($"{ActorDisplayName(s, target.Id)}战术训练到阶位{tier.Tier}，每次普通行动{NormalActivationAp(target)}个行动点；消耗{TierCost(tier)}");
            return null;
        }
    }
}
