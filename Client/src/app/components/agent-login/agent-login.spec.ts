import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AgentLogin } from './agent-login';
import { AuthService } from '../../services/auth.service';
import { CaptchaService } from '../../services/captcha.service';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('AgentLogin', () => {
    let component: AgentLogin;
    let fixture: ComponentFixture<AgentLogin>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let captchaServiceSpy: jasmine.SpyObj<CaptchaService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['login', 'getRoleFromToken']);
        captchaServiceSpy = jasmine.createSpyObj('CaptchaService', ['generateCaptcha', 'validateCaptcha']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        captchaServiceSpy.generateCaptcha.and.returnValue('AGENT789');
        captchaServiceSpy.validateCaptcha.and.returnValue(true);

        await TestBed.configureTestingModule({
            imports: [AgentLogin, FormsModule, CommonModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: CaptchaService, useValue: captchaServiceSpy },
                { provide: Router, useValue: routerSpy },
                { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(AgentLogin);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should login and navigate to agent dashboard', () => {
        authServiceSpy.login.and.returnValue(of({ token: 'agent-token' }));
        authServiceSpy.getRoleFromToken.and.returnValue('Agent');

        component.email = 'agent@vims.com';
        component.password = 'AgentPass';
        component.login();

        expect(routerSpy.navigate).toHaveBeenCalledWith(['/agent-dashboard']);
    });
});
