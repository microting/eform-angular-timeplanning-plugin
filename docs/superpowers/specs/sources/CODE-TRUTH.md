# Code ground truth (extracted 2026-08-08, plugin stable @ 9fed1a55, base master @ 5cbe0af)

This document is a byte-level transcription of the ACTUAL encoded state of all 31 GLS-A
pay-rule-set presets, read directly from code (never from docs), plus the engine facts
that determine which of those encoded rules actually execute at runtime. It is the
ground-truth reference that later audit phases (W4/W5/W6) verify documentation claims
against.

Sources:
- Catalogue (TS): `eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn/models/pay-rule-sets/pay-rule-set-presets.ts`, read from this repo's working tree (branch `docs/glsa-findings-verification`, even with `stable` for code files) at commit `9fed1a55`.
- C# fixtures: `eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/OverenskomstFixtureHelper.cs` (Gartneri, Skovbrug, Golf, KA Landbrug, KA Gron, and the two Jordbrug Praktikant presets) and `eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/GlsAFixtureHelper.cs` (the 5 Jordbrug Standard/Dyrehold/Elev presets), both branch `master` at commit `5cbe0af`.
- Engine: `eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/TimePlanningWorkingHoursService.cs`, `.../Infrastructure/Helpers/PayRuleSetLock.cs` (this repo), and `eform-timeplanning-base/Microting.TimePlanningBase/Infrastructure/Helpers/PayLineGenerator.cs` (base repo — note: the real path is `Infrastructure/Helpers/PayLineGenerator.cs`, not `Infrastructure/Data/PayLineGenerator*.cs` as originally guessed).

The 31 `glsa-*` preset keys in the catalogue, grouped by family as dispatched:

1. Jordbrug Standard + Dyrehold (2): `glsa-jordbrug-standard`, `glsa-jordbrug-dyrehold`
2. Jordbrug Elev ×3 (3): `glsa-jordbrug-elev-u18`, `glsa-jordbrug-elev-o18`, `glsa-jordbrug-elev-u18-dyrehold`
3. Gartneri ×3 + Skovbrug ×3 (6): `glsa-gartneri-standard`, `glsa-gartneri-elev-u18`, `glsa-gartneri-elev-o18`, `glsa-skovbrug-standard`, `glsa-skovbrug-elev-u18`, `glsa-skovbrug-elev-o18`
4. Golf ×2 + praktikant ×2 (4): `glsa-golf-standard`, `glsa-golf-elev`, `glsa-jordbrug-praktikant-udl-andet`, `glsa-jordbrug-praktikant-udl-staldarbejde`
5. Agroindustri ×16 (16): `glsa-agro-fjerkrae-standard`, `glsa-agro-fjerkrae-elev`, `glsa-agro-grovvare-standard`, `glsa-agro-grovvare-elev`, `glsa-agro-gulerod-standard`, `glsa-agro-gulerod-elev`, `glsa-agro-kartoffelmel-standard`, `glsa-agro-kartoffelmel-elev`, `glsa-agro-kartoffelsorter-standard`, `glsa-agro-kartoffelsorter-elev`, `glsa-agro-lucerne-standard`, `glsa-agro-lucerne-elev`, `glsa-agro-minkfoder-standard`, `glsa-agro-minkfoder-elev`, `glsa-agro-ovrige-standard`, `glsa-agro-ovrige-elev`

Total: 2 + 3 + 6 + 4 + 16 = 31 presets. (The catalogue file also contains 8 non-GLS-A
`ka-*` presets — `ka-landbrug-svine-*`, `ka-landbrug-plante-*`, `ka-landbrug-maskin-*`,
`ka-gron-*` — which are out of scope for this audit and not covered below.)

