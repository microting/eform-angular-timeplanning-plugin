using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NUnit.Framework;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class ExcelNumericPrecisionIntegrationTests
{
    [Test]
    public void Excel_PreservesFullDoublePrecision_WhenWritingAndReading()
    {
        // Arrange
        var testValue = -945.16999999999996;
        var filePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
        
        try
        {
            // Act - Create Excel file with the value
            CreateExcelWithNumericValue(filePath, testValue);
            
            // Assert - Read the value back from Excel
            var readValue = ReadNumericValueFromExcel(filePath);
            
            // The value should match (allowing for double precision tolerance)
            Assert.That(readValue, Is.EqualTo(testValue).Within(1e-14), 
                $"Expected {testValue} but got {readValue}. Excel is rounding the value.");
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
    
    [TestCase(-945.16999999999996)]
    [TestCase(123.456789012345678)]
    [TestCase(0.123456789012345678)]
    [TestCase(-0.999999999999999)]
    public void Excel_PreservesMultipleDoubleValues(double testValue)
    {
        // Arrange
        var filePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
        
        try
        {
            // Act - Create Excel file with the value
            CreateExcelWithNumericValue(filePath, testValue);
            
            // Assert - Read the value back from Excel
            var readValue = ReadNumericValueFromExcel(filePath);
            
            // Convert to string to check precision preservation
            var originalString = testValue.ToString("G17", CultureInfo.InvariantCulture);
            var readString = readValue.ToString("G17", CultureInfo.InvariantCulture);
            
            Assert.That(readString, Is.EqualTo(originalString), 
                $"Expected '{originalString}' but got '{readString}'. Precision was lost in Excel.");
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
    
    [Test]
    public void Excel_DoesNotRoundTo2Decimals()
    {
        // Arrange
        var testValue = -945.16999999999996;
        var filePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xlsx");
        
        try
        {
            // Act
            CreateExcelWithNumericValue(filePath, testValue);
            var readValue = ReadNumericValueFromExcel(filePath);
            var readString = readValue.ToString("G17", CultureInfo.InvariantCulture);
            
            // Assert - Should NOT be rounded to 2 decimals
            Assert.That(readString, Is.Not.EqualTo("-945.17"), 
                "Value was rounded to 2 decimal places");
            Assert.That(readString, Does.StartWith("-945.169"), 
                "Value should preserve precision beyond 2 decimal places");
        }
        finally
        {
            // Cleanup
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
    
    private void CreateExcelWithNumericValue(string filePath, double value)
    {
        using (SpreadsheetDocument document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
        {
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            
            // Add stylesheet with custom number format
            WorkbookStylesPart workbookStylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            GenerateStylesheet(workbookStylesPart);
            
            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());
            
            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
            Sheet sheet = new Sheet()
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Test"
            };
            sheets.Append(sheet);
            
            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            Row row = new Row();
            
            // Create the cell using the same logic as CreateNumericCell
            Cell cell = new Cell()
            {
                CellValue = new CellValue(value.ToString("G17", CultureInfo.InvariantCulture)),
                DataType = CellValues.Number,
                StyleIndex = (UInt32Value)3U  // Use the custom number format
            };
            
            row.Append(cell);
            sheetData.Append(row);
            
            workbookPart.Workbook.Save();
        }
    }
    
    private void GenerateStylesheet(WorkbookStylesPart workbookStylesPart)
    {
        Stylesheet stylesheet = new Stylesheet();
        
        // Add custom number format
        NumberingFormats numberingFormats = new NumberingFormats() { Count = (UInt32Value)1U };
        NumberingFormat numberingFormat = new NumberingFormat()
        {
            NumberFormatId = (UInt32Value)164U,
            FormatCode = "0.##################"
        };
        numberingFormats.Append(numberingFormat);
        
        // Fonts
        Fonts fonts = new Fonts() { Count = (UInt32Value)1U };
        Font font = new Font();
        fonts.Append(font);
        
        // Fills
        Fills fills = new Fills() { Count = (UInt32Value)1U };
        Fill fill = new Fill();
        fills.Append(fill);
        
        // Borders
        Borders borders = new Borders() { Count = (UInt32Value)1U };
        Border border = new Border();
        borders.Append(border);
        
        // Cell Style Formats
        CellStyleFormats cellStyleFormats = new CellStyleFormats() { Count = (UInt32Value)1U };
        CellFormat cellStyleFormat = new CellFormat();
        cellStyleFormats.Append(cellStyleFormat);
        
        // Cell Formats
        CellFormats cellFormats = new CellFormats() { Count = (UInt32Value)4U };
        cellFormats.Append(new CellFormat());  // 0 - default
        cellFormats.Append(new CellFormat());  // 1 - not used
        cellFormats.Append(new CellFormat());  // 2 - not used  
        cellFormats.Append(new CellFormat()    // 3 - custom number format
        {
            NumberFormatId = (UInt32Value)164U,
            ApplyNumberFormat = true
        });
        
        stylesheet.Append(numberingFormats);
        stylesheet.Append(fonts);
        stylesheet.Append(fills);
        stylesheet.Append(borders);
        stylesheet.Append(cellStyleFormats);
        stylesheet.Append(cellFormats);
        
        workbookStylesPart.Stylesheet = stylesheet;
    }
    
    private double ReadNumericValueFromExcel(string filePath)
    {
        using (SpreadsheetDocument document = SpreadsheetDocument.Open(filePath, false))
        {
            WorkbookPart workbookPart = document.WorkbookPart;
            WorksheetPart worksheetPart = workbookPart.WorksheetParts.First();
            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            Row row = sheetData.Elements<Row>().First();
            Cell cell = row.Elements<Cell>().First();
            
            return double.Parse(cell.CellValue.Text, CultureInfo.InvariantCulture);
        }
    }
}
