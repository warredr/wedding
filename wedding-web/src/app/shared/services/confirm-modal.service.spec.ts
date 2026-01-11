import { TestBed } from '@angular/core/testing';
import { ConfirmModalService } from './confirm-modal.service';

describe('ConfirmModalService', () => {
  it('resolves true on confirm', async () => {
    TestBed.configureTestingModule({ providers: [ConfirmModalService] });
    const svc = TestBed.inject(ConfirmModalService);

    const p = svc.open('msg');
    expect(svc.state().open).toBeTrue();

    svc.confirm();
    await expectAsync(p).toBeResolvedTo(true);
    expect(svc.state().open).toBeFalse();
  });

  it('resolves false on cancel', async () => {
    TestBed.configureTestingModule({ providers: [ConfirmModalService] });
    const svc = TestBed.inject(ConfirmModalService);

    const p = svc.open('msg');
    svc.cancel();

    await expectAsync(p).toBeResolvedTo(false);
  });
});
