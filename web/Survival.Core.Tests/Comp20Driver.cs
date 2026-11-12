using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// COMP20 端到端路线驱动（开发计划 v0.9 B25）：只通过 GameEngine 公开接口像玩家一样移动、探索、接战、吃喝与睡觉，
/// 逐段记下世界分钟、饱食/水分变化、随身卡数与重量、等待分钟、给同行者吃喝的次数。
/// B30（PLAN-Q232-04）起食水只来自真实采集/配给/携带（不再有开发补给），同行者用 B28 同行食饮事务从主角或本人随身出物；
/// 固定战一律实打（不再有开发记胜）。走不通时抛出带原因的异常，由调用的测试决定失败或报告走通到哪一步。
/// </summary>
public sealed class Comp20Driver
{
    public readonly GameEngine E;
    public readonly GameState S;
    public readonly List<string> Ledger = new();
    public readonly List<string> Trace = new();

    public int FoodEaten { get; private set; }
    public int WaterDrunk { get; private set; }
    /// <summary>给同行者吃喝（B28 同行食饮事务）的次数：食用、饮用。</summary>
    public int CompanionFood { get; private set; }
    public int CompanionWater { get; private set; }
    /// <summary>同行食饮的出物人：主角随身、目标本人随身各几次。</summary>
    public int FedFromPlayer { get; private set; }
    public int FedFromSelf { get; private set; }
    /// <summary>每名同行者被喂的份数（显示名 → 食、水）。</summary>
    public readonly Dictionary<string, (int Food, int Water)> FedBy = new();
    /// <summary>缺食/缺水记录（谁、何时、状态多少、随身两人都没有可用的）。同一人同一日同一类只记一次。</summary>
    public readonly List<string> Shortages = new();
    private readonly HashSet<string> _shortKeys = new();
    private int _seq;
    public int Battles { get; private set; }
    public int Retreats { get; private set; }
    public int Bypasses { get; private set; }
    public int WaitMinutes { get; private set; }
    public int SleepMinutes { get; private set; }
    public int ExploreMinutes { get; private set; }
    public int TravelMinutes { get; private set; }
    /// <summary>固定战前就地休整的分钟。</summary>
    public int PrepMinutes { get; private set; }

    /// <summary>用开发入口替代的环节（写进账本与汇报，不冒充实走）。</summary>
    public readonly List<string> Shortcuts = new();

    public Comp20Driver(GameEngine e, GameState s) { E = e; S = s; Verbose = Environment.GetEnvironmentVariable("COMP20_TRACE") != null; }

    /// <summary>战斗逐轮写进 Trace（排查用）。</summary>
    public bool Verbose { get; set; }

    public string Here => S.CurrentLocation;

    private void Log(string msg) =>
        Trace.Add($"{Clock.DayTime(S.GameMinute)} @{Here} H{S.Stats.Health:0.0} N{S.Stats.Satiety:0} W{S.Stats.Hydration:0} E{S.Stats.Energy:0.0} S{S.Stats.Wakefulness:0} | {msg}");

    public Exception Stuck(string why)
    {
        var tail = string.Join("\n", Trace.TakeLast(25));
        return new InvalidOperationException($"COMP20 路线卡住：{why}（{Clock.DayTime(S.GameMinute)} 在 {Here}）\n{tail}");
    }

    // ───────────── 分段记账 ─────────────

    /// <summary>执行一段路线并记一行：分钟、饱食/水分变化、吃喝份数、随身卡数/重量、等待。</summary>
    public void Leg(string label, Action act)
    {
        var t0 = S.GameMinute; var n0 = S.Stats.Satiety; var w0 = S.Stats.Hydration;
        var f0 = FoodEaten; var d0 = WaterDrunk; var cf0 = CompanionFood; var cw0 = CompanionWater;
        var wait0 = WaitMinutes; var sl0 = SleepMinutes; var b0 = Battles;
        act();
        Ledger.Add($"{label}｜{Clock.DayTime(t0)}→{Clock.DayTime(S.GameMinute)}（{S.GameMinute - t0}分钟）"
            + $"｜饱食{n0:0}→{S.Stats.Satiety:0} 水分{w0:0}→{S.Stats.Hydration:0}"
            + $"｜主角吃{FoodEaten - f0}份 喝{WaterDrunk - d0}份｜给同行者吃{CompanionFood - cf0}份 喝{CompanionWater - cw0}份"
            + $"｜随身{E.RootCardCount(S)}/{E.MaxRootCards}张 {E.CarriedGrams(S) / 1000.0:0.00}kg"
            + $"｜等待{WaitMinutes - wait0} 睡眠{SleepMinutes - sl0} 战斗{Battles - b0}｜同行{PartyText()}");
    }

    public void Note(string text) => Ledger.Add("  · " + text);

