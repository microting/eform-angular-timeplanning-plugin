using System.Linq;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Data.Seed.Data;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class ConfigurationSeedDataTests
{
    [Test]
    public void SeedData_Contains_PauseIdSelfHealEnabled()
    {
        var seedData = new TimePlanningConfigurationSeedData();

        Assert.That(
            seedData.Data.Any(x =>
                x.Name == "TimePlanningBaseSettings:PauseIdSelfHealEnabled"
                && x.Value == "true"),
            Is.True);
    }

    [Test]
    public void SeedData_Contains_DaysBackInTimeAllowedEditingEnabled()
    {
        var seedData = new TimePlanningConfigurationSeedData();

        Assert.That(
            seedData.Data.Any(x =>
                x.Name == "TimePlanningBaseSettings:DaysBackInTimeAllowedEditingEnabled"
                && x.Value == "0"),
            Is.True);
    }
}
