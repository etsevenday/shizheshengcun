using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B45「设施加工移至中央次级菜单、右栏只读」的内核只读规则（S14「2026-11-12 用户直接裁定：设施加工改在中央次级菜单操作」、
    /// 「2026-11-12 B45 Q542—Q548」与「2026-11-13 B45 Q558—Q562」；正式句见《重墨 V2 开工裁定与界面逐字稿》同名节）。
    /// 只读、0 分钟，不预留、不抽签。
    /// </summary>
    public sealed partial class GameEngine
    {
        // B35a1（Q795 B）：工坊汇总卡与「选择设施」层撤掉（每座真实设施一张卡直接进入，见 GameEngine.CraftScope.cs）
        /// <summary>FACPROC 第 5 条：菜单开启期间设施真实消失。</summary>
        public const string FacGoneText = "这处设施已无法操作，请关闭后重新选择。";
        /// <summary>BM4a（Q586；逐字稿 Q586 行）：关上层窗口返回时原设施已无法操作、不再恢复加工窗口（原仍开着菜单时的关闭提示仍用 <see cref="FacGoneText"/>）。</summary>
        public const string FacReturnGoneText = "这处设施已无法操作，请重新选择。";
        /// <summary>FACPROC／Q544：来源或目标失效（保持原句）。</summary>
        public const string ContentChangedText = "内容已变化，请重新选择。";
        public const string PickSourceText = "请选择原料来源。";
        public const string PickTargetText = "请选择加工目标。";
        /// <summary>Q545：原预演最大 0 但没有更具体原因。</summary>
        public const string NoQtyText = "当前没有可放入数量。";
        /// <summary>BM5b1（S14 Q603；逐字稿「木材原因」）：主层起来源区木材灰行的完整原因（加工位层／锅层仍用原各自拒绝句，Q637 选 A 保留；更优先原拒绝句不被覆盖）。</summary>
        public const string WoodSourceWhy = "木材用于添加燃料；烧制木炭请在空加工位选择‘煅烧木炭’。";
        public const string SourceTitle = "原料来源";
        public const string SourceCarried = "随身";
        public const string SourceGround = "当前地面";

        /// <summary>
        /// Q543／Q561：火堆／柴炉加工菜单的位置行。无已建安全屋分区：「当前位置：{地点}」；已分区按真实所在区「 · 屋外」或「 · 屋内」。
        /// 不把仅有安全屋建造资格当已分区。
        /// </summary>
        public string FacMenuPlaceLine(GameState s)
        {
            var loc = s.CurrentLocation;
            var head = $"当前位置：{LocName(loc)}";
            if (s.CurrentZone == GameState.SafehouseZone) return head + " · 屋内";
            return SafehouseBuilt(s, loc) ? head + " · 屋外" : head;
        }
    }
}
