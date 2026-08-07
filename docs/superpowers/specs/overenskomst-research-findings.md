# Overenskomst Research Findings

Living document tracking which Danish collective agreements can be added as presets to the Pay Rule Set system. Updated as new research is done.

## System Capabilities

Our PayLineGenerator supports:
- **Tier-based splitting:** Normal hours -> OT tier 1 (%) -> OT tier 2 (%) based on cumulative seconds
- **Time-band splitting:** Time-of-day bands (e.g., 06:00-18:00 NORMAL, 18:00-23:00 EVENING)
- **Day classification:** WEEKDAY, SATURDAY, SUNDAY, HOLIDAY, GRUNDLOVSDAG
- **Locked presets:** Non-editable, singleton, with validity period in name

---

## Already Implemented (21 presets)

### GLS-A / 3F (13 presets)
| Preset | OT Tiers | Period | Status |
|--------|----------|--------|--------|
| Jordbrug - Standard | 30%/80% | 2026-2029 | Done |
| Jordbrug - Dyrehold | 30%/80% + animal time bands | 2026-2029 | Done |
| Jordbrug - Elev u18 | 50% (8h cap) | 2026-2029 | Done |
| Jordbrug - Elev o18 | 30%/80% weekday, 50%/80% Sun | 2026-2029 | Done |
| Jordbrug - Elev u18 Dyrehold | 50% + animal | 2026-2029 | Done |
| Gartneri - Standard | 50%/100% (Sat split 12:30) | 2026-2029 | Done |
| Gartneri - Elev u18 | 50%/100% (8h cap) | 2026-2029 | Done |
| Gartneri - Elev o18 | 50%/100% | 2026-2029 | Done |
| Skovbrug - Standard | 30%/100% | 2026-2029 | Done |
| Skovbrug - Elev u18 | 30%/100% (8h cap) | 2026-2029 | Done |
| Skovbrug - Elev o18 | 30%/100% | 2026-2029 | Done |
| Udenlandske praktikanter Landbrug - Andet arbejde | 50%/80% (2h middle tier) | 2026-2029 | Done |
| Udenlandske praktikanter Landbrug - Staldarbejde | 50%/80% weekday + Sat 12:00 split + ANIMAL_SUN_HOLIDAY | 2026-2029 | Done |

