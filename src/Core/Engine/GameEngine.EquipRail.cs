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

        /// <summary>木盾固定特性名（S14 Q608 木盾例）：盾类格挡是真实已接入的机制（普通防御，无技能前提）。</summary>
        private const string ShieldFaceTrait = "盾面：";

        /// <summary>S14 Q638（逐字稿「未穿戴但已实装且具备技能资格的特性组」）：未穿戴时的组名。</summary>
        public const string HeldTraitsLabel = "具备特性";

        /// <summary>S14 Q638：未穿戴时组内的必要说明（不写“生效中”）。</summary>
        public const string WornTriggerHint = "穿戴后可触发";

        /// <summary>首屏特性组名：穿戴中为「生效效果」（Q608），未穿戴为「具备特性」（Q638）。</summary>
        public string EquipTraitGroupLabel(ItemInstance it) => it.IsWorn ? ActiveEffectsLabel : HeldTraitsLabel;

        /// <summary>
        /// 这件装备当前进首屏特性组的固定特性（按原配置顺序、每条一次）：资格见 <see cref="WeaponTraitQualify"/>（Q638：机制已实装，且有绑定技能时实际持有人／穿戴者已解锁）。
        /// 木盾写「盾面：格挡时减伤{(1−盾类格挡系数)×100}%。」（取实际配置，表示满足格挡时的真实能力），其余沿原特性句（已含触发条件）。
        /// 耐久为 0 时按原规则效果暂停，不列（原「状态」行说明暂停）。只读，不加任何人物属性。
        /// </summary>
        public List<string> EquipActiveTraitLines(GameState s, ItemInstance it)
        {
            var list = new List<string>();
            if (Gear(it.ItemId) is GearConfig g)
            {
                // BM7b（S14 Q650）：非武器固定效果逐项——已有真实消费者的分项进同一特性组；耐久为 0 暂停时不列（原「状态」行说明暂停）
                if (it.Durability.HasValue && it.Durability.Value <= 1e-9) return list;
                list.AddRange(GearEffectClauses(g).Where(c => c.Live).Select(c => c.Text));
                return list;
            }
            if (Weapon(it.ItemId) is not WeaponConfig w || w.TraitText == null) return list;
            if (it.Durability.HasValue && it.Durability.Value <= 1e-9) return list;
            if (WeaponTraitQualify(s, it, w) != TraitQualify.Qualified) return list;
            list.Add(IsShieldFace(w) ? $"{ShieldFaceTrait}格挡时减伤{Fmt.T((1 - Config.CombatMath.ShieldGuardFactor) * 100)}%。" : w.TraitText);
            return list;
        }

        private static bool IsShieldFace(WeaponConfig w) => w.WeaponKind == "shield" && w.TraitText != null && w.TraitText.StartsWith(ShieldFaceTrait);

        /// <summary>Q638 固定特性资格：已具备／已实装但绑定技能未解锁／未实装。</summary>
        public enum TraitQualify { Qualified, SkillLocked, NotImplemented }

        /// <summary>
        /// Q638：这件物品的真实持有人——穿戴取穿戴者，随身取其持有人（锅等容器按容器所在逐层上溯）；地面、屋柜、设施、遗物等无人物持有人一律按主角「你」。
        /// 不用当前右栏人物页或查看者；持有人已不在名册时按主角。
        /// </summary>
        public string TraitHolderId(GameState s, ItemInstance it)
        {
            var cur = it;
            for (var depth = 0; depth < 8; depth++)
            {
                if (ItemInstance.TryEquip(cur.Owner, out var wearer, out _)) return Known(wearer);
                if (cur.IsCarried) return GameState.PlayerId;
                if (cur.Owner.StartsWith(ItemInstance.Carried + ":")) return Known(cur.Owner.Substring(ItemInstance.Carried.Length + 1));
                if (cur.InContainer && int.TryParse(cur.Owner.Substring(ItemInstance.ContainerPrefix.Length), out var cid)
                    && s.Items.FirstOrDefault(i => i.InstanceId == cid) is ItemInstance box) { cur = box; continue; }
                break;
            }
            return GameState.PlayerId;
            string Known(string id) => s.Actors.ContainsKey(id) ? id : GameState.PlayerId;
        }

        /// <summary>
        /// Q638：固定特性资格，同源读取现有特性、技能解锁与效果查询，不新加门槛。
        /// 机制已实装（<see cref="TraitTrigger"/> 为 true）且——无绑定技能前提直接具备；有绑定技能时，实际持有人／穿戴者已解锁本武器门下任一触发该特性的技能节点即具备。
        /// 动作当刻的 AP、弹药、目标或冷却不足不影响资格（只看解锁）。
        /// </summary>
        public TraitQualify WeaponTraitQualify(GameState s, ItemInstance it, WeaponConfig w)
        {
            if (!TraitTrigger(w, out var pred)) return TraitQualify.NotImplemented;
            if (pred == null) return TraitQualify.Qualified;
            var nodes = Config.SkillNodes.Where(n => n.SkillId == w.SkillId && _exec.TryGetValue(n.Id, out var x) && pred(x)).ToList();
            if (nodes.Count == 0) return TraitQualify.NotImplemented;
            var id = TraitHolderId(s, it);
            var actor = s.Actors.TryGetValue(id, out var a) ? a : new ActorState { Id = id };
            return nodes.Any(n => SkillLevel(actor, n.SkillId) >= n.Level) ? TraitQualify.Qualified : TraitQualify.SkillLocked;
        }

        /// <summary>
        /// 特性机制是否已实装，及其绑定技能（pred 为 null 表示无技能前提）。与实际结算读取处同源：
        /// bleedAccuracy／bleedTriggers（流血技能）、knockbackAccuracy（逼退类）、suppressAccuracy（枪械削弱）、staggerUntilAttack（失衡）、
        /// armorBreakBonus（破甲）、interruptSlow（打断）、counterDamage（反击架势）、braceExtra／braceDamage（架矛）、commandRounds（鼓舞／总攻令）、
        /// firstCommandAccuracy（非保护、非整队的指挥技能）、medicalHealBonus（回血医疗）、criticalCareBonus（危急救护）、postMedicalDamage（医疗）；
        /// markedAccuracy／markedDamage／animalDamage／protectorDamage（任一直接攻击）、displaceResist（穿戴）与忽略护甲、盾类格挡（普通防御）、
        /// 锁闭资源点条件工具倍率（Q137 破门）、指挥／医疗器具开放的技能页（号令、医疗）、配置站位（长柄、短枪）无技能前提；
        /// 霰弹散布绑定 areaWeapons 含本武器的技能。其余（如 reloadMax 快装，及轻巧、撬动、无声、朴素、包铁、护送、集合）尚无结算读取，按未实装。
        /// </summary>
        private bool TraitTrigger(WeaponConfig w, out System.Func<SkillExecConfig, bool>? pred)
        {
            pred = null;
            static bool Has(SkillExecConfig x, string st) => x.Effects.Any(e => e.Status == st);
            if (w.TraitEffects.Count > 0)
            {
                var preds = new List<System.Func<SkillExecConfig, bool>>();
                foreach (var key in w.TraitEffects.Keys)
                {
                    switch (key)
                    {
                        case "bleedAccuracy": case "bleedTriggers": preds.Add(x => Has(x, "bleed")); break;
                        case "knockbackAccuracy": preds.Add(x => Has(x, "knockback")); break;
                        case "suppressAccuracy": if (w.SkillId != "firearms") return false; preds.Add(x => Has(x, "weak")); break;
                        case "staggerUntilAttack": preds.Add(x => Has(x, "stagger")); break;
                        case "armorBreakBonus": preds.Add(x => Has(x, "armor_break")); break;
                        case "interruptSlow": preds.Add(x => Has(x, "cancel_charge")); break;
                        case "counterDamage": preds.Add(x => x.CounterFactor > 0); break;
                        case "braceExtra": case "braceDamage": preds.Add(x => x.BraceCharges > 0); break;
                        case "commandRounds": preds.Add(x => x.InspireAccuracy > 0 || x.AllOutFactor > 0); break;
                        case "firstCommandAccuracy": preds.Add(x => x.Type == "ally" && !x.Protect && !x.Reorder); break;
                        case "medicalHealBonus": preds.Add(x => x.Type == "medical" && x.Heal > 0); break;
                        case "criticalCareBonus": preds.Add(x => x.Type == "medical" && x.Heal > 0 && x.HealMaxRatio.HasValue); break;
                        case "postMedicalDamage": preds.Add(x => x.Type == "medical"); break;
                        case "markedAccuracy": case "markedDamage": case "animalDamage": case "protectorDamage": case "displaceResist": break;
                        default: return false;
                    }
                }
                if (preds.Count > 0) pred = x => preds.All(p => p(x));
                return true;
            }
            if (w.ArmorIgnore > 0 || IsShieldFace(w) || IsConditionalTool(w.Id) || !w.CanAttack || PositionTraitWeapons.Contains(w.Id)) return true;
            if (_exec.Values.Any(x => x.AreaWeapons.Contains(w.Id))) { pred = x => x.AreaWeapons.Contains(w.Id); return true; }
            return false;
        }

        /// <summary>固定特性就是配置站位本身（长柄：基础射程123＝targetPositions；短枪：允许我方2号位＝ownPositions），已按真实站位结算。</summary>
        private static readonly HashSet<string> PositionTraitWeapons = new HashSet<string> { "wooden_spear", "pistol" };

        /// <summary>
        /// 右栏用的装备行：<see cref="EquipDetailLines"/> 原行里，已逐字进首屏特性组的固定特性行去掉（每条一次）；摘要改写过的（木盾）在「说明」留原特性句与原条件（如「不被动增加护甲」）。
        /// Q638：已实装但绑定技能未解锁、或耐久为 0 暂停的特性在「说明」留原特性句（完整条件、数值），不再笼统写「（相关技能或多人战斗上线后生效）」；尚未实装的仍原行。
        /// </summary>
        public List<(string Label, string Value)> RailEquipLines(GameState s, ItemInstance it, out List<string> active)
        {
            var lines = EquipDetailLines(s, it);
            active = EquipActiveTraitLines(s, it);
            if (Gear(it.ItemId) is GearConfig g)
            {
                // BM7b（S14 Q650）：已进特性组的分项不再在「固定效果」行重复；暂停的分项留原句，确实没有消费者的分项才写旧「上线后生效」
                var shown = active;
                var rest = GearEffectClauses(g).Where(c => !shown.Contains(c.Text)).Select(c => c.Live ? c.Text : c.Text + GearNotLiveNote).ToList();
                return lines.SelectMany(x => x.Label != "固定效果" ? new[] { x } : rest.Count == 0 ? new (string, string)[0] : new[] { (x.Label, string.Join("；", rest)) }).ToList();
            }
            if (Weapon(it.ItemId) is not WeaponConfig w || w.TraitText == null) return lines;
            if (WeaponTraitQualify(s, it, w) == TraitQualify.NotImplemented) return lines;
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

        /// <summary>原「固定效果」行里确实未接入部分的注记（沿原句，只用于没有真实消费者的分项）。</summary>
        public const string GearNotLiveNote = "（相关技能上线后生效，当前只显示）";

        /// <summary>
        /// BM7b（S14 Q650）：有真实结算消费者的装备效果键。只认实际读取处（战斗合计、求生乘数／加值、修理、包扎、食用），不看 effects 列表非空或旧 <see cref="EffectLive"/>：
        /// combat.displaceResist 已由位移抵抗判定（<see cref="PlayerControlResist"/>）读取；work.purificationTablet 未找到工作量读取，不在此列。
        /// </summary>
        private static readonly HashSet<string> GearConsumedKeys = new HashSet<string>
        {
            "combat.armor", "combat.speed", "combat.accuracy", "combat.crit", "combat.displaceResist",
            "work.craft", "work.scavenge", "work.move", "work.route",
            "rate.gather.energy", "rate.move.energy", "rate.scavengeClear.energy", "rate.nonCombat.satiety", "rate.nonCombat.hydration",
            "hazard.health", "hazard.woodScrapChance", "pos.shelteredRest.energy", "pos.sleep.energy",
            "repair.fraction", "bandage.health", "eat.satiety",
        };

        /// <summary>原效果句分项里认结构化键的词（同一分项含其中一个词、且本件确有该键才算这一键的分项）。</summary>
        private static readonly (string Word, string Prefix)[] GearClauseWords =
        {
            ("护甲", "combat.armor"), ("速度", "combat.speed"), ("命中能力", "combat.accuracy"), ("暴击能力", "combat.crit"), ("位移抵抗", "combat.displaceResist"),
            ("工作量", "work."), ("小时率", "rate."), ("划伤", "hazard."), ("恢复额外", "pos."), ("维护", "repair."), ("包扎", "bandage."), ("食用", "eat."),
        };

        /// <summary>
        /// BM7b（S14 Q650）：非武器装备原效果句（<see cref="GearConfig.EffectText"/>）按「；」分项，以「不」起头的限定句（如「不改即时效果」「不增物资卡」）接回前一项；
        /// 每项按本件的结构化效果键（或护拳绑带的 <see cref="GearConfig.UnarmedBasicTargets"/>，普通拳击查询读取）判是否有真实消费者。
        /// 写明为 0 的数值项（如「速度0」）是原配置的真实数值，随本件一同生效。分项文字逐字取原句，不改数值、不新编。
        /// </summary>
        public List<(string Text, bool Live)> GearEffectClauses(GearConfig g)
        {
            var parts = new List<string>();
            foreach (var raw in (g.EffectText ?? "").Split('；'))
            {
                var t = raw.Trim();
                if (t.Length == 0) continue;
                if (parts.Count > 0 && t.StartsWith("不")) parts[parts.Count - 1] += "；" + t;
                else parts.Add(t);
            }
            var keys = g.Effects.Select(e => e.Key).ToList();
            var list = new List<(string, bool)>();
            foreach (var p in parts)
            {
                var head = p.Split('；')[0];
                bool live;
                if (g.UnarmedBasicTargets != null && head.Contains("拳击")) live = true;
                else if (System.Text.RegularExpressions.Regex.IsMatch(head, "^(速度|护甲)0$")) live = keys.Count > 0 && keys.All(GearConsumedKeys.Contains);
                else
                {
                    var hit = GearClauseWords.Where(cw => head.Contains(cw.Word)).Select(cw => cw.Prefix)
                        .SelectMany(pre => keys.Where(k => k == pre || (pre.EndsWith(".") && k.StartsWith(pre)))).ToList();
                    live = hit.Count > 0 && hit.All(GearConsumedKeys.Contains);
                }
                list.Add((p, live));
            }
            return list;
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
