using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B37a2（Q806 A、Q811 A、Q835 补问 B、Q839 A）：人物肖像／全身图按身份取 ink-v2 清单图。只决定“用哪个稳定 ID”，不读不写存档字段。
/// 查询顺序：在岗人物 actor:{id} → 代班岗位 duty:{岗位 npcId} → 主角 actor:p0 → 无职业保底护送员 recruitTemplate:legacy_neutral → 查不到 glyph:unknown。
/// BM11（A11／S14 Q884 B）：随机有职业招募同伴正式个人图未齐前统一 glyph:unknown 问号占位，不用职业模板图冒充本人。
/// 纯规则，不依赖界面；测试项目直接编译本文件（见 Survival.Core.Tests.csproj）。
/// </summary>
public static class ActorArt
{
    /// <summary>Q839 A：主角按 a9b93f0 manifest 的 actor:p0。</summary>
    public const string Protagonist = "actor:p0";
    /// <summary>饱饱（manifest actor:baobao）。</summary>
    public const string Baobao = "actor:baobao";
    /// <summary>Q835 必要补问 B：无职业模板保底护送员保持固定通用图 recruitTemplate:legacy_neutral 及其同源裁窗。</summary>
    public const string EscortFallback = "recruitTemplate:legacy_neutral";
    /// <summary>查不到具体身份时的中性标记（Q811 A“不猜另一名人物”；16 号稿“没有专属肖像的角色使用新版中性标记”）。</summary>
    public const string Unknown = "glyph:unknown";
    /// <summary>BM11（Q920 A）：主角小头像图位的头脸裁窗（portrait-actor-p0.webp 160×240 内的 viewBox：x y 宽 高），开发按原图头脸范围（约 x59—105、y2—56）取。</summary>
    public const string ProtagonistFaceCrop = "54 0 60 60";
    /// <summary>主角肖像图（清单 portraits actor:p0）像素尺寸。</summary>
    public static readonly (int W, int H) ProtagonistPortraitSize = (160, 240);

    /// <summary>
    /// BM11（N02 附录／S14 Q886 A）：头像读屏替代文字——正式肖像只写实际显示名；仍是过渡问号 glyph:unknown 时写“{显示名}（临时形象）”。
    /// 已批准的固定通用补员图（recruitTemplate:legacy_neutral）是正式图，不算临时。
    /// </summary>
    public static string AltText(string key, string name) => key == Unknown ? $"{name}（临时形象）" : name;

    /// <summary>队伍里的一个角色（整备、战斗站位／队列／操作台、特写、人物详情）：主角 → 随机招募（Q884 B 问号占位）→ 保底护送员 → 命名人物 actor → 查不到。</summary>
    public static string ForActor(ArtCatalog cat, GameState s, string actorId)
    {
        if (actorId == GameState.PlayerId) return Protagonist;
        var r = s.Roster.FirstOrDefault(x => x.ActorId == actorId);
        if (r != null && r.IsRandom) return Unknown;
        if (r != null && r.IsEscort) return Known(cat, EscortFallback);
        return Known(cat, "actor:" + actorId);
    }

    /// <summary>地点上的人物卡：岗位离岗由代班接手时用岗位 duty（不借原人图），否则用本人 actor（城市岗位、序章来客、固定同行者邀约卡）。</summary>
    public static string ForNpc(ArtCatalog cat, string npcId, bool substitute) =>
        Known(cat, (substitute ? "duty:" : "actor:") + npcId);

    /// <summary>
    /// 一句对白的发言身份（Q811 A）：按这句的 LineId 反查配置的发言人；记录里这句的发言者快照是代班名时用所属岗位的 duty（不借原离岗人物）。
    /// 主角句恒为 actor:p0；旁白／文书等非人物行为 null（不配头像，Q305）。配置里已没有这句、或发言人对不上任何人物／岗位时为 glyph:unknown，不猜。
    /// </summary>
    public static string? ForLine(ArtCatalog cat, GameConfig cfg, DialogueLine line)
    {
        if (!DialogueKinds.IsPerson(line) || line.Side == DialogueSide.Narration) return null;
        if (line.Side == DialogueSide.Left) return Protagonist;
        var d = cfg.Story.Dialogues.FirstOrDefault(x => x.Id == line.LineId);
        if (d == null) return Unknown;
        var npcs = cfg.City.Npcs;
        var byName = npcs.FirstOrDefault(n => n.Name == d.Speaker);
        if (npcs.Any(n => n.DutySpeaker != "" && n.DutySpeaker == line.Speaker))
        {
            // 代班：原句属于某命名岗位人物（展示时已离岗）→ 该岗位；配置本身就是代班句 → 按所在对白组属于哪个岗位的对白集
            var post = byName ?? npcs.FirstOrDefault(n => n.DialogueSet != "" && (d.Event == n.DialogueSet || d.Event.StartsWith(n.DialogueSet + "_", StringComparison.Ordinal)));
            return post == null ? Unknown : Known(cat, "duty:" + post.NpcId);
        }
        if (d.Speaker == GameEngine.BaobaoSpeaker) return Known(cat, Baobao);
        if (byName != null) return Known(cat, "actor:" + byName.NpcId);
        if (cfg.Party.NamedCompanions.FirstOrDefault(c => c.Name == d.Speaker) is { } named) return Known(cat, "actor:" + named.ActorId);
        if (cfg.Prologue.Visitor.Name != "" && cfg.Prologue.Visitor.Name == d.Speaker) return Known(cat, "actor:" + cfg.Prologue.Visitor.NpcId);
        return Unknown;
    }

    /// <summary>
    /// 对白右侧（对方）当前该显示的那一句（Q811 A“肖像／名牌跟当前真实发言句走”）：截至当前句（含）最近一句右侧人物对白；
    /// 本组对方还没说过话时为 null，右侧留空（Q306）。
    /// </summary>
    public static DialogueLine? RightSpeakerLine(IReadOnlyList<DialogueLine> lines, int upToOrder) =>
        lines.LastOrDefault(l => l.Order <= upToOrder && l.Side == DialogueSide.Right && DialogueKinds.IsPerson(l));

    /// <summary>清单里同时有肖像和全身裁窗才算这个身份有图；否则落到中性标记。</summary>
    private static string Known(ArtCatalog cat, string key) =>
        cat.Portraits.ContainsKey(key) && cat.HasSprite(key) ? key : Unknown;
}
