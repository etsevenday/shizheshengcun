using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// BM5b1「装备数据与已生效固定特性紧凑对齐」（S14「2026-11-14 BM4b Q606—Q614完整答复」Q608）：常态全局右栏（对象详情、整备只读详情）的装备首屏。
    /// 已具备资格的固定特性在主要数值之后的「生效效果」摘要里列一次；说明组首行（物品原描述）与类型、固定特性／固定效果完全同义时只留一份。
    /// 整备比较等一级面板内页不走这里（Q609），仍用 <see cref="EquipDetailLines"/> 原行。只读。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>S14 Q608：首屏摘要的标签。</summary>
        public const string ActiveEffectsLabel = "生效效果";

        /// <summary>木盾固定特性名（S14 Q608 木盾例）：盾类格挡是真实已接入的机制。</summary>
        private const string ShieldFaceTrait = "盾面：";

        /// <summary>
        /// 这件装备当前进首屏「生效效果」的固定特性（按原配置顺序、每条一次）：木盾「盾面：格挡时减伤{(1−盾类格挡系数)×100}%。」（取实际配置，表示满足格挡时的真实能力），
        /// 以及原已标「生效中」的忽略护甲类特性（原特性句）。其余固定特性资格待 Q638，仍在「说明」组原句。耐久为 0 时按原规则效果暂停，不列。
        /// </summary>
        public List<string> EquipActiveTraitLines(ItemInstance it)
        {
            var list = new List<string>();
            if (Weapon(it.ItemId) is not WeaponConfig w || w.TraitText == null) return list;
            if (it.Durability.HasValue && it.Durability.Value <= 1e-9) return list;
            if (w.WeaponKind == "shield" && w.TraitText.StartsWith(ShieldFaceTrait))
                list.Add($"{ShieldFaceTrait}格挡时减伤{Fmt.T((1 - Config.CombatMath.ShieldGuardFactor) * 100)}%。");
            else if (w.ArmorIgnore > 0)
                list.Add(w.TraitText);
            return list;
        }

        /// <summary>
        /// 右栏用的装备行：<see cref="EquipDetailLines"/> 原行里，已逐字进首屏摘要的固定特性行去掉（每条一次）；摘要改写过的（木盾）在「说明」留原特性句，
        /// 去掉与首屏摘要矛盾的「（相关技能或多人战斗上线后生效）」注记，原条件（如「不被动增加护甲」）保留。
        /// </summary>
        public List<(string Label, string Value)> RailEquipLines(GameState s, ItemInstance it, out List<string> active)
        {
            var lines = EquipDetailLines(s, it);
            active = EquipActiveTraitLines(it);
            if (active.Count == 0 || Weapon(it.ItemId) is not WeaponConfig w || w.TraitText == null) return lines;
            var trait = w.TraitText;
            var verbatim = active.Contains(trait);
            var outList = new List<(string, string)>();
            foreach (var (label, value) in lines)
            {
                if (label == "固定特性")
                {
                    if (!verbatim) outList.Add((label, trait));
                    continue;
                }
                outList.Add((label, value));
            }
            return outList;
        }

        /// <summary>
        /// 右栏「说明」组首行的原物品描述去重（S14 Q608「固定特性原说明首行与固定特性行完全同义时只显示一份」）：
        /// 原描述按「{类型}；[固定特性：]{特性／效果}」写成、且两段都已在别的行（类型行、固定特性／固定效果行）完整出现时不再重复；
        /// 有任何独有内容（条件、例外、数值）时原样保留。不是装备或没有描述为原值。
        /// </summary>
        public string? RailEquipDesc(ItemInstance it, string? desc)
        {
            if (string.IsNullOrEmpty(desc)) return desc;
            string? kind = null, fixedText = null;
            if (Weapon(it.ItemId) is WeaponConfig w) { kind = $"{WeaponKinds.Name(w.WeaponKind)}·T{w.Tier}"; fixedText = w.TraitText; }
            else if (Gear(it.ItemId) is GearConfig g) { kind = $"{EquipSlots.Name(g.Slot)}·T{g.Tier}"; fixedText = g.EffectText; }
            if (kind == null || string.IsNullOrEmpty(fixedText)) return desc;
            var cut = desc!.IndexOf('；');
            if (cut < 0) return desc;
            var head = desc.Substring(0, cut);
            var tail = desc.Substring(cut + 1);
            const string tag = "固定特性：";
            if (tail.StartsWith(tag)) tail = tail.Substring(tag.Length);
            if (head != kind) return desc;
            if (tail == fixedText) return null;
            // 原描述里的特性句是特性行去掉末尾补充后的前段（如消防斧「（仅实际选为工具）」→ 特性行「（仅实际选为工具，名单见Q137）」）：内容已全在特性行
            var open = tail.EndsWith("）") ? tail.Substring(0, tail.Length - 1) : tail;
            return open.Length > 0 && fixedText!.StartsWith(open) ? null : desc;
        }
    }
}
