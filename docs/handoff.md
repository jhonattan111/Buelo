# Session handoff — current state (read this first)

> Authoritative state doc. Last updated: 2026-06-30.
> Everything below is committed and pushed to `origin/master` on all three repos.

## Latest commits (all on origin/master)

| Repo | Commit |
|---|---|
| BueloApi | `9ce9169` |
| BueloWeb | `019cd18` |
| umbrella | `ca3cef2` (+ this handoff bump) |

Checks: **backend `dotnet test` 203/203 green (coverage ~77% line)**; frontend `pnpm typecheck` +
`pnpm build` + `pnpm test:run` green (10 tests).

## Where the product stands

The declarative engine is done and the declarative path is **usable end-to-end in the editor**.
Persistence is **database-backed**. Recent work focused on editor UX, validation, onboarding, and tabs.

### Authoring & rendering
- **Declarative YAML** is the primary path: `*.report.yml` → `POST /api/report/render-declarative`
  (PDF/Excel), format + data source from Report Settings. **C# (`IDocument`)** is the escape hatch.
- **Modules / `import:` work from the editor.** When a report `import:`s modules, the editor gathers
  the workspace's `*.{styles,component,theme,formats,lib,validator}.yml` and sends them in `Modules`
  (`reportStore.renderDeclarativeWithSettings` → `workspaceService.listModuleDefinitions`). Verified
  end-to-end.
- **YAML autocomplete/validation** works via monaco-yaml + the API JSON Schemas (named model URIs,
  `monaco-editor` pinned to **0.54.0** — see constraints).

### Persistence (database-backed)
- **`Buelo.Persistence`** project (Contracts + EF only, **no Roslyn**) holds the `BueloDbContext`, the
  EF stores, and the SQLite migrations. **`Buelo.Persistence.Postgres`** holds the Npgsql migrations.
- **All durable content is in the DB:** definitions, editor workspace, C# templates (+ versions),
  global artefacts, render log. EF stores are singletons over `IDbContextFactory<BueloDbContext>`.
- **Provider switch** `Buelo:Database:Provider` = `sqlite` (default, `buelo.db`) | `postgres`
  (`Buelo:Database:ConnectionString`). Both ship migrations (picked via `MigrationsAssembly`);
  `EnsureBueloDatabase()` `Migrate()`s the active provider, else `EnsureCreated()`.
- **First-run seeding** imports the shipped `definitions/` examples (idempotent, `_system/seeded`
  marker). **Backup:** SQLite file copy / Litestream, or Postgres `pg_dump`/PITR/managed.

### Editor UX
- **Explicit save** — auto-save removed. **Ctrl/Cmd+S** saves the active file (suppresses the browser
  "save page" dialog); a beforeunload guard warns before leaving with unsaved edits.
- **Dirty indicator** (orange dot) is derived from a saved baseline vs live content — no more false
  positives on freshly-opened files.
