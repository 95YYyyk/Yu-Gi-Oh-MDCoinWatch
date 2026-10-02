import os, sys
import numpy as np
from PIL import Image, ImageDraw
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
D = r'D:\库文件\文档\ChatGPT\MD'
OUT = os.path.join(D, 'probe', 'out')
os.makedirs(OUT, exist_ok=True)
F = {n: np.asarray(Image.open(os.path.join(D, n)).convert('RGB')).astype(np.float32) for n in os.listdir(D) if n.endswith('.png')}
KEY = lambda kw: next(k for k in F if kw in k)
gray = lambda a: 0.299*a[:,:,0] + 0.587*a[:,:,1] + 0.114*a[:,:,2]
print('== 60x60 window grid scan, x 860-1060 step 10 / y 440-640 step 10 ==')
for kw, better in (('硬币正面', max), ('硬币反面', min)):
    g = gray(F[KEY(kw)])
    rows = [(g[y:y+60, x:x+60].mean(), x, y) for y in range(440, 641, 10) for x in range(860, 1061, 10)]
    m, x, y = better(rows)
    print('  %s: extreme window mean=%.2f at (%d,%d)   range %.2f..%.2f' % (kw, m, x, y, min(r[0] for r in rows), max(r[0] for r in rows)))
ROIS = {
 '硬币正面': [(930,510,990,570,'coin 60x60','lime')],
 '硬币反面': [(930,510,990,570,'coin 60x60','lime')],
 '我方选择先后手': [(930,510,990,570,'coin','lime'),(780,698,1140,742,'choice line','red'),(725,822,835,858,'btn FIRST','cyan'),(1105,822,1215,858,'btn SECOND','cyan')],
 '反面后对手选择先后手': [(680,698,1240,742,'wait line','red'),(930,510,990,570,'coin','lime')],
 '我方先手': [(930,690,1030,742,'kw 先攻','lime'),(840,686,1065,750,'line','red')],
 '我方后攻': [(930,690,1030,742,'kw 后攻','lime'),(840,686,1065,750,'line','red')],
}
for name, boxes in ROIS.items():
    src = KEY(name)
    im = Image.open(os.path.join(D, src)).convert('RGB')
    d = ImageDraw.Draw(im)
    for (x0,y0,x1,y1,label,col) in boxes:
        d.rectangle([x0,y0,x1,y1], outline=col, width=3)
        d.text((x0, y0-14 if y0 > 20 else y1+4), label, fill=col)
    p = os.path.join(OUT, os.path.splitext(src)[0] + '_roi.png')
    im.save(p)
    print('wrote', p)
