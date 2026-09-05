# AORUS fan-curve floor probe

- Created: 2026-09-05 13:38:26 -03:00
- Question: does the EC store curve values below the verified floor of raw 57, or clamp them?
- Method: write one candidate into points 0 and 1, read all 15 points back, restore.
- The fan mode is never switched to Dynamic, so the probe curve never regulates the fans.
- Explicit curve-write confirmation present: yes


- Probe failed: Der Test startet nur aus dem verifizierten Normalzustand.
