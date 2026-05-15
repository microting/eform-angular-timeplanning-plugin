using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.PayrollExporters;
using TimePlanning.Pn.Models.PayrollExport;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class DanLonFileExporterTests
{
    private DanLonFileExporter _exporter;

    [SetUp]
    public void SetUp()
    {
        _exporter = new DanLonFileExporter();
    }

    [Test]
    public async Task Export_SingleWorkerSingleDay_CorrectCsvFormat()
    {
        var model = CreateModel(new[]
        {
            ("EMP001", "Test Worker", "0100", 7.5m, new DateTime(2026, 4, 1))
        });

        var result = await _exporter.Export(model);

        Assert.That(result.Success, Is.True);
        var csv = GetCsvContent(result);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines[0].Trim(), Is.EqualTo("MedarbejderNr;Lønart;Timer;FraDato;TilDato"));
        Assert.That(lines[1].Trim(), Is.EqualTo("EMP001;0100;7.50;2026-04-01;2026-04-01"));
    }

    [Test]
    public async Task Export_MultipleWorkers_GroupedByEmployee()
    {
        var model = CreateModel(new[]
        {
            ("EMP002", "Worker B", "0100", 8.0m, new DateTime(2026, 4, 1)),
            ("EMP001", "Worker A", "0100", 7.5m, new DateTime(2026, 4, 1)),
        });

        var result = await _exporter.Export(model);
        var csv = GetCsvContent(result);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Workers appear in model order (EMP002 first since that's how we built the model)
        Assert.That(lines.Length, Is.EqualTo(3)); // header + 2 data rows
    }

    [Test]
    public async Task Export_MultiplePayCodes_SeparateRows()
    {
        var model = new PayrollExportModel
        {
            PeriodStart = new DateTime(2026, 4, 1),
            PeriodEnd = new DateTime(2026, 4, 30),
            Workers = new List<PayrollExportWorkerModel>
            {
                new()
                {
                    EmployeeNo = "EMP001",
                    FullName = "Test",
                    Lines = new List<PayrollExportLineModel>
                    {
                        new() { PayrollCode = "0100", PayCode = "Normal", Hours = 7.0m, Date = new DateTime(2026, 4, 1) },
                        new() { PayrollCode = "0200", PayCode = "Overtime", Hours = 1.0m, Date = new DateTime(2026, 4, 1) },
                        new() { PayrollCode = "0300", PayCode = "Night", Hours = 0.5m, Date = new DateTime(2026, 4, 1) },
                    }
                }
            }
        };

        var result = await _exporter.Export(model);
        var csv = GetCsvContent(result);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(4)); // header + 3 data
    }

    [Test]
    public async Task Export_ZeroHours_IncludedInOutput()
    {
        var model = CreateModel(new[] { ("EMP001", "Test", "0100", 0.0m, new DateTime(2026, 4, 1)) });
        var result = await _exporter.Export(model);
        var csv = GetCsvContent(result);
        Assert.That(csv, Does.Contain("0.00"));
    }

    [Test]
    public async Task Export_EmptyModel_ReturnsEmptyFileWithHeader()
    {
        var model = new PayrollExportModel
        {
            PeriodStart = new DateTime(2026, 4, 1),
            PeriodEnd = new DateTime(2026, 4, 30),
            Workers = new List<PayrollExportWorkerModel>()
        };

        var result = await _exporter.Export(model);
        Assert.That(result.Success, Is.True);
        var csv = GetCsvContent(result);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(1)); // header only
    }

    [Test]
    public async Task Export_ResultContainsCorrectFileName()
    {
        var model = CreateModel(new[] { ("EMP001", "Test", "0100", 8.0m, new DateTime(2026, 4, 1)) });
        model.PeriodStart = new DateTime(2026, 3, 20);
        model.PeriodEnd = new DateTime(2026, 4, 19);

        var result = await _exporter.Export(model);
        Assert.That(result.FileName, Does.Contain("DanLon"));
        Assert.That(result.FileName, Does.Contain("20260320"));
        Assert.That(result.FileName, Does.Contain("20260419"));
    }

    [Test]
    public async Task Export_DateFormat_IsIso8601()
    {
        var model = CreateModel(new[] { ("EMP001", "Test", "0100", 8.0m, new DateTime(2026, 1, 5)) });
        var result = await _exporter.Export(model);
        var csv = GetCsvContent(result);
        Assert.That(csv, Does.Contain("2026-01-05"));
    }

    [Test]
    public async Task Export_HoursFormat_TwoDecimalPlaces()
    {
        var model = CreateModel(new[] { ("EMP001", "Test", "0100", 8.0m, new DateTime(2026, 4, 1)) });
        var result = await _exporter.Export(model);
        var csv = GetCsvContent(result);
        Assert.That(csv, Does.Contain("8.00"));
    }

    // --- Helpers ---

    private static PayrollExportModel CreateModel(
        (string empNo, string name, string payrollCode, decimal hours, DateTime date)[] lines)
    {
        var workers = new Dictionary<string, PayrollExportWorkerModel>();
        foreach (var (empNo, name, payrollCode, hours, date) in lines)
        {
            if (!workers.ContainsKey(empNo))
                workers[empNo] = new PayrollExportWorkerModel { EmployeeNo = empNo, FullName = name, Lines = new() };
            workers[empNo].Lines.Add(new PayrollExportLineModel
            {
                PayrollCode = payrollCode, PayCode = "test", Hours = hours, Date = date
            });
        }
        return new PayrollExportModel
        {
            PeriodStart = new DateTime(2026, 4, 1),
            PeriodEnd = new DateTime(2026, 4, 30),
            Workers = workers.Values.ToList()
        };
    }

    private static string GetCsvContent(PayrollExportResult result)
    {
        // Skip UTF-8 BOM (3 bytes) if present
        var bytes = result.FileContent;
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }
}
