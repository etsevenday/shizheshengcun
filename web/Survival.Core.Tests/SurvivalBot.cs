using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 求生案 07“数值可行性的检查方法”的脚本化玩家。只通过 GameEngine 的公开接口行动，
/// 每次出行都返回营地放下物资，晚间回营地睡眠。
/// </summary>
public sealed class SurvivalBot
{
    public enum Route { StreamAndRoots, BerriesAndCaches }

    private readonly GameEngine E;
    private readonly GameState S;
    private readonly Route _route;
    public readonly List<string> Trace = new();
    private const string Camp = "camp";

    private static readonly HashSet<string> Keep = new() { "pot", "lighter", "hatchet", "stone_knife" };
    private static readonly HashSet<string> Junk = new() { "scrap", "parts", "hide" };

    /// <summary>战斗策略：false 时遇敌踪一律绕行；true 时接敌，并主动接近出行地点的可见敌人（体力与健康充足时）。</summary>
    private readonly bool _fight;

    public SurvivalBot(GameEngine e, GameState s, Route route, bool fight = false)
    {
        E = e;
        S = s;
        _route = route;
        _fight = fight;
    }

    public int Battles { get; private set; }
    public int Retreats { get; private set; }

    /// <summary>
    /// 简单战斗策略：轮到自己时，健康低于上限30%撤退；有出血且有绷带先包扎；体力不够普通攻击就调整呼吸或勉强拳击；
    /// 否则用当前武器普通攻击第一个可打的目标；都不行就结束行动。只调公开接口。
    /// </summary>
    private void FightBattle()
    {
        Battles++;
        for (var guard = 0; guard < 400 && S.Battle != null && !S.Progress.Dead; guard++)
        {
            ReadDialogues();
            if (!E.AwaitingPlayer(S)) { E.BattleStep(S); continue; }
            var opts = E.BattleOptions(S);
            BattleCommandOption? Opt(string id) => opts.FirstOrDefault(o => o.Action == id && o.Available);
            BattleCommand cmd;
            if (S.Stats.Health < 0.3 * S.Stats.HealthMax && Opt("retreat") != null) { cmd = BattleCommand.Of("retreat"); Retreats++; }
            else if (S.Body.BleedTriggers > 0 && Opt("bandage") != null) cmd = BattleCommand.Of("bandage");
            else if (Opt("basic_attack") is BattleCommandOption atk && atk.Targets.Count > 0) cmd = BattleCommand.Of("basic_attack", atk.Targets[0]);
            else if (Opt("reload") != null) cmd = BattleCommand.Of("reload");
            else if (Opt("breathe") != null) cmd = BattleCommand.Of("breathe");
            else if (Opt("desperate_punch") is BattleCommandOption dp && dp.Targets.Count > 0) cmd = BattleCommand.Of("desperate_punch", dp.Targets[0]);
            else cmd = BattleCommand.Of("end_turn");
            if (!E.ExecuteCommand(S, cmd).Ok) E.ExecuteCommand(S, BattleCommand.Of("end_turn"));
        }
        Log($"战斗结束 H{S.Stats.Health:0.00}");
        ReadDialogues();
        PickUpUseful();
    }

    private void MaybeHunt()
    {
        if (!_fight || S.Battle != null || S.Main != null) return;
        if (S.Stats.Health < 0.8 * S.Stats.HealthMax || S.Stats.Energy < 0.5 * S.Stats.EnergyMax) return;
        var target = E.VisibleEnemiesAt(S, Here).FirstOrDefault(w => E.Enemy(w.EnemyId).Rarity == "common");
        if (target == null) return;
        EquipBest();
        if (E.Approach(S, target.Id).Ok) FightBattle();
    }

    private void EquipBest()
    {
        var best = E.CarriedWeapons(S).OrderByDescending(i => E.Weapon(i.ItemId)!.DamageMax).FirstOrDefault(i => GameEngine.HasWear(i, 1));
        E.EquipWeapon(S, best?.InstanceId);
    }

    private int Tod => Clock.MinuteOfDay(S.GameMinute);

    /// <summary>脚本的休息线：体力低于上限的30%（v0.6 起上限20，原写死30/100）。</summary>
    private bool LowEnergy => S.Stats.Energy < 0.3 * S.Stats.EnergyMax;
    private string Here => S.CurrentLocation;

