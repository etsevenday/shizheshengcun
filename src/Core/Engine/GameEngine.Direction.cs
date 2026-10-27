using System;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>地块相对方位（S02 §3.1、S14 §16）：八方向，屏幕坐标 X 向右、Y 向下。None = 自身、未发现或方位无法判定。</summary>
    public enum MapDirection
    {
        None,
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft,
    }

    public sealed partial class GameEngine
    {
        /// <summary>
        /// 从 from 看 to 的方位：以区域图 mapX/mapY 为唯一来源，取目标减原点的向量最接近的八方向。
        /// 扇区按屏幕顺时针从“上”起每45°一个，正好落在 22.5° 边界时归顺时针下一扇区（S02 §3.1、开发假设 A13），
        /// 因此 A→B 与 B→A 恒为相反方向。两点坐标重合（布局缺失/重叠）返回 None，不按卡序编造方向。
        /// </summary>
        public static MapDirection DirectionBetween(LocationConfig from, LocationConfig to)
        {
            var dx = to.MapX - from.MapX;
            var dy = to.MapY - from.MapY;
            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return MapDirection.None;
            // 屏幕“上”为 0°、顺时针为正：atan2(dx, −dy)
            var deg = Math.Atan2(dx, -dy) * 180 / Math.PI;
            if (deg < 0) deg += 360;
            var sector = (int)Math.Floor((deg + 22.5 + 1e-9) / 45) % 8;
            return (MapDirection)(sector + 1);
        }

        /// <summary>
        /// 某地点相对主角当前所在地的方位（S14 §16）：参照点是 s.CurrentLocation——移动途中、暂停、取消或死亡都仍是起点，
        /// 到达提交后才换参照；当前地点自身、未发现地点（不泄露）以及方位无法判定时返回 None。只读，不推进时间、不需存档字段。
        /// </summary>
        public MapDirection DirectionFromHere(GameState s, string locationId)
        {
            if (locationId == s.CurrentLocation) return MapDirection.None;
            if (!_locs.TryGetValue(locationId, out var to) || !_locs.TryGetValue(s.CurrentLocation, out var from)) return MapDirection.None;
            if (!s.Locations.TryGetValue(locationId, out var ls) || !ls.Discovered) return MapDirection.None;
            return DirectionBetween(from, to);
        }

        /// <summary>方位箭头字符：↑ ↗ → ↘ ↓ ↙ ← ↖；None 为空串（不显示伪箭头）。</summary>
        public static string DirectionArrow(MapDirection d) => d switch
        {
            MapDirection.Up => "↑",
            MapDirection.UpRight => "↗",
            MapDirection.Right => "→",
            MapDirection.DownRight => "↘",
            MapDirection.Down => "↓",
            MapDirection.DownLeft => "↙",
            MapDirection.Left => "←",
            MapDirection.UpLeft => "↖",
            _ => "",
        };

        /// <summary>方位短名：上/右上/右/右下/下/左下/左/左上；None 为空串。</summary>
        public static string DirectionName(MapDirection d) => d switch
        {
            MapDirection.Up => "上",
            MapDirection.UpRight => "右上",
            MapDirection.Right => "右",
            MapDirection.DownRight => "右下",
            MapDirection.Down => "下",
            MapDirection.DownLeft => "左下",
            MapDirection.Left => "左",
            MapDirection.UpLeft => "左上",
            _ => "",
        };

        /// <summary>悬停/辅助名称：“位于当前地点右侧”“位于当前地点右上方”；None 为“方位未配置”。</summary>
        public static string DirectionHint(MapDirection d) => d switch
        {
            MapDirection.None => "方位未配置",
            MapDirection.Left or MapDirection.Right => $"位于当前地点{DirectionName(d)}侧",
            _ => $"位于当前地点{DirectionName(d)}方",
        };

        /// <summary>相反方向（往返校验用）。</summary>
        public static MapDirection Opposite(MapDirection d) => d == MapDirection.None ? d : (MapDirection)(((int)d - 1 + 4) % 8 + 1);
    }
}
