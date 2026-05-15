# GLS-A Golf + Agroindustri Presets Design

## Context

Add remaining GLS-A farming-related sectors: Golf (#4014) and Agroindustri (#4012) with all 8 sub-sector loenbilag. Period: 2024-2026.

## New Presets (18 total)

### Golf (2 presets)

Simple flat 100% OT for all overtime. Source: Golf Overenskomst §16.

| Key | Label | OT All | Sun/Holiday | Normal |
|-----|-------|--------|-------------|--------|
| `glsa-golf-standard` | Golf - Standard | 100% | 100% | 37h/week |
| `glsa-golf-elev` | Golf - Elev | 100% | 100% | 8h/day cap |

PayDayRules for Standard:
- WEEKDAY: NORMAL@26640, OVERTIME_100@null
- SATURDAY: SAT_NORMAL@21600, SAT_AFTERNOON@null (split at 12:00 per §16)
- SUNDAY: SUN_HOLIDAY@null
- HOLIDAY: SUN_HOLIDAY@null
- GRUNDLOVSDAG: GRUNDLOVSDAG@null

### Agroindustri (16 presets = 8 sub-sectors x 2 variants)

Source: Agroindustri Overenskomst Kapitel 21 Lonninger. Normal hours: 37h/week, Mon-Sat 06:00-18:00. All have +100% Sun/Holiday.

| Sub-sector | Key prefix | OT 1-2h | OT 3h+ | Notes |
|-----------|-----------|---------|--------|-------|
| Fjerkraeproduktion | `glsa-agro-fjerkrae` | 30% | 50% then 100% | Poultry/hatcheries |
| Grovvarehandler | `glsa-agro-grovvare` | 40% (3h!) | 100% | Grain trade |
| Gulerodspakkerier | `glsa-agro-gulerod` | 30% | 80% then 100% | Carrot packing |
| Kartoffelmelsfabrikker | `glsa-agro-kartoffelmel` | 30% | 50% then 100% | Potato starch |
| Kartoffelsortercentraler | `glsa-agro-kartoffelsorter` | 30% | 80% then 100% | Potato sorting |
| Lucerne/Graestorrerier | `glsa-agro-lucerne` | 30% | 80% | Alfalfa/grass drying |
| Minkfodercentraler | `glsa-agro-minkfoder` | 30% | 80% then 100% | Mink feed |
| Ovrige agroindustrielle | `glsa-agro-ovrige` | 30% | 80% | Other agro-industrial |

### Tier Structures (PayDayRules WEEKDAY)

**Pattern A - Fjerkrae/Kartoffelmel (30%/50%/100%):**
- Tier1: NORMAL@26640 (7.4h)
- Tier2: OVERTIME_30@33840 (7.4h+2h)
- Tier3: OVERTIME_50@? (need to determine 3h boundary)
- Tier4: OVERTIME_100@null

Actually, re-reading the PDF: Fjerkrae §3 says "For 1. og 2. time (+30%)" then "For 3. time (+50%)" then "Derefter og for son- og helligdage (+100%)". So:
- Tier1: NORMAL@26640
- Tier2: OVERTIME_30@33840 (2h at 30%)
- Tier3: OVERTIME_50@37440 (1h at 50%... wait that's only 1 hour)

Re-reading more carefully: "For 1. og 2. time efter normal daglig arbejdstids ophor (+30 %)" means first 2 hours at +30%. Then "For 3. time efter normal arbejdstids ophor (+50 %)" means the 3rd hour at +50%. Then "Derefter og for son- og helligdage (+100%)" means 4th hour+ and all Sun/Holiday at +100%.

So Fjerkrae: 2h@30%, 1h@50%, then 100%:
- Tier1: NORMAL@26640
- Tier2: OVERTIME_30@33840 (2h)
- Tier3: OVERTIME_50@37440 (1h more = 3h total OT)
- Tier4: OVERTIME_100@null

**Pattern B - Grovvare (40%/100%):**
"For 1., 2. og 3. time (+40%)" then "Derefter og son- og helligdage (+100%)"
- Tier1: NORMAL@26640
- Tier2: OVERTIME_40@37440 (3h at 40%)
- Tier3: OVERTIME_100@null

**Pattern C - Gulerod/Kartoffelsorter/Minkfoder (30%/80%/100%):**
"For 1. og 2. time (+30%)" then "For 3. time (+50%)" then "Derefter og helligdage (+100%)"

Wait - re-reading Gulerodspakkerier §4: "For 1. og 2. time (+30%)" then "For 3. time (+80%)" - that's different from what I initially noted. Let me re-check each.

Actually from the PDF wage appendices:
- **Gulerodspakkerier §3**: "For 1. og 2. time (+30%)" then "For efterfolgende overarbejdstimer indtil kl. 20.00, samt arbejde pa son- og helligdage (+80%)" then "For natarbejde, regnet fra kl. 20.00 og til normal arbejdstids begyndelse, samt for arbejde pa son- og helligdage efter kl. 12.00 (+100%)"
- **Kartoffelsortercentraler §3**: Same as Gulerod pattern
- **Kartoffelmelsfabrikker §3**: "For 1. og 2. time (+30%)" then "For 3. time (+50%)" then "Derefter og son- og helligdage (+100%)"
- **Lucerne §2**: "For 1. og 2. time (+30%)" then "Derefter samt arbejde pa son- og helligdage (+80%)"
- **Minkfoder §3**: "For 1. og 2. time (+30%)" then "For efterfolgende overarbejdstimer indtil kl. 20.00 (+80%)" then "For natarbejde kl. 20.00 til normal (+100%)"
- **Ovrige §3**: "For 1. og 2. time (+30%)" then "Derefter (+80%)" then "son- og helligdage (+100%)"

So the accurate tier structures are:

| Sub-sector | Tier1 | Tier2 | Tier3 | Tier4 | Sun/Holiday |
|-----------|-------|-------|-------|-------|-------------|
| Fjerkrae | NORMAL@26640 | OT_30@33840 | OT_50@37440 | OT_100@null | 100% |
| Grovvare | NORMAL@26640 | OT_40@37440 | OT_100@null | - | 100% |
| Gulerod | NORMAL@26640 | OT_30@33840 | OT_80@null | - | 100% (simplified) |
| Kartoffelmel | NORMAL@26640 | OT_30@33840 | OT_50@37440 | OT_100@null | 100% |
| Kartoffelsorter | NORMAL@26640 | OT_30@33840 | OT_80@null | - | 100% (simplified) |
| Lucerne | NORMAL@26640 | OT_30@33840 | OT_80@null | - | 80% |
| Minkfoder | NORMAL@26640 | OT_30@33840 | OT_80@null | - | 100% (simplified) |
| Ovrige | NORMAL@26640 | OT_30@33840 | OT_80@null | - | 100% |

Note: Gulerod, Kartoffelsorter, Minkfoder have complex night/evening splits (before/after 20:00) but for tier-based splitting we simplify to the dominant weekday pattern. The time-band rules (after 20:00 = 100%) can be added as PayDayTypeRules.

### Elev Variants

All Agroindustri elev presets follow the under-18 pattern: 8h/day cap, same OT rates but with ELEV_ prefix. Each sub-sector gets one Elev variant.

### Dropdown Structure Addition

```
GLS-A / 3F
  ... (existing 11 presets) ...
  Golf - Standard 2024-2026               (NEW)
  Golf - Elev 2024-2026                   (NEW)
  Agroindustri Fjerkrae - Standard 2024-2026  (NEW)
  Agroindustri Fjerkrae - Elev 2024-2026      (NEW)
  Agroindustri Grovvare - Standard 2024-2026  (NEW)
  Agroindustri Grovvare - Elev 2024-2026      (NEW)
  Agroindustri Gulerod - Standard 2024-2026   (NEW)
  Agroindustri Gulerod - Elev 2024-2026       (NEW)
  Agroindustri Kartoffelmel - Standard 2024-2026  (NEW)
  Agroindustri Kartoffelmel - Elev 2024-2026      (NEW)
  Agroindustri Kartoffelsorter - Standard 2024-2026  (NEW)
  Agroindustri Kartoffelsorter - Elev 2024-2026      (NEW)
  Agroindustri Lucerne - Standard 2024-2026   (NEW)
  Agroindustri Lucerne - Elev 2024-2026       (NEW)
  Agroindustri Minkfoder - Standard 2024-2026 (NEW)
  Agroindustri Minkfoder - Elev 2024-2026     (NEW)
  Agroindustri Ovrige - Standard 2024-2026    (NEW)
  Agroindustri Ovrige - Elev 2024-2026        (NEW)
```

## Files to Modify

| File | Change |
|------|--------|
| `pay-rule-set-presets.ts` | Add 18 new preset definitions |
| `PayRuleSetService.cs` | Add 18 names to LockedPresetNames |
| `OverenskomstFixtureHelper.cs` | Add 18 fixture methods |
| `ExpandedOverenskomstPayLineTests.cs` | Add tests for new OT patterns (30%/50%/100%, 40%/100%, 100% flat) |
| E2E test | Add 1 scenario for Golf preset creation |

## New OT Patterns to Test

| Pattern | Example | Tier Structure |
|---------|---------|---------------|
| Flat 100% | Golf | NORMAL → OVERTIME_100 |
| 30%/50%/100% (4 tiers) | Fjerkrae, Kartoffelmel | NORMAL → OT_30 → OT_50 → OT_100 |
| 40%/100% (3h first) | Grovvare | NORMAL → OT_40 → OT_100 |
| 30%/80% + 100% Sun | Ovrige | NORMAL → OT_30 → OT_80 (Sun: SUN_HOLIDAY) |
| 30%/80% + 80% Sun | Lucerne | NORMAL → OT_30 → OT_80 (Sun: SUN_HOLIDAY_80) |

## Verification

1. Total presets after: 19 existing + 18 new = 37
2. Backend unit tests cover all 5 new OT patterns
3. E2E test creates Golf preset via UI
4. CI passes
