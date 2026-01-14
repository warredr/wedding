import { CommonModule } from '@angular/common';
import { AfterViewInit, ChangeDetectorRef, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import { ConfigDto } from '../../../../api/types';
import { SessionExpiryService } from '../../../../shared/services/session-expiry.service';

@Component({
  selector: 'app-welcome-screen',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './welcome-screen.component.html',
  styleUrls: ['./welcome-screen.component.scss'],
})
export class WelcomeScreenComponent implements OnInit, AfterViewInit, OnDestroy {
  config: ConfigDto | null = null;
  loading = true;
  error: string | null = null;

  // Transition states
  leaving = false;
  enteringFromLeft = false;

  // Auth & Modal states
  showDoneModal = false;
  showUnlockModal = false;
  isAuthenticated = false;
  isFocused = false;

  // Code Input Form
  readonly form = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{6}$/)],
    }),
  });

  @ViewChild('codeInput')
  private codeInput?: ElementRef<HTMLInputElement>;

  // Unlock Modal specific state
  unlockLoading = false;
  unlockError: string | null = null;
  unlockSuccess = false;
  validationState: 'idle' | 'valid' | 'invalid' = 'idle';

  private leaveTimer: number | null = null;
  private enterTimer: number | null = null;

  get deadlineDateNl(): string {
    const iso = this.config?.deadlineDate;
    if (!iso) {
      return '';
    }

    return formatIsoDateNl(iso) ?? iso;
  }

  get displayDigits(): string[] {
    const raw = this.form.controls.code.value ?? '';
    const digits = raw.replace(/\D/g, '').slice(0, 6);
    return Array.from({ length: 6 }, (_, i) => digits[i] ?? '');
  }

  constructor(
    private readonly api: RsvpApi,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
    private readonly sessionExpiry: SessionExpiryService,
    private readonly cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    const state = history.state as { nav?: unknown; showDoneModal?: unknown } | null | undefined;
    if (state?.nav === 'back') {
      this.enteringFromLeft = true;
    }
    if (state?.showDoneModal === true) {
      this.showDoneModal = true;
    }

    const k = this.route.snapshot.queryParamMap.get('k');
    if (k) {
      this.api.startSessionFromQr(k).subscribe((res) => {
        if (res) {
          const anyRes = res as any;
          if (typeof anyRes.expiresAtUtc === 'string') {
            this.sessionExpiry.setExpiresAtUtc(anyRes.expiresAtUtc);
          }
          this.isAuthenticated = true;
        }
        // Load config after auth attempt
        this.loadConfig();
      });
    } else {
      this.loadConfig();
    }
  }

  private loadConfig(): void {
    this.api
      .getConfig({ skipRedirect: true })
      .pipe(
        catchError(() => of(null))
      )
      .subscribe((cfg) => {
        this.config = cfg;
        this.loading = false;

        if (this.config && !this.config.isClosed) {
          this.isAuthenticated = true;
        } else if (cfg) {
          this.isAuthenticated = true;
        }

        this.cdr.markForCheck();
      });
  }

  ngAfterViewInit(): void {
    if (!this.enteringFromLeft) {
      return;
    }

    this.enterTimer = window.setTimeout(() => {
      this.enteringFromLeft = false;
    }, 0);
  }

  continue(): void {
    if (this.leaving) return;

    // If authenticated, go to search
    if (this.isAuthenticated) {
      this.navigateForward();
      return;
    }

    // Otherwise show unlock modal
    this.openUnlockModal();
  }

  // --- Navigation ---

  private navigateForward(): void {
    this.leaving = true;
    this.leaveTimer = window.setTimeout(() => {
      void this.router.navigateByUrl('/search', { state: { nav: 'forward' } });
    }, 220);
  }

  // --- Modal & Code Logic ---

  openUnlockModal(): void {
    this.showUnlockModal = true;
    document.body.classList.add('no-scroll');
    setTimeout(() => this.focusInput(), 100);
  }

  closeUnlockModal(): void {
    this.showUnlockModal = false;
    document.body.classList.remove('no-scroll');
    this.form.reset();
    this.validationState = 'idle';
    this.unlockError = null;
  }

  focusInput(): void {
    this.codeInput?.nativeElement?.focus();
    this.isFocused = true;
  }

  onCodeValueChange(rawValue: string): void {
    const digits = (rawValue ?? '').replace(/\D/g, '').slice(0, 6);
    this.unlockError = null;
    this.validationState = 'idle';

    this.form.controls.code.setValue(digits);
    this.form.controls.code.markAsTouched();

    const el = this.codeInput?.nativeElement;
    if (el && el.value !== digits) {
      el.value = digits;
    }

    if (digits.length === 6) {
      this.submitCode();
    }
  }

  submitCode(): void {
    this.unlockError = null;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.unlockLoading = true;
    const code = this.form.controls.code.value;

    this.api.startSession(code)
      .pipe(
        catchError(() => {
          this.unlockError = 'Ongeldige code.';
          return of(null);
        }),
        finalize(() => {
          this.unlockLoading = false;
        })
      )
      .subscribe((res) => {
        if (res) {
          this.validationState = 'valid';
          this.unlockSuccess = true;

          const anyRes = res as any;
          if (typeof anyRes.expiresAtUtc === 'string') {
            this.sessionExpiry.setExpiresAtUtc(anyRes.expiresAtUtc);
          }

          this.isAuthenticated = true;

          // Wait briefly for success animation, then navigate
          setTimeout(() => {
            this.closeUnlockModal();

            // Re-fetch config to ensure we have data, then navigate
            this.api.getConfig().subscribe(cfg => {
              if (cfg) { this.config = cfg; }
            });
            this.navigateForward();
          }, 800);

        } else {
          this.validationState = 'invalid';
          this.cdr.markForCheck();
          setTimeout(() => {
            this.validationState = 'idle';
            this.unlockError = null;
            this.form.controls.code.setValue('');
            this.cdr.markForCheck();
          }, 1500);
        }
      });
  }

  closeDoneModal(): void {
    this.showDoneModal = false;
  }

  ngOnDestroy(): void {
    document.body.classList.remove('no-scroll');
    if (this.leaveTimer) window.clearTimeout(this.leaveTimer);
    if (this.enterTimer) window.clearTimeout(this.enterTimer);
  }
}

function formatIsoDateNl(iso: string): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso.trim());
  if (!match) {
    return null;
  }

  const year = Number(match[1]);
  const monthIndex = Number(match[2]) - 1;
  const day = Number(match[3]);
  if (!Number.isFinite(year) || !Number.isFinite(monthIndex) || !Number.isFinite(day)) {
    return null;
  }

  const date = new Date(year, monthIndex, day);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return new Intl.DateTimeFormat('nl-NL', { day: 'numeric', month: 'long', year: 'numeric' }).format(date);
}
