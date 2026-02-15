# Rule Engine Implementation Guide

## Overview
This document provides a comprehensive guide for implementing the advanced rule engine features for overtime and holiday logic in the TimePlanning plugin.

## Current Status

### ✅ Completed Foundation (in Microting.TimePlanningBase v10.0.15)
- Database entities exist:
  - `PayRuleSet`, `PayDayRule`, `PayDayTypeRule`, `PayTierRule`, `PayTimeBandRule`
  - `WorkingTimeRuleSet` (with overtime fields: `OvertimePeriodLengthDays`, `OvertimeAveragingWindowDays`, `MonthlyNormMode`, `OvertimeAllocationStrategy`)
  - `BreakPolicy`, `BreakPolicyRule`
  - `PlanRegistrationPayLine`
- Helper methods in `PlanRegistrationHelper.cs`:
  - `GetWorkIntervals()` - Extract work time intervals
  - `GetPauseIntervals()` - Extract pause time intervals
  - `CalculateTotalSeconds()` - Sum interval durations
  - `GetDayCode()` - Classify day type
  - `IsOfficialHoliday()` - Check holiday status
  - `ComputeTimeTrackingFields()` - Calculate work hours
  - `MarkAsRuleEngineCalculated()` - Mark as processed

### 🚧 To Be Implemented
1. Break Policy Application Logic
2. Pay Line Generation Logic
3. Overtime Calculation Logic
4. Holiday Paid-Off Logic
5. Time Band Resolution Logic
6. 11-Hour Rest Rule Validation
7. API CRUD Endpoints for all rule entities
8. Integration Tests

---

## Implementation Approach

### Principle 1: Backward Compatibility
**All new logic must be opt-in and gated behind configuration checks.**

Example pattern:
```csharp
// If new feature is not configured, use existing behavior
if (assignedSite.BreakPolicyId == null || breakPolicy == null)
{
    // Existing behavior - no changes
    return;
}

// New feature logic only runs when explicitly configured
ApplyBreakPolicy(planRegistration, breakPolicy);
```

### Principle 2: Incremental Implementation
Each feature should be:
1. Implemented in isolation
2. Tested independently
3. Integrated with existing code via opt-in flags
4. Validated that existing tests still pass

### Principle 3: Seconds-First Calculation
All calculations use seconds internally to avoid floating-point errors:
```csharp
long totalSeconds = intervals.Sum(i => (long)(i.End - i.Start).TotalSeconds);
double hours = totalSeconds / 3600.0; // Only convert to hours for display
```

---

## Feature Implementation Details

## 1. Break Policy Application

### Purpose
Split pause time into paid/unpaid breaks based on weekday-specific rules.

### Database Structure
```
BreakPolicy (1) --> (M) BreakPolicyRule
  - Id
  - Name
  - Description

BreakPolicyRule
  - Id
  - BreakPolicyId
  - DayOfWeek (0-6: Sunday-Saturday)
  - PaidBreakSeconds (per day)
  - UnpaidBreakSeconds (per day)
```

### Implementation

**Location**: `PlanRegistrationHelper.cs`

```csharp
/// <summary>
/// Apply break policy to split total pause time into paid and unpaid breaks.
/// Uses weekday-specific rules from BreakPolicy.
/// Backward compatible: Does nothing if break policy is null.
/// </summary>
/// <param name="planRegistration">The plan registration to update</param>
/// <param name="breakPolicy">The break policy with rules (null-safe)</param>
/// <param name="totalPauseSeconds">Total pause time in seconds</param>
public static void ApplyBreakPolicy(
    PlanRegistration planRegistration,
    BreakPolicy breakPolicy,
    long totalPauseSeconds)
{
    // Backward compatibility: If no break policy, do nothing
    if (breakPolicy == null || breakPolicy.BreakPolicyRules == null)
    {
        planRegistration.TotalPauseHoursInSeconds = (int)totalPauseSeconds;
        planRegistration.UnpaidBreakHoursInSeconds = 0;
        planRegistration.PaidBreakHoursInSeconds = 0;
        return;
    }

    // Find the rule for this day of week
    var dayOfWeek = planRegistration.Date.DayOfWeek;
    var rule = breakPolicy.BreakPolicyRules
        .FirstOrDefault(r => r.DayOfWeek == (int)dayOfWeek);

    if (rule == null)
    {
        // No rule for this day - treat all pause as unpaid (conservative)
        planRegistration.TotalPauseHoursInSeconds = (int)totalPauseSeconds;
        planRegistration.UnpaidBreakHoursInSeconds = (int)totalPauseSeconds;
        planRegistration.PaidBreakHoursInSeconds = 0;
        return;
    }

    // Apply the rule: allocate paid break first, rest is unpaid
    var paidBreakSeconds = Math.Min(totalPauseSeconds, rule.PaidBreakSeconds);
    var unpaidBreakSeconds = totalPauseSeconds - paidBreakSeconds;

    planRegistration.TotalPauseHoursInSeconds = (int)totalPauseSeconds;
    planRegistration.PaidBreakHoursInSeconds = (int)paidBreakSeconds;
    planRegistration.UnpaidBreakHoursInSeconds = (int)unpaidBreakSeconds;

    // Update PaidForExportSeconds (used for payroll export)
    planRegistration.PaidForExportSeconds =
        planRegistration.EffectiveNetHoursInSeconds +
        planRegistration.PaidBreakHoursInSeconds;
}
```

