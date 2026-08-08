# GLS-A findings adversarial verification — design

**Date:** 2026-08-08
**Status:** Approved
**Goal:** Attack every GLS-A claim in `overenskomst-research-findings.md` against the primary agreement texts, prove or refute each with verbatim quotes, sweep the full texts for rules we missed entirely, and leave a permanent evidence trail.

## Why

The findings doc drives real payroll-attribution decisions (the praktikant migration shipped from it). Its GLS-A claims — 18 recorded defects, the implied target definitions of all 31 GLS-A presets, and several cross-cutting readings — were produced in one audit pass and have never been independently attacked. The extracted agreement texts that audit used were lost with the session, so today not a single quote in the doc is re-checkable without re-downloading. Before any of the 18 defect corrections is decided, the findings they rest on must survive adversarial verification, and the sources must be persisted.

## Scope — claims under attack

Everything GLS-A in `overenskomst-research-findings.md`:

1. The **18 recorded defects** (IMPLEMENTATION STATUS section) and the § 50 praktikant conclusions. The praktikant work is shipped; a refutation there triggers a follow-up product decision, never a silent change.
2. The **target definitions of all 31 GLS-A presets**: Jordbrug Standard/Dyrehold/Elev ×3, Gartneri ×3, Skovbrug ×3, Golf ×2, Agroindustri ×16, praktikant ×2.
3. **Cross-cutting decisions:** the 7,4 t (26640 s) overtime boundary; the bands-shadow-tiers routing reading; per-day vs per-hour supplements; Grundlovsdag / 24 Dec / 1 May treatment; 296 t / 8 uger averaging; the Elev u18/o18 split's textual basis.
4. The **"Ready to Implement" GLS-A sections** (Fiskeopdræt, GASA Sortering, GASA Transport, Holddrift, Metal, HK) — lighter pass: summary accuracy and staleness only.

Out of scope: KA/Krifa and non-GLS-A sectors; any code, catalogue, fixture, or migration change; deciding which corrections ship (each confirmed defect keeps needing its own praktikant-style decision + migration).

## Evidence rules

