import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, AfterViewInit } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import type { ConfigDto } from '../../../../api/types';

@Component({
  selector: 'app-welcome-screen',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './welcome-screen.component.html',
})
export class WelcomeScreenComponent implements OnInit, AfterViewInit, OnDestroy {
  config: ConfigDto | null = null;
  loading = true;
  error: string | null = null;
  leaving = false;
  enteringFromLeft = false;
  showDoneModal = false;

  private leaveTimer: number | null = null;
  private enterTimer: number | null = null;

  get deadlineDateNl(): string {
    const iso = this.config?.deadlineDate;
    if (!iso) {
      return '';
    }

    return formatIsoDateNl(iso) ?? iso;
  }

  constructor(
    private readonly api: RsvpApi,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    const state = history.state as { nav?: unknown; showDoneModal?: unknown } | null | undefined;
    if (state?.nav === 'back') {
      this.enteringFromLeft = true;
    }
    if (state?.showDoneModal === true) {
      this.showDoneModal = true;
    }

    this.api
      .getConfig()
      .pipe(
        catchError(() => {
          this.error = 'Kon configuratie niet laden.';
          return of(null);
        })
      )
      .subscribe((cfg) => {
        this.config = cfg;
        this.loading = false;
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
    if (this.leaving) {
      return;
    }

    this.leaving = true;
    this.leaveTimer = window.setTimeout(() => {
      void this.router.navigateByUrl('/search', { state: { nav: 'forward' } });
    }, 220);
  }

  closeDoneModal(): void {
    this.showDoneModal = false;
  }

  ngOnDestroy(): void {
    if (this.leaveTimer) {
      window.clearTimeout(this.leaveTimer);
    }

    if (this.enterTimer) {
      window.clearTimeout(this.enterTimer);
    }
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
