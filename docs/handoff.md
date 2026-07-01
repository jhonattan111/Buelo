# Session handoff — current state (read this first)

> Authoritative state doc. Last updated: 2026-07-01.
> Everything below is committed and pushed to `origin/master` on all three repos.

## Latest commits (all on origin/master)

| Repo | Commit |
|---|---|
| BueloApi | `dcf49bc` |
| BueloWeb | `e3d8ea4` |
| umbrella | `876b73f` (+ this handoff bump) |

Checks (all green in CI): **backend 256 xUnit tests (~84% line)**; **frontend 140 unit tests
(~35% stmts) + 2 Playwright E2E**; lint/format, vulnerability scan and CodeQL gates pass on
every push/PR.

## Where the product stands

The declarative engine is done and usable **end-to-end in the editor**; persistence is
**database-backed**; the **full stack runs from a single `docker compose`** (validated live). The last
push was a full **infrastructure pass**: security automation, quality gates, container CD, and E2E.

### Authoring & rendering
- **Declarative YAML** is the primary path: `*.report.yml` → `POST /api/report/render-declarative`
  (PDF/Excel), format + data source from Report Settings. **C# (`IDocument`)** is the escape hatch.
- **Modules / `import:` work from the editor.** When a report `import:`s modules, the editor gathers
  the workspace's `*.{styles,component,theme,formats,lib,validator}.yml` and sends them in `Modules`
  (`reportStore.renderDeclarativeWithSettings` → `workspaceService.listModuleDefinitions`). The gather
  scans the **whole workspace** by name, so modules resolve regardless of folder.
- **YAML autocomplete/validation** works via monaco-yaml + the API JSON Schemas (named model URIs,
  `monaco-editor` pinned to **0.54.0** — see constraints).

### Persistence (database-backed)
- **`Buelo.Persistence`** (Contracts + EF only, **no Roslyn**) holds `BueloDbContext`, the EF stores,
  and the SQLite migrations. **`Buelo.Persistence.Postgres`** holds the Npgsql migrations.
- **All durable content is in the DB:** definitions, editor workspace, C# templates (+ versions),
  global artefacts, render log. EF stores are singletons over `IDbContextFactory<BueloDbContext>`.
- **Provider switch** `Buelo:Database:Provider` = `sqlite` (default, `buelo.db`) | `postgres`
  (`Buelo:Database:ConnectionString`). Both ship migrations; `EnsureBueloDatabase()` `Migrate()`s the
  active provider, else `EnsureCreated()`. **Postgres validated live** (real `__EFMigrationsHistory`).
- **First-run seeding** imports the shipped `definitions/` examples (idempotent, `_system/seeded`).

### Editor UX
- **Explicit save** — **Ctrl/Cmd+S** saves the active file (suppresses the browser dialog); beforeunload
  guard warns on unsaved edits. **Dirty dot** derived from a saved baseline. **Close confirm** on dirty tabs.
- **Tabs:** middle-click closes; the strip **wraps to rows** (VS-style). **Drag-to-reorder** tabs; an
  **open-editors "⌄" overflow menu** (lucide ChevronDown; jump to any tab + **Save all**); **Save all**
  via **Ctrl/Cmd+K S**.

### Onboarding (first-run showcase)
- First-run modal (`buelo.onboarded` flag) **and** an always-available **"Load examples"** button on the
  empty-workspace tree. **Each report gets its own folder** `examples/<name>/` (report + data colocated;
  `statement` ships its `letterhead.component.yml`, `letter` ships `helpers.csx`).
