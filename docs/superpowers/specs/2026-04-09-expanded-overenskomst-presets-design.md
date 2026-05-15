# Expanded Overenskomst Presets Design

## Context

The preset selector currently has 5 GLS-A/3F Jordbrug presets. Farming businesses in Denmark may be covered by different collective agreements depending on their employer association (GLS-A or KA) and their specific sector (agriculture, horticulture, forestry, etc.). Each agreement has different overtime percentages, time boundaries, and Saturday split points. We need to expand the preset list to cover the most common farming-related agreements.

## Current State

5 locked presets under "GLS-A / 3F" group:
- Jordbrug - Standard (30%/80% OT, Mon-Sat 06:00-18:00, Saturday split at 12:00)
- Jordbrug - Dyrehold (same + animal care time bands)
- Jordbrug - Elev u18 (50% OT, 8h/day cap)
- Jordbrug - Elev o18 (30%/80% weekday OT, 50%/80% Sun/Holiday)
- Jordbrug - Elev u18 Dyrehold

## New Presets

### Group: GLS-A / 3F (6 new presets)

#### Gartneri og Planteskole (Horticulture)
Source: Overenskomst #4011, 2024-2026

| Variant | Key | Normal hours | OT 1-2h | OT 3h+ | Sun/Holiday | Saturday | Shifted |
|---------|-----|-------------|---------|--------|-------------|----------|---------|
| Standard | `glsa-gartneri-standard` | 37h/week, Mon-Fri 06:00-18:00, Sat 06:00-12:30 | +50% | +100% | +50% 2h, +100% | Split at 12:30 (45000s) | Morning before 06:00, Evening after 18:00 |
| Elev u18 | `glsa-gartneri-elev-u18` | 8h/day, 40h/week | +50% | +100% | +50% 2h, +100% | 8h cap | Same under-18 restrictions |
| Elev o18 | `glsa-gartneri-elev-o18` | 37h/week | +50% | +100% | +50% 2h, +100% | Split at 12:30 | Same |

Pay codes: NORMAL, OVERTIME_50, OVERTIME_100, SAT_NORMAL, SAT_AFTERNOON, SUN_HOLIDAY, GRUNDLOVSDAG, SHIFTED_MORNING, SHIFTED_EVENING

#### Skovbrug (Forestry)
Source: Overenskomst #4013, 2024-2026

| Variant | Key | Normal hours | OT 1-2h | OT 3h+ | Sun/Holiday | Saturday |
|---------|-----|-------------|---------|--------|-------------|----------|
| Standard | `glsa-skovbrug-standard` | 37h/week, Mon-Sat 06:00-18:00 | +30% | +100% | +100% all | Split at 12:00 (43200s) |
| Elev u18 | `glsa-skovbrug-elev-u18` | 8h/day, 40h/week | +30% | +100% | +50% 2h, +100% | 8h cap |
| Elev o18 | `glsa-skovbrug-elev-o18` | 37h/week | +30% | +100% | +50% 2h, +100% | Split at 12:00 |

Pay codes: NORMAL, OVERTIME_30, OVERTIME_100, SAT_NORMAL, SAT_AFTERNOON, SUN_HOLIDAY, GRUNDLOVSDAG

### Group: KA / Krifa (8 new presets)

Source: Den Tvaerfaglige Overenskomst 2025-2028, Fagoverenskomst Landbrug + Det gronne omrade

#### Landbrug - Svine/Kvaegbrug (Pigs/Cattle + animal care)

| Variant | Key | Normal hours | OT 1-2h | OT 3h+ | Sun/Holiday |
|---------|-----|-------------|---------|--------|-------------|
| Standard | `ka-landbrug-svine-standard` | 37h/week, all weekdays 06:00-19:00, Sun/Holiday 06:00-18:00 | +50% | +100% | +100% all |
| Elev | `ka-landbrug-svine-elev` | 8h/day, 40h/week | +50% | +100% | +100% all |

Pay codes: NORMAL, OVERTIME_50, OVERTIME_100, SUN_HOLIDAY, SHIFTED_NIGHT (19:00-06:00)

Time bands (weekday): 06:00-19:00 NORMAL, 19:00-06:00 SHIFTED_NIGHT
Time bands (Sun/Holiday): 06:00-18:00 SUN_HOLIDAY, 18:00-06:00 SHIFTED_NIGHT

#### Landbrug - Plantebrug (Crops)

| Variant | Key | Normal hours | OT 1-3h | OT 4h+ | Sun/Holiday |
|---------|-----|-------------|---------|--------|-------------|
| Standard | `ka-landbrug-plante-standard` | 37h/week, all weekdays 06:00-19:00 | +50% (3h!) | +100% | +100% all |
| Elev | `ka-landbrug-plante-elev` | 8h/day, 40h/week | +50% (3h!) | +100% | +100% all |

Note: Plantebrug has +50% for first THREE hours (not 2), then +100%. This differs from Svine/Kvaeg.

