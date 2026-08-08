# GLS-A Findings Adversarial Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Attack every GLS-A claim in `overenskomst-research-findings.md` against re-acquired, persisted primary agreement texts; produce a per-claim verdict ledger with verbatim quotes, a corrected findings doc, proposed encodings for confirmed defects, and an engine-gap list — as a docs-only PR to `stable`.

**Architecture:** Six sequenced phases (W1–W6 from the spec), each a bounded fan-out of subagents dispatched by the orchestrating session, with the orchestrator reading every phase's output before launching the next and committing artifacts per task. No code, catalogue, fixture or migration changes.

**Tech Stack:** Claude subagents (parallel Agent dispatches or Workflow tool — either mechanism is acceptable; prompts below are the contract), `curl` + `pdftotext -layout` for acquisition, git on branch `docs/glsa-findings-verification` in `eform-angular-timeplanning-plugin`.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-08-glsa-findings-verification-design.md`. Every task inherits its rules.
- All work happens in `/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin` on branch `docs/glsa-findings-verification` (already exists, contains the spec). Base repo and other repos are read-only inputs.
- A verdict requires a verbatim quote from a persisted file under `docs/superpowers/specs/sources/`, cited as `file:line`. Verdict vocabulary is exactly: `CONFIRMED`, `REFUTED`, `UNVERIFIABLE`. Secondary sources never carry a verdict.
- Never modify: any `.ts`/`.cs` file, anything under `eFormAPI/` or `eform-client/`, any repo other than this one. Docs only.
- Ledger claim IDs: `G-001`… in doc order; missing-rule IDs: `M-001`…. One claim = one row; the 18 IMPLEMENTATION-STATUS defects map 1:1 to ledger rows (they may share a row with duplicate statements of the same claim elsewhere in the doc — the row lists all doc locations).
- Commit after every task, staging files by name (never `git add .`). Commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Subagent outputs are written by the subagents to the exact paths given; the orchestrator verifies with the commands given before committing.
- If a required text is member-only/unpublished/unfetchable: record the gap in `SOURCES.md` under `## Gaps`; dependent claims become `UNVERIFIABLE` — never guessed.

---

### Task 1: W1 — Acquire and persist the primary texts

**Files:**
- Create: `docs/superpowers/specs/sources/SOURCES.md`
- Create: `docs/superpowers/specs/sources/jordbrug-2026-2029.txt`
- Create: `docs/superpowers/specs/sources/gartneri-<period>.txt` (2026-2029 if published, else 2024-2026)
- Create: `docs/superpowers/specs/sources/skovbrug-<period>.txt` (same rule)
- Create: `docs/superpowers/specs/sources/golf-<period>.txt` (same rule)
- Create: `docs/superpowers/specs/sources/agroindustri-2026-2029.txt`
- Create: `docs/superpowers/specs/sources/loenoversigt-landbrug-2026.txt` (plus any GLS-A overtime/rate tables found as separate documents, named `ratesheet-<topic>-<year>.txt`)

**Interfaces:**
- Produces: the persisted text corpus + manifest that every later task quotes from. Later tasks reference lines as `sources/<file>.txt:<line>`.

- [ ] **Step 1: Dispatch 6 parallel acquisition agents**

One agent per bundle: (1) Jordbrug 2026-2029, (2) Gartneri, (3) Skovbrug, (4) Golf, (5) Agroindustri 2026-2029, (6) GLS-A rate sheets (Lønoversigt Landbrugsarbejde marts 2026 + any overtime tables). Prompt template (fill `<BUNDLE>`):