Source for Udenlandske praktikanter: [GLS-A Loenoversigt: Praktikanter-landbrug 2025](https://www.gls-a.dk/wp-content/uploads/2025/02/Praktikanter-landbrug2025.pdf).
The loenoversigt distinguishes trainees under 25 vs 25+ during months 7-18 of the praktik, but only in the base kr/time - not the rule structure. Tier cutoffs, day types, pay codes, and supplements are identical for both age groups, so a single preset per work variant serves both.

### KA / Krifa (8 presets)
| Preset | OT Tiers | Period | Status |
|--------|----------|--------|--------|
| Landbrug Svine/Kvaeg - Standard | 50%/100% (normal to 19:00) | 2025-2028 | Done |
| Landbrug Svine/Kvaeg - Elev | 50%/100% (8h cap) | 2025-2028 | Done |
| Landbrug Plantebrug - Standard | 50%/100% (3h first tier!) | 2025-2028 | Done |
| Landbrug Plantebrug - Elev | 50%/100% (8h cap) | 2025-2028 | Done |
| Landbrug Maskinstation - Standard | 30%/80% | 2025-2028 | Done |
| Landbrug Maskinstation - Elev | 30%/80% (8h cap) | 2025-2028 | Done |
| Gron - Standard | 50%/100% (3h first tier) | 2025-2028 | Done |
| Gron - Elev | 50%/100% (8h cap) | 2025-2028 | Done |

---

## Ready to Implement (researched, rules known)

### GLS-A / 3F - Remaining Farming Sectors

#### Agroindustri (#4012, 2024-2026)
- **Parties:** GLS-A / 3F Den Gronne Gruppe
- **Covers:** Agro-industrial processing (feed mills, grain processing, etc.)
- **OT:** Similar to Jordbrug (30%/80%)
- **Normal:** 37h/week, Mon-Sat 06:00-18:00
- **Fit:** Perfect - same structure as existing Jordbrug presets
- **Variants needed:** Standard, Elev u18, Elev o18
- **Source:** [Agroindustri 2024-2026 PDF](https://www.3f.dk/-/media/files/artikler/overenskomst/den-groenne-gruppe/overenskomster/4012---agroindustri--2024-2026---endelig-17,-d-,05,-d-,24.pdf)

#### Golf (#4014, 2024-2026)
- **Parties:** GLS-A / 3F Den Gronne Gruppe
- **Covers:** Golf course workers (greenkeepers, maintenance)
- **OT:** Similar to Gartneri (50%/100%)
- **Normal:** 37h/week, seasonal variation
- **Fit:** Good - same structure, seasonal hours may need attention
- **Variants needed:** Standard, Elev
- **Source:** [Golf 2024-2026 PDF](https://www.3f.dk/-/media/files/artikler/overenskomst/den-groenne-gruppe/overenskomster/4014---golf-2024-2026---endelig-30,-d-,05,-d-,24.pdf)

#### Fiskeopdraet, -slagterier og -foraedling (Fish farming/processing)
- **Parties:** GLS-A / 3F
- **Covers:** Fish farming, fish slaughterhouses, fish processing
- **OT:** Need to verify - likely similar to Agroindustri
- **Period:** 2024-2026
- **Fit:** Good - hourly workers with standard OT tiers
- **Source:** [GLS-A Overenskomster](https://www.gls-a.dk/overenskomst/)

#### GASA Sortering og Pakning (Sorting & Packing)
- **Parties:** GLS-A / 3F
- **Covers:** Sorting and packing of agricultural produce
- **OT:** Need to verify
- **Period:** 2024-2026
- **Fit:** Good - production workers

#### GASA Transport
- **Parties:** GLS-A / 3F
- **Covers:** Transport of agricultural produce
- **OT:** Need to verify
- **Period:** 2024-2026
- **Source:** [GASA Transport 2024-2026 PDF](https://www.gls-a.dk/wp-content/uploads/2024/07/4016-GASA-TRANSPORT-2024-2026-endelig-05.07.24.pdf)

#### Holddrift (Shift work agreement)
- **Parties:** GLS-A / Dansk Metal + 3F
- **Covers:** Shift workers across GLS-A sectors
- **Structure:** Different - uses shift supplements (tillaegstyper) rather than OT tiers
- **Period:** 2024-2026
- **Fit:** Possible but may need different pay code structure
- **Source:** [GLS-A Holddrift 2024-2026](https://www.danskmetal.dk/pjecer-og-udgivelser/overenskomst-gls-a-holddrift-2024-2026)

#### GLS-A / Dansk Metal
- **Parties:** GLS-A / Dansk Metal
- **Covers:** Metal workers in agriculture (mechanics, technicians)
- **OT:** Need to verify - likely follows Dansk Metal patterns
- **Period:** 2024-2026
- **Source:** [GLS-A Dansk Metal](https://www.danskmetal.dk/overenskomster/andre-brancher/gls-a)

#### GLS-A / HK
- **Parties:** GLS-A / HK Privat
- **Covers:** Office/administrative staff in agricultural companies
- **OT:** Salaried (funktionaer) - no hourly OT tiers
- **Fit:** Poor - salaried workers don't use our tier system
- **Source:** [HK GLS-A](https://www.hk.dk/raadogstoette/vaerktoejer/overenskomster/privat/11400/11380_gartneri-land-og-skovbrug)

### KA / Krifa - Remaining Sectors

#### Dag- og Dogninstitutioner (Day/residential institutions)
- **Parties:** KA / Krifa
- **Covers:** Pedagogical, care, social work staff
- **OT:** 50%/100% (1-3h then 100%)
- **Time bands:** Weekday 17:00-06:00, Saturday 06:00-24:00, Sunday/Holiday 00:00-24:00
- **Period:** 2025-2028
- **Fit:** Good - same OT tier structure
- **Variants needed:** Standard, Elev
- **Source:** Pages 94-96 of KA/Krifa Hovedoverenskomst

---

## Needs More Research

### Fodevareindustri (NNF)
- **Parties:** DI / Fodevareforbundet NNF
- **Covers:** Food production, bakeries, chocolate, meat processing, dairy
- **Sub-agreements:** Mejeri (dairy), Slagteri (slaughter), Fodevareindustri (general food)
- **OT:** Need to download and read the actual agreements
- **Period:** 2025-2028
- **Fit:** Likely good - production workers with hourly OT
- **Sources:**
  - [NNF Fodevareindustri](https://nnf.dk/overenskomst/fodevareindustri/)
  - [NNF Mejeri](https://nnf.dk/overenskomst/mejeri/)
  - [NNF Slagteri](https://nnf.dk/overenskomst/slagterindustri/)

### Fitness/Traeningscentre
- **No dedicated overenskomst found** for fitness centers specifically
- May fall under HORESTA (Hotel & Restaurant) or DI Service depending on the employer
- Staff types: instructors (often freelance), reception (may be HK), cleaning (3F Service)
- **Fit:** Unclear - need to determine which agreement applies
- **Action:** Ask customer which employer association their fitness center belongs to

### Industriens Overenskomst (DI / CO-industri)
- **Parties:** DI / CO-industri (3F, Dansk Metal, HK)
- **Covers:** ~250,000 workers in manufacturing, production, VVS
- **OT:** 50% first 3h, 100% thereafter, Sun/Holiday 100%
- **Shift:** 3 shift supplement levels (forskudt tid)
- **Period:** 2025-2028
- **Fit:** Perfect - same tier structure as Gartneri
- **Source:** [Industriens Overenskomst 2025-2028](https://www.co-industri.dk/sites/default/files/2025-05/Industriens-Overenskomst-2025-2028-2025_05_19.pdf)

### Transport & Logistik (DI/ATL / 3F)
- **Parties:** DI Overenskomst I (ATL) / 3F Transport
- **Covers:** Drivers, warehouse workers, logistics
- **OT:** 50% first 3h, 100% thereafter
- **Period:** 2025-2028
- **Fit:** Good - same OT structure
- **Source:** [Transport og Logistik 2025-2028](https://www.3f.dk/-/media/files/artikler/overenskomst/transportgruppen/transport-og-logistikoverenskomst-2025-2028---bog-i----overenskomst.pdf)

### Bygge & Anlaeg (Construction)
- **Parties:** Dansk Byggeri / 3F Byggegruppen
- **Covers:** Construction workers
- **OT:** 50% first 3h, 100% thereafter
- **Period:** 2025-2028
- **Fit:** Good - same structure but seasonal patterns
- **Source:** [Bygningsoverenskomsten 2025-2028](https://www.3f.dk/-/media/files/artikler/overenskomst/byggegruppen/overenskomster/bygge--og-anlaegsoverenskomsten-2025-2028.pdf)

### Hotel & Restaurant (HORESTA / 3F)
- **Parties:** HORESTA / 3F Privat Service
- **Covers:** ~70,000 hotel, restaurant, catering workers
- **OT:** Need to verify exact rates
- **Period:** 2025-2028
- **Fit:** Possible - hospitality has complex scheduling
- **Source:** [HORESTA OK 2025](https://www.horesta.dk/dit-personale/ok-2025/)

### Rengoring / Service (Cleaning)
- **Parties:** DI Service / 3F, ESL
- **Covers:** Cleaning staff, facility services
- **OT:** Similar to Industri
- **Period:** 2025-2028
- **Fit:** Good

---

## Not a Good Fit for Our System

| Agreement | Why Not |
|-----------|---------|
| **HK Privat (Office)** | Salaried/funktionaer - no hourly OT tiers |
| **Akademikere (AC)** | Salaried professionals |
| **Laeger/Tandlaeger** | Complex on-call/duty, not simple OT |
| **Kommuner/Regioner (FOA/BUPL/DSR)** | Percentage-based supplements on salary, not OT tiers on hours |

---

## Priority Queue (suggested implementation order)

1. **GLS-A Agroindustri** - Same structure as Jordbrug, minimal effort
2. **GLS-A Golf** - Same structure as Gartneri
3. **GLS-A Fiskeopdraet** - Same structure, farming-adjacent
4. **KA Dag- og Dogninstitutioner** - Already have KA group, clear rules
5. **Industriens Overenskomst** - Huge worker base, 50%/100% structure
6. **Transport & Logistik** - Growing sector
7. **Bygge & Anlaeg** - Large sector
8. **Hotel & Restaurant** - Large sector
9. **NNF Fodevareindustri** - Multiple sub-agreements
10. **GASA Sortering/Pakning + Transport** - Niche but farming-related

## OK26 Verification (2026-07-08)

All GLS-A/3F Den Grønne Gruppe families were renewed in a single combined OK26 settlement signed 25 February 2026, effective 1 March 2026, with minimum contract term through 1 March 2029 → all GLS-A presets period label is **2026-2029**.

### Per-family verification status:
- **Jordbrug 2026-2029** — full text published (gls-a.dk, 4010, 2. udgave 06.07.26). Structure (overtime 30%/80%, 7h24m/9h24m cutoffs, dyrehold bands, praktikant 50%/80%, Grundlovsdag) unchanged vs 2024-2026.
- **Agroindustri 2026-2029** — full text published (4012, 07.07.26). All eight sub-areas' overtime clauses word-for-word identical to 2024-2026.
- **Golf 2026-2029** — confirmed by GLS-A's official lønoversigt (March 2026: "overenskomstperioden 2026-2029"); full text not yet typeset. Wage-overview wording identical to prior year except rates.
- **Gartneri / Skovbrug 2026-2029** — full texts not yet typeset, but covered by the same signed settlement; the 31-protocol master document (Protokollat 23) states overtime rates "beregnes som hidtil" (unchanged).

### Pre-existing encoding discrepancies (open verification questions for second-opinion review against primary texts)

These mismatches exist against both the 2024-2026 and 2026-2029 texts (unchanged wording), so the rename neither fixes nor worsens them:

1. Agro **Gulerodspakkerier**: text has 30% (h1-2) / 80% (h3) / 100% (beyond + Sun/Hol); preset ends at 80%.
2. Agro **Kartoffelsortering**: text has 30% → 100% (no 80% tier); preset has 30% → 80%.
3. Agro **Minkfoder**: text has a third tier at 100% keyed to clock time (after 20:00; Sun after 12:00); preset ends at 80%.
4. Agro **Øvrige**: text uses flat-DKK supplements across three clock-hour bands + a separate two-band Sunday scale; preset models 30%→80% percentages.
5. **Skovbrug** evening forskudt-tid band: text allows 18:00-19:00 (1h); preset encodes 18:00-20:00.
6. **Skovbrug** Saturday: text treats all Saturday work as overtime from hour 1; preset gives 6h "normal" first.
7. **Gartneri** Sunday/holiday: text tiers it 50% (first 2h) / 100%; preset uses a single all-day SUN_HOLIDAY code.
8. **Elev u18 8h/day threshold** (Gartneri/Skovbrug/Golf/Agro): no basis in the overenskomst texts — likely from the statutory youth-work rules; verify intent before changing.

---

# Overarbejde × forskudt tid: how the two interact (research 2026-08-07)

**Scope note.** This system reports **how many minutes of a work period fall under which rule**. It does not compute money. Everything below is therefore framed as *minute attribution*; rates are quoted only where they prove which rule an hour belongs to.

## There is no universal Danish rule — it is per agreement

Researched against primary agreement PDFs. The three patterns found:

| Agreement | Overtime hour inside a displaced-time band | Evidence |
|---|---|---|
| **KA / Krifa** (Landbrug svine/kvæg, plantebrug, maskinstation; Grøn) | **Cumulative — both** | Hovedoverenskomst § 16 stk. 5: *"Udføres overarbejde på særlige tidspunkter … betales **foruden overtidstillæg også tillæg for arbejde på særlige tidspunkter**."* |
| **GLS-A Jordbrug — dyrehold (§ 15)** | **Not cumulative** | § 15 opens *"ved arbejde **inden for normal arbejdstid**"* — an overtime hour is outside normal time, so the stald supplement cannot attach to it. |
| **GLS-A Jordbrug markarbejde (§ 23), Gartneri (§ 23), Skovbrug (§ 22)** | **Agreement is silent** | Exhaustive search for `samtidig`, `foruden`, `i tilslutning`, `ydes ikke tillæg`, `bortfalder` finds no clause either way. |

Wider-market benchmarks, for the silent cases: Industriens Overenskomst § 14 stk. 6 stacks them **if the overtime is contiguous with the displaced shift**; Anlægsgartner (4002) § 11 stk. 11 stacks them outright; the municipal sector defines them as disjoint (*"Overarbejde er ikke arbejde i forskudt tid"*, KL 04.89 § 1 stk. 2).

Supporting detail for the GLS-A Jordbrug reading: § 22's overtime figures are **totals**, not add-ons — the column is headed `Timeløn i alt pr. overtime`, and C-løn × 1.30 / × 1.80 reproduces the published figures to the øre for all three years.

**Consequence for this system:** the interaction cannot be a single global engine rule. It belongs as a per-rule-set property.

## The engine currently satisfies none of these

`TimePlanningWorkingHoursService.CalculatePayLinesForDay` routes a day **exclusively**: if the matching `DayType` has any `TimeBandRules`, only band attribution runs and the `PayDayRule` tiers are never evaluated.

Because the GLS-A/KA "Standard" presets define weekday bands, their overtime tiers are unreachable — a 12-hour weekday yields one `NORMAL` line of 43200 s and **no overtime minutes at all**. This is wrong under every reading above: under the cumulative reading the overtime minutes are missing, and under the non-cumulative reading the minutes past the daily norm should have been attributed to the overtime rule *instead of* the band rule.

Affected presets (weekday tiers shadowed by weekday bands) — 8 of 39:

Jordbrug Standard · Jordbrug Dyrehold · Gartneri Standard · Skovbrug Standard · KA Landbrug Svine/Kvæg · KA Landbrug Plantebrug · KA Landbrug Maskinstation · KA Grøn

The 31 remaining presets (all Elev/praktikant variants, Golf, Agroindustri) declare no weekday bands, so their tiers do run.

## Additional Jordbrug/dyrehold discrepancies found

- **Dyrehold 05:00–06:00 `SHIFTED_MORNING` and 18:00–24:00 `SHIFTED_EVENING` bands have no basis in § 15**, which lists only weekday 00:00–05:00, Saturday after 12:00, and Sun/holidays. Minutes are being attributed to two rules that do not exist for dyrehold.
- **§ 15 Saturday-afternoon and Sunday/holiday supplements are per *day*, not per hour.** Encoding them as hourly pay codes attributes N hours where the agreement triggers once.
- **The 9h24m tier boundary is an assumption.** § 22 keys off *"efter den normale daglige arbejdstids **ophør**"* — the end of the actual scheduled day. It equals 9h24m only when that day is exactly 7,4 h; under § 9 stk. 5 varying hours and the alternative-scheduling protocol (*"Ingen arbejdsdag … over 9,25 timer"*) it does not.

## Sources

Jordbrug 2026-2029 · Jordbrug 2024-2026 · Jordbrug 2021-2024 · Gartneri 2024-2026 · Skovbrug 2024-2026 (all gls-a.dk / 3f.dk PDFs) · GLS-A Lønoversigt Landbrugsarbejde marts 2026 · KA/Krifa Overenskomst 2025-2028 (krifa.dk) · KA fagoverenskomst Landbrug og Grøn (ka.dk) · Industriens Overenskomst 2025-2028 (co-industri.dk) · Anlægsgartnerarbejde 2025-2028 (3f.dk) · KL Aftale 04.89 (kl.dk).

**Not accessible:** GLS-A member-only guidance on forskudttidstillæg and on udenlandske praktikanter — the most likely place where GLS-A states its administrative practice on the points the text leaves open. Worth checking with a member login before building on the silent cases. Gartneri and Skovbrug exist only as 2024-2026 texts; 2026-2029 versions are pending and may add an interaction clause.

---

# Udenlandske praktikanter, landbrug (§ 50) — authoritative rules

Governing clause is **inside the main Jordbrugsoverenskomst (4010)**, not a separate praktikantaftale:

| Period | Clause |
|---|---|
| 2026-03-01 → 2029 | **§ 50. Udenlandske praktikanter** |
| 2024-03-01 → 2026-02-28 | § 48 |
| 2021-03-01 → 2024 | § 48 |

The 50 %/80 % overtime wording and the two-item staldarbejde tillæg structure are **verbatim identical across all three generations**. What changed in 2026-2029: a new pensionsafsavnstillæg (stk. 5), and the inheritance clause was narrowed from *"Overenskomstens øvrige bestemmelser er gældende for praktikanter."* to *"… **hvor andet ikke følger af § 50**."*

## Working time — the staldarbejde / andet arbejde fork

§ 50 contains **no arbejdstid provision**; it inherits § 8 and § 9 via stk. 7. GLS-A's own rate sheet restates the result:

> **Staldarbejde** — normal arbejdstid indtil 37 timer pr. uge **eller 296 timer i en 8 ugers periode**, og kan lægges **på alle ugens dage, hele døgnet**.
> **Andet arbejde** — normal arbejdstid indtil 37 timer pr. uge, **mandag til lørdag mellem kl. 6.00 og 18.00**.

**There is no daily norm anywhere in the agreement** — only the placement window. So "overarbejde efter den normale arbejdstids ophør" has no fixed daily trigger for a praktikant; it depends on the individual praktikaftale. The 7h24m (26640 s) tier boundary currently encoded is an assumption, not agreement text.

This fork is the single biggest attribution difference: **the same Sunday hour is normal time for a stald praktikant and overtime for a field praktikant.**

## Overtime — 50 % / 80 %, and Sunday differs from ordinary workers

§ 50 stk. 4 c, verbatim:

> *"Overarbejde og arbejde på søn- og helligdage afregnes med et tillæg til praktikantens normaltimeløn på **50 % for de første 2 timer og herefter 80 %** eller tilsvarende frihed. Overarbejde afspadseres eller betales med overarbejdsbetaling **efter praktikantens ønske**."*

Note this is *worse in structure* than for ordinary staff on Sundays: § 22 gives ordinary workers +80 % from hour one on søn-/helligdage, whereas a praktikant gets +50 % for the first two hours first. Overtime is **not** restricted for trainees, and the afspadsering/payment choice is the trainee's.

## Supplements

- **Staldarbejde tillæg (§ 50 stk. 4 d)** — praktikanter have their *own* reduced two-item schedule: Saturday afternoon **per day**, Sunday/holiday **per day**. Both carry the limiter *"For arbejde **i normal arbejdstid**"*, so an overtime hour does not additionally trigger them.
- **No night item.** § 15 pays ordinary dyrehold workers for weekday 00:00–05:00; § 50 stk. 4 d has no equivalent. Since stald normal time may be placed around the clock, a praktikant milking at 03:00 is inside normal time with no supplement item — see open question 1 below.
- **Forskudttidstillæg (§ 23) is inherited unmodified** via stk. 7 (no trainee reduction). Relevant mainly to *andet arbejde*, whose normal time is confined to 06:00–18:00.

## Weekend / holiday / Grundlovsdag

| | Staldarbejde | Andet arbejde |
|---|---|---|
| Saturday before 12:00 | Normal time | Normal time (within 06–18) |
| Saturday after 12:00 | Normal time + per-day tillæg | Normal time until 18:00 |
| Sunday / helligdag | Can be **normal time** + per-day tillæg | Outside normal time → **overtime 50 % / 80 %** |

**Grundlovsdag is a half day** — § 29 stk. 1: *"Grundlovsdag er fridag **fra kl. 12.00**. For arbejde efter kl. 12.00 betales som for arbejde på en søgnehelligdag."* Minutes **before** 12:00 are ordinary working time. The same clause makes **24 December** a full fridag paid as a søgnehelligdag.

## Encoding audit — what the presets do vs what § 50 says

| # | Item | Encoded | Agreement | Impact on minute attribution |
|---|---|---|---|---|
| 1 | Weekday tiers 50 %/80 % | ✅ | ✅ | correct |
| 2 | Andet arbejde Saturday = weekday | ✅ | ✅ (Mon–Sat 06–18 is normal) | correct |
| 3 | Andet arbejde Sun/holiday all overtime | ✅ | ✅ (outside normal time) | correct |
| 4 | **Grundlovsdag whole day as søn-/helligdag** | both presets | ❌ half day — normal before 12:00 | **morning minutes mis-attributed** |
| 5 | **Staldarbejde Saturday/Sunday overtime** | never reachable | overtime exists (stk. 4 c) | **all Sat/Sun overtime minutes lost** — bands shadow the tiers |
| 6 | Stald Sat/Sun supplements as hourly codes | hourly | **per day** | N hours reported where the rule triggers once |
| 7 | Daily norm 7h24m | 26640 s | no daily norm in text | boundary between NORMAL and OVERTIME is an assumption |
| 8 | Stald 37 h averaged over **8 weeks** | not modelled | § 8 stk. 2 | weekly/period overtime cannot be detected at all |
| 9 | Pay codes shared with ordinary Dyrehold preset | `SAT_ANIMAL_AFTERNOON`, `ANIMAL_SUN_HOLIDAY` | praktikant amounts differ | not a minute-attribution issue, but downstream cannot tell the two apart from the code alone — every *other* trainee preset uses `ELEV_`-prefixed codes |

Item 5 is the most serious: for **Staldarbejde**, Saturday, Sunday and holidays declare time bands, so the router takes the band path and the tiers never execute. A praktikant working 12 hours on a Saturday is reported as `SAT_NORMAL` + `SAT_ANIMAL_AFTERNOON` with **zero overtime minutes**. Weekdays are unaffected (no bands). *Andet arbejde* is unaffected throughout (no bands at all).

## Test-coverage gap

The 14 existing praktikant tests in `ExpandedOverenskomstPayLineTests` call `PayLineGenerator.GeneratePayLines` and `GenerateTimeBandPayLines` **directly**, never through `CalculatePayLinesForDay`. Both paths therefore pass in isolation while telling you nothing about which one production takes. `PraktikantUdlStald_Saturday_TierPath_6hNormal_Then_AnimalAfternoon` asserts an outcome that **cannot occur in production** for that preset.

Any further work here needs end-to-end tests through the router, per day type, spanning the boundaries: below / exactly at / above the daily norm, the 12:00 Saturday split, the Grundlovsdag noon split, midnight-spanning stald shifts, and the andet-arbejde 06:00/18:00 window edges.

## Open questions (need GLS-A confirmation, not code changes)

1. **Night stald work for a praktikant.** § 50 stk. 4 d omits the § 15 night item, and 2026-2029's *"hvor andet ikke følger af § 50"* arguably displaces § 15 wholesale. Three defensible readings: § 15's night rate still applies; nothing applies; or § 23 applies. The 2024-2026 wording supported the first reading more cleanly.
2. **§ 23 forskudttid vs overtime for the same hour** — unresolved in the text for praktikanter *and* for ordinary workers.
3. **Rate-sheet typo**: GLS-A's overtime table says "7 - 12 måneders praktik" while the wage table and § 50 stk. 4 b say **7–18 months**. Present in both the 2025 and 2026 sheets; treat 7–18 as authoritative.

## Naming drift

Both praktikant presets are named `… 2026-2029` in the frontend catalogue while the C# fixtures still say `… 2024-2026`. Existing customer rows will carry whichever name was current when they were created — the same rename-without-migration mismatch that silently unlocked the GLS-A presets (fixed by normalising the trailing validity period when matching).

---

# "Normal daglig arbejdstid" — what sets the overtime boundary (decided 2026-08-07)

**Decision: the boundary stays 7,4 t (26640 s) by default. A planned day SHORTER than
7,4 t does NOT lower it.** Researched against the primary texts; this is the GLS-A
reading and it is what the presets encode.

## The asymmetry

§ 22 stk. 1 triggers overtime *"efter normal daglig arbejdstids ophør"* but **never
defines that figure numerically**. Searching the 2026-2029 agreement for
`planlagt arbejdstid`, `arbejdsplan`, `vagtplan`, `aftalt arbejdstid`: `vagtplan` and
`aftalt arbejdstid` do not occur at all, and the others appear **only inside opt-in
flexible regimes**, never in the ordinary regime § 22 governs.

| Planned day | Does the plan set the overtime line? | Authority |
|---|---|---|
| **Longer** than 7,4 t | **Yes** — but only within an opt-in regime, up to its ceiling | § 9 stk. 4 (40 t/uge banked), § 9 stk. 5 (premium only above **45** t/uge), protokollat stk. 1 (*"Ingen arbejdsdag kan være under 6 timer eller over 9,25 timer"*) |
| **Shorter** than 7,4 t | **No — no clause anywhere lowers it** | — |

Two clauses cut against lowering it:

- § 22 stk. 4: *"Ved opgørelse af overarbejde fradrages forsømt tid af **den normale
  ugentlige arbejdstid**…"* — the reckoning is anchored weekly.
- § 8 stk. 4 c (deltid): *"…så der ikke må ydes de beskæftigede nogen form for
  lønmæssig kompensation, **fordi arbejdstiden er kortere end den normale**."*

7,4 t is the agreement's own daily unit — § 9 stk. 2 reduces the weekly norm by
*"7,4 timer pr. dag"* for søgnehelligdage — though it is deployed there for SH
reduction rather than stated as the overtime trigger.

**For praktikanter the question is largely moot anyway.** The circular's monthly
figures divide out to **160,33 t/md** (13.315,41 ÷ 83,05; and 37 × 52 ÷ 12 = 160,33),
i.e. a praktikant is a **full-timer at 37 t/uge by definition**. A short planned day is
already paid inside that salary; the next hour draws against the same 37-hour week.

**Unsettled at the margin (honest limit):** because § 22 stk. 1 never defines the
figure, an employer with a fixed *written* daily schedule could argue that schedule is
the normal daily working time. No clause, protokollat, fortolkningsbidrag or published
voldgift resolves it. Worth putting to GLS-A in writing if a customer disputes it:
> *"For en udenlandsk praktikant på månedsløn, hvis den planlagte arbejdstid en given
> dag er kortere end 7,4 timer — hvornår begynder overarbejde: ved den planlagte
> arbejdstids ophør eller ved 7,4 timer?"*

Note the contrary pattern elsewhere in the Danish market: Industriens Overenskomst
§ 13 stk. 2 *defines* overtime as work outside *"den i den enkelte uge fastlagte
daglige arbejdstid for den enkelte medarbejder"* — plan-anchored. That is a different
agreement and does not govern here, but it explains why the plan-anchored reading is a
reasonable expectation.

## 296 timer / 8 uger is an average, not a weekly cap

The circular reads *"37 timer pr. uge **eller** 296 timer i en 8 ugers periode"*; the
agreement removes the ambiguity — § 8 stk. 2: *"indtil 37 timer **i gennemsnit** over en
periode på op til 8 uger."*

- A single 40-hour week inside the window creates **no** overtime by itself.
- 296 is **not a constant** — § 9 stk. 2 reduces the weekly norm by 7,4 t per
  søgnehelligdag falling in the window.
- Averaging the *weekly* norm does not license unpaid overrun of the *daily* one:
  § 22 stk. 1 still applies per day.
- Gap worth noting: § 8 stk. 2 imposes **no arbejdsplan requirement and no weekly
  ceiling**, unlike § 9 stk. 5 which demands a 3-week rolling plan and pays premium
  above 45 t/uge.

## Weekly netting (§ 22 stk. 4) — not yet modelled

> *"Ved opgørelse af overarbejde fradrages forsømt tid af den normale ugentlige
> arbejdstid, medmindre forsømmelsen skyldes en medarbejderen utilregnelig grund eller
> en grund, som er rettidigt anmeldt til arbejdsgiveren og godkendt af denne."*

Overtime is therefore **not** a pure per-day sum: culpable absence within the week is
netted off first. The two exceptions must be modellable per absence, or the netting
will wrongly erode overtime for sick or approved-absence workers. The engine has no
weekly context today, so this is unimplemented.

## Other praktikant details worth keeping

- **Afspadsering is the trainee's choice** — § 50 stk. 4 c: *"Overarbejde afspadseres
  eller betales med overarbejdsbetaling **efter praktikantens ønske**."*
- **Overtime is a multiple of the praktikant's own rate**, not the § 22 C-løn basis used
  for ordinary staff. Verified: 83,05 × 1,5 = 124,58 and × 1,8 = 149,49; same for the
  other two steps, exact to the øre.
- **Three defects in GLS-A's own circular** (marts 2026), all present in the published
  PDF: the overtime table is headed *"7 - 12 måneders praktik"* while carrying the
  **7–18** rates (confirmed by the arithmetic above); `15,446,19` has a comma where a
  period belongs (§ 50 stk. 4 b confirms **15.446,19**); and `ferieberettede` is a typo.

## Circular — Arbejdstid and Overarbejdsbetaling, verbatim

```
Overarbejdsbetaling            2 første timer hverdage
                               samt søn- og helligdage    Herudover 80 %
                                        +50 %
                                    Kr. pr. time            Kr. pr. time
0 - 6 måneders praktik                124,58                  149,49
7 - 12 måneders praktik               144,51                  173,41
7 - 12 måneders praktik (fyldt 25 år) 167,31                  200,77

Arbejdstid
Staldarbejde Den normale arbejdstid er indtil 37 timer pr. uge eller 296 timer i en 8 ugers
periode og kan lægges på alle ugens dage, hele døgnet.
Andet arbejde Den normale arbejdstid er indtil 37 timer pr. uge og kan lægges mandag til
lørdag mellem kl. 6.00 og 18.00.

Tillægsbetaling ved staldarbejde (inden for normal arbejdstid)          Kr. pr. dag
Lørdag efter kl. 12.00                                                        73,90
Søn- og helligdage                                                           177,60
```

Source: [GLS-A Lønoversigt, udenlandske praktikanter landbrug, marts 2026](https://www.gls-a.dk/wp-content/uploads/2026/04/Praktikanter-landbrug.pdf) ·
[Jordbrug 2026-2029](https://www.3f.dk/-/media/files/artikler/overenskomst/den-groenne-gruppe/overenskomster/4010---jordbrug-2026-2029---2,-d-,-udgave---06,-d-,07,-d-,26.pdf)

---

# IMPLEMENTATION STATUS — all overenskomst presets (audit 2026-08-07)

Every GLS-A / 3F preset was re-verified clause by clause against the primary
agreement PDFs (obtained and text-extracted, not summarised). **Verdict: the
presets are NOT true to the documents.** All 8 previously-listed discrepancies are
confirmed and 8 further categories were found.

Framing as always: this system reports **which rule each minute falls under**. A
wrong pay code is a wrong minute attribution, and since pay codes carry no
percentage mapping in the backend, the code string *is* the payload handed
downstream — `OVERTIME_80` where the text says 100 % is a real defect, not a naming
preference.

## Texts obtained

| Agreement | Edition verified against | Preset claims |
|---|---|---|
| Jordbrug 4010 | **2026-2029** (2. udg. 06.07.26) | 2026-2029 ✅ |
| Agroindustri 4012 | **2026-2029** (endelig 07.07.26) | 2026-2029 ✅ |
| Gartneri 4011 | 2024-2026 (no 2026-2029 published) | 2026-2029 ⚠️ |
| Skovbrug 4013 | 2024-2026 (no 2026-2029 published) | 2026-2029 ⚠️ |
| Golf 4014 | 2024-2026 (no 2026-2029 exists) | 2026-2029 ⚠️ |

⚠️ = the preset name claims an agreement period whose text has not been published.

## Status per preset family

| Family | Presets | Status |
|---|---|---|
| **Udenlandske praktikanter** (Stald, Andet) | 2 | ✅ **Corrected 2026-08-07.** Tiers, Saturday, Sunday and the § 29 Grundlovsdag noon split all match. Open: per-day vs per-hour supplement unit; pay codes shared with adult Dyrehold whose amounts differ |
| Jordbrug Standard / Dyrehold | 2 | ❌ fabricated Saturday supplement; Dyrehold band errors; flat Grundlovsdag |
| Jordbrug Elev u18 / o18 / u18 Dyrehold | 3 | ❌ wrong first tier (o18), missing top tier (u18), fabricated u18/o18 split |
| Gartneri Standard / Elev ×2 | 3 | ❌ Sunday not tiered; Saturday code; 1. maj missing; Elev tiers |
| Skovbrug Standard / Elev ×2 | 3 | ❌ Saturday should be overtime from hour 1; evening band 1 h too long; Elev Sunday step invented |
| Golf Standard / Elev | 2 | ❌ Saturday-afternoon code; Elev boundary; fridage missing |
| Agroindustri Standard ×8 / Elev ×8 | 16 | ❌ 3 wrong OT ceilings; Øvrige structurally wrong; no forskudt bands; fabricated Saturday split; **Elev variants have no textual basis at all** |

## Defects, ordered by payroll harm

### Workers under-attributed (minutes coded too low)

1. **Jordbrug Elev o18 first OT tier is 30 %; § 47 stk. 4 gives lærlinge 50 %.**
   Hits every hour 1–2 of daily overtime for every Jordbrug apprentice.
2. **All 14 "u18" presets are missing their top overtime tier.** Hour 3 onward stays
   at the first-tier rate forever (Jordbrug 50 vs 80; Gartneri/Skovbrug 50/30 vs
   100). Unbounded: the longer the day, the larger the error.
3. **Agro Kartoffelsortering** codes `OVERTIME_80`; the text has only 30 → **100**.
4. **Agro Gulerod** missing the 100 % tier, and its 80 % band should stop after
   overtime hour 3 (37440 s).
5. **Agro Minkfoder** missing the clock-keyed 100 % tier (overtime after 20:00; Sun/
   holiday after 12:00), and the Sunday 12:00 split.
6. **Skovbrug Saturday** gives 6 h "normal"; § 7 stk. 1 puts the week on
   *"ugens 5 første hverdage"*, so Saturday is the 30 %/100 % ladder from hour 1.

### Minutes over-attributed (supplements not earned)

7. **Saturday-afternoon supplement is fabricated** in Jordbrug Standard and all 8
   Agro Standard presets. An exhaustive search of "lørdag" finds Saturday
   supplements only for dyrehold (§ 15), lærling stald (§ 47 stk. 5) and praktikant
   (§ 50 stk. 4 d) — never for ordinary workers.
8. **Dyrehold 18:00–24:00 `SHIFTED_EVENING`** — § 23 caps displacement at 2 h after
   18:00, so 20:00–24:00 is unsupported (4 h/day).
9. **Dyrehold 05:00–06:00 `SHIFTED_MORNING`** — no such item in § 15.
10. **Skovbrug evening band 18:00–20:00** — § 22 allows *"indtil 1 time efter kl.
    18.00"*, so 19:00–20:00 is not entitled.
11. **Skovbrug Elev Sunday 50 % first-2h step is invented** — § 21 stk. 1 puts
    søn-/helligdage in the 100 % clause with no 50 % step.
12. **Gartneri Sunday/holiday flat** where § 22 stk. 2 tiers it 50 % (first 2 h) /
    100 % — first two hours over-coded, remainder under-coded.

### Structural / missing

13. **Agro Øvrige models percentages where § 4 a pays flat DKK** across clock bands
    (kr. 49,25 / 78,46 / 146,77, plus a separate Sunday scale). No minute maps to a
    correct code; needs a DKK-band model, not a tweak.
14. **Agroindustri encodes no forskudt bands at all**, though § 19 defines
    18:00–22:00 and 22:00–06:00. All displaced-time minutes across 16 presets are
    unattributed.
15. **Dyrehold Saturday 00:00–05:00 misses the § 15 night rate** — § 15 runs
    *"mandag til lørdag"* (5 h/week lost).
16. **Grundlovsdag is a flat all-day code on 37 of 39 presets.** § 29 makes it a
    fridag **from 12:00**; morning minutes are ordinary time. Only the two praktikant
    presets handle it.
17. **Per-day supplements encoded per-hour** — § 15 dyrehold, § 47 stk. 5 elev stald,
    § 50 stk. 4 d praktikant all say *"pr. dag"*. N hours reported where the rule
    triggers once.
18. **Unmodelled fridage**: 24 December (all families), 1 May (Gartneri 4 h @50 %
    then 100 %; Golf as søgnehelligdag; Agro kartoffelsortering fridag from 12:00),
    31 December (Gartneri, Agro kartoffelsortering).

### The Elev/lærling problem is bigger than a threshold

**The u18/o18 split is fabricated.** Each agreement has exactly **one** lærling
overtime rule — Jordbrug § 47 stk. 4 (50/80), Gartneri § 45 stk. 3 c (50/100),
Skovbrug § 44 stk. 9 (30/100) — or none at all. In Jordbrug the only "under 18 år"
occurrences concern *pension*; § 17 Ungarbejdere is a wage table. **Agroindustri
contains the word "lærling" zero times**, yet 8 Agro Elev presets are shipped.
The 8 h/day cap has no basis in any text.

## Engine-level defects that determine what surfaces

- **Weekday bands shadow the tiers** on the 8 "Standard" presets, so several tier
  errors above are currently invisible rather than harmless.
- **Presets are copy-at-create-time snapshots.** Correcting a preset does **not**
  change rule sets already created, and no migration exists. Any future correction
  needs a migration story *before* it ships — see the 2026-08-07 regression where a
  name-gated engine change met stale rows and moved up to 6 h/Saturday onto an
  afternoon supplement (fixed by additionally requiring the corrected tier shape).
- **Pay lines are never persisted**, so re-exporting an already-paid period reflects
  today's rules, not the rules it was paid under. There is no snapshot to reconcile.

## Recommended sequence

1. Decide the fate of the 14 Elev presets — correct to the single real lærling rule,
   or retire the u18/o18 split. Largest single source of under-attribution.
2. Fix the three Agro overtime ceilings (Kartoffelsortering, Gulerod, Minkfoder) —
   small, unambiguous, clearly quoted.
3. Remove the fabricated Saturday supplements (Jordbrug Standard, 8 Agro Standard)
   and fix Skovbrug Saturday.
4. Model Grundlovsdag's noon split generally (needs a `DayType` for it in the base
   package) plus the other fridage.
5. Re-model Agro Øvrige on flat DKK bands.
6. Then the per-day supplement unit, and Agro forskudt bands.

Each step needs a data migration for existing rule sets, not just a catalogue edit.
