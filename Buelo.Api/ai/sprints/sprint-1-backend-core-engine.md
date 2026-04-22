# Sprint B1: Core Rendering Engine - QuestPDF Foundation

## 🎯 Objetivo
Estabelecer a base sólida para rendering de relatórios usando QuestPDF puro com C#. Remover completamente a complexidade do DSL Buelo e focar em templates C# IDocument bem estruturados.

## ✅ Tarefas

### Backend

#### 1. TemplateEngine Refactor
- [x] Remover todas as referências a BueloDsl (parser, compiler, engine)
- [x] Atualizar TemplateMode para usar apenas `FullClass`
- [x] Simplificar RenderAsync() para lidar com C# puro
- [ ] Implementar validação básica de templates (IDocument check)
- [ ] Setup para compilação dinâmica com Roslyn (futuro)

#### 2. Contrato de Dados
- [x] Remover TemplatePath e DataSourcePath de ReportRequest
- [x] Simplificar ReportRequest para: Template, FileName, Data, PageSettings
- [x] Atualizar defaults em TemplateRecord e ValidationResult
- [ ] Criar exemplos de modelos de dados simples

#### 3. ReportController Validação
- [ ] Testar POST /api/report/validate com template C# válido
- [ ] Testar POST /api/report/validate com template C# inválido
- [ ] Verificar que erros são retornados corretamente

### Frontend

#### 1. Report Editor UI
- [ ] Atualizar Monaco Editor para C# language highlighting
- [ ] Remover referencias a buelo-language
- [ ] Mostrar modo "C# Template" em vez de "Buelo DSL"

#### 2. Report Settings Panel
- [ ] Implementar página de Report Settings
  - Data Source selector (JSON)
  - Page Size dropdown (A4, Letter, etc)
  - Margin controls (top, right, bottom, left)
  - Orientation toggle (Portrait/Landscape)
  - Background color picker
  - Default font size input

## 📚 Referências de Saída
- Exemplo simples de template QuestPDF: `InvoiceDocument.cs`
- Exemplo com KPIs: `FinancialDashboardDocument.cs`
- Exemplo com tabelas: ver referência de SalesPerformanceDocument

## 🚀 Próximo Sprint
Sprint B2: Report API & Data Flow (mock data rendering)
