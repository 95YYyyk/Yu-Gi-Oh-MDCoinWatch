# -*- coding: utf-8 -*-
"""Extract the embedded detection templates from the sample screenshots."""
import os, sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'app', 'templates')
os.makedirs(OUT, exist_ok=True)
SEARCH = [ROOT, os.path.join(ROOT, '新补充截图')]

SPEC = [
    ('kw_first',    '我方先手.png',            930, 690, 100,  52),
    ('kw_second',   '我方后攻.png',            930, 690, 100,  52),
    ('choice_line', '我方选择先后手.png',       780, 698, 360,  44),
    ('wait_line',   '反面后对手选择先后手.png', 680, 698, 560,  44),
    ('end_row',     '结束失败画面.png',         690, 775, 555, 125),
]

def find(name):
    for d in SEARCH:
        p = os.path.join(d, name)
        if os.path.exists(p):
            return p
    raise SystemExit('missing sample: ' + name)

def gray(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.float32)
    return (0.299 * a[:, :, 0] + 0.587 * a[:, :, 1] + 0.114 * a[:, :, 2])

meta = []
for key, sample, x, y, w, h in SPEC:
    g = gray(find(sample))
    if y + h > g.shape[0] or x + w > g.shape[1]:
        raise SystemExit('crop out of bounds for ' + key)
    patch = np.rint(g[y:y + h, x:x + w]).clip(0, 255).astype(np.uint8)
    path = os.path.join(OUT, key + '.bin')
    patch.tofile(path)
    meta.append((key, sample, x, y, w, h))
    print('%-12s %-26s (%4d,%4d) %3dx%3d  %6d B  mean=%6.1f std=%5.1f'
          % (key, sample, x, y, w, h, os.path.getsize(path), patch.mean(), patch.std()))

with open(os.path.join(OUT, 'geometry.txt'), 'w', encoding='utf-8') as f:
    f.write('reference frame 1920x1080; templates are raw 8-bit grayscale, row major\n')
    for key, sample, x, y, w, h in meta:
        f.write('%s %d %d %d %d   # from %s\n' % (key, x, y, w, h, sample))
print('total', sum(os.path.getsize(os.path.join(OUT, k + '.bin')) for k, *_ in meta), 'bytes')
