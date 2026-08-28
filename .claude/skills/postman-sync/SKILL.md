---
name: postman-sync
description: Syncs new or changed NWC FSMS API endpoints to the Postman collection MK.FormEngine via the Postman MCP server. Ask the user before using Postman MCP unless they already requested a Postman sync.
---

# Postman Sync

## Target

| Setting | Value |
|---------|-------|
| Collection | `MK.FormEngine` |
| Route prefix | `/api/v1/` |
| MCP server | `postman` (configured in `.cursor/mcp.json`) |

## When to apply

After every new or changed API endpoint in `src/WebApps/MK.FormEngine.API/`, **ask the user** whether to sync Postman. Run this skill only when:

- The user confirms the offer, or
- The user explicitly asks to update/sync Postman

Do **not** call Postman MCP tools proactively without that confirmation.

## Steps

1. **Confirm with the user** — unless they already asked to sync Postman, ask before any MCP call.
2. **Inspect the endpoint** — note HTTP method, path, auth, request body, and `Result<T>` response type.
3. **Discover MCP tools** — call `GetMcpTools` with `server: "postman"`.
4. **Find the collection** — search workspaces/collections for `MK.FormEngine`.
5. **Add or update the request** — include:
   - Method + full path
   - JSON body example from the command/query record
   - Headers (`Content-Type`, `Authorization` when `[Authorize]` is present)
   - Description from OpenAPI `.WithSummary()` if available
6. **Organize** — place under a folder named after the feature slice (e.g. `TemplateManagement`).
7. **Auth token capture (env-only, dual tokens)** — tokens live **only** in the active environment (never collection variables):

| Login request | Env vars set |
|---|---|
| `Auth → Login` / `Auth → Refresh Token` | `Fsms_userAccessToken`, `Fsms_userRefreshToken` |
| `Field Team → Team Login` | `Fsms_ftAccessToken`, `Fsms_ftRefreshToken` |

Bearer auth defaults to `{{Fsms_userAccessToken}}`; the **Field Team** folder uses `{{Fsms_ftAccessToken}}`.

```javascript
// User login / refresh
if (accessToken) pm.environment.set('Fsms_userAccessToken', accessToken);
if (refreshToken) pm.environment.set('Fsms_userRefreshToken', refreshToken);

// Field-team login
if (accessToken) pm.environment.set('Fsms_ftAccessToken', accessToken);
if (refreshToken) pm.environment.set('Fsms_ftRefreshToken', refreshToken);
```

## Closing action (only after user approval)

Use the Postman MCP server to update the Postman workspace and add/update the endpoints.

## MCP setup (one-time)

Copy `.cursor/mcp.local.env.example` to `.cursor/mcp.local.env` and set `POSTMAN_API_KEY`. Restart Cursor after changes.
