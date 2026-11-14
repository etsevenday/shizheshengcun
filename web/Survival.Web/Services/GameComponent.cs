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

    /// <summary>
    /// B34 试玩修复 E2：战斗收束前的短反馈停留中，界面组件一律不重绘（战斗画面冻结在提交前最后一帧，背后组件也不读已收束的战斗）；
    /// 只有承接停留与收束的外壳（GameScreen）照常绘制。
    /// </summary>
    protected virtual bool RenderDuringClosingHold => false;

    protected override bool ShouldRender() => !G.BattleClosingHold || RenderDuringClosingHold;

    public virtual void Dispose() => G.Changed -= OnSessionChanged;
}
