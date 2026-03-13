import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TransfersComponent } from './transfers.component';
import { signal } from '@angular/core';

describe('TransfersComponent', () => {
  let component: TransfersComponent;
  let fixture: ComponentFixture<TransfersComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TransfersComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TransfersComponent);
    component = fixture.componentInstance;

    // Set inputs
    component.incomingTransfers = signal([]);
    component.outgoingTransfers = signal([]);

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
