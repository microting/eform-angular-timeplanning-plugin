# Dependency alignment map — the repos that ship in `work-items-planning-container`

**Date:** 2026-09-13
**Trigger:** four `*-service-build` jobs failing deterministically on
`work-items-planning-container` run 34753032043 (all four attempts).

---

## 1. The failure, and why it is a version problem and not a code problem

```
Could not load file or assembly 'System.ComponentModel.Composition, Version=10.0.0.12,
  Culture=neutral, PublicKeyToken=b77a5c561934e089'
Could not load file or assembly
  '/app/Plugins/ServiceTimePlanningPlugin/System.ComponentModel.Composition.dll'.
The located assembly's manifest definition does not match the assembly reference.
```

The service host loads plugins out of `/app/Plugins/<Plugin>/`. Each plugin ships its own
copy of its transitive dependencies into that directory. When the host and the plugin were
built against different versions of the same package, the host's reference and the DLL in
the plugin folder disagree and the load fails **at runtime** — nothing catches this at
compile time, and no unit test catches it either, because the assembly only has to resolve
once the two are combined in the container.

Traced to the exact pins:

| | `System.ComponentModel.Composition` |
|---|---|
| `eform-debian-service/MicrotingService/MicrotingService.csproj:44` (host) | **10.0.10** |
| `eform-service-timeplanning-plugin/ServiceTimePlanningPlugin/…csproj:20` (plugin) | **10.0.11** |
| `eform-service-items-planning-plugin` | **10.0.9** |
| `eform-service-workflow-plugin` | **2010.2.11.1** ← a 2010-era package |
| assembly the runtime actually demanded | **10.0.0.12** (i.e. package `10.0.12`) |

Four different versions of one package across a host and the three plugins it loads, and
none of them is the one the runtime wanted.

**`2010.2.11.1` deserves its own callout.** `System.ComponentModel.Composition` has an
ancient release whose version number sorts highest by major, so anything that picks "the
latest version" naively lands on a package from 2010. Whoever bumped
`eform-service-workflow-plugin` almost certainly did that.

---

## 2. Scope: 11 repos ship together

From `work-items-planning-container/.github/workflows/dotnet-core-docker.yml`:

```
eform-angular-frontend                     eform-debian-service
eform-angular-greate-belt-plugin           eform-service-backendconfiguration-plugin
eform-angular-items-planning-plugin        eform-service-items-planning-plugin
eform-angular-timeplanning-plugin          eform-service-timeplanning-plugin
eform-angular-workflow-plugin              eform-service-workflow-plugin
eform-backendconfiguration-plugin
```

These eleven must agree. The other ~35 repos in the workspace do not ship in this
container and are out of scope here.

---

## 3. Current state

### 3a. The two SDK packages

| Repo | `Microting.eForm` | `Microting.eFormApi.BasePn` |
|---|---|---|
| **eform-angular-frontend** | **10.0.35, 10.0.37, 10.0.38, 10.0.39** | **10.0.29, 10.0.31, 10.0.32, 10.0.33** |
| eform-angular-greate-belt-plugin | 10.0.35 | 10.0.29 |
| eform-angular-items-planning-plugin | 10.0.35 | 10.0.29 |
| eform-angular-timeplanning-plugin | 10.0.39 *(on `stable`)* | 10.0.33 *(on `stable`)* |
| eform-angular-workflow-plugin | 10.0.35 | 10.0.29 |
| eform-backendconfiguration-plugin | 10.0.37, 10.0.38 | 10.0.31, 10.0.32 |
| eform-debian-service *(host csproj)* | 10.0.36 | — |
| eform-service-backendconfiguration-plugin | 10.0.38 | 10.0.32 |
| eform-service-items-planning-plugin | 10.0.35 | 10.0.29 |
| eform-service-timeplanning-plugin | 10.0.38 | 10.0.32 |
| **eform-service-workflow-plugin** | **10.0.22** | **10.0.21** |

Spread: **17 versions** between the newest (`eFormAPI.Web` at 10.0.39) and the oldest
(`eform-service-workflow-plugin` at 10.0.22).

### 3b. The host disagrees with itself

`eform-angular-frontend` is the epicentre — the host process and the plugins bundled inside
it are built against four different SDKs:

