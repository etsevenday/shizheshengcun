#!/usr/bin/env python3
"""
生成 8 个地点的矢量场景图，画风沿用 UI 策划 v0.1 预览稿的 forest()：
渐变天空 + 柔光 + 层叠山丘 + 剪影，前景压暗。
【实现同步 v0.4】同一张图既是地块卡片桌面的弱化背景，也裁切成当前/相邻地图卡的主图；地点主体请放在画面正中，
安全区见 doc/场景图替换说明.md。

输出：web/Survival.Web/wwwroot/scenes/<地点ID>.svg（1200×650，按 slice 铺满）。
以后有美术图时，不必改这个脚本：把图片放进同一目录，并把 data/config.v0.4.json 里该地点的 scene 改成新文件名即可。

用法：python tools/gen_scenes.py
"""
import os
import random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "web", "Survival.Web", "wwwroot", "scenes")
W, H = 1200, 650


def svg(defs, body):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" preserveAspectRatio="xMidYMid slice">'
            f'<defs>{defs}</defs>{body}</svg>\n')


def sky(id_, top, bottom):
    return f'<linearGradient id="{id_}" x2="0" y2="1"><stop stop-color="{top}"/><stop offset="1" stop-color="{bottom}"/></linearGradient>'


def glow(id_, color, op=0.6):
    return f'<radialGradient id="{id_}"><stop stop-color="{color}" stop-opacity="{op}"/><stop offset="1" stop-color="{color}" stop-opacity="0"/></radialGradient>'


def base(sky_top, sky_bottom, light="#d6cca0", lx=740, ly=180, lr=330):
    defs = sky("sky", sky_top, sky_bottom) + glow("light", light)
    body = f'<path fill="url(#sky)" d="M0 0H{W}V{H}H0Z"/><circle cx="{lx}" cy="{ly}" r="{lr}" fill="url(#light)"/>'
    return defs, body


def spikes(rng, n, y_top_range, colors, width=42, x_step=83, start=0):
    out = []
    for i in range(n):
        x = (start + i * x_step) % (W + 60) - 30
        top = rng.randint(*y_top_range)
        c = colors[i % len(colors)]
        out.append(f'<path d="M{x} {H}L{x + width // 2} {top} {x + width} {H}" fill="{c}"/>')
        by = top + 160 + rng.randint(0, 60)
        out.append(f'<path d="M{x + 10} {by}L{x - 30} {by - 60}M{x + 20} {by - 55}L{x + 75} {by - 117}" stroke="{c}" stroke-width="10" opacity=".9"/>')
    return "".join(out)


def grass(rng, n, y0, y1, color, h=(14, 30)):
    s = []
    for _ in range(n):
        x = rng.randint(0, W)
        y = rng.randint(y0, y1)
        hh = rng.randint(*h)
        s.append(f'M{x} {y}L{x + rng.randint(-6, 6)} {y - hh}')
    return f'<path d="{"".join(s)}" stroke="{color}" stroke-width="4" stroke-linecap="round" fill="none"/>'


def stones(items, fill="#4b5b45", edge="#3a4636"):
    return "".join(f'<path d="M{x} {y}l{w * .2:.0f} {-h}l{w * .45:.0f} {-h * .25:.0f}l{w * .35:.0f} {h * 1.25:.0f}Z" fill="{fill}" stroke="{edge}" stroke-width="3"/>' for x, y, w, h in items)


# ───────────────────────── 8 个地点 ─────────────────────────

def woods():
    rng = random.Random(4)
    d, b = base("#96a98b", "#2e4d34")
    b += '<path d="M0 380Q260 230 460 390T1200 280V650H0" fill="#36533a"/><path d="M0 490Q450 350 1200 470V650H0" fill="#253f2d"/>'
    b += spikes(rng, 16, (70, 220), ["#294630", "#233c2b"])
    b += '<path d="M530 650Q520 510 720 365 600 510 820 650Z" fill="#9c9b7599"/>'
    b += '<path d="M0 595Q300 520 490 590T1200 550V650H0Z" fill="#172e23"/>'
    b += stones([(50, 608, 110, 60), (980, 610, 170, 80)])
    b += grass(rng, 26, 560, 640, "#74825b")
    return svg(d, b)


