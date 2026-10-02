# -*- coding: utf-8 -*-
"""Measure the choice buttons and the coin geometry."""
import os
import sys
import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
D = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def rgb(name):
    return np.asarray(Image.open(os.path.join(D, name)).convert("RGB")).astype(np.int16)

F = {n: rgb(n) for n in os.listdir(D) if n.endswith(".png")}
KEY = lambda kw: next(k for k in F if kw in k)

def yellow(box):
    r, g, b = box[:, :, 0], box[:, :, 1], box[:, :, 2]
    return int(((r > 190) & (g > 150) & (b < 140) & (r - b > 80)).sum())

print("== yellow pixels per button label box ==")
print(f"  {'image':26s} {'先攻 btn (725-835,822-858)':>28s} {'后攻 btn (1105-1215,822-858)':>30s}")
for nm in ("我方选择先后手", "反面后对手选择先后手", "我方先手", "我方后攻", "后攻进入游戏后的画面"):
    im = F[KEY(nm)]
    a = yellow(im[822:858, 725:835])
    b = yellow(im[822:858, 1105:1215])
    print(f"  {nm:26s} {a:28d} {b:30d}")

print("\n== coin: bright/dark blob inside centre window (800-1120, 400-680) ==")
for kw in ("硬币正面", "硬币反面"):
    im = F[KEY(kw)]
    win = im[400:680, 800:1120]
    g = (0.299 * win[:, :, 0] + 0.587 * win[:, :, 1] + 0.114 * win[:, :, 2])
    m = g > 170 if kw == "硬币正面" else g < 70
    ys, xs = np.where(m)
    if len(ys):
        print(f"  {kw}  blob px={len(ys):6d}  bbox x={xs.min() + 800}-{xs.max() + 800}  y={ys.min() + 400}-{ys.max() + 400}"
              f"  centre=({int(xs.mean()) + 800},{int(ys.mean()) + 400})")
    print(f"        full-window mean={g.mean():6.2f}")

print("\n== same centre window on the other screens (false-positive check) ==")
for nm in ("匹配界面", "我方选择先后手", "反面后对手选择先后手", "我方先手", "我方后攻", "后攻进入游戏后的画面"):
    im = F[KEY(nm)]
    win = im[400:680, 800:1120]
    g = (0.299 * win[:, :, 0] + 0.587 * win[:, :, 1] + 0.114 * win[:, :, 2])
    print(f"  {nm:26s} mean={g.mean():6.2f}  dark<70={(g < 70).mean() * 100:6.2f}%  bright>170={(g > 170).mean() * 100:6.2f}%")
