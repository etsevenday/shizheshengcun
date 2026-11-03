using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// COMP20 端到端路线驱动（开发计划 v0.9 B25）：只通过 GameEngine 公开接口像玩家一样移动、探索、接战、吃喝与睡觉，
/// 逐段记下世界分钟、饱食/水分变化、随身卡数与重量、等待分钟，以及哪些物资是测试“开发补给”给的（不冒充野外所得）。
/// 走不通时抛出带原因的异常，由调用的测试决定失败或 Ignore。
/// </summary>
public sealed class Comp20Driver
{
    public readonly GameEngine E;
    public readonly GameState S;
    public readonly List<string> Ledger = new();
    public readonly List<string> Trace = new();

    /// <summary>测试替玩家补的食水份数（随身没有可吃可喝时才补，各记一份）。</summary>
    public int DevFood { get; private set; }
    public int DevWater { get; private set; }
    public int FoodEaten { get; private set; }
    public int WaterDrunk { get; private set; }
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

    public Comp20Driver(GameEngine e, GameState s) { E = e; S = s; }

    /// <summary>挡路的固定战用开发入口直接记胜（写进账本），不实打。</summary>
    public bool DevWinFixedBattles { get; set; }

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
        var f0 = FoodEaten; var d0 = WaterDrunk; var df0 = DevFood; var dw0 = DevWater;
        var wait0 = WaitMinutes; var sl0 = SleepMinutes; var b0 = Battles;
        act();
        Ledger.Add($"{label}｜{Clock.DayTime(t0)}→{Clock.DayTime(S.GameMinute)}（{S.GameMinute - t0}分钟）"
            + $"｜饱食{n0:0}→{S.Stats.Satiety:0} 水分{w0:0}→{S.Stats.Hydration:0}"
            + $"｜吃{FoodEaten - f0}份 喝{WaterDrunk - d0}份（其中开发补给 食{DevFood - df0}/水{DevWater - dw0}）"
            + $"｜随身{E.RootCardCount(S)}/{E.MaxRootCards}张 {E.CarriedGrams(S) / 1000.0:0.00}kg"
            + $"｜等待{WaitMinutes - wait0} 睡眠{SleepMinutes - sl0} 战斗{Battles - b0}｜同行{string.Join("、", S.Party.Select(a => E.ActorDisplayName(S, a)))}");
    }

    public void Note(string text) => Ledger.Add("  · " + text);

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
                cmd = BattleCommand.Of(atk.Action, atk.Targets[0]);
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
            foreach (var f in new[] { "boiled_root", "cooked_root", "roast_meat", "berry", "canned_food", "hardtack" })
                if (Has(f, sub)) return ActionRequest.UseItem(f);
        return null;
    }

    /// <summary>行动之间：饿/渴先吃喝（随身没有时由测试补一份并记账）；困了就地睡（能睡的话）。</summary>
    public void Needs()
    {
        if (S.Battle != null || S.Encounter != null || S.Progress.Dead) return;
        for (var k = 0; k < 6; k++)
        {
            var r = NextNeed(false);
            if (r == null) break;
            if (!Do(r)) break;
            Count(r);
        }
        if (S.Stats.Hydration < 45 && E.AvailableCount(S, "clean_water", true, includeContainers: true) == 0)
        {
            E.DebugGive(S, "clean_water", 1);
            DevWater++;
            if (Do(ActionRequest.UseItem("clean_water"))) WaterDrunk++;
        }
        if (S.Stats.Satiety < 45 && E.AvailableCount(S, "canned_food", true, includeContainers: true) == 0)
        {
            E.DebugGive(S, "canned_food", 1);
            DevFood++;
            if (Do(ActionRequest.UseItem("canned_food"))) FoodEaten++;
        }
        if (S.Stats.Wakefulness < 25 || S.Stats.Energy < 0.25 * S.Stats.EnergyMax)
        {
            var mins = E.Config.Actions.SleepMinutes.Max();
            if (!Do(ActionRequest.Sleep(mins)))
                Do(ActionRequest.Rest(60));
        }
    }

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
                    && E.CanStart(S, ActionRequest.Rest(60)) == null && (S.Stats.Health < 0.8 * S.Stats.HealthMax || S.Stats.Energy < 0.8 * S.Stats.EnergyMax))
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
                if (E.CanEngageFixed(S) == null && DevWinFixedBattles)
                {
                    var names = string.Join("、", E.FixedEnemiesAt(S, Here).Select(w => E.Enemy(w.EnemyId).Name));
                    E.DebugWinFixedHere(S);
                    ReadDialogues();
                    Shortcut($"{E.LocName(Here)}固定战（{names}）用开发入口“固定战记为胜利”");
                    continue;
                }
                if (E.CanEngageFixed(S) == null)
                {
                    if (!E.EngageFixed(S).Ok) throw Stuck($"固定战接不了：{why}");
                    Fight(allowRetreat: false);
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
                if (DevWinFixedBattles) { E.DebugWinFixedHere(S); ReadDialogues(); Shortcut($"{E.LocName(Here)}固定战用开发入口“固定战记为胜利”"); }
                else { if (!E.EngageFixed(S).Ok) throw Stuck("固定战接不了"); Fight(allowRetreat: false); }
                if (S.Progress.Dead) throw Stuck("角色死亡");
                continue;
            }
            if (!Do(ActionRequest.Explore())) throw Stuck($"探索 {Here} 被拒：{E.CanStart(S, ActionRequest.Explore())}");
        }
    }

    /// <summary>
    /// 在当前（室内/有遮蔽）地点休息或睡觉直到健康回到上限的 frac（S12：有遮蔽 rest/sleep 且 N/W≥40 每小时 +0.2）；最多 maxHours 小时。返回用时分钟。
    /// </summary>
    public int Recover(double frac = 0.85, int maxHours = 96, double energyFrac = 0)
    {
        var t0 = S.GameMinute;
        for (var k = 0; k < maxHours * 2 && (S.Stats.Health < frac * S.Stats.HealthMax || S.Stats.Energy < energyFrac * S.Stats.EnergyMax) && !S.Progress.Dead; k++)
        {
            Needs();
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
            var left = minute - S.GameMinute;
            if (!Do(ActionRequest.Wait(Math.Min(60, Math.Max(5, left)))))
                throw Stuck($"等待被拒：{E.CanStart(S, ActionRequest.Wait(30))}");
        }
    }
}
