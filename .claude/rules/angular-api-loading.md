---
alwaysApply: true
description: >
  Angular client must show loading UI for every API call, and disable + show
  a loader on submit/action controls that trigger API requests.
---

# Angular API Loading & Action Disable (Required)

When working in `src/WebApps/MK.FormEngine.Web` (or any client code that calls an API), always wire loading UX. Never fire an API call without a visible busy state.

## Page / data loads

- Track loading with a signal (`loading`, `isLoading`, query `isPending`, etc.).
- Show a spinner, skeleton, or table `[loading]` while the request is in flight.
- Clear loading in `finalize` / `finally` so errors still stop the spinner.

## Submit / action buttons that call APIs

- Set a busy signal before the call; clear it when the call completes (success or error).
- Disable the triggering control for the duration of the request.
- Show an inline loader on that control (PrimeNG: `[loading]` + `[disabled]`).
- Prefer disabling related cancel/secondary actions too while the primary action is busy.

**Incorrect:**

```typescript
save(): void {
  this.api.save(dto).subscribe({ next: () => this.toast.success() });
}
```

```html
<p-button (onClick)="save()" [label]="t('common.save')" />
```

**Correct:**

```typescript
saving = signal(false);

save(): void {
  if (this.saving()) return;
  this.saving.set(true);
  this.api.save(dto).pipe(finalize(() => this.saving.set(false))).subscribe({
    next: () => this.toast.success(),
  });
}
```

```html
<p-button
  (onClick)="save()"
  [label]="t('common.save')"
  [loading]="saving()"
  [disabled]="saving()"
/>
```

## Checklist (before finishing Angular API work)

- [ ] Every API read has a page/list/dialog loading indicator
- [ ] Every submit/delete/publish/clone/etc. button has `[loading]` + `[disabled]` (or equivalent)
- [ ] Loading is cleared on both success and failure
