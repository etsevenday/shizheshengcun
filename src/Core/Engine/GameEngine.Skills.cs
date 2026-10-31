using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 十级熟练度（A06 §1/§4、R01 §1/§2；开发计划 v0.9 B5，取代 v0.6 §03“8 项无封顶”）：15 门各自经验，升级门槛 50×(L+1) 逐级扣，
    /// 最高 10 级（1→10 累计 2700），Lv10 停止获取该门经验（溢出不兑换任何东西）。熟练度属于角色（ActorState.Skills）。
    /// 能力公式本身仍按等级计算；状态里的等级超过上限时（只可能来自测试注入）按上限取用。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>做法知识（Q64）：存档里直接记下 knowledge:id，或有它的授予事实（护拳绑带=营区登记 hub_registered）。</summary>
        public bool KnowsKnowledge(GameState s, string knowledgeId)
        {
            if (s.Progress.Flags.Contains("knowledge:" + knowledgeId)) return true;
            var k = Config.Knowledge.FirstOrDefault(x => x.Id == knowledgeId);
            return k != null && k.GrantedByFacts.Any(s.Progress.Flags.Contains);
        }

        /// <summary>配方需要的做法知识还没有时的原因（制作列表不列出这类配方；直接请求时拒绝）；不需要或已知为 null。</summary>
        public string? RecipeUnknownReason(GameState s, RecipeConfig rc)
        {
            if (rc.Knowledge == null || KnowsKnowledge(s, rc.Knowledge)) return null;
            var k = Config.Knowledge.FirstOrDefault(x => x.Id == rc.Knowledge);
            return $"还不知道{k?.Name ?? rc.Name + "的做法"}（{k?.Source ?? "后续解锁"}）";
        }

        public bool RecipeKnown(GameState s, RecipeConfig rc) => RecipeUnknownReason(s, rc) == null;

        public const string Crafting = "crafting";
        public const string Medicine = "medicine";
        public const string Gathering = "gathering";
        public const string Scavenging = "scavenging";
        public const string Cooking = "cooking";
        public const string Athletics = "athletics";

        public SkillConfig? SkillCfg(string id) => Config.Skills.FirstOrDefault(x => x.Id == id);

        public string SkillName(string id) => SkillCfg(id)?.Name ?? id;

        /// <summary>该门等级上限：技能自身 levelCap，否则 skillGrowth.skillLevelCap，都没有则不封顶。</summary>
        public int SkillCap(string id) => SkillCfg(id)?.LevelCap ?? Config.SkillGrowth.SkillLevelCap ?? int.MaxValue;

        public int SkillLevel(GameState s, string id) => SkillLevel(s.Player, id);

        public int SkillLevel(ActorState actor, string id) =>
            Math.Min(SkillCap(id), actor.Skills.TryGetValue(id, out var sk) ? sk.Level : SkillCfg(id)?.InitialLevel ?? 1);

        /// <summary>该门已到上限（Lv10），不再获得经验。</summary>
        public bool SkillMaxed(GameState s, string id) => SkillLevel(s, id) >= SkillCap(id);

        /// <summary>从 L 升到 L+1 所需经验：50×(L+1)。</summary>
        public long NextLevelXp(string id, int level)
        {
            var c = SkillCfg(id);
            var b = c?.XpBase ?? 50;
            var o = c?.XpOffset ?? 1;
            return checked((long)Math.Round(b * (level + o)));
        }

        /// <summary>从初始等级累计到上限所需的总经验（50×(2+…+10)=2700）。</summary>
        public long TotalXpToCap(string id)
        {
            long total = 0;
            var cap = SkillCap(id);
            for (var l = SkillCfg(id)?.InitialLevel ?? 1; l < cap && cap != int.MaxValue; l++) total = checked(total + NextLevelXp(id, l));
            return total;
        }

        /// <summary>新局与旧档迁移：缺的技能补初始等级、经验 0（已有的不动）。</summary>
        public void InitSkills(GameState s)
        {
            foreach (var a in s.Actors.Values) InitSkills(a);
        }

        public void InitSkills(ActorState actor)
        {
            foreach (var sk in Config.Skills)
                if (!actor.Skills.ContainsKey(sk.Id)) actor.Skills[sk.Id] = new SkillState { Level = sk.InitialLevel, Xp = 0 };
        }

        /// <summary>获得经验并逐级升级（扣当前门槛后继续判断多级提升）。返回升了几级。主角。</summary>
        public int GainXp(GameState s, string id, long amount) => GainXp(s, s.Player, id, amount);

        /// <summary>
        /// 某角色获得经验：Lv10（上限）时不再累计，返回 0；升到上限时剩余经验清零（A06 §1“Lv10 停止获取该门经验，不将溢出经验兑换局外点数”）。
        /// 加减一律 checked，溢出报错不静默回零。
        /// </summary>
        public int GainXp(GameState s, ActorState actor, string id, long amount)
        {
            if (amount <= 0) return 0;
            if (!actor.Skills.TryGetValue(id, out var sk))
            {
                sk = new SkillState { Level = SkillCfg(id)?.InitialLevel ?? 1 };
                actor.Skills[id] = sk;
            }
            var cap = SkillCap(id);
            if (sk.Level >= cap) return 0;
            sk.Xp = checked(sk.Xp + amount);
            var ups = 0;
            while (sk.Level < cap)
            {
                var need = NextLevelXp(id, sk.Level);
                if (sk.Xp < need) break;
                sk.Xp = checked(sk.Xp - need);
                sk.Level = checked(sk.Level + 1);
                ups++;
            }
            if (sk.Level >= cap) sk.Xp = 0;
            if (ups > 0)
                Emit(s, GameEventKind.Info, sk.Level >= cap
                    ? $"{SkillName(id)}熟练度升到{sk.Level}级（满级，此后不再获得{SkillName(id)}经验）。"
                    : $"{SkillName(id)}熟练度升到{sk.Level}级。");
            return ups;
        }

        /// <summary>给玩家看的经验文字：“制造经验+3”；已满级写“制造已满10级，不再获得经验”。</summary>
        public string XpText(GameState s, string id, long amount) =>
            SkillMaxed(s, id) ? $"{SkillName(id)}已满{SkillCap(id)}级，不再获得经验" : $"{SkillName(id)}经验+{amount}";

        // ───────── 能力公式（系数读配置里的公式字符串） ─────────

        /// <summary>对应武器伤害倍率 1+0.035L。</summary>
        public double SkillDamageFactor(int level) => Config.SkillGrowth.DamageBase + Config.SkillGrowth.DamagePerLevel * level;

        /// <summary>命中能力值的等级加成 3L（加在武器基础命中能力上）。</summary>
        public double SkillAccuracyBonus(int level) => Config.SkillGrowth.AccuracyPerLevel * level;

        /// <summary>体力成本 基础/(1+0.025L)：持续减少且为正，不取整。</summary>
        public double SkillEnergyCost(double baseCost, int level) =>
            baseCost / (Config.SkillGrowth.EnergyBase + Config.SkillGrowth.EnergyPerLevel * level);

        /// <summary>制造经验 clamp(ceil(原始基础分钟/10),1,12)。</summary>
        public long CraftXp(int originalBaseMinutes)
        {
            var x = Config.Xp;
            return (long)Math.Min(x.CraftMax, Math.Max(x.CraftMin, Math.Ceiling(originalBaseMinutes / x.CraftDivisor)));
        }

        /// <summary>绷带健康恢复 1×(1+0.02×医疗L)（未扣已满部分）。</summary>
        public double MedicalHeal(double baseHeal, int medicineLevel) =>
            baseHeal * (Config.SkillGrowth.MedicalHealOne + Config.SkillGrowth.MedicalHealPerLevel * medicineLevel);

        /// <summary>人物页技能区：15 门的名称、类别、等级/上限、当前经验与下级门槛、已获得的专属节点。</summary>
        public List<SkillView> SkillViews(GameState s) => SkillViews(s.Player);

        public List<SkillView> SkillViews(ActorState actor) => Config.Skills.Select(c =>
        {
            var lv = SkillLevel(actor, c.Id);
            var cap = SkillCap(c.Id);
            return new SkillView
            {
                Id = c.Id, Name = c.Name, Category = c.Category, Level = lv, Cap = cap,
                Xp = lv >= cap ? 0 : actor.Skills.TryGetValue(c.Id, out var sk) ? sk.Xp : 0,
                NextXp = lv >= cap ? 0 : NextLevelXp(c.Id, lv),
                Nodes = Config.SkillNodes.Where(n => n.SkillId == c.Id).OrderBy(n => n.Level).ToList(),
            };
        }).ToList();
    }

    public sealed class SkillView
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "combat";
        public int Level { get; set; }
        public int Cap { get; set; }
        public long Xp { get; set; }
        /// <summary>升到下一级的门槛；满级为 0。</summary>
        public long NextXp { get; set; }
        public bool Maxed => Level >= Cap;
        /// <summary>该门 5 个专属节点（按等级）。</summary>
        public List<SkillNodeConfig> Nodes { get; set; } = new List<SkillNodeConfig>();
    }
}
