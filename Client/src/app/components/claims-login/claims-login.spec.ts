import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClaimsLogin } from './claims-login';
import { AuthService } from '../../services/auth.service';
import { CaptchaService } from '../../services/captcha.service';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('ClaimsLogin', () => {
    let component: ClaimsLogin;
    let fixture: ComponentFixture<ClaimsLogin>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let captchaServiceSpy: jasmine.SpyObj<CaptchaService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['login', 'getRoleFromToken']);
        captchaServiceSpy = jasmine.createSpyObj('CaptchaService', ['generateCaptcha', 'validateCaptcha']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        captchaServiceSpy.generateCaptcha.and.returnValue('CLAIM555');
        captchaServiceSpy.validateCaptcha.and.returnValue(true);

        await TestBed.configureTestingModule({
            imports: [ClaimsLogin, FormsModule, CommonModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: CaptchaService, useValue: captchaServiceSpy },
                { provide: Router, useValue: routerSpy },
                { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(ClaimsLogin);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should login and navigate to claims dashboard', () => {
        authServiceSpy.login.and.returnValue(of({ token: 'claim-token' }));
        authServiceSpy.getRoleFromToken.and.returnValue('ClaimsOfficer');

        component.email = 'claims@vims.com';
        component.password = 'ClaimPass';
        component.login();

        expect(routerSpy.navigate).toHaveBeenCalledWith(['/claims-dashboard']);
    });
});
