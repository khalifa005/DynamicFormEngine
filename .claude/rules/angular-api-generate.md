---
alwaysApply: true
description: >
  After API contract changes that require client work, regenerate the Angular
  NSwag client with npm run api:generate before coding against new endpoints/DTOs.
---

# Regenerate Angular API Client After API Updates (Required)

When the backend API changes and the Angular client needs to call new or changed endpoints/DTOs, regenerate the NSwag client **before** writing or updating client code that depends on those contracts.

## When to run

- New or changed controller/minimal-API endpoints
- Request/response DTO shape changes (properties added/removed/renamed)
- New clients/services exposed in OpenAPI that the web app will use
- Any follow-up work in `src/WebApps/MK.FormEngine.Web` that consumes `api-client.generated.ts`

## How to run

1. Ensure `MK.FormEngine.API` is running (NSwag reads the live OpenAPI document).
2. From `src/WebApps/MK.FormEngine.Web`:

```bash
npm run api:generate
```

3. Confirm `src/app/core/api/api-client.generated.ts` updated, then implement client changes against the generated types/clients.

## Do not

- Hand-edit `api-client.generated.ts`
- Call new API methods/DTOs from Angular using ad-hoc `HttpClient` types when a generated client should exist — regenerate first, then use the generated client
