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

    it('should call registerCustomer API', () => {
        const mockData = { email: 'test@test.com' };
        const mockResponse = 'Customer Registered Successfully';

        service.registerCustomer(mockData).subscribe(response => {
            expect(response).toEqual(mockResponse);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Auth/customer/register');
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

    it('should call changePassword API', () => {
        sessionStorage.setItem('token', 'fake-token');
        const data = { currentPassword: '123', newPassword: '456' };
        const mockResponse = 'Password updated';

        service.changePassword(data).subscribe(res => {
            expect(res).toEqual(mockResponse);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Auth/change-password');
        expect(req.request.method).toBe('PUT');
        req.flush(mockResponse);
    });

    it('should fetch security question', () => {
        const email = 'test@test.com';
        const mockResponse = { question: 'What is your pet name?' };

        service.getSecurityQuestion(email).subscribe(res => {
            expect(res).toEqual(mockResponse);
        });

        const req = httpMock.expectOne(`https://localhost:7257/api/Auth/forgot-password/security-question/${encodeURIComponent(email)}`);
        expect(req.request.method).toBe('GET');
        req.flush(mockResponse);
    });

    it('should reset password', () => {
        const data = { email: 'test@test.com', newPassword: '456' };
        const mockResponse = 'Reset success';

        service.resetPassword(data).subscribe(res => {
            expect(res).toEqual(mockResponse);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Auth/forgot-password/reset');
        expect(req.request.method).toBe('POST');
        req.flush(mockResponse);
    });

    it('should set security question', () => {
        sessionStorage.setItem('token', 'fake-token');
        const data = { question: 'Q', answer: 'A' };
        const mockResponse = 'Question set';

        service.setSecurityQuestion(data).subscribe(res => {
            expect(res).toEqual(mockResponse);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Auth/set-security-question');
        expect(req.request.method).toBe('POST');
        req.flush(mockResponse);
    });

    it('should remove token and navigate to login on logout', () => {
        spyOn(router, 'navigate');
        sessionStorage.setItem('token', 'fake-token');

        service.logout();

        expect(sessionStorage.getItem('token')).toBeNull();
        expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
});
