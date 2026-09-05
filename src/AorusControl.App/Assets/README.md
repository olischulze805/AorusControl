# Assets

## app.ico

The application icon.

## thermal-body.jpg, thermal-fan-left.png, thermal-fan-right.png, thermal-pipes.png

The live picture on the cooling page: this laptop's own cooling assembly seen from below.

**Where it comes from.** A single frame of Gigabyte's own product video for the AORUS 5
(`aorus5ve_thermal.mp4`, the thermal-design clip from the AORUS 5 product page). The video
itself is not in this repository - it is Gigabyte's material, kept out of version control the
same way the decompiled vendor software under `third-party/` is. Only these four derived
files are checked in, because the app has to ship something to draw.

If this app is ever published more widely than "the owner of this laptop and their own
machine", these four files are the part to look at first: they are a derivative of a vendor
image, and replacing them with an own drawing (there was one, in the history before this) is
the clean answer.

**How they were made.** `tools/Build-ThermalAssets.py`, from frame 39 of the video:

| File | What it is |
| --- | --- |
| `thermal-body.jpg` | The chassis, cropped to the cooling assembly (1218 × 600), dimmed to sit in a dark card, with the video's red ambient glow desaturated and the outer edge faded |
| `thermal-fan-left/right.png` | Each fan's blades cut out as a circle with a soft edge, so `FanRotor` can rotate them over the housing they came from |
| `thermal-pipes.png` | An alpha mask of the heat pipes and fin stacks, picked out by their brass colour - the warm pulse is drawn through this, so it can only ever light up actual metal |

The coordinates the app draws with (`ThermalLayout`) are this image's own pixel space, so
regenerating the files with different bounds means updating the fan centres there too.
