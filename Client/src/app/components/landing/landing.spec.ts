import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Landing } from './landing';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';
import { of } from 'rxjs';

describe('Landing', () => {
    let component: Landing;
    let fixture: ComponentFixture<Landing>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn', 'logout']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        authServiceSpy.isLoggedIn.and.returnValue(false);

        await TestBed.configureTestingModule({
            imports: [Landing],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(Landing);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should navigate to /explore-plans', () => {
        const event = new Event('click');
        spyOn(event, 'preventDefault');
        component.explorePlans(event);
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/explore-plans']);
    });

    it('should call logout', () => {
        component.logout();
        expect(authServiceSpy.logout).toHaveBeenCalled();
        expect(component.isLoggedIn()).toBeFalse();
    });
});
