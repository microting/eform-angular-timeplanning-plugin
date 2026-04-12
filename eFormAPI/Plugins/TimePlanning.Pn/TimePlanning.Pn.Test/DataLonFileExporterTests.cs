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
public class DataLonFileExporterTests
{
    private DataLonFileExporter _exporter;

    [SetUp]
    public void SetUp()
    {
        _exporter = new DataLonFileExporter();
    }

    [Test]
    public void SystemType_IsDataLon()
    {
        Assert.That(_exporter.SystemType, Is.EqualTo(PayrollSystemType.DataLon));
    }

    [Test]
    public async Task Export_ResultContainsDataLonInFileName()
    {
        var model = new PayrollExportModel
        {
            PeriodStart = new DateTime(2026, 3, 20),
            PeriodEnd = new DateTime(2026, 4, 19),
            Workers = new List<PayrollExportWorkerModel>
            {
                new()
                {
                    EmployeeNo = "EMP001",
                    FullName = "Test",
                    Lines = new List<PayrollExportLineModel>
                    {
                        new() { PayrollCode = "0100", PayCode = "Normal", Hours = 8.0m, Date = new DateTime(2026, 4, 1) }
                    }
                }
            }
        };

        var result = await _exporter.Export(model);
        Assert.That(result.FileName, Does.Contain("DataLon"));
        Assert.That(result.FileName, Does.Not.Contain("DanLon"));
    }

    [Test]
    public async Task Export_ProducesSameFormatAsDanLon()
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
                        new() { PayrollCode = "0100", PayCode = "Normal", Hours = 7.50m, Date = new DateTime(2026, 4, 1) }
                    }
                }
            }
        };

        var result = await _exporter.Export(model);
        var bytes = result.FileContent;
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        var csv = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines[0].Trim(), Is.EqualTo("MedarbejderNr;Lønart;Timer;FraDato;TilDato"));
        Assert.That(lines[1].Trim(), Is.EqualTo("EMP001;0100;7.50;2026-04-01;2026-04-01"));
    }
}
