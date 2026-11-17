from PIL import Image, ImageDraw
S = 96; g = (226, 184, 74, 255)

# a flag: Military's "show positions" toggle
im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
d.rectangle([28, 12, 34, 84], fill=g); d.polygon([(34, 14), (78, 28), (34, 46)], fill=g); d.ellipse([20, 78, 46, 90], fill=g)
im.save("Textures/WilsFamilyLife/Pages/posts.png")

# a clock face: the Today page
im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
d.ellipse([10, 10, 86, 86], outline=g, width=7)
d.line([48, 48, 48, 22], fill=g, width=6); d.line([48, 48, 66, 58], fill=g, width=6); d.ellipse([43, 43, 53, 53], fill=g)
im.save("Textures/WilsFamilyLife/Pages/today.png")
