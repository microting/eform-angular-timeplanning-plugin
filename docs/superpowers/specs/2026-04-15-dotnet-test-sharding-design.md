# Shard `test-dotnet` into 8 parallel GitHub Actions jobs

Status: **Draft** — awaiting review.
Author: Claude (brainstormed with René)
Date: 2026-04-15

## Problem

The `test-dotnet` CI job runs all 243 tests across 33 classes in a single GitHub Actions runner and takes >1 hour. The harness `TestBaseSetup` does a full `EnsureDeleted` + `Database.Migrate` + seed on every `[SetUp]`, and 8 SDK-heavy classes additionally load a 19 MB `420_SDK.sql` on each test. Wall-clock time has become the long pole on every PR.

In-process parallelism is blocked today: `test.runsettings` pins `MaxCpuCount=0`, every test writes to the same database name (`420_eform-angular-items-planning-plugin`), and nothing is marked `[Parallelizable]`. Fixing those requires per-test DB isolation and a harness refactor — deferred.

## Solution

Split the single job into a `strategy.matrix` of 8 jobs (`a`–`h`), each running a disjoint subset of the test classes against its own Testcontainer. No code inside the test project changes. The branch-protection surface is kept stable by gating on an aggregate `test-dotnet-gate` job.

### Target workflow shape

```yaml
test-dotnet:
  runs-on: ubuntu-latest
  strategy:
    fail-fast: false
    matrix:
      shard:
        - { name: a, filter: "..." }
        - { name: b, filter: "..." }
        # ...
  steps:
    - uses: actions/checkout@v4
    # existing setup-dotnet, restore, build
    - name: Run shard ${{ matrix.shard.name }}
      run: >
        dotnet test --no-restore -c Release
        --settings eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/test.runsettings
        --filter "${{ matrix.shard.filter }}"
        eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj

test-dotnet-gate:
  needs: test-dotnet
  if: always()
  runs-on: ubuntu-latest
  steps:
    - run: |
        if [ "${{ needs.test-dotnet.result }}" != "success" ]; then exit 1; fi
```

`test-dotnet-gate` is the single required check added to branch protection, replacing `test-dotnet`.

### Filter semantics

`dotnet test --filter` on the vstest format accepts predicates OR-joined with `|`. We use **exact-match** `FullyQualifiedName=TimePlanning.Pn.Test.<ClassName>` (with the `GrpcServices` sub-namespace for the 6 gRPC classes).

Exact match is required, not `~Contains`: class names overlap (e.g. `SettingsServiceTests` is a substring of `SettingsServiceExtendedTests` and `SettingsServicePhoneNumberTests`; `PlanRegistrationHelperTests` is a substring of three siblings). A `~` filter would cross-match and the same test would run in multiple shards.

Each shard's filter is a long pipe-separated list (one `FullyQualifiedName=…` per class). Not pretty but unambiguous and completely inert to future refactors that might shift substrings.

### Shard buckets

Each of the 8 SDK-heavy classes (grep: `GetCore|StartSqlOnly`) is spread one per shard so wall-time is even. Remaining 25 classes are distributed 3–4 per shard alphabetically to keep the list easy to maintain. One shard (`h`) carries one extra non-SDK class (5 total vs 4 elsewhere) because 25 doesn't divide cleanly into 8.

**Bucket list (authoritative):**

| Shard | SDK-heavy (1 ea.)                       | Non-SDK classes                                                                                                                                                                       |
|-------|------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| a     | `AbsenceRequestServiceTests`             | `BreakPolicyControllerTests`, `BreakPolicyServiceTests`, `CanaryInAColeMine`                                                                                                          |
| b     | `PictureSnapshotServiceTests`            | `ContentHandoverServiceTests`, `DanLonFileExporterTests`, `DataLonFileExporterTests`                                                                                                  |
| c     | `PlanningServiceMultiShiftTests`         | `DeviceTokenServiceTests`, `GpsCoordinateServiceTests`, `PayDayTypeRuleServiceTests`                                                                                                  |
| d     | `PlanRegistrationVersionHistoryTests`    | `PayRuleSetControllerTests`, `PayRuleSetServiceTests`, `PayTierRuleServiceTests`                                                                                                      |
| e     | `PushNotificationIntegrationTests`       | `PayTimeBandRuleServiceTests`, `PlanRegistrationHelperComputationTests`, `PlanRegistrationHelperHolidayTests`                                                                         |
| f     | `SettingsServiceExtendedTests`           | `PlanRegistrationHelperReadBySiteAndDateTests`, `PlanRegistrationHelperTests`, `PushNotificationServiceTests`                                                                         |
| g     | `SettingsServicePhoneNumberTests`        | `TimePlanningWorkingHoursExportTests`, `TimePlanningAbsenceRequestGrpcServiceTests`, `TimePlanningAuthGrpcServiceTests`                                                               |
| h     | `SettingsServiceTests`                   | `TimePlanningContentHandoverGrpcServiceTests`, `TimePlanningPlanningsGrpcServiceTests`, `TimePlanningSettingsGrpcServiceTests`, `TimePlanningWorkingHoursGrpcServiceTests`            |

