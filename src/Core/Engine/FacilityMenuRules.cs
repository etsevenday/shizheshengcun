using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>B45（S14 Q542／Q559）：工坊汇总卡「选择设施」里的一行（当前区已建的陶窑或固定净水台）。</summary>
    public sealed class StationChoice
    {
        public string StationId { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>行文字：「{设施名} · {已}/{需}分」或「{设施名} · 空闲」（Q559 省一次站名）。</summary>
        public string Line { get; set; } = "";
    }

    /// <summary>
    /// B45「设施加工移至中央次级菜单、右栏只读」的内核只读规则（S14「2026-11-12 用户直接裁定：设施加工改在中央次级菜单操作」、
    /// 「2026-11-12 B45 Q542—Q548」与「2026-11-13 B45 Q558—Q562」；正式句见《重墨 V2 开工裁定与界面逐字稿》同名节）。
    /// 只读、0 分钟，不预留、不抽签。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>Q542：选择设施只列这两类现有加工设施（按原工坊设施顺序）；工作台、锻炉等制作工坊与晾制架不列。</summary>
        public static readonly IReadOnlyList<string> ChooserStationIds = new[] { "kiln", "water_station" };

        public const string ChooserTitle = "选择设施";
        /// <summary>Q560：回到选择层时本区已无可操作的两类设施。</summary>
        public const string ChooserEmpty = "当前区域没有可操作的加工设施。";
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
        /// <summary>BM5b1（S14 Q603；逐字稿「木材原因」）：主层起来源区木材灰行的完整原因（加工位层／锅层仍用原各自拒绝句，Q637 待定）。</summary>
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

        /// <summary>Q542／Q559：当前地点当前区已建的陶窑、固定净水台（原工坊设施顺序），行摘要取该座当前批实际进度，没有正在加工的批为「空闲」。</summary>
        public List<StationChoice> ProcessingStationChoices(GameState s)
        {
            var place = s.Ground;
            var fac = FacOf(s, place);
            var list = new List<StationChoice>();
            if (fac == null) return list;
            foreach (var st in Config.Workshops.Stations)
            {
                if (!ChooserStationIds.Contains(st.Id) || !WorkshopBuilt(s, place, st.Id)) continue;
                var b = fac.StationBatches.FirstOrDefault(x => x.StationId == st.Id && !x.Queued);
                var name = StationName(st.Id);
                list.Add(new StationChoice
                {
                    StationId = st.Id,
                    Name = name,
                    Line = b != null ? $"{name} · {b.DoneMinutes}/{b.RequiredMinutes}分" : $"{name} · 空闲",
                });
            }
            return list;
        }
    }
}
