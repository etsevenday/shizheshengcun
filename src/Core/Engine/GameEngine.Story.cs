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
            OpenDialogue(s, EventOf(DialogueAwakening1));
        }

        // ───────────── 对白（S01 §8、S14 §10、Q10：读完才能操作） ─────────────

        /// <summary>对话等待期间拒绝玩法请求的统一原因。</summary>
        public const string DialogueLockedReason = "请先读完对话";

        /// <summary>主角在对白里的发言者名：配置里写这个名字的台词归左侧；其他发言者归右侧，发言者为空的是中性叙述。</summary>
        public const string ProtagonistSpeaker = "你";

        public DialogueConfig? Dialogue(string id) => Config.Story.Dialogues.FirstOrDefault(d => d.Id == id);

        private string EventOf(string lineId) => Dialogue(lineId)?.Event ?? lineId;

        /// <summary>当前对话（第一段未结束的）；没有为 null。死亡后不再有对话（终局优先）。</summary>
        public DialogueSession? ActiveDialogue(GameState s) =>
            s.Progress.Dead ? null : s.Story.DialogueLog.FirstOrDefault(d => !d.Ended);

        /// <summary>
        /// 有未读完的对话：整局游戏输入锁定（Q10、S14 §10.1）。内核据此拒绝开始/取消/继续行动、即时操作、接敌、战斗指令与换武器，
        /// 主行动与战斗在下一个安全边界前等待（Advance 返回 Dialogue、BattleStep 返回 Dialogue），不 tick、不改原状态。
        /// </summary>
        public bool DialogueLocked(GameState s) => ActiveDialogue(s) != null;

        /// <summary>当前对话里正在显示的那句（已展示的最后一句）；没有为 null。</summary>
        public DialogueLine? CurrentDialogueLine(GameState s) => ActiveDialogue(s)?.Lines.LastOrDefault();

        /// <summary>当前要显示的对白配置（同 CurrentDialogueLine 的那句）；没有为 null。</summary>
        public DialogueConfig? CurrentDialogue(GameState s)
        {
            var line = CurrentDialogueLine(s);
            return line == null ? null : Dialogue(line.LineId);
        }

        /// <summary>当前对话还有没展示的台词：按钮写“继续”；否则写“结束对话”。不提供剩余句数（S14 §10.2）。</summary>
        public bool DialogueHasMore(GameState s) => (ActiveDialogue(s)?.Pending.Count ?? 0) > 0;

        /// <summary>对话记录（S14 §10.4）：已结束的各次对话，按发生顺序；只含实际展示过的台词，不含当前未结束的那次。</summary>
        public List<DialogueSession> DialogueHistory(GameState s) => s.Story.DialogueLog.Where(d => d.Ended).ToList();

        private static DialogueSide SideOf(DialogueConfig d) =>
            string.IsNullOrEmpty(d.Speaker) ? DialogueSide.Narration : d.Speaker == ProtagonistSpeaker ? DialogueSide.Left : DialogueSide.Right;

        /// <summary>
        /// 打开一次对话：配置里同一 eventId 的台词按顺序连成一次对话。同一事件只记一次，已读/待读的台词不重复入队（读档、重复回调都不重复）。
        /// 前面没有未结束的对话时首句立即展示并登记时间地点；否则排在后面，前一次结束后再展示。不推进时间、不发奖励。
        /// </summary>
        private void OpenDialogue(GameState s, string eventId)
        {
            var st = s.Story;
            if (st.DialogueLog.Any(d => d.EventId == eventId)) return;
            var ids = Config.Story.Dialogues.Where(d => d.Event == eventId).Select(d => d.Id)
                .Where(id => !st.PendingDialogues.Contains(id) && !st.SeenDialogues.Contains(id)).ToList();
            if (ids.Count == 0) return;
            var counterpart = Config.Story.Dialogues.Where(d => ids.Contains(d.Id) && SideOf(d) == DialogueSide.Right).Select(d => d.Speaker).FirstOrDefault() ?? "";
            var session = new DialogueSession { Id = st.NextDialogueSessionId++, EventId = eventId, Counterpart = counterpart, Pending = ids };
            st.DialogueLog.Add(session);
            st.PendingDialogues.AddRange(ids);
            if (ReferenceEquals(ActiveDialogue(s), session)) ShowNextLine(s, session);
        }

        /// <summary>展示下一句：首次展示即登记快照（发言者、正文、叙述、左右、时间、地点），与游标一同存档。</summary>
        private void ShowNextLine(GameState s, DialogueSession d)
        {
            while (d.Pending.Count > 0)
            {
                var id = d.Pending[0];
                d.Pending.RemoveAt(0);
                var cfg = Dialogue(id);
                if (cfg == null) { s.Story.PendingDialogues.Remove(id); continue; }
                d.Lines.Add(new DialogueLine
                {
                    LineId = id, Order = d.Lines.Count + 1, Speaker = cfg.Speaker, Text = cfg.Text, Note = cfg.Note, Side = SideOf(cfg),
                    Minute = s.GameMinute, LocationId = s.CurrentLocation,
                });
                if (d.Minute == null) { d.Minute = s.GameMinute; d.LocationId = s.CurrentLocation; }
                return;
            }
            // 配置里的台词全被删掉了：这一次没有可展示内容，直接结束，不卡住输入锁
            if (d.Lines.Count == 0) d.Ended = true;
        }

        /// <summary>
        /// 推进对话（“继续”/“结束对话”共用）：lineId 须是当前正在显示的那句，否则视为重复点击、不改状态（双击不会跳过未读句）。
        /// 还有下一句就追加展示；已是末句则结束本次对话、解除输入锁，并展示排在后面的下一次对话的首句。
        /// 只改对话与剧情标记（命名、饮食提示锁存），不推进世界时间、不消耗物品、不发奖励、不调用战斗/融合（N01 §6、S01 §8）。
        /// </summary>
        public ActionResult AckDialogue(GameState s, string lineId)
        {
            var st = s.Story;
            var d = ActiveDialogue(s);
            var cur = d?.Lines.LastOrDefault();
            if (d == null || cur == null || cur.LineId != lineId) return ActionResult.Repeated();
            st.PendingDialogues.Remove(lineId);
            if (!st.SeenDialogues.Contains(lineId)) st.SeenDialogues.Add(lineId);
            if (lineId == DialogueAwakening2) st.AwakeningDone = true;
            if (lineId == DialogueFoodWater) st.FoodWaterRead = true;
            if (d.Pending.Count > 0) ShowNextLine(s, d);
            else
            {
                d.Ended = true;
                for (var next = ActiveDialogue(s); next != null && next.Lines.Count == 0; next = ActiveDialogue(s))
                    ShowNextLine(s, next);
            }
            return ActionResult.Success();
        }

        /// <summary>只在模拟副本上用：把所有待读对话直接读完（对话不耗时；预览/估时不能卡在输入锁上）。</summary>
        private void SimReadDialogues(GameState c)
        {
            for (var k = 0; k < 256 && DialogueLocked(c); k++)
            {
                var line = CurrentDialogueLine(c);
                if (line == null) { ActiveDialogue(c)!.Ended = true; continue; }
                AckDialogue(c, line.LineId);
            }
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
            StartBattle(s, new List<WorldEnemy> { w }, false, "首战", FearTutorial, tutorial: true);
            // 首战建立并保存后先展示警告对白，读完才接受第一条战斗指令（R03 §7.1）
            OpenDialogue(s, EventOf(DialogueTutorialArrive));
            st.TutorialBattleId = s.Battle?.BattleId;
        }

        /// <summary>
        /// 首战最终胜利且唯一轮末 tick 存活后：唯一一次发放教学白色未鉴定活性物质到当前地面（按真实 owner，背包满也不丢），
        /// 不再抽该敌普通活性物质；记录发放事件 ID，重复回调、读档不再发（R07 §3、S15 §5）。返回发放的文字，没有发放为 null。
        /// </summary>
        private string? GrantTutorialReward(GameState s, BattleState b)
        {
            var st = s.Story;
            // 试玩修复 B7d-2：首战敌人在任何一场战斗里被击杀都算首战完成（例：首战撤退后经敌踪“接敌”与别的敌人一起打死），
            // 否则它已从世界移除、首战门控永远关不上
            if (!TutorialGateOpen(s) || (!b.Tutorial && !TutorialEnemyKilled(s, b))) return null;
            st.TutorialWon = true;
            if (!string.IsNullOrEmpty(st.TutorialGrantEventId)) return null;
            var sub = Config.ActiveSubstance;
            if (string.IsNullOrEmpty(sub.ItemId)) return null;
            var grant = $"tutorial-reward-b{b.BattleId}";
            st.TutorialGrantEventId = grant;
            var wid = b.Enemies.FirstOrDefault(e => e.WorldId == st.TutorialEnemyId)?.WorldId ?? b.Enemies.FirstOrDefault()?.WorldId;
            CreateItems(s, sub.ItemId, Math.Max(1, sub.TutorialDrop.Count), b.LocationId, false,
                new ItemSource { WorldEnemyId = wid, BattleId = b.BattleId, GrantEventId = grant, TutorialReward = true });
            OpenDialogue(s, EventOf(DialogueTutorialWon));
            return $"{ItemName(sub.ItemId)}+{Math.Max(1, sub.TutorialDrop.Count)}（首战教学）";
        }

        /// <summary>首战保留的那只敌人在这场战斗里倒下了。</summary>
        private static bool TutorialEnemyKilled(GameState s, BattleState b) =>
            s.Story.TutorialEnemyId is int tid && b.Enemies.Any(e => e.WorldId == tid && e.Dead);

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
                OpenDialogue(s, EventOf(DialogueFusionDone1));
                text = $"饱饱吞噬融合了首战教学的活性物质：获得绿色词条“{te.Label}”。";
            }
            else text = Config.ActiveSubstance.NormalFusionText;
            a.Label = "吞噬融合";
            Emit(s, GameEventKind.ActionCompleted, text);
        }
    }
}
