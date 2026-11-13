using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>这一下 Esc 由谁承接（B43，S14 2026-11-12「返回 ESC／取消 ESC：哪个真实控件承接当前Esc层级，就在哪个控件显示」）。</summary>
public enum EscOwner
{
    /// <summary>没有游戏局（标题页等）。</summary>
    None,
    /// <summary>没有可收的子层：Esc 打开当前情境的菜单（顶栏或战斗区的「菜单 ESC」）。</summary>
    OpenMenu,
    /// <summary>游戏菜单／结算菜单开着：底部「继续游戏／返回结算」收起菜单。</summary>
    Menu,
    /// <summary>战斗层菜单开着：底部「返回战斗／继续游戏」收起菜单。</summary>
    BattleMenu,
    /// <summary>死亡结算页的「查看记录」层。</summary>
    DeathLog,
    DropQty,
    Status,
    /// <summary>居中局部盘（就近盘、点火、食饮、选卡盘）：盘里的「取消」。</summary>
    Disk,
    Pop,
    CityPick,
    CityMap,
    Ignite,
    Feed,
    /// <summary>整备交接盘或比较／来源工作区：「返回」。</summary>
    GearSub,
    /// <summary>面板开着时选中卡的操作（先清选中）。</summary>
    PanelSelection,
    /// <summary>BM4a（Q554）：成长页练习盘开着——盘内「返回」承接（只收盘、丢草稿），下一次 ESC 才关成长页。</summary>
    PracticeBack,
    /// <summary>B45：火堆加工菜单的内容层／加工位层「返回」（退回父层，主层才由 Panel 关闭）。</summary>
    FacBack,
    /// <summary>系统说明内退一级：「‹ 返回」（目录层时等于关闭）。</summary>
    HelpBack,
    /// <summary>一级面板本身：「关闭／取消／返回」。</summary>
    Panel,
    /// <summary>右栏人物二级页：「‹ 人物」。</summary>
    CharPage,
    Selection,
    FeedTail,
    // 全屏战斗（BattleEscape 的顺序）
    MedTray,
    BattleHelp,
    BattleDrawer,
    /// <summary>B34a：全屏战斗操作台下方展开的「战斗信息／战报」阅读区（Q570）。</summary>
    BattleInfo,
    BattleAction,
    BattleView,
}

public sealed partial class GameSession
{
    private bool ShelfSelectionKind => Selected.Kind is SelectionKind.ItemGroup or SelectionKind.Tool or SelectionKind.PotLiquid;

    /// <summary>
    /// B43：当前这一下 Esc 由哪一层承接——与 GameScreen.Escape 实际执行的顺序同源（常态 <see cref="NormalEscOwner"/>、
    /// 全屏战斗 <see cref="BattleEscape"/> 的顺序；B41e3 起没有“敌踪选择”层），供界面只在承接本次 Esc 的那个控件上显示「ESC」键帽。
    /// 对白锁时 Esc 不收背后子层、直接开菜单（Q322）；反馈窗由窗口自己处理，不在这里。
    /// </summary>
    public EscOwner EscOwner
    {
        get
        {
            if (State == null) return EscOwner.None;
            if (S.Progress.Dead) return MenuOpen ? EscOwner.Menu : DeathLogOpen ? EscOwner.DeathLog : EscOwner.OpenMenu;
            if (MenuOpen) return EscOwner.Menu;
            if (BattleMenuOpen) return EscOwner.BattleMenu;
            if (DialogueLocked) return EscOwner.OpenMenu;
            if (BattleFullscreen) return BattleEscOwner();
            return NormalEscOwner();
        }
    }

    /// <summary>全屏战斗可关闭子层（同 <see cref="BattleEscape"/> 的顺序；右栏查看敌人／队友不算子层，见 <see cref="BattleHasEscLayer"/>）。</summary>
    private EscOwner BattleEscOwner()
    {
        if (!BattleHasEscLayer) return EscOwner.OpenMenu;
        if (PendingDrop != null) return EscOwner.DropQty;
        if (MedTrayOpen) return EscOwner.MedTray;
        if (Pop != null) return EscOwner.Pop;
        if (StatusPinned != null) return EscOwner.Status;
        if (BattleDrawer == "help") return EscOwner.BattleHelp;
        if (BattleDrawer != null) return EscOwner.BattleDrawer;
        if (BattleInfoPage != null) return EscOwner.BattleInfo;
        if (BattleAction != null) return EscOwner.BattleAction;
        return EscOwner.OpenMenu;
    }

    /// <summary>
    /// 常态可关闭的子层，每次只退一级：数量框 → 状态浮层 → 居中局部盘 → 就近操作浮窗 → 鸟瞰说明/鸟瞰 → 点火工具菜单 → 食饮与交接浮窗 → 整备子层
    /// → 面板里的选中 → 系统说明逐层 → 面板 → 人物二级页 → 清选中 → 说明里挂着的食饮草稿。（原 GameScreen.NormalLayer／NormalStep 的顺序，B43 移到这里同源。）
    /// </summary>
    public EscOwner NormalEscOwner()
    {
        if (PendingDrop != null) return EscOwner.DropQty;
        if (StatusPinned != null) return EscOwner.Status;
        if (DiskMaskShown) return EscOwner.Disk;
        if (Pop != null) return EscOwner.Pop;
        if (Panel == "citymap" && CityPick != null) return EscOwner.CityPick;
        if (Panel == "citymap") return EscOwner.CityMap;
        if (IgniteMenuOpen) return EscOwner.Ignite;
        if (FeedPending != null && Panel != "help") return EscOwner.Feed;
        if (GearHasSubLayer) return EscOwner.GearSub;
        if (Panel == FireMenuPanel && FireMenuLevel > 0) return EscOwner.FacBack;
        if (Panel == "growth" && PracticeOpen) return EscOwner.PracticeBack;
        if (Panel != null && Panel != "help" && Panel != FireMenuPanel && !CardPopCovered && ShelfSelectionKind) return EscOwner.PanelSelection;
        if (Panel == "help") return EscOwner.HelpBack;
        if (Panel != null) return EscOwner.Panel;
        if (CharPage != null && DetailTab == DetailTab.Character) return EscOwner.CharPage;
        if (Selected.Kind != SelectionKind.None) return EscOwner.Selection;
        if (FeedPending != null) return EscOwner.FeedTail;
        return EscOwner.OpenMenu;
    }

    /// <summary>居中局部盘里的「取消／关闭」此刻承接 Esc（盘只有一个，所以盘里的取消即是这一层）。</summary>
    public bool EscOnDisk => EscOwner is EscOwner.Disk or EscOwner.Pop or EscOwner.Ignite or EscOwner.Feed;

    /// <summary>系统说明「‹ 返回」此刻承接 Esc（不在目录层；从详情定位进来的返回即关闭，也算返回键）。</summary>
    public bool EscOnHelpBack => (EscOwner == EscOwner.HelpBack || EscOwner == EscOwner.BattleHelp) && HelpTopic != null;

    /// <summary>系统说明「关闭」此刻承接 Esc（目录层的退一级就是关闭）。</summary>
    public bool EscOnHelpClose => (EscOwner == EscOwner.HelpBack || EscOwner == EscOwner.BattleHelp) && HelpTopic == null;
}
