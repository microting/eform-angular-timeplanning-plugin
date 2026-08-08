# GLS-A proposed corrected encodings + engine gap analysis

**Date:** 2026-08-08
**Spec reference:** W6a of the GLS-A findings verification audit (branch `docs/glsa-findings-verification`).
**Verdict source:** `docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md` (52 CONFIRMED / 28 REFUTED / 18 UNVERIFIABLE G-rows + 23 MISSING M-rows).
**Code ground truth:** `docs/superpowers/specs/sources/CODE-TRUTH.md` — current encodings and the `## Engine facts` section every expressibility verdict below is checked against.
**Primary texts:** `docs/superpowers/specs/sources/*.txt`.

**Status: PROPOSED — every table needs its own product decision + praktikant-style data-migration story before shipping. No code changes.**

## How to read this document

Every table states EXPRESSIBLE or NOT-EXPRESSIBLE against the engine facts; NOT-EXPRESSIBLE names the exact missing capability, and those capabilities are deduplicated into `## Engine gaps` at the foot of the document.

**Scope rule.** Encodings are drawn from CONFIRMED defect rows and MISSING rows only. REFUTED rows get no encoding — with one deliberate exception: where a row was REFUTED purely for overreach (a wrong preset count, a wrong twin preset, an over-absolute "never"), the confirmed *portion* is encoded and the refutation is stated in the section itself. Seven rows are in that category: G-016, G-019, G-023, G-033, G-086, G-089, G-090. Two rows appear as explicit no-encoding entries because their refutation means the code is already right: G-031 (Packet 2) and G-087 (Packet 4).

**Not encoded, by design.** The remaining CONFIRMED rows are correctness confirmations (G-055-G-057, G-085), interpretive or arithmetic findings that yield no corrected encoding (G-001, G-026, G-028, G-039-G-041, G-044, G-045, G-047-G-049, G-051, G-052, G-066, G-069-G-072, G-074, G-076, G-077, G-080, G-097 is used only as supporting evidence in Packet 1), and absence claims already carried by the packets that need them. The two ranges above are narrower than a naive reading of the old G-039-G-052/G-069-G-078 span would suggest: G-042/G-043 are REFUTED and G-046/G-073/G-075 are UNVERIFIABLE, so despite falling numerically inside that span they are excluded, not swept in — see the ledger for each. G-050, previously in this bucket, now has its own encoding in Packet 4. Three further CONFIRMED rows get a one-line reason instead of a bare listing:
- **G-025** — subsumed by G-033's per-day-unit treatment of the same codes (`ANIMAL_NIGHT`/`SAT_ANIMAL_AFTERNOON`/`ANIMAL_SUN_HOLIDAY`, Packet 7): a flat per-day item cannot partially cumulate into overtime hours by construction, so G-025's "the stald supplement isn't cumulative with overtime" finding needs no table of its own once G-033's unit-of-measure treatment is in place.
- **G-034** — the 33840 s tier-2 boundary's 9h24m derivation is an assumption, holding only when the day is exactly 7,4 h; it gets the same carried-forward-with-caveat treatment G-023's section already gives the elev 28800 s boundary (Packet 1), so no separate table is proposed.
- **G-078** — § 9 stk. 5's above-45 t/uge premium is a period-accumulation clause needing the same capability as G-060/M-011/G-079/M-021 (Packet 8); it is added to E18's needed-by list below rather than given its own table.

All UNVERIFIABLE rows are excluded — an unsettled claim cannot justify a tier value.

**Coverage**

| Packet | Rows |
|---|---|
| 1 — Elev / lærling structural family | G-088, G-096, G-097, G-098, G-023 (portion), G-089 (portion) |
| 2 — Jordbrug Standard + Dyrehold | G-032, G-093, G-090 (portion), M-002; G-031 no-encoding |
| 3 — Gartneri + Skovbrug | G-020, G-021, G-022, G-086 (portion), G-091, M-010, M-012, M-013, M-014 |
| 4 — Golf + fridage / Grundlovsdag | G-094, G-053, G-095, G-054, G-084, G-050; G-087 no-encoding |
| 5 — Agroindustri ceilings + grovvare | G-016 (portion), G-017, G-018, M-017, M-018 |
| 6 — Agroindustri structural | G-019 (portion), M-019, M-020, G-092, M-015, M-016 |
| 7 — Jordbrug kapitel 22 + per-day supplements | M-007, M-008, M-009, G-033 (portion), G-061 |
| 8 — Cross-family regimes and engine-level gaps | G-030, M-001, M-003, M-004, M-005, M-006, M-011, G-060, G-079, M-021, M-022, M-023 |

---

## Packet 1 — Elev / lærling structural family

PROPOSED — not product-decided. No code changes.

### G-088 — Jordbrug Elev o18 WEEKDAY tier 2 rate (30 % → 50 %)
**Current:** `glsa-jordbrug-elev-o18` WEEKDAY tier 2 = 33840 s / `ELEV_OVERTIME_30` (CODE-TRUTH.md:118).
**Agreement requires:** Jordbrug § 47 stk. 4 is age-blind — 50 % for the first two hours of overtime, 80 % thereafter — so tier 2 is wrong for both o18 and (by the same clause) u18 apprentices.

**Proposed encoding — `glsa-jordbrug-elev-o18`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `ELEV_NORMAL`; 2: 33840 → `ELEV_OVERTIME_50`; 3: null → `ELEV_OVERTIME_80` | (none) |

**Expressibility:** EXPRESSIBLE — plain three-tier `PayTierRule` ladder, no bands configured for this day (`payDayTypeRules: []`), tier path runs unmodified.
**Justification:**
- Tier 1 (26640, `ELEV_NORMAL`): unchanged — not part of the defect.
- Tier 2 (33840, `ELEV_OVERTIME_50`, was `ELEV_OVERTIME_30`): G-088 — "Overarbejde og arbejde på søn- og helligdage afregnes med et tillæg til lærlingens normal-" (jordbrug-2026-2029.txt:1874), continuing "timeløn på 50 % for de første 2 timer og herefter 80 % eller tilsvarende frihed." (jordbrug-2026-2029.txt:1875) — 33840 − 26640 = 7200 s = "de første 2 timer".
- Tier 3 (null, `ELEV_OVERTIME_80`): already correct — same quote, "herefter 80 %" (jordbrug-2026-2029.txt:1875).

### G-096, G-097 — u18/o18 split is fabricated; Jordbrug § 17 is a wage table, not an overtime rule
**Current:** Jordbrug, Gartneri and Skovbrug each ship two Elev presets per age band (`*-elev-u18` / `*-elev-o18`) with different WEEKDAY tier counts/rates (CODE-TRUTH.md:100-243).
**Agreement requires:** each agreement defines exactly one lærling overtime rule, age-blind: Jordbrug § 47 stk. 4 (50/80), Gartneri § 45 stk. 3 c (50/100), Skovbrug § 44 stk. 9 (30/100); none references age. Jordbrug § 17 "Ungarbejdere" — the only other clause naming under-18 workers — is a flat wage-percentage table with no overtime language: "Ungarbejderes løn beregnes af A-lønnen." (jordbrug-2026-2029.txt:609).

**Open product decision (not resolved here):** whether Jordbrug/Gartneri/Skovbrug collapse to one Elev preset per agreement (matching the age-blind clause) or keep the u18/o18 split for other product reasons (e.g. distinct base-wage percentages under § 17) while forcing identical overtime tiers on both variants.

**Proposed encoding if the split is retained — WEEKDAY overtime rate pair, u18 and o18 forced identical per agreement**

| Agreement | Overtime rate pair (first 2 h → beyond) |
|---|---|
| Jordbrug (`glsa-jordbrug-elev-u18`, `glsa-jordbrug-elev-u18-dyrehold`, `glsa-jordbrug-elev-o18`) | `ELEV_OVERTIME_50` → `ELEV_OVERTIME_80` |
| Gartneri (`glsa-gartneri-elev-u18`, `glsa-gartneri-elev-o18`) | `ELEV_OVERTIME_50` → `ELEV_OVERTIME_100` |
| Skovbrug (`glsa-skovbrug-elev-u18`, `glsa-skovbrug-elev-o18`) | `ELEV_OVERTIME_30` → `ELEV_OVERTIME_100` |

(Full tiers with boundaries are given per-preset in the G-089 tables below; this table only establishes that the rate pair must be identical across u18/o18 for a given agreement.)

**Expressibility:** EXPRESSIBLE — same plain tier mechanism; no engine change needed either way the open decision resolves.
**Justification:**
- Split itself fabricated: G-096 — "Overarbejde og arbejde på søn- og helligdage afregnes med et tillæg til lærlingens normal-" (jordbrug-2026-2029.txt:1874) is unconditional, no age qualifier; same shape confirmed for Gartneri (gartneri-2024-2026.txt:1753-1755) and Skovbrug (skovbrug-2024-2026.txt:1962, 1969).
- § 17 is not an overtime rule: G-097 — "Ungarbejderes løn beregnes af A-lønnen." (jordbrug-2026-2029.txt:609); the surrounding 17-/16-/14-15-årige percentage rows (jordbrug-2026-2029.txt:611-624) are base-wage-by-age, not overtime tiers.

### G-089 (confirmed portion) — missing top overtime tier, 4 presets, WEEKDAY and SATURDAY
**Current — WEEKDAY:** `glsa-jordbrug-elev-u18` and `glsa-jordbrug-elev-u18-dyrehold` WEEKDAY = 1: 28800 → `ELEV_NORMAL`; 2: null → `ELEV_OVERTIME_50` (CODE-TRUTH.md:106, 132). `glsa-gartneri-elev-u18` WEEKDAY = 1: 28800 → `ELEV_NORMAL`; 2: null → `ELEV_OVERTIME_50` (CODE-TRUTH.md:194). `glsa-skovbrug-elev-u18` WEEKDAY = 1: 28800 → `ELEV_NORMAL`; 2: null → `ELEV_OVERTIME_30` (CODE-TRUTH.md:227). All four have only one overtime tier, so hour 3 onward never escalates.
**Current — SATURDAY:** `glsa-jordbrug-elev-u18` SATURDAY = 1: 28800 → `ELEV_SAT_NORMAL`; 2: null → `ELEV_SAT_OVERTIME_50` (CODE-TRUTH.md:107). `glsa-jordbrug-elev-u18-dyrehold` SATURDAY = 1: 28800 → `ELEV_SAT_NORMAL`; 2: null → `ELEV_SAT_ANIMAL_AFTERNOON` (CODE-TRUTH.md:133). `glsa-gartneri-elev-u18` SATURDAY = 1: 28800 → `ELEV_SAT_NORMAL`; 2: null → `ELEV_SAT_OVERTIME_50` (CODE-TRUTH.md:195). Same shape as WEEKDAY — one overtime tier, no escalation. (`glsa-skovbrug-elev-u18`'s SATURDAY defect is separate — § 21/§ 44's Saturday-is-overtime-from-hour-one finding, G-021, Packet 3 — and is already corrected there.)
**Agreement requires:** each clause is a two-step ladder — a rate for the first two overtime hours, then a higher rate for everything beyond — and none of the three clauses distinguishes weekday overtime from Saturday overtime. G-089 is REFUTED as filed (its "14 presets" scope is wrong on two counts, and its "WEEKDAY, SATURDAY" scope narrows to these same 4 presets); the missing-top-tier defect is confirmed for exactly these four, on both day types.

**Proposed encoding — `glsa-jordbrug-elev-u18`, `glsa-jordbrug-elev-u18-dyrehold` (WEEKDAY)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 28800 (boundary itself disputed, see G-023) → `ELEV_NORMAL`; 2: 36000 → `ELEV_OVERTIME_50`; 3: null → `ELEV_OVERTIME_80` | (none) |

**Proposed encoding — `glsa-gartneri-elev-u18` (WEEKDAY)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 28800 (disputed, see G-023) → `ELEV_NORMAL`; 2: 36000 → `ELEV_OVERTIME_50`; 3: null → `ELEV_OVERTIME_100` | (none) |

**Proposed encoding — `glsa-skovbrug-elev-u18` (WEEKDAY)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 28800 (disputed, see G-023) → `ELEV_NORMAL`; 2: 36000 → `ELEV_OVERTIME_30`; 3: null → `ELEV_OVERTIME_100` | (none) |

**Proposed encoding — `glsa-jordbrug-elev-u18` (SATURDAY)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 28800 (disputed, see G-023) → `ELEV_SAT_NORMAL`; 2: 36000 → `ELEV_SAT_OVERTIME_50`; 3: null → `ELEV_SAT_OVERTIME_80` | (none) |

**Proposed encoding — `glsa-jordbrug-elev-u18-dyrehold` (SATURDAY)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 28800 (disputed, see G-023) → `ELEV_SAT_NORMAL`; 2: 36000 → `ELEV_SAT_ANIMAL_AFTERNOON` (existing tier-2 name kept unchanged — see note below); 3: null → `ELEV_SAT_OVERTIME_80` | (none) |

**Proposed encoding — `glsa-gartneri-elev-u18` (SATURDAY)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 28800 (disputed, see G-023) → `ELEV_SAT_NORMAL`; 2: 36000 → `ELEV_SAT_OVERTIME_50`; 3: null → `ELEV_SAT_OVERTIME_100` | (none) |

**Expressibility:** EXPRESSIBLE — three ascending `PayTierRule` entries, no bands on any of these days (CODE-TRUTH.md:106-107, 132-133, 194-195: `payDayTypeRules: []` covers every day type on all three presets), tier path runs unmodified for all four presets on both WEEKDAY and SATURDAY.
**Justification:**
- Jordbrug WEEKDAY tier 2 boundary (36000 = 28800 + 7200) and rate `ELEV_OVERTIME_50`, tier 3 `ELEV_OVERTIME_80`: G-089 — "Overarbejde og arbejde på søn- og helligdage afregnes med et tillæg til lærlingens normal-" (jordbrug-2026-2029.txt:1874) / "timeløn på 50 % for de første 2 timer og herefter 80 % eller tilsvarende frihed." (jordbrug-2026-2029.txt:1875).
- Gartneri WEEKDAY tier 2 rate `ELEV_OVERTIME_50`, tier 3 `ELEV_OVERTIME_100`: G-089 — Gartneri § 45 stk. 3 c, "c. Betaling for overarbejde" / "Overarbejde og arbejde på søn- og helligdage afregnes med et tillæg til lærlingens" / "normaltimeløn på 50 % for de første 2 timer og herefter 100 % eller tilsvarende frihed." (gartneri-2024-2026.txt:1753-1755).
- Skovbrug WEEKDAY tier 2 rate `ELEV_OVERTIME_30`: G-089 — "1. og 2. time efter normal arbejdstid: tillæg, svarende til 30 % af B-løn pr. time" (skovbrug-2024-2026.txt:1962).
- Skovbrug WEEKDAY tier 3 rate `ELEV_OVERTIME_100`: G-089 — "For overarbejde herudover samt søn- og helligdage: tillæg, svarende til 100 % af B-løn pr." (skovbrug-2024-2026.txt:1969).
- Tier 1 boundary (28800) in every table above is carried forward unchanged pending G-023 below — it is not itself supported by any of these quotes.
- SATURDAY uses the identical clauses as WEEKDAY: none of § 47 stk. 4, § 45 stk. 3 c, or § 44 stk. 9 distinguishes weekday overtime from Saturday overtime — each reads "Overarbejde og arbejde på søn- og helligdage" (jordbrug-2026-2029.txt:1874; gartneri-2024-2026.txt:1754) or "1. og 2. time efter normal arbejdstid" (skovbrug-2024-2026.txt:1962, restating § 21 stk. 1 for lærlinge without a weekday-only qualifier) — so the same 50/80, 50/100 and 30/100 pairs apply to Saturday's tier 3 boundary at 36000 s (28800 + 7200) exactly as they do on WEEKDAY.
- `glsa-jordbrug-elev-u18-dyrehold`'s SATURDAY tier 2 keeps the pay-code name already shipped, `ELEV_SAT_ANIMAL_AFTERNOON`, rather than renaming it to `ELEV_SAT_OVERTIME_50` to match its non-dyrehold sibling: this fix only adds the missing top tier, it does not touch the existing tier-2 naming, which is out of scope here.

### G-089 (confirmed portion) / G-098 — 9 presets with no apprentice clause at all (fabrications, not missing-tier cases)
**Current:** `glsa-golf-elev` WEEKDAY = 1: 28800 → `ELEV_NORMAL`; 2: null → `ELEV_OVERTIME_100` (CODE-TRUTH.md:300). The 8 `glsa-agro-*-elev` presets each have WEEKDAY = 1: 28800 → `ELEV_NORMAL`; 2: null → `ELEV_OVERTIME_30`/`_40` (CODE-TRUTH.md:370, 390, 410, 430, 450, 470, 490, 510).
**Agreement requires:** nothing — there is no apprentice overtime clause to encode.

**Open product decision (not resolved here):** withdraw these 9 presets, or give them a rate ladder inherited from their agreement's Standard preset. No proposed encoding table is given, because neither the current values nor any replacement values have textual support — supplying one would fabricate a second clause.

**Expressibility:** N/A (no table proposed). If the decision resolves toward "inherit the Standard ladder", the result is EXPRESSIBLE by the same plain-tier mechanism as every other table in this packet.
**Justification (why no table can be written):**
- Golf: G-089 — Golf § 36 has no overarbejde subsection.
- Agroindustri: G-098 — `grep -ci "lærling" agroindustri-2026-2029.txt` returns 0; the word occurs nowhere in the 3765-line agreement.

