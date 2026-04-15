# WORKFLOWS.md — Buelo Backend

## 1. Workflow de execução por agentes

1. Ler `TASKS.md` e escolher o próximo sprint `[ ] pending` respeitando a cadeia de dependências
2. Ler o arquivo de sprint escolhido em `ai/sprints/`
3. Confirmar que todas as dependências listadas na seção `Dependencies` estão marcadas como `done`
4. Implementar somente o escopo definido no sprint — não antecipar tarefas de sprints futuros
5. Rodar `dotnet build` e `dotnet test` — ambos devem passar com zero erros
6. Marcar todas as tasks do sprint como `[x]` no arquivo de sprint
7. Atualizar o status do sprint para `[x] done` em `ai/TASKS.md`

---

## 2. Workflow de implementação de um sprint

### Ordem padrão por camada

```
1. Buelo.Contracts   ← novos modelos, interfaces, enums
2. Buelo.Engine      ← lógica de negócio, parsers, store
3. Buelo.Api         ← endpoints, controllers
4. Buelo.Tests       ← testes unitários cobrindo cada camada
```

Sempre nessa ordem — nunca referenciar `Buelo.Engine` a partir de `Buelo.Contracts`.

---

## 3. Workflow de adição de um novo parser/componente do engine

1. Criar o arquivo em `Buelo.Engine/`
2. Se expor tipos novos: adicionar models/interfaces em `Buelo.Contracts/` primeiro
3. Injetar no `TemplateEngine` via construtor ou chamada direta (sem DI interna no engine)
4. Registrar no contêiner em `EngineExtensions.cs` se necessário
5. Cobrir com testes em `Buelo.Tests/Engine/`

---

## 4. Workflow de adição de um novo endpoint

1. Definir o contrato de request/response (records em `Buelo.Contracts` se reutilizáveis, ou records locais no controller se exclusivos)
2. Adicionar o método no controller com o atributo de rota correto
3. Chamar `TemplateEngine` ou `ITemplateStore` via injeção (primary constructor)
4. Retornar tipos explícitos (`Ok(result)`, `NotFound()`, `BadRequest(msg)`) — sem retornar `IActionResult` genérico sem necessidade
5. Testar em `Buelo.Tests/Api/` com pelo menos: happy path, not found, bad input

---

## 5. Workflow de adição de uma nova `ITemplateStore`

1. Criar a classe em `Buelo.Engine/` implementando `ITemplateStore`
2. Adicionar método de extensão de registro em `EngineExtensions.cs` (ex: `AddBueloFileSystemStore()`)
3. NÃO registrar como padrão — `InMemoryTemplateStore` é o default; a nova impl é opt-in via `appsettings` ou chamada explícita no `Program.cs`
4. Cobrir com round-trip tests em `Buelo.Tests/Engine/`

---

## 6. Workflow de deprecação de um modo de template

1. Adicionar `[Obsolete("mensagem")]` no valor do enum em `Buelo.Contracts/TemplateMode.cs`
2. NÃO remover o valor — manter suporte de runtime
3. NÃO lançar exceção ao processar o modo obsoleto — apenas executar normalmente
4. Documentar na seção `## Template Modes` do `SKILLS.md`
5. O frontend trata a deprecação visualmente — o backend apenas avisa em tempo de compilação via `[Obsolete]`

---

## 7. Workflow de validação de template (endpoint /validate)

```
POST /api/report/validate
  body: { template, mode }
      ↓
  TemplateHeaderParser.Parse(source)
      ↓
  SectionsTemplateParser.Parse(stripped)
      ↓
  WrapSectionsTemplateAsync(...)
      ↓
  CSharpScript.Create(...).GetDiagnostics()   ← compilar sem executar
      ↓
  mapear linha: diagnosticLine - wrapperLineOffset = userLine
      ↓
  retornar { valid, errors[] }    ← sempre HTTP 200
```

Nunca lançar exceção not-handled neste endpoint — capturar tudo e retornar `valid: false`.

---

## 8. Workflow de versionamento em SaveAsync

```
SaveAsync(record)
  ↓
  ler versão atual por GetAsync(record.Id)    ← se existir
  ↓
  criar TemplateVersion { Version = atual.Version + 1, Template, Artefacts, SavedAt }
  ↓
  persistir snapshot (memory dict / arquivo versions/{n}.snapshot.json)
  ↓
  sobrescrever registro principal
```

Não versionar quando `Id == Guid.Empty` (create) — versão 1 é o próprio registro inicial.
