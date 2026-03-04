import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ClaimsOfficerDashboard } from './claims-officer-dashboard';
import { ClaimsOfficerService } from '../../services/claims-officer.service';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('ClaimsOfficerDashboard', () => {
    let component: ClaimsOfficerDashboard;
    let fixture: ComponentFixture<ClaimsOfficerDashboard>;
    let claimsServiceSpy: jasmine.SpyObj<ClaimsOfficerService>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        claimsServiceSpy = jasmine.createSpyObj('ClaimsOfficerService', ['getMyAssignedClaims', 'decideClaim']);
        authServiceSpy = jasmine.createSpyObj('AuthService', ['logout']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        claimsServiceSpy.getMyAssignedClaims.and.returnValue(of([]));

        await TestBed.configureTestingModule({
            imports: [ClaimsOfficerDashboard, FormsModule, CommonModule],
            providers: [
                { provide: ClaimsOfficerService, useValue: claimsServiceSpy },
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(ClaimsOfficerDashboard);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should switch tabs', () => {
        component.switchTab('history');
        expect(component.activeTab()).toBe('history');
    });

    it('should load claims on init', () => {
        expect(claimsServiceSpy.getMyAssignedClaims).toHaveBeenCalled();
    });

    it('should validate decision form for Damage claims', () => {
        component.selectedClaim.set({ claimId: 1, claimType: 'Damage' });
        component.decisionForm.approved = true;
        component.decisionForm.repairCost = null;
        component.submitDecision();
        expect(component.errorMessage()).toBe('Repair cost is required for Damage claims.');
    });

    it('should submit decision successfully', fakeAsync(() => {
        claimsServiceSpy.decideClaim.and.returnValue(of({}));
        component.selectedClaim.set({ claimId: 1, claimType: 'Damage' });
        component.decisionForm.approved = true;
        component.decisionForm.repairCost = 5000;

        component.submitDecision();
        tick();

        expect(claimsServiceSpy.decideClaim).toHaveBeenCalled();
        expect(component.successMessage()).toContain('Approved');
    }));
});
