# Break Policies Menu Integration Documentation

## Overview

This document describes the integration of Break Policies into the Time Planning plugin menu system, including menu seeding configuration and test updates.

## Implementation Summary

### Changes Made
1. **Backend**: Added menu entry to `EformTimePlanningPlugin.cs` (lines 551-608)
2. **Frontend**: Updated Cypress tests to use menu navigation instead of direct URL
3. **Documentation**: Created comprehensive integration guide

### Commit
- **Hash**: 9b4e3a2
- **Title**: Add Break Policies menu entry and update Cypress tests to use menu navigation
- **Files**: 2 modified (+78/-21 lines)

## Menu Entry Configuration

### Location
`eFormAPI/Plugins/TimePlanning.Pn/TimePlanning.Pn/EformTimePlanningPlugin.cs`

### Position in Menu
Position 5 (after Timer, before future entries)

### Configuration Details

```csharp
new()
{
    Name = "Break Policies",
    E2EId = "time-planning-pn-break-policies",
    Link = "/plugins/time-planning-pn/break-policies",
    Type = MenuItemTypeEnum.Link,
    Position = 5,
    MenuTemplate = new()
    {
        Name = "Break Policies",
        E2EId = "time-planning-pn-break-policies",
        DefaultLink = "/plugins/time-planning-pn/break-policies",
        Permissions = [],
        Translations =
        [
            new()
            {
                LocaleName = LocaleNames.English,
                Name = "Break Policies",
                Language = LanguageNames.English
            },
            new()
            {
                LocaleName = LocaleNames.German,
                Name = "Pausenrichtlinien",
                Language = LanguageNames.German
            },
            new()
            {
                LocaleName = LocaleNames.Danish,
                Name = "Pausepolitikker",
                Language = LanguageNames.Danish
            }
        ]
    },
    Translations =
    [
        new()
        {
            LocaleName = LocaleNames.English,
            Name = "Break Policies",
            Language = LanguageNames.English
        },
        new()
        {
            LocaleName = LocaleNames.German,
            Name = "Pausenrichtlinien",
            Language = LanguageNames.German
        },
        new()
        {
            LocaleName = LocaleNames.Danish,
            Name = "Pausepolitikker",
            Language = LanguageNames.Danish
        }
    ]
}
```

## Translations

| Language | Translation | Notes |
|----------|-------------|-------|
| English  | Break Policies | Standard English term |
| German   | Pausenrichtlinien | Literal: "Pause guidelines/policies" |
| Danish   | Pausepolitikker | Direct translation of "Break policies" |

## Menu Structure

### Complete Time Planning Menu
1. **Working hours** / Timeregistrering (Position 1)
2. **Flex** (Position 2)
3. **Registration devices** / Registreringsenheder (Position 3)
4. **Dashboard** (Position 4)
5. **Break Policies** / Pausepolitikker (Position 5) ← NEW

### Menu Hierarchy
```
Time Planning / Timeregistrering
├── Working hours / Timeregistrering
├── Flex
├── Registration devices / Registreringsenheder
├── Dashboard
└── Break Policies / Pausepolitikker
```

## Cypress Test Updates

### File
`eform-client/cypress/e2e/plugins/time-planning-pn/o/break-policies.spec.cy.ts`

### Navigation Pattern

#### Before (Direct URL)
```typescript
cy.visit('http://localhost:4200/plugins/time-planning-pn/break-policies');
cy.wait(500);
```

#### After (Menu Navigation)
```typescript
const navigateToBreakPolicies = () => {
  cy.get('#spinner-animation').should('not.exist');
  cy.contains('Timeregistrering').click();
  cy.wait(500);
  cy.contains('Pausepolitikker').click();
  cy.wait(500);
};

// Usage in tests
navigateToBreakPolicies();
```

### Why Danish?
The test environment uses Danish locale by default, so menu items appear in Danish:
- "Time Planning" → "Timeregistrering"
- "Break Policies" → "Pausepolitikker"

### Test Scenarios Updated
All 7 test scenarios now use the navigation helper:
1. ✅ should navigate to break policies page
2. ✅ should display break policies list
3. ✅ should open create modal
4. ✅ should create new break policy
5. ✅ should edit break policy
6. ✅ should delete break policy
7. ✅ should validate required fields

## Benefits

### User Experience
- **Discoverability**: Users can find Break Policies through menu navigation
- **Consistency**: Same navigation pattern as other features
- **Multi-language**: Supports English, German, and Danish
- **Intuitive**: Grouped under Time Planning with related features

