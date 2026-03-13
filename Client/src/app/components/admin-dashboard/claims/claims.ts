import { Component, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-claims',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './claims.html',
  styleUrl: './claims.css',
})
export class ClaimsComponent {
  sortedClaims = input.required<any[]>();
  claimsSortOption = model<string>('dateDesc');
  showClaimsSortDropdown = model<boolean>(false);

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