```
eForm=10.0.39  BasePn=10.0.33   eFormAPI/eFormAPI.Web/eFormAPI.Web.csproj          ← the host
eForm=10.0.38  BasePn=10.0.32   eFormAPI/Plugins/BackendConfiguration.Pn/…
eForm=10.0.37  BasePn=10.0.31   eFormAPI/Plugins/TimePlanning.Pn/…
eForm=10.0.35  BasePn=10.0.29   eFormAPI/Plugins/GreateBelt.Pn/…
eForm=10.0.35  BasePn=10.0.29   eFormAPI/Plugins/InsightDashboard.Pn/…
eForm=10.0.35  BasePn=10.0.29   eFormAPI/Plugins/ItemsPlanning.Pn/…
eForm=10.0.35  BasePn=10.0.29   eFormAPI/Plugins/OuterInnerResource.Pn/…
eForm=10.0.35  BasePn=10.0.29   eFormAPI/Plugins/Workflow.Pn/…
```

Note these are **devinstall mirrors**, not sources — but they are what the container builds,
so their pins are load-bearing. The same is true of
`eform-debian-service/Plugins/Service*Plugin/`, which holds stale copies of all four service
plugins pinning `Microting.eForm` **10.0.4** and `Microting.TimePlanningBase` **10.0.7**.

### 3c. Runtime packages that actually broke the build

| Package | Host | timeplanning | backendconfig | items-planning | workflow | **Target** |
|---|---|---|---|---|---|---|
| `System.ComponentModel.Composition` | 10.0.10 | 10.0.11 | 10.0.11 | 10.0.9 | **2010.2.11.1** | **10.0.12** |
| `Microsoft.Extensions.DependencyModel` | 10.0.10 | 10.0.11 | 10.0.11 | 10.0.9 | 10.0.7 | **10.0.12** |
| `Sentry` | 6.0.0 / 6.7.0 | 6.9.0 | 6.9.0 | 6.6.0 | 6.4.1 | **6.11.0** |

These three are the ones with a proven runtime consequence. Others differ too
(`DocumentFormat.OpenXml`, `QuestPDF`, `SkiaSharp`, `HtmlToOpenXml`, `HarfBuzzSharp`,
`Google.Apis.Sheets.v4`) but only inside single plugins, so they cannot collide across the
host boundary the same way.

---

## 4. Targets

All are the latest published, verified against nuget.org on 2026-09-13:

| Package | Target | Note |
|---|---|---|
| `Microting.eForm` | **10.0.39** | `eFormAPI.Web` is already here — this is catch-up, not a new bump |
| `Microting.eFormApi.BasePn` | **10.0.33** | ditto |
| `Microting.TimePlanningBase` | **10.0.62** | timeplanning already here |
| `Microting.ItemsPlanningBase` | **10.0.38** | |
| `Microting.EformAngularFrontendBase` | **10.0.37** | |
| `Microting.EformBackendConfigurationBase` | **10.0.50** | |
| `Microting.eFormWorkflowBase` | **10.0.35** | |
| `Microting.eFormCaseTemplateBase` | **10.0.36** | |
| `System.ComponentModel.Composition` | **10.0.12** | **NOT** `2010.2.11.1` — see §1 |
| `Microsoft.Extensions.DependencyModel` | **10.0.12** | ships on the .NET 10 servicing cadence |
| `Sentry` | **6.11.0** | |

---

## 5. Per-repo actions

Ordered by dependency: **bases → plugins → hosts.** A host bumped before its plugins just
moves the mismatch.

### Wave 1 — base packages (publish first, plugins consume them)

| Repo | eForm | BasePn |
|---|---|---|
| `eform-items-planning-base` | 10.0.36 → **10.0.39** | 10.0.30 → **10.0.33** |
| `eform-workflow-base` | 10.0.36 → **10.0.39** | 10.0.30 → **10.0.33** |
| `eform-angular-frontend-base` | 10.0.36 → **10.0.39** | 10.0.30 → **10.0.33** |
| `eform-timeplanning-base` | 10.0.38 → **10.0.39** | 10.0.32 → **10.0.33** |
| `eform-backendconfiguration-base` | 10.0.38 → **10.0.39** | 10.0.32 → **10.0.33** |