    public void Play(int untilMinute)
    {
        var guard = 0;
        while (S.GameMinute < untilMinute && !S.Progress.Dead && guard++ < 20000)
        {
            var before = S.GameMinute;
            HandleEncounter();
            Step();
            if (S.GameMinute == before && !S.Progress.Dead) Do(ActionRequest.Wait(30));
        }
    }

    private void Log(string msg) => Trace.Add($"{Clock.DayTime(S.GameMinute)} H{S.Stats.Health:0.00} N{S.Stats.Satiety:0} W{S.Stats.Hydration:0} E{S.Stats.Energy:0.00} S{S.Stats.Wakefulness:0} M{S.Stats.Morale:0} I{S.Body.Immunity:0.0} V{S.Body.VirusLoad:0.0} | {msg}");

    // ───────────── 执行 ─────────────

    /// <summary>像玩家一样读完当前所有对话（Q10：读完才能操作；不耗时）。</summary>
    private void ReadDialogues()
    {
        for (var k = 0; k < 64 && E.DialogueLocked(S); k++)
            E.AckDialogue(S, E.CurrentDialogueLine(S)!.LineId);
    }

    private bool Do(ActionRequest r)
    {
        ReadDialogues();
        var res = E.Start(S, r);
        if (!res.Ok) return false;
        Log($"{r.Type} {r.TargetId}{(r.Minutes > 0 ? " " + r.Minutes : "")}");
        Drive();
        HandleStory();
        HandleEncounter();
        return true;
    }

    /// <summary>
    /// 系统策划总册 v1.0：首次到达荒地强制首战（不论战斗策略都要打，撤退条件同普通战斗）；读完饱饱对白（不耗时）；
    /// 首战胜利后像玩家一样把教学活性物质吞噬融合（5 分钟）。
    /// </summary>
    private void HandleStory()
    {
        ReadDialogues();
        if (S.Battle != null && !S.Progress.Dead)
        {
            Log(S.Battle.Tutorial ? "首战" : "战斗");
            EquipBest();
            FightBattle();
        }
        if (S.Progress.Dead || S.Main != null || S.Battle != null) return;
        var sub = S.Items.FirstOrDefault(i => i.Source?.TutorialReward == true && (i.Owner == Here || i.IsCarried));
        if (sub != null && E.Start(S, ActionRequest.Fuse(sub.InstanceId)).Ok)
        {
            Log("吞噬融合教学活性物质");
            Drive();
        }
        ReadDialogues();
    }

    /// <summary>v0.6：探索/清理后发现敌踪时，脚本选择绕行10分钟（不打架），之后60分钟保护期。</summary>
    private void HandleEncounter()
    {
        if (S.Encounter == null || S.Progress.Dead) return;
        if (_fight && S.Stats.Health >= 0.6 * S.Stats.HealthMax)
        {
            Encounters++;
            EquipBest();
            if (E.Engage(S).Ok) { FightBattle(); return; }
        }
        if (E.Start(S, ActionRequest.Bypass()).Ok)
        {
            Encounters++;
            Log("Bypass 敌踪");
            Drive();
        }
    }

    public int Encounters { get; private set; }

    private void Drive()
    {
        for (var i = 0; i < 3000 && !S.Progress.Dead; i++)
        {
            if (S.Main == null && S.Sub == null) return;
            var st = E.Advance(S).Status;
            if (st == SegmentStatus.Dialogue) { ReadDialogues(); continue; }
            if (st == SegmentStatus.Paused) HandlePause();
            else if (st != SegmentStatus.Running) { if (S.Main == null) return; }
        }
    }

    private void DriveSub()
    {
        for (var i = 0; i < 100 && S.Sub != null && !S.Progress.Dead; i++) { ReadDialogues(); E.Advance(S); }
    }

    private void HandlePause()
    {
        var m = S.Main;
        if (m == null) return;
        if (m.PauseReason == PauseReason.FireOut)
        {
            E.DoInstant(S, InstantRequest.AddFuel());
            if (E.Start(S, IgniteRequest()).Ok) DriveSub();
            if (!S.Locations[Here].Facility.FireLit) { E.Cancel(S); Log("缺燃料，取消加工"); return; }
            E.Resume(S);
            return;
        }
        SupplyAsSub();
        E.Resume(S);
    }

    private void SupplyAsSub()
    {
        for (var k = 0; k < 4; k++)
        {
            var req = NextNeed(sub: true);
            if (req == null || !E.Start(S, req).Ok) return;
            DriveSub();
        }
    }

    // ───────────── 需求 ─────────────

