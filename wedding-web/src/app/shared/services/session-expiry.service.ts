import { Injectable } from '@angular/core';
import { Router } from '@angular/router';

const STORAGE_KEY = 'wv_session_expiresAtUtc_v1';

@Injectable({ providedIn: 'root' })
export class SessionExpiryService {
  private timer: number | null = null;
  private expiresAtUtcMs: number | null = null;

  constructor(private readonly router: Router) {
    this.restoreFromStorage();
  }

  setExpiresAtUtc(expiresAtUtc: string | null | undefined): void {
    if (!expiresAtUtc) {
      this.clear();
      return;
    }

    const ms = Date.parse(expiresAtUtc);
    if (!Number.isFinite(ms)) {
      return;
    }

    this.expiresAtUtcMs = ms;

    try {
      localStorage.setItem(STORAGE_KEY, expiresAtUtc);
    } catch {
      // ignore
    }

    this.armTimer();
  }

  clear(): void {
    this.expiresAtUtcMs = null;
    if (this.timer !== null) {
      window.clearInterval(this.timer);
      this.timer = null;
    }

    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }

  expireNow(): void {
    this.clear();

    // Clear local draft/session answers so a fresh start is easy.
    try {
      sessionStorage.clear();
    } catch {
      // ignore
    }

    if (this.router.url !== '/locked') {
      void this.router.navigateByUrl('/locked');
    }
  }

  private restoreFromStorage(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return;
      const ms = Date.parse(raw);
      if (!Number.isFinite(ms)) return;
      this.expiresAtUtcMs = ms;
      this.armTimer();
    } catch {
      // ignore
    }
  }

  private armTimer(): void {
    if (this.timer !== null) {
      return;
    }

    this.timer = window.setInterval(() => {
      if (this.expiresAtUtcMs === null) {
        return;
      }

      if (Date.now() >= this.expiresAtUtcMs) {
        this.expireNow();
      }
    }, 1000);
  }
}
