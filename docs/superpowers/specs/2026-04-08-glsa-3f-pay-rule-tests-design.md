# GLS-A/3F Jordbrug Pay Rule Set Test Design

## Context

The timeplanning plugin needs tests that verify Pay Rule Sets correctly classify hours into pay codes according to the GLS-A/3F Jordbrugsoverenskomsten 2024-2026. These pay codes drive the Excel export used for payroll calculations. The system must handle:

- **Standard agriculture** workers (field work, crop production)
- **Animal care** workers (pasning af dyr - different night/weekend supplements)
- **Apprentices under 18** (strict legal hour caps: 8h/day, 40h/week)
- **Apprentices over 18** (different overtime tier: 50% first 2h vs adult 30%)
- **Transitions** from apprentice to standard/animal care mid-period

Classification only - no enforcement of hour caps. The Excel export must show correct pay code splits for a given period.

## Overenskomst Rules Summary

Source: Jordbrugsoverenskomsten 2024-2026 between GLS-A and 3F Den Gronne Gruppe.

### Standard Agriculture (SS 8-9, 22-23)

| Rule | Detail |
|------|--------|
| Normal hours | 37h/week, Mon-Sat 06:00-18:00 |
| Daily norm | 7.4h (37h / 5 days) |
| Overtime 1st+2nd hour | +30% of B-lon |
| Overtime 3rd+ hour | +80% of B-lon |
| Overtime Sun/Holiday | +80% of B-lon (all hours) |
| Shifted: before 06:00 | Up to 2h, supplement per hour |
| Shifted: after 18:00 | Up to 2h, supplement per hour |
| Saturday before 12:00 | Normal rate |
| Saturday after 12:00 | Supplement per day |
| Sunday/Holiday | Supplement per day |
| Grundlovsdag | Half day (separate from Holiday) |

### Animal Care - SS 15 (on top of standard)

| Rule | Detail |
|------|--------|
| Night Mon-Sat 00:00-05:00 | Supplement per hour |
| Saturday after 12:00 | Supplement per occurrence |
| Sunday/Holiday | Supplement per day |
| Working time | Can be placed all 7 days, hele dognet |
| Weekly norm | 37h/week or 296h/8 weeks |

### Apprentice Under-18 (Arbejdstilsynet + GLS-A)

| Rule | Detail |
|------|--------|
| Max daily | 8 hours |
| Max weekly | 40 hours |
| Night restriction | No work 20:00-06:00 (agriculture: start from 04:00 for stald) |
| Daily rest | 12 consecutive hours |
| Weekly rest | 2 consecutive days |
| Break | 30 min after 4.5h |
| Overtime Sun/Holiday 1st 2h | +50% |
| Overtime Sun/Holiday 3rd+ | +80% |

### Apprentice Over-18 (GLS-A)

| Rule | Detail |
|------|--------|
| Working time | Same as adult (37h/week) |
| Overtime weekday 1st+2nd h | Same as adult (+30%) |
| Overtime Sun/Holiday 1st 2h | +50% (NOT 30% like adults) |
| Overtime Sun/Holiday 3rd+ | +80% |

## Architecture

### Approach: A+C Hybrid

- **Backend (A):** Fixture-based Pay Rule Set configurations as C# helper methods, one per overenskomst variant. Unit tests feed PlanRegistration data into `PayLineGenerator` and assert correct PayCode/seconds output.
- **E2E (C):** Scenario-driven Playwright tests that create rule sets through UI, register work hours for realistic weeks/months, export Excel, and verify pay code columns.

### Existing Infrastructure Used

