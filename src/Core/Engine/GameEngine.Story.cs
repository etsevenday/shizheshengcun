using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 开场、饱饱与首战（N01、S15 §5、R07）：新局在营地醒来 → 饱饱对白与饮食提示 → 首次 camp→field 强制游荡尸首战 →
    /// 胜利得唯一教学活性物质 → 吞噬融合得绿色“攻击力+0.1”。旧档（T06）不补。对白不推进时间、不发奖励。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>本局有没有本轮新剧情（新局为真；旧档迁移为假）。</summary>
        public static bool StoryActive(GameState s) => !s.Story.Legacy;

        /// <summary>新局：饱饱在体内；荒地初始游荡尸登记为首战保留实例。</summary>
        public void InitStory(GameState s)
        {
            var st = new StoryState { BaobaoPresent = Config.Infection.BaobaoInput.NewGamePresent };
            var tb = Config.Story.TutorialBattle;
            if (!string.IsNullOrEmpty(tb.LocationId))
                st.TutorialEnemyId = s.WorldEnemies.Where(w => w.LocationId == tb.LocationId && w.EnemyId == tb.EnemyId).OrderBy(w => w.Id).FirstOrDefault()?.Id;
            s.Story = st;
        }
    }
}
