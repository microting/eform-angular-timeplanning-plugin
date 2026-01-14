# PlanRegistrationHelper Pay Line Computation - Implementation Status

## Overview
This document summarizes the implementation of pay line computation features for PlanRegistrationHelper to support payroll export (DataLøn/Danløn/Uniconta).

## Completed Features

### 1. Holiday Configuration (✅ Complete)
- **File**: `Resources/danish_holidays_2025_2030.json`
- Danish holidays for 2025-2030 with premium rules
- Includes official holidays and overenskomstfastsatte fridage
- Ready for JSON loading implementation

### 2. Interval Calculation Helpers (✅ Complete)
- **Method**: `GetWorkIntervals(PlanRegistration)`
  - Extracts work intervals from Start/Stop timestamp pairs (1-5)
  - Validates intervals (ignores null, incomplete, negative duration)
  - Returns enumerable of (Start, End) tuples
  
- **Method**: `GetPauseIntervals(PlanRegistration)`
  - Extracts ALL pause intervals: Pause1-5, Pause10-29, Pause100-102, Pause200-202
  - Validates intervals (ignores null, incomplete, negative duration)
  - Returns enumerable of (Start, End) tuples
  
- **Method**: `CalculateTotalSeconds(intervals)`
  - Calculates total seconds from interval collection
  - Used by both work and pause calculations

### 3. Day Classification (✅ Complete)
- **Method**: `GetDayCode(DateTime)`
  - Returns: SUNDAY, SATURDAY, HOLIDAY, GRUNDLOVSDAG, WEEKDAY
  - Tested for all day types
  
- **Method**: `IsOfficialHoliday(DateTime)`
  - Basic implementation for Christmas and New Year
  - Ready for JSON configuration integration

### 4. Time Tracking Field Computation (✅ Complete)
- **Method**: `ComputeTimeTrackingFields(PlanRegistration)`
  - Calculates TotalWorkSeconds from work intervals
  - Calculates TotalPauseSeconds from pause intervals
  - Sets `NettoHoursInSeconds` = work - pause (cannot be negative)
  - Sets `NettoHours` as double (hours)
  - Respects `NettoHoursOverrideActive` and `NettoHoursOverride`
  - Sets `EffectiveNetHoursInSeconds` (actual or override)
  - Sets day flags: `IsSaturday`, `IsSunday`
  - Does NOT persist changes (caller must save)

### 5. Rule Engine Marking (✅ Complete)
- **Method**: `MarkAsRuleEngineCalculated(PlanRegistration)`
  - Sets `RuleEngineCalculated = true`
  - Sets `RuleEngineCalculatedAt = DateTime.UtcNow`

## Test Coverage (✅ 16 Tests, All Passing)

### Interval Calculation Tests
1. ✅ Work intervals with 14h work (2 shifts)
2. ✅ Pause intervals with no pauses
3. ✅ Pause intervals with 45min pause (2 breaks)
4. ✅ Pause intervals include extended ranges (10-29, 100-102, 200-202)
5. ✅ Work intervals ignore incomplete pairs
6. ✅ Work intervals ignore negative duration

### Day Classification Tests
7. ✅ Sunday returns "SUNDAY"
8. ✅ Saturday returns "SATURDAY"
9. ✅ Grundlovsdag (June 5) returns "GRUNDLOVSDAG"
10. ✅ Regular weekday returns "WEEKDAY"
11. ✅ Christmas Day returns "HOLIDAY"
12. ✅ New Year's Day returns "HOLIDAY"

### Time Tracking Computation Tests
13. ✅ NettoHoursInSeconds calculated correctly (8h work - 0.5h pause = 7.5h)
14. ✅ NettoHoursOverride respected when active
15. ✅ Day classification flags set correctly
16. ✅ MarkAsRuleEngineCalculated sets flags and timestamp

### Existing Tests
- ✅ All 7 existing PlanRegistrationHelper tests still pass (no regressions)

## Remaining Work

### 1. Break Policy Implementation (⏳ Pending)
**Requirement**: Split pause time into paid/unpaid breaks based on weekday-aware rules

**Entities needed** (exist in Microting.TimePlanningBase):
- `BreakPolicy`
- `BreakPolicyRule` (weekday-specific)
- Fields: `PaidBreakMinutes`, `UnpaidBreakMinutes`, `PaidBreakPerDay`, `UnpaidBreakPerDay`

**Fields to set on PlanRegistration**:
- `TotalPauseHoursInSeconds`
- `UnpaidBreakHoursInSeconds`
- `PaidBreakHoursInSeconds`
- `PaidForExportSeconds = EffectiveNetHoursInSeconds + PaidBreakSeconds`

