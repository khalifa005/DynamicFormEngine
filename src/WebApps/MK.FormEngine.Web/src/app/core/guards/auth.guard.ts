import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../auth/auth.store';

/**
 * Protects routes that require an authenticated session.
 *
 * The attempted URL rides along to the login page so the user lands back where they were aiming
 * rather than on the dashboard — which matters more with SSO, where signing in means a round trip
 * out to the identity provider and back.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const store = inject(AuthStore);
  const router = inject(Router);

  if (store.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Keeps already-authenticated users away from the login page. */
export const guestGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);

  if (!store.isAuthenticated()) {
    return true;
  }
  return router.createUrlTree(['/dashboard']);
};
