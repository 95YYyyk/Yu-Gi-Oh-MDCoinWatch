import os, sys
import numpy as np
from PIL import Image
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
D = r'D:\库文件\文档\ChatGPT\MD'
N = os.path.join(D, '新补充截图')
def gray(a): return (0.299*a[:,:,0] + 0.587*a[:,:,1] + 0.114*a[:,:,2]).astype(np.float64)
def cc_norm(img, T):
    Hh, Ww = img.shape; h, w = T.shape; n = float(h*w)
    sumT, sumT2 = T.sum(), float((T*T).sum())
    Tp = np.zeros((Hh, Ww)); Tp[:h, :w] = T
    c = np.fft.irfft2(np.fft.rfft2(img) * np.conj(np.fft.rfft2(Tp)), s=(Hh, Ww))
    TI = c[:Hh-h+1, :Ww-w+1]
    I1 = np.zeros((Hh+1, Ww+1)); I2 = np.zeros((Hh+1, Ww+1))
    I1[1:,1:] = img.cumsum(0).cumsum(1); I2[1:,1:] = (img*img).cumsum(0).cumsum(1)
    bx = lambda I: I[h:, w:] - I[:Hh-h+1, w:] - I[h:, :Ww-w+1] + I[:Hh-h+1, :Ww-w+1]
    sI, sI2 = bx(I1), bx(I2)
    return (TI - sumT*sI/n) / np.sqrt(np.maximum(sumT2 - sumT*sumT/n, 1e-9) * np.maximum(sI2 - sI*sI/n, 1e-9))
G, C = {}, {}
for root in (D, N):
    for n in os.listdir(root):
        if n.endswith('.png'):
            a = np.asarray(Image.open(os.path.join(root, n)).convert('RGB')).astype(np.float32)
            G[n] = gray(a); C[n] = a
print('== 结束失败画面.png: white-pixel bbox in the central band ==')
a = C['结束失败画面.png']
band = a[330:500, 300:1650]
m = (band[:,:,0] > 245) & (band[:,:,1] > 245) & (band[:,:,2] > 245)
ys, xs = np.where(m)
print('  px=%d  bbox x=%d-%d  y=%d-%d' % (len(ys), xs.min()+300, xs.max()+300, ys.min()+330, ys.max()+330))
print()
print('== "决斗结果" title template (14,40)-(170,82) from 结束失败画面2.png ==')
T2 = G['结束失败画面2.png'][40:82, 14:170]
for n in sorted(G):
    s = cc_norm(G[n], T2); iy, ix = np.unravel_index(s.argmax(), s.shape)
    print('  %-26s peak=%6.3f at (%d,%d)' % (n, s.max(), ix, iy))
print()
print('== coin ROI colour profile incl. the new screens ==')
for n in sorted(C):
    b = C[n][510:570, 930:990]
    print('  %-26s R-B=%7.1f mean=%7.2f std=%6.2f' % (n, b[:,:,0].mean()-b[:,:,2].mean(), b.mean(axis=2).mean(), b.mean(axis=2).std()))
print()
print('== coin-ROI profile for the 6 designs, straight from 硬币图标.png thumbnails ==')
th = C['硬币图标.png']
spots = [('design1 gold', 560), ('design2 gold', 700), ('design3 gold', 855), ('design4 gold', 1030), ('design5 gold', 1195), ('design6 silver-paw', 590)]
for name, x in spots:
    y = 260 if 'silver' not in name else 390
    b = th[y-45:y+45, x-45:x+45]
    print('  %-18s R-B=%7.1f mean=%7.2f std=%6.2f' % (name, b[:,:,0].mean()-b[:,:,2].mean(), b.mean(axis=2).mean(), b.mean(axis=2).std()))
