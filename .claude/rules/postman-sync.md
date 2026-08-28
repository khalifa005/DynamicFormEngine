---
alwaysApply: true
description: >
  After creating any API endpoint, ask before syncing to the FSMS Postman
  collection via the Postman MCP server.
---

# Postman MCP Sync

After creating or modifying any API endpoint in `MK.FormEngine.API`:

1. **Ask the user first** whether to sync Postman. Do not call Postman MCP tools until they confirm (or they already asked to sync).
2. Target collection: **MK.FormEngine**
3. Match route prefix `/api/v1/...`, include request/response examples, and group by feature slice folder.
