---
title: Loading State Patterns
impact: HIGH
impactDescription: Required for every client API call
tags: ui, loading, skeleton, empty-state, api
globs: src/WebApps/MK.FormEngine.Web/**
---


## Loading State Patterns

Every client API call must show a busy state. Use skeleton/spinner for page loads; for action buttons use loader + disable. Clear loading in `finalize`/`finally`. Always show helpful `@empty` UI for zero-data states.

```html
@if (loading()) {
  <app-card-skeleton />
} @else {
  <app-card [data]="data()" />
}

<p-button [loading]="saving()" [disabled]="saving()" (onClick)="save()" />
```
