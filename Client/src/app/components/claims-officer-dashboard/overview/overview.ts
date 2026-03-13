import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './overview.html',
  styleUrl: './overview.css'
})
export class Overview {
  officerName = input.required<string>();
  pendingClaims = input.required<any[]>();
  reviewedClaims = input.required<any[]>();
  totalPending = input.required<number>();
  totalReviewed = input.required<number>();
  sortedPendingClaims = input.required<any[]>();

  onTabSwitch = output<string>();
  onOpenClaimReview = output<any>();

  switchTab(tab: string) {
    this.onTabSwitch.emit(tab);
  }

  openClaimReview(claim: any) {
    this.onOpenClaimReview.emit(claim);
  }
}
