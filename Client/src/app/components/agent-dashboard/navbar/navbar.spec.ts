import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Navbar } from './navbar';

describe('Navbar', () => {
  let component: Navbar;
  let fixture: ComponentFixture<Navbar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Navbar]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Navbar);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('activeTab', 'overview');
    fixture.componentRef.setInput('agentName', 'Agent');
    fixture.componentRef.setInput('userRole', 'Agent');
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
