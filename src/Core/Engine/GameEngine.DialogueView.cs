using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 对白行显示类型（N02 §2、附录 §1「Q305–Q316 · B39 开工定稿」）：取值与 assets/20261107/dialogue-rewrite.json 的 kind / noteKind 一致。
    /// 类型只决定呈现，不参与事务、不改触发与顺序。
    /// </summary>
    public static class DialogueKinds
    {
        public const string Speech = "speech";
        public const string Narration = "narration";
        public const string Document = "document";
        public const string Guide = "guide";
        public const string Result = "result";
        public const string Status = "status";

        /// <summary>对白行 kind 的有效值。</summary>
        public static readonly IReadOnlyList<string> LineKinds = new[] { Speech, Narration, Document, Guide, Result, Status };

        /// <summary>附注 noteKind 的有效值（附注不是人物发言，不取 speech）。</summary>
        public static readonly IReadOnlyList<string> NoteKinds = new[] { Narration, Document, Guide, Result, Status };

        /// <summary>Q305：只有 noteKind=guide 的教学附注行首显示的标签。</summary>
        public const string GuideNoteLabel = "操作提示";

        /// <summary>
        /// 一句的实际呈现类型：快照有有效 kind 用它；旧快照缺类型时按 N02 §2 兼容——有发言人按原说话方（人物对白），空发言人按中性旁白。
        /// </summary>
        public static string Of(DialogueLine l) =>
            LineKinds.Contains(l.Kind) ? l.Kind : string.IsNullOrEmpty(l.Speaker) ? Narration : Speech;

        /// <summary>附注的呈现类型：有有效 noteKind 用它；旧快照或无附注为空串（普通附注）。</summary>
        public static string NoteOf(DialogueLine l) =>
            !string.IsNullOrEmpty(l.Note) && NoteKinds.Contains(l.NoteKind) ? l.NoteKind : "";

        /// <summary>人物对白（点亮头像、带发言者名牌）；其余都是中性行。</summary>
        public static bool IsPerson(DialogueLine l) => Of(l) == Speech;

        /// <summary>附注行首标签：只有教学附注是「操作提示」，其余为 null（Q305）。</summary>
        public static string? NoteLabel(DialogueLine l) => NoteOf(l) == Guide ? GuideNoteLabel : null;

        /// <summary>
        /// Q316：扫描配置里各对白来源（现为 story.dialogues），报告缺失或无效的 kind、有非空附注却缺失或无效的 noteKind。
        /// 每条写“{source} 组 {eventId} 行 {id}：…”，source 由调用方给（配置路径）。
        /// </summary>
        public static List<string> ConfigErrors(GameConfig c, string source)
        {
            var e = new List<string>();
            foreach (var d in c.Story.Dialogues)
            {
                var where = $"{source} story.dialogues 组 {d.Event} 行 {d.Id}";
                if (string.IsNullOrEmpty(d.Kind)) e.Add($"{where}：缺 kind");
                else if (!LineKinds.Contains(d.Kind)) e.Add($"{where}：kind「{d.Kind}」无效");
                if (string.IsNullOrEmpty(d.Note)) continue;
                if (string.IsNullOrEmpty(d.NoteKind)) e.Add($"{where}：有附注但缺 noteKind");
                else if (!NoteKinds.Contains(d.NoteKind)) e.Add($"{where}：noteKind「{d.NoteKind}」无效");
            }
            return e;
        }
    }

    /// <summary>对白层（当前对白）的显示数据（附录 §1 B39 定稿 Q306、Q307、Q310、Q313）。只读，不改状态。</summary>
    public sealed class DialogueLayerView
    {
        public DialogueSession Session { get; set; } = null!;
        public DialogueLine Current { get; set; } = null!;
        /// <summary>Q307：正文上方的可见标题：真实组名 → 真实对象名 → null（省略标题行）。</summary>
        public string? Title { get; set; }
        /// <summary>真实对象（右侧名牌与头像）；整组无对方发言时为空串，右侧留空（Q306）。</summary>
        public string Counterpart { get; set; } = "";
        /// <summary>对话框读屏名称（Q306：无对方时用组名，无组名用「当前对白」）。</summary>
        public string AriaName { get; set; } = "";
        /// <summary>还有没展示的台词：按钮写“继续”，否则“结束对话”。</summary>
        public bool HasMore { get; set; }
        /// <summary>同行邀约读完：末句换成两选项（B15a），此时没有文书脚注。</summary>
        public string? RecruitQuestId { get; set; }
        /// <summary>Q310：有行动等待对白时的控制条状态句；无等待行动为 null（省略）。</summary>
        public string? StatusLine { get; set; }
        /// <summary>Q313：当前行是文书、控制条给「继续」或「结束对话」时的阅读脚注；其他情况为 null。</summary>
        public string? Footnote { get; set; }
        /// <summary>当前行是否人物对白（非人物行不点亮头像，Q305）。</summary>
        public bool CurrentIsPerson { get; set; }
    }

    public sealed partial class GameEngine
    {
        /// <summary>Q306：无对方、无组名时对话框的读屏名称。</summary>
        public const string DialogueGenericName = "当前对白";

        /// <summary>Q313：文书阅读的两句脚注。</summary>
        public const string DocumentFootnoteNext = "选择「继续」阅读下一段。";
        public const string DocumentFootnoteEnd = "选择「结束对话」收起文书。";

        /// <summary>Q315：对话记录无记录时的正文空态。</summary>
        public const string DialogueLogEmptyText = "结束一段对话后，就能在这里回看。";

        /// <summary>Q315：有记录时标题下的副标题（数量按可回看的已归档组计，一组一段）；无记录为 null（省略）。</summary>
        public static string? DialogueLogSubtitle(int count) => count > 0 ? $"已收录{count}段 · 回看不消耗时间" : null;

        /// <summary>Q310：控制条状态句；没有行动等待对白时为 null。行动名取当前实际等待的行动（子行动优先，与中央推进条同一行动）。</summary>
        public string? DialogueWaitingText(GameState s)
        {
            var a = s.Sub ?? s.Main;
            if (a == null || !DialogueLocked(s)) return null;
            return $"阅读期间，{a.Label}的进度保持不变。";
        }

        /// <summary>Q307：当前对白可见标题——真实组名优先，其次真实对象名，都缺为 null。兜底字样与记录专用旧别名不参与。</summary>
        public string? DialogueLiveTitle(DialogueSession d)
        {
            var title = DialogueSetTitle(d.EventId);
            if (!string.IsNullOrEmpty(title)) return title;
            return string.IsNullOrEmpty(d.Counterpart) ? null : d.Counterpart;
        }

        /// <summary>当前对白层的显示数据；没有当前对白（或还没展示任何一句）为 null。</summary>
        public DialogueLayerView? DialogueLayer(GameState s)
        {
            var d = ActiveDialogue(s);
            var cur = d?.Lines.LastOrDefault();
            if (d == null || cur == null) return null;
            var more = d.Pending.Count > 0;
            var recruit = more ? null : RecruitOfferPending(s);
            var setTitle = DialogueSetTitle(d.EventId);
            string? foot = null;
            if (DialogueKinds.Of(cur) == DialogueKinds.Document && recruit == null)
                foot = more ? DocumentFootnoteNext : DocumentFootnoteEnd;
            return new DialogueLayerView
            {
                Session = d,
                Current = cur,
                Title = DialogueLiveTitle(d),
                Counterpart = d.Counterpart,
                AriaName = string.IsNullOrEmpty(d.Counterpart)
                    ? (string.IsNullOrEmpty(setTitle) ? DialogueGenericName : setTitle!)
                    : $"与{d.Counterpart}的对话：读完后才能继续操作",
                HasMore = more,
                RecruitQuestId = recruit,
                StatusLine = DialogueWaitingText(s),
                Footnote = foot,
                CurrentIsPerson = DialogueKinds.IsPerson(cur),
            };
        }

        /// <summary>Q312：人物卡「交谈」旁的接取预告句。</summary>
        public static string TalkOfferPreviewText(string questTitle) => $"这次交谈会接取「{questTitle}」。";

        /// <summary>
        /// Q312：这次「交谈」确实会接取的委托名（与 TalkCityNpc 共用 <see cref="TalkOffer"/> 的选择与优先级）；交谈当前受阻、不是城市岗位、
        /// 或只会闲谈/介绍/交付时为 null。只读。
        /// </summary>
        public string? TalkOfferTitle(GameState s, string npcId)
        {
            var n = CityNpc(npcId);
            if (n == null || s.CurrentLocation != n.LocationId || TalkBlocked(s) != null) return null;
            return TalkOffer(s, npcId)?.Title;
        }
    }
}
