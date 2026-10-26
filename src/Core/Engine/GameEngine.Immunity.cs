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

        /// <summary>绷带一次的健康恢复量（未扣除已满部分）。</summary>
        public double BandageHeal(GameState s) => BandageItem?.Use?.Health ?? 0;

        private void ApplyMedicine(GameState s, ItemUseConfig u, List<string> parts)
        {
            if (!u.IsBandage) return;
            var heal = Math.Min(BandageHeal(s), s.Stats.HealthMax - s.Stats.Health);
            if (heal > 0)
            {
                s.Stats.Add(StatId.Health, heal);
                parts.Add("健康" + Fmt.Delta(heal));
            }
            s.BandageReadyAt = s.GameMinute + Config.ItemActions.BandageCooldownMinutes;
        }

        private void TickBody(GameState s, TickStart start, SegmentContext ctx, bool battle) { }

        private bool Zombified(GameState s) => false;

        private void Zombify(GameState s) { }

        private void TickNaturalCure(GameState s, TickStart start) { }
    }
}
