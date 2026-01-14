import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionExpiryService } from '../../../../shared/services/session-expiry.service';
import { catchError, of } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';

@Component({
  selector: 'app-boot',
  standalone: true,
  template: '',
})
export class BootComponent implements OnInit {
  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: RsvpApi,
    private readonly sessionExpiry: SessionExpiryService
  ) {}

  ngOnInit(): void {
    const k = this.route.snapshot.queryParamMap.get('k') ?? this.route.snapshot.paramMap.get('k');

    if (k) {
      this.api
        .startSessionFromQr(k)
        .pipe(
          catchError(() => {
            void this.router.navigateByUrl('/locked');
            return of(null);
          })
        )
        .subscribe((res) => {
          const anyRes = res as any;
          if (anyRes && typeof anyRes.expiresAtUtc === 'string') {
            this.sessionExpiry.setExpiresAtUtc(anyRes.expiresAtUtc);
          }
          void this.router.navigateByUrl('/welcome');
        });

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
          void this.router.navigateByUrl('/welcome');
        }
      });
  }
}
