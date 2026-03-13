import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SettingsComponent } from './settings';

describe('SettingsComponent', () => {
  let component: SettingsComponent;
  let fixture: ComponentFixture<SettingsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SettingsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SettingsComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('adminName', 'Admin');
    fixture.componentRef.setInput('userRole', 'Admin');
    fixture.componentRef.setInput('changePasswordForm', {});
    fixture.componentRef.setInput('changePwdLoading', false);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