### Test Cases

```csharp
[Test]
public void ApplyBreakPolicy_WithNoPolicy_DoesNotModifyBreakFields()
{
    // Arrange
    var pr = new PlanRegistration { Date = new DateTime(2026, 1, 15) }; // Thursday
    
    // Act
    PlanRegistrationHelper.ApplyBreakPolicy(pr, null, 3600);
    
    // Assert
    Assert.That(pr.TotalPauseHoursInSeconds, Is.EqualTo(3600));
    Assert.That(pr.PaidBreakHoursInSeconds, Is.EqualTo(0));
    Assert.That(pr.UnpaidBreakHoursInSeconds, Is.EqualTo(0));
}

[Test]
public void ApplyBreakPolicy_WithPolicy_SplitsPaidAndUnpaid()
{
    // Arrange
    var pr = new PlanRegistration { Date = new DateTime(2026, 1, 15) }; // Thursday
    var policy = new BreakPolicy
    {
        BreakPolicyRules = new List<BreakPolicyRule>
        {
            new BreakPolicyRule
            {
                DayOfWeek = 4, // Thursday
                PaidBreakSeconds = 1800, // 30 minutes paid
                UnpaidBreakSeconds = 0
            }
        }
    };
    
    // Act
    PlanRegistrationHelper.ApplyBreakPolicy(pr, policy, 3600); // 60 min total
    
    // Assert
    Assert.That(pr.TotalPauseHoursInSeconds, Is.EqualTo(3600));
    Assert.That(pr.PaidBreakHoursInSeconds, Is.EqualTo(1800)); // 30 min paid
    Assert.That(pr.UnpaidBreakHoursInSeconds, Is.EqualTo(1800)); // 30 min unpaid
}
```

---

## 2. Pay Line Generation

### Purpose
Generate `PlanRegistrationPayLine` records based on `PayRuleSet` tier rules for different day types.

### Database Structure
```
PayRuleSet (1) --> (M) PayDayRule (1) --> (M) PayTierRule
  - Id                 - Id                   - Id
  - Name               - PayRuleSetId         - PayDayRuleId
  - Description        - DayCode (SUNDAY,     - Order (1, 2, 3...)
                         SATURDAY, WEEKDAY,   - UpToSeconds
                         HOLIDAY,             - PayCode (e.g. "SUN_80")
                         GRUNDLOVSDAG)        - Description

Example: Sunday work
- DayCode: SUNDAY
- Tier 1: 0-39600s (11h) → PayCode "SUN_80" (80% rate)
- Tier 2: 39600s+ → PayCode "SUN_100" (100% premium)
```

### Implementation

