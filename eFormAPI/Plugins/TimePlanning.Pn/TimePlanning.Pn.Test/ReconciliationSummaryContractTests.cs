using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Controllers;
using TimePlanning.Pn.Infrastructure.Models.Reconciliation;
using TimePlanning.Pn.Services.ReconciliationSummaryService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Pins the HTTP contract my-microting's scan depends on. No database.
/// The serializer settings mirror the host (eFormAPI.Web ServiceCollectionExtensions:
/// AddNewtonsoftJson with only CamelCasePropertyNamesContractResolver set,
/// so DateTimeZoneHandling stays RoundtripKind and dates are ISO).
/// </summary>
[TestFixture]
public class ReconciliationSummaryContractTests
{
    private static string Serialize(object value) => JsonConvert.SerializeObject(value,
        new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

    [Test]
    public void WireShape_FullModel()
    {
        var model = new ReconciliationSummaryModel
        {
            CutoffDay = 19,
            PeriodStart = "2026-07-20",
            PeriodEnd = "2026-08-19",
            WorkersInPeriod = 10,
            WorkersLockedThroughPeriod = 7,
            WorkersNeverReconciled = 1,
            CoveragePercent = 70.0,
            OldestBoundary = "2026-06-30",
            WorkersWithAnyReconciled = 9,
            LastReconciledAt = DateTime.SpecifyKind(new DateTime(2026, 9, 18, 12, 32, 0), DateTimeKind.Utc),
        };

        var json = Serialize(new OperationDataResult<ReconciliationSummaryModel>(true, model));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"success\":true"));
            Assert.That(json, Does.Contain("\"message\":\"Success\""));
            Assert.That(json, Does.Contain("\"model\":{"));
            Assert.That(json, Does.Contain("\"cutoffDay\":19"));
            Assert.That(json, Does.Contain("\"periodStart\":\"2026-07-20\""));
            Assert.That(json, Does.Contain("\"periodEnd\":\"2026-08-19\""));
            Assert.That(json, Does.Contain("\"workersInPeriod\":10"));
            Assert.That(json, Does.Contain("\"workersLockedThroughPeriod\":7"));
            Assert.That(json, Does.Contain("\"workersNeverReconciled\":1"));
            Assert.That(json, Does.Contain("\"coveragePercent\":70.0"));
            Assert.That(json, Does.Contain("\"oldestBoundary\":\"2026-06-30\""));
            Assert.That(json, Does.Contain("\"workersWithAnyReconciled\":9"));
            Assert.That(json, Does.Contain("\"lastReconciledAt\":\"2026-09-18T12:32:00Z\""));
            Assert.That(json, Does.Not.Contain("workersBehind"), "derived by the consumer, not sent");
        });
    }

    [Test]
    public void WireShape_LastReconciledAtKeepsFractionalSecondsAndZ()
    {
        var model = new ReconciliationSummaryModel
        {
            LastReconciledAt = DateTime.SpecifyKind(
                new DateTime(2026, 9, 18, 12, 32, 0).AddTicks(1_234_560), DateTimeKind.Utc),
        };

        Assert.That(Serialize(model), Does.Contain("\"lastReconciledAt\":\"2026-09-18T12:32:00.123456Z\""));
    }

    [Test]
    public void WireShape_NullsAreWrittenExplicitly()
    {
        var json = Serialize(new ReconciliationSummaryModel { PeriodStart = "2026-08-20", PeriodEnd = "2026-09-19" });

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"coveragePercent\":null"));
            Assert.That(json, Does.Contain("\"oldestBoundary\":null"));
            Assert.That(json, Does.Contain("\"lastReconciledAt\":null"));
        });
    }

    [Test]
    public void WireShape_Failure()
    {
        var json = Serialize(new OperationDataResult<ReconciliationSummaryModel>(
            false, ReconciliationSummaryService.ErrorMessage));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"success\":false"));
            Assert.That(json, Does.Contain("\"message\":\"ErrorWhileReadingReconciliationSummary\""));
            Assert.That(json, Does.Contain("\"model\":null"));
        });
    }

    [Test]
    public void Controller_RouteAndAuthorization()
    {
        var type = typeof(ReconciliationSummaryController);
        var route = type.GetCustomAttribute<RouteAttribute>();
        var authorize = type.GetCustomAttribute<AuthorizeAttribute>();
        var get = type.GetMethod(nameof(ReconciliationSummaryController.Summary))!
            .GetCustomAttribute<HttpGetAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(route?.Template, Is.EqualTo("api/time-planning-pn/reconciliation"));
            Assert.That(authorize, Is.Not.Null, "must require an authenticated caller");
            Assert.That(get?.Template, Is.EqualTo("summary"));
            Assert.That(type.GetMethods().Count(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a =>
                a.HttpMethods.Any(h => h != "GET"))), Is.Zero, "read-only: no write verbs");
        });
    }

    [Test]
    public async Task Controller_ReturnsTheServiceResultUnchanged()
    {
        var expected = new OperationDataResult<ReconciliationSummaryModel>(true, new ReconciliationSummaryModel());
        var service = Substitute.For<IReconciliationSummaryService>();
        service.GetSummaryAsync().Returns(expected);

        var actual = await new ReconciliationSummaryController(service).Summary();

        Assert.That(actual, Is.SameAs(expected));
    }
}
