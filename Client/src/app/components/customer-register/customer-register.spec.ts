import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { CustomerRegister } from './customer-register';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('CustomerRegister', () => {
    let component: CustomerRegister;
    let fixture: ComponentFixture<CustomerRegister>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['registerCustomer']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        await TestBed.configureTestingModule({
            imports: [CustomerRegister, FormsModule, CommonModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(CustomerRegister);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should navigate to /error if registration form is incomplete', () => {
        component.firstName = '';
        component.register();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/error'], jasmine.any(Object));
    });

    it('should navigate to /error if passwords do not match', () => {
        component.firstName = 'John';
        component.lastName = 'Doe';
        component.email = 'john@test.com';
        component.password = '123';
        component.confirmPassword = '456';
        component.securityQuestion = 'Q';
        component.securityAnswer = 'A';
        component.register();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/error'], jasmine.objectContaining({
            state: jasmine.objectContaining({ message: 'Passwords do not match.' })
        }));
    });

    it('should register successfully and redirect after timeout', fakeAsync(() => {
        authServiceSpy.registerCustomer.and.returnValue(of('Success'));
        component.firstName = 'John';
        component.lastName = 'Doe';
        component.email = 'john@test.com';
        component.password = '123';
        component.confirmPassword = '123';
        component.securityQuestion = 'Q';
        component.securityAnswer = 'A';

        component.register();

        expect(component.successMessage()).toContain('Registration successful');
        tick(2500);
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
    }));
});
