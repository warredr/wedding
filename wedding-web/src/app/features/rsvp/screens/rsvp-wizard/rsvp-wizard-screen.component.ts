import { CommonModule } from '@angular/common';
import { Component, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import type { ClaimGroupResponseDto } from '../../../../api/types';
import { ApiClient } from '../../../../api/api-client';
import { TopBarComponent } from '../../../../shared/components/top-bar/top-bar.component';
import { ToggleYesNoComponent } from '../../../../shared/components/toggle-yes-no/toggle-yes-no.component';
import { ConfirmModalService } from '../../../../shared/services/confirm-modal.service';
import { RsvpDraftService } from '../../services/rsvp-draft.service';
import { isPersonCompleted } from '../../utils/rsvp-completion';
import type { RsvpDraftState } from '../../services/rsvp-draft.service';

@Component({
  selector: 'app-rsvp-wizard-screen',
  standalone: true,
  imports: [CommonModule, FormsModule, TopBarComponent, ToggleYesNoComponent],
  templateUrl: './rsvp-wizard-screen.component.html',
})
export class RsvpWizardScreenComponent implements OnDestroy {
  loading = true;
  error: string | null = null;
  showErrors = false;
  saving = false;
  popupMessage: string | null = null;

  finalSubmitPhase: 'idle' | 'submitting' | 'success' = 'idle';
  private finalSubmitStartedAtMs = 0;
  private finalSubmitTimer: number | null = null;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly api: RsvpApi,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly confirmModal: ConfirmModalService,
    readonly draft: RsvpDraftService
  ) {
    this.bootstrap();
  }

  hasUnsavedChanges(): boolean {
    return this.draft.isDirty();
  }

  discardUnsavedChanges(): void {
    const s = this.draft.state();
    this.draft.clear(s?.groupId);
  }

  private bootstrap(): void {
    const groupId = this.route.snapshot.paramMap.get('groupId');
    if (!groupId) {
      this.error = 'Ongeldige groep.';
      this.loading = false;
      return;
    }

    const selectedPersonId = this.route.snapshot.queryParamMap.get('personId');

    this.loading = true;
    this.error = null;

    this.api
      .claimGroup(groupId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (claim) => this.loadGroup(groupId, claim, selectedPersonId),
        error: (err) => {
          this.loading = false;

          const http = ApiClient.toError(err);
          const notice = noticeFromHttpError(http);
          const lock = lockInfoFromHttpError(http);
          void this.router.navigate(['/search'], {
            state: {
              notice,
              lock: lock ? { groupId, ...lock } : undefined,
            },
          });
        },
      });
  }

  backToSearch(): void {
    void this.router.navigate(['/search']);
  }

  async onBackPressed(): Promise<void> {
    const s = this.draft.state();
    if (!s) {
      void this.router.navigate(['/search']);
      return;
    }

    const idx = s.memberOrder.indexOf(s.currentPersonId);
    if (idx > 0) {
      this.draft.setCurrentPerson(s.memberOrder[idx - 1]!);
      this.showErrors = false;
      return;
    }

    const ok = await this.confirmModal.open('Je wijzigingen gaan verloren. Weet je het zeker?');
    if (!ok) {
      return;
    }

    this.draft.clear(s.groupId);
    void this.router.navigate(['/search']);
  }

  personFirstName(fullName: string): string {
    return firstName(fullName) ?? fullName;
  }

  goToPerson(personId: string): void {
    this.draft.setCurrentPerson(personId);
    this.showErrors = false;
  }

  private loadGroup(groupId: string, claim: ClaimGroupResponseDto, selectedPersonId: string | null): void {
    this.api
      .getGroup(groupId, claim.sessionId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (group) => {
          this.loading = false;
          this.draft.initialize(groupId, claim, group, selectedPersonId);

          if (group.members.length > 1 && !hasShownMultiPersonHint(groupId)) {
            markMultiPersonHintShown(groupId);
            this.popupMessage = 'Tot deze groep horen meerdere personen. Vul de aanwezigheid in voor iedereen.';
          }
        },
        error: (err) => {
          this.loading = false;

          const http = ApiClient.toError(err);
          if (http?.status === 409) {
            this.draft.clear(groupId);
            const notice = noticeFromHttpError(http);
            const lock = lockInfoFromHttpError(http);
            void this.router.navigate(['/search'], {
              state: {
                notice,
                lock: lock ? { groupId, ...lock } : undefined,
              },
            });
            return;
          }

          this.error = 'Kon de groep niet laden.';
          void this.router.navigate(['/search']);
        },
      });
  }

  nextButtonLabel(s: RsvpDraftState): string {
    const idx = s.memberOrder.indexOf(s.currentPersonId);
    const isLast = idx === s.memberOrder.length - 1;
    if (isLast) {
      return 'Bevestigen';
    }
    return 'Volgende persoon';
  }

  isFinalStep(s: RsvpDraftState): boolean {
    const idx = s.memberOrder.indexOf(s.currentPersonId);
    return idx === s.memberOrder.length - 1;
  }

  setAttending(value: boolean): void {
    const s = this.draft.state();
    if (!s) return;
    this.draft.setAttending(s.currentPersonId, value ? 'Yes' : 'No');
    this.showErrors = false;
    // Keep this as an inline hint only (no modal).
  }

  setDinnerAttending(value: boolean): void {
    const s = this.draft.state();
    if (!s) return;
    this.draft.setDinnerAttending(s.currentPersonId, value);
    this.showErrors = false;
  }

  setEveningPartyAttending(value: boolean): void {
    const s = this.draft.state();
    if (!s) return;
    this.draft.setEveningPartyAttending(s.currentPersonId, value);
    this.showErrors = false;
  }

  setHasAllergies(value: boolean): void {
    const s = this.draft.state();
    if (!s) return;
    this.draft.setHasAllergies(s.currentPersonId, value);
    this.showErrors = false;
  }

  setAllergiesText(text: string): void {
    const s = this.draft.state();
    if (!s) return;
    this.draft.setAllergiesText(s.currentPersonId, text);
  }

  closePopup(): void {
    this.popupMessage = null;
  }

  allergiesLabel(s: RsvpDraftState): string {
    if (s.members.length <= 1) {
      return 'Heb je allergieën?';
    }

    const first = firstName(this.draft.currentPerson()?.fullName);
    return first ? `Heeft ${first} allergieën?` : 'Heeft deze persoon allergieën?';
  }

  next(): void {
    const s = this.draft.state();
    if (!s) return;

    const currentDraft = s.personDrafts[s.currentPersonId];
    if (!currentDraft) return;

    const required = {
      dinner: s.invitedToEvents.includes('Dinner'),
      eveningParty: s.invitedToEvents.includes('EveningParty'),
    };

    if (!isPersonCompleted(currentDraft, required)) {
      this.showErrors = true;
      return;
    }

    const idx = s.memberOrder.indexOf(s.currentPersonId);
    if (idx < 0) return;

    if (idx === s.memberOrder.length - 1) {
      this.confirm();
      return;
    }

    this.draft.setCurrentPerson(s.memberOrder[idx + 1]!);
    this.showErrors = false;
  }

  // Validation hints are shown inline via `showErrors` (no modal popups).

  private confirm(): void {
    const s = this.draft.state();
    if (!s) return;
    if (this.saving) return;

    // Validate derived event attendance is representable.
    try {
      const payload = this.draft.buildSubmitRequest();

      this.saving = true;
      this.error = null;
      this.finalSubmitPhase = 'submitting';
      this.finalSubmitStartedAtMs = Date.now();

      this.api.submitGroup(s.groupId, s.sessionId, payload).subscribe({
        next: () => {
          const elapsedMs = Date.now() - this.finalSubmitStartedAtMs;
          const minSpinnerMs = 2000;
          const remainingMs = Math.max(0, minSpinnerMs - elapsedMs);

          this.clearFinalSubmitTimer();
          this.finalSubmitTimer = window.setTimeout(() => {
            this.finalSubmitPhase = 'success';

            this.clearFinalSubmitTimer();
            this.finalSubmitTimer = window.setTimeout(() => {
              this.draft.clear(s.groupId);
              void this.router.navigate(['/welcome'], { state: { nav: 'back', showDoneModal: true } });
            }, 1500);
          }, remainingMs);
        },
        error: (err) => {
          this.saving = false;
          this.finalSubmitPhase = 'idle';
          this.clearFinalSubmitTimer();

          const http = ApiClient.toError(err);
          if (http?.status === 409) {
            this.draft.clear(s.groupId);
            const notice = noticeFromHttpError(http);
            const lock = lockInfoFromHttpError(http);
            void this.router.navigate(['/search'], {
              state: {
                notice,
                lock: lock ? { groupId: s.groupId, ...lock } : undefined,
              },
            });
            return;
          }

          this.popupMessage = 'Bevestigen mislukt. Probeer opnieuw.';
        },
      });
    } catch (e) {
      this.popupMessage = 'Bevestigen mislukt. Probeer opnieuw.';
    }
  }

  private clearFinalSubmitTimer(): void {
    if (this.finalSubmitTimer !== null) {
      window.clearTimeout(this.finalSubmitTimer);
      this.finalSubmitTimer = null;
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();

    this.clearFinalSubmitTimer();
  }
}

