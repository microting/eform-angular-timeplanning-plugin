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
