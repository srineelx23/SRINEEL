import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Customers } from './customers';

describe('Customers', () => {
  let component: Customers;
  let fixture: ComponentFixture<Customers>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Customers]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Customers);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('customers', []);
    fixture.componentRef.setInput('sortedCustomers', []);
    fixture.componentRef.setInput('selectedCustomerRecord', null);
    fixture.componentRef.setInput('customersSortOption', 'nameAsc');
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
