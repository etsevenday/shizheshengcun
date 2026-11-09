using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 事项盘忙碌反馈的归属（N02 附录 §2「2026-11-10 Q318 · 事项盘忙碌反馈清理」）：
    /// 在城市事项盘里触发受阻控件后，原反馈位（网页壳 GameSession.Notice）写的 Q317 句 <see cref="GameEngine.CityBusyText"/>
    /// 依附触发时实际占用的行动（主行动与补给子行动的 ActionId）。这些行动全部完成、取消或已停止时立即清掉这条反馈；
    /// 退出请求尚待段边界（单段事务“本段完成后结束”）时行动仍在，按实际占用保留；之后开始的新行动不继承旧反馈。
    /// 只认这一句，其他原因、结果、日志与存档提示不经这里。只是界面状态，不进存档。
    /// 网页壳每次给反馈位赋值时调 <see cref="Rearm"/>（新一次触发），每次刷新界面前调 <see cref="Observe"/>。
    /// </summary>
    public sealed class BusyNoticeTracker
    {
        private string? _text;
        private int[] _owners = new int[0];

        /// <summary>当前这条忙碌反馈依附的行动 ActionId（未依附为空）。</summary>
        public IReadOnlyList<int> Owners => _owners;

        /// <summary>是否 Q317 事项盘忙碌反馈这一句。</summary>
        public static bool IsBusyNotice(string? notice) => notice == GameEngine.CityBusyText;

        /// <summary>当前实际占用的行动：主行动（含暂停）与补给子行动。</summary>
        public static int[] Occupants(GameState? s)
        {
            if (s == null) return new int[0];
            var list = new List<int>(2);
            if (s.Main != null) list.Add(s.Main.ActionId);
            if (s.Sub != null) list.Add(s.Sub.ActionId);
            return list.ToArray();
        }

        /// <summary>反馈位被重新赋值（新一次触发或被别的提示替换）：下一次 <see cref="Observe"/> 按当时的占用重新归属。</summary>
        public void Rearm()
        {
            _text = null;
            _owners = new int[0];
        }

        /// <summary>
        /// 刷新前核对：反馈位是忙碌句时，新触发的记下当时占用的行动；已归属的在这些行动都不再占用时返回 true（应清掉这条反馈）。
        /// 触发时已没有任何行动占用也返回 true（句子已失效）。反馈位是别的内容时只清空归属，返回 false。
        /// </summary>
        public bool Observe(string? notice, GameState? s)
        {
            if (!IsBusyNotice(notice)) { Rearm(); return false; }
            var now = Occupants(s);
            if (_text == null)
            {
                if (now.Length == 0) { Rearm(); return true; }
                _text = notice;
                _owners = now;
                return false;
            }
            if (_owners.Any(id => now.Contains(id))) return false;
            Rearm();
            return true;
        }

        /// <summary>暂停层此刻是否显示（主行动暂停且不在制作面板，或补给子行动进行中），与 GameScreen 的显示条件一致。</summary>
        public static bool PauseLayerShown(GameState? s, string? panel) =>
            s != null && ((s.Main != null && s.Main.Status == ActionStatus.Paused && panel != "craft") || s.Sub != null);

        /// <summary>
        /// 右栏该显示的反馈：暂停层显示时，忙碌句只在暂停层底部出现一次，右栏省略；其他反馈与其他时候照常。
        /// </summary>
        public static string? RightColumnNotice(string? notice, GameState? s, string? panel) =>
            IsBusyNotice(notice) && PauseLayerShown(s, panel) ? null : notice;
    }
}
