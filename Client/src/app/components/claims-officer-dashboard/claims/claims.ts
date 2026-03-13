import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-claims',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './claims.html',
  styleUrl: './claims.css'
})
export class Claims {
  @Input({ required: true }) pendingClaims!: any[];
  @Input({ required: true }) sortedPendingClaims!: any[];
  @Input({ required: true }) selectedClaim!: any;
  @Input({ required: true }) claimsSortOption!: string;
  @Input({ required: true }) payoutLoading!: boolean;
  @Input({ required: true }) decisionForm!: any;
  @Input({ required: true }) payoutBreakdown!: any;
  @Input({ required: true }) payoutWarning!: string | null;
  
  @Output() onOpenClaimReview = new EventEmitter<any>();
  @Output() onCloseClaimReview = new EventEmitter<void>();
  @Output() onClaimsSortOptionChange = new EventEmitter<string>();
  @Output() onUpdateBreakdown = new EventEmitter<void>();
  @Output() onSubmitDecision = new EventEmitter<void>();

  showSortDropdown = signal(false);

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

  updateBreakdown() {
    this.onUpdateBreakdown.emit();
  }

  submitDecision() {
    this.onSubmitDecision.emit();
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

  getEstimatedPayout(): number | null {
    return this.payoutBreakdown?.finalPayout ?? null;
  }

  getPayoutBreakdown(): any {
    return this.payoutBreakdown;
  }

  getPayoutWarning(): string | null {
    return this.payoutWarning;
  }
}
