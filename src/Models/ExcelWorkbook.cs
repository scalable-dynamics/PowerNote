using System.Xml.Serialization;

namespace PowerNote.Models;

[Serializable]
[XmlRoot("workbook", Namespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main")]
public class ExcelWorkbook
{
    [XmlArray("sheets")]
    [XmlArrayItem("sheet")]
    public ExcelWorkbookSheet[] Sheets { get; set; }

    [XmlArray("definedNames")]
    [XmlArrayItem("definedName")]
    public ExcelWorkbookDefinedName[] DefinedNames { get; set; }
}

public class ExcelWorkbookDefinedName
{
    [XmlAttribute("name")]
    public string Name { get; set; }

    [XmlElement]
    public string Text { get; set; }
}

public class ExcelWorkbookSheet
{
    [XmlAttribute("name")]
    public string Name { get; set; }

    [XmlAttribute("sheetId")]
    public string SheetId { get; set; }

    [XmlAttribute("id", Namespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships")]
    public string SheetReferenceId { get; set; }
}

[Serializable]
[XmlRoot("sst", Namespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main")]
public class ExcelWorkbookStringTable
{
    [XmlAttribute("uniqueCount")]
    public string UniqueCount { get; set; }

    [XmlAttribute("count")]
    public string Count { get; set; }

    [XmlElement("si")]
    public ExcelWorkbookStringTableText[] Items { get; set; }
}

public class ExcelWorkbookStringTableText
{
    [XmlElement("t")]
    public string Text { get; set; }
}

[Serializable]
[XmlRoot("worksheet", Namespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main")]
public class ExcelWorksheet
{
    [XmlArray("sheetData")]
    [XmlArrayItem("row")]
    public ExcelWorksheetRow[] Rows { get; set; }

    [XmlArray("mergeCells")]
    [XmlArrayItem("mergeCell")]
    public ExcelWorksheetMergedCell[] MergedCells { get; set; }
}

public class ExcelWorksheetCell
{
    [XmlAttribute("r")]
    public string CellReference { get; set; }

    [XmlAttribute("t")]
    public string CellType { get; set; }

    [XmlElement("v")]
    public string Value { get; set; }

    [XmlElement("f")]
    public string Formula { get; set; }
}

public class ExcelWorksheetMergedCell
{
    [XmlAttribute("ref")]
    public string CellRange { get; set; }
}

public class ExcelWorksheetRow
{
    [XmlAttribute("r")]
    public int RowReference { get; set; }

    [XmlElement("c")]
    public ExcelWorksheetCell[] Cells { get; set; }
}