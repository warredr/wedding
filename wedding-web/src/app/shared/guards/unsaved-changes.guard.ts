import type { CanDeactivateFn } from '@angular/router';
import { inject } from '@angular/core';
import { ConfirmModalService } from '../services/confirm-modal.service';

export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
  discardUnsavedChanges?(): void;
}

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (
  component,
  _currentRoute,
  _currentState,
  nextState
) => {
  const nextUrl = nextState?.url ?? '';
  if (nextUrl.startsWith('/rsvp/') || nextUrl === '/done') {
    return true;
  }

  if (!component?.hasUnsavedChanges?.()) {
    return true;
  }

  const confirmModal = inject(ConfirmModalService);
  return confirmModal.open('Je wijzigingen gaan verloren. Weet je het zeker?').then((ok) => {
    if (ok) {
      component?.discardUnsavedChanges?.();
    }
    return ok;
  });
};
