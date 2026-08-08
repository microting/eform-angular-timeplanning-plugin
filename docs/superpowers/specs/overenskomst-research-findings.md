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
- **OT:** NOT one ladder [corrected 2026-08-08, see ledger G-002]: § 18 stk. 1 defers all
  rates to the eight kapitel-21 lønbilag, which carry five different ladders —
  grovvarehandler 40%, fjerkræ/kartoffelmel 30%/50%/100%, kartoffelsortercentraler
  30%/100% (no 80% tier), gulerodspakkerier 30%/80%/100%, lucerne 30%/80% (no 100%),
  øvrige flat DKK per klokketime. Only three of eight resemble Jordbrug's 30/80.
- **Normal:** 37h/week, Mon-Sat 06:00-18:00 (confirmed, agroindustri-2026-2029.txt:232, 293-294)
- **Fit:** Per-lønbilag encodings required; "same structure as Jordbrug" was wrong
- **Variants needed:** Standard, Elev u18, Elev o18
- **Source:** [Agroindustri 2024-2026 PDF](https://www.3f.dk/-/media/files/artikler/overenskomst/den-groenne-gruppe/overenskomster/4012---agroindustri--2024-2026---endelig-17,-d-,05,-d-,24.pdf) — the "2024-2026" period claim is unverified (no 2024-2026 text in the audit corpus; verified edition is 2026-2029)

#### Golf (#4014, 2024-2026)
- **Parties:** GLS-A / 3F Den Gronne Gruppe
- **Covers:** Golf course workers (greenkeepers, maintenance)
- **OT:** Flat 100% supplement with no first-tier step (golf-2024-2026.txt:472-476) —
  NOT Gartneri-like 50%/100% [corrected 2026-08-08, see ledger G-003]. The shipped
  single-tier 100% preset matches the text; the error was this doc's comparison.
- **Normal:** 37h/week averaged over up to 52 weeks (confirmed, golf-2024-2026.txt:236-237)
- **Fit:** Good - shipped presets match the 2024-2026 text
- **Variants needed:** Standard, Elev
- **Source:** [Golf 2024-2026 PDF](https://www.3f.dk/-/media/files/artikler/overenskomst/den-groenne-gruppe/overenskomster/4014---golf-2024-2026---endelig-30,-d-,05,-d-,24.pdf)

#### Fiskeopdraet, -slagterier og -foraedling (Fish farming/processing)
- **Parties:** GLS-A / 3F
- **Covers:** Fish farming, fish slaughterhouses, fish processing
- **OT:** Need to verify - likely similar to Agroindustri
- **Period:** 2024-2026
- **Fit:** Good - hourly workers with standard OT tiers
- **Source:** [GLS-A Overenskomster](https://www.gls-a.dk/overenskomst/)
- *Unverified: agreement text not in the audit corpus [2026-08-08, see ledger G-004]*

#### GASA Sortering og Pakning (Sorting & Packing)
- **Parties:** GLS-A / 3F
- **Covers:** Sorting and packing of agricultural produce
- **OT:** Need to verify
- **Period:** 2024-2026
- **Fit:** Good - production workers
- *Unverified: agreement text not in the audit corpus [2026-08-08, see ledger G-005]*

#### GASA Transport
- **Parties:** GLS-A / 3F
- **Covers:** Transport of agricultural produce
- **OT:** Need to verify
- **Period:** 2024-2026
- **Source:** [GASA Transport 2024-2026 PDF](https://www.gls-a.dk/wp-content/uploads/2024/07/4016-GASA-TRANSPORT-2024-2026-endelig-05.07.24.pdf)
- *Unverified: agreement text not in the audit corpus [2026-08-08, see ledger G-006]*

#### Holddrift (Shift work agreement)
- **Parties:** GLS-A / 3F (Den Grønne Gruppe) — never Dansk Metal; the incorporation
  clauses in all four full agreements name only GLS-A/3F, and "Metal" has zero hits in
  the corpus [corrected 2026-08-08, see ledger G-007]
- **Covers:** Shift workers across GLS-A sectors (incorporated by reference into
  Jordbrug, Gartneri, Skovbrug and Agroindustri; not Golf)
- **Structure:** Shift supplements coexist WITH overtime tiers, not instead of them —
  overtime draws its own overtidstillæg on top of the holddriftstillæg
  (ratesheet-holddriftstillaeg-2026.txt:26-27) [corrected 2026-08-08, see ledger G-007].
  Weekly norm differs per shift: 1. skift 37 t, 2./3. skift 34 t (see ledger M-021).
- **Period:** persisted rate sheet covers 1 March 2026 - 28 February 2027; the
  agreement text itself is not in the audit corpus
- **Fit:** Needs shift-category norms, clock windows (weekday 17:00-06:00, Saturday
  14:00 → end of Sunday) and roster-based fridag categories — see ledger M-021..M-023
- **Source:** [GLS-A Holddrift 2024-2026](https://www.danskmetal.dk/pjecer-og-udgivelser/overenskomst-gls-a-holddrift-2024-2026)

#### GLS-A / Dansk Metal
- **Parties:** GLS-A / Dansk Metal
- **Covers:** Metal workers in agriculture (mechanics, technicians)
- **OT:** Need to verify - likely follows Dansk Metal patterns
- **Period:** 2024-2026
- **Source:** [GLS-A Dansk Metal](https://www.danskmetal.dk/overenskomster/andre-brancher/gls-a)
- *Unverified: agreement text not in the audit corpus [2026-08-08, see ledger G-008]*

#### GLS-A / HK
- **Parties:** GLS-A / HK Privat
- **Covers:** Office/administrative staff in agricultural companies
- **OT:** Salaried (funktionaer) - no hourly OT tiers
- **Fit:** Poor - salaried workers don't use our tier system
- **Source:** [HK GLS-A](https://www.hk.dk/raadogstoette/vaerktoejer/overenskomster/privat/11400/11380_gartneri-land-og-skovbrug)
- *Unverified: agreement text not in the audit corpus [2026-08-08, see ledger G-009]*

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

The signing date 25 February 2026, effect 1 March 2026 and minimum term through
1 March 2029 are confirmed in the published Jordbrug and Agroindustri 2026-2029 texts
only. "All GLS-A families in a single combined settlement" is not supported: the
Gartneri and Skovbrug 2026-2029 texts are unpublished, and GLS-A's OK26 news does not
mention Golf at all [corrected 2026-08-08, see ledger G-010].

### Per-family verification status:
- **Jordbrug 2026-2029** — full text published (gls-a.dk, 4010, 2. udgave 06.07.26).
  Confirmed in the 2026-2029 text: overtime 30%/80%, dyrehold bands (§ 15), praktikant
  50%/80%, Grundlovsdag (§ 29). The "7h24m/9h24m cutoffs" are NOT agreement text — the
  agreement states no clock-duration cutoffs; 26640/33840 s are engine-side derivations
  of 37÷5 [corrected 2026-08-08, see ledger G-011]. "Unchanged vs 2024-2026" is
  unverified (no 2024-2026 text in the audit corpus).
- **Agroindustri 2026-2029** — full text published (4012, 07.07.26). The
  "word-for-word identical to 2024-2026" claim is unverified — no 2024-2026 text in
  the audit corpus [2026-08-08, see ledger G-012].
- **Golf** — NO 2026-2029 edition exists at all: both GLS-A's and 3F's listings still
  show 2024-2026 as current, GLS-A's OK26 news never mentions Golf, and the March-2026
  Golf lønoversigt this doc cited is not in the corpus (the only persisted marts-2026
  circular is the landbrug wage sheet, which mentions neither Golf nor any
  overenskomstperiode) [corrected 2026-08-08, see ledger G-013, G-084]. The Golf
  presets' "2026-2029" name claims a period whose text does not exist.
- **Gartneri / Skovbrug 2026-2029** — full texts not published. The "covered by the
  same signed settlement" half has partial corroboration (GLS-A's OK26 news covers
  Gartneri; a signed Feb-2026 protocol exists for Skovbrug), but the load-bearing
  Protokollat 23 "beregnes som hidtil" wording is absent from every persisted source —
  unverifiable until the texts publish [2026-08-08, see ledger G-014].

### Pre-existing encoding discrepancies (open verification questions for second-opinion review against primary texts)

These mismatches were verified 2026-08-08 against the persisted texts (Jordbrug/Agro
2026-2029; Gartneri/Skovbrug/Golf 2024-2026 — the "exist against both editions" framing
is unverifiable where a prior or successor edition is not in the corpus, see ledger
G-015). Verdicts: items 2, 3, 5, 6, 7 CONFIRMED; items 1, 4, 8 partially refuted as
noted [corrected 2026-08-08, see ledger G-016..G-023]:

1. Agro **Gulerodspakkerier**: text has 30% (h1-2) / 80% (h3) / 100% (beyond + Sun/Hol); preset ends at 80%. Confirmed for `-standard` only — the `-elev` twin has no 80% band at all (single unbounded 30% tier), so the remediation applies to one preset, not two [see ledger G-016].
2. Agro **Kartoffelsortering**: text has 30% → 100% (no 80% tier); preset has 30% → 80%.
3. Agro **Minkfoder**: text has a third tier at 100% keyed to clock time (after 20:00; Sun after 12:00); preset ends at 80%.
4. Agro **Øvrige**: text uses flat-DKK supplements across three clock-hour bands + a separate two-band Sunday scale; preset models 30%→80% percentages. The `-standard` half is confirmed; the `-elev` twin models a single unbounded 30% tier, a different wrong shape — and § 4 also has a pre-shift two-band scale (§ 4 b) and a hverdagsfridag scale (§ 4 c) that no preset touches [see ledger G-019, M-019, M-020].
5. **Skovbrug** evening forskudt-tid band: text allows 18:00-19:00 (1h); preset encodes 18:00-20:00.
6. **Skovbrug** Saturday: text treats all Saturday work as overtime from hour 1; preset gives 6h "normal" first.
7. **Gartneri** Sunday/holiday: text tiers it 50% (first 2h) / 100%; preset uses a single all-day SUN_HOLIDAY code.
8. **Elev u18 8h/day threshold** (Gartneri/Skovbrug/Golf/Agro): no basis in the overenskomst texts as a *pay* threshold — but the framing "likely from statutory youth-work rules" was itself unevidenced; the audit found no standalone daily 8-hour clause in any of the five texts (the only "8 timer" hits are substrings of "48 timer") and the statutory-origin hypothesis remains exactly that [corrected 2026-08-08, see ledger G-023].

---

# Overarbejde × forskudt tid: how the two interact (research 2026-08-07)

**Scope note.** This system reports **how many minutes of a work period fall under which rule**. It does not compute money. Everything below is therefore framed as *minute attribution*; rates are quoted only where they prove which rule an hour belongs to.

## There is no universal Danish rule — it is per agreement

Researched against primary agreement PDFs. Within the persisted GLS-A texts, the
attested patterns are stacking, not-cumulative and silence; the KA/Krifa exemplar
below is an out-of-corpus comparator, not persisted primary text
[corrected 2026-08-08, see ledger G-024]:

| Agreement | Overtime hour inside a displaced-time band | Evidence |
|---|---|---|
| **GLS-A Agroindustri (§ 19 stk. 4)** and **Holddrift** | **Cumulative — both** | § 19 stk. 4: overtime in tilslutning to forskudt arbejdstid draws the overtidstillæg *in addition to* the forskudt supplement (agroindustri-2026-2029.txt:672-673); same principle in the Holddrift sheet (ratesheet-holddriftstillaeg-2026.txt:26-27). |
| **KA / Krifa** (Landbrug svine/kvæg, plantebrug, maskinstation; Grøn) — *comparator, text not in audit corpus* | **Cumulative — both** | Hovedoverenskomst § 16 stk. 5: *"Udføres overarbejde på særlige tidspunkter … betales **foruden overtidstillæg også tillæg for arbejde på særlige tidspunkter**."* |
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

- **Dyrehold 18:00–24:00 `SHIFTED_EVENING` is over-broad**: § 23 caps displacement at
  2 h after 18:00, so 20:00–24:00 is unsupported (4 h/day). The 05:00–06:00
  `SHIFTED_MORNING` band, however, is NOT baseless — its basis is § 23 forskudt
  arbejdstid (2 h window before 06:00, jordbrug-2026-2029.txt:783), not § 15; on
  Dyrehold it is truncated to 05:00–06:00 only because 00:00–05:00 is already the § 15
  ANIMAL_NIGHT band [corrected 2026-08-08, see ledger G-031, G-032].
- **§ 15 Saturday-afternoon and Sunday/holiday supplements are per *day*, not per hour**
  (its weekday/Saturday night item is expressly "pr. time" — § 15 is mixed). The
  agreement-text half is confirmed; whether the presets "encode them as hourly" is not
  decidable in-engine — no rule or pay-line entity carries a rate or unit field, so the
  per-day-vs-per-hour distinction lives downstream [corrected 2026-08-08, see ledger
  G-033; capability gap recorded as E11 in the proposed-encodings doc].
- **The 9h24m tier boundary is an assumption.** § 22 keys off *"efter den normale daglige arbejdstids **ophør**"* — the end of the actual scheduled day. It equals 9h24m only when that day is exactly 7,4 h; under § 9 stk. 5 varying hours and the alternative-scheduling protocol (*"Ingen arbejdsdag … over 9,25 timer"*) it does not.

## Sources

**Canonical manifest: `sources/SOURCES.md`** — the persisted, hash-verified corpus this
doc's GLS-A claims are checkable against (retrieved 2026-08-08): Jordbrug 2026-2029 ·
Gartneri 2024-2026 · Skovbrug 2024-2026 · Golf 2024-2026 · Agroindustri 2026-2029 ·
Lønoversigt Landbrugsarbejde marts 2026 · Holddriftstillæg sheet 2026-2027. Documents
cited below but NOT persisted (claims resting solely on them are marked unverifiable):
Jordbrug 2024-2026 / 2021-2024 · KA/Krifa Overenskomst 2025-2028 (krifa.dk) · KA
fagoverenskomst Landbrug og Grøn (ka.dk) · Industriens Overenskomst 2025-2028
(co-industri.dk) · Anlægsgartnerarbejde 2025-2028 (3f.dk) · KL Aftale 04.89 (kl.dk).

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

**No clause states a daily overtime-trigger figure** — only the placement window. So "overarbejde efter den normale arbejdstids ophør" has no fixed daily trigger for a praktikant; it depends on the individual praktikaftale. Note the narrowing [corrected 2026-08-08, see ledger G-042, G-043]: "7,4 timer" IS literal agreement text — three times, always as the søgnehelligdag/fridag reduction unit (37÷5), never as an overtime trigger. The encoded 26640 s boundary is a borrowed SH-reduction figure that no clause adopts as the praktikant daily overtime boundary — not an invented number, but not a stated trigger either.

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
| 4 | **Grundlovsdag noon split** | handled by BOTH praktikant presets since 2026-08-07 (weekday-shaped tiers + gated noon-split path) — the flat whole-day pattern belongs to the other 37 presets, not these two [corrected 2026-08-08, see ledger G-058, G-094] | § 29: half day — normal before 12:00 | correct for the two praktikant presets |
| 5 | **Staldarbejde Saturday/Sunday overtime** | reachable since 2026-08-07: the name+shape-gated split lets bands attribute normal time while tiers 2-3 take the overflow [corrected 2026-08-08, see ledger G-059, G-085] | overtime exists (stk. 4 c) | correct post-correction; the "bands shadow the tiers" description is historical |
| 6 | Stald Sat/Sun supplements as hourly codes | unit not decidable in-engine (no rate/unit field on rules or pay lines) [corrected 2026-08-08, see ledger G-033] | **per day** (night item is per hour — § 15 is mixed) | per-day vs per-hour is a downstream payroll decision; engine gap E11 |
| 7 | Daily norm 7h24m | 26640 s | no daily overtime-trigger figure in text (7,4 t exists only as the SH-reduction unit) [see ledger G-043] | boundary between NORMAL and OVERTIME is an assumption |
| 8 | Stald 37 h averaged over **8 weeks** | not modelled | § 8 stk. 2 | weekly/period overtime cannot be detected at all |
| 9 | Pay codes shared with ordinary Dyrehold preset | `SAT_ANIMAL_AFTERNOON`, `ANIMAL_SUN_HOLIDAY` | praktikant amounts differ | not a minute-attribution issue, but downstream cannot tell the two apart from the code alone — every *other* trainee preset uses `ELEV_`-prefixed codes |

Item 5 *was* the most serious defect and is FIXED as of 2026-08-07 [corrected 2026-08-08, see ledger G-059, G-085]: the corrected tiers plus the identity+shape-gated split mean a praktikant working 12 hours on a Saturday now gets band-attributed normal time until the 26640 s boundary and OVERTIME_50/OVERTIME_80 beyond it. The historical failure mode (band path shadowing the tiers, zero overtime minutes) still describes the OTHER banded presets — see the engine-level defects section. Data migration for pre-correction customer rows shipped 2026-08-07 (eform-timeplanning-base v10.0.57).

## Test-coverage gap

The 14 existing praktikant tests in `ExpandedOverenskomstPayLineTests` call `PayLineGenerator.GeneratePayLines` and `GenerateTimeBandPayLines` **directly**, never through `CalculatePayLinesForDay`. Both paths therefore pass in isolation while telling you nothing about which one production takes. `PraktikantUdlStald_Saturday_TierPath_6hNormal_Then_AnimalAfternoon` asserts an outcome that **cannot occur in production** for that preset.

Any further work here needs end-to-end tests through the router, per day type, spanning the boundaries: below / exactly at / above the daily norm, the 12:00 Saturday split, the Grundlovsdag noon split, midnight-spanning stald shifts, and the andet-arbejde 06:00/18:00 window edges.

## Open questions (need GLS-A confirmation, not code changes)

1. **Night stald work for a praktikant.** § 50 stk. 4 d omits the § 15 night item, and 2026-2029's *"hvor andet ikke følger af § 50"* arguably displaces § 15 wholesale. Three defensible readings: § 15's night rate still applies; nothing applies; or § 23 applies. The 2024-2026 wording supported the first reading more cleanly.
2. **§ 23 forskudttid vs overtime for the same hour** — unresolved in the Jordbrug text for praktikanter *and* for ordinary workers. NOT unresolved GLS-A-wide: Agroindustri § 19 stk. 4 settles the same question explicitly in favour of stacking [corrected 2026-08-08, see ledger G-063].
3. ~~Rate-sheet typo "7 - 12 måneders praktik"~~ — REFUTED: the persisted marts-2026 circular contains no praktikant overtime table at all; the alleged heading does not exist there. The 2025 sheet is not in the corpus, so that half is untestable. § 50 stk. 4 b's **7–18 months** stands as authoritative [corrected 2026-08-08, see ledger G-064].
4. **Is § 22's Sunday +80% really from hour one for ordinary workers?** The praktikant-is-worse comparison rests on it, and the text supports two readings — tagged `[open question for GLS-A]` [2026-08-08, see ledger G-046].
5. **Does § 15's night rate survive § 50 stk. 7 for praktikanter?** Three defensible readings (see question 1) — tagged `[open question for GLS-A]` [2026-08-08, see ledger G-062].

## Naming drift

Both praktikant presets are named `… 2026-2029` in the frontend catalogue while the C# fixtures still say `… 2024-2026`. Existing customer rows will carry whichever name was current when they were created — the same rename-without-migration mismatch that silently unlocked the GLS-A presets (fixed by normalising the trailing validity period when matching).

---

# "Normal daglig arbejdstid" — what sets the overtime boundary (decided 2026-08-07)

**Decision: the boundary stays 7,4 t (26640 s) by default. A planned day SHORTER than
7,4 t does NOT lower it by default.** Two of the decision's original justifications
were overclaims [corrected 2026-08-08, see ledger G-065]: § 22 stk. 1 states no figure,
so 7,4 t is an interpretive convention layered on the § 8 stk. 1 weekly norm (37÷5),
textually attested only as the SH-reduction unit; and "it is what the presets encode"
is false as a blanket — 26640 s is tier 1 on the adult Standard/Dyrehold presets and
the three o18 Elev presets, while the 13 presets carrying the 28800 s first-tier shape
(all *-elev-u18 plus Golf Elev and the 8 Agro Elev) use 28800 s. The decision may still
be the right default; it is not agreement text and it is not universal in the presets.

## The asymmetry

§ 22 stk. 1 triggers overtime *"efter normal daglig arbejdstids ophør"* but **never
defines that figure numerically**. Searching the 2026-2029 agreement for
`planlagt arbejdstid`, `arbejdsplan`, `vagtplan`, `aftalt arbejdstid`: `vagtplan` and
`aftalt arbejdstid` do not occur at all, and the others appear **only inside opt-in
flexible regimes**, never in the ordinary regime § 22 governs.

| Planned day | Does the plan set the overtime line? | Authority |
|---|---|---|
| **Longer** than 7,4 t | **Yes — but only the alternativ-arbejdstid protokollat actually redefines a per-day range.** § 9 stk. 4 is a weekly 40 t banking scheme settled by afspadsering (it defers an overtime line, doesn't set one) and § 9 stk. 5 sets a weekly 45 t premium threshold independent of any single day [corrected 2026-08-08, see ledger G-067] | protokollat stk. 1 (*"Ingen arbejdsdag kan være under 6 timer eller over 9,25 timer"*); § 9 stk. 4/5 are weekly mechanisms |
| **Shorter** than 7,4 t | **Not by default — but "no clause anywhere" was too absolute**: § 52 Overenskomstfravigende lokalaftaler expressly permits written local derogations from the arbejdstid provisions, and § 9 stk. 9 routes arbejdstid derogations there [corrected 2026-08-08, see ledger G-068] | § 52; § 9 stk. 9 |

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
- ~~Three defects in GLS-A's own circular~~ — REFUTED [corrected 2026-08-08, see ledger
  G-064, G-081]: none of the three alleged defects exists in the persisted marts-2026
  circular. It contains no praktikant overtime table, no "7 - 12 måneders praktik"
  heading, no "15,446,19" (that figure lives only in § 50 stk. 4 b of the agreement,
  correctly punctuated), and "ferieberettigede" is spelled correctly throughout. The
  figures previously attributed to the circular are § 50 agreement figures.

## Circular — Arbejdstid and Overarbejdsbetaling — MISATTRIBUTED, kept for the record

The block below was previously presented as a verbatim transcription of the marts-2026
circular. It is NOT [corrected 2026-08-08, see ledger G-082]: the persisted circular
contains no such Overarbejdsbetaling table and no 73,90/177,60 staldarbejde figures.
The overtime figures are derivable from § 50's hourly rates × 1,5 / × 1,8, and
73,90/177,60 are § 50 stk. 4 d agreement figures (jordbrug-2026-2029.txt:2216, 2221).
The Arbejdstid wording matches the agreement (§ 50 via §§ 8-9), not a circular page.

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

> **Adversarially verified 2026-08-08 against persisted sources — see
> `2026-08-08-glsa-verification-ledger.md`** (98 claims: 52 confirmed, 28 refuted,
> 18 unverifiable; plus 23 missing rules M-001..M-023). Proposed corrections and
> engine gaps: `2026-08-08-glsa-proposed-encodings.md`. Sources: `sources/SOURCES.md`.

Every GLS-A / 3F preset was re-verified clause by clause against primary agreement
PDFs (obtained and text-extracted, not summarised) — for Jordbrug and Agroindustri
the 2026-2029 editions; for Gartneri, Skovbrug and Golf necessarily the 2024-2026
editions, since no 2026-2029 texts exist [corrected 2026-08-08, see ledger G-083].
**Verdict: the presets are NOT true to the documents.** 18 defects are enumerated
below (the earlier "8 confirmed + 8 further" arithmetic did not match its own list);
the 2026-08-08 verification confirmed 12 of the 18 as filed, narrowed or refuted 6
(defects 2, 4, 7, 9, 13-as-to-elev, 17 — see the list), removed one false defect
(Golf Saturday code), and found 23 further missing rules.

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
| **Udenlandske praktikanter** (Stald, Andet) | 2 | ✅ **Corrected 2026-08-07.** Tiers, Saturday, Sunday and the § 29 Grundlovsdag noon split all match. **Data migration shipped 2026-08-07** (eform-timeplanning-base `CorrectPraktikantSection50Tiers`, v10.0.57): existing customer rule sets under either validity-period name are rewritten to the corrected tiers idempotently, so pre-correction rows now take the split path and earn overtime like freshly created ones. Open: per-day vs per-hour supplement unit; pay codes shared with adult Dyrehold whose amounts differ |
| Jordbrug Standard / Dyrehold | 2 | ❌ fabricated Saturday supplement (default regime — the opt-in alternativ-arbejdstid protokollat DOES pay any worker a Saturday kr/time rate, see ledger G-090); Dyrehold evening band over-broad (morning band has § 23 basis — defect 9 refuted, see ledger G-031/G-032); flat Grundlovsdag. Missing: kapitel 22 sector ladders (frugtplantager, fjerkræproduktion, minkfarme) have NO presets at all — ledger M-007..M-009 |
| Jordbrug Elev u18 / o18 / u18 Dyrehold | 3 | ❌ wrong first tier (o18), missing top tier (u18 — WEEKDAY and SATURDAY), fabricated u18/o18 split |
| Gartneri Standard / Elev ×2 | 3 | ❌ Sunday not tiered; Saturday-afternoon code baseless on `-standard` and `-elev-o18` (u18 already uses the tiered code) [see ledger G-086]; 1. maj missing; Elev tiers. Missing: detailsalg § 14 bands, deltid § 11, turnus notice § 22 stk. 2, anlægsgartner carve-out § 2 — ledger M-010, M-012..M-014 |
| Skovbrug Standard / Elev ×2 | 3 | ❌ Saturday should be overtime from hour 1; evening band 1 h too long; Elev Sunday step invented |
| Golf Standard / Elev | 2 | ❌ Elev boundary; fridage missing. The previously listed "Saturday-afternoon code" defect was FALSE — the preset matches the text exactly (normal Saturday 06:00-12:00, 100% after noon) [corrected 2026-08-08, see ledger G-087] |
| Agroindustri Standard ×8 / Elev ×8 | 16 | ❌ 3 wrong OT ceilings (Gulerod's applies to `-standard` only, see ledger G-016); Øvrige structurally wrong (incl. unencoded pre-shift § 4 b and hverdagsfridag § 4 c scales); no forskudt bands; fabricated Saturday split; **Elev variants have no textual basis at all**. Missing: grovvare pre-shift tier + conditional ladder, deltid § 5 stk. 3 d, Weekendarbejde § 7 stk. 4 — ledger M-015..M-020 |
| *Cross-family regimes (no presets)* | — | Flekstid, fastløn, alternativ arbejdstidsplanlægning (stk. 1 + stk. 3), varierende ugentlig arbejdstid, Holddrift shift norms/windows/fridage — ledger M-001, M-003, M-005/M-006, M-011, M-021..M-023 |

## Defects, ordered by payroll harm

### Workers under-attributed (minutes coded too low)

1. **Jordbrug Elev o18 first OT tier is 30 %; § 47 stk. 4 gives lærlinge 50 %.**
   Hits every hour 1–2 of daily overtime for every Jordbrug apprentice.
2. **The missing-top-tier defect holds for exactly 4 presets, not 14** [corrected
   2026-08-08, see ledger G-089]: 13 GLS-A presets carry the 28800 s two-tier shape,
   and only `glsa-jordbrug-elev-u18`, `glsa-jordbrug-elev-u18-dyrehold` (§ 47 stk. 4,
   50→80), `glsa-gartneri-elev-u18` (§ 45 stk. 3 c, 50→100) and
   `glsa-skovbrug-elev-u18` (§ 44 stk. 9, 30→100) are governed by an apprentice
   overtime clause — on WEEKDAY and SATURDAY alike. The other 9 (`glsa-golf-elev` +
   8 `glsa-agro-*-elev`) have no apprentice overtime clause at all and are
   fabrications (defect: the whole preset, not a tier — see the Elev section).
3. **Agro Kartoffelsortering** codes `OVERTIME_80`; the text has only 30 → **100**.
4. **Agro Gulerod `-standard`** missing the 100 % tier, and its 80 % band should stop
   after overtime hour 3 (37440 s). Applies to the standard preset only — the `-elev`
   twin has no 80 % band to bound [corrected 2026-08-08, see ledger G-016].
5. **Agro Minkfoder** missing the clock-keyed 100 % tier (overtime after 20:00; Sun/
   holiday after 12:00), and the Sunday 12:00 split.
6. **Skovbrug Saturday** gives 6 h "normal"; § 7 stk. 1 puts the week on
   *"ugens 5 første hverdage"*, so Saturday is the 30 %/100 % ladder from hour 1.

### Minutes over-attributed (supplements not earned)

7. **Saturday-afternoon supplement is fabricated** in Jordbrug Standard and all 8
   Agro Standard presets — for the DEFAULT kapitel-3 regime those presets model. The
   original "never for ordinary workers" was too absolute [corrected 2026-08-08, see
   ledger G-090]: the opt-in alternativ-arbejdstidsplanlægning protokollat stk. 4 a
   pays ANY medarbejder kr. 82,90/time on Saturdays, and Holddrift pays a Saturday
   window rate. In the default regime, Saturday supplements exist only for dyrehold
   (§ 15), lærling stald (§ 47 stk. 5), § 49 stk. 4 and praktikant (§ 50 stk. 4 d) —
   so the defect and its remediation stand unchanged.
8. **Dyrehold 18:00–24:00 `SHIFTED_EVENING`** — § 23 caps displacement at 2 h after
   18:00, so 20:00–24:00 is unsupported (4 h/day).
9. ~~Dyrehold 05:00–06:00 `SHIFTED_MORNING` — no such item in § 15~~ — REFUTED
   [corrected 2026-08-08, see ledger G-031]: the band's basis is § 23 forskudt
   arbejdstid (2 h window before 06:00), not § 15; on Dyrehold it is truncated to
   05:00–06:00 because 00:00–05:00 is already the § 15 ANIMAL_NIGHT band. Not a
   defect. (The second cited preset, `glsa-jordbrug-elev-u18-dyrehold`, declares no
   bands at all, so the claim never applied there.)
10. **Skovbrug evening band 18:00–20:00** — § 22 allows *"indtil 1 time efter kl.
    18.00"*, so 19:00–20:00 is not entitled.
11. **Skovbrug Elev Sunday 50 % first-2h step is invented** — § 21 stk. 1 puts
    søn-/helligdage in the 100 % clause with no 50 % step.
12. **Gartneri Sunday/holiday flat** where § 22 stk. 2 tiers it 50 % (first 2 h) /
    100 % — first two hours over-coded, remainder under-coded.

### Structural / missing

13. **Agro Øvrige models percentages where § 4 a pays flat DKK** across clock bands
    (kr. 49,25 / 78,46 / 146,77, plus a separate Sunday scale). No minute maps to a
    correct code; needs a DKK-band model, not a tweak. The `-elev` twin is wrong in a
    different way (single unbounded 30 % tier), and § 4 b (pre-shift) and § 4 c
    (hverdagsfridag) scales are additionally unencoded [see ledger G-019, M-019, M-020].
14. **Agroindustri encodes no forskudt bands at all**, though § 19 defines
    18:00–22:00 and 22:00–06:00. All displaced-time minutes across 16 presets are
    unattributed.
15. **Dyrehold Saturday 00:00–05:00 misses the § 15 night rate** — § 15 runs
    *"mandag til lørdag"* (5 h/week lost).
16. **Grundlovsdag is a flat all-day code on 37 of 39 presets.** § 29 makes it a
    fridag **from 12:00**; morning minutes are ordinary time. Only the two praktikant
    presets handle it. (Count precision: 29 of 31 `glsa-*` presets verified flat via
    CODE-TRUTH; the 8 `ka-*` presets are asserted flat from the catalogue but sit
    outside the audited corpus [see ledger G-094].)
17. **Per-day supplements: the texts say "pr. dag"** (§ 15 dyrehold Saturday/Sunday
    items, § 47 stk. 5 elev stald, § 50 stk. 4 d praktikant; § 15's night item is
    expressly "pr. time"). Whether the presets mis-encode this is NOT decidable
    in-engine — no rule or pay-line entity carries a rate or unit field, so per-day
    vs per-hour is a downstream payroll decision; the gap is the missing
    unit-of-measure capability (E11) [corrected 2026-08-08, see ledger G-033].
18. **Unmodelled fridage**: 24 December (all families), 1 May (Gartneri 4 h @50 %
    then 100 %; Golf as søgnehelligdag; Agro kartoffelsortering fridag from 12:00),
    31 December (Gartneri, Agro kartoffelsortering).

### Missing rules found by the 2026-08-08 completeness sweep

[added 2026-08-08, see ledger M-001..M-023] — full-text sweeps of all five agreements
plus the rate sheets found 23 pay-relevant rules absent from BOTH the presets and this
doc's earlier findings. Highest-surface first:

- **Jordbrug kapitel 22 sector ladders have no presets at all** (M-007..M-009):
  frugtplantager (30/50/100 with a søn-/helligdag NOON split on ordinary weekends),
  fjerkræproduktion (30/50/100 + a task-conditional 80 % override for fodring/æg
  lørdag-søn-/helligdage), minkfarme (clock-keyed 30/80/100). § 16 routes this work
  to kapitel 22; today it silently falls on `glsa-jordbrug-standard`'s 30/80.
- **Opt-in regimes that reroute attribution** (M-001 flekstid, M-003 fastløn, M-005/
  M-006 alternativ arbejdstidsplanlægning incl. the 5×24 h plan-change penalty, M-016
  Agro Weekendarbejde which *suppresses* søgnehelligdagsforskud).
- **Worker-category variants**: deltid (Gartneri § 11 = M-012; Agro § 5 stk. 3 d =
  M-015), detailsalg bands with lærling/ungarbejder percentages (M-013).
- **Structure the tier model cannot express**: grovvare pre-shift +50 % hour and its
  conditional ladder shrink (M-017/M-018), Øvrige § 4 b/§ 4 c scales (M-019/M-020).
- **Holddrift**: per-shift weekly norms (37/34 t), supplement clock windows (weekday
  17:00-06:00; lørdag 14:00 → end of Sunday), roster-based fridag categories
  (M-021..M-023).
- Klargøring af traktorer pre-shift ½-hour (M-002), Gartneri/Skovbrug 26-week
  averaging (M-011), Gartneri out-of-turnus Sunday notice payment (M-014), Gartneri
  anlægsgartner carve-out to the Anlægsgartner overenskomst's ladder (M-010), and the
  borderline søn-/helligdag minimum-hours floor (M-004 — likely a scheduling
  guarantee, kept for the encoding-phase decision).

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
  change rule sets already created. Any future correction needs a migration story
  *before* it ships — see the 2026-08-07 regression where a name-gated engine
  change met stale rows and moved up to 6 h/Saturday onto an afternoon supplement
  (fixed by additionally requiring the corrected tier shape). The praktikant
  correction's migration shipped 2026-08-07 (`CorrectPraktikantSection50Tiers`,
  eform-timeplanning-base v10.0.57); the other 37 presets still have **no**
  migration, and their 18 recorded defects remain undecided.
- **Pay lines are never persisted**, so re-exporting an already-paid period reflects
  today's rules, not the rules it was paid under. There is no snapshot to reconcile.

## Recommended sequence

(updated 2026-08-08 from the verification — proposed target tables and expressibility
per item live in `2026-08-08-glsa-proposed-encodings.md`)

1. Decide the fate of the Elev presets — correct the 4 genuinely clause-governed u18
   presets, and decide whether the 9 fabricated ones (Golf Elev + 8 Agro Elev) and the
   u18/o18 split are retired. Largest single source of under-attribution.
2. Fix the shippable-today items: Agro Kartoffelsortering ceiling, Gulerod `-standard`
   ceiling, Minkfoder Sunday/holiday noon bands, Jordbrug Elev o18 first tier, the
   praktikant andet-arbejde § 23 forskudt bands (G-050 — expressible via the existing
   split gate), and the config-only fridage (Golf 1 May, Agro kartoffelsortering
   31 Dec).
3. Remove the fabricated Saturday supplements (Jordbrug Standard, 8 Agro Standard;
   Gartneri `-standard`/`-elev-o18` afternoon codes) and fix Skovbrug Saturday —
   note these need band DELETION, not tier edits, or the change is a runtime no-op.
4. Decide the kapitel-22 question: new preset families for frugtplantager,
   fjerkræproduktion, minkfarme (M-007..M-009), or documented out-of-scope.
5. Model Grundlovsdag's noon split generally (needs a `DayType` for it in the base
   package) plus the other fridage; re-model Agro Øvrige on flat DKK bands.
6. Then the engine gaps by leverage: E1 band/tier stacking (unblocks defects across
   4+ packets and is itself a live payroll defect on the banded Standard presets),
   E11 unit-of-measure, E10 flat-DKK amounts, Agro forskudt bands.

Each step needs a data migration for existing rule sets, not just a catalogue edit —
the praktikant migration (`CorrectPraktikantSection50Tiers`) is the template.
