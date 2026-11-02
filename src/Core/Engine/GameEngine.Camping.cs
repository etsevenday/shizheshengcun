using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 野营遮雨布与折叠柴炉（A07 X1.6、S11 X4、expansion-supply.json operationalRules.camping；开发计划 v0.9 B17 R56）：
    /// 只在普通主世界 temporaryCamp 许可点（8 处）的外部区部署，角色空闲、无战斗/敌踪/移动。
    /// 野营布铺开/收起各 10 分钟：铺开期间当地外部区睡眠算遮蔽（至多 4 名同行），不给安全屋休息精神加成、不挡遭遇、不托底 N/W。
    /// 折叠柴炉部署/收起各 5 分钟：部署后当作本区一处火堆（2 加工位、最多 1 口锅、燃料上限 60、木材每份 60、5 分钟打火器点火且点火段不受热），
    /// 当前区已有固定火堆时不能部署；收起须先取空、熄火，剩余燃料须明确放弃（不退木材）。部署中的布/炉留在当地地面、不能拾取。
    /// 部署与收起按“等待”结算身体消耗（开发假设），可中途取消（物品原样、时间不退）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>部署/收起行动的 TargetId 前缀：deploy:tarp:&lt;实例&gt;、pack:stove:&lt;实例&gt;[:abandon]。</summary>
        public const string DeployPrefix = "deploy:";
        public const string PackPrefix = "pack:";

        private CampingConfig Camping => Config.Safehouse.Camping;

        public bool IsTarp(ItemInstance i) => i.ItemId == Camping.TarpItemId;
        public bool IsStove(ItemInstance i) => i.ItemId == Camping.StoveItemId;

        /// <summary>这件布/炉正部署在某地外部区（不能拾取、搬动或当材料）。</summary>
        public bool IsDeployed(GameState s, ItemInstance i) =>
            s.Locations.TryGetValue(i.Owner, out var ls) && (ls.Facility.TarpInstanceId == i.InstanceId || ls.Facility.StoveInstanceId == i.InstanceId);

        /// <summary>某区铺着野营遮雨布（只在外部区）。</summary>
        public bool TarpDeployedAt(GameState s, string place) =>
            s.Locations.TryGetValue(place, out var ls) && ls.Facility.TarpInstanceId is int id && FindItem(s, id) is ItemInstance t && t.Owner == place;

        /// <summary>睡眠是否遮蔽：原遮蔽（室内、营地棚、安全屋屋内），或铺开的野营遮雨布（只改睡眠条件，A07 X1.6）。</summary>
        public bool SleepSheltered(GameState s, string place) => IsSheltered(s, place) || TarpDeployedAt(s, place);

        /// <summary>某区火堆的燃料上限：折叠柴炉 60，固定火堆 240（A07 X1.6）。</summary>
        public int FuelCapOf(FacilityState fac) => fac.StoveInstanceId.HasValue ? Camping.StoveMaxFuelMinutes : Config.Fire.MaxFuelMinutes;

        /// <summary>当前地点能不能部署野营物品（地点许可与所在区）；能为 null。</summary>
        public string? CampingSiteBlocked(GameState s)
        {
            var lc = _locs[s.CurrentLocation];
            if (s.CurrentZone != ExteriorZone) return "只能在屋外部署";
            if (!lc.TemporaryCamp || lc.WorldKind != "overworld" || lc.IsDungeon) return "只能在临时露营许可点部署（野外 8 处）";
            return null;
        }

        /// <summary>部署（铺开）野营布/柴炉的行动请求。</summary>
        public static ActionRequest Deploy(ItemInstance item) =>
            new ActionRequest { Type = ActionType.Wait, TargetId = DeployPrefix + KindOf(item) + ":" + item.InstanceId };
        /// <summary>收起的行动请求；柴炉还剩燃料时 abandonFuel 表示明确放弃（不退木材）。</summary>
        public static ActionRequest Pack(ItemInstance item, bool abandonFuel = false) =>
            new ActionRequest { Type = ActionType.Wait, TargetId = PackPrefix + KindOf(item) + ":" + item.InstanceId + (abandonFuel ? ":abandon" : "") };

        private static string KindOf(ItemInstance item) => item.ItemId == "folding_stove" ? "stove" : "tarp";

        /// <summary>解析部署/收起请求：(是部署, 是柴炉, 实例, 放弃燃料)。</summary>
        private static bool TryCampingTarget(string target, out bool deploy, out bool stove, out int id, out bool abandon)
        {
            deploy = target.StartsWith(DeployPrefix);
            stove = false; id = 0; abandon = false;
            if (!deploy && !target.StartsWith(PackPrefix)) return false;
            var parts = target.Split(':');
            if (parts.Length < 3 || !int.TryParse(parts[2], out id)) return false;
            stove = parts[1] == "stove";
            abandon = parts.Length > 3 && parts[3] == "abandon";
            return true;
        }

        public static bool IsCampingTarget(string target) => target.StartsWith(DeployPrefix) || target.StartsWith(PackPrefix);

        /// <summary>部署/收起的开工校验（Validate 的 Wait 分支调用）。</summary>
        private string? ValidateCamping(GameState s, ActionRequest r, Plan plan)
        {
            if (!TryCampingTarget(r.TargetId, out var deploy, out var stove, out var id, out var abandon)) return "部署请求无效";
            if (CampingSiteBlocked(s) is string site) return site;
            var item = FindItem(s, id);
            var name = stove ? "折叠柴炉" : "野营遮雨布";
            if (item == null || (stove ? !IsStove(item) : !IsTarp(item))) return $"{name}已不在";
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            if (deploy)
            {
                if (!(item.IsCarried || item.Owner == place)) return $"只能部署随身或当前地面的{name}";
                if (item.ReservedBy != null || IsLocked(s, item)) return "该物品已被预留";
                if (stove)
                {
                    if (fac.StoveInstanceId.HasValue) return "这里已经部署了一个折叠柴炉";
                    if (HasFireAt(s, place)) return "当前区已有固定火堆，不能再部署第二处火堆";
                }
                else if (fac.TarpInstanceId.HasValue) return "这里已经铺开了一块野营遮雨布";
            }
            else
            {
                if ((stove ? fac.StoveInstanceId : fac.TarpInstanceId) != id) return $"这块{name}没有部署在当前区";
                if (stove)
                {
                    if (FireContents(s, place).Count > 0) return "先把锅和加工位里的东西取出来再收起";
                    if (fac.FireLit) return "先熄火再收起";
                    if (fac.FuelMinutes > 0 && !abandon) return $"柴炉里还剩燃料{fac.FuelMinutes}分钟：要放弃这些燃料（不退木材）才能收起";
                }
            }
            plan.Planned = stove ? (deploy ? Camping.StoveDeployMinutes : Camping.StovePackMinutes) : (deploy ? Camping.TarpDeployMinutes : Camping.TarpPackMinutes);
            plan.Label = $"{(deploy ? (stove ? "部署" : "铺开") : "收起")}{name}";
            plan.CampingItem = item;
            return null;
        }

        /// <summary>部署/收起完成（等待到点，在当前区原子提交）。</summary>
        private void CompleteCamping(GameState s, ActionState a)
        {
            if (!TryCampingTarget(a.TargetId, out var deploy, out var stove, out var id, out _)) return;
            var item = FindItem(s, id);
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            var name = stove ? "折叠柴炉" : "野营遮雨布";
            if (item == null) { Emit(s, GameEventKind.ActionAborted, $"{name}已不在，没有完成。"); return; }
            if (deploy)
            {
                if (item.Owner != place) MoveItems(s, item, 1, place);
                if (stove)
                {
                    fac.StoveInstanceId = item.InstanceId;
                    fac.FacilityId = FireIdOf(place);
                    fac.FireLit = false;
                    fac.WentOut = false;
                    fac.FuelMinutes = 0;
                    SyncFacilities(s);
                    Emit(s, GameEventKind.ActionCompleted, $"折叠柴炉部署好：当作{LocName(place)}的一处火堆（2个加工位、最多1口锅、燃料上限{Camping.StoveMaxFuelMinutes}分钟），初始熄灭、没有燃料和锅。");
                }
                else
                {
                    fac.TarpInstanceId = item.InstanceId;
                    Emit(s, GameEventKind.ActionCompleted, $"野营遮雨布铺开：在{LocName(place)}睡眠算遮蔽（至多{Camping.TarpPartyCapacity}人）；不挡遭遇、没有安全屋休息加成。");
                }
            }
            else
            {
                if (stove)
                {
                    var lost = fac.FuelMinutes;
                    fac.StoveInstanceId = null;
                    fac.FuelMinutes = 0;
                    fac.FireLit = false;
                    fac.WentOut = false;
                    fac.FacilityId = "";
                    fac.ProcessingSlots.Clear();
                    fac.Processes.Clear();
                    Emit(s, GameEventKind.ActionCompleted, "折叠柴炉已收起，放在当前地面" + (lost > 0 ? $"（放弃剩余燃料{lost}分钟，不退木材）。" : "。"));
                }
                else
                {
                    fac.TarpInstanceId = null;
                    Emit(s, GameEventKind.ActionCompleted, "野营遮雨布已收起，放在当前地面。");
                }
            }
            UpdateFlags(s);
        }
    }
}
