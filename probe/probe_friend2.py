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
end = np.asarray(Image.open(os.path.join(N, '结束失败画面.png')).convert('RGB')).astype(np.float32)
g = gray(end)
print('== locate the hand+ button: dark blob near x 960-1130 ==')
reg = g[740:940, 950:1140]
m = reg < 60
ys, xs = np.where(m)
print('  dark px=%d  bbox x=%d-%d  y=%d-%d' % (len(ys), xs.min()+950, xs.max()+950, ys.min()+740, ys.max()+740))
x0, x1, y0, y1 = xs.min()+950, xs.max()+950, ys.min()+740, ys.max()+740
print()
G = {}
for root in (D, N):
    for n in os.listdir(root):
        if n.endswith('.png'):
            G[n] = gray(np.asarray(Image.open(os.path.join(root, n)).convert('RGB')).astype(np.float32))
def sweep(T, tag):
    print('-- %s  (%dx%d) --' % (tag, T.shape[1], T.shape[0]))
    rows = []
    for n in sorted(G):
        s = cc_norm(G[n], T); iy, ix = np.unravel_index(s.argmax(), s.shape)
        rows.append((s.max(), n, ix, iy))
    for sc, n, ix, iy in sorted(rows, reverse=True)[:6]:
        print('     %-26s peak=%6.3f at (%d,%d)' % (n, sc, ix, iy))
    print('     second best = %.3f  margin = %.3f' % (sorted(rows, reverse=True)[1][0], 1 - sorted(rows, reverse=True)[1][0]))
    print()
sweep(g[y0:y1+1, x0:x1+1], 'hand+ icon alone')
sweep(g[775:900, 690:1245], 'whole 3-button row')
