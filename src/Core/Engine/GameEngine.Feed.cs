using System.Collections.Generic;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 食饮结算（开发计划 v0.9 B28 R12，合同 feeding.effect）：主角自用与给同行者吃喝共用同一函数，效果、上限、腹泻都落到传入的角色。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>
        /// 食品/净水/脏水的即时效果落到 actor：配给袋看 actor 本人穿戴，夹紧 0—上限。
        /// actualDelta=false 时文字写配置量（主角自用原文字，如“饱食+35”）；true 时写裁上限后的实际变化（Q257 的{变化}）。
        /// </summary>
        private void ApplyUseEffect(GameState s, ActorState actor, ItemUseConfig u, List<string> parts, bool actualDelta)
        {
            var sat = UseSatiety(s, u, actor.Id);   // 配给袋：非战斗食用正饱食×1.05（B4）
            if (sat != 0) AddUseStat(actor, StatId.Satiety, "饱食", sat, parts, actualDelta);
            if (u.Hydration != 0) AddUseStat(actor, StatId.Hydration, "水分", u.Hydration, parts, actualDelta);
            if (u.Morale != 0) AddUseStat(actor, StatId.Morale, "精神", u.Morale, parts, actualDelta);
            if (u.Health != 0 && !u.IsBandage) AddUseStat(actor, StatId.Health, "健康", u.Health, parts, actualDelta);
            if (u.CausesDiarrhea)
            {
                actor.DiarrheaUntil = s.GameMinute + Config.ItemActions.DiarrheaMinutes;
                parts.Add("出现腹泻（12小时）");
            }
        }

        private static void AddUseStat(ActorState actor, StatId id, string name, double amount, List<string> parts, bool actualDelta)
        {
            var before = actor.Stats.Get(id);
            actor.Stats.Add(id, amount);
            parts.Add(Signed(name, actualDelta ? actor.Stats.Get(id) - before : amount));
        }
    }
}
