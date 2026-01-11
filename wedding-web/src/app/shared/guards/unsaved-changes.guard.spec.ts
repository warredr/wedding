import { TestBed } from '@angular/core/testing';
import { ConfirmModalService } from '../services/confirm-modal.service';
import { unsavedChangesGuard, type HasUnsavedChanges } from './unsaved-changes.guard';

describe('unsavedChangesGuard', () => {
  it('does not prompt when navigating within RSVP flow', () => {
    TestBed.configureTestingModule({ providers: [ConfirmModalService] });
    const svc = TestBed.inject(ConfirmModalService);
    spyOn(svc, 'open');

    const comp: HasUnsavedChanges = { hasUnsavedChanges: () => true };

    const result = TestBed.runInInjectionContext(() =>
      unsavedChangesGuard(comp as any, null as any, null as any, { url: '/rsvp/abc/overview' } as any)
    );

    expect(result).toBeTrue();
    expect(svc.open).not.toHaveBeenCalled();
  });

  it('prompts when navigating away and there are changes', async () => {
    TestBed.configureTestingModule({ providers: [ConfirmModalService] });
    const svc = TestBed.inject(ConfirmModalService);
    spyOn(svc, 'open').and.returnValue(Promise.resolve(true));

    const comp: HasUnsavedChanges = { hasUnsavedChanges: () => true };

    const result = TestBed.runInInjectionContext(() =>
      unsavedChangesGuard(comp as any, null as any, null as any, { url: '/search' } as any)
    );

    await expectAsync(result as Promise<boolean>).toBeResolvedTo(true);
    expect(svc.open).toHaveBeenCalled();
  });
});
