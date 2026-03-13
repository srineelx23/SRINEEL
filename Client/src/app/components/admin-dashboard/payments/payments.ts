import { Component, input, model, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-payments',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './payments.html',
  styleUrl: './payments.css',
})
export class PaymentsComponent {
  allTransactions = input.required<any[]>();
  totalRevenue = input.required<number>();
  totalPayoutAmount = input.required<number>();
  netProfit = input.required<number>();
  
  paymentsSortOption = model<string>('dateDesc');
  showPaymentsSortDropdown = model<boolean>(false);

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
