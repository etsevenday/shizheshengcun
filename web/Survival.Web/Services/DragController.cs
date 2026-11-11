using Microsoft.JSInterop;
using Newtonsoft.Json;
using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>
/// drag.js 的 .NET 一侧：起拖时一次性解析全部可见落点，松手时重新校验并执行。
/// 白名单与校验全部在内核 GameEngine.ResolveDrop，这里只做键的解析与分派。
/// </summary>
public sealed class DragController
{
    private readonly GameSession G;
    private string _requestId = "";

    public DragController(GameSession session) => G = session;

    [JSInvokable]
    public string DragBegin(string sourceKey, string[] targetKeys)
    {
        _requestId = Guid.NewGuid().ToString("N");
        var map = new Dictionary<string, object>();
        // B33（Q431-2、Q453-6）：整备来源卡只能拖到七槽——相容槽等同点选候选并明确选该槽（只打开比较），不相容槽给原装卸判定原因；其余落点不响应
        if (GearDrag(sourceKey) is int gid)
        {
            if (G.State == null || G.E.FindItem(G.S, gid) is not { } git) return "{}";
            foreach (var key in targetKeys.Where(k => k.StartsWith(GearSlotKey)))
            {
                var slot = key.Substring(GearSlotKey.Length);
                var ok = G.E.SlotsFor(git.ItemId).Contains(slot);
                map[key] = new { ok, kind = ok ? "preview" : "no", label = "", reason = ok ? "" : G.E.EquipBlocked(G.S, G.GearActorId, gid, slot) ?? GameEngine.Unsupported, fall = false, qty = false };
            }
            return JsonConvert.SerializeObject(map);
        }
        var src = DropSource.Parse(sourceKey);
        if (G.State == null || src == null) return "{}";
        // B24（S07 中枢联动归档 1）：主城鸟瞰浏览中第四行只看，不起拖、不成为建筑/NPC 投料热区
        if (G.CityBrowsing)
        {
            foreach (var key in targetKeys) map[key] = new { ok = false, kind = "no", label = "", reason = GameSession.CityReadOnlyText, fall = false, qty = false };
            return JsonConvert.SerializeObject(map);
        }
        foreach (var key in targetKeys)
        {
            var dst = DropTarget.Parse(key);
            if (dst == null) continue;
            var i = G.E.ResolveDrop(G.S, src, dst);
            map[key] = new
            {
                ok = i.Ok,
                kind = !i.Ok ? "no" : i.Verb == DropVerb.Reorder ? "reorder" : i.IsPreview ? "preview" : i.StartsAction ? "start" : "instant",
                label = i.Label,
                reason = i.Reason,
                fall = i.FallThrough,
                qty = i.Ok && i.Quantifiable,
            };
        }
        return JsonConvert.SerializeObject(map);
    }

    [JSInvokable]
    public string DragDrop(string sourceKey, string targetKey, int index, bool shift)
    {
        if (G.State == null) return Result(false, "没有进行中的游戏");
        if (G.CityBrowsing) return Result(false, GameSession.CityReadOnlyText);
        if (GearDrag(sourceKey) is int gid)
        {
            if (!targetKey.StartsWith(GearSlotKey)) return Result(false, GameEngine.Unsupported);
            var r = G.GearDropToSlot(gid, targetKey.Substring(GearSlotKey.Length));
            return Result(r.Ok, r.Reason);
        }
        var src = DropSource.Parse(sourceKey);
        var dst = DropTarget.Parse(targetKey, index);
        if (src == null || dst == null) return Result(false, GameEngine.Unsupported);
        // 松手时按当前状态重新解析：起拖后状态可能已变（引用失效即刷新）
        var intent = G.E.ResolveDrop(G.S, src, dst);
        if (!intent.Ok) return Result(false, intent.Reason);
        if (shift && intent.Quantifiable)
        {
            G.AskDropQuantity(intent, _requestId);
            return Result(true, "");
        }
        var res = G.ExecuteDrop(intent, 1, _requestId);
        return Result(res.Ok, res.Reason);
    }

    /// <summary>
    /// 拖动中指针所在落点变化（drag.js 只在落点变化时调用）：打火器悬停在合法火堆落点时，右栏临时显示该点火的预测
    /// （具体工具、火堆、5分钟、身体与燃料变化、是否自动投木，v0.7 第 4.1 节）；离开或松手传 null 清空。
    /// </summary>
    [JSInvokable]
    public void DragHover(string? sourceKey, string? targetKey)
    {
        if (G.State == null) return;
        var src = sourceKey == null ? null : DropSource.Parse(sourceKey);
        var dst = targetKey == null ? null : DropTarget.Parse(targetKey);
        if (src == null || dst == null) { G.Hover(null); return; }
        var intent = G.E.ResolveDrop(G.S, src, dst);
        if (intent.Ok && intent.StartsAction && intent.Action != null)
        {
            var lighter = G.S.Items.FirstOrDefault(i => i.InstanceId == intent.Action.InstanceId);
            G.Hover(intent.Action, lighter == null ? "点燃火堆" : $"用{G.E.ItemName(lighter.ItemId)}点火");
        }
        else G.Hover(null);
    }

    /// <summary>整备来源卡的拖拽键「gear:{实例号}」与七槽落点键「gearslot:{槽}」（B33）。</summary>
    public const string GearSrcKey = "gear:";
    public const string GearSlotKey = "gearslot:";

    private static int? GearDrag(string sourceKey) =>
        sourceKey.StartsWith(GearSrcKey) && int.TryParse(sourceKey.Substring(GearSrcKey.Length), out var id) ? id : null;

    private static string Result(bool ok, string reason) => JsonConvert.SerializeObject(new { ok, reason });
}