```csharp
/// <summary>
/// Generate pay lines for a plan registration based on pay rule set.
/// Splits paid hours across tiers defined in PayDayRule.
/// Backward compatible: Returns empty list if no pay rule set configured.
/// </summary>
/// <param name="planRegistrationId">The plan registration ID</param>
/// <param name="dayCode">Day classification (SUNDAY, SATURDAY, WEEKDAY, HOLIDAY, GRUNDLOVSDAG)</param>
/// <param name="paidForExportSeconds">Seconds to allocate across pay tiers</param>
/// <param name="payRuleSet">The pay rule set with day rules and tiers</param>
/// <param name="calculatedAtUtc">Timestamp of calculation</param>
/// <returns>List of pay lines to persist</returns>
public static List<PlanRegistrationPayLine> GeneratePayLines(
    int planRegistrationId,
    string dayCode,
    long paidForExportSeconds,
    PayRuleSet payRuleSet,
    DateTime calculatedAtUtc)
{
    var payLines = new List<PlanRegistrationPayLine>();

    // Backward compatibility: If no pay rule set, return empty
    if (payRuleSet == null || payRuleSet.PayDayRules == null)
    {
        return payLines;
    }

    // Find the day rule for this day code
    var dayRule = payRuleSet.PayDayRules
        .FirstOrDefault(r => r.DayCode == dayCode);

    if (dayRule == null || dayRule.PayTierRules == null)
    {
        // No rule for this day type - no pay lines generated
        return payLines;
    }

    // Sort tier rules by Order
    var tiers = dayRule.PayTierRules
        .Where(t => t.WorkflowState != Constants.WorkflowStates.Removed)
        .OrderBy(t => t.Order)
        .ToList();

    if (!tiers.Any())
    {
        return payLines;
    }

    // Allocate paid seconds across tiers
    long remainingSeconds = paidForExportSeconds;
    long previousBoundary = 0;

    foreach (var tier in tiers)
    {
        if (remainingSeconds <= 0)
        {
            break;
        }

        // Calculate tier capacity
        long tierCapacity = tier.UpToSeconds.HasValue
            ? tier.UpToSeconds.Value - previousBoundary
            : long.MaxValue; // Last tier has no upper limit

        // Allocate to this tier
        long allocatedSeconds = Math.Min(remainingSeconds, tierCapacity);

        if (allocatedSeconds > 0)
        {
            payLines.Add(new PlanRegistrationPayLine
            {
                PlanRegistrationId = planRegistrationId,
                PayCode = tier.PayCode,
                Seconds = (int)allocatedSeconds,
                Hours = allocatedSeconds / 3600.0,
                CalculatedAtUtc = calculatedAtUtc,
                TierOrder = tier.Order,
                DayCode = dayCode,
                CreatedAt = calculatedAtUtc,
                UpdatedAt = calculatedAtUtc,
                WorkflowState = Constants.WorkflowStates.Created
            });

            remainingSeconds -= allocatedSeconds;
        }

        if (tier.UpToSeconds.HasValue)
        {
            previousBoundary = tier.UpToSeconds.Value;
        }
    }

    return payLines;
}
```

### Test Cases

```csharp
[Test]
public void GeneratePayLines_Sunday14Hours_SplitsInto11And3Hours()
{
    // Arrange
    var payRuleSet = new PayRuleSet
    {
        PayDayRules = new List<PayDayRule>
        {
            new PayDayRule
            {
                DayCode = "SUNDAY",
                PayTierRules = new List<PayTierRule>
                {
                    new PayTierRule
                    {
                        Order = 1,
                        UpToSeconds = 39600, // 11 hours
                        PayCode = "SUN_80"
                    },
                    new PayTierRule
                    {
                        Order = 2,
                        UpToSeconds = null, // Unlimited
                        PayCode = "SUN_100"
                    }
                }
            }
        }
    };

    // Act
    var payLines = PlanRegistrationHelper.GeneratePayLines(
        planRegistrationId: 123,
        dayCode: "SUNDAY",
        paidForExportSeconds: 50400, // 14 hours
        payRuleSet: payRuleSet,
        calculatedAtUtc: DateTime.UtcNow
    );

    // Assert
    Assert.That(payLines.Count, Is.EqualTo(2));
    Assert.That(payLines[0].PayCode, Is.EqualTo("SUN_80"));
    Assert.That(payLines[0].Seconds, Is.EqualTo(39600)); // 11h
    Assert.That(payLines[1].PayCode, Is.EqualTo("SUN_100"));
    Assert.That(payLines[1].Seconds, Is.EqualTo(10800)); // 3h
}
```

---

## 3. Pay Day Type Rules (New Entity Support)

### Purpose
Map day types to different pay rate structures, supporting time-of-day bands.

### Database Structure
```
PayRuleSet (1) --> (M) PayDayTypeRule (1) --> (M) PayTimeBandRule
  - Id                 - Id                      - Id
  - Name               - PayRuleSetId            - PayDayTypeRuleId
                       - DayType (Weekday,       - StartTimeOfDay (HH:MM)
                         Saturday, Sunday,       - EndTimeOfDay (HH:MM)
                         PublicHoliday,          - PayCode
                         CompanyHoliday)         - Order
```

### Implementation

