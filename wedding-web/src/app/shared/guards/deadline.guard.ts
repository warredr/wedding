import { Injectable, inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError, of } from 'rxjs';
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
        catchError(() => {
            // If config fails, we might want to fail safe or allow. 
            // If we assume failure means "offline" or "error", maybe allow? 
            // Or block?
            // Let's assume safely open, or handle error. 
            // But typically we should just return true or let the error bubble?
            // Safe default: return true, let other guards/interceptors handle auth.
            return of(true);
        })
    );
};
