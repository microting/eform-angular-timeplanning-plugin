using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Factories;

namespace TimePlanning.Pn.Infrastructure.Helpers;

public class TimePlanningDbContextHelper(string connectionString) : ITimePlanningDbContextHelper
{
    private string ConnectionString { get;} = connectionString;

    public TimePlanningPnDbContext GetDbContext()
    {
        TimePlanningPnContextFactory contextFactory = new TimePlanningPnContextFactory();

        return contextFactory.CreateDbContext([ConnectionString]);
    }
}

public interface ITimePlanningDbContextHelper
{
    TimePlanningPnDbContext GetDbContext();
}