using System;

namespace Survival.Core.State
{
    /// <summary>阅读字体（Q295、PXb）：默认「细像素」；「清晰阅读」用系统黑体，是 Q275 的玩家自选例外。</summary>
    public enum ReadingFont
    {
        /// <summary>细像素：寒蝉点阵体 16px（Q275 默认）。</summary>
        Pixel,
        /// <summary>清晰阅读：系统黑体。</summary>
        Clear,
    }

    /// <summary>动画速度（Q295、Q321）：快／标准／慢 = 基准呈现时长 ×0.5／×1／×1.5，默认标准。</summary>
    public enum AnimationSpeed
    {
        Fast,
        Standard,
        Slow,
    }

    /// <summary>
    /// 阅读字体与动画速度两项浏览器偏好（重墨 V2 开工逐字稿 §1 Q295、§1.1 Q321；开发计划 v1.0 B31a）。
    /// 纯界面偏好：存当前浏览器（localStorage），不进存档；换档、新游戏、重载沿用。常态菜单与战斗菜单读同一份。
    /// 没有记录或记录无效时用默认（细像素、标准），读到无效值不写回。
    /// 速度只改变呈现时长：世界推进每 5 游戏分钟段基准 120 毫秒 → 60／120／180；当前敌方逐条展示基准 480 毫秒 → 240／480／720。
    /// 世界分钟、结算与跳过、退出响应都与速度无关。
    /// </summary>
    public sealed class UiPresentationPrefs
    {
        /// <summary>浏览器偏好键（网页壳加前缀 survival.pref.）。</summary>
        public const string FontKey = "readFont";
        public const string SpeedKey = "animSpeed";

        public const ReadingFont DefaultFont = ReadingFont.Pixel;
        public const AnimationSpeed DefaultSpeed = AnimationSpeed.Standard;

        /// <summary>世界推进每个 5 游戏分钟段的基准呈现间隔（Q321，取代 S14 的 40／120／250 三档）。</summary>
        public const int SegmentBaseMs = 120;
        /// <summary>当前敌方逐条展示的基准间隔（Q321；Q297 特写替换前的过渡展示）。</summary>
        public const int EnemyStepBaseMs = 480;

        public ReadingFont Font { get; set; } = DefaultFont;
        public AnimationSpeed Speed { get; set; } = DefaultSpeed;

        /// <summary>该档对基准时长的倍率：快 ×0.5、标准 ×1、慢 ×1.5。</summary>
        public static double Multiplier(AnimationSpeed speed) => speed switch
        {
            AnimationSpeed.Fast => 0.5,
            AnimationSpeed.Slow => 1.5,
            _ => 1.0,
        };

        /// <summary>按档位缩放一个基准呈现时长（四舍五入到整毫秒，至少 0）。</summary>
        public static int Scale(int baseMs, AnimationSpeed speed) => Math.Max(0, (int)Math.Round(baseMs * Multiplier(speed), MidpointRounding.AwayFromZero));

        public int SegmentDelayMs => Scale(SegmentBaseMs, Speed);
        public int EnemyStepDelayMs => Scale(EnemyStepBaseMs, Speed);

        /// <summary>存储值（小写英文，与 wwwroot/js/app.js 的启动预读一致）。</summary>
        public static string FontValue(ReadingFont f) => f == ReadingFont.Clear ? "clear" : "pixel";
        public static string SpeedValue(AnimationSpeed s) => s switch
        {
            AnimationSpeed.Fast => "fast",
            AnimationSpeed.Slow => "slow",
            _ => "standard",
        };

        /// <summary>读回字体偏好；缺失或无效时为默认（细像素）。</summary>
        public static ReadingFont ParseFont(string? raw) => raw switch
        {
            "clear" => ReadingFont.Clear,
            "pixel" => ReadingFont.Pixel,
            _ => DefaultFont,
        };

        /// <summary>读回速度偏好；缺失或无效时为默认（标准）。</summary>
        public static AnimationSpeed ParseSpeed(string? raw) => raw switch
        {
            "fast" => AnimationSpeed.Fast,
            "standard" => AnimationSpeed.Standard,
            "slow" => AnimationSpeed.Slow,
            _ => DefaultSpeed,
        };

        public static UiPresentationPrefs Parse(string? font, string? speed) =>
            new UiPresentationPrefs { Font = ParseFont(font), Speed = ParseSpeed(speed) };

        /// <summary>菜单显示名（逐字稿 §2 PXb）。</summary>
        public static string Label(ReadingFont f) => f == ReadingFont.Clear ? "清晰阅读" : "细像素";
        public static string Label(AnimationSpeed s) => s switch
        {
            AnimationSpeed.Fast => "快",
            AnimationSpeed.Slow => "慢",
            _ => "标准",
        };
    }
}
