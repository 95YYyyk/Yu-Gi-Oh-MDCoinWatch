# -*- coding: utf-8 -*-
"""Cross-match candidate anchors between the Master Duel screenshots."""
import os
import sys
import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
D = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def load(name):
    rgb = np.asarray(Image.open(os.path.join(D, name)).convert("RGB")).astype(np.float32)
    return (0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]).astype(np.float64)

def cc_norm(img, T):
    """TM_CCOEFF_NORMED. img (H,W), T (h,w)."""
    H, W = img.shape
    h, w = T.shape
    n = float(h * w)
    sumT, sumT2 = T.sum(), float((T * T).sum())
    Tp = np.zeros((H, W)); Tp[:h, :w] = T
    c = np.fft.irfft2(np.fft.rfft2(img) * np.conj(np.fft.rfft2(Tp)), s=(H, W))
    TI = c[:H - h + 1, :W - w + 1]
    I1 = np.zeros((H + 1, W + 1)); I2 = np.zeros((H + 1, W + 1))
    I1[1:, 1:] = img.cumsum(0).cumsum(1)
    I2[1:, 1:] = (img * img).cumsum(0).cumsum(1)
    box = lambda I: I[h:, w:] - I[:H - h + 1, w:] - I[h:, :W - w + 1] + I[:H - h + 1, :W - w + 1]
    sumI, sumI2 = box(I1), box(I2)
    num = TI - sumT * sumI / n
    den = np.sqrt(np.maximum(sumT2 - sumT * sumT / n, 1e-9) * np.maximum(sumI2 - sumI * sumI / n, 1e-9))
    return num / den

_synth = np.random.RandomState(0).rand(300, 400) * 255
_patch = _synth[120:180, 200:280].copy()
_s = cc_norm(_synth, _patch)
print(f"[selftest] peak={_s.max():.4f} at {tuple(int(v) for v in np.unravel_index(_s.argmax(), _s.shape))} expected (120,200)")

F = {n: load(n) for n in os.listdir(D) if n.endswith(".png")}
KEY = lambda kw: next(k for k in F if kw in k)
band = lambda img, x0, x1, y0, y1: img[y0:y1, x0:x1]

first_img, second_img = F[KEY("我方先手")], F[KEY("我方后攻")]
choice_img, wait_img = F[KEY("我方选择")], F[KEY("对手选择")]
duel_img = F[KEY("进入游戏后")]
NAMES = {id(first_img): "w-first", id(second_img): "w-second", id(choice_img): "choice",
         id(wait_img): "wait", id(duel_img): "in-duel"}

kw_first = band(first_img, 930, 1030, 690, 742)
kw_second = band(second_img, 930, 1030, 690, 742)
line_first = band(first_img, 840, 1065, 686, 750)
choice_line = band(choice_img, 780, 1140, 698, 742)
wait_line = band(wait_img, 680, 1240, 698, 742)

TESTS = [
    ("kw_first", kw_first, first_img), ("kw_first", kw_first, second_img),
    ("kw_second", kw_second, second_img), ("kw_second", kw_second, first_img),
    ("line_first", line_first, first_img), ("line_first", line_first, second_img),
    ("line_first", line_first, duel_img), ("line_first", line_first, choice_img),
    ("choice_line", choice_line, choice_img), ("choice_line", choice_line, wait_img),
    ("wait_line", wait_line, wait_img), ("wait_line", wait_line, choice_img),
]
print(f"\n{'template':12s} {'searched in':11s} {'peak':>8s} {'loc':>12s}")
for name, T, img in TESTS:
    s = cc_norm(img, T)
    iy, ix = np.unravel_index(s.argmax(), s.shape)
    print(f"{name:12s} {NAMES[id(img)]:11s} {s.max():8.3f} {str((int(ix), int(iy))):>12s}")

print("\n-- yellow pixels in tight keyword box (930:1030, 690:742) --")
for nm, key in (("w-first", KEY("我方先手")), ("w-second", KEY("我方后攻")),
                ("choice", KEY("我方选择")), ("in-duel", KEY("进入游戏后"))):
    rgb = np.asarray(Image.open(os.path.join(D, key)).convert("RGB")).astype(np.int16)
    bx = rgb[690:742, 930:1030]
    r, g, b = bx[:, :, 0], bx[:, :, 1], bx[:, :, 2]
    y = (r > 190) & (g > 150) & (b < 140) & (r - b > 80)
    print(f"  {nm:8s} yellow={int(y.sum()):5d} / {bx.shape[0] * bx.shape[1]}")

print("\n-- coin 60x60 ROI at (930,510) --")
for kw in ("硬币正面", "硬币反面"):
    roi = band(F[KEY(kw)], 930, 990, 510, 570)
    print(f"  {kw}  mean={roi.mean():7.2f} std={roi.std():6.2f} bright>200={(roi > 200).mean() * 100:6.2f}% dark<60={(roi < 60).mean() * 100:6.2f}%")
