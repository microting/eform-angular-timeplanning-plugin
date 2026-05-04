-- Post-migration patch for the g1m variant shard.
--
-- The base seed (`420_eform-angular-time-planning-plugin.sql`) is an old EF
-- baseline dump that pre-dates the `UseOneMinuteIntervals` column on
-- `AssignedSites`. The column is added at runtime by base-package migration
-- `20250226060341_Adding3MoreShifts` (executed by `Database.Migrate()` at
-- plugin startup) with default value 0.
--
-- This patch flips the flag on for every active assigned site so the g1m
-- shard exercises the flag-on rendering / form / picker code paths. The
-- workflow runs this AFTER `Wait for app` (which gates on migrations being
-- complete) and BEFORE the matrix Playwright invocation.
--
-- Shifts 3-5 active flags are flipped on alongside `UseOneMinuteIntervals`
-- (FU-A pattern, mirrors b1m/c1m/d1m/e1m/f1m/h1m/i1m/j1m). The g1m spec
-- needs all 5 shifts visible in the workday-entity-dialog so its
-- multishift-shape `beforeEach` can fill shifts 1-5 ascending before
-- saving the comment under test.
UPDATE AssignedSites
SET UseOneMinuteIntervals = 1,
    ThirdShiftActive = 1,
    FourthShiftActive = 1,
    FifthShiftActive = 1
WHERE WorkflowState = 'created';
