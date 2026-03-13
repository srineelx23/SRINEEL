import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuditLogsComponent } from './audit-logs';
import { signal } from '@angular/core';

describe('AuditLogsComponent', () => {
  let component: AuditLogsComponent;
  let fixture: ComponentFixture<AuditLogsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuditLogsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AuditLogsComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('sortedAuditLogs', []);
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
