import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaymentsComponent } from './payments';

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

    // Set required inputs
    fixture.componentRef.setInput('allTransactions', []);
    fixture.componentRef.setInput('totalRevenue', 0);
    fixture.componentRef.setInput('totalPayoutAmount', 0);
    fixture.componentRef.setInput('netProfit', 0);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
