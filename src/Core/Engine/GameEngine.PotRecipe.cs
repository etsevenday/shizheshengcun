using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>锅卡“锅食谱”里一条食谱的只读说明（投料按钮的禁用原因与确认浮窗共用；不改状态）。</summary>
    public sealed class PotRecipeOption
    {
        public string ProcessId { get; set; } = "";
        public string RecipeId { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Ok { get; set; }
        public string? Reason { get; set; }
        /// <summary>“谷物×2（占2个食材位）＋盐×1（辅料，不占位）＋锅里净水500毫升”。</summary>
        public string InputText { get; set; } = "";
        /// <summary>这次会取的实例：“随身谷物#3×1、地面谷物#9×1、随身盐#5×1”（随身优先，再当前地面）。</summary>
        public List<string> Sources { get; set; } = new List<string>();
        public int HeatMinutes { get; set; }
        public string OutputText { get; set; } = "";
        public string Note { get; set; } = "";
    }

    /// <summary>
    /// 复合锅食谱（开发计划 v0.9 B16，R50；S10 X4、A07 X1.5、S11 X4、R06 X4）：远行硬饼 grain×2 占两个食材位、盐×1 辅料锁进锅里不占位、
    /// 锅里净水 500 毫升；谷肉炖食 grain×1+raw_meat×1 占两位、净水 1000 毫升，完成两份。投料 0 分钟：锅须在当前火堆里、食材位全空、
    /// 锅里净水够（只用锅水，不从随身补）；食材与辅料从随身/当前地面未预留未占用的实例取（随身优先）。之后按火堆有效受热被动推进
    /// （不吃熟练加速）：熄火保进度；取出任一食材/辅料或把锅取下即作废（已受热清零，辅料退回该火堆地面，已耗燃料不退）；
    /// 完成一次核销全部主食材与辅料、扣锅水一次，成品按份留在锅里，从完成起按自身保质（硬饼 336 小时、炖食 24 小时）。
    /// 原料生肉实际受热后受本次加工保护（Q16）。烹饪经验给投料人一次（clamp(ceil(受热分钟/10),1,12)，计入每日 30，Q60）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public IEnumerable<ProcessConfig> PotRecipeProcesses => Config.Processes.Where(p => p.IsPotRecipe);

        /// <summary>这件锅内物品是不是某个进行中锅食谱的辅料（不占食材位）。</summary>
        public bool IsPotAux(GameState s, ItemInstance i) =>
            i.InContainer && AllProcesses(s).Any(p => p.AuxInstanceIds != null && p.AuxInstanceIds.Contains(i.InstanceId));

        /// <summary>这一份要从哪些实例取（随身优先、再当前地面；不含已预留、借用、占用、腐败的）。</summary>
        private List<(ItemInstance inst, int take)> PotRecipeSources(GameState s, Dictionary<string, int> need)
        {
            var list = new List<(ItemInstance, int)>();
            foreach (var kv in need)
            {
                var left = kv.Value;
                foreach (var w in UsableBatches(s, kv.Key, true).Where(w => !IsLocked(s, w)))
                {
                    if (left <= 0) break;
                    var take = Math.Min(left, w.Count);
                    list.Add((w, take));
                    left -= take;
                }
            }
            return list;
        }

        /// <summary>锅卡上列出的全部锅食谱（只读）。</summary>
        public List<PotRecipeOption> PotRecipeOptions(GameState s, ItemInstance pot) =>
            PotRecipeProcesses.Select(pc => PotRecipePreview(s, pc, pot)).ToList();

        /// <summary>
        /// 锅食谱按钮上的经验预估（B22f 试玩修复 M428，Q207）：每批一次给开做者，按预计完成时刻（锅在燃烧的火堆上为现在 + 所需受热分钟，否则按现在）
        /// 所在世界日的烹饪总额（含净化，每日 30）余量写“只剩N”/“已满30（含净化）”，完成日不是今天时写“按完成日第N日额度”。只读。
        /// </summary>
        public string PotRecipeXpPreview(GameState s, ProcessConfig pc, ItemInstance pot)
        {
            var per = CookFoodXpFor(pc.Id);
            if (per <= 0) return "不给经验";
            var basic = $"完成后{SkillName(Cooking)}经验+{per}（{CookTotalCapText}）";
            if (SkillMaxed(s, Cooking)) return basic;
            var loc = PotFireLocation(pot);
            var finish = loc != null && FireHot(s, loc) ? s.GameMinute + pc.HeatMinutes : s.GameMinute;
            var day = Clock.Day(finish);
            var room = CookTotalRoom(s.Player, day);
            var c = Config.Progression.Cooking;
            if (room <= 0) return $"{DayWord(s, day)}{SkillName(Cooking)}经验已满{c.FoodDailyCap}（含净化），完成不再给经验";
            if (room < per) return $"完成后{SkillName(Cooking)}经验+{room}（{CookTotalCapText}，{DayWord(s, day)}只剩{room}）";
            return day == Clock.Day(s.GameMinute) ? basic : $"完成后{SkillName(Cooking)}经验+{per}（{CookTotalCapText}，按{DayWord(s, day)}额度）";
        }

        public PotRecipeOption PotRecipePreview(GameState s, ProcessConfig pc, ItemInstance pot)
        {
            var rc = _recipes[pc.RecipeId!];
            var o = new PotRecipeOption { ProcessId = pc.Id, RecipeId = rc.Id, Name = rc.Name, HeatMinutes = pc.HeatMinutes };
            var parts = pc.FoodInputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}").ToList();
            o.InputText = $"{string.Join("＋", parts)}（占{pc.FoodInputs.Values.Sum()}个食材位）"
                          + string.Concat(pc.AuxInputs.Select(kv => $"＋{ItemName(kv.Key)}×{kv.Value}（辅料，不占位）"))
                          + $"＋锅里{ItemName(pc.RequiredLiquid ?? "clean_water")}{pc.WaterCostMl}毫升";
            var outCfg = _items[pc.OutputId];
            o.OutputText = $"{outCfg.Name}×{pc.OutputCount}" + (outCfg.ShelfLifeMinutes is int sl ? $"（完成起保质{Clock.Duration(sl)}）" : "");
            o.Note = $"需{pc.HeatMinutes}有效受热分钟，火堆燃料按世界时间另扣、不吃熟练加速；熄火保留进度；取出食材或取下锅即作废（辅料退回地面）；"
                     + PotRecipeXpPreview(s, pc, pot);
            var src = PotRecipeSources(s, pc.FoodInputs.Concat(pc.AuxInputs).ToDictionary(k => k.Key, k => k.Value));
            o.Sources = src.Select(x => $"{(x.inst.IsCarried ? "随身" : "地面")}{ItemName(x.inst.ItemId)}#{x.inst.InstanceId}×{x.take}").ToList();
            o.Reason = PotRecipeBlocked(s, pc, rc, pot);
            o.Ok = o.Reason == null;
            return o;
        }

        private string? PotRecipeBlocked(GameState s, ProcessConfig pc, RecipeConfig rc, ItemInstance pot)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (s.Battle != null) return "战斗中不能整理物品";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (HolderBlocked(s) is string busy) return busy;
            if (RecipeUnknownReason(s, rc) is string unknown) return unknown;
            foreach (var kv in rc.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) return $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
            }
            var here = s.Ground;
            if (PotFireLocation(pot) != here) return "先把锅放进当前地点的火堆加工位";
            var ct = PotConfig(pot);
            var foods = PotFood(s, pot.InstanceId);
            if (foods.Count > 0) return $"锅里已有食材{foods.Count}/{ct.FoodSlots}（{string.Join("、", foods.Select(f => ItemName(f.ItemId)))}），请先取出";
            if (pc.FoodInputs.Values.Sum() > ct.FoodSlots) return "食材位不够";
            var liquid = PotLiquid(s, pot.InstanceId);
            var need = pc.RequiredLiquid ?? "clean_water";
            if (liquid == null) return $"锅里没有水：先装{ItemName(need)}{pc.WaterCostMl}毫升（只用锅里的水）";
            if (liquid != need) return $"锅里是{ItemName(liquid)}：先在火堆上烧成{ItemName(need)}（只用锅里的{ItemName(need)}）";
            var free = PotFreeWaterUnits(s, pot) * ct.UnitMl;
            if (free < pc.WaterCostMl) return $"锅里可用{ItemName(need)}{free}/{pc.WaterCostMl}毫升，先往锅里补水（不从随身扣）";
            foreach (var kv in pc.FoodInputs.Concat(pc.AuxInputs))
            {
                var have = UsableBatches(s, kv.Key, true).Where(w => !IsLocked(s, w)).Sum(w => w.Count);
                if (have < kv.Value) return $"缺少{ItemName(kv.Key)}（随身与当前地面{have}/{kv.Value}）";
            }
            return null;
        }

        /// <summary>投料开做（0 分钟）：整体校验后一次把主食材逐份、辅料移进锅，建立锅食谱进程（受热 0）。失败什么都不变。</summary>
        private ActionResult LoadPotRecipe(GameState s, string processId, int? potId)
        {
            var pc = Config.Processes.FirstOrDefault(p => p.Id == processId && p.IsPotRecipe);
            if (pc == null) return ActionResult.Fail("没有这个锅食谱");
            var pot = potId.HasValue ? FindItem(s, potId.Value) : null;
            if (pot == null || !IsPot(pot)) return ActionResult.Fail("没有这口锅");
            var rc = _recipes[pc.RecipeId!];
            if (PotRecipeBlocked(s, pc, rc, pot) is string why) return ActionResult.Fail(why);
            var owner = ItemInstance.ContainerOwner(pot.InstanceId);
            var inputs = new List<int>();
            var aux = new List<int>();
            var taken = new List<string>();
            foreach (var (inst, take) in PotRecipeSources(s, pc.FoodInputs))
            {
                taken.Add($"{(inst.IsCarried ? "随身" : "地面")}{ItemName(inst.ItemId)}×{take}");
                for (var k = 0; k < take; k++) inputs.Add(MoveOne(s, inst, owner).InstanceId);
            }
            foreach (var (inst, take) in PotRecipeSources(s, pc.AuxInputs))
            {
                taken.Add($"{(inst.IsCarried ? "随身" : "地面")}{ItemName(inst.ItemId)}×{take}（辅料）");
                for (var k = 0; k < take; k++) aux.Add(MoveOne(s, inst, owner).InstanceId);
            }
            var here = s.Ground;
            var fac = FacOf(s, here)!;
            fac.Processes.Add(new FacilityProcess
            {
                ProcessId = $"{pc.Id}#{s.NextProcessSeq++}",
                RecipeId = pc.Id,
                TargetInstanceId = inputs[0],
                PotInstanceId = pot.InstanceId,
                RequiredHeatMinutes = pc.HeatMinutes,
                Status = ProcessStatus.Waiting,
                LastWorldMinute = s.GameMinute,
                InitiatorActorId = GameState.PlayerId,
                InputInstanceIds = inputs,
                AuxInstanceIds = aux,
            });
            SyncFacility(s, here);
            Emit(s, GameEventKind.Info, $"在{ItemName(pot.ItemId)}#{pot.InstanceId}里开做{rc.Name}：投入{string.Join("、", taken)}，预留锅里{ItemName(pc.RequiredLiquid ?? "clean_water")}{pc.WaterCostMl}毫升；"
                                        + $"需{pc.HeatMinutes}有效受热分钟，完成得{ItemName(pc.OutputId)}×{pc.OutputCount}，留在锅里。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>锅食谱进程是否仍成立（锅在这处火堆、全部主食材与辅料仍在锅里且种类不变）。</summary>
        private bool PotRecipeIntact(GameState s, string loc, FacilityProcess p)
        {
            var pc = Process(p.RecipeId);
            var pot = p.PotInstanceId.HasValue ? FindItem(s, p.PotInstanceId.Value) : null;
            if (pot == null || pot.Owner != FireOwner(loc) || p.InputInstanceIds == null) return false;
            var owner = ItemInstance.ContainerOwner(pot.InstanceId);
            var want = pc.FoodInputs.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value)).OrderBy(x => x).ToList();
            var have = p.InputInstanceIds.Select(id => FindItem(s, id)).ToList();
            if (have.Any(i => i == null || i.Owner != owner || i.Count != 1)) return false;
            if (!have.Select(i => i!.ItemId).OrderBy(x => x).SequenceEqual(want)) return false;
            var auxWant = pc.AuxInputs.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value)).OrderBy(x => x).ToList();
            var auxHave = (p.AuxInstanceIds ?? new List<int>()).Select(id => FindItem(s, id)).ToList();
            if (auxHave.Any(i => i == null || i.Owner != owner || i.Count != 1)) return false;
            return auxHave.Select(i => i!.ItemId).OrderBy(x => x).SequenceEqual(auxWant);
        }

        /// <summary>作废一个锅食谱进程：辅料退回该火堆地面（主食材仍在锅里，由玩家取出）；进度清零、保护解除。返回退回说明。</summary>
        private void AbandonPotRecipe(GameState s, string loc, FacilityProcess p)
        {
            foreach (var id in p.AuxInstanceIds ?? new List<int>())
            {
                var it = FindItem(s, id);
                if (it == null || !it.InContainer) continue;
                it.Owner = loc;
                it.PlacedOrder = null;
            }
            Normalize(s, loc);
        }

        /// <summary>锅食谱的受热条件：仍成立、主食材未腐败（或已受保护）、锅水为所需水种且已预留足量。</summary>
        private bool PotRecipeHeatEligible(GameState s, string loc, FacilityProcess p, bool ignoreSpoil)
        {
            if (!PotRecipeIntact(s, loc, p)) return false;
            var pc = Process(p.RecipeId);
            if (!ignoreSpoil && p.InputInstanceIds!.Any(id => IsSpoiled(s, FindItem(s, id)!))) return false;
            return PotLiquid(s, p.PotInstanceId!.Value) == pc.RequiredLiquid && p.ReservedWaterMl >= pc.WaterCostMl;
        }

        /// <summary>完成：一次核销主食材与辅料、扣锅水一次，成品逐份留在锅里（完成起计保质），给投料人烹饪经验一次。</summary>
        private string CompletePotRecipe(GameState s, string loc, FacilityProcess p)
        {
            var pc = Process(p.RecipeId);
            var pot = FindItem(s, p.PotInstanceId!.Value)!;
            var used = p.InputInstanceIds!.Concat(p.AuxInstanceIds ?? new List<int>()).Select(id => FindItem(s, id)!).ToList();
            var usedText = string.Join("、", used.GroupBy(i => i.ItemId).Select(g => $"{ItemName(g.Key)}×{g.Count()}"));
            foreach (var i in used) s.Items.Remove(i);
            ConsumeWater(s, pot, pc.RequiredLiquid!, pc.WaterCostMl);
            CreateItems(s, pc.OutputId, pc.OutputCount, ItemInstance.ContainerOwner(pot.InstanceId));
            var text = $"{LocName(loc)}火堆：{ItemName(pot.ItemId)}#{pot.InstanceId}里的{usedText}已做成{ItemName(pc.OutputId)}×{pc.OutputCount}，留在锅里"
                       + $"（{ItemName(pc.RequiredLiquid!)}−{pc.WaterCostMl}毫升）。";
            if (CookFoodXp(s, p, pc, 1) is string fx) text = text.TrimEnd('。') + $"（{fx}）。";
            return text;
        }

        /// <summary>锅卡/食材卡上的锅食谱状态：“远行硬饼烘制中 受热10/60分钟 · 受热保护中”。</summary>
        private string PotRecipeStatus(GameState s, ItemInstance inst, FacilityProcess p)
        {
            var pc = Process(p.RecipeId);
            var name = _recipes[pc.RecipeId!].Name;
            var loc = p.PotInstanceId.HasValue && FindItem(s, p.PotInstanceId.Value) is ItemInstance pot ? PotFireLocation(pot) : null;
            if (loc == null) return $"{name}：锅不在火堆上";
            if (p.InputInstanceIds!.Any(id => FindItem(s, id) is ItemInstance x && IsSpoiled(s, x))) return $"{name}：原料已腐败，不再加工";
            if (p.ReservedWaterMl < pc.WaterCostMl) return $"{name}：待{ItemName(pc.RequiredLiquid ?? "")}{pc.WaterCostMl}毫升";
            return (FireHot(s, loc)
                       ? $"{name}{pc.Name}中 受热{p.HeatDoneMinutes}/{p.RequiredHeatMinutes}分钟"
                       : $"火堆未燃烧 · {name}{pc.Name}暂停 {p.HeatDoneMinutes}/{p.RequiredHeatMinutes}分钟")
                   + (p.HeatProtected ? (PastExpiryButProtected(s, inst) ? " · 受热保护中，已过原保质期，取出即腐败" : " · 受热保护中") : "");
        }

        /// <summary>开发入口（?dev=1，B16）：远行硬饼与谷肉炖食各一份的材料（谷物3、盐1、生肉1）放当前地面。</summary>
        public string DebugPotRecipeKit(GameState s)
        {
            CreateItems(s, "grain", 3, s.Ground);
            CreateItems(s, "salt", 1, s.Ground);
            CreateItems(s, "raw_meat", 1, s.Ground);
            var text = "开发：锅食谱材料（谷物×3、盐×1、生肉×1）放在当前地面";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }
    }
}
