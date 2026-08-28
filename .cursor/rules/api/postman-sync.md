---
title: Postman MCP Sync After API Creation
impact: HIGH
impactDescription: Keeps Postman collection in sync with new endpoints
tags: api, postman, mcp, endpoints
globs: src/WebApps/MK.FormEngine.API/**,src/modules/Application/**
---

## Postman MCP Sync (Ask First)

After creating or modifying any API endpoint in `MK.FormEngine.API`, **ask the user before** using the Postman MCP server. Do not call Postman MCP tools until the user confirms.

**Target collection:** `MK.FormEngine`

### When to offer

- New minimal API endpoint or controller action added
- Route, HTTP method, or request/response contract changed
- New API version or route group introduced
- User explicitly asks to sync/update Postman

### Gate

1. Finish the endpoint implementation (handler, validator, route registration, OpenAPI metadata).
2. Tell the user which endpoints changed and ask whether to sync Postman.
3. Only if the user says yes (or explicitly requested sync), proceed with the workflow below.

### Workflow (after approval)

1. Call `GetMcpTools` for the `postman` server, then use Postman MCP tools to locate the `MK.FormEngine` collection.
2. Add or update the request with:
   - Correct HTTP method and path (`/api/v1/...`)
   - Request body example (from the command/query record)
   - Expected success and error responses (`Result<T>` shape)
   - Auth header placeholder if the endpoint requires `[Authorize]`
3. Group the request under a folder matching the feature slice name (e.g. `Templates`, `Surveys`).
