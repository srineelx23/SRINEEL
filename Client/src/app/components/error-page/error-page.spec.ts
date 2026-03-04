import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ErrorPage } from './error-page';
import { Router } from '@angular/router';

describe('ErrorPage', () => {
    let component: ErrorPage;
    let fixture: ComponentFixture<ErrorPage>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        routerSpy = jasmine.createSpyObj('Router', ['navigate', 'getCurrentNavigation']);

        // Mock getCurrentNavigation to return custom state
        routerSpy.getCurrentNavigation.and.returnValue({
            extras: {
                state: { status: 500, message: 'Server Error', title: 'Oops' }
            }
        } as any);

        await TestBed.configureTestingModule({
            imports: [ErrorPage],
            providers: [
                { provide: Router, useValue: routerSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(ErrorPage);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should load error data from navigation state', () => {
        expect(component.errorData.status).toBe(500);
        expect(component.errorData.message).toBe('Server Error');
    });

    it('should navigate home when goHome is called', () => {
        component.goHome();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/']);
    });
});
