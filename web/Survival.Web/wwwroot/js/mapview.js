// 分层地图视口（开发计划 v0.9 批次 B12，R38；A10「A10-EXUI」§1）：拖动平移、居中、阅读位置记忆。
// 缩放档与坐标换算在 C#（MapOverlay.razor）里做；这里只管滚动位置。浏览、拖动都不推进游戏时间。
//   · 拖动：按下后移动超过 4px 才算拖（不误吞点选），拖完紧跟的那次 click 被吞掉；
//   · 每个视口的滚动中心按 data-map-key（当前位置|层|缩放）记在内存里，再打开同一状态时恢复（EXUI13）；
//   · 滚轮由 wheel.js 统一处理（视口横纵都可滚：普通滚轮纵向、Shift+滚轮横向，Ctrl+滚轮放给浏览器）。
window.survivalMap = (function () {
  var saved = {};

  function remember(el) {
    var key = el.getAttribute('data-map-key');
    if (key) saved[key] = [el.scrollLeft + el.clientWidth / 2, el.scrollTop + el.clientHeight / 2];
  }

  function attach(el) {
    if (!el || el.__mapPan) return;
    el.__mapPan = true;
    var start = null, dragged = false;
    el.addEventListener('pointerdown', function (ev) {
      if (ev.button !== 0) return;
      start = { x: ev.clientX, y: ev.clientY, l: el.scrollLeft, t: el.scrollTop };
      dragged = false;
    });
    window.addEventListener('pointermove', function (ev) {
      if (!start) return;
      var dx = ev.clientX - start.x, dy = ev.clientY - start.y;
      if (!dragged && Math.abs(dx) + Math.abs(dy) <= 4) return;
      dragged = true;
      el.classList.add('dragging');
      el.scrollLeft = start.l - dx;
      el.scrollTop = start.t - dy;
    });
    window.addEventListener('pointerup', function () {
      if (!start) return;
      start = null;
      el.classList.remove('dragging');
      if (dragged) {
        var eat = function (e) { e.stopPropagation(); e.preventDefault(); };
        el.addEventListener('click', eat, { capture: true, once: true });
        setTimeout(function () { el.removeEventListener('click', eat, { capture: true }); }, 0);
      }
    });
    el.addEventListener('scroll', function () { remember(el); }, { passive: true });
  }

  return {
    attach: attach,
    center: function (el, x, y) {
      if (!el) return;
      attach(el);
      el.scrollLeft = x - el.clientWidth / 2;
      el.scrollTop = y - el.clientHeight / 2;
      remember(el);
    },
    size: function (el) {
      if (!el) return [0, 0];
      return [el.clientWidth, el.clientHeight];
    },
    getCenter: function (el) {
      if (!el) return [0, 0];
      return [el.scrollLeft + el.clientWidth / 2, el.scrollTop + el.clientHeight / 2];
    },
    restore: function (el, key) {
      if (!el) return false;
      attach(el);
      var c = saved[key];
      if (!c) return false;
      el.scrollLeft = c[0] - el.clientWidth / 2;
      el.scrollTop = c[1] - el.clientHeight / 2;
      return true;
    }
  };
})();
