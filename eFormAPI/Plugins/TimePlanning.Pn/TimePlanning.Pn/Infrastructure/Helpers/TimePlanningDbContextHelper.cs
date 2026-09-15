using System;
using Microsoft.EntityFrameworkCore;
using Microting.TimePlanningBase.Infrastructure.Data;
using TimePlanning.Pn.Infrastructure.Interceptors;
// NB: no Pomelo using. This repo uses the Microting.EntityFrameworkCore.MySql
// fork, and MariaDbServerVersion/ServerVersion come from
// Microsoft.EntityFrameworkCore -- which is why EformTimePlanningPlugin.cs
// needs no provider-specific using either. Adding a Pomelo PackageReference
// would introduce a second, conflicting provider.

namespace TimePlanning.Pn.Infrastructure.Helpers;

/// <summary>
/// Builds plugin DbContexts with the day-lock interceptor attached.
///
/// This no longer delegates to TimePlanningPnContextFactory: that factory
/// builds its DbContextOptionsBuilder in a method-local and exposes no hook, so
/// there is no way to attach an interceptor through it. The context's public
/// options constructor is the supported seam, and it needs no base-package change.
/// </summary>
public class TimePlanningDbContextHelper(string connectionString) : ITimePlanningDbContextHelper
{
    private string ConnectionString { get; } = connectionString;

    public TimePlanningPnDbContext GetDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimePlanningPnDbContext>();

        // Hardcoded version, exactly as TimePlanningPnContextFactory does.
        // ServerVersion.AutoDetect OPENS A CONNECTION and runs a version
        // query; this method is called once per assigned site on every
        // dashboard load, so AutoDetect here would add a round-trip per
        // worker per page view that the current code does not pay.
        optionsBuilder.UseMySql(
            ConnectionString,
            new MariaDbServerVersion(new Version(10, 5, 0)),
            mySqlOptionsAction: builder => { builder.EnableRetryOnFailure(); });

        optionsBuilder.AddInterceptors(ReconciledDayLockInterceptor.Instance);

        return new TimePlanningPnDbContext(optionsBuilder.Options);
    }
}

public interface ITimePlanningDbContextHelper
{
    TimePlanningPnDbContext GetDbContext();
}