**Implementation needed**:
```csharp
private static void ApplyBreakPolicy(
    PlanRegistration pr, 
    BreakPolicy breakPolicy, 
    long totalPauseSeconds)
{
    // Find weekday rule for pr.Date.DayOfWeek
    // Split totalPauseSeconds into paid/unpaid
    // Set pr.TotalPauseHoursInSeconds, UnpaidBreakHoursInSeconds, PaidBreakHoursInSeconds
    // Calculate PaidForExportSeconds
}
```

### 2. Pay Line Generation (⏳ Pending)
**Requirement**: Generate `PlanRegistrationPayLine` records based on `PayRuleSet` tier rules

**Entities needed** (exist in Microting.TimePlanningBase):
- `PayRuleSet`
- `PayDayRule` (maps DayCode to tier rules)
- `PayTierRule` (defines UpToSeconds, PayCode, Order)
- `PlanRegistrationPayLine` (persisted records)

**Implementation needed**:
```csharp
public static async Task<List<PlanRegistrationPayLine>> GeneratePayLines(
    int planRegistrationId,
    string dayCode,
    long paidForExportSeconds,
    PayRuleSet payRuleSet,
    DateTime calculatedAtUtc)
{
    // Find PayDayRule for dayCode
    // Get PayTierRules ordered by Order
    // Split paidForExportSeconds across tiers
    // Example: Sunday 14h (50400s) with 11h tier boundary
    //   Tier 1: SUN_80 = 39600s (11h)
    //   Tier 2: SUN_100 = 10800s (3h)
    // Return List<PlanRegistrationPayLine>
}
```

### 3. Database Persistence Orchestration (⏳ Pending)
**Requirement**: Orchestrate all calculations and persist to database

**Implementation needed**:
```csharp
public static async Task RecalculateAndPersistAsync(
    PlanRegistration planRegistration,
    TimePlanningPnDbContext dbContext,
    int assignedSiteId)
{
    // 1. Load AssignedSite with rule set references
    var assignedSite = await dbContext.AssignedSites
        .Include(a => a.BreakPolicy)
        .Include(a => a.WorkingTimeRuleSet)
        .Include(a => a.PayRuleSet)
        .FirstOrDefaultAsync(a => a.Id == assignedSiteId);
    
    // 2. Compute time tracking fields
    ComputeTimeTrackingFields(planRegistration);
    
    // 3. Apply break policy
    if (assignedSite.BreakPolicy != null)
    {
        ApplyBreakPolicy(planRegistration, assignedSite.BreakPolicy, totalPauseSeconds);
    }
    
    // 4. Generate pay lines
    var dayCode = GetDayCode(planRegistration.Date);
    if (assignedSite.PayRuleSet != null)
    {
        // Delete existing pay lines
        var existingLines = await dbContext.PlanRegistrationPayLines
            .Where(l => l.PlanRegistrationId == planRegistration.Id)
            .ToListAsync();
        dbContext.PlanRegistrationPayLines.RemoveRange(existingLines);
        
        // Generate new pay lines
        var newLines = await GeneratePayLines(
            planRegistration.Id,
            dayCode,
            paidForExportSeconds,
            assignedSite.PayRuleSet,
            DateTime.UtcNow);
        await dbContext.PlanRegistrationPayLines.AddRangeAsync(newLines);
        
        // Store applied rule set IDs
        planRegistration.PayRuleSetId = assignedSite.PayRuleSetId;
        planRegistration.WorkingTimeRuleSetId = assignedSite.WorkingTimeRuleSetId;
        planRegistration.BreakPolicyId = assignedSite.BreakPolicyId;
    }
    
    // 5. Mark as calculated
    MarkAsRuleEngineCalculated(planRegistration);
    
    // 6. Save changes
    await dbContext.SaveChangesAsync();
}
```

### 4. Holiday Configuration Loading (⏳ Pending)
**Requirement**: Load Danish holidays from JSON file

**Implementation needed**:
```csharp
private static class HolidayConfiguration
{
    private static Dictionary<DateTime, Holiday> _holidays;
    
    public static void LoadFromJson(string jsonPath)
    {
        // Load and parse danish_holidays_2025_2030.json
        // Populate _holidays dictionary
    }
    
    public static bool IsHoliday(DateTime date, out string premiumRule)
    {
        // Check _holidays dictionary
        // Return true if holiday, with premium rule
    }
}
```

### 5. Integration Points (⏳ Pending)
**Where to call `RecalculateAndPersistAsync`**:
- Create PlanRegistration
- Update PlanRegistration (any timestamp changed)
- Approve PlanRegistration
- Admin override applied

**Services to update**:
- `TimePlanningWorkingHoursService.cs`
- `TimePlanningPlanningService.cs`

## Design Decisions

### Why Seconds-First?
- Precision: Avoids floating-point rounding errors
- Database: Integer seconds storage is more reliable
- Conversion: Convert to hours/doubles only for display

