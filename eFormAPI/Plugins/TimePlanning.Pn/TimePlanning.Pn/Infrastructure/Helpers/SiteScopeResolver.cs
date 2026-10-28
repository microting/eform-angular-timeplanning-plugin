/*
The MIT License (MIT)
Copyright (c) 2007 - 2021 Microting A/S
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#nullable enable
namespace TimePlanning.Pn.Infrastructure.Helpers;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;

/// <summary>
/// The set of AssignedSites a signed-in caller may see.
/// </summary>
/// <remarks>
/// Built only through the three factories, so every call site reads as its
/// outcome rather than as four positional arguments. <see cref="OwnSiteId"/> is
/// kept separate from <see cref="RestrictToOwnSite"/> so a worker whose site
/// carries no MicrotingUid still reads as restricted instead of unrestricted.
/// </remarks>
public sealed class SiteScope
{
    private SiteScope(
        List<AssignedSite> assignedSites, bool restrictToOwnSite, int? ownSiteId, string? errorKey)
    {
        AssignedSites = assignedSites;
        RestrictToOwnSite = restrictToOwnSite;
        OwnSiteId = ownSiteId;
        ErrorKey = errorKey;
    }

    /// <summary>The unnarrowed pool. Private on purpose: reading it instead of
    /// <see cref="ScopedSites"/> silently skips the own-site restriction, which
    /// is how a restricted worker ends up seeing the whole board.</summary>
    private List<AssignedSite> AssignedSites { get; }

    public bool RestrictToOwnSite { get; }

    public int? OwnSiteId { get; }

    /// <summary>Localization key of the failure, or null when the scope resolved.
    /// Callers must check this before reading anything else.</summary>
    public string? ErrorKey { get; }

    /// <summary>The caller could not be resolved; no scope exists.</summary>
    public static SiteScope Failed(string errorKey) => new([], false, null, errorKey);

    /// <summary>These sites, with no further narrowing — an admin (every site)
    /// or a manager (the sites of the tags they manage, plus their own).</summary>
    public static SiteScope Unrestricted(List<AssignedSite> assignedSites) =>
        new(assignedSites, false, null, null);

    /// <summary>A plain worker: only their own site, named by
    /// <paramref name="ownSiteId"/>.</summary>
    public static SiteScope OwnSiteOnly(List<AssignedSite> assignedSites, int? ownSiteId) =>
        new(assignedSites, true, ownSiteId, null);

    /// <summary>The AssignedSites this caller may see, with the own-site
    /// restriction applied. A restricted worker whose site carries no
    /// MicrotingUid narrows to nothing rather than to everything: showing too
    /// little is a far cheaper failure than showing the whole organisation.</summary>
    public List<AssignedSite> ScopedSites =>
        RestrictToOwnSite
            ? AssignedSites.Where(x => x.SiteId == OwnSiteId).ToList()
            : AssignedSites;

    /// <summary>Narrows a list of site MicrotingUids to this scope, preserving
    /// the caller's order. An unrestricted scope whose AssignedSites cover the
    /// input returns it unchanged, which is what keeps an admin's output
    /// byte-identical to an unscoped query.</summary>
    public List<int> Narrow(List<int> siteIds)
    {
        if (RestrictToOwnSite)
        {
            // Null OwnSiteId narrows to nothing — see ScopedSites.
            return siteIds.Where(x => x == OwnSiteId).ToList();
        }

        var allowed = AssignedSites.Select(x => x.SiteId).ToHashSet();
        return siteIds.Where(allowed.Contains).ToList();
    }
}

/// <summary>
/// Resolves which sites the signed-in caller may see on the planning board.
/// </summary>
/// <remarks>
/// Lives here, rather than on either service, because BOTH the planning board
/// and the all-workers export must apply it and must apply the SAME one: the
/// export dialog's worker count is only meaningful if it cannot exceed what the
/// page shows and what the export produces, and two copies of this non-trivial
/// rule would drift apart.
/// </remarks>
public static class SiteScopeResolver
{
    /// <summary>
    /// The external-login provider my.microting.com writes when a partner
    /// advisor is federated into this tenant as a credential-less "delegated
    /// principal".
    ///
    /// The source of truth is <c>ExternalLoginProviders.MyMicrotingDelegated</c>
    /// in eFormAPI.Web. This plugin references Microting.EformAngularFrontendBase,
    /// not the host web project, so the literal has to be repeated here; the two
    /// copies must stay byte-identical, and
    /// <c>SiteScopeResolverTests.TheDelegatedLoginProviderLiteralIsPinned</c>
    /// goes red if this one drifts.
    ///
    /// static readonly rather than const, deliberately, for the same reason the
    /// host gives: a const is folded into every referencing assembly at compile
    /// time — including the test that pins it, which would then silently agree
    /// with whatever the literal had been changed to.
    /// </summary>
    public static readonly string DelegatedLoginProvider = "my.microting.com/delegated";

    /// <param name="assignedSites">Every non-removed AssignedSite — the pool to
    /// narrow. The manager branch narrows this list; the restricted branch
    /// reports its site instead, because the callers apply it differently.</param>
    public static async Task<SiteScope> ResolveForCurrentUserAsync(
        List<AssignedSite> assignedSites,
        TimePlanningPnDbContext dbContext,
        MicrotingDbContext sdkDbContext,
        BaseDbContext baseDbContext,
        IUserService userService)
    {
        var currentUserAsync = await userService.GetCurrentUserAsync();
        if (currentUserAsync == null)
        {
            return SiteScope.Failed("UserNotFound");
        }

        var currentUser = baseDbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Single(x => x.Id == currentUserAsync.Id);

        var isAdmin = currentUser.UserRoles
            .Any(x => x.Role.Name == "admin");
        if (!isAdmin)
        {
            var userSecurityGroups = baseDbContext.SecurityGroupUsers
                .Include(x => x.SecurityGroup)
                .Where(x => x.EformUserId == currentUser.Id)
                .ToList();
            var eFormAdminsGroup = userSecurityGroups
                .Any(x => x.SecurityGroup.Name == "eForm admins");
            isAdmin = eFormAdminsGroup;
            if (!isAdmin)
            {
                var isEformUsersGroup = userSecurityGroups
                    .Any(x => x.SecurityGroup.Name == "eForm users");
                var isKunTidGroup = userSecurityGroups
                    .Any(x => x.SecurityGroup.Name == "Kun tid");
                if (isEformUsersGroup && !isKunTidGroup)
                {
                    // Fallback: when no user in the system is configured as a manager,
                    // grant "eForm users" members the admin-for-visibility view on this
                    // endpoint so the planning dashboard isn't empty in that degenerate
                    // state. Users also in "Kun tid" (time-registration device users with
                    // WebAccess) are explicitly excluded and stay restricted to own site.
                    var anyManagerExists = await dbContext.AssignedSites
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .AnyAsync(x => x.IsManager)
                        .ConfigureAwait(false);
                    if (!anyManagerExists)
                    {
                        isAdmin = true;
                    }
                }
            }
        }

        if (isAdmin)
        {
            return SiteScope.Unrestricted(assignedSites);
        }

        // A delegated advisor is a LEADER here, and this is the last point at
        // which that can still be decided: everything below resolves the caller
        // to an SDK Worker by email and fails when there is none.
        //
        // An advisor acts across the customer's WHOLE organisation on the
        // partner's behalf — that is the entire point of the delegation — and is
        // never employed at one of its sites, so they have no Worker row and no
        // own site to be narrowed to. Their email is synthetic
        // (delegated-<digest>@delegated.invalid), so the lookup below cannot ever
        // match: left to run, it returned ErrorWhileObtainingPlannings and sent
        // Sentry a message on EVERY request. Returning above it is what stops
        // both, so this check must stay ahead of it.
        //
        // The marker is the UserLogins row, exactly as the host's own
        // UserManagerLockoutExtensions.IsDelegatedPrincipalAsync decides it.
        // Deliberately NOT the email domain (spoofable, and merely a convention)
        // and NOT a null PasswordHash (plenty of ordinary accounts have none —
        // the host already had to fix that same over-broad assumption once).
        var isDelegatedPrincipal = await baseDbContext.UserLogins
            .AnyAsync(x => x.UserId == currentUser.Id
                           && x.LoginProvider == DelegatedLoginProvider)
            .ConfigureAwait(false);
        if (isDelegatedPrincipal)
        {
            return SiteScope.Unrestricted(assignedSites);
        }

        var worker = await sdkDbContext.Workers
            .Include(x => x.SiteWorkers)
            .ThenInclude(x => x.Site)
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .FirstOrDefaultAsync(x => x.Email == currentUser.Email);

        if (worker == null)
        {
            SentrySdk.CaptureMessage($"Worker with email {currentUser.Email} not found");
            return SiteScope.Failed("ErrorWhileObtainingPlannings");
        }

        // Deterministically resolve the active site (excludes removed
        // SiteWorker/Site rows). No active site -> same error path as a
        // missing worker (previously NRE'd on empty SiteWorkers).
        var site = worker!.ResolveActiveSite();
        if (site == null)
        {
            SentrySdk.CaptureMessage($"No active site for worker with email {currentUser.Email}");
            return SiteScope.Failed("ErrorWhileObtainingPlannings");
        }

        var assignedSite = assignedSites
            .FirstOrDefault(x => x.SiteId == site.MicrotingUid);
        if (assignedSite == null || !assignedSite.IsManager)
        {
            return SiteScope.OwnSiteOnly(assignedSites, site.MicrotingUid);
        }

        var assignedSiteTags = await dbContext.AssignedSiteManagingTags
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.AssignedSiteId == assignedSite.Id)
            .Select(x => x.TagId)
            .ToListAsync();
        var assignedSiteIdsWithTags = await sdkDbContext.SiteTags
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Where(x => x.TagId != null && assignedSiteTags.Contains(x.TagId.Value))
            .Where(x => x.Site.MicrotingUid != null)
            .Select(x => x.Site.MicrotingUid!.Value)
            .Distinct()
            .ToListAsync();
        var managedSites = assignedSites
            .Where(x => assignedSiteIdsWithTags.Contains(x.SiteId))
            .ToList();
        // Only re-add the manager's own AssignedSite if the manager-tag
        // filter dropped it. When the manager is in their own managed
        // tag (SiteTag joins the manager's own SiteId to that TagId),
        // it is already present and a blind Add() produced two rows
        // for the manager on the planning page.
        if (managedSites.All(x => x.Id != assignedSite.Id))
        {
            managedSites.Add(assignedSite);
        }

        return SiteScope.Unrestricted(managedSites);
    }
}