| Component | Location | Purpose |
|-----------|----------|---------|
| `PayRuleSet` entity | `eform-timeplanning-base/.../Entities/PayRuleSet.cs` | Top-level rule container |
| `PayDayRule` entity | `eform-timeplanning-base/.../Entities/PayDayRule.cs` | Rules per day code |
| `PayTierRule` entity | `eform-timeplanning-base/.../Entities/PayTierRule.cs` | Tiered hour allocation |
| `PayDayTypeRule` entity | `eform-timeplanning-base/.../Entities/PayDayTypeRule.cs` | Rules per day type enum |
| `PayTimeBandRule` entity | `eform-timeplanning-base/.../Entities/PayTimeBandRule.cs` | Time-of-day bands |
| `PayLineGenerator` | `eform-timeplanning-base/.../Helpers/PayLineGenerator.cs` | Core calculation engine |
| `WorkingTimeRuleSet` entity | `eform-timeplanning-base/.../Entities/WorkingTimeRuleSet.cs` | Normal hours/overtime config |
| `AssignedSiteRuleSetAssignments` | `eform-timeplanning-base/.../Entities/AssignedSiteRuleSetAssignments.cs` | Time-bound rule set assignment |
| `PlanRegistrationPayLine` | `eform-timeplanning-base/.../Entities/PlanRegistrationPayLine.cs` | Output pay line records |

## Pay Rule Set Fixtures

### Fixture 1: GlsA_Jordbrug_Standard

```
PayRuleSet "GLS-A Jordbrug Standard"
  WorkingTimeRuleSet:
    WeeklyNormalSeconds = 133200 (37h)
    DailyNormalSeconds  = 26640 (7.4h)
    WeekStartsOn        = Monday
    OvertimeBasis        = DailyThenWeekly

  PayDayRules:
    WEEKDAY:
      Tier 1: Order=1, UpToSeconds=26640 (7.4h), PayCode="NORMAL"
      Tier 2: Order=2, UpToSeconds=33840 (7.4h+2h), PayCode="OVERTIME_30"
      Tier 3: Order=3, UpToSeconds=null, PayCode="OVERTIME_80"

    SATURDAY:
      Tier 1: Order=1, UpToSeconds=null, PayCode="SAT_WORK"
      (Saturday time-of-day split handled via PayDayTypeRules below)

    SUNDAY:
      Tier 1: Order=1, UpToSeconds=null, PayCode="SUN_HOLIDAY"

    HOLIDAY:
      Tier 1: Order=1, UpToSeconds=null, PayCode="SUN_HOLIDAY"

    GRUNDLOVSDAG:
      Tier 1: Order=1, UpToSeconds=null, PayCode="GRUNDLOVSDAG"

  PayDayTypeRules (time-of-day supplements):
    WEEKDAY:
      TimeBand 14400-21600 (04:00-06:00): PayCode="SHIFTED_MORNING"
      TimeBand 21600-64800 (06:00-18:00): PayCode="NORMAL"
      TimeBand 64800-72000 (18:00-20:00): PayCode="SHIFTED_EVENING"

    SATURDAY:
      TimeBand 21600-43200 (06:00-12:00): PayCode="SAT_NORMAL"
      TimeBand 43200-64800 (12:00-18:00): PayCode="SAT_AFTERNOON"
```

### Fixture 2: GlsA_Jordbrug_DyrePasning

Extends Standard with animal care time bands:

```
PayRuleSet "GLS-A Jordbrug DyrePasning"
  WorkingTimeRuleSet:
    WeeklyNormalSeconds = 133200 (37h)
    DailyNormalSeconds  = null (flexible for animal care)
    WeekStartsOn        = Monday
    OvertimeBasis        = Weekly (296h/8 weeks model)

  PayDayRules: (same tier structure as Standard for overtime)
    WEEKDAY: same tiers as Standard
    SATURDAY: same tiers as Standard
    SUNDAY: same as Standard
    HOLIDAY: same as Standard
    GRUNDLOVSDAG: same as Standard

  PayDayTypeRules (animal care time bands):
    WEEKDAY:
      TimeBand 0-18000 (00:00-05:00): PayCode="ANIMAL_NIGHT"
      TimeBand 18000-21600 (05:00-06:00): PayCode="SHIFTED_MORNING"
      TimeBand 21600-64800 (06:00-18:00): PayCode="NORMAL"
      TimeBand 64800-86400 (18:00-24:00): PayCode="SHIFTED_EVENING"

    SATURDAY:
      TimeBand 0-43200 (00:00-12:00): PayCode="SAT_NORMAL"
      TimeBand 43200-86400 (12:00-24:00): PayCode="SAT_ANIMAL_AFTERNOON"

    SUNDAY:
      TimeBand 0-86400 (00:00-24:00): PayCode="ANIMAL_SUN_HOLIDAY"

    HOLIDAY:
      TimeBand 0-86400 (00:00-24:00): PayCode="ANIMAL_SUN_HOLIDAY"
```