### Testing
- **Realistic**: Tests validate actual user navigation path
- **Menu Validation**: Ensures menu entry is properly configured
- **Integration**: Catches menu seeding issues early
- **Locale Testing**: Validates Danish locale works correctly

### Code Quality
- **DRY**: Navigation helper eliminates code duplication
- **Maintainable**: Single place to update navigation logic
- **Readable**: Clear test structure and intent
- **Reliable**: Spinner wait prevents race conditions

## Technical Details

### Menu Item Properties

| Property | Value | Purpose |
|----------|-------|---------|
| Name | "Break Policies" | Display name (English) |
| E2EId | "time-planning-pn-break-policies" | Test automation selector |
| Link | "/plugins/time-planning-pn/break-policies" | Angular route |
| Type | MenuItemTypeEnum.Link | Menu item type |
| Position | 5 | Display order |
| Permissions | [] | Empty = all users can access |

### MenuTemplate vs Translations

**MenuTemplate**:
- Defines the menu item template in the system
- Used for menu generation and management
- Contains DefaultLink and base configuration

**Translations**:
- Provides localized display names
- Used for rendering menu in different languages
- Must include all supported locales

Both are required for proper menu functionality.

## Testing Guidelines

### Navigation Helper
The `navigateToBreakPolicies()` helper function:
1. Waits for spinner to disappear (page loaded)
2. Clicks main menu ("Timeregistrering")
3. Waits for menu expansion
4. Clicks submenu ("Pausepolitikker")
5. Waits for page navigation

### Best Practices
- Always wait for spinner before interacting
- Use appropriate waits between clicks (500ms)
- Validate URL after navigation
- Test all scenarios with same navigation pattern

### Troubleshooting
If tests fail:
1. Check locale setting (should be Danish)
2. Verify menu entry was seeded (run activate-plugin first)
3. Check menu text matches locale ("Pausepolitikker" for Danish)
4. Ensure no timing issues (increase waits if needed)

## Future Considerations

### Additional Menu Entries
Other rule entities can follow this pattern:
- **PayRuleSet** (position 6)
- **PayDayTypeRule** (position 7)
- **PayTierRule** (position 8)
- **PayTimeBandRule** (position 9)

### Pattern Template
```csharp
new()
{
    Name = "[English Name]",
    E2EId = "time-planning-pn-[feature-name]",
    Link = "/plugins/time-planning-pn/[route]",
    Type = MenuItemTypeEnum.Link,
    Position = [number],
    MenuTemplate = new()
    {
        Name = "[English Name]",
        E2EId = "time-planning-pn-[feature-name]",
        DefaultLink = "/plugins/time-planning-pn/[route]",
        Permissions = [],
        Translations = [/* 3 languages */]
    },
    Translations = [/* 3 languages */]
}
```

### Test Pattern Template
```typescript
const navigateTo[Feature]s = () => {
  cy.get('#spinner-animation').should('not.exist');
  cy.contains('Timeregistrering').click();
  cy.wait(500);
  cy.contains('[Danish Translation]').click();
  cy.wait(500);
};
```

## Verification Checklist

### Menu Configuration ✅
- [x] Name set correctly
- [x] E2EId follows pattern
- [x] Link points to correct route
- [x] Type is Link
- [x] Position is correct
- [x] MenuTemplate configured
- [x] Permissions set (empty = all)
- [x] Translations for all 3 languages
- [x] Both MenuTemplate.Translations and Translations populated

### Tests ✅
- [x] Navigation helper created
- [x] All test scenarios updated
- [x] Danish menu items used
- [x] Spinner wait added
- [x] Appropriate waits between actions
- [x] Comments updated/removed
- [x] Code follows DRY principle

### Documentation ✅
- [x] Implementation documented
- [x] Benefits explained
- [x] Technical details provided
- [x] Future patterns described
- [x] Troubleshooting guide included

## Status

✅ **Implementation Complete**
✅ **Tests Updated and Passing**
✅ **Documentation Complete**
✅ **Ready for Production**

## Related Files

- `EformTimePlanningPlugin.cs` - Menu seeding configuration
- `break-policies.spec.cy.ts` - Cypress test navigation
- `break-policies.module.ts` - Angular module
- `break-policies.routing.ts` - Angular routing

## References

- Original Issue: #[issue-number]
- Implementation Guide: `BREAK_POLICY_COMPONENT_IMPLEMENTATION.md`
- PR: #[pr-number]
- Commit: 9b4e3a2
