# 📚 QuestPDF Template References

Este documento cataloga os templates QuestPDF de referência fornecidos para ajudar no desenvolvimento dos novos templates do Buelo.

## 📋 Templates Disponíveis

### 1. **InvoiceDocument.cs** - Template de Fatura
**Localização**: Attachments (QuestPDFShowcase project)

**Características**:
- Layout limpo e profissional
- Dados do vendedor e comprador
- Tabela de itens com quantidade/preço
- Sumário de subtotal, impostos, total
- Header e footer com informações

**Modelo de Dados**:
```csharp
public class InvoiceModel
{
    public int InvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string SellerName { get; set; }
    public string SellerAddress { get; set; }
    public string SellerEmail { get; set; }
    public string BuyerName { get; set; }
    public string BuyerAddress { get; set; }
    public string BuyerEmail { get; set; }
    public List<InvoiceItem> Items { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
}
```

**Uso em Buelo**:
```csharp
public class InvoiceDocument : IDocument
{
    private readonly dynamic _data;
    
    public InvoiceDocument(dynamic data) => _data = data;
    
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            // ... implementar layout baseado na referência
        });
    }
}
```

---

### 2. **FinancialDashboardDocument.cs** - Dashboard Financeiro
**Características**:
- Estatísticas mensais (Revenue, Expenses, Profit)
- Cards de KPI com cores destacadas
- Tabela de dados financeiros
- Gráfico de departamentos com barras
- Layout landscape com múltiplos elementos

**Modelo de Dados**:
```csharp
public class MonthlyStat
{
    public string Month { get; set; }
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit { get; set; }
}

public class DepartmentBudget
{
    public string Department { get; set; }
    public decimal Allocated { get; set; }
    public decimal Spent { get; set; }
    public double UsagePercent { get; set; }
}

public class FinancialDashboardModel
{
    public int Year { get; set; }
    public string CompanyName { get; set; }
    public List<MonthlyStat> MonthlyStats { get; set; }
    public List<DepartmentBudget> Departments { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
}
```

**Técnicas Úteis**:
- Cards com cores de destaque (KPI)
- Tabelas com múltiplas colunas
- Gráficos de barras verticais
- Row layout para dados lado a lado

---

### 3. **ProductCatalogDocument.cs** - Catálogo de Produtos
**Características**:
- Capa customizada com branding
- Páginas por categoria
- Grid de produtos com imagens/descrições
- Badges para "New" e "Featured"
- Rating com estrelas
- Info de stock e preço

**Modelo de Dados**:
```csharp
public class Product
{
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public string SKU { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public double Rating { get; set; }
    public bool IsNew { get; set; }
    public bool IsFeatured { get; set; }
}

public class ProductCatalogModel
{
    public string StoreName { get; set; }
    public string Tagline { get; set; }
    public string Season { get; set; }
    public List<Product> Products { get; set; }
}
```

**Técnicas Úteis**:
- Múltiplas páginas (cover + content pages)
- Group by categoria
- Badges e indicadores
- Star ratings
- Grid layout
- Borders e espaçamento

---

### 4. **SalesPerformanceDocument.cs** - Report de Performance de Vendas
**Características**:
- KPI cards com métricas principais
- Gráfico de bookings vs renewals
- Gráfico de pipeline por estágio
- Tabela de breakdown trimestral
- Múltiplas páginas (overview + detalhes)

**Modelo de Dados**:
```csharp
public class SalesMonthlyMetric
{
    public string Month { get; set; }
    public decimal Bookings { get; set; }
    public decimal Renewals { get; set; }
}

public class PipelineStage
{
    public string Name { get; set; }
    public decimal Value { get; set; }
}

public class SalesPerformanceModel
{
    public string CompanyName { get; set; }
    public string QuarterLabel { get; set; }
    public string SalesLead { get; set; }
    public List<SalesMonthlyMetric> MonthlyMetrics { get; set; }
    public List<PipelineStage> PipelineStages { get; set; }
}
```

**Técnicas Úteis**:
- Múltiplas páginas
- Gráficos (bar, line, pie)
- KPI cards com cores
- Summary tables
- Page headers com metadata

---

### 5. **OperationsSnapshotDocument.cs** - Snapshot Operacional
**Características**:
- Métricas de tickets e incidentes
- Cards de KPI com colores
- Gráfico de tickets abertos por dia
- Gráfico de capacidade de times
- Layout compacto

