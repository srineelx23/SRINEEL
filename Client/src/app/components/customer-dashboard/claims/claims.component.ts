import { Component, Input, Output, EventEmitter, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-claims',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './claims.component.html',
  styleUrl: './claims.component.css',
})
export class ClaimsComponent {
  @Input() claims!: Signal<any[]>;
  @Input() sortedClaims!: Signal<any[]>;
  @Input() isFilingClaim!: Signal<boolean>;
  @Input() claimForm!: any;
  @Input() claimDoc1!: File | null;
  @Input() claimDoc2!: File | null;
  @Input() selectedClaim!: Signal<any>;
  @Input() claimablePolicies!: Signal<any[]>;
  @Input() availableClaimTypes!: Signal<any[]>;
  @Input() claimsSortOption!: Signal<string>;
  @Input() showClaimsSortDropdown!: Signal<boolean>;

  @Output() onViewClaimDetails = new EventEmitter<any>();
  @Output() onStartClaim = new EventEmitter<void>();
  @Output() onCancelClaim = new EventEmitter<void>();
  @Output() onSubmitClaim = new EventEmitter<void>();
  @Output() onClaimPolicyChange = new EventEmitter<void>();
  @Output() onClaimFileChange = new EventEmitter<{event: any, type: 'doc1' | 'doc2'}>();
  @Output() onCloseClaimDetails = new EventEmitter<void>();
  @Output() onSetSortOption = new EventEmitter<string>();
  @Output() onToggleSortDropdown = new EventEmitter<boolean>();

  getStatusString(status: any): string {
    const s = status?.toString();
    if (s === '0' || s === 'Submitted') return 'Submitted';
    if (s === '1' || s === 'Approved') return 'Approved';
    if (s === '2' || s === 'Rejected') return 'Rejected';
    return 'Under Review';
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
