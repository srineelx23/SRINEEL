import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { CustomerLogin } from './customer-login';
import { AuthService } from '../../services/auth.service';
import { CaptchaService } from '../../services/captcha.service';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('CustomerLogin', () => {
    let component: CustomerLogin;
    let fixture: ComponentFixture<CustomerLogin>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let captchaServiceSpy: jasmine.SpyObj<CaptchaService>;
    let routerSpy: jasmine.SpyObj<Router>;
    let routeMock: any;

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['login', 'getRoleFromToken']);
        captchaServiceSpy = jasmine.createSpyObj('CaptchaService', ['generateCaptcha', 'validateCaptcha']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);
        routeMock = {
            snapshot: {
                queryParamMap: convertToParamMap({})
            }
        };

        captchaServiceSpy.generateCaptcha.and.returnValue('ABC123');
        captchaServiceSpy.validateCaptcha.and.returnValue(true);

        await TestBed.configureTestingModule({
            imports: [CustomerLogin, FormsModule, CommonModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: CaptchaService, useValue: captchaServiceSpy },
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

    it('should navigate to /error if email or password missing', () => {
        component.email = '';
        component.password = '';
        component.login();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/error'], jasmine.objectContaining({
            state: jasmine.objectContaining({ status: 400 })
        }));
    });

    it('should navigate to /error if CAPTCHA is invalid', () => {
        captchaServiceSpy.validateCaptcha.and.returnValue(false);
        component.email = 'test@test.com';
        component.password = 'password';
        component.userCaptcha = 'wrong';
        component.login();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/error'], jasmine.objectContaining({
            state: jasmine.objectContaining({ title: 'Security Check' })
        }));
    });

    it('should navigate to dashboard on successful login', () => {
        const mockResponse = { token: 'fake-token' };
        authServiceSpy.login.and.returnValue(of(mockResponse));
        authServiceSpy.getRoleFromToken.and.returnValue('Customer');

        component.email = 'customer@vims.com';
        component.password = 'password';
        component.userCaptcha = 'ABC123';
        component.login();

        expect(sessionStorage.getItem('token')).toBe('fake-token');
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/customer-dashboard']);
    });

    it('should navigate to /error on login failure', () => {
        authServiceSpy.login.and.returnValue(throwError({ status: 401, error: { message: 'Invalid' } }));

        component.email = 'wrong@vims.com';
        component.password = 'password';
        component.login();

        expect(routerSpy.navigate).toHaveBeenCalledWith(['/error'], jasmine.any(Object));
    });

    it('should switch to forgot password mode', () => {
        component.openForgotPassword();
        expect(component.isForgotPasswordMode()).toBeTrue();
    });
});