### Fixture 3: GlsA_Jordbrug_Laerling_Under18

```
PayRuleSet "GLS-A Jordbrug Laerling U18"
  WorkingTimeRuleSet:
    WeeklyNormalSeconds = 144000 (40h)
    DailyNormalSeconds  = 28800 (8h)
    WeekStartsOn        = Monday
    OvertimeBasis        = Daily

  PayDayRules:
    WEEKDAY:
      Tier 1: Order=1, UpToSeconds=28800 (8h), PayCode="ELEV_NORMAL"
      Tier 2: Order=2, UpToSeconds=null, PayCode="ELEV_OVERTIME_50"

    SATURDAY:
      Tier 1: Order=1, UpToSeconds=28800 (8h), PayCode="ELEV_SAT_WORK"
      Tier 2: Order=2, UpToSeconds=null, PayCode="ELEV_SAT_OVERTIME_50"
      (Saturday before/after noon split via PayDayTypeRules if needed)

    SUNDAY:
      Tier 1: Order=1, UpToSeconds=7200 (2h), PayCode="ELEV_SUN_OT_50"
      Tier 2: Order=2, UpToSeconds=null, PayCode="ELEV_SUN_OT_80"

    HOLIDAY:
      Tier 1: Order=1, UpToSeconds=7200 (2h), PayCode="ELEV_HOL_OT_50"
      Tier 2: Order=2, UpToSeconds=null, PayCode="ELEV_HOL_OT_80"
```

### Fixture 4: GlsA_Jordbrug_Laerling_Over18

```
PayRuleSet "GLS-A Jordbrug Laerling O18"
  WorkingTimeRuleSet:
    WeeklyNormalSeconds = 133200 (37h)
    DailyNormalSeconds  = 26640 (7.4h)
    WeekStartsOn        = Monday
    OvertimeBasis        = DailyThenWeekly

  PayDayRules:
    WEEKDAY:
      Tier 1: Order=1, UpToSeconds=26640 (7.4h), PayCode="ELEV_NORMAL"
      Tier 2: Order=2, UpToSeconds=33840 (7.4h+2h), PayCode="ELEV_OVERTIME_30"
      Tier 3: Order=3, UpToSeconds=null, PayCode="ELEV_OVERTIME_80"

    SATURDAY:
      Tier 1: Order=1, UpToSeconds=null, PayCode="ELEV_SAT_WORK"
      (Saturday before/after noon split via PayDayTypeRules if needed)

    SUNDAY:
      Tier 1: Order=1, UpToSeconds=7200 (2h), PayCode="ELEV_SUN_OT_50"
      Tier 2: Order=2, UpToSeconds=null, PayCode="ELEV_SUN_OT_80"

    HOLIDAY:
      Tier 1: Order=1, UpToSeconds=7200 (2h), PayCode="ELEV_HOL_OT_50"
      Tier 2: Order=2, UpToSeconds=null, PayCode="ELEV_HOL_OT_80"
```

### Fixture 5: GlsA_Jordbrug_Laerling_Under18_DyrePasning

Combines Under-18 tiers with animal care time bands.

