import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OverviewComponent } from './overview';

describe('OverviewComponent', () => {
  let component: OverviewComponent;
  let fixture: ComponentFixture<OverviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OverviewComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(OverviewComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('adminName', 'Admin');
    fixture.componentRef.setInput('totalRevenue', 0);
    fixture.componentRef.setInput('totalPayoutAmount', 0);
    fixture.componentRef.setInput('netProfit', 0);
    fixture.componentRef.setInput('totalActivePolicies', 0);
    fixture.componentRef.setInput('totalClaimsApproved', 0);
    fixture.componentRef.setInput('revenueChartData', { labels: [], datasets: [] });
    fixture.componentRef.setInput('revenueChartOptions', {});
    fixture.componentRef.setInput('claimsChartData', { labels: [], datasets: [] });
    fixture.componentRef.setInput('claimsChartOptions', {});
    fixture.componentRef.setInput('planPremiumsChartData', { labels: [], datasets: [] });
    fixture.componentRef.setInput('planAnalyticsOptions', {});
    fixture.componentRef.setInput('planClaimsChartData', { labels: [], datasets: [] });
    fixture.componentRef.setInput('planClaimsCountOptions', {});
    fixture.componentRef.setInput('vehicleTypePremiumsChartData', { labels: [], datasets: [] });
    fixture.componentRef.setInput('vehicleTypeClaimsChartData', { labels: [], datasets: [] });

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