```csharp
/// <summary>
/// Resolve pay code based on day type and time bands.
/// Supports splitting a single day across multiple pay codes based on time of day.
/// Example: Sunday work before 18:00 = SUN_80, after 18:00 = SUN_100.
/// Backward compatible: Falls back to PayDayRule if no PayDayTypeRule exists.
/// </summary>
/// <param name="planRegistration">The plan registration</param>
/// <param name="payRuleSet">The pay rule set with type rules</param>
/// <returns>List of (PayCode, Seconds) tuples for each time band</returns>
public static List<(string PayCode, long Seconds)> ResolvePayCodesByTimeBand(
    PlanRegistration planRegistration,
    PayRuleSet payRuleSet)
{
    var result = new List<(string, long)>();

    // Determine day type
    var dayType = GetDayType(planRegistration.Date);

    // Find day type rule
    var dayTypeRule = payRuleSet.PayDayTypeRules?
        .FirstOrDefault(r => r.DayType == dayType);

    if (dayTypeRule == null || dayTypeRule.PayTimeBandRules == null)
    {
        // Fallback to simple day code mapping
        var dayCode = GetDayCode(planRegistration.Date);
        var paidSeconds = planRegistration.PaidForExportSeconds;
        return new List<(string, long)> { (dayCode, paidSeconds) };
    }

    // Get work intervals
    var workIntervals = GetWorkIntervals(planRegistration);

    // For each time band, calculate overlap with work intervals
    var timeBands = dayTypeRule.PayTimeBandRules
        .OrderBy(r => r.Order)
        .ToList();

    foreach (var band in timeBands)
    {
        var bandStart = TimeSpan.Parse(band.StartTimeOfDay);
        var bandEnd = TimeSpan.Parse(band.EndTimeOfDay);

        long bandSeconds = CalculateOverlap(workIntervals, bandStart, bandEnd);

        if (bandSeconds > 0)
        {
            result.Add((band.PayCode, bandSeconds));
        }
    }

    return result;
}

/// <summary>
/// Determine day type from date and holiday configuration.
/// </summary>
private static string GetDayType(DateTime date)
{
    if (IsOfficialHoliday(date))
    {
        return "PublicHoliday";
    }

    return date.DayOfWeek switch
    {
        DayOfWeek.Sunday => "Sunday",
        DayOfWeek.Saturday => "Saturday",
        _ => "Weekday"
    };
}

/// <summary>
/// Calculate overlap between work intervals and a time band.
/// </summary>
private static long CalculateOverlap(
    IEnumerable<(DateTime Start, DateTime End)> workIntervals,
    TimeSpan bandStart,
    TimeSpan bandEnd)
{
    long totalSeconds = 0;

    foreach (var (start, end) in workIntervals)
    {
        var workStart = start.TimeOfDay;
        var workEnd = end.TimeOfDay;

        // Handle midnight crossing
        if (workEnd < workStart)
        {
            workEnd = workEnd.Add(TimeSpan.FromDays(1));
        }
        if (bandEnd < bandStart)
        {
            bandEnd = bandEnd.Add(TimeSpan.FromDays(1));
        }

        // Calculate overlap
        var overlapStart = workStart > bandStart ? workStart : bandStart;
        var overlapEnd = workEnd < bandEnd ? workEnd : bandEnd;

        if (overlapEnd > overlapStart)
        {
            totalSeconds += (long)(overlapEnd - overlapStart).TotalSeconds;
        }
    }

    return totalSeconds;
}
```

---

## 4. Overtime Calculation

### Purpose
Calculate overtime based on configurable periods (weekly, bi-weekly, monthly) and allocation strategies.

### Database Structure
```
WorkingTimeRuleSet
  - Id
  - Name
  - OvertimePeriodLengthDays (7 = weekly, 14 = bi-weekly, null = monthly)
  - OvertimeAveragingWindowDays (for rolling average)
  - MonthlyNormMode (CalendarDays, WorkingDays)
  - OvertimeAllocationStrategy (LatestFirst, EarliestFirst, Proportional)
  - StandardHoursPerWeek (e.g., 37.0)
```

### Implementation

