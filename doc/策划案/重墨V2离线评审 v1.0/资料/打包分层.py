from io import BytesIO
import json
import sys
from pathlib import Path
from xml.etree import ElementTree as ET
from zipfile import ZipFile, ZIP_STORED
from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat

root = Path(__file__).resolve().parent.parent
scratch = Path(sys.argv[1])
meta = json.loads((root / '资料/覆盖与素材映射.json').read_text('utf-8'))
renders = json.loads((scratch / '渲染记录.json').read_text('utf-8'))
assert {r['id'] for r in renders} == {p['id'] for p in meta['pages']}
w, h = meta['designSize']
out = root / '分层文件'
out.mkdir(exist_ok=True)
report = []
for p in meta['pages']:
    tag = p['id']
    merged_file = root / '效果图' / (tag + '.png')
    merged = Image.open(merged_file).convert('RGBA')
    assert merged.size == (w, h) and merged.getchannel('A').getextrema() == (255, 255), tag
    composite = Image.new('RGBA', (w, h))
    files = [scratch / tag / (str(i) + '.png') for i in range(1, 8)]
    for i, file in enumerate(files, 1):
        layer = Image.open(file).convert('RGBA')
        assert layer.size == (w, h), (tag, i, layer.size)
        alpha = layer.getchannel('A')
        assert alpha.getbbox(), (tag, i)
        if i > 1:
            assert alpha.getextrema()[0] == 0, (tag, i, 'layer has no transparency')
        composite.alpha_composite(layer)
    diff = ImageChops.difference(composite.convert('RGB'), merged.convert('RGB'))
    mean_error = max(ImageStat.Stat(diff).mean)
    # Chromium and Pillow round premultiplied edge pixels differently.
    # One and a half levels out of 255 across the full frame is our upper bound.
    assert mean_error <= 1.5, (tag, mean_error)
    stack_root = ET.Element('image', {'w':str(w), 'h':str(h), 'name':tag+' '+p['title'], 'version':'0.0.3'})
    stack = ET.SubElement(stack_root, 'stack')
    for i in range(7,0,-1):
        ET.SubElement(stack, 'layer', {'name':meta['layers'][i-1], 'src':f'data/layer{i}.png', 'x':'0','y':'0','opacity':'1.0','visibility':'visible','composite-op':'svg:src-over'})
    thumb = merged.copy()
    thumb.thumbnail((256,256))
    thumb_buffer = BytesIO()
    thumb.save(thumb_buffer, format='PNG')
    with ZipFile(out / (tag+'.ora'), 'w', ZIP_STORED) as archive:
        archive.writestr('mimetype', 'image/openraster')
        archive.writestr('stack.xml', ET.tostring(stack_root, encoding='utf-8', xml_declaration=True))
        archive.write(merged_file, 'mergedimage.png')
        archive.writestr('Thumbnails/thumbnail.png', thumb_buffer.getvalue())
        for i, file in enumerate(files, 1):
            archive.write(file, f'data/layer{i}.png')
    report.append({'id':tag,'size':[w,h],'layers':len(files),'maxChannelMeanDifference':round(mean_error,4)})

# Contact sheets for fast review. All thumbnail artwork is scaled from the delivered composite.
review = root / '总览'
review.mkdir(exist_ok=True)
system_font = Path('C:/Windows/Fonts/msyh.ttc')
title_font = ImageFont.truetype(str(system_font), 34)
label_font = ImageFont.truetype(str(system_font), 17)
note_font = ImageFont.truetype(str(system_font), 18)
pages = meta['pages']
for index, start in enumerate(range(0,len(pages),24), 1):
    subset = pages[start:start+24]
    cols, cw, ch, pad = 4, 398, 260, 24
    canvas = Image.new('RGB',(2*pad+cols*cw,112+((len(subset)+3)//4)*ch),'#16110e')
    draw = ImageDraw.Draw(canvas)
    draw.text((pad,16),f'重墨 V2 · 全界面效果总览 {index:02}',fill='#f0d5a7',font=title_font)
    draw.text((pad,64),'适者生存  /  静态参考稿  /  点击离线索引查看完整图层',fill='#c4aa88',font=note_font)
    for i,p in enumerate(subset):
        x,y=pad+(i%cols)*cw,108+(i//cols)*ch
        shot=Image.open(root/'效果图'/(p['id']+'.png')).convert('RGB')
        shot.thumbnail((382,216))
        canvas.paste(shot,(x,y))
        draw.rectangle((x,y,x+shot.width,y+shot.height),outline='#826145')
        draw.text((x,y+226),p['id']+' · '+p['title'],fill='#f1d8ac',font=label_font)
    canvas.save(review/f'{index:02}-效果总览.jpg',quality=91,subsampling=0)

(root/'资料/验收结果.json').write_text(json.dumps({'baseline':meta['baseline'],'designSize':[w,h],'scenes':len(report),'componentCount':meta['baselineComponentCount'],'referenceCount':len(meta['sourceReferences']),'layersPerScene':7,'rendered':renders,'openRaster':report},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(report)} OpenRaster files, {len(report)*7} aligned layer exports')
