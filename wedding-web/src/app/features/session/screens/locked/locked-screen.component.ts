import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import { SessionExpiryService } from '../../../../shared/services/session-expiry.service';

@Component({
  selector: 'app-locked-screen',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './locked-screen.component.html',
  styleUrls: ['./locked-screen.component.scss'],
})
export class LockedScreenComponent implements OnInit {
  readonly form = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{6}$/)],
    }),
  });

  @ViewChild('codeInput')
  private codeInput?: ElementRef<HTMLInputElement>;

  loading = false;
  error: string | null = null;
  isFocused = false;
  validationState: 'idle' | 'valid' | 'invalid' = 'idle';
  fadeOut = false;

  constructor(
    private readonly api: RsvpApi,
    private readonly router: Router,
    private readonly sessionExpiry: SessionExpiryService
  ) {}

  ngOnInit(): void {
    // Auto-focus on the input when component initializes
    setTimeout(() => this.focusInput(), 100);
  }

  get displayDigits(): string[] {
    const raw = this.form.controls.code.value ?? '';
    const digits = raw.replace(/\D/g, '').slice(0, 6);
    return Array.from({ length: 6 }, (_, i) => digits[i] ?? '');
  }

  focusInput(): void {
    const el = this.codeInput?.nativeElement;
    if (!el) return;
    el.focus();
    this.isFocused = true;
  }

  onCodeValueChange(rawValue: string): void {
    const digits = (rawValue ?? '').replace(/\D/g, '').slice(0, 6);
    this.error = null;
    this.validationState = 'idle';

    // Keep the form control as the single source of truth.
    this.form.controls.code.setValue(digits);
    this.form.controls.code.markAsTouched();

    // Ensure the native input reflects our sanitized value.
    const el = this.codeInput?.nativeElement;
    if (el && el.value !== digits) {
      el.value = digits;
    }
  }

  submit(): void {
    this.error = null;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading = true;
    const code = this.form.controls.code.value;

    this.api
      .startSession(code)
      .pipe(
        catchError(() => {
          this.error = 'Ongeldige code.';
          return of(null);
        }),
        finalize(() => {
          this.loading = false;
        })
      )
      .subscribe((res) => {
        if (res) {
          // Show success state
          this.validationState = 'valid';
          
          // Arm auto-logout timer.
          const anyRes = res as any;
          if (typeof anyRes.expiresAtUtc === 'string') {
            this.sessionExpiry.setExpiresAtUtc(anyRes.expiresAtUtc);
          }
          
          // Wait for animation, then fade out and navigate
          setTimeout(() => {
            this.fadeOut = true;
            setTimeout(() => {
              void this.router.navigateByUrl('/welcome');
            }, 400);
          }, 800);
        } else {
          // Show error state
          this.validationState = 'invalid';
          setTimeout(() => {
            this.validationState = 'idle';
          }, 1500);
        }
      });
  }
}
