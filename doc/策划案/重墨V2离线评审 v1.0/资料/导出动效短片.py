import json
from pathlib import Path
import sys
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
scratch = Path(sys.argv[1])
frames = json.loads((scratch/'关键帧路径.json').read_text('utf-8'))
out = root/'动效分镜'
clip = []
for file in frames:
    image = Image.open(file).convert('RGB')
    image.thumbnail((760, 430), Image.Resampling.LANCZOS)
    clip.append(image)
clip[0].save(out/'近战特写.gif', save_all=True, append_images=clip[1:], duration=[400]+[50]*(len(clip)-2)+[700], loop=0, optimize=True)
sheet = Image.new('RGB', (1580, 1510), '#18110d')
draw = ImageDraw.Draw(sheet)
title = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 31)
label = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
draw.text((22,18),'重墨 V2 · 近战特写分镜',font=title,fill='#f6d8ac')
panels = [(0,'全局站位 · 确认指令'),(5,'镜头切入 · 攻击方前压'),(8,'接触 · 判定与反应'),(11,'数字出现 · 结果停留'),(18,'恢复全景 · 镜头回位'),(24,'最终结果 · 释放输入')]
for i,(frame,caption) in enumerate(panels):
    x,y=22+(i%2)*780,72+(i//2)*478
    sheet.paste(clip[frame],(x,y))
    draw.rectangle((x,y,x+clip[frame].width,y+clip[frame].height),outline='#89603d',width=2)
    draw.text((x,y+435),f'{frame*50:04} ms  ·  {caption}',font=label,fill='#efcb95')
sheet.save(out/'近战特写分镜.jpg',quality=93)
print('Animated GIF and six-panel storyboard exported.')
