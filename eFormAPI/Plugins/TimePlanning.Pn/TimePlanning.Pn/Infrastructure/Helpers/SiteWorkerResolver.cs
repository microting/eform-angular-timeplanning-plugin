using System.Linq;
using Microting.eForm.Infrastructure.Constants;
using Microting.eForm.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Single source of truth for resolving "which site is this personal-mode
/// worker" from an already-loaded <see cref="Worker"/> aggregate.
///
/// The historical pattern across the plugin was:
///   <c>.Include(x =&gt; x.SiteWorkers).ThenInclude(x =&gt; x.Site)</c>
///   (which only filters the outer <see cref="Worker.WorkflowState"/>)
/// followed by <c>worker.SiteWorkers.First()</c>. The included
/// SiteWorkers/Site collection is loaded UNFILTERED and UNORDERED, so a worker
/// carrying more than one SiteWorker row (site transfer, stale/removed
/// SiteWorker) resolved NON-DETERMINISTICALLY — potentially to a removed site
/// or a removed SiteWorker — silently misdirecting every downstream
/// PlanRegistration read/write keyed on the resolved SdkSitId.
///
/// These helpers operate IN MEMORY over the already-materialized navigation
/// collection (they do not issue SQL). They exclude removed SiteWorker rows and
/// removed Sites, then order deterministically by <see cref="SiteWorker.Id"/>
/// so the same worker always resolves to the same active site.
/// </summary>
public static class SiteWorkerResolver
{
    /// <summary>
    /// Deterministically selects the active <see cref="SiteWorker"/> for a
    /// loaded worker: excludes removed SiteWorker rows and removed Sites,
    /// ordered by <see cref="SiteWorker.Id"/> (stable), first match.
    /// Returns null when the worker is null, has no loaded SiteWorkers, or has
    /// no active (non-removed) SiteWorker pointing at an active Site.
    /// </summary>
    public static SiteWorker? ResolveActiveSiteWorker(this Worker? worker)
    {
        if (worker?.SiteWorkers == null)
        {
            return null;
        }

        return worker.SiteWorkers
            .Where(sw => sw.WorkflowState != Constants.WorkflowStates.Removed
                         && sw.Site != null
                         && sw.Site.WorkflowState != Constants.WorkflowStates.Removed)
            .OrderBy(sw => sw.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// The active <see cref="Site"/> for a loaded worker, or null when none
    /// resolves. See <see cref="ResolveActiveSiteWorker"/>.
    /// </summary>
    public static Site? ResolveActiveSite(this Worker? worker)
        => worker.ResolveActiveSiteWorker()?.Site;

    /// <summary>
    /// The SDK site id (<see cref="Site.MicrotingUid"/>) of the active site for
    /// a loaded worker, or null when no active site resolves or its MicrotingUid
    /// is null. Callers that previously did <c>... .Site.MicrotingUid ?? 0</c>
    /// should use <c>ResolveActiveSdkSiteId() ?? 0</c>.
    /// </summary>
    public static int? ResolveActiveSdkSiteId(this Worker? worker)
        => worker.ResolveActiveSite()?.MicrotingUid;
}
