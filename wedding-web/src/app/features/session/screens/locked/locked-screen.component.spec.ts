import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { throwError, of } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import { LockedScreenComponent } from './locked-screen.component';

describe('LockedScreenComponent', () => {
  let fixture: ComponentFixture<LockedScreenComponent>;

  function setup(apiMock: Pick<RsvpApi, 'startSession'>): void {
    const routerSpy = {
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    } as unknown as Router;

    TestBed.configureTestingModule({
      imports: [LockedScreenComponent],
      providers: [
        { provide: RsvpApi, useValue: apiMock },
        { provide: Router, useValue: routerSpy },
      ],
    });

    fixture = TestBed.createComponent(LockedScreenComponent);
    fixture.detectChanges();
  }

  it('submits code and navigates to /welcome on success', () => {
    const apiMock = {
      startSession: jasmine.createSpy('startSession').and.returnValue(of({ ok: true })),
    } as Pick<RsvpApi, 'startSession'>;

    setup(apiMock);

    fixture.componentInstance.form.controls.code.setValue('662026');
    fixture.componentInstance.submit();

    expect(apiMock.startSession).toHaveBeenCalledWith('662026');
    const router = TestBed.inject(Router) as any;
    expect(router.navigateByUrl).toHaveBeenCalledWith('/welcome');
  });

  it('shows an error when API call fails', () => {
    const apiMock = {
      startSession: jasmine.createSpy('startSession').and.returnValue(throwError(() => new Error('fail'))),
    } as Pick<RsvpApi, 'startSession'>;

    setup(apiMock);

    fixture.componentInstance.form.controls.code.setValue('662026');
    fixture.componentInstance.submit();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ongeldige code');
  });
});