```
PayRuleSet "GLS-A Jordbrug Laerling U18 DyrePasning"
  WorkingTimeRuleSet:
    WeeklyNormalSeconds = 144000 (40h)
    DailyNormalSeconds  = 28800 (8h)
    WeekStartsOn        = Monday
    OvertimeBasis        = Daily

  PayDayRules: (same tier structure as Laerling_Under18)

  PayDayTypeRules: (same time bands as DyrePasning but with ELEV_ pay codes)
    WEEKDAY:
      TimeBand 14400-18000 (04:00-05:00): PayCode="ELEV_ANIMAL_NIGHT"
      TimeBand 18000-21600 (05:00-06:00): PayCode="ELEV_SHIFTED_MORNING"
      TimeBand 21600-64800 (06:00-18:00): PayCode="ELEV_NORMAL"
      TimeBand 64800-72000 (18:00-20:00): PayCode="ELEV_SHIFTED_EVENING"
    Note: Under-18 cannot work 20:00-04:00 (agriculture exception allows 04:00 start)

    SATURDAY:
      TimeBand 14400-43200 (04:00-12:00): PayCode="ELEV_SAT_NORMAL"
      TimeBand 43200-72000 (12:00-20:00): PayCode="ELEV_SAT_ANIMAL_AFTERNOON"
```

## Backend Tests (C# NUnit)

### File: `eform-timeplanning-base/Microting.TimePlanningBase.Tests/GlsAJordbrugPayLineTests.cs`

Shared fixture helper class provides `CreateGlsAStandard()`, `CreateGlsADyrePasning()`, etc.

### Standard Agriculture Tests

| Test Name | DayCode | TotalSeconds | Expected PayLines |
|-----------|---------|-------------|-------------------|
| `Standard_Weekday_Normal_7h24m` | WEEKDAY | 26640 | NORMAL:26640 |
| `Standard_Weekday_Overtime_2h` | WEEKDAY | 33840 | NORMAL:26640, OVERTIME_30:7200 |
| `Standard_Weekday_Overtime_4h` | WEEKDAY | 41040 | NORMAL:26640, OVERTIME_30:7200, OVERTIME_80:7200 |
| `Standard_Weekday_Short_4h` | WEEKDAY | 14400 | NORMAL:14400 |
| `Standard_Saturday_BeforeNoon_6h` | SATURDAY | 06:00-12:00 (21600s) | SAT_NORMAL:21600 |
| `Standard_Saturday_SpanNoon_10h` | SATURDAY | 06:00-16:00 (36000s) | SAT_NORMAL:21600, SAT_AFTERNOON:14400 |
| `Standard_Sunday_8h` | SUNDAY | 28800 | SUN_HOLIDAY:28800 |
| `Standard_Holiday_8h` | HOLIDAY | 28800 | SUN_HOLIDAY:28800 |
| `Standard_Grundlovsdag_4h` | GRUNDLOVSDAG | 14400 | GRUNDLOVSDAG:14400 |

### Animal Care Tests

| Test Name | DayCode | Scenario | Expected |
|-----------|---------|----------|----------|
| `Animal_Weekday_Night_5h` | WEEKDAY | 00:00-05:00 | ANIMAL_NIGHT:18000 |
| `Animal_Weekday_Night_And_Day` | WEEKDAY | 03:00-12:00 | ANIMAL_NIGHT:7200, SHIFTED_MORNING:3600, NORMAL:18000 |
| `Animal_Saturday_Afternoon` | SATURDAY | 08:00-18:00 | SAT_NORMAL:14400, SAT_ANIMAL_AFTERNOON:21600 |
| `Animal_Sunday_Full` | SUNDAY | 06:00-14:00 | ANIMAL_SUN_HOLIDAY:28800 |

### Apprentice Under-18 Tests

| Test Name | DayCode | TotalSeconds | Expected |
|-----------|---------|-------------|----------|
| `ElevU18_Weekday_Normal_8h` | WEEKDAY | 28800 | ELEV_NORMAL:28800 |
| `ElevU18_Weekday_Over_10h` | WEEKDAY | 36000 | ELEV_NORMAL:28800, ELEV_OVERTIME_50:7200 |
| `ElevU18_Sunday_2h` | SUNDAY | 7200 | ELEV_SUN_OT_50:7200 |
| `ElevU18_Sunday_4h` | SUNDAY | 14400 | ELEV_SUN_OT_50:7200, ELEV_SUN_OT_80:7200 |
| `ElevU18_Saturday_6h` | SATURDAY | 21600 | ELEV_SAT_NORMAL:21600 |

