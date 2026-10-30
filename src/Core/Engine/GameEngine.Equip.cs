using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 七个穿戴槽（S06 2026-10-30 全系统更新目标 §1、R02 §1/§5、A05 §5；开发计划 v0.9 B3 H2–H5、H8）：
    /// 穿戴 owner = "equip:&lt;actorId&gt;:&lt;槽&gt;"，不占五张物资卡，重量计本人；两武器槽同 kind 最多 1 件。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string WornMoveBlocked = "穿戴中：先在装备区卸下";

        /// <summary>某角色某槽当前穿戴的物品；空槽为 null（同槽多件只会出现在损坏数据里，取实例号最小者）。</summary>
        public ItemInstance? WornItem(GameState s, string actorId, string slot)
        {
            var owner = ItemInstance.EquipOwner(actorId, slot);
            return s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId).FirstOrDefault();
        }

        /// <summary>某角色全部穿戴物（按七槽顺序）。</summary>
        public List<ItemInstance> WornItems(GameState s, string actorId = GameState.PlayerId) =>
            EquipSlots.All.Select(slot => WornItem(s, actorId, slot)).Where(i => i != null).Select(i => i!).ToList();

        /// <summary>物品的武器类型（A05 kind）；不是武器为 null。</summary>
        public string? WeaponKindOf(string itemId) => Weapon(itemId)?.WeaponKind;
    }
}
