using Microsoft.AspNetCore.Components;

namespace Survival.Web.Services;

/// <summary>所有界面组件的基类：会话状态一变就重绘，避免子组件因参数未变而停留在旧画面。</summary>
public abstract class GameComponent : ComponentBase, IDisposable
{
    [Inject] protected GameSession G { get; set; } = null!;

    protected override void OnInitialized()
    {
        G.Changed += OnSessionChanged;
        base.OnInitialized();
    }

    private void OnSessionChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => G.Changed -= OnSessionChanged;
}
