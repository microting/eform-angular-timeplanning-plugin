using System;
using System.Threading.Tasks;
using BackendConfiguration.Pn.Integration.Test;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class MessageCheckTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
    }

    [Test]
    public async Task CheckMessages()
    {
        var messages = await TimePlanningPnDbContext.Messages.ToListAsync();
        Console.WriteLine($"Found {messages.Count} messages:");
        foreach (var msg in messages)
        {
            Console.WriteLine($"  ID: {msg.Id}, Name: {msg.Name}");
        }
        Assert.That(messages.Count, Is.GreaterThan(0));
    }
}
