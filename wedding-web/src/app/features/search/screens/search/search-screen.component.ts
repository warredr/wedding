import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, takeUntil, catchError, of } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import type { SearchResultDto } from '../../../../api/types';
import { DropdownListComponent, type DropdownListItem } from '../../../../shared/components/dropdown-list/dropdown-list.component';
import { TopBarComponent } from '../../../../shared/components/top-bar/top-bar.component';

@Component({
  selector: 'app-search-screen',
  standalone: true,
  imports: [CommonModule, DropdownListComponent, TopBarComponent],
  templateUrl: './search-screen.component.html',
})
export class SearchScreenComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('searchInput') private readonly searchInput?: ElementRef<HTMLInputElement>;

  q = '';
  results: SearchResultDto[] = [];
  loading = false;
  error: string | null = null;
  flashMessage: string | null = null;
  notice: 'group_locked' | 'group_confirmed' | 'locked_or_confirmed' | null = null;

  private cooldownByGroupId: Record<string, string> = {};
  private nowMs = Date.now();
  private countdownTimer: number | null = null;

  lastCompletedQuery: string | null = null;
  private inFlightQuery: string | null = null;

  enteringFromRight = false;
  leavingToRight = false;

  private enterTimer: number | null = null;
  private leaveTimer: number | null = null;
  private focusTimer: number | null = null;
  private focusTimer2: number | null = null;
  private pointerStartX: number | null = null;
  private pointerStartY: number | null = null;
  private pointerStartAt: number | null = null;
  private lastPointerUpAt = 0;

  private readonly destroy$ = new Subject<void>();
  private readonly query$ = new Subject<string>();

  constructor(
    private readonly api: RsvpApi,
    private readonly router: Router
  ) {
    const notice = (history.state as { notice?: unknown } | null | undefined)?.notice;
    this.notice =
      notice === 'group_locked' || notice === 'group_confirmed' || notice === 'locked_or_confirmed'
        ? notice
        : null;

    const lock = (history.state as { lock?: unknown } | null | undefined)?.lock as
      | { groupId?: unknown; expiresAtUtc?: unknown }
      | null
      | undefined;
    if (lock && typeof lock.groupId === 'string' && typeof lock.expiresAtUtc === 'string') {
      this.cooldownByGroupId[lock.groupId] = lock.expiresAtUtc;
      this.startCountdownTimer();
    }

    this.query$
      .pipe(
        // Only search after the user pauses typing.
        debounceTime(350),
        distinctUntilChanged(),
        switchMap((q) => {
          this.error = null;
          this.flashMessage = null;
          const trimmed = q.trim();
          if (trimmed.length < 3) {
            this.results = [];
            this.loading = false;
            this.inFlightQuery = null;
            this.lastCompletedQuery = null;
            return of([]);
          }

          this.loading = true;
          this.inFlightQuery = trimmed;
          return this.api.search(trimmed).pipe(
            catchError(() => {
              this.error = 'Zoeken mislukt.';
              return of([]);
            })
          );
        }),
        takeUntil(this.destroy$)
      )
      .subscribe((items) => {
        this.results = items;
        this.loading = false;

        // Empty state should only appear after we completed a search.
        this.lastCompletedQuery = this.inFlightQuery;
      });
  }

  clearQuery(): void {
    this.q = '';
    this.results = [];
    this.error = null;
    this.flashMessage = null;
    this.lastCompletedQuery = null;
    this.inFlightQuery = null;
    this.query$.next('');

    // Keep the keyboard open on mobile.
    this.focusSearchInput();
  }

  onClearActivate(ev: Event): void {
    // Support both pointer taps and keyboard activation.
    // iOS can fire a synthetic click after pointerup; ignore the click in that case.
    if (ev instanceof MouseEvent && Date.now() - this.lastPointerUpAt < 700) {
      return;
    }

    if (ev instanceof PointerEvent) {
      this.lastPointerUpAt = Date.now();
    }

    this.clearQuery();
  }

  private startCountdownTimer(): void {
    if (this.countdownTimer) {
      return;
    }

    this.countdownTimer = window.setInterval(() => {
      this.nowMs = Date.now();
    }, 1000);
  }

  private secondsLeftForGroup(groupId: string): number {
    const expiresAtUtc = this.cooldownByGroupId[groupId];
    if (!expiresAtUtc) {
      return 0;
    }

    const ms = Date.parse(expiresAtUtc);
    if (Number.isNaN(ms)) {
      return 0;
    }

    const deltaMs = ms - this.nowMs;
    if (deltaMs <= 0) {
      return 0;
    }

    return Math.ceil(deltaMs / 1000);
  }

  formatCountdown(secondsLeft: number): string {
    const s = Math.max(0, Math.floor(secondsLeft));
    const m = Math.floor(s / 60);
    const r = s % 60;
    if (m <= 0) {
      return `0:${String(r).padStart(2, '0')}`;
    }
    return `${m}:${String(r).padStart(2, '0')}`;
  }

  get lockedNoticeText(): string {
    if (this.notice !== 'group_locked') {
      return '';
    }

    const ids = Object.keys(this.cooldownByGroupId);
    const groupId = ids[0];
    if (!groupId) {
      return 'Probeer het straks opnieuw.';
    }

    const secondsLeft = this.secondsLeftForGroup(groupId);
    if (secondsLeft <= 0) {
      return 'Probeer het nu opnieuw.';
    }

    return `Probeer opnieuw binnen ${this.formatCountdown(secondsLeft)}.`;
  }

  ngOnInit(): void {
    const nav = (history.state as { nav?: unknown } | null | undefined)?.nav;
    if (nav === 'forward') {
      this.enteringFromRight = true;
    }
  }

  ngAfterViewInit(): void {
    // Trigger enter transition on forward navigation.
    if (this.enteringFromRight) {
      this.enterTimer = window.setTimeout(() => {
        this.enteringFromRight = false;
      }, 0);
    }

    // Autofocus is unreliable on mobile Safari; focus programmatically.
    // We do a quick attempt, plus a second attempt after the transition.
    this.focusTimer = window.setTimeout(() => {
      this.focusSearchInput();
    }, 50);

    this.focusTimer2 = window.setTimeout(() => {
      this.focusSearchInput();
    }, 320);
  }

  private focusSearchInput(): void {
    const el = this.searchInput?.nativeElement;
    if (!el) {
      return;
    }

    try {
      el.focus({ preventScroll: true });
    } catch {
      el.focus();
    }
  }

  goBack(): void {
    if (this.leavingToRight) {
      return;
    }

    this.leavingToRight = true;
    this.leaveTimer = window.setTimeout(() => {
      void this.router.navigateByUrl('/welcome', { state: { nav: 'back' } });
    }, 220);
  }

  private isInteractiveSwipeTarget(target: EventTarget | null): boolean {
    if (!(target instanceof Element)) {
      return false;
    }

    // Avoid treating normal taps/scroll gestures on interactive UI as a swipe-to-go-back.
    return !!target.closest(
      'input, textarea, select, button, a, [role="button"], .dropdown-list, .topbar, .input-wrap'
    );
  }

  onPointerDown(ev: PointerEvent): void {
    // Only track touch swipes; mouse/pen should not trigger swipe navigation.
    if (!ev.isPrimary || ev.pointerType !== 'touch') {
      return;
    }

    if (this.isInteractiveSwipeTarget(ev.target)) {
      return;
    }

    this.pointerStartX = ev.clientX;
    this.pointerStartY = ev.clientY;
    this.pointerStartAt = Date.now();
  }

  onPointerCancel(): void {
    this.pointerStartX = null;
    this.pointerStartY = null;
    this.pointerStartAt = null;
  }

  onPointerUp(ev: PointerEvent): void {
    if (!ev.isPrimary || ev.pointerType !== 'touch') {
      return;
    }

    const startX = this.pointerStartX;
    const startY = this.pointerStartY;
    const startAt = this.pointerStartAt;
    this.pointerStartX = null;
    this.pointerStartY = null;
    this.pointerStartAt = null;

    if (startX === null || startY === null || startAt === null) {
      return;
    }

    const dx = ev.clientX - startX;
    const dy = ev.clientY - startY;
    const dt = Date.now() - startAt;

    // Right swipe: go back.
    if (dx > 70 && Math.abs(dy) < 40 && dt < 700) {
      this.goBack();
    }
  }

  get dropdownItems(): Array<DropdownListItem<SearchResultDto>> {
    // Defensive coding: if the backend/data ever returns a partial record (or a bad cache on mobile),
    // avoid throwing in a template getter which can make the UI feel "frozen".
    return this.results
      .filter((r) => !!r && typeof (r as any).groupId === 'string' && typeof (r as any).personId === 'string')
      .map((r) => {
        const groupId = (r as any).groupId as string;
        const personId = (r as any).personId as string;
        const fullName = typeof (r as any).fullName === 'string' ? ((r as any).fullName as string) : '';
        const groupLabelFirstNames = typeof (r as any).groupLabelFirstNames === 'string'
          ? ((r as any).groupLabelFirstNames as string)
          : '';

        const secondsLeft = this.secondsLeftForGroup(groupId);
        const cooldownSuffix = secondsLeft > 0 ? ` · Probeer opnieuw binnen ${this.formatCountdown(secondsLeft)}` : '';
        const groupLabel = groupLabelFirstNames.trim();
        const isMultiPerson = groupLabel.length > 0 && /,|\s&\s|\sen\s/i.test(groupLabel);
        const groupPart = isMultiPerson ? `Groep: ${groupLabel}` : '';
        const statusPart = r.groupStatus === 'Confirmed' ? 'Bevestigd' : '';
        const baseSecondary = [groupPart, statusPart].filter(Boolean).join(' · ');
        const secondary = `${baseSecondary}${cooldownSuffix}`.trim();

        return {
          id: `${groupId}:${personId}`,
          primary: fullName,
          secondary: secondary.length > 0 ? secondary : undefined,
          disabled: r.groupStatus === 'Confirmed' || secondsLeft > 0,
          class: r.groupStatus === 'Confirmed' ? 'dropdown-list__item--confirmed' : undefined,
          isConfirmed: r.groupStatus === 'Confirmed',
          data: r,
        } satisfies DropdownListItem<SearchResultDto>;
      });
  }

  onInput(value: string): void {
    this.q = value;
    const trimmed = value.trim();
    if (trimmed.length >= 3) {
      this.loading = true;
    } else {
      this.loading = false;
    }
    this.query$.next(value);
  }

  select(item: SearchResultDto): void {
    if (!item || typeof (item as any).groupId !== 'string' || typeof (item as any).personId !== 'string') {
      return;
    }

    if (item.groupStatus === 'Confirmed') {
      return;
    }

    if (this.secondsLeftForGroup(item.groupId) > 0) {
      return;
    }

    void this.router.navigate(['/rsvp', item.groupId], {
      queryParams: { personId: item.personId },
    });
  }

  onSelectDropdownItem(item: DropdownListItem<SearchResultDto>): void {
    if (!item.data) {
      return;
    }

    this.select(item.data);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();

    if (this.enterTimer) {
      window.clearTimeout(this.enterTimer);
    }
    if (this.leaveTimer) {
      window.clearTimeout(this.leaveTimer);
    }
    if (this.focusTimer) {
      window.clearTimeout(this.focusTimer);
    }
    if (this.focusTimer2) {
      window.clearTimeout(this.focusTimer2);
    }

    if (this.countdownTimer) {
      window.clearInterval(this.countdownTimer);
    }
  }
}