    /// <summary>同行名单与各人健康/饱食/水分（账本用）。</summary>
    public string PartyText() => string.Join("、", S.Party.Select(a =>
        a == GameState.PlayerId ? E.ActorDisplayName(S, a) : $"{E.ActorDisplayName(S, a)}(H{S.Actors[a].Stats.Health:0.0} N{S.Actors[a].Stats.Satiety:0} W{S.Actors[a].Stats.Hydration:0})"));

    public void Shortcut(string text) { Shortcuts.Add(text); Ledger.Add("  · 【开发入口】" + text); }

    // ───────────── 基础动作 ─────────────

    public void ReadDialogues()
    {
        for (var k = 0; k < 128 && E.DialogueLocked(S); k++)
            E.AckDialogue(S, E.CurrentDialogueLine(S)!.LineId);
    }

    /// <summary>开始一个行动并推进到结束（暂停就继续；途中对白读完；遭遇敌踪按策略处理）。</summary>
    public bool Do(ActionRequest r, bool required = false)
    {
        ReadDialogues();
        var res = E.Start(S, r);
        if (!res.Ok)
        {
            if (required) throw Stuck($"{r.Type} {r.TargetId} 被拒：{res.Reason}");
            return false;
        }
        var t0 = S.GameMinute;
        Log($"{r.Type} {r.TargetId}{(r.Minutes > 0 ? " " + r.Minutes : "")}");
        Drive();
        var used = S.GameMinute - t0;
        switch (r.Type)
        {
            case ActionType.Wait: WaitMinutes += used; break;
            case ActionType.Sleep: SleepMinutes += used; break;
            case ActionType.Explore: ExploreMinutes += used; break;
            case ActionType.Travel: TravelMinutes += used; break;
        }
        AfterAction();
        return true;
    }

    public void Drive()
    {
        for (var i = 0; i < 5000 && !S.Progress.Dead; i++)
        {
            if (S.Main == null && S.Sub == null) return;
            if (S.Battle != null && !S.Progress.Dead)
            {
                // B41b（R05 Q416）：敌踪到期主动开战打断当前行动——打完；能继续原行动就继续，不能（撤退离地等）就退出原行动
                ReadDialogues();
                Fight();
                ReadDialogues();
                if (S.Progress.Dead || S.Battle != null || S.Main?.Status != ActionStatus.Paused || S.Sub != null) continue;
                if (E.ResumeBlockReason(S) == null) E.Resume(S);
                else { Log($"无法继续{S.Main.Label}：{E.ResumeBlockReason(S)}，退出"); E.Cancel(S); }
                continue;
            }
            var st = E.Advance(S).Status;
            if (st == SegmentStatus.Dialogue) { ReadDialogues(); continue; }
            if (st == SegmentStatus.Paused)
            {
                if (S.Main?.PauseReason == PauseReason.FireOut) { E.Cancel(S); return; }
                ReadDialogues();
                NeedsAsSub();
                E.Resume(S);
                continue;
            }
            if (st != SegmentStatus.Running && S.Main == null) return;
        }
    }

    private void NeedsAsSub()
    {
        for (var k = 0; k < 3; k++)
        {
            var r = NextNeed(true);
            if (r == null) return;
            if (!E.Start(S, r).Ok) return;
            Count(r);
            for (var i = 0; i < 100 && S.Sub != null && !S.Progress.Dead; i++) { ReadDialogues(); E.Advance(S); }
        }
    }

    private void AfterAction()
    {
        ReadDialogues();
        if (S.Battle != null && !S.Progress.Dead) Fight();
        HandleEncounter();
        ReadDialogues();
    }

    /// <summary>敌踪：默认绕行；绕不开就接敌。</summary>
    public void HandleEncounter()
    {
        for (var k = 0; k < 4 && S.Encounter != null && !S.Progress.Dead; k++)
        {
            if (E.Start(S, ActionRequest.Bypass()).Ok)
            {
                Bypasses++;
                Log("绕行敌踪");
                Drive();
                ReadDialogues();
                if (S.Battle != null) Fight();
                continue;
            }
            if (E.Engage(S).Ok) Fight();
        }
    }

