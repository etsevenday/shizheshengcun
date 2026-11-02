using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>设施栏的一张公共工坊站卡（B14b，Q141）：能用时 Blocked 为 null。</summary>
    public sealed class PublicStationView
    {
        public string Id { get; set; } = "";
        public string StationId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Blocked { get; set; }
    }

    /// <summary>
    /// 工坊、装备配方与队员交接（A07「工坊设施」「材料加工链」、A05 §3、A10 §7.2/§9、S08 X4；开发计划 v0.9 B10）。
    /// 工坊 = 地点设施 FacilityState.Workshops（每类每地一座，固定在地点、不进包）；hand 不需工坊。
    /// B10-1：只开放营地（camp）建造（待确认 Q107）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string WorkshopPrefix = "workshop:";

        /// <summary>工坊显示名（hand 为“手工”）。</summary>
        public string StationName(string stationId) =>
            Config.Workshops.Stations.FirstOrDefault(x => x.Id == stationId)?.Name ?? (stationId == "hand" ? "手工" : stationId);

        /// <summary>
        /// Q112 选 A（S08/A05/A07 本轮章、workshop-adjudications.json forgeBatchFuel）：配方里某物品按 N 份配方的分账——
        /// 原料 = 单份原料数×N，燃料 = 单份燃料数×N（锻炉每份配方另需木炭1；N 是配方执行份数，不是成品件数），总数 = 两项合计。
        /// 校验与预留都按总数一次合并（同一份炭不能同时算原料和燃料）；燃料不计入拆解预算，也不按时间另烧。
        /// </summary>
        public static (int Ingredient, int Fuel, int Total) RecipeItemSplit(RecipeConfig rc, string itemId, int portions)
        {
            var n = Math.Max(0, portions);
            var ing = (rc.Inputs.TryGetValue(itemId, out var a) ? a : 0) * n;
            var fuel = (rc.Fuel.TryGetValue(itemId, out var b) ? b : 0) * n;
            return (ing, fuel, ing + fuel);
        }

        /// <summary>Q112：燃料说明“锻炉每份配方另需木炭1”（多种燃料逐项写）；无燃料为 null。</summary>
        public string? RecipeFuelRule(RecipeConfig rc) => rc.Fuel.Count == 0 ? null
            : $"{StationName(rc.Station)}每份配方另需{string.Join("、", rc.Fuel.Select(kv => $"{ItemName(kv.Key)}{kv.Value}"))}";

        /// <summary>
        /// Q112 预览分账（S14 本轮章）：“执行N份 · 成品… · 原料木炭a＋燃料木炭b＝共c”。只读、不预留；无燃料配方为 null。
        /// </summary>
        public string? RecipeFuelSummary(RecipeConfig rc, int portions)
        {
            if (rc.Fuel.Count == 0) return null;
            var n = Math.Max(1, portions);
            var outs = string.Join("、", rc.Outputs.Select(kv => $"{ItemName(kv.Key)}{kv.Value * n}"));
            var parts = rc.Fuel.Keys.Select(id =>
            {
                var (ing, fuel, total) = RecipeItemSplit(rc, id, n);
                var name = ItemName(id);
                return ing > 0 ? $"原料{name}{ing}＋燃料{name}{fuel}＝共{total}" : $"燃料{name}{fuel}（原料无{name}）";
            });
            return $"执行{n}份配方 · 成品{outs} · {string.Join("；", parts)}（{RecipeFuelRule(rc)}）";
        }

        /// <summary>某地点是否已建成该工坊（hand 永远可用）。</summary>
        public bool WorkshopBuilt(GameState s, string locationId, string stationId) =>
            stationId == "hand" || (s.Locations.TryGetValue(locationId, out var l) && l.Facility.Workshops.Contains(stationId));

        /// <summary>建造配方的设施在当前地点是否已建成（棚、床、工坊）。</summary>
        public bool FacilityBuiltHere(GameState s, RecipeConfig rc) =>
            rc.FacilityResult != null && FacilityFlag(s.Locations[s.CurrentLocation].Facility, rc.FacilityResult);

        /// <summary>这个地点能不能建工坊（B10-1：配置 workshops.buildLocations，现在只有 camp）。</summary>
        public bool CanBuildWorkshopsAt(string locationId) => Config.Workshops.BuildLocations.Contains(locationId);

        // ───────────── 公共工坊站（B14b，Q141 选 B；city-adjudications.json publicStations） ─────────────

        /// <summary>某地点配置的公共工坊站（未列出的站不提供：h03 只有工作台/锻炉/军械台，r07 只有工作台，h02 缝纫台，h04 医疗台）。</summary>
        public IEnumerable<PublicStationConfig> PublicStationsAt(string locationId) => Config.City.PublicStations.Where(p => p.LocationId == locationId);

        /// <summary>
        /// 公共站现在不能用的原因；能用为 null。须实际在该地点（当前地点且已到访），AllFacts 全有，AnyFacts 有其一（锻炉/军械台：
        /// FA02 记录资格或可信旧 QMAP02 完成；任务接取、公开路线已领、手里有枪都不算）。0 额外使用费、不需自建蓝图。
        /// </summary>
        public string? PublicStationBlocked(GameState s, PublicStationConfig ps)
        {
            if (s.CurrentLocation != ps.LocationId || !s.Locations.TryGetValue(ps.LocationId, out var ls) || !ls.Visited)
                return $"须实际到达{LocName(ps.LocationId)}";
            var f = s.Progress.Flags;
            var miss = ps.AllFacts.FirstOrDefault(x => !f.Contains(x));
            if (miss != null) return $"需要：{PublicFactLabel(miss)}";
            if (ps.AnyFacts.Count > 0 && !ps.AnyFacts.Any(f.Contains))
            {
                // Q148 选 A：旧档资格只在判定里认、不向玩家展示——锻炉/军械台只写“需要：在教会地下圣所核心领取圣所旧路档册”
                var shown = ps.AnyFacts.Where(x => !HiddenQualificationFacts.Contains(x)).ToList();
                return "需要：" + string.Join("，或", (shown.Count > 0 ? shown : ps.AnyFacts).Select(PublicFactLabel));
            }
            return null;
        }

        /// <summary>只参与判定、不写进玩家条件文字的资格事实（Q148 选 A：可信旧 QMAP02 完成）。</summary>
        private static readonly HashSet<string> HiddenQualificationFacts = new HashSet<string> { "trusted_legacy_QMAP02_completed" };

        /// <summary>公共站条件的中文说法（地图事实说明）。</summary>
        private string PublicFactLabel(string fact) => Config.Map.FactLabels.TryGetValue(fact, out var t) ? t : fact;

        /// <summary>当前地点这一类公共站已开放（实际在场且条件满足）。</summary>
        public bool PublicStationOpenHere(GameState s, string stationId) =>
            PublicStationsAt(s.CurrentLocation).Any(p => p.StationId == stationId && PublicStationBlocked(s, p) == null);

        /// <summary>当前地点能不能用这一类工坊：hand、本地自建已建成，或本地公共站已开放。</summary>
        public bool StationUsableHere(GameState s, string stationId) =>
            WorkshopBuilt(s, s.CurrentLocation, stationId) || PublicStationOpenHere(s, stationId);

        /// <summary>当前地点的公共站（设施栏卡片与工坊入口说明用）：名称、工坊类型、能不能用与原因。</summary>
        public List<PublicStationView> PublicStationsHere(GameState s)
        {
            if (!s.Locations.TryGetValue(s.CurrentLocation, out var ls) || !ls.Visited) return new List<PublicStationView>();
            return PublicStationsAt(s.CurrentLocation)
                .Select(p => new PublicStationView { Id = p.Id, StationId = p.StationId, Name = p.Name, Blocked = PublicStationBlocked(s, p) }).ToList();
        }

        /// <summary>配方所需工坊不在当前地点时的原因；hand、已建成或本地公共站已开放为 null（A05 §3“其他工坊须在当前地点”；Q141 公共站）。</summary>
        public string? StationBlocked(GameState s, RecipeConfig rc)
        {
            if (rc.Station == "hand") return null;
            var here = s.CurrentLocation;
            if (WorkshopBuilt(s, here, rc.Station)) return null;
            var name = StationName(rc.Station);
            var pub = PublicStationsAt(here).FirstOrDefault(p => p.StationId == rc.Station);
            if (pub != null)
            {
                var why = PublicStationBlocked(s, pub);
                if (why == null) return null;
                return $"需要{name}：{pub.Name}还没开放（{why}）";
            }
            return CanBuildWorkshopsAt(here)
                ? $"需要{name}：{LocName(here)}还没有建成{name}（在“营地建造”里建）"
                : $"需要{name}：{LocName(here)}没有{name}，工坊只能建在{string.Join("、", Config.Workshops.BuildLocations.Select(LocName))}";
        }

        /// <summary>
        /// 配方现在全部的“门槛”原因（只读，界面一次列全）：未掌握知识、熟练等级不够、工坊不在当前地点。缺料与占用不在这里，开工校验另报。
        /// </summary>
        public List<string> RecipeLocks(GameState s, RecipeConfig rc)
        {
            var res = new List<string>();
            if (RecipeUnknownReason(s, rc) is string k) res.Add(k);
            foreach (var kv in rc.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) res.Add($"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）");
            }
            if (StationBlocked(s, rc) is string st) res.Add(st);
            return res;
        }

        /// <summary>某件装备的制作与来源（A05 §3）；不是装备为 null。</summary>
        public EquipmentSourceConfig? EquipmentSource(string itemId) => Config.Workshops.EquipmentSources.FirstOrDefault(x => x.ItemId == itemId);

        /// <summary>主角称呼前缀：主角为空，队友为“名字：”（交接/换装日志）。</summary>
        public string ActorPrefix(GameState s, string actorId) => actorId == GameState.PlayerId ? "" : ActorDisplayName(s, actorId) + "：";

        // ───────────── 队员交接（A10 §7.2：当前地点同队合法状态、指定接收者与数量、双方卡数重量一次校验） ─────────────

        /// <summary>
        /// 能否把 fromId 随身的一批物品（count 份）交给同行的 toId（只读）。只在安全空闲时（无战斗/敌踪/行动/对白），双方都在同行（同行者与主角同地）；
        /// 只交本人随身五卡里的物品（穿戴中的先卸下），不能是预留/占用的；接收方最终根卡数 ≤ 5（旧档已超限的不得再增加）、总重不超过 30 公斤硬线。
        /// </summary>
        public string? GiveBlocked(GameState s, string fromId, string toId, int instanceId, int count)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中不能交接物品";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (s.Main != null || s.Sub != null) return "行动进行中，不能交接物品";
            if (fromId == toId) return "不能交给自己";
            if (!s.Party.Contains(fromId) || !s.Party.Contains(toId)) return "只能在同行的队员之间交接（候补不在身边）";
            var it = FindItem(s, instanceId);
            if (it == null) return "物品已不在";
            if (it.Owner != ItemInstance.CarriedOwner(fromId))
                return it.IsWornBy(fromId) ? "穿戴中：先卸下再交给队友" : $"只能交出{ActorDisplayName(s, fromId)}随身的物品";
            if (it.ReservedBy != null) return "该物品已被预留";
            if (IsLocked(s, it)) return "行动占用：该物品正被当前行动使用";
            if (count <= 0 || count > it.Count) return "数量无效";
            var after = RootCardsAfterOf(s, toId, it, count, null, 0);
            if (!RootCardsAllowOf(s, toId, after))
                return $"{ActorDisplayName(s, toId)}随身卡位不够（交接后{after}/{MaxRootCards}张）";
            var grams = count >= it.Count ? ItemGrams(s, it) : _items[it.ItemId].WeightGrams * count;
            var total = CarriedGramsOf(s, toId) + grams;
            var hard = Config.Carry.HardGrams;
            if (total > hard) return $"{ActorDisplayName(s, toId)}会携带{Fmt.N(total / 1000.0)}公斤，超过{hard / 1000}公斤";
            return null;
        }

        /// <summary>交接后双方的随身卡数与总重（预览；不改状态）。</summary>
        public (int FromCards, int ToCards, int FromGrams, int ToGrams) GiveOutcome(GameState s, string fromId, string toId, ItemInstance it, int count)
        {
            var grams = count >= it.Count ? ItemGrams(s, it) : _items[it.ItemId].WeightGrams * count;
            return (RootCardsAfterOf(s, fromId, null, 0, it, count), RootCardsAfterOf(s, toId, it, count, null, 0),
                CarriedGramsOf(s, fromId) - grams, CarriedGramsOf(s, toId) + grams);
        }

        /// <summary>交接（0 时间、原子：校验通过才一次改 owner；带 requestId 幂等）。不复制、不重抽实例属性，保质与耐久不变。</summary>
        public ActionResult Give(GameState s, string fromId, string toId, int instanceId, int count, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = GiveBlocked(s, fromId, toId, instanceId, count);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var it = FindItem(s, instanceId)!;
            var name = ItemName(it.ItemId);
            MoveItems(s, it, count, ItemInstance.CarriedOwner(toId));
            Normalize(s, ItemInstance.CarriedOwner(fromId));
            Emit(s, GameEventKind.Info, $"{ActorDisplayName(s, fromId)}把{name}×{count}交给{ActorDisplayName(s, toId)}（{ActorDisplayName(s, toId)}随身{RootCardCountOf(s, toId)}/{MaxRootCards}张、{Fmt.N(CarriedGramsOf(s, toId) / 1000.0)}公斤）。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        // ───────────── 开发入口 ─────────────

        /// <summary>开发：在营地建成全部工坊（不扣材料、不推进时间）。不在可建地点时也建在营地。</summary>
        public string DebugBuildAllWorkshops(GameState s)
        {
            var loc = Config.Workshops.BuildLocations.FirstOrDefault() ?? s.CurrentLocation;
            var f = s.Locations[loc].Facility;
            var added = new List<string>();
            foreach (var st in Config.Workshops.Stations.Where(x => x.BuildRecipe != null))
                if (!f.Workshops.Contains(st.Id)) { f.Workshops.Add(st.Id); added.Add(st.Name); }
            var text = added.Count == 0 ? $"开发：{LocName(loc)}的工坊已全部建成" : $"开发：在{LocName(loc)}建成{string.Join("、", added)}";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发：学会全部做法与蓝图（写 knowledge:id 事实）。</summary>
        public string DebugLearnAllKnowledge(GameState s)
        {
            var n = 0;
            foreach (var k in Config.Knowledge)
                if (!KnowsKnowledge(s, k.Id)) { s.Progress.Flags.Add("knowledge:" + k.Id); n++; }
            var text = $"开发：学会全部做法与蓝图（新学会{n}项）";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发（B10b，Q111 试煅烧）：木材×10 放当前地面（够两批原料 + 2 份燃料）；不点火、不改燃料。</summary>
        public string DebugCharcoalKit(GameState s)
        {
            CreateItems(s, "wood", 10, s.CurrentLocation);
            var text = $"开发：木材×10放在{LocName(s.CurrentLocation)}地面（煅烧木炭每批4份，燃料另投）";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发（B10b，Q110 试医疗台三材料经验）：盐水剂/消毒液/草药浸膏各做一次的原料放当前地面。</summary>
        public string DebugMedicalMaterialKit(GameState s)
        {
            var kit = new (string id, int n)[] { ("clean_water", 3), ("salt", 1), ("lab_reagent", 1), ("herb", 2) };
            foreach (var (id, n) in kit) if (_items.ContainsKey(id)) CreateItems(s, id, n, s.CurrentLocation);
            var text = $"开发：医疗台材料加工原料放在{LocName(s.CurrentLocation)}地面（{string.Join("、", kit.Select(k => $"{ItemName(k.id)}×{k.n}"))}，盐水剂/消毒液/草药浸膏各一次）";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发（B10c，Q112）：钢材×3份原料——铁矿料×9、木炭×6（原料炭3＋燃料炭3），放当前地面，供试玩核对锻炉分账。</summary>
        public string DebugForgeSteelKit(GameState s)
        {
            var kit = new (string id, int n)[] { ("iron_ore", 9), ("charcoal", 6) };
            foreach (var (id, n) in kit) if (_items.ContainsKey(id)) CreateItems(s, id, n, s.CurrentLocation);
            var text = $"开发：锻炉钢材3份原料放在{LocName(s.CurrentLocation)}地面（{string.Join("、", kit.Select(k => $"{ItemName(k.id)}×{k.n}"))}；原料木炭3＋燃料木炭3，产钢材6）";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>开发：工坊材料包（各类工坊建造与常用装备材料各若干，放当前地面）。</summary>
        public string DebugWorkshopKit(GameState s)
        {
            var kit = new (string id, int n)[]
            {
                ("plank", 12), ("fastener", 12), ("parts", 12), ("thread", 10), ("stone", 10), ("iron_plate", 14), ("steel", 10),
                ("medical_gauze", 10), ("precision_parts", 6), ("charcoal", 6), ("leather", 8), ("cloth", 12), ("wood", 10),
                ("scrap", 10), ("fiber", 10), ("rope", 4), ("rubber", 4), ("plastic", 4), ("wire", 4), ("ballistic_fiber", 4),
            };
            foreach (var (id, n) in kit) if (_items.ContainsKey(id)) CreateItems(s, id, n, s.CurrentLocation);
            var text = $"开发：工坊材料包放在{LocName(s.CurrentLocation)}地面（{string.Join("、", kit.Select(k => $"{ItemName(k.id)}×{k.n}"))}）";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }
    }
}
