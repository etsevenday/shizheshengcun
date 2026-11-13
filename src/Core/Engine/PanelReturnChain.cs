using System.Collections.Generic;

namespace Survival.Core.Engine
{
    /// <summary>
    /// BM4a（S14「2026-11-13 B45 Q581—Q590」§Q586、§Q590；逐字稿同日节）：顶栏面板覆盖打开时的窗口返回链（只属于当前会话界面，不进存档）。
    /// 链里只记“被盖住的下层”，最上层是当前显示的面板；每种面板在整条链里最多一份——请求的同类页已在下层时把它提到最上层、
    /// 其余层保持相对顺序，不复制。关闭当前最高层只恢复紧挨着的下一层（一次一层）。
    /// 每层自己的未提交选择、滚动与焦点由网页壳按面板种类保存；这里只管层序。
    /// </summary>
    public sealed class PanelReturnChain
    {
        /// <summary>有下层时原只有「关闭」的顶栏阅读页的关闭控件业务名（逐字稿 Q590「返回 ESC」）。</summary>
        public const string ReturnLabel = "返回";
        /// <summary>无下层时的关闭控件业务名（逐字稿 Q590「关闭 ESC」）。</summary>
        public const string CloseLabel = "关闭";

        /// <summary>系统说明「ESC 与界面层级」新增段落（逐字稿 Q590，取代原顶栏切页收盘的同义说明）。</summary>
        public const string EscHelpParagraph = "在当前允许打开的顶栏面板之间切换时，新面板盖住原窗口，原窗口和未确认的选择会保留。按ESC逐层返回；没有可关闭窗口时，打开当前情境菜单。文字输入、对白和其他锁定仍优先。";

        private readonly List<string> _under = new List<string>();

        /// <summary>被盖住的下层，自底向上（最后一个紧挨着当前最上层）。</summary>
        public IReadOnlyList<string> Under => _under;

        public int Count => _under.Count;

        public bool Contains(string panel) => _under.Contains(panel);

        /// <summary>关闭控件业务名：当前最上层下面还有窗口时「返回」，没有时「关闭」。</summary>
        public string CloseLabelFor() => _under.Count > 0 ? ReturnLabel : CloseLabel;

        /// <summary>
        /// 在当前最上层 <paramref name="top"/> 之上覆盖打开 <paramref name="next"/>：top 进入下层（保留原状态），next 成为最上层。
        /// next 原本就在下层时返回 true（提到最上层，沿用它原来的状态，不新开第二份）。top 与 next 相同时不动、返回 false。
        /// </summary>
        public bool Cover(string top, string next)
        {
            if (top == next) return false;
            var up = _under.Remove(next);
            _under.Remove(top);
            _under.Add(top);
            return up;
        }

        /// <summary>取出紧挨着最上层的那一层（关闭当前最上层后要恢复的窗口）；没有下层为 null。</summary>
        public string? Pop()
        {
            if (_under.Count == 0) return null;
            var p = _under[_under.Count - 1];
            _under.RemoveAt(_under.Count - 1);
            return p;
        }

        /// <summary>从下层拿掉某种面板（被替换跳转重新打开、或已失效）；不在链里返回 false。</summary>
        public bool Remove(string panel) => _under.Remove(panel);

        /// <summary>真实换场、战斗、终局、读档等：整条返回链作废。返回被清掉的下层（自底向上），供壳丢弃各层草稿。</summary>
        public List<string> Clear()
        {
            var all = new List<string>(_under);
            _under.Clear();
            return all;
        }
    }
}
