import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClaimsComponent } from './claims';

describe('ClaimsComponent', () => {
  let component: ClaimsComponent;
  let fixture: ComponentFixture<ClaimsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ClaimsComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('sortedClaims', []);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
