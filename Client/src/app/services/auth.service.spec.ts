import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';

describe('AuthService', () => {
    let service: AuthService;
    let httpMock: HttpTestingController;
    let router: Router;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule, RouterTestingModule],
            providers: [AuthService]
        });
        service = TestBed.inject(AuthService);
        httpMock = TestBed.inject(HttpTestingController);
        router = TestBed.inject(Router);
    });

    afterEach(() => {
        httpMock.verify();
        sessionStorage.clear();
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('should call login API and return response', () => {
        const mockResponse = { token: 'fake-token' };
        const credentials = { email: 'test@test.com', password: 'password' };

        service.login(credentials).subscribe(response => {
            expect(response).toEqual(mockResponse);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Auth/login');
        expect(req.request.method).toBe('POST');
        req.flush(mockResponse);
    });

    it('should return true for isLoggedIn when token exists', () => {
        sessionStorage.setItem('token', 'fake-token');
        expect(service.isLoggedIn()).toBeTrue();
    });

    it('should return false for isLoggedIn when token does not exist', () => {
        expect(service.isLoggedIn()).toBeFalse();
    });

    it('should remove token and navigate to login on logout', () => {
        spyOn(router, 'navigate');
        sessionStorage.setItem('token', 'fake-token');

        service.logout();

        expect(sessionStorage.getItem('token')).toBeNull();
        expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
});
