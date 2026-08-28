# GLS-A / 3F Jordbrugsoverenskomsten — the overtime basis, and what the engine implements

Findings consolidated 2026-08-24. Supersedes nothing; extends the daily-boundary section of
`overenskomst-research-findings.md` (decided 2026-08-07) with primary-source and external
evidence gathered 2026-08-23/24.

**Position taken:** the overtime trigger under this agreement is **per day**, and the engine's
daily tier ladder is the correct shape. The daily boundary *value* is not stated in the
agreement; 7,4 t (26640 s) is a derivation from § 8 stk. 1 (37 ÷ 5), not agreement text.
The counter-evidence for a period-based reading of staldarbejde is recorded in section 4 below and
is not dismissed — it is the one point that would need GLS-A to close.

Primary text throughout:
`/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/docs/superpowers/specs/sources/jordbrug-2026-2029.txt`
(Jordbrugsoverenskomsten 4010, GLS-A / 3F Den Grønne Gruppe, 2026-2029, 2. udgave 06.07.2026;
SHA-256 and source URL in `sources/SOURCES.md`). Line numbers refer to that extraction.

---

## 1. The trigger is daily — the evidence

**§ 22 stk. 1 (lines 731-734), verbatim:**

> Stk. 1 Overarbejdsbetaling
> For overarbejde efter den normale arbejdstids ophør betales følgende:
> 1. og 2. time efter normal **daglig** arbejdstids ophør (+30 % af C-løn)
> For overarbejde herudover samt søn- og helligdage (+80 % af C-løn)

The operative trigger contains the word *daglig*, and the rate table is structured per day —
"1. og 2. time" counted from that day's cessation point. This is the agreement's own wording,
not an interpretation.

**§ 22 stk. 3 (lines 752-758)** corroborates it structurally:

> Overarbejde er medarbejderne forpligtede til at udføre, når arbejdsgiveren anser det for
> nødvendigt af hensyn til driften. Om sådant overarbejde skal medarbejderne have besked
> **senest inden middag den pågældende dag**.
> I tilfælde hvor overarbejdet består i søn- og helligdagsarbejde, skal medarbejderne have
> besked senest dagen før.

A same-day notice deadline presupposes a daily baseline to notify against. Reinforced at
line 449: *"**Beordret** overarbejde udløser overtidsbetaling efter overenskomstens regler herom."*
Overtime under this agreement is ordered against a daily norm, not accrued against a period budget.

**External corroboration — the daily basis is standard Danish drafting.**

- *Industriens Overenskomst § 13 stk. 2* defines overtime as work outside
  "den i den enkelte uge fastlagte daglige arbejdstid for den enkelte medarbejder" — daily,
  and explicitly plan-anchored. Different agreement; does not govern here; establishes the pattern.