```csharp
/// <summary>
/// Calculate overtime for a period of plan registrations.
/// Supports weekly, bi-weekly, monthly periods with configurable allocation strategies.
/// Backward compatible: Returns null if no working time rule set configured.
/// </summary>
/// <param name="planRegistrations">Plan registrations in the period</param>
/// <param name="workingTimeRuleSet">The working time rule set</param>
/// <returns>Dictionary of (PlanRegistrationId, OvertimeSeconds)</returns>
public static Dictionary<int, long> CalculateOvertime(
    List<PlanRegistration> planRegistrations,
    WorkingTimeRuleSet workingTimeRuleSet)
{
    var result = new Dictionary<int, long>();

    // Backward compatibility
    if (workingTimeRuleSet == null ||
        workingTimeRuleSet.OvertimePeriodLengthDays == null)
    {
        return result; // No overtime calculation
    }

    // Calculate period norm
    var periodLengthDays = workingTimeRuleSet.OvertimePeriodLengthDays.Value;
    var standardHoursPerWeek = workingTimeRuleSet.StandardHoursPerWeek ?? 37.0;
    var periodNormSeconds = (long)((standardHoursPerWeek / 7.0) * periodLengthDays * 3600);

    // Sum actual hours in period
    var totalWorkedSeconds = planRegistrations
        .Sum(pr => (long)pr.EffectiveNetHoursInSeconds);

    // Calculate overtime
    var overtimeSeconds = Math.Max(0, totalWorkedSeconds - periodNormSeconds);

    if (overtimeSeconds == 0)
    {
        return result;
    }

    // Allocate overtime based on strategy
    var strategy = workingTimeRuleSet.OvertimeAllocationStrategy ?? "LatestFirst";

    switch (strategy)
    {
        case "LatestFirst":
            result = AllocateOvertimeLatestFirst(planRegistrations, overtimeSeconds);
            break;
        case "EarliestFirst":
            result = AllocateOvertimeEarliestFirst(planRegistrations, overtimeSeconds);
            break;
        case "Proportional":
            result = AllocateOvertimeProportional(planRegistrations, overtimeSeconds, totalWorkedSeconds);
            break;
        default:
            throw new InvalidOperationException($"Unknown allocation strategy: {strategy}");
    }

    return result;
}

private static Dictionary<int, long> AllocateOvertimeLatestFirst(
    List<PlanRegistration> planRegistrations,
    long overtimeSeconds)
{
    var result = new Dictionary<int, long>();
    var sorted = planRegistrations.OrderByDescending(pr => pr.Date).ToList();

    long remaining = overtimeSeconds;
    foreach (var pr in sorted)
    {
        if (remaining <= 0) break;

        var allocated = Math.Min(remaining, pr.EffectiveNetHoursInSeconds);
        result[pr.Id] = allocated;
        remaining -= allocated;
    }

    return result;
}

private static Dictionary<int, long> AllocateOvertimeEarliestFirst(
    List<PlanRegistration> planRegistrations,
    long overtimeSeconds)
{
    var result = new Dictionary<int, long>();
    var sorted = planRegistrations.OrderBy(pr => pr.Date).ToList();

    long remaining = overtimeSeconds;
    foreach (var pr in sorted)
    {
        if (remaining <= 0) break;

        var allocated = Math.Min(remaining, pr.EffectiveNetHoursInSeconds);
        result[pr.Id] = allocated;
        remaining -= allocated;
    }

    return result;
}

private static Dictionary<int, long> AllocateOvertimeProportional(
    List<PlanRegistration> planRegistrations,
    long overtimeSeconds,
    long totalWorkedSeconds)
{
    var result = new Dictionary<int, long>();

    foreach (var pr in planRegistrations)
    {
        var proportion = (double)pr.EffectiveNetHoursInSeconds / totalWorkedSeconds;
        var allocated = (long)(overtimeSeconds * proportion);
        result[pr.Id] = allocated;
    }

    return result;
}
```

---

## 5. Orchestration Method

### Purpose
Orchestrate all calculations and persist to database in a single transaction.

### Implementation

