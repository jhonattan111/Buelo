# Buelo

Plataforma de **criação de relatórios**. O usuário escreve um template em **C#** (uma classe QuestPDF `IDocument`), fornece dados em JSON, e a plataforma compila o template em runtime e gera um **PDF** (ou **Excel**).

Este é o repositório **guarda-chuva**: agrega os dois componentes como **git submodules**.

| Componente | Descrição | Stack |
|---|---|---|
| **[BueloApi](BueloApi)** | API de compilação + render | ASP.NET Core 10, Roslyn, QuestPDF, ClosedXML |
| **[BueloWeb](BueloWeb)** | Editor web (workspace VS Code-like) | Vue 3, Vite, Monaco, Pinia, Tailwind |

## Começando

```bash
# clonar com os submodules
git clone --recurse-submodules git@github.com:jhonattan111/Buelo.git
cd Buelo

# (se já clonou sem submodules)
git submodule update --init --recursive
```

### Rodar em desenvolvimento

```powershell
./dev.ps1
```

Ou em dois terminais:

```bash
# Terminal 1 — API (http://localhost:5238)
dotnet run --project BueloApi/Buelo.Api

# Terminal 2 — Web (http://localhost:5173)
cd BueloWeb
pnpm install
pnpm dev
```

O front lê a URL da API de `BueloWeb/.env` (`VITE_API_BASE_URL=http://localhost:5238`).

## Estrutura

```
Buelo/                  ← este repo (docs + ponteiros de submodule)
├── BueloApi/           ← submodule → github.com/jhonattan111/BueloApi
├── BueloWeb/           ← submodule → github.com/jhonattan111/BueloWeb
├── CLAUDE.md           ← guia para agentes de IA
├── dev.ps1            ← sobe API + Web juntos
└── README.md
```

## Trabalhando nos submodules

Cada submodule é um repositório git **independente**, com seu próprio histórico e branch. Faça commits **dentro** de cada um. Depois, registre o novo ponteiro aqui:

```bash
git add BueloApi BueloWeb
git commit -m "chore: bump submodules"
```
