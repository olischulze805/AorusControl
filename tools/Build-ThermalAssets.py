"""Builds the cooling page's live picture from Gigabyte's own product video.

    python tools/Build-ThermalAssets.py path/to/aorus5ve_thermal.mp4

Needs ffmpeg on PATH and Pillow plus NumPy. The video is not in this repository (see
src/AorusControl.App/Assets/README.md); this script exists so the four checked-in files can
be rebuilt or re-cropped rather than being four binaries nobody can reproduce.

Everything below is measured from the frame, not guessed: the chassis bounds by where the
grey body starts, and each fan's centre by walking the run of hub-grey pixels through it.
Change CROP and the fan centres in ThermalLayout have to change with it - they are this
image's own pixel space.
"""

import os
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

FRAME = 39                          # the brightest frame: both fans fully lit
CROP = (366, 100, 1584, 522)        # the cooling assembly: chassis edge down to just below the lower pipe loop
FANS = ((564, 355.5), (1383.5, 366.5))   # hub centres in the full frame
BLADE, EDGE = 88, 97                # blade radius, and how much of the housing comes along
HUB = 46                            # the metal cap in the middle, kept as it is
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "AorusControl.App", "Assets")


def frame(video):
    with tempfile.TemporaryDirectory() as scratch:
        subprocess.run(
            ["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", video, "-vsync", "0",
             os.path.join(scratch, "f_%03d.png")], check=True)
        return Image.open(os.path.join(scratch, f"f_{FRAME:03d}.png")).convert("RGB").copy()


def body(crop, pipe_mask):
    """The chassis, dimmed for a dark card and stripped of the video's red stage lighting.

    The pipes are taken down a little further than the rest. In the video they are lit like a
    product shot, near the top of the range, which leaves the app's travelling light nowhere
    to go; resting them lower gives the pulse room to actually brighten something.
    """
    image = ImageEnhance.Color(ImageEnhance.Brightness(crop).enhance(0.88)).enhance(0.94)
    pixels = np.asarray(image).astype(float)
    pixels *= (1 - 0.30 * pipe_mask)[..., None]
    r, g, b = pixels[..., 0], pixels[..., 1], pixels[..., 2]

    # Purely red pixels are the glow behind the machine; brass is red AND green, so it stays.
    red = np.clip((r - g - 30) / 60.0, 0, 1) * np.clip((r - b - 40) / 60.0, 0, 1) * (g < 110)
    grey = (r + g + b) / 3
    for channel, value in enumerate((r, g, b)):
        pixels[..., channel] = value * (1 - red * 0.75) + grey * red * 0.75

    # A soft edge, so the picture ends in the card instead of stopping like a pasted photo.
    height, width = pixels.shape[:2]
    y, x = np.mgrid[0:height, 0:width]
    fade = np.clip(np.minimum.reduce([x, y, width - 1 - x, height - 1 - y]) / 14.0, 0.55, 1)
    return Image.fromarray(np.clip(pixels * fade[..., None], 0, 255).astype(np.uint8))


