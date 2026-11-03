using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 品阶野外修补包（开发计划 v0.9 B22b，R168；Q168 策划答复与本轮「修补包完整定稿」，survival-adjudications.json patch）：
    /// 白/绿/蓝三品阶（field_patch / field_patch_green / field_patch_blue），每包整数耐久 100；取代 B16 “整份消耗只修 10%、只修非枪金属武器/工具”。
    /// 目标：有耐久的武器/工具或七槽防具，其正式制作输入或正式拆解回收表至少一项为废金属/钢材/铁片（scrap 即 scrap_metal），枪械、锅、打火器排除。
    /// 公式（十分位整数，不用浮点）：k=20×2^(e−p)，L = e≤p ? 1 : 2^(p−e)，r=floor(10×min(M−d, M×L, u×M/k))/10，c=ceil(k×r/M)；r&lt;0.1 拒绝。
    /// 开工锁定目标与所选包（整堆锁，完成时只拆出一包），完成时一次性 d+=r、u−=c，u=0 销毁；取消解锁、包与目标不变、已过时间不退。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>现场修补配方的 repairFilter 值。</summary>
        public const string FieldPatchFilter = "field_patch";
        /// <summary>现场修补配方 ID（沿用 B16 field_patch_repair）。</summary>
        public const string FieldPatchRepairId = "field_patch_repair";

        /// <summary>金属依据（Q168 eligibility：scrap_metal/steel/iron_plate；本配置的废金属 ID 是 scrap，按别名同认）。</summary>
        private static readonly HashSet<string> FieldPatchMetals = new HashSet<string> { "scrap", "scrap_metal", "steel", "iron_plate" };

        private Dictionary<string, bool>? _fieldPatchEligible;

        public RecipeConfig? FieldPatchRecipe => Config.Recipes.FirstOrDefault(IsFieldPatchRecipe);

        public static bool IsFieldPatchRecipe(RecipeConfig rc) => rc.IsRepair && rc.RepairFilter == FieldPatchFilter;

        /// <summary>修补包品阶配置；不是修补包为 null。</summary>
        public FieldPatchConfig? FieldPatchOf(string itemId) => _items.TryGetValue(itemId, out var c) ? c.FieldPatch : null;

        public bool IsFieldPatch(ItemInstance i) => FieldPatchOf(i.ItemId) != null;

        /// <summary>包的整数耐久上限（实例记录优先，没有记录按配置）。</summary>
        public int PatchMax(ItemInstance pack) => (int)Math.Round(pack.MaxDurability ?? FieldPatchOf(pack.ItemId)?.MaxDurability ?? 0);

        /// <summary>包的剩余整数耐久 u（没有记录的按满包）。</summary>
        public int PatchRemaining(ItemInstance pack) => (int)Math.Round(pack.Durability ?? PatchMax(pack));

        /// <summary>新生成的修补包记满包耐久（Q168 grades：每包 100，无保质、确定品阶，不抽品质词条）。</summary>
        private static void InitFieldPatch(ItemInstance inst, ItemConfig cfg)
        {
            if (cfg.FieldPatch == null) return;
            inst.MaxDurability = cfg.FieldPatch.MaxDurability;
            inst.Durability = cfg.FieldPatch.MaxDurability;
        }

        /// <summary>“野外修补包（白）#12 剩余80/100”。</summary>
        public string PatchText(ItemInstance pack) => $"{ItemName(pack.ItemId)}#{pack.InstanceId}（剩余{PatchRemaining(pack)}/{PatchMax(pack)}）";

        /// <summary>装备品质序号 e（equipmentRanks：white 0、green 1、blue 2；旧档没记品质按白，开发假设 B22b-5）；未知品质为 null（拒绝修补）。</summary>
        public static int? EquipmentRank(string? quality) => quality switch
        {
            null => 0,
            "white" => 0,
            "green" => 1,
            "blue" => 2,
            _ => null,
        };

        /// <summary>
        /// 这种物品能否用修补包修（Q168 eligibility，按物品 ID 缓存）：有耐久；是武器、工具或七槽装备；不是枪械、锅、打火器；
        /// 正式拆解回收表（回收材料或回收专家追加材料）或任一正式制作配方的输入至少一项是金属。缺失的表不猜测。
        /// </summary>
        public bool FieldPatchEligible(string itemId)
        {
            _fieldPatchEligible ??= new Dictionary<string, bool>();
            if (_fieldPatchEligible.TryGetValue(itemId, out var ok)) return ok;
            ok = ComputeFieldPatchEligible(itemId);
            _fieldPatchEligible[itemId] = ok;
            return ok;
        }

        private bool ComputeFieldPatchEligible(string itemId)
        {
            if (!_items.TryGetValue(itemId, out var cfg) || !cfg.HasDurability || cfg.FieldPatch != null) return false;
            if (cfg.ContainerId != null || cfg.ToolTag == "lighter") return false;   // 锅、打火器显式排除
            var w = Weapon(itemId);
            if (w != null && w.WeaponKind == "firearms") return false;   // 枪械显式排除
            if (w == null && Gear(itemId) == null && cfg.ToolTag == null) return false;
            var sal = SalvageDef(itemId);
            if (sal != null && (FieldPatchMetals.Contains(sal.ItemId) || FieldPatchMetals.Contains(sal.ExpertMaterial ?? ""))) return true;
            return Config.Recipes.Any(r => !r.IsRepair && !r.IsSalvage && !r.IsBuild && r.Outputs.ContainsKey(itemId) && r.Inputs.Keys.Any(FieldPatchMetals.Contains));
        }

        /// <summary>
        /// 修补报价（Q168 formula，十分位整数/有理数计算）：max=M、cur=d、remaining=u、equipRank=e、patchRank=p。
        /// 返回恢复量（十分位整数，r×10）与包耐久消耗 c；恢复不足 0.1 返回 (0, 0)。
        /// </summary>
        public static (int restoreTenths, int cost) FieldPatchQuote(double max, double cur, int remaining, int equipRank, int patchRank)
        {
            var mt = (long)Math.Round(max * 10);
            if (mt <= 0 || remaining <= 0) return (0, 0);
            // M−d 的十分位向下取整（当前耐久可能不是整十分位，例如拾荒成色）；1e-6 只防浮点把 0.1 算成 0.0999…
            var deficit = Math.Max(0L, (long)Math.Floor((max - cur) * 10 + 1e-6));
            var diff = equipRank - patchRank;
            // M×L：e≤p 时 L=1；e>p 时 L=2^(p−e)
            var cap = diff <= 0 ? mt : mt >> diff;
            // k = kn/kd = 20×2^(e−p)
            long kn = diff >= 0 ? 20L << diff : 20L;
            long kd = diff >= 0 ? 1L : 1L << -diff;
            var byPack = remaining * mt * kd / kn;   // floor(10×u×M/k)
            var r = Math.Min(deficit, Math.Min(cap, byPack));
            if (r < 1) return (0, 0);
            var cost = (kn * r + kd * mt - 1) / (kd * mt);   // ceil(k×r/M)
            return ((int)r, (int)cost);
        }

        /// <summary>某件目标用某个包的报价（目标须有耐久、品质已知）。</summary>
        public (int restoreTenths, int cost) FieldPatchQuote(ItemInstance target, ItemInstance pack) =>
            target.Durability is double d && target.MaxDurability is double m && EquipmentRank(target.Quality) is int e && FieldPatchOf(pack.ItemId) is FieldPatchConfig fp
                ? FieldPatchQuote(m, d, PatchRemaining(pack), e, fp.Rank)
                : (0, 0);

        /// <summary>可用的修补包：执行者本人随身或当前地面、未预留未锁定、剩余耐久大于 0（Q168 operation，开发假设 B22b-2）。</summary>
        public List<ItemInstance> FieldPatchPacks(GameState s) =>
            s.Items.Where(i => IsFieldPatch(i) && (OnPlayer(i) || i.Owner == s.Ground) && i.ReservedBy == null && !IsLocked(s, i) && PatchRemaining(i) > 0)
                .OrderBy(i => FieldPatchOf(i.ItemId)!.Rank).ThenBy(i => OnPlayer(i) ? 0 : 1).ThenBy(i => i.InstanceId).ToList();

        /// <summary>
        /// 用过的包单独一张卡（Q168 stacking：已使用包堆叠上限 1）：随身满五张时，这次修补会多出一张卡则整次拒绝（S06 §8，开发假设 B22b-1）。
        /// 只对随身的包判断；地面包不占随身卡位。
        /// </summary>
        private string? FieldPatchCardBlocked(GameState s, ItemInstance pack, int cost)
        {
            if (!pack.IsCarried) return null;
            var after = RootCardsAfter(s, null, 0, pack, 1) + (PatchRemaining(pack) - cost > 0 ? 1 : 0);
            return RootCardsAllow(s, after) ? null
                : $"随身卡位已满（{RootCardCount(s)}/{MaxRootCards}张）：用过的修补包会单独占一张卡，请先放下一张或改用当前地面的修补包";
        }

        /// <summary>
        /// 现场修补能否开工（只读）。目标：本人随身/穿戴或当前地面、未预留未锁定、合法类别、品质已知、缺损至少 0.1。
        /// 包：指定的须可用；未指定时默认选能恢复最多、消耗最少的一包（开发假设 B22b-6）。合法时返回 null 并给出目标、包与报价。
        /// </summary>
        public string? FieldPatchBlocked(GameState s, int? targetId, int? patchId, out ItemInstance? target, out ItemInstance? pack, out (int restoreTenths, int cost) quote)
        {
            target = null;
            pack = null;
            quote = (0, 0);
            var rc = FieldPatchRecipe;
            var title = rc?.Name ?? "现场修补";
            if (!targetId.HasValue) return "请先选择要修补的武器、工具或防具";
            target = FindItem(s, targetId.Value);
            if (target == null) return "要修补的物品已不在，请重新选择";
            var cfg = _items[target.ItemId];
            if (IsFieldPatch(target)) return "野外修补包不能修补自己，也不能给修补包补充耐久";
            if (!target.Durability.HasValue || !target.MaxDurability.HasValue) return $"{cfg.Name}没有耐久，不需要修补";
            if (!(OnPlayer(target) || target.Owner == s.Ground))
                return target.InFacility ? $"先把{cfg.Name}从火堆取下" : "只能修补本人随身、穿戴中或当前地面的物品";
            if (target.ReservedBy != null || IsLocked(s, target)) return "行动占用：这件物品正被使用";
            if (!FieldPatchEligible(target.ItemId))
                return $"{title}只修有金属制作或拆解依据的武器、工具或防具，{cfg.Name}不能用它修（枪械、锅、打火器和木石布皮制品不修）";
            var e = EquipmentRank(target.Quality);
            if (e == null) return $"{cfg.Name}的品质不明，不能修补";
            var d = target.Durability.Value;
            var m = target.MaxDurability.Value;
            if (d >= m - 1e-9) return $"{cfg.Name}#{target.InstanceId}耐久已满";
            if (Math.Floor((m - d) * 10 + 1e-6) < 1) return $"{cfg.Name}#{target.InstanceId}缺损不足0.1，无需修补";

            if (patchId.HasValue)
            {
                pack = FindItem(s, patchId.Value);
                if (pack == null || !IsFieldPatch(pack)) return "选用的修补包已不在，请重新选择";
                if (!(OnPlayer(pack) || pack.Owner == s.Ground)) return "只能用本人随身或当前地面的修补包";
                if (pack.ReservedBy != null || IsLocked(s, pack)) return "行动占用：这包修补包正被使用";
                if (PatchRemaining(pack) <= 0) return "这包修补包已经用完";
                quote = FieldPatchQuote(target, pack);
                if (quote.restoreTenths < 1) return $"{ItemName(pack.ItemId)}剩余耐久{PatchRemaining(pack)}，不够恢复0.1";
                return FieldPatchCardBlocked(s, pack, quote.cost);
            }

            var packs = FieldPatchPacks(s);
            if (packs.Count == 0) return "没有可用的野外修补包（随身或当前地面）";
            var tgt = target;
            var options = packs.Select(p => (pack: p, q: FieldPatchQuote(tgt, p))).Where(x => x.q.restoreTenths >= 1).ToList();
            if (options.Count == 0) return "随身和当前地面的修补包剩余耐久都不够恢复0.1";
            // B22b 试玩修复 M463：恢复与消耗都相同时先用已用过的包（剩余低于上限），再按实例号，少拆出新的用过包卡
            var legal = options.Where(x => FieldPatchCardBlocked(s, x.pack, x.q.cost) == null)
                .OrderByDescending(x => x.q.restoreTenths).ThenBy(x => x.q.cost).ThenBy(x => PatchRemaining(x.pack) < PatchMax(x.pack) ? 0 : 1).ThenBy(x => x.pack.InstanceId)
                .ToList();
            if (legal.Count == 0) return FieldPatchCardBlocked(s, options[0].pack, options[0].q.cost);
            pack = legal[0].pack;
            quote = legal[0].q;
            return null;
        }

        /// <summary>
        /// 制作面板包列表的预标（B22b 试玩修复 M463）：选了目标时按这次报价判断（不够恢复 0.1、会多出第六张卡）；还没选目标时，
        /// 只要用后会剩下（单独成卡）且随身已满就提示。可用为 null。
        /// </summary>
        public string? FieldPatchPackNote(GameState s, ItemInstance pack, ItemInstance? target)
        {
            if (target != null)
            {
                var q = FieldPatchQuote(target, pack);
                if (q.restoreTenths < 1) return "不够恢复0.1";
                return FieldPatchCardBlocked(s, pack, q.cost) != null ? $"会多出一张卡，随身已满{MaxRootCards}张" : null;
            }
            return FieldPatchCardBlocked(s, pack, 0) != null ? $"用后单独占一张卡，随身已满{MaxRootCards}张" : null;
        }

        /// <summary>品阶角标“白/绿/蓝”（B22b 试玩修复 M463：卡名在窄屏被截断时仍看得出品阶）；不是修补包为 null。</summary>
        public string? PatchGradeTag(string itemId) => FieldPatchOf(itemId)?.Rank switch { 0 => "白", 1 => "绿", 2 => "蓝", _ => null };

        /// <summary>品阶角标的样式类（与装备品质角标同色）。</summary>
        public string PatchGradeClass(string itemId) => FieldPatchOf(itemId)?.Rank switch { 1 => "q-green", 2 => "q-blue", _ => "q-white" };

        /// <summary>批次/堆的包耐久短句“ · 耐久80/100”；不是修补包为空串。</summary>
        public string PatchDurabilitySuffix(ItemInstance i) => IsFieldPatch(i) ? $" · 耐久{PatchRemaining(i)}/{PatchMax(i)}" : "";

        /// <summary>预估与开工共用的说明行：包品阶与剩余、目标当前/最大、恢复量与消耗；不能修满时写可恢复量。</summary>
        public List<string> FieldPatchNotes(ItemInstance target, ItemInstance pack, (int restoreTenths, int cost) q)
        {
            var notes = new List<string>();
            var u = PatchRemaining(pack);
            var max = PatchMax(pack);
            var d = target.Durability ?? 0;
            var m = target.MaxDurability ?? 0;
            var r = q.restoreTenths / 10.0;
            var after = Math.Min(m, d + r);
            notes.Add($"选用{ItemName(pack.ItemId)}#{pack.InstanceId}：剩余耐久{u}/{max}，本次消耗{q.cost}→{u - q.cost}/{max}" + (u - q.cost <= 0 ? "（用完销毁）" : pack.Count > 1 ? "（从这堆拆出一包使用）" : ""));
            notes.Add($"{ItemName(target.ItemId)}#{target.InstanceId}（{QualityName(target)}）耐久{Fmt.N(d)}/{Fmt.N(m)}→{Fmt.N(after)}/{Fmt.N(m)}，恢复{Fmt.T(r)}"
                      + (after < m - 1e-9 ? "（这次修不满）" : "（修满）"));
            notes.Add("一次只用一包，不自动接着用下一包；不吃维修效果加成；不改品质、词条与最大耐久；取消不消耗包、已过时间不退");
            return notes;
        }

        /// <summary>
        /// 开发入口（B22b 试玩用）：当前地面放白/绿/蓝修补包各 2、白/绿/蓝砍刀各 1（耐久 20%）、绿色拼装护甲 1（耐久 50%）、
        /// 手枪 1（耐久 50%，试拒绝）与一份蓝包配方材料（铁片/黏胶/布料各 6）；学会远行基础做法。返回说明。
        /// </summary>
        public string DebugGiveFieldPatchKit(GameState s)
        {
            s.Progress.Flags.Add("knowledge:expedition_basics");
            foreach (var id in new[] { "field_patch", "field_patch_green", "field_patch_blue" }) CreateItems(s, id, 2, s.Ground);
            void Worn(string id, string q, double frac)
            {
                var it = DebugCreateEquipment(s, id, q, s.Ground);
                if (it.MaxDurability is double m) it.Durability = Math.Round(m * frac, 1);
            }
            Worn("machete", "white", 0.2);
            Worn("machete", "green", 0.2);
            Worn("machete", "blue", 0.2);
            Worn("scrap_armor", "green", 0.5);
            Worn("pistol", "white", 0.5);
            foreach (var m in new[] { "iron_plate", "adhesive", "cloth" }) CreateItems(s, m, 6, s.Ground);
            Normalize(s, s.Ground);
            var text = $"开发：野外修补包白/绿/蓝各2、白/绿/蓝砍刀各1（耐久20%）、绿色拼装护甲1（耐久50%）、手枪1（耐久50%，不可修）、铁片/黏胶/布料各6放在{ZoneName(s, s.Ground)}地面；已学会远行基础做法";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>
        /// 现场修补完成（一次性结算）：重找目标与包并按当前值重算报价；从包堆里只拆出一包，d+=r、u−=c，u=0 销毁。
        /// 目标或包不在、或已无需修补时什么都不改、不给经验（ok=false）。
        /// </summary>
        private (bool ok, string text) CompleteFieldPatch(GameState s, ActionState a)
        {
            var t = a.ToolInstanceId.HasValue ? FindItem(s, a.ToolInstanceId.Value) : null;
            var pack = a.PatchInstanceId.HasValue ? FindItem(s, a.PatchInstanceId.Value) : null;
            if (t?.Durability == null || t.MaxDurability == null) return (false, "要修补的物品已不在，修补包未消耗，没有经验");
            if (pack == null || !IsFieldPatch(pack)) return (false, "选用的修补包已不在，目标未修补，没有经验");
            var q = FieldPatchQuote(t, pack);
            if (q.restoreTenths < 1) return (false, $"{ItemName(t.ItemId)}#{t.InstanceId}已无需修补，修补包未消耗，没有经验");
            var u = PatchRemaining(pack);
            var max = PatchMax(pack);
            var one = pack;
            if (pack.Count > 1)
            {
                pack.Count -= 1;
                one = pack.Clone();
                one.InstanceId = s.NextInstanceId++;
                one.Count = 1;
                one.ReservedBy = null;
                s.Items.Add(one);
            }
            var before = t.Durability.Value;
            t.Durability = Math.Min(t.MaxDurability.Value, before + q.restoreTenths / 10.0);
            var left = u - q.cost;
            string packText;
            if (left <= 0)
            {
                s.Items.Remove(one);
                packText = $"{ItemName(pack.ItemId)}耐久{u}→0，已用完";
            }
            else
            {
                one.Durability = left;
                one.MaxDurability = max;
                packText = $"{ItemName(pack.ItemId)}耐久{u}→{left}/{max}";
            }
            return (true, $"{ItemName(t.ItemId)}#{t.InstanceId}耐久{Fmt.N(before)}→{Fmt.N(t.Durability.Value)}/{Fmt.N(t.MaxDurability.Value)}（恢复{Fmt.T(q.restoreTenths / 10.0)}），{packText}");
        }
    }
}
