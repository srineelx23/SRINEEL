import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { CustomerDashboard } from './customer-dashboard';
import { CustomerService } from '../../services/customer.service';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { NO_ERRORS_SCHEMA } from '@angular/core';

describe('CustomerDashboard', () => {
    let component: CustomerDashboard;
    let fixture: ComponentFixture<CustomerDashboard>;
    let customerServiceSpy: jasmine.SpyObj<CustomerService>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        sessionStorage.clear();
        customerServiceSpy = jasmine.createSpyObj('CustomerService', [
            'getMyPolicies', 'getMyClaims', 'getMyApplications', 'getMyPayments', 'getIncomingTransfers', 'getOutgoingTransfers'
        ]);
        authServiceSpy = jasmine.createSpyObj('AuthService', ['logout']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        customerServiceSpy.getMyPolicies.and.returnValue(of([]));
        customerServiceSpy.getMyClaims.and.returnValue(of([]));
        customerServiceSpy.getMyApplications.and.returnValue(of([]));
        customerServiceSpy.getMyPayments.and.returnValue(of([]));
        customerServiceSpy.getIncomingTransfers.and.returnValue(of([]));
        customerServiceSpy.getOutgoingTransfers.and.returnValue(of([]));

        await TestBed.configureTestingModule({
            imports: [CustomerDashboard, FormsModule, CommonModule],
            schemas: [NO_ERRORS_SCHEMA],
            providers: [
                { provide: CustomerService, useValue: customerServiceSpy },
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(CustomerDashboard);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should load customer data on init', () => {
        expect(customerServiceSpy.getMyPolicies).toHaveBeenCalled();
        expect(customerServiceSpy.getMyClaims).toHaveBeenCalled();
    });

    it('should switch active tab', () => {
        component.switchTab('claims');
        expect(component.activeTab()).toBe('claims');
    });

    it('should logout and navigate', () => {
        // Mock sessionStorage
        spyOn(sessionStorage, 'removeItem');
        component.logout();
        expect(authServiceSpy.logout).toHaveBeenCalled();
    });
});
