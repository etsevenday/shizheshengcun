using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// BM4b（S14「2026-11-13 B45 Q581—Q590」§Q589；《重墨 V2 开工裁定与界面逐字稿》同日节 Q589 三行）：常态右栏紧凑表头与次要折叠组的正式文字。
    /// 网页壳只从这里取分组名与可访问名称，自动测试逐条对照逐字稿。
    /// </summary>
    public static class RailText
    {
        // 逐字稿「Q589 次要折叠标题」：说明、批次、内容、加工队列、成员、装备明细、熟练度、词条、计算明细
        public const string Desc = "说明";
        /// <summary>逐字稿「Q589 只读批次标题」：只写「批次」，去掉原括号里的点选／食用／转移承诺。</summary>
        public const string Batch = "批次";
        public const string Content = "内容";
        public const string Queue = "加工队列";
        public const string Members = "成员";
        public const string EquipDetail = "装备明细";
        public const string Skills = "熟练度";
        public const string Traits = "词条";
        public const string Calc = "计算明细";

        public static readonly string[] AllGroups = { Desc, Batch, Content, Queue, Members, EquipDetail, Skills, Traits, Calc };

        /// <summary>有可靠数量时「{分组名}（{数量}）」，否则只写分组名。</summary>
        public static string Title(string group, int? count) => count is int n ? $"{group}（{n}）" : group;

        /// <summary>逐字稿「Q589 折叠操作可访问名称」：展开{分组名}／收起{分组名}。</summary>
        public static string Expand(string group) => $"展开{group}";
        public static string Collapse(string group) => $"收起{group}";
    }

    /// <summary>Q589 首屏优先级表的对象类型（折叠偏好按类型分开保存，不依赖实例号）。</summary>
    public static class RailTypes
    {
        /// <summary>食物、饮水、普通材料。</summary>
        public const string Supply = "supply";
        /// <summary>武器／装备／工具。</summary>
        public const string Gear = "gear";
        /// <summary>锅／设施／加工。</summary>
        public const string Facility = "facility";
        /// <summary>资源／地点／路线。</summary>
        public const string Place = "place";
        public const string Enemy = "enemy";
        /// <summary>当前操作预估。</summary>
        public const string Op = "op";
    }

    /// <summary>次要组的偏好键（人物页的装备／熟练度／词条沿 UiFoldPrefs 原键）。</summary>
    public static class RailGroups
    {
        public const string Desc = "desc";
        public const string Batch = "batch";
        public const string Content = "content";
        public const string Queue = "queue";
        public const string Members = "members";
        public const string Calc = "calc";
    }

    public sealed partial class GameEngine
    {
        /// <summary>
        /// Q589 来源行「{真实地点}[ · {区域}] · {真实持有人或来源}」：地点取当前实际地点；区域只在已建安全屋分区时写「屋外」／「屋内」（同 Q543 位置行口径）；
        /// 持有人／来源取物品真实位置（随身、当前地面、穿戴槽、同行者、火源加工位、锅、晾制架工位）。无数据的部分省略，不写内部编号。
        /// </summary>
        public string RailSourceLine(GameState s, ItemInstance i)
        {
            var loc = s.CurrentLocation;
            var head = LocName(loc);
            if (s.CurrentZone == GameState.SafehouseZone) head += " · 屋内";
            else if (SafehouseBuilt(s, loc)) head += " · 屋外";
            var holder = RailHolder(s, i);
            return string.IsNullOrEmpty(holder) ? head : $"{head} · {holder}";
        }

        /// <summary>Q589 来源行里的「真实持有人或来源」段。</summary>
        public string RailHolder(GameState s, ItemInstance i)
        {
            if (i.IsCarried) return SourceCarried;
            if (ItemInstance.TryEquip(i.Owner, out var actor, out var slot))
                return actor == GameState.PlayerId ? $"穿戴·{EquipSlots.Name(slot)}" : $"{ActorDisplayName(s, actor)} · 穿戴·{EquipSlots.Name(slot)}";
            var mate = s.Actors.Keys.FirstOrDefault(a => a != GameState.PlayerId && i.Owner == ItemInstance.CarriedOwner(a));
            if (mate != null) return $"{ActorDisplayName(s, mate)}随身";
            if (ItemInstance.TryContainerId(i.Owner, out _))
            {
                var pot = ContainerOf(s, i);
                if (pot == null || pot.InstanceId == i.InstanceId) return "";
                var up = RailHolder(s, pot);
                return string.IsNullOrEmpty(up) ? ItemName(pot.ItemId) : $"{up}／{ItemName(pot.ItemId)}";
            }
            if (ItemInstance.TryFacilityId(i.Owner, out var fid))
            {
                var floc = FacilityLocation(fid);
                if (floc == null) return "";
                var fac = FacOf(s, floc);
                var name = fac == null ? "火堆" : CardStates.FireName(fac);
                var k = FireSlots(s, floc).FindIndex(x => x != null && x.InstanceId == i.InstanceId);
                return k < 0 ? name : $"{name}／加工位{k + 1}";
            }
            if (ItemInstance.TryDrying(i.Owner, out _, out var dslot)) return $"晾制架工位{dslot + 1}";
            if (IsGroundOwner(i.Owner)) return i.Owner == s.Ground ? SourceGround : $"{ZoneName(s, i.Owner)}地面";
            return "";
        }
    }
}