Pay codes: Same as Svine/Kvaeg but WEEKDAY tier split at 3h OT instead of 2h.
- WEEKDAY: Tier1 NORMAL 7.4h (26640s), Tier2 OVERTIME_50 up to 10.4h (37440s = 26640+10800), Tier3 OVERTIME_100

#### Landbrug - Maskinstationer (Machine hire stations)

| Variant | Key | Normal hours | OT 1-2h | OT 3h+ | Sun/Holiday |
|---------|-----|-------------|---------|--------|-------------|
| Standard | `ka-landbrug-maskin-standard` | 37h/week, all weekdays 06:00-19:00 | +30% | +80% | +80% all |
| Elev | `ka-landbrug-maskin-elev` | 8h/day, 40h/week | +30% | +80% | +80% all |

Note: Maskinstationer use the PERSONAL LON (not mindsteloen) for OT calculation, and rates are +30%/+80% - identical to GLS-A Jordbrug!

Pay codes: NORMAL, OVERTIME_30, OVERTIME_80, SUN_HOLIDAY

#### Det gronne omrade (Green sector / Horticulture)

| Variant | Key | Normal hours | OT 1-3h | OT 4h+ | Sun/Holiday |
|---------|-----|-------------|---------|--------|-------------|
| Standard | `ka-gron-standard` | 37h/week, Mon-Fri 06:00-18:00 | +50% (3h!) | +100% | +100% all |
| Elev | `ka-gron-elev` | 8h/day, 40h/week | +50% (3h!) | +100% | +100% all |

Note: Same OT structure as Gartneri but under KA/Krifa umbrella.

Pay codes: NORMAL, OVERTIME_50, OVERTIME_100, SAT_NORMAL, SAT_AFTERNOON, SUN_HOLIDAY

Time bands (weekday): 06:00-18:00 NORMAL, 18:00-23:00 SHIFTED_EVENING, 23:00-06:00 SHIFTED_NIGHT
Time bands (Saturday): 06:00-16:00 SAT_NORMAL, 16:00-24:00 SAT_AFTERNOON
Time bands (Sun/Holiday): 00:00-24:00 SUN_HOLIDAY

## Dropdown Structure

```
-- Blank (custom rules) --

GLS-A / 3F
  Jordbrug - Standard              (existing)
  Jordbrug - Dyrehold              (existing)
  Jordbrug - Elev (under 18)       (existing)
  Jordbrug - Elev (over 18)        (existing)
  Jordbrug - Elev u18 Dyrehold     (existing)
  Gartneri - Standard              (NEW)
  Gartneri - Elev (under 18)       (NEW)
  Gartneri - Elev (over 18)        (NEW)
  Skovbrug - Standard              (NEW)
  Skovbrug - Elev (under 18)       (NEW)
  Skovbrug - Elev (over 18)        (NEW)

KA / Krifa
  Landbrug Svine/Kvaeg - Standard  (NEW)
  Landbrug Svine/Kvaeg - Elev      (NEW)
  Landbrug Plantebrug - Standard   (NEW)
  Landbrug Plantebrug - Elev       (NEW)
  Landbrug Maskinstation - Standard (NEW)
  Landbrug Maskinstation - Elev    (NEW)
  Gron - Standard                  (NEW)
  Gron - Elev                      (NEW)
```

## Implementation

### Files to modify
- `models/pay-rule-sets/pay-rule-set-presets.ts` - Add 14 new preset definitions
- `models/pay-rule-sets/index.ts` - No change (already exports)

### Backend test fixtures
- `eform-timeplanning-base/.../Tests/Helpers/GlsAFixtureHelper.cs` - Add fixture methods for new presets (for backend unit tests)
- `eform-timeplanning-base/.../Tests/GlsAJordbrugPayLineTests.cs` - Add test cases for new OT tier structures (50%/100% and 30%/100% patterns)

### E2E test
- Add scenario creating a KA/Krifa preset and verifying it appears in the grid

## Key Differences Summary

| Agreement | OT Tier 1 | OT Tier 2 | Sun/Holiday | Saturday split | Normal end |
|-----------|-----------|-----------|-------------|----------------|------------|
| GLS-A Jordbrug | 30% (2h) | 80% | 80% all | 12:00 | 18:00 |
| GLS-A Gartneri | 50% (2h) | 100% | 50% 2h + 100% | 12:30 | 18:00 |
| GLS-A Skovbrug | 30% (2h) | 100% | 100% all | 12:00 | 18:00 |
| KA Svine/Kvaeg | 50% (2h) | 100% | 100% all | N/A | 19:00 |
| KA Plantebrug | 50% (3h!) | 100% | 100% all | N/A | 19:00 |
| KA Maskinstation | 30% (2h) | 80% | 80% all | N/A | 19:00 |
| KA Gron | 50% (3h!) | 100% | 100% all | 12:00-ish | 18:00 |

## Verification

1. Open Pay Rule Sets, click Create
2. Verify dropdown shows both "GLS-A / 3F" and "KA / Krifa" groups
3. Select each new preset variant, verify read-only summary shows correct rules
4. Create each, verify singleton behavior
5. Backend unit tests verify PayLineGenerator splits hours correctly for each OT pattern
