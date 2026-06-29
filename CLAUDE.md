# CLAUDE.md — Buelo (umbrella)

**Satellite** repository of the **Buelo** product: a **report creation** platform. Aggregates the two repos that run complementarily, as **git submodules**.

## The two repositories

| Submodule | Role | Stack | Port | Guide |
|---|---|---|---|---|
| [`BueloApi`](BueloApi) | API that compiles C# templates and generates PDF/Excel | ASP.NET Core 10 + Roslyn + QuestPDF | `5238` | [BueloApi/CLAUDE.md](BueloApi/CLAUDE.md) |
| [`BueloWeb`](BueloWeb) | Web editor (VS Code-style workspace) | Vue 3 + Vite + Monaco + Pinia | `5173` | [BueloWeb/CLAUDE.md](BueloWeb/CLAUDE.md) |

**The frontend (`BueloWeb`) consumes the API (`BueloApi`) at `http://localhost:5238`.** The API only allows CORS for `http://localhost:5173`.

## Product concept

The user writes a **C# template** (a class implementing `QuestPDF.Infrastructure.IDocument`) in the Monaco editor, provides data (JSON), and the API compiles that code at runtime with Roslyn and returns a **PDF** (or **Excel**). There is no custom DSL — it's pure C# + QuestPDF.

## How to navigate (for agents)

- **Backend work** → go into `BueloApi/` and follow [`BueloApi/CLAUDE.md`](BueloApi/CLAUDE.md).
- **Frontend work** → go into `BueloWeb/` and follow [`BueloWeb/CLAUDE.md`](BueloWeb/CLAUDE.md).
- Each submodule is an independent git repo with its own history, branch, and remote. Commits and PRs happen **inside** the submodule, not here.
- This umbrella repo versions only: docs (`CLAUDE.md`, `README.md`), `dev.ps1`, and the commit **pointers** of the submodules (`.gitmodules`).

## Submodules — comandos essenciais

```bash
# clone everything at once
git clone --recurse-submodules <url-of-this-repo>

# if you already cloned without submodules
git submodule update --init --recursive

# update the submodules to the latest commit of their branches
git submodule update --remote --merge

# after advancing a submodule, register the new pointer here
git add BueloApi BueloWeb && git commit -m "chore: bump submodules"
```

## Commit & push policy

**Whenever the repo's checks/tests pass (green), commit the changes and push** — don't accumulate local work. If any test/check fails, do **not** commit or push: fix it first.

Submodule flow (commits/PRs happen **inside** the submodule):
1. In the changed submodule, run the checks (`BueloApi`: `dotnet build` + `dotnet test`; `BueloWeb`: `pnpm typecheck` + `pnpm build`). Green → `git commit` + `git push`.
2. In the umbrella, `git add <submodule>` (bump the pointer) + `git commit` + `git push`.

Commits go straight to each repo's `master` (this project's current convention).

## Run both together (dev)

```powershell
./dev.ps1          # starts API (5238) + Web (5173) in separate windows
```

Or manually, in two terminals:

```bash
dotnet run --project BueloApi/Buelo.Api     # terminal 1 → http://localhost:5238
cd BueloWeb && pnpm install && pnpm dev      # terminal 2 → http://localhost:5173
```

## AI conventions in this product

- Each repo has its own `CLAUDE.md` as the source of truth; `docs/` in each one keeps a history of sprints (reference, not current state).
- The AI config was migrated from Copilot (`ai/*.instructions.md` with `applyTo`) to the Claude Code standard (`CLAUDE.md` + `.claude/`).
