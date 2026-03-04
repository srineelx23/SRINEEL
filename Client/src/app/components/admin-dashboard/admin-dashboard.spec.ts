import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminDashboard } from './admin-dashboard';
import { AdminService } from '../../services/admin.service';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

describe('AdminDashboard', () => {
    let component: AdminDashboard;
    let fixture: ComponentFixture<AdminDashboard>;
    let adminServiceSpy: jasmine.SpyObj<AdminService>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        adminServiceSpy = jasmine.createSpyObj('AdminService', [
            'getAllPolicyPlans', 'getAllPolicies', 'getAllPayments', 'getAllClaims', 'getAllUsers', 'getAuditLogs'
        ]);
        authServiceSpy = jasmine.createSpyObj('AuthService', ['logout']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        adminServiceSpy.getAllPolicyPlans.and.returnValue(of([]));
        adminServiceSpy.getAllPolicies.and.returnValue(of([]));
        adminServiceSpy.getAllPayments.and.returnValue(of([]));
        adminServiceSpy.getAllClaims.and.returnValue(of([]));
        adminServiceSpy.getAllUsers.and.returnValue(of([]));
        adminServiceSpy.getAuditLogs.and.returnValue(of([]));

        await TestBed.configureTestingModule({
            imports: [AdminDashboard, FormsModule, CommonModule],
            schemas: [NO_ERRORS_SCHEMA],
            providers: [
                { provide: AdminService, useValue: adminServiceSpy },
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(AdminDashboard);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should switch tabs', () => {
        component.switchTab('users');
        expect(component.activeTab()).toBe('users');
    });

    it('should load all data on init', () => {
        expect(adminServiceSpy.getAllPolicies).toHaveBeenCalled();
        expect(adminServiceSpy.getAllUsers).toHaveBeenCalled();
    });

    it('should navigate to home on logout', () => {
        component.goHome();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/']);
    });
});