Each needs a release so the plugins have something to reference.

### Wave 2 — plugin repos

| Repo | eForm | BasePn | Also |
|---|---|---|---|
| `eform-service-workflow-plugin` | 10.0.22 → **10.0.39** | 10.0.21 → **10.0.33** | `System.ComponentModel.Composition` **2010.2.11.1 → 10.0.12**; `DependencyModel` → 10.0.12; `Sentry` → 6.11.0. **Biggest jump — 17 versions. Expect real breakage; do this one alone.** |
| `eform-angular-workflow-plugin` | 10.0.35 → **10.0.39** | 10.0.29 → **10.0.33** | |
| `eform-angular-greate-belt-plugin` | 10.0.35 → **10.0.39** | 10.0.29 → **10.0.33** | |
| `eform-angular-items-planning-plugin` | 10.0.35 → **10.0.39** | 10.0.29 → **10.0.33** | |
| `eform-service-items-planning-plugin` | 10.0.35 → **10.0.39** | 10.0.29 → **10.0.33** | `Composition` 10.0.9 → 10.0.12; `DependencyModel` → 10.0.12; `Sentry` 6.6.0 → 6.11.0 |
| `eform-backendconfiguration-plugin` | 10.0.37/38 → **10.0.39** | 10.0.31/32 → **10.0.33** | unify its two internal versions first |
| `eform-service-backendconfiguration-plugin` | 10.0.38 → **10.0.39** | 10.0.32 → **10.0.33** | `Composition` → 10.0.12 |
| `eform-service-timeplanning-plugin` | 10.0.38 → **10.0.39** | 10.0.32 → **10.0.33** | `Composition` 10.0.11 → **10.0.12** ← *the one in the failing log* |
| `eform-angular-timeplanning-plugin` | ✅ 10.0.39 | ✅ 10.0.33 | already aligned on `stable` (commit `9e2ae84f`) |

### Wave 3 — hosts

| Repo | Action |
|---|---|
| `eform-debian-service` | `MicrotingService.csproj`: eForm 10.0.36 → **10.0.39**; `Composition` 10.0.10 → **10.0.12**; `DependencyModel` 10.0.10 → **10.0.12**; `Sentry` → **6.11.0**. Then refresh the stale `Plugins/Service*Plugin/` mirrors, which still pin eForm **10.0.4** / TimePlanningBase **10.0.7** |
| `eform-angular-frontend` | Unify **all eight** `eFormAPI/**/*.csproj` to eForm **10.0.39** / BasePn **10.0.33**. The host is already there; the seven bundled plugin mirrors are not |

---

## 6. Preventing recurrence

Bumping eleven repos by hand puts them back in step once; it does nothing about the twelfth
time. Two options, in order of preference:

1. **Central Package Management.** A `Directory.Packages.props` per repo with
   `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`, versions declared
   once, and `<PackageReference Include="…" />` with no `Version` in the csprojs. Inside a
   repo this makes an internal disagreement (like the frontend's four-way split)
   *unrepresentable*.
2. **A CI check in the container repo** that fails the build when two checked-out repos pin
   different versions of the same package. Cheap, and it catches the cross-repo case that
   even per-repo CPM cannot: it is the combination that breaks, and only the container sees
   the combination.

The second is what would have caught this one. Neither the plugin's CI nor the host's CI can
see a mismatch that only exists once they are assembled.

---

## 7. Related but separate

Two other failures on the same run are **not** version problems:

- **`timeplanning-pn-dotnet-unit-test`** — `playwright-tour-seed.spec.ts` fails because the
  container repo's workflow copies the plugin's Angular sources (including the spec) without
  copying `playwright/helpers/tour-seen.storage.json` or the `storageState` wiring in
  `playwright.config.ts`. The plugin's own CI does copy them
  (`dotnet-core-pr.yml:68-70`, `:134-137`), which is why it is green there. Introduced by
  PR #1703. Fix: add the same two copy lines to the container workflow.
- **`timeplanning-pn-dotnet-test (b, …)`** — `Create_CreatesPictureSnapshot_WithFile` fails
  with `HTTP 403 InvalidAccessKeyId` uploading to `microting-uploaded-data`. An expired or
  missing AWS key in that repo's CI secrets. Nothing to do with packages.
