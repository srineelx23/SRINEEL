import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';

describe('authGuard', () => {
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(() => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn', 'getRoleFromToken']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        TestBed.configureTestingModule({
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        });
    });

    it('should allow access if logged in and no roles required', () => {
        authServiceSpy.isLoggedIn.and.returnValue(true);
        const route = { data: {} } as ActivatedRouteSnapshot;
        const state = { url: '/test' } as RouterStateSnapshot;

        const result = TestBed.runInInjectionContext(() => authGuard(route, state));
        expect(result).toBeTrue();
    });

    it('should deny access and redirect to login if not logged in', () => {
        authServiceSpy.isLoggedIn.and.returnValue(false);
        const route = { data: {} } as ActivatedRouteSnapshot;
        const state = { url: '/protected' } as RouterStateSnapshot;

        const result = TestBed.runInInjectionContext(() => authGuard(route, state));
        expect(result).toBeFalse();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should allow access if logged in with correct role', () => {
        authServiceSpy.isLoggedIn.and.returnValue(true);
        authServiceSpy.getRoleFromToken.and.returnValue('Admin');
        sessionStorage.setItem('token', 'fake-token');

        const route = { data: { roles: ['Admin', 'Manager'] } } as unknown as ActivatedRouteSnapshot;
        const state = { url: '/admin' } as RouterStateSnapshot;

        const result = TestBed.runInInjectionContext(() => authGuard(route, state));
        expect(result).toBeTrue();
        sessionStorage.clear();
    });

    it('should deny access and redirect to home if logged in with wrong role', () => {
        authServiceSpy.isLoggedIn.and.returnValue(true);
        authServiceSpy.getRoleFromToken.and.returnValue('Customer');
        sessionStorage.setItem('token', 'fake-token');

        const route = { data: { roles: ['Admin'] } } as unknown as ActivatedRouteSnapshot;
        const state = { url: '/admin' } as RouterStateSnapshot;

        const result = TestBed.runInInjectionContext(() => authGuard(route, state));
        expect(result).toBeFalse();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/']);
        sessionStorage.clear();
    });
});
