import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthStore } from '../auth/auth.store';
import { AuthService } from '../auth/auth.service';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

/** Centralizes API error handling: auto-refreshes token or redirects to login on 401. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const store = inject(AuthStore);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthEndpoint = req.url.includes('/auth/login') || req.url.includes('/auth/refresh');
      if (error.status === 401 && !isAuthEndpoint) {
        if (store.currentRefreshToken) {
          if (!isRefreshing) {
            isRefreshing = true;
            refreshTokenSubject.next(null);

            return authService.refresh().pipe(
              switchMap((newToken) => {
                isRefreshing = false;
                refreshTokenSubject.next(newToken.accessToken);
                return next(
                  req.clone({
                    setHeaders: { Authorization: `Bearer ${newToken.accessToken}` },
                  }),
                );
              }),
              catchError((refreshError) => {
                isRefreshing = false;
                store.clear();
                void router.navigate(['/login']);
                return throwError(() => refreshError);
              }),
            );
          } else {
            return refreshTokenSubject.pipe(
              filter((token): token is string => token !== null),
              take(1),
              switchMap((token) => {
                return next(
                  req.clone({
                    setHeaders: { Authorization: `Bearer ${token}` },
                  }),
                );
              }),
            );
          }
        } else {
          store.clear();
          void router.navigate(['/login']);
        }
      }
      return throwError(() => error);
    }),
  );
};