```
You are acquiring the primary text for <BUNDLE> for a payroll-rules audit.
1. Find the CURRENT official agreement PDF. Start at https://www.gls-a.dk/overenskomster/
   (also try 3f.dk's overenskomst pages). For Gartneri/Skovbrug/Golf explicitly check
   whether a 2026-2029 edition has been published since 2026-08-07; prefer it if so,
   otherwise take 2024-2026 and note that no newer edition exists.
2. Download with: curl -L -o /tmp/claude-1000/<bundle>.pdf '<URL>'
3. Extract: pdftotext -layout /tmp/claude-1000/<bundle>.pdf <exact target .txt path>
4. Sanity-check the extraction and report the results of:
   grep -c '§' <file>   (must be > 20 for agreements)
   grep -m1 'æ' <file> && grep -m1 'ø' <file>   (Danish characters intact)
   plus confirm the agreement's period appears in the first 100 lines.
5. Compute sha256sum of the PDF.
6. Return (as your final message, machine-readable): bundle, edition found, exact URL,
   retrieval date 2026-08-08, sha256, extraction line count, sanity results, and any
   gap (member-only / not found / no newer edition).
Do NOT summarize the agreement content. If the PDF cannot be found or downloaded,
return the gap instead — do not substitute a secondary source.
```

- [ ] **Step 2: Verify extractions**

