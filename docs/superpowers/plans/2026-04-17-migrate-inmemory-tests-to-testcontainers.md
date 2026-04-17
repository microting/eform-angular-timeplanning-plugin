# Migrate InMemory Tests to TestBaseSetup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate `DeviceTokenServiceTests.cs` and `PushNotificationServiceTests.cs` off the `Microsoft.EntityFrameworkCore.InMemory` provider onto the existing `TestBaseSetup` base class (Testcontainers + MariaDB), so the already-staged removal of the InMemory NuGet package does not break the test project build.

**Architecture:** The TimePlanning plugin test project already uses `Testcontainers.MariaDb` via `TestBaseSetup.cs` (inherited by ~20 other test classes like `GpsCoordinateServiceTests`). Two orphaned test files still spin up EF Core InMemory contexts directly; those contexts become uncompilable once `Microsoft.EntityFrameworkCore.InMemory` is dropped from the csproj. Migration is mechanical: inherit from `TestBaseSetup`, call `await base.Setup()` in `[SetUp]`, replace local `_dbContext` field usage with the inherited `TimePlanningPnDbContext` field. No production code changes.

**Tech Stack:** .NET, NUnit, EF Core, Testcontainers.MariaDb, NSubstitute.

**Working directory:** `/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/` (source plugin repo — not dev-mode, edits land directly here).

---

## File Structure

**Modified:**
- `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DeviceTokenServiceTests.cs` — inherit `TestBaseSetup`, remove InMemory provider.
- `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PushNotificationServiceTests.cs` — inherit `TestBaseSetup`, remove InMemory provider.
- `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj` — already staged in working tree (InMemory package reference removed). Committed as part of the final task.

**Unchanged:**
- `TestBaseSetup.cs` — already provides `TimePlanningPnDbContext` field initialized per test against a containerized MariaDB.
- Production services (`DeviceTokenService`, `PushNotificationService`) — no behaviour change under test.

---

## Task 1: Migrate `DeviceTokenServiceTests.cs` to TestBaseSetup

**Files:**
- Modify: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DeviceTokenServiceTests.cs`

**Context the new engineer needs:**
- `TestBaseSetup` exposes a protected `TimePlanningPnDbContext? TimePlanningPnDbContext` field, initialized in its `[SetUp] Setup()` against a fresh MariaDB (DB dropped + re-migrated per test). See lines 26 and 101 of `TestBaseSetup.cs` (`/home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TestBaseSetup.cs`).
- The canonical pattern for a derived test class is `GpsCoordinateServiceTests.cs` — inherit, override `SetUp`, call `await base.Setup()` first.
- The current `DeviceTokenServiceTests` uses a per-test InMemory database name keyed on `TestContext.CurrentContext.Test.Name`. That isolation is automatic under `TestBaseSetup` because the base class drops & re-migrates the DB on each `[SetUp]`.
- A real MariaDB enforces the unique index on `DeviceToken.Token` (defined in `TimePlanningPnDbContext` with `HasIndex(x => x.Token).IsUnique()`). The existing test `RegisterAsync_SameTokenTwice_UpsertsWithoutDuplicate` now becomes a genuine validation of that constraint rather than a no-op.

- [ ] **Step 1: Establish baseline — attempt the build and confirm the InMemory removal currently breaks it**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet build
```

Expected: build fails with errors like `The type or namespace 'UseInMemoryDatabase' could not be found` (or equivalent) pointing at `DeviceTokenServiceTests.cs` line 22 and `PushNotificationServiceTests.cs` lines 17 and 34. This confirms the InMemory package has been removed from the csproj and the two orphaned files need migrating.

- [ ] **Step 2: Rewrite `DeviceTokenServiceTests.cs` to inherit `TestBaseSetup`**

Replace the entire file `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DeviceTokenServiceTests.cs` with:

```csharp
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.DeviceTokenService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class DeviceTokenServiceTests : TestBaseSetup
{
    private DeviceTokenService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();

        _service = new DeviceTokenService(
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<DeviceTokenService>>());
    }

    [Test]
    public async Task RegisterAsync_NewToken_IsStored()
    {
        var result = await _service.RegisterAsync(42, "fcm-token-abc", "android");

        Assert.That(result.Success, Is.True);

        var stored = await TimePlanningPnDbContext!.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(42));
        Assert.That(stored.Token, Is.EqualTo("fcm-token-abc"));
        Assert.That(stored.Platform, Is.EqualTo("android"));
    }

    [Test]
    public async Task RegisterAsync_SameTokenTwice_UpsertsWithoutDuplicate()
    {
        await _service.RegisterAsync(1, "dup-token", "android");

        var result = await _service.RegisterAsync(2, "dup-token", "ios");

        Assert.That(result.Success, Is.True);
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var stored = await TimePlanningPnDbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(2));
        Assert.That(stored.Platform, Is.EqualTo("ios"));
    }

    [Test]
    public async Task UnregisterAsync_ExistingToken_IsRemoved()
    {
        await _service.RegisterAsync(1, "remove-me", "android");
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var result = await _service.UnregisterAsync("remove-me");

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task UnregisterAsync_NonExistentToken_SucceedsWithoutError()
    {
        var result = await _service.UnregisterAsync("does-not-exist");

        Assert.That(result.Success, Is.True);
    }
}
```

Notes on the diff:
- Drops `Microting.TimePlanningBase.Infrastructure.Data` import (the DbContext is inherited as a field).
- Removes the private `_dbContext` field; uses the inherited `TimePlanningPnDbContext` member (nullable, so dereference with `!`).
- Removes `[TearDown]` entirely; `TestBaseSetup.TearDown()` already disposes the context.
- Local `databaseName:` per-test key is no longer needed — `TestBaseSetup.Setup()` drops and re-migrates per `[SetUp]`.

- [ ] **Step 3: Build just the test project to verify `DeviceTokenServiceTests` compiles**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet build
```

Expected: compile errors for `DeviceTokenServiceTests.cs` are gone. Remaining errors will be limited to `PushNotificationServiceTests.cs` (handled in Task 2). If you see any other compile error, stop and investigate — it indicates a pre-existing issue unrelated to this migration.

- [ ] **Step 4: Run the migrated DeviceTokenServiceTests and verify green**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~DeviceTokenServiceTests"
```

Expected: 4 tests pass. First run will take longer (~20–40s) because Testcontainers boots a MariaDB image. If `RegisterAsync_SameTokenTwice_UpsertsWithoutDuplicate` fails with a unique-constraint violation, that is a genuine finding — the service upsert logic races or uses the wrong index — and should be reported rather than worked around. Do not modify the production service here.

If the command cannot run PushNotificationServiceTests yet because the test project still has compile errors, use `dotnet build -p:CompilerErrorsAsWarnings=false` as normal — the filter will only run matching tests, but compilation still has to succeed project-wide. In that case, skip the run to after Task 2 and run both together.

- [ ] **Step 5: Commit the DeviceTokenServiceTests migration**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin && git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/DeviceTokenServiceTests.cs && git commit -m "test: migrate DeviceTokenServiceTests off EF InMemory onto TestBaseSetup

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

Expected: a single-file commit on the current `stable` branch.

---

## Task 2: Migrate `PushNotificationServiceTests.cs` to TestBaseSetup