- A verdict requires a **verbatim clause quote from a persisted primary text** (agreement PDF or GLS-A's own rate sheet). Secondary sources (3F news, union summaries) may only locate primary text, never carry a verdict.
- Verdicts: **CONFIRMED** (quote carries the claim), **REFUTED** (quote contradicts it; corrected reading stated), **UNVERIFIABLE** (text does not settle it; the ambiguity is stated). Never silently confirm.
- Where the doc flags an open question because Gartneri/Skovbrug/Golf 2026-2029 texts were unpublished, the verifier checks whether they have since been published and re-verifies against the new edition if so.
- Code ground truth comes from the **source repos' current state** (plugin `stable`, base `master`): catalogue TS (`pay-rule-set-presets.ts`), C# fixtures (`OverenskomstFixtureHelper`), engine routing (`PayLineGenerator`, `CalculatePayLinesForDay`), `PayRuleSetLock` — never from the doc's own description of the code.

## Source persistence

- `docs/superpowers/specs/sources/<agreement>-<period>.txt` — pdftotext-extracted text of each primary PDF (`pdftotext -layout`; extraction sanity-checked: §-headings present, æ/ø/å intact).
- `docs/superpowers/specs/sources/SOURCES.md` — manifest: agreement, edition, URL, retrieval date, SHA-256 of the PDF, extraction tool + version.
- Committed **before** verification starts, so every ledger quote is greppable (`source file:line`) and permanently re-checkable. PDFs themselves are not committed.
- Member-only or unpublished texts are recorded as explicit gaps; dependent claims become UNVERIFIABLE.

## Pipeline — six sequenced workflows

Each workflow is a bounded fan-out (≤15 agents); the orchestrating session reads every phase's output before launching the next and commits artifacts as they land.

**W1 — Acquire (~6 agents).** One per text bundle: Jordbrug 2026-2029, Gartneri, Skovbrug, Golf, Agroindustri, rate sheets (Lønoversigt Landbrugsarbejde + overtime tables). Each: WebSearch for the current official PDF (explicitly checking for newly published 2026-2029 editions), download, extract, sanity-check, write `.txt` + manifest row.

**W2 — Code ground truth (~5 agents).** One per preset family. Output: per preset, the actually-encoded day rules / tiers / bands / supplement codes as tables, plus the engine facts that decide behaviour (band-shadows-tier routing, praktikant split opt-in, no per-day supplement unit, no Grundlovsdag DayType). Committed as `sources/CODE-TRUTH.md`.

**W3 — Claim ledger (1–2 agents + orchestrator).** Every GLS-A claim in the doc becomes a numbered row: ID, claim text, doc section, preset/day touched, harm direction, agreement §, status TBD. Orchestrator sanity-checks the inventory for missed claims before W4.

**W4 — Adversarial verify (2 batches ≤15 agents).** One agent per claim-cluster (a defect plus neighbouring claims about the same preset/day). Prompt: *refute this claim — find the governing clause in the persisted text, quote it verbatim, check the doc's reading and the code-truth tables; default to REFUTED or UNVERIFIABLE when the quote does not carry the claim.* The six highest-harm defects (Elev o18 first tier, u18 missing top tiers, Agro Kartoffelsortering/Gulerod/Minkfoder ceilings, fabricated Saturday supplement) get a second independent verifier with a contradiction-hunting lens (search the whole agreement for any clause that undermines the reading). Verifier disagreement escalates to the orchestrator.

**W5 — Completeness sweep (~6 agents).** One per agreement text: read the whole text section-by-section and list every pay-relevant rule (supplements, fridage, special days, averaging, night/shift rates); cross-check against CODE-TRUTH and the ledger; emit MISSING-RULE candidates with quotes.

**W6 — Encodings + engine gap + synthesis (~8 agents + orchestrator).** Per confirmed defect and accepted MISSING-RULE: proposed corrected tier/band tables with supporting quotes, marked **PROPOSED — not product-decided**; expressibility check against current engine capabilities, with a required-feature list where inexpressible (e.g. Grundlovsdag DayType, per-day supplement unit, DKK bands for Agro Øvrige). Then the orchestrator rewrites the findings doc, writes the ledger, and dispatches a review agent over the full diff (quotes match sources byte-for-byte, no claim dropped without a ledger row, doc and ledger agree).

## Outputs

1. **`docs/superpowers/specs/2026-08-08-glsa-verification-ledger.md`** — one row per claim: `ID | Claim | Verdict | Clause | Verbatim quote | Source file:line | Notes`. MISSING-RULE entries use the same format. This is the durable evidence trail.
2. **`overenskomst-research-findings.md` corrected in place** — refuted claims rewritten with the corrected reading and marked `[corrected 2026-08-08, see ledger #N]`; confirmed claims untouched; new missing rules added to the defect lists; IMPLEMENTATION STATUS tables updated. Nothing deleted without a ledger row explaining why.
3. **`docs/superpowers/specs/sources/`** — persisted texts + manifest + CODE-TRUTH.
4. **PROPOSED encodings section** — target tables per confirmed defect, plus the engine-gap list.

## Error handling

- Unfetchable / member-only / unpublished text → explicit gap list in SOURCES.md; dependent claims UNVERIFIABLE.
- Garbled extraction of a section → that section's claims UNVERIFIABLE, not guessed; noted in the manifest.
- Verifier disagreement unresolved by the orchestrator → recorded as an open question for GLS-A with both readings quoted.
- A refutation touching the shipped praktikant behaviour → flagged prominently at the top of the ledger for an explicit product decision; no code change from this audit.

## Done when

- Every ledger row has a verdict + quote or an explicit gap.
- Doc and ledger are consistent; sources + manifest + CODE-TRUTH committed.
- Review agent passes the final diff.
- All artifacts land on a docs branch → PR to `stable` (docs-only) for user review and merge.

## Verification of this audit itself

The review agent's diff check plus two spot rules for the orchestrator: re-grep five random ledger quotes against the source files by hand, and confirm the 18 original defects each map to exactly one ledger row (none silently dropped or merged).