Run for each produced file:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin
for f in docs/superpowers/specs/sources/*.txt; do echo "== $f"; wc -l "$f"; grep -c '§' "$f"; done
```
Expected: every agreement file exists, >1000 lines, `§` count >20 (rate sheets may have fewer §; they must instead contain "kr." — check `grep -c 'kr\.' <file>` > 5). If a file fails, re-dispatch that one agent with the failure described.

- [ ] **Step 3: Write SOURCES.md from the agents' reports**

Format (one row per document, plus a Gaps section):
```markdown
# GLS-A primary-source manifest (retrieved 2026-08-08)
| File | Agreement / document | Edition | URL | SHA-256 (PDF) | Extracted with |
|---|---|---|---|---|---|
| jordbrug-2026-2029.txt | Jordbrugsoverenskomsten 4010 | 2026-2029 (2. udg.) | https://… | … | pdftotext 24.02 -layout |
…
## Gaps
- <document>: <why unavailable> — dependent claims are UNVERIFIABLE.
```
Get the pdftotext version with `pdftotext -v 2>&1 | head -1`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/sources/SOURCES.md docs/superpowers/specs/sources/*.txt
git commit -m "audit(glsa): persist primary agreement texts with provenance

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: W2 — Code ground truth

**Files:**
- Create: `docs/superpowers/specs/sources/CODE-TRUTH.md`

**Interfaces:**
- Consumes: nothing from Task 1 (independent — may run in parallel with it).
- Produces: per-preset encoded-state tables + engine-facts section that W4/W5/W6 verify claims against.

- [ ] **Step 1: Dispatch 5 parallel code-truth agents**

One per family: (1) Jordbrug Standard + Dyrehold, (2) Jordbrug Elev ×3, (3) Gartneri ×3 + Skovbrug ×3, (4) Golf ×2 + praktikant ×2, (5) Agroindustri ×16. Prompt template (fill `<FAMILY>`, `<PRESET KEYS>`):

```
Extract the ACTUAL encoded state of the <FAMILY> pay-rule presets. Read ONLY code,
never docs. Sources of truth:
- Catalogue: eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn/models/pay-rule-sets/pay-rule-set-presets.ts (branch stable) — presets <PRESET KEYS>
- C# fixtures: eform-timeplanning-base/Microting.TimePlanningBase.Tests/Helpers/OverenskomstFixtureHelper.cs (branch master) — the same presets
For EACH preset output a markdown table: DayCode | tier order/upToSeconds/payCode list |
dayTypeRules bands (dayType, start-end seconds, payCode). Note any divergence between
catalogue and C# fixture (byte-level: names, values, missing days).
Return the tables as your final message — raw markdown, no commentary.
```

Plus a 6th agent for engine facts:

```
Document the engine facts that determine which encoded rules actually execute. Read:
- eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Services/TimePlanningWorkingHoursService/ (CalculatePayLinesForDay and helpers, branch stable)
- eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/PayRuleSetLock.cs
- eform-timeplanning-base/Microting.TimePlanningBase/Infrastructure/Data/PayLineGenerator*.cs (or wherever GeneratePayLines/GenerateTimeBandPayLines live in the base repo, branch master)
Answer precisely, citing file:line: (a) when a day has both bands and tiers, which wins,
and for which presets the normal-time/overtime split applies; (b) which DayTypes exist
(is there one for Grundlovsdag? 24 Dec? 1 May?); (c) is any supplement expressible
per-day rather than per-hour; (d) what happens when no rule matches; (e) any cap/floor
logic (e.g. 8h Elev cap) and where it lives. Raw markdown, no commentary.
```

- [ ] **Step 2: Assemble CODE-TRUTH.md**

Concatenate the six outputs under headers: `# Code ground truth (extracted 2026-08-08, plugin stable @ <sha>, base master @ <sha>)`, `## <family>` ×5, `## Engine facts`. Fill the two `<sha>` values from `git -C <repo> rev-parse --short stable|master`.

- [ ] **Step 3: Spot-verify two tables**

Pick the praktikant Staldarbejde SATURDAY row and the Jordbrug Elev o18 WEEKDAY row from CODE-TRUTH.md and compare by hand against `pay-rule-set-presets.ts` (grep the preset key, read the day block). Expected: exact match. Mismatch → re-dispatch that family's agent with the discrepancy quoted.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/sources/CODE-TRUTH.md
git commit -m "audit(glsa): record code ground truth for all 31 GLS-A presets + engine facts

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: W3 — Claim ledger (inventory, verdicts TBD)

**Files:**
- Create: `docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md`

**Interfaces:**
- Consumes: `overenskomst-research-findings.md` (the claims), CODE-TRUTH.md (to tag each claim with the preset/day it touches).
- Produces: numbered rows `G-001`… that W4 fills in. Row format is fixed here and MUST NOT be changed by later tasks:
  `| ID | Claim (verbatim-condensed) | Doc section(s) | Preset/day | Agreement § | Verdict | Quote (source file:line) | Notes |`

- [ ] **Step 1: Dispatch 1 inventory agent**

```
Enumerate EVERY GLS-A claim in eform-angular-timeplanning-plugin/docs/superpowers/specs/overenskomst-research-findings.md
(branch docs/glsa-findings-verification). A claim = any assertion about what an agreement
says, what a preset encodes relative to the agreement, or a decided interpretation.
In scope: the 18 numbered defects in IMPLEMENTATION STATUS; the § 50 praktikant section;
the "Normal daglig arbejdstid" decision section (incl. 296 t/8 uger and weekly netting);
the OK26 verification notes; the "Overarbejde × forskudt tid" GLS-A parts; the encoding
audit table rows; the Ready-to-Implement GLS-A subsections (Agroindustri, Golf,
Fiskeopdræt, GASA ×2, Holddrift, Metal, HK — summary-level claims only).
Out of scope: KA/Krifa, NNF, and later non-GLS-A sectors; pure code-state statements
(covered by CODE-TRUTH.md); process notes.
Output: the full ledger table in exactly this row format, Verdict column "TBD",
Quote column empty:
| ID | Claim (verbatim-condensed) | Doc section(s) | Preset/day | Agreement § | Verdict | Quote (source file:line) | Notes |
Number G-001… in document order. Where the same claim appears in two sections, ONE row
listing both sections. Raw markdown only.
```

- [ ] **Step 2: Orchestrator sanity check**

- All 18 defects present: for each of the 18 numbered defects in the findings doc's IMPLEMENTATION STATUS, find its ledger row; write the mapping (defect # → G-###) at the bottom of the ledger under `## Defect map`.
- Expected row count: roughly 55–80 rows. Under 45 means claims were missed (re-read the § 50 encoding-audit table and the OK26 section; every row/observation there is a claim). Over 100 means code-state rows leaked in — remove them.
- Add a header above the table: date, spec reference, verdict vocabulary, and the sentence "Rows are append-only; verdicts fill in during W4/W5."

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md
git commit -m "audit(glsa): inventory all GLS-A claims as verification ledger (verdicts pending)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: W4 batch 1 — Adversarial verify: Jordbrug, praktikant, cross-cutting

**Files:**
- Modify: `docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md` (fill verdicts for batch-1 rows)

**Interfaces:**
- Consumes: ledger rows (Task 3), sources corpus (Task 1), CODE-TRUTH.md (Task 2).
- Produces: verdict-complete rows for clusters C1–C8.

- [ ] **Step 1: Dispatch 11 parallel verifier agents (8 clusters + 3 contradiction-hunters)**

Clusters (assign each agent its cluster's ledger rows, copied verbatim into the prompt):
- **C1** Jordbrug Elev o18 first OT tier 30 % vs § 47 stk. 4 (defect 1) + related Elev tier claims. *High-harm → also gets contradiction-hunter C1x.*
- **C2** all u18 presets missing top OT tier (defect 2) + "u18/o18 split fabricated" + "8 h/day cap has no basis". *High-harm → C2x.*
- **C3** Jordbrug Standard fabricated Saturday supplement (defect 7) + Jordbrug Standard weekday-band claims. *High-harm → C3x.*
- **C4** Dyrehold: SHIFTED_EVENING beyond § 23's 2 h cap (defect 8), SHIFTED_MORNING 05–06 not in § 15 (defect 9), Saturday 00–05 night rate missed (defect 15).
- **C5** Praktikant § 50 section: 50 %/80 % tiers, stk. 4 d supplements limited to normal time, Sunday-differs-from-ordinary-workers, stald/andet arbejdstid fork, naming drift.
- **C6** Cross-cutting: 7,4 t boundary decision (incl. "no clause lowers it"), 296 t/8 uger is an average, weekly netting § 22 stk. 4.
- **C7** Grundlovsdag § 29 half-day + 24 Dec + unmodelled fridage claims (defects 16, 18 as they touch Jordbrug).
- **C8** Per-day supplements encoded per-hour (defect 17): § 15, § 47 stk. 5, § 50 stk. 4 d all "pr. dag".

Verifier prompt template (fill `<ROWS>`, `<SOURCE FILES>`):

```
You are an adversarial verifier. Your job is to REFUTE these claims if possible:
<ROWS — the full ledger rows for this cluster>
Evidence rules (absolute):
- Only the persisted texts under eform-angular-timeplanning-plugin/docs/superpowers/specs/sources/
  may carry a verdict. Relevant files: <SOURCE FILES>. Cite as file:line and quote the
  clause VERBATIM (Danish, exact bytes).
- Cross-check the claim's description of what the presets encode against
  docs/superpowers/specs/sources/CODE-TRUTH.md — a claim can be refuted on the code side too.
- Default skeptical: if the quote does not straightforwardly carry the claim, the verdict
  is REFUTED (with the correct reading) or UNVERIFIABLE (with the ambiguity stated) — not CONFIRMED.
- Search the WHOLE relevant agreement for the governing clause; do not stop at the § the
  doc names — the doc may cite the wrong §.
Return: for each row, `ID | verdict | quote | file:line | corrected reading if REFUTED |
notes (including if the doc cites the wrong §)`. Raw markdown only.
```

Contradiction-hunter prompt (C1x/C2x/C3x — fill `<CLAIM>`, `<FIRST READING>`):

```
Independent second opinion. The claim under test: <CLAIM — the ledger row>.
Do NOT verify it directly. Instead: search the ENTIRE agreement text (<SOURCE FILE>) for
ANY clause, protokollat, note, or table that could contradict, qualify, or scope-limit
this reading — exceptions, opt-in regimes, transitional rules, age/seniority conditions,
local-agreement carve-outs. Quote verbatim with file:line anything you find, and state
whether it undermines the claim. If nothing undermines it after a full pass, say so
explicitly, listing the §§ you checked. Raw markdown only.
```

- [ ] **Step 2: Merge verdicts into the ledger**

Fill the batch-1 rows. Where a contradiction-hunter disagrees with its cluster verifier: orchestrator reads both quotes against the source file and decides; unresolved → verdict `UNVERIFIABLE`, both readings quoted in Notes, and the row is tagged `[open question for GLS-A]`.

- [ ] **Step 3: Spot-check 3 quotes**

Pick 3 filled rows at random; `grep -n "<first 6 words of quote>" docs/superpowers/specs/sources/<file>` must land on the cited line ±2. A miss → return the row to its verifier.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md
git commit -m "audit(glsa): verdicts for Jordbrug, praktikant and cross-cutting claims (batch 1)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: W4 batch 2 — Adversarial verify: Gartneri, Skovbrug, Golf, Agroindustri, Ready-to-Implement

**Files:**
- Modify: `docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md` (fill remaining verdicts)

**Interfaces:**
- Consumes/Produces: same contract as Task 4, clusters C9–C14.

- [ ] **Step 1: Dispatch 7 parallel verifier agents (6 clusters + 1 contradiction-hunter)**

- **C9** Gartneri: Sunday/holiday should tier 50 %/100 % (defect 12), Saturday code, 1 May missing, 31 Dec, Elev tier claims.
- **C10** Skovbrug: Saturday = overtime ladder from hour 1 (defect 6), evening band 1 h too long (defect 10), Elev Sunday 50 % step invented (defect 11).
- **C11** Golf: Saturday-afternoon code, Elev boundary, fridage missing.
- **C12** Agroindustri overtime ceilings: Kartoffelsortering 30→100 not OVERTIME_80 (defect 3), Gulerod missing 100 % tier + 80 % stop after hour 3 (defect 4), Minkfoder clock-keyed 100 % tier + Sunday 12:00 split (defect 5). *High-harm → C12x contradiction-hunter over the same three.*
- **C13** Agroindustri structural: Øvrige is flat DKK bands (defect 13), no forskudt bands vs § 19 (defect 14), fabricated Saturday split, "Agro Elev variants have no textual basis".
- **C14** Ready-to-Implement GLS-A summaries (Fiskeopdræt, GASA Sortering, GASA Transport, Holddrift, Metal, HK): are the summary claims accurate and current? If a summary's source text is not in the corpus, verdict UNVERIFIABLE with the missing document named — do NOT fetch new sources in this phase.

Use the same verifier and contradiction-hunter prompt templates as Task 4, with each cluster's ledger rows and source files filled in. Note for C9–C11: if Task 1 found 2026-2029 editions, verify against those and record in Notes when a 2024-2026-based doc claim changed in the new edition.

- [ ] **Step 2: Merge, resolve disagreements, spot-check 2 quotes** (same procedure as Task 4 steps 2–3)

- [ ] **Step 3: Verify ledger completeness**

```bash
grep -c '| TBD |' docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md
```
Expected: `0`. Any remaining TBD row gets dispatched to a mop-up verifier before proceeding.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md
git commit -m "audit(glsa): verdicts for Gartneri, Skovbrug, Golf, Agroindustri, RTI claims (batch 2)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: W5 — Completeness sweep (rules we missed entirely)

**Files:**
- Modify: `docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md` (append `## Missing rules (M-###)` section)

**Interfaces:**
- Consumes: sources corpus, CODE-TRUTH.md, verdict-complete ledger.
- Produces: `M-001`… rows in the same 8-column format (Claim column holds the rule found; Verdict column holds `MISSING` — the one extra verdict value allowed only for M-rows).

- [ ] **Step 1: Dispatch 6 parallel sweep agents** (one per agreement file: jordbrug, gartneri, skovbrug, golf, agroindustri, rate sheets)

```
Read docs/superpowers/specs/sources/<FILE> from start to finish, section by section.
List EVERY rule that affects which pay code a worked minute (or day) falls under:
overtime triggers/ladders, supplements (tillæg) of any kind, forskudt tid, night/shift
rates, weekend/holiday rules, fridage and special days (Grundlovsdag, 24/12, 31/12,
1/5), averaging/reference periods, age/apprentice/trainee variants, opt-in regimes that
change attribution. Rates in kr. matter only to identify the rule, not the amount.
For each rule: quote the clause verbatim with file:line, then check
(a) docs/superpowers/specs/sources/CODE-TRUTH.md — is it encoded in any preset of this family?
(b) docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md — is it already claimed/known?
Output ONLY the rules absent from both, as rows:
| M-TBD | <rule found> | sweep:<FILE> | <family/preset it should affect> | <§> | MISSING | <quote> (<file:line>) | <note> |
If everything is covered, return "NO GAPS after full pass" plus the list of §§ read.
Raw markdown only.
```

- [ ] **Step 2: Merge and de-duplicate**

Orchestrator assigns final `M-001`… numbers, drops duplicates across agents (same clause found from two families keeps one row listing both), and rejects rows that merely restate an existing G-row (note the rejection in the row's place is NOT kept — silently dropping is fine here since G-row already covers it).

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md
git commit -m "audit(glsa): completeness sweep — missing-rule findings appended to ledger

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7: W6a — Proposed encodings + engine gap analysis

**Files:**
- Create: `docs/superpowers/specs/2026-08-08-glsa-proposed-encodings.md`

**Interfaces:**
- Consumes: CONFIRMED defect rows + MISSING rows from the ledger, CODE-TRUTH.md engine facts.
- Produces: per-defect proposed tier/band tables (format identical to CODE-TRUTH tables) + `## Engine gaps` list that the findings-doc rewrite (Task 8) references.

- [ ] **Step 1: Group CONFIRMED defects + accepted M-rows into encoding work packets**

Expected packets (adjust to actual verdicts): (1) Jordbrug Elev family, (2) Jordbrug Standard + Dyrehold, (3) Gartneri family, (4) Skovbrug family, (5) Golf family, (6) Agro ceilings trio, (7) Agro structural (Øvrige DKK, forskudt bands), (8) fridage/Grundlovsdag cross-family. One agent per packet, max 8.

- [ ] **Step 2: Dispatch encoding agents**

```
For these CONFIRMED/MISSING findings: <ROWS with quotes>
Write the corrected target encoding per affected preset/day as tables in exactly the
CODE-TRUTH.md format (DayCode | tiers | bands), plus per-day-unit or DKK-band notation
where hourly tiers cannot express the rule. Each table row cites the ledger ID(s) and
quote that justify it. Then check expressibility against the Engine facts section of
CODE-TRUTH.md: mark each table EXPRESSIBLE or NOT-EXPRESSIBLE, and for NOT-EXPRESSIBLE
name the exact missing capability (e.g. "DayType for Grundlovsdag", "per-day supplement
unit", "flat-DKK band model", "clock-keyed overtime tier").
Header every packet with: PROPOSED — not product-decided. No code changes.
Raw markdown only.
```

- [ ] **Step 3: Assemble the file**

Header: title, date, `**Status: PROPOSED — every table needs its own product decision + praktikant-style data-migration story before shipping**`, link to ledger and spec. Body: the packets. Footer: `## Engine gaps` — the deduplicated union of NOT-EXPRESSIBLE capabilities with the packet(s) needing each.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-08-08-glsa-proposed-encodings.md
git commit -m "audit(glsa): proposed corrected encodings + engine gap list (PROPOSED, not decided)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 8: W6b — Rewrite the findings doc from the verdicts

**Files:**
- Modify: `docs/superpowers/specs/overenskomst-research-findings.md`

**Interfaces:**
- Consumes: the completed ledger, proposed-encodings doc.
- Produces: the corrected findings doc; every changed passage tagged `[corrected 2026-08-08, see ledger G-###]` or `[added 2026-08-08, see ledger M-###]`.

- [ ] **Step 1: Orchestrator rewrites (no subagent — this is judgment work)**

Rules, applied section by section over the GLS-A parts:
- CONFIRMED claim → text untouched.
- REFUTED claim → passage rewritten to the corrected reading + tag. The old wording is not preserved in the doc (the ledger holds it).
- UNVERIFIABLE → passage softened to state the ambiguity + tag; if tagged `[open question for GLS-A]`, add it to the doc's open-questions list.
- M-rows → added to the appropriate defect list / family table + tag.
- IMPLEMENTATION STATUS per-family table and the defect counts updated to post-audit numbers; add one audit-record line: `Adversarially verified 2026-08-08 against persisted sources — see 2026-08-08-glsa-verification-ledger.md.`
- Sources section: point to `sources/SOURCES.md` as the canonical manifest.
- Nothing deleted without its ledger row stating why.

- [ ] **Step 2: Consistency pass**

```bash
grep -n 'corrected 2026-08-08\|added 2026-08-08' docs/superpowers/specs/overenskomst-research-findings.md | wc -l
```
Expected: equals (REFUTED + UNVERIFIABLE-softened + M-added) count from the ledger. Reconcile any difference before committing.

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/overenskomst-research-findings.md
git commit -m "audit(glsa): correct findings doc from verification verdicts

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 9: Final review gate — audit the audit

**Files:**
- Modify (fixes only): ledger, findings doc, proposed encodings, SOURCES.md

**Interfaces:**
- Consumes: the full branch diff `stable..docs/glsa-findings-verification`.
- Produces: a review-passed branch ready for PR.

- [ ] **Step 1: Dispatch 1 review agent over the full diff**

```
Review the branch diff (git diff stable..docs/glsa-findings-verification in
eform-angular-timeplanning-plugin) as an auditor of an audit. Check, with file:line
citations for every failure:
1. Every ledger verdict's quote appears byte-identical at its cited source file:line.
   Verify ALL of them, not a sample (grep each).
2. The 18 original defects each map to exactly one ledger row (Defect map section) and
   none was dropped or merged away.
3. Doc↔ledger consistency: every [corrected/added 2026-08-08] tag resolves to a ledger
   row whose verdict justifies the edit; every REFUTED row has a corresponding doc edit.
4. No file outside docs/superpowers/ was touched.
5. Proposed-encodings tables cite only CONFIRMED/MISSING ledger IDs, and every
   NOT-EXPRESSIBLE mark names a concrete missing capability listed in Engine gaps.
6. SOURCES.md rows are complete (no empty URL/SHA cells) and every source file cited
   anywhere exists in sources/.
Output: PASS, or the numbered list of failures. Raw markdown only.
```

- [ ] **Step 2: Orchestrator spot rules (from the spec)**

- Re-grep 5 random ledger quotes by hand (same command as Task 4 step 3). Expected: all land.
- Re-check the Defect map covers 1–18 with no duplicates: `grep -o 'defect [0-9]*' … ` visually confirm 18 lines.

- [ ] **Step 3: Fix all failures, re-dispatch the review agent, repeat until PASS. Commit fixes**

```bash
git add <specific fixed files>
git commit -m "audit(glsa): fixes from final audit review

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 10: PR to stable and user review

**Files:** none new (branch as-is)

- [ ] **Step 1: Push and open the PR**

```bash
git push -u origin docs/glsa-findings-verification
gh pr create --base stable --title "audit: adversarial verification of GLS-A overenskomst findings" --body "<summary: sources persisted with provenance; N claims verified (X confirmed / Y refuted / Z unverifiable); M missing rules found; findings doc corrected in place; proposed encodings + engine gaps documented — PROPOSED, no product decisions taken. Ends with the Claude Code attribution line.>"
```

- [ ] **Step 2: Watch CI (docs-only — expect trivially green), report the verdict summary table to the user, and stop for their review of the PR.** Do not merge without the user's go-ahead: unlike code PRs, this one rewrites the team's reference document.

---

## Self-Review (completed at write time)

- **Spec coverage:** W1→Task 1, W2→Task 2, W3→Task 3, W4→Tasks 4–5 (batches, double-verify on the six high-harm defects via C1x/C2x/C3x/C12x), W5→Task 6, W6→Tasks 7–8, review-the-audit→Task 9, PR/done→Task 10. Error-handling rules embedded in Global Constraints + Task 1 gaps + Task 4 disagreement procedure. Praktikant-refutation flagging: covered by C5 rows + Task 8 rule (UNVERIFIABLE/REFUTED tagging) — and Task 10's summary must surface any praktikant refutation prominently.
- **Placeholder scan:** no TBDs except the ledger's deliberate initial verdict value, which is defined semantics, not a plan gap.
- **Type consistency:** ledger row format defined once (Task 3) and referenced by Tasks 4–6, 9; encoding-table format defined by CODE-TRUTH (Task 2) and reused in Task 7.
