import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PoliciesComponent } from './policies.component';
import { signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

describe('PoliciesComponent', () => {
  let component: PoliciesComponent;
  let fixture: ComponentFixture<PoliciesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PoliciesComponent, FormsModule]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PoliciesComponent);
    component = fixture.componentInstance;

    // Set inputs
    component.policyFilter = signal('Active');
    component.activePolicies = signal([]);
    component.renewablePolicies = signal([]);
    component.pendingPolicies = signal([]);
    component.pendingPaymentPolicies = signal([]);
    component.inactivePolicies = signal([]);
    component.selectedPolicy = signal(null);
    component.renewingPolicyId = signal(null);
    component.renewForm = { NewPlanId: null, SelectedYears: 1 };
    component.filteredRenewPlans = signal([]);

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