**Files:**
- Modify: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PushNotificationServiceTests.cs`

**Context the new engineer needs:**
- `PushNotificationService`'s constructor queries `PluginConfigurationValues` during construction (see `PushNotificationService.cs:28-29`). Under the real MariaDB provided by `TestBaseSetup`, that table exists (migrations create it) and the `FirstOrDefault` returns `null` — so the `_isEnabled` path stays `false`, which is exactly what the two tests validate ("without Firebase config").
- Both tests assert fire-and-forget / constructor behaviour: no DB rows read or written. The migration simply swaps the DbContext source; the assertions stay identical.

- [ ] **Step 1: Rewrite `PushNotificationServiceTests.cs` to inherit `TestBaseSetup`**

Replace the entire file `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PushNotificationServiceTests.cs` with:

```csharp
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.PushNotificationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PushNotificationServiceTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
    }

    [Test]
    public void Constructor_WithoutFirebaseConfig_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
        {
            _ = new PushNotificationService(
                TimePlanningPnDbContext!,
                Substitute.For<ILogger<PushNotificationService>>());
        });
    }

    [Test]
    public async Task SendToSiteAsync_WhenFirebaseNotConfigured_IsNoOp()
    {
        var service = new PushNotificationService(
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<PushNotificationService>>());

        await service.SendToSiteAsync(1, "Title", "Body");
    }
}
```

Notes on the diff:
- Drops `Microsoft.EntityFrameworkCore` and `Microting.TimePlanningBase.Infrastructure.Data` imports — no longer needed at call site.
- Removes both local `DbContextOptionsBuilder` blocks; uses inherited `TimePlanningPnDbContext`.
- Removes per-test `dbContext.Dispose()` — `TestBaseSetup.TearDown()` handles it.
- `SendToSiteAsync_WhenFirebaseNotConfigured_IsNoOp` keeps its `async Task` signature because `SendToSiteAsync` is awaited; no assertion needed — passing = didn't throw.

- [ ] **Step 2: Build the test project**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet build
```

Expected: build succeeds with no errors. All references to `UseInMemoryDatabase` are gone.

- [ ] **Step 3: Run the migrated PushNotificationServiceTests and verify green**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test --filter "FullyQualifiedName~PushNotificationServiceTests&FullyQualifiedName!~Integration"
```

The `!~Integration` exclusion avoids running `PushNotificationIntegrationTests`, which is a larger test and orthogonal to this migration.

Expected: 2 tests pass.

- [ ] **Step 4: Commit the PushNotificationServiceTests migration**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin && git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PushNotificationServiceTests.cs && git commit -m "test: migrate PushNotificationServiceTests off EF InMemory onto TestBaseSetup

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

Expected: a single-file commit on the current `stable` branch.

---

## Task 3: Full-suite verification and csproj commit

**Files:**
- Modify: `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj` (already modified in the working tree — InMemory package reference removed).

- [ ] **Step 1: Run the full test suite**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test && dotnet test
```

Expected: all tests pass. First invocation is slow because the whole suite shares the Testcontainers MariaDB lifecycle. If any test other than the two migrated ones fails, it is a pre-existing issue unrelated to this plan — stop and report rather than attempting to fix, as unrelated fixes would pollute the migration commits.

- [ ] **Step 2: Inspect working tree and confirm only the intended csproj change remains**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin && git status && git diff eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj
```

Expected: the only unstaged change is the removal of the `<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" ... />` line from the csproj. No other modifications should be present. If anything else shows up, `git checkout` it back before the next step.

- [ ] **Step 3: Commit the csproj change**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin && git add eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/TimePlanning.Pn.Test.csproj && git commit -m "test: drop Microsoft.EntityFrameworkCore.InMemory dependency

All tests now run against a real MariaDB via Testcontainers (TestBaseSetup),
matching production behaviour for unique constraints, transactions, and
referential integrity.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

Expected: a single-file commit. Three commits total on the branch after this plan completes.

- [ ] **Step 4: Final verification that the branch is clean and green**

Run:
```bash
cd /home/rene/Documents/workspace/microting/eform-angular-timeplanning-plugin && git status && git log --oneline -4
```

Expected: clean working tree; the three new commits at the top of the log (csproj drop, PushNotification migration, DeviceToken migration) in addition to prior history.

---

## Out of Scope

- No production code changes (`DeviceTokenService`, `PushNotificationService`).
- No changes to `TestBaseSetup` or any other test file.
- No addition of new test cases — migration preserves the existing 4 + 2 cases.
- Pushing or opening a PR — the user will handle that separately.
