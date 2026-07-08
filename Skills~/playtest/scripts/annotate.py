#!/usr/bin/env python3
"""playtest: 스크린샷에 액션 표기(빨간 점/화살표 + 스텝 라벨)를 그린다.
좌표는 클릭공간(좌하단 원점, y-up). 이미지(y-down)로 자동 변환: y_img = H - y_click.
사용:
  annotate.py <in.png> <out.png> [--tap X Y]... [--arrow X1 Y1 X2 Y2]... [--label "텍스트"]...
"""
import argparse
import math
import sys

from PIL import Image, ImageDraw, ImageFont

RED = (255, 40, 40, 255)


def conv(x, y, h):
    """클릭공간(y-up) → 이미지(y-down)."""
    return float(x), h - float(y)


def draw_tap(d, x, y, r):
    d.ellipse([x - r, y - r, x + r, y + r], outline=RED, width=max(3, r // 6))
    d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=RED)


def draw_arrow(d, x1, y1, x2, y2, w):
    d.line([x1, y1, x2, y2], fill=RED, width=w)
    ang = math.atan2(y2 - y1, x2 - x1)
    head = max(16, w * 4)
    for off in (math.radians(150), math.radians(-150)):
        hx = x2 + head * math.cos(ang + off)
        hy = y2 + head * math.sin(ang + off)
        d.line([x2, y2, hx, hy], fill=RED, width=w)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("infile")
    ap.add_argument("outfile")
    ap.add_argument("--tap", nargs=2, type=float, action="append", default=[], metavar=("X", "Y"))
    ap.add_argument("--arrow", nargs=4, type=float, action="append", default=[], metavar=("X1", "Y1", "X2", "Y2"))
    ap.add_argument("--label", action="append", default=[], metavar="TEXT")
    args = ap.parse_args()

    img = Image.open(args.infile).convert("RGBA")
    w, h = img.size
    d = ImageDraw.Draw(img)
    r = max(20, w // 30)
    lw = max(4, w // 180)

    for x, y in args.tap:
        cx, cy = conv(x, y, h)
        draw_tap(d, cx, cy, r)
    for x1, y1, x2, y2 in args.arrow:
        ax1, ay1 = conv(x1, y1, h)
        ax2, ay2 = conv(x2, y2, h)
        draw_arrow(d, ax1, ay1, ax2, ay2, lw)

    if args.label:
        # 한글 글리프 포함 폰트 우선(macOS) → 없으면 기본
        font = None
        for fp in (
            "/System/Library/Fonts/AppleSDGothicNeo.ttc",
            "/System/Library/Fonts/Supplemental/AppleGothic.ttf",
            "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
        ):
            try:
                font = ImageFont.truetype(fp, max(20, w // 28))
                break
            except Exception:
                continue
        if font is None:
            font = ImageFont.load_default()
        pad = 8
        ty = pad
        for text in args.label:
            box = d.textbbox((0, 0), text, font=font)
            tw, th = box[2] - box[0], box[3] - box[1]
            d.rectangle([pad, ty, pad + tw + 2 * pad, ty + th + 2 * pad], fill=(0, 0, 0, 180))
            d.text((pad * 2, ty + pad), text, fill=(255, 255, 255, 255), font=font)
            ty += th + 3 * pad

    img.convert("RGB").save(args.outfile)
    print(f"ANNOTATED {args.outfile} ({w}x{h})")


if __name__ == "__main__":
    sys.exit(main())
