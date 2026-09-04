# AORUS visible RGB-effect test — batch 1

- Created: 2026-09-01 20:09:39 -03:00
- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`
- Sequence: Breathing → Wave → Fade-on-keypress
- Hold per effect: 8 seconds
- Shared parameters: raw speed `5`, brightness `50`, direction `1`
- Restore policy: capture all zones; restore and verify in `finally`

## Requests

- `Breathing` ID `2`: request `0008000205320201BB`, global readback `008800000000000000`. Expected observation: The whole keyboard should pulse green.
- `Wave` ID `3`: request `0008000305320801B4`, global readback `008800000000000000`. Expected observation: A moving/rainbow wave should cross the three zones.
- `Fade-on-keypress` ID `4`: request `0008000405320201B9`, global readback `008800000000000000`. Expected observation: Press several keys; their zone should react/fade if supported.

## Restoration

- Zone 1: `#00FF00`, brightness `50`, verified **yes**
- Zone 2: `#00FF00`, brightness `50`, verified **yes**
- Zone 3: `#00FF00`, brightness `50`, verified **yes**

## Observation status

- Visible behavior requires the user's physical observation; HID global readback is known to return zeros on this firmware.