    /// <summary>
    /// 简单战斗策略：轮到我方时，健康低于上限 30% 且可撤退就撤退；否则用第一项可用的攻击打第一个可选目标；
    /// 没有攻击就调整呼吸，再没有就结束行动。只调公开接口。
    /// </summary>
    public void Fight(bool allowRetreat = true)
    {
        Battles++;
        Log($"开战（{S.Battle?.Enemies.Count}敌）");
        var swapped = -1;
        for (var guard = 0; guard < 1500 && S.Battle != null && !S.Progress.Dead; guard++)
        {
            ReadDialogues();
            if (!E.AwaitingPlayer(S)) { E.BattleStep(S); continue; }
            var opts = E.BattleOptions(S);
            if (Verbose && guard % 10 == 0) Log($"第{S.Battle!.Round}轮 敌" + string.Join(" ", S.Battle.Enemies.Select(x => $"{x.EnemyId}@{x.Slot}:{x.Hp:0.0}{(x.Dead ? "死" : "")}")) + " 我" + string.Join(" ", S.Party.Select(a => $"{a}:{S.Actors[a].Stats.Health:0.0}"))
                + " 选项" + string.Join(" ", opts.Where(o => o.Available || o.Group == "attack").Select(o => $"{o.Action}/t{o.Targets.Count}{(o.Available ? "" : "/" + o.Reason)}")));
            BattleCommandOption? Opt(string id) => opts.FirstOrDefault(o => o.Action == id && o.Available);
            BattleCommand cmd;
            if (allowRetreat && S.Stats.Health < 0.3 * S.Stats.HealthMax && Opt("retreat") != null) { cmd = BattleCommand.Of("retreat"); Retreats++; }
            else if (opts.Where(o => o.Available && o.Group == "attack" && o.Targets.Count > 0).OrderByDescending(o => o.Ap).FirstOrDefault() is BattleCommandOption atk)
                cmd = BattleCommand.Of(atk.Action, atk.Targets.OrderBy(t => S.Battle!.Enemies.FirstOrDefault(x => x.Index == t)?.Hp ?? 999).First());
            else if (swapped != S.Battle!.Round * 100 + S.Battle.Cursor
                     && opts.Any(o => o.Group == "attack" && !o.Available && o.Reason != null && (o.Reason.Contains("射程") || o.Reason.Contains("耐久")))
                     && E.SelectWeaponPage(S, S.Battle.ActiveWeaponSlot == 1 ? 2 : 1).Ok)
            {
                swapped = S.Battle.Round * 100 + S.Battle.Cursor;
                Log($"切到武器{S.Battle.ActiveWeaponSlot}页");
                continue;
            }
            else if (Opt("reload") != null) cmd = BattleCommand.Of("reload");
            else if (Opt("breathe") != null) cmd = BattleCommand.Of("breathe");
            else cmd = BattleCommand.Of("end_turn");
            var er = E.ExecuteCommand(S, cmd);
            if (Verbose && guard < 30) Log($"命令 {cmd.Action} → {(er.Ok ? "ok" : er.Reason)}");
            if (!er.Ok) E.ExecuteCommand(S, BattleCommand.Of("end_turn"));
        }
        ReadDialogues();
        if (S.Battle != null && !S.Progress.Dead) throw Stuck($"战斗 1500 步仍未结束（第{S.Battle.Round}轮）");
        Log($"战斗结束（{(S.Progress.Dead ? "死亡" : "存活")}）");
    }

    /// <summary>把当前地面上的装备逐件穿到 actor 身上第一个合法槽（旧件放地面）。返回穿上的件数。</summary>
    public int EquipFromGround(string actorId)
    {
        var n = 0;
        foreach (var it in S.Items.Where(i => i.Owner == S.Ground && E.Item(i.ItemId).IsEquipment).ToList())
        {
            var choices = E.EquipChoices(S, actorId, it.InstanceId).Where(c => c.reason == null).Select(c => c.slot).ToList();
            var slot = choices.FirstOrDefault(sl => E.WornItem(S, actorId, sl) == null);
            if (slot == null) continue;
            if (E.Equip(S, actorId, it.InstanceId, slot, null, true).Ok) n++;
        }
        return n;
    }

    // ───────────── 需求：吃喝睡 ─────────────

    private bool Has(string id, bool sub) =>
        E.AvailableCount(S, id, !(sub && S.Main?.Type == ActionType.Travel), includeContainers: true) > 0;

    private void Count(ActionRequest r)
    {
        if (r.TargetId == "clean_water") WaterDrunk++; else FoodEaten++;
    }

    private ActionRequest? NextNeed(bool sub)
    {
        var st = S.Stats;
        if (st.Hydration < 60 && Has("clean_water", sub)) return ActionRequest.UseItem("clean_water");
        if (st.Satiety < 60)
            foreach (var f in new[] { "boiled_root", "cooked_root", "roast_meat", "berry", "canned_food", "canned_beans", "hardtack" })
                if (Has(f, sub)) return ActionRequest.UseItem(f);
        return null;
    }