function firstName(fullName: string | null | undefined): string {
  const name = (fullName ?? '').trim();
  if (!name) {
    return '';
  }

  const parts = name.split(/\s+/g);
  return parts[0] ?? '';
}

function noticeFromHttpError(http: ReturnType<typeof ApiClient.toError>): 'group_locked' | 'group_confirmed' {
  const reason = (http?.error as any)?.details?.reason;
  return reason === 'confirmed' ? 'group_confirmed' : 'group_locked';
}

function lockInfoFromHttpError(
  http: ReturnType<typeof ApiClient.toError>
): { expiresAtUtc?: string; secondsLeft?: number } | null {
  const details = (http?.error as any)?.details;
  if (!details || details.reason !== 'locked') {
    return null;
  }

  return {
    expiresAtUtc: typeof details.expiresAtUtc === 'string' ? details.expiresAtUtc : undefined,
    secondsLeft: typeof details.secondsLeft === 'number' ? details.secondsLeft : undefined,
  };
}

const MULTI_HINT_PREFIX = 'wv_rsvp_multi_hint_v1:';

function hasShownMultiPersonHint(groupId: string): boolean {
  try {
    return sessionStorage.getItem(`${MULTI_HINT_PREFIX}${groupId}`) === '1';
  } catch {
    return true;
  }
}

function markMultiPersonHintShown(groupId: string): void {
  try {
    sessionStorage.setItem(`${MULTI_HINT_PREFIX}${groupId}`, '1');
  } catch {
    // ignore
  }
}