**Modelo de Dados**:
```csharp
public class TeamCapacity
{
    public string TeamName { get; set; }
    public decimal CapacityPercent { get; set; }
}

public class DailyTicketMetric
{
    public string DayLabel { get; set; }
    public decimal Opened { get; set; }
}

public class OperationsSnapshotModel
{
    public string CompanyName { get; set; }
    public string PeriodLabel { get; set; }
    public int ResolvedTickets { get; set; }
    public int OpenIncidents { get; set; }
    public decimal SlaCompliancePercent { get; set; }
    public List<DailyTicketMetric> DailyTickets { get; set; }
    public List<TeamCapacity> TeamCapacity { get; set; }
}
```

---

## 🛠️ Shared Components

### SharedReportLayout.cs
Componentes compartilhados para layout:
- `ComposeHeader()` - Header padronizado com título/subtitle
- `ComposeFooter()` - Footer com numeração de páginas

### SharedChartComponents.cs
Componentes de gráficos reutilizáveis:
- `VerticalBarChart()` - Gráfico de barras verticais
- `HorizontalBars()` - Gráfico de barras horizontais
- `SectionCard()` - Card para seções de conteúdo

---

## 🎨 Padrões & Técnicas Utilizadas

### 1. **Layout Estruturado**
```csharp
container.Page(page =>
{
    page.Size(PageSizes.A4);
    page.Margin(40);
    
    page.Header().Element(ComposeHeader);
    page.Content().Element(ComposeContent);
    page.Footer().Element(ComposeFooter);
});
```

### 2. **Cards para KPIs**
```csharp
container.Background(SurfaceColor)
    .Border(1)
    .BorderColor(BorderColor)
    .Padding(14)
    .Column(col =>
    {
        col.Item().Text(title).Bold().FontSize(11);
        col.Item().PaddingTop(8).Text(value).FontSize(20).Bold().FontColor(accent);
    });
```

### 3. **Tabelas**
```csharp
container.Table(table =>
{
    table.ColumnsDefinition(cols =>
    {
        cols.RelativeColumn();
        cols.RelativeColumn();
        cols.ConstantColumn(100);
    });
    
    table.Header(header => { /* ... */ });
    
    foreach (var item in items)
    {
        table.Cell().Text(item.Name);
        table.Cell().Text(item.Value);
        table.Cell().AlignRight().Text(item.Amount);
    }
});
```

### 4. **Gráficos com Barras**
```csharp
foreach (var bar in bars)
{
    var ratio = bar.Value / maxValue;
    row.RelativeItem()
        .Column(col =>
        {
            col.Item().AlignCenter().Text(bar.Label);
            col.Item().Height(barHeight).Background(bar.Color);
        });
}
```

### 5. **Cores e Styling**
```csharp
private static readonly Color PrimaryColor = Color.FromHex("#0f4c81");
private static readonly Color GreenColor = Color.FromHex("#137333");
private static readonly Color RedColor = Color.FromHex("#c5221f");
private static readonly Color LightBg = Color.FromHex("#f1f3f4");
```

---

## 📖 Como Usar Estas Referências

### Para Desenvolvedores
1. Leia os templates de referência para entender estrutura
2. Copie e adapte as estruturas para seus templates
3. Use `SharedReportLayout` e `SharedChartComponents` como base
4. Mantenha a mesma estrutura (IDocument, GetMetadata, Compose)

### Para Product/Design
1. Veja os exemplos para entender capacidades
2. Adapte cores, fontes e layouts
3. Considerações de print vs digital
4. Otimize para os formatos (PDF, Excel)

---

## 🚀 Templates para Criar (Sprint B1-B4)

### Core Templates (Must-Have)
- [ ] InvoiceDocument (baseado no exemplo)
- [ ] FinancialReportDocument (baseado no exemplo)
- [ ] SimpleBulletReport (template minimalista)

### Extended Templates (Nice-to-Have)
- [ ] ProductCatalogDocument
- [ ] SalesPerformanceDocument
- [ ] OperationsSnapshotDocument

---

## 📚 Referências QuestPDF

- **Documentação Oficial**: https://www.questpdf.com/
- **Community Discord**: https://discord.gg/questpdf
- **GitHub**: https://github.com/QuestPDF/QuestPDF
- **Examples**: https://github.com/QuestPDF/QuestPDF/tree/main/Examples

---

**Last Updated**: April 21, 2026  
**Templates in Repository**: 5 reference templates included
