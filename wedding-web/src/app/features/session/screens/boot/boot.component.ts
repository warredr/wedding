import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, forkJoin, map, of, retry, switchMap, timer } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import { SessionExpiryService } from '../../../../shared/services/session-expiry.service';

@Component({
  selector: 'app-boot',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['../locked/locked-screen.component.scss'],
  template: `
    <div class="locked-container" [class.fade-out]="fadeOut">
      <!-- Lock Icon -->
      <svg class="lock-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
        <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
      </svg>
      
      <!-- Spinner -->
      <div class="spinner" *ngIf="loading">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
        </svg>
      </div>

      <!-- Action Button (Fallback) -->
      <button 
        *ngIf="!loading" 
        class="submit-button"
        (click)="activate()"
        type="button">
        <span>Tik om te openen</span>
        <span class="arrow" aria-hidden="true">→</span>
      </button>
      
      <p class="boot-message" *ngIf="loading">Controleren...</p>
    </div>
  `,
  styles: [`
    .spinner {
      margin: 2rem auto 1rem;
      animation: spin 1s linear infinite;
      width: 24px;
      height: 24px;
      color: var(--wv-brand);
    }
    .boot-message {
      text-align: center;
      color: var(--wv-ink);
      opacity: 0.6;
      font-size: 0.9rem;
      margin: 0;
    }
    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }
  `]
})
export class BootComponent implements OnInit {
  fadeOut = false;
  loading = true;
  private k: string | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: RsvpApi,
    private readonly sessionExpiry: SessionExpiryService
  ) { }

  ngOnInit(): void {
    this.k = this.route.snapshot.queryParamMap.get('k');

    if (this.k) {
      this.tryAuth(this.k);
      return;
    }

    // Cheap authenticated call: if we can fetch config, we have a session.
    this.api
      .getConfig()
      .pipe(
        catchError(() => {
          void this.router.navigateByUrl('/locked');
          return of(null);
        })
      )
      .subscribe((cfg) => {
        if (cfg) {
          this.sessionExpiry.setExpiresAtUtc(cfg.sessionExpiresAtUtc);
          this.finish();
        }
      });
  }

  activate(): void {
    if (this.k) {
      this.loading = true;
      this.tryAuth(this.k);
    }
  }

  private tryAuth(k: string): void {
    forkJoin({
      res: this.api.startSessionFromQr(k).pipe(
        retry(2),
        catchError(() => of(null))
      ),
      minTime: timer(800)
    })
      .pipe(
        map(({ res }) => res),
        switchMap((res) => {
          if (!res) return of(false); // Auth failed completely

          // Auth succeeded? Now VERIFY cookie stuck.
          // We pass skipRedirect to avoid auto-bouncing to /locked if this fails.
          return this.api.getConfig({ skipRedirect: true }).pipe(
            map(cfg => {
              if (cfg) {
                // Verified!
                this.sessionExpiry.setExpiresAtUtc(cfg.sessionExpiresAtUtc);
                return true;
              }
              return false;
            }),
            catchError(() => of(false))
          );
        })
      )
      .subscribe((success) => {
        if (success) {
          this.finish();
        } else {
          // Did auth fail, or cookie blocked?
          // We stop loading and show the button.
          // Interaction might fix the cookie block.
          this.loading = false;
        }
      });
  }

  private finish(): void {
    this.fadeOut = true;
    setTimeout(() => {
      void this.router.navigateByUrl('/welcome');
    }, 400); // Match CSS transition duration presumably
  }
}
