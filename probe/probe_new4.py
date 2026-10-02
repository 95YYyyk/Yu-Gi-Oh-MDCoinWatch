import os, sys
import numpy as np
from PIL import Image
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
N = r'D:\库文件\文档\ChatGPT\MD\新补充截图'
a = np.asarray(Image.open(os.path.join(N, '结束失败画面.png')).convert('RGB')).astype(np.float32)
for th in (200, 160):
    reg = a[150:900, 250:1700]
    m = (reg[:,:,0] > th) & (reg[:,:,1] > th) & (reg[:,:,2] > th)
    ys, xs = np.where(m)
    print('th=%d white bbox x=%d-%d y=%d-%d px=%d' % (th, xs.min()+250, xs.max()+250, ys.min()+150, ys.max()+150, len(ys)))
rowsum = ((a[150:900, 250:1700, 0] > 200) & (a[150:900, 250:1700, 1] > 200) & (a[150:900, 250:1700, 2] > 200)).sum(axis=1)
nz = np.where(rowsum > 20)[0]
print('rows with >20 white px: %d..%d' % (nz.min()+150, nz.max()+150))
for y in range(300, 560, 20):
    print('  y=%4d white cols in 250-1700: %5d' % (y, rowsum[y-150]))
