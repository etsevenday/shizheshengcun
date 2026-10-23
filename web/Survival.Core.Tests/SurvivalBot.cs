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
    private static readonly HashSet<string> Junk = new() { "scrap", "parts" };

    public SurvivalBot(GameEngine e, GameState s, Route route)
    {
        E = e;
        S = s;
        _route = route;
    }

    private int Tod => Clock.MinuteOfDay(S.GameMinute);
    private string Here => S.CurrentLocation;

    public void Play(int untilMinute)
    {
        var guard = 0;
        while (S.GameMinute < untilMinute && !S.Progress.Dead && guard++ < 20000)
        {
            var before = S.GameMinute;
            Step();
            if (S.GameMinute == before && !S.Progress.Dead) Do(ActionRequest.Wait(30));
        }
    }

    private void Log(string msg) => Trace.Add($"{Clock.DayTime(S.GameMinute)} H{S.Stats.Health:0} N{S.Stats.Satiety:0} W{S.Stats.Hydration:0} E{S.Stats.Energy:0} S{S.Stats.Wakefulness:0} M{S.Stats.Morale:0} | {msg}");

    // ───────────── 执行 ─────────────

    private bool Do(ActionRequest r)
    {
        var res = E.Start(S, r);
        if (!res.Ok) return false;
        Log($"{r.Type} {r.TargetId}{(r.Minutes > 0 ? " " + r.Minutes : "")}");
        Drive();
        return true;
    }

    private void Drive()
    {
        for (var i = 0; i < 3000 && !S.Progress.Dead; i++)
        {
            if (S.Main == null && S.Sub == null) return;
            var st = E.Advance(S).Status;
            if (st == SegmentStatus.Paused) HandlePause();
            else if (st != SegmentStatus.Running) { if (S.Main == null) return; }
        }
    }

    private void DriveSub()
    {
        for (var i = 0; i < 100 && S.Sub != null && !S.Progress.Dead; i++) E.Advance(S);
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
            foreach (var f in new[] { "cooked_root", "berry", "canned_food" })
                if (Has(f)) return ActionRequest.UseItem(f);
        }
        if (st.Health < 70 && Has("bandage") && S.BandageReadyAt <= S.GameMinute) return ActionRequest.UseItem("bandage");
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

    /// <summary>不再自动补水：烧水前把可用的脏水装进锅，凑满一锅。</summary>
    private void FillPotWithDirtyWater()
    {
        var pot = S.Items.FirstOrDefault(i => i.ItemId == "pot" && (i.IsCarried || i.Owner == Camp));
        if (pot == null) return;
        foreach (var w in E.UsableBatches(S, "dirty_water", true))
        {
            var space = E.PotConfig(pot).CapacityUnits - E.PotUnits(S, pot.InstanceId);
            if (space <= 0) break;
            E.DoInstant(S, InstantRequest.FillPot(w.InstanceId, pot.InstanceId, Math.Min(space, w.Count)));
        }
    }

    /// <summary>v0.3：烧好的净水留在锅里。再烧水或烹饪前先把锅里的净水倒到营地地面。</summary>
    private void EmptyPotOfCleanWater()
    {
        foreach (var pot in S.Items.Where(i => i.ItemId == "pot" && (i.IsCarried || i.Owner == Camp)).ToList())
            foreach (var w in E.PotContents(S, pot.InstanceId).Where(i => i.ItemId == "clean_water").ToList())
                E.DoInstant(S, InstantRequest.Decant(w.InstanceId, Camp, w.Count));
    }

    private int Avail(string node) => S.Resources[node].Discovered ? S.Resources[node].Available ?? 99 : 0;

    // ───────────── 营地 ─────────────

    private bool CampWork()
    {
        var fac = S.Locations[Camp].Facility;
        var canBoil = Stock("dirty_water") >= 2;
        var canCook = Stock("raw_root") >= 2;
        if (canBoil || canCook)
        {
            EmptyPotOfCleanWater();
            if (!fac.FireLit && !Do(IgniteRequest())) return false;
            if (canBoil) FillPotWithDirtyWater();
            if (canBoil && Do(ActionRequest.Craft("boil"))) return true;
            if (canCook && Do(ActionRequest.Craft("cook"))) return true;
        }
        if (fac.FireLit) E.DoInstant(S, InstantRequest.Extinguish());

        if (!fac.ShelterBuilt && Do(ActionRequest.Craft("shelter"))) return true;
        if (fac.ShelterBuilt && !fac.BedBuilt && Do(ActionRequest.Craft("bed"))) return true;
        if (fac.BedBuilt && Stock("hatchet") == 0 && Stock("wood") >= 4 && Do(ActionRequest.Craft("axe"))) return true;
        if (S.Stats.Energy < 30) return Do(ActionRequest.Rest(60));
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
        if (S.Stats.Energy < 30) { Do(ActionRequest.Rest(60)); return; }
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
