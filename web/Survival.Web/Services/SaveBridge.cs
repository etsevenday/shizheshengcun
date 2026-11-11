using Microsoft.JSInterop;

namespace Survival.Web.Services;

/// <summary>
/// B40 多存档的页面回调（app.js initSaves）：每秒一次的现实计时（Q335 / Q408）与另一页面改了存档键的 storage 事件（Q381）。
/// </summary>
public sealed class SaveBridge
{
    private readonly GameSession _g;

    public SaveBridge(GameSession g) => _g = g;

    [JSInvokable]
    public void OnTimer() => _g.OnSaveTimer();

    [JSInvokable]
    public void OnStorage() => _g.OnSaveStorageChanged();
}
