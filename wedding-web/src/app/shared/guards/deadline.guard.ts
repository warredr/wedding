import { Injectable, inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError, of } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { RsvpApi } from '../../api/rsvp-api';

export const deadlineGuard: CanActivateFn = (route, state) => {
    const api = inject(RsvpApi);
    const router = inject(Router);

    // We skip redirect here because we want to know the *config state*, 
    // not necessarily if we are authenticated (though usually we are if we are navigating these routes).
    // Actually, for public routes (if any), this matters. But these are protected routes.
    // If we are unauthenticated, the interceptor or another mechanism handles it.
    // Here we purely check "isClosed".

    return api.getConfig({ skipRedirect: true }).pipe(
        map(config => {
            if (config?.isClosed) {
                // If closed, redirect to welcome screen
                return router.createUrlTree(['/']);
            }
            return true;
        }),
        catchError((err: unknown) => {
            // These routes are protected; if we are unauthorized, send the user back to the welcome screen.
            if (err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)) {
                return of(router.createUrlTree(['/']));
            }

            // Other failures (temporary network issues) should not hard-block navigation.
            return of(true);
        })
    );
};
