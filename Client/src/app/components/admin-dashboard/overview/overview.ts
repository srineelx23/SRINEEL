import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  templateUrl: './overview.html',
  styleUrl: './overview.css',
})
export class OverviewComponent {
  adminName = input.required<string>();
  
  // Financial Metrics
  grossPremium = input.required<number>();
  claimsPaid = input.required<number>();
  netReserve = input.required<number>();
  lossRatio = input.required<number>();

  // Operational Metrics
  activePoliciesCount = input.required<number>();
  pendingPremiumAmount = input.required<number>();
  pendingClaimsCount = input.required<number>();
  pendingApplicationsForApprovalCount = input.required<number>();
  totalClaimsCount = input.required<number>();
  totalCustomersCount = input.required<number>();

  // Data Tables
  recentPolicies = input<any[]>([]);

  // Charts
  premiumChartData = input.required<ChartData<'bar'>>();
  premiumChartOptions = input.required<ChartConfiguration['options']>();

  claimsPaidChartData = input.required<ChartData<'line'>>();
  claimsPaidChartOptions = input.required<ChartConfiguration['options']>();

  policyDistributionChartData = input.required<ChartData<'doughnut'>>();
  policyDistributionChartOptions = input.required<ChartConfiguration['options']>();

  claimsStatusChartData = input.required<ChartData<'doughnut'>>();
  claimsStatusChartOptions = input.required<ChartConfiguration['options']>();

  applicationApprovalChartData = input.required<ChartData<'doughnut'>>();
  applicationApprovalChartOptions = input.required<ChartConfiguration['options']>();

  vehicleTypeChartData = input.required<ChartData<'doughnut'>>();
  vehicleTypeChartOptions = input.required<ChartConfiguration['options']>();
  vehicleTypeBreakdown = input<{ label: string; count: number; share: number }[]>([]);
}