```csharp
/// <summary>
/// Recalculate and persist all rule engine outputs for a plan registration.
/// This is the main orchestration method that coordinates all calculations.
/// Backward compatible: Falls back to simple calculation if no rules configured.
/// </summary>
/// <param name="planRegistration">The plan registration to process</param>
/// <param name="dbContext">Database context</param>
/// <param name="assignedSite">The assigned site with rule references</param>
public static async Task RecalculateAndPersistAsync(
    PlanRegistration planRegistration,
    TimePlanningPnDbContext dbContext,
    AssignedSite assignedSite)
{
    // Step 1: Compute basic time tracking fields
    ComputeTimeTrackingFields(planRegistration);

    // Step 2: Apply break policy (if configured)
    if (assignedSite.BreakPolicyId.HasValue)
    {
        var breakPolicy = await dbContext.BreakPolicies
            .Include(bp => bp.BreakPolicyRules)
            .FirstOrDefaultAsync(bp => bp.Id == assignedSite.BreakPolicyId.Value);

        if (breakPolicy != null)
        {
            ApplyBreakPolicy(
                planRegistration,
                breakPolicy,
                planRegistration.TotalPauseHoursInSeconds);
        }
    }

    // Step 3: Generate pay lines (if configured)
    if (assignedSite.PayRuleSetId.HasValue)
    {
        var payRuleSet = await dbContext.PayRuleSets
            .Include(prs => prs.PayDayRules)
                .ThenInclude(pdr => pdr.PayTierRules)
            .Include(prs => prs.PayDayTypeRules)
                .ThenInclude(pdtr => pdtr.PayTimeBandRules)
            .FirstOrDefaultAsync(prs => prs.Id == assignedSite.PayRuleSetId.Value);

        if (payRuleSet != null)
        {
            // Delete existing pay lines
            var existingLines = await dbContext.PlanRegistrationPayLines
                .Where(l => l.PlanRegistrationId == planRegistration.Id)
                .ToListAsync();

            foreach (var line in existingLines)
            {
                await line.Delete(dbContext);
            }

            // Generate new pay lines
            var dayCode = GetDayCode(planRegistration.Date);
            var newLines = GeneratePayLines(
                planRegistration.Id,
                dayCode,
                planRegistration.PaidForExportSeconds,
                payRuleSet,
                DateTime.UtcNow);

            foreach (var line in newLines)
            {
                await line.Create(dbContext);
            }

            // Store applied rule set IDs
            planRegistration.PayRuleSetId = assignedSite.PayRuleSetId;
            planRegistration.BreakPolicyId = assignedSite.BreakPolicyId;
        }
    }

    // Step 4: Mark as calculated
    MarkAsRuleEngineCalculated(planRegistration);

    // Step 5: Save changes
    await planRegistration.Update(dbContext);
}
```

---

## 6. API CRUD Endpoints

### Example: PayRuleSetsController

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Abstractions;
using TimePlanning.Pn.Infrastructure.Models.PayRules;

namespace TimePlanning.Pn.Controllers
{
    [Authorize]
    [Route("api/time-planning-pn/pay-rule-sets")]
    public class PayRuleSetsController : Controller
    {
        private readonly IPayRuleSetsService _payRuleSetsService;

        public PayRuleSetsController(IPayRuleSetsService payRuleSetsService)
        {
            _payRuleSetsService = payRuleSetsService;
        }

        [HttpGet]
        public async Task<OperationDataResult<PayRuleSetsListModel>> Index(
            [FromQuery] PayRuleSetsRequestModel requestModel)
        {
            return await _payRuleSetsService.Index(requestModel);
        }

        [HttpGet("{id}")]
        public async Task<OperationDataResult<PayRuleSetModel>> Read(int id)
        {
            return await _payRuleSetsService.Read(id);
        }

        [HttpPost]
        public async Task<OperationResult> Create([FromBody] PayRuleSetCreateModel model)
        {
            return await _payRuleSetsService.Create(model);
        }

        [HttpPut("{id}")]
        public async Task<OperationResult> Update(int id, [FromBody] PayRuleSetUpdateModel model)
        {
            return await _payRuleSetsService.Update(id, model);
        }

        [HttpDelete("{id}")]
        public async Task<OperationResult> Delete(int id)
        {
            return await _payRuleSetsService.Delete(id);
        }
    }
}
```

### Service Interface

```csharp
public interface IPayRuleSetsService
{
    Task<OperationDataResult<PayRuleSetsListModel>> Index(PayRuleSetsRequestModel requestModel);
    Task<OperationDataResult<PayRuleSetModel>> Read(int id);
    Task<OperationResult> Create(PayRuleSetCreateModel model);
    Task<OperationResult> Update(int id, PayRuleSetUpdateModel model);
    Task<OperationResult> Delete(int id);
}
```

### Service Implementation

```csharp
public class PayRuleSetsService : IPayRuleSetsService
{
    private readonly TimePlanningPnDbContext _dbContext;
    private readonly ILogger<PayRuleSetsService> _logger;

