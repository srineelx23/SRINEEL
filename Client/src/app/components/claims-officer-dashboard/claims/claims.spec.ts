import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Claims } from './claims';
import { FormsModule } from '@angular/forms';

describe('Claims', () => {
  let component: Claims;
  let fixture: ComponentFixture<Claims>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Claims, FormsModule]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Claims);
    component = fixture.componentInstance;
    
    // Set required inputs
    component.pendingClaims = [];
    component.sortedPendingClaims = [];
    component.selectedClaim = null;
    component.claimsSortOption = 'dateDesc';
    component.payoutLoading = false;
    component.decisionForm = { approved: true };
    component.payoutBreakdown = {};
    component.payoutWarning = null;

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
