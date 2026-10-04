# Ikona aplikacji: płomień świecy na nocnym granacie (barwy z Themes/Palette.axaml).
# Rysuje w 1024 px i zapisuje app/src/Uwielbienia.App/Assets/icon.png oraz icon.ico (16–256 px).
# Użycie: python app/tools/make_icon.py  (wymaga Pillow)
import math, os
from PIL import Image, ImageDraw, ImageFilter

S = 1024
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src', 'Uwielbienia.App', 'Assets')
os.makedirs(out, exist_ok=True)

def hexc(h, a=255):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)

img = Image.new('RGBA', (S, S), (0, 0, 0, 0))

# tło: zaokrąglony kwadrat, nocny granat z delikatnym przejściem ku górze
bg = Image.new('RGBA', (S, S))
top, bottom = hexc('#2A3360'), hexc('#121729')
for y in range(S):
    t = y / (S - 1)
    c = tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(3)) + (255,)
    ImageDraw.Draw(bg).line([(0, y), (S, y)], fill=c)
mask = Image.new('L', (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
img.paste(bg, (0, 0), mask)

cx = S / 2

# poświata płomienia
glow = Image.new('RGBA', (S, S), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gy = S * 0.33
for r, a in [(330, 40), (250, 55), (170, 70)]:
    gd.ellipse([cx - r, gy - r, cx + r, gy + r], fill=hexc('#F2B84B', a))
glow = glow.filter(ImageFilter.GaussianBlur(70))
glow_masked = Image.new('RGBA', (S, S), (0, 0, 0, 0))
glow_masked.paste(glow, (0, 0), mask)
img = Image.alpha_composite(img, glow_masked)

d = ImageDraw.Draw(img)

def teardrop(cx, cy, w, h, p=1.6, n=400):
    # czubek u góry, zaokrąglony dół; (cx, cy) = środek, w/h = połowa szerokości / wysokości
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = math.sin(t) * math.sin(t / 2) ** p
        y = -math.cos(t)
        pts.append((cx + x * w, cy + y * h))
    return pts

# świeca: kość słoniowa, zaokrąglona
body_top, body_bottom = S * 0.53, S * 0.89
bw = S * 0.135
d.rounded_rectangle([cx - bw, body_top, cx + bw, body_bottom], radius=int(S * 0.035), fill=hexc('#F4ECDD'))
# cień z prawej strony świecy dla bryły
d.rounded_rectangle([cx + bw * 0.35, body_top, cx + bw, body_bottom], radius=int(S * 0.03), fill=hexc('#D9CDB6'))
d.rectangle([cx + bw * 0.35, body_top, cx + bw * 0.6, body_bottom], fill=hexc('#D9CDB6'))
# knot
d.rounded_rectangle([cx - S * 0.008, body_top - S * 0.045, cx + S * 0.008, body_top + 4], radius=6, fill=hexc('#3A2A1A'))

# płomień: złoty z jaśniejszym wnętrzem
d.polygon(teardrop(cx, S * 0.30, S * 0.16, S * 0.205), fill=hexc('#F2B84B'))
d.polygon(teardrop(cx, S * 0.35, S * 0.08, S * 0.12), fill=hexc('#FFF1C9'))

img.save(os.path.join(out, 'icon.png'))
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
img.save(os.path.join(out, 'icon.ico'), sizes=[(s, s) for s in sizes])
print('ok')
