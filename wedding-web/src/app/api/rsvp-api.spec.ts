import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { RsvpApi } from './rsvp-api';

describe('RsvpApi', () => {
  it('calls /config', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ApiClient, RsvpApi],
    });

    const api = TestBed.inject(RsvpApi);
    const httpMock = TestBed.inject(HttpTestingController);

    api.getConfig().subscribe((cfg) => {
      expect(cfg.deadlineDate).toBe('2026-05-01');
      expect(cfg.isClosed).toBeFalse();
    });

    const req = httpMock.expectOne((r) => r.url.endsWith('/config'));
    expect(req.request.method).toBe('GET');
    req.flush({ deadlineDate: '2026-05-01', isClosed: false });

    httpMock.verify();
  });
});
