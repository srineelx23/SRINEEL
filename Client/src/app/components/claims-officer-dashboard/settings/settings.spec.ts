import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Settings } from './settings';
import { FormsModule } from '@angular/forms';

describe('Settings', () => {
  let component: Settings;
  let fixture: ComponentFixture<Settings>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Settings, FormsModule]
    })
    .compileComponents();

    fixture = TestBed.createComponent(Settings);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('officerName', 'Test');
    fixture.componentRef.setInput('userRole', 'Officer');
    fixture.componentRef.setInput('changePwdLoading', false);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
