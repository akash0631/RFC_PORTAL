# Claude Code Setup — V2 Retail Devs

One-time setup so your local Claude Code has the same SAP / DataV2 / Snowflake / GitHub tooling that Akash uses.

---

## What you get

After setup, your Claude Code session can:

- Build SAP RFCs / FMs / Programs / Tables / TRs (no SAP GUI needed for most builds)
- Read any DataV2 SQL Server table + Snowflake table
- Query Cloudflare Workers + GitHub repos
- Use shared rules in `CLAUDE.md` so your Claude follows V2 conventions automatically (one-TR rule, AFABE filter, no `@DATA` in standalone REPORTs, etc.)

Every call you make is logged to `V2RETAIL.BRONZE.MCP_AUDIT_LOG` under your identity (`X-MCP-User`), so Akash can see who did what.

---

## Step 1 — Install Claude Code

```powershell
npm install -g @anthropic-ai/claude-code
```

Verify:
```powershell
claude --version
```

---

## Step 2 — Get your credentials from Akash

DM Akash on Telegram and ask for:

1. `V2_MCP_API_KEY` — shared API key for `universal-mcp.akash-bab.workers.dev`
2. Your `V2_MCP_USER` identity string (e.g. `nikhil@v2retail`)

---

## Step 3 — Set environment variables

**Windows PowerShell (persistent, user-level):**

```powershell
[Environment]::SetEnvironmentVariable("V2_MCP_API_KEY", "<paste-key-here>", "User")
[Environment]::SetEnvironmentVariable("V2_MCP_USER", "nikhil@v2retail", "User")
```

Close + reopen PowerShell after this so the new vars load.

Verify:
```powershell
echo $env:V2_MCP_API_KEY
echo $env:V2_MCP_USER
```

---

## Step 4 — Clone the repo

```powershell
cd C:\Users\<your-username>\source\repos
git clone https://github.com/akash0631/v2-claude-dev-kit.git
# or: git clone https://github.com/akash0631/RFC_PORTAL.git
```

`.mcp.json` and `CLAUDE.md` are already committed at the repo root.

---

## Step 5 — Launch Claude Code

```powershell
cd v2-claude-dev-kit
claude
```

First launch will prompt:
> Approve MCP server `v2-universal-mcp` for this project? (y/n)

Type `y`. Claude Code reads `.mcp.json`, substitutes `${V2_MCP_API_KEY}` and `${V2_MCP_USER}` from your env vars, and connects.

Verify in Claude:
> "list MCP tools"

You should see ~78 tools including `sap_dispatcher`, `sap_build_rfc`, `datav2_list_tables`, `sf_query`, `github_file`, `cf_workers_all`, etc.

---

## Step 6 — First test

In Claude Code:

> Call `get_all_context` and show me a summary.

Expected: live tool registry with credential map for SAP DEV/QA/PROD, DataV2, Snowflake.

---

## How the config works

**`.mcp.json`** (committed):
```json
{
  "mcpServers": {
    "v2-universal-mcp": {
      "type": "http",
      "url": "https://universal-mcp.akash-bab.workers.dev/",
      "headers": {
        "X-API-Key": "${V2_MCP_API_KEY}",
        "X-MCP-User": "${V2_MCP_USER}"
      }
    }
  }
}
```

- `X-API-Key` — gate token, same for everyone, kept in your env vars (never committed)
- `X-MCP-User` — your identity, written into `MCP_AUDIT_LOG` for every call

**`CLAUDE.md`** (committed): rules that auto-load every session — critical V2 conventions (one TR per development, AFABE=01 filter, wrapper subrc lies, etc.).

---

## Rules of engagement

Read `CLAUDE.md` once before doing SAP work. Most painful gotchas are listed there with reasons.

Hard rules:
- **Never deploy RFC to .46 — always .174**
- **One development = one TR** (call `CREATE_TR` first, pass `dev_tr` everywhere)
- **Never use `@DATA` / `@host_vars` in standalone REPORTs** — they dump on SE38 run
- **Always verify wrapper return values** — `ok:true` lies
- **Use SAP-specific skills**: `sap-deploy-fm`, `sap-lint`, `sap-release-tr`, `sap-test-fm`

---

## Troubleshooting

**"MCP server not connecting"**
- Check `$env:V2_MCP_API_KEY` is set in the SAME shell that launched `claude`
- Reopen PowerShell after `SetEnvironmentVariable` calls

**"Tool returns 401"**
- API key wrong or expired — DM Akash

**"Tool returns 500 with `DATA_BUFFER_EXCEEDED`"**
- `RFC_READ_TABLE` field list too wide — split fields into chunks ≤500 bytes cumulative

**"My calls don't show up in `MCP_AUDIT_LOG`"**
- Check `X-MCP-User` env var is set
- Audit is fire-and-forget — may take 5-10 sec to land in Snowflake

---

## Questions

Telegram Akash. Don't open GitHub issues for setup questions.
