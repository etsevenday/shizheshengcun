using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 开场、饱饱与首战（N01、S15 §5、R07）：新局在营地醒来 → 饱饱对白与饮食提示 → 首次 camp→field 强制游荡尸首战 →
    /// 胜利得唯一教学活性物质 → 吞噬融合得绿色“攻击力+0.1”。旧档（T06）不补。对白不推进时间、不发奖励。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string DialogueAwakening1 = "awakening_1";
        public const string DialogueAwakening2 = "awakening_2";
        public const string DialogueFoodWater = "food_water";
        public const string DialogueTutorialArrive = "tutorial_arrive";
        public const string DialogueTutorialWon = "tutorial_won";
        public const string DialogueFusionDone1 = "fusion_done_1";
        public const string DialogueFusionDone2 = "fusion_done_2";

        /// <summary>本局有没有本轮新剧情（新局为真；旧档迁移为假）。</summary>
        public static bool StoryActive(GameState s) => !s.Story.Legacy;

        /// <summary>首战尚未胜利（新局）：其他敌人的主动接近与随机接敌都关闭，第一次入战必须由首战承接（S15 §5、R05 §6.2）。</summary>
        public static bool TutorialGateOpen(GameState s) => StoryActive(s) && !s.Story.TutorialWon;

        /// <summary>首战保留的世界敌人（胜利前）。</summary>
        public static bool IsTutorialEnemy(GameState s, int worldEnemyId) =>
            TutorialGateOpen(s) && s.Story.TutorialEnemyId == worldEnemyId;

        /// <summary>新局：饱饱在体内；荒地初始游荡尸登记为首战保留实例；开场对白入队（营地醒来 → 不知姓名只知饿、命名饱饱 → 饮食提示）。</summary>
        public void InitStory(GameState s)
        {
            var st = new StoryState { BaobaoPresent = Config.Infection.BaobaoInput.NewGamePresent };
            var tb = Config.Story.TutorialBattle;
            if (!string.IsNullOrEmpty(tb.LocationId))
                st.TutorialEnemyId = s.WorldEnemies.Where(w => w.LocationId == tb.LocationId && w.EnemyId == tb.EnemyId).OrderBy(w => w.Id).FirstOrDefault()?.Id;
            s.Story = st;
            QueueDialogue(s, DialogueAwakening1);
            QueueDialogue(s, DialogueAwakening2);
            QueueDialogue(s, DialogueFoodWater);
        }

        // ───────────── 对白 ─────────────

        public DialogueConfig? Dialogue(string id) => Config.Story.Dialogues.FirstOrDefault(d => d.Id == id);

        /// <summary>当前要显示的饱饱对白（队首）；没有为 null。</summary>
        public DialogueConfig? CurrentDialogue(GameState s) =>
            s.Story.PendingDialogues.Select(Dialogue).FirstOrDefault(d => d != null);

        /// <summary>对白入队：同一条只入队一次（回看、读档、重复回调都不重复）。</summary>
        private void QueueDialogue(GameState s, string id)
        {
            if (Dialogue(id) == null) return;
            if (s.Story.PendingDialogues.Contains(id) || s.Story.SeenDialogues.Contains(id)) return;
            s.Story.PendingDialogues.Add(id);
        }

        /// <summary>
        /// 读完一条对白：出队并记为已读；命名与饮食提示锁存。只改剧情标记，不推进世界时间、不消耗物品、不发奖励（N01 §6）。
        /// </summary>
        public ActionResult AckDialogue(GameState s, string id)
        {
            var st = s.Story;
            if (!st.PendingDialogues.Contains(id)) return ActionResult.Repeated();
            st.PendingDialogues.Remove(id);
            if (!st.SeenDialogues.Contains(id)) st.SeenDialogues.Add(id);
            if (id == DialogueAwakening2) st.AwakeningDone = true;
            if (id == DialogueFoodWater) st.FoodWaterRead = true;
            return ActionResult.Success();
        }

        // ───────────── 首战 ─────────────

        /// <summary>
        /// 移动完成后（已落点并记访问）：第一次从营地到达荒地、且首战未胜利时，由保留游荡尸唯一触发一次教学首战（剧情情境 K=1）。
        /// 重复到达、死亡、取消移动都不触发；之后撤退的同一敌人仍留在荒地，可以主动接近继续挑战。
        /// </summary>
        private void TryStartTutorialBattle(GameState s, ActionState travel)
        {
            var st = s.Story;
            var tb = Config.Story.TutorialBattle;
            if (!TutorialGateOpen(s) || st.TutorialTriggered || s.Progress.Dead) return;
            if (travel.TargetId != tb.LocationId || travel.SourceLocation != tb.FromLocationId) return;
            st.TutorialTriggered = true;
            var w = st.TutorialEnemyId.HasValue ? FindWorldEnemy(s, st.TutorialEnemyId.Value) : null;
            if (w == null || w.LocationId != tb.LocationId || w.Engaged || InCombatFlow(s)) return;
            w.HiddenUntil = null;
            QueueDialogue(s, DialogueTutorialArrive);
            StartBattle(s, new List<WorldEnemy> { w }, false, "首战", FearTutorial, tutorial: true);
            st.TutorialBattleId = s.Battle?.BattleId;
        }

        /// <summary>
        /// 首战最终胜利且唯一轮末 tick 存活后：唯一一次发放教学白色未鉴定活性物质到当前地面（按真实 owner，背包满也不丢），
        /// 不再抽该敌普通活性物质；记录发放事件 ID，重复回调、读档不再发（R07 §3、S15 §5）。返回发放的文字，没有发放为 null。
        /// </summary>
        private string? GrantTutorialReward(GameState s, BattleState b)
        {
            var st = s.Story;
            if (!b.Tutorial || !TutorialGateOpen(s)) return null;
            st.TutorialWon = true;
            if (!string.IsNullOrEmpty(st.TutorialGrantEventId)) return null;
            var sub = Config.ActiveSubstance;
            if (string.IsNullOrEmpty(sub.ItemId)) return null;
            var grant = $"tutorial-reward-b{b.BattleId}";
            st.TutorialGrantEventId = grant;
            var wid = b.Enemies.FirstOrDefault(e => e.WorldId == st.TutorialEnemyId)?.WorldId ?? b.Enemies.FirstOrDefault()?.WorldId;
            CreateItems(s, sub.ItemId, Math.Max(1, sub.TutorialDrop.Count), b.LocationId, false,
                new ItemSource { WorldEnemyId = wid, BattleId = b.BattleId, GrantEventId = grant, TutorialReward = true });
            QueueDialogue(s, DialogueTutorialWon);
            return $"{ItemName(sub.ItemId)}+{Math.Max(1, sub.TutorialDrop.Count)}（首战教学）";
        }

        // ───────────── 吞噬融合（R07 §4） ─────────────

        /// <summary>是不是活性物质（饱饱吞噬融合的材料）。</summary>
        public bool IsSubstance(string itemId) => !string.IsNullOrEmpty(Config.ActiveSubstance.ItemId) && itemId == Config.ActiveSubstance.ItemId;

        /// <summary>“首战教学·未鉴定的活性物质”或“未鉴定的活性物质（白色）”。</summary>
        public string SubstanceLabel(ItemInstance i) =>
            i.Source?.TutorialReward == true ? $"首战教学·{ItemName(i.ItemId)}" : $"{ItemName(i.ItemId)}（白色）";

        /// <summary>融合会得到什么（预览与卡下说明）：教学实例为指定绿色词条，普通实例无属性。</summary>
        public string FusionOutcomeText(GameState s, ItemInstance i)
        {
            var te = Config.ActiveSubstance.TutorialEffect;
            if (i.Source?.TutorialReward == true && !s.Story.TutorialFusionDone)
                return $"绿色词条“{te.Label}”（首战教学固定结果，只取得一次）";
            return "无属性变化（普通白色活性物质）";
        }

        private string? ValidateFuse(GameState s, ActionRequest r, Plan plan, bool allowGround)
        {
            var fc = Config.ActiveSubstance.Fusion;
            if (plan.IsSub) return "吞噬融合只能在空闲时进行（暂停中不行）";
            if (!r.InstanceId.HasValue) return "请选择要吞噬融合的活性物质";
            var inst = FindItem(s, r.InstanceId.Value);
            if (inst == null) return "这份活性物质已不在";
            if (!IsSubstance(inst.ItemId)) return "饱饱只能吞噬融合活性物质";
            if (!(inst.IsCarried || (allowGround && inst.Owner == s.CurrentLocation)))
                return "只能吞噬融合随身或当前地面的活性物质";
            if (inst.ReservedBy != null || IsLocked(s, inst)) return "该物品已被预留";
            if (inst.Count < 1) return "数量不足";
            plan.UseItem = inst;
            plan.TargetId = inst.ItemId;
            plan.Planned = fc.Minutes;
            plan.Label = "吞噬融合";
            return null;
        }

        /// <summary>
        /// 融合事务（R07 §4–§5）：开始时一次提交——消耗 1 份、登记词条（教学实例且尚未融合过）、完成标记、对白；
        /// 之后按 wait 速率推进固定 5 分钟（额外体力 0），期间不可取消。重复 requestId 由 Start 拦下，不部分扣除。
        /// </summary>
        private void StartFuse(GameState s, ActionState a, Plan p)
        {
            var inst = p.UseItem!;
            var tutorial = inst.Source?.TutorialReward == true && !s.Story.TutorialFusionDone && StoryActive(s);
            var fusionId = $"fusion-{a.ActionId}";
            ConsumeOne(s, inst);
            s.Story.FusionIds.Add(fusionId);
            if (s.Story.FusionIds.Count > 64) s.Story.FusionIds.RemoveRange(0, s.Story.FusionIds.Count - 64);
            string text;
            if (tutorial)
            {
                var te = Config.ActiveSubstance.TutorialEffect;
                s.Traits.Add(new TraitInstance { EffectId = te.EffectId, Quality = te.Quality, Label = te.Label, FlatAttack = te.FlatAttack, FusionId = fusionId, Minute = s.GameMinute });
                s.Story.TutorialFusionDone = true;
                QueueDialogue(s, DialogueFusionDone1);
                QueueDialogue(s, DialogueFusionDone2);
                text = $"饱饱吞噬融合了首战教学的活性物质：获得绿色词条“{te.Label}”。";
            }
            else text = Config.ActiveSubstance.NormalFusionText;
            a.Label = "吞噬融合";
            Emit(s, GameEventKind.ActionCompleted, text);
        }
    }
}
