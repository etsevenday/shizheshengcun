using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
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

        /// <summary>某地点是否已建成该工坊（hand 永远可用）。</summary>
        public bool WorkshopBuilt(GameState s, string locationId, string stationId) =>
            stationId == "hand" || (s.Locations.TryGetValue(locationId, out var l) && l.Facility.Workshops.Contains(stationId));

        /// <summary>建造配方的设施在当前地点是否已建成（棚、床、工坊）。</summary>
        public bool FacilityBuiltHere(GameState s, RecipeConfig rc) =>
            rc.FacilityResult != null && FacilityFlag(s.Locations[s.CurrentLocation].Facility, rc.FacilityResult);

        /// <summary>这个地点能不能建工坊（B10-1：配置 workshops.buildLocations，现在只有 camp）。</summary>
        public bool CanBuildWorkshopsAt(string locationId) => Config.Workshops.BuildLocations.Contains(locationId);

        /// <summary>配方所需工坊不在当前地点时的原因；hand 或已建成为 null（A05 §3“其他工坊须在当前地点”）。</summary>
        public string? StationBlocked(GameState s, RecipeConfig rc)
        {
            if (rc.Station == "hand") return null;
            var here = s.CurrentLocation;
            if (WorkshopBuilt(s, here, rc.Station)) return null;
            var name = StationName(rc.Station);
            return CanBuildWorkshopsAt(here)
                ? $"需要{name}：{LocName(here)}还没有建成{name}（在“工坊建造”里建）"
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
