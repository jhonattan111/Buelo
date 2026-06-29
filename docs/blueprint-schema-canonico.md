# Blueprint — Schema Canônico do Buelo Declarativo

> Documento de **design** (sem código de implementação). Define o modelo declarativo do Buelo, o IR `BueloDocument` que ele produz, e como tudo se encaixa na arquitetura **engine × recipe × extension** (inspirada no jsreport, com QuestPDF como recipe).
>
> Status: **draft para discussão** · Data: 2026-06-26 · Escopo: engine declarativo (o engine C# é paralelo, ver §10).

---

## 1. Enquadramento

O Buelo separa três responsabilidades ortogonais (modelo jsreport):

- **Engine** — transforma *linguagem de autoria + dados* num documento. O Buelo tem dois: o **interpretador declarativo** (YAML/JSON → `BueloDocument`) e o **compilador C#** (Roslyn → `IDocument`).
- **Recipe** — transforma o documento em bytes. Já existe: `OutputRendererRegistry` → `PdfRenderer` (QuestPDF), `ExcelRenderer` (ClosedXML).
- **Extension** — plugins registráveis (helpers, validadores, recipes novos). Self-hosted ⇒ código custom é confiável.

Este blueprint especifica o **engine declarativo** e o **IR** que ele emite.

### Pipeline

```
arquivos .yml (report + imports)
  → parse + resolução de imports/símbolos
  → avaliação de expressões e diretivas (forEach/if/groupBy) contra os dados
  → BueloDocument (IR tipado, sem expressões pendentes)
  → recipe (QuestPDF / ClosedXML)
  → bytes (PDF / XLSX)
```

**Princípio:** o `BueloDocument` é o contrato entre engine e recipe. Qualquer engine que produza um `BueloDocument` válido renderiza em qualquer recipe. Expressões e diretivas **não** chegam ao recipe — são resolvidas antes.

---

## 2. Modelo de arquivos (`kind`)

Todo arquivo declara um `kind` no topo. Um produto = um conjunto desses arquivos (no template local e/ou no registro global de artefatos).

| `kind` | Papel | Renderável? |
|---|---|---|
| `report` | Um relatório completo | ✅ |
| `component` | Fragmento de layout reusável (params + slots) | via `use` |
| `styles` | Classes de estilo nomeadas | importável |
| `formats` | Máscaras/formatos nomeados | importável |
| `lib` | Expressões puras nomeadas (campos calculados) | importável |
| `validator` | Regras de validação declarativas | importável |
| `theme` | Bundle de `styles` + `page settings` + paleta | importável |

**Resolução de nomes:** artefato **local do template primeiro**, depois **registro global**. Colisão → erro de compilação com a origem de cada definição.

**Versionamento:** `use: layoutPadrao@2` (pin opcional). Sem pin = última versão.

---

## 3. Anatomia de um `report`

```yaml
kind: report
name: fatura

meta:
  engine: declarative          # declarative | csharp
  recipe: pdf                  # pdf | excel
  page: { size: A4, margin: 2cm, orientation: portrait }

import:                        # bibliotecas que este report usa
  - styles: corporativo
  - formats: br
  - component: layoutPadrao

data:
  source: fatura.json          # binding de dados (arquivo, ou injetado via API)

use: layoutPadrao              # opcional: envolve o report num componente de layout
with:
  nomeRelatorio: "Fatura #{{ data.numero }}"

content:                       # blocos do corpo (preenche o slot do layoutPadrao)
  - text: { value: "Itens", class: titulo }
  - table: { ... }             # ver §5
  - if: "{{ data.comentarios }}"
    card: { class: nota }
    content:
      - markdown: "{{ data.comentarios }}"
```

`meta` carrega o que hoje vive em `TemplateRecord` (engine, recipe/`OutputFormat`, `PageSettings`). `use`/`with` é o mecanismo de layout-padrão (§7).

---

## 4. Vocabulário de layout (blocos)

Cada bloco mapeia para um nó do IR e, daí, para uma chamada QuestPDF. Lista núcleo (v1):

| Bloco | Descrição | QuestPDF (recipe) |
|---|---|---|
| `text` | Texto simples ou com runs/spans | `.Text(...)` |
| `markdown` / `html` | Conteúdo rico (subset → runs) | spans/elementos |
| `row` | Itens lado a lado | `.Row(...)` |
| `column` | Itens empilhados | `.Column(...)` |
| `table` | Tabela orientada a dados | `.Table(...)` |
| `image` | Imagem (url/base64/artefato) | `.Image(...)` |
| `card` / `panel` | Container com borda/fundo/padding | `.Background().Border().Padding()` |
| `chart` | Gráfico curado (bar/line/pie) | composição de primitivas |
| `spacer` | Espaço vertical | `.PaddingVertical(...)` |
| `line` / `divider` | Régua/separador | `.LineHorizontal(...)` |
| `pageBreak` | Quebra de página | `.PageBreak()` |

### Bandas (estrutura de página)

`page` tem três slots, no espírito banded (Stimulsoft/jsreport):

```yaml
page:
  header:  [ ... ]    # repete no topo de cada página
  content: [ ... ]    # fluxo principal
  footer:  [ ... ]    # repete no rodapé (page/pageCount disponíveis)
```

### Sizing

`width`/`height` aceitam: `*` (relativo, peso 1), `3*` (peso 3), `120px`, `40%`, `2cm`. Mapeiam para `RelativeColumn`/`ConstantColumn`/etc.

### Estilo

Três formas, combináveis (precedência: inline > class > theme):

```yaml
text:
  value: "Relatório"
  class: titulo                              # de um `kind: styles`
  style: { color: "#c00", bold: true }       # override inline
```

---

## 5. Tabela (orientada a dados)

```yaml
table:
  data: data.itens                # array a iterar
  rowStyle: { borderBottom: "1px #DDD", paddingY: 5 }
  columns:
    - { width: 25px, header: "#",        cell: "{{ index + 1 }}" }
    - { width: 3*,   header: "Produto",  cell: "{{ item.nome }}" }
    - { width: 1*,   header: "Unitário", cell: "{{ item.preco | moeda }}", class: monetario }
    - { width: 1*,   header: "Qtd",      cell: "{{ item.qtd }}", align: right }
    - { width: 1*,   header: "Total",    cell: "{{ moeda(item.preco * item.qtd) }}", class: monetario }
  footer:
    - { span: 4, text: "Total", style: { bold: true, align: right } }
    - { text: "{{ moeda(sum(data.itens, 'preco * qtd')) }}", class: monetario }
```

`cell` é uma expressão avaliada por linha; `item`, `index`, `first`, `last` são variáveis de contexto da linha.

### Agrupamento

```yaml
table:
  data: data.colaboradores
  groupBy: departamento
  group:
    header: { text: "{{ group.key }}", style: { bold: true, background: "#E8E8E8" } }
    footer: { text: "Subtotal: {{ moeda(sum(group.items, 'salario')) }}", class: monetario }
  columns: [ ... ]
```

---

## 6. Linguagem de expressão `{{ }}` (deliberadamente simples)

**Princípio (a linha anti-inner-platform):** expressões são **puras, de uma só linha, determinísticas, sem efeito colateral**. Não há `for`/`if` imperativo, definição de função, nem estado. Controle de fluxo vive em **diretivas** (`forEach`/`if`/`groupBy`). Algoritmo de verdade vive em **extension C#** (self-hosted ⇒ barato e seguro). Essa fronteira é o que impede o YAML de virar uma linguagem ruim.

### Acesso a dados
`data.campo`, `data.lista[0].x`, e no escopo de linha/grupo: `item`, `index`, `first`, `last`, `group.key`, `group.items`.

### Variáveis de contexto
`now`, `today`, `page`, `pageCount`, `report.name`.

### Operadores
aritméticos `+ - * / %`, comparação `== != < <= > >=`, lógicos `&& || !`, ternário `cond ? a : b`, null-coalescing `??`.

### Biblioteca padrão (stdlib do motor — estende `IHelperRegistry`)
- **Agregação:** `sum(lista, 'expr')`, `avg`, `count`, `min`, `max`
- **Formato (BR de fábrica):** `moeda(x)`, `data(x, 'dd/MM/yyyy')`, `cnpj(x)`, `cpf(x)`, `cep(x)`, `telefone(x)`, `percent(x)`
- **String:** `upper`, `lower`, `trim`, `join(lista, sep)`, `len`, `mask(x, '##.###')`, `digits(x)`
- **Lógica:** `if(cond, a, b)`, `coalesce(...)`

### Pipes (açúcar)
`{{ data.cnpj | cnpj }}` ≡ `{{ cnpj(data.cnpj) }}`; encadeável: `{{ data.valor | moeda }}`.

### Expressões nomeadas reutilizáveis (`kind: lib`)
```yaml
kind: lib
name: vendas
expr:
  precoFinal: "{{ price * (1 - desconto) }}"
  margemPct:  "{{ (receita - custo) / receita * 100 }}"
```
Uso: `{{ vendas.precoFinal }}` (com `item`/`data` no escopo).

---

## 7. Componentes (params + slots)

Mecanismo de reúso. Substitui herança OOP (a `LayoutPadrao` abstrata de hoje) por **composição**.

```yaml
kind: component
name: layoutPadrao
params:
  nomeRelatorio: { type: string }
  nomeEmpresa:   { type: string, default: "Contar Consultoria e Contabilidade" }
slots: [content]
body:
  page:
    size: A4
    margin: 24
    header: { use: cabecalhoPadrao, empresa: "{{ nomeEmpresa }}", relatorio: "{{ nomeRelatorio }}" }
    content: { slot: content }            # ponto de injeção do report
    footer:  { use: rodapePadrao, empresa: "{{ nomeEmpresa }}" }
```

- **Escopo (decisão recomendada):** params explícitos + contexto ambiente **mínimo** (`now`, `page`, `pageCount`). O componente **não** enxerga `data` do report a menos que recebido via `with`. Higiene = reúso seguro.
- **Slots:** um ou mais pontos nomeados; o report preenche via `content:` (slot default) ou `slots: { nome: [...] }`.
- **Aninhamento:** componentes usam componentes (`cabecalhoPadrao` acima).

---

## 8. Validadores (a escada de extensibilidade)

```yaml
kind: validator
name: cpf
# Degrau 1 — declarativo (formato + checksum comum)
format: "###.###.###-##"
rules:
  - { digits: 11 }
  - { checksum: { scheme: mod11, weights: [10,9,8,7,6,5,4,3,2] } }
```

```yaml
kind: validator
name: steuerId
# Degrau 2 — expressão pura (check-digit que cabe em reduce)
expr: "{{ len(digits(id)) == 11 && checkDigit(digits(id)) == last(digits(id)) }}"
params: [id]
```

```yaml
kind: validator
name: steuerIdComplexo
# Degrau 3 — referência a extension C# registrada (self-hosted)
ref: HelpersAlemaes.ValidarSteuerId
```

Uso em dados/campos: `validate: cpf` numa coluna ou binding. Degraus 1–2 são seguros de compartilhar no registro global; degrau 3 (código) entra só por registro local/extension.

---

## 9. O IR — `BueloDocument`

O alvo de *lowering*. Modelo tipado, **sem expressões pendentes** (tudo já avaliado), **sem imports** (tudo já resolvido), **sem estilos por nome** (tudo já achatado). É o que o recipe consome.

Taxonomia de nós (esboço):

```
BueloDocument
  Meta { page, recipe, fonts }
  Page { header: Node[], content: Node[], footer: Node[] }

Node (abstrato) — todos carregam Style resolvido:
  TextNode      { runs: Run[] }                 Run { text, style }
  RowNode       { items: { size, node }[] }
  ColumnNode    { items: Node[], spacing }
  TableNode     { columns: Col[], rows: Cell[][], groups?: Group[], footer?: Cell[] }
  ImageNode     { source, fit }
  ContainerNode { kind: card|panel, children: Node[] }   // borda/fundo/padding no Style
  ChartNode     { type, series, axis }
  ChartNode/LineNode/SpacerNode/PageBreakNode ...

Style (resolvido) { font, size, bold, italic, color, bg, align, padding, margin, border, width, height }
```

### Mapeamento construto → IR → QuestPDF (prova de que desce)

| Declarativo | Nó IR | QuestPDF |
|---|---|---|
| `page.header` | `Page.header` | `page.Header()` |
| `text` / `markdown` | `TextNode` (runs) | `.Text(t => t.Span(...))` |
| `row` | `RowNode` | `.Row(r => r.RelativeItem()/.ConstantItem())` |
| `column` | `ColumnNode` | `.Column(c => c.Item())` |
| `table` (+forEach/groupBy resolvido) | `TableNode` | `.Table(...)` + `.Header()` |
| `card` | `ContainerNode` | `.Background().Border().Padding()` |
| `image` | `ImageNode` | `.Image(...)` |
| `{{ page }}` | já em `Run` dinâmica | `CurrentPageNumber()` |

> O mesmo IR alimenta o recipe Excel (ClosedXML): `TableNode` vira planilha, `TextNode` vira célula. Recipes que não suportam um nó (ex.: `chart` no Excel) degradam de forma definida.

---

## 10. Relação com o engine C# (resumo)

Fora do escopo deste blueprint, mas para situar:

- **C# "raw IDocument"** — poder total do QuestPDF, vai direto pro recipe PDF (não passa pelo IR). É o escape hatch.
- **Eject** — gera, a partir de um `BueloDocument`, um `IDocument` C# equivalente. Caminho de graduação declarativo → código.
- O IR é o ponto de convergência do caminho **declarativo**; o raw-C# converge no próprio QuestPDF. Trade-off aceito e consciente.

---

## 11. JSON Schema → IntelliSense de graça

Cada `kind` ganha um **JSON Schema**. No front, `monaco-yaml` + esse schema dão autocomplete, validação inline e hover **sem language server** — resolvendo o objetivo #1 (editor fluido) para a superfície declarativa, que é onde ele é barato de entregar.

---

## 12. Decisões em aberto (precisam de você)

> **Status 2026-06-28:** quase tudo aqui foi **decidido e implementado**. (1) YAML only ✅ · (2) markdown
> lidera ✅ (bloco `markdown`; `html`-subset não feito) · (3) stdlib v1 entregue ✅ · (4) pin **opcional**
> (advisory — `StripPin` remove `@versão`, não valida) · (5) escopo de componente = params + ambiente
> mínimo ✅ · (6) chart = v2 (fora). Restam só pontas: `validate:` em coluna durante render, recipe Excel
> do IR, e o `html`-subset — ver handoff.

1. **Serialização canônica:** ✅ **decidido (2026-06-28) — YAML only** para as definições (autoria). Os **dados** que alimentam o report continuam **JSON** (vêm da API). Um parser só na fatia vertical: YamlDotNet. JSON-de-definição pode entrar depois se a API precisar, mas não é v1.
2. **Conteúdo rico:** `markdown` lidera, `html`-subset secundário. Confirmar.
3. **Riqueza da stdlib v1:** quais funções entram já (sugestão: agregação + formatos BR + string básica). Definir a lista de corte.
4. **Pin de versão:** default última vs pin obrigatório em produção. Recomendo pin opcional.
5. **Escopo de componente:** params explícitos + ambiente mínimo (recomendado) vs acesso ambiente a `data`.
6. **Chart:** ✅ **decidido — v2.** Fica fora da fatia vertical; entra depois que os reports básicos estiverem funcionando.

---

## 13. Persistência (decidido 2026-06-26, fork resolvido 2026-06-28)

**Decisão: tudo plugável, mas por DOIS EIXOS diferentes** — porque definições e dados operacionais têm paradigmas distintos.

| Domínio | Abstração | Default dev | Default prod | Outros providers |
|---|---|---|---|---|
| **Definições** | `ITemplateStore` (custom, **já existe** no código) | **fs (git)** | fs (git) *ou* Postgres | InMemory (testes), SQLite |
| **Operacional** | **EF Core `DbContext`** (provider por config) | **SQLite** | **PostgreSQL** | InMemory (testes) |

> **Por que eixos diferentes?** Definições têm um backend (fs/git) que **não é banco** — ele dá diff/PR/review de graça, coisa que DB nenhum entrega. Por isso precisam de uma interface própria (`ITemplateStore`, que já existe com impls InMemory + FileSystem). Operacional é tudo relacional ⇒ **o próprio EF Core já é a abstração**: troca `UseSqlite()`↔`UseNpgsql()` por config, **um único modelo de entidades**, sem interface extra.

### Definições — `ITemplateStore` plugável (fork resolvido)

Provider escolhido por config. **Default fs-store** (arquivos `.yml` em disco): versionamento por git de graça, legível, modelo jsreport, reaproveita `FileSystemWorkspaceStore`/`FileSystemTemplateStore` que **já existem**. Providers SQLite/Postgres (definição como blob jsonb/text + metadados) ficam disponíveis pela mesma interface para quem preferir store único. Nenhuma decisão irreversível: a abstração já está no código.

### Operacional — EF Core, provider por config

**SQLite default no dev** (arquivo ou `:memory:`, **zero infra** — sobe a API sem container), **PostgreSQL default em prod** (concorrência de escrita sob carga + multi-nó com banco compartilhado). Mesmo modelo de entidades nos dois.

- O ganho do Postgres é concorrência de escrita e escala horizontal — **não** volume (SQLite aguenta muito numa instância única).
- **Schema provider-agnóstico:** colunas normais; campo JSON (ex.: `parameters`) guardado como texto/jsonb via value-converter que funciona nos dois providers.
- **Ressalva — migrations são por-provider:** mantém-se um set de migrations p/ SQLite e outro p/ Postgres (ou `EnsureCreated` no dev SQLite). Custo pequeno e conhecido.

### Perfis dos dois domínios

- **Definições** (`report`, `component`, `styles`, `formats`, `lib`, `validator`, `theme`): documento, versionado, read-heavy, poucos registros.
- **Dados operacionais** (logs de emissão, login/auditoria, histórico de render, parâmetros): append-heavy, consultado/agregado.

### Config de instância ≠ definição

Connection string do banco, API keys e settings de auth vivem em **env/secrets** (ovo-e-galinha: não podem morar no banco que inicializam). "Arquivo de configuração de report" (theme/page/parâmetros) **é** uma definição e segue a regra acima.

### Sequência de implementação
**Definições não bloqueiam a fatia vertical** — a abstração (`ITemplateStore` / `FileSystemWorkspaceStore`) já existe. A persistência operacional (EF Core) é hardening e entra depois.

1. **Fatia vertical declarativa primeiro** (não depende de DbContext): YAML → interpretador → `BueloDocument` → recipe QuestPDF → PDF, lendo a definição do fs-store que já existe.
2. **Operacional depois (hardening):** `DbContext` EF + entidades operacionais → SQLite default (dev, zero infra) → provider Postgres (prod) → tabelas de logs/auth/audit/render-history.
