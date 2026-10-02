import os, sys
import numpy as np
from PIL import Image
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
D = r'D:\库文件\文档\ChatGPT\MD'
F = {n: np.asarray(Image.open(os.path.join(D, n)).convert('RGB')).astype(np.float32) for n in os.listdir(D) if n.endswith('.png')}
print('== ROI (930,510)-(990,570): colour statistics ==')
print('%-26s %7s %7s %7s %7s %8s %8s' % ('image', 'R', 'G', 'B', 'R-B', 'warm%', 'std'))
for n in sorted(F):
    b = F[n][510:570, 930:990]
    R, G, B = b[:,:,0].mean(), b[:,:,1].mean(), b[:,:,2].mean()
    warm = ((b[:,:,0] - b[:,:,2] > 60) & (b[:,:,0] > 150)).mean() * 100
    print('%-26s %7.1f %7.1f %7.1f %7.1f %8.2f %8.2f' % (n, R, G, B, R - B, warm, b.mean(axis=2).std()))
print()
for n, key in (('硬币正面.png','front'), ('硬币反面.png','back')):
    a = F[n]
    if key == 'front':
        m = (a[:,:,0] - a[:,:,2] > 40) & (a[:,:,0] > 150) & (a[:,:,1] > 110)
    else:
        m = (a.max(axis=2) < 70)
    sub = m[380:700, 820:1120]
    ys, xs = np.where(sub)
    print('%s: mask px=%d bbox x=%d-%d y=%d-%d  centroid=(%d,%d)' % (n, len(ys), xs.min()+820, xs.max()+820, ys.min()+380, ys.max()+380, xs.mean()+820, ys.mean()+380))
print()
print('== does the front-face mask fire anywhere else? ==')
for n in sorted(F):
    a = F[n]
    m = (a[:,:,0] - a[:,:,2] > 40) & (a[:,:,0] > 150) & (a[:,:,1] > 110)
    centre = m[380:700, 820:1120].sum()
    print('  %-26s centre-window warm px=%6d   whole-frame=%7d' % (n, centre, m.sum()))
