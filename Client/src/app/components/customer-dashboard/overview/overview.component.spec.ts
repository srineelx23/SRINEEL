import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OverviewComponent } from './overview.component';
import { signal } from '@angular/core';

describe('OverviewComponent', () => {
  let component: OverviewComponent;
  let fixture: ComponentFixture<OverviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OverviewComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(OverviewComponent);
    component = fixture.componentInstance;

    // Set inputs (which are signals in this component)
    component.customerName = signal('Test Customer');
    component.policies = signal([]);
    component.activePolicies = signal([]);
    component.pendingApplicationsCount = signal(0);
    component.pendingPaymentPolicies = signal([]);
    component.pendingClaimsList = signal([]);
    component.approvedClaims = signal([]);
    component.myVehicles = signal([]);

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
