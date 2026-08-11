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

    // ------------------------------------------------------------------
    // Seed messages: the new Id 13 "PregnancyLeave" entry, plus uniqueness
    // guards for future additions. (TimePlanningPluginSeed.SeedData upserts
    // this catalog into the Messages table by Name; the DB-side assertion
    // lives in WorkingHoursMessagePersistenceTests, which has a database.)
    // ------------------------------------------------------------------

    [Test]
    public void SeedMessages_Contain_PregnancyLeaveWithId13()
    {
        var seedMessages = new TimePlanningSeedMessages();

        var pregnancyLeave = seedMessages.Data
            .Where(x => x.Id == 13)
            .ToList();

        Assert.That(pregnancyLeave.Count, Is.EqualTo(1),
            "Exactly one seed message with Id 13");
        Assert.That(pregnancyLeave[0].Name, Is.EqualTo("PregnancyLeave"));
        Assert.That(pregnancyLeave[0].DaName, Is.EqualTo("Graviditetsbetinget fravær"));
    }

    [Test]
    public void SeedMessages_IdsAndNamesAreUnique()
    {
        var seedMessages = new TimePlanningSeedMessages();

        var ids = seedMessages.Data.Select(x => x.Id).ToList();
        Assert.That(ids, Is.Unique,
            "Seed message Ids must stay unique — SeedData upserts by Name, and the Excel " +
            "Total sheet emits one column per Id");

        var names = seedMessages.Data.Select(x => x.Name).ToList();
        Assert.That(names, Is.Unique,
            "Seed message Names must stay unique — TimePlanningPluginSeed matches existing " +
            "rows by Name, so a duplicate would upsert the wrong row");
    }
}
