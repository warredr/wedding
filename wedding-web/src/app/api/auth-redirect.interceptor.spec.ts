import { HttpClient } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { authRedirectInterceptor } from './auth-redirect.interceptor';

describe('authRedirectInterceptor', () => {
  it('navigates to / on 401', () => {
    const routerSpy = {
      url: '/search',
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    } as unknown as Router;

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: routerSpy },
        provideHttpClient(withInterceptors([authRedirectInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    const http = TestBed.inject(HttpClient);
    const httpMock = TestBed.inject(HttpTestingController);

    http.get('/api/config').subscribe({ error: () => { } });

    const req = httpMock.expectOne('/api/config');
    req.flush({ message: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect((routerSpy as any).navigateByUrl).toHaveBeenCalledWith('/');
    httpMock.verify();
  });

  it('does not re-navigate when already on /', () => {
    const routerSpy = {
      url: '/',
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    } as unknown as Router;

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: routerSpy },
        provideHttpClient(withInterceptors([authRedirectInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    const http = TestBed.inject(HttpClient);
    const httpMock = TestBed.inject(HttpTestingController);

    http.get('/api/config').subscribe({ error: () => { } });

    const req = httpMock.expectOne('/api/config');
    req.flush({ message: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect((routerSpy as any).navigateByUrl).not.toHaveBeenCalled();
    httpMock.verify();
  });
});
