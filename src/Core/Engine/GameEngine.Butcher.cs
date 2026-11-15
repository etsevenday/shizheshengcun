using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 动物尸体与“处理尸体”（v0.5 §14）：30 基础分钟、按 craft 消耗，需随身有效的石刀/厨房刀/简易斧，成功磨损该工具 1；
    /// 制造熟练不加速。B51a（Q701／Q722）起开工即暂停尸体保质（R），取消退回原尸体并恢复 C+R、无产物，完成时不再因原截止判腐败；尸体源与产物不同时存在。
    /// </summary>
    public sealed partial class GameEngine
    {
        public CarcassConfig? CarcassOf(string itemId) => Config.AnimalCarcasses.FirstOrDefault(c => c.CarcassItemId == itemId);

        /// <summary>
        /// 处理尸体用的工具：随身、配置列出的刀斧、成色最高者。B51a（Q701.3、Q726-A01）起资格由“够一次磨损”改为真实内部耐久 D&gt;0（不按显示取整），
        /// D=0.01 也可开工，成功磨损照原 1 扣到 0 为止。
        /// </summary>
        public ItemInstance? ButcherTool(GameState s, CarcassConfig c) =>
            s.Items.Where(i => OnPlayer(i) && c.RequiredCarriedTools.Contains(i.ItemId)
 && i.ReservedBy == null && !IsLocked(s, i) && ProductionToolUsable(i))
                .OrderByDescending(Condition).ThenBy(i => i.InstanceId).FirstOrDefault();

        /// <summary>
        /// Q726-A02：缺刀斧时的拒绝句。随身合法（未预留、未锁定）的所需刀斧存在但全部真实 D=0，且没有正耐久的同类只是被占用，才用“这件工具已损坏，请更换或维修。”；
        /// 其余（完全没有、正 D 被占用等）沿原“需要随身有效的{工具清单}”。
        /// </summary>
        private string ButcherToolMissingText(GameState s, CarcassConfig c)
        {
            var own = s.Items.Where(i => OnPlayer(i) && c.RequiredCarriedTools.Contains(i.ItemId)).ToList();
            var legal = own.Where(i => i.ReservedBy == null && !IsLocked(s, i)).ToList();
            var occupiedPositive = own.Any(i => (i.ReservedBy != null || IsLocked(s, i)) && ProductionToolUsable(i));
            return legal.Count > 0 && legal.All(i => !ProductionToolUsable(i)) && !occupiedPositive
                ? ToolBrokenText
                : $"需要随身有效的{string.Join("/", c.RequiredCarriedTools.Select(ItemName))}";
        }

        private string? ValidateButcher(GameState s, ActionRequest r, Plan plan)
        {
            if (!r.InstanceId.HasValue) return "请选择要处理的尸体";
            var inst = FindItem(s, r.InstanceId.Value);
            if (inst == null) return "尸体已不在";
            if (IsSpoiledMatter(inst)) return SpoiledMatterRejectText;   // BM5b2（Q610）
            var c = CarcassOf(inst.ItemId);
            if (c == null) return "这不是可处理的尸体";
            if (!(inst.IsCarried || inst.Owner == s.Ground)) return "只能处理随身或当前地面的尸体";
            if (inst.ReservedBy != null) return "该尸体已被预留";
            if (IsSpoiled(s, inst)) return "尸体已腐败，不能处理";
            var tool = ButcherTool(s, c);
            if (tool == null) return ButcherToolMissingText(s, c);
            plan.UseItem = inst;
            plan.Tool = tool;
            plan.BaseWork = c.ButcherBaseMinutes;   // 制造熟练度不作用于处理尸体
            plan.TargetId = inst.ItemId;
            plan.Label = $"处理{ItemName(inst.ItemId)}";
            foreach (var kv in c.Outputs) plan.Outputs.Add(Line(kv.Key, kv.Value));
            return null;
        }

        private void StartButcher(GameState s, ActionState a, Plan p)
        {
            var inst = p.UseItem!;
            // 尸体逐个实例（堆叠上限1），整件预留
            inst.ReservedBy = a.ActionId;
            a.ReservedItemIds.Add(inst.InstanceId);
            a.ToolInstanceId = p.Tool!.InstanceId;
            a.LockedItemIds.Add(p.Tool.InstanceId);
            GuardProductionTool(a, p.Tool);   // B51a Q701.3：开工 D>0 的本单保障
        }

        private bool CompleteButcher(GameState s, ActionState a)
        {
            if (AnyReservedSpoiled(s, a))
            {
                AbortSpoiled(s, a);
                return false;
            }
            var here = s.Ground;
            var carcass = a.ReservedItemIds.Select(id => FindItem(s, id)).FirstOrDefault(i => i != null);
            var c = carcass == null ? null : CarcassOf(carcass.ItemId);
            foreach (var id in a.ReservedItemIds) s.Items.RemoveAll(i => i.InstanceId == id);
            a.ReservedItemIds.Clear();
            var made = new List<string>();
            if (c != null)
                foreach (var kv in c.Outputs)
                {
                    CreateItems(s, kv.Key, kv.Value, here);
                    made.Add($"{ItemName(kv.Key)}+{kv.Value}");
                }
            var tool = a.ToolInstanceId.HasValue ? FindItem(s, a.ToolInstanceId.Value) : null;
            if (tool != null && c != null) { made.Add(Wear(tool, c.ToolWearOnSuccess)); MarkToolGuardSpent(a, tool); }
            ClearAction(s, a);
            Emit(s, GameEventKind.ActionCompleted, $"{a.Label}完成：{string.Join("，", made)}；产物已放在{ZoneName(s, here)}地面。", null, c?.Outputs.Keys);
            return true;
        }
    }
}