def field():
    rng = random.Random(2)
    d, b = base("#b3b48f", "#5d6a45", light="#efe1a6", lx=860, ly=150, lr=300)
    b += '<path d="M0 330Q300 290 600 320T1200 300V650H0" fill="#7d8657"/>'
    b += '<path d="M0 390Q400 350 800 385T1200 370V650H0" fill="#6b7449"/>'
    # 远处孤树
    b += '<path d="M905 330L912 270 920 330Z" fill="#3f4a33"/><circle cx="912" cy="262" r="26" fill="#45523a"/><circle cx="895" cy="275" r="18" fill="#45523a"/>'
    # 田垄
    for i in range(7):
        y = 420 + i * 34
        b += f'<path d="M0 {y}Q600 {y - 18} 1200 {y + 6}" stroke="#5a6340" stroke-width="5" fill="none" opacity=".7"/>'
    # 残破栅栏
    for x in (120, 175, 232, 296):
        b += f'<path d="M{x} 470L{x + 4} 405" stroke="#5b4a34" stroke-width="9" stroke-linecap="round"/>'
    b += '<path d="M115 425L300 418M118 450L240 444" stroke="#6b5739" stroke-width="6"/>'
    b += '<path d="M0 560Q400 520 800 555T1200 540V650H0Z" fill="#454d31"/>'
    b += grass(rng, 70, 430, 640, "#9aa16b", (16, 40))
    return svg(d, b)


def stream():
    rng = random.Random(3)
    d, b = base("#a7b8b0", "#4b6556", light="#dfe6cf", lx=420, ly=170, lr=300)
    d += '<linearGradient id="water" x2="0" y2="1"><stop stop-color="#8fa9a5"/><stop offset="1" stop-color="#4f6d6c"/></linearGradient>'
    b += '<path d="M0 300Q300 250 560 300T1200 270V650H0" fill="#56715a"/>'
    b += spikes(rng, 9, (150, 260), ["#3d5a42", "#36523b"], width=34, x_step=140, start=40)
    b += '<path d="M0 400Q300 360 620 395T1200 380V650H0" fill="#47614a"/>'
    # 溪流
    b += '<path d="M470 400Q560 430 520 480T610 560Q700 610 640 650H980Q900 600 860 545T760 460Q700 420 720 395Z" fill="url(#water)"/>'
    for y, x0, x1 in ((430, 560, 640), (470, 560, 660), (520, 640, 760), (575, 700, 820), (620, 760, 880)):
        b += f'<path d="M{x0} {y}L{x1} {y - 4}" stroke="#d7e2d8" stroke-width="3" opacity=".6"/>'
    b += stones([(470, 520, 70, 30), (880, 560, 90, 36), (560, 610, 60, 26)], fill="#6f7a6c", edge="#4c574b")
    # 芦苇
    for x in (360, 385, 410, 1000, 1030, 1060):
        b += f'<path d="M{x} 600L{x + 6} 470" stroke="#6b7b52" stroke-width="5"/><ellipse cx="{x + 6}" cy="465" rx="5" ry="16" fill="#7a6a49"/>'
    b += '<path d="M0 600Q250 560 460 600L420 650H0Z" fill="#2c4231"/><path d="M1200 590Q1050 570 960 610L1000 650H1200Z" fill="#2c4231"/>'
    return svg(d, b)


def house(x, y, w, h, roof, wall, broken=False):
    s = f'<path d="M{x} {y}h{w}v{h}h{-w}Z" fill="{wall}"/>'
    if broken:
        s += f'<path d="M{x - 12} {y}L{x + w * .5:.0f} {y - h * .6:.0f}L{x + w * .62:.0f} {y - h * .38:.0f}L{x + w * .7:.0f} {y - h * .5:.0f}L{x + w + 12} {y}Z" fill="{roof}"/>'
    else:
        s += f'<path d="M{x - 12} {y}L{x + w / 2:.0f} {y - h * .6:.0f}L{x + w + 12} {y}Z" fill="{roof}"/>'
    s += f'<path d="M{x + w * .18:.0f} {y + h * .25:.0f}h{w * .2:.0f}v{h * .25:.0f}h{-w * .2:.0f}Z" fill="#1c241e"/>'
    s += f'<path d="M{x + w * .58:.0f} {y + h * .45:.0f}h{w * .22:.0f}v{h * .55:.0f}h{-w * .22:.0f}Z" fill="#1c241e"/>'
    return s


def village():
    rng = random.Random(5)
    d, b = base("#a9a58a", "#4f5444", light="#e2d3a1", lx=640, ly=170, lr=320)
    b += '<path d="M0 330Q300 300 600 325T1200 310V650H0" fill="#5e6450"/>'
    b += house(90, 300, 150, 110, "#4a3f33", "#6d6656", broken=True)
    b += house(300, 320, 120, 90, "#544637", "#76705d")
    b += house(760, 305, 170, 115, "#4a3f33", "#6a6453")
    b += house(980, 320, 130, 95, "#544637", "#736d5b", broken=True)
    b += '<path d="M0 420Q400 395 800 418T1200 410V650H0" fill="#4d5442"/>'
    # 土路
    b += '<path d="M520 650Q560 520 610 420L690 420Q690 520 760 650Z" fill="#8a7d5f" opacity=".8"/>'
    # 电线杆
    b += '<path d="M470 430L478 230M440 255L515 250" stroke="#3c362b" stroke-width="8"/><path d="M478 250Q700 300 960 262" stroke="#3c362b" stroke-width="2" fill="none"/>'
    b += '<path d="M0 580Q300 545 500 585T1200 560V650H0Z" fill="#33392d"/>'
    b += grass(rng, 40, 450, 640, "#7e8360")
    return svg(d, b)