    /// <summary>
    /// 行动之间：主角饿/渴先吃喝随身（含地面）真实物品；同行者饿/渴用同行食饮事务喂（FeedCompanions）；
    /// 两人随身都没有可用的就记一条缺食/缺水（不补给，B30 起无开发补给）；困了就地睡（能睡的话）。
    /// </summary>
    public void Needs()
    {
        if (S.Battle != null || S.Encounter != null || S.Progress.Dead) return;
        TakeBack();
        for (var k = 0; k < 6; k++)
        {
            var r = NextNeed(false);
            if (r == null) break;
            if (!Do(r)) break;
            Count(r);
        }
        if (S.Stats.Hydration < 45) Short(GameState.PlayerId, true, S.Stats.Hydration);
        if (S.Stats.Satiety < 45) Short(GameState.PlayerId, false, S.Stats.Satiety);
        FeedCompanions();
        if (S.Stats.Wakefulness < 25 || S.Stats.Energy < 0.25 * S.Stats.EnergyMax)
        {
            var mins = E.Config.Actions.SleepMinutes.Max();
            if (!Do(ActionRequest.Sleep(mins)))
                Do(ActionRequest.Rest(60));
        }
    }

    /// <summary>主角饿/渴而自己随身没有时，从同行者随身拿回一份（队员交接 Give，0 分钟，A10 §7.2）。</summary>
    private void TakeBack()
    {
        if (S.Main != null || S.Sub != null) return;
        var wants = new List<string>();
        if (S.Stats.Hydration < 60 && CarriedCount(GameState.PlayerId, "clean_water") == 0) wants.Add("clean_water");
        if (S.Stats.Satiety < 60 && CarriedCount(GameState.PlayerId, "canned_food") + CarriedCount(GameState.PlayerId, "canned_beans") == 0)
            wants.Add(S.Party.Any(a => a != GameState.PlayerId && CarriedCount(a, "canned_food") > 0) ? "canned_food" : "canned_beans");
        foreach (var id in wants)
        {
            var mate = S.Party.Where(a => a != GameState.PlayerId).OrderByDescending(a => CarriedCount(a, id)).FirstOrDefault();
            var it = mate == null ? null : S.Items.FirstOrDefault(i => i.ItemId == id && i.Owner == ItemInstance.CarriedOwner(mate));
            if (it == null) continue;
            var r = E.Give(S, mate!, GameState.PlayerId, it.InstanceId, 1, $"comp20-back-{++_seq}");
            Log(r.Ok ? $"从{E.ActorDisplayName(S, mate!)}拿回{E.ItemName(id)}×1" : $"从{E.ActorDisplayName(S, mate!)}拿回{id}被拒：{r.Reason}");
        }
    }

    private void Short(string actorId, bool water, double value)
    {
        var key = $"{actorId}|{water}|{Clock.Day(S.GameMinute)}";
        if (!_shortKeys.Add(key)) return;
        var who = E.ActorDisplayName(S, actorId);
        var line = $"{Clock.DayTime(S.GameMinute)} 在{E.LocName(Here)}：{who}{(water ? "水分" : "饱食")}{value:0}，"
                   + (actorId == GameState.PlayerId ? "主角随身" : $"你和{who}随身") + $"都没有可用的{(water ? "净水" : "食物")}";
        Shortages.Add(line);
        Ledger.Add("  · 【缺" + (water ? "水" : "食") + "】" + line);
    }

    private static bool CleanWater(FeedSourceInfo x) => x.ItemId == "clean_water" && (x.Kind == FeedSourceKind.Water || x.Kind == FeedSourceKind.PotWater);
    private static bool Food(FeedSourceInfo x) => x.Kind == FeedSourceKind.Food || x.Kind == FeedSourceKind.PotFood;

