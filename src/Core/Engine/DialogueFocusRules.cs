namespace Survival.Core.Engine
{
    /// <summary>
    /// BM6（S14「2026-11-14 BM5c／BM5a Q620—Q628」§Q625、§Q625／Q628、§Q628，沿 §Q612）：人物对白的背景聚焦参数与白名单。
    /// 普通对白、全屏面板上的对白与全屏战斗内对白用同一套：对白以外整屏背景一次性压暗（黑色遮罩 <see cref="Dim"/>）＋模糊（<see cref="BlurPx"/> CSS 像素），
    /// 对白表面微提亮（<see cref="Lift"/>），入场 <see cref="InMs"/>／退场 <see cref="ExitMs"/> 毫秒（标准档基准，× 动画速度 0.5／1／1.5）。
    /// 只是视觉：不改输入权限、台词时序、世界时间或战斗推进。网页壳的 CSS 与 app.js survivalFocus 按这里的值实现。
    /// </summary>
    public static class DialogueFocus
    {
        /// <summary>背景黑色遮罩不透明度（S14 默认 0.48）。</summary>
        public const double Dim = 0.48;
        /// <summary>背景模糊半径（CSS 像素，S14 默认 2；正常目标必须有模糊，不静默设 0）。</summary>
        public const int BlurPx = 2;
        /// <summary>对白表面亮度倍率（S14「约提高6%」）。</summary>
        public const double Lift = 1.06;
        /// <summary>入场时长（标准档，毫秒）。</summary>
        public const int InMs = 180;
        /// <summary>退场时长（标准档，毫秒）。</summary>
        public const int ExitMs = 140;

        /// <summary>
        /// 主界面白名单（Q625）：对白层本身，以及原对白期间本来就可用的顶栏「菜单」、底栏存档处理按钮（「在此页面继续保存」「重试」）与结果提示的 ×。
        /// 这些元素在遮罩上挖洞保持原亮度、清晰与原权限；原本禁用的仍禁用，不存在的不画。
        /// </summary>
        public const string MainKeep = ".center-scroll.four > .dlg-layer, .top [data-menu-key], .footer .save button, .toast > button";

        /// <summary>
        /// 全屏战斗白名单（Q628）：只有战斗对白层本身。战斗顶条（含战斗「菜单」键）在对白期间原本就不受理点击（ESC 开菜单），
        /// 外层顶栏／底栏被全屏战斗盖住，不因白名单获得本来没有的点击或保存权限。
        /// </summary>
        public const string BattleKeep = ".bfs-dlg > .dlg-layer";

        /// <summary>主界面遮罩是否亮着：对白锁期间，且不在全屏战斗（含收束停留）、未进入死亡结算。</summary>
        public static bool MainOn(bool dialogueLocked, bool fullBattle, bool dead) => dialogueLocked && !fullBattle && !dead;

        /// <summary>
        /// 全屏战斗遮罩是否亮着：对白已接管——对白锁期间且没有正在独占播放的特写（Q572 先播完已提交出手再承接对白；
        /// 播放中只按原播放输入语义，不预开对白聚焦）。
        /// </summary>
        public static bool BattleOn(bool dialogueLocked, bool playbackLock) => dialogueLocked && !playbackLock;
    }
}