- Examples: `invoice`, `employees` (groupBy+sum), `dashboard` (cards/row/panel), `sales` (→ Excel preset),
  `statement` (import/use/with an external layout), `letter.cs` (C#).

### Deploy (Docker, self-host) — VALIDATED LIVE
- Turnkey **PostgreSQL + API + web** via umbrella [`docker-compose.yml`](../docker-compose.yml). Two ways:
  - **Build from source:** `cp .env.example .env` (set `POSTGRES_PASSWORD`) → `docker compose up -d --build`.
  - **Pull prebuilt (no build):** `docker compose pull && docker compose up -d` — images from
    `ghcr.io/jhonattan111/buelo-{api,web}` (`:edge` on master, `:x.y.z` on tags). Pin via `IMAGE_TAG`.
  - → open `http://localhost:${WEB_PORT:-8080}`.
- The **web (nginx) proxies** `/api`, `/ping`, `/health` to the API → browser is same-origin (no CORS);
  the built SPA calls relative `/api`. API migrates + seeds on first boot.
- **Verified live with Docker (2026-07-01):** images build, all three containers become healthy,
  Postgres `InitialCreate` migration applies, and `render-stored/invoice` returns a valid PDF through
  the proxy. The GHCR `pull && up` path was also run end-to-end.

### Testing, CI/CD & security (infra pass — 2026-07-01)
- **Unit tests:** backend 256 xUnit (~84% line / 68% branch, coverlet — the controller layer was
  raised from 34% to 79%); frontend 140 Vitest (`src/**/*.test.ts`, happy-dom, ~35% stmts / 23%
  branch — up from ~20%/9%; services ~93%, stores ~82%, composables ~62%). Monaco-coupled `.vue`
  are excluded from coverage.
- **E2E:** **Playwright** (`BueloWeb/e2e/`, `pnpm test:e2e`). `playwright.config.ts` starts **both**
  servers (the API from `../BueloApi/Buelo.Api` + the Vite dev server) and drives Chromium: (1) create
  showcase → open report → Render → assert a PDF blob iframe; (2) overflow menu → Save all. A dedicated
  `e2e.yml` job checks out **both repos side by side** and runs it in CI.
- **Quality gates:** frontend **ESLint** (flat config, eslint-plugin-vue + @vue/eslint-config-typescript)
  + **Prettier** (scoped to code; docs/workflows/YAML excluded) — `pnpm lint` / `format:check` gate CI.
  Backend **.editorconfig** + `dotnet format --verify-no-changes` gate CI.
- **Security:** **Dependabot** (npm/nuget + github-actions, weekly), **CodeQL** (csharp + js/ts), and
  **vulnerability gates** in CI (`dotnet list package --vulnerable` fails on High/Critical; `pnpm audit
  --prod --audit-level high`). Two high-severity transitive advisories were fixed (see constraints).
- **CD:** each submodule's `docker-publish.yml` builds + pushes its image to GHCR on master/tag.
- **Dependabot auto-merge:** `dependabot-auto-merge.yml` enables auto-merge on patch/minor + github-actions
  PRs; **branch protection** on BueloApi/BueloWeb master requires the CI (and E2E, web) checks with
  `enforce_admins=false` (owner still pushes directly). Major library bumps stay manual.

## Constraints / gotchas (don't trip on these)

- **Do NOT bump `monaco-editor` past 0.54.x** — `monaco-worker-manager@2.0.1` (a monaco-yaml dep) still
  calls monaco's removed `createWebWorker`, so 0.55 breaks the YAML worker. Blocked **upstream**;
  Dependabot ignores `monaco-editor >= 0.55`. Revisit only when monaco-yaml ships a fixed worker-manager.
- **Monaco workers are native Vite `?worker`** (`src/lib/monaco/workerSetup.ts`); the abandoned
  `vite-plugin-monaco-editor` is gone. **`path-browserify` is aliased to a vendored ESM shim**
  (`src/lib/monaco/path-browserify.js`): Vite dev serves *module* workers without CJS→ESM interop for the
  worker graph, so monaco-yaml's CJS `path-browserify` threw `module is not defined` and killed YAML
  tooling. The alias fixes it. **Vite is now on major 8** (bumped 6→7→8; E2E confirms the native worker
  works on 8 incl. dev).
- **`Microsoft.OpenApi` is pinned to 2.x** (`Buelo.Api.csproj`, currently 2.9.0): AspNetCore.OpenApi
  10.0.9 references the 2.x API and **3.x breaks the build**. Dependabot ignores its major. Also
  **`SQLitePCLRaw.bundle_e_sqlite3` is pinned to 3.0.3** in `Buelo.Persistence` (EF's transitive 2.1.11
  had GHSA-2m69-gcr7-jv3q). Drop both pins once the frameworks reference patched versions transitively.
- **Commit & push policy:** green checks → commit + push, then bump the umbrella pointer. See umbrella
  `CLAUDE.md`. Direct pushes to protected submodule master work because `enforce_admins=false`.

## Open / optional (pick up here)

Closed this session (2026-07-01):
- **`lucide-vue-next` → `@lucide/vue`** — migrated across all 12 icon-importing components
  (BueloWeb `bfc3154`). Drop-in replacement, same API.
- **Frontend unit coverage raised** ~20% → ~35% stmts (BueloWeb `b037942`) — added/extended tests
  for `useWorkspaceTree`, `reportService`/`templateService`/`workspaceService`, `reportStore`,
  `lib/utils`, `PreviewPanel`. **Coverage gates are still not enforced in CI** (measured only) —
  the remaining 0%-covered surface is almost entirely presentational `.vue` (`FileTreePanel`,
  `SidebarTemplates`, `AppLayout`, dialogs, etc.); the E2E suite covers their integration, but
  component tests could push unit coverage further if desired.
- **Umbrella branch protection — set.** `master` on `jhonattan111/Buelo` now blocks force-push +
  deletion (`enforce_admins=false`, matching the submodules' convention). No required status
  checks — the umbrella has no CI to check against.
- **Docs repagination** across all three repos. New `## Documentation conventions` in the umbrella
  `CLAUDE.md` (README = human entry point, CLAUDE.md = agent canonical/wins on conflict, `docs/` =
  deep reference + historical sprint log, obsolete docs get deleted not accumulated). BueloApi's
  declarative-YAML reference is now `docs/reference/` — one file per component (report/blocks/
  expressions/modules/page-settings), each re-verified against the current code; the 7 docs left over
  from the April-2026 DSL→QuestPDF migration (some actively wrong — referenced removed `IReport`/
  `TemplateMode.Sections`) plus the root `ARCHITECTURE.md` are deleted. New `docs/workflows.md` (how
  to add an endpoint/block/stdlib fn/store/migration). All 42 sprint files (18 BueloApi + 24 BueloWeb)
  standardized onto one Goal/Status/Dependencies/Scope/Notes template; both `sprint-history.md`
  index files had their stale "DSL removed, pure C#" framing corrected. The umbrella's
  `blueprint-schema-canonico.md` is trimmed (components/validators design moved to
  `BueloApi/docs/reference/modules.md`) and banner-marked as historical/implemented.

Nothing else outstanding; next open items will land here as they come up.

## How to run

```powershell
./dev.ps1            # API on 5238 + Web on 5173 (separate windows)
```
`pnpm` lives in `C:\Users\jhona\AppData\Local\pnpm` (set `PNPM_HOME` + PATH). E2E: `pnpm test:e2e` from
`BueloWeb` (starts both servers). Full stack: `docker compose up -d --build` (or `pull`) → `:8080`.
Re-trigger onboarding by clearing the `buelo.onboarded` localStorage flag or using "Load examples".
