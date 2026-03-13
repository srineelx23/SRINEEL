import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Applications } from './applications';

describe('Applications', () => {
  let component: Applications;
  let fixture: ComponentFixture<Applications>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Applications]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Applications);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('pendingApps', []);
    fixture.componentRef.setInput('sortedPendingApps', []);
    fixture.componentRef.setInput('selectedApp', null);
    fixture.componentRef.setInput('appsSortOption', 'dateDesc');
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