Total: 33 classes (8 SDK-heavy + 25 non-SDK). 243 tests split across 8 jobs.

### Filter strings

Each shard's `filter` value is `FullyQualifiedName~Cls1|FullyQualifiedName~Cls2|...`. The concrete strings are committed into the workflow YAML verbatim (see Implementation section for the eight filter lines).

## Isolation

- **Testcontainers MariaDB**: each runner starts its own container. No cross-shard DB collision.
- **DB names**: identical across shards (`420_eform-angular-items-planning-plugin`, `420_SDK`). Safe because each shard runs in its own container.
- **No shared cache**: `gradle-cache` / nuget cache can still be shared per actions/setup-dotnet convention — read-only from the test's perspective.
- **Port conflicts**: Testcontainers picks random high ports per container start, so no host-level contention across matrix jobs on the same runner pool.

## Rollout

1. Update `.github/workflows/dotnet-core-pr.yml` with the matrix + gate.
2. Open a throwaway PR on `feat/shard-dotnet-ci` and verify all 8 shards pass and `test-dotnet-gate` turns green.
3. Update branch-protection in GitHub repo settings: remove `test-dotnet`, add `test-dotnet-gate`. Do this **after** the PR is green so the branch doesn't get stuck.
4. Mirror the matrix change into `.github/workflows/dotnet-core-master.yml`.
5. Announce in the team channel — anyone with an open PR needs to rebase to pick up the new required check.

## Maintenance

- **Adding a new test class**: the bucket list above lives in this spec plus the workflow filter strings. When someone adds `FooServiceTests.cs`, they append it to the shortest shard's filter string in `dotnet-core-pr.yml` and `dotnet-core-master.yml`, and update this doc's table. A `CONTRIBUTING.md` note or a PR-template reminder helps make this habit-forming.
- **SDK-heavy test added (calls `GetCore()`)**: put it in whichever shard currently has no SDK-heavy class — there should never be one, but if a shard grew a second SDK-heavy class and another has none, rebalance.
- **Drift detection (stretch)**: a small script `scripts/verify-test-shards.sh` can `dotnet test --list-tests` the project, compare class names against the union of shard filters, and fail if any class isn't covered. Not required for v1 but worth adding once the matrix is stable.

## Expected result

- Per-shard runtime: ~7–9 min (Testcontainer startup ~30 s + one-eighth of the 1-hour sequential budget).
- Total wall time: ~10 min including GHA scheduling latency.
- CI-minute cost: roughly unchanged (same total work, 8 runners × 8 min vs 1 runner × 60 min — GHA bills by minute, so ~8 × 10 = 80 min billed vs 60 min today — ~33% more minutes for a ~6× wall-clock speedup). Acceptable tradeoff on the paid plan; worth confirming cost budget allows.

## Out of scope

- Enabling in-process NUnit parallelism (`MaxCpuCount>0`). Blocked by shared DB names; requires per-test DB-name or schema isolation.
- Moving `GetCore()` from per-test `[SetUp]` into class-level `[OneTimeSetUp]`. Separate optimization, ~2–4 min additional savings across the suite. Worth doing later.
- Snapshot/restore MariaDB state instead of drop+migrate per test. Larger harness refactor.
- Changing `test.runsettings` at all — the file is untouched; sharding happens purely at the GHA layer.

## Open questions

- **Runner concurrency**: does the Microting GH org have 8 parallel runners available simultaneously? If the account is throttled to e.g. 4 concurrent jobs, the wall-time win drops to ~15–20 min and the matrix serializes. Check before rollout.
- **Flaky test cross-contamination**: if any class happens to depend on global state (static fields, environment vars), different shard orderings could expose or hide flakiness. Watch the first few runs for new flakes.

## Verification

1. Throwaway PR green across all 8 shards.
2. Total wall time for `test-dotnet-gate` ≤ 12 min.
3. No test loss: sum of per-shard test counts equals pre-change count (243).
4. Re-run one shard in isolation on `gh run rerun --failed` works (no dependency on other shards).
