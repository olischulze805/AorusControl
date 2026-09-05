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
    """One fan's blades as a circle with a feathered edge, ready to be rotated."""
    x, y = (int(round(value)) for value in centre)
    tile = full.crop((x - EDGE, y - EDGE, x + EDGE, y + EDGE))
    side = tile.size[0]
    # Drawn at 4x and scaled down: an ellipse mask at final size has visibly stepped edges.
    alpha = Image.new("L", (side * 4, side * 4), 0)
    ImageDraw.Draw(alpha).ellipse(
        (4 * (EDGE - BLADE), 4 * (EDGE - BLADE), 4 * (EDGE + BLADE), 4 * (EDGE + BLADE)), fill=255)
    tile.putalpha(alpha.resize((side, side), Image.LANCZOS).filter(ImageFilter.GaussianBlur(2.5)))
    return tile


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
