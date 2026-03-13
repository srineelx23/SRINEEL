import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Overview } from './overview';

describe('Overview', () => {
  let component: Overview;
  let fixture: ComponentFixture<Overview>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Overview]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Overview);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('agentName', 'Agent');
    fixture.componentRef.setInput('pendingApps', []);
    fixture.componentRef.setInput('pendingPaymentCount', 0);
    fixture.componentRef.setInput('reviewedApps', []);
    fixture.componentRef.setInput('customers', []);
    fixture.componentRef.setInput('sortedPendingApps', []);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
