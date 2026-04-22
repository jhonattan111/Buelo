using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class Colaborador 
{
    public string Name {get; set;}
}

public class Relatorio_40001 : IDocument
{
    private readonly dynamic _data;

    public Relatorio_40001(dynamic data) => _data = data;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.Header().Text("My Report").FontSize(24);
            page.Content().Column(col =>
            {
                col.Item().Text($"Hello {_data.name}");
            });
            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }
}