    /// <summary>
    /// 给同行者吃喝（B28 同行食饮事务，PLAN-Q232-01—04）：同地点同区存活同行者水分 &lt; 60 喂一份净水、饱食 &lt; 60 喂一份食物；
    /// 出物：净水先用主角随身（PLAN-Q232-02 主角净水卡给目标）、食物先用目标本人随身（PLAN-Q232-03 目标本人包可出物），没有再用另一人的
    /// （FeedSources 只列这两人随身含随身锅的合法批次），不喂脏水；每次 5 分钟走完再看下一个。
    /// </summary>
    public void FeedCompanions()
    {
        for (var k = 0; k < 12 && S.Battle == null && S.Encounter == null && !S.Progress.Dead && S.Main == null && S.Sub == null; k++)
        {
            var fed = false;
            foreach (var t in E.FeedTargets(S))
            {
                var st = t.Stats;
                var srcs = E.FeedSources(S, t.ActorId);
                FeedSourceInfo? pick = null;
                var water = false;
                if (st.Hydration < 60)
                {
                    pick = srcs.Where(CleanWater).OrderBy(x => x.SourceActorId == GameState.PlayerId ? 0 : 1).FirstOrDefault();
                    if (pick != null) water = true;
                    else if (st.Hydration < 45) Short(t.ActorId, true, st.Hydration);
                }
                if (pick == null && st.Satiety < 60)
                {
                    pick = srcs.Where(Food).OrderBy(x => x.SourceActorId == t.ActorId ? 0 : 1).ThenBy(x => x.ExpiresAt ?? int.MaxValue).FirstOrDefault();
                    if (pick == null && st.Satiety < 45) Short(t.ActorId, false, st.Satiety);
                }
                if (pick == null) continue;
                var f = new FeedRequest
                {
                    TargetActorId = t.ActorId, SourceActorId = pick.SourceActorId, InstanceId = pick.InstanceId,
                    PotInstanceId = pick.PotInstanceId, ConfirmStamp = pick.Stamp, RequestId = $"comp20-feed-{++_seq}",
                };
                var res = E.Feed(S, f);
                if (!res.Ok) { Log($"给{t.Name}吃喝被拒：{res.Reason}"); continue; }
                Log($"给{t.Name}{(water ? "饮用" : "食用")}{pick.ItemName}（{pick.SourceName}随身）");
                Drive();
                if (water) CompanionWater++; else CompanionFood++;
                if (pick.SourceActorId == GameState.PlayerId) FedFromPlayer++; else FedFromSelf++;
                FedBy.TryGetValue(t.Name, out var c);
                FedBy[t.Name] = water ? (c.Food, c.Water + 1) : (c.Food + 1, c.Water);
                ReadDialogues();
                fed = true;
                break;
            }
            if (!fed) return;
        }
    }

    private static readonly string[] Provisions = { "clean_water", "canned_food", "canned_beans" };

    /// <summary>
    /// 营区补给（真实采集，不是开发补给）：在营区广场把净水/罐头/豆罐头配给处当日库存能采的都采（每次 10 分钟，受随身卡位/重量限制），
    /// 地面剩的捡回随身；再把食水按人头平分交给每名同行者（Give，0 分钟）。返回本次采到的（净水、食物）份数。
    /// </summary>
    public (int Water, int Food) Provision()
    {
        var w0 = Stock("clean_water"); var f0 = Stock("canned_food") + Stock("canned_beans");
        foreach (var node in new[] { "h02_square_ration", "h02_square_beans", "h02_square_water" })   // 先食物后净水：卡位不够时先装食物
            for (var k = 0; k < 20; k++)
            {
                if (S.Resources.TryGetValue(node, out var rs) && rs.Available is int left && left <= 0) break;
                if (!Do(ActionRequest.Gather(node))) break;
                PickupGround();
            }
        PickupGround();
        Share();
        var got = (Stock("clean_water") - w0, Stock("canned_food") + Stock("canned_beans") - f0);
        Note($"{Clock.DayTime(S.GameMinute)} 营区配给处采集：净水+{got.Item1} 食物+{got.Item2}；现有（全队随身+广场地面）净水{Stock("clean_water")} 罐头{Stock("canned_food")} 豆罐头{Stock("canned_beans")}");
        return got;
    }

    /// <summary>全队随身加当前地面上的份数（营区囤货时地面也算）。</summary>
    public int Stock(string itemId) => Total(itemId) + S.Items.Where(i => i.ItemId == itemId && i.Owner == S.Ground).Sum(i => i.Count);

    /// <summary>
    /// 出发前装包：主角随身重复的同种工具（如两口金属锅，各占一张卡）留一件在地面腾卡位；地面上的食水捡起、按人头分给同行者；
    /// 主角卡位满而地面还有时，把主角一整叠食水交给卡位最空的同行者再捡，反复到地面捡完或谁都装不下。账本记每人随身。
    /// </summary>
    public void Pack()
    {
        var root = ItemInstance.CarriedOwner(GameState.PlayerId);
        foreach (var g in S.Items.Where(i => i.Owner == root && !Provisions.Contains(i.ItemId)).GroupBy(i => i.ItemId).Where(g => g.Count() > 1).ToList())
            foreach (var extra in g.Skip(1))
                if (E.DoInstant(S, InstantRequest.Drop(extra.InstanceId, extra.Count)).Ok) Note($"装包：随身重复的{E.ItemName(extra.ItemId)}留一件在{E.LocName(Here)}地面（腾出一张卡）");
        for (var k = 0; k < 8; k++)
        {
            PickupGround();
            Share();
            if (!S.Items.Any(i => i.Owner == S.Ground && Provisions.Contains(i.ItemId))) break;
            var mate = S.Party.Where(a => a != GameState.PlayerId && E.RootCardCountOf(S, a) < E.MaxRootCards).OrderBy(a => E.RootCardCountOf(S, a)).FirstOrDefault();
            var stack = S.Items.Where(i => i.Owner == root && Provisions.Contains(i.ItemId)).OrderBy(i => i.Count).FirstOrDefault();
            if (mate == null || stack == null || !E.Give(S, GameState.PlayerId, mate, stack.InstanceId, stack.Count, $"comp20-pack-{++_seq}").Ok) break;
        }
        Note($"装包：全队随身 净水{Total("clean_water")} 罐头{Total("canned_food")} 豆罐头{Total("canned_beans")}；"
             + string.Join("；", S.Party.Select(a => $"{E.ActorDisplayName(S, a)}随身{E.RootCardCountOf(S, a)}/{E.MaxRootCards}张 {E.CarriedGramsOf(S, a) / 1000.0:0.00}kg"
                 + $"（{string.Join("、", S.Items.Where(i => i.Owner == ItemInstance.CarriedOwner(a)).Select(i => $"{E.ItemName(i.ItemId)}×{i.Count}"))}）"))
             + (S.Items.Any(i => i.Owner == S.Ground && Provisions.Contains(i.ItemId)) ? "；地面还剩" + string.Join("、", S.Items.Where(i => i.Owner == S.Ground && Provisions.Contains(i.ItemId)).Select(i => $"{E.ItemName(i.ItemId)}×{i.Count}")) : ""));
    }

