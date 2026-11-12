using Microsoft.JSInterop;

namespace Survival.Web.Services;

/// <summary>
/// B42：app.js survivalPeek 的 .NET 一侧。页面上带 data-peek 的卡片或条目在指针真实移动进入（鼠标／笔，未按键、未拖拽）
/// 或键盘焦点到达（:focus-visible）时调用；触屏、DOM 重排而指针未动、被 inert 遮挡的热区都不会到这里。
/// 只转给 <see cref="GameSession.PeekEnter"/>，不执行任何操作。
/// </summary>
public sealed class PeekController
{
    private readonly GameSession G;

    public PeekController(GameSession session) => G = session;

    [JSInvokable]
    public void PeekEnter(string key) => G.PeekEnter(key);
}
