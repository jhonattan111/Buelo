# Session handoff — current state (read this first)

> Authoritative state doc. Last updated: 2026-06-30. Replaces the old dated handoffs.
> Everything below is committed and pushed to `origin/master` on all three repos.

## Latest commits (all on origin/master)

| Repo | Commit |
|---|---|
| BueloApi | `06e1e73` |
| BueloWeb | `62cfd2d` |
| umbrella | `8123296` (+ this handoff) |

Checks: **`dotnet test` 203/203 green**; BueloWeb `pnpm typecheck` + `pnpm build` green.

## What this session delivered

The declarative engine was already done. This session made the declarative path **usable end-to-end in the editor**, fixed real bugs, added onboarding, and translated the whole project to English.

- **YAML autocomplete/validation — fixed** (was silently broken). Two blockers: (1) the Monaco model had no named URI, so monaco-yaml's `*.report.yml` fileMatch never bound a schema → `useMonacoEditor` now creates the model with a named URI; (2) monaco-yaml's worker is incompatible with monaco-editor 0.55 (its `monaco-worker-manager@2.0.1` calls the old `createWebWorker({moduleId,...})` API) → **monaco-editor is pinned to `0.54.0`**.
- **Declarative render in the UI** — the Render button now works for `*.report.yml` (`POST /api/report/render-declarative`), format + data source from Report Settings.
- **Editor blank-render bug — fixed.** `AppLayout` renders the editor twice (desktop + mobile `<main>`s); two instances created models with the same named URI and clobbered each other → each instance now gets a unique URI (`file:///buelo-<n>/<path>`, basename preserved for fileMatch) + `editor.layout()` nudges (rAF/timeouts/ResizeObserver) for the 0→sized container.
- **Data source select** — showed blank for the saved value → always renders a fallback `<option>` + refreshes the JSON list when the panel opens.
- **Onboarding** — first-run modal ("Welcome to Buelo", localStorage flag `buelo.onboarded`) creates example reports in `examples/` (invoice, employees, dashboard [card/row/panel], letter [C#] + data + a `.csx` helper), with the data source pre-set so "open + Render" works immediately.
- **New File dialog** — added "Declarative report (.report.yml)"; renamed "Report (.cs)" → "C# report (.cs)".
- **Tree icons** — `*.report.yml` and module YAMLs get their own icon/color (no more blank sheet).
- **AI-friendly declarative reference** — `BueloApi/docs/declarative-format.md` (for delegating report generation to an AI).
- **Full English pass** — all docs, code comments, tests, examples, and the shipped `definitions/` (renamed colaboradores→employees, layoutPadrao→defaultLayout, corporativo→corporate, vendas→sales). Stdlib functions renamed: `moeda→currency`, `telefone→phone`, date formatter `data()→date()`. **Kept** Brazilian/locale tokens: `cpf`, `cnpj`, `cep`, `R$`, `pt-BR`, locale `br`. README mojibake fixed.
- **Commit & push policy** — green checks → commit + push (see umbrella `CLAUDE.md` §Commit & push policy).

## Constraints / gotchas (don't trip on these)

- **Do NOT bump `monaco-editor` past 0.54.x** until `monaco-yaml`/`monaco-worker-manager` support monaco's new `createWebWorker` API — 0.55+ breaks the YAML worker.
- **vite is stuck on major 6** — `vite-plugin-monaco-editor@1.1.0` (abandoned) breaks on vite 7/8. Autocomplete works today via that plugin + the 0.54 pin. Migrating off the plugin to the native `?worker` setup is optional/deferred.
- **Declarative render from the UI is self-contained only** — `import:` of modules isn't passed yet. Next step: gather the workspace's `*.{styles,component,theme,...}.yml` and send them in the `Modules` field of `render-declarative`.

## DONE — persistence is now database-backed (was the open decision)

The user rejected files+git on operational grounds (auto-committing every CRUD makes the app a fragile git client) and chose a **database**, SQLite **or** Postgres. Implemented in full:

- **New `Buelo.Persistence` project** (Contracts + EF only, **no Roslyn**) — this isolation is what unblocks EF migrations: the Design tooling no longer collides with Engine's `CodeAnalysis.CSharp`. `InitialCreate` migration is generated and lives in `Buelo.Persistence/Migrations/`.
- **All durable content is now in the DB by default:** definitions, editor workspace, C# templates (+ version history), global artefacts, render log. EF stores implement the existing Contracts interfaces and are singletons over `IDbContextFactory<BueloDbContext>` (short-lived context per op → no captive dependency in the singleton engine). `AddBueloPersistence(config)` `Replace`s the engine's in-memory/file-system defaults.
- **Provider switch** `Buelo:Database:Provider` = `sqlite` (default, single file `buelo.db`) | `postgres`; connection via `Buelo:Database:ConnectionString`. `EnsureBueloDatabase()` runs `Migrate()` on SQLite, `EnsureCreated()` on Postgres.
- **Durability gap fixed** — templates/artefacts/workspace survive restart (verified live). **Backup story:** SQLite file copy / Litestream, or Postgres `pg_dump`/PITR/managed.
- **First-run seeding** imports the shipped `definitions/` examples into the DB (idempotent, `_system/seeded` marker — deletions don't resurrect). The on-disk `definitions/` is now seed data only.
- **Engine** dropped the EF packages + its `Persistence/` folder; `NullRenderLog` (the no-DB fallback) stays. The `FileSystem*`/`InMemory*` stores remain for tests + `AddBueloFileSystemStore()`.
- **Tests:** +18 EF store tests over a throwaway SQLite db (185 → 203 green).

### Optional follow-ups (not blocking)
- **Postgres migrations:** today Postgres uses `EnsureCreated()` (no schema evolution). Add an Npgsql migration set when the schema first changes for a Postgres deployment.
- **`BueloApi/README.md` is stale** (still describes the removed Sections/IReport/DSL architecture and an old persistence recommendation). CLAUDE.md is canonical; README needs a rewrite pass.
- **Declarative render `import:` modules** from the UI still aren't passed (see autocomplete/render notes above).
- Background task chip exists to untrack `tsconfig.tsbuildinfo` (gitignore it).

## How to run

```powershell
./dev.ps1            # API on 5238 + Web on 5173 (separate windows)
```
`pnpm` lives in `C:\Users\jhona\AppData\Local\pnpm` (set `PNPM_HOME` + PATH). Re-trigger onboarding by clearing the localStorage `buelo.onboarded` flag.
