# -*- coding: utf-8 -*-
"""Locate UI anchors in the Master Duel screenshots and measure how separable they are."""
import os
import numpy as np
from numpy.lib.stride_tricks import sliding_window_view
from PIL import Image

D = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

NAMES = [
    "匹配界面.png",
    "硬币正面.png",
    "硬币反面.png",
    "我方选择先后手.png",
    "反面后对手选择先后手.png",
    "我方先手.png",
    "我方后攻.png",
    "后攻进入游戏后的画面.png",
]


def load_rgb(name):
    return np.asarray(Image.open(os.path.join(D, name)).convert("RGB")).astype(np.float32)


def gray(rgb):
    return (0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]).astype(np.float64)


def bbox(mask):
    ys, xs = np.where(mask)
    if len(ys) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def cc_norm(img, templ):
    """TM_CCOEFF_NORMED equivalent. img: (H,W) float, templ: (h,w) float."""
    H, W = img.shape
    h, w = templ.shape
    n = float(h * w)
    sumT, sumT2 = templ.sum(), float((templ * templ).sum())

    I1 = np.zeros((H + 1, W + 1))
    I2 = np.zeros((H + 1, W + 1))
    I1[1:, 1:] = img.cumsum(0).cumsum(1)
    I2[1:, 1:] = (img * img).cumsum(0).cumsum(1)

    def box(I):
        return (I[h:, w:] - I[: H - h + 1, w:] - I[h:, : W - w + 1] + I[: H - h + 1, : W - w + 1])

    sumI, sumI2 = box(I1), box(I2)
    TI = np.zeros((H - h + 1, W - w + 1), dtype=np.float64)
    for r in range(h):
        TI += sliding_window_view(img[r], w).dot(templ[r])

    num = TI - sumT * sumI / n
    den = np.sqrt(np.maximum(sumT2 - sumT * sumT / n, 1e-9) * np.maximum(sumI2 - sumI * sumI / n, 1e-9))
    return num / den


def best(score):
    return float(score.max()), int(np.unravel_index(score.argmax(), score.shape)[1]), int(np.unravel_index(score.argmax(), score.shape)[0])


def main():
    imgs = {n: load_rgb(n) for n in NAMES}
    print("== sizes ==")
    for n, im in imgs.items():
        print(f"  {n}: {im.shape[1]}x{im.shape[0]}")

    print("\n== central-band colour masks (x 500-1450, y 620-820) ==")
    for n, im in imgs.items():
        band = im[620:820, 500:1450]
        r, g, b = band[:, :, 0], band[:, :, 1], band[:, :, 2]
        yellow = (r > 190) & (g > 160) & (b < 130) & (r - b > 90)
        white = (r > 225) & (g > 225) & (b > 225)
        yb, wb = bbox(yellow), bbox(white)
        shift = lambda t: None if t is None else (t[0] + 500, t[1] + 620, t[2] + 500, t[3] + 620)
        print(f"  {n}\n    yellow px={int(yellow.sum()):6d} bbox={shift(yb)}\n    white  px={int(white.sum()):6d} bbox={shift(wb)}")

    print("\n== coin ROI brightness (centre 240x240 at 960,540) ==")
    for n in ("硬币正面.png", "硬币反面.png"):
        roi = imgs[n][420:660, 840:1080]
        print(f"  {n}: mean={roi.mean():7.2f}  std={roi.std():7.2f}  bright>200={(roi.mean(axis=2) > 200).mean() * 100:6.2f}%")


if __name__ == "__main__":
    main()
