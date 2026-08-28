---
title: Handle Form Submission Properly
impact: HIGH
impactDescription: Loading + disable on API submit is required
tags: forms, submission, loading, api
globs: src/WebApps/MK.FormEngine.Web/**
---


## Handle Form Submission Properly

Check validity and mark touched before submit. Track a busy signal, bind PrimeNG `[loading]` + `[disabled]` on the submit button, and clear busy state in `finally`/`finalize`. Use `getRawValue()` to include disabled fields.

```typescript
async onSubmit() {
  if (this.form.invalid) { this.form.markAllAsTouched(); return; }
  this.loading.set(true);
  try { await this.api.submit(this.form.getRawValue()); }
  finally { this.loading.set(false); }
}
```

```html
<p-button type="submit" [loading]="loading()" [disabled]="loading() || form.invalid" />
```
