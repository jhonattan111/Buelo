# CLAUDE.md — Buelo (guarda-chuva)

Repositório **satélite** do produto **Buelo**: uma plataforma de **criação de relatórios**. Agrega os dois repos que rodam de forma complementar, como **git submodules**.

## Os dois repositórios

| Submodule | Papel | Stack | Porta | Guia |
|---|---|---|---|---|
| [`BueloApi`](BueloApi) | API que compila templates C# e gera PDF/Excel | ASP.NET Core 10 + Roslyn + QuestPDF | `5238` | [BueloApi/CLAUDE.md](BueloApi/CLAUDE.md) |
| [`BueloWeb`](BueloWeb) | Editor web (workspace estilo VS Code) | Vue 3 + Vite + Monaco + Pinia | `5173` | [BueloWeb/CLAUDE.md](BueloWeb/CLAUDE.md) |

**O front (`BueloWeb`) consome a API (`BueloApi`) em `http://localhost:5238`.** A API libera CORS só para `http://localhost:5173`.

## Conceito do produto

O usuário escreve um **template C#** (uma classe que implementa `QuestPDF.Infrastructure.IDocument`) no editor Monaco, fornece dados (JSON), e a API compila esse código em runtime com Roslyn e devolve um **PDF** (ou **Excel**). Não há DSL própria — é C# puro + QuestPDF.

## Como navegar (para agentes)

- **Trabalho no backend** → entre em `BueloApi/` e siga [`BueloApi/CLAUDE.md`](BueloApi/CLAUDE.md).
- **Trabalho no frontend** → entre em `BueloWeb/` e siga [`BueloWeb/CLAUDE.md`](BueloWeb/CLAUDE.md).
- Cada submodule é um repo git independente com seu próprio histórico, branch e remote. Commits e PRs acontecem **dentro** do submodule, não aqui.
- Este repo guarda-chuva versiona apenas: docs (`CLAUDE.md`, `README.md`), `dev.ps1` e os **ponteiros** de commit dos submodules (`.gitmodules`).

## Submodules — comandos essenciais

```bash
# clonar tudo de uma vez
git clone --recurse-submodules <url-deste-repo>

# se já clonou sem submodules
git submodule update --init --recursive

# atualizar os submodules para o último commit dos seus branches
git submodule update --remote --merge

# após avançar um submodule, registre o novo ponteiro aqui
git add BueloApi BueloWeb && git commit -m "chore: bump submodules"
```

## Rodar os dois juntos (dev)

```powershell
./dev.ps1          # sobe API (5238) + Web (5173) em janelas separadas
```

Ou manualmente, em dois terminais:

```bash
dotnet run --project BueloApi/Buelo.Api     # terminal 1 → http://localhost:5238
cd BueloWeb && pnpm install && pnpm dev      # terminal 2 → http://localhost:5173
```

## Convenções de IA neste produto

- Cada repo tem seu `CLAUDE.md` como fonte de verdade; `docs/` em cada um guarda histórico de sprints (referência, não estado atual).
- A config de IA foi migrada de Copilot (`ai/*.instructions.md` com `applyTo`) para o padrão Claude Code (`CLAUDE.md` + `.claude/`).
