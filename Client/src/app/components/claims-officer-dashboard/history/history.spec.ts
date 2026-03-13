import { ComponentFixture, TestBed } from '@angular/core/testing';
import { History } from './history';

describe('History', () => {
  let component: History;
  let fixture: ComponentFixture<History>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [History]
    })
    .compileComponents();

    fixture = TestBed.createComponent(History);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('reviewedClaims', []);
    fixture.componentRef.setInput('sortedReviewedClaims', []);
    fixture.componentRef.setInput('selectedClaim', null);
    fixture.componentRef.setInput('claimsSortOption', 'dateDesc');
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
