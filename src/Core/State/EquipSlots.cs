using System.Collections.Generic;

namespace Survival.Core.State
{
    /// <summary>
    /// 每角色七个穿戴槽（S06 2026-10-30 全系统更新目标 §1、R02 §1、A05 §5）：武器×2、防具、战斗饰品、上衣、下衣、求生饰品。
    /// 穿戴物的 owner 为 "equip:&lt;actorId&gt;:&lt;槽ID&gt;"（ItemInstance.EquipOwner），不占五张物资卡，重量计入本人。
    /// 本批（开发计划 v0.9 B3）只有武器能穿进 weapon1/weapon2，其余五槽是空结构。界面名取 R02 总案写法。
    /// </summary>
    public static class EquipSlots
    {
        public const string Weapon1 = "weapon1";
        public const string Weapon2 = "weapon2";
        public const string Armor = "armor";
        public const string CombatAccessory = "combat_acc";
        public const string Top = "top";
        public const string Bottom = "bottom";
        public const string SurvivalAccessory = "survival_acc";

        /// <summary>七槽，按界面顺序。</summary>
        public static readonly string[] All = { Weapon1, Weapon2, Armor, CombatAccessory, Top, Bottom, SurvivalAccessory };

        /// <summary>两个武器槽（战斗页 1、2）。</summary>
        public static readonly string[] Weapons = { Weapon1, Weapon2 };

        public static bool IsWeaponSlot(string slot) => slot == Weapon1 || slot == Weapon2;

        public static bool IsValid(string slot) => System.Array.IndexOf(All, slot) >= 0;

        /// <summary>武器页号 1/2 对应的槽；其他值为 null。</summary>
        public static string? WeaponSlotOfPage(int page) => page == 1 ? Weapon1 : page == 2 ? Weapon2 : null;

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            [Weapon1] = "武器Ⅰ",
            [Weapon2] = "武器Ⅱ",
            [Armor] = "防具",
            [CombatAccessory] = "战斗饰品",
            [Top] = "上衣",
            [Bottom] = "下衣",
            [SurvivalAccessory] = "求生饰品",
        };

        public static string Name(string slot) => Names.TryGetValue(slot, out var n) ? n : slot;
    }

    /// <summary>
    /// 武器类型 kind（A05 §1“类”列；“同类型武器限制（用户跨会话补充，2026-10-30）”）：每角色两武器槽同 kind 最多 1 件。
    /// </summary>
    public static class WeaponKinds
    {
        public static readonly string[] All = { "blade", "axe", "spear", "blunt", "archery", "firearms", "shield", "command", "medical" };

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["blade"] = "刀剑",
            ["axe"] = "斧",
            ["spear"] = "矛",
            ["blunt"] = "钝器",
            ["archery"] = "弓弩",
            ["firearms"] = "枪械",
            ["shield"] = "盾",
            ["command"] = "指挥",
            ["medical"] = "医疗",
        };

        public static bool IsValid(string? kind) => kind != null && System.Array.IndexOf(All, kind) >= 0;

        public static string Name(string? kind) => kind != null && Names.TryGetValue(kind, out var n) ? n : kind ?? "";
    }
}
