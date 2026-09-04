# AORUS Fn+Space brightness signal hunt

- Created: 2026-09-03 12:04:18 -03:00
- Exact target device: `VID 1044 / PID 7A41`
- Mode: **read-only**. No setter, no output report, no WMI, no EC write
- Privacy gate: every collection declaring keyboard usage page `0x0007` is skipped, so keystrokes cannot be captured
- Report ID `0x5A` (ITE flash channel) written: **no**
- New ground 1: official getter `0x88` queried for selectors `0` to `15`; only `0` to `3` were ever used before
- New ground 2: input listening on the small `MI_02` collections, including `COL_03` and `COL_04`, which declare no usages

- `MI_02 / COL_07/COL_07`: skipped, no input reports

- Collections listened to: `10`
  - `MI_01`, input report length `65`
  - `MI_02 / COL_04/COL_04`, input report length `4`
  - `MI_02 / COL_05/COL_05`, input report length `31`
  - `MI_02 / COL_06/COL_06`, input report length `5`
  - `MI_02 / COL_02/COL_02`, input report length `2`
  - `MI_02 / COL_08/COL_08`, input report length `2`
  - `MI_02 / COL_03/COL_03`, input report length `3`
  - `MI_02 / COL_01/COL_01`, input report length `8`
  - `MI_00`, input report length `37`
  - `MI_03`, input report length `65`

## Captured input reports

- Round 1: `MI_00` could not be opened (Unable to open HID class device (\\?\hid#vid_1044&pid_7a41&mi_00#7&21d0e8&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}\kbd).)
- Round 1: `MI_02 / COL_01/COL_01` could not be opened (Unable to open HID class device (\\?\hid#vid_1044&pid_7a41&mi_02&col01#7&26820964&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}).)
- Round 1: `MI_02 / COL_05/COL_05` could not be opened (Unable to open HID class device (\\?\hid#vid_1044&pid_7a41&mi_02&col05#7&26820964&0&0004#{4d1e55b2-f16f-11cf-88cb-001111000030}\kbd).)
- Round 1: `MI_02 / COL_06/COL_06` could not be opened (Unable to open HID class device (\\?\hid#vid_1044&pid_7a41&mi_02&col06#7&26820964&0&0005#{4d1e55b2-f16f-11cf-88cb-001111000030}).)

## State per round

| Query | /stop |
|---|---|
| `0x88 sel  0` | `008800000000000000` |
| `0x88 sel  1` | `0088010000FF320000` |
| `0x88 sel  2` | `008802FF02FF320000` |
| `0x88 sel  3` | `008803FF0006320000` |
| `0x88 sel  4` | `008804000000000000` |
| `0x88 sel  5` | `008805000000000000` |
| `0x88 sel  6` | `008806000000000000` |
| `0x88 sel  7` | `008807000000000000` |
| `0x88 sel  8` | `008808000000000000` |
| `0x88 sel  9` | `008809000000000000` |
| `0x88 sel 10` | `00880A000000000000` |
| `0x88 sel 11` | `00880B000000000000` |
| `0x88 sel 12` | `00880C000000000000` |
| `0x88 sel 13` | `00880D000000000000` |
| `0x88 sel 14` | `00880E000000000000` |
| `0x88 sel 15` | `00880F000000000000` |
| `0x80 firmware` | `008013040000000000` |

## Verdict

- **No queried value changed across the rounds.** Neither the extended selector range of the official getter nor the firmware query carries the Fn+Space step.
