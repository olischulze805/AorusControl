# AORUS Fn+Space brightness event monitor

- Created: 2026-09-03 12:35:41 -03:00
- Exact target device: `VID 1044 / PID 7A41`
- Listened collection: `MI_02 / COL_04`, input report length `4`
- Mode: **read-only**. No setter, no output report, no feature report, no WMI, no EC access
- Privacy gate: the collection must not declare keyboard usage page `0x0007`
- Duration: `10` s
- Known so far: byte 0 is report ID `0x04`, byte 1 is constant `0x01`, byte 2 carries the step; observed `0`, `24`, `32`

## Events

- No input report was received during the monitoring window.
