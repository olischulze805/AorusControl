# Windows power overlay round-trip test

- Created: 2026-09-03 13:56:43 -03:00
- Scope: Windows power overlay only
- Gigabyte firmware/EC methods invoked: **no**
- Explicit write confirmation present: yes

- Original AC overlay: `00000000-0000-0000-0000-000000000000` (Balanced)

## Round trip

- BestEfficiency: expected `961cc777-2547-4f9d-8174-7d86181b8a7a`, read `961cc777-2547-4f9d-8174-7d86181b8a7a` — match
- Balanced: expected `00000000-0000-0000-0000-000000000000`, read `00000000-0000-0000-0000-000000000000` — match
- BestPerformance: expected `ded574b5-45a0-4f42-8737-46345c09c238`, read `ded574b5-45a0-4f42-8737-46345c09c238` — match

## Restore

- Restored `00000000-0000-0000-0000-000000000000` (Balanced)
- Exact original restored: yes