    private ActionRequest? NextNeed(bool sub)
    {
        var st = S.Stats;
        var allowGround = !(sub && S.Main?.Type == ActionType.Travel);
        bool Has(string id) => E.AvailableCount(S, id, allowGround, includeContainers: true) > 0;

        if (st.Hydration < 60 && Has("clean_water")) return ActionRequest.UseItem("clean_water");
        if (st.Hydration < 12 && Has("dirty_water")) return ActionRequest.UseItem("dirty_water");

        // 快要腐败的野果先吃
        var berry = E.UsableBatches(S, "berry", allowGround).FirstOrDefault();
        if (berry != null && st.Satiety < 90 && berry.ExpiresAt - S.GameMinute < 8 * 60) return ActionRequest.UseInstance(berry.InstanceId);

        if (st.Satiety < 60)
        {
            foreach (var f in new[] { "boiled_root", "cooked_root", "berry", "canned_food" })
                if (Has(f)) return ActionRequest.UseItem(f);
        }
        if (st.Health < 0.7 * st.HealthMax && Has("bandage") && S.BandageReadyAt <= S.GameMinute) return ActionRequest.UseItem("bandage");
        return null;
    }

    private void Needs()
    {
        for (var k = 0; k < 6; k++)
        {
            var r = NextNeed(sub: false);
            if (r == null || !Do(r)) return;
        }
    }

    // ───────────── 移动与物资 ─────────────

