using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>持久世界敌人、刷新、巡游与接敌（v0.6 §14、§15）。M41 实现，M37 先留世界 tick 的挂点。</summary>
    public sealed partial class GameEngine
    {
        /// <summary>战斗中或敌踪待选：普通行动推进暂停，由战斗流程自己推进世界时间。</summary>
        public bool InCombatFlow(GameState s) => false;

        private void TickWorldEnemies(GameState s) { }
    }
}
