using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 安全屋固定柜（S09 X2.6、S06 X4；开发计划 v0.9 B17 R54）：每屋一柜，owner = safehouseStorage:&lt;safehouseId&gt;，与角色无关、不进包、
    /// 不计携重、不设五卡上限，容量 200000g 按柜内根实例连同后代（锅内水与食材）各算一次。只有人在该屋屋内且空闲时 0 分钟存取：
    /// 柜↔随身、柜↔屋内地面；超柜容量/个体五卡/30kg 整次拒绝、不静默取一部分、不自动塞给队友。柜子不保鲜（绝对到期时间不刷新）。
    /// 远处只能看“上次记录”，不能取放或当作可用材料。
    /// </summary>
    public sealed partial class GameEngine
    {
        public int CabinetCapacityGrams => Config.Safehouse.StorageCapacityGrams;

        /// <summary>某地点安全屋柜子的 owner；没建成为 null。</summary>
        public string? CabinetOwnerOf(GameState s, string locationId)
        {
            var sh = SafehouseAt(s, locationId);
            return sh != null && sh.Built ? ItemInstance.SafehouseStorageOwner(sh.Id) : null;
        }

        /// <summary>柜内根实例（按物品、到期、实例号）。</summary>
        public List<ItemInstance> CabinetItems(GameState s, string locationId)
        {
            var owner = CabinetOwnerOf(s, locationId);
            return owner == null ? new List<ItemInstance>()
                : s.Items.Where(i => i.Owner == owner).OrderBy(i => i.ItemId).ThenBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).ToList();
        }

        /// <summary>柜内总克重（根实例连同锅内后代，各算一次）。</summary>
        public int CabinetGrams(GameState s, string locationId) => CabinetItems(s, locationId).Sum(i => ItemGrams(s, i));

        /// <summary>现在能不能动柜子：只能在该屋屋内、空闲时；能为 null。</summary>
        public string? CabinetBlocked(GameState s)
        {
            if (s.CurrentZone != SafehouseZone || !SafehouseBuilt(s, s.CurrentLocation)) return "只有在安全屋屋内才能存取柜子";
            if (s.Battle != null) return "战斗中不能整理物品";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (IsBusy(s)) return "行动进行中，不能存取柜子";
            return null;
        }

        /// <summary>这件随身/屋内地面的物品最多能放进柜子几份（柜容量整份核算）；不能放时给原因。</summary>
        public int MaxStoreInCabinet(GameState s, ItemInstance inst, out string? reason)
        {
            reason = CabinetBlocked(s);
            if (reason != null) return 0;
            if (!(inst.IsCarried || inst.Owner == s.Ground)) { reason = "只能把随身或屋内地面的物品放进柜子"; return 0; }
            if (inst.ReservedBy != null) { reason = "该物品已被预留"; return 0; }
            if (IsLocked(s, inst)) { reason = "行动占用：该物品正被当前行动使用"; return 0; }
            var unit = inst.Count > 0 ? ItemGrams(s, inst) / inst.Count : 0;
            var room = CabinetCapacityGrams - CabinetGrams(s, s.CurrentLocation);
            var max = unit <= 0 ? inst.Count : System.Math.Min(inst.Count, room / unit);
            if (max <= 0) reason = $"柜子已满（{Fmt.Kg(CabinetGrams(s, s.CurrentLocation))}/{Fmt.Kg(CabinetCapacityGrams)}），放不下{ItemName(inst.ItemId)}";
            return System.Math.Max(0, max);
        }

        /// <summary>这件柜内物品最多能取几份到 zone（随身：五卡与 30kg；屋内地面：不限）。</summary>
        public int MaxTakeFromCabinet(GameState s, ItemInstance inst, string zone, out string? reason)
        {
            reason = CabinetBlocked(s);
            if (reason != null) return 0;
            if (inst.Owner != CabinetOwnerOf(s, s.CurrentLocation)) { reason = "不是这间安全屋柜子里的物品"; return 0; }
            if (zone == s.Ground) return inst.Count;
            if (zone != ItemInstance.Carried) { reason = "只能取到随身或屋内地面"; return 0; }
            var max = MaxPickupBatch(s, inst, inst.Count);
            if (max < inst.Count) reason = PickupBlockedText(s, inst, inst.Count);
            return max;
        }

        private ActionResult StoreInCabinet(GameState s, InstantRequest r)
        {
            var inst = FindItem(s, r.InstanceId);
            if (inst == null) return ActionResult.Fail("物品已不在");
            var max = MaxStoreInCabinet(s, inst, out var why);
            if (r.Count < 1 || r.Count > inst.Count) return ActionResult.Fail(why ?? "数量无效");
            if (r.Count > max) return ActionResult.Fail(why ?? $"柜子最多还能放{max}份{ItemName(inst.ItemId)}（容量{Fmt.Kg(CabinetCapacityGrams)}）", max);
            var from = inst.IsCarried ? "随身" : "屋内地面";
            var name = ItemName(inst.ItemId);
            MoveItems(s, inst, r.Count, CabinetOwnerOf(s, s.CurrentLocation)!);
            RecordSafehouse(s, s.CurrentLocation);
            Emit(s, GameEventKind.Info, $"把{from}的{name}×{r.Count}放进{SafehouseName(s, s.CurrentLocation)}的柜子（柜内{Fmt.Kg(CabinetGrams(s, s.CurrentLocation))}/{Fmt.Kg(CabinetCapacityGrams)}）。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        private ActionResult TakeFromCabinet(GameState s, InstantRequest r)
        {
            var inst = FindItem(s, r.InstanceId);
            if (inst == null) return ActionResult.Fail("物品已不在");
            var zone = string.IsNullOrEmpty(r.Zone) ? ItemInstance.Carried : r.Zone;
            var max = MaxTakeFromCabinet(s, inst, zone, out var why);
            if (r.Count < 1 || r.Count > inst.Count) return ActionResult.Fail(CabinetBlocked(s) ?? "数量无效");
            // 超五卡/30kg 整次拒绝，不静默只取一部分（S09 X2.6、SH05）
            if (r.Count > max) return ActionResult.Fail(why ?? $"最多可取{max}份", max);
            var name = ItemName(inst.ItemId);
            MoveItems(s, inst, r.Count, zone);
            RecordSafehouse(s, s.CurrentLocation);
            Emit(s, GameEventKind.Info, $"从{SafehouseName(s, s.CurrentLocation)}的柜子取出{name}×{r.Count}到{(zone == ItemInstance.Carried ? "随身" : "屋内地面")}。");
            NoteTransfer(s);
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>远处查看某屋柜子的“上次记录”（只读、不是可用材料）：(记录, 记录时刻)；没建成或从没记录为空表与 null。</summary>
        public (List<SafehouseRecordLine> Lines, int? Minute) CabinetRecord(GameState s, string locationId)
        {
            var sh = SafehouseAt(s, locationId);
            return sh == null || !sh.Built ? (new List<SafehouseRecordLine>(), null) : (sh.LastRecord, sh.LastRecordMinute);
        }

        /// <summary>全部已建成的安全屋所在地点（按地点配置顺序）。</summary>
        public List<string> BuiltSafehouses(GameState s) =>
            Config.Locations.Where(l => SafehouseBuilt(s, l.Id)).Select(l => l.Id).ToList();
    }
}
