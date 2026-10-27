// 全局鼠标滚轮（基础UI布局与操作修订案 v0.7 第 5 节）。
// 一次输入只由一个容器消费：从鼠标下的元素向上找第一个“在这个方向还能动”的容器，手动滚动并阻止默认行为，父级不再同时滚动。
//   · 纯竖向滚轮遇到只横向溢出的栏（data-wheel="x"：相邻地图、第二行、地面、随身）→ 向下滚=向右、向上滚=向左；
//   · 同时有横纵溢出的区域：普通滚轮纵向，Shift+滚轮横向；
//   · 触控板原生横向输入按原生方向，只把纯竖向鼠标滚轮映射到横向栏，不二次反转或放大；
//   · 到边界才交给父级：横向栏到边界交给中部纵滚；纵向列表（如点火菜单、锅卡所在区）到底后只交给更外层的纵向容器，
//     不突然改成外层横向滑动；
//   · Ctrl+滚轮放行给浏览器缩放；数量框是文本框，滚轮不改数量；滚动、查看不消耗游戏时间。
// 拖拽中同样生效：drag.js 监听滚动，用最后的指针坐标重算高亮与落点。
(function () {
  if (window.__survivalWheel) return;
  window.__survivalWheel = true;

  var LINE = 16;

  function scrollable(el, axis) {
    var cs = getComputedStyle(el);
    var ov = axis === 'x' ? cs.overflowX : cs.overflowY;
    if (ov !== 'auto' && ov !== 'scroll') return false;
    return axis === 'x' ? el.scrollWidth > el.clientWidth + 1 : el.scrollHeight > el.clientHeight + 1;
  }

  function canMove(el, axis, d) {
    if (axis === 'x') return d > 0 ? el.scrollLeft + el.clientWidth < el.scrollWidth - 1 : el.scrollLeft > 0;
    return d > 0 ? el.scrollTop + el.clientHeight < el.scrollHeight - 1 : el.scrollTop > 0;
  }

  // 只横向溢出的卡片栏：显式标了 data-wheel="x"，或只有横向可滚
  function xBar(el) {
    return el.getAttribute && (el.getAttribute('data-wheel') === 'x' || (scrollable(el, 'x') && !scrollable(el, 'y')));
  }

  function onWheel(ev) {
    if (ev.ctrlKey || ev.defaultPrevented) return;            // Ctrl+滚轮：浏览器缩放
    if (window.FeedbackWidget && FeedbackWidget.isOpen && FeedbackWidget.isOpen()) return;
    var k = ev.deltaMode === 1 ? LINE : ev.deltaMode === 2 ? window.innerHeight : 1;
    var dx = ev.deltaX * k, dy = ev.deltaY * k;
    if (ev.shiftKey && dx === 0) { dx = dy; dy = 0; }       // Shift+滚轮：横向
    var horizontal = Math.abs(dx) > Math.abs(dy);
    var d = horizontal ? dx : dy;
    if (d === 0) return;
    var mouseVertical = !horizontal && !ev.shiftKey;         // 纯竖向滚轮：可映射到横向栏
    // 人物对话层（S14 §10.3）：滚轮只回看本次对话流，不推进对白、到边界也不透传给背后的卡片栏或中部
    var dlg = ev.target.closest && ev.target.closest('.dlg-layer');
    if (dlg) {
      ev.preventDefault();
      var stream = dlg.querySelector('.dlg-stream');
      if (stream && !horizontal) stream.scrollTop += d;
      return;
    }
    if (walk(ev, ev.target, horizontal, mouseVertical, d)) return;
    // 鼠标下是没有溢出的提示浮层（结果提示条、落点失败提示）：交给它下面的界面，不吞掉这次滚动（试玩修复 B6）。
    // 只对这类提示生效：暂停层、弹窗等遮罩不把滚轮漏给背后的内容。
    var tip = ev.target.closest && ev.target.closest('.toast, .drop-flash');
    if (!tip) return;
    var stack = document.elementsFromPoint ? document.elementsFromPoint(ev.clientX, ev.clientY) : [];
    for (var i = 0; i < stack.length; i++) {
      var under = stack[i];
      if (tip.contains(under) || under.contains(tip)) continue;
      if (walk(ev, under, horizontal, mouseVertical, d)) return;
    }
  }

  // 从 start 向上找第一个在该方向还能动的容器并滚动；找到返回 true
  function walk(ev, start, horizontal, mouseVertical, d) {
    var passedVertical = false;
    for (var el = start; el && el !== document.documentElement; el = el.parentElement) {
      if (el.nodeType !== 1) continue;
      if (horizontal) {
        if (scrollable(el, 'x') && canMove(el, 'x', d)) { consume(ev, el, 'x', d); return true; }
        continue;
      }
      // 标了 data-wheel="x" 的卡片栏（地面、随身、设施行、相邻地图）同时纵向溢出时（卡下操作条、锅卡更高，S14 §1.3 区内纵滚）：
      // 纯竖向滚轮先横向翻卡，到横向边界再纵向滚动本区，仍不透传给父级
      if (mouseVertical && !passedVertical && el.getAttribute && el.getAttribute('data-wheel') === 'x' && scrollable(el, 'x') && canMove(el, 'x', d)) {
        consume(ev, el, 'x', d); return true;
      }
      if (scrollable(el, 'y')) {
        if (canMove(el, 'y', d)) { consume(ev, el, 'y', d); return true; }
        passedVertical = true;                              // 纵向列表到底：只交给更外层的纵向容器
        continue;
      }
      if (mouseVertical && !passedVertical && xBar(el) && scrollable(el, 'x')) {
        if (canMove(el, 'x', d)) { consume(ev, el, 'x', d); return true; }
        // 横向栏到边界：继续向上，交给中部纵滚
      }
    }
    return false;
  }

  function consume(ev, el, axis, d) {
    ev.preventDefault();
    if (axis === 'x') el.scrollLeft += d; else el.scrollTop += d;
  }

  document.addEventListener('wheel', onWheel, { capture: true, passive: false });
})();
