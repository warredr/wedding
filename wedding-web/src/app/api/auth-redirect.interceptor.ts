import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SessionExpiryService } from '../shared/services/session-expiry.service';

export const authRedirectInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const sessionExpiry = inject(SessionExpiryService);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)) {
        sessionExpiry.expireNow();
        // Avoid infinite loops if we're already on /locked.
        if (router.url !== '/locked') {
          void router.navigateByUrl('/locked');
        }
      }

      return throwError(() => err);
    })
  );
};
