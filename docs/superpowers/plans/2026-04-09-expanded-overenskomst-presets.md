# Expanded Overenskomst Presets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 14 new overenskomst presets (6 GLS-A + 8 KA/Krifa) covering Gartneri, Skovbrug, and KA Landbrug/Gron sectors, with complete C# unit tests and Playwright E2E tests.

**Architecture:** Frontend-only presets defined as TypeScript constants in `pay-rule-set-presets.ts`. Each preset maps to a `PayRuleSetPreset` object with `payDayRules` (tier-based) and `payDayTypeRules` (time-band-based). Backend C# fixture helpers mirror the presets for unit testing `PayLineGenerator`. E2E tests verify preset creation via the UI.

**Tech Stack:** Angular/TypeScript (presets), C#/NUnit (backend tests), Playwright (E2E tests)

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `eform-angular-frontend/.../models/pay-rule-sets/pay-rule-set-presets.ts` | EDIT | Add 14 new preset definitions + shared time band constants |
| `eform-timeplanning-base/.../Tests/Helpers/OverenskomstFixtureHelper.cs` | CREATE | C# fixture methods for all 14 new presets |
| `eform-timeplanning-base/.../Tests/ExpandedOverenskomstPayLineTests.cs` | CREATE | NUnit tests for all new OT tier patterns |
| `eform-angular-timeplanning-plugin/.../c/time-planning-glsa-3f-pay-rules.spec.ts` | EDIT | Add E2E scenarios for new presets |

---

### Task 1: Add GLS-A Gartneri + Skovbrug presets to TypeScript

**Files:**
- Edit: `eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn/models/pay-rule-sets/pay-rule-set-presets.ts`

**Context:** The file already has 5 presets and shared constants `WEEKDAY_TIME_BANDS_STANDARD`, `WEEKDAY_TIME_BANDS_DYREHOLD`, `WEEKDAYS`, and the `weekdayTypeRules()` helper. Add new shared constants and 6 new presets.

- [ ] **Step 1: Add shared time band constants for Gartneri**

Add after the existing `WEEKDAY_TIME_BANDS_DYREHOLD` constant:

```typescript
const WEEKDAY_TIME_BANDS_GARTNERI = [
  { startSecondOfDay: 14400, endSecondOfDay: 21600, payCode: 'SHIFTED_MORNING', priority: 1 },
  { startSecondOfDay: 21600, endSecondOfDay: 64800, payCode: 'NORMAL', priority: 1 },
  { startSecondOfDay: 64800, endSecondOfDay: 72000, payCode: 'SHIFTED_EVENING', priority: 1 },
];
// Gartneri Saturday split at 12:30 (45000s) instead of 12:00 (43200s)
```

- [ ] **Step 2: Add GLS-A Gartneri Standard preset**

Add to the `PAY_RULE_SET_PRESETS` array (after the last existing Jordbrug preset):

```typescript
{
  key: 'glsa-gartneri-standard',
  group: 'GLS-A / 3F',
  label: 'Gartneri - Standard',
  name: 'GLS-A / 3F - Gartneri Standard',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },       // 7.4h
        { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_50' },   // +2h
        { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
      ],
    },
    {
      dayCode: 'SATURDAY',
      payTierRules: [
        { order: 1, upToSeconds: 23400, payCode: 'SAT_NORMAL' },   // 6.5h (12:30 split)
        { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
      ],
    },
    {
      dayCode: 'SUNDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'HOLIDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'GRUNDLOVSDAG',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' },
      ],
    },
  ],
  payDayTypeRules: [
    ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_GARTNERI),
    {
      dayType: 'Saturday',
      defaultPayCode: 'SAT_NORMAL',
      priority: 1,
      timeBandRules: [
        { startSecondOfDay: 21600, endSecondOfDay: 45000, payCode: 'SAT_NORMAL', priority: 1 },
        { startSecondOfDay: 45000, endSecondOfDay: 64800, payCode: 'SAT_AFTERNOON', priority: 1 },
      ],
    },
  ],
},
```

