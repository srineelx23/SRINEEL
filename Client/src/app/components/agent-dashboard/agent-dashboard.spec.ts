import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AgentDashboard } from './agent-dashboard';
import { AgentService } from '../../services/agent.service';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('AgentDashboard', () => {
    let component: AgentDashboard;
    let fixture: ComponentFixture<AgentDashboard>;
    let agentServiceSpy: jasmine.SpyObj<AgentService>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(async () => {
        sessionStorage.clear();
        agentServiceSpy = jasmine.createSpyObj('AgentService', ['getPendingApplications', 'getCustomers', 'getApplications']);
        authServiceSpy = jasmine.createSpyObj('AuthService', ['logout']);
        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        agentServiceSpy.getPendingApplications.and.returnValue(of([]));
        agentServiceSpy.getCustomers.and.returnValue(of([]));
        agentServiceSpy.getApplications.and.returnValue(of([]));

        await TestBed.configureTestingModule({
            imports: [AgentDashboard, FormsModule, CommonModule],
            providers: [
                { provide: AgentService, useValue: agentServiceSpy },
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(AgentDashboard);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should switch tabs', () => {
        component.switchTab('customers');
        expect(component.activeTab()).toBe('customers');
    });

    it('should load agent data on init', () => {
        expect(agentServiceSpy.getPendingApplications).toHaveBeenCalled();
        expect(agentServiceSpy.getCustomers).toHaveBeenCalled();
    });
});
