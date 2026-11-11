using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>Q385 手动存档阻断（由上到下只取第一条命中的；写权限另由存档协调器先判，Q408-3）。</summary>
    public enum ManualSaveBlock
    {
        None,
        /// <summary>主角已终局：死亡结算菜单不提供入口（Q385），没有原因句。</summary>
        Ended,
        Dialogue,
        Battle,
        Encounter,
        Extracting,
        Running,
        Paused,
        ForcedTidy,
        RecruitPlacement,
    }

    public sealed partial class GameEngine
    {
        /// <summary>
        /// 存档卡摘要（Q335 / Q407）：游戏内时刻、当前实际地点名（与品牌行同取：移动途中是起点、安全屋内只写地点名、
        /// 终局后状态不再移动即终局地点）、是否已结束。
        /// </summary>
        public SaveSlotSummary SaveSummaryOf(GameState s) => new SaveSlotSummary
        {
            Minute = s.GameMinute,
            Location = SaveLocationName(s.CurrentLocation),
            Ended = s.Progress.Dead,
        };

        /// <summary>存档卡上的地点名：配置里有的地点取显示名，读不出返回 null（卡面省略，Q404）。</summary>
        public string? SaveLocationName(string? locationId) =>
            !string.IsNullOrEmpty(locationId) && _locs.ContainsKey(locationId!) ? LocName(locationId!) : null;

        /// <summary>
        /// Q385 / Q411 手动存档的安全空闲阻断（不含写权限）：强制对白、战斗、敌踪、撤出、主行动推进或子行动、主行动暂停、
        /// 强制整理（同类双武器、超五卡）、招募待安置。Q243 岗位接续待补播不算。
        /// </summary>
        public ManualSaveBlock ManualSaveBlocked(GameState s)
        {
            if (s.Progress.Dead) return ManualSaveBlock.Ended;
            if (DialogueLocked(s)) return ManualSaveBlock.Dialogue;
            if (s.Battle != null) return ManualSaveBlock.Battle;
            if (s.Encounter != null) return ManualSaveBlock.Encounter;
            if (s.Expedition?.IsExtracting == true) return ManualSaveBlock.Extracting;
            if (s.Sub != null || (s.Main != null && s.Main.Status != ActionStatus.Paused)) return ManualSaveBlock.Running;
            if (s.Main != null) return ManualSaveBlock.Paused;
            if (DuplicateWeaponsIllegal(s) || CarriedOverflow(s)) return ManualSaveBlock.ForcedTidy;
            if (s.Recruit.Pending?.AwaitingPlacement == true) return ManualSaveBlock.RecruitPlacement;
            return ManualSaveBlock.None;
        }

        /// <summary>Q385 阻断原句（Ended、None 没有原句）。</summary>
        public static string? ManualSaveBlockText(ManualSaveBlock b) => b switch
        {
            ManualSaveBlock.Dialogue => "请先读完对话",
            ManualSaveBlock.Battle => "战斗中不能手动存档。",
            ManualSaveBlock.Encounter => "先处理敌踪再存档。",
            ManualSaveBlock.Extracting => "撤出途中不能手动存档。",
            ManualSaveBlock.Running => "行动进行中，不能手动存档。",
            ManualSaveBlock.Paused => "行动暂停中，先继续或退出行动再存档。",
            ManualSaveBlock.ForcedTidy => "先完成整理再存档。",
            ManualSaveBlock.RecruitPlacement => "先办完当前事务再存档。",
            _ => null,
        };
    }
}
