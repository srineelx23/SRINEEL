import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlansComponent } from './plans';

describe('PlansComponent', () => {
  let component: PlansComponent;
  let fixture: ComponentFixture<PlansComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlansComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PlansComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('orderedPlans', []);
    fixture.componentRef.setInput('isCreatingPlan', false);
    fixture.componentRef.setInput('planForm', {});
    fixture.componentRef.setInput('vehicleCategories', []);
    fixture.componentRef.setInput('policyCategories', []);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
