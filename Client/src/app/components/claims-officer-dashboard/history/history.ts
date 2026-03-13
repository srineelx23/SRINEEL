import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './history.html',
  styleUrl: './history.css'
})
export class History {
  reviewedClaims = input.required<any[]>();
  sortedReviewedClaims = input.required<any[]>();
  selectedClaim = input.required<any>();
  claimsSortOption = input.required<string>();
  showSortDropdown = signal(false);

  onOpenClaimReview = output<any>();
  onCloseClaimReview = output<void>();
  onClaimsSortOptionChange = output<string>();

  openClaimReview(claim: any) {
    this.onOpenClaimReview.emit(claim);
  }

  closeClaimReview() {
    this.onCloseClaimReview.emit();
  }

  setSortOption(option: string) {
    this.onClaimsSortOptionChange.emit(option);
    this.showSortDropdown.set(false);
  }

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Newest First';
      case 'dateAsc': return 'Oldest First';
      case 'amountDesc': return 'Amount: High to Low';
      case 'amountAsc': return 'Amount: Low to High';
      default: return 'Newest First';
    }
  }
}
