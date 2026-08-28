---
title: Never Hand-Edit NSwag Client — API First Then Regenerate
impact: HIGH
impactDescription: Keeps Angular types/clients in sync with OpenAPI; prevents drift
tags: api, nswag, angular, client
globs: src/WebApps/MK.FormEngine.API/**,src/modules/Application/**,src/WebApps/MK.FormEngine.Web/**
---

## Never Hand-Edit NSwag Client — API First Then Regenerate

Never update `api-client.generated.ts` manually. Change the API contract first; regenerate so the client reflects it. Do not invent client types to bypass regeneration.

**Incorrect:**

```typescript
// editing api-client.generated.ts by hand
export interface SurveyDetailDto { newField?: string; }
```

**Correct:**

```bash
# 1) Change API/DTO  2) Start MK.FormEngine.API  3) regenerate from MK.FormEngine.Web
npm run api:generate
```
