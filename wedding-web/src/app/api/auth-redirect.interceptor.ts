import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SessionExpiryService } from '../shared/services/session-expiry.service';

export const authRedirectInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const sessionExpiry = inject(SessionExpiryService);

  let request = req;
  const skipRedirect = req.headers.has('X-Skip-Auth-Redirect');
  if (skipRedirect) {
    request = req.clone({ headers: req.headers.delete('X-Skip-Auth-Redirect') });
  }

  return next(request).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)) {
        // If this request is explicitly marked as a "check" call, do not mutate session state.
        // (e.g. invalid QR keys should not wipe an existing valid session.)
        if (!skipRedirect) {
          sessionExpiry.expireNow();

          // Avoid infinite loops if we're already on /welcome.
          if (router.url !== '/') {
            void router.navigateByUrl('/');
          }
        }
      }

      return throwError(() => err);
    })
  );
};