- *Faglig voldgift FV 2015.0097* (2. november 2015, Kristelig Fagforening mod Kristelig
  Arbejdsgiverforening, https://arbejdsretten.dk/media/16489/kendelse-kf-mod-ka-fv-2015-0097.pdf)
  construes KA/KF § 10 stk. 1, which defines overarbejde as work "ud over den planlagte daglige
  arbejdstid". Also daily. The same agreement handles short-notice *placement* changes through a
  separate deeming clause — "betales der for de ændrede timer **som ved** overarbejde" — which sits
  definitionally outside the overtime definition. Jordbrug has no such deeming clause.

**Placement is a separate instrument, not overtime.** § 23 Forskudt arbejdstid pays a
forskydningstillæg for hours up to 2 h before 06:00 or after 18:00. GLS-A's own guidance states
the two are alternatives, not cumulative. Working a full planned duration displaced in time is
therefore *not* overtime under this agreement — it is at most forskudt tid. For staldarbejde it is
not even that; see section 4.

---

## 2. The daily boundary value is NOT stated in the agreement

§ 22 stk. 1 never states a figure for "normal daglig arbejdstid". Searching the 2026-2029 text:

- `planlagt arbejdstid`, `arbejdsplan` — appear only inside the opt-in flexible regimes
  (§ 9 stk. 4, § 9 stk. 5, and the kapitel 21 protokollat), never in the ordinary regime § 22 governs.
- `vagtplan`, `aftalt arbejdstid` — do not occur at all.
- `7,4` — occurs only as the **søgnehelligdag reduction unit** (§ 9 stk. 2; protokollat stk. 2),
  never as a stated daily norm.

So 26640 s is a derivation: § 8 stk. 1's *"Den normale effektive arbejdstid er indtil 37 timer
ugentlig"* (line 309), divided by 5. It is a reasonable and conventional derivation, and 7,4 is
demonstrably the agreement's own daily unit — but it is not quoted agreement text, and any claim
that it is should be corrected.

**The ordinary regime imposes no planning duty at all.** § 9 stk. 1 (lines 377-378) states only a
permissible window: *"Den daglige arbejdstid lægges mandag til lørdag, mellem kl. 6.00 og kl. 18.00."*
A mandatory written plan and a 6-hour daily floor exist **only** in the opt-in protokollat
(kapitel 21, lines 3204-3327). The drafters knew how to require a plan and chose to require it only
in the alternative regime. Consequence: in the ordinary regime there may be no plan to anchor a
boundary to, which is a structural argument for keeping the derived 7,4 t as the default.

---

## 3. What § 50 fixes for udenlandske praktikanter

**§ 50 stk. 4 c (lines 2207-2212)** — the rates, and they match the engine exactly:

> Overarbejde og arbejde på søn- og helligdage afregnes med et tillæg til praktikantens
> normaltimeløn på **50 % for de første 2 timer og herefter 80 %** eller tilsvarende frihed.
> Overarbejde afspadseres eller betales med overarbejdsbetaling efter praktikantens ønske.

This is the `26640 / 33840 / null` ladder: normal time, then a 2-hour OVERTIME_50 band, then
OVERTIME_80 open-ended. Note the rates differ from ordinary staff (§ 22 stk. 1 gives 30 %/80 %),
and that overtime is a multiple of the *praktikant's own* rate, not the § 22 C-løn basis.

**§ 50 stk. 4 d (lines 2213-2222)** — the staldarbejde supplements:

> For arbejde **i normal arbejdstid** på lørdag eftermiddag betales et tillæg **pr. dag** på:
> pr. 1. marts 2026 ... kr. 73,90
> For arbejde **i normal arbejdstid** på søn- og helligdage betales et tillæg **pr. dag** på:
> pr. 1. marts 2026 ... kr. 177,60

Two things follow, both load-bearing:

1. These are **flat per-day amounts**, not hourly rates. See § 5 for the engine discrepancy.
2. They are payable only for work **i normal arbejdstid**. A day treated as entirely overtime
   cannot simultaneously claim the supplement. This is a hard consistency constraint on any
   reading that makes whole days overtime.

**§ 50 stk. 7 (lines 2255-2257):** *"Overenskomstens øvrige bestemmelser er gældende for
praktikanter, hvor andet ikke følger af § 50."* The rest of the agreement applies by default;
§ 50 contains no carve-out from the arbejdstid rules or from the kapitel 21 protokollat.

**Salary basis.** § 50 stk. 4 a's monthly figures divide to 160,33 t/md (13.315,41 ÷ 83,05),
and the agreement's own conversion constant is attested at lines 3041-3042:
*"...omregnes timelønnen til månedsløn med det gældende timetal, p.t. 160,33."*
160,33 = 37 × 52 ÷ 12. A praktikant is a full-timer at 37 t/uge by definition.

---

## 4. The counter-evidence, recorded — staldarbejde and the period framing

This is the one point the daily reading does not fully dispose of, and it is recorded here
deliberately rather than omitted.

**§ 8 stk. 2 (lines 311-313):**
> Ved arbejde med pasning af dyr er den ugentlige arbejdstid indtil 37 timer **i gennemsnit over
> en periode på op til 8 uger**.

**§ 9 stk. 2 (line 385):**
> Ved arbejde med pasning af dyr kan den normale arbejdstid lægges **på alle ugens dage, hele døgnet**.

So for animal care there is no placement window (hence no § 23 forskudt tid), and the weekly norm
is an 8-week average rather than a fixed weekly cap.

**§ 22 stk. 4 (lines 760-763)** anchors the *reckoning* weekly, not daily:
> Ved opgørelse af overarbejde fradrages forsømt tid af **den normale ugentlige arbejdstid**,
> medmindre forsømmelsen skyldes en medarbejderen utilregnelig grund eller en grund, som er
> rettidigt anmeldt til arbejdsgiveren og godkendt af denne.

**GLS-A's own circular for this exact worker category** ("Lønoversigt — udenlandske praktikanter –
landbrug") states:
> **Staldarbejde** Den normale arbejdstid er indtil 37 timer pr. uge eller **296 timer i en 8 ugers
> periode** og kan lægges på alle ugens dage, hele døgnet.

and GLS-A's guidance page on arbejdstidens lægning ved pasning af dyr describes overtime as hours
**above the turnus allotment** — 74 t for a 2-week rotation, 111 t for a 3-week — with normal hours
reduced by 7,4 t per søgnehelligdag. SIRI's trainee folder likewise ties the permit condition to the
**weekly** figure: *"din ugentlige arbejdstid skal være 37 timer."*

**Assessment.** § 22 stk. 1's daily trigger is agreement text and governs. § 8 stk. 2 governs the
*norm* (how many hours are normal over the period), not the *trigger* (when a given day's hours
become overtime); averaging a weekly norm does not license unpaid overrun of a daily one. That is
the reading this document adopts. But GLS-A's own guidance describes the trigger itself in turnus
terms, and GLS-A wrote the agreement — so their guidance carries real interpretive weight even
though it is not agreement text. This tension is unresolved and is the subject of section 8 below.

---

## 5. What the engine implements correctly

| Rule | Source | Engine | Status |
|---|---|---|---|
| Overtime triggered per day | § 22 stk. 1 | `CalculatePayLinesForDay`, invoked per row | correct |
| 2 h at 50 %, then 80 % | § 50 stk. 4 c | tiers `26640 / 33840 / null` | correct |
| Overtime on the praktikant's own rate | § 50 stk. 4 c | pay codes carry hours; rate applied downstream | correct |
| Daily boundary 37 ÷ 5 | derived from § 8 stk. 1 | `PayRuleSetLock.NormalTimeBoundarySeconds = 26640` | defensible derivation, not agreement text |
| Saturday / Sunday / holiday day codes | § 50 stk. 4 d | `SAT_NORMAL`, `ANIMAL_SUN_HOLIDAY`, `SAT_ANIMAL_AFTERNOON` | correct in routing (see defect 6.1 on units) |
| Saturday afternoon split at 12:00 | § 50 stk. 4 d / § 15 | `PayDayTypeRule` time bands | correct |
| Grundlovsdag from 12:00, 24 Dec all day | § 29 stk. 1 | `CalculateGrundlovsdagPayLines` | correct |
| Placement is not overtime | § 23 vs § 22; § 9 stk. 2 for dyr | no forskudt-tid pay code; none needed for staldarbejde | correct for this preset |

Two structural guards are worth recording as deliberate design, not accident:
`PayRuleSetLock.IsNormalTimeSplitPresetName` gates the § 50 split by preset *identity* (exactly two
normalized names), and `HasNormalTimeBoundaryShape` re-checks the *stored tier data* — because
presets are copy-at-create-time snapshots and a customer row may predate the
`20260807174115_CorrectPraktikantSection50Tiers` data migration.

---

## 6. What the engine does not implement — open items

**6.1 § 50 stk. 4 d supplements are per-day, modelled as hourly tiers.**
The agreement pays a flat kr. 73,90 / kr. 177,60 *pr. dag*. The engine encodes `SAT_NORMAL` and
`ANIMAL_SUN_HOLIDAY` as tier-1 pay codes accumulating **hours** up to 26640 s. If payroll multiplies
those hours by an hourly rate, the amount is wrong. Needs confirmation of how the receiving payroll system consumes the
export before it can be called a defect or a convention.

**6.2 § 22 stk. 4 weekly netting is not modelled.**
The engine has no cross-day context at all. Culpable absence within a week should be netted off the
normal weekly hours before overtime is reckoned, with two exempt categories (utilregnelig grund;
rettidigt anmeldt og godkendt). The data model *can* carry this — `PlanRegistration` has `Sick`,
`OnVacation`, `OtherAllowedAbsence`, `AbsenceWithoutPermission` — but all four are **zero across all
14.108 rows**, so the classification does not exist in practice. Implementing netting without that
classification would wrongly erode overtime for sick and approved-absence workers.

**6.3 Planned shift data is never read, and never written.**
`PlannedStartOfShift1..5` / `PlannedEndOfShift1..5` / `PlannedBreakOfShift1..5` are omitted from the
`Index()` projection (`TimePlanningWorkingHoursService.cs:128-192`), so they cannot reach
`CalculatePayLinesForDay`, whose DTO has no such fields. They are also non-zero in **0 of 14.108
rows** — only `ServiceTimePlanningPlugin` (gRPC / flutter-time) ever writes them, and they are
minutes-since-midnight, not the 5-minute tick grid used by `Shift{N}Start/Stop`. Any future
plan-anchored work must fix the projection first and convert units deliberately.

**6.4 `OvertimeBasis` is dead code.**
`enum OvertimeBasis { Weekly, Daily, DailyThenWeekly }` exists and nothing reads it. If the § 8 stk. 2
question in § 4 is ever resolved in favour of a period basis, this is where it would live.

**6.5 Pay lines are never persisted.**
`CalculatePayLinesForDay` has exactly two call sites, both Excel-export paths, and its results are
discarded. No production code writes `PlanRegistrationPayLines`; `PayrollExportService` reads a table
nothing populates. Any recalculation/audit feature starts by building that persistence.

**6.6 Gross-vs-worked truncation.**
Pre-existing, previously reported: the `Index()` projection also drops pause stamps, so segment
truncation measures gross rather than worked time. Workers are **underpaid** roughly 11 minutes per
affected shift. Same root cause as 6.3 — a projection silently omitting columns the calculation needs.

---

## 7. Readings considered and rejected

**Overtime triggered by working outside the planned clock window.** Rejected. § 22 stk. 1 is a
duration test; placement is § 23's separate forskydningstillæg, and GLS-A's guidance treats the two
as alternatives. For staldarbejde § 9 stk. 2 removes the window entirely, so there is nothing to fall
outside of.

**No plan ⇒ no overtime at all.** Rejected. § 22 stk. 1 is unconditional; it does not depend on a
plan existing, and the ordinary regime requires none.

**Any unplanned day is entirely overtime.** Assessed as **doubtful** and not adopted. The syllogism
(no planned hours ⇒ cessation at hour 0 ⇒ everything is "efter" it) is formally valid but cuts against
§ 22 stk. 3's ordered-and-notified framing, § 9 stk. 2's "any day, any hour" placement for animal care,
§ 22 stk. 4's weekly netting, and § 50 stk. 4 d's requirement that supplement days be worked
*i normal arbejdstid*. The single point in its favour is the ordinary regime's total absence of a
planning duty (section 2 above) — a gap, not an affirmative rule.

**Protokollat om alternativ arbejdstidsplanlægning as a route to plan-anchored boundaries.**
Not applicable to this customer. The protokollat (lines 3204-3327) replaces kapitel 3 wholesale,
confines placement to 06:00-18:00, caps days at 9,25 t with a 6 t floor, allows at most 5 working days
a week with two consecutive days off, and adds a fleksibilitetstillæg of kr. 4,10 per hour plus
weekend honorering. It is employer-elected with 14 days' notice and needs no union agreement, and
§ 50 stk. 7 does not exclude praktikanter from it. But it **never uses the word "overarbejde"** and
never states when overtime begins; and the recorded October 2025 data breaches it four ways at once
(7 working days in week 2025-W42, a 10,42 t day, no consecutive days off, second shifts 18:55-19:55
outside the window). Electing it would also remove § 9 stk. 2's around-the-clock allowance, which is
what makes this worker's actual pattern lawful.

---

## 8. The open question for GLS-A

One question remains genuinely unresolved by the text, and it is the only one that would change the
engine's shape rather than its parameters:

> 1. For en udenlandsk praktikant på månedsløn beskæftiget med staldarbejde (§ 50, jf. § 8 stk. 2):
>    hvad udgør "normal daglig arbejdstid" i § 22 stk. 1's forstand, når overenskomsten ikke angiver
>    et tal?
> 2. Hvis der foreligger en skriftlig arbejdsplan uden for protokollatet om alternativ
>    arbejdstidsplanlægning — fastsætter den planlagte arbejdstids ophør da overarbejdsgrænsen for
>    den enkelte dag, eller gælder 7,4 timer uanset?
> 3. Hvordan forholder § 22 stk. 1's daglige udløser sig til § 8 stk. 2's gennemsnit over op til
>    8 uger? Beregnes overarbejde dagligt, eller mod periodens normtimetal, jf. GLS-A's egen
>    vejledning om turnus (74 timer / 2 uger, 111 timer / 3 uger)?

Question 3 decides whether the engine models a daily **boundary** or a period **budget**. Until it is
answered, the daily reading in section 1 stands as this document's position, on the strength of § 22 stk. 1's
own wording.

---

## 9. Statutory backdrop (all regimes)

- **Arbejdstidsloven § 4** — average ≤ 48 t/uge including overtime, over a 4-month reference period.
  Does not define overarbejde; folds it into the ceiling.
- **Arbejdsmiljøloven § 50 stk. 1** — at least 11 consecutive hours' rest per 24 h. Stk. 2 permits
  reduction to 8 h for agriculture up to 30 days per calendar year.
- **Arbejdsmiljøloven § 51** — one rest day per 7-day period; agriculture exempt from the Sunday
  requirement; for care of animals the rest day may be postponed against equivalent later time off.
- **Direktiv 2003/88/EF art. 2** — defines "working time" and "rest period" only; **silent on
  overtime**, which is left to national law and collective agreements.
- **Ansættelsesbevisloven § 8** — attaches consequences to work outside referencetimer/-dage, but the
  remedy is a right to refuse, not overtime pay; §§ 6-11 are displaced where a qualifying CBA applies.

Statutory sourcing caveat: retsinformation.dk returned HTTP 403 to every fetch attempt; the Danish
statutory wording above came from danskelove.dk / elov.dk mirrors. The EU directive and the
collective agreements were fetched from primary sources directly.

---

## 10. Consequence for the payslip-reconciliation case

Recorded here because it is what prompted this research, and because the conclusion is negative.
Customer and worker identifiers are deliberately omitted; see the SDD ledger for the specific case.

The payslip overtime figures examined in that case — one udenlandsk praktikant on staldarbejde,
ten monthly periods 2025-10 through 2026-07 — were **not produced by applying any rule to recorded
work**:

- In every period whose totals reconcile, `OT1 = Arbejdstimer − Løn − OT2` exactly, to the øre —
  OT1 is an arithmetic remainder, not a rule output.
- 9 of 10 OT2 values are exact integers (20, 20, 25, 23, 22, 20, 25, 25, 18); 12 of the 20 OT figures
  land on exactly .00. Clock-derived hours do not behave that way.
- Per-period, per-week and per-turnus readings of the 50/80 split are each falsified across all
  periods; the per-day reading is structurally impossible for 2026-03 (OT1 7,74 permits at most 3 days
  to reach OT80, which would each need 6,67 t of it — a 16,07 t day).
- Recorded hours diverge from payslip totals by up to ±54 t per month in alternating directions,
  while the annual totals are internally consistent.
- 2026-02 and 2026-04 totals exceed `Løn + OT1 + OT2` by exactly their own stated retro lines
  (5,83 and 27,74, the latter being 2026-03's own OT1+OT2). Taking the totals at face value puts two
  4-month windows over Arbejdstidsloven § 4's 48 t/uge ceiling (48,38); removing the duplication
  brings them to 46,43. The law independently corroborates the double-counting reading.
- 2025-12 and 2026-06 fail even the bookkeeping identity, by −46,35 and −48,59 t. Both carry
  "Afholdt ferie −5,00 dage", and neither gap is 5 × 7,4.

A lawful roster reproducing all ten periods **can** be constructed (4 planned days/week at 9,25 t,
overtime from planned-day overrun plus a small number of unplanned days), but it depends on the
unresolved § 8 question and on the doubtful unplanned-day reading, and June 2026 requires 92 % of
available days worked. It would be a fabricated roster reproducing hand-entered figures.

**Conclusion: the remaining nine periods should not be fitted.** The engine is not reproducing these
numbers because these numbers were not generated by an engine.

---

## 11. Sources

Primary agreement texts, with SHA-256 and URLs, in
`/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/docs/superpowers/specs/sources/SOURCES.md`.

- Jordbrugsoverenskomsten 4010, 2026-2029 (2. udgave 06.07.2026) — `sources/jordbrug-2026-2029.txt`
- Jordbrugsoverenskomsten 4010, 2024-2026 (2. udgave 17.05.24) — fetched from gls-a.dk, structurally
  identical protokollat; praktikanter are § 48 in that edition, § 50 in 2026-2029
- FV 2015.0097 — https://arbejdsretten.dk/media/16489/kendelse-kf-mod-ka-fv-2015-0097.pdf
- GLS-A Lønoversigt, udenlandske praktikanter – landbrug (staldarbejde / andet arbejde)
- GLS-A, Arbejdstidens lægning ved pasning af dyr — https://www.gls-a.dk/nyheder/arbejdstidens-laegning-ved-pasning-af-dyr/
- SIRI, Information til praktikanter inden for det grønne område (aug. 2020), nyidanmark.dk
- Direktiv 2003/88/EF — https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX%3A32003L0088

GLS-A's members-only pages under `gls-a.dk/services/arbejdstid/jordbrugsoverenskomsten/` — including
its own "Alternativ arbejdstidsplanlægning" page — are paywalled and were not accessible. If member
credentials exist, that is the most likely place an explicit answer to § 8's question already lives.
