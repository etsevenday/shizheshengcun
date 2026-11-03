using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 72 世界小时地牢重置（开发计划 v0.9 批次 B22，需求 R69；R03 D2「RESET_READY」、D8「通关72小时后重置」、D9、D10「重置顺序」，A09「E8」，
    /// S01「B3/B4 世界时钟与实例原子事务」，A10 §7）：
    /// <list type="bullet">
    /// <item>计时：只从本代 clearedAt 起算（recordNode 领取/核对成功，Q127），resetEligibleAt=clearedAt+4320；按世界分钟，离线不走（EC15）；未通关反复退出不计时（DR15）。</item>
    /// <item>到点且内部无人才标“可重置”（RESET_READY；Q204 选B，有人时仍“已通关”并注明冷却已到、需离开），不自动换代、不踢人、不删遗物（DR14）。</item>
    /// <item>确认：全队在父地点、实例里没人（没有进行中的远征/撤出）、没有行动/战斗/敌踪/对白、地牢里没有预留中的材料；
    /// 重验 → 预演清单 → 同一事务迁全部旧实例遗留物到父地点“旧实例遗物认领箱” + 关旧代 + 建新代 → 保存。写盘失败回到确认前的状态（不出现两代、不半迁，DR18/SCB03）；
    /// 同一请求号只生效一次，新一代未通关、再点也不会再生一代。</item>
    /// <item>新代刷新：普通箱、守卫回收、固定战（新 worldEnemyId）、房间探索与门；不重发：核心记录/首通包/唯一箱/知识/容量（DR17）。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>一条重置条件（文字 + 是否满足）。</summary>
        public sealed class ResetCondition
        {
            public string Text { get; set; } = "";
            public bool Met { get; set; }
        }

        /// <summary>重置条件逐条（A10 §7“剩余条件”）：本代已通关、已过 72 世界小时、全队在父地点、地牢里没人/没有进行中的远征、没有行动战斗敌踪、没有预留中的材料。</summary>
        public List<ResetCondition> ResetConditions(GameState s, string dungeonId)
        {
            var list = new List<ResetCondition>();
            var d = DungeonCfg(dungeonId);
            if (d == null) return list;
            s.DungeonInstances.TryGetValue(dungeonId, out var inst);
            var cleared = DungeonCleared(s, dungeonId) && inst?.ResetEligibleAt != null;
            list.Add(new ResetCondition { Text = "本代已通关（核心记录已领取或核对）", Met = cleared });
            list.Add(new ResetCondition
            {
                Text = cleared ? $"通关后满{DR.ResetCooldownMinutes / 60}世界小时（{Clock.DayTime(inst!.ResetEligibleAt!.Value)}起）" : $"通关后满{DR.ResetCooldownMinutes / 60}世界小时",
                Met = cleared && s.GameMinute >= inst!.ResetEligibleAt!.Value,
            });
            list.Add(new ResetCondition { Text = $"全队都在{LocName(d.Parent)}", Met = s.CurrentLocation == d.Parent });
            list.Add(new ResetCondition { Text = "地牢里没有人、没有进行中的远征或撤出", Met = s.Expedition?.DungeonId != dungeonId && DungeonAt(s.CurrentLocation) != dungeonId });
            list.Add(new ResetCondition { Text = "没有进行中的行动、战斗、敌踪或对白", Met = !IsBusy(s) && !DialogueLocked(s) });
            var reserved = Config.Resources.Any(r => DungeonOf(r) == dungeonId && s.Resources.TryGetValue(r.NodeId, out var rs) && rs.Reserved > 0)
                           || DungeonLeftovers(s, dungeonId).Any(x => x.Item.ReservedBy != null);
            list.Add(new ResetCondition { Text = "地牢里没有预留中的材料或未完成的领取", Met = !reserved });
            return list;
        }

        /// <summary>现在能否确认重置；可以为 null，否则为第一条未满足的条件。</summary>
        public string? ResetBlocked(GameState s, string dungeonId)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (DungeonCfg(dungeonId) == null) return "地牢不存在";
            var miss = ResetConditions(s, dungeonId).FirstOrDefault(c => !c.Met);
            return miss == null ? null : "还不能重置：" + miss.Text;
        }

        /// <summary>
        /// 重置确认窗（R03 D8、A10 §7）：会刷新的可重复部分、不重发的本局唯一部分、旧实例全部遗留真实物与迁往何处、哪些已腐败、条件逐条。只读。
        /// </summary>
        public ResetPreviewInfo ResetPreview(GameState s, string dungeonId)
        {
            var d = DungeonCfg(dungeonId)!;
            s.DungeonInstances.TryGetValue(dungeonId, out var inst);
            var p = new ResetPreviewInfo
            {
                DungeonId = dungeonId,
                DungeonName = d.Name,
                ParentName = LocName(d.Parent),
                Generation = inst?.Generation ?? 1,
                ClearedAt = inst?.ClearedAt,
                ResetEligibleAt = inst?.ResetEligibleAt,
                Reason = ResetBlocked(s, dungeonId),
                Conditions = ResetConditions(s, dungeonId),
                Destination = $"{LocName(d.Parent)}的旧实例遗物认领箱",
            };
            foreach (var r in Config.Resources.Where(r => r.IsInstance && DungeonOf(r) == dungeonId))
                p.Refresh.Add($"{r.Name}：{PackText(r.Outputs)}（补满）");
            if (SalvageOf(dungeonId) is { } sv) p.Refresh.Add($"守卫回收材料：{PackText(sv.Items)}（新一代守卫战胜利后再发一次）");
            p.Refresh.Add("固定战敌人：新一代第一次到达时重新出现（新的敌人，经验照常）");
            p.Refresh.Add("房间探索度与清理次数回到初值，本代开通的门与核心核对清空（已发现的房间、路线、资源点整局保留）");
            if (RecordResourceOf(dungeonId) is { } rec)
                p.NotReissued.Add(CampaignClaimed(s, rec.NodeId) ? $"{rec.Name}：本局已领，新一代只需再核对（不再发放、不再加容量与知识）" : $"{rec.Name}：还没领，本局唯一一次（新一代领取即通关）");
            if (FirstClearOf(dungeonId) is { } fc) p.NotReissued.Add($"首次通关补给（{PackText(fc.Items)}）：本局只发一次");
            foreach (var r in Config.Resources.Where(r => r.IsCampaignPhysical && DungeonOf(r) == dungeonId))
                p.NotReissued.Add($"{r.Name}：{(CampaignClaimed(s, r.NodeId) ? "已领取，不会复活" : "还没领，新一代仍可领一次（本局唯一）")}");
            p.NotReissued.Add("已经带出的物资、技能与训练、借用装备额度都不变");
            var left = DungeonLeftovers(s, dungeonId);
            p.Leftovers = left.Select(x => $"{ItemName(x.Item.ItemId)}×{x.Item.Count}（{x.From}{(IsSpoiled(s, x.Item) ? "，已腐败" : "")}）").ToList();
            p.SpoiledCount = left.Count(x => IsSpoiled(s, x.Item));
            return p;
        }

        /// <summary>
        /// 确认重置（R03 D8/D10）：请求号幂等 → 重验 → 同一事务迁遗留物到父地点认领箱 + 关旧代 + 建新代（NewDungeonGeneration）。返回结果；
        /// 写盘由调用方负责，用 <see cref="ResetDungeonAtomic"/> 可在写盘失败时整体回滚。
        /// </summary>
        public ActionResult ResetDungeon(GameState s, string dungeonId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = ResetBlocked(s, dungeonId);
            if (why != null) return ActionResult.Fail(why);
            var d = DungeonCfg(dungeonId)!;
            var oldGen = s.DungeonInstances[dungeonId].Generation;
            var moved = DungeonLeftovers(s, dungeonId);
            var gen = NewDungeonGeneration(s, dungeonId);
            if (gen != null) return ActionResult.Fail(gen);
            RememberRequest(s, requestId);
            var newGen = s.DungeonInstances[dungeonId].Generation;
            Emit(s, GameEventKind.Info,
                $"地牢重置：{d.Name}第{oldGen}代关闭，第{newGen}代建立。" +
                (moved.Count > 0 ? $"旧实例遗留物{moved.Sum(x => x.Item.Count)}份已迁到{LocName(d.Parent)}的旧实例遗物认领箱（保质不变，只能在这里取）。" : "旧实例里没有遗留物。") +
                "普通物资箱、守卫回收材料与固定战在新一代刷新；核心记录、首次通关补给与唯一箱不重发。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 重置并写盘的原子事务（R03 D10“任何阶段失败不能出现两代同时有效”、S01“任何写盘失败旧owner保持”、DR18/SCB03/COMP11/COMP15）：
        /// 先存确认前快照 → 重置 → persist(新状态)；persist 返回 false 或抛错时，返回确认前状态（旧代、遗留物 owner、请求号都回到原样，可以重试）。
        /// state 出参为调用方之后应持有的状态。
        /// </summary>
        public ActionResult ResetDungeonAtomic(GameState s, string dungeonId, string? requestId, Func<GameState, bool> persist, out GameState state)
        {
            state = s;
            var snapshot = SaveSerializer.Serialize(s);
            var r = ResetDungeon(s, dungeonId, requestId);
            if (!r.Ok || r.Duplicate) return r;
            bool ok;
            try { ok = persist(s); }
            catch { ok = false; }
            if (ok) return r;
            state = SaveSerializer.Deserialize(snapshot, Config);
            return ActionResult.Fail("写盘失败：这次重置没有生效，旧一代和全部物品保持原样（可以再试一次）");
        }

        /// <summary>
        /// 进入预告/入口摘要里的重置一行（A10 §7）：未通关“不计时”；已通关“可重置于第N日HH:MM（还要X）”；到点且里面没人“已可重置”；
        /// 到点但里面有人（Q204）“冷却已到，但地牢里还有人：离开后才可重置”。
        /// </summary>
        public string ResetStatusText(GameState s, string dungeonId)
        {
            s.DungeonInstances.TryGetValue(dungeonId, out var inst);
            if (!DungeonCleared(s, dungeonId) || inst?.ResetEligibleAt == null)
                return "未通关：不计重置时间（只能继续原实例，已领的箱子、已打掉的敌人不会刷新）";
            var parent = LocName(DungeonCfg(dungeonId)!.Parent);
            var head = $"已通关于{Clock.DayTime(inst.ClearedAt!.Value)}；";
            if (DungeonCooldownDone(s, dungeonId) && DungeonOccupied(s, dungeonId))
                return head + $"冷却已到，但地牢里还有人：全队离开地牢后才显示可重置（再到{parent}确认，不会把人踢出）";
            return DungeonResetReady(s, dungeonId)
                ? head + $"已可重置（在{parent}确认：全队在场、地牢里没有人和未完成的事）"
                : head + $"可重置于{Clock.DayTime(inst.ResetEligibleAt.Value)}（还要{Clock.Duration(inst.ResetEligibleAt.Value - s.GameMinute)}，按世界时间计，离线不走）";
        }

        /// <summary>开发入口（?dev=1）B22：时间快进到本地牢可重置（只改时钟、不结算身体），或直接给出说明。</summary>
        public string DebugResetReady(GameState s, string dungeonId)
        {
            if (!s.DungeonInstances.TryGetValue(dungeonId, out var inst) || !DungeonCleared(s, dungeonId) || inst.ResetEligibleAt == null)
                return "开发：这座地牢本代还没通关（先领取/核对核心记录，或用“本地牢核心已领取”）";
            if (IsBusy(s)) return "开发：行动进行中或有敌踪/战斗，先结束再拨时间";
            if (s.GameMinute < inst.ResetEligibleAt.Value) s.GameMinute = inst.ResetEligibleAt.Value;
            var inside = DungeonOccupied(s, dungeonId) ? "冷却已到，但你还在地牢里：离开后才显示可重置" : "已可重置";
            return $"开发：时间拨到{Clock.DayTime(s.GameMinute)}（只改时钟，不结算身体）；{DungeonCfg(dungeonId)!.Name}{inside}——到{LocName(DungeonCfg(dungeonId)!.Parent)}确认";
        }
    }

    public sealed partial class GameEngine
    {
        /// <summary>开发入口（?dev=1）B22：随身各加 2 份三种战术品（试战斗指令）。</summary>
        public string DebugGiveTactical(GameState s)
        {
            foreach (var t in Config.DungeonRewards.Tactical) CreateItems(s, t.Id, 2, ItemInstance.Carried);
            return $"开发：随身加了{string.Join("、", Config.DungeonRewards.Tactical.Select(t => ItemName(t.Id) + "×2"))}（超出五张卡会弹整理窗）";
        }

        /// <summary>开发入口（?dev=1）B22：某地牢的奖励账与重置状态（代号、通关/可重置时刻、守卫回收与首通包、封存、回收箱与遗物箱件数）。</summary>
        public string DebugRewardLedger(GameState s, string dungeonId)
        {
            var d = DungeonCfg(dungeonId);
            if (d == null) return "开发：地牢不存在";
            s.DungeonInstances.TryGetValue(dungeonId, out var inst);
            var sealedN = SealedItems(s, InstanceId(s, dungeonId)).Sum(i => i.Count);
            int Box(string key) => RemainsItems(s, key).Sum(i => i.Count);
            return $"开发：{d.Name} {InstanceId(s, dungeonId)} · {DungeonStatusName(DungeonStatus(s, dungeonId))}；"
                + (inst?.ClearedAt is int c ? $"通关于{Clock.DayTime(c)}，可重置于{Clock.DayTime(inst.ResetEligibleAt!.Value)}" : "本代未通关")
                + $"；守卫回收{(SalvageClaimed(s, dungeonId) ? "本代已发" : "本代未发")}；首通补给{(FirstClearClaimed(s, dungeonId) ? "已领" : "未领")}"
                + $"；封存{sealedN}份；{LocName(d.Parent)}远征回收箱{Box(RecoveryBoxKey(dungeonId))}份、旧实例遗物认领箱{Box(RelicBoxKey(dungeonId))}份；遗留物{DungeonLeftovers(s, dungeonId).Count}堆。";
        }
    }

    /// <summary>重置确认窗（R03 D8、A10 §7，B22）。</summary>
    public sealed class ResetPreviewInfo
    {
        public string DungeonId { get; set; } = "";
        public string DungeonName { get; set; } = "";
        public string ParentName { get; set; } = "";
        public int Generation { get; set; }
        public int? ClearedAt { get; set; }
        public int? ResetEligibleAt { get; set; }
        /// <summary>不能重置的原因；可以为 null。</summary>
        public string? Reason { get; set; }
        public List<GameEngine.ResetCondition> Conditions { get; set; } = new List<GameEngine.ResetCondition>();
        public List<string> Refresh { get; set; } = new List<string>();
        public List<string> NotReissued { get; set; } = new List<string>();
        public List<string> Leftovers { get; set; } = new List<string>();
        public int SpoiledCount { get; set; }
        public string Destination { get; set; } = "";
    }
}
