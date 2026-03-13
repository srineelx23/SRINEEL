import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClaimsComponent } from './claims.component';
import { signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

describe('ClaimsComponent', () => {
  let component: ClaimsComponent;
  let fixture: ComponentFixture<ClaimsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimsComponent, FormsModule]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ClaimsComponent);
    component = fixture.componentInstance;

    // Set inputs
    component.claims = signal([]);
    component.sortedClaims = signal([]);
    component.isFilingClaim = signal(false);
    component.claimForm = { PolicyId: null, ClaimType: 'Damage' };
    component.claimDoc1 = null;
    component.claimDoc2 = null;
    component.selectedClaim = signal(null);
    component.claimablePolicies = signal([]);
    component.availableClaimTypes = signal([]);
    component.claimsSortOption = signal('dateDesc');
    component.showClaimsSortDropdown = signal(false);

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