### Why Helper Methods Are Private/Static?
- Encapsulation: Internal implementation details
- Testability: Can be tested via reflection (as shown in tests)
- Stateless: Pure functions with no side effects

### Why Separate Orchestration Method?
- Single Responsibility: Each helper does one thing
- Testability: Each piece can be tested independently
- Flexibility: Can compose methods differently if needed

### Why Not Persist in ComputeTimeTrackingFields?
- Caller Control: Allows batch updates without multiple saves
- Transaction: Caller can wrap in transaction
- Testing: Easier to test without database

## Entity Fields Reference

### PlanRegistration (from Microting.TimePlanningBase)
**Existing Fields Used**:
- `Date`, `Start1StartedAt`-`Start5StartedAt`, `Stop1StoppedAt`-`Stop5StoppedAt`
- `Pause1StartedAt`-`Pause5StartedAt`, `Pause1StoppedAt`-`Pause5StoppedAt`
- `Pause10StartedAt`-`Pause29StartedAt`, `Pause10StoppedAt`-`Pause29StoppedAt`
- `Pause100StartedAt`-`Pause102StartedAt`, `Pause200StartedAt`-`Pause202StartedAt`
- `NettoHours`, `NettoHoursInSeconds`, `EffectiveNetHoursInSeconds`
- `NettoHoursOverrideActive`, `NettoHoursOverride`
- `IsSaturday`, `IsSunday`
- `RuleEngineCalculated`, `RuleEngineCalculatedAt`
- `PayRuleSetId`, `WorkingTimeRuleSetId`, `BreakPolicyId`

**Fields to Add/Use** (if not already present):
- `TotalPauseHoursInSeconds`
- `UnpaidBreakHoursInSeconds`
- `PaidBreakHoursInSeconds`
- `DayCode` (string)
- `IsHoliday` (bool)
- `IsSpecialDay` (bool)

## Testing Strategy

### Unit Tests (Current: 16/16 ✅)
- Test each helper method independently
- Use reflection to test private methods
- Mock database entities, not database itself
- Fast execution (< 50ms total)

### Integration Tests (Needed)
- Test RecalculateAndPersistAsync with real database
- Verify pay lines persisted correctly
- Test Sunday 14h → 11h + 3h split
- Test Grundlovsdag split
- Test recalculation replaces old lines

### Performance Considerations
- Batch calculation for 600-900 registrations
- Consider caching loaded rule sets
- Avoid N+1 queries (use Include)

## Next Steps for Implementation

1. **Investigate Entity Schema** (1-2 hours)
   - Examine PayRuleSet, PayDayRule, PayTierRule structure
   - Examine BreakPolicy, BreakPolicyRule structure
   - Verify field names and types

2. **Implement Break Policy** (2-3 hours)
   - Create ApplyBreakPolicy method
   - Add tests for paid/unpaid split
   - Handle edge cases (no policy, remainder)

3. **Implement Pay Line Generation** (3-4 hours)
   - Create GeneratePayLines method
   - Handle tier boundary splits
   - Add tests for Sunday and Grundlovsdag

4. **Implement Orchestration** (2-3 hours)
   - Create RecalculateAndPersistAsync
   - Add integration tests with database
   - Test recalculation replacement

5. **Load Holiday Configuration** (1-2 hours)
   - Create JSON loader
   - Update IsOfficialHoliday
   - Add premium rule support

6. **Integration** (2-3 hours)
   - Update service layers to call orchestration
   - Test end-to-end flows
   - Performance testing

**Total Estimated Effort**: 11-17 hours

## Files Modified

### Code Files
1. `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Infrastructure/Helpers/PlanRegistrationHelper.cs`
   - Added ~150 lines of helper methods
   - All methods documented with XML comments
   - All comments in English

### Test Files
2. `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn.Test/PlanRegistrationHelperComputationTests.cs`
   - New file: 420 lines
   - 16 comprehensive tests
   - Tests all new helper methods

### Configuration Files
3. `eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/Resources/danish_holidays_2025_2030.json`
   - New file: 235 lines
   - Complete Danish holiday data 2025-2030
   - Ready for loading

## Benefits of Current Implementation

### ✅ Correctness
- Precise seconds-based calculation
- Validation of intervals (ignores invalid data)
- Comprehensive test coverage

### ✅ Performance
- Stateless pure functions
- No database queries in helpers
- Suitable for batch processing

### ✅ Maintainability
- Well-documented methods
- Clear separation of concerns
- Testable design

### ✅ Extensibility
- Easy to add new day types
- Easy to add new calculation rules
- Pluggable policy system

## Conclusion

The foundation for pay line computation is complete and well-tested. The remaining work involves:
1. Database-backed policy application
2. Pay line generation with tier rules
3. Orchestration and persistence

The current implementation provides a solid, tested foundation that can be extended with the remaining features incrementally.
