import os, sys
import numpy as np
from PIL import Image
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
D = r'D:\库文件\文档\ChatGPT\MD'
N = os.path.join(D, '新补充截图')
OUT = os.path.join(D, 'probe', 'out2')
a = np.asarray(Image.open(os.path.join(N, '结束失败画面.png')).convert('RGB')).astype(np.float32)
g = 0.299*a[:,:,0] + 0.587*a[:,:,1] + 0.114*a[:,:,2]
print('== dark columns in the icon row (y 790-880) ==')
row = g[790:880]
darkcols = (row < 55).sum(axis=0)
runs, start = [], None
for x, v in enumerate(darkcols):
    if v > 25 and start is None:
        start = x
    elif v <= 25 and start is not None:
        runs.append((start, x - 1)); start = None
if start is not None: runs.append((start, len(darkcols) - 1))
for s, e in runs:
    if e - s > 20:
        print('  x %4d - %4d  (w=%d)' % (s, e, e - s + 1))
print()
print('== dark rows for each run (vertical extent) ==')
for s, e in runs:
    if e - s <= 20:
        continue
    col = g[:, s:e+1]
    darkrows = (col < 55).sum(axis=1)
    ys = np.where(darkrows > (e - s) * 0.3)[0]
    print('  x %4d-%4d  y %4d-%4d' % (s, e, ys.min(), ys.max()))
Image.open(os.path.join(N, '结束失败画面.png')).convert('RGB').crop((580, 760, 1350, 910)).resize((1540, 300)).save(os.path.join(OUT, 'friendbar.png'))
print()
print('wrote', os.path.join(OUT, 'friendbar.png'))
