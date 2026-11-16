using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Services;

namespace Survival.Core.Tests;

/// <summary>
/// B37a2 人物肖像与延后图位（S14 2026-11-16 B36 节 Q806 A、Q811 A；2026-11-17 B37 节 Q835 补问 B、Q839 A；
/// a9b93f0《资料/逐页坐标与组件槽.json》P01a 顶栏、P03a 整备、P05a 战斗、P09a／P09c 对白槽）：
/// 身份 → 清单图（在岗 actor → 代班 duty → 主角 actor:p0 → 保底护送员 recruitTemplate:legacy_neutral → 查不到 glyph:unknown）、
/// 对白逐句按 LineId 查身份且不增存档字段、右侧肖像跟当前真实发言句、旧 img/portraits 不再引用、顶栏 12 键图标。
/// 身份规则 <see cref="ActorArt"/> 与清单 <see cref="ArtCatalog"/> 由网页工程源文件直接编译进本测试（见 csproj）。
/// </summary>
public class B37a2PortraitTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray());
    private static string WebText(params string[] parts) => File.ReadAllText(Web(parts));

    private static ArtCatalog Cat => _cat.Value;
    private static readonly Lazy<ArtCatalog> _cat = new(() => ArtCatalog.Parse(WebText("Art", "ink-v2.json")));
    private static GameConfig Cfg => _cfg.Value;
    private static readonly Lazy<GameConfig> _cfg = new(() => ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json"))));

    private static DialogueLine Line(string id, string speaker, DialogueSide side, int order = 1, string kind = "speech") =>
        new() { LineId = id, Speaker = speaker, Side = side, Order = order, Kind = kind };

    // ───── M6：队伍角色 ─────

    /// <summary>
    /// 查询顺序（B37a2 任务单；Q839 A 主角 actor:p0；Q835 补问 B 无职业保底护送员固定 recruitTemplate:legacy_neutral；
    /// A11 §6 固定同行者是命名人物 actor:{actorId}；Q811／16 号稿查不到用中性标记 glyph:unknown；Q884 待答：随机招募同伴保持现状）。
    /// </summary>
    [Test]
    public void M6_ActorIdentityOrder()
    {
        var s = new GameState();
        s.Roster.Add(new RosterEntry { ActorId = "npc_lin_zhao", Kind = "named", Name = "林昭" });
        s.Roster.Add(new RosterEntry { ActorId = "escort_1", Kind = "escort", Name = "护送员" });
        s.Roster.Add(new RosterEntry { ActorId = "rc_3_1", Kind = "random", Name = "某人", TemplateId = "hunter" });
        Assert.That(s.Roster, Has.Count.EqualTo(3));
        Assert.That(ActorArt.ForActor(Cat, s, GameState.PlayerId), Is.EqualTo("actor:p0"), "Q839 A");
        Assert.That(ActorArt.ForActor(Cat, s, "npc_lin_zhao"), Is.EqualTo("actor:npc_lin_zhao"), "命名人物 actor");
        Assert.That(ActorArt.ForActor(Cat, s, "escort_1"), Is.EqualTo("recruitTemplate:legacy_neutral"), "Q835 补问 B");
        Assert.That(ActorArt.ForActor(Cat, s, "rc_3_1"), Is.EqualTo("legacy:random_recruit"), "Q884 待答：保持现有临时形象");
        Assert.That(ActorArt.ForActor(Cat, s, "ghost_actor"), Is.EqualTo("glyph:unknown"), "查不到不猜");
        // 随机招募同伴的现有临时形象就是原 unknown.svg（02 Q884 游戏现状）
        Assert.That(ActorArt.RandomRecruitLegacyFile, Is.EqualTo("img/portraits/unknown.svg"));
    }

    /// <summary>
    /// A11 §6 六名固定同行者（林昭、周砚、沈禾、唐梨、顾承、叶洵）都有专属 actor 图，不落到中性标记（a9b93f0 manifest 人物 25 张）。
    /// </summary>
    [Test]
    public void M6_NamedCompanionsHaveOwnArt()
    {
        var ids = new[] { "npc_lin_zhao", "npc_zhou_yan", "npc_shen_he", "npc_tang_li", "npc_gu_cheng", "npc_ye_xun" };
        Assert.That(Cfg.Party.NamedCompanions.Select(c => c.ActorId), Is.EquivalentTo(ids), "配置里固定同行者就是这六人");
        var s = new GameState();
        foreach (var id in ids) s.Roster.Add(new RosterEntry { ActorId = id, Kind = "named" });
        foreach (var id in ids) Assert.That(ActorArt.ForActor(Cat, s, id), Is.EqualTo("actor:" + id), id);
    }

    /// <summary>
    /// 地点人物卡：在岗用本人 actor、代班用岗位 duty（S14 Q811“代班句使用所属岗位的 duty 身份，不能借原离岗人物肖像”；05 P09“代班使用岗位/当班身份”）。
    /// 16 个城市岗位都有 actor 与 duty 两套图（开工前核对“岗位 16/16”）；序章来客程望、固定同行者邀约卡用本人 actor。
    /// </summary>
    [Test]
    public void M6_NpcCardOnDutyAndSubstitute()
    {
        Assert.That(ActorArt.ForNpc(Cat, "npc_xu_an", false), Is.EqualTo("actor:npc_xu_an"));
        Assert.That(ActorArt.ForNpc(Cat, "npc_xu_an", true), Is.EqualTo("duty:npc_xu_an"));
        Assert.That(ActorArt.ForNpc(Cat, "npc_cheng_wang", false), Is.EqualTo("actor:npc_cheng_wang"), "序章来客");
        Assert.That(ActorArt.ForNpc(Cat, "npc_lin_zhao", false), Is.EqualTo("actor:npc_lin_zhao"), "固定同行者邀约卡");
        Assert.That(Cfg.City.Npcs, Has.Count.EqualTo(16));
        foreach (var n in Cfg.City.Npcs)
        {
            Assert.That(ActorArt.ForNpc(Cat, n.NpcId, false), Is.EqualTo("actor:" + n.NpcId), n.NpcId);
            Assert.That(ActorArt.ForNpc(Cat, n.NpcId, true), Is.EqualTo("duty:" + n.NpcId), n.NpcId);
        }
    }

    // ───── M6：对白逐句身份（Q811 A） ─────

    /// <summary>
    /// Q811 A：按每句现有 LineId 反查身份。行 ID 与发言人取自 data/config.v0.6.json story.dialogues（期望身份是规则结论的字面量）：
    /// awakening_1 饱饱、NTL_0003 你、NTL_0001 旁白（不配头像）、NTL_0176 许安在岗、同一句记录时已是代班名 → 许安岗位 duty（不借原人图）、
    /// NTL_0174／NTL_Q236_IDLE_01 配置本身就是陆衡岗位的代班句 → duty:npc_lu_heng、NTL_RN_01 林昭、NTL_0002 程望、配置里没有的句 → 中性标记。
    /// </summary>
    [Test]
    public void M6_LineIdentityByLineId()
    {
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("awakening_1", "饱饱", DialogueSide.Right)), Is.EqualTo("actor:baobao"));
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_0003", "你", DialogueSide.Left)), Is.EqualTo("actor:p0"));
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_0001", "", DialogueSide.Narration, kind: "narration")), Is.Null, "Q305 非人物行不配头像");
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_0176", "许安", DialogueSide.Right)), Is.EqualTo("actor:npc_xu_an"));
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_0176", "机构当班接续员", DialogueSide.Right)), Is.EqualTo("duty:npc_xu_an"), "Q811：代班不借原人肖像");
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_0174", "机构当班接续员", DialogueSide.Right)), Is.EqualTo("duty:npc_lu_heng"));
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_Q236_IDLE_01", "机构当班接续员", DialogueSide.Right)), Is.EqualTo("duty:npc_lu_heng"));
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_RN_01", "林昭", DialogueSide.Right)), Is.EqualTo("actor:npc_lin_zhao"));
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NTL_0002", "程望", DialogueSide.Right)), Is.EqualTo("actor:npc_cheng_wang"));
        Assert.That(ActorArt.ForLine(Cat, Cfg, Line("NO_SUCH_LINE", "许安", DialogueSide.Right)), Is.EqualTo("glyph:unknown"), "Q811：查不到不猜另一名人物");
    }

    /// <summary>
    /// Q811 A 全覆盖：配置里全部右侧人物对白（发言人非空、非“你”，共 282 句，都是 speech）按 LineId 都查到具体身份，没有一句落到中性标记；
    /// 其中配置发言人是“机构当班接续员”的 64 句全部是岗位 duty，其余 218 句是人物 actor。命名岗位人物的句子若记录时已是代班名，同一岗位的 duty。
    /// </summary>
    [Test]
    public void M6_AllConfigLinesResolve()
    {
        var right = Cfg.Story.Dialogues.Where(d => d.Speaker != "" && d.Speaker != GameEngine.ProtagonistSpeaker).ToList();
        Assert.That(right, Has.Count.EqualTo(282));
        var keys = right.Select(d => (d, k: ActorArt.ForLine(Cat, Cfg, Line(d.Id, d.Speaker, DialogueSide.Right, kind: d.Kind)))).ToList();
        Assert.That(keys.Count(x => x.k == "glyph:unknown" || x.k == null), Is.EqualTo(0), string.Join("，", keys.Where(x => x.k == "glyph:unknown" || x.k == null).Select(x => x.d.Id)));
        Assert.That(keys.Count(x => x.k!.StartsWith("duty:")), Is.EqualTo(64));
        Assert.That(keys.Count(x => x.k!.StartsWith("actor:")), Is.EqualTo(218));
        var named = Cfg.City.Npcs.ToDictionary(n => n.Name, n => n.NpcId);
        var postLines = right.Where(d => named.ContainsKey(d.Speaker)).ToList();
        Assert.That(postLines, Is.Not.Empty);
        foreach (var d in postLines)
            Assert.That(ActorArt.ForLine(Cat, Cfg, Line(d.Id, "机构当班接续员", DialogueSide.Right)), Is.EqualTo("duty:" + named[d.Speaker]), d.Id);
    }

    /// <summary>
    /// Q811 A：右侧肖像／名牌跟截至当前句最近一句对方人物对白走；本组对方还没说话时为空（Q306 留空）；旁白、主角句不换右侧。
    /// </summary>
    [Test]
    public void M6_RightSideFollowsCurrentSpeaker()
    {
        var lines = new List<DialogueLine>
        {
            Line("NTL_0003", "你", DialogueSide.Left, 1),
            Line("awakening_1", "饱饱", DialogueSide.Right, 2),
            Line("NTL_0001", "", DialogueSide.Narration, 3, "narration"),
            Line("NTL_0176", "机构当班接续员", DialogueSide.Right, 4),
            Line("NTL_0003", "你", DialogueSide.Left, 5),
        };
        Assert.That(ActorArt.RightSpeakerLine(lines, 1), Is.Null, "对方未发言：留空");
        Assert.That(ActorArt.RightSpeakerLine(lines, 2)?.Order, Is.EqualTo(2), "对方发言后出现");
        Assert.That(ActorArt.RightSpeakerLine(lines, 3)?.Order, Is.EqualTo(2), "旁白不换");
        Assert.That(ActorArt.RightSpeakerLine(lines, 4)?.Order, Is.EqualTo(4), "换人说话跟着换");
        Assert.That(ActorArt.RightSpeakerLine(lines, 5)?.Order, Is.EqualTo(4), "主角说话时右侧保留最近对方");
        Assert.That(ActorArt.ForLine(Cat, Cfg, ActorArt.RightSpeakerLine(lines, 5)!), Is.EqualTo("duty:npc_xu_an"));
    }

    /// <summary>Q811 A“不增加身份保存字段、不升级记录结构”：DialogueLine／DialogueSession 的属性集合保持 B37a2 前原样。</summary>
    [Test]
    public void M6_NoNewSaveFields()
    {
        Assert.That(typeof(DialogueLine).GetProperties().Select(p => p.Name),
            Is.EquivalentTo(new[] { "LineId", "Order", "Speaker", "Text", "Note", "Kind", "NoteKind", "Side", "Minute", "LocationId" }));
        Assert.That(typeof(DialogueSession).GetProperties().Select(p => p.Name),
            Is.EquivalentTo(new[] { "Id", "EventId", "Counterpart", "Minute", "LocationId", "Ended", "Legacy", "Lines", "Pending" }));
    }

    /// <summary>身份落到的肖像文件：主角、饱饱、保底护送员、中性标记都按 a9b93f0 manifest 原绑定，文件在随发目录里（Q839 A、Q835 补问 B）。</summary>
    [Test]
    public void M6_PortraitFilesBound()
    {
        var expect = new Dictionary<string, string>
        {
            ["actor:p0"] = "portrait-actor-p0.webp",
            ["actor:baobao"] = "portrait-actor-baobao.webp",
            ["recruitTemplate:legacy_neutral"] = "portrait-recruitTemplate-legacy_neutral.webp",
            ["glyph:unknown"] = "portrait-glyph-unknown.webp",
            ["duty:npc_xu_an"] = "portrait-duty-npc_xu_an.webp",
        };
        Assert.That(expect, Has.Count.EqualTo(5));
        foreach (var (k, f) in expect)
        {
            Assert.That(Cat.Portraits[k], Is.EqualTo(f), k);
            Assert.That(File.Exists(Web("wwwroot", "art", "ink-v2", f)), f);
            Assert.That(Cat.HasSprite(k), k + " 有全身裁窗");
        }
    }

    /// <summary>
    /// 七个替换点不再引用 img/portraits：主角与饱饱临时 svg 已删；只剩 Q884 待答的随机招募同伴临时形象 unknown.svg，且只由 ActorArt 引用。
    /// </summary>
    [Test]
    public void M6_NoOldPortraitReferences()
    {
        Assert.That(File.Exists(Web("wwwroot", "img", "portraits", "protagonist.svg")), Is.False);
        Assert.That(File.Exists(Web("wwwroot", "img", "portraits", "baobao.svg")), Is.False);
        Assert.That(File.Exists(Web("wwwroot", "img", "portraits", "unknown.svg")), Is.True, "Q884 待答保留");
        var files = Directory.GetFiles(Web(), "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => new[] { ".razor", ".cs", ".css", ".js", ".html" }.Contains(Path.GetExtension(f)))
            .ToList();
        Assert.That(files.Count, Is.GreaterThan(50));
        var hits = files.Where(f => File.ReadAllText(f).Contains("img/portraits/")).Select(Path.GetFileName).ToList();
        Assert.That(hits, Is.EqualTo(new[] { "ActorArt.cs" }));
    }

    /// <summary>七个组件按身份取图（接线）：对白层右侧跟当前句、对话记录按 LineId、战斗队列／操作台／站位／特写、整备、人物详情、地点人物卡。</summary>
    [Test]
    public void M6_ComponentsWired()
    {
        var dlg = WebText("Components", "DialogueLayer.razor");
        Assert.That(dlg, Does.Contain("ActorArt.RightSpeakerLine(v.Session.Lines, cur.Order)").And.Contain("@ArtAssets.LinePortrait(G.E.Config, rightLine)").And.Contain("@ArtAssets.Portrait(ActorArt.Protagonist)"));
        Assert.That(dlg, Does.Not.Contain("var other = v.Counterpart;"), "右侧不再按整组 Counterpart 定");
        var log = WebText("Components", "DialogueLogOverlay.razor");
        Assert.That(log, Does.Contain("ActorArt.RightSpeakerLine(pick.Lines, int.MaxValue)").And.Contain("@ArtAssets.LinePortrait(G.E.Config, pickRight)"));
        var bs = WebText("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("<img src=\"@ArtAssets.ActorPortrait(s, qAlly!)\"").And.Contain("<img src=\"@ArtAssets.ActorPortrait(s, whoId)\"").And.Contain("@ArtAssets.ActorFigure(s, id, \"anc-fig\")"));
        Assert.That(WebText("Components", "BattleFxLayer.razor"), Does.Contain("@if (u.IsAlly) { @ArtAssets.ActorFigure(G.VS, u.Id) }"));
        Assert.That(WebText("Components", "MemberPeekDetail.razor"), Does.Contain("<Thumb><img src=\"@ArtAssets.ActorPortrait(s, id)\""));
        Assert.That(WebText("Components", "PrologueCards.razor"), Does.Contain("@ArtAssets.Figure(ActorArt.ForNpc(ArtAssets.Catalog, n.NpcId, n.Substitute), \"npc-portrait\", $\"{n.Name}（临时形象）\")"));
    }

    // ───── M7：顶栏键面图标、整备人物图、战斗特写 ─────

    /// <summary>
    /// a9b93f0《资料/逐页坐标与组件槽.json》P01a 顶栏 12 个 control 槽（区域地图…菜单）各含一个 18×18 art 槽，依次是
    /// glyph:map、craft、rest、log、formation、equipment、growth、goal、warning、dialogue、help、menu；顺序与 ShellText.TopKeys 一一对应，清单都有图。
    /// </summary>
    [Test]
    public void M7_TopKeyGlyphs()
    {
        var expect = new[] { "glyph:map", "glyph:craft", "glyph:rest", "glyph:log", "glyph:formation", "glyph:equipment",
            "glyph:growth", "glyph:goal", "glyph:warning", "glyph:dialogue", "glyph:help", "glyph:menu" };
        Assert.That(ArtCatalog.TopKeyGlyphs, Is.EqualTo(expect));
        Assert.That(ShellText.TopKeys, Has.Count.EqualTo(12));
        Assert.That(ShellText.TopKeys[0], Is.EqualTo("区域地图"));
        Assert.That(ShellText.TopKeys[8], Is.EqualTo("生存提示"));
        foreach (var k in expect) Assert.That(Cat.HasSprite(k), k);
        Assert.That(WebText("Components", "GameScreen.razor"), Does.Contain("<span class=\"nk-ico\" aria-hidden=\"true\">@Icons.Markup(ArtCatalog.TopKeyGlyphs[i])</span>"));
    }

    /// <summary>P03a 整备：队员列表是 portrait 槽、中栏是当前队员全身 art 槽；P05a 战斗：队列与当前行动者是 portrait 槽、站位是全身 art 槽；特写用攻击者全身图（动效合同 battle）。</summary>
    [Test]
    public void M7_GearAndBattleSlots()
    {
        var g = WebText("Components", "GearOverlay.razor");
        Assert.That(g, Does.Contain("<img class=\"g3-face\" src=\"@ArtAssets.ActorPortrait(s, aid)\"").And.Contain("@ArtAssets.ActorFigure(s, me, \"g3-fig\")"));
        Assert.That(WebText("wwwroot", "css", "app.css"), Does.Contain(".g3-figure > .g3-fig {"));
        Assert.That(WebText("wwwroot", "css", "battle.css"), Does.Contain(".anc.ally.dying .anc-art > .anc-fig {"), "濒死黯淡跪姿沿用到全身图");
        // 清单全身裁窗：主角取 atlas-actor-survivor-sample.webp 原裁窗（Q839 A）
        Assert.That(Cat.Sprites["actor:p0"].File, Is.EqualTo("atlas-actor-survivor-sample.webp"));
        Assert.That(Cat.Sprites["actor:p0"].Crop, Is.EqualTo(new[] { 0, 0, 1024, 1536 }));
    }
}
