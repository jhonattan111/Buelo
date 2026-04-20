using Buelo.Contracts;
using Buelo.Engine.BueloDsl;
using ClosedXML.Excel;
using System.Text.Json;

namespace Buelo.Engine.Renderers;

public class ExcelRenderer : IOutputRenderer
{
    public string Format => "excel";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => ".xlsx";

    public bool SupportsMode(TemplateMode mode) => mode == TemplateMode.BueloDsl;

    public Task<byte[]> RenderAsync(RendererInput input, CancellationToken cancellationToken = default)
    {
        if (!SupportsMode(input.Mode))
            throw new NotSupportedException("Excel rendering requires .buelo DSL mode.");

        var doc = input.BueloDslDocument ?? BueloDslParser.Parse(input.Source);

        input.FormatHints.TryGetValue("excel.sheetName", out var sheetName);
        sheetName = string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetName);

        int currentRow = 1;

        foreach (var comp in doc.Components)
        {
            if (comp is not BueloDslLayoutComponent layout) continue;

            var typeLower = layout.ComponentType.ToLowerInvariant();

            // Title / resume → merged text row.
            if (typeLower is "report title" or "report resume")
            {
                foreach (var child in layout.Children.OfType<BueloDslTextComponent>())
                {
                    var cell = ws.Cell(currentRow++, 1);
                    cell.Value = StripExpressions(child.Value);
                    cell.Style.Font.Bold = true;
                }
                continue;
            }

            // Page header/footer → skip in Excel.
            if (typeLower is "page header" or "page footer" or "header" or "footer")
                continue;

            // Data / group header → render tables inside.
            foreach (var child in layout.Children.OfType<BueloDslTableComponent>())
            {
                currentRow = RenderTable(ws, child, input.RawData, currentRow);
            }
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    // ── Table rendering ───────────────────────────────────────────────────────

    private static int RenderTable(IXLWorksheet ws, BueloDslTableComponent table, object? rawData, int startRow)
    {
        // Write column headers.
        for (int col = 0; col < table.Columns.Count; col++)
        {
            var cell = ws.Cell(startRow, col + 1);
            cell.Value = table.Columns[col].Label;
            cell.Style.Font.Bold = true;
            if (table.HeaderStyle?.BackgroundColor is { } bg)
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(bg);
        }
        startRow++;

        // Write data rows.
        var rows = ToJsonRows(rawData);
        foreach (var row in rows)
        {
            for (int col = 0; col < table.Columns.Count; col++)
            {
                var column = table.Columns[col];
                var cell = ws.Cell(startRow, col + 1);
                SetCellValue(cell, row, column.Field, column.Format);
            }
            startRow++;
        }

        return startRow;
    }

    private static void SetCellValue(IXLCell cell, JsonElement row, string field, string? format)
    {
        if (!row.TryGetProperty(field, out var value))
        {
            // Try case-insensitive property lookup.
            foreach (var prop in row.EnumerateObject())
            {
                if (string.Equals(prop.Name, field, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    break;
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Undefined)
        {
            cell.Value = string.Empty;
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                var num = value.GetDecimal();
                cell.Value = num;
                if (string.Equals(format, "currency", StringComparison.OrdinalIgnoreCase))
                    cell.Style.NumberFormat.SetFormat("R$ #,##0.00");
                else if (string.Equals(format, "date", StringComparison.OrdinalIgnoreCase))
                    cell.Style.NumberFormat.SetFormat("dd/MM/yyyy");
                break;

            case JsonValueKind.True:
                cell.Value = true;
                break;

            case JsonValueKind.False:
                cell.Value = false;
                break;

            case JsonValueKind.Null:
                cell.Value = string.Empty;
                break;

            default:
                var str = value.GetString() ?? string.Empty;
                // Try parsing as date.
                if (string.Equals(format, "date", StringComparison.OrdinalIgnoreCase) &&
                    DateTimeOffset.TryParse(str, out var dt))
                {
                    cell.Value = dt.DateTime;
                    cell.Style.NumberFormat.SetFormat("dd/MM/yyyy");
                }
                else
                {
                    cell.Value = str;
                }
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<JsonElement> ToJsonRows(object? rawData)
    {
        if (rawData is null) yield break;

        JsonElement element;
        if (rawData is JsonElement je)
            element = je;
        else
        {
            var json = JsonSerializer.Serialize(rawData);
            element = JsonSerializer.Deserialize<JsonElement>(json);
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                yield return item;
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
        }
    }

    private static string StripExpressions(string text)
    {
        // Remove {{ ... }} expression placeholders.
        int start;
        while ((start = text.IndexOf("{{", StringComparison.Ordinal)) >= 0)
        {
            int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0) break;
            text = text[..start] + text[(end + 2)..];
        }
        return text.Trim();
    }
}
