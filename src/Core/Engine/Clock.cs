namespace Survival.Core.Engine
{
    /// <summary>绝对游戏分钟的换算。第1日00:00 = 分钟0。</summary>
    public static class Clock
    {
        public const int MinutesPerDay = 1440;

        public static int Day(int minute) => minute / MinutesPerDay + 1;
        public static int MinuteOfDay(int minute) => minute % MinutesPerDay;

        public static bool IsDaylight(int minute, int dayStart = 360, int dayEnd = 1080)
        {
            var m = MinuteOfDay(minute);
            return m >= dayStart && m < dayEnd;
        }

        public static string HHmm(int minute)
        {
            var m = MinuteOfDay(minute);
            return $"{m / 60:00}:{m % 60:00}";
        }

        public static string DayTime(int minute) => $"第{Day(minute)}日 {HHmm(minute)}";

        /// <summary>到下一次昼夜切换还剩多少分钟。</summary>
        public static int MinutesToSwitch(int minute, int dayStart = 360, int dayEnd = 1080)
        {
            var m = MinuteOfDay(minute);
            if (m < dayStart) return dayStart - m;
            if (m < dayEnd) return dayEnd - m;
            return MinutesPerDay - m + dayStart;
        }

        public static string Duration(int minutes)
        {
            if (minutes < 60) return $"{minutes}分钟";
            var h = minutes / 60;
            var mm = minutes % 60;
            return mm == 0 ? $"{h}小时" : $"{h}小时{mm}分";
        }
    }
}
