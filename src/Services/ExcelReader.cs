using PowerNote.Models;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace PowerNote.Services;

public record SpreadsheetCellReference(string Sheet, int Row, int Column);
public record SpreadsheetCellRange(string FromSheet, int FromRow, int FromColumn, int ToRow, int ToColumn) : SpreadsheetCellReference(FromSheet, FromRow, FromColumn);
public record SpreadsheetCell(string CellSheet, int CellRow, int CellColumn, string Text, string Formula) : SpreadsheetCellReference(CellSheet, CellRow, CellColumn)
{
    public SpreadsheetCellReference MergedToCell { get; set; }
}

public class ExcelReader
{
    public IEnumerable<SpreadsheetCell> ReadExcel(string xlsxPath)
    {
        var items = new List<SpreadsheetCell>();
        using var file = File.OpenRead(xlsxPath);
        using var zipArchive = new ZipArchive(file, ZipArchiveMode.Read, false);
        var workbook = ObjectZipEntry<ExcelWorkbook>(GetZipArchiveEntry(zipArchive, "xl/workbook.xml"));
        var stringTable = ObjectZipEntry<ExcelWorkbookStringTable>(GetZipArchiveEntry(zipArchive, "xl/sharedStrings.xml"))?.Items;
        var relationships = XmlDocumentZipEntry(GetZipArchiveEntry(zipArchive, "xl/_rels/workbook.xml.rels"));
        foreach (var sheet in workbook.Sheets)
        {
            if (!string.IsNullOrEmpty(sheet.SheetReferenceId))
            {
                var sheetPath = FindRelationshipTarget(sheet.SheetReferenceId, relationships);
                if (!string.IsNullOrEmpty(sheetPath))
                {
                    var worksheet = ObjectZipEntry<ExcelWorksheet>(GetZipArchiveEntry(zipArchive, "xl/" + sheetPath));
                    if (worksheet.Rows != null)
                    {
                        var merged_items = new List<SpreadsheetCellRange>();
                        if (worksheet.MergedCells != null && worksheet.MergedCells.Length > 0)
                        {
                            merged_items.AddRange(worksheet.MergedCells.Where(m => m.CellRange.Contains(":")).Select(m =>
                            {
                                var cellRange = GetCellReference(m.CellRange, sheet.Name) as SpreadsheetCellRange;
                                if (cellRange != null)
                                {
                                    return cellRange;
                                }
                                else
                                {
                                    return null;
                                }
                            }).Where(r => r != null));
                        }
                        var sheetIndex = workbook.Sheets.ToList().IndexOf(sheet);
                        items.AddRange(from row in worksheet.Rows
                                       where row.Cells != null
                                       from cell in row.Cells
                                       let t = GetText(stringTable, cell)
                                       where !string.IsNullOrEmpty(t) || !string.IsNullOrEmpty(cell.Formula)
                                       let s = sheet.Name
                                       let r = row.RowReference - 1
                                       let c = GetColumnIndex(cell.CellReference)
                                       let m = merged_items.Where(m => m.Sheet == s && m.Row == r && m.Column == c).FirstOrDefault()
                                       select new SpreadsheetCell(s, r, c, t, cell.Formula)
                                       {
                                           MergedToCell = (m != null ? new SpreadsheetCellReference(m.Sheet, m.Row, m.Column) : null)
                                       });
                    }
                }
            }
        }
        return items;
    }

    private static string GetText(ExcelWorkbookStringTableText[] stringTableItems, ExcelWorksheetCell cell)
    {
        int s = 0;
        if (cell.CellType == "s" && int.TryParse(cell.Value, out s) && s < stringTableItems.Length)
        {
            return stringTableItems[s].Text?.Trim();
        }
        else if (cell.CellType == "str")
        {
            if (!string.IsNullOrEmpty(cell.Value))
            {
                return cell.Value.Trim();
            }
            else
            {
                return string.Empty;
            }
        }
        else if (!string.IsNullOrEmpty(cell.Value))
        {
            //double amount;
            //if (double.TryParse(cell.Value, out amount))
            //{
            //    var date = DateTime.FromOADate(amount);
            //    return date.ToShortDateString();
            //    //return amount.ToString("#,##0.##");
            //}
            //else
            //{
            //    return cell.Value;
            //}
            return cell.Value;
        }
        else
        {
            return string.Empty;
        }
    }

    private static int GetColumnIndex(string cellReference)
    {
        var colLetters = new Regex("[A-Za-z]+").Match(cellReference).Value.ToUpper();
        var colIndex = 0;
        for (int i = 0; i < colLetters.Length; i++)
        {
            colIndex *= 26;
            colIndex += (colLetters[i] - 'A' + 1);
        }
        return colIndex - 1;
    }

    private static int GetRowIndex(string cellReference)
    {
        var cellNumbers = new Regex("[0-9]+").Match(cellReference).Value;
        if (!string.IsNullOrEmpty(cellNumbers))
        {
            return Convert.ToInt32(cellNumbers) - 1;
        }
        else
        {
            return -1;
        }
    }

    private static SpreadsheetCellReference GetCellReference(string cellReference, string currentSheet)
    {
        if (cellReference.Contains(':'))
        {
            var cellFrom = GetCellReference(cellReference.Split(':')[0], currentSheet);
            var cellTo = GetCellReference(cellReference.Split(':')[1], currentSheet);
            if (cellFrom.Row > cellTo.Row || cellFrom.Column > cellTo.Column)
            {
                return null;
            }
            else
            {
                return new SpreadsheetCellRange(cellFrom.Sheet, cellFrom.Row, cellFrom.Column, cellTo.Row, cellTo.Column);
            }
        }
        else if (cellReference.Contains("#REF!"))
        {
            return null;
        }
        else if (cellReference.Contains('!'))
        {
            var sheet = cellReference.Split('!')[0].Replace("'", "");
            var cell = cellReference.Split('!')[1];
            var row = GetRowIndex(cell);
            var column = GetColumnIndex(cell);
            return new SpreadsheetCellReference(sheet, row, column);
        }
        else
        {
            var row = GetRowIndex(cellReference);
            var column = GetColumnIndex(cellReference);
            return new SpreadsheetCellReference(currentSheet, row, column);
        }
    }

    private static string FindRelationshipTarget(string relId, XmlDocument relationships)
    {
        var sheetReference = relationships.SelectSingleNode("//node()[@Id='" + relId + "']");
        if (sheetReference != null)
        {
            var targetAttribute = sheetReference.Attributes["Target"];
            if (targetAttribute != null)
            {
                return targetAttribute.Value;
            }
        }
        return null;
    }

    private static ZipArchiveEntry GetZipArchiveEntry(ZipArchive zipArchive, string zipPath)
    {
        return zipArchive.Entries.First(n => n.FullName.Equals(zipPath));
    }

    private static T ObjectZipEntry<T>(ZipArchiveEntry zipArchiveEntry)
    {
        using (var stream = zipArchiveEntry.Open())
            return (T)new XmlSerializer(typeof(T)).Deserialize(XmlReader.Create(stream));
    }

    private static XmlDocument XmlDocumentZipEntry(ZipArchiveEntry zipArchiveEntry)
    {
        var xmlDocument = new XmlDocument();
        using (var stream = zipArchiveEntry.Open())
        {
            xmlDocument.Load(stream);
            return xmlDocument;
        }
    }
}
