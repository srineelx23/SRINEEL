import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HistoryComponent } from './history';

describe('HistoryComponent', () => {
  let component: HistoryComponent;
  let fixture: ComponentFixture<HistoryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HistoryComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(HistoryComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('reviewedApps', []);
    fixture.componentRef.setInput('sortedReviewedApps', []);
    fixture.componentRef.setInput('selectedApp', null);
    fixture.componentRef.setInput('appsSortOption', 'dateDesc');
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
