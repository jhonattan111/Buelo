# Sprint B3: Global Artefacts & Data Sources

## 🎯 Objetivo
Implementar sistema de Global Artefacts para armazenar e gerenciar fontes de dados JSON, permitindo que templates referenciem dados externos de forma centralizada.

## ✅ Tarefas

### Backend

#### 1. GlobalArtefactStore Enhancement
- [ ] Implementar armazenamento persistente de artefatos globais
- [ ] Permitir criar/atualizar/deletar artefatos
- [ ] Suportar artefatos JSON como data sources

#### 2. GlobalArtefactsController
- [ ] GET /api/artefacts - listar todos
- [ ] GET /api/artefacts/{id} - obter específico
- [ ] POST /api/artefacts - criar novo
- [ ] PUT /api/artefacts/{id} - atualizar
- [ ] DELETE /api/artefacts/{id} - deletar

#### 3. Data Binding
- [ ] Permitir que template referencie Global Artefacts por ID
- [ ] Implementar resolução de referências em RenderAsync()
- [ ] Validar que artefato existe antes de render

#### 4. Environment-Specific Data
- [ ] Suportar override de data sources por ambiente
- [ ] Development vs Production data sources
- [ ] Environment configuration em appsettings

### Frontend

#### 1. Data Sources Panel
- [ ] Listar Global Artefacts disponíveis
- [ ] Editor inline para JSON data sources
- [ ] Carregar/salvar data sources
- [ ] Validar JSON syntax

#### 2. Template Data Binding
- [ ] Permitir selecionar data source para template
- [ ] Dropdown com artefatos disponíveis
- [ ] Preview com data source selecionada

#### 3. Artefact Manager
- [ ] Interface para criar/editar artefatos globais
- [ ] Upload de JSON files
- [ ] Teste de artefato antes de salvar

## 🗂️ Exemplo de Estrutura
```
GlobalArtefacts/
├── invoices.json (lista de faturas)
├── products.json (catálogo de produtos)
├── employees.json (dados de colaboradores)
└── financial-2024.json (dados financeiros)
```

## 📋 ReportRequest com Data Binding
```csharp
new ReportRequest {
    Template = "...", // C# template code
    Data = new { GlobalArtefactId = "invoices.json", Filter = "status=pending" }
}
```

## 🚀 Próximo Sprint
Sprint B4: Multi-Format Output (PDF, Excel, etc)
