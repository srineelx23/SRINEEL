import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UsersComponent } from './users';

describe('UsersComponent', () => {
  let component: UsersComponent;
  let fixture: ComponentFixture<UsersComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UsersComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UsersComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('filteredUsers', []);
    fixture.componentRef.setInput('rolesList', []);
    fixture.componentRef.setInput('isCreatingAgent', false);
    fixture.componentRef.setInput('isCreatingClaimsOfficer', false);
    fixture.componentRef.setInput('registerForm', {});
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
