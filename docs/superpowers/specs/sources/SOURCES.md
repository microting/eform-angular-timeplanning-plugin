# GLS-A primary-source manifest (retrieved 2026-08-08)

| File | Agreement / document | Edition | URL | SHA-256 (PDF) | Extracted with |
|---|---|---|---|---|---|
| jordbrug-2026-2029.txt | Jordbrugsoverenskomsten (4010, GLS-A / 3F Den Grønne Gruppe) | 2026-2029 (2. udgave, 06.07.2026; effective 1. marts 2026) | https://www.3f.dk/-/media/files/artikler/overenskomst/den-groenne-gruppe/overenskomster/4010---jordbrug-2026-2029---2,-d-,-udgave---06,-d-,07,-d-,26.pdf | 1c8d9bef0d5c5fc7c664713d6c81e2f54baf3f71aa1118580e9ab0da56f815c0 | pdftotext 25.03.0 -layout |
| gartneri-2024-2026.txt | Gartneri- og Planteskoleoverenskomsten (4011, GLS-A / 3F Den Grønne Gruppe) | 2024-2026 (endelig 15.05.24) — no 2026-2029 edition published yet | https://www.gls-a.dk/wp-content/uploads/2024/05/4011-GARTNERI-OG-PLANTESKOLE-2024-2026-endelig-15.05.24.pdf | a79591a6a47b95b8cbd717c0e656fce5627a9f0eaf0cbacf8421704f0f607335 | pdftotext 25.03.0 -layout |
| skovbrug-2024-2026.txt | Skovbrugsoverenskomsten (4013, GLS-A / 3F Den Grønne Gruppe) | 2024-2026 (endelig 30.05.24) — no 2026-2029 edition published yet | https://www.gls-a.dk/wp-content/uploads/2024/05/4013-SKOVBRUG-2024-2026-endelig-30.05.24.pdf | 3ec22a0febbe5db7f6af1f0f82f7d09e6d624a4220b2125ea8f1246b89f03953 | pdftotext 25.03.0 -layout |
| golf-2024-2026.txt | Golfoverenskomsten (4014, GLS-A / 3F Den Grønne Gruppe) | 2024-2026 (endelig 30.05.24) — no 2026-2029 edition published yet | https://www.gls-a.dk/wp-content/uploads/2024/05/4014-GOLF-2024-2026-endelig-30.05.24.pdf | a7d19e3569a305dabff1e15dbafa3e3937e2397e23b6603b2bfb94c29d13a675 | pdftotext 25.03.0 -layout |
| agroindustri-2026-2029.txt | Agroindustrioverenskomsten (4012, GLS-A / 3F Den Grønne Gruppe) | 2026-2029 (endelig 07.07.26; effective 1. marts 2026) | https://www.3f.dk/-/media/files/artikler/overenskomst/den-groenne-gruppe/overenskomster/4012---agroindustri--2026-2029---endelig-07,-d-,07,-d-,26---web.pdf | bc1b30f5a23366f4be8bbb4d2686935f2508ab588256b412f94e741508e0c310 | pdftotext 25.03.0 -layout |
| loenoversigt-landbrug-2026.txt | Lønoversigt Marts 2026 — Lønninger m.v. for landbrugsarbejde (GLS-A wage overview) | Marts 2026 (perioden 1. marts 2026 – 28. februar 2027) | https://www.gls-a.dk/wp-content/uploads/2026/03/Landbrugsarbejde.pdf | e06e62152155a36f79a2dbe38b8547156807e8009482c5e8ff615c768ab9c4e7 | pdftotext 25.03.0 -layout |
| ratesheet-holddriftstillaeg-2026.txt | Lønoversigt Marts 2026 — Holddriftstillæg (GLS-A shift-work / overtime supplement rate sheet) | Marts 2026 (perioden 1. marts 2026 – 28. februar 2027) | https://www.gls-a.dk/wp-content/uploads/2026/04/Holddriftstillaeg.pdf | b952fa19ad4e1b3a211da6e4f4bd0587676c5321ca258ffec17668c1c03c2c1b | pdftotext 25.03.0 -layout |

