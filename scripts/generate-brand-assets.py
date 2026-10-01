#!/usr/bin/env python3
"""Generate Toren's platform icon files from its shared vector geometry (Pillow)."""
from pathlib import Path
import argparse
import subprocess
import tempfile
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
LEFT = [(17, 18), (29, 18), (25, 26), (17, 26)]
MAIN = [(32, 18), (47, 18), (47, 26), (37, 26), (37, 47), (29, 51), (29, 26), (28, 26)]
SVG = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
  <defs><linearGradient id="tile" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#303F78"/><stop offset="1" stop-color="#141B32"/></linearGradient></defs>
  <rect x="6" y="6" width="52" height="52" rx="15" fill="url(#tile)" stroke="#6273A6" stroke-width="0.6"/>
  <path d="M17 18H29L25 26H17Z" fill="#85DCEE"/>
  <path d="M32 18H47V26H37V47L29 51V26H28Z" fill="#CDD7FF"/>
</svg>
'''

def render(size):
    scale = max(size, 1024) / 64
    pixels = round(64 * scale)
    tile = Image.new('RGBA', (pixels, pixels))
    # A diagonal gradient stays subtle inside the compact rounded tile.
    start, end = (48, 63, 120), (20, 27, 50)
    data = []
    for y in range(pixels):
        for x in range(pixels):
            mix = max(0, min(1, ((x + y) / scale - 12) / 104))
            data.append(tuple(round(a + (b - a) * mix) for a, b in zip(start, end)) + (255,))
    tile.putdata(data)
    mask = Image.new('L', (pixels, pixels))
    ImageDraw.Draw(mask).rounded_rectangle(tuple(round(v * scale) for v in (6, 6, 58, 58)), radius=round(15 * scale), fill=255)
    tile.putalpha(mask)
    draw = ImageDraw.Draw(tile)
    draw.rounded_rectangle(tuple(round(v * scale) for v in (6, 6, 58, 58)), radius=round(15 * scale), outline='#6273A6', width=max(1, round(.6 * scale)))
    draw.polygon([(round(x*scale), round(y*scale)) for x,y in LEFT], fill='#85DCEE')
    draw.polygon([(round(x*scale), round(y*scale)) for x,y in MAIN], fill='#CDD7FF')
    return tile.resize((size, size), Image.Resampling.LANCZOS)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT/'src/Toren.App/Assets/Brand')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output/'toren-logo.svg').write_text(SVG)
    master = render(1024)
    master.save(args.output/'toren-icon-1024.png')
    master.resize((256,256), Image.Resampling.LANCZOS).save(args.output/'toren-icon-256.png')
    master.save(args.output/'toren.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
    with tempfile.TemporaryDirectory(prefix='toren-brand-') as temp:
        iconset = Path(temp)/'Toren.iconset'; iconset.mkdir()
        for size in (16,32,128,256,512):
            for factor in (1,2):
                suffix = '@2x' if factor == 2 else ''
                master.resize((size*factor,size*factor),Image.Resampling.LANCZOS).save(iconset/f'icon_{size}x{size}{suffix}.png')
        subprocess.run(['iconutil','-c','icns',str(iconset),'-o',str(args.output/'Toren.icns')],check=True)
    print('Generated SVG, PNG, ICO and ICNS assets.')

if __name__ == '__main__': main()