- [ ] **Step 3: Add GLS-A Gartneri Elev u18 preset**

```typescript
{
  key: 'glsa-gartneri-elev-u18',
  group: 'GLS-A / 3F',
  label: 'Gartneri - Elev (under 18)',
  name: 'GLS-A / 3F - Gartneri Elev u18',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
      ],
    },
    {
      dayCode: 'SATURDAY',
      payTierRules: [
        { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_50' },
      ],
    },
    {
      dayCode: 'SUNDAY',
      payTierRules: [
        { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
      ],
    },
    {
      dayCode: 'HOLIDAY',
      payTierRules: [
        { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
      ],
    },
    {
      dayCode: 'GRUNDLOVSDAG',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' },
      ],
    },
  ],
  payDayTypeRules: [],
},
```

- [ ] **Step 4: Add GLS-A Gartneri Elev o18 preset**

Same as Standard but with ELEV_ pay codes:

```typescript
{
  key: 'glsa-gartneri-elev-o18',
  group: 'GLS-A / 3F',
  label: 'Gartneri - Elev (over 18)',
  name: 'GLS-A / 3F - Gartneri Elev o18',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 26640, payCode: 'ELEV_NORMAL' },
        { order: 2, upToSeconds: 33840, payCode: 'ELEV_OVERTIME_50' },
        { order: 3, upToSeconds: null, payCode: 'ELEV_OVERTIME_100' },
      ],
    },
    {
      dayCode: 'SATURDAY',
      payTierRules: [
        { order: 1, upToSeconds: 23400, payCode: 'ELEV_SAT_NORMAL' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_AFTERNOON' },
      ],
    },
    {
      dayCode: 'SUNDAY',
      payTierRules: [
        { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
      ],
    },
    {
      dayCode: 'HOLIDAY',
      payTierRules: [
        { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
      ],
    },
    {
      dayCode: 'GRUNDLOVSDAG',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' },
      ],
    },
  ],
  payDayTypeRules: [],
},
```

- [ ] **Step 5: Add GLS-A Skovbrug Standard preset**

Key difference from Jordbrug: OT 3h+ is +100% (not +80%), Sun/Holiday is +100% all hours.

```typescript
{
  key: 'glsa-skovbrug-standard',
  group: 'GLS-A / 3F',
  label: 'Skovbrug - Standard',
  name: 'GLS-A / 3F - Skovbrug Standard',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
        { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
        { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
      ],
    },
    {
      dayCode: 'SATURDAY',
      payTierRules: [
        { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
        { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
      ],
    },
    {
      dayCode: 'SUNDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'HOLIDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'GRUNDLOVSDAG',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' },
      ],
    },
  ],
  payDayTypeRules: [
    ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_STANDARD),
    {
      dayType: 'Saturday',
      defaultPayCode: 'SAT_NORMAL',
      priority: 1,
      timeBandRules: [
        { startSecondOfDay: 21600, endSecondOfDay: 43200, payCode: 'SAT_NORMAL', priority: 1 },
        { startSecondOfDay: 43200, endSecondOfDay: 64800, payCode: 'SAT_AFTERNOON', priority: 1 },
      ],
    },
  ],
},
```

- [ ] **Step 6: Add GLS-A Skovbrug Elev u18 + Elev o18 presets**

