import os, sys
import numpy as np
from PIL import Image, ImageDraw
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
D = r'D:\库文件\文档\ChatGPT\MD'
N = os.path.join(D, '新补充截图')
OUT = os.path.join(D, 'probe', 'out2')
os.makedirs(OUT, exist_ok=True)

front = Image.open(os.path.join(D, '硬币正面.png')).convert('RGB').crop((860, 440, 1060, 650))
back  = Image.open(os.path.join(D, '硬币反面.png')).convert('RGB').crop((860, 440, 1060, 650))
row   = Image.open(os.path.join(N, '硬币图标.png')).convert('RGB').crop((530, 175, 1315, 475))
big   = Image.open(os.path.join(N, '硬币图标.png')).convert('RGB').crop((1420, 360, 1720, 620))

W, H = 960, 700
canvas = Image.new('RGB', (W, H), (20, 20, 25))
d = ImageDraw.Draw(canvas)
canvas.paste(front, (0, 20));   d.text((6, 4), 'in-duel FRONT (from 硬币正面.png)', fill='lime')
canvas.paste(back,  (210, 20)); d.text((216, 4), 'in-duel BACK (from 硬币反面.png)', fill='lime')
canvas.paste(big,   (420, 20)); d.text((426, 4), 'selected design preview', fill='cyan')
canvas.paste(row.resize((785, 300)), (0, 250))
d.text((6, 234), 'all 6 coin designs on offer', fill='cyan')
canvas.save(os.path.join(OUT, 'coin_compare.png'))
print('wrote', os.path.join(OUT, 'coin_compare.png'))

def gray(a):
    return (0.299*a[:,:,0] + 0.587*a[:,:,1] + 0.114*a[:,:,2]).astype(np.float64)

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
    num = TI - sumT*sI/n
    den = np.sqrt(np.maximum(sumT2 - sumT*sumT/n, 1e-9) * np.maximum(sI2 - sI*sI/n, 1e-9))
    return num/den

G = {}
for root in (D, N):
    for n in os.listdir(root):
        if n.endswith('.png'):
            G[n] = gray(np.asarray(Image.open(os.path.join(root, n)).convert('RGB')).astype(np.float32))

print()
print('== sizes of the new screens ==')
for n in ('硬币图标.png', '结束失败画面.png', '结束失败画面2.png'):
    print('  %-18s %dx%d' % (n, G[n].shape[1], G[n].shape[0]))

T = G['结束失败画面.png'][365:470, 500:1430]
print()
print('== DEFEAT-word template (930x105 from 结束失败画面.png) searched everywhere ==')
for n in sorted(G):
    s = cc_norm(G[n], T)
    iy, ix = np.unravel_index(s.argmax(), s.shape)
    print('  %-26s peak=%6.3f at (%d,%d)' % (n, s.max(), ix, iy))

print()
print('== centre 60x60 coin ROI, colour split (R-B) ==')
for n in sorted(G):
    b = np.asarray(Image.open(os.path.join(D, n)).convert('RGB')).astype(np.float32)[510:570, 930:990] if os.path.exists(os.path.join(D, n)) else None
    if b is None:
        continue
    print('  %-26s R-B=%7.1f  mean=%7.2f  std=%6.2f' % (n, b[:,:,0].mean()-b[:,:,2].mean(), b.mean(axis=2).mean(), b.mean(axis=2).std()))
