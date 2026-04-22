using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;

namespace Buelo.Defaults;

public abstract class LayoutPadrao : IDocument
{
    protected string NomeRelatorio { get; }
    protected string NomeEmpresa { get; } = "Contar Consultoria e Contabilidade";
    protected virtual PageSize TamanhoPagina => PageSizes.A4;

    protected LayoutPadrao(string nomeRelatorio)
    {
        NomeRelatorio = nomeRelatorio;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        ComposeDocument(container);
    }

    protected void ComposeDocument(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(TamanhoPagina);
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Calibri"));

            page.Header().PaddingBottom(12).Element(ComposeHeader);

            page.Content().Element(ComposeContent);

            page.Footer().Element(ComposeFooter);
        });
    }

    protected virtual void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            //row.ConstantItem(100).Height(100).AlignMiddle().Element(ComposeLogo);
            row.ConstantItem(100).Height(100);
            row.RelativeItem().AlignMiddle().Column(col =>
            {
                col.Item().Text(NomeEmpresa).Bold().FontSize(14);
                col.Item().Text(NomeRelatorio).FontSize(12).FontColor(Colors.Grey.Darken2);
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().AlignRight().Text($"Emitido em: {DateTime.Now}");
            });

            row.Spacing(10);
        });
    }

    //private void ComposeLogo(IContainer container)
    //{
    //    string path = Path.Combine(AppContext.BaseDirectory, "Assets", "LogoContarConsultoria.png");
    //    container.Image(path).FitArea();
    //}

    protected virtual void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text($"{NomeEmpresa} - {DateTime.UtcNow.AddHours(-3).Year}")
                .FontSize(10)
                .FontColor(Colors.Grey.Darken2);

            row.RelativeItem()
            .AlignRight()
            .Text(text =>
            {
                text.DefaultTextStyle(x =>
                    x
                    .FontSize(10)
                    .FontColor(Colors.Grey.Darken2)
                );

                text.Span($"Página ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    protected abstract void ComposeContent(IContainer container);
}