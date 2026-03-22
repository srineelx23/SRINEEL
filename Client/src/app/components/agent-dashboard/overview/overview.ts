import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './overview.html',
  styleUrl: './overview.css'
})
export class OverviewComponent {
  agentName = input.required<string>();
  pendingApps = input.required<any[]>();
  pendingPaymentCount = input.required<number>();
  reviewedApps = input.required<any[]>();
  customers = input.required<any[]>();
  sortedPendingApps = input.required<any[]>();

  onTabSwitch = output<string>();

  switchTab(tab: string) {
    this.onTabSwitch.emit(tab);
  }
}
