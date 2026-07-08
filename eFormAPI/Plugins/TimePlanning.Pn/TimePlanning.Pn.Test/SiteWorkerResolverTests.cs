using System.Collections.Generic;
using Microting.eForm.Infrastructure.Constants;
using Microting.eForm.Infrastructure.Data.Entities;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for the non-deterministic "which site is this worker"
/// resolution. The historical pattern
///   <c>.Include(x =&gt; x.SiteWorkers).ThenInclude(x =&gt; x.Site)</c>
/// followed by <c>worker.SiteWorkers.First()</c> loaded the SiteWorkers/Site
/// navigation UNFILTERED and UNORDERED. A worker with more than one SiteWorker
/// row (site transfer, stale/removed SiteWorker) resolved to the WRONG or a
/// REMOVED site, misdirecting every downstream PlanRegistration read/write.
///
/// <see cref="SiteWorkerResolver"/> filters removed SiteWorker rows and removed
/// Sites in memory and orders deterministically by SiteWorker.Id. These tests
/// exercise the helper directly over Worker aggregates shaped exactly as EF
/// materializes them (the naive First() picks the wrong row; the helper does
/// not).
/// </summary>
[TestFixture]
public class SiteWorkerResolverTests
{
    private const string Removed = Constants.WorkflowStates.Removed;   // "removed"
    private const string Active = Constants.WorkflowStates.Created;    // non-removed

    private static Site Site(int id, int microtingUid, string workflowState = Active)
        => new() { Id = id, MicrotingUid = microtingUid, WorkflowState = workflowState };

    private static SiteWorker SiteWorker(int id, Site site, string workflowState = Active)
        => new() { Id = id, Site = site, SiteId = site.Id, WorkflowState = workflowState };

    private static Worker Worker(params SiteWorker[] siteWorkers)
        => new() { WorkflowState = Active, SiteWorkers = new List<SiteWorker>(siteWorkers) };

    [Test]
    public void ResolveActiveSiteWorker_SkipsRemovedSiteWorker_WhenItEnumeratesFirst()
    {
        // Removed row deliberately placed first in enumeration order (and with
        // the lower Id) so a naive First() would pick it.
        var removed = SiteWorker(1, Site(10, 111), Removed);
        var active = SiteWorker(2, Site(20, 222));
        var worker = Worker(removed, active);

        // Sanity: the naive pattern this fix replaces would pick the removed one.
        Assert.That(new List<SiteWorker>(worker.SiteWorkers)[0].WorkflowState, Is.EqualTo(Removed),
            "Guard: the removed SiteWorker must enumerate first so this test proves the fix");

        var resolved = worker.ResolveActiveSiteWorker();

        Assert.That(resolved, Is.SameAs(active));
        Assert.That(worker.ResolveActiveSdkSiteId(), Is.EqualTo(222));
    }

    [Test]
    public void ResolveActiveSiteWorker_SkipsSiteWorkerPointingAtRemovedSite()
    {
        // SiteWorker row is active, but its Site has been removed.
        var pointsAtRemovedSite = SiteWorker(1, Site(10, 111, Removed));
        var active = SiteWorker(2, Site(20, 222));
        var worker = Worker(pointsAtRemovedSite, active);

        var resolved = worker.ResolveActiveSiteWorker();

        Assert.That(resolved, Is.SameAs(active));
        Assert.That(worker.ResolveActiveSdkSiteId(), Is.EqualTo(222));
    }

    [Test]
    public void ResolveActiveSiteWorker_IsDeterministicByLowestId_RegardlessOfEnumerationOrder()
    {
        // Two active site workers; higher Id enumerates first. Deterministic
        // selection must still return the lowest Id.
        var higherIdFirst = SiteWorker(9, Site(90, 999));
        var lowerIdSecond = SiteWorker(3, Site(30, 333));
        var worker = Worker(higherIdFirst, lowerIdSecond);

        var resolved = worker.ResolveActiveSiteWorker();

        Assert.That(resolved, Is.SameAs(lowerIdSecond));
        Assert.That(worker.ResolveActiveSdkSiteId(), Is.EqualTo(333));
    }

    [Test]
    public void ResolveActiveSiteWorker_SingleActive_ReturnsIt()
    {
        var only = SiteWorker(1, Site(10, 111));
        var worker = Worker(only);

        Assert.That(worker.ResolveActiveSiteWorker(), Is.SameAs(only));
        Assert.That(worker.ResolveActiveSite(), Is.SameAs(only.Site));
        Assert.That(worker.ResolveActiveSdkSiteId(), Is.EqualTo(111));
    }

    [Test]
    public void ResolveActiveSiteWorker_AllRemoved_ReturnsNull()
    {
        var removed1 = SiteWorker(1, Site(10, 111), Removed);
        var removed2 = SiteWorker(2, Site(20, 222, Removed));
        var worker = Worker(removed1, removed2);

        Assert.That(worker.ResolveActiveSiteWorker(), Is.Null);
        Assert.That(worker.ResolveActiveSite(), Is.Null);
        // Callers use `?? 0` on this -> collapses to the "no site" sentinel.
        Assert.That(worker.ResolveActiveSdkSiteId(), Is.Null);
    }

    [Test]
    public void ResolveActiveSiteWorker_NoSiteWorkers_ReturnsNull()
    {
        var worker = Worker();

        Assert.That(worker.ResolveActiveSiteWorker(), Is.Null);
        Assert.That(worker.ResolveActiveSdkSiteId(), Is.Null);
    }

    [Test]
    public void ResolveActiveSiteWorker_NullWorker_ReturnsNull()
    {
        Worker nullWorker = null;

        Assert.That(nullWorker.ResolveActiveSiteWorker(), Is.Null);
        Assert.That(nullWorker.ResolveActiveSite(), Is.Null);
        Assert.That(nullWorker.ResolveActiveSdkSiteId(), Is.Null);
    }
}
