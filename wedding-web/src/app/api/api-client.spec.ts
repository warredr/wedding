import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';

describe('ApiClient', () => {
  it('uses withCredentials for GET and POST', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), ApiClient],
    });

    const api = TestBed.inject(ApiClient);
    const httpMock = TestBed.inject(HttpTestingController);

    api.get('/config').subscribe();
    const getReq = httpMock.expectOne((r) => r.url.endsWith('/config'));
    expect(getReq.request.withCredentials).toBeTrue();
    getReq.flush({ deadlineDate: '2026-05-01', isClosed: false });

    api.post('/session/start', { code: '662026' }).subscribe();
    const postReq = httpMock.expectOne((r) => r.url.endsWith('/session/start'));
    expect(postReq.request.withCredentials).toBeTrue();
    postReq.flush({ ok: true, expiresAtUtc: 'now' });

    httpMock.verify();
  });
});
