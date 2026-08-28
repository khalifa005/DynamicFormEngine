---
title: Always Show Loading for Client API Calls
impact: HIGH
impactDescription: Prevents double-submit and blank waiting states
tags: ui, loading, api, buttons, primeng
globs: src/WebApps/MK.FormEngine.Web/**
---

## Always Show Loading for Client API Calls

In the Angular client, every API call must show a busy state. Submit/action controls that call an API must show a loader and be disabled until the request finishes.

**Incorrect:**

```typescript
save(): void {
  this.api.save(dto).subscribe({ next: () => this.done() });
}
```

```html
<p-button (onClick)="save()" [label]="t('common.save')" />
```

**Correct:**

```typescript
saving = signal(false);
save(): void {
  this.saving.set(true);
  this.api.save(dto).pipe(finalize(() => this.saving.set(false))).subscribe();
}
```

```html
<p-button (onClick)="save()" [loading]="saving()" [disabled]="saving()" />
```
