import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthStore } from '../auth/auth.store';

/** Attaches the in-memory bearer token to outgoing API requests. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthStore).currentAccessToken;

  // Do not attach auth to static asset requests (e.g. runtime config / i18n).
  const isAsset = req.url.includes('/config/') || req.url.includes('/i18n/');
  if (!token || isAsset) {
    return next(req);
  }

  return next(
    req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }),
  );
};
