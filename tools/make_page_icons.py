"""Draws the Community window's navigation icons (one glyph per page) into Textures/WilsFamilyLife/Pages.
Run: python tools/make_page_icons.py   (needs Pillow). Glyphs are drawn at 4x and reduced, so edges are smooth at 20px."""
import math
import os
from PIL import Image, ImageDraw

S = 256                      # drawing size; saved at 64
INK = (236, 229, 208, 255)   # parchment, readable on the dark navigation
GOLD = (228, 184, 74, 255)
OUT = os.path.join(os.path.dirname(__file__), "..", "Textures", "WilsFamilyLife", "Pages")


def canvas():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def overview(d):          # four tiles
    for x, y in ((40, 40), (140, 40), (40, 140), (140, 140)):
        d.rounded_rectangle((x, y, x + 76, y + 76), 12, fill=INK)
    d.rounded_rectangle((140, 140, 216, 216), 12, fill=GOLD)


def families(d):          # a house with a heart door
    d.polygon([(128, 28), (232, 120), (206, 120), (206, 220), (50, 220), (50, 120), (24, 120)], fill=INK)
    d.ellipse((98, 118, 134, 154), fill=(40, 40, 44, 255)); d.ellipse((122, 118, 158, 154), fill=(40, 40, 44, 255))
    d.polygon([(100, 142), (156, 142), (128, 184)], fill=(40, 40, 44, 255))


def professions(d):       # a ladder of three rungs rising (career tiers)
    d.rectangle((44, 196, 100, 224), fill=INK); d.rectangle((100, 140, 156, 224), fill=INK); d.rectangle((156, 76, 212, 224), fill=INK)
    d.polygon([(184, 22), (214, 62), (154, 62)], fill=GOLD)


def assembly(d):          # three heads and shoulders
    for cx, cy, r, c in ((128, 84, 34, GOLD), (60, 112, 26, INK), (196, 112, 26, INK)):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=c)
    d.pieslice((74, 128, 182, 236), 180, 360, fill=GOLD)
    d.pieslice((18, 146, 104, 232), 180, 360, fill=INK); d.pieslice((152, 146, 238, 232), 180, 360, fill=INK)


def education(d):         # an open book
    d.polygon([(128, 70), (128, 214), (44, 190), (44, 46)], fill=INK)
    d.polygon([(128, 70), (128, 214), (212, 190), (212, 46)], fill=(214, 206, 182, 255))
    d.line((128, 70, 128, 214), fill=(40, 40, 44, 255), width=8)
    for k in range(3):
        y = 88 + k * 30
        d.line((60, y - 8, 112, y + 4), fill=(40, 40, 44, 255), width=6); d.line((144, y + 4, 196, y - 8), fill=(40, 40, 44, 255), width=6)


def care(d):              # a medical cross
    d.rounded_rectangle((92, 30, 164, 226), 16, fill=INK); d.rounded_rectangle((30, 92, 226, 164), 16, fill=INK)
    d.rounded_rectangle((110, 48, 146, 208), 8, fill=(190, 60, 60, 255)); d.rounded_rectangle((48, 110, 208, 146), 8, fill=(190, 60, 60, 255))


def treasury(d):          # a coin with a mark
    d.ellipse((30, 30, 226, 226), fill=GOLD); d.ellipse((58, 58, 198, 198), outline=(120, 86, 20, 255), width=10)
    d.rectangle((118, 84, 138, 172), fill=(120, 86, 20, 255)); d.rectangle((96, 104, 160, 120), fill=(120, 86, 20, 255))


def establishments(d):    # a shop with an awning
    d.rectangle((44, 112, 212, 222), fill=INK)
    for i in range(6):
        d.polygon([(36 + i * 31, 54), (67 + i * 31, 54), (74 + i * 31, 112), (30 + i * 31, 112)], fill=GOLD if i % 2 == 0 else INK)
    d.rectangle((108, 150, 148, 222), fill=(40, 40, 44, 255)); d.rectangle((60, 134, 94, 176), fill=(40, 40, 44, 255)); d.rectangle((162, 134, 196, 176), fill=(40, 40, 44, 255))


def sciences(d):          # a flask
    d.rectangle((104, 28, 152, 108), fill=INK)
    d.polygon([(104, 100), (152, 100), (222, 214), (34, 214)], fill=INK)
    d.polygon([(76, 170), (180, 170), (206, 208), (50, 208)], fill=GOLD)
    d.ellipse((112, 134, 136, 158), fill=GOLD)
    d.rectangle((92, 22, 164, 38), fill=INK)


def military(d):          # a shield
    d.polygon([(128, 24), (216, 56), (216, 128), (128, 232), (40, 128), (40, 56)], fill=INK)
    d.polygon([(128, 52), (190, 74), (190, 126), (128, 204), (66, 126), (66, 74)], fill=(150, 48, 48, 255))
    d.line((128, 76, 128, 170), fill=INK, width=14); d.line((96, 108, 160, 108), fill=INK, width=14)


def prisoners(d):         # a cell window: frame and bars
    d.rounded_rectangle((36, 28, 220, 228), 22, fill=INK)
    d.rounded_rectangle((60, 52, 196, 204), 10, fill=(40, 40, 44, 255))
    for x in (92, 128, 164):
        d.rectangle((x - 7, 52, x + 7, 204), fill=GOLD)
    d.rectangle((60, 112, 196, 128), fill=GOLD)


def works(d):             # a hammer crossed with a pick
    d.line((52, 204, 176, 80), fill=INK, width=24)
    d.polygon([(150, 38), (214, 102), (186, 130), (122, 66)], fill=GOLD)
    d.line((204, 204, 80, 80), fill=INK, width=24)
    d.arc((22, 30, 134, 142), 190, 290, fill=INK, width=24)


ICONS = [overview, families, professions, assembly, education, care, treasury, establishments, sciences, military, prisoners, works]


def main():
    os.makedirs(OUT, exist_ok=True)
    for fn in ICONS:
        im, d = canvas(); fn(d)
        im.resize((64, 64), Image.LANCZOS).save(os.path.join(OUT, fn.__name__ + ".png"))
        print("wrote", fn.__name__)


if __name__ == "__main__":
    main()
