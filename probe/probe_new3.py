import os, sys
import numpy as np
from PIL import Image, ImageDraw
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
D = r'D:\库文件\文档\ChatGPT\MD'
N = os.path.join(D, '新补充截图')
OUT = os.path.join(D, 'probe', 'out2')
os.makedirs(OUT, exist_ok=True)
load = lambda p: np.asarray(Image.open(p).convert('RGB')).astype(np.float32)

end1 = load(os.path.join(N, '结束失败画面.png'))
band = end1[340:520, 300:1650]
m = (band[:,:,0] > 200) & (band[:,:,1] > 200) & (band[:,:,2] > 200)
ys, xs = np.where(m)
print('DEFEAT white text bbox (>200): x=%d-%d y=%d-%d  px=%d' % (xs.min()+300, xs.max()+300, ys.min()+340, ys.max()+340, len(ys)))

menu = load(os.path.join(N, '硬币图标.png'))
print()
print('== the 6 coin designs, 100x100 box at each tile centre ==')
tiles = [('1 gold', 607, 250), ('2 gold', 767, 250), ('3 gold', 927, 250), ('4 gold', 1087, 250), ('5 gold', 1247, 250), ('6 silver-paw', 607, 392)]
for name, x, y in tiles:
    b = menu[y-50:y+50, x-50:x+50]
    print('  %-14s R-B=%7.1f  mean=%7.2f  std=%6.2f' % (name, b[:,:,0].mean()-b[:,:,2].mean(), b.mean(axis=2).mean(), b.mean(axis=2).std()))
print()
print('  对比 duel 中的正面币 (silver-paw): R-B=+44.3 mean=198.1 std=51.8')

BOX = {
 '结束失败画面.png': [(500,365,1430,470,'DEFEAT / VICTORY','lime'), (300,620,1620,700,'双方名条','cyan'), (690,930,1230,1010,'确认','yellow')],
 '结束失败画面2.png': [(14,40,170,82,'决斗结果 标题','lime'), (110,190,810,270,'决斗分数','cyan'), (1110,930,1690,1010,'保存决斗/返回菜单','yellow')],
}
for name, boxes in BOX.items():
    im = Image.open(os.path.join(N, name)).convert('RGB')
    d = ImageDraw.Draw(im)
    for (x0,y0,x1,y1,lab,col) in boxes:
        d.rectangle([x0,y0,x1,y1], outline=col, width=4)
        d.text((x0, y0-16 if y0 > 20 else y1+4), lab, fill=col)
    p = os.path.join(OUT, name.replace('.png', '_roi.png'))
    im.save(p)
    print('wrote', p)
