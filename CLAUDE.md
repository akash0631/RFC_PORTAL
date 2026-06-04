# Claude Code — V2 Retail Dev Rules

Project-level instructions. Auto-loaded by Claude Code on session start. Applies to every developer working in this repo.

---

## MCP Access

V2 Universal MCP server: `https://universal-mcp.akash-bab.workers.dev/`

All tooling routed through it. Config in `.mcp.json` at repo root. Auth headers required:
- `X-API-Key` — shared (issued by Akash; never commit to git)
- `X-MCP-User` — your identity (e.g. `nikhil@v2retail`); shows up in `V2RETAIL.BRONZE.MCP_AUDIT_LOG` for every tool call

See `README-claude-setup.md` for first-time setup.

Call `get_all_context` for the live tool + credential registry.

---

## Critical Rules (NEVER violate)

### SAP
1. **ONE development = ONE TR.** Before any SAP build, call `sap_dispatcher action=CREATE_TR` FIRST with a SPECIFIC `IM_TR_TEXT` of format `<OBJECT_NAME>: <purpose w/ key params>`. Pass the returned TR as `dev_tr` to EVERY subsequent `sap_build_rfc`, `sap_rpy_create_fm`, and `sap_dispatcher` call. End with `sap_tr_manifest` to verify all objects landed in one TR.
2. **Never deploy RFC to .46** — always .174 (Dev).
3. **One TR = one development type.** Helper wrappers / infra utilities built mid-session go to their own infra TR, never sweep into the active dev TR.
4. **TR description must be specific.** Generic `Claude AI Studio: ...` is forbidden. Always set `IM_TR_TEXT`.
5. **Wrapper subrc=0 lies.** `Z_CLAUDE_TR_RELEASE` / `Z_CLAUDE_TABL_ACTIVATE` / dispatcher `CREATE_TABLE` return `ok:true` even when SAP op fails async. Always verify via direct read: `E070.TRSTATUS=R`, `DD02L.AS4LOCAL=A`, etc.
6. **Object presence ≠ runnable.** TRDIR row + dispatcher "active" status do NOT mean object runs. Always SE38-EXECUTE or SUBMIT a REPORT before declaring ship-ready. RFC wrapper smoke is NOT a substitute (wrapper FG has FIXPT=X, standalone REPORT has FIXPT=' ' — `@DATA` / `@host_vars` will dump on standalone).
7. **FI-AA reads need `AFABE = '01'` filter.** Every ANLB / ANLC SELECT for book WDV/depreciation terms MUST filter `AFABE = '01'`. V2 assets carry rows in multiple areas (01 book, 15 tax, 30 derived).
8. **SAP TX-code max 20 chars.** TSTCT.TTEXT cut at 37 chars. E07T.AS4TEXT cut at 60 chars.
9. **`sap_rpy_create_fm` ignores TABLES interface.** Use STRING/JSON pipe-delim params.
10. **`abap_read_source` MCP = FMs only.** For REPORTs use dispatcher `READ_PROG`.

### Data
1. **Never DISTINCT on DataV2** — use GROUP BY.
2. **SALE_V only** (never TAX_V).
3. **Store status = 'ACT'** (not 'Active').
4. **WITH(NOLOCK)** on all S28 queries.
5. **`RFC_READ_TABLE DELIMITER='|'`** collides with `|` in stored fields → use `^` delimiter.
6. **MARD blind to LGORT 0099** (V2-synthetic in-transit, DataV2 only, NOT in SAP T001L/MARD).
7. **Snowflake `ET_STOCK_DATA` stale since 2026-05-13** — trust DataV2.dbo.ET_STOCK_DATA, NOT Snowflake. Always `SELECT MAX(STOCK_DATE)` before quoting SF stock.

### Misc
1. **Never use rupee symbol** — use "Rs".
2. **Snowflake URL params:** `schemaName=` triggers spurious "Multiple SQL statements" error — use 2-part naming.

---

## Build Speed Targets

- Trivial REPORT: 2-3 min
- Similar WM/MM REPORT: 5-10 min
- New domain: 10-30 min
- Complex: 20-45 min
- Floor per iteration: ~30 sec (edit→deploy→VBS→read)

Quote 5-10 min for similar future requests.

---

## SAP 7-Phase Deployment Protocol

Use the `sap-deploy-fm` skill for any new ABAP function module — Plan → Pre-check → Write → Lint → Deploy → Test → Audit. Use `sap-lint` for standalone checks. Use `sap-release-tr` for DEV→QA / QA→PROD transports with human approval gate.

## Recovery Wrappers (Infra)

- `Z_CLAUDE_TR_OBJ_PURGE` — clear E071 + TLOCK both (use over E071-only).
- `Z_CLAUDE_TR_SET_TEXT` — relabel TR description (E07T row update).
- `Z_CLAUDE_E071_DEL` — E071 row delete only (INCOMPLETE — prefer TR_OBJ_PURGE).
- `Z_CLAUDE_TABL_ACTIVATE` — DDIF_TABL_ACTIVATE wrapper. Does NOT guarantee AS4LOCAL flip — verify after.

## TR Release Diagnostic Triangle

Export error 8 = check 3 things:
1. **E071** object list
2. **TLOCK** object lock (survives E071 delete — silent killer)
3. **DDIC AS4LOCAL='A'** for TABL/DTEL (inactive table = no active version to ship)

## STMS Import (Headless)

Driver: `~/claude/scripts/stms_import_robust.vbs <TRKORR>`. Auto-ticks "Ignore Invalid Component Version" in Options tab to bypass S/4 component mismatch.

`TMSBUFFER.CVERSFLG = '-'` blocks auto-import. Manual: STMS_IMPORT → row → **Ctrl+F11 (vKey 35, NOT 33)** → Options → tick `chkPARMS-IGN_CVER` → Enter → Yes.

---

## Tool Reference

### SAP build
- `sap_dispatcher` — CREATE_TR, CREATE_FG, CREATE_PROG, READ_PROG, CREATE_TABLE, Z_DEV_TXN_CREATE
- `sap_build_rfc` — build RFC FM (classic syntax only — no `@DATA`, no `@host_vars`)
- `sap_rpy_create_fm` — create FM (no TABLES support; use STRING params)
- `sap_read_table` — generic RFC_READ_TABLE any env (`^` delimiter)
- `sap_tr_manifest` — verify TR contents
- `sap_verify_fm` — verify FM compile + active

### DataV2 + Snowflake
- `datav2_list_tables`, `datav2_describe_table`, `v2_sql_query`
- `sf_query`, `sf_list_tables`, `sf_describe_table`
- `datav2_vs_sf` — paired diff on key_cols

### GitHub + Cloudflare
- `github_file`, `github_commits`, `github_akash_repos`, `github_repos`
- `cf_workers_all`, `cf_worker_check`, `cf_worker_crons`

---

## Code Style

- Default to writing no comments. Only comment WHY when non-obvious.
- Don't add error handling / fallbacks / validation for scenarios that can't happen.
- Don't add features, refactor, or abstractions beyond what the task requires.
- Three similar lines is better than a premature abstraction.

## Git

- Create NEW commits, never amend.
- Never `--no-verify`, never bypass hooks.
- Commit only when explicitly asked.