    /// <summary>全队随身（主角与同行者，含随身锅/容器里）这种物品的份数。</summary>
    public int Total(string itemId) => S.Party.Sum(a => CarriedCount(a, itemId));

    private int CarriedCount(string actorId, string itemId) =>
        S.Items.Where(i => i.ItemId == itemId && E.PlaceOf(S, i) == ItemInstance.CarriedOwner(actorId)).Sum(i => i.Count);

    /// <summary>地面上的净水/罐头/豆罐头捡回主角随身（能捡多少捡多少；卡位不够时先捡食物）。</summary>
    private void PickupGround()
    {
        foreach (var it in S.Items.Where(i => i.Owner == S.Ground && Provisions.Contains(i.ItemId)).OrderBy(i => i.ItemId == "clean_water" ? 1 : 0).ToList())
            for (var n = it.Count; n > 0; n--)
                if (E.DoInstant(S, InstantRequest.Pickup(it.InstanceId, n)).Ok) break;
    }

    /// <summary>把主角随身的净水与罐头按人头平分交给同行者（同行者已有的算进去）。</summary>
    public void Share()
    {
        var mates = S.Party.Where(a => a != GameState.PlayerId).ToList();
        if (mates.Count == 0) return;
        foreach (var id in Provisions)
        {
            var share = Total(id) / (mates.Count + 1);
            foreach (var m in mates)
            {
                var want = share - CarriedCount(m, id);
                var src = S.Items.FirstOrDefault(i => i.ItemId == id && i.Owner == ItemInstance.CarriedOwner(GameState.PlayerId));
                if (want <= 0 || src == null) continue;
                var r = E.Give(S, GameState.PlayerId, m, src.InstanceId, Math.Min(want, src.Count), $"comp20-give-{++_seq}");
                if (!r.Ok) Log($"交给{E.ActorDisplayName(S, m)} {id}×{want} 被拒：{r.Reason}");
            }
        }
    }

    /// <summary>
    /// 出发前换装（开发入口，属“开发装备”一类，不在 PLAN-Q232-04 范围内）：全队穿戴中耐久低于上限一半的装备，
    /// 各换一件开发入口新造的同款同品质（满耐久，旧件留在地面），代替维修或重新制作；换了什么写进账本【开发入口】。
    /// </summary>
    public void RefitWorn(string label, double below = 0.5)
    {
        var done = new List<string>();
        foreach (var a in S.Party.ToList())
            foreach (var it in S.Items.Where(i => i.IsWornBy(a) && i.MaxDurability is > 0 && i.Durability < below * i.MaxDurability).ToList())
            {
                if (!ItemInstance.TryEquip(it.Owner, out _, out var slot)) continue;
                var fresh = E.DebugCreateEquipment(S, it.ItemId, it.Quality ?? "white", S.Ground);
                var r = E.Equip(S, a, fresh.InstanceId, slot, null, true);
                if (r.Ok) done.Add($"{E.ActorDisplayName(S, a)}的{E.ItemName(it.ItemId)}（{it.Durability:0.0}/{it.MaxDurability:0.0}）");
                else Log($"换装 {it.ItemId} 被拒：{r.Reason}");
            }
        if (done.Count > 0) Shortcut($"{label}：耐久不足一半的穿戴装备换成开发入口新造的同款满耐久装备（代替维修或重新制作）：{string.Join("、", done)}");
    }

