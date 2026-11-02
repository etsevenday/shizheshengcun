using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed partial class GameEngine
    {
        // 换物在 M369 实装；本里程碑只占位（面板换物页不可用）
        private string? TradeSampleBlocked(GameState s, ItemInstance i) => "以物换物尚未开放";

        private void FillTradePreview(GameState s, AppraisalView v)
        {
            v.TradeBlocked = "以物换物尚未开放";
        }

        private string? CompleteTrade(GameState s, CityServiceTxn p, ItemInstance sample, out string made)
        {
            made = "";
            return "以物换物尚未开放";
        }
    }
}
