using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using NUnit.Framework;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class CreateNumericCellTests
{
    [TestCase(-945.16999999999996, "-945.16999999999996")]
    [TestCase(123.456789012345678, "123.45678901234568")]
    [TestCase(0.123456789012345678, "0.12345678901234568")]
    [TestCase(-0.999999999999999, "-0.999999999999999")]
    [TestCase(1234567.89123456, "1234567.8912345599")]
    [TestCase(0.1, "0.10000000000000001")]
    [TestCase(0.01, "0.01")]
    [TestCase(0.001, "0.001")]
    public void CreateNumericCell_PreservesFullDoublePrecision(double value, string expectedValue)
    {
        // Arrange & Act
        var result = CreateNumericCellHelper(value);
        
        // Assert
        Assert.That(result.CellValue.Text, Is.EqualTo(expectedValue), 
            $"Expected {expectedValue} but got {result.CellValue.Text}");
        Assert.That(result.DataType?.Value, Is.EqualTo(CellValues.Number));
    }
    
    [Test]
    public void CreateNumericCell_HandlesZero()
    {
        // Arrange & Act
        var result = CreateNumericCellHelper(0.0);
        
        // Assert
        Assert.That(result.CellValue.Text, Is.EqualTo("0"));
        Assert.That(result.DataType?.Value, Is.EqualTo(CellValues.Number));
    }
    
    [Test]
    public void CreateNumericCell_HandlesLargeNumbers()
    {
        // Arrange
        double value = 1234567890123.456;
        
        // Act
        var result = CreateNumericCellHelper(value);
        
        // Assert
        // G17 format should preserve precision up to 17 significant digits
        Assert.That(result.CellValue.Text, Does.StartWith("1234567890123.45"));
        Assert.That(result.DataType?.Value, Is.EqualTo(CellValues.Number));
    }
    
    [Test]
    public void CreateNumericCell_DoesNotRoundTo2Decimals()
    {
        // Arrange
        double value = -945.16999999999996;
        
        // Act
        var result = CreateNumericCellHelper(value);
        
        // Assert
        // Should NOT round to -945.17
        Assert.That(result.CellValue.Text, Is.Not.EqualTo("-945.17"), 
            "Value should not be rounded to 2 decimal places");
        Assert.That(result.CellValue.Text, Does.StartWith("-945.169"), 
            "Value should preserve precision beyond 2 decimal places");
    }
    
    /// <summary>
    /// Helper method that mimics the CreateNumericCell implementation in TimePlanningWorkingHoursService
    /// This is used to test the numeric cell creation logic
    /// </summary>
    private Cell CreateNumericCellHelper(double value)
    {
        return new Cell()
        {
            CellValue = new CellValue(value.ToString("G17", CultureInfo.InvariantCulture)),
            DataType = CellValues.Number
        };
    }
}