Follow the same pattern as Gartneri elev variants but with OVERTIME_30/OVERTIME_100 for weekday (matching Skovbrug's 30%/100% structure).

- [ ] **Step 7: Commit GLS-A presets**

```bash
cd eform-angular-frontend
# No commit here - dev mode. Changes will be synced via devgetchanges.sh
```

---

### Task 2: Add KA/Krifa presets to TypeScript

**Files:**
- Edit: `eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn/models/pay-rule-sets/pay-rule-set-presets.ts`

- [ ] **Step 1: Add KA time band constants**

```typescript
const WEEKDAY_TIME_BANDS_KA_LANDBRUG = [
  { startSecondOfDay: 21600, endSecondOfDay: 68400, payCode: 'NORMAL', priority: 1 },    // 06:00-19:00
  { startSecondOfDay: 68400, endSecondOfDay: 86400, payCode: 'SHIFTED_NIGHT', priority: 1 }, // 19:00-24:00
  { startSecondOfDay: 0, endSecondOfDay: 21600, payCode: 'SHIFTED_NIGHT', priority: 1 },     // 00:00-06:00
];

const WEEKDAY_TIME_BANDS_KA_GRON = [
  { startSecondOfDay: 21600, endSecondOfDay: 64800, payCode: 'NORMAL', priority: 1 },    // 06:00-18:00
  { startSecondOfDay: 64800, endSecondOfDay: 82800, payCode: 'SHIFTED_EVENING', priority: 1 }, // 18:00-23:00
  { startSecondOfDay: 82800, endSecondOfDay: 86400, payCode: 'SHIFTED_NIGHT', priority: 1 },   // 23:00-24:00
  { startSecondOfDay: 0, endSecondOfDay: 21600, payCode: 'SHIFTED_NIGHT', priority: 1 },       // 00:00-06:00
];
```

- [ ] **Step 2: Add KA Landbrug Svine/Kvaeg Standard + Elev**

Standard: 37h/week, weekdays 06:00-19:00, OT +50%/+100%, Sun/Holiday +100% all.

```typescript
{
  key: 'ka-landbrug-svine-standard',
  group: 'KA / Krifa',
  label: 'Landbrug Svine/Kvaeg - Standard',
  name: 'KA / Krifa - Landbrug Svine/Kvaeg Standard',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
        { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_50' },
        { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
      ],
    },
    {
      dayCode: 'SATURDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SAT_WORK' },
      ],
    },
    {
      dayCode: 'SUNDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'HOLIDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'GRUNDLOVSDAG',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' },
      ],
    },
  ],
  payDayTypeRules: [
    ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_LANDBRUG),
  ],
},
```

Elev: 8h/day cap, same OT rates.

```typescript
{
  key: 'ka-landbrug-svine-elev',
  group: 'KA / Krifa',
  label: 'Landbrug Svine/Kvaeg - Elev',
  name: 'KA / Krifa - Landbrug Svine/Kvaeg Elev',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 28800, payCode: 'ELEV_NORMAL' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_OVERTIME_50' },
      ],
    },
    {
      dayCode: 'SATURDAY',
      payTierRules: [
        { order: 1, upToSeconds: 28800, payCode: 'ELEV_SAT_NORMAL' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_SAT_OVERTIME_50' },
      ],
    },
    {
      dayCode: 'SUNDAY',
      payTierRules: [
        { order: 1, upToSeconds: 7200, payCode: 'ELEV_SUN_OT_50' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_SUN_OT_100' },
      ],
    },
    {
      dayCode: 'HOLIDAY',
      payTierRules: [
        { order: 1, upToSeconds: 7200, payCode: 'ELEV_HOL_OT_50' },
        { order: 2, upToSeconds: null, payCode: 'ELEV_HOL_OT_100' },
      ],
    },
    {
      dayCode: 'GRUNDLOVSDAG',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' },
      ],
    },
  ],
  payDayTypeRules: [],
},
```

- [ ] **Step 3: Add KA Landbrug Plantebrug Standard + Elev**

Key difference: OT +50% for first THREE hours (10800s), not 2h. So tier2 UpToSeconds = 26640 + 10800 = 37440.

```typescript
{
  key: 'ka-landbrug-plante-standard',
  group: 'KA / Krifa',
  label: 'Landbrug Plantebrug - Standard',
  name: 'KA / Krifa - Landbrug Plantebrug Standard',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
        { order: 2, upToSeconds: 37440, payCode: 'OVERTIME_50' },   // 7.4h + 3h = 10.4h
        { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
      ],
    },
    // SATURDAY, SUNDAY, HOLIDAY, GRUNDLOVSDAG same as Svine/Kvaeg
    ...  // (full definitions in implementation)
  ],
  payDayTypeRules: [
    ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_LANDBRUG),
  ],
},
```

- [ ] **Step 4: Add KA Landbrug Maskinstation Standard + Elev**

OT +30%/+80% - identical rates to GLS-A Jordbrug but under KA umbrella.

```typescript
{
  key: 'ka-landbrug-maskin-standard',
  group: 'KA / Krifa',
  label: 'Landbrug Maskinstation - Standard',
  name: 'KA / Krifa - Landbrug Maskinstation Standard',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
        { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
        { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
      ],
    },
    // ... same SATURDAY/SUNDAY/HOLIDAY/GRUNDLOVSDAG as Svine
  ],
  payDayTypeRules: [
    ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_LANDBRUG),
  ],
},
```

- [ ] **Step 5: Add KA Gron Standard + Elev**

OT +50% for 3h, +100% thereafter. Time bands: 06:00-18:00 NORMAL, 18:00-23:00 SHIFTED_EVENING, 23:00-06:00 SHIFTED_NIGHT.

```typescript
{
  key: 'ka-gron-standard',
  group: 'KA / Krifa',
  label: 'Gron - Standard',
  name: 'KA / Krifa - Gron Standard',
  locked: true,
  payDayRules: [
    {
      dayCode: 'WEEKDAY',
      payTierRules: [
        { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
        { order: 2, upToSeconds: 37440, payCode: 'OVERTIME_50' },   // 3h OT window
        { order: 3, upToSeconds: null, payCode: 'OVERTIME_100' },
      ],
    },
    {
      dayCode: 'SATURDAY',
      payTierRules: [
        { order: 1, upToSeconds: 21600, payCode: 'SAT_NORMAL' },
        { order: 2, upToSeconds: null, payCode: 'SAT_AFTERNOON' },
      ],
    },
    {
      dayCode: 'SUNDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'HOLIDAY',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'SUN_HOLIDAY' },
      ],
    },
    {
      dayCode: 'GRUNDLOVSDAG',
      payTierRules: [
        { order: 1, upToSeconds: null, payCode: 'GRUNDLOVSDAG' },
      ],
    },
  ],
  payDayTypeRules: [
    ...weekdayTypeRules('NORMAL', WEEKDAY_TIME_BANDS_KA_GRON),
    {
      dayType: 'Saturday',
      defaultPayCode: 'SAT_NORMAL',
      priority: 1,
      timeBandRules: [
        { startSecondOfDay: 21600, endSecondOfDay: 57600, payCode: 'SAT_NORMAL', priority: 1 },
        { startSecondOfDay: 57600, endSecondOfDay: 86400, payCode: 'SAT_AFTERNOON', priority: 1 },
      ],
    },
    {
      dayType: 'Sunday',
      defaultPayCode: 'SUN_HOLIDAY',
      priority: 1,
      timeBandRules: [
        { startSecondOfDay: 0, endSecondOfDay: 86400, payCode: 'SUN_HOLIDAY', priority: 1 },
      ],
    },
    {
      dayType: 'Holiday',
      defaultPayCode: 'SUN_HOLIDAY',
      priority: 1,
      timeBandRules: [
        { startSecondOfDay: 0, endSecondOfDay: 86400, payCode: 'SUN_HOLIDAY', priority: 1 },
      ],
    },
  ],
},
```

- [ ] **Step 6: Commit KA/Krifa presets**

No git commit (dev mode). Changes synced via devgetchanges.sh later.

---

### Task 3: Create C# fixture helper for new presets

**Files:**
- Create: `eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/OverenskomstFixtureHelper.cs`

**Context:** Follow the exact same pattern as `GlsAFixtureHelper.cs` - static methods returning in-memory `PayRuleSet` objects. Use IDs starting at 200 to avoid conflicts with existing fixtures (100-104).

- [ ] **Step 1: Create the file with MIT license header and all fixture methods**

The file needs these static methods (14 total, matching the 14 new presets):

```csharp
// GLS-A Gartneri (50%/100% OT, Saturday split at 23400s)
public static PayRuleSet GlsA_Gartneri_Standard()      // Id=200
public static PayRuleSet GlsA_Gartneri_Elev_Under18()   // Id=201
public static PayRuleSet GlsA_Gartneri_Elev_Over18()    // Id=202

// GLS-A Skovbrug (30%/100% OT)
public static PayRuleSet GlsA_Skovbrug_Standard()       // Id=203
public static PayRuleSet GlsA_Skovbrug_Elev_Under18()   // Id=204
public static PayRuleSet GlsA_Skovbrug_Elev_Over18()    // Id=205

// KA Landbrug Svine/Kvaeg (50%/100% OT)
public static PayRuleSet KA_Landbrug_Svine_Standard()    // Id=206
public static PayRuleSet KA_Landbrug_Svine_Elev()        // Id=207

// KA Landbrug Plantebrug (50%/100% OT, 3h first tier!)
public static PayRuleSet KA_Landbrug_Plante_Standard()   // Id=208
public static PayRuleSet KA_Landbrug_Plante_Elev()       // Id=209

// KA Landbrug Maskinstation (30%/80% OT)
public static PayRuleSet KA_Landbrug_Maskin_Standard()   // Id=210
public static PayRuleSet KA_Landbrug_Maskin_Elev()       // Id=211

// KA Gron (50%/100% OT, 3h first tier)
public static PayRuleSet KA_Gron_Standard()              // Id=212
public static PayRuleSet KA_Gron_Elev()                  // Id=213
```

Key tier configurations per fixture (all use 7.4h=26640s normal unless elev):

| Fixture | Weekday Tiers | Sun/Holiday |
|---------|--------------|-------------|
| Gartneri Standard | NORMAL@26640, OVERTIME_50@33840, OVERTIME_100@null | SUN_HOLIDAY flat |
| Gartneri Elev u18 | ELEV_NORMAL@28800, ELEV_OVERTIME_50@null | ELEV_SUN_OT_50@7200, ELEV_SUN_OT_100@null |
| Skovbrug Standard | NORMAL@26640, OVERTIME_30@33840, OVERTIME_100@null | SUN_HOLIDAY flat |
| KA Svine Standard | NORMAL@26640, OVERTIME_50@33840, OVERTIME_100@null | SUN_HOLIDAY flat |
| KA Plante Standard | NORMAL@26640, OVERTIME_50@**37440**, OVERTIME_100@null | SUN_HOLIDAY flat |
| KA Maskin Standard | NORMAL@26640, OVERTIME_30@33840, OVERTIME_80@null | SUN_HOLIDAY flat |
| KA Gron Standard | NORMAL@26640, OVERTIME_50@**37440**, OVERTIME_100@null | SUN_HOLIDAY flat |

Note the `37440` for Plantebrug/Gron: 26640 + 10800 (3h) = 37440.

- [ ] **Step 2: Run existing tests to verify no regressions**

```bash
cd eform-timeplanning-base
dotnet test --filter "FullyQualifiedName~GlsAJordbrugPayLineTests" -v normal
```

Expected: All 35 existing tests pass.

- [ ] **Step 3: Commit fixture helper**

```bash
git add Microting.TimePlanningBase.Tests/Helpers/OverenskomstFixtureHelper.cs
git commit -m "feat: add C# fixture helpers for 14 new overenskomst presets"
```

---

### Task 4: Create C# unit tests for new presets

**Files:**
- Create: `eform-timeplanning-base/Microting.TimePlanningBase.Tests/ExpandedOverenskomstPayLineTests.cs`

**Context:** Follow the exact test pattern from `GlsAJordbrugPayLineTests.cs` - pure in-memory tests using `PayLineGenerator.GeneratePayLines()`.

- [ ] **Step 1: Create test file with standard structure**

Each new OT pattern needs tests for: normal hours, OT tier 1, OT tier 2 (overflow), Sunday/Holiday, and Grundlovsdag. That's 5 tests per Standard variant.

Test cases for the distinct OT patterns:

**Pattern A: 50%/100% with 2h OT window (Gartneri, KA Svine)**
```
Gartneri_Standard_Weekday_Normal: WEEKDAY, 26640s -> NORMAL:26640
Gartneri_Standard_Weekday_OT_2h: WEEKDAY, 33840s -> NORMAL:26640, OVERTIME_50:7200
Gartneri_Standard_Weekday_OT_4h: WEEKDAY, 41040s -> NORMAL:26640, OVERTIME_50:7200, OVERTIME_100:7200
Gartneri_Standard_Sunday_8h: SUNDAY, 28800s -> SUN_HOLIDAY:28800
Gartneri_Standard_Saturday_SpanNoon: SATURDAY, 28000s -> SAT_NORMAL:23400, SAT_AFTERNOON:4600
```

**Pattern B: 30%/100% with 2h OT window (Skovbrug)**
```
Skovbrug_Standard_Weekday_OT_4h: WEEKDAY, 41040s -> NORMAL:26640, OVERTIME_30:7200, OVERTIME_100:7200
Skovbrug_Standard_Sunday_8h: SUNDAY, 28800s -> SUN_HOLIDAY:28800
```

**Pattern C: 50%/100% with 3h OT window (KA Plantebrug, KA Gron)**
```
KA_Plante_Standard_Weekday_Normal: WEEKDAY, 26640s -> NORMAL:26640
KA_Plante_Standard_Weekday_OT_3h: WEEKDAY, 37440s -> NORMAL:26640, OVERTIME_50:10800
KA_Plante_Standard_Weekday_OT_5h: WEEKDAY, 44640s -> NORMAL:26640, OVERTIME_50:10800, OVERTIME_100:7200
KA_Plante_Standard_Sunday_8h: SUNDAY, 28800s -> SUN_HOLIDAY:28800
```

**Pattern D: 30%/80% (KA Maskinstation - same as GLS-A Jordbrug)**
```
KA_Maskin_Standard_Weekday_OT_4h: WEEKDAY, 41040s -> NORMAL:26640, OVERTIME_30:7200, OVERTIME_80:7200
```

**Elev patterns (one per variant to verify):**
```
Gartneri_ElevU18_Weekday_Over_10h: WEEKDAY, 36000s -> ELEV_NORMAL:28800, ELEV_OVERTIME_50:7200
Gartneri_ElevU18_Sunday_4h: SUNDAY, 14400s -> ELEV_SUN_OT_50:7200, ELEV_SUN_OT_100:7200
KA_Svine_Elev_Weekday_Over_10h: same pattern
KA_Plante_Elev_Weekday_Over_10h: WEEKDAY, 36000s -> ELEV_NORMAL:28800, ELEV_OVERTIME_50:7200
KA_Maskin_Elev_Weekday_Over_10h: WEEKDAY, 36000s -> ELEV_NORMAL:28800, ELEV_OVERTIME_30:7200 (wait - Maskin elev uses 30%!)
```

Total: ~25 new test cases covering all 7 distinct OT patterns.

- [ ] **Step 2: Run all tests**

```bash
cd eform-timeplanning-base
dotnet test --filter "FullyQualifiedName~ExpandedOverenskomstPayLineTests" -v normal
```

Expected: All ~25 tests pass.

- [ ] **Step 3: Run full test suite to check no regressions**

```bash
dotnet test -v normal
```

- [ ] **Step 4: Commit tests**

```bash
git add Microting.TimePlanningBase.Tests/ExpandedOverenskomstPayLineTests.cs
git commit -m "feat: add unit tests for 14 expanded overenskomst presets"
```

---

### Task 5: Add E2E Playwright tests for new presets

**Files:**
- Edit: `eform-angular-timeplanning-plugin/eform-client/playwright/e2e/plugins/time-planning-pn/c/time-planning-glsa-3f-pay-rules.spec.ts`

**Context:** The existing test has 2 scenarios. Add a 3rd scenario that creates a KA/Krifa preset to verify the new group appears in the dropdown.

- [ ] **Step 1: Add Scenario 3 to the test file**

Add after Scenario 2 in the test describe block:

```typescript
test('Scenario 3: KA/Krifa preset - create Landbrug Svine/Kvaeg Standard and verify in grid', async ({ page }) => {
  // Navigate to Pay Rule Sets via direct URL
  await navigateToPayRuleSets(page);
  await openCreatePayRuleSetModal(page);

  // Select the KA/Krifa preset
  await selectPreset(page, 'Landbrug Svine/Kvaeg - Standard');

  // Verify the locked preset view shows KA/Krifa group
  const dialog = page.locator('mat-dialog-container');
  await expect(dialog.locator('.lock-banner')).toBeVisible({ timeout: 5000 });
  await expect(dialog.locator('.preset-name')).toContainText('KA / Krifa - Landbrug Svine/Kvaeg Standard');

  // Verify the read-only rules summary
  await expect(dialog.locator('.rules-summary').first()).toBeVisible({ timeout: 5000 });

  // Click Create
  await submitCreatePayRuleSet(page);

  // Verify it appears in the grid
  const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
  await grid.waitFor({ state: 'visible', timeout: 10000 });
  await expect(grid.getByText('KA / Krifa - Landbrug Svine/Kvaeg Standard')).toBeVisible({ timeout: 10000 });
});

test('Scenario 4: GLS-A Gartneri preset - create and verify different Saturday split', async ({ page }) => {
  await navigateToPayRuleSets(page);
  await openCreatePayRuleSetModal(page);

  // Select Gartneri
  await selectPreset(page, 'Gartneri - Standard');

  const dialog = page.locator('mat-dialog-container');
  await expect(dialog.locator('.lock-banner')).toBeVisible({ timeout: 5000 });
  await expect(dialog.locator('.preset-name')).toContainText('GLS-A / 3F - Gartneri Standard');

  await submitCreatePayRuleSet(page);

  const grid = page.locator('#time-planning-pn-pay-rule-sets-grid');
  await expect(grid.getByText('GLS-A / 3F - Gartneri Standard')).toBeVisible({ timeout: 10000 });
});
```

- [ ] **Step 2: Commit E2E tests**

```bash
cd eform-angular-timeplanning-plugin
git add eform-client/playwright/e2e/plugins/time-planning-pn/c/time-planning-glsa-3f-pay-rules.spec.ts
git commit -m "feat: add E2E tests for KA/Krifa and Gartneri preset creation"
```

---

### Task 6: Sync, push, and verify CI

- [ ] **Step 1: Sync frontend changes from host app to plugin repo**

```bash
cd eform-angular-timeplanning-plugin
bash devgetchanges.sh
git checkout -- '*.csproj' '*.conf.ts' '*.xlsx' '*.docx'
```

- [ ] **Step 2: Verify intended changes only**

```bash
git status --short
```

Expected: Only `pay-rule-set-presets.ts` modified (plus any E2E test changes).

- [ ] **Step 3: Commit synced frontend changes**

```bash
git add eform-client/src/app/plugins/modules/time-planning-pn/models/pay-rule-sets/pay-rule-set-presets.ts
git commit -m "feat: add 14 new overenskomst presets (GLS-A Gartneri/Skovbrug + KA/Krifa Landbrug/Gron)"
```

- [ ] **Step 4: Push base repo**

```bash
cd eform-timeplanning-base
git push origin master
```

- [ ] **Step 5: Push plugin repo**

```bash
cd eform-angular-timeplanning-plugin
git push origin stable
```

- [ ] **Step 6: Monitor CI**

```bash
gh run list --limit 1 --json databaseId,status
gh run watch <run-id> --exit-status
```

Expected: All 18 jobs pass (angular-unit-test, build, test-dotnet, all playwright suites).
