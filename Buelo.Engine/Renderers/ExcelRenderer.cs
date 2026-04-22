using Buelo.Contracts;
using ClosedXML.Excel;

namespace Buelo.Engine.Renderers;

/// <summary>
/// Generates an Excel workbook (.xlsx) from the request data.
/// Each top-level array in the JSON data becomes a separate worksheet.
/// For a flat object, key-value pairs are written to a single sheet.
/// </summary>
public class ExcelRenderer : IOutputRenderer
{
    public string Format => "excel";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => ".xlsx";

    public bool SupportsMode(TemplateMode mode) => mode == TemplateMode.FullClass;

    public Task<byte[]> RenderAsync(RendererInput input, CancellationToken cancellationToken = default)
    {
        if (!SupportsMode(input.Mode))
            throw new NotSupportedException($"ExcelRenderer does not support mode '{input.Mode}'.");

        using var workbook = new XLWorkbook();

        var dynData = TemplateEngine.ConvertToDynamic(input.RawData ?? new { });
        RenderToWorkbook(workbook, dynData, input.PageSettings);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    // ── Workbook population ───────────────────────────────────────────────────

    private static void RenderToWorkbook(XLWorkbook wb, object data, PageSettings settings)
    {
        if (data is IList<object> list)
        {
            RenderListToSheet(wb.Worksheets.Add("Data"), list);
        }
        else if (data is IDictionary<string, object> dict)
        {
            bool hasArraySheets = false;
            foreach (var (key, value) in dict)
            {
                if (value is IList<object> items && items.Count > 0)
                {
                    RenderListToSheet(wb.Worksheets.Add(SheetName(key)), items);
                    hasArraySheets = true;
                }
            }

            if (!hasArraySheets)
                RenderFlatDictToSheet(wb.Worksheets.Add("Data"), dict);
        }
        else
        {
            var ws = wb.Worksheets.Add("Data");
            ws.Cell(1, 1).Value = data?.ToString() ?? string.Empty;
        }

        if (!wb.Worksheets.Any())
            wb.Worksheets.Add("Empty");
    }

    private static void RenderListToSheet(IXLWorksheet ws, IList<object> list)
    {
        if (list.Count == 0)
        {
            ws.Cell(1, 1).Value = "(empty)";
            return;
        }

        // Collect column names from all row dictionaries
        var columns = list
            .OfType<IDictionary<string, object>>()
            .SelectMany(r => r.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (columns.Count == 0)
        {
            // Scalar list
            ws.Cell(1, 1).Value = "Value";
            ws.Cell(1, 1).Style.Font.Bold = true;
            for (int i = 0; i < list.Count; i++)
                ws.Cell(i + 2, 1).Value = list[i]?.ToString() ?? string.Empty;
            ws.Column(1).AdjustToContents();
            return;
        }

        // Header row
        for (int c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");
        }

        // Data rows
        int row = 2;
        foreach (var item in list)
        {
            if (item is not IDictionary<string, object> rowDict) continue;
            for (int c = 0; c < columns.Count; c++)
            {
                rowDict.TryGetValue(columns[c], out var val);
                SetCellValue(ws.Cell(row, c + 1), val);
            }
            row++;
        }

        ws.Range(1, 1, 1, columns.Count).SetAutoFilter();
        ws.Columns().AdjustToContents();
    }

    private static void RenderFlatDictToSheet(IXLWorksheet ws, IDictionary<string, object> dict)
    {
        ws.Cell(1, 1).Value = "Property";
        ws.Cell(1, 2).Value = "Value";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 2).Style.Font.Bold = true;

        int row = 2;
        foreach (var (key, value) in dict)
        {
            ws.Cell(row, 1).Value = key;
            SetCellValue(ws.Cell(row, 2), value);
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: break;
            case bool b: cell.Value = b; break;
            case long l: cell.Value = l; break;
            case double d: cell.Value = d; break;
            case DateTime dt: cell.Value = dt; cell.Style.NumberFormat.Format = "yyyy-MM-dd"; break;
            case IList<object>: cell.Value = "[array]"; break;
            case IDictionary<string, object>: cell.Value = "[object]"; break;
            default: cell.Value = value.ToString(); break;
        }
    }

    private static string SheetName(string key)
    {
        var name = key.Length > 31 ? key[..31] : key;
        return string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ' ? c : '_'));
    }
}