    /// <summary>固定战实打（不撤退）：账本记敌人、全队开战前后健康与结果；主角死亡抛出卡住。</summary>
    public void FixedFight()
    {
        var names = string.Join("、", E.FixedEnemiesAt(S, Here).Select(w => E.Enemy(w.EnemyId).Name));
        var before = PartyHealth();
        var loc = E.LocName(Here);
        var t0 = S.GameMinute;
        if (!E.EngageFixed(S).Ok) throw Stuck($"固定战接不了：{E.CanEngageFixed(S)}");
        Fight(allowRetreat: false);
        var won = E.FixedStateAt(S, Here)?.Won == true || !E.FixedEnemiesAt(S, Here).Any();
        FixedResults.Add($"{Clock.DayTime(t0)} {loc}（{names}）{(S.Progress.Dead ? "主角死亡" : won ? "胜" : "未胜")}：{before} → {PartyHealth()}");
        Ledger.Add($"  · 固定战实打 {FixedResults[^1]}");
        if (S.Progress.Dead) throw Stuck($"{loc}固定战主角死亡");
    }

    /// <summary>实打的固定战结果（时间、地点、敌人、全队前后健康）。</summary>
    public readonly List<string> FixedResults = new();

    /// <summary>每条线的走通摘要（线名、起止世界时间、同行、给同行者吃喝次数、固定战）。</summary>
    public readonly List<string> LineSummaries = new();

    public (int T, int Cf, int Cw, int Fx, int Pf, int Pw) Mark() => (S.GameMinute, CompanionFood, CompanionWater, FixedResults.Count, FoodEaten, WaterDrunk);

    /// <summary>记一条线的摘要（从 Mark 起到现在）。</summary>
    public void LineSummary(string name, (int T, int Cf, int Cw, int Fx, int Pf, int Pw) m, IEnumerable<string> mates)
    {
        var line = $"{name}：{Clock.DayTime(m.T)}→{Clock.DayTime(S.GameMinute)}（{(S.GameMinute - m.T) / 60.0:0.#}小时）"
                   + $"｜同行{string.Join("、", mates.Select(a => E.ActorDisplayName(S, a)))}，结束时{PartyText()}"
                   + $"｜主角吃{FoodEaten - m.Pf}喝{WaterDrunk - m.Pw}｜给同行者吃{CompanionFood - m.Cf}份 喝{CompanionWater - m.Cw}份"
                   + $"｜固定战实打{FixedResults.Count - m.Fx}场：{string.Join("；", FixedResults.Skip(m.Fx))}";
        LineSummaries.Add(line);
        Note("【走通】" + line);
    }

    private string PartyHealth() => string.Join(" ", S.Party.Select(a => $"{E.ActorDisplayName(S, a)}H{S.Actor(a).Stats.Health:0.0}"));

    // ───────────── 移动 ─────────────

    /// <summary>未开通的边靠探索哪一格能开（探索类门槛）；不是探索类返回 null。</summary>
    private static string? ExploreNodeOf(EdgeConfig e)
    {
        var g = e.Gate ?? "";
        if (g == "from_explore100" || g == "instance_from_explore100") return e.A;
        if (g == "parent_explore100") return e.DungeonParent;
        foreach (var n in new[] { "field", "village", "hardware", "woods" })
            if (g.StartsWith(n)) return n;
        return null;
    }

    /// <summary>从 cur 出发这条边现在能走，或按玩家能做的事（探索 cur、在守卫/记录格接战或领取）可能打开。</summary>
    private bool Passable(EdgeConfig e, string cur)
    {
        if (!e.TraversableFrom(cur)) return false;
        if (E.EdgeOpen(S, e)) return true;
        if (ExploreNodeOf(e) == cur) return true;
        if (e.Gate == "guardian_defeated" || e.Gate == "instance_complete" || e.Gate == "clear_checkpoint") return true;
        return false;
    }

    private List<string>? Path(string to)
    {
        var dist = new Dictionary<string, int> { [Here] = 0 };
        var prev = new Dictionary<string, string>();
        var open = new SortedSet<(int, string)> { (0, Here) };
        while (open.Count > 0)
        {
            var (d, cur) = open.Min;
            open.Remove(open.Min);
            if (cur == to) break;
            foreach (var e in E.Config.Edges.Where(e => e.Touches(cur)))
            {
                if (!Passable(e, cur)) continue;
                var nx = e.Other(cur);
                var nd = d + e.BaseMinutes + (E.EdgeOpen(S, e) ? 0 : 60);
                if (dist.TryGetValue(nx, out var old) && old <= nd) continue;
                if (dist.ContainsKey(nx)) open.Remove((old, nx));
                dist[nx] = nd;
                prev[nx] = cur;
                open.Add((nd, nx));
            }
        }
        if (!dist.ContainsKey(to)) return null;
        var path = new List<string>();
        for (var c = to; c != Here; c = prev[c]) path.Insert(0, c);
        return path;
    }