**Correction (superseding an earlier draft of this document):** the five presets —
`glsa-jordbrug-standard`, `glsa-jordbrug-dyrehold`, `glsa-jordbrug-elev-u18`,
`glsa-jordbrug-elev-o18`, `glsa-jordbrug-elev-u18-dyrehold` — are **not** absent from
the C# fixtures. An earlier pass searched only `OverenskomstFixtureHelper.cs` (which
indeed has no Jordbrug Standard/Dyrehold/Elev factories) and stopped there. Their
fixtures actually live in a separate file in the same directory,
`eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/GlsAFixtureHelper.cs`
(branch `master`): `GlsA_Jordbrug_Standard` (Id 100), `GlsA_Jordbrug_DyrePasning` (Id
101), `GlsA_Jordbrug_Laerling_Under18` (Id 102), `GlsA_Jordbrug_Laerling_Over18` (Id
103), `GlsA_Jordbrug_Laerling_Under18_DyrePasning` (Id 104). Byte-level comparison
(below, in each preset's divergence section) shows all 5 match the catalogue exactly
in tiers/seconds/payCodes/bands — the only divergence is the `Name` year suffix
(`2024-2026` in the fixture vs `2026-2029` in the catalogue), the same pattern seen
across the other 26 presets in this document.

---

## Jordbrug Standard + Dyrehold

Sources read (code only):
- `eform-angular-timeplanning-plugin/eform-client/.../pay-rule-set-presets.ts` (working tree, lines 61-176)
- `eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/GlsAFixtureHelper.cs` (branch `master`)

### Preset: `glsa-jordbrug-standard`

Catalogue metadata: `group: 'GLS-A / 3F'`, `label: 'Jordbrug - Standard'`, `name: 'GLS-A / 3F - Jordbrug Standard 2026-2029'`, `locked: true`.

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; 3: null → `OVERTIME_80` | Monday, Tuesday, Wednesday, Thursday, Friday (each identical, defaultPayCode `NORMAL`, priority 1): 14400-21600 `SHIFTED_MORNING` (p1); 21600-64800 `NORMAL` (p1); 64800-72000 `SHIFTED_EVENING` (p1) |
| SATURDAY | 1: 21600 → `SAT_NORMAL`; 2: null → `SAT_AFTERNOON` | Saturday (defaultPayCode `SAT_NORMAL`, priority 1): 21600-43200 `SAT_NORMAL` (p1); 43200-64800 `SAT_AFTERNOON` (p1) |
| SUNDAY | 1: null → `SUN_HOLIDAY` | none defined |
| HOLIDAY | 1: null → `SUN_HOLIDAY` | none defined |
| GRUNDLOVSDAG | 1: null → `GRUNDLOVSDAG` | none defined |

### Preset: `glsa-jordbrug-dyrehold`

Catalogue metadata: `group: 'GLS-A / 3F'`, `label: 'Jordbrug - Dyrehold'`, `name: 'GLS-A / 3F - Jordbrug Dyrehold 2026-2029'`, `locked: true`.

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; 3: null → `OVERTIME_80` | Monday, Tuesday, Wednesday, Thursday, Friday (each identical, defaultPayCode `NORMAL`, priority 1): 0-18000 `ANIMAL_NIGHT` (p1); 18000-21600 `SHIFTED_MORNING` (p1); 21600-64800 `NORMAL` (p1); 64800-86400 `SHIFTED_EVENING` (p1) |
| SATURDAY | 1: 21600 → `SAT_NORMAL`; 2: null → `SAT_ANIMAL_AFTERNOON` | Saturday (defaultPayCode `SAT_NORMAL`, priority 1): 0-43200 `SAT_NORMAL` (p1); 43200-86400 `SAT_ANIMAL_AFTERNOON` (p1) |
| SUNDAY | 1: null → `ANIMAL_SUN_HOLIDAY` | Sunday (defaultPayCode `ANIMAL_SUN_HOLIDAY`, priority 1): 0-86400 `ANIMAL_SUN_HOLIDAY` (p1) |
| HOLIDAY | 1: null → `ANIMAL_SUN_HOLIDAY` | Holiday (defaultPayCode `ANIMAL_SUN_HOLIDAY`, priority 1): 0-86400 `ANIMAL_SUN_HOLIDAY` (p1) |
| GRUNDLOVSDAG | 1: null → `GRUNDLOVSDAG` | none defined |

### Catalogue vs C# fixture divergence

**`glsa-jordbrug-standard`** (fixture: `GlsAFixtureHelper.GlsA_Jordbrug_Standard`, Id=100)
- Name string differs: catalogue `GLS-A / 3F - Jordbrug Standard 2026-2029` vs fixture `GLS-A / 3F - Jordbrug Standard 2024-2026` (year range) — same pattern as every other preset in this document.
- WEEKDAY tiers match byte-for-byte: 26640→`NORMAL`, 33840→`OVERTIME_30`, null→`OVERTIME_80`.
- WEEKDAY dayTypeRules match byte-for-byte: Monday-Friday, `DefaultPayCode NORMAL`, priority 1, bands 14400-21600 `SHIFTED_MORNING` / 21600-64800 `NORMAL` / 64800-72000 `SHIFTED_EVENING` (fixture builds these via the shared `StandardWeekdayTimeBands()` helper, same values).
- SATURDAY tiers match byte-for-byte: 21600→`SAT_NORMAL`, null→`SAT_AFTERNOON`. SATURDAY dayTypeRule matches byte-for-byte: `DefaultPayCode SAT_NORMAL`, priority 1, bands 21600-43200 `SAT_NORMAL` / 43200-64800 `SAT_AFTERNOON`.
- SUNDAY, HOLIDAY, GRUNDLOVSDAG tiers match byte-for-byte (`SUN_HOLIDAY` / `SUN_HOLIDAY` / `GRUNDLOVSDAG`); no dayTypeRules defined for any of the three in either source — match.
- **No other divergence found.**

**`glsa-jordbrug-dyrehold`** (fixture: `GlsAFixtureHelper.GlsA_Jordbrug_DyrePasning`, Id=101)
- Name string differs: catalogue `GLS-A / 3F - Jordbrug Dyrehold 2026-2029` vs fixture `GLS-A / 3F - Jordbrug Dyrehold 2024-2026` (year range).
- WEEKDAY tiers match byte-for-byte: 26640→`NORMAL`, 33840→`OVERTIME_30`, null→`OVERTIME_80`.
- WEEKDAY dayTypeRules match byte-for-byte: Monday-Friday, `DefaultPayCode NORMAL`, priority 1, bands 0-18000 `ANIMAL_NIGHT` / 18000-21600 `SHIFTED_MORNING` / 21600-64800 `NORMAL` / 64800-86400 `SHIFTED_EVENING` (fixture's `AnimalWeekdayTimeBands()` helper — same values).
- SATURDAY tiers match byte-for-byte: 21600→`SAT_NORMAL`, null→`SAT_ANIMAL_AFTERNOON`. SATURDAY dayTypeRule matches byte-for-byte: `DefaultPayCode SAT_NORMAL`, priority 1, bands 0-43200 `SAT_NORMAL` / 43200-86400 `SAT_ANIMAL_AFTERNOON`.
- SUNDAY tiers match byte-for-byte: null→`ANIMAL_SUN_HOLIDAY`. SUNDAY dayTypeRule matches byte-for-byte: `DefaultPayCode ANIMAL_SUN_HOLIDAY`, priority 1, band 0-86400 `ANIMAL_SUN_HOLIDAY`.
- HOLIDAY tiers match byte-for-byte: null→`ANIMAL_SUN_HOLIDAY`. HOLIDAY dayTypeRule matches byte-for-byte: `DefaultPayCode ANIMAL_SUN_HOLIDAY`, priority 1, band 0-86400 `ANIMAL_SUN_HOLIDAY`.
- GRUNDLOVSDAG tiers match byte-for-byte: null→`GRUNDLOVSDAG`; no dayTypeRule defined for GRUNDLOVSDAG in either source — match.
- **No other divergence found.**

---

## Jordbrug Elev x3

Source A (catalogue): `eform-angular-timeplanning-plugin/eform-client/.../pay-rule-set-presets.ts`
Source B (C# fixtures): `eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/GlsAFixtureHelper.cs`

### Preset 1: `glsa-jordbrug-elev-u18` (Jordbrug - Elev under 18)

Catalogue `name`: `GLS-A / 3F - Jordbrug Elev u18 2026-2029`

| DayCode | tier order/upToSeconds/payCode list | dayTypeRules bands (dayType, start-end seconds, payCode) |
|---|---|---|
| WEEKDAY | 1: upToSeconds=28800, payCode=ELEV_NORMAL<br>2: upToSeconds=null, payCode=ELEV_OVERTIME_50 | (none — `payDayTypeRules: []`) |
| SATURDAY | 1: upToSeconds=28800, payCode=ELEV_SAT_NORMAL<br>2: upToSeconds=null, payCode=ELEV_SAT_OVERTIME_50 | (none) |
| SUNDAY | 1: upToSeconds=7200, payCode=ELEV_SUN_OT_50<br>2: upToSeconds=null, payCode=ELEV_SUN_OT_80 | (none) |
| HOLIDAY | 1: upToSeconds=7200, payCode=ELEV_HOL_OT_50<br>2: upToSeconds=null, payCode=ELEV_HOL_OT_80 | (none) |
| GRUNDLOVSDAG | 1: upToSeconds=null, payCode=GRUNDLOVSDAG | (none) |

### Preset 2: `glsa-jordbrug-elev-o18` (Jordbrug - Elev over 18)

Catalogue `name`: `GLS-A / 3F - Jordbrug Elev o18 2026-2029`

| DayCode | tier order/upToSeconds/payCode list | dayTypeRules bands (dayType, start-end seconds, payCode) |
|---|---|---|
| WEEKDAY | 1: upToSeconds=26640, payCode=ELEV_NORMAL<br>2: upToSeconds=33840, payCode=ELEV_OVERTIME_30<br>3: upToSeconds=null, payCode=ELEV_OVERTIME_80 | (none — `payDayTypeRules: []`) |
| SATURDAY | 1: upToSeconds=21600, payCode=ELEV_SAT_NORMAL<br>2: upToSeconds=null, payCode=ELEV_SAT_AFTERNOON | (none) |
| SUNDAY | 1: upToSeconds=7200, payCode=ELEV_SUN_OT_50<br>2: upToSeconds=null, payCode=ELEV_SUN_OT_80 | (none) |
| HOLIDAY | 1: upToSeconds=7200, payCode=ELEV_HOL_OT_50<br>2: upToSeconds=null, payCode=ELEV_HOL_OT_80 | (none) |
| GRUNDLOVSDAG | 1: upToSeconds=null, payCode=GRUNDLOVSDAG | (none) |

**Spot-verified against the catalogue working tree (Step 3 of the brief): the WEEKDAY row above (26640/ELEV_NORMAL, 33840/ELEV_OVERTIME_30, null/ELEV_OVERTIME_80) was independently re-read from `pay-rule-set-presets.ts` lines 224-262 and matches exactly.**

### Preset 3: `glsa-jordbrug-elev-u18-dyrehold` (Jordbrug - Elev u18 Dyrehold)

Catalogue `name`: `GLS-A / 3F - Jordbrug Elev u18 Dyrehold 2026-2029`

| DayCode | tier order/upToSeconds/payCode list | dayTypeRules bands (dayType, start-end seconds, payCode) |
|---|---|---|
| WEEKDAY | 1: upToSeconds=28800, payCode=ELEV_NORMAL<br>2: upToSeconds=null, payCode=ELEV_OVERTIME_50 | (none — `payDayTypeRules: []`) |
| SATURDAY | 1: upToSeconds=28800, payCode=ELEV_SAT_NORMAL<br>2: upToSeconds=null, payCode=ELEV_SAT_ANIMAL_AFTERNOON | (none) |
| SUNDAY | 1: upToSeconds=7200, payCode=ELEV_SUN_OT_50<br>2: upToSeconds=null, payCode=ELEV_SUN_OT_80 | (none) |
| HOLIDAY | 1: upToSeconds=7200, payCode=ELEV_HOL_OT_50<br>2: upToSeconds=null, payCode=ELEV_HOL_OT_80 | (none) |
| GRUNDLOVSDAG | 1: upToSeconds=null, payCode=GRUNDLOVSDAG | (none) |

### Catalogue vs C# fixture divergence

All three catalogue keys have direct fixtures in `GlsAFixtureHelper.cs` (a separate file from `OverenskomstFixtureHelper.cs`, same directory) — an earlier pass only searched `OverenskomstFixtureHelper.cs` and, not finding them there, mistakenly compared against the unrelated Gartneri Elev fixtures instead. The correct fixtures are:

**Preset 1 (`glsa-jordbrug-elev-u18`) vs `GlsAFixtureHelper.GlsA_Jordbrug_Laerling_Under18`, Id=102:**
- Name string differs: catalogue `GLS-A / 3F - Jordbrug Elev u18 2026-2029` vs fixture `GLS-A / 3F - Jordbrug Elev u18 2024-2026` (year range).
- WEEKDAY tiers match byte-for-byte: 28800→`ELEV_NORMAL`, null→`ELEV_OVERTIME_50`.
- SATURDAY tiers match byte-for-byte: 28800→`ELEV_SAT_NORMAL`, null→`ELEV_SAT_OVERTIME_50`.
- SUNDAY tiers match byte-for-byte: 7200→`ELEV_SUN_OT_50`, null→`ELEV_SUN_OT_80`.
- HOLIDAY tiers match byte-for-byte: 7200→`ELEV_HOL_OT_50`, null→`ELEV_HOL_OT_80` — the fixture has dedicated Holiday payCodes, it does not reuse the Sunday codes.
- GRUNDLOVSDAG tiers match byte-for-byte: null→`GRUNDLOVSDAG`.
- dayTypeRules: catalogue is an explicit empty array `[]`; the fixture's `PayRuleSet` object has no `DayTypeRules` property set at all (structurally absent, not merely empty) — same practical effect, different representation, same pattern noted for the Gartneri/Skovbrug Elev presets above.
- **No other divergence found.**

**Preset 2 (`glsa-jordbrug-elev-o18`) vs `GlsAFixtureHelper.GlsA_Jordbrug_Laerling_Over18`, Id=103:**
- Name string differs: catalogue `GLS-A / 3F - Jordbrug Elev o18 2026-2029` vs fixture `GLS-A / 3F - Jordbrug Elev o18 2024-2026` (year range).
- WEEKDAY tiers match byte-for-byte: 26640→`ELEV_NORMAL`, 33840→`ELEV_OVERTIME_30`, null→`ELEV_OVERTIME_80`.
- SATURDAY tiers match byte-for-byte: 21600→`ELEV_SAT_NORMAL`, null→`ELEV_SAT_AFTERNOON`.
- SUNDAY tiers match byte-for-byte: 7200→`ELEV_SUN_OT_50`, null→`ELEV_SUN_OT_80`.
- HOLIDAY tiers match byte-for-byte: 7200→`ELEV_HOL_OT_50`, null→`ELEV_HOL_OT_80`.
- GRUNDLOVSDAG tiers match byte-for-byte: null→`GRUNDLOVSDAG`.
- dayTypeRules: same absent-vs-empty-array representation note as Preset 1.
- **No other divergence found.**

**Preset 3 (`glsa-jordbrug-elev-u18-dyrehold`) vs `GlsAFixtureHelper.GlsA_Jordbrug_Laerling_Under18_DyrePasning`, Id=104:**
- Name string differs: catalogue `GLS-A / 3F - Jordbrug Elev u18 Dyrehold 2026-2029` vs fixture `GLS-A / 3F - Jordbrug Elev u18 Dyrehold 2024-2026` (year range).
- WEEKDAY tiers match byte-for-byte: 28800→`ELEV_NORMAL`, null→`ELEV_OVERTIME_50`.
- SATURDAY tiers match byte-for-byte: 28800→`ELEV_SAT_NORMAL`, null→`ELEV_SAT_ANIMAL_AFTERNOON`.
- SUNDAY tiers match byte-for-byte: 7200→`ELEV_SUN_OT_50`, null→`ELEV_SUN_OT_80`.
- HOLIDAY tiers match byte-for-byte: 7200→`ELEV_HOL_OT_50`, null→`ELEV_HOL_OT_80`.
- GRUNDLOVSDAG tiers match byte-for-byte: null→`GRUNDLOVSDAG`.
- dayTypeRules: same absent-vs-empty-array representation note as Preset 1.
- **No other divergence found.**

---

## Gartneri & Skovbrug (6 presets)

Sources read: `pay-rule-set-presets.ts` (working tree, this repo) and `OverenskomstFixtureHelper.cs` (master, eform-timeplanning-base). Tables below transcribe the catalogue (TS) values exactly; the C# fixture is compared against it in the divergence section for each preset.

### 1. `glsa-gartneri-standard`
Catalogue name: `GLS-A / 3F - Gartneri Standard 2026-2029`

| DayCode | tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end seconds, payCode) |
|---|---|---|
| WEEKDAY | 1 / 26640 / `NORMAL`; 2 / 33840 / `OVERTIME_50`; 3 / null / `OVERTIME_100` | Monday, Tuesday, Wednesday, Thursday, Friday (defaultPayCode `NORMAL`, priority 1) each: 14400-21600 `SHIFTED_MORNING`; 21600-64800 `NORMAL`; 64800-72000 `SHIFTED_EVENING` |
| SATURDAY | 1 / 23400 / `SAT_NORMAL`; 2 / null / `SAT_AFTERNOON` | Saturday (defaultPayCode `SAT_NORMAL`, priority 1): 21600-45000 `SAT_NORMAL`; 45000-64800 `SAT_AFTERNOON` |
| SUNDAY | 1 / null / `SUN_HOLIDAY` | (none) |
| HOLIDAY | 1 / null / `SUN_HOLIDAY` | (none) |
| GRUNDLOVSDAG | 1 / null / `GRUNDLOVSDAG` | (none) |

### 2. `glsa-gartneri-elev-u18`
Catalogue name: `GLS-A / 3F - Gartneri Elev u18 2026-2029`

| DayCode | tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1 / 28800 / `ELEV_NORMAL`; 2 / null / `ELEV_OVERTIME_50` | (none — payDayTypeRules: []) |
| SATURDAY | 1 / 28800 / `ELEV_SAT_NORMAL`; 2 / null / `ELEV_SAT_OVERTIME_50` | (none) |
| SUNDAY | 1 / 7200 / `ELEV_SUN_OT_50`; 2 / null / `ELEV_SUN_OT_100` | (none) |
| HOLIDAY | 1 / 7200 / `ELEV_HOL_OT_50`; 2 / null / `ELEV_HOL_OT_100` | (none) |
| GRUNDLOVSDAG | 1 / null / `GRUNDLOVSDAG` | (none) |

### 3. `glsa-gartneri-elev-o18`
Catalogue name: `GLS-A / 3F - Gartneri Elev o18 2026-2029`

| DayCode | tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1 / 26640 / `ELEV_NORMAL`; 2 / 33840 / `ELEV_OVERTIME_50`; 3 / null / `ELEV_OVERTIME_100` | (none — payDayTypeRules: []) |
| SATURDAY | 1 / 23400 / `ELEV_SAT_NORMAL`; 2 / null / `ELEV_SAT_AFTERNOON` | (none) |
| SUNDAY | 1 / 7200 / `ELEV_SUN_OT_50`; 2 / null / `ELEV_SUN_OT_100` | (none) |
| HOLIDAY | 1 / 7200 / `ELEV_HOL_OT_50`; 2 / null / `ELEV_HOL_OT_100` | (none) |
| GRUNDLOVSDAG | 1 / null / `GRUNDLOVSDAG` | (none) |

### 4. `glsa-skovbrug-standard`
Catalogue name: `GLS-A / 3F - Skovbrug Standard 2026-2029`

| DayCode | tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1 / 26640 / `NORMAL`; 2 / 33840 / `OVERTIME_30`; 3 / null / `OVERTIME_100` | Monday, Tuesday, Wednesday, Thursday, Friday (defaultPayCode `NORMAL`, priority 1) each: 14400-21600 `SHIFTED_MORNING`; 21600-64800 `NORMAL`; 64800-72000 `SHIFTED_EVENING` |
| SATURDAY | 1 / 21600 / `SAT_NORMAL`; 2 / null / `SAT_AFTERNOON` | Saturday (defaultPayCode `SAT_NORMAL`, priority 1): 21600-43200 `SAT_NORMAL`; 43200-64800 `SAT_AFTERNOON` |
| SUNDAY | 1 / null / `SUN_HOLIDAY` | (none) |
| HOLIDAY | 1 / null / `SUN_HOLIDAY` | (none) |
| GRUNDLOVSDAG | 1 / null / `GRUNDLOVSDAG` | (none) |

### 5. `glsa-skovbrug-elev-u18`
Catalogue name: `GLS-A / 3F - Skovbrug Elev u18 2026-2029`

| DayCode | tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1 / 28800 / `ELEV_NORMAL`; 2 / null / `ELEV_OVERTIME_30` | (none — payDayTypeRules: []) |
| SATURDAY | 1 / 28800 / `ELEV_SAT_NORMAL`; 2 / null / `ELEV_SAT_OVERTIME_30` | (none) |
| SUNDAY | 1 / 7200 / `ELEV_SUN_OT_50`; 2 / null / `ELEV_SUN_OT_100` | (none) |
| HOLIDAY | 1 / 7200 / `ELEV_HOL_OT_50`; 2 / null / `ELEV_HOL_OT_100` | (none) |
| GRUNDLOVSDAG | 1 / null / `GRUNDLOVSDAG` | (none) |

### 6. `glsa-skovbrug-elev-o18`
Catalogue name: `GLS-A / 3F - Skovbrug Elev o18 2026-2029`

| DayCode | tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1 / 26640 / `ELEV_NORMAL`; 2 / 33840 / `ELEV_OVERTIME_30`; 3 / null / `ELEV_OVERTIME_100` | (none — payDayTypeRules: []) |
| SATURDAY | 1 / 21600 / `ELEV_SAT_NORMAL`; 2 / null / `ELEV_SAT_AFTERNOON` | (none) |
| SUNDAY | 1 / 7200 / `ELEV_SUN_OT_50`; 2 / null / `ELEV_SUN_OT_100` | (none) |
| HOLIDAY | 1 / 7200 / `ELEV_HOL_OT_50`; 2 / null / `ELEV_HOL_OT_100` | (none) |
| GRUNDLOVSDAG | 1 / null / `GRUNDLOVSDAG` | (none) |

### Catalogue vs C# fixture divergence

**`glsa-gartneri-standard`** (fixture: `GlsA_Gartneri_Standard`, Id=200)
- Name string differs: catalogue `GLS-A / 3F - Gartneri Standard 2026-2029` vs fixture `GLS-A / 3F - Gartneri Standard 2024-2026` (year range).
- All DayRules/Tiers (WEEKDAY, SATURDAY, SUNDAY, HOLIDAY, GRUNDLOVSDAG) match byte-for-byte.
- dayTypeRules: catalogue defines Monday-Friday weekday bands and a Saturday band (as tabulated above); the fixture's `PayRuleSet` object has no `DayTypeRules` property set at all — the structure is entirely absent from the fixture for this preset.

**`glsa-gartneri-elev-u18`** (fixture: `GlsA_Gartneri_Elev_Under18`, Id=201)
- Name string differs: `2026-2029` vs `2024-2026`.
- WEEKDAY, SATURDAY, SUNDAY, GRUNDLOVSDAG tiers match.
- HOLIDAY tier payCodes diverge: catalogue uses `ELEV_HOL_OT_50` (upToSeconds 7200) / `ELEV_HOL_OT_100`; fixture uses `ELEV_SUN_OT_50` (upToSeconds 7200) / `ELEV_SUN_OT_100` for the same HOLIDAY dayCode. upToSeconds values match; only the payCode names differ.
- dayTypeRules: catalogue is an explicit empty array `[]`; fixture has no `DayTypeRules` property set (structurally absent, not merely empty) — same practical effect, different representation.

**`glsa-gartneri-elev-o18`** (fixture: `GlsA_Gartneri_Elev_Over18`, Id=202)
- Name string differs: `2026-2029` vs `2024-2026`.
- WEEKDAY, SATURDAY, SUNDAY, GRUNDLOVSDAG tiers match.
- HOLIDAY tier payCodes diverge identically to the u18 case: catalogue `ELEV_HOL_OT_50`/`ELEV_HOL_OT_100` vs fixture `ELEV_SUN_OT_50`/`ELEV_SUN_OT_100` (upToSeconds 7200 matches in both).
- dayTypeRules: same absent-vs-empty-array note as above.

**`glsa-skovbrug-standard`** (fixture: `GlsA_Skovbrug_Standard`, Id=203)
- Name string differs: `2026-2029` vs `2024-2026`.
- All DayRules/Tiers match byte-for-byte.
- dayTypeRules: catalogue defines Monday-Friday weekday bands + Saturday band; fixture has no `DayTypeRules` property set at all (structurally absent).

**`glsa-skovbrug-elev-u18`** (fixture: `GlsA_Skovbrug_Elev_Under18`, Id=204)
- Name string differs: `2026-2029` vs `2024-2026`.
- WEEKDAY, SATURDAY, SUNDAY, GRUNDLOVSDAG tiers match.
- HOLIDAY tier payCodes diverge: catalogue `ELEV_HOL_OT_50`/`ELEV_HOL_OT_100` vs fixture `ELEV_SUN_OT_50`/`ELEV_SUN_OT_100` (upToSeconds 7200 matches).
- dayTypeRules: absent-vs-empty-array note as above.

**`glsa-skovbrug-elev-o18`** (fixture: `GlsA_Skovbrug_Elev_Over18`, Id=205)
- Name string differs: `2026-2029` vs `2024-2026`.
- WEEKDAY, SATURDAY, SUNDAY, GRUNDLOVSDAG tiers match.
- HOLIDAY tier payCodes diverge: catalogue `ELEV_HOL_OT_50`/`ELEV_HOL_OT_100` vs fixture `ELEV_SUN_OT_50`/`ELEV_SUN_OT_100` (upToSeconds 7200 matches).
- dayTypeRules: absent-vs-empty-array note as above.

---

## Golf + Praktikant (glsa-golf-standard, glsa-golf-elev, glsa-jordbrug-praktikant-udl-andet, glsa-jordbrug-praktikant-udl-staldarbejde)

### Preset: `glsa-golf-standard` ("Golf - Standard")

| DayCode | Tier order/upToSeconds/payCode | dayTypeRules bands (dayType, start–end seconds, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: null → `OVERTIME_100` | (none) |
| SATURDAY | 1: 21600 → `SAT_NORMAL`; 2: null → `SAT_AFTERNOON` | (none) |
| SUNDAY | 1: null → `SUN_HOLIDAY` | (none) |
| HOLIDAY | 1: null → `SUN_HOLIDAY` | (none) |
| GRUNDLOVSDAG | 1: null → `GRUNDLOVSDAG` | (none) |

`payDayTypeRules: []` (empty, whole preset).

### Preset: `glsa-golf-elev` ("Golf - Elev")

| DayCode | Tier order/upToSeconds/payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800 → `ELEV_NORMAL`; 2: null → `ELEV_OVERTIME_100` | (none) |
| SATURDAY | 1: 28800 → `ELEV_SAT_NORMAL`; 2: null → `ELEV_SAT_OVERTIME_100` | (none) |
| SUNDAY | 1: null → `ELEV_SUN_OT_100` | (none) |
| HOLIDAY | 1: null → `ELEV_HOL_OT_100` | (none) |
| GRUNDLOVSDAG | 1: null → `GRUNDLOVSDAG` | (none) |

`payDayTypeRules: []` (empty, whole preset).

### Preset: `glsa-jordbrug-praktikant-udl-andet` ("Praktikant udl - Andet arbejde")

| DayCode | Tier order/upToSeconds/payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | (none) |
| SATURDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | (none) |
| SUNDAY | 1: 7200 → `OVERTIME_50`; 2: null → `OVERTIME_80` | (none) |
| HOLIDAY | 1: 7200 → `OVERTIME_50`; 2: null → `OVERTIME_80` | (none) |
| GRUNDLOVSDAG | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | (none) |

`payDayTypeRules: []` (empty, whole preset).

### Preset: `glsa-jordbrug-praktikant-udl-staldarbejde` ("Praktikant udl - Staldarbejde")

| DayCode | Tier order/upToSeconds/payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | (none) |
| **SATURDAY** | 1: 26640 → `SAT_NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | dayType `Saturday`, defaultPayCode `SAT_NORMAL`, priority 1: band [0–43200 → `SAT_NORMAL`, priority 1]; band [43200–86400 → `SAT_ANIMAL_AFTERNOON`, priority 1] |
| SUNDAY | 1: 26640 → `ANIMAL_SUN_HOLIDAY`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | dayType `Sunday`, defaultPayCode `ANIMAL_SUN_HOLIDAY`, priority 1: band [0–86400 → `ANIMAL_SUN_HOLIDAY`, priority 1] |
| HOLIDAY | 1: 26640 → `ANIMAL_SUN_HOLIDAY`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | dayType `Holiday`, defaultPayCode `ANIMAL_SUN_HOLIDAY`, priority 1: band [0–86400 → `ANIMAL_SUN_HOLIDAY`, priority 1] |
| GRUNDLOVSDAG | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` | (none) |

(No `payDayTypeRules` entry for WEEKDAY or GRUNDLOVSDAG — only Saturday/Sunday/Holiday have bands.)

**Spot-verified against the catalogue working tree (Step 3 of the brief): the SATURDAY row above — tiers 26640/`SAT_NORMAL`, 33840/`OVERTIME_50`, null/`OVERTIME_80`, and bands 0–43200/`SAT_NORMAL`, 43200–86400/`SAT_ANIMAL_AFTERNOON` — was independently re-read from `pay-rule-set-presets.ts` lines 1739-1849 and matches exactly.**

### Catalogue vs C# fixture divergence

**`glsa-golf-standard`**: Only divergence is the `name`/`Name` string year suffix — catalogue: `"GLS-A / 3F - Golf Standard 2026-2029"`; fixture: `"GLS-A / 3F - Golf Standard 2024-2026"`. All day codes, tier orders, `upToSeconds`, and pay codes are byte-identical between the two sources (WEEKDAY 26640/NORMAL→OVERTIME_100, SATURDAY 21600/SAT_NORMAL→SAT_AFTERNOON, SUNDAY/HOLIDAY flat SUN_HOLIDAY, GRUNDLOVSDAG flat GRUNDLOVSDAG).

**`glsa-golf-elev`**: Only divergence is the name year suffix — catalogue: `"GLS-A / 3F - Golf Elev 2026-2029"`; fixture: `"GLS-A / 3F - Golf Elev 2024-2026"`. All day codes/tiers/seconds/payCodes match exactly (WEEKDAY 28800/ELEV_NORMAL→ELEV_OVERTIME_100, SATURDAY 28800/ELEV_SAT_NORMAL→ELEV_SAT_OVERTIME_100, SUNDAY ELEV_SUN_OT_100, HOLIDAY ELEV_HOL_OT_100, GRUNDLOVSDAG GRUNDLOVSDAG).

**`glsa-jordbrug-praktikant-udl-andet`**: Only divergence is the name year suffix — catalogue: `"...Andet arbejde 2026-2029"`; fixture: `"...Andet arbejde 2024-2026"`. All day codes/tiers/seconds/payCodes are identical, including the SATURDAY row (26640/NORMAL, 33840/OVERTIME_50, null/OVERTIME_80) and SUNDAY/HOLIDAY (7200/OVERTIME_50, null/OVERTIME_80).

**`glsa-jordbrug-praktikant-udl-staldarbejde`**: Only divergence is the name year suffix — catalogue: `"...Staldarbejde 2026-2029"`; fixture: `"...Staldarbejde 2024-2026"`. All day codes/tiers/seconds/payCodes match exactly, including the SATURDAY row (tier: 26640/SAT_NORMAL, 33840/OVERTIME_50, null/OVERTIME_80; bands: 0–43200/SAT_NORMAL priority 1, 43200–86400/SAT_ANIMAL_AFTERNOON priority 1) and the SUNDAY/HOLIDAY tiers+bands (26640/ANIMAL_SUN_HOLIDAY, 33840/OVERTIME_50, null/OVERTIME_80; band 0–86400/ANIMAL_SUN_HOLIDAY priority 1).

Note: the fixture identifies presets by a numeric `Id` (214, 215, 232, 233) while the catalogue uses a string `key` (`glsa-golf-standard`, `glsa-golf-elev`, `glsa-jordbrug-praktikant-udl-andet`, `glsa-jordbrug-praktikant-udl-staldarbejde`) — this is a schema difference, not a content divergence, and the two identifier sets align 1:1 with the four presets in the same order.

---

## Agroindustri (16 presets)

**Sources:**
- Catalogue (TS): `eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn/models/pay-rule-sets/pay-rule-set-presets.ts` (lines 1029-1645)
- C# fixture: `eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/OverenskomstFixtureHelper.cs` (lines 1089-2083)

All 16 presets have `payDayTypeRules: []` (TS) / no `DayTypeRules` populated (C#) — no dayTypeRules bands exist in either source for any of the 16 presets.

### 1. glsa-agro-fjerkrae-standard (TS) / GlsA_Agro_Fjerkrae_Standard, Id=216 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 33840: OVERTIME_30 → 3: 37440: OVERTIME_50 → 4: null: OVERTIME_100 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 2. glsa-agro-fjerkrae-elev (TS) / GlsA_Agro_Fjerkrae_Elev, Id=217 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_30 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_30 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 3. glsa-agro-grovvare-standard (TS) / GlsA_Agro_Grovvare_Standard, Id=218 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 37440: OVERTIME_40 → 3: null: OVERTIME_100 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 4. glsa-agro-grovvare-elev (TS) / GlsA_Agro_Grovvare_Elev, Id=219 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_40 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_40 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 5. glsa-agro-gulerod-standard (TS) / GlsA_Agro_Gulerod_Standard, Id=220 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 33840: OVERTIME_30 → 3: null: OVERTIME_80 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 6. glsa-agro-gulerod-elev (TS) / GlsA_Agro_Gulerod_Elev, Id=221 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_30 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_30 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 7. glsa-agro-kartoffelmel-standard (TS) / GlsA_Agro_Kartoffelmel_Standard, Id=222 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 33840: OVERTIME_30 → 3: 37440: OVERTIME_50 → 4: null: OVERTIME_100 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 8. glsa-agro-kartoffelmel-elev (TS) / GlsA_Agro_Kartoffelmel_Elev, Id=223 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_30 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_30 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 9. glsa-agro-kartoffelsorter-standard (TS) / GlsA_Agro_Kartoffelsorter_Standard, Id=224 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 33840: OVERTIME_30 → 3: null: OVERTIME_80 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 10. glsa-agro-kartoffelsorter-elev (TS) / GlsA_Agro_Kartoffelsorter_Elev, Id=225 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_30 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_30 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 11. glsa-agro-lucerne-standard (TS) / GlsA_Agro_Lucerne_Standard, Id=226 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 33840: OVERTIME_30 → 3: null: OVERTIME_80 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 12. glsa-agro-lucerne-elev (TS) / GlsA_Agro_Lucerne_Elev, Id=227 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_30 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_30 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 13. glsa-agro-minkfoder-standard (TS) / GlsA_Agro_Minkfoder_Standard, Id=228 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 33840: OVERTIME_30 → 3: null: OVERTIME_80 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 14. glsa-agro-minkfoder-elev (TS) / GlsA_Agro_Minkfoder_Elev, Id=229 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_30 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_30 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 15. glsa-agro-ovrige-standard (TS) / GlsA_Agro_Ovrige_Standard, Id=230 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 26640: NORMAL → 2: 33840: OVERTIME_30 → 3: null: OVERTIME_80 | None |
| SATURDAY | 1: 21600: SAT_NORMAL → 2: null: SAT_AFTERNOON | None |
| SUNDAY | 1: null: SUN_HOLIDAY | None |
| HOLIDAY | 1: null: SUN_HOLIDAY | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### 16. glsa-agro-ovrige-elev (TS) / GlsA_Agro_Ovrige_Elev, Id=231 (C#)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|
| WEEKDAY | 1: 28800: ELEV_NORMAL → 2: null: ELEV_OVERTIME_30 | None |
| SATURDAY | 1: 28800: ELEV_SAT_NORMAL → 2: null: ELEV_SAT_OVERTIME_30 | None |
| SUNDAY | 1: null: ELEV_SUN_OT_100 | None |
| HOLIDAY | 1: null: ELEV_HOL_OT_100 | None |
| GRUNDLOVSDAG | 1: null: GRUNDLOVSDAG | None |

### Catalogue vs C# fixture divergence

For all 16 presets, `dayCode`/`payTierRules`/`order`/`upToSeconds`/`payCode` values are byte-identical between the TS catalogue and the C# fixture. The only divergence found, present in **every single one of the 16 presets**, is the year range embedded in the `name`/`Name` field:

- **glsa-agro-fjerkrae-standard**: TS `name: 'GLS-A / 3F - Agroindustri Fjerkrae Standard 2026-2029'` vs C# `Name = "GLS-A / 3F - Agroindustri Fjerkrae Standard 2024-2026"`
- **glsa-agro-fjerkrae-elev**: TS `...Fjerkrae Elev 2026-2029` vs C# `...Fjerkrae Elev 2024-2026`
- **glsa-agro-grovvare-standard**: TS `...Grovvare Standard 2026-2029` vs C# `...Grovvare Standard 2024-2026`
- **glsa-agro-grovvare-elev**: TS `...Grovvare Elev 2026-2029` vs C# `...Grovvare Elev 2024-2026`
- **glsa-agro-gulerod-standard**: TS `...Gulerod Standard 2026-2029` vs C# `...Gulerod Standard 2024-2026`
- **glsa-agro-gulerod-elev**: TS `...Gulerod Elev 2026-2029` vs C# `...Gulerod Elev 2024-2026`
- **glsa-agro-kartoffelmel-standard**: TS `...Kartoffelmel Standard 2026-2029` vs C# `...Kartoffelmel Standard 2024-2026`
- **glsa-agro-kartoffelmel-elev**: TS `...Kartoffelmel Elev 2026-2029` vs C# `...Kartoffelmel Elev 2024-2026`
- **glsa-agro-kartoffelsorter-standard**: TS `...Kartoffelsorter Standard 2026-2029` vs C# `...Kartoffelsorter Standard 2024-2026`
- **glsa-agro-kartoffelsorter-elev**: TS `...Kartoffelsorter Elev 2026-2029` vs C# `...Kartoffelsorter Elev 2024-2026`
- **glsa-agro-lucerne-standard**: TS `...Lucerne Standard 2026-2029` vs C# `...Lucerne Standard 2024-2026`
- **glsa-agro-lucerne-elev**: TS `...Lucerne Elev 2026-2029` vs C# `...Lucerne Elev 2024-2026`
- **glsa-agro-minkfoder-standard**: TS `...Minkfoder Standard 2026-2029` vs C# `...Minkfoder Standard 2024-2026`
- **glsa-agro-minkfoder-elev**: TS `...Minkfoder Elev 2026-2029` vs C# `...Minkfoder Elev 2024-2026`
- **glsa-agro-ovrige-standard**: TS `...Ovrige Standard 2026-2029` vs C# `...Ovrige Standard 2024-2026`
- **glsa-agro-ovrige-elev**: TS `...Ovrige Elev 2026-2029` vs C# `...Ovrige Elev 2024-2026`

No other divergences found for any preset: day codes present (WEEKDAY, SATURDAY, SUNDAY, HOLIDAY, GRUNDLOVSDAG), tier counts, tier order, upToSeconds values, and payCode strings all match exactly between the two sources for all 16 presets. Neither source defines any `dayTypeRules`/`DayTypeRules` bands for any of the 16 presets (both are empty/absent).

---

## Engine facts

### (a) Bands vs. tiers — which wins, and for which presets/day-types the tier split actually runs

`CalculatePayLinesForDay` (`eform-angular-timeplanning-plugin/.../TimePlanningWorkingHoursService.cs:4684-4813`) picks the route in this order:

1. **Grundlovsdag special-case** (line 4707): only entered if `usesNormalTimeSplit` (see below) is true *and* `dayCode == "GRUNDLOVSDAG"`.
2. **Time-band path wins by default whenever it applies.** Line 4719 resolves `DayType` via `TryGetDayType`; line 4721-4724 checks `hasTimeBandRule` — true iff `payRuleSet.DayTypeRules` has a row for that `DayType` with a non-empty `TimeBandRules`. If so, bands are used (line 4726 `if (hasTimeBandRule)`), **not** the tiers, unless the extra opt-in condition below fires.
3. **Tier path is the fallback**, used only when there is no time-band rule for the resolved `DayType` (or `TryGetDayType` returned false), at lines 4805-4812: `PayLineGenerator.GeneratePayLines(...)`.

So structurally, bands beat tiers whenever both exist for a day — **except** that within the banded path, tiers 2..n can still contribute an "overtime" split on top of the bands, gated by identity, not shape:

- `usesNormalTimeSplit = PayRuleSetLock.IsNormalTimeSplitPresetName(payRuleSet.Name)` (line 4702) is true **only** for the two presets in `PayRuleSetLock.cs:150-152`:
  - `"GLS-A / 3F - Udenlandske praktikanter Landbrug Staldarbejde"`
  - `"GLS-A / 3F - Udenlandske praktikanter Landbrug Andet arbejde"`
- Even for those two, the split only executes when the day's ordered tiers pass `PayRuleSetLock.HasNormalTimeBoundaryShape` (`PayRuleSetLock.cs:229-241`: exactly 3 tiers, tier1.UpToSeconds==26640, tier2.UpToSeconds==33840 && PayCode=="OVERTIME_50", tier3.UpToSeconds==null && PayCode=="OVERTIME_80") — checked at `TimePlanningWorkingHoursService.cs:4766-4769`.
- When both conditions hold, normal-time seconds (`Math.Min(totalSeconds, normalSeconds)`, line 4771) are attributed by clock position via `GenerateTimeBandPayLines` (lines 4779-4783) and the overflow via `GenerateOvertimeTierPayLines` from tier 2 onward (lines 4785-4790, method at 5118-5168).
- All other bands-configured presets/days — explicitly named in the comment at `TimePlanningWorkingHoursService.cs:4743-4752` as "thirteen other preset/day combinations" (Jordbrug Standard & Dyrehold WEEKDAY+SATURDAY, Gartneri Standard WEEKDAY+SATURDAY, Skovbrug Standard WEEKDAY+SATURDAY, KA Svine/Plante/Maskin WEEKDAY, KA Gron WEEKDAY+SATURDAY) — fall through to the plain bands-only branch at lines 4795-4801 (`GenerateTimeBandPayLines` per shift segment, no tier involvement at all).
- Same identity+shape gate applies to Grundlovsdag: `CalculateGrundlovsdagPayLines` (`TimePlanningWorkingHoursService.cs:5006-5103`) requires `HasNormalTimeBoundaryShape` on the `"GRUNDLOVSDAG"` day rule's tiers (line 5029-5031) or returns `null`, in which case the caller (line 4712) falls through to the ordinary time-band/tier routing above using dayCode `"GRUNDLOVSDAG"`.

Net effect: for every preset except the two Udenlandske praktikanter ones, if a day has time bands configured, tiers never execute for that day at all; the normal/overtime tier split is a name+shape-gated exception, not general engine behavior.

### (b) DayType enum values

`eform-timeplanning-base/Microting.TimePlanningBase/Infrastructure/Data/Entities/DayType.cs:27-37`:

```csharp
public enum DayType
{
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
    Sunday,
    Holiday
}
```

Eight values total — one per weekday plus a single generic `Holiday`. **No distinct `DayType` exists for Grundlovsdag, 24 December, or 1 May.**

- Grundlovsdag is not a `DayType` at all; it's a separate string `dayCode` (`"GRUNDLOVSDAG"`) computed in `GetDayCodeForDate` (`TimePlanningWorkingHoursService.cs:4218-4243`, priority-first check at 4221-4224) and duplicated in `PlanRegistrationHelper.GetDayCode` (`PlanRegistrationHelper.cs:2585-2612`, same June-5 check at 2588-2591). `TryGetDayType` (`TimePlanningWorkingHoursService.cs:4250-4275`) explicitly returns `false` for it: `if (dayCode == "GRUNDLOVSDAG") { dayType = DayType.Monday; /* unused */ return false; }` (lines 4252-4256).
- 24 December and 1 May have no dedicated code path anywhere searched in either repo. They are classified purely through `PlanRegistrationHelper.IsOfficialHoliday(date)` (`PlanRegistrationHelper.cs:2618-2627`), which checks membership in a bundled JSON file (`eform-angular-timeplanning-plugin/.../TimePlanning.Pn/Resources/danish_holidays_2025_2030.json`, loaded per `PlanRegistrationHelper.cs:35,58,79`). If present in that config they get dayCode `"HOLIDAY"` → `DayType.Holiday` (line 4258-4261); if not, they fall through to ordinary weekday classification by `date.DayOfWeek` (lines 4264-4273). Either way they collapse into one of the 8 enum values above — there is no `DayType.LabourDay` or `DayType.ChristmasEve`.

### (c) Flat per-day (kr/dag) supplement — is it encoded, and where

**Not as a distinct amount field anywhere in the engine.** The engine's data model has no monetary/amount fields at all — only `PayCode` (a string label) and `HoursInSeconds`/`Hours`:

- `PayTierRule` (`eform-timeplanning-base/.../Entities/PayTierRule.cs:27-36`): `PayDayRuleId, UpToSeconds, PayCode, PayrollCode, Order`.
- `PayTimeBandRule` (`.../PayTimeBandRule.cs:27-37`): `PayDayTypeRuleId, StartSecondOfDay, EndSecondOfDay, PayCode, PayrollCode, Priority`.
- `PlanRegistrationPayLine` (`.../PlanRegistrationPayLine.cs:27-38`): `PlanRegistrationId, PayCode, PayrollCode, Hours, HoursInSeconds, PayRuleSetId, CalculatedAt`.

None of these carry a rate or amount column. The engine only ever emits hours tagged with a `PayCode`/`PayrollCode`; whether a given `PayCode` is paid per-hour or as a flat kr/dag amount is entirely a downstream/payroll-system interpretation outside this codebase. The only place "flat per-day" is even mentioned is a code comment, not a data field: `PayRuleSetLock.cs:214`, describing `SAT_ANIMAL_AFTERNOON` as "a fixed kr/dag afternoon supplement" purely as prose explaining why misreading stale tier data would be wrong — there is no `FlatAmount`/`PerDayAmount`/`IsFlatRate` field backing that description (confirmed absent via repo-wide search for `FlatAmount|PerDayAmount|AmountPerDay|FixedAmount|RatePerHour|HourlyRate` across both repos — the only hits are that comment, the plugin's test file, and the frontend `pay-rule-set-presets.ts`, none of which add a code-enforced amount field).

### (d) No rule matches — fallback, drop, or throw

Never throws; falls back to a synthetic `"DEFAULT"` pay code covering all the time, immediately rewritten to `"NORMAL"` by the caller:

- Tier path, no matching `PayDayRule`/empty tiers: `PayLineGenerator.GeneratePayLines` (`eform-timeplanning-base/.../PayLineGenerator.cs:44-59`) — `if (dayRule == null || dayRule.Tiers == null || !dayRule.Tiers.Any())` emits a single line `PayCode = "DEFAULT"` with `HoursInSeconds = totalSeconds` (i.e. **all** the seconds, nothing dropped), and returns early (line 58).
- Band path, no matching `PayDayTypeRule`/empty bands: `PayLineGenerator.GenerateTimeBandPayLines` (`.../PayLineGenerator.cs:130-135`) — same fallback, one `"DEFAULT"` line for `totalSeconds`.
- Band path, partial coverage: gaps before the first band and trailing time after the last band are also filled with `dayTypeRule.DefaultPayCode` (not `"DEFAULT"`) at lines 152-162 and 176-181 respectively — so within a day-type rule that does exist but whose bands don't cover the whole shift, uncovered minutes get the rule's configured `DefaultPayCode`, still never dropped.
- `TimePlanningWorkingHoursService.MapDefaultToNormal` (`TimePlanningWorkingHoursService.cs:5182-5196`) is the single exit point that rewrites any `"DEFAULT"` line's `PayCode` to `"NORMAL"` (lines 5188-5191), re-merging by pay code only if a rewrite happened (line 5195), so the base package's raw `"DEFAULT"` fallback is never visible to callers of `CalculatePayLinesForDay`.
- The only place time is actually **dropped** (zero lines returned) is when there is nothing to allocate in the first place: `if (totalSeconds <= 0) { return new List<PlanRegistrationPayLine>(); }` (`TimePlanningWorkingHoursService.cs:4806-4809`) and the top-level `if (payRuleSet == null) return new List<...>();` (lines 4691-4694) — both are "no work to attribute" cases, not "no rule matched" cases.

### (e) Cap/floor logic (e.g. 8-hour cap for Elev/apprentices)

**No such cap or floor exists anywhere in the engine code searched.** Repo-wide search across both `eform-timeplanning-base` and the `TimePlanning.Pn` plugin source for `Elev`, `28800` (8h in seconds), `DailyCap`, `MaxSeconds`, `CapSeconds`, and general "8 hour" patterns returns no hits outside `PayRuleSetLock.cs` itself, and that file's only match is the string `"Elev"` embedded in preset **names** in `LockedPresetNames` (e.g. `"GLS-A / 3F - Jordbrug Elev u18 2026-2029"`, `PayRuleSetLock.cs:41-67`) — these are just catalogue identifiers for locking/opt-in matching, not cap logic. The only numeric bound anywhere near this code is `tier.UpToSeconds` (`PayTierRule.cs:32`), which is a generic per-tier cumulative threshold used identically for every preset (`PayLineGenerator.cs:73-77`, `TimePlanningWorkingHoursService.cs:5139-5143`) — there is no apprentice-specific 8-hour ceiling encoded as a distinct mechanism; if an Elev preset wants an 8h boundary it would have to be expressed as an ordinary tier `UpToSeconds` value like any other preset, and no such Elev-specific value or special-cased logic was found in the engine.
