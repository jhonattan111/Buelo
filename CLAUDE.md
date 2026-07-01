# CLAUDE.md — Buelo (umbrella)

**Satellite** repository of the **Buelo** product: a **report creation** platform. Aggregates the two repos that run complementarily, as **git submodules**.

## The two repositories

| Submodule | Role | Stack | Port | Guide |
|---|---|---|---|---|
| [`BueloApi`](BueloApi) | API that compiles C# templates and generates PDF/Excel | ASP.NET Core 10 + Roslyn + QuestPDF | `5238` | [BueloApi/CLAUDE.md](BueloApi/CLAUDE.md) |
| [`BueloWeb`](BueloWeb) | Web editor (VS Code-style workspace) | Vue 3 + Vite + Monaco + Pinia | `5173` | [BueloWeb/CLAUDE.md](BueloWeb/CLAUDE.md) |

**The frontend (`BueloWeb`) consumes the API (`BueloApi`) at `http://localhost:5238`.** The API only allows CORS for `http://localhost:5173`.

## Product concept

Two authoring paths, both rendered with QuestPDF. **Declarative YAML** (`*.report.yml`) is the primary
path — no code compilation, lowered to a typed IR and composed by QuestPDF/ClosedXML; see
[`BueloApi/docs/reference/`](BueloApi/docs/reference/). **C# `IDocument`** is the full-power escape
hatch — the user writes a class implementing `QuestPDF.Infrastructure.IDocument`, and the API compiles
it at runtime with Roslyn. Either way, data is provided as JSON and the API returns a **PDF** (or
**Excel**).

## How to navigate (for agents)

- **Backend work** → go into `BueloApi/` and follow [`BueloApi/CLAUDE.md`](BueloApi/CLAUDE.md).
- **Frontend work** → go into `BueloWeb/` and follow [`BueloWeb/CLAUDE.md`](BueloWeb/CLAUDE.md).
- Each submodule is an independent git repo with its own history, branch, and remote. Commits and PRs happen **inside** the submodule, not here.
- This umbrella repo versions only: docs (`CLAUDE.md`, `README.md`), `dev.ps1`, and the commit **pointers** of the submodules (`.gitmodules`).

## Documentation conventions

Applies to this repo and both submodules — where new content goes:

- **`README.md`** — human-facing entry point. What the project is, stack, quick start (install/run),
  primary usage examples, links out. No AI-agent process instructions.
- **`CLAUDE.md`** — the canonical engineering doc per repo, for agents. Architecture, conventions,
  "how to do X", commands, config, commit policy. **Wins on conflict** with anything in `docs/`.
- **`docs/`** — supporting material that doesn't fit either of the above: reference material too deep
  for `CLAUDE.md` (organized by component/topic, e.g. `BueloApi/docs/reference/`) and the sprint history
  (standardized format, explicitly historical — not current state). **Obsolete docs are deleted, not
  accumulated** — git history is the record; don't leave contradictory old docs lying around. A
  point-in-time design doc earns its keep only as long as it stays accurate — once its content has
  fully migrated to a maintained reference, delete it rather than keeping a second, drifting copy.

`docs/handoff.md` is the exception: it's the living "current state" doc for the whole product (updated
every session), read alongside each repo's `CLAUDE.md`.

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

## Deploy (Docker, self-host)

Turnkey stack — **PostgreSQL + API + web editor** — via [`docker-compose.yml`](docker-compose.yml):

```bash
cp .env.example .env          # set POSTGRES_PASSWORD
docker compose up -d --build  # builds the API + web images, starts Postgres
# open http://localhost:8080
```

- The **web** (nginx) serves the SPA and **proxies** `/api`, `/ping`, `/health` to the **API**, so the
  browser uses a single origin (no CORS). The **API** migrates + seeds the database on first boot.
- **Postgres** is the provider here (`Buelo__Database__Provider=postgres`); data persists in the
  `buelo-pgdata` volume — back up with `pg_dump`. (Validated live against a real Postgres.)
- **Health:** API exposes `/ping` (liveness) and `/health` (readiness — checks the DB). Compose waits for
  Postgres healthy before the API, then health-checks the API.
- Images: [`BueloApi/Dockerfile`](BueloApi/Dockerfile) (ASP.NET publish) and
  [`BueloWeb/Dockerfile`](BueloWeb/Dockerfile) (Vite build → nginx). Config via env in `.env`
  (template: [`.env.example`](.env.example)); secrets stay out of git.

## AI conventions in this product

- Each repo has its own `CLAUDE.md` as the source of truth — see [Documentation conventions](#documentation-conventions) above.
- The AI config was migrated from Copilot (`ai/*.instructions.md` with `applyTo`) to the Claude Code standard (`CLAUDE.md` + `.claude/`).
- This repo's own doc: [`docs/handoff.md`](docs/handoff.md) (current state, read first). The
  declarative engine's design doc (`docs/blueprint-schema-canonico.md`) was deleted once its content
  fully migrated to the maintained [`BueloApi/docs/reference/`](BueloApi/docs/reference/) — see it in
  git history if the original design rationale is ever needed.
