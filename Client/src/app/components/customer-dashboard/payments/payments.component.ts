import { Component, Input, Output, EventEmitter, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-payments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './payments.component.html',
  styleUrl: './payments.component.css',
})
export class PaymentsComponent {
  @Input() totalPremiumPaid!: Signal<number>;
  @Input() totalClaimPayouts!: Signal<number>;
  @Input() premiumPayments!: Signal<any[]>;
  @Input() claimPayments!: Signal<any[]>;
  @Input() transferPayments!: Signal<any[]>;
  @Input() paymentsSortOption!: Signal<string>;
  @Input() showPaymentsSortDropdown!: Signal<boolean>;

  @Output() onSwitchTab = new EventEmitter<string>();
  @Output() onDownloadInvoice = new EventEmitter<number>();
  @Output() onSetSortOption = new EventEmitter<string>();
  @Output() onToggleSortDropdown = new EventEmitter<boolean>();

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
