using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>地图清理（02 案）与探索随机收获（03 案）：次数串行恢复、共享掉落表、开始时锁定的隐藏结果。</summary>
    public sealed partial class GameEngine
    {
        // ───────────────────────── 清理次数（02 案 3.1） ─────────────────────────

        /// <summary>按当前世界分钟计算某地的清理次数与恢复累计，不改状态。</summary>
        public (int charges, int progress) ClearChargesAt(GameState s, string locationId)
        {
            var lc = _locs[locationId];
            var cfg = lc.Clear;
            var ls = s.Locations[locationId];
            // 地牢房间每代 1 次：世界分钟、离开、72 小时未重置都不补（S04 Q31 节 instanceClear，B12）
            if (lc.ClearPerGeneration || cfg.RefillIntervalMinutes <= 0) return (ls.ClearCharges, 0);
            var elapsed = s.GameMinute - ls.ClearUpdatedMinute;
            if (elapsed < 0) return (ls.ClearCharges, ls.ClearRefillProgress);
            if (ls.ClearCharges >= cfg.Cap) return (ls.ClearCharges, 0);
            var total = ls.ClearRefillProgress + elapsed;
            var charges = Math.Min(cfg.Cap, ls.ClearCharges + total / cfg.RefillIntervalMinutes);
            var progress = charges >= cfg.Cap ? 0 : total % cfg.RefillIntervalMinutes;
            return (charges, progress);
        }

        /// <summary>距恢复下一次还需的世界分钟；已满为 null。</summary>
        public int? NextClearRefillMinutes(GameState s, string locationId)
        {
            var (charges, progress) = ClearChargesAt(s, locationId);
            var lc = _locs[locationId];
            if (lc.ClearPerGeneration) return null;
            return charges >= lc.Clear.Cap ? (int?)null : lc.Clear.RefillIntervalMinutes - progress;
        }

        /// <summary>清理一次的基础分钟：新节点按逐点配置（Q31 统一 20），旧 8 地点用全局 actions.clearMinutes。</summary>
        public int ClearMinutesAt(string locationId) => _locs[locationId].ClearMinutes ?? Config.Actions.ClearMinutes;

        /// <summary>清理动作的叫法：新节点按 Q31 clearLabel（“清理翻找”/城市公共“整理搜集”），旧 8 地点“清理”。</summary>
        public string ClearLabelAt(string locationId) => _locs[locationId].ClearLabel ?? "清理";

        /// <summary>按钮小字：“次数2/3 · 恢复还需3小时”“次数3/3·已满”；地牢房间“本代次数1/1”“本代已用完”。</summary>
        public string ClearCountText(GameState s, string locationId)
        {
            var lc = _locs[locationId];
            var (charges, _) = ClearChargesAt(s, locationId);
            if (lc.ClearPerGeneration) return charges > 0 ? $"本代次数{charges}/{lc.Clear.Cap}" : "本代已用完（地牢换代后恢复）";
            var next = NextClearRefillMinutes(s, locationId);
            return next == null ? $"次数{charges}/{lc.Clear.Cap}·已满" : $"次数{charges}/{lc.Clear.Cap} · 恢复还需{Clock.Duration(next.Value)}";
        }

        /// <summary>把某地清理次数更新到当前世界分钟。时间倒退时记错误日志，不产生恢复。</summary>
        private void UpdateClearCharges(GameState s, string locationId)
        {
            var ls = s.Locations[locationId];
            if (s.GameMinute < ls.ClearUpdatedMinute)
            {
                Emit(s, GameEventKind.Info, $"{LocName(locationId)}清理次数的上次更新时间晚于当前时间，本次不恢复。");
                return;
            }
            var (charges, progress) = ClearChargesAt(s, locationId);
            ls.ClearCharges = charges;
            ls.ClearRefillProgress = progress;
            ls.ClearUpdatedMinute = s.GameMinute;
        }

        /// <summary>每个 5 分钟段推进时间后更新全部地点：清理、睡眠、移动、维护都计入恢复，浏览和离线不计。</summary>
        private void UpdateAllClearCharges(GameState s)
        {
            foreach (var id in s.Locations.Keys.ToList())
                if (_locs.TryGetValue(id, out var lc) && lc.ClearEnabled) UpdateClearCharges(s, id);
        }

        /// <summary>“次数3/4 · 距恢复1次3小时40分”；已满为“4/4·已满”。</summary>
        public string ClearChargesText(GameState s, string locationId)
        {
            if (_locs[locationId].ClearPerGeneration) return ClearCountText(s, locationId);
            var cap = _locs[locationId].Clear.Cap;
            var (charges, _) = ClearChargesAt(s, locationId);
            var next = NextClearRefillMinutes(s, locationId);
            return next == null ? $"{charges}/{cap}·已满" : $"次数{charges}/{cap} · 距恢复1次{Clock.Duration(next.Value)}";
        }

        // ───────────────────────── 共享掉落表 ─────────────────────────

        public LootTableConfig LootTableOf(string locationId) => Config.LootTables[_locs[locationId].LootTableId];

        /// <summary>开始时锁定本次隐藏结果与表版本；完成前不公开，读档不重抽（00 案 4.1、03 案 3）。</summary>
        private void PlanLoot(GameState s, ActionState a, string locationId)
        {
            var table = LootTableOf(locationId);
            var r = (int)Math.Floor(s.Rng.NextDouble() * 100);
            var e = LootTable.Pick(table, r);
            a.LootPlanned = true;
            a.LootRoll = r;
            a.LootItemId = e.ItemId;
            a.LootCount = e.ItemId == null ? 0 : e.Count;
            a.LootTableVersion = table.Version;
        }

        /// <summary>预览用的可能物资与概率，含“未获得物资”行，按表内顺序。</summary>
        public List<LootOddsLine> LootOdds(string locationId) =>
            LootTableOf(locationId).Entries.Select(e => new LootOddsLine
            {
                ItemId = e.ItemId,
                Name = e.ItemId == null ? "未获得物资" : $"{ItemName(e.ItemId)}×{e.Count}",
                Percent = e.Weight,
            }).ToList();

        /// <summary>按已保存的计划生成随机物品到该地地面（保质期从完成时刻算）。返回结果描述，空结果为 null。</summary>
        private string? GrantLoot(GameState s, ActionState a, string locationId)
        {
            if (!a.LootPlanned || a.LootItemId == null || a.LootCount <= 0) return null;
            CreateItems(s, a.LootItemId, a.LootCount, locationId);
            return $"{ItemName(a.LootItemId)}+{a.LootCount}，已放在{LocName(locationId)}地面";
        }

        // ───────────────────────── 完成提交 ─────────────────────────

        /// <summary>探索完成：探索度、里程碑、随机物品、日志一次提交（00 案 4.2、03 案 5）。</summary>
        private bool CompleteExplore(GameState s, ActionState a)
        {
            ClearAction(s, a);
            var loc = a.TargetId;
            var (old, now, found, ids) = ApplyExplore(s, loc);
            var parts = new List<string>
            {
                $"探索度{old}%→{now}%",
                found.Count > 0 ? $"发现{string.Join("与", found)}" : "未发现新线索",
            };
            if (!a.LegacyNoLoot)
            {
                var got = GrantLoot(s, a, loc);
                parts.Add(got ?? "本次未找到可用物资");
                if (got != null) ids.Add(a.LootItemId!);
            }
            var text = string.Join("；", parts) + "。";
            if (now >= 100) text += "此地已充分探索。";
            Emit(s, GameEventKind.ActionCompleted, text, null, ids);
            return true;
        }

        /// <summary>清理完成：有产物落当前地面，空结果只记日志；不改探索度、不发现任何东西（02 案 6、7）。</summary>
        private bool CompleteClear(GameState s, ActionState a)
        {
            ClearAction(s, a);
            var loc = a.TargetId;
            var got = GrantLoot(s, a, loc);
            var label = ClearLabelAt(loc);
            var text = got != null ? $"{label}完成：{got}。" : $"{label}完成：本次未找到可用物资；已消耗1次{label}机会。";
            Emit(s, GameEventKind.ActionCompleted, text, null, got != null ? new[] { a.LootItemId! } : null);
            Emit(s, GameEventKind.Info, $"{LocName(loc)}{label}{ClearChargesText(s, loc)}；本次耗时{Clock.Duration(a.ElapsedMinutes)}。");
            return true;
        }
    }
}
