# Sprint B4: Multi-Format Output & Advanced Rendering

## 🎯 Objetivo
Estender capacidades de rendering para múltiplos formatos (PDF, Excel) e otimizar performance com cache e compilação em background.

## ✅ Tarefas

### Backend

#### 1. Output Renderer Registry
- [ ] Verificar IOutputRenderer interface
- [ ] Registrar PdfRenderer e ExcelRenderer
- [ ] Adicionar suporte para mais formatos conforme necessário

#### 2. Excel Rendering
- [ ] Implementar ExcelRenderer usando ClosedXML
- [ ] Criar templates Excel equivalentes aos PDFs
- [ ] Tester rendering de dados complexos em Excel

#### 3. Performance Optimization
- [ ] Implementar cache de templates compilados
- [ ] Lazy loading de templates grandes
- [ ] Batch rendering para múltiplos relatórios

#### 4. ReportController Format Support
- [ ] Query parameter `format=pdf|excel` em render endpoint
- [ ] Content-Type correto por formato
- [ ] Filename correto com extensão

### Frontend

#### 1. Export Options
- [ ] Dropdown "Export As" (PDF, Excel)
- [ ] Direct download após click
- [ ] Preview para formato selecionado

#### 2. Template Preview by Format
- [ ] Mostrar preview em PDF vs Excel
- [ ] Diferentes layous por formato
- [ ] Validação de template por formato

## 📊 Supported Output Formats
- PDF (QuestPDF) - ✅ Primary
- Excel (ClosedXML) - Implementation
- HTML (Future)
- CSV (Future)

## 🔄 Render Pipeline
```
Template (C#) 
  ↓ Compile
  ↓ Instantiate IDocument
  ↓ Bind Data
  ↓ Select Renderer (PDF/Excel/etc)
  ↓ Render
  ↓ Return bytes + content-type
```

## ✅ Sprint Completion Criteria
- [ ] PDF rendering funcionando 100%
- [ ] Excel rendering funcionando
- [ ] Format selector no frontend
- [ ] Performance aceitável (<2s para relatório médio)
- [ ] Cache implementado

## 🚀 Future Sprints
- Sprint B5: Template Versioning & History
- Sprint B6: Scheduling & Batch Reports
- Sprint B7: Advanced PageSettings (headers, footers, watermarks)
