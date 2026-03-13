import { ComponentFixture, TestBed } from '@angular/core/testing';
import { VehiclesComponent } from './vehicles';

describe('VehiclesComponent', () => {
  let component: VehiclesComponent;
  let fixture: ComponentFixture<VehiclesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VehiclesComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VehiclesComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('filteredVehicles', []);
    fixture.componentRef.setInput('vehiclePremiumsMap', new Map());
    fixture.componentRef.setInput('vehicleClaimsMap', new Map());
    fixture.componentRef.setInput('selectedVehicle', null);
    fixture.componentRef.setInput('vehiclePolicies', []);
    fixture.componentRef.setInput('vehicleTransactions', []);
    fixture.componentRef.setInput('vehicleClaims', []);
    fixture.componentRef.setInput('vehicleDocuments', []);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
