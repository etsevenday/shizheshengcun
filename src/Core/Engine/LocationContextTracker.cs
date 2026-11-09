namespace Survival.Core.Engine
{
    /// <summary>
    /// 右栏对象上下文所属的真实地点（S14「2026-11-09 Q281–Q282」Q281）：以真实地点 ID 前后不同为条件。
    /// 网页壳每次刷新界面前用当前 GameState.CurrentLocation 调 <see cref="Observe"/>；返回 true 表示实际地点已变
    /// （正常抵达、撤退、撤出逐段换点、现有传送），应在新地点首次可操作前清掉旧右栏选中及只依附它的临时状态。
    /// 打开地图、鸟瞰预览、移动在途、取消或移动失败都不改 CurrentLocation，返回 false；同地点进出安全屋区（只改 CurrentZone）也不算。
    /// 只是界面状态，不进存档。
    /// </summary>
    public sealed class LocationContextTracker
    {
        private string? _location;

        /// <summary>上次记下的地点；null 表示还没有基准（新开局、读档或回标题后）。</summary>
        public string? Location => _location;

        /// <summary>记下当前地点；与上次记下的不同（且上次有基准）时返回 true。</summary>
        public bool Observe(string? location)
        {
            var changed = _location != null && location != null && location != _location;
            _location = location;
            return changed;
        }

        /// <summary>清掉基准（新开局、读档、回标题）：下一次 Observe 只记基准，不算地点改变。</summary>
        public void Reset() => _location = null;
    }
}
