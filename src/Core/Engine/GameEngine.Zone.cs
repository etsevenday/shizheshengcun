using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 地点内的区（zone，S09 X2.1/X2.5/X2.7，开发计划 v0.9 B17）：每个地点有外部区（地点默认区域，旧地面 owner 就是地点 ID），
    /// 已完工安全屋另有屋内区（地面 owner = safehouseFloor:&lt;safehouseId&gt;）。
    /// “place”= 某个区的地面 owner 字符串：外部区就是地点 ID（与旧档完全相同），屋内区是屋内地面 owner。
    /// 火堆、加工位、工坊、床都挂在 place 对应的 FacilityState 上；火堆按 place 唯一（“每个可访问区域内唯一”）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string ExteriorZone = GameState.ExteriorZone;
        public const string SafehouseZone = GameState.SafehouseZone;
        /// <summary>屋内火堆设施 ID 的后缀：&lt;地点&gt;:safehouse:fire（外部火堆仍是旧的 &lt;地点&gt;:fire，旧档不改）。</summary>
        public const string SafehouseFireSuffix = ":" + GameState.SafehouseZone + FireSuffix;

        /// <summary>某地点某区的 place（地面 owner）。</summary>
        public static string ZonePlace(string locationId, string zoneId) =>
            zoneId == SafehouseZone ? ItemInstance.SafehouseFloorOwner(ItemInstance.SafehouseIdOf(locationId)) : locationId;

        /// <summary>拆 place：外部区（地点 ID）或屋内区（safehouseFloor:sh_&lt;地点&gt;）；都不是返回 false。</summary>
        public bool TryPlace(string place, out string locationId, out string zoneId)
        {
            zoneId = ExteriorZone;
            locationId = place;
            if (_locs.ContainsKey(place)) return true;
            if (ItemInstance.TrySafehouseFloor(place, out var sid) && ItemInstance.TrySafehouseLocation(sid, out var loc) && _locs.ContainsKey(loc))
            {
                locationId = loc;
                zoneId = SafehouseZone;
                return true;
            }
            locationId = "";
            return false;
        }

        /// <summary>place 所在的地点（不是 place 时为空串）。</summary>
        public string LocationOfPlace(string place) => TryPlace(place, out var loc, out _) ? loc : "";

        /// <summary>这个 owner 是不是某个区的地面（外部地面或安全屋屋内地面）。</summary>
        public bool IsGroundOwner(string owner) => TryPlace(owner, out _, out _);

        /// <summary>主角当前所在区的 place（= 当前地面 owner）。</summary>
        public static string CurrentPlace(GameState s) => s.Ground;

        /// <summary>某区的设施状态：外部区为地点 FacilityState，屋内区为该屋的屋内设施；没有为 null。</summary>
        public FacilityState? FacOf(GameState s, string place)
        {
            if (!TryPlace(place, out var loc, out var zone)) return null;
            if (!s.Locations.TryGetValue(loc, out var ls)) return null;
            if (zone == ExteriorZone) return ls.Facility;
            return ls.Safehouse?.Interior;
        }

        /// <summary>这个区有没有可用的火堆：外部区看地点配置（临时营地），屋内区看屋内火堆已建成。</summary>
        public bool HasFireAt(GameState s, string place)
        {
            if (!TryPlace(place, out var loc, out var zone)) return false;
            if (zone == ExteriorZone) return _locs[loc].HasFire || (s.Locations.TryGetValue(loc, out var ls) && ls.Facility.StoveInstanceId.HasValue);
            return s.Locations.TryGetValue(loc, out var l2) && l2.Safehouse != null && l2.Safehouse.Built && l2.Safehouse.Interior.FireBuilt;
        }

        /// <summary>所有现在有火堆的区（世界时钟统一推进：离开后照样燃烧、加工）。</summary>
        public IEnumerable<string> FirePlaces(GameState s)
        {
            foreach (var kv in s.Locations)
            {
                if (!_locs.ContainsKey(kv.Key)) continue;
                if (HasFireAt(s, kv.Key)) yield return kv.Key;
                var inner = ZonePlace(kv.Key, SafehouseZone);
                if (kv.Value.Safehouse != null && HasFireAt(s, inner)) yield return inner;
            }
        }

        /// <summary>全部区的设施状态（外部区 + 已开工记录的安全屋屋内）。</summary>
        public IEnumerable<(string Place, FacilityState Fac)> AllFacilityStates(GameState s)
        {
            foreach (var kv in s.Locations)
            {
                yield return (kv.Key, kv.Value.Facility);
                if (kv.Value.Safehouse != null) yield return (ZonePlace(kv.Key, SafehouseZone), kv.Value.Safehouse.Interior);
            }
        }

        /// <summary>区的显示名（带状态）：屋内区用安全屋显示名（玩家改过名用改的），外部区为地点名（B17 试玩修复 B3）。</summary>
        public string ZoneName(GameState s, string place) =>
            TryPlace(place, out var loc, out var zone) && zone == SafehouseZone ? SafehouseName(s, loc) : LocName(place);

        /// <summary>区的显示名：外部区为地点名，屋内区为“××安全屋”。</summary>
        public string PlaceName(string place)
        {
            if (!TryPlace(place, out var loc, out var zone)) return place;
            return zone == SafehouseZone ? LocName(loc) + "安全屋" : LocName(loc);
        }
    }
}
