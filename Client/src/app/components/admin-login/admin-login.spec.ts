import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminLogin } from './admin-login';
import { AuthService } from '../../services/auth.service';
import { CaptchaService } from '../../services/captcha.service';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('AdminLogin', () => {
    let component: AdminLogin;
    let fixture: ComponentFixture<AdminLogin>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let captchaServiceSpy: jasmine.SpyObj<CaptchaService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['login', 'getRoleFromToken']);
        captchaServiceSpy = jasmine.createSpyObj('CaptchaService', ['generateCaptcha', 'validateCaptcha']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        captchaServiceSpy.generateCaptcha.and.returnValue('ADMIN123');
        captchaServiceSpy.validateCaptcha.and.returnValue(true);

        await TestBed.configureTestingModule({
            imports: [AdminLogin, FormsModule, CommonModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: CaptchaService, useValue: captchaServiceSpy },
                { provide: Router, useValue: routerSpy },
                { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(AdminLogin);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should navigate to /error if validation fails', () => {
        component.email = '';
        component.login();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/error'], jasmine.any(Object));
    });

    it('should login and navigate to admin dashboard', () => {
        authServiceSpy.login.and.returnValue(of({ token: 'admin-token' }));
        authServiceSpy.getRoleFromToken.and.returnValue('Admin');

        component.email = 'admin@vims.com';
        component.password = 'AdminPass';
        component.login();

        expect(routerSpy.navigate).toHaveBeenCalledWith(['/admin-dashboard']);
    });
});
