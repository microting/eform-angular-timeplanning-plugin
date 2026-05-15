---

# Request History Admin Page & Handover Bug Fixes

**Date:** 2026-04-24

## Overview

Three changes to the TimePlanning plugin:
1. Admin-only web UI page showing all handover and absence request history
2. Bug fix: shift data not transferred when receiver has a message (e.g., vacation)
3. Bug fix: PlanHours not recalculated for sender/receiver after shift transfer

## 1. Web UI — Admin Request History Page

### Purpose
Admin/managers need a single overview page showing all handover and absence requests across all workers, with filtering capabilities.

### Layout
Single combined table with filters (Approach A — single table, no sub-tabs).

**Filters (horizontal bar):**
- Type: All / Handovers / Absences
- Status: All / Pending / Accepted / Rejected / Cancelled
- Worker: All Workers / [worker name] — matches if worker is on either side of the request (from OR to for handovers, requester for absences)
- Date range: From / To

**Table columns:**
- Type (badge: HANDOVER or ABSENCE)
- Date
- From (requester/sender name)
- To (receiver/approver name)
- Status (color-coded: Pending=yellow, Accepted=green, Rejected=red, Cancelled=grey)
- Requested (date/time)
- Responded (date/time or dash)
- Comment (truncated if long)

### Server-Side: New REST Endpoints

**GET /api/time-planning-pn/content-handover-requests/all**
- Query params: status (string), fromDate (ISO date), toDate (ISO date), sdkSiteId (int, matches FromSdkSitId OR ToSdkSitId)
- Returns: list of all ContentHandoverRequest records matching filters
- Auth: admin/manager role required

**GET /api/time-planning-pn/absence-requests/all**
- Query params: status (string), fromDate (ISO date), toDate (ISO date), sdkSiteId (int, matches RequestedBySdkSitId)
- Returns: list of all AbsenceRequest records matching filters
- Auth: admin/manager role required

### Angular Module

New lazy-loaded module at `modules/request-history/` following the `absence-requests` module pattern:
- `request-history.module.ts`
- `request-history.routing.ts`
- `components/request-history-page/` — main page component with table + filters
- `services/request-history.service.ts` — HTTP service calling the new endpoints

Menu entry under Time Planning navigation for admin/manager roles.

i18n: update all 23 language files with new translation keys.

## 2. Bug Fix — Shift Data Not Transferred When Receiver Has Message

### Problem
When accepting a shift handover, if the receiver has a message (e.g., "vacation") on that day, the shift data (PlannedStartOfShift, PlannedEndOfShift, PlannedBreakOfShift) is not transferred to the receiver's PlanRegistration. The accept succeeds (status changes to Accepted) but the plan is not updated. Works fine for days without a message.

### Design Decision
Both the message and shift data should coexist. Transferring a shift to someone on vacation is allowed — the vacation message stays, and the shift data is added alongside it.

### Fix
In `ContentHandoverService.AcceptAsync` and/or `MoveContent`/`MoveShift`:
- Ensure shift fields are copied regardless of whether the target PlanRegistration has a MessageId
- Do NOT clear the existing MessageId when writing shift data
- Root cause must be pinpointed in the actual copy logic

### Files
- `TimePlanning.Pn/Services/ContentHandoverService/ContentHandoverService.cs`

## 3. Bug Fix — PlanHours Not Recalculated After Transfer

### Problem
After a shift is transferred via handover, PlanHours are not correctly recalculated for either side:
- Sender's PlanHours should decrease (shift was removed)
- Receiver's PlanHours should increase (shift was added)
- Currently neither updates correctly

### Fix
In `AcceptAsync`, after `MoveContent` completes:
- Ensure `PlanRegistrationHelper` recalculates PlanHours for BOTH the sender's and receiver's PlanRegistration
- Verify that the recalculation uses the updated shift field values (after the move)

### Files
- `TimePlanning.Pn/Services/ContentHandoverService/ContentHandoverService.cs`
- Possibly `Microting.TimePlanningBase` if `PlanRegistrationHelper` needs changes

## Verification

1. **Web UI**: Open admin panel → Time Planning → Request History. Verify table shows all requests. Test all filters (type, status, worker, date range).
2. **Handover bug**: Transfer a shift to a worker who has vacation on that day. Verify shift data appears on receiver's plan alongside the vacation message.
3. **PlanHours bug**: Transfer a shift. Verify sender's PlanHours decrease and receiver's PlanHours increase correctly.

---