    private List<string>? Path(string to)
    {
        var prev = new Dictionary<string, string?> { [Here] = null };
        var q = new Queue<string>();
        q.Enqueue(Here);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == to) break;
            foreach (var e in E.Config.Edges.Where(e => e.Touches(cur) && S.UnlockedEdges.Contains(e.Id)))
            {
                var nx = e.Other(cur);
                if (prev.ContainsKey(nx)) continue;
                prev[nx] = cur;
                q.Enqueue(nx);
            }
        }
        if (!prev.ContainsKey(to)) return null;
        var path = new List<string>();
        for (var c = to; c != Here; c = prev[c]!) path.Insert(0, c);
        return path;
    }

    private bool GoTo(string loc)
    {
        if (Here == loc) return true;
        var p = Path(loc);
        if (p == null) return false;
        foreach (var step in p)
        {
            Needs();
            if (!Do(ActionRequest.Travel(step)) || Here != step) return false;
            MaybeHunt();
        }
        return true;
    }

    private void PickUpUseful()
    {
        foreach (var inst in S.Items.Where(i => i.Owner == Here && i.ReservedBy == null && !Junk.Contains(i.ItemId)).ToList())
        {
            if (E.IsSpoiled(S, inst)) continue;
            var w = E.Item(inst.ItemId).WeightGrams;
            var room = 18000 - E.CarriedGrams(S);
            var n = Math.Min(inst.Count, Math.Max(0, room / w));
            if (n > 0) E.DoInstant(S, InstantRequest.Pickup(inst.InstanceId, n));
        }
    }

    private void DropAtCamp()
    {
        foreach (var inst in S.Items.Where(i => i.IsCarried && !Keep.Contains(i.ItemId)).ToList())
            E.DoInstant(S, InstantRequest.Drop(inst.InstanceId, inst.Count));
        // 出行携带一份净水
        var w = S.Items.FirstOrDefault(i => i.Owner == Camp && i.ItemId == "clean_water" && i.ReservedBy == null);
        if (w != null) E.DoInstant(S, InstantRequest.Pickup(w.InstanceId, 1));
    }

    private void ReturnToCamp()
    {
        PickUpUseful();
        GoTo(Camp);
        DropAtCamp();
    }

    /// <summary>随身与营地的存量，锅里的水按锅所在计入。</summary>
    private int Stock(string id) => S.Items.Where(i => i.ItemId == id && !E.IsSpoiled(S, i))
        .Where(i => { var p = E.PlaceOf(S, i); return p == ItemInstance.Carried || p == Camp; })
        .Sum(i => i.Count);

    /// <summary>点火必须选定一把打火器（策划答复 2026-10-24 第 1 条）：脚本用随身或地面上的第一把。</summary>
    private ActionRequest IgniteRequest() => ActionRequest.Ignite(E.AccessibleTool(S, "lighter")?.InstanceId);

    private ItemInstance? CampPot() => S.Items.FirstOrDefault(i => i.ItemId == "pot" && E.PlaceOf(S, i) is var p && (p == ItemInstance.Carried || p == Camp));

    /// <summary>v0.4：取出火堆与锅里的成品放到营地地面，锅里未被预留的净水倒到地面（出行时才能带走）。</summary>
    private void CollectFromFire()
    {
        foreach (var done in S.Items.Where(i => (i.InFacility || i.InContainer) && (i.ItemId == "cooked_root" || i.ItemId == "boiled_root" || E.IsSpoiled(S, i))).ToList())
            if (E.PlaceOf(S, done) == Camp) E.DoInstant(S, InstantRequest.TakeOut(done.InstanceId, Camp));
        var pot = CampPot();
        if (pot == null) return;
        foreach (var w in E.PotLiquids(S, pot.InstanceId).Where(i => i.ItemId == "clean_water").ToList())
        {
            var free = E.PotFreeWaterUnits(S, w);
            if (free > 0) E.DoInstant(S, InstantRequest.Decant(w.InstanceId, Camp, Math.Min(free, w.Count)));
        }
    }

    /// <summary>不自动补水：把可用的脏水装进锅（半锅也能烧）。</summary>
    private void FillPotWithDirtyWater(ItemInstance pot)
    {
        foreach (var w in E.UsableBatches(S, "dirty_water", true))
        {
            var space = E.PotConfig(pot).CapacityUnits - E.PotUnits(S, pot.InstanceId);
            if (space <= 0 || E.PotLiquid(S, pot.InstanceId) == "clean_water") break;
            E.DoInstant(S, InstantRequest.FillPot(w.InstanceId, pot.InstanceId, Math.Min(space, w.Count)));
        }
    }

    /// <summary>
    /// v0.4 营地加工：有脏水就把锅放进火堆烧水；生块根放进空加工位直接烤（烤制无需净水，水分−2 由饮水补足）；
    /// 火堆有东西在加工时保持燃烧并等待30分钟推进世界时间，没有就熄火省燃料。
    /// </summary>
    private bool TendFire()
    {
        var fac = S.Locations[Camp].Facility;
        CollectFromFire();
        var pot = CampPot();
        var dirtyOutside = E.AvailableCount(S, "dirty_water", true);
        if (pot != null)
        {
            var inFire = pot.InFacility;
            var potDirty = E.PotLiquid(S, pot.InstanceId) == "dirty_water";
            if (dirtyOutside > 0 || potDirty)
            {
                FillPotWithDirtyWater(pot);
                if (!inFire) E.DoInstant(S, InstantRequest.PutInFire(pot.InstanceId));
            }
            else if (inFire && E.PotFood(S, pot.InstanceId).Count == 0)
                E.DoInstant(S, InstantRequest.TakeOut(pot.InstanceId, ItemInstance.Carried));
        }
        foreach (var root in E.UsableBatches(S, "raw_root", true).ToList())
        {
            var free = E.FireFreeSlots(S, Camp);
            if (free <= 0) break;
            E.DoInstant(S, InstantRequest.PutInFire(root.InstanceId, Math.Min(free, root.Count)));
        }
        var working = S.Locations[Camp].Facility.Processes.Any(p =>
        {
            var t = E.FindItem(S, p.TargetInstanceId);
            return t != null && !E.IsSpoiled(S, t) && E.ContentStatus(S, t) is var st && !st.StartsWith("缺水") && !st.StartsWith("待");
        });
        if (!working)
        {
            if (fac.FireLit) E.DoInstant(S, InstantRequest.Extinguish());
            return false;
        }
        if (fac.FuelMinutes < 35 && E.MaxWoodForFire(fac) > 0 && E.AvailableCount(S, "wood", true) > 0) E.DoInstant(S, InstantRequest.AddFuel());
        if (!fac.FireLit && !Do(IgniteRequest())) return false;
        return Do(ActionRequest.Wait(30));
    }

    private int Avail(string node) => S.Resources[node].Discovered ? S.Resources[node].Available ?? 99 : 0;

    // ───────────── 营地 ─────────────

    private bool CampWork()
    {
        var fac = S.Locations[Camp].Facility;
        if (TendFire()) return true;

        if (!fac.ShelterBuilt && Do(ActionRequest.Craft("shelter"))) return true;
        if (fac.ShelterBuilt && !fac.BedBuilt && Do(ActionRequest.Craft("bed"))) return true;
        if (fac.BedBuilt && Stock("hatchet") == 0 && Stock("wood") >= 4 && Do(ActionRequest.Craft("axe"))) return true;
        if (LowEnergy) return Do(ActionRequest.Rest(60));
        return false;
    }

    // ───────────── 每一步 ─────────────

    private void Step()
    {
        Needs();
        if (S.Progress.Dead) return;

        var night = Tod >= 20 * 60 + 30 || Tod < 5 * 60;
        if (night)
        {
            if (Here != Camp) { ReturnToCamp(); return; }
            if (CampWork()) return;
            if (S.Stats.Wakefulness < 80) Do(ActionRequest.Sleep(480));
            else Do(ActionRequest.Wait(60));
            return;
        }

        if (Here != Camp) { ReturnToCamp(); return; }
        if (CampWork()) return;
        if (Tod < 6 * 60) { Do(ActionRequest.Wait(30)); return; }
        if (S.Stats.Wakefulness < 25) { Do(ActionRequest.Sleep(240)); return; }
        if (LowEnergy) { Do(ActionRequest.Rest(60)); return; }
        if (Tod > 18 * 60 + 30) { Do(ActionRequest.Wait(60)); return; }

        Trip();
    }

    private void Trip()
    {
        var fac = S.Locations[Camp].Facility;
        var field = S.Locations["field"];
        var water = Stock("clean_water") + Stock("dirty_water");
        var food = Stock("cooked_root") + Stock("raw_root") + Stock("berry") + Stock("canned_food");

        if (field.Progress < 50) { GoTo("field"); Do(ActionRequest.Explore()); Do(ActionRequest.Explore()); ReturnToCamp(); return; }

        if (water < 6 && GoTo("stream"))
        {
            for (var i = 0; i < 3; i++) Do(ActionRequest.Gather("water"));
            ReturnToCamp();
            return;
        }

        var woods = S.Locations["woods"];
        var needWood = (!fac.ShelterBuilt && Stock("wood") < 5) || Stock("wood") < 3;
        if (needWood && GoTo("woods"))
        {
            while (woods.Progress < 25) Do(ActionRequest.Explore());
            for (var i = 0; i < 3 && Avail("wood") > 0; i++) Do(ActionRequest.Gather("wood"));
            ReturnToCamp();
            return;
        }

        var needFiber = (!fac.ShelterBuilt && Stock("fiber") < 4) || (!fac.BedBuilt && Stock("fiber") < 4);
        if (needFiber && GoTo("field"))
        {
            for (var i = 0; i < 2 && Avail("field_fiber") > 0; i++) Do(ActionRequest.Gather("field_fiber"));
            ReturnToCamp();
            return;
        }

        if (_route == Route.StreamAndRoots && food < 6 && Avail("roots") >= 2 && GoTo("field"))
        {
            while (Avail("roots") > 0 && Tod < 19 * 60) Do(ActionRequest.Gather("roots"));
            ReturnToCamp();
            return;
        }

        if (_route == Route.BerriesAndCaches && food < 8 && GoTo("woods"))
        {
            while (woods.Progress < 75) Do(ActionRequest.Explore());
            while (Avail("berries") > 0) Do(ActionRequest.Gather("berries"));
            ReturnToCamp();
            return;
        }

        // 探索与遗迹搜集
        foreach (var loc in new[] { "field", "woods", "stream", "village", "hardware", "clinic", "scrapyard" })
        {
            if (!S.Locations[loc].Discovered || Path(loc) == null) continue;
            var caches = E.Config.Resources.Where(r => r.LocationId == loc && r.IsCache && S.Resources[r.NodeId].Discovered && Avail(r.NodeId) > 0).ToList();
            if (S.Locations[loc].Progress >= 100 && caches.Count == 0 && S.Locations[loc].Visited) continue;
            if (!GoTo(loc)) continue;
            for (var i = 0; i < 2 && S.Locations[loc].Progress < 100; i++) Do(ActionRequest.Explore());
            foreach (var r in E.Config.Resources.Where(r => r.LocationId == loc && r.IsCache && S.Resources[r.NodeId].Discovered && Avail(r.NodeId) > 0))
                Do(ActionRequest.Gather(r.NodeId));
            ReturnToCamp();
            return;
        }

        // 无事可做：补充木材与食物
        if (GoTo("woods"))
        {
            for (var i = 0; i < 2 && Avail("wood") > 0; i++) Do(ActionRequest.Gather("wood"));
            if (Avail("berries") > 0) Do(ActionRequest.Gather("berries"));
            ReturnToCamp();
            return;
        }
        Do(ActionRequest.Wait(60));
    }
}
