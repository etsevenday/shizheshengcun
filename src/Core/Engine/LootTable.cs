using Survival.Core.Config;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 地块共享掉落表的抽取契约（地图清理案 5.1）：按条目顺序构建累积区间，
    /// 从 [0,100) 取一个整数命中唯一结果；不逐行独立判定、不连抽、不保底。
    /// </summary>
    public static class LootTable
    {
        public static LootEntryConfig Pick(LootTableConfig table, int r)
        {
            var acc = 0;
            foreach (var e in table.Entries)
            {
                acc += e.Weight;
                if (r < acc) return e;
            }
            return table.Entries[table.Entries.Count - 1];
        }
    }
}
