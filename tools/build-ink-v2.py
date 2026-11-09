"""Export the approved planning art for the Blazor game (Pillow and fonttools[woff])."""

import json
import re
import shutil
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from fontTools.ttLib import TTFont
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'doc/策划案/重墨像素风界面与卡片 v0.9/assets'
REDRAW = PLAN / '重墨V2重制'
LIBRARY = PLAN / '全量素材库'
DEST = ROOT / 'web/Survival.Web/wwwroot/art/ink-v2'


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def box(rect):
    x, y, w, h = rect
    return x, y, x + w, y + h


def save(image, filename, max_side):
    image.thumbnail((max_side, max_side), Image.Resampling.LANCZOS)
    image.save(DEST / filename, 'WEBP', quality=90, method=4, exact=True)


def export_atlas(job):
    source, entries = job
    with Image.open(source) as original:
        image = original.convert('RGBA')
    filename = f'atlas-{source.stem}.webp'
    sprites = {}
    portraits = {}
    for key, row in entries:
        if not re.fullmatch(r'[a-zA-Z0-9_:-]+', key) or key in sprites:
            raise ValueError(f'Invalid or duplicate art key: {key}')
        crop = row.get('crop') or [0, 0, *image.size]
        l, t, r, b = box(crop)
        if not (0 <= l < r <= image.width and 0 <= t < b <= image.height):
            raise ValueError(f'Crop outside source: {key}')
        sprites[key] = {'file': filename, 'size': list(image.size), 'crop': crop}
        if row.get('portraitCrop') or key in {'actor:p0', 'actor:baobao', 'glyph:unknown'}:
            portrait = f'portrait-{key.replace(":", "-")}.webp'
            save(image.crop(box(row.get('portraitCrop') or crop)), portrait, 240)
            portraits[key] = portrait
    save(image, filename, 1024)
    return sprites, portraits


def export_font():
    font = REDRAW / 'fonts/chill16/ChillBitmap_16px.ttf'
    # WOFF2 preserves the complete glyph repertoire and outline data of the source.
    with TTFont(font, recalcTimestamp=False) as face:
        face.flavor = 'woff2'
        face.save(DEST / 'chill-bitmap-16.woff2')
    shutil.copyfile(font.with_name('ChillBitmap_16px_License.txt'), DEST / 'chill-bitmap-license.txt')


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    approved = read(REDRAW / '替换清单.json')
    library = read(REDRAW / '新版全量视觉清单.json')['assets']
    if approved['pending'] or len(approved['bindings']) != approved['planned']:
        raise ValueError('The approved replacement table is incomplete')

    sprites = {}
    portraits = {}
    scenes = {}
    atlases = {}
    for key, row in approved['bindings'].items():
        atlases.setdefault((REDRAW / row['file']).resolve(), []).append((key, row))
    for row in library:
        asset = row['asset']
        if row['key'] in approved['bindings'] or not asset or 'file' not in asset:
            continue
        source = (LIBRARY / asset['file']).resolve()
        if row['category'] in {'actor', 'enemy'}:
            atlases.setdefault(source, []).append((row['key'], asset))
        if row['category'] == 'location' and not source.is_relative_to(DEST.parents[1]):
            rect = asset.get('crop')
            name = source.stem if not rect else row['id']
            filename = f'scene-{name}.webp'
            if filename not in scenes.values():
                with Image.open(source) as image:
                    save(image.crop(box(rect)) if rect else image.copy(), filename, 1280)
            scenes[row['id']] = filename

    with ThreadPoolExecutor(max_workers=6) as pool:
        font = pool.submit(export_font)
        for frames, faces in pool.map(export_atlas, sorted(atlases.items())):
            sprites.update(frames)
            portraits.update(faces)
        font.result()

    layer_dir = PLAN / 'UI分层样件/ui'
    for filename in ('frame-card.svg', 'frame-action.svg', 'frame-panel.svg', 'frame-selected.svg',
                     'texture-paper.svg', 'texture-iron.svg'):
        shutil.copyfile(layer_dir / filename, DEST / filename)
    manifest = {'sprites': sprites, 'portraits': portraits, 'scenes': scenes}
    (DEST / 'manifest.json').write_text(json.dumps(manifest, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    print(f'{len(approved["bindings"])} approved redraws, {len(sprites)} total sprites, '
          f'{len(portraits)} portraits, {len(scenes)} scene bindings')


if __name__ == '__main__':
    main()
