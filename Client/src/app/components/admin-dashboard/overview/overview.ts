import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  templateUrl: './overview.html',
  styleUrl: './overview.css',
})
export class OverviewComponent {
  adminName = input.required<string>();
  totalRevenue = input.required<number>();
  totalPayoutAmount = input.required<number>();
  netProfit = input.required<number>();
  totalActivePolicies = input.required<number>();
  totalClaimsApproved = input.required<number>();

  // Charts
  revenueChartData = input.required<ChartData<'bar'>>();
  revenueChartOptions = input.required<ChartConfiguration['options']>();
  revenueChartType = input<ChartType>('bar');

  claimsChartData = input.required<ChartData<'doughnut'>>();
  claimsChartOptions = input.required<ChartConfiguration['options']>();
  claimsChartType = input<ChartType>('doughnut');

  planPremiumsChartData = input.required<ChartData<'bar'>>();
  planAnalyticsOptions = input.required<ChartConfiguration['options']>();
  planBarChartType = input<ChartType>('bar');

  planClaimsChartData = input.required<ChartData<'bar'>>();
  planClaimsCountOptions = input.required<ChartConfiguration['options']>();

  vehicleTypePremiumsChartData = input.required<ChartData<'bar'>>();
  vehicleTypeClaimsChartData = input.required<ChartData<'bar'>>();
}
