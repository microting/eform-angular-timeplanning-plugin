# Playwright Always-Record Video Design

## Context

Playwright tests currently only capture video/screenshot on failure (`retain-on-failure`). This makes it impossible to review test behavior when tests pass. We want video recordings always available as CI artifacts, similar to Cypress video recording.

## Changes

### 1. playwright.config.ts - Always record video

Change `video` from `'retain-on-failure'` to `'on'`. Keep screenshot as `'only-on-failure'` (screenshots are less useful when video exists). Skip trace (large files, not needed for general monitoring).

### 2. CI workflow - Upload artifacts always

Change artifact upload condition from `if: failure()` to `if: always()` in both workflow files. This ensures video recordings are available as downloadable artifacts even when all tests pass.

### Files to modify

| File | Change |
|------|--------|
| `eform-client/playwright.config.ts` | `video: 'retain-on-failure'` -> `video: 'on'` |
| `.github/workflows/dotnet-core-master.yml` | `if: failure()` -> `if: always()` on artifact upload step |
| `.github/workflows/dotnet-core-pr.yml` | Same change |

## Verification

After merge, check any CI run's artifacts - every playwright-report-{a..o} artifact should contain video files (`.webm`) regardless of test outcome.
