import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaymentsComponent } from './payments.component';
import { signal } from '@angular/core';

describe('PaymentsComponent', () => {
  let component: PaymentsComponent;
  let fixture: ComponentFixture<PaymentsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PaymentsComponent);
    component = fixture.componentInstance;

    // Set inputs
    component.totalPremiumPaid = signal(0);
    component.totalClaimPayouts = signal(0);
    component.premiumPayments = signal([]);
    component.claimPayments = signal([]);
    component.transferPayments = signal([]);
    component.paymentsSortOption = signal('dateDesc');
    component.showPaymentsSortDropdown = signal(false);

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