All PDFs retrieved 2026-08-08. SHA-256 recomputed independently against each downloaded PDF via `sha256sum` (matches each acquisition agent's self-reported hash).

## Verification results (Step 2)

| File | Lines | `§` count | Danish chars (æ/ø) | Period in first 100 lines |
|---|---|---|---|---|
| jordbrug-2026-2029.txt | 3940 | 209 | present | yes (line 2/5/18) |
| gartneri-2024-2026.txt | 3468 | 188 | present | yes (line 2/8/22) |
| skovbrug-2024-2026.txt | 3972 | 201 | present | yes (line 2/8/21) |
| golf-2024-2026.txt | 2773 | 131 | present | yes (line 2/8/21) |
| agroindustri-2026-2029.txt | 3765 | 189 | present | yes (line 2/5/18) |
| loenoversigt-landbrug-2026.txt | 95 | 0 (rate sheet; `grep -ci 'kr\.'` = 9) | present | yes (line 12/13) |
| ratesheet-holddriftstillaeg-2026.txt | 32 | 0 (rate sheet; `grep -ci 'kr\.'` = 3, see note below) | present | yes (line 13/14) |

Note on `ratesheet-holddriftstillaeg-2026.txt`: the brief's rate-sheet sanity heuristic (`grep -c 'kr\.' > 5`) is
tuned for the larger main wage sheet. This document's case-sensitive `kr.` count is 0 (source uses capitalized
"Kr." throughout) and its case-insensitive count is 3, below the >5 threshold. The acquisition agent manually
confirmed the 32-line extraction is the complete, non-truncated document (covers holddriftstillæg rate,
overarbejde-on-holddrift rule, overflytning rate, and fridag/vagtliste-forskydning rates) — not a retrieval
failure, just a short source document. Flagged here for transparency; not listed under Gaps because the primary
text was successfully acquired and verified.

## Gaps

- **Gartneri 2026-2029**: not yet published as a standalone agreement PDF. GLS-A's OK26 settlement news
  (announced March 2026) covers Gartneri, but as of retrieval (2026-08-08) neither gls-a.dk nor 3f.dk hosts a
  typeset 2026-2029 Gartneri agreement PDF — only the 2024-2026 edition is downloadable. The 2024-2026 text was
  persisted per the task's fallback rule; any claims that depend specifically on 2026-2029 Gartneri wording are
  UNVERIFIABLE until that edition is published.
- **Skovbrug 2026-2029**: not yet published as a standalone agreement PDF. A signed OK26 negotiation protocol
  (`Protokollater-OK26-GLS-A-3F-Den-Groenne-Gruppe-underskrevet-1.pdf`, dated Feb 2026) exists, but the full
  consolidated 2026-2029 Skovbrug agreement text has not been typeset/published as of retrieval (2026-08-08),
  consistent with the ~2-month lag observed for the 2024-2026 edition (protocol → published text). The
  2024-2026 text was persisted per the task's fallback rule; any claims that depend specifically on 2026-2029
  Skovbrug wording are UNVERIFIABLE until that edition is published.
- **Golf 2026-2029**: no successor edition found. Both the GLS-A agreements listing and 3F's Den Grønne
  Gruppe overenskomst listing show "Golf 2024-2026" as the current edition with no 2026-2029 successor. GLS-A's
  OK26 settlement news explicitly covers landbrug/maskinstationer/skovbrug/gartnerier/planteskoler/agroindustri
  but does not mention Golf, suggesting Golf may be on a different renegotiation cycle. The 2024-2026 text was
  persisted per the task's fallback rule; any claims that depend specifically on 2026-2029 Golf wording are
  UNVERIFIABLE until (or unless) that edition is published.
