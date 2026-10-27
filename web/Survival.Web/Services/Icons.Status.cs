namespace Survival.Web.Services;

/// <summary>
/// 左栏状态图标（S14 §12.2）：开发方同风格简笔占位图，形状/图案互不相同，不只靠颜色区分；
/// 没有专用图的状态用统一占位 status_unknown（圆框问号），并保留可访问名称。正式美术可按键替换。
/// </summary>
public static partial class Icons
{
    private static readonly Dictionary<string, string> Status = new()
    {
        // 腹泻：水滴 + 向下箭头
        ["status_diarrhea"] = @"<path d=""M40 10Q20 40 20 56 20 76 40 78 60 76 60 56 60 40 40 10Z"" fill=""#7ba7c4"" stroke=""#3e6680"" stroke-width=""4""/><path d=""M74 30V82M62 70L74 84 86 70"" fill=""none"" stroke=""#e4b374"" stroke-width=""7"" stroke-linecap=""round"" stroke-linejoin=""round""/>",
        // 出血：两滴血
        ["status_bleed"] = @"<path d=""M38 8Q16 42 16 60 16 82 38 84 60 82 60 60 60 42 38 8Z"" fill=""#c4483a"" stroke=""#7a231b"" stroke-width=""4""/><path d=""M74 44Q62 62 62 72 62 84 74 86 86 84 86 72 86 62 74 44Z"" fill=""#c4483a"" stroke=""#7a231b"" stroke-width=""4""/><path d=""M30 52Q28 62 34 70"" stroke=""#f2c0b6"" stroke-width=""4"" fill=""none"" stroke-linecap=""round""/>",
        // 绷带冷却：斜绷带 + 小钟
        ["status_bandage_cd"] = @"<rect x=""10"" y=""36"" width=""62"" height=""26"" rx=""10"" transform=""rotate(-30 41 49)"" fill=""#efe3c4"" stroke=""#8a7a55"" stroke-width=""4""/><circle cx=""70"" cy=""68"" r=""20"" fill=""#2f3a42"" stroke=""#c8d2d6"" stroke-width=""4""/><path d=""M70 56V68L78 74"" stroke=""#c8d2d6"" stroke-width=""4"" fill=""none"" stroke-linecap=""round""/>",
        // 饱饱持续影响：抽象柔软病毒团
        ["status_baobao"] = @"<path d=""M50 14Q66 12 74 26 90 30 86 48 94 64 78 74 72 90 54 86 38 94 28 80 10 74 16 56 6 40 22 30 30 14 50 14Z"" fill=""#8fbf7a"" stroke=""#4f7a3e"" stroke-width=""4""/><circle cx=""40"" cy=""46"" r=""5"" fill=""#2c3a24""/><circle cx=""60"" cy=""46"" r=""5"" fill=""#2c3a24""/><path d=""M40 62Q50 70 60 62"" stroke=""#2c3a24"" stroke-width=""4"" fill=""none"" stroke-linecap=""round""/>",
        // 病毒感染：带刺球
        ["status_infected"] = @"<g stroke=""#5a7a2a"" stroke-width=""5"" stroke-linecap=""round""><path d=""M50 8V22M50 78V92M8 50H22M78 50H92M20 20L30 30M70 70L80 80M80 20L70 30M30 70L20 80""/></g><circle cx=""50"" cy=""50"" r=""26"" fill=""#a6c45a"" stroke=""#5a7a2a"" stroke-width=""5""/><circle cx=""42"" cy=""44"" r=""5"" fill=""#5a7a2a""/><circle cx=""58"" cy=""58"" r=""6"" fill=""#5a7a2a""/>",
        // 抗生素作用中：胶囊
        ["status_antibiotic"] = @"<g transform=""rotate(-35 50 50)""><rect x=""14"" y=""34"" width=""72"" height=""32"" rx=""16"" fill=""#efe9d5"" stroke=""#4e5752"" stroke-width=""4""/><path d=""M50 34H70Q86 34 86 50 86 66 70 66H50Z"" fill=""#c4483a"" stroke=""#4e5752"" stroke-width=""4""/></g>",
        // 抗生素给药间隔：胶囊 + 小钟
        ["status_antibiotic_cd"] = @"<g transform=""rotate(-35 40 44)""><rect x=""8"" y=""30"" width=""62"" height=""28"" rx=""14"" fill=""#efe9d5"" stroke=""#4e5752"" stroke-width=""4""/><path d=""M39 30H56Q70 30 70 44 70 58 56 58H39Z"" fill=""#c4483a"" stroke=""#4e5752"" stroke-width=""4""/></g><circle cx=""70"" cy=""70"" r=""20"" fill=""#2f3a42"" stroke=""#c8d2d6"" stroke-width=""4""/><path d=""M70 58V70L78 76"" stroke=""#c8d2d6"" stroke-width=""4"" fill=""none"" stroke-linecap=""round""/>",
        // 绷带病毒减量冷却：绷带 + 小病毒
        ["status_bandage_virus_cd"] = @"<rect x=""8"" y=""34"" width=""60"" height=""24"" rx=""10"" transform=""rotate(-30 38 46)"" fill=""#efe3c4"" stroke=""#8a7a55"" stroke-width=""4""/><circle cx=""70"" cy=""70"" r=""16"" fill=""#a6c45a"" stroke=""#5a7a2a"" stroke-width=""4""/><path d=""M70 48V54M70 86V92M48 70H54M86 70H92"" stroke=""#5a7a2a"" stroke-width=""4"" stroke-linecap=""round""/>",
        // 归零：三角警告
        ["status_zero"] = @"<path d=""M50 8L94 88H6Z"" fill=""#d38e7b"" stroke=""#7a2e1f"" stroke-width=""5"" stroke-linejoin=""round""/><path d=""M50 34V60"" stroke=""#2a120c"" stroke-width=""9"" stroke-linecap=""round""/><circle cx=""50"" cy=""74"" r=""5"" fill=""#2a120c""/>",
        // 体力偏低：电量低
        ["status_low_energy"] = @"<rect x=""10"" y=""28"" width=""70"" height=""44"" rx=""6"" fill=""#2f3a2f"" stroke=""#c8c2a8"" stroke-width=""5""/><rect x=""80"" y=""40"" width=""10"" height=""20"" rx=""3"" fill=""#c8c2a8""/><rect x=""18"" y=""36"" width=""16"" height=""28"" fill=""#e4b374""/>",
        // 精神低落：低垂的脸
        ["status_low_morale"] = @"<circle cx=""50"" cy=""50"" r=""38"" fill=""#c9bf9f"" stroke=""#6f6648"" stroke-width=""5""/><path d=""M32 42L42 46M68 42L58 46"" stroke=""#3c3a30"" stroke-width=""5"" stroke-linecap=""round""/><path d=""M34 70Q50 58 66 70"" stroke=""#3c3a30"" stroke-width=""5"" fill=""none"" stroke-linecap=""round""/>",
        // 战斗恐慌：心跳折线
        ["status_panic"] = @"<path d=""M50 86Q14 62 12 38 12 18 30 16 44 16 50 30 56 16 70 16 88 18 88 38 86 62 50 86Z"" fill=""#d38e7b"" stroke=""#7a2e1f"" stroke-width=""4""/><path d=""M18 50H36L42 36 52 64 58 46 64 50H82"" fill=""none"" stroke=""#2a120c"" stroke-width=""5"" stroke-linejoin=""round"" stroke-linecap=""round""/>",
        // 统一状态占位：圆框问号
        ["status_unknown"] = @"<circle cx=""50"" cy=""50"" r=""38"" fill=""#c9bf9f"" stroke=""#8a8466"" stroke-width=""5"" stroke-dasharray=""8 6""/><path d=""M38 38Q38 26 50 26 62 26 62 38 62 46 52 50 50 52 50 60"" fill=""none"" stroke=""#4a4636"" stroke-width=""7"" stroke-linecap=""round""/><circle cx=""50"" cy=""72"" r=""5"" fill=""#4a4636""/>",
    };
}
