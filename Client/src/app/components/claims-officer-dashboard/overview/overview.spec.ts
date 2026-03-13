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
    fixture.componentRef.setInput('officerName', 'Test');
    fixture.componentRef.setInput('pendingClaims', []);
    fixture.componentRef.setInput('reviewedClaims', []);
    fixture.componentRef.setInput('totalPending', 0);
    fixture.componentRef.setInput('totalReviewed', 0);
    fixture.componentRef.setInput('sortedPendingClaims', []);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