### Apprentice Over-18 Tests

| Test Name | DayCode | TotalSeconds | Expected |
|-----------|---------|-------------|----------|
| `ElevO18_Weekday_Normal` | WEEKDAY | 26640 | ELEV_NORMAL:26640 |
| `ElevO18_Weekday_OT_2h` | WEEKDAY | 33840 | ELEV_NORMAL:26640, ELEV_OVERTIME_30:7200 |
| `ElevO18_Sunday_2h` | SUNDAY | 7200 | ELEV_SUN_OT_50:7200 |
| `ElevO18_Sunday_5h` | SUNDAY | 18000 | ELEV_SUN_OT_50:7200, ELEV_SUN_OT_80:10800 |

### Transition Test

| Test Name | Scenario | Expected |
|-----------|----------|----------|
| `Transition_Laerling_To_Standard` | Worker has Laerling U18 rule set Jan-Mar, Standard from Apr 1. Register 8h weekday in March, 8h weekday in April. | March: ELEV_NORMAL:28800. April: NORMAL:26640, OVERTIME_30:1360 |

## E2E Playwright Tests

### File: `eform-client/playwright/e2e/plugins/time-planning-pn/c/time-planning-glsa-3f-pay-rules.spec.ts`

### Scenario 1: Standard Agriculture Full Week Export

**Setup:**
1. Navigate to Pay Rule Sets page
2. Create "GLS-A Jordbrug Standard" with WEEKDAY (3 tiers), SATURDAY (2 tiers), SUNDAY (1 tier), HOLIDAY (1 tier)
3. Assign rule set to test worker

**Test Data:**
- Monday: 7.4h normal (06:00-13:24)
- Tuesday: 9.4h with 2h overtime (06:00-15:24)
- Wednesday: 7.4h normal
- Thursday: 7.4h normal
- Friday: 7.4h normal
- Saturday: 4h before noon (08:00-12:00)

**Verification:**
1. Export Excel for the test week
2. Parse XLSX
3. Assert columns: NORMAL, OVERTIME_30, SAT_NORMAL have correct hour totals

### Scenario 2: Animal Care Weekend

**Setup:**
1. Create "GLS-A Jordbrug DyrePasning" with animal care time bands
2. Assign to test worker

**Test Data:**
- Wednesday night: 5h (00:00-05:00)
- Saturday: 10h (06:00-16:00, spanning noon split)
- Sunday: 8h (06:00-14:00)

**Verification:**
1. Export Excel
2. Assert: ANIMAL_NIGHT, SAT_NORMAL, SAT_ANIMAL_AFTERNOON, ANIMAL_SUN_HOLIDAY columns

### Scenario 3: Apprentice Transition

**Setup:**
1. Create "GLS-A Laerling U18" rule set
2. Create "GLS-A Jordbrug Standard" rule set
3. Assign Laerling U18 from 2026-01-01 to 2026-03-31
4. Assign Standard from 2026-04-01

**Test Data:**
- March 15 (within apprentice period): 8h weekday
- April 15 (within standard period): 8h weekday

**Verification:**
1. Export for January-March: ELEV_NORMAL hours present
2. Export for April onwards: NORMAL + OVERTIME_30 hours present (8h exceeds 7.4h norm)

## Verification Plan

### Backend Tests
```bash
cd eform-timeplanning-base
dotnet test --filter "FullyQualifiedName~GlsAJordbrugPayLineTests" -v normal
```

### E2E Tests
```bash
cd eform-angular-frontend/eform-client
npx playwright test time-planning-glsa-3f-pay-rules.spec.ts
```

### Manual Verification
1. Start the application in dev mode
2. Create a GLS-A/3F Standard rule set through the UI
3. Register a week of work hours with various day types
4. Export to Excel
5. Open Excel and verify pay code columns match expected splits
