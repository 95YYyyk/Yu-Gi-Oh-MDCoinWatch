# -*- coding: utf-8 -*-
"""从截图里抠出金币图标（透明底），压暗一份当反面，再生成 .ico。"""
import os, sys, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.stdout.reconfigure(encoding='utf-8', errors='replace')

D = 'D:/库文件/文档/ChatGPT/MD'
OUT = os.path.join(D, 'app', 'assets')
PREVIEW = os.path.join(D, 'probe', 'out3')
os.makedirs(OUT, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)

gray = lambda a: 0.299 * a[:, :, 0] + 0.587 * a[:, :, 1] + 0.114 * a[:, :, 2]


def fit_disc(tile, inset=0.10, thresh=90):
    """亮圆盘的包围盒 -> 圆心和半径。币面本身有暗纹，所以用整体外接框而不是质心。"""
    a = np.asarray(tile).astype(np.float32)
    h, w = a.shape[:2]
    iy0, iy1 = int(h * inset), int(h * (1 - inset))
    ix0, ix1 = int(w * inset), int(w * (1 - inset))
    m = gray(a[iy0:iy1, ix0:ix1]) > thresh
    rows = np.where(m.any(axis=1))[0]
    cols = np.where(m.any(axis=0))[0]
    if len(rows) < 10 or len(cols) < 10:
        raise SystemExit('没找到硬币')
    ytop, ybot = int(rows[0]), int(rows[-1])
    xl, xr = int(cols[0]), int(cols[-1])
    cx = (xl + xr) / 2.0 + ix0
    cy = (ytop + ybot) / 2.0 + iy0
    radius = max(xr - xl, ybot - ytop) / 2.0
    print('    掩码范围 x %d-%d  y %d-%d' % (xl + ix0, xr + ix0, ytop + iy0, ybot + iy0))
    return cx, cy, radius


def cut(img, cx, cy, r, pad=1.02, feather=0.018, size=128, shrink=0.985):
    """按圆心半径从整图里裁正方形，套羽化圆形 alpha，再缩放。"""
    half = int(round(r * shrink * pad))
    cx_i, cy_i = int(round(cx)), int(round(cy))
    box = (cx_i - half, cy_i - half, cx_i + half, cy_i + half)
    crop = img.crop(box).convert('RGBA')
    n = crop.size[0]
    ss = 4
    m = Image.new('L', (n * ss, n * ss), 0)
    ImageDraw.Draw(m).ellipse((0, 0, n * ss - 1, n * ss - 1), fill=255)
    m = m.resize((n, n), Image.LANCZOS).filter(ImageFilter.GaussianBlur(max(0.6, n * feather)))
    crop.putalpha(m)
    return crop.resize((size, size), Image.LANCZOS)


def write_ico(path, base, sizes=(16, 24, 32, 48, 64, 128, 256)):
    """把 PNG 塞进 ICO，Vista 之后都支持。"""
    blobs = []
    for s in sizes:
        im = base.resize((s, s), Image.LANCZOS)
        tmp = os.path.join(PREVIEW, '_ico_%d.png' % s)
        im.save(tmp, 'PNG')
        blobs.append(open(tmp, 'rb').read())
        os.remove(tmp)
    n = len(sizes)
    out = bytearray(struct.pack('<HHH', 0, 1, n))
    offset = 6 + 16 * n
    for s, b in zip(sizes, blobs):
        w = 0 if s >= 256 else s
        out += struct.pack('<BBBBHHII', w, w, 0, 0, 1, 32, len(b), offset)
        offset += len(b)
    for b in blobs:
        out += b
    open(path, 'wb').write(bytes(out))


shot = Image.open(os.path.join(D, '新补充截图', '硬币图标.png')).convert('RGB')
TILE = (850, 172, 1005, 332)          # 第 3 枚：带翅膀的小妖精，跟聊天里那张同款

cx_t, cy_t, r = fit_disc(shot.crop(TILE))
cx, cy = cx_t + TILE[0], cy_t + TILE[1]
print('硬币  截图坐标圆心=(%.1f, %.1f)  半径=%.1f' % (cx, cy, r))

front = cut(shot, cx, cy, r)
front.save(os.path.join(OUT, 'coin_front.png'))
front.resize((64, 64), Image.LANCZOS).save(os.path.join(OUT, 'coin_front64.png'))

a = np.asarray(front).astype(np.float32)
lum = 0.299 * a[:, :, 0] + 0.587 * a[:, :, 1] + 0.114 * a[:, :, 2]
dark = np.zeros_like(a)
dark[:, :, 0] = lum * 0.26 + 5
dark[:, :, 1] = lum * 0.28 + 7
dark[:, :, 2] = lum * 0.33 + 13
dark[:, :, 3] = a[:, :, 3]
back = Image.fromarray(np.clip(dark, 0, 255).astype(np.uint8), 'RGBA')
back.save(os.path.join(OUT, 'coin_back.png'))
back.resize((64, 64), Image.LANCZOS).save(os.path.join(OUT, 'coin_back64.png'))

def to_bgra_premul(im, path):
    """AlphaBlend 要的是预乘 alpha 的 BGRA。"""
    a = np.asarray(im).astype(np.float32)
    al = a[:, :, 3:4] / 255.0
    rgb = a[:, :, :3] * al
    out = np.empty((a.shape[0], a.shape[1], 4), dtype=np.uint8)
    out[:, :, 0] = np.clip(rgb[:, :, 2], 0, 255)
    out[:, :, 1] = np.clip(rgb[:, :, 1], 0, 255)
    out[:, :, 2] = np.clip(rgb[:, :, 0], 0, 255)
    out[:, :, 3] = a[:, :, 3]
    out.tofile(path)
    return os.path.getsize(path)


nf = to_bgra_premul(front, os.path.join(OUT, 'coin_front.bgra'))
nb = to_bgra_premul(back, os.path.join(OUT, 'coin_back.bgra'))
print('已输出 coin_front.bgra (%d B) / coin_back.bgra (%d B)' % (nf, nb))

write_ico(os.path.join(OUT, 'app.ico'), front)
print('已输出 coin_front.png / coin_back.png / app.ico')

# 预览
sheet = Image.new('RGB', (620, 330), (18, 20, 26))
sheet.paste(shot.crop(TILE).resize((160, 165), Image.LANCZOS), (16, 40))
for i, s in enumerate((28, 40, 56)):
    t = front.resize((s, s), Image.LANCZOS)
    card = Image.new('RGB', (s + 24, s + 24), (26, 28, 36))
    card.paste(t, (12, 12), t)
    sheet.paste(card, (200 + (i % 2) * 96, 20 + (i // 2) * 96))
for i, s in enumerate((28, 40, 56)):
    t = back.resize((s, s), Image.LANCZOS)
    card = Image.new('RGB', (s + 24, s + 24), (26, 28, 36))
    card.paste(t, (12, 12), t)
    sheet.paste(card, (200 + (i % 2) * 96, 150 + (i // 2) * 96))
sheet.paste(front, (420, 40), front)
sheet.paste(back, (420, 180), back)
sheet.save(os.path.join(PREVIEW, 'coin_sheet.png'))
print('预览 probe/out3/coin_sheet.png')
