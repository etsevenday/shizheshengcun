using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>免疫力、病毒、药效与出血（v0.6 §08–§11）。M40 补全，M37 先留世界 tick 的挂点与绷带回血。</summary>
    public sealed partial class GameEngine
    {
        private ItemConfig? BandageItem => Config.Items.FirstOrDefault(i => i.Use != null && i.Use.IsBandage);

        /// <summary>绷带一次的健康恢复量 1×(1+0.02×医疗L)（未扣除已满部分）。</summary>
        public double BandageHeal(GameState s) => MedicalHeal(BandageItem?.Use?.Health ?? 0, SkillLevel(s, Medicine));

        /// <summary>
        /// 药物能否使用（v0.6 §11）：绷带回血、病毒减量与止血三种效果都没有实际收益时禁用并写明原因。
        /// </summary>
        public string? MedicineBlocked(GameState s, ItemUseConfig u)
        {
            if (u.IsBandage)
            {
                var healReady = s.BandageReadyAt <= s.GameMinute;
                var canHeal = healReady && s.Stats.Health < s.Stats.HealthMax - 1e-9;
                if (canHeal || s.Body.BleedTriggers > 0 || BandageVirusUseful(s)) return null;
                if (!healReady) return $"绷带冷却中，还需{s.BandageReadyAt - s.GameMinute}分钟";
                return "健康已满且没有出血，包扎没有效果";
            }
            return null;
        }

        private bool BandageVirusUseful(GameState s) => false;

        private void ApplyMedicine(GameState s, ItemUseConfig u, List<string> parts)
        {
            if (!u.IsBandage) return;
            var heal = s.BandageReadyAt <= s.GameMinute ? Math.Min(BandageHeal(s), s.Stats.HealthMax - s.Stats.Health) : 0;
            var effective = false;
            if (heal > 0)
            {
                s.Stats.Add(StatId.Health, heal);
                parts.Add("健康" + Fmt.Delta(heal));
                // 回血冷却只在回血实际生效时启动
                s.BandageReadyAt = s.GameMinute + Config.ItemActions.BandageCooldownMinutes;
                effective = true;
            }
            else parts.Add("回血冷却中或健康已满，本次不回血");
            // 有效治疗才给医疗经验，同次使用只发一次（v0.6 §03、§11）
            if (effective)
            {
                GainXp(s, Medicine, Config.Xp.Medical);
                parts.Add($"医疗经验+{Config.Xp.Medical}");
            }
        }

        private void TickBody(GameState s, TickStart start, SegmentContext ctx, bool battle) { }

        private bool Zombified(GameState s) => false;

        private void Zombify(GameState s) { }

        private void TickNaturalCure(GameState s, TickStart start) { }
    }
}