- **Close confirm** — closing a tab with unsaved changes prompts (Cancel / Don't save / Save).
- **Tabs:** middle-click closes a tab (Chrome-like, still prompts if dirty); the tab strip **wraps to
  multiple rows** (VS-style, `max-h-32` then vertical scroll) instead of an overflow-x scrollbar that
  pushed the Render/Validate buttons.
- **Validation noise removed:** the backend no longer emits a "No validator available" warning for
  unsupported extensions; YAML is validated client-side via monaco-yaml markers (real errors show in
  the status bar and the bottom-bar click jumps to them).

### Onboarding (first-run showcase)
- First-run modal (localStorage flag `buelo.onboarded`) **and** an always-available **"Load examples"**
  button in the empty-workspace file tree (independent of the flag). Creates files in `examples/`.
- Examples: `invoice`, `employees` (groupBy+sum), `dashboard` (cards/row/panel), **`sales`
  (tabular → Excel output, preset)**, **`statement` importing `letterhead.component.yml` (external
  layout via import/use/with)**, `letter.cs` (C#), plus per-report data and a `.csx` helper.

### Deploy (Docker, self-host) — NEW
- Turnkey **PostgreSQL + API + web** via umbrella [`docker-compose.yml`](../docker-compose.yml):
  `cp .env.example .env` (set `POSTGRES_PASSWORD`) → `docker compose up -d --build` → `http://localhost:8080`.
- `BueloApi/Dockerfile` (publish → aspnet, ships `definitions/` for seeding) and `BueloWeb/Dockerfile`
  (Vite build → nginx). The **web proxies** `/api`, `/ping`, `/health` to the API → browser is
  same-origin (no CORS). API migrates + seeds on first boot.
- New: **CORS configurable** (`Buelo:Cors:Origins`), **`/health`** readiness probe (DB check) alongside
  `/ping`. pnpm pinned via `package.json` `packageManager`.
- **Not container-built here** (no Docker on the dev box) — Dockerfiles/compose written to standard
  patterns; the API code changes (CORS-from-config, `/health`) and the compose YAML were verified.

### Testing & CI
- **Backend:** xUnit, 203 tests. Coverage via `dotnet test --collect:"XPlat Code Coverage"
  --settings coverlet.runsettings` (Cobertura); `coverlet.runsettings` excludes Program, EF
  migrations, the Postgres migrations assembly, and `[ExcludeFromCodeCoverage]`. ~77% line / 63% branch.
- **Frontend:** Vitest + @vue/test-utils (happy-dom), `src/**/*.test.ts`; `pnpm test` / `test:run` /
  `test:coverage` (v8). First tests cover the dirty-baseline logic, module gathering, and the status
  bar. Coverage is low (~9%) — a starting point; Monaco-coupled files are excluded (need the editor).
- **CI:** GitHub Actions in **each submodule** (`.github/workflows/ci.yml`) — they are separate repos,
  so CI runs where the code is. Backend: build + test + coverage. Frontend: typecheck + build + test +
  coverage. No coverage gate yet (measure first). The umbrella only tracks pointers — no CI.

### Docs (canonical)
- `BueloApi/CLAUDE.md` (backend) and `BueloWeb/CLAUDE.md` (frontend) are the source of truth.
  `BueloApi/README.md` is rewritten for the current architecture; `BueloApi/docs/declarative-format.md`
  is the AI-friendly format reference. Whole project is in English.

## Constraints / gotchas (don't trip on these)

- **Do NOT bump `monaco-editor` past 0.54.x** — **re-verified live 2026-06-30** with the *latest*
  `monaco-editor@0.55.1` + `monaco-yaml@5.5.1`: the YAML worker still fails with
  `Cannot use 'in' operator to search for 'then' in undefined` because `monaco-worker-manager@2.0.1`
  (a monaco-yaml dep, unchanged) still calls monaco's old `createWebWorker` API that 0.55 removed.
  This breaks identically under **both** the plugin and the native `?worker` setup. Blocked **upstream** —
  revisit when monaco-yaml updates monaco-worker-manager for monaco 0.55+.
- **vite is stuck on major 6** — `vite-plugin-monaco-editor@1.1.0` (abandoned, patched for Node compat)
  breaks on vite 7/8 **and** can't drive monaco 0.55. The native `?worker` migration was attempted and
  **reverted**: on monaco 0.54 it fails in Vite **dev** with `module is not defined` (workers fall back
  to the main thread → YAML validation dead); `worker.format:'es'` + `optimizeDeps` include/exclude
  didn't help. **Important nuance (verified 2026-06-30):** the native `?worker` **build is clean** —
  `pnpm build` emits proper ESM worker bundles (editor/json/yaml) with no top-level `module` refs, so
  it's *production-viable*. The wall is **Vite 6 dev** specifically (it serves the workers' CJS deps
  un-prebundled). A newer **Vite (7/8)** likely fixes the dev worker handling — but that requires
  dropping the plugin in the same move (the plugin breaks on vite 7/8). So the realistic native path is
  a combined "drop plugin + bump Vite + native workers" change (speculative, needs browser verification).
  Net: both pins (monaco 0.54, vite 6) are currently load-bearing. A *maintained alternative* Vite-monaco
  plugin (keeping monaco 0.54) could also unstick the vite pin — untried.
- **Commit & push policy:** green checks → commit + push, then bump the umbrella pointer. See umbrella
  `CLAUDE.md`.

## Open / optional (pick up here)

- **Postgres path — VALIDATED live (2026-06-30).** Against a real Postgres server, startup applied the
  Npgsql `InitialCreate` migration (real `__EFMigrationsHistory`, not `EnsureCreated`), `Migrate()`
  created the database, seeding ran, and workspace + render-log read/write round-tripped (render-history
  returned the logged event). Provider via `Buelo:Database:Provider=postgres` +
  `Buelo:Database:ConnectionString` (env: `Buelo__Database__Provider` / `Buelo__Database__ConnectionString`).
- **Optional editor polish (ideas, not committed work):** tab overflow "⌄" menu like VS, drag-to-reorder
  tabs, "Save all" (Ctrl+K S).
- **Monaco stack upgrade / plugin migration — BLOCKED upstream (investigated 2026-06-30).** See the
  Constraints note: monaco 0.55 + monaco-yaml break (worker-manager not updated), native `?worker`
  breaks on 0.54 in dev. Reverted; nothing committed. Re-attempt when monaco-yaml ships a fixed
  monaco-worker-manager, or try a maintained alternative Vite-monaco plugin to unstick *just* the vite pin.

## How to run

```powershell
./dev.ps1            # API on 5238 + Web on 5173 (separate windows)
```
`pnpm` lives in `C:\Users\jhona\AppData\Local\pnpm` (set `PNPM_HOME` + PATH). Re-trigger onboarding by
clearing the localStorage `buelo.onboarded` flag, or use the "Load examples" button on an empty workspace.