    /// <summary>
    /// 沿数据路线一格一格真实移动（每步重算：只走已开通的边，或从这一格探索/接战/领取可能打开的边）：
    /// 下一条边没开、且当前地点探索未满 100% 就先探索；当前地点有固定战挡路就接战；地牢危险房间带确认。
    /// </summary>
    public void Go(string to)
    {
        for (var hops = 0; Here != to; hops++)
        {
            if (hops > 200) throw Stuck($"去 {to} 走了太多步");
            var path = Path(to) ?? throw Stuck($"没有能走或能开的路到 {to}");
            var step = path[0];
            var prepared = false;
            for (var tries = 0; ; tries++)
            {
                if (S.Progress.Dead) throw Stuck("角色死亡");
                Needs();
                var req = ActionRequest.Travel(step, E.NeedsExpeditionConfirm(S, step));
                var why = E.CanStart(S, req);
                if (why == null && !prepared && E.FixedBattleAt(step) != null && E.FixedStateAt(S, step)?.Won != true
                    && E.CanStart(S, ActionRequest.Rest(60)) == null && (MinHealthFrac() < 0.8 || S.Stats.Energy < 0.8 * S.Stats.EnergyMax))
                {
                    prepared = true;
                    var used = Recover(0.8, 48, 0.9);
                    Log($"固定战前在{Here}休整{used}分钟");
                    PrepMinutes += used;
                    why = E.CanStart(S, req);
                }
                if (why == null)
                {
                    Do(req, required: true);
                    if (S.Progress.Dead) throw Stuck("角色死亡");
                    if (Here == step || tries > 30) break;
                    continue;
                }
                if (tries > 400) throw Stuck($"去 {step} 被拒：{why}");
                if (E.CanEngageFixed(S) == null)
                {
                    FixedFight();
                    continue;
                }
                if (S.Locations.TryGetValue(Here, out var ls) && ls.Progress < 100 && E.CanStart(S, ActionRequest.Explore()) == null)
                {
                    Do(ActionRequest.Explore());
                    continue;
                }
                if (Path(to) is List<string> p2 && p2.Count > 0 && p2[0] != step) break;   // 这条路走不通，换一条
                throw Stuck($"去 {step} 被拒：{why}");
            }
        }
    }

    /// <summary>就地探索到 100%（门槛要求时用）。</summary>
    public void ExploreFull()
    {
        for (var k = 0; k < 400 && S.Locations[Here].Progress < 100; k++)
        {
            Needs();
            if (E.CanEngageFixed(S) == null)
            {
                FixedFight();
                continue;
            }
            if (!Do(ActionRequest.Explore())) throw Stuck($"探索 {Here} 被拒：{E.CanStart(S, ActionRequest.Explore())}");
        }
    }

    /// <summary>
    /// 在当前（室内/有遮蔽）地点休息或睡觉直到全队（主角与同行者）健康都回到上限的 frac（S12：有遮蔽 rest/sleep 且 N/W≥40 每小时 +0.2）；最多 maxHours 小时。返回用时分钟。
    /// </summary>
    public double MinHealthFrac() => S.Party.Min(a => S.Actor(a).Stats.Health / Math.Max(1e-9, S.Actor(a).Stats.HealthMax));

    public int Recover(double frac = 0.85, int maxHours = 96, double energyFrac = 0)
    {
        var t0 = S.GameMinute;
        for (var k = 0; k < maxHours * 2 && (MinHealthFrac() < frac || S.Stats.Energy < energyFrac * S.Stats.EnergyMax) && !S.Progress.Dead; k++)
        {
            Needs();
            if (S.Party.Any(a => S.Actor(a).Stats.Satiety < 40 || S.Actor(a).Stats.Hydration < 40))
            {
                Log("有人饱食或水分低于40、随身没有可补的，休整不再回血，停止休整");
                break;
            }
            if (S.Stats.Wakefulness < 70 || S.Stats.Energy < 0.6 * S.Stats.EnergyMax)
            {
                if (Do(ActionRequest.Sleep(E.Config.Actions.SleepMinutes.Max()))) continue;
            }
            if (!Do(ActionRequest.Rest(60))) throw Stuck($"休息被拒：{E.CanStart(S, ActionRequest.Rest(60))}");
        }
        return S.GameMinute - t0;
    }

    /// <summary>等待到某一绝对分钟（期间照常吃喝睡）。</summary>
    public void WaitUntil(int minute)
    {
        for (var k = 0; k < 2000 && S.GameMinute < minute && !S.Progress.Dead; k++)
        {
            Needs();
            if (S.GameMinute >= minute) break;
            var req = ActionRequest.Wait(minute - S.GameMinute >= 60 ? 60 : 30);   // 等待只有 30/60 分钟两档
            if (!Do(req)) throw Stuck($"等待被拒：{E.CanStart(S, req)}");
        }
    }
}