def interior(wall_top, wall_bottom, floor, beam="#efe3b0"):
    defs = sky("wall", wall_top, wall_bottom) + glow("beamGlow", beam, .45)
    defs += f'<linearGradient id="beam" x1="0" y1="0" x2="1" y2="1"><stop stop-color="{beam}" stop-opacity=".45"/><stop offset="1" stop-color="{beam}" stop-opacity="0"/></linearGradient>'
    body = f'<path fill="url(#wall)" d="M0 0H{W}V430H0Z"/><path d="M0 430H{W}V{H}H0Z" fill="{floor}"/>'
    body += f'<path d="M0 430L{W} 430" stroke="#2b2a22" stroke-width="6"/>'
    # 窗与光束
    body += '<path d="M820 70h220v170h-220Z" fill="#c9c6a6"/><path d="M820 155h220M930 70v170" stroke="#4b4a3d" stroke-width="10"/>'
    body += '<path d="M820 240L1040 240 760 650 430 650Z" fill="url(#beam)"/><circle cx="930" cy="160" r="260" fill="url(#beamGlow)"/>'
    return defs, body


def hardware():
    d, b = interior("#6e6a58", "#4c4a3f", "#3d3a31")
    # 货架
    for sx in (60, 330):
        b += f'<path d="M{sx} 90h220v340h-220Z" fill="#3a3a33"/>'
        for k in range(4):
            y = 160 + k * 80
            b += f'<path d="M{sx} {y}h220" stroke="#5b5848" stroke-width="10"/>'
            for j in range(3):
                bx = sx + 15 + j * 68
                b += f'<path d="M{bx} {y - 48}h50v43h-50Z" fill="{["#7d6a4b", "#6b7466", "#8a7c5a"][(j + k) % 3]}"/>'
    # 工具挂板
    b += '<path d="M600 110h180v150h-180Z" fill="#5a5444"/>'
    b += '<path d="M630 140l30 80M700 130v100M740 135l-15 90" stroke="#9aa196" stroke-width="7" stroke-linecap="round"/>'
    # 柜台
    b += '<path d="M520 360h420v120h-420Z" fill="#584a36"/><path d="M505 350h450v20h-450Z" fill="#6d5b41"/>'
    b += '<path d="M0 560H1200V650H0Z" fill="#2e2c25" opacity=".7"/>'
    return svg(d, b)


def clinic():
    d, b = interior("#a8b0a6", "#6f7a72", "#5a6460", beam="#f2f0dc")
    # 瓷砖
    for y in range(40, 430, 48):
        b += f'<path d="M0 {y}H{W}" stroke="#93a097" stroke-width="2" opacity=".6"/>'
    # 红十字褪色牌
    b += '<path d="M120 80h150v100h-150Z" fill="#d7d3c4"/><path d="M180 98h30v64h-30ZM163 115h64v30h-64Z" fill="#a4594b"/>'
    # 药柜
    b += '<path d="M360 120h200v300h-200Z" fill="#c6c7bb"/><path d="M460 120v300M360 270h200" stroke="#8d9488" stroke-width="6"/>'
    b += '<path d="M380 140h60v110h-60ZM480 140h60v110h-60Z" fill="#9fb3b0" opacity=".7"/>'
    # 病床
    b += '<path d="M600 380h330v40h-330Z" fill="#d6d4c8"/><path d="M600 340h80v40h-80Z" fill="#e3e0d2"/><path d="M610 420v80M920 420v80" stroke="#6e746c" stroke-width="10"/>'
    b += '<path d="M0 560H1200V650H0Z" fill="#3f4744" opacity=".6"/>'
    return svg(d, b)