    public PayRuleSetsService(
        TimePlanningPnDbContext dbContext,
        ILogger<PayRuleSetsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OperationDataResult<PayRuleSetsListModel>> Index(
        PayRuleSetsRequestModel requestModel)
    {
        try
        {
            var query = _dbContext.PayRuleSets
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            var total = await query.CountAsync();

            var ruleSets = await query
                .Skip(requestModel.Offset)
                .Take(requestModel.PageSize)
                .Select(prs => new PayRuleSetSimpleModel
                {
                    Id = prs.Id,
                    Name = prs.Name,
                    Description = prs.Description
                })
                .ToListAsync();

            return new OperationDataResult<PayRuleSetsListModel>(
                true,
                new PayRuleSetsListModel
                {
                    Total = total,
                    PayRuleSets = ruleSets
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pay rule sets");
            return new OperationDataResult<PayRuleSetsListModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationDataResult<PayRuleSetModel>> Read(int id)
    {
        try
        {
            var payRuleSet = await _dbContext.PayRuleSets
                .Include(prs => prs.PayDayRules)
                    .ThenInclude(pdr => pdr.PayTierRules)
                .FirstOrDefaultAsync(prs => prs.Id == id);

            if (payRuleSet == null)
            {
                return new OperationDataResult<PayRuleSetModel>(
                    false,
                    "Pay rule set not found");
            }

            var model = new PayRuleSetModel
            {
                Id = payRuleSet.Id,
                Name = payRuleSet.Name,
                Description = payRuleSet.Description,
                PayDayRules = payRuleSet.PayDayRules?.Select(pdr => new PayDayRuleModel
                {
                    Id = pdr.Id,
                    DayCode = pdr.DayCode,
                    PayTierRules = pdr.PayTierRules?.Select(ptr => new PayTierRuleModel
                    {
                        Id = ptr.Id,
                        Order = ptr.Order,
                        UpToSeconds = ptr.UpToSeconds,
                        PayCode = ptr.PayCode,
                        Description = ptr.Description
                    }).ToList()
                }).ToList()
            };

            return new OperationDataResult<PayRuleSetModel>(true, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error reading pay rule set {id}");
            return new OperationDataResult<PayRuleSetModel>(
                false,
                $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Create(PayRuleSetCreateModel model)
    {
        try
        {
            var payRuleSet = new PayRuleSet
            {
                Name = model.Name,
                Description = model.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                WorkflowState = Constants.WorkflowStates.Created
            };

            await payRuleSet.Create(_dbContext);

            return new OperationResult(true, "Pay rule set created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pay rule set");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Update(int id, PayRuleSetUpdateModel model)
    {
        try
        {
            var payRuleSet = await _dbContext.PayRuleSets
                .FirstOrDefaultAsync(prs => prs.Id == id);

            if (payRuleSet == null)
            {
                return new OperationResult(false, "Pay rule set not found");
            }

            payRuleSet.Name = model.Name;
            payRuleSet.Description = model.Description;
            payRuleSet.UpdatedAt = DateTime.UtcNow;

            await payRuleSet.Update(_dbContext);

            return new OperationResult(true, "Pay rule set updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating pay rule set {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var payRuleSet = await _dbContext.PayRuleSets
                .FirstOrDefaultAsync(prs => prs.Id == id);

            if (payRuleSet == null)
            {
                return new OperationResult(false, "Pay rule set not found");
            }

            await payRuleSet.Delete(_dbContext);

            return new OperationResult(true, "Pay rule set deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting pay rule set {id}");
            return new OperationResult(false, $"Error: {ex.Message}");
        }
    }
}
```

---

## 7. Integration Tests

### Example: PayRuleSetsControllerTests

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services;

namespace TimePlanning.Pn.Test
{
    [TestFixture]
    public class PayRuleSetsServiceTests : TestBaseSetup
    {
        private PayRuleSetsService _service;

        [SetUp]
        public async Task Setup()
        {
            await base.SetUp();
            _service = new PayRuleSetsService(
                TimePlanningPnDbContext,
                Substitute.For<ILogger<PayRuleSetsService>>());
        }

        [Test]
        public async Task Create_ValidModel_CreatesPayRuleSet()
        {
            // Arrange
            var model = new PayRuleSetCreateModel
            {
                Name = "Test Rule Set",
                Description = "Test description"
            };

            // Act
            var result = await _service.Create(model);

            // Assert
            Assert.That(result.Success, Is.True);
            var created = await TimePlanningPnDbContext.PayRuleSets
                .FirstOrDefaultAsync(prs => prs.Name == "Test Rule Set");
            Assert.That(created, Is.Not.Null);
            Assert.That(created.Description, Is.EqualTo("Test description"));
        }

        [Test]
        public async Task Read_ExistingId_ReturnsPayRuleSet()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Test Rule Set",
                Description = "Test description"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            // Act
            var result = await _service.Read(payRuleSet.Id);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Model.Name, Is.EqualTo("Test Rule Set"));
        }

        [Test]
        public async Task Update_ExistingId_UpdatesPayRuleSet()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Original Name",
                Description = "Original description"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            var updateModel = new PayRuleSetUpdateModel
            {
                Name = "Updated Name",
                Description = "Updated description"
            };

            // Act
            var result = await _service.Update(payRuleSet.Id, updateModel);

            // Assert
            Assert.That(result.Success, Is.True);
            var updated = await TimePlanningPnDbContext.PayRuleSets
                .FirstOrDefaultAsync(prs => prs.Id == payRuleSet.Id);
            Assert.That(updated.Name, Is.EqualTo("Updated Name"));
            Assert.That(updated.Description, Is.EqualTo("Updated description"));
        }

        [Test]
        public async Task Delete_ExistingId_SoftDeletesPayRuleSet()
        {
            // Arrange
            var payRuleSet = new PayRuleSet
            {
                Name = "Test Rule Set"
            };
            await payRuleSet.Create(TimePlanningPnDbContext);

            // Act
            var result = await _service.Delete(payRuleSet.Id);

            // Assert
            Assert.That(result.Success, Is.True);
            var deleted = await TimePlanningPnDbContext.PayRuleSets
                .FirstOrDefaultAsync(prs => prs.Id == payRuleSet.Id);
            Assert.That(deleted.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed));
        }
    }
}
```

---

## Implementation Order

1. **Phase 1: Break Policy** (2-3 hours)
   - Implement `ApplyBreakPolicy()` method
   - Add unit tests
   - Verify backward compatibility

2. **Phase 2: Pay Line Generation** (3-4 hours)
   - Implement `GeneratePayLines()` method
   - Add unit tests for tier splitting
   - Test Sunday 14h → 11h + 3h scenario

3. **Phase 3: Day Type & Time Band Resolution** (2-3 hours)
   - Implement `ResolvePayCodesByTimeBand()` method
   - Add tests for time band overlaps
   - Test Sunday late night shifts

4. **Phase 4: Overtime Calculation** (4-5 hours)
   - Implement `CalculateOvertime()` method
   - Implement allocation strategies
   - Add tests for weekly, bi-weekly, monthly periods

5. **Phase 5: Orchestration** (2-3 hours)
   - Implement `RecalculateAndPersistAsync()` method
   - Add integration tests with database
   - Verify transactional behavior

6. **Phase 6: API Endpoints** (8-10 hours)
   - Implement PayRuleSetsController + Service
   - Implement PayDayTypeRulesController + Service
   - Implement PayTimeBandRulesController + Service
   - Implement WorkingTimeRuleSetsController + Service
   - Implement BreakPolicyController + Service
   - Add integration tests for all endpoints

7. **Phase 7: Integration** (3-4 hours)
   - Update TimePlanningWorkingHoursService
   - Update TimePlanningPlanningService
   - Add end-to-end tests

8. **Phase 8: Security & Documentation** (2-3 hours)
   - Run code_review
   - Run codeql_checker
   - Add inline documentation
   - Update IMPLEMENTATION_STATUS.md

**Total Estimated Effort**: 26-35 hours

---

## Testing Strategy

### Unit Tests
- Test each calculation method independently
- Use reflection to test private methods
- Mock database entities, not database itself
- Fast execution (< 100ms per test)

### Integration Tests
- Test with real database (Testcontainers)
- Verify persistence and transactional behavior
- Test API endpoints with full request/response cycle
- Use NSubstitute for external dependencies

### Backward Compatibility Tests
- Create "golden" tests that assert existing behavior unchanged
- Run existing test suite after each change
- Verify null/default configurations fall back to current behavior

### Performance Tests
- Benchmark batch calculation for 600-900 registrations
- Ensure no N+1 queries
- Profile memory usage

---

## Backward Compatibility Checklist

Before releasing each feature:
- [ ] Existing tests still pass unchanged
- [ ] New logic only activates when explicitly configured
- [ ] Default behavior matches current production behavior
- [ ] API changes are additive only (no breaking changes)
- [ ] Database migrations are reversible
- [ ] Feature can be disabled via configuration
- [ ] Performance regression tests pass

---

## Conclusion

This guide provides a comprehensive roadmap for implementing the advanced rule engine. Each feature is designed to be:
- **Backward compatible**: Existing behavior preserved by default
- **Incremental**: Can be implemented and tested independently
- **Well-tested**: Full unit and integration test coverage
- **Documented**: Clear examples and patterns

Follow the implementation order, ensure tests pass after each step, and maintain backward compatibility throughout.