### G-023 (confirmed portion) — 28800 s (8 h) first-tier boundary has no basis, 13 presets
**Current:** all 13 presets with a WEEKDAY/SATURDAY tier-1 `upToSeconds = 28800`: `glsa-jordbrug-elev-u18`, `glsa-jordbrug-elev-u18-dyrehold`, `glsa-gartneri-elev-u18`, `glsa-skovbrug-elev-u18`, `glsa-golf-elev`, and the 8 `glsa-agro-*-elev` presets (CODE-TRUTH.md:106, 132, 194, 227, 300, 370-510).
**Agreement requires:** no clause anywhere in the five GLS-A texts sets an 8-hour daily threshold for apprentices. G-023 is REFUTED as filed on its count only ("14 presets"); the absence finding itself holds for the 13.

**Open product decision (not resolved here):** what the correct tier-1 boundary should be for these 13 presets — candidates include inheriting each agreement's adult Standard-preset boundary (26640 s) or retaining 28800 as a deliberate simplification. This packet does not pick one; the tables above carry 28800 forward unchanged with this caveat attached.

**Expressibility:** EXPRESSIBLE regardless of which value is chosen — `PayTierRule.UpToSeconds` is a generic numeric threshold with no apprentice-specific special-casing in the engine (Engine facts (e), CODE-TRUTH.md:608: "if an Elev preset wants an 8h boundary it would have to be expressed as an ordinary tier `UpToSeconds` value like any other preset").
**Justification:**
- Absence of any 8-hour/28800 s threshold clause: G-023 — "8 timer" and "otte timer" return zero hits in all five agreement texts; the representative pension-only age clause "Lærlinge under 18 år, der ikke er omfattet af overenskomstens pensionsordning, samt lær-" (jordbrug-2026-2029.txt:1905) is the only kind of "under 18" hit that exists, and it supplies no daily-hours boundary.
- Scope is 13 presets, not 14 (G-023's refutation ground); the `*-elev-o18` presets use 26640 and are outside this finding.

---

## Packet 2 — Jordbrug Standard + Dyrehold

PROPOSED — not product-decided. No code changes.

### G-031 — no encoding required
REFUTED: the Dyrehold WEEKDAY 18000-21600 `SHIFTED_MORNING` band has a valid § 23 basis (jordbrug-2026-2029.txt:783) — no encoding change needed.

### G-032 — Dyrehold evening forskydning exceeds § 23's 2 h cap
**Current:** `glsa-jordbrug-dyrehold` WEEKDAY band runs `64800-86400` (18:00-24:00, 6 h) tagged `SHIFTED_EVENING` (CODE-TRUTH.md:67).
**Agreement requires:** § 23 caps the post-18:00 forskydningstillæg at 2 h. G-032 (CONFIRMED, defect 8): "Forskydningstillæg indtil 2 timer efter kl. 18.00 pr. time:" (jordbrug-2026-2029.txt:788).

**Proposed encoding — `glsa-jordbrug-dyrehold`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; 3: null → `OVERTIME_80` (unchanged) | Monday–Friday (defaultPayCode `NORMAL`, priority 1): 0-18000 `ANIMAL_NIGHT` (p1); 18000-21600 `SHIFTED_MORNING` (p1); 21600-64800 `NORMAL` (p1); **64800-72000 `SHIFTED_EVENING` (p1)** — end shortened from 86400 to 72000; 72000-86400 left unbanded, auto-filled by `DefaultPayCode NORMAL` |

**Expressibility:** EXPRESSIBLE — Jordbrug Standard & Dyrehold WEEKDAY are already inside the bands-configured "thirteen other" combinations (Engine facts (a), CODE-TRUTH.md:558), so shortening the band's `EndSecondOfDay` is a same-mechanism edit; the resulting gap is safely absorbed by the existing gap-fill-to-`DefaultPayCode` behavior (Engine facts (d), CODE-TRUTH.md:602).
**Justification:**
- G-032: end-of-band cut from 86400 to 72000, mirroring `glsa-jordbrug-standard`'s WEEKDAY `64800-72000 SHIFTED_EVENING` (CODE-TRUTH.md:55), because § 23 caps the tillæg 2 h after 18.00 — "Forskydningstillæg indtil 2 timer efter kl. 18.00 pr. time:" (jordbrug-2026-2029.txt:788).

### G-093 — Dyrehold Saturday missing the § 15 night carve-out
**Current:** `glsa-jordbrug-dyrehold` SATURDAY is a flat band `0-43200 SAT_NORMAL` then `43200-86400 SAT_ANIMAL_AFTERNOON` (CODE-TRUTH.md:68), no night code.
**Agreement requires:** G-093 (CONFIRMED, defect 15): § 15's night item explicitly extends to Saturday — "På hverdage (mandag til lørdag) mellem kl. 00.00 – 5.00 om morgenen, pr. time:" (jordbrug-2026-2029.txt:582).

**Proposed encoding — `glsa-jordbrug-dyrehold`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 21600 → `SAT_NORMAL`; 2: null → `SAT_ANIMAL_AFTERNOON` (unchanged) | Saturday (defaultPayCode `SAT_NORMAL`, priority 1): **0-18000 `ANIMAL_NIGHT` (p1)** — new; 18000-43200 `SAT_NORMAL` (p1) — start moved from 0 to 18000; 43200-86400 `SAT_ANIMAL_AFTERNOON` (p1, unchanged) |

**Expressibility:** EXPRESSIBLE — same bands-configured SATURDAY path already listed in Engine facts (a) (CODE-TRUTH.md:558); inserting an additional priority-1 band before the existing `SAT_NORMAL` band start uses the identical mechanism already carrying `ANIMAL_NIGHT` on WEEKDAY (CODE-TRUTH.md:67).
**Justification:**
- G-093: 0-18000 `ANIMAL_NIGHT` band added, matching the WEEKDAY `ANIMAL_NIGHT` band's start/pay code, because "På hverdage (mandag til lørdag) mellem kl. 00.00 – 5.00 om morgenen, pr. time:" (jordbrug-2026-2029.txt:582) parenthetically includes Saturday in the same night item that already produces the WEEKDAY 0-18000 `ANIMAL_NIGHT` band.
- `SAT_NORMAL` band start moved from 0 to 18000 — pure boundary adjustment to avoid overlap with the new band.

### G-090 — Saturday-afternoon supplement fabricated in the default kapitel-3 regime
**Current:** `glsa-jordbrug-standard` SATURDAY band `43200-64800 SAT_AFTERNOON` (CODE-TRUTH.md:56); all 8 `glsa-agro-*-standard` presets carry SATURDAY tier `2: null → SAT_AFTERNOON` with no bands defined (CODE-TRUTH.md:361, 381, 401, 421, 441, 461, 481, 501).
**Agreement requires:** G-090 — REFUTED for overreach only (opt-in alternativ-arbejdstid stk. 4 a and the incorporated Holddrift agreement do pay Saturday, but both are opt-in whole-Saturday kr/time regimes, structurally unlike a per-day "lørdag eftermiddag" item). Confirmed for the default kapitel-3 regime the Standard presets model: the exhaustive "lørdag" sweep finds no ordinary-worker Saturday-afternoon item in either Jordbrug or Agroindustri — "betales med et tillæg på kr. 82,90 pr. time." (jordbrug-2026-2029.txt:3274) belongs only to the separate opt-in stk. 4 a protokollat, not the default regime.

**Proposed encoding — `glsa-jordbrug-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | **1: null → `SAT_NORMAL`** — tier 2 (`SAT_AFTERNOON`) removed | Saturday (defaultPayCode `SAT_NORMAL`, priority 1): **21600-64800 `SAT_NORMAL` (p1)** — bands merged, `SAT_AFTERNOON` band removed |

**Proposed encoding — the 8 `glsa-agro-*-standard` presets** (`fjerkrae`, `grovvare`, `gulerod`, `kartoffelmel`, `kartoffelsorter`, `lucerne`, `minkfoder`, `ovrige`)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | **1: null → `SAT_NORMAL`** — tier 2 (`SAT_AFTERNOON`) removed | none (unchanged — these presets define no SATURDAY bands) |

**Expressibility:** EXPRESSIBLE — `glsa-jordbrug-standard` SATURDAY is bands-configured (Engine facts (a), CODE-TRUTH.md:558), so removing the second band and widening the first is a same-mechanism edit; the 8 Agro Standard presets have no SATURDAY bands, so their tier path is the operative one (Engine facts (a) point 3, CODE-TRUTH.md:549), and collapsing to a single tier is likewise a same-mechanism edit.
**Justification:**
- G-090: `SAT_AFTERNOON` removed from `glsa-jordbrug-standard`'s SATURDAY band/tier and the 8 Agro Standard presets' SATURDAY tier, because the default kapitel-3 regime has no ordinary-worker Saturday-afternoon item — the only Saturday-afternoon-shaped hit, "betales med et tillæg på kr. 82,90 pr. time." (jordbrug-2026-2029.txt:3274), is gated behind the opt-in stk. 4 a protokollat (jordbrug-2026-2029.txt:3266-3274), not part of the default regime these Standard presets model.
- Per the REFUTED scope: this correction removes the fabrication from the *default* presets only; it does not add the opt-in stk. 4 a / Holddrift kr/time regimes anywhere.

### M-002 — Jordbrug § 18 Klargøring af traktorer (pre-shift, half-wage, 30-min cap)
**Current:** No encoding exists on any Jordbrug preset for a pre-shift tractor-readiness item; WEEKDAY bands start at either `14400` (Standard, `SHIFTED_MORNING`, CODE-TRUTH.md:55) or `0`/`18000` (Dyrehold, `ANIMAL_NIGHT`/`SHIFTED_MORNING`, CODE-TRUTH.md:67) — nothing pays a fraction-of-wage code before those.
**Agreement requires:** M-002 (MISSING): § 18 — "For at holde traktorer startklare til arbejdstidens begyndelse indtil ½ time dagligt uden for" / "normal arbejdstid betales ½ timeløn." (jordbrug-2026-2029.txt:628-629).

**Proposed encoding — `glsa-jordbrug-standard`, `glsa-jordbrug-dyrehold` (WEEKDAY)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | no tier change possible (see Expressibility) | *cannot be expressed as a `StartSecondOfDay`/`EndSecondOfDay` band* — would require a floating, duration-capped pre-shift segment: `PRESHIFT_TRACTOR_HALF`, min(actual pre-shift seconds worked, 1800) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **duration-capped floating pre-shift segment (min(actual pre-shift seconds, 1800), independent of a fixed clock window)**. Engine facts (e) confirms "no such cap or floor exists anywhere in the engine code searched" (CODE-TRUTH.md:608), and `PayTimeBandRule` only supports fixed absolute `StartSecondOfDay`/`EndSecondOfDay` boundaries while `PayTierRule` only supports cumulative `UpToSeconds` thresholds (Engine facts (c), CODE-TRUTH.md:590-592).
**Justification:**
- M-002: § 18's window is defined by *duration before an unfixed normal start*, not a fixed clock range — "indtil ½ time dagligt uden for" / "normal arbejdstid betales ½ timeløn." (jordbrug-2026-2029.txt:628-629) — so it cannot be pinned to a `StartSecondOfDay`/`EndSecondOfDay` pair the way `SHIFTED_MORNING`/`ANIMAL_NIGHT` are.
- The fractional-rate aspect (½ timeløn, not a supplement on top) is not independently blocking — `PayCode` is only ever a string label with rate interpretation left to downstream payroll (Engine facts (c), CODE-TRUTH.md:588-594) — but that covers only the *rate*, not the *cap*.
- No existing band can absorb it: `SHIFTED_MORNING` (CODE-TRUTH.md:55, 67) pays a supplement *on top of* the hourly wage under § 23, while § 18 pays a *fraction* of it and is capped at 30 min.

---

## Packet 3 — Gartneri + Skovbrug

PROPOSED — not product-decided. No code changes.

### G-020 — Skovbrug Standard evening forskudt-tid band capped at 2 h instead of 1 h
**Current:** `glsa-skovbrug-standard` WEEKDAY band 64800-72000 s (18:00-20:00) `SHIFTED_EVENING` (CODE-TRUTH.md:216).
**Agreement requires:** § 22 caps the evening displacement at one hour after 18.00 — "Arbejdstiden kan af arbejdsgiveren forskydes indtil 2 timer før kl. 6.00 og indtil 1 time efter" / "kl. 18.00 mod, at der betales forskydningstillæg." (skovbrug-2024-2026.txt:910-911), repeated on the rate line: "Tillæg ved forskudt arbejdstid indtil 1 time efter kl. 18.00 pr. time" (skovbrug-2024-2026.txt:917). No clause extends it to 20:00 (G-020).

**Proposed encoding — `glsa-skovbrug-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | unchanged (tier path inactive for this day — band path wins with no tier involvement, Engine facts (a), CODE-TRUTH.md:558) | Monday-Friday: 14400-21600 `SHIFTED_MORNING`; 21600-64800 `NORMAL`; **64800-68400 `SHIFTED_EVENING`** (was 64800-72000) |

**Expressibility:** EXPRESSIBLE — a single `EndSecondOfDay` edit on an already-banded day.
**Justification:**
- G-020: band end moved 72000 → 68400 s (18:00-19:00) — "indtil 1 time efter" (skovbrug-2024-2026.txt:910), rate line at 917.
- The freed 68400-72000 s (19:00-20:00) needs no new band: trailing time after the last band is filled with the dayTypeRule's `DefaultPayCode` (`NORMAL`) — Engine facts (d), CODE-TRUTH.md:602.

### G-021 — Skovbrug Saturday is entirely § 21 stk. 1 overtime, not SAT_NORMAL first
**Current:** `glsa-skovbrug-standard` SATURDAY bands 21600-43200 `SAT_NORMAL` / 43200-64800 `SAT_AFTERNOON`, tiers 21600 `SAT_NORMAL` → null `SAT_AFTERNOON` (CODE-TRUTH.md:217) — a bands-only day, so the tiers never execute (CODE-TRUTH.md:558). `glsa-skovbrug-elev-o18` SATURDAY tiers 21600 `ELEV_SAT_NORMAL` → null `ELEV_SAT_AFTERNOON`, no bands (CODE-TRUTH.md:239). `glsa-skovbrug-elev-u18` SATURDAY tiers 28800 `ELEV_SAT_NORMAL` → null `ELEV_SAT_OVERTIME_30`, no bands (CODE-TRUTH.md:228).
**Agreement requires:** § 7 stk. 1 confines the normal week to weekdays — "Den ugentlige arbejdstid fordeles på hver af ugens 5 første hverdage." (skovbrug-2024-2026.txt:320) — so all Saturday work falls into § 21 stk. 1's ladder from hour one: "1. og 2. time efter normal arbejdstid: tillæg, svarende til 30 % af B-løn pr. time" (skovbrug-2024-2026.txt:862) then "For overarbejde herudover samt søn- og helligdage: tillæg, svarende til 100 % af B-løn pr." (skovbrug-2024-2026.txt:866). § 44 stk. 9 restates the identical ladder for lærlinge (skovbrug-2024-2026.txt:1962, 1969).

**Proposed encoding — `glsa-skovbrug-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 7200 → `SAT_OVERTIME_30`; 2: null → `SAT_OVERTIME_100` | **(none)** — existing SAT_NORMAL/SAT_AFTERNOON bands removed |

**Proposed encoding — `glsa-skovbrug-elev-o18`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 7200 → `ELEV_SAT_OVERTIME_30`; 2: null → `ELEV_SAT_OVERTIME_100` | (none — unchanged) |

**Proposed encoding — `glsa-skovbrug-elev-u18`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 7200 → `ELEV_SAT_OVERTIME_30`; 2: null → `ELEV_SAT_OVERTIME_100` | (none — unchanged) |

**Expressibility:** EXPRESSIBLE for both elev presets (already tier-only days). EXPRESSIBLE for `glsa-skovbrug-standard` **only if the SATURDAY band rows are deleted** — per Engine facts (a) (CODE-TRUTH.md:558, 561) Skovbrug Standard SATURDAY is one of the "thirteen other preset/day combinations" that fall to the bands-only branch with "no tier involvement at all", so editing tier values alone would not change runtime behaviour.
**Justification:**
- Tier boundary 7200 s (2 h) on all three presets: G-021 — skovbrug-2024-2026.txt:862, restated for lærlinge at 1962.
- `SAT_OVERTIME_100` / `ELEV_SAT_OVERTIME_100` top tier: G-021 — skovbrug-2024-2026.txt:866, restated at 1969.
- SATURDAY bands removed on Standard because § 7 stk. 1 leaves no "normal" Saturday segment to band by clock time: G-021 — skovbrug-2024-2026.txt:320.

### G-022 — Gartneri Sunday/Holiday flat code vs § 22 stk. 2's 50 %/100 % tiering
**Current:** `glsa-gartneri-standard` SUNDAY and HOLIDAY each carry a single tier `null → SUN_HOLIDAY`, no bands (CODE-TRUTH.md:185-186).
**Agreement requires:** G-022 — § 22 stk. 2: "Søn- og helligdagsarbejde og arbejde på frilørdage, der udføres på skift mellem de i" / "virksomheden beskæftigede medarbejdere, betales med et tillæg på 50 % af timelønnen" / "for de første 2 timer og med et tillæg på 100 % af timelønnen for de resterende timer." (gartneri-2024-2026.txt:753-755).

**Proposed encoding — `glsa-gartneri-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SUNDAY | 1: 7200 → `SUN_HOLIDAY_50`; 2: null → `SUN_HOLIDAY_100` | (none) |
| HOLIDAY | 1: 7200 → `SUN_HOLIDAY_50`; 2: null → `SUN_HOLIDAY_100` | (none) |

**Expressibility:** EXPRESSIBLE — tier-only day; no band exists for SUNDAY/HOLIDAY on this preset, so the tier path is already the active route (Engine facts (a) point 3, CODE-TRUTH.md:549).
**Justification:**
- Tier 1 = 7200 s (2 h) at 50 %: G-022 — gartneri-2024-2026.txt:753-754.
- Tier 2 = unbounded at 100 %: G-022 — gartneri-2024-2026.txt:755.
- Applied identically to HOLIDAY — § 22 stk. 2 names "Søn- og helligdagsarbejde" as one clause covering both day types (gartneri-2024-2026.txt:752-755).

### G-086 (confirmed portion) — Gartneri Standard & Elev o18 Saturday afternoon is untiered with no basis
**Current:** `glsa-gartneri-standard` SATURDAY tiers 23400 `SAT_NORMAL` → null `SAT_AFTERNOON`, bands 21600-45000 `SAT_NORMAL` / 45000-64800 `SAT_AFTERNOON` (CODE-TRUTH.md:184) — a bands-only day. `glsa-gartneri-elev-o18` SATURDAY tiers 23400 `ELEV_SAT_NORMAL` → null `ELEV_SAT_AFTERNOON`, no bands (CODE-TRUTH.md:206).
**Agreement requires:** § 8 stk. 2 places normal Saturday hours 06.00-12.30 — "Arbejdstiden lægges mandag til fredag mellem kl. 6.00 og kl. 18.00, og lørdag mellem kl." / "6.00 og kl. 12.30." (gartneri-2024-2026.txt:274-275). § 8 stk. 4 pays Saturday hours outside that window "som for overarbejde på hverdage, jf. § 22, stk. 1" — G-086, "varierende ugentlige arbejdstider placeres arbejdstimer på lørdage, afregnes der for disse" / "timer som for overarbejde på hverdage, jf. § 22, stk. 1." (gartneri-2024-2026.txt:313-314).
**Honesty note:** G-086 is REFUTED as filed on scope ("Elev ×2"); the defect holds for 2 of the 3 named presets. `glsa-gartneri-elev-u18` already uses `ELEV_SAT_OVERTIME_50` (CODE-TRUTH.md:195) and is correctly overtime-labelled — excluded here; its remaining missing-top-tier gap is fixed in Packet 1's G-089 SATURDAY table for `glsa-gartneri-elev-u18` above (28800 → `ELEV_SAT_NORMAL`; 36000 → `ELEV_SAT_OVERTIME_50`; null → `ELEV_SAT_OVERTIME_100`).

**Proposed encoding — `glsa-gartneri-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 7200 → `SAT_OVERTIME_50`; 2: null → `SAT_OVERTIME_100` | **(none)** — existing SAT_NORMAL/SAT_AFTERNOON bands removed |

**Proposed encoding — `glsa-gartneri-elev-o18`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SATURDAY | 1: 7200 → `ELEV_SAT_OVERTIME_50`; 2: null → `ELEV_SAT_OVERTIME_100` | (none — unchanged) |

**Expressibility:** EXPRESSIBLE for `glsa-gartneri-elev-o18` (tier-only day). EXPRESSIBLE for `glsa-gartneri-standard` **only if the SATURDAY band is deleted** — Gartneri Standard SATURDAY is also in the bands-only "thirteen" list (CODE-TRUTH.md:558), so leaving the band in place keeps the tiers inert.
**Justification:**
- Saturday hours are overtime, not a flat afternoon rate: G-086 — gartneri-2024-2026.txt:313-314.
- The 7200 s / unbounded split mirrors WEEKDAY's own § 22 stk. 1 overtime tier (26640→33840 = 7200 s at `OVERTIME_50`, then unbounded `OVERTIME_100`, CODE-TRUTH.md:183), applied from hour zero since no normal Saturday segment survives outside 06.00-12.30 — gartneri-2024-2026.txt:274-275.

### G-091 — Skovbrug Elev Sunday/Holiday invented 50 % step
**Current:** `glsa-skovbrug-elev-u18` SUNDAY 7200 `ELEV_SUN_OT_50` → null `ELEV_SUN_OT_100`; HOLIDAY 7200 `ELEV_HOL_OT_50` → null `ELEV_HOL_OT_100` (CODE-TRUTH.md:229-230). `glsa-skovbrug-elev-o18` identical (CODE-TRUTH.md:240-241).
**Agreement requires:** G-091 — § 21 stk. 1 puts søn-/helligdage straight in the 100 % clause with no intermediate step: "For overarbejde herudover samt søn- og helligdage: tillæg, svarende til 100 % af B-løn pr." (skovbrug-2024-2026.txt:866), restated verbatim for lærlinge at § 44 stk. 9 (skovbrug-2024-2026.txt:1969).

**Proposed encoding — `glsa-skovbrug-elev-u18`, `glsa-skovbrug-elev-o18`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SUNDAY | 1: null → `ELEV_SUN_OT_100` | (none) |
| HOLIDAY | 1: null → `ELEV_HOL_OT_100` | (none) |

**Expressibility:** EXPRESSIBLE — removing a tier from a tier-only day.
**Justification:**
- Invented 7200 s 50 % step removed; single flat 100 % tier from hour one: G-091 — skovbrug-2024-2026.txt:866, restated at 1969.
- Grep evidence backing removal: every "50 %" hit in skovbrug-2024-2026.txt sits in an age-based wage table, a piece-rate premium or a pension clause — none is an overtime tier (G-091 Notes).

### M-010 — Gartneri § 2 anlægsgartner carve-out: incorporated ladder unknown
**Current:** no `glsa-gartneri-*` preset has an anlægsgartner variant; all three route grønt anlægsgartnerarbejde through the ordinary § 22 stk. 1 ladder.
**Agreement requires:** M-010 — § 2 adopts the DAG/3F Anlægsgartneroverenskomst's provisions for this work, expressly including overtime: "For grønt anlægsgartnerarbejde tiltræder parterne gensidigt nedenstående bestemmelser i" (gartneri-2024-2026.txt:189); the incorporated list item reads "§ 22, stk. 1-5      Overarbejde" (gartneri-2024-2026.txt:197).

**Proposed encoding:** none can be written. The incorporated Anlægsgartneroverenskomst text is not in the corpus, so the ladder's tiers, boundaries and pay codes are unknown — only the fact that a *different* ladder applies is established. Substituting the ordinary § 22 stk. 1 ladder as a stand-in would fabricate a clause.

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY, SATURDAY | *unknown — governed by Anlægsgartneroverenskomst § 22 stk. 1-5, text not in corpus* | *unknown* |

**Expressibility:** NOT ASSESSABLE — no engine capability is missing; the blocker is a missing source. The engine could express whatever ladder the Anlægsgartneroverenskomst prescribes, but that text is not in the corpus, so no table can be written. A distinct `glsa-gartneri-anlaeg-*` family would be needed once it is obtained — a source-acquisition and product decision, not code work. (Recorded under "Not engine gaps" in the footer rather than as an engine gap.)
**Justification:**
- Carve-out exists: M-010 — gartneri-2024-2026.txt:189.
- Incorporation covers overtime specifically: M-010 — gartneri-2024-2026.txt:197.

### M-012 — Gartneri § 11 Deltidsarbejde: shift-length supplement + no-notice payment
**Current:** no deltid variant or shift-length condition exists in any `glsa-gartneri-*` preset.
**Agreement requires:** M-012 — shifts of 4 timer or less draw a flat per-hour supplement on every hour: "I tilfælde, hvor medarbejderne er deltidsbeskæftigede (4 timer eller derunder), betales et" (gartneri-2024-2026.txt:492). Separately, exceeding the agreed hours by more than 1 time without day-before notice triggers a payment: "Såfremt den aftalte arbejdstid overskrides med mere end 1 time, og dette ikke er varslet" / "senest dagen før, betales for manglende varsel:" (gartneri-2024-2026.txt:495-496).

**Proposed encoding — `glsa-gartneri-*` deltid variant**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY, SATURDAY | *would need* a whole-day gate: if total worked ≤ 14400 s, every second also carries `DELTID_SUPPLEMENT` — not expressible as a threshold tier | *would need* a notice-state flag, not a clock window: `DELTID_MANGLENDE_VARSEL` |

**Expressibility:** NOT-EXPRESSIBLE for both items — missing capabilities: **whole-shift-length gating condition** (a `PayTierRule.UpToSeconds` threshold splits pay *within* the day at a cumulative point; it cannot test "is the day's total ≤ 4 h" and then supplement every hour — Engine facts (e), CODE-TRUTH.md:606-608 finds no cap/floor/length-conditional mechanism at all) and **notice/advance-warning-conditioned pay trigger** (nothing in `PayTierRule`/`PayTimeBandRule`/`PlanRegistrationPayLine`, Engine facts (c), reads whether a schedule change was communicated by a deadline).
**Justification:**
- Flat per-hour deltid supplement conditioned on shift ≤ 4 h: M-012 — gartneri-2024-2026.txt:492.
- No-notice payment conditioned on > 1 h overage without prior-day notice: M-012 — gartneri-2024-2026.txt:495-496.

### M-013 — Gartneri § 14 Tillægsbetaling i detailsalg: clock bands + age/lærling percentage ladder
**Current:** no `glsa-gartneri-*` preset encodes any detailsalg clock-band supplement.
**Agreement requires:** M-013 — three clock bands inside the 37-hour norm: "Hverdage kl. 18.00 – 22.00" (gartneri-2024-2026.txt:537), "Lørdage kl. 12.30 – 20.00" (gartneri-2024-2026.txt:541), "Søn- og helligdage kl. 8.00 – 18.00" (gartneri-2024-2026.txt:545), with lærlinge at 75 %: "Til lærlinge betales 75 % af ovenstående satser." (gartneri-2024-2026.txt:549) and ungarbejdere age-tiered (17-årige 75 %, 16-årige 65 %, under 16 år 50 %).

**Proposed encoding — detailsalg-scoped variant (preset key a product decision; bands additive to whichever Gartneri preset the worker is on)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | unchanged (supplement layers on, does not replace NORMAL/OVERTIME) | Monday-Friday 64800-79200 `DETAIL_EVENING` (18:00-22:00) |
| SATURDAY | unchanged | Saturday 45000-72000 `DETAIL_SATURDAY` (12:30-20:00) |
| SUNDAY | unchanged | Sunday 28800-64800 `DETAIL_SUN_HOL` (08:00-18:00) |
| HOLIDAY | unchanged | Holiday 28800-64800 `DETAIL_SUN_HOL` (08:00-18:00) |

Per-worker-category pay codes, mirroring the catalogue's existing `ELEV_*` labelling rather than a numeric rate field: `DETAIL_*_LAERLING` (75 %), `DETAIL_*_UNGARB17` (75 %), `DETAIL_*_UNGARB16` (65 %), `DETAIL_*_UNGARB_U16` (50 %), selected the same way the catalogue already selects standard vs elev-u18 vs elev-o18.

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **E1, bands and overtime tiers stacking on the same day for non-praktikant presets**. The clock-band mechanics and the age/lærling ladder are individually ordinary `PayTimeBandRule`/`PayCode` shapes — since the engine has no rate field at all (Engine facts (c), CODE-TRUTH.md:588-594), every existing percentage in the catalogue is already carried as a distinct `PayCode` string interpreted downstream — but the bands cannot actually be added to a Gartneri preset without breaking it, per the caveat below.
**Caveat (why it's blocked):** § 14 is textually scoped to detailsalg work, but the preset catalogue varies only by age/elev status, not by job function — encoding this needs a new preset axis or a worker-category flag, a product decision. More fundamentally, it inherits the bands-vs-tiers routing problem: whichever Gartneri preset these bands attach to relies on its WEEKDAY/SATURDAY tier ladder (Engine facts (a)), and none of the Gartneri presets is on the two-name `IsNormalTimeSplitPresetName` allowlist (`PayRuleSetLock.cs:150-152`) that lets bands and tiers coexist — so adding WEEKDAY/SATURDAY bands here would suppress those tiers outright, the same E1 gap G-092/G-030/M-009 hit.
**Justification:**
- Weekday band 18:00-22:00 = 64800-79200 s: M-013 — gartneri-2024-2026.txt:537.
- Saturday band 12:30-20:00 = 45000-72000 s: M-013 — gartneri-2024-2026.txt:541.
- Sunday/Holiday band 08:00-18:00 = 28800-64800 s: M-013 — gartneri-2024-2026.txt:545.
- Lærling multiplier 75 %: M-013 — gartneri-2024-2026.txt:549.
- Scope: the table is expressly for work inside the 37-hour norm (gartneri-2024-2026.txt:534), so the bands supplement rather than replace the existing tiers.

### M-014 — Gartneri § 22 stk. 2: notice deadline for out-of-turnus søn-/helligdagsarbejde
**Current:** no notice-conditioned payment exists on `glsa-gartneri-standard` SUNDAY/HOLIDAY beyond the (now-tiered, per G-022) 50 %/100 % split.
**Agreement requires:** M-014 — "medarbejderne på søn- og helligdage skal arbejde udover den fastlagte turnus, skal dette" / "varsles tidligst muligt, dog senest onsdagen før kl. 16.00. I modsat fald betales en ekstra" / "timeløn." (gartneri-2024-2026.txt:762-764).

**Proposed encoding — `glsa-gartneri-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SUNDAY, HOLIDAY | G-022 tiers unchanged; *would need* an additional whole-day item `SUN_HOL_MANGLENDE_VARSEL` gated on "not notified by Wednesday 16:00 of the preceding week" — no tier or band can carry that gate | (none) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **notice/advance-warning-conditioned pay trigger** (the same gap as M-012's second item). No field in `PayTierRule`, `PayTimeBandRule` or `PlanRegistrationPayLine` (Engine facts (c), CODE-TRUTH.md:590-592) can represent whether a shift was announced by a deadline in the preceding week.
**Justification:**
- Notice deadline and consequence: M-014 — gartneri-2024-2026.txt:762-764.
- Distinct from G-022: this clause adds the notice trigger on top of, not instead of, the stk. 2 tiering encoded above.

---

## Packet 4 — Golf + fridage / Grundlovsdag cross-family

PROPOSED — not product-decided. No code changes.

### G-094, G-053 — Grundlovsdag noon split on the 29 non-praktikant presets (Golf as worked example)
**Current:** `glsa-golf-standard` GRUNDLOVSDAG `1: null → GRUNDLOVSDAG` (CODE-TRUTH.md:292); `glsa-golf-elev` the same (CODE-TRUTH.md:304) — flat, single-tier, no clock awareness, identical to 27 other non-praktikant presets (G-094).
**Agreement requires:** G-053/G-094 — "Grundlovsdag er fridag fra kl. 12.00. For arbejde efter kl. 12.00 betales som for arbejde på" (jordbrug-2026-2029.txt:960; the same clause appears in Golf's own text at golf-2024-2026.txt:656): ordinary rate before noon, søgnehelligdag rate from noon.

**Proposed encoding — `glsa-golf-standard`, `glsa-golf-elev` (pattern applies to all 29)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| GRUNDLOVSDAG (`glsa-golf-standard`) | 1: 43200 → `NORMAL`; 2: null → `GRUNDLOVSDAG` | (none possible — see below) |
| GRUNDLOVSDAG (`glsa-golf-elev`) | 1: 43200 → `ELEV_NORMAL`; 2: null → `GRUNDLOVSDAG` | (none possible) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **a clock-time boundary rule for GRUNDLOVSDAG that is not restricted to a hardcoded two-preset name allowlist and to the literal pay-code strings `OVERTIME_50`/`OVERTIME_80`**.
**Justification:**
- 43200 s is the direct encoding of "fra kl. 12.00" (G-053, jordbrug-2026-2029.txt:960); the engine's own `NoonSecondOfDay` constant is 43200 (`TimePlanningWorkingHoursService.cs:4966`).
- Bands cannot carry this split at all: `TryGetDayType` explicitly returns `false` for `dayCode == "GRUNDLOVSDAG"` (Engine facts (b), CODE-TRUTH.md:583), so `hasTimeBandRule` never resolves and the band path is structurally unreachable for this day — for all 29 presets, not just Golf.
- Ordinary tiers cannot carry a clock split either: tiers allocate against cumulative seconds worked, with no notion of clock position (Engine facts (a)/(c)).
- The one mechanism that does read clock position here, `CalculateGrundlovsdagPayLines`, runs only when `usesNormalTimeSplit` is true (gated to the two Udenlandske praktikanter names, `PayRuleSetLock.cs:150-152`) **and** the day's tiers pass `HasNormalTimeBoundaryShape`, which hardcodes not just seconds (26640/33840) but the literal pay codes `"OVERTIME_50"`/`"OVERTIME_80"` (`PayRuleSetLock.cs:229-241`; CODE-TRUTH.md:556, 559).
- Golf's own scheme is a flat `OVERTIME_100` after the daily quota (CODE-TRUTH.md:288), with no 50→80 escalation. Forcing Golf's GRUNDLOVSDAG tiers into the gate's literal shape would fabricate an overtime structure the agreement does not contain — there is no shape that is simultaneously gate-passing and semantically correct.
- Net: the proposed rule is expressible *in the data model* but would never execute; the engine falls through to the flat single-tier `GRUNDLOVSDAG` line unchanged.

### G-095, G-054 — 24 December (Jordbrug, Golf, Skovbrug, Agroindustri)
**Current:** a flat all-day HOLIDAY tier already exists in every affected preset — e.g. `glsa-golf-standard` `1: null → SUN_HOLIDAY` (CODE-TRUTH.md:291), `glsa-agro-kartoffelsorter-standard` (CODE-TRUTH.md:443).
**Agreement requires:** G-054 — "Den 24. december er fridag hele dagen. For arbejde den 24. december betales som for ar-" (jordbrug-2026-2029.txt:963, continuing at 964); the same clause in Golf (golf-2024-2026.txt:659), Skovbrug (skovbrug-2024-2026.txt:1101) and Agroindustri (agroindustri-2026-2029.txt:851).

**Proposed encoding — no tier/band change**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| HOLIDAY (Jordbrug / Golf / Skovbrug / Agro, Standard + Elev) | unchanged: `1: null → SUN_HOLIDAY` (or the preset's Elev equivalent, e.g. `ELEV_HOL_OT_100`) | unchanged |

**Expressibility:** EXPRESSIBLE — already correctly encoded; contingent on a config entry, not a schema or tier change.
**Justification:**
- `danish_holidays_2025_2030.json` contains a 24 December entry for every year in range, and `IsOfficialHoliday` (`PlanRegistrationHelper.cs:2618-2627`) routes a matching date to dayCode `"HOLIDAY"` → `DayType.Holiday` (Engine facts (b), CODE-TRUTH.md:584).
- The existing flat all-day `SUN_HOLIDAY`-family tier already *is* the søgnehelligdag rate the text requires (G-054, jordbrug-2026-2029.txt:963), so no new pay code, tier or band is needed for these four families. G-095's "unmodelled" verdict still holds for fridage in general; 24 December is the sub-case the generic Holiday bucket happens to serve correctly.
- Gartneri is excluded here — its 24 December clause is not an unconditional fridag but a 24-or-31 local choice; see the Gartneri 31 December table below.

### G-095 — 1 May, Gartneri
**Current:** `glsa-gartneri-standard` HOLIDAY `1: null → SUN_HOLIDAY` (CODE-TRUTH.md:186). 1 May is additionally absent from `danish_holidays_2025_2030.json`, so it does not even reach `HOLIDAY` today — it falls through to ordinary weekday classification by `date.DayOfWeek` (Engine facts (b), CODE-TRUTH.md:584).
**Agreement requires:** G-095 — "1. maj er fridag hele dagen. For arbejde på 1. maj betales de 4 første timer med et tillæg" (gartneri-2024-2026.txt:974), continuing at 975 with +50 % for the first four hours and +100 % thereafter.

**Proposed encoding — `glsa-gartneri-standard` (requires a dayCode that does not exist)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| MAY1 *(proposed — no such dayCode exists)* | 1: 14400 → `HOL_OT_50`; 2: null → `HOL_OT_100` | (none) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **a distinct DayType/dayCode for 1 May**.
**Justification:**
- The ladder itself is an ordinary elapsed-seconds structure: 4 h = 14400 s at +50 %, remainder at +100 % (G-095, gartneri-2024-2026.txt:974-975). The blocker is not the tier mechanism but that there is nowhere to hang a 1-May-only rule.
- `DayType` has exactly 8 values with a single generic `Holiday` and no 1 May value (Engine facts (b), CODE-TRUTH.md:565-581); a HOLIDAY-keyed change would also hit Christmas Day, New Year's Day and every other holiday, which must stay on the plain `SUN_HOLIDAY` rate.
- Independently, adding a JSON entry alone would not suffice — it would collapse 1 May into the same generic Holiday rate as Christmas, which is precisely what Gartneri's text does not say.
- `HOL_OT_50`/`HOL_OT_100` are proposed by analogy to `glsa-gartneri-elev-u18`'s existing HOLIDAY tiers `7200 → ELEV_HOL_OT_50; null → ELEV_HOL_OT_100` (CODE-TRUTH.md:197); the Standard catalogue has no non-Elev sibling today.

### G-095 — 1 May, Golf
**Current:** `glsa-golf-standard` HOLIDAY `1: null → SUN_HOLIDAY` (CODE-TRUTH.md:291); 1 May is absent from the holiday JSON, so it currently classifies as an ordinary weekday.
**Agreement requires:** G-095 — "1. maj er fridag hele dagen. For arbejde på 1. maj betales som for arbejde på en" (golf-2024-2026.txt:651), continuing "søgnehelligdag." at 652.

**Proposed encoding — no tier/band change**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| HOLIDAY (`glsa-golf-standard`, `glsa-golf-elev`) | unchanged: `1: null → SUN_HOLIDAY` / `1: null → ELEV_HOL_OT_100` | unchanged |

**Expressibility:** EXPRESSIBLE — config-only fix, no schema or tier change.
**Justification:**
- Golf's required 1 May rate (søgnehelligdag) is identical to what the existing flat HOLIDAY tier already pays (G-095, golf-2024-2026.txt:651-652) — unlike Gartneri, there is no rate to isolate from other holidays.
- The only defect is the JSON gap: adding a 1 May entry per year to `danish_holidays_2025_2030.json` routes it into `DayType.Holiday` via `IsOfficialHoliday` (CODE-TRUTH.md:584) and the existing correct rate applies with zero encoding change.

### G-095 — 1 May, Agro kartoffelsortering
**Current:** `glsa-agro-kartoffelsorter-standard` HOLIDAY `1: null → SUN_HOLIDAY`, GRUNDLOVSDAG `1: null → GRUNDLOVSDAG` (CODE-TRUTH.md:443-444); 1 May is absent from the JSON and currently classifies as an ordinary weekday.
**Agreement requires:** G-095 — "a. 1. maj er fridag fra kl. 12.00." (agroindustri-2026-2029.txt:3309), with forced overtime paid at "timeløn plus de almindelige overtidsprocenter" (3311-3312) rather than a fixed søgnehelligdag code.

**Proposed encoding — `glsa-agro-kartoffelsorter-standard` (requires a dayCode that does not exist)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| MAY1 *(proposed)* | 1: 43200 → `NORMAL`; 2: null → `OVERTIME_100` (the preset's own corrected top overtime code, per G-017) | (none) |

**Expressibility:** NOT-EXPRESSIBLE — missing capabilities: **a distinct DayType/dayCode for 1 May** *and* **clock-time noon-split routing generalised beyond the hardcoded `dayCode == "GRUNDLOVSDAG"` check**.
**Justification:**
- Structurally the same noon split as Grundlovsdag (43200 s), but the only existing clock-split routine is entered only when `dayCode == "GRUNDLOVSDAG"` (Engine facts (a), CODE-TRUTH.md:547), so it cannot be reused for 1 May even if a `MAY1` dayCode existed.
- Compounded by the same DayType-granularity gap as Gartneri's 1 May (Engine facts (b), CODE-TRUTH.md:581).
- The post-noon code follows the preset's own overtime ladder rather than a holiday code, matching "de almindelige overtidsprocenter" (G-095, agroindustri-2026-2029.txt:3311-3312) — which is why this variant cannot be merged with the Golf one.

### G-095 — 31 December, Gartneri
**Current:** `glsa-gartneri-standard` HOLIDAY `1: null → SUN_HOLIDAY` (CODE-TRUTH.md:186); no 24-or-31 choice mechanism, no shift-cutoff mechanism, and 31 December is absent from the holiday JSON.
**Agreement requires:** G-095 — "Den 24. december eller 31. december er fridag hele dagen. Fastlæggelse af fridagen sker" (gartneri-2024-2026.txt:980), continuing through 985: the parties choose which of the two dates is the fridag, the worked day ends by kl. 12.00, and hours actually worked are paid as on a søgnehelligdag.

**Proposed encoding — the rate-for-hours-worked fragment only**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| HOLIDAY (whichever of 24/31 Dec is classified) | unchanged: `1: null → SUN_HOLIDAY` | unchanged |
| *the 24-or-31 choice, and the noon shift-end* | *unrepresentable — no field exists* | *unrepresentable* |

**Expressibility:** NOT-EXPRESSIBLE overall — missing capabilities: **employer-configurable holiday-date selection (a 24-or-31 December choice)** and **shift-length/cutoff-time enforcement**. The rate-for-hours-worked fragment alone is EXPRESSIBLE.
**Justification:**
- The rate for hours actually worked is the same flat søgnehelligdag rate already encoded on HOLIDAY (G-095, gartneri-2024-2026.txt:984-985).
- The either/or choice has no home: the holiday JSON is a fixed date→holiday map for the whole customer base (CODE-TRUTH.md:584), not a per-employer selectable relation between two candidate dates.
- "slutter arbejdet senest kl. 12.00" (gartneri-2024-2026.txt:983) is a maximum-shift-length constraint, and Engine facts (e) (CODE-TRUTH.md:606-608) confirms no cap or floor logic exists anywhere in the engine.

### G-095 — 31 December, Agro kartoffelsortering
**Current:** `glsa-agro-kartoffelsorter-standard` HOLIDAY `1: null → SUN_HOLIDAY` (CODE-TRUTH.md:443); 31 December is absent from the holiday JSON, so it currently classifies as an ordinary weekday.
**Agreement requires:** G-095 — "b. Juleaftensdag og nytårsaftensdag er fridage." (agroindustri-2026-2029.txt:3314), with forced overtime paid at hourly rate plus +100 % (3316-3317).

**Proposed encoding — no tier/band change**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| HOLIDAY (`glsa-agro-kartoffelsorter-standard`, `-elev`) | unchanged: `1: null → SUN_HOLIDAY` / `1: null → ELEV_HOL_OT_100` | unchanged |

**Expressibility:** EXPRESSIBLE — config-only fix, no schema or tier change.
**Justification:**
- Unlike Gartneri there is no local choice and no shift cutoff: 31 December is unconditionally a fridag and forced work draws a flat +100 %, matching the existing HOLIDAY rate (G-095, agroindustri-2026-2029.txt:3314).
- The only defect is the JSON gap; adding 31 December entries per year routes it to `DayType.Holiday` and the existing rate applies unchanged.
- The clause's 24 December half is redundant with the general Agroindustri clause already covered above (agroindustri-2026-2029.txt:851).

### G-084 — Golf preset metadata claims an unpublished 2026-2029 period
**Current:** catalogue names `GLS-A / 3F - Golf Standard 2026-2029` (CODE-TRUTH.md:336) and `GLS-A / 3F - Golf Elev 2026-2029` (CODE-TRUTH.md:338).
**Agreement requires:** G-084 — the only published Golf edition is 2024-2026: "Overenskomst mellem GLS-A og 3F Den Grønne Gruppe" (golf-2024-2026.txt:7), with the period line at 8.

**Proposed metadata correction**

| Field | Current | Proposed |
|---|---|---|
| `glsa-golf-standard` name | `GLS-A / 3F - Golf Standard 2026-2029` | `GLS-A / 3F - Golf Standard 2024-2026` |
| `glsa-golf-elev` name | `GLS-A / 3F - Golf Elev 2026-2029` | `GLS-A / 3F - Golf Elev 2024-2026` |

**Expressibility:** EXPRESSIBLE — a plain string-field edit; no engine or schema change.
**Justification:**
- Metadata only: CODE-TRUTH.md:336, 338 record the year suffix as the *sole* divergence, with day codes, tier orders, `upToSeconds` and pay codes byte-identical between catalogue and fixture.
- Locking consequence checked rather than assumed: `IsLockedPresetName` compares against names run through `NormalizePresetName`, a regex that strips a trailing `" YYYY-YYYY"` validity-period suffix before comparison (`PayRuleSetLock.cs:81-111`), and the code's own docstring names this exact scenario. The rename is therefore safe for the lock.
- `IsNormalTimeSplitPresetName`'s two-name allowlist (`PayRuleSetLock.cs:150-152`) does not include either Golf preset, so the rename has no bearing on the Grundlovsdag gate above.
- Hygiene, not a functional requirement: update the literal `"2026-2029"` strings in `PayRuleSetLock.LockedPresetNames` alongside the catalogue rename so the pre-normalisation source stays textually accurate.

### G-087 — no encoding required (REFUTED)
`glsa-golf-standard` SATURDAY encodes 21600 s (06:00-12:00) → `SAT_NORMAL` then `SAT_AFTERNOON` (CODE-TRUTH.md:289), which matches the Golf text exactly (golf-2024-2026.txt:231-232, 475-476). No change proposed. (`glsa-golf-elev`'s 28800 s Saturday boundary is G-023's scope, in Packet 1.)

### G-050 — § 23 forskudttidstillæg inherited unmodified by praktikanter via stk. 7
**Current:** `glsa-jordbrug-praktikant-udl-andet` ships `payDayTypeRules: []` for the whole preset (CODE-TRUTH.md:318) — no forskudttidstillæg bands on WEEKDAY or any other day. `glsa-jordbrug-standard` carries the § 23 bands on WEEKDAY: 14400-21600 `SHIFTED_MORNING`, 64800-72000 `SHIFTED_EVENING` (CODE-TRUTH.md:55).
**Agreement requires:** G-050 (CONFIRMED) — § 50 stk. 7 does not modify § 23: "Overenskomstens øvrige bestemmelser er gældende for praktikanter, hvor andet ikke føl-" / "ger af § 50." (jordbrug-2026-2029.txt:2255-2256). § 23 itself sets "Forskydningstillæg indtil 2 timer før kl. 6.00 pr. time:" (jordbrug-2026-2029.txt:783) and "Forskydningstillæg indtil 2 timer efter kl. 18.00 pr. time:" (jordbrug-2026-2029.txt:788), with no age, lærling or praktikant qualifier anywhere in § 23 (jordbrug-2026-2029.txt:781-791).

**Proposed encoding — `glsa-jordbrug-praktikant-udl-andet`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | unchanged: 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` (CODE-TRUTH.md:312) | ADD: Monday-Friday 14400-21600 `SHIFTED_MORNING` (p1); 64800-72000 `SHIFTED_EVENING` (p1) — same bands as `glsa-jordbrug-standard` (CODE-TRUTH.md:55) |

**Expressibility:** EXPRESSIBLE today — unlike every other band/tier-stacking table in this document, this preset is not blocked by E1. Its catalogue name is one of the exact two strings `PayRuleSetLock.IsNormalTimeSplitPresetName` matches, `"GLS-A / 3F - Udenlandske praktikanter Landbrug Andet arbejde"` (`PayRuleSetLock.cs:150-152`, CODE-TRUTH.md:553-555), and its WEEKDAY tiers — 26640/`NORMAL`, 33840/`OVERTIME_50`, null/`OVERTIME_80` (CODE-TRUTH.md:312) — are exactly the shape `PayRuleSetLock.HasNormalTimeBoundaryShape` requires (`PayRuleSetLock.cs:229-241`, CODE-TRUTH.md:556). So adding the § 23 bands here runs through the same identity+shape-gated path Engine facts (a) describes for the two praktikant presets: normal-time seconds attributed by clock position via `GenerateTimeBandPayLines`, overflow from tier 2 onward via `GenerateOvertimeTierPayLines` (CODE-TRUTH.md:557) — bands and tiers stack instead of bands suppressing tiers outright.
**Justification:**
- Unmodified inheritance: G-050 — "Overenskomstens øvrige bestemmelser er gældende for praktikanter, hvor andet ikke føl-" / "ger af § 50." (jordbrug-2026-2029.txt:2255-2256).
- Band values identical to `glsa-jordbrug-standard`'s existing § 23 bands: § 23 — "Forskydningstillæg indtil 2 timer før kl. 6.00 pr. time:" (jordbrug-2026-2029.txt:783); "Forskydningstillæg indtil 2 timer efter kl. 18.00 pr. time:" (jordbrug-2026-2029.txt:788); CODE-TRUTH.md:55.
**Honesty note:** whether § 23's morning/evening clock windows are the right fit for *andet arbejde* praktikant work — whose normal time this document elsewhere treats as confined to 06:00-18:00 Monday-Saturday — needs its own product decision; it is not settled here. This table proposes the entitlement G-050 confirms exists; whether andet-arbejde praktikanter ever actually work the pre-6.00/post-18.00 hours the bands cover is a separate, unresolved question.

---

## Packet 5 — Agroindustri ceilings + grovvare

PROPOSED — not product-decided. No code changes.

### G-016 (confirmed portion) — Gulerodspakkerier weekday 80 % ceiling
**Current:** `glsa-agro-gulerod-standard` WEEKDAY = `1: 26640 → NORMAL; 2: 33840 → OVERTIME_30; 3: null → OVERTIME_80` (CODE-TRUTH.md:400).
**Agreement requires:** G-016 — "For 1. og 2. time efter normal arbejdstids ophør (+30 % af b-løn)" (agroindustri-2026-2029.txt:3044); "For 3. time efter normal arbejdstids ophør (+80 % af b-løn)" (agroindustri-2026-2029.txt:3051); "For overarbejde herudover, samt arbejde på søn- og helligdage (+100 % af b-løn)" (agroindustri-2026-2029.txt:3059).
**Honesty note:** G-016 is REFUTED as filed because it names both gulerod presets; the defect and the 37440 s arithmetic are confirmed for `glsa-agro-gulerod-standard` alone. The `-elev` twin has no 80 % band to bound.

**Proposed encoding — `glsa-agro-gulerod-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; 3: 37440 → `OVERTIME_80`; 4: null → `OVERTIME_100` | (none) |
| SUNDAY | 1: null → `SUN_HOLIDAY` (unchanged — the text folds søn-/helligdage into the same +100 % clause; not part of this defect) | (none) |
| HOLIDAY | 1: null → `SUN_HOLIDAY` (unchanged, same reasoning) | (none) |

**Expressibility:** EXPRESSIBLE — four ascending `PayTierRule` entries, no bands on this day, tier path runs unmodified.
**Justification:**
- Tier 1→2 (26640→33840, `OVERTIME_30`): G-016 — "For 1. og 2. time efter normal arbejdstids ophør (+30 % af b-løn)" (agroindustri-2026-2029.txt:3044); unchanged from current code.
- Tier 2→3 (33840→37440, `OVERTIME_80`): G-016 — "For 3. time efter normal arbejdstids ophør (+80 % af b-løn)" (agroindustri-2026-2029.txt:3051). Arithmetic: 26640 + 7200 = 33840 (end of hour 2); 33840 + 3600 = 37440 (end of hour 3).
- Tier 3→4 (37440→null, `OVERTIME_100`): G-016 — "For overarbejde herudover, samt arbejde på søn- og helligdage (+100 % af b-løn)" (agroindustri-2026-2029.txt:3059); the tier the shipped preset never reaches because its tier 3 is unbounded.
- Cross-reference only: `glsa-agro-gulerod-elev` (CODE-TRUTH.md:410) is a single unbounded `ELEV_OVERTIME_30` tier, so this remediation is inapplicable to it; whether the 8 Agro Elev presets should exist at all is Packet 1's finding (Agroindustri has zero "lærling" hits).

### G-017 — Kartoffelsortercentraler: invented 80 % tier, text has none
**Current:** `glsa-agro-kartoffelsorter-standard` WEEKDAY = `1: 26640 → NORMAL; 2: 33840 → OVERTIME_30; 3: null → OVERTIME_80` (CODE-TRUTH.md:440).
**Agreement requires:** G-017 — "For 1. og 2. time efter normal arbejdstids ophør (+30 % af b-løn)" (agroindustri-2026-2029.txt:3293); "For overarbejde herudover samt arbejde på søn- og helligdage (+100 % af b-løn)" (agroindustri-2026-2029.txt:3300).

**Proposed encoding — `glsa-agro-kartoffelsorter-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; 3: null → `OVERTIME_100` | (none) |

**Expressibility:** EXPRESSIBLE — a pure relabel of tier 3; no new mechanism.
**Justification:**
- Tier 1→2 (26640→33840, `OVERTIME_30`): G-017 — agroindustri-2026-2029.txt:3293. Arithmetic: 26640 + 7200 = 33840.
- Tier 2→3 (33840→null, `OVERTIME_100`): G-017 — agroindustri-2026-2029.txt:3300. This is the correction: the lønbilag has exactly two overtime tiers, and the literal string "80 %" does not occur in it at all, so `OVERTIME_80` must become `OVERTIME_100` with no intervening tier.

### G-018 — Minkfodercentraler: missing clock-keyed 100 % tier + søn-/helligdag noon split
**Current:** `glsa-agro-minkfoder-standard` WEEKDAY = `1: 26640 → NORMAL; 2: 33840 → OVERTIME_30; 3: null → OVERTIME_80`; SUNDAY and HOLIDAY = `1: null → SUN_HOLIDAY` (CODE-TRUTH.md:480-483).
**Agreement requires:** G-018 — "For 1. og 2. time efter normal arbejdstids ophør (+30 % af b-løn)" (agroindustri-2026-2029.txt:3476); "For efterfølgende overarbejdstimer indtil kl. 20.00, samt arbejde på søn- og helligdage ind-" / "til kl. 12.00 (+80 % af b-løn)" (agroindustri-2026-2029.txt:3483-3484); "For natarbejde, regnet fra kl. 20.00 og til normal arbejdstids begyndelse, samt for arbejde" / "på søn- og helligdage efter kl. 12.00 (+100 % af b-løn)" (agroindustri-2026-2029.txt:3491-3492).

**Proposed encoding — `glsa-agro-minkfoder-standard`, WEEKDAY**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; 3: *clock-keyed, "indtil kl. 20.00"* → `OVERTIME_80`; 4: *clock-keyed, "fra kl. 20.00"* → `OVERTIME_100` | *would need* Monday-Friday 72000-(next normal start) `OVERTIME_100` |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **clock-keyed overtime tier coexisting with an elapsed-seconds tier ladder on a non-praktikant preset**.
**Justification:**
- 30 % tier boundary (26640→33840): G-018 — agroindustri-2026-2029.txt:3476; elapsed-seconds, expressible on its own.
- The 80 % tier is bounded by wall-clock time, not elapsed seconds: G-018 — agroindustri-2026-2029.txt:3483-3484 ("indtil kl. 20.00"). Clock boundaries are the domain of `PayTimeBandRule.StartSecondOfDay`/`EndSecondOfDay`, not `PayTierRule.UpToSeconds` (Engine facts (c), CODE-TRUTH.md:590-591).
- The 100 % tier is clock-keyed from the other side: G-018 — agroindustri-2026-2029.txt:3491-3492.
- Why a band cannot substitute: per Engine facts (a) (CODE-TRUTH.md:548, 561), if any `PayTimeBandRule` exists for WEEKDAY the band path wins and tiers never execute for that day — except for the two hardcoded praktikant preset names (`PayRuleSetLock.cs:150-152`) and only at the rigid `HasNormalTimeBoundaryShape` (26640 / 33840 `OVERTIME_50` / null `OVERTIME_80`). Minkfoder matches neither, so adding a 20:00 band would silently discard the whole tier ladder rather than layer on it.

**Proposed encoding — `glsa-agro-minkfoder-standard`, SUNDAY / HOLIDAY**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SUNDAY | (tier path unused — bands present) | Sunday (defaultPayCode `SUN_HOLIDAY_80`, priority 1): 0-43200 `SUN_HOLIDAY_80`; 43200-86400 `SUN_HOLIDAY_100` |
| HOLIDAY | (tier path unused — bands present) | Holiday (defaultPayCode `SUN_HOLIDAY_80`, priority 1): 0-43200 `SUN_HOLIDAY_80`; 43200-86400 `SUN_HOLIDAY_100` |

**Expressibility:** EXPRESSIBLE — unlike WEEKDAY, these day types carry only a single flat tier, so replacing the tier route with a pure band route loses nothing.
**Justification:**
- 80 % until noon: G-018 — agroindustri-2026-2029.txt:3483-3484; kl. 12.00 = 43200 s, a clean `EndSecondOfDay`.
- 100 % after noon: G-018 — agroindustri-2026-2029.txt:3491-3492.

### M-017 — Grovvarehandler: missing pre-shift 50 % tier
**Current:** `glsa-agro-grovvare-standard` WEEKDAY = `1: 26640 → NORMAL; 2: 37440 → OVERTIME_40; 3: null → OVERTIME_100`, no pre-shift tier (CODE-TRUTH.md:380).
**Agreement requires:** M-017 — "For timen før normal arbejdstids begyndelse (+50 % af b-løn)" (agroindustri-2026-2029.txt:2985).

**Proposed encoding — `glsa-agro-grovvare-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 37440 → `OVERTIME_40`; 3: null → `OVERTIME_100` (post-shift ladder unchanged) | *would need* Monday-Friday (normal-start − 3600) → normal-start, `OVERTIME_50_PRESHIFT` |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **pre-shift (before-normal-start) attribution segment coexisting with the post-shift tier ladder on a non-praktikant preset**.
**Justification:**
- M-017 — "For timen før normal arbejdstids begyndelse (+50 % af b-løn)" (agroindustri-2026-2029.txt:2985): a full extra hour at +50 % occurring *before* the normal day starts.
- `PayTierRule.UpToSeconds` measures elapsed seconds forward only (Engine facts (c), CODE-TRUTH.md:590); there is no negative-offset or pre-start tier concept.
- `PayTimeBandRule` could express the clock interval, but per Engine facts (a) defining any WEEKDAY band routes the whole day through the band-only path and discards the `OVERTIME_40`/`OVERTIME_100` ladder — the identity+shape exception does not cover this preset.

### M-018 — Grovvarehandler: after-shift 40 % band shrinks when pre-shift overtime was worked
**Current:** `glsa-agro-grovvare-standard` WEEKDAY is a single fixed ladder with no conditional variant (CODE-TRUTH.md:380).
**Agreement requires:** M-018 — "Når der således har været arbejdet om morgenen, afregnes i tilfælde af fortsat overarbejde" (agroindustri-2026-2029.txt:2992), continuing at 2993-2994 with two hours at +40 % and thereafter +100 %.

**Proposed encoding — `glsa-agro-grovvare-standard`, two conditional variants of the same WEEKDAY rule**

| Condition | DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands |
|---|---|---|---|
| No pre-shift overtime that day (base ladder) | WEEKDAY | 1: 26640 → `NORMAL`; 2: 37440 → `OVERTIME_40`; 3: null → `OVERTIME_100` | (none) |
| Pre-shift overtime **was** worked that day | WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_40` (shrunk to 2 h); 3: null → `OVERTIME_100` | (none) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **tier boundary conditional on same-day runtime state (whether pre-shift overtime occurred), rather than a fixed per-day-type `UpToSeconds`**.
**Justification:**
- Shrink trigger: M-018 — agroindustri-2026-2029.txt:2992; "således" ties the shrink explicitly to the M-017 morning overtime having occurred.
- Shrunk boundary: M-018 — agroindustri-2026-2029.txt:2993, two hours at +40 %. Arithmetic: 26640 + 7200 = 33840, versus the base 26640 + 10800 = 37440 (the shipped value at CODE-TRUTH.md:380). Both boundaries are individually expressible; the *choice between them* is not.
- Continuation at +100 %: M-018 — agroindustri-2026-2029.txt:2994.
- `PayTierRule` carries no condition/predicate field (Engine facts (c), CODE-TRUTH.md:590) and `PayLineGenerator` walks one fixed chain (Engine facts (d)). M-018 is blocked both by its own conditional-boundary gap and transitively by M-017's missing pre-shift segment.

---

## Packet 6 — Agroindustri structural

PROPOSED — not product-decided. No code changes.

### G-019 (confirmed portion) — Øvrige lønbilag § 4 a after-shift DKK bands (flat, not percentage)
**Current:** `glsa-agro-ovrige-standard` WEEKDAY tiers `1: 26640 → NORMAL; 2: 33840 → OVERTIME_30; 3: null → OVERTIME_80` (CODE-TRUTH.md:500); `glsa-agro-ovrige-elev` WEEKDAY tiers `1: 28800 → ELEV_NORMAL; 2: null → ELEV_OVERTIME_30` — a single unbounded 30 % tier, no 80 % band at all (CODE-TRUTH.md:510).
**Agreement requires:** G-019 — "5. klokketime og derefter indtil den normale arbejdstids begyndelse:" (agroindustri-2026-2029.txt:3577), with the two preceding bands at agroindustri-2026-2029.txt:3567 and 3572, plus the § 4 d søn-/helligdag split at agroindustri-2026-2029.txt:3616 and 3621.
**Honesty note:** G-019's code half is correct only for `glsa-agro-ovrige-standard` (which does model 30 % → 80 %); it is wrong for `glsa-agro-ovrige-elev` (single unbounded 30 % tier). The refutation applies to that overreach only — the remediation direction below (a flat-DKK band model replacing percentage tiers) is unaffected and applies to both presets.

**Proposed encoding — `glsa-agro-ovrige-standard`, `glsa-agro-ovrige-elev`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL` (unchanged) | clock-keyed after-shift: `OT_DKK_BAND_1` (klokketime 1-2 after shift end), `OT_DKK_BAND_2` (klokketime 3-4), `OT_DKK_BAND_3` (klokketime 5+ until next normal-time start) — NOT expressible as `StartSecondOfDay`/`EndSecondOfDay` bands, since boundaries are relative to shift end, not to fixed clock time |
| SUNDAY / HOLIDAY | none (existing `SUN_HOLIDAY` tier replaced by the § 4 d scale) | `SUN_HOL_DKK_BAND_1` (normal-time start → 12:00), `SUN_HOL_DKK_BAND_2` (12:00 → next normal-time start) |

**Expressibility:** NOT-EXPRESSIBLE — missing capabilities: **flat-DKK amount field on a pay code** (Engine facts (c), CODE-TRUTH.md:588-594: `PayTierRule`/`PayTimeBandRule`/`PlanRegistrationPayLine` carry no rate or amount column) and **shift-relative (rather than clock-of-day) band boundaries** (`PayTimeBandRule` takes absolute `StartSecondOfDay`/`EndSecondOfDay`). The § 4 a bands are anchored to "1./2. klokketime efter normal arbejdstid" — counted from whenever the shift ends. The § 4 d SUNDAY/HOLIDAY bands are anchored the same way from the other side: "Fra den daglige normale arbejdstids begyndelse og indtil kl. 12.00" and "Fra kl. 12.00 og indtil den normale arbejdstids begyndelse" (agroindustri-2026-2029.txt:3616, 3621) both move with the shift's normal-time start, not a fixed clock boundary alone — so both halves of this table are blocked by the same shift-relative-boundary gap, not just § 4 a.
**Justification:**
- `OT_DKK_BAND_1` = kr. 49,25/time (1. og 2. klokketime efter normal arbejdstid) — G-019, agroindustri-2026-2029.txt:3567-3568.
- `OT_DKK_BAND_2` = kr. 78,46/time (3. og 4. klokketime efter normal arbejdstid) — G-019, agroindustri-2026-2029.txt:3572-3573.
- `OT_DKK_BAND_3` = kr. 146,77/time — G-019, "5. klokketime og derefter indtil den normale arbejdstids begyndelse:" (agroindustri-2026-2029.txt:3577), rate at 3578.
- `SUN_HOL_DKK_BAND_1` (normal-time start → kl. 12.00) — G-019, agroindustri-2026-2029.txt:3616-3617.
- `SUN_HOL_DKK_BAND_2` (kl. 12.00 → next normal-time start) — G-019, agroindustri-2026-2029.txt:3621-3622.

### M-019 — Øvrige lønbilag § 4 b: pre-shift overtime, day/night DKK scale
**Current:** neither `glsa-agro-ovrige-standard` nor `glsa-agro-ovrige-elev` has any pre-shift tier at all (CODE-TRUTH.md:500, 510 — WEEKDAY tiers start at `NORMAL`/`ELEV_NORMAL` with nothing preceding).
**Agreement requires:** M-019 — "b. Overarbejde forud for normal arbejdstid:" (agroindustri-2026-2029.txt:3582).

**Proposed encoding — `glsa-agro-ovrige-standard`, `glsa-agro-ovrige-elev`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY (pre-shift segment only) | none — pre-shift work is not reachable by a forward-counting tier | `PRE_OT_DKK_DAY` (kl. 6.00-18.00), `PRE_OT_DKK_NIGHT` (kl. 18.00-06.00), applied only to minutes worked before normal-time start |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **pre-shift (before normal-time start) attribution segment** — the tier model counts forward from shift start via cumulative `UpToSeconds` with no concept of time worked *before* the normal-time boundary. Also missing the **flat-DKK amount field** (Engine facts (c)).
**Justification:**
- `PRE_OT_DKK_DAY` — overarbejde forud for normal arbejdstid within kl. 6.00-18.00, M-019, agroindustri-2026-2029.txt:3585-3589.
- `PRE_OT_DKK_NIGHT` — the same work within kl. 18.00-06.00, M-019, agroindustri-2026-2029.txt:3592-3593.

### M-020 — Øvrige lønbilag § 4 c: hverdagsfridag call-in, day/night DKK scale
**Current:** no preset has any hverdagsfridag pay code; "hverdagsfridag" returns zero hits in CODE-TRUTH.md, whose 16 Agro preset tables (lines 356-514) list only NORMAL/OVERTIME/SAT/SUN_HOLIDAY-family codes.
**Agreement requires:** M-020 — "c. Tilsiges en medarbejder til at udføre arbejde på en i forvejen tilsikret hel hverdagsfridag" (agroindustri-2026-2029.txt:3597).

**Proposed encoding — `glsa-agro-ovrige-standard`, `glsa-agro-ovrige-elev`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY (only when this weekday is *this worker's* guaranteed day off) | none | `FRIDAG_CALLBACK_DKK_DAY` (kl. 6.00-18.00), `FRIDAG_CALLBACK_DKK_NIGHT` (kl. 18.00-06.00) |

**Expressibility:** NOT-EXPRESSIBLE — missing capabilities: **per-employee guaranteed-day-off marker** (Engine facts (b), CODE-TRUTH.md:565-581: the eight `DayType` values are all calendar-derived, none worker-individual, and this fridag recurs on ordinary weekdays so it cannot be modelled as a fixed date) and the **flat-DKK amount field** (Engine facts (c)).
**Justification:**
- `FRIDAG_CALLBACK_DKK_DAY` — hours between kl. 6.00 and kl. 18.00 on a called-in hverdagsfridag, M-020, agroindustri-2026-2029.txt:3600-3601.
- `FRIDAG_CALLBACK_DKK_NIGHT` — hours between kl. 18.00 and kl. 6.00, M-020, agroindustri-2026-2029.txt:3604-3605.

### G-092 — Agroindustri § 19 Forskudt arbejdstid: missing bands across all 16 presets
**Current:** all 16 `glsa-agro-*` presets have `payDayTypeRules: []` (TS) and no `DayTypeRules` (C#), and none of the 16 preset tables carries a forskudt pay code (CODE-TRUTH.md:354, 356-514).
**Agreement requires:** G-092 — "Fra kl. 18.00 til kl. 22.00" (agroindustri-2026-2029.txt:656) and "Fra kl. 22.00 til kl. 6.00" (agroindustri-2026-2029.txt:661), each with its own kr/time supplement.

**Proposed encoding — all 16 `glsa-agro-*` presets**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | existing tiers unchanged per preset (CODE-TRUTH.md:356-514) | ADD: Monday-Friday 64800-79200 (18:00-22:00) `FORSKUDT_1`; 79200-86400 + 0-21600 (22:00-06:00, split across midnight) `FORSKUDT_2` |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **bands and overtime tiers stacking on the same day for non-praktikant presets**, plus the **flat kr/time amount field** (Engine facts (c)).
This is the crux of the row. Per Engine facts (a) (CODE-TRUTH.md:545-561), `CalculatePayLinesForDay` picks bands *or* tiers per day, not both: "bands beat tiers whenever both exist for a day", and the tier-on-top-of-bands split is gated by preset **identity**, hard-coded to the two Udenlandske praktikanter presets (`PayRuleSetLock.cs:150-152`). None of the 16 `glsa-agro-*` presets is on that list, so adding WEEKDAY bands to any of them today would make the band path win outright and **suppress that day's overtime tiers entirely** — the opposite of § 19 stk. 4's requirement that overarbejdstillæg is payable *in addition to* the forskudt supplement. Encoding this correctly requires either extending the identity+shape stacking gate to these presets, or a new capability that runs bands and tiers concurrently. Until then the bands above are unsafe to ship as-is.
**Justification:**
- `FORSKUDT_1` band 18:00-22:00 — G-092, "Fra kl. 18.00 til kl. 22.00" (agroindustri-2026-2029.txt:656).
- `FORSKUDT_2` band 22:00-06:00 — G-092, "Fra kl. 22.00 til kl. 6.00" (agroindustri-2026-2029.txt:661).
- Additivity (why a bare band-add is unsafe) — G-092, "Kræves der i tilslutning til forskudt arbejdstid udført overarbejde, betales der under sådant" (agroindustri-2026-2029.txt:672), continuing at 673-674.

### M-015 — Agroindustri § 5 stk. 3 d: deltid forskudt window, without § 19's establishment mechanics
**Current:** no deltid variant exists in any of the 16 presets; all model full-time WEEKDAY/SATURDAY structures only (CODE-TRUTH.md:356-514).
**Agreement requires:** M-015 — "For den del af den daglige arbejdstid, der for deltidsbeskæftigede medarbejdere ligger" / "uden for tidsrummet fra kl. 6.00 til kl. 18.00, ydes en tillægsbetaling svarende til den, der" / "ydes ved arbejde på forskudt tid." (agroindustri-2026-2029.txt:279-281).

**Proposed encoding — new deltid variant per line, e.g. `glsa-agro-<line>-deltid`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY (deltid worker only) | base preset's NORMAL tier, scaled to actual scheduled hours | Monday-Friday 0-21600 (00:00-06:00) `DELTID_FORSKUDT`; 64800-86400 (18:00-24:00) `DELTID_FORSKUDT` — the 06:00-18:00 exclusion window is itself the trigger, with no established/varslet forskudt arrangement required |

**Expressibility:** NOT-EXPRESSIBLE — missing capabilities: **worker-category (deltid) conditional rule selection** (the catalogue's only variation axis is product line × standard/elev, CODE-TRUTH.md:20) and the **flat kr/time amount field** (Engine facts (c)). The band geometry itself would be expressible, but only at the cost of the same tier-suppression problem described under G-092.
**Justification:**
- Trigger: worker is deltidsbeskæftiget and part of the daily working time lies outside kl. 6.00-18.00 — M-015, agroindustri-2026-2029.txt:279-281.
- Rate is borrowed, not stated: "ydes ved arbejde på forskudt tid" (agroindustri-2026-2029.txt:281) points at the § 19 rates — G-092, agroindustri-2026-2029.txt:656, 661.
- No establishment/notice gate, unlike § 19 — sub-heading "d. Arbejde uden for tidsrummet kl. 6.00 – 18.00" (agroindustri-2026-2029.txt:278).

### M-016 — Agroindustri § 7 stk. 4 Weekendarbejde: opt-in regime suppresses søgnehelligdagsforskud
**Current:** no Weekendarbejde preset or day type exists; all 16 `glsa-agro-*` HOLIDAY rows carry `SUN_HOLIDAY`/`ELEV_HOL_OT_*` (CODE-TRUTH.md:356-514).
**Agreement requires:** M-016 — "For arbejde på søgnehelligdage betales alene den normale løn, og der betales således" / "ikke søgnehelligdagsforskud." (agroindustri-2026-2029.txt:401-402).

**Proposed encoding — new opt-in variant, e.g. `glsa-agro-<line>-weekendarbejde`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| HOLIDAY (while the § 7 stk. 4 regime is in force) | 1: null → `NORMAL` — replaces `SUN_HOLIDAY`/`ELEV_HOL_OT_100`, no søgnehelligdagsforskud | (none) |
| SATURDAY / SUNDAY | unchanged from the base preset | unchanged |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **establishment-level opt-in regime flag that overrides a preset's default day-type pay code**. No mechanism exists to conditionally suppress a pay code based on a regime election; the only variation axis is which preset is assigned (CODE-TRUTH.md:20). (Shipping a whole parallel preset per line would technically work but multiplies the catalogue by the number of opt-in regimes — a product decision, not a technical fix.)
**Justification:**
- Regime scope — opt-in two-shift 24-hour Saturday/Sunday staffing, M-016, stk. header at agroindustri-2026-2029.txt:397.
- Suppression rule — M-016, "For arbejde på søgnehelligdage betales alene den normale løn, og der betales således" (agroindustri-2026-2029.txt:401).

---

## Packet 7 — Jordbrug kapitel 22 + per-day supplements

PROPOSED — not product-decided. No code changes.

### M-007 — new preset candidate `glsa-jordbrug-frugtplantage-standard`
**Current:** no preset exists; frugt- og bærplantager work is entered against `glsa-jordbrug-standard` (CODE-TRUTH.md:49-59), whose WEEKDAY ladder is 26640 → `NORMAL` / 33840 → `OVERTIME_30` / null → `OVERTIME_80` and whose SUNDAY/HOLIDAY rows are flat `SUN_HOLIDAY` with no noon split.
**Agreement requires:** M-007 — jordbrug-2026-2029.txt:3526, 3533, 3540. § 1 (jordbrug-2026-2029.txt:191) puts "frugt- og bærplantager" in scope and § 16 (jordbrug-2026-2029.txt:600-601) routes their wages to kapitel 22.

**Proposed encoding — `glsa-jordbrug-frugtplantage-standard`** (new preset — a product decision, plus a praktikant-style data-migration story for existing frugtplantage entries currently on `glsa-jordbrug-standard`)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 30240 → `OVERTIME_30`; 3: null → `OVERTIME_50` | (none) |
| SUNDAY | 1: null → `OVERTIME_50` (fallback) | Sunday (defaultPayCode `OVERTIME_50`, priority 1): 0-43200 `OVERTIME_50` (p1); 43200-86400 `OVERTIME_100` (p1) |
| HOLIDAY | 1: null → `OVERTIME_50` (fallback) | Holiday (defaultPayCode `OVERTIME_50`, priority 1): 0-43200 `OVERTIME_50` (p1); 43200-86400 `OVERTIME_100` (p1) |

**Expressibility:** EXPRESSIBLE.
**Justification:**
- Tier 1 = 26640, the same normal-daglig-arbejdstid cutoff every Jordbrug preset uses (CODE-TRUTH.md:55). "For 1. time efter normal daglig arbejdstids ophør betales et tillæg på (+30 % af B-løn)" (jordbrug-2026-2029.txt:3526) covers exactly ONE overtime hour, so tier 2 = 26640 + 3600 = 30240 — not the family default 33840. This is what makes M-007 "unlike the family's encoded 30 %/80 %".
- Tier 3 and the pre-noon Sunday/Holiday band: M-007 — "For efterfølgende timer samt søn- og helligdage indtil kl. 12.00 (+50 % af B-løn)" (jordbrug-2026-2029.txt:3533). 12:00 = 43200 s.
- Post-noon Sunday/Holiday band: M-007 — "For søn- og helligdage efter kl. 12.00 (+100 % af B-løn)" (jordbrug-2026-2029.txt:3540).
- Why the noon split is safe here: SUNDAY/HOLIDAY have no elapsed "normal work" portion to preserve, so the bands-beat-tiers routing (Engine facts (a)) costs nothing — this is a pure clock cut, not the mixed clock+elapsed problem that blocks M-009.
- No SATURDAY row is proposed: the frugtplantage clause states no Saturday-specific rate.

### M-008 — new preset candidate `glsa-jordbrug-fjerkraeproduktion-standard`
**Current:** no preset exists. Distinct from `glsa-agro-fjerkrae-standard`/`-elev`, which encode the *Agroindustri 4012* fjerkræ lønbilag — a different clause in a different agreement. Jordbrug fjerkræproduktion work today falls on `glsa-jordbrug-standard` (CODE-TRUTH.md:49-59).
**Agreement requires:** M-008 — jordbrug-2026-2029.txt:3614, 3621, 3628, 3635-3636. § 1 (jordbrug-2026-2029.txt:192) places "fjerkræproduktion undtaget rugerier" in Jordbrug's own scope; § 16 (jordbrug-2026-2029.txt:600-601) routes it to kapitel 22.

**Proposed encoding — `glsa-jordbrug-fjerkraeproduktion-standard`** (new preset — product decision + data-migration story)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; 3: 37440 → `OVERTIME_50`; 4: null → `OVERTIME_100` | (none) |
| SATURDAY | 1: null → `OVERTIME_100` (baseline only — see below) | (none) |
| SUNDAY | 1: null → `OVERTIME_100` | (none) |
| HOLIDAY | 1: null → `OVERTIME_100` | (none) |

**Expressibility:** the WEEKDAY/SUNDAY/HOLIDAY baseline ladder is EXPRESSIBLE. The full rule, including the fodring/æg-indsamling carve-out, is NOT-EXPRESSIBLE — missing capability: **task/activity-type dimension on a pay rule**. Per Engine facts (c) (CODE-TRUTH.md:590-592), `PayTierRule` and `PayTimeBandRule` key purely on elapsed seconds or clock-time plus `DayType`; neither records what kind of work was performed, so "fodring og indsamling af æg" cannot be distinguished from any other overtime on the same day.
**Justification:**
- Tier 2 = 26640 + 7200 = 33840: M-008 — "For 1. og 2. time efter normal daglig arbejdstids ophør (+30 % af C-løn)" (jordbrug-2026-2029.txt:3614).
- Tier 3 = 33840 + 3600 = 37440: M-008 — "For 3. time efter normal daglig arbejdstids ophør (+50 % af C-løn)" (jordbrug-2026-2029.txt:3621).
- Tier 4 and the flat SUNDAY/HOLIDAY tier: M-008 — "Derefter og for søn- og helligdage (+100 % af C-løn)" (jordbrug-2026-2029.txt:3628).
- SATURDAY's `OVERTIME_100` baseline is inferred, not directly quoted: it follows from the carve-out "Idet dog nødvendigt overarbejde ifm. fodring og indsamling af æg lørdag samt søn- og hel-" / "ligdage betales med (+80 % af C-løn)" (jordbrug-2026-2029.txt:3635-3636), which only makes sense as a reduction of an otherwise-higher Saturday rate; the only higher rate quoted is the 100 % "derefter" rate. Flagged as an inference, not a citation.
- The 80 % carve-out attaches to neither tiers (elapsed-seconds, no task concept) nor a clock band (it applies to the *whole* day for the named tasks) — confirming the gap is task-type, not time-of-day.

### M-009 — new preset candidate `glsa-jordbrug-minkfarm-standard`
**Current:** no preset exists; mink husbandry work today falls on `glsa-jordbrug-standard` / `glsa-jordbrug-dyrehold` (CODE-TRUTH.md:49-71). Structurally the same shape G-018 found missing for `glsa-agro-minkfoder-*`, but that is the *Agroindustri* minkfodercentraler lønbilag — a separate clause in a separate agreement (Packet 5's scope).
**Agreement requires:** M-009 — jordbrug-2026-2029.txt:3774, 3781-3782, 3789-3790. § 1 (jordbrug-2026-2029.txt:185) puts mink husbandry in Jordbrug's scope; § 16 (jordbrug-2026-2029.txt:600-601) routes minkfarme to kapitel 22.

**Proposed encoding — `glsa-jordbrug-minkfarm-standard`** (new preset — product decision + data-migration story)

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_30`; **3: clock-keyed continuation to kl. 20.00 → `OVERTIME_80`; 4: from kl. 20.00 → `OVERTIME_100` — unrepresentable** | (none possible alongside tiers 1-2 — see below) |
| SUNDAY | 1: null → `OVERTIME_80` (fallback) | Sunday (defaultPayCode `OVERTIME_80`, priority 1): 0-43200 `OVERTIME_80` (p1); 43200-86400 `OVERTIME_100` (p1) |
| HOLIDAY | 1: null → `OVERTIME_80` (fallback) | Holiday (defaultPayCode `OVERTIME_80`, priority 1): 0-43200 `OVERTIME_80` (p1); 43200-86400 `OVERTIME_100` (p1) |

**Expressibility:** WEEKDAY — NOT-EXPRESSIBLE — missing capability: **clock-keyed overtime tier chained after an elapsed-seconds tier on a non-name-gated preset**. SUNDAY/HOLIDAY — EXPRESSIBLE (pure clock bands, same reasoning as M-007).
**Justification:**
- WEEKDAY tiers 1-2 = 26640 + 7200 = 33840: M-009 — "1. og 2. time efter normal daglig arbejdstids ophør (+30 % af B-løn)" (jordbrug-2026-2029.txt:3774).
- WEEKDAY continuation bounded by wall-clock 20:00: M-009 — "For efterfølgende overarbejdstimer indtil kl. 20.00 samt for arbejde på søn- og helligdage" / "indtil kl. 12.00 (+80 % af B-løn)" (jordbrug-2026-2029.txt:3781-3782). `PayTierRule.UpToSeconds` is a cumulative elapsed-seconds threshold (Engine facts (c), CODE-TRUTH.md:590) and cannot represent "until the clock reads 20:00".
- WEEKDAY night portion: M-009 — "For natarbejde, regnet fra kl. 20.00 og til normal arbejdstids begyndelse, samt for arbejde" / "på søn- og helligdage efter kl. 12.00 (+100 % af B-løn)" (jordbrug-2026-2029.txt:3789-3790). 20:00 = 72000 s.
- Why bands cannot rescue it: per Engine facts (a) (CODE-TRUTH.md:548, 553-556), a non-empty `TimeBandRules` set for a `DayType` makes the band path win and tiers never run — except for the two presets gated by exact name (`PayRuleSetLock.cs:150-152`) *and* exact tier shape (`HasNormalTimeBoundaryShape`, `PayRuleSetLock.cs:229-241`). A new minkfarm preset is neither of those names, and its tier 2 pay code is `OVERTIME_30`, not `OVERTIME_50`, so it could not pass the shape gate even if renamed.
- SUNDAY/HOLIDAY have no elapsed normal-work portion to preserve, so the noon split at 43200 s is pure clock banding with nothing competing.

### G-033 (confirmed agreement half; REFUTED on the code half) — per-day supplements
**Current:** `PayTierRule`, `PayTimeBandRule` and `PlanRegistrationPayLine` (CODE-TRUTH.md:588-594) carry only `PayCode`/`PayrollCode` string labels and `Hours`/`HoursInSeconds` — **no rate or amount column exists anywhere in the engine**. The claim that these clauses are encoded "as hourly pay codes" is therefore not verifiable from the code in either direction; that interpretation lives downstream. This is why G-033 is REFUTED as written.
**Agreement requires (CONFIRMED for all three clauses on the day types cited):**
- § 15: "På lørdage efter kl. 12.00, pr. dag:" (jordbrug-2026-2029.txt:587) and the søn-/helligdag item at 593; the marts-2026 rate sheet agrees (loenoversigt-landbrug-2026.txt:65-69).
- § 47 stk. 5 (elev stald): jordbrug-2026-2029.txt:1880 and 1886, both "pr. dag".
- § 50 stk. 4 d (praktikant): "For arbejde i normal arbejdstid på lørdag eftermiddag betales et tillæg pr. dag på:" (jordbrug-2026-2029.txt:2215) and the søn-/helligdag item at 2220.
- § 15 is MIXED, not uniformly per-day: "På hverdage (mandag til lørdag) mellem kl. 00.00 – 5.00 om morgenen, pr. time:" (jordbrug-2026-2029.txt:582) is expressly hourly.

**Proposed target notation** — what a per-day supplement would need if the engine carried a unit:

| PayCode (current) | Preset(s) / location | DayCode | Clause | Required unit annotation |
|---|---|---|---|---|
| `ANIMAL_NIGHT` | `glsa-jordbrug-dyrehold` WEEKDAY band 0-18000 (CODE-TRUTH.md:67) | WEEKDAY | § 15 night item (jordbrug-2026-2029.txt:582) | `UNIT: per-hour` — already correct, no change |
| `SAT_ANIMAL_AFTERNOON` | `glsa-jordbrug-dyrehold` (CODE-TRUTH.md:68); `glsa-jordbrug-praktikant-udl-staldarbejde` (CODE-TRUTH.md:325) | SATURDAY | § 15 (587); § 50 stk. 4 d (2215) | `UNIT: per-day (flat, once per Saturday worked)` |
| `ELEV_SAT_ANIMAL_AFTERNOON` | `glsa-jordbrug-elev-u18-dyrehold` (CODE-TRUTH.md:133) | SATURDAY | § 47 stk. 5 (1880) | `UNIT: per-day (flat)` |
| `ANIMAL_SUN_HOLIDAY` | `glsa-jordbrug-dyrehold` (CODE-TRUTH.md:69-70); `glsa-jordbrug-praktikant-udl-staldarbejde` (CODE-TRUTH.md:326-327) | SUNDAY, HOLIDAY | § 15 (593); § 50 stk. 4 d (2220) | `UNIT: per-day (flat)` |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **unit-of-measure field (per-hour vs per-day/flat) on `PayTierRule` / `PayTimeBandRule` / `PlanRegistrationPayLine`**. Those entities only ever carry `Hours`/`HoursInSeconds` (Engine facts (c)); there is no `IsFlatRate`/`PerOccurrence` marker, so a pr.-dag clause and a pr.-time clause are indistinguishable in-engine and can only be told apart by a downstream lookup keyed on the `PayCode` string.
**Justification:**
- Each unit annotation is a direct transcription of "pr. dag" vs "pr. time" in the cited clause; no arithmetic applies, since this concerns unit of measure, not seconds boundaries.
- No proposed change touches any `UpToSeconds`/`StartSecondOfDay`/`EndSecondOfDay` value — the existing thresholds (CODE-TRUTH.md:67-70, 325-327) stay as they are.

### G-061 — praktikant staldarbejde shares ordinary Dyrehold pay-code strings
**Current:** `glsa-jordbrug-praktikant-udl-staldarbejde` SATURDAY/SUNDAY/HOLIDAY reuse the same pay-code strings as ordinary `glsa-jordbrug-dyrehold` — `SAT_ANIMAL_AFTERNOON` and `ANIMAL_SUN_HOLIDAY` (CODE-TRUTH.md:325-327 vs 68-70) — while every other trainee preset gets prefixed codes, e.g. `glsa-jordbrug-elev-u18-dyrehold` uses `ELEV_SAT_ANIMAL_AFTERNOON` (CODE-TRUTH.md:133).
**Agreement requires:** G-061 — the amounts genuinely differ: § 15 pays 154,74 and 327,75 (jordbrug-2026-2029.txt:588, 594; loenoversigt-landbrug-2026.txt:68-69) while § 50 stk. 4 d pays 73,90 and 177,60 (jordbrug-2026-2029.txt:2216, 2221). Because `PayCode` is the only channel a downstream payroll system has, one string cannot carry two rates.

**Proposed encoding — `glsa-jordbrug-praktikant-udl-staldarbejde`**

| DayCode | Current PayCode | Proposed praktikant-only PayCode | Basis |
|---|---|---|---|
| SATURDAY (band 43200-86400 only — `SAT_ANIMAL_AFTERNOON` is not a tier here; tier 2 is a separate, unrelated `OVERTIME_50` pay code, CODE-TRUTH.md:325, left untouched by this rename) | `SAT_ANIMAL_AFTERNOON` | `PRAKTIKANT_SAT_ANIMAL_AFTERNOON` | § 50 stk. 4 d pays 73,90 (jordbrug-2026-2029.txt:2215-2216), not § 15's 154,74 (587-588) |
| SUNDAY (tier 1 / band 0-86400) | `ANIMAL_SUN_HOLIDAY` | `PRAKTIKANT_ANIMAL_SUN_HOLIDAY` | § 50 stk. 4 d pays 177,60 (jordbrug-2026-2029.txt:2220-2221), not § 15's 327,75 (593-594) |
| HOLIDAY (tier 1 / band 0-86400) | `ANIMAL_SUN_HOLIDAY` | `PRAKTIKANT_ANIMAL_SUN_HOLIDAY` | same clause as SUNDAY — § 50 stk. 4 d makes no Sunday/Holiday distinction |

**Expressibility:** EXPRESSIBLE — a pure `PayCode` string rename; no schema or engine-logic change.
**Justification:**
- `PRAKTIKANT_` prefixing rather than reusing `ELEV_`: praktikant (§ 50 stk. 4 d) and elev (§ 47 stk. 5) are separate clauses with independently set amounts that happen to coincide today; collapsing them onto one string would reintroduce the same ambiguity G-061 flags.
- The rename touches only SATURDAY/SUNDAY/HOLIDAY codes, never this preset's WEEKDAY tiers (26640 → `NORMAL`, 33840 → `OVERTIME_50`, null → `OVERTIME_80`, CODE-TRUTH.md:324), so it cannot disturb `HasNormalTimeBoundaryShape` / `IsNormalTimeSplitPresetName` (`PayRuleSetLock.cs:150-152, 229-241`), which gate on the preset NAME and the WEEKDAY tier shape.
- `glsa-jordbrug-praktikant-udl-andet` is out of scope — it carries no `ANIMAL_*` codes at all (CODE-TRUTH.md:308-318).

---

## Packet 8 — Cross-family regimes and engine-level gaps

PROPOSED — not product-decided. No code changes.

### G-030 — whole 12-hour weekday wrongly attributed to NORMAL (the banded Standard presets)
**Current:** for Jordbrug Standard, the WEEKDAY band `21600-64800 → NORMAL` (CODE-TRUTH.md:55) spans the entire 06:00-18:00 window; the WEEKDAY tiers `1: 26640 → NORMAL; 2: 33840 → OVERTIME_30; 3: null → OVERTIME_80` sit on the same row and never execute, because bands beat tiers whenever both exist and the exception is gated by preset name to the two Udenlandske praktikanter presets (CODE-TRUTH.md:548, 551-556). Gartneri Standard (CODE-TRUTH.md:183) and Skovbrug Standard (CODE-TRUTH.md:216) are named in the same dead-tier list — the "thirteen other preset/day combinations" at CODE-TRUTH.md:558.
**Agreement requires:** G-030 — "1. og 2. time efter normal daglig arbejdstids ophør (+30 % af C-løn)" (jordbrug-2026-2029.txt:734); § 22 stk. 1 tiers work beyond the normal day under either reading, so attributing a whole 12 h weekday to NORMAL is wrong on both.

The ledger scopes G-030 to "the 8 banded 'Standard' presets" — matching CODE-TRUTH.md:558's "thirteen other preset/day combinations" list of preset *names*: Jordbrug Standard, Jordbrug Dyrehold, Gartneri Standard, Skovbrug Standard, KA Svine, KA Plante, KA Maskin, KA Gron. Four of those eight are the non-GLS-A `ka-landbrug-svine-*`/`ka-landbrug-plante-*`/`ka-landbrug-maskin-*`/`ka-gron-*` presets that CODE-TRUTH.md:24 explicitly scopes out of this audit ("out of scope for this audit and not covered below"), leaving exactly the 4 GLS-A presets named in the table below.

**Proposed encoding — `glsa-jordbrug-standard`, `glsa-jordbrug-dyrehold`, `glsa-gartneri-standard`, `glsa-skovbrug-standard`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY (Jordbrug Standard) | 1: 26640 → `NORMAL`; **2: 33840 → `OVERTIME_30` — must actually execute past 7,4 h elapsed**; 3: null → `OVERTIME_80` | 14400-21600 `SHIFTED_MORNING`; 21600-64800 `NORMAL` (must stop contributing past 26640 s elapsed within the shift); 64800-72000 `SHIFTED_EVENING` |
| WEEKDAY (Gartneri Standard) | 1: 26640 → `NORMAL`; **2: 33840 → `OVERTIME_50`**; 3: null → `OVERTIME_100` | unchanged shape (CODE-TRUTH.md:183) |
| WEEKDAY (Skovbrug Standard) | 1: 26640 → `NORMAL`; **2: 33840 → `OVERTIME_30`**; 3: null → `OVERTIME_100` | unchanged shape (CODE-TRUTH.md:216) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **bands and overtime tiers stacking on the same day for non-praktikant presets**.
**Justification:**
- A 12 h weekday entirely inside the `21600-64800 NORMAL` band produces zero overtime lines even though the tier column beside it already defines the escalation from 26640 s — the data is present and simply not executed (CODE-TRUTH.md:548, 558, 561: "the normal/overtime tier split is a name+shape-gated exception, not general engine behavior").
- The gate is not only a name whitelist: `HasNormalTimeBoundaryShape` additionally hardcodes tier 2 `PayCode == "OVERTIME_50"` and tier 3 `PayCode == "OVERTIME_80"` (CODE-TRUTH.md:556). Jordbrug Standard/Dyrehold use `OVERTIME_30`/`OVERTIME_80` and Skovbrug Standard `OVERTIME_30`/`OVERTIME_100`, so neither matches even if the name whitelist were widened. Enabling this needs both the name gate widened *and* the pay-code string match generalised.
- G-030 — jordbrug-2026-2029.txt:734 is unsatisfiable while bands unconditionally suppress tiers for these presets. This is the single most consequential engine gap in the document: it silently zeroes overtime on the four banded Standard presets.

### M-001 — Flekstid (opt-in flex-account banking)
**Current:** `PayTierRule` carries only `PayDayRuleId, UpToSeconds, PayCode, PayrollCode, Order` (CODE-TRUTH.md:590) — no balance field — and `UpToSeconds` is evaluated fresh per calendar day (CODE-TRUTH.md:545-561), so no surplus/deficit is carried across days.
**Agreement requires:** M-001 — "Under forudsætning af lokal enighed er der adgang til at træffe aftale om flekstid." (jordbrug-2026-2029.txt:431); account limits at skovbrug-2024-2026.txt:361-362; "Beordret overarbejde udløser overtidsbetaling efter overenskomstens regler herom." (skovbrug-2024-2026.txt:366).

**Proposed encoding — `glsa-<family>-flekstid` (opt-in overlay on the family's Standard preset)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `FLEKS_BANKED` (variance banked, not overtime-coded); 3: null → `OVERTIME_80` (beordret overtime only) | unchanged from the underlying Standard preset |
| SATURDAY-HOLIDAY | unchanged from the underlying preset | unchanged |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **cross-day hour banking account (flex balance) per employee**.
**Justification:**
- The account needs a persisted running balance across pay periods — M-001, limits at skovbrug-2024-2026.txt:361-362; no such field exists on any entity (CODE-TRUTH.md:590-592).
- `FLEKS_BANKED` above is only a relabelled tier boundary: it still emits a normal per-day `PayLine` and cannot withhold hours from that day's payout, so it renames rather than implements banking.
- The beordret carve-out — M-001, skovbrug-2024-2026.txt:366 — additionally requires distinguishing ordered from voluntary variance at the same boundary, a distinction the engine has no field for.

### M-003 — Fastlønsaftaler (opt-in fixed-salary absorption)
**Current:** `PlanRegistrationPayLine` carries `PlanRegistrationId, PayCode, PayrollCode, Hours, HoursInSeconds, PayRuleSetId, CalculatedAt` (CODE-TRUTH.md:592) — no opt-in/absorption flag; every worked second is routed through the band-or-tier path and emitted as an hour-tagged line (CODE-TRUTH.md:545-561, 596-604).
**Agreement requires:** M-003 — "Der er adgang til indgåelse af frivillige og individuelle aftaler om fast løn. Ved fast løn for-" / "stås aftaler, hvor lønnen indeholder betaling for normal arbejdstid, overarbejde og eventu-" / "elt arbejde på forskudt tid, holddrift og/eller weekender." (jordbrug-2026-2029.txt:678-680).

**Proposed encoding — `glsa-<family>-fastloen` (per-employee opt-in override)**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: null → `FASTLON_ABSORBED` | (none — all clock-time distinctions collapsed) |
| SATURDAY | 1: null → `FASTLON_ABSORBED` | (none) |
| SUNDAY | 1: null → `FASTLON_ABSORBED` | (none) |
| HOLIDAY | 1: null → `FASTLON_ABSORBED` | (none) |
| GRUNDLOVSDAG | 1: null → `FASTLON_ABSORBED` | (none) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **per-employee opt-in regime flag on a pay rule set**.
**Justification:**
- The table above is mechanically producible, but the regime is "frivillige og individuelle aftaler" (M-003, jordbrug-2026-2029.txt:678) — an *individual* opt-in, not a crew-wide preset swap. Nothing in the data model (CODE-TRUTH.md:590-592) flags one employee on a shared roster as opted in while colleagues on the identical shift stay on the ordinary preset.
- The clause requires the line to leave per-minute attribution entirely (jordbrug-2026-2029.txt:679-680), but the engine's exit paths always emit an hour-tagged line for worked time (CODE-TRUTH.md:596-604), so `FASTLON_ABSORBED` is an approximation, not an implementation.

### M-004 — 3-hour minimum-work floor on søn-/helligdage (and Skovbrug lørdage) — BORDERLINE
**Current:** Engine facts (e): "No such cap or floor exists anywhere in the engine code searched" (CODE-TRUTH.md:606-608).
**Agreement requires:** M-004 — Skovbrug: "Ved overarbejde på lørdage samt søn- og helligdage har medarbejdere ret til forinden at" / "forlange mindst 3 timers arbejde." (skovbrug-2024-2026.txt:872-873); Jordbrug/Golf: "mindst 3 timers arbejde." (jordbrug-2026-2029.txt:750; identical golf-2024-2026.txt:480); Gartneri variant: "Medarbejderne har pligt til på sådanne dage at møde til normal tid, men har krav på" / "mindst 3 sammenhængende timer." (gartneri-2024-2026.txt:756-757).

**Table 1 — reading (1): scheduling guarantee, no pay-code effect**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SUNDAY | unchanged, e.g. 1: null → `SUN_HOLIDAY` (Jordbrug Standard, CODE-TRUTH.md:57) | unchanged |
| HOLIDAY | unchanged | unchanged |
| SATURDAY (Skovbrug only) | unchanged (CODE-TRUTH.md:217) | unchanged |

**Expressibility (Table 1):** EXPRESSIBLE — no encoding change needed; a guarantee that the employee will not be called for less than 3 hours has no pay-code consequence.

**Table 2 — reading (2): claimed-hours pay floor**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| SUNDAY | 1: **floor 10800 s (3 h)** → `SUN_HOLIDAY_MIN_FLOOR` (paid even if fewer seconds worked); 2: remaining actual seconds → `SUN_HOLIDAY` | unchanged |
| HOLIDAY | 1: **floor 10800 s** → `HOL_MIN_FLOOR`; 2: → `SUN_HOLIDAY` | unchanged |
| SATURDAY (Skovbrug) | 1: **floor 10800 s** → `SAT_MIN_FLOOR`; 2: → `SAT_*` | unchanged |

**Expressibility (Table 2):** NOT-EXPRESSIBLE — missing capability: **minimum-hours floor independent of actual worked seconds** (`UpToSeconds` caps a tier from above; it cannot pad a payout above worked seconds — Engine facts (e), CODE-TRUTH.md:606-608).
**Verdict:** BORDERLINE, carried forward unresolved. The ledger's own note holds that the text supports a right to *demand work*, not explicitly a right to claimed pay for unworked time, so reading (1) is at least as well supported as reading (2). This packet does not pick one.

### M-005 — Protokollat om alternativ arbejdstidsplanlægning, stk. 1
**Current:** no fleksibilitetstillæg-shaped band exists in any preset (CODE-TRUTH.md:53-71, 178-220); SATURDAY/SUNDAY/HOLIDAY always route to `SAT_*`/`SUN_HOLIDAY`, never a flat per-hour supplement across all seven days.
**Agreement requires:** M-005 — "Den normale ugentlige arbejdstid på 37 timer kan placeres på op til 5 ugentlige arbejds-" / "dage. Arbejdstiden kan placeres på alle ugens dage, samt på søgnehelligdage og over-" / "enskomstmæssige fridage i tidsrummet fra kl. 6.00 - 18.00." (jordbrug-2026-2029.txt:3210-3212); "Medarbejdere, der arbejder efter denne bestemmelse, modtager et fleksibilitetstillæg, der" / "udgør kr. 4,10 pr. time samt særligt tillæg for arbejde på lørdage, søndage, søgnehellig-" / "dage og overenskomstmæssige fridage, jf. stk. 4." (jordbrug-2026-2029.txt:3219-3221).

**Proposed encoding — `glsa-<family>-alt-arbejdstidsplan`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | (tier path unused) | Monday-Friday 21600-64800 (06:00-18:00) `FLEKS_NORMAL` (p1) |
| SATURDAY | (tier path unused) | Saturday 21600-64800 `FLEKS_NORMAL` (p1) **+ `FLEKS_WEEKEND_SUPPLEMENT` over the same seconds (jf. stk. 4)** |
| SUNDAY | (tier path unused) | Sunday 21600-64800 `FLEKS_NORMAL` (p1) **+ `FLEKS_WEEKEND_SUPPLEMENT` over the same seconds** |
| HOLIDAY | (tier path unused) | Holiday 21600-64800 `FLEKS_NORMAL` (p1) **+ `FLEKS_WEEKEND_SUPPLEMENT` over the same seconds** |
| outside 06:00-18:00, or > 9,25 h/day | out of the protokollat's scope — kapitel 3 applies | — |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **concurrent same-second stacking of two independent pay-code lines**.
**Justification:**
- The 21600-64800 window and the reroute onto a flat per-hour supplement are ordinary `PayTimeBandRule` shapes and individually expressible; Jordbrug Dyrehold's SUNDAY 0-86400 `ANIMAL_SUN_HOLIDAY` band (CODE-TRUTH.md:69) is direct precedent for overriding a normally-`SUN_HOLIDAY` day with a flat replacement code.
- What breaks it: stk. 1's closing sentence requires **both** the flat fleksibilitetstillæg **and** the stk. 4 weekend supplement for the identical seconds — M-005, jordbrug-2026-2029.txt:3219-3221. `PayTimeBandRule` has one `PayCode` per row (CODE-TRUTH.md:591) and the routing picks a single path per day (CODE-TRUTH.md:545-561); nothing emits two rule rows concurrently for the same clock range.

### M-006 — Same protokollat, stk. 3: late schedule change pays the overtime rate
**Current:** all routing keys on `DayType`/clock position or cumulative `UpToSeconds` (CODE-TRUTH.md:545-561); nothing reads a schedule-change event.
**Agreement requires:** M-006 — "minimum 5 x 24 timer til en kalenderuges udgang. Hvis arbejdstiden ændres med et kor-" / "tere varsel, betales et tillæg for den ændrede arbejdstid, svarende til overenskomstens be-" (jordbrug-2026-2029.txt:3262-3263).

**Proposed encoding — `glsa-<family>-alt-arbejdstidsplan`, schedule-change override**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: null → `SCHEDULE_CHANGE_OT` — **only** for hours moved on < 5 × 24 h notice; otherwise unchanged `FLEKS_NORMAL` | unchanged from the M-005 table |
| SATURDAY, SUNDAY, HOLIDAY | 1: null → `SCHEDULE_CHANGE_OT` (same condition) | unchanged |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **schedule-change notice-period event trigger for a pay-code override**.
**Justification:**
- The trigger is procedural: whether the *plan* was altered with less than "5 x 24 timer til en kalenderuges udgang" notice (M-006, jordbrug-2026-2029.txt:3262) — not a function of the worked second's clock position or cumulative daily seconds, the only two axes the rule entities support (CODE-TRUTH.md:590-591).
- No entity records an arbejdstidsplan version or its publication timestamp, so there is nothing to compare the 5 × 24 h against; `SCHEDULE_CHANGE_OT` cannot be conditioned on anything the engine tracks.

### M-011 — Gartneri § 8 stk. 4 / Skovbrug § 7 stk. 3: 26-week averaging with a hard 45 t ceiling
**Current:** all pay computation is per-day and tier `UpToSeconds` resets daily (CODE-TRUTH.md:545-561); no weekly or period accumulation exists anywhere in the engine.
**Agreement requires:** M-011 — Gartneri: "for en periode på højst 26 uger ikke overstiger 37 timer. Arbejdstiden i den enkelte uge må" / "dog ikke overstige 45 timer." (gartneri-2024-2026.txt:305-306); Skovbrug: "nemsnitlige ugentlige arbejdstid er 37 timer inden for en periode på højst 26 uger." / "Arbejdstiden i den enkelte uge må dog ikke overstige 45 timer." (skovbrug-2024-2026.txt:331-332).

**Proposed encoding — `glsa-gartneri-standard`, `glsa-skovbrug-standard`, WEEKDAY period overlay**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: **weekly cumulative > 162000 s (45 h)** → `PERIOD_OVERTIME_45`; 3: **26-week rolling average > 133200 s (37 h)** → `PERIOD_AVG_ADJUST` | unchanged (CODE-TRUTH.md:183, 216) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **weekly/period hour accumulation across days**.
**Justification:**
- `tier.UpToSeconds` is a per-day cumulative threshold that resets daily (CODE-TRUTH.md:545-561, 608); it can express neither a 45 t single-week ceiling nor a 26-week rolling average, both of which need state spanning calendar days.
- This is a third averaging shape distinct from Jordbrug's 8-week § 8 stk. 2 (G-060) and § 9 stk. 5's above-45 premium; all three share the identical missing capability, so the placeholder codes above are illustrative only.

### G-060 — Jordbrug § 8 stk. 2: 37 h averaged over 8 weeks, unmodelled
**Current:** § 8 stk. 2's averaging has no execution path — computation is per-day and tier `UpToSeconds` resets daily (CODE-TRUTH.md:545-561).
**Agreement requires:** G-060 — "Ved arbejde med pasning af dyr er den ugentlige arbejdstid indtil 37 timer i gennemsnit" (jordbrug-2026-2029.txt:312), continuing at 313.

**Proposed encoding — `glsa-jordbrug-praktikant-udl-staldarbejde`, WEEKDAY period overlay**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: 33840 → `OVERTIME_50`; 3: null → `OVERTIME_80` (existing, `usesNormalTimeSplit`-eligible per CODE-TRUTH.md:553-556) **+ hypothetical 4: 8-week rolling average > 133200 s → `PERIOD_OVERTIME_8WK`** | (none) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **weekly/period hour accumulation across days**.
**Justification:**
- This preset already qualifies for the daily normal/overtime split (one of exactly two matching `IsNormalTimeSplitPresetName`, CODE-TRUTH.md:553-556), but that mechanism governs a single day's boundary at 26640 s and has no notion of an 8-week window.
- `PERIOD_OVERTIME_8WK` cannot be triggered by any existing field; the only threshold primitive is `tier.UpToSeconds`, which resets daily (CODE-TRUTH.md:608) — the identical gap as M-011, confirming this is document-wide, not preset-specific.

### G-079 — Jordbrug § 22 stk. 4 weekly netting with two absence exceptions
**Current:** the same accumulation gap as G-060/M-011, plus: no rule entity carries any absence or reason field (CODE-TRUTH.md:590-592).
**Agreement requires:** G-079 — "Ved opgørelse af overarbejde fradrages forsømt tid af den normale ugentlige arbejdstid," (jordbrug-2026-2029.txt:761), with the two exceptions at 762-763 ("en medarbejderen utilregnelig grund"; "en grund, som er rettidigt anmeldt til arbejdsgiveren og godkendt af denne").

**Proposed encoding — `glsa-jordbrug-standard`, `glsa-jordbrug-dyrehold`, WEEKDAY netting overlay**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | 1: 26640 → `NORMAL`; 2: **(weekly worked seconds − culpable-absence seconds, excluding the two named exception reasons) > weekly norm** → `NETTED_OVERTIME_30`; 3: null → `NETTED_OVERTIME_80` | unchanged (CODE-TRUTH.md:55) |

**Expressibility:** NOT-EXPRESSIBLE — missing capabilities: **weekly/period hour accumulation across days** *and* **per-absence reason codes feeding weekly netting**.
**Justification:**
- Netting first needs a weekly total to subtract from — the same missing accumulation as G-060/M-011.
- Even with that, G-079 (jordbrug-2026-2029.txt:761-763) requires classifying *why* time was missed, with exactly two exempting reasons, per absence event. No entity has an absence-reason field (CODE-TRUTH.md:590-592), so a naive netting implementation would erode overtime for sick or approved-absence workers exactly as the row warns.

### M-021 — Holddrift: shift-category-scoped weekly norm
**Current:** the `DayType` enum has exactly 8 calendar-derived values (CODE-TRUTH.md:568-578); there is no shift-number axis, and no `glsa-holddrift-*` preset exists at all.
**Agreement requires:** M-021 — "Den ugentlige arbejdstid er på 1. skift 37 timer, på 2. og 3. skift 34 timer." (ratesheet-holddriftstillaeg-2026.txt:16).

**Proposed encoding — two new presets `glsa-holddrift-skift1`, `glsa-holddrift-skift23`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY (`glsa-holddrift-skift1`) | 1: **133200** (37 h weekly norm) → `HOLDDRIFT_SKIFT1_NORMAL`; 2: null → `HOLDDRIFT_SKIFT1_OVERTIME` | see M-022 for the clock-window overlay |
| WEEKDAY (`glsa-holddrift-skift23`) | 1: **122400** (34 h weekly norm) → `HOLDDRIFT_SKIFT23_NORMAL`; 2: null → `HOLDDRIFT_SKIFT23_OVERTIME` | see M-022 |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **shift-category-scoped normal-hours norm**.
**Justification:**
- Both tables are individually well-formed, and the seconds are direct conversions of M-021's figures (ratesheet-holddriftstillaeg-2026.txt:16): 37 × 3600 = 133200, 34 × 3600 = 122400. But these are *weekly* norms and `UpToSeconds` is a per-day threshold, so they also inherit the accumulation gap.
- The deeper gap: nothing in `DayType` (CODE-TRUTH.md:568-578) or elsewhere records which skift an employee's day belongs to, so the engine has no basis to select one preset over the other — that assignment would have to live outside the pay-rule-set model.

### M-022 — Holddriftstillæg clock windows
**Current:** no `glsa-holddrift-*` preset exists, but the mechanism is exactly clock-window-shaped: `PayTimeBandRule (PayDayTypeRuleId, StartSecondOfDay, EndSecondOfDay, PayCode, PayrollCode, Priority)` (CODE-TRUTH.md:591), and Jordbrug Dyrehold's WEEKDAY `0-18000 ANIMAL_NIGHT` band (CODE-TRUTH.md:67) is direct precedent for a night band continuing past midnight.
**Agreement requires:** M-022 — "Fra kl. 17.00 – 06.00" (ratesheet-holddriftstillaeg-2026.txt:22, under "Mandag til fredag:" at line 21); "Fra lørdag kl. 14.00 til søndagsdøgnets afslutning" (ratesheet-holddriftstillaeg-2026.txt:24, under "Lørdag og søndag samt søgnehelligdage:" at line 23).

**Proposed encoding — `glsa-holddrift-skift1`, `glsa-holddrift-skift23`, clock-window overlay**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| WEEKDAY | unchanged from M-021 | Monday-Friday: 0-21600 (00:00-06:00, continuation of the prior evening) `HOLDDRIFT_NIGHT_TILLAEG` (p1); 61200-86400 (17:00-24:00) `HOLDDRIFT_NIGHT_TILLAEG` (p1) |
| SATURDAY | unchanged | Saturday: 50400-86400 (14:00-24:00) `HOLDDRIFT_WEEKEND_TILLAEG` (p1) |
| SUNDAY | unchanged | Sunday: 0-86400 `HOLDDRIFT_WEEKEND_TILLAEG` (p1) |
| HOLIDAY | unchanged | Holiday: 0-86400 `HOLDDRIFT_WEEKEND_TILLAEG` (p1) |

**Expressibility:** EXPRESSIBLE for the band geometry itself.
**Justification:**
- M-022 — ratesheet-holddriftstillaeg-2026.txt:22 under the heading at 21: 17:00 = 61200 s, 06:00 = 21600 s, expressed exactly as Jordbrug Dyrehold's existing split night/evening pattern (CODE-TRUTH.md:67).
- M-022 — ratesheet-holddriftstillaeg-2026.txt:24 under the heading at 23: lørdag 14:00 = 50400 s through end of day, continuing as a full-day Sunday band; the heading also scopes søgnehelligdage into the same window.
- Caveat: whether these codes may additionally stack with an overtime tier — "For overarbejde på de tidspunkter, hvor der ydes holddriftstillæg, betales foruden" (ratesheet-holddriftstillaeg-2026.txt:26, continuing at 27) — is the same concurrent-stacking gap as G-030/M-005, and is out of scope for this table.

### M-023 — Holddrift fridag categories (erstatningsfridag / vagtlistefridag)
**Current:** `DayType`/`dayCode` are computed purely from `date.DayOfWeek` and a bundled holiday JSON, never from an individual's roster (CODE-TRUTH.md:568-578, 583-584).
**Agreement requires:** M-023 — "Arbejde på erstatningsfridag" (ratesheet-holddriftstillaeg-2026.txt:31, under the heading "Arbejde på eller forskydning af fridag" at line 30); "Forskydning af vagtlistefridag" (ratesheet-holddriftstillaeg-2026.txt:32).

**Proposed encoding — new dayCodes `ERSTATNINGSFRIDAG` / `VAGTLISTEFRIDAG_FORSKYDNING`**

| DayCode | Tier order / upToSeconds / payCode | dayTypeRules bands (dayType, start–end sec, payCode) |
|---|---|---|
| `ERSTATNINGSFRIDAG` | 1: null → `HOLDDRIFT_ERSTATNINGSFRIDAG` | (none) |
| `VAGTLISTEFRIDAG_FORSKYDNING` | 1: null → `HOLDDRIFT_VAGTLISTEFRIDAG_FORSKYDNING` | (none) |

**Expressibility:** NOT-EXPRESSIBLE — missing capability: **per-employee roster (scheduled-off) input**.
**Justification:**
- Both rules are trivially valid in isolation, but nothing can ever *select* them: they depend on knowing that this date was *this employee's* rostered day off, and dayCode is derived from the calendar date or the holiday JSON only (CODE-TRUTH.md:568-578, 583-584).
- M-023 — ratesheet-holddriftstillaeg-2026.txt:31 and 32 are two distinct triggers under one heading (line 30), each with its own per-hour rate, both blocked on the same missing roster input.

---

## Engine gaps

The deduplicated union of every capability named NOT-EXPRESSIBLE above, with the packets that need it. Nothing here is a proposal to build; it is the list of things that must exist before the corresponding tables above could ship.

### Routing and stacking

| # | Missing capability | Needed by |
|---|---|---|
| E1 | **Bands and overtime tiers stacking on the same day for non-praktikant presets** — equivalently, a clock-keyed segment coexisting with an elapsed-seconds tier ladder. Today the band path wins outright whenever any band exists for a day type, and the tier-on-top exception is gated by preset *name* plus an exact tier *shape* (CODE-TRUTH.md:548, 553-556, 561). | 3 (M-013), 5 (G-018 WEEKDAY, M-017), 6 (G-092, M-015), 7 (M-009), 8 (G-030) |
| E2 | **Concurrent same-second stacking of two independent pay-code lines** — one worked second drawing two supplements at once. `PayTimeBandRule` carries one `PayCode` per row and the router picks a single path per day. | 8 (M-005, M-022 caveat) |
| E3 | **Clock-time split routing generalised beyond the hardcoded `dayCode == "GRUNDLOVSDAG"` check and the two-preset name+shape gate.** | 4 (G-094/G-053, 1 May Agro kartoffelsortering) |
| E4 | **Shift-relative (rather than clock-of-day) band boundaries** — "1./2. klokketime efter normal arbejdstid" counts from whenever the shift ends (§ 4 a); § 4 d's søn-/helligdag bands are anchored the same way, from "den daglige normale arbejdstids begyndelse" to noon and from noon back to "den normale arbejdstids begyndelse" (agroindustri-2026-2029.txt:3616, 3621) — a boundary that moves with the shift, not a fixed clock time. | 6 (G-019 § 4 a, § 4 d) |
| E5 | **Pre-shift (before-normal-start) attribution segment** — the tier model counts forward from shift start only. | 2 (M-002), 5 (M-017), 6 (M-019) |
| E6 | **Duration-capped floating pre-shift segment** — `min(actual pre-shift seconds, 1800)`, with no fixed clock window to anchor it. | 2 (M-002) |

### Day-type granularity

| # | Missing capability | Needed by |
|---|---|---|
| E7 | **Distinct DayType/dayCode value for 1 May** — the enum has one generic `Holiday`, so a 1-May-only rate cannot be isolated from Christmas Day (CODE-TRUTH.md:565-581). 31 December needs no distinct value: both its tables below (Gartneri, Agro kartoffelsortering) are blocked by E8/E17 or resolved as config-only, never by day-type granularity. | 4 (G-095) |
| E8 | **Employer-configurable holiday-date selection** — the Gartneri 24-or-31 December local choice; the holiday JSON is a fixed date map for the whole customer base. | 4 (G-095, Gartneri) |
| E9 | **Per-employee roster / guaranteed-day-off input** — working an erstatningsfridag, a displaced vagtlistefridag, or an individually guaranteed hverdagsfridag; day types are calendar-derived only. | 6 (M-020), 8 (M-023) |

### Amounts and units

| # | Missing capability | Needed by |
|---|---|---|
| E10 | **Flat-DKK amount field on a pay code** — no rule or pay-line entity carries a rate or amount column at all (CODE-TRUTH.md:588-594). | 6 (G-019, M-015, M-019, M-020, G-092) |
| E11 | **Unit-of-measure field (per-hour vs per-day/flat)** on pay rules and pay lines — a "pr. dag" clause and a "pr. time" clause are indistinguishable in-engine. | 7 (G-033) |

### Conditions the rule model cannot express

| # | Missing capability | Needed by |
|---|---|---|
| E12 | **Tier boundary conditional on same-day runtime state** — the after-shift band shrinking because pre-shift overtime was worked. | 5 (M-018) |
| E13 | **Task/activity-type dimension on a pay rule** — the fodring/æg-indsamling 80 % override replaces the day-type rate for specific *kinds* of work. | 7 (M-008) |
| E14 | **Whole-shift-length gating condition** — "if the day's total is ≤ 4 h, supplement every hour"; `UpToSeconds` splits within a day, it cannot test the day's total. | 3 (M-012) |
| E15 | **Notice / advance-warning-conditioned pay trigger** — whether a shift or plan change was announced by a deadline. | 3 (M-012 item 2, M-014), 8 (M-006) |
| E16 | **Minimum-hours floor independent of actual worked seconds** — `UpToSeconds` caps from above and cannot pad a payout upward. Carried as BORDERLINE: the ledger holds the scheduling-guarantee reading at least as well supported. | 8 (M-004) |
| E17 | **Shift-length / cutoff-time enforcement** — "arbejdet slutter senest kl. 12.00"; Engine facts (e) records no cap or floor logic anywhere (CODE-TRUTH.md:606-608). | 4 (G-095, Gartneri) |

### State the engine does not carry

| # | Missing capability | Needed by |
|---|---|---|
| E18 | **Weekly / period hour accumulation across days** — every threshold resets daily, so no 8-week average, 26-week average, 45 t/uge ceiling or weekly norm can be evaluated. | 8 (G-060, G-079, M-011, M-021, G-078) |
| E19 | **Per-absence reason codes feeding weekly netting** — § 22 stk. 4 nets culpable absence off the weekly total with exactly two exempting reasons; a naive implementation would erode overtime for sick and approved-absence workers. | 8 (G-079) |
| E20 | **Cross-day hour banking account (flex balance) per employee**, including a beordret-vs-voluntary distinction at the same boundary. | 8 (M-001) |
| E21 | **Per-employee opt-in regime flag on a pay rule set** — fastløn absorption for one employee on a shared roster; and, at establishment level, a regime toggle that overrides a preset's default day-type pay code (Weekendarbejde suppressing søgnehelligdagsforskud). | 6 (M-016), 8 (M-003) |
| E22 | **Worker-category conditional rule selection (deltid)** — the catalogue varies only by product line and elev status. | 3 (M-012), 6 (M-015) |
| E23 | **Shift-category-scoped normal-hours norm** — Holddrift's 1. skift 37 t vs 2./3. skift 34 t; nothing records which skift a day belongs to. | 8 (M-021) |

### Not engine gaps

Two items above are blocked by something other than engine capability, and are recorded here so they are not mistaken for code work:

- **Missing source text** — the DAG/3F Anlægsgartneroverenskomst § 22 stk. 1-5 incorporated by Gartneri § 2 is not in the corpus, so M-010's ladder cannot be written at all. Acquiring the text comes first (Packet 3).
- **Catalogue modelling, not engine** — a job-function preset axis (detailsalg under M-013; anlægsgartner under M-010; the nine baseless Elev presets and the three new kapitel-22 preset candidates) is a product/catalogue decision. The engine can express the resulting presets; someone has to decide they should exist, and how existing rows migrate onto them (Packets 1, 3, 7).
