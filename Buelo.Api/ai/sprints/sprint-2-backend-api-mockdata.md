# Sprint B2: Report API & Mock Data Flow

## 🎯 Objetivo
Implementar o fluxo completo de renderização: dados JSON → template C# → PDF usando QuestPDF puro, com dados mockados para validação.

## ✅ Tarefas

### Backend

#### 1. Template Storage com MockData
- [ ] Criar alguns templates de exemplo com MockData:
  - Invoice template com dados ficcionais
  - Financial Dashboard com KPIs
  - Operations Snapshot com métricas
- [ ] Verificar que MockData está sendo persistido corretamente
- [ ] Implementar validação de MockData contra formato esperado

#### 2. ReportController Endpoints
- [x] POST /api/report/validate - validação de template
- [ ] POST /api/report/render - renderização com dados mockados
  - Receber: Template, Data, PageSettings
  - Retornar: PDF bytes
- [ ] GET /api/report/templates - listar templates
- [ ] GET /api/report/templates/{id} - obter template específico

#### 3. C# Template Validation
- [ ] Verificar compilação de template (sem Roslyn dinamicamente por enquanto)
- [ ] Retornar erros de sintaxe claros
- [ ] Validar presença de IDocument interface

### Frontend

#### 1. Report Editor Interface
- [ ] Criar abas: Editor, Preview, Settings
- [ ] Monaco Editor com C# syntax highlighting
- [ ] Preview panel mostrando resultado em tempo real
- [ ] Validação on-keystroke

#### 2. Report Settings Panel
- [ ] Form para configurar:
  - Data source (JSON selector)
  - Page size
  - Margins
  - Colors
- [ ] Preview dos settings aplicados

#### 3. Mock Data Integration
- [ ] Carregar MockData do template
- [ ] Permitir editar MockData inline
- [ ] Atualizar preview automaticamente

## 📊 Exemplo de Fluxo
```
1. User escreve template C# (implements IDocument)
2. User clica "Preview"
3. Frontend valida template via /api/report/validate
4. Frontend envia Template + MockData para /api/report/render
5. Backend renderiza e retorna PDF bytes
6. Frontend exibe PDF preview
```

## 🚀 Próximo Sprint
Sprint B3: Global Artefacts & Data Sources (dados reais)
