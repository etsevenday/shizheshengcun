using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 三种战术品（开发计划 v0.9 批次 B22，需求 R70；R03「战术物合法性与效果引用」、A09「E7 制作战术消耗品」、A07 X1.13；config dungeonRewards.tactical）：
    /// <list type="bullet">
    /// <item>flash_marker 闪光标记器：2AP、体力 0.1，敌方任一存活单体（1—4 号位都行）；标记 2 大回合，全队对它的命中能力+6，与猎标同名取强、不叠层；
    /// 无伤害、不判命中/暴击/咬伤、不眩晕；噪声+5。目标已有同等或更强、更长的标记时拒绝（不消耗）。</item>
    /// <item>brace_pack 支撑护带包：1AP，自身位移抵抗+25 个百分点 2 大回合，与稳固同类取强；不格挡、不免伤。已有同等或更强、更长的稳固时拒绝。</item>
    /// <item>rally_patch 鼓舞提示贴：2AP，任一存活友方（含自己）当前恐慌−6（最低 0）；每目标每大回合来自此道具合计最多 6；恐慌为 0 或本轮已减满时拒绝。</item>
    /// </list>
    /// 三者共享“施用者每大回合最多 1 次”（追击不刷新；不是全队一次）；只扣施用者本人五物资卡里的 1 份（不能从封存账或队友包拿）；
    /// 不给经验、不磨武器、不占第三武器槽，体力为固定值不受熟练省力；预览与提交用同一个校验（<see cref="TacticalBlocked"/>）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>战术品配置（按战斗指令 ID）；不是战术品为 null。</summary>
        public TacticalItemConfig? TacticalOf(string action) => Config.DungeonRewards.Tactical.FirstOrDefault(t => t.Action == action);

        /// <summary>某战术品指令对应的物品 ID（网页数随身份数）；不是战术品为 null。</summary>
        public string? TacticalItemOf(string action) => TacticalOf(action)?.Id;

        /// <summary>效果短句（指令悬停与预览）。</summary>
        public string TacticalEffectText(TacticalItemConfig t) => t.Target switch
        {
            "enemy" => $"标记敌方任一存活目标{t.Rounds}大回合：全队对它命中能力+{Fmt.T(t.MarkBonus)}（与猎标取强、不叠层；无伤害、不判命中）；噪声+{Fmt.T(t.Noise)}",
            "self" => $"自己位移抵抗+{Fmt.T(t.DisplaceResist)}个百分点，{t.Rounds}大回合（与稳固取强；不格挡、不免伤）",
            _ => $"任一存活友方（含自己）当前恐慌−{Fmt.T(t.PanicReduce)}（最低0；每人每大回合来自此物合计最多{Fmt.T(t.PerTargetRoundCap)}）",
        };

        /// <summary>某友方本大回合已从鼓舞提示贴减掉的恐慌。</summary>
        private static double RallyTakenThisRound(BattleState b, string actorId)
        {
            var f = b.Fighter(actorId);
            return f.RallyRound == b.Round ? f.RallyAmount : 0;
        }

        /// <summary>战术品指令的合法性（只读；预览与提交同源）：随身份数、每人每大回合一次、AP、体力、目标与“有没有收益”。null 为可以。</summary>
        private string? TacticalBlocked(GameState s, BattleState b, BattleCommand c, TacticalItemConfig t)
        {
            if (CarriedItem(s, t.Id, c.ItemInstanceId) == null) return $"随身没有{ItemName(t.Id)}（只能用自己五张物资卡里的，不能从封存或队友包里拿）";
            if (b.Me.TacticalRound == b.Round) return "本大回合已经用过战术品（每人每大回合1次，追击不刷新）";
            if (t.Ap > b.PlayerAp) return $"行动点不足（需{t.Ap}，剩{b.PlayerAp}）";
            if (s.Stats.Energy < t.Energy - 1e-9) return $"体力不足（需{Fmt.N(t.Energy)}，现有{Fmt.N(s.Stats.Energy)}）";
            switch (t.Target)
            {
                case "enemy":
                {
                    if (c.Target == null) return "请选择敌方目标（1—4号位都可以）";
                    var e = b.Enemies.FirstOrDefault(x => x.Index == c.Target.Value);
                    if (e == null || !e.Alive || !e.Hostile) return "目标已经不在了";
                    if (EnemyMarked(b, e) && e.MarkBonus >= t.MarkBonus - 1e-9 && e.MarkUntilRound >= b.Round + t.Rounds)
                        return "目标已有同等或更强、更长的标记：用了没有收益，不消耗";
                    return null;
                }
                case "self":
                {
                    var f = b.Me;
                    if (f.DisplaceUntilRound >= b.Round && f.DisplaceResist >= t.DisplaceResist - 1e-9 && f.DisplaceUntilRound >= b.Round + t.Rounds)
                        return "已有同等或更强、更长的稳固（位移抵抗）：用了没有收益，不消耗";
                    return null;
                }
                default:
                {
                    var target = c.AllyTarget ?? b.FocusId;
                    if (!AlliesOnField(b).Contains(target)) return "目标不在场上（只能给存活的友方）";
                    var f = b.Fighter(target);
                    if (f.Panic <= 1e-9) return $"{(target == b.FocusId ? "你" : ActorDisplayName(s, target))}恐慌已经是0：用了没有收益，不消耗";
                    if (RallyTakenThisRound(b, target) >= t.PerTargetRoundCap - 1e-9) return $"{(target == b.FocusId ? "你" : ActorDisplayName(s, target))}本大回合已经从鼓舞提示贴减过{Fmt.T(t.PerTargetRoundCap)}点恐慌";
                    return null;
                }
            }
        }

        /// <summary>指令条里的三个战术品选项（Group "item"，与医疗同一快捷栏）：闪光标记器点敌人；护带自用；提示贴多人时点友方、单人自用。</summary>
        private IEnumerable<BattleCommandOption> TacticalOptions(GameState s, BattleState b)
        {
            foreach (var t in Config.DungeonRewards.Tactical)
            {
                var have = s.Items.Where(i => CarriedByMe(s, i) && i.ItemId == t.Id).Sum(i => i.Count);
                var o = new BattleCommandOption
                {
                    Action = t.Action, Label = $"{ItemName(t.Id)}（随身×{have}）", Group = "item", Ap = t.Ap, Energy = t.Energy,
                    EffectText = TacticalEffectText(t), TargetMode = "self",
                };
                if (t.Target == "enemy")
                {
                    o.TargetMode = "single";
                    o.NeedsTarget = true;
                    foreach (var e in b.Alive) if (CommandBlocked(s, BattleCommand.Of(t.Action, e.Index)) == null) o.Targets.Add(e.Index);
                    o.Reason = CommandBlocked(s, BattleCommand.Of(t.Action, o.Targets.Count > 0 ? o.Targets[0] : b.Alive.Select(e => (int?)e.Index).FirstOrDefault()));
                }
                else if (t.Target == "ally" && AllyCount(b) > 1)
                {
                    o.TargetMode = "ally";
                    foreach (var a in AlliesOnField(b))
                        if (CommandBlocked(s, new BattleCommand { Action = t.Action, AllyTarget = a }) == null) o.AllyTargets.Add(a);
                    o.Reason = o.AllyTargets.Count > 0 ? null : CommandBlocked(s, new BattleCommand { Action = t.Action, AllyTarget = b.FocusId });
                    if (o.AllyTargets.Count == 0 && o.Reason == null) o.Reason = "没有可选的友方目标";
                }
                else o.Reason = CommandBlocked(s, BattleCommand.Of(t.Action));
                o.Available = o.Reason == null;
                yield return o;
            }
        }

        /// <summary>战术品预览（右栏）：成本、目标、效果与“每人每大回合1次”，与提交同一校验。</summary>
        private void TacticalPreview(GameState s, BattleState b, BattleCommand c, BattlePreview p, TacticalItemConfig t)
        {
            p.Title = ItemName(t.Id);
            p.Ap = t.Ap;
            p.EnergyCost = t.Energy;
            p.EnergyAfter = Math.Max(0, s.Stats.Energy - t.Energy);
            if (t.Target == "enemy")
            {
                p.NoiseAfter = b.Noise + t.Noise;
                if (c.Target is int ti && b.Enemies.FirstOrDefault(x => x.Index == ti) is BattleEnemy e)
                    p.EffectText = EnemyMarked(b, e)
                        ? $"{EnemyName(e)}：标记 命中能力+{Fmt.T(Math.Max(e.MarkBonus, t.MarkBonus))}，到第{Math.Max(e.MarkUntilRound, b.Round + t.Rounds)}轮末（已有标记取强、不叠层）"
                        : $"{EnemyName(e)}：标记 全队对它命中能力+{Fmt.T(t.MarkBonus)}，到第{b.Round + t.Rounds}轮末";
            }
            else if (t.Target == "self")
                p.EffectText = $"你：位移抵抗+{Fmt.T(t.DisplaceResist)}个百分点，到第{b.Round + t.Rounds}轮末（与稳固取强）";
            else
            {
                var target = c.AllyTarget ?? b.FocusId;
                var f = b.Fighter(target);
                var cut = Math.Max(0, Math.Min(Math.Min(t.PanicReduce, f.Panic), t.PerTargetRoundCap - RallyTakenThisRound(b, target)));
                p.EffectText = $"{(target == b.FocusId ? "你" : ActorDisplayName(s, target))}：恐慌{Fmt.N(f.Panic)}→{Fmt.N(f.Panic - cut)}";
            }
            if (p.EffectText != null) p.Notes.Add(p.EffectText);
            p.Notes.Add(TacticalEffectText(t));
            p.Notes.Add($"消耗随身{ItemName(t.Id)}1份；{t.Ap}AP{(t.Energy > 0 ? $"、体力{Fmt.N(t.Energy)}（固定值）" : "")}；战术品每人每大回合1次（追击不刷新）；不给经验、不磨武器");
        }

        /// <summary>执行战术品（ExecuteCommand 已做请求号与合法性）：扣 AP/体力与 1 份物品、记本轮已用，再结算效果。</summary>
        private void TacticalExecute(GameState s, BattleState b, BattleCommand c, TacticalItemConfig t)
        {
            var it = CarriedItem(s, t.Id, c.ItemInstanceId)!;
            b.PlayerAp -= t.Ap;
            if (t.Energy > 0) s.Stats.Add(StatId.Energy, -t.Energy);
            ConsumeOne(s, it);
            b.Me.TacticalRound = b.Round;
            var name = ItemName(t.Id);
            switch (t.Target)
            {
                case "enemy":
                {
                    var e = b.Enemies.First(x => x.Index == c.Target!.Value);
                    var was = EnemyMarked(b, e);
                    e.MarkBonus = was ? Math.Max(e.MarkBonus, t.MarkBonus) : t.MarkBonus;
                    e.MarkUntilRound = Math.Max(was ? e.MarkUntilRound : 0, b.Round + t.Rounds);
                    AddNoise(s, b, t.Noise);
                    BattleLog(s, $"{Who(s)}用{name}标记了{EnemyName(e)}（全队对它命中能力+{Fmt.T(e.MarkBonus)}，到第{e.MarkUntilRound}轮末；噪声+{Fmt.T(t.Noise)}）。");
                    break;
                }
                case "self":
                {
                    var f = b.Me;
                    var active = f.DisplaceUntilRound >= b.Round;
                    f.DisplaceResist = active ? Math.Max(f.DisplaceResist, t.DisplaceResist) : t.DisplaceResist;
                    f.DisplaceUntilRound = Math.Max(active ? f.DisplaceUntilRound : 0, b.Round + t.Rounds);
                    BattleLog(s, $"{Who(s)}系上{name}（位移抵抗+{Fmt.T(f.DisplaceResist)}个百分点，到第{f.DisplaceUntilRound}轮末）。");
                    break;
                }
                default:
                {
                    var target = c.AllyTarget ?? b.FocusId;
                    var f = b.Fighter(target);
                    var cut = Math.Max(0, Math.Min(Math.Min(t.PanicReduce, f.Panic), t.PerTargetRoundCap - RallyTakenThisRound(b, target)));
                    if (f.RallyRound != b.Round) { f.RallyRound = b.Round; f.RallyAmount = 0; }
                    f.RallyAmount += cut;
                    var before = f.Panic;
                    f.Panic = Math.Max(0, f.Panic - cut);
                    BattleLog(s, $"{Who(s)}给{(target == b.FocusId ? "自己" : ActorDisplayName(s, target))}贴上{name}：恐慌{Fmt.N(before)}→{Fmt.N(f.Panic)}。");
                    break;
                }
            }
        }
    }
}
