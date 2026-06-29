# Buelo

A **report creation** platform. The user writes a template in **C#** (a QuestPDF `IDocument` class), provides data in JSON, and the platform compiles the template at runtime and generates a **PDF** (or **Excel**).

This is the **umbrella** repository: it aggregates the two components as **git submodules**.

| Component | Description | Stack |
|---|---|---|
| **[BueloApi](BueloApi)** | Compilation + render API | ASP.NET Core 10, Roslyn, QuestPDF, ClosedXML |
| **[BueloWeb](BueloWeb)** | Web editor (VS Code-like workspace) | Vue 3, Vite, Monaco, Pinia, Tailwind |

## Getting started

```bash
# clone with the submodules
git clone --recurse-submodules git@github.com:jhonattan111/Buelo.git
cd Buelo

# (if you already cloned without submodules)
git submodule update --init --recursive
```

### Run in development

```powershell
./dev.ps1
```

Or in two terminals:

```bash
# Terminal 1 — API (http://localhost:5238)
dotnet run --project BueloApi/Buelo.Api

# Terminal 2 — Web (http://localhost:5173)
cd BueloWeb
pnpm install
pnpm dev
```

The frontend reads the API URL from `BueloWeb/.env` (`VITE_API_BASE_URL=http://localhost:5238`).

## Structure

```
Buelo/                  ← this repo (docs + submodule pointers)
├── BueloApi/           ← submodule → github.com/jhonattan111/BueloApi
├── BueloWeb/           ← submodule → github.com/jhonattan111/BueloWeb
├── CLAUDE.md           ← guide for AI agents
├── dev.ps1            ← starts API + Web together
└── README.md
```

## Working in the submodules

Each submodule is an **independent** git repository, with its own history and branch. Make commits **inside** each one. Then register the new pointer here:

```bash
git add BueloApi BueloWeb
git commit -m "chore: bump submodules"
```
