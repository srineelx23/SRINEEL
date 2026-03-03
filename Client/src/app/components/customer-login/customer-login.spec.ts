import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { CustomerLogin } from './customer-login';
import { AuthService } from '../../services/auth.service';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('CustomerLogin', () => {
    let component: CustomerLogin;
    let fixture: ComponentFixture<CustomerLogin>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;
    let routeMock: any;

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['login', 'getRoleFromToken', 'getSecurityQuestion', 'resetPassword']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);
        routeMock = {
            snapshot: {
                queryParamMap: convertToParamMap({})
            }
        };

        await TestBed.configureTestingModule({
            imports: [CustomerLogin, FormsModule, CommonModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy },
                { provide: ActivatedRoute, useValue: routeMock }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(CustomerLogin);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should show error if email or password missing on login', () => {
        component.email = '';
        component.password = '';
        component.login();
        expect(component.errorMessage()).toBe('Please enter both email and password.');
    });

    it('should navigate on successful login as Admin', () => {
        const mockResponse = { token: 'admin-token' };
        authServiceSpy.login.and.returnValue(of(mockResponse));
        authServiceSpy.getRoleFromToken.and.returnValue('Admin');

        component.email = 'admin@vims.com';
        component.password = 'AdminPass';
        component.login();

        expect(sessionStorage.getItem('token')).toBe('admin-token');
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/admin-dashboard']);
    });

    it('should handle login error correctly', fakeAsync(() => {
        const errorMsg = 'Invalid credentials';
        authServiceSpy.login.and.returnValue(throwError({ error: errorMsg }));

        component.email = 'wrong@vims.com';
        component.password = 'WrongPass';
        component.login();

        expect(component.errorMessage()).toBe(errorMsg);

        tick(5000); // Test auto-hide
        expect(component.errorMessage()).toBe('');
    }));

    it('should switch to forgot password mode', () => {
        component.openForgotPassword();
        expect(component.isForgotPasswordMode()).toBeTrue();
        expect(component.forgotPasswordStep()).toBe(1);
    });
});