def disc(full, centre):
    """One fan's blades as a circle with a feathered edge, ready to be rotated.

    The video lights the fans with a stylised swirl of orange and blue, which is fine in a
    product clip and wrong here: rotating it looks like a ring of fire turning, not like a
    fan. So the illumination is divided out. Averaging the tile over many small rotations
    gives the lighting alone - the blades repeat every ~7.7 degrees and average away, while
    the swirl spans half the disc and survives - and dividing the tile by that average leaves
    the blades on an even ground. The hub is put back untouched, because it has no blades to
    recover and the flat-field only adds noise there.
    """
    x, y = (int(round(value)) for value in centre)
    tile = full.crop((x - EDGE, y - EDGE, x + EDGE, y + EDGE))
    plain = tile.convert("L")
    grey = np.asarray(plain).astype(float)

    lighting = np.mean(
        [np.asarray(plain.rotate(angle, resample=Image.BICUBIC)).astype(float)
         for angle in np.arange(-26, 27, 2)], axis=0)

    side = tile.size[0]
    yy, xx = np.mgrid[0:side, 0:side]
    distance = np.hypot(xx - (side - 1) / 2, yy - (side - 1) / 2)
    blades = (distance > HUB) & (distance < BLADE)

    # The resting level comes from the darker end of the ring, not its median: the median is
    # dragged up by the very glow being removed, and a fan lit like a studio prop is exactly
    # what this is meant to stop being.
    level = np.percentile(grey[blades], 34)
    # A clamped ratio, because where the swirl was brightest the division still leaves hot
    # specks - the blades there carry no more information than anywhere else.
    ratio = np.clip(grey / np.maximum(lighting, 8), 0.78, 1.3)
    flat = np.clip(level * (1 + (ratio - 1) * 1.15), 0, 255)
    # Slightly recessed towards the rim, the way a fan sits in its well.
    flat *= 1 - 0.28 * np.clip((distance - HUB) / (BLADE - HUB), 0, 1) ** 2

    # Only the blades are replaced: the hub has none to recover, and the feathered edge keeps
    # the original housing so the disc does not sit on a bright halo of its own making.
    # The hub keeps its own shading but comes down with the blades; at full brightness it
    # reads as a lamp in the middle of a dark fan.
    result = np.where((distance > HUB) & (distance < BLADE - 3), flat, grey * 0.82)
    disc_image = Image.fromarray(np.clip(result, 0, 255).astype(np.uint8)).convert("RGB")

    # Drawn at 4x and scaled down: an ellipse mask at final size has visibly stepped edges.
    alpha = Image.new("L", (side * 4, side * 4), 0)
    ImageDraw.Draw(alpha).ellipse(
        (4 * (EDGE - BLADE), 4 * (EDGE - BLADE), 4 * (EDGE + BLADE), 4 * (EDGE + BLADE)), fill=255)
    disc_image.putalpha(alpha.resize((side, side), Image.LANCZOS).filter(ImageFilter.GaussianBlur(2.5)))
    return disc_image


def pipe_mask(crop):
    """How much of each pixel is heat pipe or fin stack, found by their brass colour."""
    pixels = np.asarray(crop).astype(float)
    r, g, b = pixels[..., 0], pixels[..., 1], pixels[..., 2]
    mask = (np.clip((r - b - 35) / 55.0, 0, 1) ** 0.7) * (np.clip((r - 75) / 70.0, 0, 1) ** 0.5)
    mask[(r <= g + 4) | (g <= b + 10)] = 0

    # The fans rotate as their own images on top, so they must not be in the mask as well.
    height, width = mask.shape
    y, x = np.mgrid[0:height, 0:width]
    for cx, cy in FANS:
        mask[(x - (cx - CROP[0])) ** 2 + (y - (cy - CROP[1])) ** 2 < (EDGE + 7) ** 2] = 0

    return np.clip(mask, 0, 1)


def main():
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)

    full = frame(sys.argv[1])
    crop = full.crop(CROP)
    os.makedirs(OUT, exist_ok=True)

    mask = pipe_mask(crop)
    body(crop, mask).save(os.path.join(OUT, "thermal-body.jpg"), quality=90, subsampling=1, optimize=True)
    alpha = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
    Image.merge("RGBA", (Image.new("L", alpha.size, 255),) * 3 + (alpha,)).save(
        os.path.join(OUT, "thermal-pipes.png"), optimize=True)
    for name, centre in zip(("left", "right"), FANS):
        disc(full, centre).save(os.path.join(OUT, f"thermal-fan-{name}.png"), optimize=True)

    for file in sorted(os.listdir(OUT)):
        print(f"{file:28} {os.path.getsize(os.path.join(OUT, file)) // 1024:>4} KB")


if __name__ == "__main__":
    main()
