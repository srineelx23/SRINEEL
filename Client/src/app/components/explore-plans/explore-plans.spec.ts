import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ExplorePlans } from './explore-plans';
import { CustomerService } from '../../services/customer.service';
import { AuthService } from '../../services/auth.service';
import { Router, ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('ExplorePlans', () => {
    let component: ExplorePlans;
    let fixture: ComponentFixture<ExplorePlans>;
    let customerServiceSpy: jasmine.SpyObj<CustomerService>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        customerServiceSpy = jasmine.createSpyObj('CustomerService', ['getAllPolicyPlans', 'calculateQuote', 'createVehicleApplication']);
        authServiceSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn', 'getUserName', 'getRoleFromStoredToken', 'logout']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        customerServiceSpy.getAllPolicyPlans.and.returnValue(of([]));
        authServiceSpy.isLoggedIn.and.returnValue(false);

        await TestBed.configureTestingModule({
            imports: [ExplorePlans, FormsModule, CommonModule],
            providers: [
                { provide: CustomerService, useValue: customerServiceSpy },
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy },
                { provide: ActivatedRoute, useValue: { queryParamMap: of(convertToParamMap({})) } }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(ExplorePlans);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should redirect to login if not authenticated when getting quote', () => {
        authServiceSpy.isLoggedIn.and.returnValue(false);
        component.onGetQuote(1);
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/login'], jasmine.any(Object));
    });

    it('should navigate to /error if staff tries to buy policy', () => {
        authServiceSpy.isLoggedIn.and.returnValue(true);
        authServiceSpy.getRoleFromStoredToken.and.returnValue('Admin');
        component.onGetQuote(1);
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/error'], jasmine.objectContaining({
            state: jasmine.objectContaining({ title: 'Access Restricted' })
        }));
    });

    it('should calculate quote and update calculatedQuote signal', () => {
        authServiceSpy.isLoggedIn.and.returnValue(true);
        authServiceSpy.getRoleFromStoredToken.and.returnValue('Customer');
        const mockQuote = { premium: 5000 };
        customerServiceSpy.calculateQuote.and.returnValue(of(mockQuote));

        component.onGetQuote(1);
        component.quoteForm.InvoiceAmount = 100000 as any;
        component.calculateQuote();

        expect(component.calculatedQuote()).toEqual(mockQuote);
    });
});
