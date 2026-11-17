from PIL import Image, ImageDraw
S = 96; g = (226, 184, 74, 255)
im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
d.line([48, 88, 48, 22], fill=g, width=5)          # a stalk of wheat with grain pairs
for i, y in enumerate(range(24, 70, 11)):
    d.ellipse([48 - 22, y, 48 - 4, y + 12], fill=g); d.ellipse([48 + 4, y, 48 + 22, y + 12], fill=g)
d.ellipse([42, 6, 54, 26], fill=g)
im.save("Textures/WilsFamilyLife/Pages/agriculture.png")
