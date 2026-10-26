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
        var src = DropSource.Parse(sourceKey);
        if (G.State == null || src == null) return "{}";
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

    private static string Result(bool ok, string reason) => JsonConvert.SerializeObject(new { ok, reason });
}
