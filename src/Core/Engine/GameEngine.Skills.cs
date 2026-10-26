using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 无等级封顶的熟练度（v0.6 §03）：8 项技能各自经验，升级门槛 50×(L+1) 逐级扣；能力收益随等级持续增长，
    /// 不设 10 级/95% 之类的硬上限，只有概率≤1、成本为正、恢复不超过上限这类结果范围。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string Crafting = "crafting";
        public const string Medicine = "medicine";

        public SkillConfig? SkillCfg(string id) => Config.Skills.FirstOrDefault(x => x.Id == id);

        public string SkillName(string id) => SkillCfg(id)?.Name ?? id;

        public int SkillLevel(GameState s, string id) =>
            s.Skills.TryGetValue(id, out var sk) ? sk.Level : SkillCfg(id)?.InitialLevel ?? 1;

        /// <summary>从 L 升到 L+1 所需经验：50×(L+1)。</summary>
        public long NextLevelXp(string id, int level)
        {
            var c = SkillCfg(id);
            var b = c?.XpBase ?? 50;
            var o = c?.XpOffset ?? 1;
            return checked((long)Math.Round(b * (level + o)));
        }

        /// <summary>新局与旧档迁移：全部技能初始等级、经验 0。</summary>
        public void InitSkills(GameState s)
        {
            foreach (var sk in Config.Skills)
                if (!s.Skills.ContainsKey(sk.Id)) s.Skills[sk.Id] = new SkillState { Level = sk.InitialLevel, Xp = 0 };
        }

        /// <summary>获得经验并逐级升级（扣当前门槛后继续判断多级提升）。返回升了几级。</summary>
        public int GainXp(GameState s, string id, long amount)
        {
            if (amount <= 0) return 0;
            if (!s.Skills.TryGetValue(id, out var sk))
            {
                sk = new SkillState { Level = SkillCfg(id)?.InitialLevel ?? 1 };
                s.Skills[id] = sk;
            }
            sk.Xp = checked(sk.Xp + amount);
            var ups = 0;
            while (true)
            {
                var need = NextLevelXp(id, sk.Level);
                if (sk.Xp < need) break;
                sk.Xp = checked(sk.Xp - need);
                sk.Level = checked(sk.Level + 1);
                ups++;
            }
            if (ups > 0) Emit(s, GameEventKind.Info, $"{SkillName(id)}熟练度升到{sk.Level}级。");
            return ups;
        }

        // ───────── 能力公式（系数读配置里的公式字符串） ─────────

        /// <summary>对应武器伤害倍率 1+0.035L。</summary>
        public double SkillDamageFactor(int level) => Config.SkillGrowth.DamageBase + Config.SkillGrowth.DamagePerLevel * level;

        /// <summary>命中能力值的等级加成 3L（加在武器基础命中能力上）。</summary>
        public double SkillAccuracyBonus(int level) => Config.SkillGrowth.AccuracyPerLevel * level;

        /// <summary>体力成本 基础/(1+0.025L)：持续减少且为正，不取整。</summary>
        public double SkillEnergyCost(double baseCost, int level) =>
            baseCost / (Config.SkillGrowth.EnergyBase + Config.SkillGrowth.EnergyPerLevel * level);

        /// <summary>制造工作量倍率 1/(1+0.025L)。设施被动受热与处理尸体不吃。</summary>
        public double CraftWorkFactor(int level) => 1 / (1 + Config.Crafting.PerLevel * level);

        public double CraftWorkFactor(GameState s) => CraftWorkFactor(SkillLevel(s, Crafting));

        /// <summary>制造经验 clamp(ceil(原始基础分钟/10),1,12)。</summary>
        public long CraftXp(int originalBaseMinutes)
        {
            var x = Config.Xp;
            return (long)Math.Min(x.CraftMax, Math.Max(x.CraftMin, Math.Ceiling(originalBaseMinutes / x.CraftDivisor)));
        }

        /// <summary>绷带健康恢复 1×(1+0.02×医疗L)（未扣已满部分）。</summary>
        public double MedicalHeal(double baseHeal, int medicineLevel) =>
            baseHeal * (Config.SkillGrowth.MedicalHealOne + Config.SkillGrowth.MedicalHealPerLevel * medicineLevel);

        /// <summary>人物页技能区：名称、等级、当前经验与下级门槛。</summary>
        public List<SkillView> SkillViews(GameState s) => Config.Skills.Select(c =>
        {
            var lv = SkillLevel(s, c.Id);
            return new SkillView
            {
                Id = c.Id, Name = c.Name, Level = lv,
                Xp = s.Skills.TryGetValue(c.Id, out var sk) ? sk.Xp : 0,
                NextXp = NextLevelXp(c.Id, lv),
            };
        }).ToList();
    }

    public sealed class SkillView
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public long Xp { get; set; }
        public long NextXp { get; set; }
    }
}
