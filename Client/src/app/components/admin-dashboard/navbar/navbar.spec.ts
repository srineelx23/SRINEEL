import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavbarComponent } from './navbar';

describe('NavbarComponent', () => {
  let component: NavbarComponent;
  let fixture: ComponentFixture<NavbarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NavbarComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NavbarComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('adminName', 'Admin');
    fixture.componentRef.setInput('userRole', 'Admin');
    fixture.componentRef.setInput('activeTab', 'overview');
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
