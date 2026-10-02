import os, sys
import numpy as np
from PIL import Image
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
N = r'D:\库文件\文档\ChatGPT\MD\新补充截图'
a = np.asarray(Image.open(os.path.join(N, '结束失败画面.png')).convert('RGB')).astype(np.float32)
g = 0.299*a[:,:,0] + 0.587*a[:,:,1] + 0.114*a[:,:,2]
prof = g[815:865, :].mean(axis=0)
runs, start = [], None
for x in range(600, 1350):
    if prof[x] < 33 and start is None: start = x
    elif prof[x] >= 33 and start is not None:
        if x - start > 30: runs.append((start, x-1))
        start = None
print('black octagon columns (mean<33 over y 815-865):')
for s, e in runs:
    sub = g[740:940, s:e+1]
    rows = np.where((sub < 33).sum(axis=1) > (e-s)*0.5)[0]
    print('   x %4d-%4d (w=%3d)   y %4d-%4d' % (s, e, e-s+1, rows.min()+740, rows.max()+740))
print()
print('name-bar brightness next to the buttons: %.1f' % prof[850])
print('octagon interior brightness: %.1f' % prof[1040])
