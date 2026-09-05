# AORUS fan-curve floor probe

- Created: 2026-09-05 13:47:24 -03:00
- Question: does the EC store curve values below the verified floor of raw 57, or clamp them?
- Method: write one candidate into points 0 and 1, read all 15 points back, restore.
- The fan mode is never switched to Dynamic, so the probe curve never regulates the fans.
- Explicit curve-write confirmation present: yes

- Fan state at start: fixed 0, step 0, auto 0, thermal 0

## Original curve

- (30,57), (34,57), (37,57), (40,57), (44,57), (48,57), (52,57), (59,57), (64,73), (68,87), (72,101), (77,115), (82,126), (87,137), (90,229)

## Candidates

| Written | Read back point 0 | Read back point 1 | Other 13 points unchanged |
|---|---|---|---|
| 50 | 50 | 50 | yes |
| 40 | 40 | 40 | yes |
| 30 | 30 | 30 | yes |
| 20 | 20 | 20 | yes |
| 10 | 10 | 10 | yes |
| 0 | 0 | 0 | yes |

## Restore

- All 15 original points restored and verified: yes
- (30,57), (34,57), (37,57), (40,57), (44,57), (48,57), (52,57), (59,57), (64,73), (68,87), (72,101), (77,115), (82,126), (87,137), (90,229)
