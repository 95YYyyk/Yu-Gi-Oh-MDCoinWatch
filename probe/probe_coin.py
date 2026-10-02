import os, sys
import numpy as np
from PIL import Image
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
D = r'D:\库文件\文档\ChatGPT\MD'
F = {n: np.asarray(Image.open(os.path.join(D, n)).convert('RGB')).astype(np.float32) for n in os.listdir(D) if n.endswith('.png')}
gray = lambda a: 0.299*a[:,:,0] + 0.587*a[:,:,1] + 0.114*a[:,:,2]
print('%-26s %8s %8s %8s %8s' % ('image', 'coinROI', 'std', '>200%', '<60%'))
for n in sorted(F):
    g = gray(F[n])[510:570, 930:990]
    print('%-26s %8.2f %8.2f %8.2f %8.2f' % (n, g.mean(), g.std(), (g > 200).mean()*100, (g < 60).mean()*100))
print()
print('%-26s %8s %8s' % ('image', 'btnrow_yel', 'kwROI_yel'))
for n in sorted(F):
    a = F[n]
    btn = a[815:865, 700:1240]
    kw  = a[690:742, 930:1030]
    f = lambda b: int((((b[:,:,0] > 190) & (b[:,:,1] > 150) & (b[:,:,2] < 140) & (b[:,:,0] - b[:,:,2] > 80)).sum()))
    print('%-26s %8d %8d' % (n, f(btn), f(kw)))