def scrapyard():
    rng = random.Random(8)
    d, b = base("#a69d86", "#544c40", light="#e0c99a", lx=300, ly=160, lr=300)
    b += '<path d="M0 320Q300 300 600 318T1200 300V650H0" fill="#5d564a"/>'
    # 吊臂
    b += '<path d="M880 420L900 140 1080 200" stroke="#3b3329" stroke-width="16" fill="none"/><path d="M1080 200v120" stroke="#3b3329" stroke-width="4"/><path d="M1065 320h30v20h-30Z" fill="#3b3329"/>'
    # 废料堆
    for cx, cy, r, c in ((250, 470, 240, "#4f483d"), (640, 480, 260, "#5a5145"), (1050, 500, 220, "#4a443a")):
        b += f'<path d="M{cx - r} {cy + 100}Q{cx - r * .4:.0f} {cy - r * .55:.0f} {cx} {cy - r * .5:.0f}T{cx + r} {cy + 100}Z" fill="{c}"/>'
    for _ in range(26):
        x = rng.randint(40, 1160)
        y = rng.randint(380, 560)
        w = rng.randint(20, 60)
        b += f'<path d="M{x} {y}l{w} {rng.randint(-14, 14)}l{rng.randint(-10, 10)} {rng.randint(10, 24)}l{-w} {rng.randint(-6, 6)}Z" fill="{rng.choice(["#6f6a5f", "#7e7466", "#8c6a4f", "#5f6460"])}"/>'
    # 报废车
    b += '<path d="M430 420h170l30 40v40h-230v-40Z" fill="#6b5a46"/><path d="M470 428h50v28h-50ZM535 428h45l20 28h-65Z" fill="#2e2a24"/>'
    b += '<circle cx="465" cy="500" r="22" fill="#2a2620"/><circle cx="590" cy="500" r="22" fill="#2a2620"/>'
    # 铁丝网
    b += '<path d="M0 350H1200" stroke="#3a352d" stroke-width="3"/>' + "".join(f'<path d="M{x} 350v90" stroke="#3a352d" stroke-width="5"/>' for x in range(30, 1200, 150))
    b += '<path d="M0 590Q300 560 600 588T1200 575V650H0Z" fill="#2f2b25"/>'
    return svg(d, b)


def camp():
    # 主体放在 y≈230–400：横幅按居中裁切只露出中段，下方还压着资源卡
    rng = random.Random(1)
    d, b = base("#9fae8c", "#3b5639", light="#e4d4a0", lx=600, ly=170, lr=320)
    b += '<path d="M0 300Q260 210 500 300T1200 240V650H0" fill="#3f5a3e"/>'
    b += spikes(rng, 8, (60, 160), ["#2f4a33", "#2a432f"], width=38, x_step=170, start=20)
    b += '<path d="M0 380Q450 320 1200 370V650H0" fill="#4a5b3c"/>'
    # 空地
    b += '<ellipse cx="620" cy="420" rx="480" ry="80" fill="#6b6a4c" opacity=".75"/>'
    # 窝棚
    b += '<path d="M250 410L370 250L490 410Z" fill="#6d6346"/><path d="M370 250L490 410L510 405L385 245Z" fill="#564d36"/><path d="M340 410L370 340L400 410Z" fill="#2b2a20"/>'
    b += '<path d="M290 360L460 360M310 330L440 330" stroke="#8a7c55" stroke-width="5"/>'
    # 火堆（未点燃的石圈与木柴；火焰由界面按燃烧状态叠加，中心约在 745,402）
    b += stones([(700, 420, 30, 14), (735, 427, 30, 14), (770, 421, 30, 14), (712, 437, 28, 12), (760, 439, 28, 12)], fill="#6f705f", edge="#4b4c40")
    b += '<path d="M705 417L790 401M712 401L792 421" stroke="#5c4730" stroke-width="10" stroke-linecap="round"/>'
    # 原木凳与晾衣绳
    b += '<path d="M860 420h110v26h-110Z" fill="#6a5236"/><ellipse cx="860" cy="433" rx="10" ry="13" fill="#a88a5e"/>'
    b += '<path d="M560 300L566 400M1020 290L1014 400" stroke="#4d3f2c" stroke-width="7"/><path d="M563 312Q790 345 1017 302" stroke="#8a7c55" stroke-width="2" fill="none"/>'
    b += '<path d="M660 330h40v34h-40ZM860 334h34v28h-34Z" fill="#9b8c6a" opacity=".85"/>'
    b += '<path d="M0 520Q300 480 600 510T1200 495V650H0Z" fill="#33452f"/>'
    b += '<path d="M0 600Q300 570 600 598T1200 585V650H0Z" fill="#26372a"/>'
    b += grass(rng, 40, 450, 645, "#6f7d55")
    return svg(d, b)


SCENES = {
    "camp": camp, "field": field, "stream": stream, "woods": woods,
    "village": village, "hardware": hardware, "clinic": clinic, "scrapyard": scrapyard,
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, fn in SCENES.items():
        data = fn()
        with open(os.path.join(OUT, f"{name}.svg"), "w", encoding="utf-8", newline="\n") as f:
            f.write(data)
        print(f"{name}.svg  {len(data) // 1024 + 1}KB")
