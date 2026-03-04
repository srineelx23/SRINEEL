import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { Router } from '@angular/router';

describe('authInterceptor', () => {
    let httpClient: HttpClient;
    let httpTestingController: HttpTestingController;
    let router: Router;

    beforeEach(() => {
        const routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(withInterceptors([authInterceptor])),
                provideHttpClientTesting(),
                { provide: Router, useValue: routerSpy }
            ]
        });

        httpClient = TestBed.inject(HttpClient);
        httpTestingController = TestBed.inject(HttpTestingController);
        router = TestBed.inject(Router);
        sessionStorage.clear();
    });

    afterEach(() => {
        httpTestingController.verify();
    });

    it('should add an Authorization header when token is present', () => {
        const mockToken = 'test-token';
        sessionStorage.setItem('token', mockToken);

        httpClient.get('/api/test').subscribe();

        const req = httpTestingController.expectOne('/api/test');
        expect(req.request.headers.has('Authorization')).toBeTrue();
        expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    });

    it('should not add an Authorization header when token is absent', () => {
        httpClient.get('/api/test').subscribe();

        const req = httpTestingController.expectOne('/api/test');
        expect(req.request.headers.has('Authorization')).toBeFalse();
    });

    it('should navigate to /error on HttpErrorResponse', () => {
        httpClient.get('/api/test').subscribe({
            error: (error) => {
                expect(error.status).toBe(500);
            }
        });

        const req = httpTestingController.expectOne('/api/test');
        req.error(new ProgressEvent('error'), { status: 500, statusText: 'Internal Server Error' });

        expect(router.navigate).toHaveBeenCalledWith(['/error'], jasmine.any(Object));
    });
});
